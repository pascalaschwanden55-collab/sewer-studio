"""Laufzeitteil des Modell-Lesers (Spec Abschnitte 3 und 5).

Zwei Dinge werden hier festgehalten:
  1. Der Ausschnitt wird auf feste Zeichenhoehe normiert. SD und HD sehen fuer
     das Modell dadurch gleich aus - der Aufloesungsfehler vom 2026-08-14 kann
     bauartbedingt nicht wiederkehren.
  2. Die Sicherheit einer Lesung ist die KLEINSTE Zeichensicherheit. Ein
     wackliges Zeichen macht die ganze Lesung wacklig.
"""

from PIL import Image

from sidecar import osd_meter, osd_modell


def test_normierung_macht_sd_und_hd_gleich_gross():
    sd = Image.new("RGB", (274, 92))
    hd = Image.new("RGB", (548, 184))

    assert (osd_modell.normiere_ausschnitt(sd).height
            == osd_modell.normiere_ausschnitt(hd).height)


def test_normierung_haelt_das_seitenverhaeltnis():
    bild = Image.new("RGB", (400, 100))

    normiert = osd_modell.normiere_ausschnitt(bild, ziel_hoehe=50)

    assert normiert.height == 50
    assert normiert.width == 200


def test_zeichen_werden_von_links_nach_rechts_gelesen():
    # Absichtlich in falscher Reihenfolge uebergeben.
    erkennungen = [
        (osd_meter.ZEICHEN.find("4"), 0.7, 0.5, 0.1, 0.4, 0.9),
        (osd_meter.ZEICHEN.find("9"), 0.3, 0.5, 0.1, 0.4, 0.9),
        (osd_meter.ZEICHEN.find("."), 0.5, 0.5, 0.05, 0.4, 0.9),
    ]

    folge, _ = osd_modell.zu_zeichenfolge(erkennungen)

    assert folge == "9.4"


def test_kleinste_sicherheit_zaehlt():
    erkennungen = [
        (osd_meter.ZEICHEN.find("9"), 0.3, 0.5, 0.1, 0.4, 0.95),
        (osd_meter.ZEICHEN.find("4"), 0.7, 0.5, 0.1, 0.4, 0.41),
    ]

    _, sicherheit = osd_modell.zu_zeichenfolge(erkennungen)

    assert sicherheit == 0.41


def test_leere_erkennung_liefert_leere_folge_und_null():
    folge, sicherheit = osd_modell.zu_zeichenfolge([])

    assert folge == ""
    assert sicherheit == 0.0


def test_doppelte_box_am_selben_ort_wird_einmal_gezaehlt():
    # Zwei Erkennungen fast am selben Ort: die schwaechere faellt weg.
    erkennungen = [
        (osd_meter.ZEICHEN.find("9"), 0.30, 0.5, 0.10, 0.4, 0.90),
        (osd_meter.ZEICHEN.find("8"), 0.31, 0.5, 0.10, 0.4, 0.40),
    ]

    folge, sicherheit = osd_modell.zu_zeichenfolge(erkennungen)

    assert folge == "9"
    assert sicherheit == 0.90


def test_unbekannte_klasse_liefert_leere_folge_und_null():
    # Fail-closed: eine Klassen-ID ausserhalb von ZEICHEN darf nie zu einem
    # geratenen Zeichen fuehren. Ein falscher Wert ist teurer als zehn
    # fehlende.
    erkennungen = [
        (len(osd_meter.ZEICHEN), 0.5, 0.5, 0.1, 0.4, 0.99),
    ]

    folge, sicherheit = osd_modell.zu_zeichenfolge(erkennungen)

    assert folge == ""
    assert sicherheit == 0.0


def test_gemischte_erkennung_mit_unbekannter_klasse_verwirft_die_ganze_lesung():
    # Eine einzelne unbekannte Klasse darf die uebrigen, gueltig erkannten
    # Zeichen nicht als Teillesung durchrutschen lassen - die ganze Lesung
    # wird verworfen, nicht nur die eine Stelle.
    erkennungen = [
        (osd_meter.ZEICHEN.find("9"), 0.3, 0.5, 0.1, 0.4, 0.95),
        (len(osd_meter.ZEICHEN), 0.7, 0.5, 0.1, 0.4, 0.95),
    ]

    folge, sicherheit = osd_modell.zu_zeichenfolge(erkennungen)

    assert folge == ""
    assert sicherheit == 0.0
