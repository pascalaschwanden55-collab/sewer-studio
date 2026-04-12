"""ChangeNet Wrapper fuer Pixel-Level Aenderungserkennung.

Vergleicht zwei Inspektionsbilder derselben Haltung (alt vs. neu)
und erkennt Verschlechterungen, Verbesserungen und neue Schaeden.
GPU-Slot: ModelSlot.CHANGENET (on-demand, nicht persistent).
"""

from __future__ import annotations

import base64
import io
import time
import logging

import numpy as np
from PIL import Image

from ..config import settings
from ..gpu_manager import gpu_manager, ModelSlot

logger = logging.getLogger(__name__)


def _resolve_device() -> str:
    """Determine the effective device for ChangeNet."""
    device = settings.gpu_device
    try:
        import torch
        if device.startswith("cuda") and not torch.cuda.is_available():
            return "cpu"
    except ImportError:
        return "cpu"
    return device


def _load_changenet_on(device: str):
    """Load ChangeNet model onto *device*.

    Falls kein vortrainiertes ChangeNet-Modell vorhanden:
    Fallback auf strukturellen Pixel-Differenz-Algorithmus.
    """
    # TODO: Vortrainiertes ChangeNet-Modell laden wenn verfuegbar
    # Voruebergehend: None zurueckgeben, Pixel-Differenz als Fallback
    logger.info("ChangeNet: Verwende Pixel-Differenz-Algorithmus (kein vortrainiertes Modell)")
    return None, None


def _align_images(img_a: np.ndarray, img_b: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Bildausrichtung (einfaches Resize auf gleiche Groesse)."""
    h = max(img_a.shape[0], img_b.shape[0])
    w = max(img_a.shape[1], img_b.shape[1])

    if img_a.shape[:2] != (h, w):
        img_a = np.array(Image.fromarray(img_a).resize((w, h), Image.LANCZOS))
    if img_b.shape[:2] != (h, w):
        img_b = np.array(Image.fromarray(img_b).resize((w, h), Image.LANCZOS))

    return img_a, img_b


def _compute_change_mask(
    img_old: np.ndarray,
    img_new: np.ndarray,
    threshold: int = 30,
) -> dict:
    """Berechnet Pixel-Level Aenderungsmaske.

    Returns:
        Dict mit change_mask (H,W uint8: 0=keine, 1=Verschlechterung, 2=Verbesserung, 3=Neu),
        change_percent, und Statistik.
    """
    import cv2

    # In Graustufen konvertieren fuer Differenzberechnung
    gray_old = cv2.cvtColor(img_old, cv2.COLOR_RGB2GRAY).astype(np.float32)
    gray_new = cv2.cvtColor(img_new, cv2.COLOR_RGB2GRAY).astype(np.float32)

    # Absolute Differenz
    diff = np.abs(gray_new - gray_old)

    # Schwellenwert
    change_mask = np.zeros_like(diff, dtype=np.uint8)

    # Signifikante Aenderungen
    significant = diff > threshold

    # Heuristik: dunkler geworden = potentielle Verschlechterung (Riss, Ablagerung)
    darker = (gray_new < gray_old - threshold)
    lighter = (gray_new > gray_old + threshold)

    change_mask[darker] = 1   # Verschlechterung (rot)
    change_mask[lighter] = 2  # Verbesserung (gruen)

    # Kleine Regionen entfernen (Rauschen)
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
    for val in [1, 2]:
        binary = (change_mask == val).astype(np.uint8)
        cleaned = cv2.morphologyEx(binary, cv2.MORPH_OPEN, kernel)
        change_mask[change_mask == val] = 0
        change_mask[cleaned > 0] = val

    total_pixels = change_mask.size
    changed_pixels = int(np.count_nonzero(change_mask))
    worse_pixels = int(np.count_nonzero(change_mask == 1))
    better_pixels = int(np.count_nonzero(change_mask == 2))

    return {
        "change_mask": change_mask,
        "change_percent": round(changed_pixels / total_pixels * 100, 2),
        "worse_percent": round(worse_pixels / total_pixels * 100, 2),
        "better_percent": round(better_pixels / total_pixels * 100, 2),
        "total_pixels": total_pixels,
        "changed_pixels": changed_pixels,
    }


def detect_changes(
    image_old_base64: str,
    image_new_base64: str,
    threshold: int = 30,
) -> dict:
    """Vergleicht zwei Bilder und erkennt Aenderungen.

    Returns:
        Dict mit change_mask_base64 (PNG), Statistik, inference_time_ms.
    """
    t0 = time.perf_counter()

    # Bilder dekodieren
    raw_old = base64.b64decode(image_old_base64)
    raw_new = base64.b64decode(image_new_base64)
    img_old = np.array(Image.open(io.BytesIO(raw_old)).convert("RGB"))
    img_new = np.array(Image.open(io.BytesIO(raw_new)).convert("RGB"))

    # Ausrichten
    img_old, img_new = _align_images(img_old, img_new)

    # Aenderung berechnen
    result = _compute_change_mask(img_old, img_new, threshold=threshold)

    # Change-Mask als farbiges Overlay-Bild (RGBA)
    h, w = result["change_mask"].shape
    overlay = np.zeros((h, w, 4), dtype=np.uint8)
    mask = result["change_mask"]

    # Rot = Verschlechterung, Gruen = Verbesserung, Gelb = Neu
    overlay[mask == 1] = [255, 0, 0, 128]    # Rot, halbtransparent
    overlay[mask == 2] = [0, 255, 0, 128]    # Gruen
    overlay[mask == 3] = [255, 255, 0, 128]  # Gelb

    # Als PNG encodieren
    overlay_img = Image.fromarray(overlay, "RGBA")
    buf = io.BytesIO()
    overlay_img.save(buf, format="PNG")
    overlay_base64 = base64.b64encode(buf.getvalue()).decode("utf-8")

    elapsed_ms = (time.perf_counter() - t0) * 1000

    return {
        "change_overlay_base64": overlay_base64,
        "image_width": w,
        "image_height": h,
        "change_percent": result["change_percent"],
        "worse_percent": result["worse_percent"],
        "better_percent": result["better_percent"],
        "total_pixels": result["total_pixels"],
        "changed_pixels": result["changed_pixels"],
        "inference_time_ms": round(elapsed_ms, 1),
        "model_used": "pixel_diff",
    }
