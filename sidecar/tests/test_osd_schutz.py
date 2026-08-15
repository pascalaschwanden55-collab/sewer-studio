"""Sperrliste des OSD-Trainings (Spec Abschnitt 4.4).

Die 197 Goldbilder und ihre Haltungen duerfen in keiner Trainingsquelle
auftauchen. Sonst misst die Goldmessung am Ende sich selbst.
"""

import json
import sys
from pathlib import Path

import pytest

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_schutz


def _schreibe_satz(wurzel: Path, name: str, eintraege: list[dict]) -> None:
    satz = wurzel / name
    (satz / "frames").mkdir(parents=True)
    (satz / "manifest.json").write_text(
        json.dumps({"schema_version": 1, "name": name, "eintraege": eintraege}),
        encoding="utf-8")


def test_goldhash_ist_gesperrt(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461",
         "bild_sha256": "aa" * 32, "meter": 0.0},
    ])

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",))

    assert schutz.ist_gesperrt("aa" * 32, None) is True


def test_gegenrichtung_der_haltung_ist_gesperrt(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461",
         "bild_sha256": "aa" * 32, "meter": 0.0},
    ])

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",))

    # Dasselbe Rohr, andere Fahrtrichtung - muss ebenfalls gesperrt sein.
    assert schutz.ist_gesperrt("bb" * 32, "33461-36051") is True


def test_unbeteiligtes_bild_ist_frei(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461",
         "bild_sha256": "aa" * 32, "meter": 0.0},
    ])

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",))

    assert schutz.ist_gesperrt("cc" * 32, "10261-10262") is False


def test_fehlender_satz_bricht_ab(tmp_path):
    with pytest.raises(SystemExit):
        osd_schutz.lade_schutz(tmp_path, saetze=("gibt_es_nicht",))


def test_eintrag_ohne_hash_bricht_ab(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461", "meter": 0.0},
    ])

    with pytest.raises(SystemExit):
        osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",))
