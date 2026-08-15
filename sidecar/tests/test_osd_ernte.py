"""Lehrer-Ernte (Spec Abschnitt 4.1).

Nur VOLLSTAENDIGE Lesungen des Vorlagenwegs werden uebernommen. Genau dieser
Zweig hat auf dem gesamten Goldbestand null falsche Werte; eine Bruchstueck-
Lesung dagegen raet den Dezimalpunkt und waere ein falsches Etikett.
"""

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


def test_haltung_aus_ordnername_verwirft_nicht_passende_namen():
    # Weder "bilder" noch "gold_frames" noch "1-2-3" sehen wie eine Haltung
    # mit genau zwei Seiten aus - lieber None als eine geratene Haltung.
    assert osd_ernte.haltung_aus_ordnername("bilder") is None
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
