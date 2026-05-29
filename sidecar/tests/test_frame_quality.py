"""Tests fuer das Frame-Quality-Gate (_is_frame_usable).

Kernpunkt: dunkle, aber inhaltlich gueltige Kanal-Frames duerfen NICHT mehr
verworfen werden, waehrend echtes Schwarz/Ueberbelichtung/Uniform weiter rausfaellt.
"""

import numpy as np
from PIL import Image

from sidecar.models.yolo_wrapper import _is_frame_usable


def _img(arr: np.ndarray) -> Image.Image:
    return Image.fromarray(arr.astype(np.uint8))


def _checkerboard(value_lo: int, value_hi: int, h: int = 64, w: int = 64) -> np.ndarray:
    base = np.indices((h, w)).sum(axis=0) % 2
    gray = np.where(base == 0, value_lo, value_hi)
    return np.stack([gray, gray, gray], axis=2)


def test_dim_but_textured_frame_is_usable():
    # Mittlere Helligkeit ~7 (frueher als "too_dark" verworfen) MIT Textur
    # -> typischer dunkler Kanal-Frame mit Inhalt, muss jetzt nutzbar sein.
    usable, reason = _is_frame_usable(_img(_checkerboard(2, 12)))
    assert usable, f"dunkler texturierter Frame faelschlich verworfen: {reason}"


def test_fully_black_frame_rejected():
    usable, reason = _is_frame_usable(_img(np.zeros((64, 64, 3))))
    assert not usable and reason == "too_dark"


def test_overexposed_frame_rejected():
    usable, reason = _is_frame_usable(_img(np.full((64, 64, 3), 255)))
    assert not usable and reason == "too_bright"


def test_uniform_frame_rejected():
    usable, reason = _is_frame_usable(_img(np.full((64, 64, 3), 100)))
    assert not usable and reason == "too_uniform"
