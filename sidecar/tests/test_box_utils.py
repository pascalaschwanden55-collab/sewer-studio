"""Tests fuer clamp_box: Box-Ordnung, Clamping auf Bildgrenzen, Degenerate-Verwurf."""

from sidecar.models.box_utils import clamp_box


def test_normal_box_unchanged():
    assert clamp_box(20, 30, 60, 70, 100, 100) == (20, 30, 60, 70)


def test_inverted_corners_reordered():
    assert clamp_box(60, 70, 20, 30, 100, 100) == (20, 30, 60, 70)


def test_out_of_bounds_clamped():
    assert clamp_box(-10, 0, 120, 50, 100, 100) == (0, 0, 100, 50)


def test_degenerate_zero_area_returns_none():
    # x1 == x2 -> keine Flaeche
    assert clamp_box(50, 20, 50, 70, 100, 100) is None


def test_fully_out_of_image_returns_none():
    # komplett links ausserhalb -> nach Clamp Null-Breite
    assert clamp_box(-50, 10, -10, 80, 100, 100) is None
