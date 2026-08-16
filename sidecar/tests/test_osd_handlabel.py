"""Handliste fuer harte OSD-Faelle (Spec: Rework nach dem gescheiterten
ersten Pruefplatz-Entwurf, 2026-08-16).

Deckt pruefe_bild() (der Grund fuer die Aufnahme in die Handliste - die
vollstaendige Vorlagenlesung scheitert, "jeder Grund zaehlt"),
zeichen_in_kasten() (die eigentliche neue Zeichenfindung im eng gezogenen
Kasten), die deterministische Auswahl (waehle_faelle), den Modus "queue" ueber
main() und den Modus "publizieren" ueber main() ab.
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest
from PIL import Image, ImageDraw, ImageFont

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
# pruefe_bild() - "hart" heisst: osd_meter.lese_meter() liefert keinen
# Meterwert, jeder Grund zaehlt. Ob dabei Boxen gefunden wurden, ist bewusst
# KEIN Kriterium mehr (das war der kaputte erste Entwurf, siehe Moduldocstring).
# ---------------------------------------------------------------------------

def test_pruefe_bild_liefert_none_bei_erfolgreicher_lesung(monkeypatch):
    monkeypatch.setattr(osd_handlabel.osd_meter, "lese_meter",
                        lambda *_a, **_k: {"meter": 9.4, "stil": "dunkel"})

    ergebnis = osd_handlabel.pruefe_bild(_Bild(), None)

    assert ergebnis is None


def test_pruefe_bild_liefert_harten_fall_wenn_lesung_scheitert(monkeypatch):
    monkeypatch.setattr(osd_handlabel.osd_meter, "lese_meter",
                        lambda *_a, **_k: {"meter": None, "stil": "hell"})

    ergebnis = osd_handlabel.pruefe_bild(_Bild(), None)

    assert ergebnis is not None
    assert ergebnis.stil == "hell"


def test_pruefe_bild_scheitert_auch_wenn_tesseract_rueckfall_nichts_liefert(monkeypatch):
    """Der komplette Vorlagenleser (inkl. Tesseract-Rueckfaellen) muss
    scheitern, nicht nur der reine Vorlagenweg - "jeder Grund zaehlt"."""
    monkeypatch.setattr(osd_handlabel.osd_meter, "lese_meter",
                        lambda *_a, **_k: {
                            "meter": None, "stil": "dunkel_video",
                            "leseweg": None, "tesseract_text": ""})

    ergebnis = osd_handlabel.pruefe_bild(_Bild(), None)

    assert ergebnis is not None
    assert ergebnis.stil == "dunkel_video"


def _Bild() -> Image.Image:
    return Image.new("RGB", (64, 48), (10, 10, 10))


# ---------------------------------------------------------------------------
# zeichen_in_kasten() - die eigentliche neue Zeichenfindung. Arbeitet nur auf
# einem eng gezogenen Kasten; siehe Docstring der Funktion fuer die Messung
# gegen ein echtes Archivbild (Haltung 43661-44201), die diese Konstruktion
# begruendet.
# ---------------------------------------------------------------------------

def _schrift(groesse: int = 24) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", groesse)


def _gerendertes_bild(text: str, hell_auf_dunkel: bool = True,
                      groesse=(220, 70), position=(20, 15)) -> tuple[Image.Image, tuple[int, int, int, int]]:
    """Rendert `text` und liefert (Bild, eng gezogener Kasten mit 2px Rand)."""
    hintergrund = (15, 15, 15) if hell_auf_dunkel else (220, 220, 220)
    vordergrund = (230, 230, 230) if hell_auf_dunkel else (20, 20, 20)
    bild = Image.new("RGB", groesse, hintergrund)
    zeichner = ImageDraw.Draw(bild)
    schrift = _schrift()
    zeichner.text(position, text, fill=vordergrund, font=schrift)
    bbox = zeichner.textbbox(position, text, font=schrift)
    kasten = (bbox[0] - 2, bbox[1] - 2, bbox[2] + 2, bbox[3] + 2)
    return bild, kasten


def test_zeichen_in_kasten_zaehlt_zeichen_korrekt():
    bild, kasten = _gerendertes_bild("9.42m")

    boxen = osd_handlabel.zeichen_in_kasten(bild, kasten)

    assert len(boxen) == 5  # '9', '.', '4', '2', 'm'


def test_zeichen_in_kasten_leerer_kasten_liefert_leere_liste():
    bild, _kasten = _gerendertes_bild("9.42m")

    assert osd_handlabel.zeichen_in_kasten(bild, (50, 10, 50, 30)) == []


def test_zeichen_in_kasten_kasten_ausserhalb_des_bilds_liefert_leere_liste():
    bild, _kasten = _gerendertes_bild("9.42m")

    assert osd_handlabel.zeichen_in_kasten(bild, (1000, 1000, 1100, 1100)) == []


def test_zeichen_in_kasten_null_hoehe_liefert_leere_liste():
    bild, kasten = _gerendertes_bild("9.42m")
    x0, y0, x1, _y1 = kasten

    assert osd_handlabel.zeichen_in_kasten(bild, (x0, y0, x1, y0)) == []


def test_zeichen_in_kasten_verliert_das_schmale_dezimalzeichen_nicht():
    bild, kasten = _gerendertes_bild("9.4")

    boxen = osd_handlabel.zeichen_in_kasten(bild, kasten)

    assert len(boxen) == 3
    breiten = [x1 - x0 for (x0, y0, x1, y1) in boxen]
    # Der Punkt (mittleres Zeichen) ist deutlich schmaler als beide Ziffern.
    assert breiten[1] < breiten[0]
    assert breiten[1] < breiten[2]


def test_zeichen_in_kasten_liefert_vollbildkoordinaten_sortiert_von_links():
    # Text NICHT bei (0,0), sondern mit echtem Versatz - full-image-Koordinaten
    # duerfen sich nicht mit kastenlokalen Koordinaten verwechseln lassen.
    bild, kasten = _gerendertes_bild("9.42m", groesse=(400, 200), position=(150, 90))

    boxen = osd_handlabel.zeichen_in_kasten(bild, kasten)

    assert len(boxen) == 5
    x0_werte = [b[0] for b in boxen]
    assert x0_werte == sorted(x0_werte)
    # Alle Boxen liegen im erwarteten Vollbildbereich (nahe der Textposition),
    # nicht bei (0,0) - haetten wir kastenlokale statt Vollbildkoordinaten
    # zurueckgegeben, laegen sie faelschlich am Bildursprung.
    assert all(b[0] >= 140 for b in boxen)


def test_zeichen_in_kasten_erkennt_helle_zeichen_auf_dunklem_grund():
    bild, kasten = _gerendertes_bild("0.00", hell_auf_dunkel=True)

    boxen = osd_handlabel.zeichen_in_kasten(bild, kasten)

    assert len(boxen) == 4


def test_zeichen_in_kasten_erkennt_dunkle_zeichen_auf_hellem_kasten():
    bild, kasten = _gerendertes_bild("0.00", hell_auf_dunkel=False)

    boxen = osd_handlabel.zeichen_in_kasten(bild, kasten)

    assert len(boxen) == 4


# ---------------------------------------------------------------------------
# waehle_faelle() - hoechstens ein Bild je physischer Haltung, deterministisch.
# Unveraendert durch den Rework: haengt nicht von Boxen ab.
# ---------------------------------------------------------------------------

def _kandidat(haltung: str, bild_pfad: str) -> dict:
    return {"haltung": haltung, "bild_pfad": bild_pfad,
            "bild_sha256": "aa" * 32, "stil": "dunkel"}


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
        "ab" * 32, "10261-10262", "D:\\OSD_Frames\\10261-10262\\a.jpg", "dunkel")

    assert fall == {
        "id": osd_ernte.bild_id("ab" * 32),
        "bild_sha256": "ab" * 32,
        "haltung": "10261-10262",
        "bild_pfad": "D:\\OSD_Frames\\10261-10262\\a.jpg",
        "stil": "dunkel",
    }
    assert "boxen" not in fall


# ---------------------------------------------------------------------------
# Modus "queue" ueber main(): Sperrliste, hoechstens ein Bild je Haltung.
# Queue-Eintraege tragen KEINE Boxen mehr.
# ---------------------------------------------------------------------------

def _harter_fall_immer():
    return osd_handlabel.HarterFall("dunkel")


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
        assert "boxen" not in fall
        assert fall["stil"] == "dunkel"


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


def test_main_queue_uebernimmt_nur_bilder_mit_scheiternder_lesung(tmp_path, monkeypatch):
    """pruefe_bild() entscheidet allein ueber osd_meter.lese_meter() -
    kein Boxen-Kriterium mehr."""
    quelle = tmp_path / "OSD_Frames"
    (quelle / "10261-10262").mkdir(parents=True)
    Image.new("RGB", (64, 48), (10, 10, 10)).save(quelle / "10261-10262" / "a.jpg")
    (quelle / "30001-30002").mkdir(parents=True)
    Image.new("RGB", (64, 48), (20, 20, 20)).save(quelle / "30001-30002" / "b.jpg")

    monkeypatch.setattr(osd_handlabel, "lade_schutz", lambda *_a, **_k: Schutz())

    def _lese_meter(bild, _templates, *_a, **_k):
        # Nur das zweite Bild (helleres Grau) gilt als bereits vollstaendig
        # lesbar - simuliert per Bildinhalt statt Pfad, um pruefe_bild()
        # wirklich end-to-end (ueber osd_meter.lese_meter) zu durchlaufen.
        import numpy as np
        arr = np.asarray(bild)
        if arr.mean() > 15:
            return {"meter": 9.4, "stil": "dunkel"}
        return {"meter": None, "stil": "dunkel"}

    monkeypatch.setattr(osd_handlabel.osd_meter, "lese_meter", _lese_meter)

    ziel = tmp_path / "ziel"
    osd_handlabel.main(
        ["queue", "--quelle", str(quelle), "--ziel", str(ziel), "--anzahl", "10"])

    dokument = json.loads((ziel / "queue.json").read_text(encoding="utf-8"))
    haltungen = {fall["haltung"] for fall in dokument["faelle"]}
    assert haltungen == {"10261-10262"}


# ---------------------------------------------------------------------------
# Modus "publizieren" ueber main(): Schema osd_ernte_v1, Hash-/Vollstaendig-
# keitspruefung, Zeichenwahrheit auch ohne gueltigen Meterwert. Die Boxen
# kommen jetzt aus der ENTSCHEIDUNG (Review), nicht mehr aus der Queue.
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
            "bild_pfad": str(bild_pfad), "stil": "dunkel",
        }],
    }
    queue_pfad = queue_ordner / "queue.json"
    queue_pfad.write_text(json.dumps(queue_doc), encoding="utf-8")
    queue_sha256 = hashlib.sha256(queue_pfad.read_bytes()).hexdigest()

    entscheidung: dict = {"aktion": aktion}
    if aktion == "uebernommen":
        entscheidung["zeichenfolge"] = zeichenfolge
        entscheidung["boxen"] = [list(box) for box in boxen]

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
    queue_ordner, review_pfad, _fall_id, _ = _queue_und_review(
        tmp_path, [], "", aktion="unleserlich")

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
    # Zwei Boxen, aber nur ein Zeichen - passt nicht zusammen.
    queue_ordner, review_pfad, _fall_id, _ = _queue_und_review(tmp_path, boxen, "9")

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


def test_publizieren_verweigert_entscheidung_ohne_boxen(tmp_path):
    x0, y0 = _zonenbox()
    boxen = [(x0, y0, x0 + 12, y0 + 18)]
    queue_ordner, review_pfad, fall_id, _ = _queue_und_review(tmp_path, boxen, "9")
    review = json.loads(review_pfad.read_text(encoding="utf-8"))
    del review["entscheidungen"][fall_id]["boxen"]
    review_pfad.write_text(json.dumps(review), encoding="utf-8")

    ziel = tmp_path / "ziel"
    with pytest.raises(SystemExit):
        osd_handlabel.main([
            "publizieren", "--queue", str(queue_ordner), "--review", str(review_pfad),
            "--ziel", str(ziel)])


def test_publizieren_berichtet_zeichen_ausserhalb_des_satzes(tmp_path, capsys):
    x0, y0 = _zonenbox()
    boxen = [(x0, y0, x0 + 12, y0 + 18)]
    queue_ordner, review_pfad, _fall_id, _ = _queue_und_review(tmp_path, boxen, "9")
    review = json.loads(review_pfad.read_text(encoding="utf-8"))
    review["zeichen_ausserhalb_satz"] = {"irgendeinefallid": ["+"]}
    review_pfad.write_text(json.dumps(review), encoding="utf-8")

    ziel = tmp_path / "ziel"
    osd_handlabel.main([
        "publizieren", "--queue", str(queue_ordner), "--review", str(review_pfad),
        "--ziel", str(ziel)])

    ausgabe = capsys.readouterr().out
    assert "Zeichen ausserhalb des Satzes versucht (Material verloren): 1" in ausgabe


if __name__ == "__main__":
    raise SystemExit(pytest.main([__file__, "-v"]))
