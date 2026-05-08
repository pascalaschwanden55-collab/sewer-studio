"""FastAPI application – Sewer-Studio Vision Sidecar.

Always-On Pipeline: YOLO + DINO + SAM werden beim Start permanent geladen.
Kein Swap-Overhead — jeder Frame bekommt die volle CV-Pipeline.
"""

import asyncio
import logging
import os
import time
from contextlib import asynccontextmanager

from fastapi import FastAPI
from starlette.requests import Request
from starlette.responses import JSONResponse

# ── DLL-Suchpfad fuer NVDEC (PyNvVideoCodec braucht cudart64_12.dll aus PyTorch) ──
# Muss VOR allen torch/CUDA-Imports passieren.
try:
    import torch

    torch_lib = os.path.join(os.path.dirname(torch.__file__), "lib")
    if os.path.isdir(torch_lib) and hasattr(os, "add_dll_directory"):
        os.add_dll_directory(torch_lib)
except ImportError:
    pass

from .config import settings
from .gpu_manager import gpu_manager, ModelSlot
from .routes import (
    health,
    yolo,
    dino,
    sam,
    training,
    lora_training,
    pipe_axis,
    parse,
    changenet,
    dinov2,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)

logger = logging.getLogger("sidecar")


def _prewarm_dino() -> None:
    """Laedt Grounding DINO beim Start in den VRAM (persistent).
    Florence-2 wird als Shadow-Modell lazy beim ersten Request geladen."""
    try:
        from .models.dino_wrapper import _load_dino_on

        device = settings.effective_dino_device
        gpu_manager.ensure_loaded(ModelSlot.DINO, device, lambda: _load_dino_on(device))
        logger.info(
            "DINO pre-warmed on %s (Florence-2 Shadow wird lazy geladen)", device
        )
    except Exception as e:
        logger.warning("DINO pre-warm fehlgeschlagen: %s — wird lazy geladen", e)


def _prewarm_sam() -> None:
    """Laedt SAM 2 beim Start in den VRAM (persistent)."""
    try:
        from .models.sam_wrapper import _load_sam2_on

        device = settings.effective_sam_device
        gpu_manager.ensure_loaded(ModelSlot.SAM, device, lambda: _load_sam2_on(device))
        logger.info("SAM 2 pre-warmed on %s", device)
    except Exception as e:
        logger.warning("SAM 2 pre-warm fehlgeschlagen: %s — wird lazy geladen", e)


def _prewarm_yolo() -> None:
    """Laedt YOLO beim Start in den VRAM (persistent).
    Inkl. TensorRT-Export falls noetig (dauert 2-5 Min beim ersten Mal)."""
    try:
        from .models.yolo_wrapper import _load_yolo_on

        device = settings.effective_yolo_device
        gpu_manager.ensure_loaded(ModelSlot.YOLO, device, lambda: _load_yolo_on(device))
        logger.info("YOLO pre-warmed on %s", device)
    except Exception as e:
        logger.warning("YOLO pre-warm fehlgeschlagen: %s — wird lazy geladen", e)


_VRAM_MONITOR_INTERVAL_SEC = int(
    os.environ.get("SEWER_SIDECAR_VRAM_MONITOR_INTERVAL", "30")
)

# Globales Flag: True wenn VRAM kritisch belegt ist (>90%)
vram_critical: bool = False


