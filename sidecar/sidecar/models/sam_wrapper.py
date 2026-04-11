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

# Max Bounding-Boxen pro SAM-Batch um OOM zu vermeiden
_SAM_MAX_BATCH = 100

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


def _is_in_annulus(cx: float, cy: float, center_x: float, center_y: float,
                   inner_r: float, outer_r: float) -> bool:
    """Prueft ob Punkt im Annulus (Ring-Bereich) liegt."""
    dist = np.sqrt((cx - center_x) ** 2 + (cy - center_y) ** 2)
    return inner_r <= dist <= outer_r


def _compute_iou(mask_a: np.ndarray, mask_b: np.ndarray) -> float:
    """Intersection over Union fuer zwei Binaer-Masken."""
    intersection = np.logical_and(mask_a, mask_b).sum()
    union = np.logical_or(mask_a, mask_b).sum()
    if union == 0:
        return 0.0
    return float(intersection / union)


def _clip_annulus_mask(mask: np.ndarray, cx: float, cy: float,
                       r_inner: float, r_outer: float) -> np.ndarray:
    """Maske auf den Annulus-Bereich zuschneiden (Pixel ausserhalb = 0)."""
    h, w = mask.shape
    yy, xx = np.ogrid[:h, :w]
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    annulus = (dist >= r_inner) & (dist <= r_outer)
    return mask & annulus


