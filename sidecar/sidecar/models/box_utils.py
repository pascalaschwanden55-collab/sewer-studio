"""Reine Geometrie-Helfer fuer Bounding-Boxen (ohne ML-Abhaengigkeiten)."""

from __future__ import annotations

from typing import Optional, Tuple


def clamp_box(
    x1: float, y1: float, x2: float, y2: float,
    width: int, height: int,
) -> Optional[Tuple[float, float, float, float]]:
    """Box in Min/Max-Ordnung bringen, auf [0,width]/[0,height] clampen und
    degenerierte (Null-Flaeche) Boxen verwerfen (-> None).

    Verhindert, dass aus dem Bild ragende oder leere DINO-Boxen an SAM gehen
    (Randartefakte / leere Masken / verschwendete Inferenz).
    """
    lo_x, hi_x = min(x1, x2), max(x1, x2)
    lo_y, hi_y = min(y1, y2), max(y1, y2)

    cx1 = max(0.0, min(lo_x, float(width)))
    cy1 = max(0.0, min(lo_y, float(height)))
    cx2 = max(0.0, min(hi_x, float(width)))
    cy2 = max(0.0, min(hi_y, float(height)))

    if cx2 <= cx1 or cy2 <= cy1:
        return None
    return (cx1, cy1, cx2, cy2)
