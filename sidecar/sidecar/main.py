"""FastAPI application – Sewer-Studio Vision Sidecar."""

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI

from .config import settings
from .gpu_manager import gpu_manager
from .routes import health, yolo, dino, sam, training

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)


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
    yield
    logging.getLogger("sidecar").info("Sidecar shutting down — unloading all models ...")
    gpu_manager.unload_all()


app = FastAPI(
    title="Sewer-Studio Vision Sidecar",
    version="1.1.0",
    description="Multi-Model Vision Pipeline (YOLO / Grounding DINO / SAM)",
    lifespan=lifespan,
)

# Register routes
app.include_router(health.router, tags=["health"])
app.include_router(yolo.router, tags=["yolo"])
app.include_router(dino.router, tags=["dino"])
app.include_router(sam.router, tags=["sam"])
app.include_router(training.router, tags=["training"])