def _ring_scan(predictor, ring_params, h: int, w: int, device: str) -> list[MaskResult]:
    """Annulus-Bereich systematisch mit SAM abtasten (Sektoren-Ansatz).

    Strategie: Jeder Sektor bekommt positive Punkte + Constraint-Punkte
    (negativ im Zentrum + ausserhalb), damit SAM nur im Annulus sucht.
    Danach werden alle Masken auf den Annulus zugeschnitten und gefiltert.
    """
    import torch

    cx, cy = ring_params.center_x, ring_params.center_y
    r_inner = ring_params.inner_radius
    r_outer = ring_params.outer_radius
    n_angles = ring_params.num_angles
    n_radii = ring_params.num_radii
    min_score = ring_params.min_score
    min_area = ring_params.min_area_pixels
    iou_thresh = ring_params.iou_threshold

    # Strategie 1: Sektoren-Scan (je 30° Sektor, mehrere pos. Punkte + neg. Constraints)
    sector_step = 2 * np.pi / n_angles
    mid_r = (r_inner + r_outer) / 2.0
    radii = np.linspace(r_inner + (r_outer - r_inner) * 0.2,
                        r_outer - (r_outer - r_inner) * 0.2,
                        n_radii)

    # Feste negative Constraint-Punkte: Zentrum + 4 Punkte knapp ausserhalb
    neg_margin = (r_outer - r_inner) * 0.5
    neg_points = [
        (cx, cy),  # Zentrum (Lumen ausschliessen)
        (cx, cy - r_outer - neg_margin),  # oben aussen
        (cx + r_outer + neg_margin, cy),  # rechts aussen
        (cx, cy + r_outer + neg_margin),  # unten aussen
        (cx - r_outer - neg_margin, cy),  # links aussen
    ]
    # Punkte im Bild-Bereich filtern
    neg_points = [(x, y) for x, y in neg_points if 0 <= x < w and 0 <= y < h]

    logger.info("Ring-Scan: %d Sektoren x %d Radien, r_inner=%.0f, r_outer=%.0f",
                n_angles, n_radii, r_inner, r_outer)

    candidates: list[tuple[np.ndarray, float, dict]] = []
    use_amp = device.startswith("cuda")

    for sector_idx in range(n_angles):
        angle = sector_idx * sector_step

        # Positive Punkte: Mehrere Radien im Sektor
        pos_coords = []
        for r in radii:
            px = cx + r * np.cos(angle)
            py = cy + r * np.sin(angle)
            if 0 <= px < w and 0 <= py < h:
                pos_coords.append((px, py))

        # Zusaetzlich: leicht versetzte Punkte (±15° fuer breitere Abdeckung)
        for offset in [-sector_step * 0.3, sector_step * 0.3]:
            a2 = angle + offset
            px2 = cx + mid_r * np.cos(a2)
            py2 = cy + mid_r * np.sin(a2)
            if 0 <= px2 < w and 0 <= py2 < h:
                pos_coords.append((px2, py2))

        if not pos_coords:
            continue

        # Alle Punkte zusammenbauen: positive + negative Constraints
        all_coords = pos_coords + neg_points
        all_labels = [1] * len(pos_coords) + [0] * len(neg_points)

        try:
            coords_np = np.array(all_coords, dtype=np.float32)
            labels_np = np.array(all_labels, dtype=np.int32)

            with torch.cuda.amp.autocast(enabled=use_amp):
                pred_masks, scores, _ = predictor.predict(
                    point_coords=coords_np,
                    point_labels=labels_np,
                    box=None,
                    multimask_output=True,
                )

            # Alle 3 Masken pruefen (nicht nur die beste)
            for mask_idx in range(len(scores)):
                score = float(scores[mask_idx])
                if score < min_score:
                    continue

                raw_mask = pred_masks[mask_idx]

                # Maske auf Annulus zuschneiden
                mask = _clip_annulus_mask(raw_mask, cx, cy, r_inner, r_outer)

                mask_area = int(mask.sum())
                if mask_area < min_area:
                    continue

                ys, xs = np.where(mask)
                if len(xs) == 0:
                    continue

                centroid_x = float(xs.mean())
                centroid_y = float(ys.mean())

                # Annulus-Anteil: Wie viel der Maske liegt im Annulus?
                # (hoher Anteil = Maske ist gut eingegrenzt)
                raw_area = int(raw_mask.sum())
                annulus_ratio = mask_area / max(raw_area, 1)

                # Maske die fast komplett ausserhalb liegt → skip
                if annulus_ratio < 0.15:
                    continue

                # Maske die den ganzen Annulus ausfuellt → skip (kein spezifisches Segment)
                annulus_area = np.pi * (r_outer ** 2 - r_inner ** 2)
                if mask_area > annulus_area * 0.6:
                    continue

                meta = {
                    "bbox": [float(xs.min()), float(ys.min()),
                             float(xs.max()), float(ys.max())],
                    "area": mask_area,
                    "centroid_x": centroid_x,
                    "centroid_y": centroid_y,
                    "h_px": int(ys.max() - ys.min() + 1),
                    "w_px": int(xs.max() - xs.min() + 1),
                    "annulus_ratio": annulus_ratio,
                }
                candidates.append((mask, score, meta))

        except Exception as exc:
            logger.debug("Ring-Scan Sektor %d fehlgeschlagen: %s", sector_idx, exc)

    logger.info("Ring-Scan: %d Kandidaten nach Filter", len(candidates))

    # NMS: Ueberlappende Masken entfernen (hoechster Score gewinnt)
    candidates.sort(key=lambda c: c[1], reverse=True)
    keep: list[tuple[np.ndarray, float, dict]] = []
    for mask, score, meta in candidates:
        is_duplicate = False
        for kept_mask, _, _ in keep:
            if _compute_iou(mask, kept_mask) > iou_thresh:
                is_duplicate = True
                break
        if not is_duplicate:
            keep.append((mask, score, meta))

    logger.info("Ring-Scan: %d Segmente nach NMS", len(keep))

    # MaskResult-Liste aufbauen
    results: list[MaskResult] = []
    for i, (mask, score, meta) in enumerate(keep):
        results.append(MaskResult(
            label=f"ring_segment_{i}",
            confidence=round(score, 4),
            bbox=meta["bbox"],
            mask_rle=_rle_encode(mask.astype(np.uint8)),
            mask_area_pixels=meta["area"],
            image_area_pixels=h * w,
            height_pixels=meta["h_px"],
            width_pixels=meta["w_px"],
            centroid_x=round(meta["centroid_x"], 1),
            centroid_y=round(meta["centroid_y"], 1),
        ))

    return results


