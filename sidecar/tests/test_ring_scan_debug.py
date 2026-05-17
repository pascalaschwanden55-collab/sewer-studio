"""Diagnostic runner for the SAM ring-scan path.

Run manually:
    $env:RUN_RING_SCAN_DEBUG='1'
    python -m pytest tests/test_ring_scan_debug.py -s

Optional overrides:
    $env:RING_DEBUG_IMAGE='D:\\...\\frame.png'
    $env:RING_DEBUG_MIN_SCORE='0.05'
    $env:RING_DEBUG_MIN_AREA='20'
    $env:RING_DEBUG_NUM_ANGLES='48'
    $env:RING_DEBUG_NUM_RADII='4'
"""

from __future__ import annotations

import base64
import io
import os
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import pytest
from PIL import Image

PROJECT_ROOT = Path(__file__).resolve().parents[2]
SIDECAR_ROOT = PROJECT_ROOT / "sidecar"
os.environ.setdefault("SEWER_SIDECAR_MODELS_DIR", str(SIDECAR_ROOT / "models"))

from sidecar.models import pipe_axis, sam_wrapper  # noqa: E402
from sidecar.schemas.segmentation import RingScanParams  # noqa: E402


DEFAULT_IMAGE = (
    r"D:\Haltungen\07.717339-690761\self_training_frames"
    r"\20220520_07_717339-690761_BCCAY_0.5m_4.png"
)


@dataclass
class DebugSummary:
    generated_positive_points: int = 0
    generated_negative_points: int = 0
    sectors_without_points: int = 0
    raw_masks: int = 0
    after_score: int = 0
    after_area: int = 0
    after_annulus_ratio: int = 0
    after_annulus_size: int = 0
    nms_input: int = 0
    nms_output: int = 0
    nms_duplicates: int = 0
    exceptions: int = 0


def _image_to_base64(path: Path) -> str:
    return base64.b64encode(path.read_bytes()).decode("ascii")


def _clamp(value: float, min_value: float, max_value: float) -> float:
    return max(min_value, min(value, max_value))


def _to_pixels(value: float, size: int) -> float:
    return value * size if 0 <= value <= 1.5 else value


def _to_radius_pixels(value: float, size: int) -> float:
    return value * size if 0 < value <= 1.5 else value


def _is_plausible(cx: float, cy: float, rx: float, ry: float, w: int, h: int) -> bool:
    min_side = min(w, h)
    if cx < 0 or cx > w or cy < 0 or cy > h:
        return False
    if rx < min_side * 0.08 or ry < min_side * 0.08:
        return False
    return not (rx > w or ry > h)


def _make_default_ring_params(image_b64: str, w: int, h: int) -> RingScanParams:
    axis = pipe_axis.analyze_pipe_axis(image_b64)
    min_side = min(w, h)

    center_x = w / 2.0
    center_y = h / 2.0
    radius_x = min_side * 0.44
    radius_y = radius_x

    ax = _to_pixels(axis.pipe_center_x, w)
    ay = _to_pixels(axis.pipe_center_y, h)
    arx = _to_radius_pixels(axis.pipe_radius_x, w)
    ary = _to_radius_pixels(axis.pipe_radius_y, h)
    if _is_plausible(ax, ay, arx, ary, w, h):
        center_x = ax
        center_y = ay
        radius_x = arx
        radius_y = ary

    outer = _clamp((radius_x + radius_y) / 2.0, min_side * 0.18, min_side * 0.48)
    inner = _clamp(outer * 0.52, min_side * 0.10, outer * 0.80)
    min_area = max(80, int(w * h * 0.00035))

    params = RingScanParams(
        center_x=float(os.environ.get("RING_DEBUG_CENTER_X", center_x)),
        center_y=float(os.environ.get("RING_DEBUG_CENTER_Y", center_y)),
        inner_radius=float(os.environ.get("RING_DEBUG_INNER_RADIUS", inner)),
        outer_radius=float(os.environ.get("RING_DEBUG_OUTER_RADIUS", outer)),
        num_angles=int(os.environ.get("RING_DEBUG_NUM_ANGLES", "32")),
        num_radii=int(os.environ.get("RING_DEBUG_NUM_RADII", "3")),
        min_score=float(os.environ.get("RING_DEBUG_MIN_SCORE", "0.30")),
        min_area_pixels=int(os.environ.get("RING_DEBUG_MIN_AREA", str(min_area))),
        iou_threshold=float(os.environ.get("RING_DEBUG_IOU_THRESHOLD", "0.35")),
    )

    print(
        "pipe_axis "
        f"center=({axis.pipe_center_x:.4f},{axis.pipe_center_y:.4f}) "
        f"radius=({axis.pipe_radius_x:.4f},{axis.pipe_radius_y:.4f}) "
        f"confidence={axis.confidence:.3f}"
    )
    print(f"ring_params {params.model_dump()}")
    return params


