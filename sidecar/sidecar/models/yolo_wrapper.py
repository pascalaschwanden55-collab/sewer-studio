"""YOLO model wrapper for pre-screening and detection."""

from __future__ import annotations

import base64
import io
import time
import logging
import threading
from pathlib import Path

import numpy as np
from PIL import Image

from ..config import settings
from ..gpu_manager import gpu_manager, ModelSlot
from ..schemas.detection import YoloDetection, YoloResponse

logger = logging.getLogger(__name__)

# Flag: True when custom sewer-specific weights are loaded, False for COCO fallback.
_using_custom_weights = False
_resolved_model_path: str | None = None

# CPU-mode singleton (bypasses GpuModelManager when YOLO runs on CPU)
_cpu_model = None
_cpu_lock = threading.Lock()


def _resolve_yolo_model_path() -> tuple[str, bool]:
    # Plan: models/yolo26m/<weights>.pt
    yolo_dir = Path(settings.models_dir) / "yolo26m"
    model_path = yolo_dir / settings.yolo_model_name

    if not model_path.exists():
        # Try flat path: models/<yolo_model_name>
        model_path = Path(settings.models_dir) / settings.yolo_model_name

    if model_path.exists():
        return str(model_path), True

    if settings.require_custom_yolo:
        raise FileNotFoundError(
            "Custom YOLO weights are required but were not found. "
            f"Expected '{settings.yolo_model_name}' in '{yolo_dir}' or '{settings.models_dir}'."
        )

    return "yolo11m.pt", False


def get_runtime_status() -> dict:
    """Return current YOLO runtime/configuration information for diagnostics."""
    yolo_dir = Path(settings.models_dir) / "yolo26m"
    candidate_nested = yolo_dir / settings.yolo_model_name
    candidate_flat = Path(settings.models_dir) / settings.yolo_model_name
    custom_exists = candidate_nested.exists() or candidate_flat.exists()

    status = {
        "configured_model_name": settings.yolo_model_name,
        "require_custom_yolo": settings.require_custom_yolo,
        "custom_weights_present": custom_exists,
        "using_custom_weights": _using_custom_weights,
        "resolved_model_path": _resolved_model_path,
        "fallback_model_name": None if _using_custom_weights or settings.require_custom_yolo else "yolo11m.pt",
        "device": _resolve_device(),
    }

    if candidate_nested.exists():
        status["custom_model_path"] = str(candidate_nested)
    elif candidate_flat.exists():
        status["custom_model_path"] = str(candidate_flat)
    else:
        status["custom_model_path"] = str(candidate_nested)

    return status


def _resolve_device() -> str:
    """Determine the effective device for YOLO inference."""
    device = settings.effective_yolo_device
    if device.startswith("cuda") and not _cuda_available():
        logger.warning("YOLO configured for %s but CUDA unavailable, falling back to cpu", device)
        return "cpu"
    return device


def _load_yolo_on(device: str):
    """Load YOLO model onto *device*. Returns (model, None)."""
    global _using_custom_weights, _resolved_model_path
    from ultralytics import YOLO

    model_path, using_custom = _resolve_yolo_model_path()
    _using_custom_weights = using_custom
    _resolved_model_path = model_path

    if using_custom:
        logger.info("Loading custom YOLO weights from %s onto %s", model_path, device)
    else:
        logger.warning(
            "Custom YOLO weights not found – downloading yolo11m.pt as fallback. "
            "Using image-quality pre-screening instead of defect detection. "
            "Fine-tune and place custom weights in models/yolo26m/ for sewer-specific detection."
        )

    model = YOLO(str(model_path))
    model.to(device)
    return model, None


def _get_yolo_model():
    """Get the YOLO model, loading if necessary.

    CPU path: module-level singleton bypassing the GPU manager.
    GPU path: uses gpu_manager.ensure_loaded for persistent slot.
    """
    global _cpu_model
    device = _resolve_device()

    if device == "cpu":
        if _cpu_model is not None:
            return _cpu_model
        with _cpu_lock:
            if _cpu_model is not None:
                return _cpu_model
            model, _ = _load_yolo_on(device)
            _cpu_model = model
            return _cpu_model
    else:
        state = gpu_manager.ensure_loaded(
            ModelSlot.YOLO, device, lambda: _load_yolo_on(device)
        )
        return state.model


