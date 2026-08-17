"""Reine Auswertungslogik der OSD-Kettenmessung."""

from __future__ import annotations

import sys
from pathlib import Path

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_kettenmessung


def _satz(name, richtig, falsch, nicht_gelesen, faelle):
    return {
        "satz": name,
        "bilder": richtig + falsch + nicht_gelesen,
        "richtig": richtig,
        "falsch": falsch,
        "nicht_gelesen": nicht_gelesen,
        "faelle": faelle,
    }


def test_vergleich_zaehlt_nur_neue_ergebnisse_aus_basis_luecken():
    basis = _satz("probe", 1, 1, 2, [
        {"datei": "a.jpg", "zustand": "richtig"},
        {"datei": "b.jpg", "zustand": "falsch"},
        {"datei": "c.jpg", "zustand": "nicht_gelesen"},
        {"datei": "d.jpg", "zustand": "nicht_gelesen"},
    ])
    kette = _satz("probe", 2, 2, 0, [
        {"datei": "a.jpg", "zustand": "richtig"},
        {"datei": "b.jpg", "zustand": "falsch"},
        {"datei": "c.jpg", "zustand": "richtig"},
        {"datei": "d.jpg", "zustand": "falsch"},
    ])

    vergleich = osd_kettenmessung.vergleiche_saetze(basis, kette)

    assert vergleich["neue_richtige"] == 1
    assert vergleich["neue_falsche"] == 1


def test_laufzeitstatistik_trennt_kaltstart_von_warmen_werten():
    statistik = osd_kettenmessung.laufzeit_statistik([100.0, 10.0, 20.0])

    assert statistik == {
        "anzahl": 3,
        "mittel_ms": 43.33,
        "median_ms": 20.0,
        "p95_ms": 92.0,
        "maximum_ms": 100.0,
    }


def test_kandidaten_pin_entspricht_dem_gemessenen_v2_gewicht():
    from sidecar.models import osd_model_wrapper

    assert osd_model_wrapper.KANDIDAT_ID == "osd_zeichen_c668e35d59cb"
    assert osd_model_wrapper.GEWICHT_SHA256 == (
        "c668e35d59cb4feba82b60b857663a11ac6f493104d03bf1b0414103a4a75845")
    assert osd_model_wrapper.SCHWELLE == 0.25
