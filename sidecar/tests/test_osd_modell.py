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


def test_zielhoehe_haelt_ziffern_ueber_der_lesbarkeitsgrenze():
    """Fix-Runde 1 (a): ZIEL_HOEHE=32 druckte die Ziffer unter GLYPHE_MIN_H.

    Realistische Zonenhoehen (Zonenanteil 0,16 der Videohoehe, siehe
    osd_meter.ZONEN): SD 576p -> 92 px Zone, in der die Ziffer die gemessene
    Referenz osd_meter.REFERENZ_GLYPHE_H (18 px) hoch ist; HD 1080p -> 172 px
    Zone. Die Ziffernhoehe skaliert linear mit der Zonenhoehe, deshalb wird
    sie hier aus REFERENZ_GLYPHE_H und der SD-Zonenhoehe hergeleitet statt als
    fester Wert hingeschrieben - der Test haelt damit auch dann, wenn jemand
    ZIEL_HOEHE spaeter aendert.
    """
    sd_zonenhoehe = 92
    hd_zonenhoehe = 172
    sd = Image.new("RGB", (450, sd_zonenhoehe))
    hd = Image.new("RGB", (900, hd_zonenhoehe))

    sd_normiert = osd_modell.normiere_ausschnitt(sd)
    hd_normiert = osd_modell.normiere_ausschnitt(hd)

    assert sd_normiert.height == hd_normiert.height

    ziffer_nach_normierung = (
        osd_meter.REFERENZ_GLYPHE_H * osd_modell.ZIEL_HOEHE / sd_zonenhoehe
    )
    assert ziffer_nach_normierung > osd_meter.GLYPHE_MIN_H


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


def test_ueberlappende_boxen_am_selben_zeichen_werden_einmal_gezaehlt():
    """Fix-Runde 1 (b): echte Doppeldetektion, hohe IoU (>= 0,5).

    Gleiche Zeichenbreite (~0,04 bei Zielhoehe 96 und SD-Zonenbreite),
    Zentren nur 0,005 auseinander -> IoU rund 0,78. Die schwaechere Box
    faellt weg, die staerkere bleibt.
    """
    erkennungen = [
        (osd_meter.ZEICHEN.find("9"), 0.300, 0.5, 0.04, 0.8, 0.90),
        (osd_meter.ZEICHEN.find("8"), 0.305, 0.5, 0.04, 0.8, 0.40),
    ]

    folge, sicherheit = osd_modell.zu_zeichenfolge(erkennungen)

    assert folge == "9"
    assert sicherheit == 0.90


def test_eng_benachbarter_punkt_neben_ziffer_bleibt_eigenes_zeichen():
    """Fix-Runde 1 (b): ein schmaler Punkt neben einer Ziffer darf nicht
    verschluckt werden.

    Die alte, reine Mittenabstands-Regel (< 0,02) unterdrueckte einen eng an
    seiner Ziffer sitzenden Punkt faelschlich als Dublette (aus "0000.30"
    wurde so "000030" - ein FALSCHER Wert mit voller Sicherheit). Die
    Mittenabstaende hier (0,019) liegen absichtlich UNTER dieser alten
    Schwelle, damit dieser Test den beschriebenen Fehler wirklich abdeckt.
    Bei realistischen Massen (Ziffer ~0,04, Punkt ~0,02 der Breite bei
    Zielhoehe 96 und SD-Zonenbreite) beruehren sich die Boxen zwar fast, ihre
    IoU bleibt aber deutlich unter der 0,5-Schwelle (rund 0,22) - beide
    Zeichen bleiben erhalten.
    """
    erkennungen = [
        (osd_meter.ZEICHEN.find("9"), 0.500, 0.5, 0.04, 0.8, 0.90),
        (osd_meter.ZEICHEN.find("."), 0.519, 0.5, 0.02, 0.8, 0.85),
    ]

    folge, sicherheit = osd_modell.zu_zeichenfolge(erkennungen)

    assert folge == "9."
    assert sicherheit == 0.85


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
