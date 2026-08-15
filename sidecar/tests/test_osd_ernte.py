"""Lehrer-Ernte (Spec Abschnitt 4.1).

Nur VOLLSTAENDIGE Lesungen des Vorlagenwegs werden uebernommen. Genau dieser
Zweig hat auf dem gesamten Goldbestand null falsche Werte; eine Bruchstueck-
Lesung dagegen raet den Dezimalpunkt und waere ein falsches Etikett.
"""

import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_ernte
from osd_schutz import Schutz


def _bild_mit_anzeige(text: str, groesse=(720, 576)) -> Image.Image:
    """Dunkles Bild mit heller Anzeige unten rechts - der SD-Normalfall."""
    bild = Image.new("RGB", groesse, (18, 18, 18))
    zeichner = ImageDraw.Draw(bild)
    zeichner.text((groesse[0] - 190, groesse[1] - 40), text, fill=(240, 240, 240))
    return bild


def test_vollstaendige_lesung_liefert_normierte_labels(monkeypatch):
    """Deterministisch: Der Leser wird gestellt, geprueft wird die Umrechnung.

    Bewusst NICHT vom echten Vorlagentreffer abhaengig. Ein Test, der bei
    ausbleibendem Treffer einfach durchlaeuft, ist ein stiller Pass - genau die
    Sorte Test, die im Audit vom 2026-08-14 als wertlos aufgefallen ist.
    """
    from sidecar import osd_meter
    import numpy as np

    bild = _bild_mit_anzeige("LZ1: 9.4m")
    breite, hoehe = bild.size
    links, oben, _r, _u = osd_meter.ZONEN["unten_rechts"]
    x0, y0 = int(links * breite) + 10, int(oben * hoehe) + 10

    monkeypatch.setattr(osd_ernte.osd_meter, "glyphenmaske",
                        lambda _b: (np.zeros((hoehe, breite), dtype="uint8"), "dunkel"))
    monkeypatch.setattr(osd_ernte.osd_meter, "boxen_aus_maske",
                        lambda _m, _s: [(x0, y0, x0 + 12, y0 + 18),
                                        (x0 + 14, y0, x0 + 26, y0 + 18)])
    folge = iter("94")
    monkeypatch.setattr(osd_ernte.osd_meter, "klassifiziere",
                        lambda _g, _t: (next(folge), 0.9))
    monkeypatch.setattr(osd_ernte.osd_meter, "_zeichenfolge_ist_vollstaendig",
                        lambda _z: True)
    monkeypatch.setattr(osd_ernte.osd_meter, "parse_meter",
                        lambda *_a, **_k: 9.4)

    ergebnis = osd_ernte.ernte_bild(bild, None, Schutz(), "ab" * 32, "10261-10262")

    assert ergebnis is not None
    assert ergebnis.zeichenfolge == "94"
    assert len(ergebnis.zeichen) == 2
    for klasse_id, x, y, b, h in ergebnis.zeichen:
        assert 0 <= klasse_id < len(osd_meter.ZEICHEN)
        assert all(0.0 <= wert <= 1.0 for wert in (x, y, b, h))
        assert b > 0 and h > 0
    # Die zweite Box liegt rechts der ersten - die Umrechnung dreht nichts um.
    assert ergebnis.zeichen[0][1] < ergebnis.zeichen[1][1]


def test_unvollstaendige_lesung_wird_verworfen(monkeypatch):
    """Bruchstueck-Lesungen sind Gift als Etikett (58 von 61 grob falsch)."""
    import numpy as np

    bild = _bild_mit_anzeige("9.4")
    breite, hoehe = bild.size

    monkeypatch.setattr(osd_ernte.osd_meter, "glyphenmaske",
                        lambda _b: (np.zeros((hoehe, breite), dtype="uint8"), "dunkel"))
    monkeypatch.setattr(osd_ernte.osd_meter, "boxen_aus_maske",
                        lambda _m, _s: [(500, 500, 512, 518)])
    monkeypatch.setattr(osd_ernte.osd_meter, "klassifiziere",
                        lambda _g, _t: ("9", 0.9))
    monkeypatch.setattr(osd_ernte.osd_meter, "_zeichenfolge_ist_vollstaendig",
                        lambda _z: False)

    ergebnis = osd_ernte.ernte_bild(bild, None, Schutz(), "ab" * 32, "10261-10262")

    assert ergebnis is None


