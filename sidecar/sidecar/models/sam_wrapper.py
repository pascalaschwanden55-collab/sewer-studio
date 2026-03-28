"""SAM (Segment Anything Model) wrapper for pixel-precise segmentation."""

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
from ..schemas.detection import BoundingBox
from ..schemas.segmentation import MaskResult, SamResponse

logger = logging.getLogger(__name__)


def _find_sam_weights() -> str:
    """Locate SAM weights in models_dir."""
    sam_dir = Path(settings.models_dir) / "sam3"
    candidates = list(sam_dir.glob("*.pth")) + list(sam_dir.glob("*.pt"))
    if not candidates:
        raise FileNotFoundError(
            f"SAM weights not found in {sam_dir}. "
            "Please place SAM checkpoint (.pth) there."
        )
    return str(candidates[0])


def _resolve_device() -> str:
    """Determine the effective device for SAM inference."""
    device = settings.effective_sam_device
    if device.startswith("cuda") and not _cuda_available():
        return "cpu"
    return device


def _load_sam_on(device: str):
    """Load SAM model onto *device*. Returns (model, predictor)."""
    try:
        from segment_anything import sam_model_registry, SamPredictor
    except ImportError:
        raise ImportError(
            "segment-anything is not installed. "
            "Install with: pip install segment-anything"
        )

    weights_path = _find_sam_weights()
    sam = sam_model_registry[settings.sam_model_type](checkpoint=weights_path)
    sam.to(device)
    predictor = SamPredictor(sam)
    return sam, predictor


def _cuda_available() -> bool:
    try:
        import torch
        return torch.cuda.is_available()
    except Exception:
        return False


def _rle_encode(mask: np.ndarray) -> str:
    """Simple run-length encoding of a binary mask."""
    flat = mask.flatten(order="C")
    if len(flat) == 0:
        return ""
    diffs = np.diff(flat.astype(np.int8))
    change_indices = np.where(diffs != 0)[0] + 1
    runs = np.diff(np.concatenate([[0], change_indices, [len(flat)]]))
    start_val = int(flat[0])
    # Format: start_value,run1,run2,...
    parts = [str(start_val)] + [str(int(r)) for r in runs]
    return ",".join(parts)


def _append_mask_result(
    masks_out: list[MaskResult],
    mask: np.ndarray,
    score: float,
    bbox: BoundingBox,
    h: int,
    w: int,
) -> None:
    """Berechnet Masken-Statistik und fuegt MaskResult hinzu."""
    mask_area = int(mask.sum())
    ys, xs = np.where(mask)
    if len(xs) == 0:
        return
    masks_out.append(MaskResult(
        label=bbox.label,
        confidence=round(score, 4),
        bbox=[bbox.x1, bbox.y1, bbox.x2, bbox.y2],
        mask_rle=_rle_encode(mask.astype(np.uint8)),
        mask_area_pixels=mask_area,
        image_area_pixels=h * w,
        height_pixels=int(ys.max() - ys.min() + 1),
        width_pixels=int(xs.max() - xs.min() + 1),
        centroid_x=round(float(xs.mean()), 1),
        centroid_y=round(float(ys.mean()), 1),
    ))


def _predict_single_box(predictor, bbox: BoundingBox, masks_out: list[MaskResult],
                         h: int, w: int, device: str) -> None:
    """Fallback: einzelne Box vorhersagen (sequentiell)."""
    import torch
    try:
        box_np = np.array([bbox.x1, bbox.y1, bbox.x2, bbox.y2])
        with torch.cuda.amp.autocast(enabled=device.startswith("cuda")):
            pred_masks, scores, _ = predictor.predict(
                point_coords=None, point_labels=None,
                box=box_np[None, :], multimask_output=False,
            )
        _append_mask_result(masks_out, pred_masks[0], float(scores[0]), bbox, h, w)
    except Exception as exc:
        logger.warning("SAM prediction failed for box %s: %s", bbox, exc)


def segment(
    image_base64: str,
    bounding_boxes: list[BoundingBox],
    pipe_diameter_mm: int | None = None,
) -> SamResponse:
    """Run SAM segmentation for each bounding box."""
    device = _resolve_device()
    state = gpu_manager.ensure_loaded(ModelSlot.SAM, device, lambda: _load_sam_on(device))
    predictor = state.processor  # SamPredictor

    raw = base64.b64decode(image_base64)
    img = Image.open(io.BytesIO(raw)).convert("RGB")
    img_array = np.array(img)
    h, w = img_array.shape[:2]

    t0 = time.perf_counter()

    import torch

    # Set image once for all boxes (Encoder laeuft 1x, Decoder pro Box)
    with torch.cuda.amp.autocast(enabled=device.startswith("cuda")):
        predictor.set_image(img_array)

    masks_out: list[MaskResult] = []

    # Batch-Predict: alle Boxen auf einmal wenn moeglich (1 Forward-Pass statt N)
    if len(bounding_boxes) > 1:
        try:
            all_boxes = np.array([[b.x1, b.y1, b.x2, b.y2] for b in bounding_boxes])
            import torch as _torch
            box_tensor = _torch.tensor(all_boxes, device=predictor.device)

            with torch.cuda.amp.autocast(enabled=device.startswith("cuda")):
                transformed_boxes = predictor.transform.apply_boxes_torch(
                    box_tensor, img_array.shape[:2])
                pred_masks, scores, _ = predictor.predict_torch(
                    point_coords=None,
                    point_labels=None,
                    boxes=transformed_boxes,
                    multimask_output=False,
                )
            # pred_masks: (N, 1, H, W), scores: (N, 1)
            for i, bbox in enumerate(bounding_boxes):
                mask = pred_masks[i, 0].cpu().numpy()  # (H, W)
                score = float(scores[i, 0].cpu())
                _append_mask_result(masks_out, mask, score, bbox, h, w)

        except Exception as exc:
            logger.warning("SAM batch predict failed, fallback to sequential: %s", exc)
            masks_out.clear()
            # Fallback: sequentiell (alter Code-Pfad)
            for bbox in bounding_boxes:
                _predict_single_box(predictor, bbox, masks_out, h, w, device)
    elif len(bounding_boxes) == 1:
        _predict_single_box(predictor, bounding_boxes[0], masks_out, h, w, device)

    elapsed_ms = (time.perf_counter() - t0) * 1000

    return SamResponse(
        masks=masks_out,
        image_width=w,
        image_height=h,
        inference_time_ms=round(elapsed_ms, 1),
    )
