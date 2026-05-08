"""SAM 2 (Segment Anything Model 2) Wrapper fuer Pixel-praezise Segmentierung.

Ersetzt SAM 3. API-Schemas (SamRequest/SamResponse) bleiben identisch.
GPU-Slot: ModelSlot.SAM (unveraendert).
Ring-Scan Logik (Annulus-Geometrie) bleibt komplett erhalten.
"""

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

# Slice 1: cv2 fuer Polygon-Approximation. ultralytics zieht opencv-python
# als Dependency, der Import sollte normalerweise klappen. Wenn nicht,
# liefern wir polygon_points=None und loggen einmalig.
try:
    import cv2 as _cv2
    _HAS_CV2 = True
except ImportError:
    _HAS_CV2 = False
    logger.warning("OpenCV nicht verfuegbar — SAM-Polygon-Approximation deaktiviert")


def _get_sam_max_batch() -> int:
    """Dynamische Batch-Groesse basierend auf verfuegbarem VRAM."""
    avail_gb = gpu_manager.get_available_vram_gb()
    return max(10, min(100, int(avail_gb * 15)))


def _find_sam2_checkpoint() -> str:
    """Locate SAM 2 checkpoint in models_dir."""
    sam_dir = Path(settings.models_dir) / "sam2"
    candidates = list(sam_dir.glob("*.pt")) + list(sam_dir.glob("*.pth"))
    if not candidates:
        raise FileNotFoundError(
            f"SAM 2 Checkpoint nicht gefunden in {sam_dir}. "
            "Bitte SAM 2 Checkpoint (.pt/.pth) dort ablegen."
        )
    return str(candidates[0])


def _resolve_device() -> str:
    """Determine the effective device for SAM 2 inference."""
    device = settings.effective_sam_device
    if device.startswith("cuda") and not _cuda_available():
        return "cpu"
    return device


def _load_sam2_on(device: str):
    """Load SAM 2 model onto *device*. Returns (model, predictor)."""
    try:
        from sam2.build_sam import build_sam2
        from sam2.sam2_image_predictor import SAM2ImagePredictor
    except ImportError:
        raise ImportError(
            "sam2 ist nicht installiert. "
            "Install mit: pip install sam2"
        )

    checkpoint = _find_sam2_checkpoint()
    # sam2.build_sam2 erwartet Hydra-Config Name (z.B. "sam2.1/sam2.1_hiera_l.yaml")
    model_cfg = settings.sam_model_type

    sam = build_sam2(model_cfg, ckpt_path=checkpoint, device=device)

    # Blackwell-Optimierung: torch.compile
    try:
        import torch
        if device.startswith("cuda"):
            sam = torch.compile(sam, mode="reduce-overhead")
            logger.info("SAM 2: torch.compile aktiviert")
    except Exception as exc:
        logger.warning("SAM 2: torch.compile fehlgeschlagen (kein Problem): %s", exc)

    predictor = SAM2ImagePredictor(sam)
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
    parts = [str(start_val)] + [str(int(r)) for r in runs]
    return ",".join(parts)


def _mask_to_polygon(mask: np.ndarray, epsilon_ratio: float = 0.005) -> list[list[float]] | None:
    """Approximiert die binaere Maske zu einem Polygon (Douglas-Peucker via cv2).

    Slice 1 (Operateur-Annotation): YOLO-seg braucht ein Polygon — die Maske
    selbst ist als RLE schon im MaskResult. Wir nehmen die groesste externe
    Kontur, weil mehrere Inseln in einer SAM-Maske die Ausnahme sind und
    YOLO-seg pro Sample sowieso nur ein Polygon erwartet.

    Returns:
        list[[x, y]] in Pixelkoordinaten, oder None wenn cv2 nicht verfuegbar
        ist, die Kontur leer ist oder das Polygon zu degeneriert ist (< 3 Punkte).
    """
    if not _HAS_CV2:
        return None

    contours, _ = _cv2.findContours(
        mask.astype(np.uint8),
        _cv2.RETR_EXTERNAL,
        _cv2.CHAIN_APPROX_SIMPLE,
    )
    if not contours:
        return None

    largest = max(contours, key=_cv2.contourArea)
    perimeter = _cv2.arcLength(largest, closed=True)
    if perimeter <= 0:
        return None

    epsilon = max(1.0, epsilon_ratio * perimeter)
    approx = _cv2.approxPolyDP(largest, epsilon, closed=True)
    if approx is None or len(approx) < 3:
        return None

    # approx-Form: (N, 1, 2). Auf list[[x, y]] flachklopfen.
    return [[float(p[0][0]), float(p[0][1])] for p in approx]


