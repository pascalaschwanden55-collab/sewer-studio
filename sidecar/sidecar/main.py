"""FastAPI application – Sewer-Studio Vision Sidecar.

Always-On Pipeline: YOLO + DINO + SAM werden beim Start permanent geladen.
Kein Swap-Overhead — jeder Frame bekommt die volle CV-Pipeline.
"""

import logging
import os
import sys
import time
from contextlib import asynccontextmanager

from fastapi import FastAPI

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
from .routes import health, yolo, dino, sam, training

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)

logger = logging.getLogger("sidecar")


def _prewarm_dino() -> None:
    """Laedt Grounding DINO beim Start in den VRAM (persistent)."""
    try:
        from .models.dino_wrapper import _load_dino_on
        device = settings.effective_dino_device
        gpu_manager.ensure_loaded(
            ModelSlot.DINO, device,
            lambda: _load_dino_on(device))
        logger.info("DINO pre-warmed on %s", device)
    except Exception as e:
        logger.warning("DINO pre-warm fehlgeschlagen: %s — wird lazy geladen", e)


def _prewarm_sam() -> None:
    """Laedt SAM beim Start in den VRAM (persistent)."""
    try:
        from .models.sam_wrapper import _load_sam_on
        device = settings.effective_sam_device
        gpu_manager.ensure_loaded(
            ModelSlot.SAM, device,
            lambda: _load_sam_on(device))
        logger.info("SAM pre-warmed on %s", device)
    except Exception as e:
        logger.warning("SAM pre-warm fehlgeschlagen: %s — wird lazy geladen", e)


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

    # ── Always-On Pre-Warm: DINO + SAM sofort laden ──
    # YOLO wird beim ersten Request via TensorRT geladen (Auto-Export dauert)
    t0 = time.perf_counter()
    _prewarm_dino()
    _prewarm_sam()
    elapsed = time.perf_counter() - t0
    logger.info("Pre-warm abgeschlossen in %.1fs", elapsed)

    yield

    logger.info("Sidecar shutting down — unloading all models ...")
    gpu_manager.unload_all()


app = FastAPI(
    title="Sewer-Studio Vision Sidecar",
    version="1.2.0",
    description="Always-On Multi-Model Vision Pipeline (YOLO / Grounding DINO / SAM / VSR)",
    lifespan=lifespan,
)

# Register routes
app.include_router(health.router, tags=["health"])
app.include_router(yolo.router, tags=["yolo"])
app.include_router(dino.router, tags=["dino"])
app.include_router(sam.router, tags=["sam"])
app.include_router(training.router, tags=["training"])

# Video + Enhance Endpoints (Phase 2/3)
try:
    from .routes import video, enhance
    app.include_router(video.router, tags=["video"])
    app.include_router(enhance.router, tags=["enhance"])
except ImportError:
    pass  # Optional: nicht alle Deployments haben video/enhance
