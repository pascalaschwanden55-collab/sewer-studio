"""Health check endpoint."""

import asyncio
import os
from pathlib import Path

from fastapi import APIRouter
from ..config import settings
from ..gpu_manager import gpu_manager
from ..models import detector_qualification, yolo_wrapper

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
    models_present = {
        "dino": (
            _weights_present("grounding_dino_swinb", ("*.pth", "*.pt"))
            or _weights_present("grounding_dino_1.5", ("*.pth", "*.pt"))
        ),
        "sam": _weights_present("sam2.1", ("*.pth", "*.pt")),
    }
    classifier = yolo_wrapper.get_classifier_status()
    # Die Qualifikationspruefung bildet den SHA-256-Hash der aktiven Gewichte.
    # Bei grossen Modelldateien darf das den FastAPI-Ereignisfaden nicht blockieren.
    detector = await asyncio.to_thread(
        detector_qualification.evaluate_active_detector
    )
    # "ok" nur, wenn wirklich alles bereit ist. Fehlende DINO/SAM-Gewichte bleiben der
    # bekannte Fehlerfall; ein nicht geladener Klassifikator degradiert jetzt ebenfalls
    # den Status (Warnung, kein harter Blocker — Analyse laeuft ohne VSA-cls-Codes).
    # Der Grund steht maschinenlesbar in status_detail (additiv, Vertrag unveraendert).
    missing_weights = [name for name, present in models_present.items() if not present]
    if missing_weights:
        status = "degraded"
        status_detail = "missing_weights:" + ",".join(missing_weights)
    elif not detector["qualified"]:
        status = "degraded"
        status_detail = "detector_unqualified:" + detector["status"]
    elif not classifier.get("loaded"):
        status = "degraded"
        status_detail = "classifier_not_loaded"
    else:
        status = "ok"
        status_detail = "all_models_ready"
    return {
        "status": status,
        "status_detail": status_detail,
        "version": VERSION,
        "process_id": os.getpid(),
        "gpu": gpu_status,
        "yolo": yolo_wrapper.get_runtime_status(),
        "classifier": classifier,
        "models_present": models_present,
        "detector_qualification": detector,
        "device_config": {
            "gpu_device": settings.gpu_device,
            "yolo_device": settings.effective_yolo_device,
            "dino_device": settings.effective_dino_device,
            "sam_device": settings.effective_sam_device,
        },
    }