def _append_mask_result(
    masks_out: list[MaskResult],
    mask: np.ndarray,
    score: float,
    bbox: BoundingBox,
    h: int,
    w: int,
    return_polygon: bool = False,
) -> None:
    """Berechnet Masken-Statistik und fuegt MaskResult hinzu.

    Wenn ``return_polygon`` True ist, wird zusaetzlich die Maske per
    cv2.approxPolyDP zu einem Polygon angenaehert und in
    ``MaskResult.polygon_points`` gelegt (Slice 1: Operateur-Annotation).
    """
    mask_area = int(mask.sum())
    ys, xs = np.where(mask)
    if len(xs) == 0:
        return

    polygon_points: list[list[float]] | None = None
    if return_polygon:
        polygon_points = _mask_to_polygon(mask)

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
        polygon_points=polygon_points,
    ))


def _predict_single_box(predictor, bbox: BoundingBox, masks_out: list[MaskResult],
                         h: int, w: int, device: str,
                         return_polygon: bool = False) -> None:
    """Fallback: einzelne Box vorhersagen (sequentiell)."""
    import torch
    try:
        box_np = np.array([bbox.x1, bbox.y1, bbox.x2, bbox.y2])
        with torch.inference_mode():
            masks, scores, _ = predictor.predict(
                point_coords=None, point_labels=None,
                box=box_np, multimask_output=False,
            )
        _append_mask_result(masks_out, masks[0], float(scores[0]), bbox, h, w, return_polygon)
    except Exception as exc:
        logger.warning("SAM 2 Prediction fehlgeschlagen fuer Box %s: %s", bbox, exc)


def _predict_boxes_batched(
    predictor, boxes: list[BoundingBox], masks_out: list[MaskResult],
    h: int, w: int, device: str,
    return_polygon: bool = False,
) -> None:
    """Batch: alle Boxen in einem Forward Pass durch SAM 2."""
    import torch
    if not boxes:
        return

    boxes_np = np.array([[b.x1, b.y1, b.x2, b.y2] for b in boxes])
    try:
        with torch.inference_mode():
            masks, scores, _ = predictor.predict(
                point_coords=None,
                point_labels=None,
                box=boxes_np,
                multimask_output=False,
            )
        # masks shape: (N, 1, H, W) oder (N, H, W)
        if masks.ndim == 4:
            masks = masks[:, 0]  # → (N, H, W)
        for i, bbox in enumerate(boxes):
            _append_mask_result(masks_out, masks[i], float(scores[i]), bbox, h, w, return_polygon)
    except Exception as exc:
        logger.warning("SAM 2 Batch fehlgeschlagen (%d Boxen): %s — Fallback sequentiell", len(boxes), exc)
        for bbox in boxes:
            _predict_single_box(predictor, bbox, masks_out, h, w, device, return_polygon)


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


