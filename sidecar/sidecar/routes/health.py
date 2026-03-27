"""Health check endpoint."""

from fastapi import APIRouter
from ..config import settings
from ..gpu_manager import gpu_manager
from ..models import yolo_wrapper
from ..models.video_decoder import get_nvdec_status
from ..models.vsr_wrapper import get_vsr_status

router = APIRouter()

VERSION = "1.2.0"


@router.get("/health")
async def health():
    return {
        "status": "ok",
        "version": VERSION,
        "gpu": gpu_manager.get_status(),
        "yolo": yolo_wrapper.get_runtime_status(),
        "nvdec": get_nvdec_status(),
        "vsr": get_vsr_status(),
        "device_config": {
            "gpu_device": settings.gpu_device,
            "yolo_device": settings.effective_yolo_device,
            "dino_device": settings.effective_dino_device,
            "sam_device": settings.effective_sam_device,
        },
    }
