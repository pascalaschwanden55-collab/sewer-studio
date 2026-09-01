"""Warmup-Endpoint: laedt YOLO/DINO/SAM vorab in den VRAM.

Damit die erste echte Analyse keinen einmaligen Lade-Verzug hat. Wird vom C#-Start
("KI starten" / Autostart) aufgerufen, nachdem der Sidecar erreichbar ist. Jedes Modell
ist best-effort: ein Fehler bei einem Modell verhindert die anderen nicht.
"""

import base64
import io
import logging
import time

from fastapi import APIRouter

from ..config import settings
from ..gpu_manager import gpu_manager, ModelSlot
from ..models import detector_qualification, yolo_wrapper, dino_wrapper, sam_wrapper

router = APIRouter()
logger = logging.getLogger("sidecar")


def _dummy_image_b64() -> str:
    """Kleines neutrales Bild (64x64) – reicht, um die Modelle ueber den Normalpfad zu laden."""
    from PIL import Image

    img = Image.new("RGB", (64, 64), (32, 32, 32))
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("ascii")


def _warm_one(name: str, fn) -> str:
    t0 = time.perf_counter()
    try:
        fn()
        ms = (time.perf_counter() - t0) * 1000
        logger.info("Warmup %s ok (%.0f ms)", name, ms)
        return "ok"
    except Exception as exc:  # best-effort: ein Modell darf scheitern, ohne die anderen zu blockieren
        logger.warning("Warmup %s fehlgeschlagen: %s: %s", name, type(exc).__name__, exc)
        return f"fehler: {type(exc).__name__}"


@router.post("/warmup")
@router.get("/warmup")
def warmup() -> dict:
    """Laedt alle Vision-Modelle (YOLO/DINO/SAM) in den Speicher. Idempotent."""
    started = time.perf_counter()
    dummy = _dummy_image_b64()

    results: dict[str, str] = {}
    details: dict[str, dict] = {}

    # Das Standard-YOLO nur laden, wenn genau das aktive Artefakt qualifiziert ist.
    # Fehlender/kaputter Marker bleibt dadurch auch beim Warmup fail-closed.
    detector = detector_qualification.evaluate_active_detector()
    if detector.get("qualified") is True:
        results["yolo"] = _warm_one(
            "YOLO", lambda: yolo_wrapper.detect(dummy, 0.25)
        )
        details["yolo"] = {
            "status": "loaded" if results["yolo"] == "ok" else "error",
            "reason_code": None,
            "qualification_status": detector.get("status"),
            "reason": None,
        }
    else:
        qualification_status = detector.get("status") or "qualification_unknown"
        reason = detector.get("reason") or "Detektor ist nicht qualifiziert."
        results["yolo"] = "uebersprungen"
        details["yolo"] = {
            "status": "skipped",
            "reason_code": "detector_not_qualified",
            "qualification_status": qualification_status,
            "reason": reason,
        }
        logger.warning(
            "Warmup YOLO uebersprungen: %s (%s)",
            reason,
            qualification_status,
        )

    # Whole-Frame-Klassifikator separat laden; er haengt nicht am YOLO-Detection-Slot.
    def _load_classifier():
        yolo_wrapper.classify(dummy, top_k=1)
        if not yolo_wrapper.get_classifier_status().get("loaded"):
            raise RuntimeError("kein YOLO-cls Modell konfiguriert")

    results["classifier"] = _warm_one("YOLO-cls", _load_classifier)

    # DINO ueber den echten Pfad (None-Prompt -> Standard-Labels aus den Settings).
    # Der Wrapper meldet normale Inferenzfehler als degraded-Antwort, damit ein
    # Analyse-Request nicht abstuerzt. Beim Warmup ist das aber kein Erfolg.
    def _load_dino():
        response = dino_wrapper.detect(dummy, None, 0.30, 0.25)
        if getattr(response, "degraded", False):
            raise RuntimeError(getattr(response, "error", None) or "DINO ist degradiert")

    results["dino"] = _warm_one("DINO", _load_dino)

    # SAM direkt laden (braucht keine Box, nur das Modell resident machen).
    def _load_sam():
        dev = sam_wrapper._resolve_device()
        gpu_manager.ensure_loaded(ModelSlot.SAM, dev, lambda: sam_wrapper._load_sam_on(dev))

    results["sam"] = _warm_one("SAM", _load_sam)

    elapsed = time.perf_counter() - started
    loaded = [name for name, state in results.items() if state == "ok"]
    logger.info("Warmup fertig in %.1fs (geladen: %s)", elapsed, ", ".join(loaded) or "keine")

    return {
        "warmup": results,
        "warmup_details": details,
        "loaded": loaded,
        "elapsed_sec": round(elapsed, 1),
        "status": gpu_manager.get_status(),
    }