def _ring_scan(predictor, ring_params, h: int, w: int, device: str,
               return_polygon: bool = False) -> list[MaskResult]:
    """Annulus-Bereich systematisch mit SAM 2 abtasten (Sektoren-Ansatz).

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

            with torch.inference_mode():
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
                raw_area = int(raw_mask.sum())
                annulus_ratio = mask_area / max(raw_area, 1)

                # Maske die fast komplett ausserhalb liegt → skip
                if annulus_ratio < 0.15:
                    continue

                # Maske die den ganzen Annulus ausfuellt → skip
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
        polygon_points = _mask_to_polygon(mask) if return_polygon else None
        results.append(MaskResult(
            label=f"ring_segment_{i}",
            confidence=round(score, 4),
            bbox=meta["bbox"],
            mask_rle=_rle_encode(mask.astype(np.uint8)),
            mask_area_pixels=meta["area"],
            image_area_pixels=0,  # wird unten gesetzt
            height_pixels=meta["h_px"],
            width_pixels=meta["w_px"],
            centroid_x=round(meta["centroid_x"], 1),
            centroid_y=round(meta["centroid_y"], 1),
            polygon_points=polygon_points,
        ))

    return results


def segment(
    image_base64: str,
    bounding_boxes: list[BoundingBox],
    pipe_diameter_mm: int | None = None,
    point_prompts: list | None = None,
    ring_scan=None,
    return_polygon: bool = False,
) -> SamResponse:
    """Run SAM 2 Segmentation mit Bounding Boxes, Point Prompts oder Ring Scan.

    Args:
        return_polygon: Slice 1 (Operateur-Annotation). Wenn True, haengt der
            Wrapper an jedes MaskResult ein cv2-approxPolyDP-Polygon. Default
            False, weil Bestandspfade (Detection-/Ring-Scan) das Polygon nicht
            brauchen und der Aufwand sonst sinnlos waere.
    """
    device = _resolve_device()
    state = gpu_manager.ensure_loaded(
        ModelSlot.SAM, device, lambda: _load_sam2_on(device)
    )
    predictor = state.processor  # SAM2ImagePredictor

    raw = base64.b64decode(image_base64)
    img = Image.open(io.BytesIO(raw)).convert("RGB")
    img_array = np.array(img)
    h, w = img_array.shape[:2]

    t0 = time.perf_counter()

    import torch

    # SAM 2: set_image (Encoder laeuft 1x)
    with torch.inference_mode():
        predictor.set_image(img_array)

    masks_out: list[MaskResult] = []

    # ── Ring-Scan: Annulus-Bereich systematisch abtasten ──
    if ring_scan is not None:
        masks_out = _ring_scan(predictor, ring_params=ring_scan, h=h, w=w, device=device,
                                return_polygon=return_polygon)
        # image_area nachtraeglich setzen
        for m in masks_out:
            m.image_area_pixels = h * w
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

            with torch.inference_mode():
                pred_masks, scores, _ = predictor.predict(
                    point_coords=coords,
                    point_labels=labels,
                    box=None,
                    multimask_output=True,
                )

            # Beste Maske waehlen (hoechster Score)
            best_idx = int(scores.argmax())
            mask = pred_masks[best_idx]
            score = float(scores[best_idx])

            ys, xs = np.where(mask)
            if len(xs) > 0:
                dummy_bbox = BoundingBox(
                    x1=float(xs.min()), y1=float(ys.min()),
                    x2=float(xs.max()), y2=float(ys.max()),
                    label="point_prompt", confidence=score,
                )
                _append_mask_result(masks_out, mask, score, dummy_bbox, h, w, return_polygon)

        except Exception as exc:
            logger.warning("SAM 2 Point-Prompt Prediction fehlgeschlagen: %s", exc)

    # ── Bounding-Box-Prompts ──
    elif len(bounding_boxes) > 1:
        max_batch = _get_sam_max_batch()
        if len(bounding_boxes) > max_batch:
            logger.warning(
                "SAM 2 Batch %d Boxen > Limit %d — wird in Chunks aufgeteilt",
                len(bounding_boxes), max_batch,
            )
        for chunk_start in range(0, len(bounding_boxes), max_batch):
            chunk = bounding_boxes[chunk_start : chunk_start + max_batch]
            _predict_boxes_batched(predictor, chunk, masks_out, h, w, device, return_polygon)

    elif len(bounding_boxes) == 1:
        _predict_single_box(predictor, bounding_boxes[0], masks_out, h, w, device, return_polygon)

    elapsed_ms = (time.perf_counter() - t0) * 1000

    return SamResponse(
        masks=masks_out,
        image_width=w,
        image_height=h,
        inference_time_ms=round(elapsed_ms, 1),
    )
