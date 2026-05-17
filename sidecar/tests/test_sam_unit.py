"""Pure-Function-Tests fuer SAM-Wrapper-Helfer.

Audit 2026-05-17: sichert die heutigen Codex-Patches gegen Regression.

``_clip_annulus_mask`` muss Float-Masken (Logits aus SAM2) zuverlaessig
binarisieren bevor das Bitwise-AND mit dem Annulus passiert. Vor dem
Codex-Patch lieferte ``mask & annulus`` auf Float-Array Garbage, der
Ring-Scan brachte 0 Masken pro Frame.

Diese Tests brauchen weder einen lebenden Sidecar noch GPU.
"""

from __future__ import annotations

import numpy as np

from sidecar.models.sam_wrapper import _clip_annulus_mask


def test_bool_input_bleibt_bool_und_wird_geclipped():
    """Bool-Maske: voll → Annulus-Form."""
    h, w = 100, 100
    mask = np.ones((h, w), dtype=bool)

    clipped = _clip_annulus_mask(mask, cx=50, cy=50, r_inner=20, r_outer=40)

    # Zentrum (dist=0): innerhalb r_inner → False
    assert bool(clipped[50, 50]) is False
    # Ecke (dist ≈ 70): ausserhalb r_outer → False
    assert bool(clipped[5, 5]) is False
    # Genau im Annulus (50,80): dist=30, 20 ≤ 30 ≤ 40 → True
    assert bool(clipped[50, 80]) is True


def test_float_input_normalisiert_0_bis_1_wird_an_0_5_schwelle_binarisiert():
    """Float [0, 1] → >= 0.5 ist True, vor dem Annulus-Clip."""
    h, w = 100, 100
    # Komplett 0.8 → alles aktiviert
    mask_high = np.full((h, w), 0.8, dtype=np.float32)
    # Komplett 0.3 → alles deaktiviert
    mask_low = np.full((h, w), 0.3, dtype=np.float32)

    clipped_high = _clip_annulus_mask(mask_high, cx=50, cy=50, r_inner=15, r_outer=45)
    clipped_low = _clip_annulus_mask(mask_low, cx=50, cy=50, r_inner=15, r_outer=45)

    # High: Annulus voll, alles drumherum leer
    assert bool(clipped_high[50, 80]) is True   # (dist=30) im Annulus
    assert bool(clipped_high[50, 50]) is False  # Zentrum, raus
    # Low: komplett leer (0.3 < 0.5)
    assert int(clipped_low.sum()) == 0


def test_float_unter_schwelle_komplett_leer():
    """Float-Maske mit allen Werten < 0.5 → 0 Masken-Pixel nach Clip."""
    h, w = 100, 100
    mask_float = np.full((h, w), 0.3, dtype=np.float32)

    clipped = _clip_annulus_mask(mask_float, cx=50, cy=50, r_inner=15, r_outer=45)

    assert int(clipped.sum()) == 0


def test_logits_mit_negativen_und_positiven_werten():
    """Logits (Range > 1.0): > 0.0 zaehlt als True."""
    h, w = 100, 100
    # Vollflaeche mit klaren positiven Logits
    mask_pos = np.full((h, w), 2.5, dtype=np.float32)
    # Vollflaeche mit negativen Logits
    mask_neg = np.full((h, w), -1.5, dtype=np.float32)

    clipped_pos = _clip_annulus_mask(mask_pos, cx=50, cy=50, r_inner=15, r_outer=45)
    clipped_neg = _clip_annulus_mask(mask_neg, cx=50, cy=50, r_inner=15, r_outer=45)

    # Positive Logits: ringfoermige Maske entsteht
    assert int(clipped_pos.sum()) > 0
    # Negative Logits: alles False
    assert int(clipped_neg.sum()) == 0


def test_dtype_wird_bool_nach_clip():
    """Ergebnis-Maske muss bool-kompatibel sein (fuer & und sum())."""
    h, w = 50, 50
    mask = np.full((h, w), 0.9, dtype=np.float32)

    clipped = _clip_annulus_mask(mask, cx=25, cy=25, r_inner=10, r_outer=20)

    # bool oder int (jedenfalls binaer-arithmetik-kompatibel)
    assert clipped.dtype == bool or clipped.dtype == np.bool_