async def _vram_monitor_loop() -> None:
    """Periodischer VRAM-Watermark-Check (alle 30s, konfigurierbar).

    Loggt Warnungen bei Ueberschreitung der Schwellen (75% warning, 90% critical).
    Setzt vram_critical Flag fuer Request-Ablehnung bei Spitzenlast.
    Laeuft als Background-Task waehrend der gesamten Sidecar-Laufzeit.
    """
    global vram_critical
    while True:
        try:
            await asyncio.sleep(_VRAM_MONITOR_INTERVAL_SEC)
            status = gpu_manager.check_vram_health()
            vram_critical = status == "critical"
            if status != "ok":
                pct = gpu_manager.get_vram_utilization_percent()
                logger.warning("VRAM-Monitor: Status=%s (%.1f%% belegt)", status, pct)
        except asyncio.CancelledError:
            break
        except Exception as e:
            logger.exception("VRAM-Monitor Fehler: %s", e)


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info(
        "Sidecar starting on %s:%d  (models: %s)\n"
        "  Device routing: YOLO=%s  DINO=%s  SAM=%s",
        settings.host,
        settings.port,
        settings.models_dir,
        settings.effective_yolo_device,
        settings.effective_dino_device,
        settings.effective_sam_device,
    )

    # ── CPU-Threading konfigurieren ──
    try:
        import torch

        torch.set_num_threads(settings.cpu_threads)
        torch.set_num_interop_threads(settings.cpu_threads)
        logger.info("Torch CPU threads konfiguriert: %d", settings.cpu_threads)
    except Exception as exc:
        logger.warning("Torch CPU-Thread-Konfiguration fehlgeschlagen: %s", exc)

    # ── Always-On Pre-Warm: YOLO + SAM sofort laden ──
    # V4.2 Phase 3.4: DINO 1.5 nicht mehr im Pre-Warm (tot bei Kanalbildern, spart 1.5 GB VRAM).
    # Opt-in via SEWER_SIDECAR_PREWARM_DINO=1 fuer Nachtbatch-Ueberraschungs-Pass.
    t0 = time.perf_counter()
    _prewarm_yolo()
    if os.environ.get("SEWER_SIDECAR_PREWARM_DINO", "0") == "1":
        _prewarm_dino()
        logger.info("DINO 1.5 Pre-Warm aktiviert (SEWER_SIDECAR_PREWARM_DINO=1)")
    else:
        logger.info(
            "DINO 1.5 im Lazy-Mode (V4.2 Default) — wird bei erstem Request geladen"
        )
    _prewarm_sam()
    elapsed = time.perf_counter() - t0
    logger.info("Pre-warm abgeschlossen in %.1fs (YOLO+SAM, DINO lazy)", elapsed)

    # ── VRAM-Watermark Monitor starten ──
    monitor_task = asyncio.create_task(_vram_monitor_loop())
    logger.info(
        "VRAM-Monitor gestartet (Intervall: %ds, Warn: %.0f%%, Kritisch: %.0f%%)",
        _VRAM_MONITOR_INTERVAL_SEC,
        gpu_manager.VRAM_WARN_PERCENT,
        gpu_manager.VRAM_ERROR_PERCENT,
    )

    yield

    monitor_task.cancel()
    try:
        await monitor_task
    except asyncio.CancelledError:
        pass
    logger.info("Sidecar shutting down — unloading all models ...")
    gpu_manager.unload_all()


app = FastAPI(
    title="Sewer-Studio Vision Sidecar",
    version="2.0.0",
    description="Always-On Multi-Model Vision Pipeline (YOLO / Florence-2 / SAM 2 / VSR)",
    lifespan=lifespan,
)


@app.middleware("http")
async def vram_guard_middleware(request: Request, call_next):
    """Lehnt Inference-Requests ab wenn VRAM kritisch belegt ist (>90%).
    Health-Endpoint bleibt immer erreichbar."""
    if vram_critical and not request.url.path.startswith("/health"):
        return JSONResponse(
            status_code=503,
            content={
                "error": "VRAM kritisch belegt (>90%) — Request abgelehnt",
                "vram_utilization_percent": gpu_manager.get_vram_utilization_percent(),
            },
        )
    return await call_next(request)


# ── Auth-Middleware (Audit 2026-04-25, SEC-H5; erweitert 2026-04-25 abends) ──
# Lokales Bearer-Token schuetzt administrative Endpunkte vor:
#   - Browser-CSRF/SSRF auf localhost:8100
#   - Anderen lokalen Prozessen (Malware) die den Sidecar ansprechen
#   - Insbesondere: /admin/reload_model laed beliebige .pt-Dateien =
#     PyTorch-Pickle-Deserialisierung = beliebige Code-Ausfuehrung
#
# Token-Quellen (Prioritaet absteigend):
#   1. ENV SEWER_SIDECAR_AUTH=disabled  -> Auth komplett aus (Dev-Modus)
#   2. ENV SEWER_SIDECAR_TOKEN          -> direkt verwenden (App-Start)
#   3. Token-Datei                       -> %LOCALAPPDATA%/SewerStudio/.sidecar_token
#                                          (geteilte Quelle App + manueller Start)
#
# Wenn keine Quelle Token liefert, bleibt Auth deaktiviert (Dev/Test).


