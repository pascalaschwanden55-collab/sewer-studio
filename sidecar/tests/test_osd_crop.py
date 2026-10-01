"""Gemeinsamer Zuschnitt der OSD-Zone (Fix-Runde 1, Aufgabe 3).

Vorher rundete osd_ernte.py (Ernte) mit int(), osd_modell_leser.py
(Inferenz) mit round() - auf zwei von drei Gold-Aufloesungen verschob das
den Ausschnitt zwischen Training und Messung um eine Bildzeile. Ein
gemeinsamer Zuschnitt-Helfer (osd_crop.py) schliesst das; dieser Test
pinnt die exakten Pixelkasten UND prueft die beiden echten Konsumenten
(osd_ernte.zonen_ausschnitt, osd_modell_leser.zuschnitt_fuer_leser) direkt
gegeneinander.
"""

import sys
from pathlib import Path

import numpy as np
from PIL import Image

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_crop
import osd_ernte
import osd_modell_leser as leser_modul

# Die drei eingefrorenen Gold-Aufloesungen (siehe osd_goldmessung.py SAETZE:
# osd_sd_v1, osd_hd_v1, osd_hd2_v1).
GOLD_AUFLOESUNGEN = ((720, 576), (1280, 720), (1920, 1080))


def _buntes_testbild(breite: int, hoehe: int) -> Image.Image:
    """Deterministischer Farbverlauf statt einer leeren Flaeche - sonst waere
    ein bytegleicher Vergleich auch bei falschem Zuschnitt trivial erfuellt."""
    x = np.linspace(0, 255, breite, dtype=np.uint8)
    y = np.linspace(0, 255, hoehe, dtype=np.uint8)
    rot = np.tile(x, (hoehe, 1))
    gruen = np.tile(y.reshape(-1, 1), (1, breite))
    blau = ((rot.astype(int) + gruen.astype(int)) % 256).astype(np.uint8)
    arr = np.dstack([rot, gruen, blau])
    return Image.fromarray(arr, mode="RGB")


# ---------------------------------------------------------------------------
# zonen_box(): exakte Pixelkasten, gemessen bei der Fehlersuche zu Aufgabe 3.
# ---------------------------------------------------------------------------

def test_zonen_box_pinnt_sd_aufloesung():
    assert osd_crop.zonen_box(720, 576) == (446, 484, 720, 576)


def test_zonen_box_pinnt_hd720_aufloesung():
    assert osd_crop.zonen_box(1280, 720) == (794, 605, 1280, 720)


def test_zonen_box_pinnt_hd1080_aufloesung():
    assert osd_crop.zonen_box(1920, 1080) == (1190, 907, 1920, 1080)


def test_zonen_box_stimmt_mit_osd_meter_glyphenmaske_ueberein():
    """osd_meter.py (UNVERAENDERT) berechnet die Zonengrenzen intern bereits
    mit round() - ein davon abweichender Zuschnitt hier wuerde die von
    boxen_aus_maske() gelieferten Vollbildkoordinaten falsch auf den
    Ausschnitt umrechnen (siehe osd_crop.py-Docstring)."""
    from sidecar import osd_meter

    links, oben, rechts, unten = osd_meter.ZONEN["unten_rechts"]
    for breite, hoehe in GOLD_AUFLOESUNGEN:
        erwartet = (
            round(links * breite), round(oben * hoehe),
            round(rechts * breite), round(unten * hoehe),
        )
        assert osd_crop.zonen_box(breite, hoehe) == erwartet


# ---------------------------------------------------------------------------
# schneide_zone(): Ausschnitt + Versatz.
# ---------------------------------------------------------------------------

def test_schneide_zone_liefert_ausschnitt_und_versatz():
    bild = Image.new("RGB", (720, 576), (10, 20, 30))

    ausschnitt, versatz = osd_crop.schneide_zone(bild)

    assert ausschnitt.size == (274, 92)
    assert versatz == (446, 484)


# ---------------------------------------------------------------------------
# Regressionspin: Ernte und Modell-Leser muessen bytegleich zuschneiden -
# ueber die ECHTEN Konsumenten, nicht nur ueber osd_crop selbst.
# ---------------------------------------------------------------------------

def test_ernte_und_leser_schneiden_bytegleich_bei_allen_gold_aufloesungen():
    for breite, hoehe in GOLD_AUFLOESUNGEN:
        bild = _buntes_testbild(breite, hoehe)

        ernte_ausschnitt, ernte_versatz = osd_ernte.zonen_ausschnitt(bild)
        leser_ausschnitt = leser_modul.zuschnitt_fuer_leser(bild)

        assert ernte_ausschnitt.size == leser_ausschnitt.size
        assert ernte_ausschnitt.tobytes() == leser_ausschnitt.tobytes()
        # Der Versatz muss der echten Zonenbox entsprechen (fuer die
        # Vollbild->Ausschnitt-Umrechnung in osd_ernte.ernte_bild()).
        assert ernte_versatz == osd_crop.zonen_box(breite, hoehe)[:2]
