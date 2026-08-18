"""FastAPI application – Sewer-Studio Vision Sidecar."""

import hmac
import logging
import os
import traceback
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.concurrency import run_in_threadpool
from fastapi.responses import JSONResponse

from .auth_token import resolve_or_create_token
from .config import settings
from .cuda_errors import looks_like_cuda_failure as _looks_like_cuda_failure
from .cuda_errors import looks_like_oom as _looks_like_oom
from .gpu_manager import gpu_manager, InsufficientVramError, ModelUnloadedError
from .routes import health, yolo, dino, sam, training, warmup

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)

logger = logging.getLogger("sidecar")


@asynccontextmanager
async def lifespan(app: FastAPI):
    logging.getLogger("sidecar").info(
        "Sidecar starting on %s:%d  (models: %s)\n"
        "  Device routing: YOLO=%s  DINO=%s  SAM=%s",
        settings.host,
        settings.port,
        settings.models_dir,
        settings.effective_yolo_device,
        settings.effective_dino_device,
        settings.effective_sam_device,
    )
    # Auth-Token aufloesen (env -> Datei -> neu erzeugen) und scharf schalten. Ab jetzt ist
    # X-Sidecar-Token Pflicht. Die Token-Datei wird mit dem C#-Client geteilt.
    settings.auth_token = _resolve_or_create_token()
    logging.getLogger("sidecar").info(
        "Auth aktiv: X-Sidecar-Token erforderlich (Token-Datei: %s).", _token_file_path()
    )
    # Inferenz-Waechter (Paket 3/A): beendet den Prozess hart, wenn ein Predict-Lock
    # laenger als das Limit haengt (fester CUDA-Call); der C#-Neustartdienst startet neu.
    # Startet bewusst UNBEDINGT (Paket 2/A5): seit dem Lease-Konzept werden auch
    # CPU-Inferenzen ueberwacht — YOLO-CPU-Singleton und YOLO-cls laufen ueber die
    # logischen Slots yolo_cpu/yolo_cls, DINO/SAM erzeugen auch auf CPU echte Slots.
    gpu_manager.start_watchdog()
    yield
    gpu_manager.stop_watchdog()
    logging.getLogger("sidecar").info("Sidecar shutting down — unloading all models ...")
    gpu_manager.unload_all()


app = FastAPI(
    title="Sewer-Studio Vision Sidecar",
    version=health.VERSION,
    description="Multi-Model Vision Pipeline (YOLO / Grounding DINO / SAM)",
    lifespan=lifespan,
)


@app.exception_handler(InsufficientVramError)
async def handle_insufficient_vram(request: Request, exc: InsufficientVramError):
    """VRAM-Zulassung verweigert (Paket 3/B): kontrollierter 503 mit maschinenlesbarem
    Detail — OHNE dass ein Ladeversuch stattgefunden hat."""
    logger.warning(
        "VRAM-Zulassung verweigert fuer %s: %.1f GB frei < %.1f GB benoetigt.",
        exc.slot.value, exc.free_gb, exc.required_gb,
    )
    return JSONResponse(
        {
            "detail": "insufficient VRAM",
            "code": "insufficient_vram",
            "slot": exc.slot.value,
            "free_gb": round(exc.free_gb, 2),
            "required_gb": round(exc.required_gb, 2),
            # Paket 2: abgezogene Ollama-Reserve im Detail (additiv, abwaertskompatibel).
            "reserved_gb": round(exc.reserved_gb, 2),
        },
        status_code=503,
    )


@app.exception_handler(ModelUnloadedError)
async def handle_model_unloaded(request: Request, exc: ModelUnloadedError):
    """Unload-Race (Paket 3/B): Slot wurde zwischen ensure_loaded und Nutzung entladen.
    503 statt 500 — der C#-Client wiederholt den Request einmal und loest das Nachladen aus."""
    logger.warning("Unload-Race abgefangen bei %s %s: %s", request.method, request.url.path, exc)
    return JSONResponse(
        {"detail": "model slot was unloaded concurrently", "code": "model_unloaded"},
        status_code=503,
    )