def test_gesperrtes_bild_wird_uebersprungen():
    from sidecar import osd_meter
    bild = _bild_mit_anzeige("LZ1: 9.4m")
    schutz = Schutz(frozenset({"ab" * 32}), frozenset())

    ergebnis = osd_ernte.ernte_bild(
        bild, osd_meter.get_templates(), schutz, "ab" * 32, "10261-10262")

    assert ergebnis is None


def test_gesperrte_haltung_wird_uebersprungen():
    from sidecar import osd_meter
    bild = _bild_mit_anzeige("LZ1: 9.4m")
    schutz = Schutz(frozenset(), frozenset({"10261-10262"}))

    # Gegenrichtung angegeben - muss trotzdem greifen.
    ergebnis = osd_ernte.ernte_bild(
        bild, osd_meter.get_templates(), schutz, "cd" * 32, "10262-10261")

    assert ergebnis is None


def test_leeres_bild_liefert_nichts():
    from sidecar import osd_meter
    bild = Image.new("RGB", (720, 576), (18, 18, 18))

    ergebnis = osd_ernte.ernte_bild(
        bild, osd_meter.get_templates(), Schutz(), "ef" * 32, "10261-10262")

    assert ergebnis is None


def test_labelzeilen_sind_yolo_format():
    zeilen = osd_ernte.als_labeltext([(3, 0.5, 0.5, 0.1, 0.4)])

    assert zeilen == "3 0.500000 0.500000 0.100000 0.400000\n"


# ---------------------------------------------------------------------------
# CLI-nahe reine Logik (Ruling: main() liest/schreibt Dateien, die folgenden
# Bausteine sind dateisystemfrei und werden deshalb direkt geprueft).
# ---------------------------------------------------------------------------

def test_bild_id_ist_erste_16_hexzeichen_des_hashes():
    voller_hash = "0123456789abcdef" + "f" * 48

    assert osd_ernte.bild_id(voller_hash) == "0123456789abcdef"
    assert len(osd_ernte.bild_id(voller_hash)) == 16


def test_haltung_aus_ordnername_erkennt_haltungsmuster():
    assert osd_ernte.haltung_aus_ordnername("10261-10262") == "10261-10262"
    assert osd_ernte.haltung_aus_ordnername("A-B") == "A-B"


def test_haltung_aus_ordnername_erkennt_echte_archivordner():
    """Fix-Runde 1 (2026-08-15).

    Die 1476 echten Ordner unter D:\\Haltungen tragen Punkte - das
    urspruengliche punktlose Muster wies das GESAMTE Archiv ab und liess den
    Gegenrichtungsschutz nirgends mehr greifen. Nur die Goldmanifeste selbst
    haben punktlose Namen ("36051-33461"); beide Formen muessen erkannt
    werden.
    """
    for name in ("06.24341-35625", "06.24379-06.24377",
                 "06.691077-06.691078", "06.691078-691070"):
        assert osd_ernte.haltung_aus_ordnername(name) == name

    # Punktlose Goldform bleibt weiterhin erkannt.
    assert osd_ernte.haltung_aus_ordnername("36051-33461") == "36051-33461"


def test_haltung_aus_ordnername_erkennt_umlaute():
    """Ein echter Archivordner ("61542-Schächen_Bach") - Umlaute sind
    Buchstaben und muessen wie alle anderen Buchstaben erkannt werden.
    """
    name = "61542-Schächen_Bach"

    assert osd_ernte.haltung_aus_ordnername(name) == name


def test_haltung_aus_ordnername_verwirft_leerzeichen_um_bindestrich():
    """Bewusst NICHT unterstuetzt (Fix-Runde 1).

    "36510 - 36906" (Leerzeichen um den Bindestrich) waere sonst nicht von
    den `<Hauptcode - Klartext>`-Ordnern in gold_frames zu unterscheiden
    (z. B. "BAB - Riss") - die sind ausdruecklich keine Haltungen.
    """
    assert osd_ernte.haltung_aus_ordnername("36510 - 36906") is None
    assert osd_ernte.haltung_aus_ordnername("BAB - Riss") is None


