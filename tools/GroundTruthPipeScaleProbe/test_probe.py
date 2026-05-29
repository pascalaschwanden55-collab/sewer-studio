"""Mini-Test fuer die Probe-Rechnung (reine Arithmetik, kein Produktionscode)."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from probe import compute  # noqa: E402


def test_compute_known_example():
    # DN300, 1920px breit -> global Ø = 1344px; Rohrkanten 480..1440 -> local Ø = 960px
    r = compute({
        "file": "x", "image_width": 1920, "dn_mm": 300,
        "pipe_left_x": 480, "pipe_right_x": 1440,
        "damage_height_px": 60, "damage_known_mm": 45,
    })
    assert r["global_px_d"] == 1344.0
    assert r["local_px_d"] == 960.0
    assert r["global_pxmm"] == 0.2232      # 300/1344
    assert r["local_pxmm"] == 0.3125       # 300/960
    assert r["diff_pct"] == 40.0           # lokal 40% groesser
    # Schaden 60px: global ~13.4mm vs local ~18.8mm; bekannt 45mm
    assert r["damage_global_mm"] == 13.4
    assert r["damage_local_mm"] == 18.8


def test_compute_without_damage():
    r = compute({"file": "y", "image_width": 768, "dn_mm": 250,
                 "pipe_left_x": 150, "pipe_right_x": 600})
    assert "damage_global_mm" not in r
    assert r["local_px_d"] == 450.0
