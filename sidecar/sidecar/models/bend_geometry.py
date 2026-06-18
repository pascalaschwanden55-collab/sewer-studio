"""Geometrische Bogen-Erkennung (VSA-KEK BCC) ueber den Fluchtpunkt.

1:1-Portierung des in C# getesteten VanishingPointBendDetector
(src/AuswertungPro.Next.Application/Ai/VanishingPointBendDetector.cs):
Der dunkelste Bereich eines Kanal-Frames ist das Rohr-Innere/Tunnelende. Bei geradem
Rohr liegt er zentral, bei einem Bogen seitlich verschoben. Robuster als DINO/SAM3-
Textlabels (die Boegen nicht von "infiltration"/geradem Rohr trennen koennen) und
ohne Training/VRAM.

Laeuft im Sidecar auf DEMSELBEN img_array, das fuer SAM dekodiert wird - keine
zweite Bilddekodierung.
"""
from __future__ import annotations

from dataclasses import dataclass

import numpy as np

# Anteil der dunkelsten Pixel, der als Tunnelende gilt (empirisch 15%).
DARKEST_FRACTION = 0.15
# Schwelle der horizontalen Fluchtpunkt-Verschiebung, ab der ein Bogen vorliegt.
# Empirisch an echten Frames: gerades Rohr ~0.00, Bogen |dx|>=0.13. 0.12 toleriert
# leicht schiefe Kameras, erkennt aber echte Boegen.
BEND_SHIFT_THRESHOLD = 0.12


@dataclass(frozen=True)
class BendResult:
    is_bend: bool
    shift: float      # horizontale Verschiebung des Fluchtpunkts von der Mitte (-0.5..+0.5)
    vanish_x: float   # Fluchtpunkt X normiert (0..1)
    vanish_y: float   # Fluchtpunkt Y normiert (0..1)


def analyze_bend(image) -> BendResult:
    """Analysiert ein Bild (Graustufen HxW oder RGB HxWx3, 0..255) und bestimmt den
    Fluchtpunkt (Schwerpunkt der dunkelsten Pixel) sowie ob ein Bogen vorliegt."""
    arr = np.asarray(image, dtype=np.float64)
    if arr.ndim == 3:
        # RGB -> Luminanz (gleiche Gewichtung wie ToLuminance im Decode reicht hier)
        arr = arr[..., :3].mean(axis=2)
    if arr.ndim != 2 or arr.size == 0:
        return BendResult(False, 0.0, 0.5, 0.5)

    h, w = arr.shape
    flat = arr.ravel()
    # Schwellwert = Grenze der dunkelsten DARKEST_FRACTION aller Pixel.
    idx = min(flat.size - 1, int(flat.size * DARKEST_FRACTION))
    threshold = np.partition(flat, idx)[idx]

    # Schwerpunkt der dunkelsten Pixel. Striktes "<" gegen den Schwellwert, damit eine
    # flache Helligkeitsverteilung nicht das ganze Bild erfasst; Fallback "<=" nur,
    # falls "<" gar keine Pixel liefert.
    mask = arr < threshold
    if not mask.any():
        mask = arr <= threshold
    if not mask.any():
        return BendResult(False, 0.0, 0.5, 0.5)

    ys, xs = np.nonzero(mask)
    vanish_x = (xs.mean()) / w
    vanish_y = (ys.mean()) / h
    shift = vanish_x - 0.5
    is_bend = abs(shift) >= BEND_SHIFT_THRESHOLD
    return BendResult(bool(is_bend), float(shift), float(vanish_x), float(vanish_y))
