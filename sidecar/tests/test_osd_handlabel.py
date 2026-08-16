"""Handliste fuer harte OSD-Faelle (Spec: Stufe 2 nach dem gescheiterten
Stufe-1-Training).

Deckt die reine Bild->Boxen-Logik (pruefe_bild/lesung_scheitert), die
deterministische Auswahl (waehle_faelle), den Modus "queue" ueber main() und
den Modus "publizieren" ueber main() ab.
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest
from PIL import Image, ImageDraw

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_ernte
import osd_handlabel
from osd_schutz import Schutz
from sidecar import osd_meter


def _bild_mit_anzeige(text: str, groesse=(720, 576)) -> Image.Image:
    """Dunkles Bild mit heller Anzeige unten rechts - der SD-Normalfall."""
    bild = Image.new("RGB", groesse, (18, 18, 18))
    zeichner = ImageDraw.Draw(bild)
    zeichner.text((groesse[0] - 190, groesse[1] - 40), text, fill=(240, 240, 240))
    return bild


def _zonenbox(breite: int = 720, hoehe: int = 576) -> tuple[int, int]:
    links, oben, _r, _u = osd_meter.ZONEN["unten_rechts"]
    return round(links * breite) + 10, round(oben * hoehe) + 10


# ---------------------------------------------------------------------------
# lesung_scheitert() - reine Logik, drei Bedingungen, ODER-verknuepft.
# ---------------------------------------------------------------------------

def test_lesung_scheitert_bei_fragezeichen(monkeypatch):
    monkeypatch.setattr(osd_handlabel.osd_meter, "_zeichenfolge_ist_vollstaendig",
                        lambda _z: True)
    monkeypatch.setattr(osd_handlabel.osd_meter, "parse_meter", lambda *_a, **_k: 9.4)

    assert osd_handlabel.lesung_scheitert("9?4", "dunkel") is True


def test_lesung_scheitert_bei_unvollstaendiger_folge(monkeypatch):
    monkeypatch.setattr(osd_handlabel.osd_meter, "_zeichenfolge_ist_vollstaendig",
                        lambda _z: False)
    monkeypatch.setattr(osd_handlabel.osd_meter, "parse_meter", lambda *_a, **_k: 9.4)

    assert osd_handlabel.lesung_scheitert("094", "dunkel") is True


def test_lesung_scheitert_wenn_parse_meter_none_liefert(monkeypatch):
    monkeypatch.setattr(osd_handlabel.osd_meter, "_zeichenfolge_ist_vollstaendig",
                        lambda _z: True)
    monkeypatch.setattr(osd_handlabel.osd_meter, "parse_meter", lambda *_a, **_k: None)

    assert osd_handlabel.lesung_scheitert("LZ1:9.4m", "dunkel") is True


def test_lesung_scheitert_nicht_bei_vollstaendiger_lesung(monkeypatch):
    monkeypatch.setattr(osd_handlabel.osd_meter, "_zeichenfolge_ist_vollstaendig",
                        lambda _z: True)
    monkeypatch.setattr(osd_handlabel.osd_meter, "parse_meter", lambda *_a, **_k: 9.4)

    assert osd_handlabel.lesung_scheitert("LZ1:9.4m", "dunkel") is False


# ---------------------------------------------------------------------------
# pruefe_bild() - Boxenzahl-Schranke und Ausschluss bereits vollstaendiger
# Lesungen (die gehoeren der Lehrer-Ernte, nicht der Handliste).
# ---------------------------------------------------------------------------

def test_pruefe_bild_verwirft_weniger_als_vier_boxen(monkeypatch):
    import numpy as np

    bild = _bild_mit_anzeige("94")
    breite, hoehe = bild.size
    x0, y0 = _zonenbox(breite, hoehe)

    monkeypatch.setattr(osd_handlabel.osd_meter, "glyphenmaske",
                        lambda _b: (np.zeros((hoehe, breite), dtype="uint8"), "dunkel"))
    monkeypatch.setattr(osd_handlabel.osd_meter, "boxen_aus_maske",
                        lambda _m, _s: [(x0, y0, x0 + 12, y0 + 18),
                                        (x0 + 14, y0, x0 + 26, y0 + 18)])

    assert osd_handlabel.pruefe_bild(bild, None) is None


def test_pruefe_bild_verwirft_bereits_vollstaendige_lesung(monkeypatch):
    """Deckt sich mit der Lehrer-Ernte: keine Dopplung der Trainingsquelle."""
    import numpy as np

    bild = _bild_mit_anzeige("LZ1: 9.4m")
    breite, hoehe = bild.size
    x0, y0 = _zonenbox(breite, hoehe)
    boxen = [(x0 + i * 14, y0, x0 + i * 14 + 12, y0 + 18) for i in range(4)]

    monkeypatch.setattr(osd_handlabel.osd_meter, "glyphenmaske",
                        lambda _b: (np.zeros((hoehe, breite), dtype="uint8"), "dunkel"))
    monkeypatch.setattr(osd_handlabel.osd_meter, "boxen_aus_maske",
                        lambda _m, _s: boxen)
    folge = iter("LZ1m")
    monkeypatch.setattr(osd_handlabel.osd_meter, "klassifiziere",
                        lambda _g, _t: (next(folge), 0.9))
    monkeypatch.setattr(osd_handlabel.osd_meter, "_zeichenfolge_ist_vollstaendig",
                        lambda _z: True)
    monkeypatch.setattr(osd_handlabel.osd_meter, "parse_meter", lambda *_a, **_k: 9.4)

    assert osd_handlabel.pruefe_bild(bild, None) is None


def test_pruefe_bild_liefert_harten_fall(monkeypatch):
    """Genug Boxen, aber die Lesung scheitert - genau der gesuchte Fall."""
    import numpy as np

    bild = _bild_mit_anzeige("??1m")
    breite, hoehe = bild.size
    x0, y0 = _zonenbox(breite, hoehe)
    boxen = [(x0 + i * 14, y0, x0 + i * 14 + 12, y0 + 18) for i in range(4)]

    monkeypatch.setattr(osd_handlabel.osd_meter, "glyphenmaske",
                        lambda _b: (np.zeros((hoehe, breite), dtype="uint8"), "dunkel"))
    monkeypatch.setattr(osd_handlabel.osd_meter, "boxen_aus_maske",
                        lambda _m, _s: boxen)
    monkeypatch.setattr(osd_handlabel.osd_meter, "klassifiziere",
                        lambda _g, _t: ("", 0.0))  # liefert ueberall "?"
    monkeypatch.setattr(osd_handlabel.osd_meter, "_zeichenfolge_ist_vollstaendig",
                        lambda _z: False)

    ergebnis = osd_handlabel.pruefe_bild(bild, None)

    assert ergebnis is not None
    assert ergebnis.boxen == boxen
    assert ergebnis.stil == "dunkel"


# ---------------------------------------------------------------------------
# waehle_faelle() - hoechstens ein Bild je physischer Haltung, deterministisch.
# ---------------------------------------------------------------------------

def _kandidat(haltung: str, bild_pfad: str) -> dict:
    return {"haltung": haltung, "bild_pfad": bild_pfad,
            "bild_sha256": "aa" * 32, "boxen": [[0, 0, 1, 1]], "stil": "dunkel"}


def test_waehle_faelle_hoechstens_ein_bild_je_physischer_haltung():
    kandidaten = [
        _kandidat("10261-10262", "z.jpg"),
        _kandidat("10262-10261", "a.jpg"),  # Gegenrichtung, gleiche physische Haltung
        _kandidat("20001-20002", "m.jpg"),
    ]

    auswahl = osd_handlabel.waehle_faelle(kandidaten, 10, saat=0)

    haltungen = [eintrag["haltung"] for eintrag in auswahl]
    assert len(auswahl) == 2
    assert haltungen.count("10261-10262") + haltungen.count("10262-10261") == 1


def test_waehle_faelle_bevorzugt_kleinsten_bildpfad_je_haltung():
    kandidaten = [
        _kandidat("10261-10262", "z.jpg"),
        _kandidat("10261-10262", "a.jpg"),
    ]

    auswahl = osd_handlabel.waehle_faelle(kandidaten, 10, saat=0)

    assert len(auswahl) == 1
    assert auswahl[0]["bild_pfad"] == "a.jpg"


def test_waehle_faelle_ist_deterministisch_bei_gleicher_saat():
    kandidaten = [_kandidat(f"{i}0001-{i}0002", f"{i}.jpg") for i in range(20)]

    erste = osd_handlabel.waehle_faelle(kandidaten, 5, saat=0)
    zweite = osd_handlabel.waehle_faelle(kandidaten, 5, saat=0)

    assert erste == zweite


def test_waehle_faelle_lehnt_ungueltige_anzahl_ab():
    with pytest.raises(ValueError):
        osd_handlabel.waehle_faelle([_kandidat("1-2", "a.jpg")], 0, saat=0)


def test_fall_erzeugen_liefert_erwartete_form():
    fall = osd_handlabel.fall_erzeugen(
        "ab" * 32, "10261-10262", "D:\\OSD_Frames\\10261-10262\\a.jpg",
        [(1, 2, 3, 4)], "dunkel")

    assert fall == {
        "id": osd_ernte.bild_id("ab" * 32),
        "bild_sha256": "ab" * 32,
        "haltung": "10261-10262",
        "bild_pfad": "D:\\OSD_Frames\\10261-10262\\a.jpg",
        "boxen": [[1, 2, 3, 4]],
        "stil": "dunkel",
    }


# ---------------------------------------------------------------------------
# Modus "queue" ueber main(): Sperrliste, hoechstens ein Bild je Haltung.
# ---------------------------------------------------------------------------

def _harter_fall_immer():
    return osd_handlabel.HarterFall(
        [(1, 1, 5, 5), (6, 1, 10, 5), (11, 1, 15, 5), (16, 1, 20, 5)], "dunkel")


def test_main_queue_schliesst_geschuetzte_haltung_aus(tmp_path, monkeypatch, capsys):
    quelle = tmp_path / "OSD_Frames"
    (quelle / "10261-10262").mkdir(parents=True)
    Image.new("RGB", (64, 48), (10, 10, 10)).save(quelle / "10261-10262" / "a.jpg")
    (quelle / "30001-30002").mkdir(parents=True)
    Image.new("RGB", (64, 48), (20, 20, 20)).save(quelle / "30001-30002" / "b.jpg")

    schutz = Schutz(frozenset(), frozenset({"10261-10262"}))
    monkeypatch.setattr(osd_handlabel, "lade_schutz", lambda *_a, **_k: schutz)
    monkeypatch.setattr(osd_handlabel, "pruefe_bild", lambda *_a, **_k: _harter_fall_immer())

    ziel = tmp_path / "ziel"
    rc = osd_handlabel.main(
        ["queue", "--quelle", str(quelle), "--ziel", str(ziel), "--anzahl", "10"])

    assert rc == 0
    dokument = json.loads((ziel / "queue.json").read_text(encoding="utf-8"))
    haltungen = {fall["haltung"] for fall in dokument["faelle"]}
    assert "10261-10262" not in haltungen
    assert "30001-30002" in haltungen

    ausgabe = capsys.readouterr().out
    assert "Geschuetzt uebersprungen: 1" in ausgabe
    assert "ACHTUNG" in ausgabe


def test_main_queue_hoechstens_ein_bild_je_physischer_haltung(tmp_path, monkeypatch):
    quelle = tmp_path / "OSD_Frames"
    for ordner, dateiname in (("10261-10262", "a.jpg"), ("10262-10261", "b.jpg"),
                              ("30001-30002", "c.jpg")):
        (quelle / ordner).mkdir(parents=True)
        Image.new("RGB", (64, 48), (10, 10, 10)).save(quelle / ordner / dateiname)

    monkeypatch.setattr(osd_handlabel, "lade_schutz", lambda *_a, **_k: Schutz())
    monkeypatch.setattr(osd_handlabel, "pruefe_bild", lambda *_a, **_k: _harter_fall_immer())

    ziel = tmp_path / "ziel"
    osd_handlabel.main(
        ["queue", "--quelle", str(quelle), "--ziel", str(ziel), "--anzahl", "10"])

    dokument = json.loads((ziel / "queue.json").read_text(encoding="utf-8"))
    assert len(dokument["faelle"]) == 2
    assert dokument["schema"] == "osd_handlabel_queue_v1"
    for fall in dokument["faelle"]:
        assert len(fall["boxen"]) == 4


def test_main_queue_ist_deterministisch_bei_gleicher_saat(tmp_path, monkeypatch):
    quelle = tmp_path / "OSD_Frames"
    for i in range(6):
        ordner = quelle / f"{i}0001-{i}0002"
        ordner.mkdir(parents=True)
        Image.new("RGB", (64, 48), (i, i, i)).save(ordner / "a.jpg")

    monkeypatch.setattr(osd_handlabel, "lade_schutz", lambda *_a, **_k: Schutz())
    monkeypatch.setattr(osd_handlabel, "pruefe_bild", lambda *_a, **_k: _harter_fall_immer())

    ziel1 = tmp_path / "ziel1"
    ziel2 = tmp_path / "ziel2"
    osd_handlabel.main(["queue", "--quelle", str(quelle), "--ziel", str(ziel1),
                        "--anzahl", "3", "--saat", "7"])
    osd_handlabel.main(["queue", "--quelle", str(quelle), "--ziel", str(ziel2),
                        "--anzahl", "3", "--saat", "7"])

    eins = json.loads((ziel1 / "queue.json").read_text(encoding="utf-8"))
    zwei = json.loads((ziel2 / "queue.json").read_text(encoding="utf-8"))
    assert [f["haltung"] for f in eins["faelle"]] == [f["haltung"] for f in zwei["faelle"]]


def test_main_queue_verweigert_ueberschreiben(tmp_path, monkeypatch):
    quelle = tmp_path / "OSD_Frames"
    (quelle / "10261-10262").mkdir(parents=True)
    Image.new("RGB", (64, 48), (10, 10, 10)).save(quelle / "10261-10262" / "a.jpg")
    ziel = tmp_path / "ziel"
    ziel.mkdir()

    with pytest.raises(SystemExit):
        osd_handlabel.main(["queue", "--quelle", str(quelle), "--ziel", str(ziel)])


# ---------------------------------------------------------------------------
# Modus "publizieren" ueber main(): Schema osd_ernte_v1, Hash-/Vollstaendig-
# keitspruefung, Zeichenwahrheit auch ohne gueltigen Meterwert.
# ---------------------------------------------------------------------------

def _queue_und_review(tmp_path: Path, boxen: list[tuple[int, int, int, int]],
                      zeichenfolge: str, aktion: str = "uebernommen",
                      queue_sha_ueberschreiben: str | None = None):
    quelle_ordner = tmp_path / "quelle" / "10261-10262"
    quelle_ordner.mkdir(parents=True)
    bild_pfad = quelle_ordner / "a.jpg"
    _bild_mit_anzeige("dummy").save(bild_pfad, quality=95)
    bild_sha256 = hashlib.sha256(bild_pfad.read_bytes()).hexdigest()
    fall_id = bild_sha256[:16]

    queue_ordner = tmp_path / "queue"
    queue_ordner.mkdir()
    queue_doc = {
        "schema": "osd_handlabel_queue_v1",
        "erzeugt_utc": "2026-08-16T00:00:00+00:00",
        "quelle": str(quelle_ordner.parent),
        "saat": 0,
        "faelle": [{
            "id": fall_id, "bild_sha256": bild_sha256, "haltung": "10261-10262",
            "bild_pfad": str(bild_pfad), "boxen": [list(box) for box in boxen],
            "stil": "dunkel",
        }],
    }
    queue_pfad = queue_ordner / "queue.json"
    queue_pfad.write_text(json.dumps(queue_doc), encoding="utf-8")
    queue_sha256 = hashlib.sha256(queue_pfad.read_bytes()).hexdigest()

    entscheidung: dict = {"aktion": aktion}
    if aktion == "uebernommen":
        entscheidung["zeichenfolge"] = zeichenfolge

    review_pfad = tmp_path / "review.json"
    review_pfad.write_text(json.dumps({
        "schema": "osd_handlabel_review_v1",
        "reviewer": "Pascal",
        "queue_sha256": queue_sha_ueberschreiben or queue_sha256,
        "entscheidungen": {fall_id: entscheidung},
    }), encoding="utf-8")

    return queue_ordner, review_pfad, fall_id, bild_sha256


def test_publizieren_erzeugt_osd_ernte_v1_eintrag(tmp_path):
    x0, y0 = _zonenbox()
    boxen = [(x0, y0, x0 + 12, y0 + 18), (x0 + 14, y0, x0 + 26, y0 + 18)]
    queue_ordner, review_pfad, fall_id, bild_sha256 = _queue_und_review(
        tmp_path, boxen, "94")

    ziel = tmp_path / "ziel"
    rc = osd_handlabel.main([
        "publizieren", "--queue", str(queue_ordner), "--review", str(review_pfad),
        "--ziel", str(ziel)])

    assert rc == 0
    dokument = json.loads((ziel / "eintraege.json").read_text(encoding="utf-8"))
    assert dokument["schema"] == "osd_ernte_v1"
    assert len(dokument["eintraege"]) == 1
    eintrag = dokument["eintraege"][0]
    assert eintrag["id"] == fall_id
    assert eintrag["bild_sha256"] == bild_sha256
    assert eintrag["haltung"] == "10261-10262"
    assert eintrag["zeichenfolge"] == "94"
    assert eintrag["meter"] == 9.4
    assert set(eintrag) == {"id", "bild_sha256", "ausschnitt_sha256", "haltung",
                            "zeichenfolge", "meter"}
    assert (ziel / "bilder" / f"{fall_id}.png").is_file()
    assert (ziel / "labels" / f"{fall_id}.txt").is_file()


def test_publizieren_behaelt_zeichenwahrheit_ohne_meterwert(tmp_path, capsys):
    x0, y0 = _zonenbox()
    boxen = [(x0, y0, x0 + 12, y0 + 18), (x0 + 14, y0, x0 + 26, y0 + 18)]
    queue_ordner, review_pfad, fall_id, _ = _queue_und_review(tmp_path, boxen, "LL")

    ziel = tmp_path / "ziel"
    osd_handlabel.main([
        "publizieren", "--queue", str(queue_ordner), "--review", str(review_pfad),
        "--ziel", str(ziel)])

    dokument = json.loads((ziel / "eintraege.json").read_text(encoding="utf-8"))
    eintrag = dokument["eintraege"][0]
    assert eintrag["zeichenfolge"] == "LL"
    assert eintrag["meter"] is None

    ausgabe = capsys.readouterr().out
    assert "ohne gueltigen Meterwert" in ausgabe


def test_publizieren_ueberspringt_unleserlich_und_boxen_passen_nicht(tmp_path):
    x0, y0 = _zonenbox()
    boxen = [(x0, y0, x0 + 12, y0 + 18)]
    queue_ordner, review_pfad, _fall_id, _ = _queue_und_review(
        tmp_path, boxen, "", aktion="unleserlich")

    ziel = tmp_path / "ziel"
    osd_handlabel.main([
        "publizieren", "--queue", str(queue_ordner), "--review", str(review_pfad),
        "--ziel", str(ziel)])

    dokument = json.loads((ziel / "eintraege.json").read_text(encoding="utf-8"))
    assert dokument["eintraege"] == []


def test_publizieren_verweigert_bei_unvollstaendiger_review(tmp_path):
    x0, y0 = _zonenbox()
    boxen = [(x0, y0, x0 + 12, y0 + 18)]
    queue_ordner, review_pfad, _fall_id, _ = _queue_und_review(tmp_path, boxen, "9")

    # Entscheidungen leeren -> unvollstaendige Review.
    review = json.loads(review_pfad.read_text(encoding="utf-8"))
    review["entscheidungen"] = {}
    review_pfad.write_text(json.dumps(review), encoding="utf-8")

    ziel = tmp_path / "ziel"
    with pytest.raises(SystemExit):
        osd_handlabel.main([
            "publizieren", "--queue", str(queue_ordner), "--review", str(review_pfad),
            "--ziel", str(ziel)])


def test_publizieren_verweigert_bei_falschem_queue_hash(tmp_path):
    x0, y0 = _zonenbox()
    boxen = [(x0, y0, x0 + 12, y0 + 18)]
    queue_ordner, review_pfad, _fall_id, _ = _queue_und_review(
        tmp_path, boxen, "9", queue_sha_ueberschreiben="ff" * 32)

    ziel = tmp_path / "ziel"
    with pytest.raises(SystemExit):
        osd_handlabel.main([
            "publizieren", "--queue", str(queue_ordner), "--review", str(review_pfad),
            "--ziel", str(ziel)])


def test_publizieren_verweigert_bei_zeichenzahl_ungleich_boxenzahl(tmp_path):
    x0, y0 = _zonenbox()
    boxen = [(x0, y0, x0 + 12, y0 + 18), (x0 + 14, y0, x0 + 26, y0 + 18)]
    # Review von Hand manipuliert: nur ein Zeichen fuer zwei Boxen.
    queue_ordner, review_pfad, fall_id, _ = _queue_und_review(tmp_path, boxen, "9")
    review = json.loads(review_pfad.read_text(encoding="utf-8"))
    review["entscheidungen"][fall_id]["zeichenfolge"] = "9"
    review_pfad.write_text(json.dumps(review), encoding="utf-8")

    ziel = tmp_path / "ziel"
    with pytest.raises(SystemExit):
        osd_handlabel.main([
            "publizieren", "--queue", str(queue_ordner), "--review", str(review_pfad),
            "--ziel", str(ziel)])


def test_publizieren_verweigert_bei_unbekanntem_zeichen(tmp_path):
    x0, y0 = _zonenbox()
    boxen = [(x0, y0, x0 + 12, y0 + 18)]
    queue_ordner, review_pfad, fall_id, _ = _queue_und_review(tmp_path, boxen, "9")
    review = json.loads(review_pfad.read_text(encoding="utf-8"))
    review["entscheidungen"][fall_id]["zeichenfolge"] = "X"
    review_pfad.write_text(json.dumps(review), encoding="utf-8")

    ziel = tmp_path / "ziel"
    with pytest.raises(SystemExit):
        osd_handlabel.main([
            "publizieren", "--queue", str(queue_ordner), "--review", str(review_pfad),
            "--ziel", str(ziel)])


if __name__ == "__main__":
    raise SystemExit(pytest.main([__file__, "-v"]))