def _cuda_available() -> bool:
    try:
        import torch
        return torch.cuda.is_available()
    except Exception:
        return False


def decode_image(image_base64: str) -> Image.Image:
    """Decode a base64-encoded image to PIL Image."""
    raw = base64.b64decode(image_base64)
    return Image.open(io.BytesIO(raw)).convert("RGB")


def _is_frame_usable(img: Image.Image) -> tuple[bool, str]:
    """Check if a frame is usable for analysis using image quality heuristics.

    Filters out:
    - Completely black/dark frames (lens cap, no signal)
    - Completely white/overexposed frames
    - Very low variance frames (solid color, no texture)

    Returns (is_usable, reason).
    """
    arr = np.array(img, dtype=np.float32)

    # Convert to grayscale for analysis
    gray = arr.mean(axis=2)
    mean_brightness = gray.mean()
    std_brightness = gray.std()

    # Too dark (lens cap, black frame, no signal)
    if mean_brightness < 15:
        return False, "too_dark"

    # Too bright (overexposed, white frame)
    if mean_brightness > 245:
        return False, "too_bright"

    # Too uniform (solid color, no texture = likely no pipe content)
    if std_brightness < 8:
        return False, "too_uniform"

    # Check edge density using Laplacian-like filter for blur detection
    # A very blurry frame has low edge variance
    from scipy.ndimage import laplace
    edges = laplace(gray)
    edge_var = edges.var()

    if edge_var < 5:
        return False, "too_blurry"

    return True, "ok"


def detect(image_base64: str, confidence_threshold: float) -> YoloResponse:
    """Run YOLO detection on a base64-encoded image.

    Behavior depends on model type:
    - Custom sewer weights: True defect detection via YOLO.
    - COCO fallback (yolo11m): Image-quality pre-screening that filters out
      dark/blank/blurry frames. YOLO detections are still returned for info,
      but is_relevant is based on image quality, not COCO class detections.
    """
    model = _get_yolo_model()

    img = decode_image(image_base64)

    # Image-quality pre-screening (always run, fast)
    usable, quality_reason = _is_frame_usable(img)

    if not usable:
        # Frame is not usable at all – skip without running YOLO inference
        return YoloResponse(
            is_relevant=False,
            detections=[],
            frame_class=quality_reason,
            inference_time_ms=0.0,
        )

    t0 = time.perf_counter()
    results = model.predict(
        source=np.array(img),
        conf=confidence_threshold,
        verbose=False,
    )
    elapsed_ms = (time.perf_counter() - t0) * 1000

    detections: list[YoloDetection] = []
    frame_class = "empty"

    if results and len(results) > 0:
        result = results[0]
        boxes = result.boxes
        if boxes is not None and len(boxes) > 0:
            frame_class = "relevant"
            for box in boxes:
                xyxy = box.xyxy[0].cpu().numpy()
                cls_id = int(box.cls[0].cpu().item())
                conf = float(box.conf[0].cpu().item())
                cls_name = result.names.get(cls_id, str(cls_id))
                detections.append(YoloDetection(
                    x1=float(xyxy[0]),
                    y1=float(xyxy[1]),
                    x2=float(xyxy[2]),
                    y2=float(xyxy[3]),
                    class_name=cls_name,
                    confidence=conf,
                ))

    if _using_custom_weights:
        # Custom weights: relevance = has defect detections
        is_relevant = len(detections) > 0
    else:
        # COCO fallback: frame passed quality check → relevant for DINO analysis.
        # COCO detections are informational only.
        is_relevant = True
        frame_class = "pipe_content" if frame_class == "empty" else frame_class

    return YoloResponse(
        is_relevant=is_relevant,
        detections=detections,
        frame_class=frame_class,
        inference_time_ms=round(elapsed_ms, 1),
    )
