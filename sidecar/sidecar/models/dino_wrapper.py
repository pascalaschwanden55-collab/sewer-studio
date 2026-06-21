"""Grounding DINO wrapper for open-vocabulary object detection."""

from __future__ import annotations

import threading
import time
import logging
from pathlib import Path

import numpy as np

from ..config import settings
from ..gpu_manager import gpu_manager, ModelSlot
from ..schemas.detection import DinoDetection, DinoResponse
from .image_decode import decode_image_safe

logger = logging.getLogger(__name__)

# Serialisiert DINO-Inferenz (Gesamtaudit P7) — analog SAM-/YOLO-Predict-Lock.
_dino_predict_lock = threading.Lock()

_DINO_CONFIG: str | None = None
_DINO_WEIGHTS: str | None = None


def _find_dino_files() -> tuple[str, str]:
    """Locate Grounding DINO config and weights in models_dir."""
    searched: list[Path] = []
    for models in _candidate_dino_dirs():
        searched.append(models)
        config_candidates = sorted(list(models.glob("*config*.py")) + list(models.glob("*cfg*.py")))
        weight_candidates = sorted(list(models.glob("*.pth")) + list(models.glob("*.pt")))

        if config_candidates and weight_candidates:
            return str(_prefer_dino_config(config_candidates)), str(_prefer_dino_weights(weight_candidates))

    searched_text = ", ".join(str(p) for p in searched)
    raise FileNotFoundError(
        f"Grounding DINO config/weights not found. Searched: {searched_text}. "
        "For the local upgrade, place GroundingDINO Swin-B config/weights under models/grounding_dino_swinb."
    )


def _candidate_dino_dirs() -> list[Path]:
    configured = settings.dino_model_dir.strip()
    if configured and configured.lower() != "auto":
        path = Path(configured)
        if not path.is_absolute():
            path = Path(settings.models_dir) / path
        return [path]

    root = Path(settings.models_dir)
    return [
        root / "grounding_dino_swinb",
        root / "grounding_dino_1.5",
        root / "grounding_dino",
        root / "groundingdino",
    ]


def _prefer_dino_config(candidates: list[Path]) -> Path:
    return sorted(
        candidates,
        key=lambda p: (0 if "swinb" in p.name.lower() else 1, p.name.lower()),
    )[0]


def _prefer_dino_weights(candidates: list[Path]) -> Path:
    return sorted(
        candidates,
        key=lambda p: (0 if "swinb" in p.name.lower() else 1, p.name.lower()),
    )[0]


def _resolve_device() -> str:
    """Determine the effective device for DINO inference."""
    device = settings.effective_dino_device
    if device.startswith("cuda") and not _cuda_available():
        return "cpu"
    return device


def _load_dino_on(device: str):
    """Load Grounding DINO model onto *device*. Returns (model, None)."""
    try:
        from groundingdino.util.inference import load_model
    except ImportError:
        raise ImportError(
            "groundingdino-py is not installed. "
            "Install with: pip install groundingdino-py"
        )

    config_path, weights_path = _find_dino_files()
    model = load_model(config_path, weights_path, device=device)
    return model, None


def _cuda_available() -> bool:
    try:
        import torch
        return torch.cuda.is_available()
    except Exception:
        return False


def detect(
    image_base64: str,
    text_prompt: str | None,
    box_threshold: float,
    text_threshold: float,
) -> DinoResponse:
    """Run Grounding DINO detection on a base64-encoded image."""
    device = _resolve_device()
    state = gpu_manager.ensure_loaded(ModelSlot.DINO, device, lambda: _load_dino_on(device))
    model = state.model

    prompt = text_prompt or settings.dino_labels

    img = decode_image_safe(
        image_base64,
        max_bytes=settings.inference_max_image_bytes,
        max_pixels=settings.max_image_pixels,
    )
    img_array = np.array(img)

    t0 = time.perf_counter()

    try:
        from groundingdino.util.inference import predict
        import torch
        from torchvision import transforms

        transform = transforms.Compose([
            transforms.ToTensor(),
            transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
        ])
        img_tensor = transform(img)

        # Inferenz serialisieren (Gesamtaudit P7): parallele Threadpool-Requests auf
        # demselben DINO-Modell koennen sich verschraenken (Race/OOM) — wie SAM/YOLO.
        with _dino_predict_lock:
            boxes, logits, phrases = predict(
                model=model,
                image=img_tensor,
                caption=prompt,
                box_threshold=box_threshold,
                text_threshold=text_threshold,
            )
    except Exception as exc:
        # KEIN stilles "200 + leer": ein Inferenzfehler darf nicht wie "kein Befund"
        # aussehen. degraded=True + Fehlertext, voller Trace ins Log.
        logger.exception("DINO inference failed")
        return DinoResponse(
            detections=[],
            inference_time_ms=round((time.perf_counter() - t0) * 1000, 1),
            degraded=True,
            error=str(exc),
            error_code="dino_inference_failed",
        )

    elapsed_ms = (time.perf_counter() - t0) * 1000
    h, w = img_array.shape[:2]

    detections: list[DinoDetection] = []
    for box, logit, phrase in zip(boxes, logits, phrases):
        # boxes are cx,cy,w,h normalized -> convert to x1,y1,x2,y2 absolute
        cx, cy, bw, bh = box.tolist()
        x1 = (cx - bw / 2) * w
        y1 = (cy - bh / 2) * h
        x2 = (cx + bw / 2) * w
        y2 = (cy + bh / 2) * h
        detections.append(DinoDetection(
            x1=round(x1, 1),
            y1=round(y1, 1),
            x2=round(x2, 1),
            y2=round(y2, 1),
            label=phrase.strip(),
            confidence=round(float(logit), 4),
            phrase=phrase.strip(),
        ))

    return DinoResponse(
        detections=detections,
        inference_time_ms=round(elapsed_ms, 1),
    )