def _load_predictor_for_image(image: Image.Image):
    import torch

    device = sam_wrapper._resolve_device()
    state = sam_wrapper.gpu_manager.ensure_loaded(
        sam_wrapper.ModelSlot.SAM,
        device,
        lambda: sam_wrapper._load_sam2_on(device),
    )
    predictor = state.processor
    with torch.inference_mode():
        predictor.set_image(np.array(image.convert("RGB")))
    return predictor, device


def run_ring_scan_debug(predictor, ring_params: RingScanParams, h: int, w: int) -> DebugSummary:
    import torch

    cx, cy = ring_params.center_x, ring_params.center_y
    r_inner = ring_params.inner_radius
    r_outer = ring_params.outer_radius
    n_angles = ring_params.num_angles
    n_radii = ring_params.num_radii
    min_score = ring_params.min_score
    min_area = ring_params.min_area_pixels
    iou_thresh = ring_params.iou_threshold

    sector_step = 2 * np.pi / n_angles
    mid_r = (r_inner + r_outer) / 2.0
    radii = np.linspace(
        r_inner + (r_outer - r_inner) * 0.2,
        r_outer - (r_outer - r_inner) * 0.2,
        n_radii,
    )
    neg_margin = (r_outer - r_inner) * 0.5
    neg_points = [
        (cx, cy),
        (cx, cy - r_outer - neg_margin),
        (cx + r_outer + neg_margin, cy),
        (cx, cy + r_outer + neg_margin),
        (cx - r_outer - neg_margin, cy),
    ]
    neg_points = [(x, y) for x, y in neg_points if 0 <= x < w and 0 <= y < h]

    summary = DebugSummary(generated_negative_points=len(neg_points))
    candidates: list[tuple[np.ndarray, float, dict]] = []
    print(f"negative_points count={len(neg_points)} points={[(round(x, 1), round(y, 1)) for x, y in neg_points]}")

    for sector_idx in range(n_angles):
        angle = sector_idx * sector_step
        pos_coords = []
        for r in radii:
            px = cx + r * np.cos(angle)
            py = cy + r * np.sin(angle)
            if 0 <= px < w and 0 <= py < h:
                pos_coords.append((px, py))

        for offset in [-sector_step * 0.3, sector_step * 0.3]:
            a2 = angle + offset
            px2 = cx + mid_r * np.cos(a2)
            py2 = cy + mid_r * np.sin(a2)
            if 0 <= px2 < w and 0 <= py2 < h:
                pos_coords.append((px2, py2))

        summary.generated_positive_points += len(pos_coords)
        if not pos_coords:
            summary.sectors_without_points += 1
            print(f"sector={sector_idx:02d} angle={angle:.3f} pos=0 skipped=no_points")
            continue

        all_coords = pos_coords + neg_points
        all_labels = [1] * len(pos_coords) + [0] * len(neg_points)

        try:
            coords_np = np.array(all_coords, dtype=np.float32)
            labels_np = np.array(all_labels, dtype=np.int32)
            with torch.inference_mode():
                pred_masks, scores, _ = predictor.predict(
                    point_coords=coords_np,
                    point_labels=labels_np,
                    box=None,
                    multimask_output=True,
                )

            scores_arr = np.asarray(scores).reshape(-1)
            masks_arr = np.asarray(pred_masks)
            if masks_arr.ndim == 4 and masks_arr.shape[0] == 1:
                masks_arr = masks_arr[0]

            raw_areas = [int(np.asarray(m).sum()) for m in masks_arr[: len(scores_arr)]]
            print(
                f"sector={sector_idx:02d} angle={angle:.3f} "
                f"pos={len(pos_coords)} neg={len(neg_points)} "
                f"scores={[round(float(s), 4) for s in scores_arr]} "
                f"raw_areas={raw_areas}"
            )

            for mask_idx, score_value in enumerate(scores_arr):
                summary.raw_masks += 1
                score = float(score_value)
                if score < min_score:
                    print(f"  mask={mask_idx} drop=score score={score:.4f}")
                    continue
                summary.after_score += 1

                raw_mask = masks_arr[mask_idx]
                mask = sam_wrapper._clip_annulus_mask(raw_mask, cx, cy, r_inner, r_outer)
                mask_area = int(mask.sum())
                if mask_area < min_area:
                    print(f"  mask={mask_idx} drop=area score={score:.4f} area={mask_area}")
                    continue
                summary.after_area += 1

                ys, xs = np.where(mask)
                if len(xs) == 0:
                    print(f"  mask={mask_idx} drop=empty score={score:.4f}")
                    continue

                raw_area = int(raw_mask.sum())
                annulus_ratio = mask_area / max(raw_area, 1)
                if annulus_ratio < 0.15:
                    print(
                        f"  mask={mask_idx} drop=annulus_ratio score={score:.4f} "
                        f"area={mask_area} raw_area={raw_area} ratio={annulus_ratio:.4f}"
                    )
                    continue
                summary.after_annulus_ratio += 1

                annulus_area = np.pi * (r_outer**2 - r_inner**2)
                if mask_area > annulus_area * 0.6:
                    print(
                        f"  mask={mask_idx} drop=annulus_size score={score:.4f} "
                        f"area={mask_area} limit={annulus_area * 0.6:.1f}"
                    )
                    continue
                summary.after_annulus_size += 1

                meta = {
                    "bbox": [float(xs.min()), float(ys.min()), float(xs.max()), float(ys.max())],
                    "area": mask_area,
                    "centroid_x": float(xs.mean()),
                    "centroid_y": float(ys.mean()),
                    "h_px": int(ys.max() - ys.min() + 1),
                    "w_px": int(xs.max() - xs.min() + 1),
                    "annulus_ratio": annulus_ratio,
                }
                print(
                    f"  mask={mask_idx} keep_candidate score={score:.4f} "
                    f"area={mask_area} ratio={annulus_ratio:.4f} bbox={meta['bbox']}"
                )
                candidates.append((mask, score, meta))
        except Exception as exc:
            summary.exceptions += 1
            print(f"sector={sector_idx:02d} exception={type(exc).__name__}: {exc}")

    summary.nms_input = len(candidates)
    candidates.sort(key=lambda c: c[1], reverse=True)
    keep: list[tuple[np.ndarray, float, dict]] = []
    for mask, score, meta in candidates:
        duplicate = False
        for kept_mask, _, _ in keep:
            if sam_wrapper._compute_iou(mask, kept_mask) > iou_thresh:
                duplicate = True
                break
        if duplicate:
            summary.nms_duplicates += 1
        else:
            keep.append((mask, score, meta))
    summary.nms_output = len(keep)

    print(
        "filter_summary "
        f"raw_masks={summary.raw_masks} after_score={summary.after_score} "
        f"after_area={summary.after_area} after_annulus_ratio={summary.after_annulus_ratio} "
        f"after_annulus_size={summary.after_annulus_size} exceptions={summary.exceptions}"
    )
    print(
        "nms_summary "
        f"input={summary.nms_input} output={summary.nms_output} "
        f"duplicates={summary.nms_duplicates}"
    )
    for idx, (_, score, meta) in enumerate(keep[:10]):
        print(
            f"nms_keep[{idx}] score={score:.4f} area={meta['area']} "
            f"centroid=({meta['centroid_x']:.1f},{meta['centroid_y']:.1f}) "
            f"bbox={meta['bbox']}"
        )
    return summary


