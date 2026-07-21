"""SAM (Segment Anything Model) wrapper for pixel-precise segmentation."""

from __future__ import annotations

import time
import logging
import threading
from pathlib import Path

import numpy as np

from ..config import settings
from ..gpu_manager import gpu_manager, ModelSlot
from ..schemas.detection import BoundingBox
from ..schemas.segmentation import MaskResult, SamResponse
from .image_decode import decode_image_safe
from .box_utils import clamp_box
from .bend_geometry import analyze_bend

logger = logging.getLogger(__name__)
_sam_predict_lock = threading.Lock()


def _find_weights_in_dir(model_dir: Path) -> str | None:
    candidates = sorted(list(model_dir.glob("*.pth")) + list(model_dir.glob("*.pt")))
    return str(candidates[0]) if candidates else None


def _find_sam_weights() -> str:
    """Locate SAM weights in models_dir."""
    _, weights_path = _resolve_sam_backend()
    return weights_path


def _is_sam21_weights(path: Path) -> bool:
    return "sam2.1" in path.name.lower() or "sam2.1" in path.parent.name.lower()


def _find_sam21_weights() -> str | None:
    configured = settings.sam2_weights_path.strip()
    if configured:
        path = Path(configured)
        if not path.is_absolute():
            path = Path(settings.models_dir) / path
        return str(path) if path.exists() and _is_sam21_weights(path) else None

    return _find_weights_in_dir(Path(settings.models_dir) / "sam2.1")


def _resolve_sam_backend() -> tuple[str, str]:
    backend = settings.sam_backend.strip().lower().replace("_", ".")
    if backend in ("sam2", "2"):
        raise ValueError("SAM 2 is no longer supported. Use SAM 2.1 weights under models/sam2.1.")

    if backend in ("sam2.1", "2.1"):
        weights = _find_sam21_weights()
        if weights is None:
            raise FileNotFoundError(
                "SAM 2.1 requested, but no weights were found. "
                "Place sam2.1_hiera_*.pt under models/sam2.1 or set SEWER_SIDECAR_SAM2_WEIGHTS_PATH."
            )
        return "sam2.1", weights

    if backend in ("auto", ""):
        weights = _find_sam21_weights()
        if weights is not None:
            return "sam2.1", weights
        raise FileNotFoundError(
            "SAM 2.1 weights not found. "
            "Place sam2.1_hiera_*.pt under models/sam2.1 or set SEWER_SIDECAR_SAM2_WEIGHTS_PATH."
        )

    raise ValueError(f"Unsupported SAM backend: {settings.sam_backend!r}")


def _resolve_device() -> str:
    """Determine the effective device for SAM inference."""
    device = settings.effective_sam_device
    if device.startswith("cuda") and not _cuda_available():
        return "cpu"
    return device


def _load_sam_on(device: str):
    """Load SAM model onto *device*. Returns (model, predictor)."""
    backend, weights_path = _resolve_sam_backend()

    try:
        from sam2.build_sam import build_sam2
        from sam2.sam2_image_predictor import SAM2ImagePredictor
    except ImportError:
        raise ImportError(
            "sam2 is not installed. Install locally with: "
            "pip install git+https://github.com/facebookresearch/sam2.git"
        )

    model_cfg = _resolve_sam2_cfg(weights_path)
    sam = build_sam2(model_cfg, weights_path, device=device)
    predictor = SAM2ImagePredictor(sam)
    setattr(predictor, "_sewer_sam_backend", backend)
    logger.info("Loading SAM 2 weights from %s onto %s", weights_path, device)
    return sam, predictor


def _resolve_sam2_cfg(weights_path: str) -> str:
    configured = settings.sam2_model_cfg.strip()
    if configured and configured.lower() != "auto":
        return configured

    name = Path(weights_path).name.lower()
    prefix = "configs/sam2.1"
    file_prefix = "sam2.1"
    if "tiny" in name:
        return f"{prefix}/{file_prefix}_hiera_t.yaml"
    if "small" in name:
        return f"{prefix}/{file_prefix}_hiera_s.yaml"
    if "base_plus" in name or "base-plus" in name or "b+" in name:
        return f"{prefix}/{file_prefix}_hiera_b+.yaml"
    return f"{prefix}/{file_prefix}_hiera_l.yaml"


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