def segment(
    image_base64: str,
    bounding_boxes: list[BoundingBox],
    pipe_diameter_mm: int | None = None,
    point_prompts: list | None = None,
    ring_scan=None,
) -> SamResponse:
    """Run SAM segmentation with bounding boxes, point prompts, or ring scan."""
    device = _resolve_device()
    state = gpu_manager.ensure_loaded(ModelSlot.SAM, device, lambda: _load_sam_on(device))
    predictor = state.processor  # SamPredictor

    raw = base64.b64decode(image_base64)
    img = Image.open(io.BytesIO(raw)).convert("RGB")
    img_array = np.array(img)
    h, w = img_array.shape[:2]

    t0 = time.perf_counter()

    import torch

    # Set image once (Encoder laeuft 1x)
    with torch.cuda.amp.autocast(enabled=device.startswith("cuda")):
        predictor.set_image(img_array)

    masks_out: list[MaskResult] = []

    # ── Ring-Scan: Annulus-Bereich systematisch abtasten ──
    if ring_scan is not None:
        masks_out = _ring_scan(predictor, ring_scan, h, w, device)
        elapsed_ms = (time.perf_counter() - t0) * 1000
        return SamResponse(
            masks=masks_out,
            image_width=w,
            image_height=h,
            inference_time_ms=round(elapsed_ms, 1),
        )

    # ── Punkt-Prompts (Linksklick=positiv, Rechtsklick=negativ) ──
    if point_prompts and len(point_prompts) > 0:
        try:
            coords = np.array([[p.x, p.y] for p in point_prompts])
            labels = np.array([p.label for p in point_prompts])

            with torch.cuda.amp.autocast(enabled=device.startswith("cuda")):
                pred_masks, scores, _ = predictor.predict(
                    point_coords=coords,
                    point_labels=labels,
                    box=None,
                    multimask_output=True,  # 3 Masken, beste waehlen
                )

            # Beste Maske waehlen (hoechster Score)
            best_idx = int(scores.argmax())
            mask = pred_masks[best_idx]
            score = float(scores[best_idx])

            mask_area = int(mask.sum())
            ys, xs = np.where(mask)
            if len(xs) > 0:
                # Dummy-BBox aus Masken-Extent
                dummy_bbox = BoundingBox(
                    x1=float(xs.min()), y1=float(ys.min()),
                    x2=float(xs.max()), y2=float(ys.max()),
                    label="point_prompt", confidence=score,
                )
                _append_mask_result(masks_out, mask, score, dummy_bbox, h, w)

        except Exception as exc:
            logger.warning("SAM point-prompt prediction failed: %s", exc)

    # ── Bounding-Box-Prompts ──
    # Batch-Limit: max 100 Boxen pro Forward-Pass um OOM zu vermeiden
    elif len(bounding_boxes) > 1:
        if len(bounding_boxes) > _SAM_MAX_BATCH:
            logger.warning(
                "SAM batch %d Boxen > Limit %d — wird in Chunks aufgeteilt",
                len(bounding_boxes), _SAM_MAX_BATCH,
            )
        # In Chunks von max _SAM_MAX_BATCH verarbeiten
        for chunk_start in range(0, len(bounding_boxes), _SAM_MAX_BATCH):
            chunk = bounding_boxes[chunk_start : chunk_start + _SAM_MAX_BATCH]
            try:
                all_boxes = np.array([[b.x1, b.y1, b.x2, b.y2] for b in chunk])
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
                for i, bbox in enumerate(chunk):
                    mask = pred_masks[i, 0].cpu().numpy()
                    score = float(scores[i, 0].cpu())
                    _append_mask_result(masks_out, mask, score, bbox, h, w)

            except Exception as exc:
                logger.warning("SAM batch chunk failed, fallback to sequential: %s", exc)
                for bbox in chunk:
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