@app.exception_handler(Exception)
async def handle_unexpected(request: Request, exc: Exception):
    """Zentraler Fallback: nie roher 500-Stacktrace nach aussen.

    - CUDA-OOM      -> VRAM freigeben, 503 (Aufrufer kann Frame ueberspringen/retryen)
    - CUDA sonst    -> 503 mit stabilem Fehlercode, ohne Treiberdetails nach aussen
    - Modell fehlt  -> 503 (Dienst voruebergehend nicht verfuegbar)
    - sonst         -> 500 mit generischer Meldung (Trace nur ins Log)
    """
    logger.error(
        "Unbehandelter Fehler bei %s %s: %s\n%s",
        request.method, request.url.path, exc, traceback.format_exc(),
    )

    if _looks_like_oom(exc):
        # Audit Fix #5: bei OOM zuerst den am laengsten ungenutzten Slot entladen (LRU), damit
        # der naechste Frame wieder VRAM hat — statt nur den Cache zu leeren. Reaktive
        # Durchsetzung des VRAM-Budgets ohne die bewusste "alle Modelle resident"-Strategie
        # im Normalbetrieb anzutasten.
        # Aufraeumarbeit (Lock, GB-Modell freigeben, empty_cache synchronisiert mit der GPU) in
        # den Threadpool verlagern, damit der Event-Loop und /health waehrend der OOM-Erholung
        # nicht blockieren (dieselbe Regel wie die bewusst sync GPU-Routen, routes/yolo.py).
        await run_in_threadpool(gpu_manager.evict_lru)
        await run_in_threadpool(gpu_manager.empty_cache)
        return JSONResponse({"detail": "GPU out of memory"}, status_code=503)

    if _looks_like_cuda_failure(exc):
        await run_in_threadpool(gpu_manager.empty_cache)
        return JSONResponse(
            {
                "detail": "GPU/CUDA temporarily unavailable",
                "code": "cuda_unavailable",
            },
            status_code=503,
        )

    if isinstance(exc, FileNotFoundError):
        return JSONResponse({"detail": "model unavailable"}, status_code=503)

    return JSONResponse({"detail": "internal error"}, status_code=500)


def _normalize_host(host_header: str | None) -> str:
    if not host_header:
        return ""

    host = host_header.strip().lower()
    if host.startswith("["):
        end = host.find("]")
        return host[1:end] if end > 0 else host

    if ":" in host:
        host = host.split(":", 1)[0]

    return host


def _trusted_hosts() -> set[str]:
    """Hosts fuer den DNS-Rebinding-Schutz, nicht fuer die Anmeldung.

    Der Host-Header kommt vom Client und ist deshalb keine verlaessliche Identitaet.
    Den Zugriff schuetzt ausschliesslich das verpflichtende Sidecar-Token.
    """
    raw = settings.trusted_hosts or ""
    return {
        item.strip().lower()
        for item in raw.split(",")
        if item.strip()
    }


def _token_file_path() -> Path:
    """Geteilte Token-Datei. Default = %LOCALAPPDATA%/SewerStudio/.sidecar_token
    (exakt der Pfad, den der C#-Client liest)."""
    configured = (settings.auth_token_file or "").strip()
    if configured:
        return Path(configured)
    base = os.environ.get("LOCALAPPDATA") or str(Path.home() / "AppData" / "Local")
    return Path(base) / "SewerStudio" / ".sidecar_token"


def _resolve_or_create_token() -> str:
    """Effektives Auth-Token: env (SEWER_SIDECAR_AUTH_TOKEN) -> Token-Datei -> neu erzeugen
    und schreiben. Eine vorhandene Datei wird wiederverwendet (kein Aussperren des Clients)."""
    return resolve_or_create_token(settings.auth_token, _token_file_path(), logger)


def _auth_token() -> str:
    return (settings.auth_token or "").strip()