def segment(
    image_base64: str,
    bounding_boxes: list[BoundingBox],
    pipe_diameter_mm: int | None = None,
) -> SamResponse:
    """Run SAM segmentation for each bounding box.

    pipe_diameter_mm wird bewusst nur durchgereicht und hat derzeit KEINE Wirkung auf die
    Maske: die mm-Quantifizierung liegt nach dem Thin-AI-Prinzip in C#. Das Feld bleibt im
    Vertrag als Reserve fuer eine spaetere geometrische Auswertung im Sidecar.
    """
    _ = pipe_diameter_mm  # bewusst ungenutzt (siehe Docstring)
    device = _resolve_device()
    state = gpu_manager.ensure_loaded(ModelSlot.SAM, device, lambda: _load_sam_on(device))
    predictor = state.processor  # SAM2ImagePredictor

    img = decode_image_safe(
        image_base64,
        max_bytes=settings.inference_max_image_bytes,
        max_pixels=settings.max_image_pixels,
    )
    img_array = np.array(img)
    h, w = img_array.shape[:2]

    t0 = time.perf_counter()

    masks_out: list[MaskResult] = []
    requested_boxes = len(bounding_boxes)
    skipped_boxes = 0
    low_score_boxes = 0
    first_error: str | None = None

    # SAM2ImagePredictor ist stateful: set_image und predict muessen pro Request atomar bleiben.
    with _sam_predict_lock:
        predictor.set_image(img_array)

        for bbox in bounding_boxes:
            clamped = clamp_box(bbox.x1, bbox.y1, bbox.x2, bbox.y2, w, h)
            if clamped is None:
                skipped_boxes += 1
                logger.warning("SAM-Box uebersprungen (ausserhalb Bild / Null-Flaeche): %s", bbox)
                continue
            bx1, by1, bx2, by2 = clamped
            try:
                box_np = np.array([bx1, by1, bx2, by2])

                pred_masks, scores, _ = predictor.predict(
                    point_coords=None,
                    point_labels=None,
                    box=box_np,
                    multimask_output=False,
                )
            except Exception as exc:
                logger.warning("SAM prediction failed for box %s: %s", bbox, exc)
                # Ehrlichkeit: die Fehlerursache dem C#-Client sichtbar machen, damit ein
                # Inferenzfehler nicht mit einer legitim verworfenen Box verwechselt wird.
                if first_error is None:
                    first_error = f"{type(exc).__name__}: {exc}"
                skipped_boxes += 1
                continue

            mask = pred_masks[0]  # (H, W) bool
            score = float(scores[0])

            if score < settings.sam_min_score:
                skipped_boxes += 1
                low_score_boxes += 1
                logger.warning(
                    "SAM-Maske verworfen (Score %.3f < sam_min_score %.2f) fuer Box %s",
                    score, settings.sam_min_score, bbox,
                )
                continue

            mask_area = int(mask.sum())
            ys, xs = np.where(mask)

            if len(xs) == 0:
                skipped_boxes += 1
                continue

            mask_h = int(ys.max() - ys.min() + 1)
            mask_w = int(xs.max() - xs.min() + 1)
            centroid_x = float(xs.mean())
            centroid_y = float(ys.mean())

            masks_out.append(MaskResult(
                label=bbox.label,
                confidence=round(score, 4),
                bbox=[bx1, by1, bx2, by2],
                mask_rle=_rle_encode(mask.astype(np.uint8)),
                mask_area_pixels=mask_area,
                image_area_pixels=h * w,
                height_pixels=mask_h,
                width_pixels=mask_w,
                centroid_x=round(centroid_x, 1),
                centroid_y=round(centroid_y, 1),
            ))

    elapsed_ms = (time.perf_counter() - t0) * 1000

    # Geometrisches Bogen-Signal aus DEMSELBEN Frame. Per Default deaktiviert,
    # damit SAM wieder wie in der Sicherung vom 14.06.2026 antwortet.
    bend = analyze_bend(img_array) if settings.bend_geometry_enabled else None

    return SamResponse(
        masks=masks_out,
        image_width=w,
        image_height=h,
        inference_time_ms=round(elapsed_ms, 1),
        requested_boxes=requested_boxes,
        skipped_boxes=skipped_boxes,
        low_score_boxes=low_score_boxes,
        degraded=skipped_boxes > 0,
        error=first_error,
        bend_shift=round(bend.shift, 4) if bend is not None else 0.0,
        is_bend=bend.is_bend if bend is not None else False,
        vanish_x=round(bend.vanish_x, 4) if bend is not None else 0.5,
        vanish_y=round(bend.vanish_y, 4) if bend is not None else 0.5,
    )
