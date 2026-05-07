"""Health check endpoint."""

import asyncio
import logging
import signal

from fastapi import APIRouter

from ..config import settings
from ..gpu_manager import gpu_manager
from ..models import yolo_wrapper
from ..models.dino_wrapper import get_shadow_stats
from ..models.video_decoder import get_nvdec_status
from ..models.vsr_wrapper import get_vsr_status

router = APIRouter()
logger = logging.getLogger("sidecar.health")

VERSION = "2.0.0"


@router.get("/health")
async def health():
    return {
        "status": "ok",
        "version": VERSION,
        "gpu": gpu_manager.get_status(),
        "yolo": yolo_wrapper.get_runtime_status(),
        "nvdec": get_nvdec_status(),
        "vsr": get_vsr_status(),
        "florence2_shadow": get_shadow_stats(),
        "device_config": {
            "gpu_device": settings.gpu_device,
            "yolo_device": settings.effective_yolo_device,
            "dino_device": settings.effective_dino_device,
            "sam_device": settings.effective_sam_device,
        },
    }


@router.post("/shutdown")
async def shutdown():
    """Graceful shutdown — Audit STAB-H4 (2026-04-23).

    Wird vom C#-Host (PythonSidecarService) vor dem Hard-Kill aufgerufen.
    Loest Uvicorns SIGINT-Handler aus, der die FastAPI-Lifespan-Cleanup
    (GPU-Modelle entladen, etc.) sauber durchlaeuft.

    Antwortet sofort, das Signal feuert nach 300 ms Delay damit die HTTP-
    Response noch zurueck zum Client kann.
    """
    logger.info("[Shutdown] Graceful shutdown via HTTP angefordert")

    def _raise_sigint():
        try:
            signal.raise_signal(signal.SIGINT)
        except Exception as exc:  # pragma: no cover
            logger.warning("[Shutdown] raise_signal fehlgeschlagen: %s", exc)

    asyncio.get_event_loop().call_later(0.3, _raise_sigint)
    return {"status": "shutdown_initiated", "delay_ms": 300}
