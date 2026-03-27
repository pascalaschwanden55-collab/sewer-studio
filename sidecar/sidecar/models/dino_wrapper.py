"""Grounding DINO wrapper for open-vocabulary object detection."""

from __future__ import annotations

import base64
import io
import time
import logging
from pathlib import Path

import numpy as np
from PIL import Image

from ..config import settings
from ..gpu_manager import gpu_manager, ModelSlot
from ..schemas.detection import DinoDetection, DinoResponse

logger = logging.getLogger(__name__)

_DINO_CONFIG: str | None = None
_DINO_WEIGHTS: str | None = None


def _find_dino_files() -> tuple[str, str]:
    """Locate Grounding DINO config and weights in models_dir."""
    models = Path(settings.models_dir) / "grounding_dino_1.5"
    # Look for common naming patterns
    config_candidates = list(models.glob("*config*.py")) + list(models.glob("*cfg*.py"))
    weight_candidates = list(models.glob("*.pth")) + list(models.glob("*.pt"))

    if not config_candidates or not weight_candidates:
        raise FileNotFoundError(
            f"Grounding DINO config/weights not found in {models}. "
            "Please place GroundingDINO config (.py) and weights (.pth) there."
        )
    return str(config_candidates[0]), str(weight_candidates[0])


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

    raw = base64.b64decode(image_base64)
    img = Image.open(io.BytesIO(raw)).convert("RGB")
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

        boxes, logits, phrases = predict(
            model=model,
            image=img_tensor,
            caption=prompt,
            box_threshold=box_threshold,
            text_threshold=text_threshold,
        )
    except Exception as exc:
        logger.error("DINO inference failed: %s", exc)
        return DinoResponse(
            detections=[],
            inference_time_ms=round((time.perf_counter() - t0) * 1000, 1),
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