def _resolve_sidecar_token() -> str:
    auth_mode = os.environ.get("SEWER_SIDECAR_AUTH", "").strip().lower()
    if auth_mode == "disabled":
        logger.warning(
            "[Auth] SEWER_SIDECAR_AUTH=disabled — Auth komplett deaktiviert."
        )
        return ""

    env_token = os.environ.get("SEWER_SIDECAR_TOKEN", "").strip()
    if env_token:
        return env_token

    # Token-Datei: app + sidecar lesen aus derselben Quelle
    try:
        local_app = os.environ.get("LOCALAPPDATA", "")
        if local_app:
            token_file = os.path.join(local_app, "SewerStudio", ".sidecar_token")
            if os.path.isfile(token_file):
                with open(token_file, "r", encoding="utf-8") as f:
                    file_token = f.read().strip()
                if file_token:
                    logger.info("[Auth] Token aus %s geladen.", token_file)
                    return file_token
    except Exception as ex:
        logger.warning("[Auth] Token-Datei-Lesen fehlgeschlagen: %s", ex)

    return ""


SIDECAR_TOKEN = _resolve_sidecar_token()
_AUTH_MODE = os.environ.get("SEWER_SIDECAR_AUTH", "").strip().lower()

# Endpunkte, die ohne Token erreichbar bleiben (read-only Health/Status).
# /docs und /openapi.json sind nur erreichbar wenn Auth komplett disabled
# ist (Dev-Modus) - sonst koennten sie Implementierungs-Details leaken.
_PUBLIC_PATHS = ("/health",)
_DEV_PUBLIC_PATHS = ("/health", "/docs", "/openapi.json", "/redoc")

# Phase 4.1: Fail-Closed. Ohne Token UND ohne explizites SEWER_SIDECAR_AUTH=disabled
# verweigern wir den Start. Das verhindert dass der Sidecar versehentlich offen
# auf 8100 lauscht (lokale Browser/SSRF/andere Prozesse koennten /admin/reload_model
# treffen = PyTorch-Pickle = RCE).
if not SIDECAR_TOKEN and _AUTH_MODE != "disabled":
    raise RuntimeError(
        "[Auth] Kein Sidecar-Token und SEWER_SIDECAR_AUTH ungesetzt. "
        "Fail-closed (Phase 4.1): Setze SEWER_SIDECAR_TOKEN, lege "
        "%LOCALAPPDATA%/SewerStudio/.sidecar_token an, oder setze "
        "SEWER_SIDECAR_AUTH=disabled fuer Dev-Modus."
    )

if not SIDECAR_TOKEN:
    logger.warning(
        "[Auth] SEWER_SIDECAR_AUTH=disabled — Auth komplett aus (Dev-Modus). "
        "/docs, /openapi.json und /redoc bleiben in diesem Modus oeffentlich."
    )
else:
    logger.info(
        "[Auth] Bearer-Token aktiv (Header: X-Sidecar-Token, Laenge=%d). "
        "/docs ist NICHT oeffentlich erreichbar.",
        len(SIDECAR_TOKEN),
    )


@app.middleware("http")
async def token_auth_middleware(request: Request, call_next):
    """Pruefen X-Sidecar-Token fuer alle nicht-Health-Endpunkte."""
    path = request.url.path

    if not SIDECAR_TOKEN:
        # Dev-Modus: alles inkl. /docs offen.
        if any(path.startswith(p) for p in _DEV_PUBLIC_PATHS):
            return await call_next(request)
        return await call_next(request)

    # Auth aktiv: nur /health bleibt oeffentlich. /docs/openapi.json/redoc
    # erfordern Token, sonst leaken sie Schema-Details.
    if any(path.startswith(p) for p in _PUBLIC_PATHS):
        return await call_next(request)

    provided = request.headers.get("X-Sidecar-Token", "").strip()
    if provided != SIDECAR_TOKEN:
        return JSONResponse(
            status_code=401,
            content={"error": "Unauthorized: missing or invalid X-Sidecar-Token"},
        )
    return await call_next(request)


# Register routes
app.include_router(health.router, tags=["health"])
app.include_router(yolo.router, tags=["yolo"])
app.include_router(dino.router, tags=["dino"])
app.include_router(sam.router, tags=["sam"])
app.include_router(training.router, tags=["training"])
app.include_router(lora_training.router, tags=["lora-training"])
app.include_router(pipe_axis.router, tags=["pipe-axis"])
app.include_router(parse.router, tags=["parse"])
app.include_router(changenet.router, tags=["changenet"])
app.include_router(dinov2.router, tags=["dinov2"])

# Video + Enhance Endpoints (Phase 2/3)
try:
    from .routes import video, enhance

    app.include_router(video.router, tags=["video"])
    app.include_router(enhance.router, tags=["enhance"])
except ImportError:
    pass  # Optional: nicht alle Deployments haben video/enhance
