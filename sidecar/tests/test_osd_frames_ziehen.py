"""Bilder aus den Archivvideos ziehen (2026-08-16).

WOZU DIESER SCHRITT UEBERHAUPT
Die Ernte sucht die Haltungsnummer im direkten Elternordner. Kein vorhandener
Bildbestand ist so abgelegt: gold_frames sortiert nach Schadenscode, der
OSD-Wahrheitsbestand traegt die Haltung im Dateinamen. Bei beiden konnte der
Gegenrichtungsschutz deshalb nicht greifen (gemessen: 300 von 300 Bildern ohne
erkennbare Haltung). Erst Bilder, die nach Haltung abgelegt sind, machen ihn
wirksam.

Das Kundenarchiv wird nur gelesen. Das Ziel darf NICHT unter dem Kundenbestand
liegen - dieselbe Regel wie in osd_wahrheit_aus_protokoll.py.
"""

import sys
from pathlib import Path

import pytest
from PIL import Image

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_frames_ziehen as zieh
from osd_schutz import Schutz


def test_ziel_unter_dem_kundenbestand_wird_abgewiesen(tmp_path):
    quelle = tmp_path / "Haltungen"
    quelle.mkdir()

    with pytest.raises(SystemExit):
        zieh.pruefe_ziel(quelle, quelle / "frames")


def test_ziel_gleich_dem_kundenbestand_wird_abgewiesen(tmp_path):
    quelle = tmp_path / "Haltungen"
    quelle.mkdir()

    with pytest.raises(SystemExit):
        zieh.pruefe_ziel(quelle, quelle)


def test_nachbarordner_als_ziel_ist_erlaubt(tmp_path):
    quelle = tmp_path / "Haltungen"
    quelle.mkdir()

    zieh.pruefe_ziel(quelle, tmp_path / "OSD_Frames")


def test_gesperrte_haltung_wird_nicht_gezogen(tmp_path):
    quelle = tmp_path / "Haltungen"
    (quelle / "10261-10262").mkdir(parents=True)
    (quelle / "10261-10262" / "v.mpg").write_bytes(b"kein echtes Video")
    schutz = Schutz(frozenset(), frozenset({"10261-10262"}))

    zaehler = zieh.ziehe_alles(quelle, tmp_path / "ziel", schutz, proben=3,
                               leser=_leser_der_nie_gerufen_werden_darf)

    assert zaehler["gesperrt"] == 1
    assert zaehler["gezogen"] == 0
    assert not (tmp_path / "ziel" / "10261-10262").exists()


def test_gegenrichtung_einer_gesperrten_haltung_wird_nicht_gezogen(tmp_path):
    quelle = tmp_path / "Haltungen"
    (quelle / "10262-10261").mkdir(parents=True)
    (quelle / "10262-10261" / "v.mpg").write_bytes(b"kein echtes Video")
    schutz = Schutz(frozenset(), frozenset({"10261-10262"}))

    zaehler = zieh.ziehe_alles(quelle, tmp_path / "ziel", schutz, proben=3,
                               leser=_leser_der_nie_gerufen_werden_darf)

    assert zaehler["gesperrt"] == 1


def test_ordner_ohne_erkennbare_haltung_wird_gezaehlt_und_uebersprungen(tmp_path):
    quelle = tmp_path / "Haltungen"
    (quelle / "irgendwas").mkdir(parents=True)
    (quelle / "irgendwas" / "v.mpg").write_bytes(b"kein echtes Video")

    zaehler = zieh.ziehe_alles(quelle, tmp_path / "ziel", Schutz(), proben=3,
                               leser=_leser_der_nie_gerufen_werden_darf)

    assert zaehler["ohne_haltung"] == 1
    assert zaehler["gezogen"] == 0


def test_freie_haltung_landet_im_ordner_mit_ihrem_namen(tmp_path):
    quelle = tmp_path / "Haltungen"
    (quelle / "06.24341-35625").mkdir(parents=True)
    (quelle / "06.24341-35625" / "20220522_x.mpg").write_bytes(b"kein echtes Video")
    ziel = tmp_path / "ziel"

    zaehler = zieh.ziehe_alles(quelle, ziel, Schutz(), proben=3, leser=_dreibilder)

    assert zaehler["gezogen"] == 3
    dateien = sorted(p.name for p in (ziel / "06.24341-35625").iterdir())
    assert dateien == ["20220522_x_000.jpg", "20220522_x_001.jpg", "20220522_x_002.jpg"]


def test_zweiter_lauf_zieht_nichts_neu(tmp_path):
    quelle = tmp_path / "Haltungen"
    (quelle / "06.24341-35625").mkdir(parents=True)
    (quelle / "06.24341-35625" / "v.mpg").write_bytes(b"kein echtes Video")
    ziel = tmp_path / "ziel"

    zieh.ziehe_alles(quelle, ziel, Schutz(), proben=3, leser=_dreibilder)
    zweiter = zieh.ziehe_alles(quelle, ziel, Schutz(), proben=3, leser=_dreibilder)

    assert zweiter["gezogen"] == 0
    assert zweiter["schon_da"] == 1


def test_defektes_video_bricht_den_lauf_nicht_ab(tmp_path):
    quelle = tmp_path / "Haltungen"
    for name in ("06.1-2", "06.3-4"):
        (quelle / name).mkdir(parents=True)
        (quelle / name / "v.mpg").write_bytes(b"kein echtes Video")

    def leser(video, proben):
        if "06.1-2" in str(video):
            raise ValueError("Video kaputt")
        return _dreibilder(video, proben)

    zaehler = zieh.ziehe_alles(quelle, tmp_path / "ziel", Schutz(), proben=3, leser=leser)

    assert zaehler["fehlgeschlagen"] == 1
    assert zaehler["gezogen"] == 3


def _dreibilder(video, proben):
    return [Image.new("RGB", (720, 576), (20, 30, 40)) for _ in range(proben)]


def _leser_der_nie_gerufen_werden_darf(video, proben):
    raise AssertionError(
        f"Fuer {video} haette gar kein Bild gelesen werden duerfen - "
        "der Schutz oder die Haltungspruefung hat nicht gegriffen.")
