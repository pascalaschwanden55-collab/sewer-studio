"""Tests fuer die geometrische Bogen-Erkennung (analyze_bend).

Ein Bogen (VSA-KEK BCC) ist geometrisch: der dunkelste Bereich (Tunnelende/Fluchtpunkt)
verschiebt sich seitlich. Diese Logik ist die 1:1-Portierung des in C# getesteten
VanishingPointBendDetector (Application/Ai), damit der Sidecar das Bogen-Veto aus
DEMSELBEN Frame liefern kann, das er fuer SAM dekodiert.
"""

import numpy as np

from sidecar.models.bend_geometry import analyze_bend


def _frame_with_dark_spot(w, h, cx_norm, cy_norm, radius_norm=0.18):
    """Helligkeits-Matrix (0..255) mit dunklem Bereich (radialer Verlauf) bei (cx,cy)."""
    cx, cy, r = cx_norm * w, cy_norm * h, radius_norm * w
    ys, xs = np.mgrid[0:h, 0:w]
    d = np.sqrt((xs - cx) ** 2 + (ys - cy) ** 2)
    val = np.where(d >= r, 255.0, 255.0 * (d / r))
    return np.clip(val, 0, 255)


def test_gerades_rohr_zentral_ist_kein_bogen():
    frame = _frame_with_dark_spot(96, 72, 0.50, 0.50)
    r = analyze_bend(frame)
    assert r.is_bend is False
    assert -0.05 <= r.shift <= 0.05


def test_bogen_links_verschoben_ist_bogen():
    frame = _frame_with_dark_spot(96, 72, 0.37, 0.50)
    r = analyze_bend(frame)
    assert r.is_bend is True
    assert r.shift < 0


def test_bogen_rechts_verschoben_ist_bogen():
    frame = _frame_with_dark_spot(96, 72, 0.63, 0.50)
    r = analyze_bend(frame)
    assert r.is_bend is True
    assert r.shift > 0


def test_leicht_dezentral_unter_schwelle_kein_bogen():
    frame = _frame_with_dark_spot(96, 72, 0.58, 0.50)
    r = analyze_bend(frame)
    assert r.is_bend is False


def test_rgb_input_wird_zu_graustufen():
    # Sidecar liefert img_array als HxWx3 - analyze_bend muss RGB akzeptieren.
    gray = _frame_with_dark_spot(96, 72, 0.37, 0.50)
    rgb = np.stack([gray, gray, gray], axis=-1).astype(np.uint8)
    r = analyze_bend(rgb)
    assert r.is_bend is True
    assert r.shift < 0
