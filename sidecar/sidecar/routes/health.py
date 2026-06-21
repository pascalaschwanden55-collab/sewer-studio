"""Health check endpoint."""

from pathlib import Path

from fastapi import APIRouter
from ..config import settings
from ..gpu_manager import gpu_manager
from ..models import yolo_wrapper

router = APIRouter()

VERSION = "1.2.0"


def _weights_present(subdir: str, patterns: tuple[str, ...]) -> bool:
    """Leichter Praesenz-Check (kein Laden, kein Hashing) fuer /health."""
    folder = Path(settings.models_dir) / subdir
    if not folder.is_dir():
        return False
    return any(any(folder.glob(p)) for p in patterns)


@router.get("/health")
async def health():
    # Gesamtaudit P2: Health pro Modell statt nur Pauschal-Status — "Full mode" darf
    # nicht behaupten, was faktisch degradiert ist (fehlende Gewichte sichtbar machen).
    gpu_status = gpu_manager.get_status()
    return {
        "status": "ok",
        "version": VERSION,
        "gpu": gpu_status,
        "yolo": yolo_wrapper.get_runtime_status(),
        "classifier": yolo_wrapper.get_classifier_status(),
        "models_present": {
            "dino": (
                _weights_present("grounding_dino_swinb", ("*.pth", "*.pt"))
                or _weights_present("grounding_dino_1.5", ("*.pth", "*.pt"))
            ),
            "sam": _weights_present("sam2.1", ("*.pth", "*.pt")),
        },
        "device_config": {
            "gpu_device": settings.gpu_device,
            "yolo_device": settings.effective_yolo_device,
            "dino_device": settings.effective_dino_device,
            "sam_device": settings.effective_sam_device,
        },
    }