@app.exception_handler(RequestValidationError)
async def handle_validation_error(request: Request, exc: RequestValidationError):
    """Validierungsfehler OHNE die Eingabe zurueckspiegeln.

    Der Standardhandler von FastAPI nimmt bei jedem Fehler das ungueltige
    Eingabeobjekt als ``input`` in die Antwort auf. Verfehlt ein CONTAINER-Feld
    seinen Typ - etwa ``samples`` als Objekt statt als Liste, oder ein
    unbekanntes Feld auf oberster Ebene -, dann ist dieses Objekt der gesamte
    Anfragekoerper samt Base64-Bildern. Gemessen am 2026-08-18: ein Koerper von
    200 KB erzeugte eine Antwort von 101 KB mit dem Base64-Inhalt darin.

    Bei den meisten Fehlerformen (falscher Literalwert, unbekanntes Feld in
    einem Sample) blieb die Antwort dagegen bei rund 1,1 KB - die Spiegelung war
    also nie das Normalverhalten, aber sie war moeglich.

    Geliefert werden nur Ort, stabiler Code und eine kurze Meldung. Der
    Aufrufer weiss damit, WO es klemmt, bekommt aber nie seine eigenen
    Nutzdaten zurueck (Gesamtaudit 2026-08-18, R-01).
    """
    fehler = [
        {
            "loc": [str(teil) for teil in (einzeln.get("loc") or ())],
            "type": str(einzeln.get("type") or "validation_error"),
        }
        for einzeln in exc.errors()[:20]
    ]
    return JSONResponse(
        {
            "detail": "Request validation failed.",
            "code": "validation_error",
            "errors": fehler,
        },
        status_code=422,
    )


@app.middleware("http")
async def enforce_request_size_limit(request: Request, call_next):
    """Groessengrenze VOR JSON und Pydantic.

    Bis hierher wurde erst in der Route geprueft - also nachdem der ganze
    Koerper als Zeichenkette und Objektbaum im Speicher stand.
    """
    grenze = int(getattr(settings, "max_request_bytes", 0) or 0)
    if grenze > 0:
        angegeben = request.headers.get("content-length")
        transfer_encoding = request.headers.get("transfer-encoding")
        braucht_laenge = request.method.upper() in {"POST", "PUT", "PATCH"}
        if braucht_laenge and (angegeben is None or transfer_encoding):
            return JSONResponse(
                {
                    "detail": "Content-Length required.",
                    "code": "content_length_required",
                },
                status_code=411,
            )
        if angegeben is not None:
            try:
                if int(angegeben) > grenze:
                    return JSONResponse(
                        {
                            "detail": "Request body too large.",
                            "code": "request_too_large",
                            "limit_bytes": grenze,
                        },
                        status_code=413,
                    )
            except ValueError:
                return JSONResponse(
                    {"detail": "Invalid Content-Length.", "code": "invalid_content_length"},
                    status_code=400,
                )

    return await call_next(request)


@app.middleware("http")
async def enforce_loopback_security(request: Request, call_next):
    # Erste Schranke gegen Browser-DNS-Rebinding. Das ist bewusst keine Authentifizierung:
    # Auch ein erlaubter oder manipulierter Host muss danach das geheime Token vorweisen.
    trusted = _trusted_hosts()
    host = _normalize_host(request.headers.get("host"))
    if "*" not in trusted and host not in trusted:
        return JSONResponse(
            {"detail": "Untrusted host."},
            status_code=403,
        )

    token = _auth_token()
    if not token:
        logger.error("Sidecar-Anfrage abgewiesen: Auth-Token ist nicht initialisiert.")
        return JSONResponse(
            {
                "detail": "Sidecar authentication is not initialized.",
                "code": "auth_unavailable",
            },
            status_code=503,
        )

    provided = request.headers.get("X-Sidecar-Token") or ""
    # Konstante-Zeit-Vergleich gegen Timing-Angriffe; fehlender/falscher Token -> 401.
    # Byte-Vergleich: ein non-ASCII-Header (Starlette dekodiert Header als Latin-1) wuerde
    # hmac.compare_digest bei str mit TypeError zu einem 500 fuehren statt zu einem sauberen 401.
    if not hmac.compare_digest(provided.encode("latin-1", "replace"), token.encode("utf-8")):
        return JSONResponse(
            {"detail": "Invalid or missing sidecar token."},
            status_code=401,
        )

    return await call_next(request)

# Register routes
app.include_router(health.router, tags=["health"])
app.include_router(yolo.router, tags=["yolo"])
app.include_router(dino.router, tags=["dino"])
app.include_router(sam.router, tags=["sam"])
app.include_router(training.router, tags=["training"])
app.include_router(warmup.router, tags=["warmup"])
