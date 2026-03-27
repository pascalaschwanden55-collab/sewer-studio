"""Video Super Resolution (VSR) fuer alte PAL-Videos (768x576 → 1080p).

Backend-Praeferenz:
  1. Real-ESRGAN (pip install realesrgan basicsr) + Gewichte in models/RealESRGAN_x4plus.pth
  2. PIL Lanczos Bicubic (immer verfuegbar, kein ML)

Real-ESRGAN erhoht die Erkennungsrate bei alten, verrauschten Kanalvideos
deutlich: Ein 3-Pixel-Riss bei 576p wird bei 1080p zu 6 Pixeln.
"""

from __future__ import annotations

import logging
import threading
from pathlib import Path

import numpy as np
from PIL import Image

from ..config import settings

logger = logging.getLogger(__name__)

_vsr_model = None
_vsr_lock = threading.Lock()
_vsr_backend: str = "none"  # "realesrgan" | "bicubic" | "none"


def _try_load_realesrgan() -> object | None:
    """Versucht Real-ESRGAN zu laden. Gibt Modell-Objekt oder None zurueck."""
    try:
        from basicsr.archs.rrdbnet_arch import RRDBNet
        from realesrgan import RealESRGANer

        model = RRDBNet(
            num_in_ch=3, num_out_ch=3,
            num_feat=64, num_block=23, num_grow_ch=32,
            scale=4,
        )

        # Gewichte suchen
        candidates = [
            Path(settings.models_dir) / "RealESRGAN_x4plus.pth",
            Path(settings.models_dir) / "vsr" / "RealESRGAN_x4plus.pth",
            Path(settings.models_dir) / "realesrgan" / "RealESRGAN_x4plus.pth",
        ]
        weights_path = next((p for p in candidates if p.exists()), None)

        if weights_path is None:
            logger.info(
                "Real-ESRGAN: Gewichte nicht gefunden (erwartet in %s/RealESRGAN_x4plus.pth)",
                settings.models_dir,
            )
            return None

        upsampler = RealESRGANer(
            scale=4,
            model_path=str(weights_path),
            model=model,
            tile=256,      # Kacheln fuer VRAM-Management (verhindert OOM bei grossen Frames)
            tile_pad=10,
            pre_pad=0,
            half=True,     # FP16 fuer RTX 5090 (2x schneller, ~1GB VRAM)
        )
        logger.info("Real-ESRGAN geladen: %s (FP16=True)", weights_path.name)
        return upsampler

    except ImportError as e:
        logger.info("Real-ESRGAN nicht installiert (%s) — Bicubic-Fallback aktiv", e)
        return None
    except Exception as e:
        logger.warning("Real-ESRGAN Ladefehler: %s — Bicubic-Fallback aktiv", e)
        return None


def _get_vsr_model():
    """Lazy-Load des VSR-Modells (Thread-safe)."""
    global _vsr_model, _vsr_backend

    if _vsr_backend != "none":
        return _vsr_model

    with _vsr_lock:
        if _vsr_backend != "none":
            return _vsr_model

        if not settings.vsr_enabled:
            _vsr_backend = "bicubic"
            logger.info("VSR: Deaktiviert in Konfiguration — Bicubic-Fallback")
            return None

        model = _try_load_realesrgan()
        if model is not None:
            _vsr_model = model
            _vsr_backend = "realesrgan"
        else:
            _vsr_model = None
            _vsr_backend = "bicubic"

        return _vsr_model


def enhance_frame(
    img_rgb: np.ndarray,
    target_height: int = 1080,
    denoise: bool = True,
) -> np.ndarray:
    """Vergroessert einen niedrigaufloesenden Frame auf target_height.

    Args:
        img_rgb:       Eingabe-Frame als RGB numpy-Array (H, W, 3).
        target_height: Ziel-Aufloesung (Standard: 1080).
        denoise:       Rauschreduzierung anwenden (nur Real-ESRGAN).

    Returns:
        Verbesserter Frame als RGB numpy-Array.
    """
    h, w = img_rgb.shape[:2]

    # Kein Upscaling wenn bereits gross genug
    if h >= target_height:
        return img_rgb

    scale = target_height / h
    model = _get_vsr_model()

    if _vsr_backend == "realesrgan" and model is not None:
        try:
            import cv2

            # Real-ESRGAN erwartet BGR
            img_bgr = cv2.cvtColor(img_rgb, cv2.COLOR_RGB2BGR)
            enhanced_bgr, _ = model.enhance(img_bgr, outscale=scale)
            return cv2.cvtColor(enhanced_bgr, cv2.COLOR_BGR2RGB)

        except Exception as e:
            logger.warning("Real-ESRGAN Inferenz-Fehler: %s — Bicubic-Fallback", e)

    # Bicubic-Fallback via PIL
    new_w = int(w * scale)
    new_h = int(h * scale)
    pil = Image.fromarray(img_rgb)
    enhanced = pil.resize((new_w, new_h), Image.LANCZOS)
    return np.array(enhanced)


def should_enhance(img_rgb: np.ndarray) -> bool:
    """True wenn der Frame von VSR profitiert (unter vsr_min_resolution Hoehe)."""
    return img_rgb.shape[0] < settings.vsr_min_resolution


def get_vsr_status() -> dict:
    """VSR-Statusinformationen fuer den Health-Endpoint."""
    return {
        "vsr_enabled": settings.vsr_enabled,
        "vsr_backend": _vsr_backend,
        "vsr_min_resolution": settings.vsr_min_resolution,
    }