@pytest.mark.skipif(os.environ.get("RUN_RING_SCAN_DEBUG") != "1", reason="manual SAM debug runner")
def test_ring_scan_debug_bccay_frame_returns_mask():
    image_path = Path(os.environ.get("RING_DEBUG_IMAGE", DEFAULT_IMAGE))
    assert image_path.exists(), f"debug image not found: {image_path}"

    image_b64 = _image_to_base64(image_path)
    image = Image.open(io.BytesIO(base64.b64decode(image_b64))).convert("RGB")
    w, h = image.size
    print(f"image path={image_path} size={w}x{h}")
    ring_params = _make_default_ring_params(image_b64, w, h)

    predictor, _ = _load_predictor_for_image(image)
    debug_summary = run_ring_scan_debug(predictor, ring_params, h, w)
    production_results = sam_wrapper._ring_scan(predictor, ring_params=ring_params, h=h, w=w, device="")
    print(f"production_ring_scan_results={len(production_results)}")

    response = sam_wrapper.segment(
        image_base64=image_b64,
        bounding_boxes=[],
        point_prompts=[],
        ring_scan=ring_params,
    )
    print(f"segment_response_masks={len(response.masks)}")

    assert debug_summary.raw_masks > 0
    assert response.masks, "ring_scan returned no masks for the BCCAY axial bend frame"