def test_haltung_aus_ordnername_verwirft_nicht_passende_namen():
    # Weder "bilder" noch "frames" noch "gold_frames" noch "1-2-3" sehen wie
    # eine Haltung mit genau zwei Seiten aus - lieber None als eine
    # geratene Haltung.
    assert osd_ernte.haltung_aus_ordnername("bilder") is None
    assert osd_ernte.haltung_aus_ordnername("frames") is None
    assert osd_ernte.haltung_aus_ordnername("gold_frames") is None
    assert osd_ernte.haltung_aus_ordnername("1-2-3") is None
    assert osd_ernte.haltung_aus_ordnername("") is None


def test_eintrag_erzeugen_liefert_erwartete_feldform():
    eintrag = osd_ernte.eintrag_erzeugen("ab" * 32, "10261-10262", "094", 9.4)

    assert eintrag == {
        "id": osd_ernte.bild_id("ab" * 32),
        "bild_sha256": "ab" * 32,
        "haltung": "10261-10262",
        "zeichenfolge": "094",
        "meter": 9.4,
    }


def test_eintrag_erzeugen_erlaubt_fehlende_haltung():
    eintrag = osd_ernte.eintrag_erzeugen("cd" * 32, None, "12", 1.2)

    assert eintrag["haltung"] is None


# ---------------------------------------------------------------------------
# main(): Fix-Runde 1 (2026-08-15) - ein unerwarteter Fehler tief im
# Verarbeitungsweg darf den Lauf nicht wertlos machen, und die Zaehlung, ob
# der Gegenrichtungsschutz greifen konnte, muss sichtbar sein.
# ---------------------------------------------------------------------------

def test_main_uebersteht_unerwarteten_fehler_mitten_im_bild(tmp_path, monkeypatch, capsys):
    """Ein Fehler tief in ernte_bild() darf den ganzen Lauf nicht wegwerfen.

    Vorher lag nur Image.open()/.load() in try/except: Ein Fehler in
    glyphenmaske/boxen_aus_maske/klassifiziere/parse_meter waere unbehandelt
    aus main() geflogen - eintraege.json waere nie geschrieben worden.
    """
    quelle = tmp_path / "quelle" / "99999-88888"
    quelle.mkdir(parents=True)
    _bild_mit_anzeige("LZ1: 9.4m").save(quelle / "a.jpg", quality=95)
    _bild_mit_anzeige("LZ2: 3.1m").save(quelle / "b.jpg", quality=95)

    monkeypatch.setattr(osd_ernte, "lade_schutz", lambda *_a, **_k: Schutz())

    def _explodiert(*_a, **_k):
        raise RuntimeError("unerwarteter Fehler tief im Verarbeitungsweg")

    monkeypatch.setattr(osd_ernte, "ernte_bild", _explodiert)

    ziel = tmp_path / "ziel"
    rc = osd_ernte.main(["--bilder", str(quelle.parent), "--ziel", str(ziel)])

    assert rc == 0
    dokument = json.loads((ziel / "eintraege.json").read_text(encoding="utf-8"))
    assert dokument == {"schema": "osd_ernte_v1", "eintraege": []}

    ausgabe = capsys.readouterr().out
    assert "Bilder gesehen: 2" in ausgabe
    assert "Uebersprungen (unlesbar/unvollstaendig): 2" in ausgabe


def test_main_zaehlt_bilder_ohne_erkennbare_haltung(tmp_path, monkeypatch, capsys):
    """Fehlt die Haltung, muss das sichtbar sein - nicht nur der Bildhash-Schutz greift dann."""
    ohne_haltung = tmp_path / "quelle" / "unsortiert"
    ohne_haltung.mkdir(parents=True)
    _bild_mit_anzeige("LZ1: 9.4m").save(ohne_haltung / "a.jpg", quality=95)

    monkeypatch.setattr(osd_ernte, "lade_schutz", lambda *_a, **_k: Schutz())
    monkeypatch.setattr(osd_ernte, "ernte_bild", lambda *_a, **_k: None)

    ziel = tmp_path / "ziel"
    osd_ernte.main(["--bilder", str(ohne_haltung.parent), "--ziel", str(ziel)])

    ausgabe = capsys.readouterr().out
    assert "Ohne erkennbare Haltung (Gegenrichtungsschutz konnte nicht greifen): 1" in ausgabe
