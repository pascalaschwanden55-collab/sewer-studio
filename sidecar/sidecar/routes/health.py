"""Health check endpoint."""

from fastapi import APIRouter
from ..config import settings
from ..gpu_manager import gpu_manager
from ..models import yolo_wrapper

router = APIRouter()

VERSION = "1.1.0"


@router.get("/health")
async def health():
    return {
        "status": "ok",
        "version": VERSION,
        "gpu": gpu_manager.get_status(),
        "yolo": yolo_wrapper.get_runtime_status(),
        "device_config": {
            "gpu_device": settings.gpu_device,
            "yolo_device": settings.effective_yolo_device,
            "dino_device": settings.effective_dino_device,
            "sam_device": settings.effective_sam_device,
        },
    }
