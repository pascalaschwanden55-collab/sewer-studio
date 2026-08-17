"""Tests fuer sidecar.osd_meter: Formvalidator, Format-Lock, Ganzbild-Lesung.

Beleglage siehe docs/quality/OSD-METERLESER-VALIDIERUNG-2026-08-08.md.
Die gescheiterte Sechs-Ziffern-Ratelei ('0.00.300' -> 3.0) kommt nicht wieder;
sie ist hier als Test verankert.
"""

from __future__ import annotations

import pytest
from PIL import Image, ImageDraw, ImageFont

from sidecar import osd_meter
from sidecar.schemas.detection import BccTestYoloRequest, BccTestYoloResponse

_templates = osd_meter.get_templates()
braucht_templates = pytest.mark.skipif(
    not _templates, reason="Windows-Referenzschriften nicht verfuegbar")


@pytest.mark.parametrize("roh,erwartet", [
    ("LZ2: 14.1m", 14.1),
    ("LZ2: 0.4m", 0.4),
    ("LZ2: 0000.30 m", 0.30),
    ("LZ2: 0007.00", 7.0),
    # formal gueltig — die Plausibilitaet gegen die Haltung ist Sequenzsache
    ("0133.08", 133.08),
])
def test_parse_meter_auto_bekannte_formen(roh, erwartet):
    assert osd_meter.parse_meter(roh) == pytest.approx(erwartet)


@pytest.mark.parametrize("roh", ["0.00.300", "", "LZ2:", "abc", "401.5"])
def test_parse_meter_auto_lehnt_unvollstaendiges_ab(roh):
    assert osd_meter.parse_meter(roh) is None


# --- Kein falscher Wert, lieber keiner (Goldmessung 2026-08-14) ---------------
#
# Der Leser lieferte im Goldbestand null falsche Werte. Bei der HD-Reparatur
# tauchten zwei auf; beide entstanden dadurch, dass eine erkennbar kaputte
# Zeichenfolge trotzdem eine plausible Zahl ergab. Ein falscher Meterstand
# wandert unbemerkt ins Protokoll — ein fehlender faellt sofort auf.

@pytest.mark.parametrize("roh,grund", [
    ("LZ:::6.4m3", "Ziffer hinter der Einheit"),
    ("LZ2: 0007.00m.7", "Ziffer hinter der Einheit"),
])
def test_eine_ziffer_hinter_der_einheit_verwirft_die_lesung(roh, grund):
    # 'LZ:::6.4m3': Sollwert war 26,4. Die fuehrende 2 wurde zu ':' verlesen, die
    # verirrte 3 verriet es. Frueher wurde alles hinter dem 'm' weggeworfen und
    # 6,4 gemeldet — 20 Meter daneben, ohne jeden Hinweis.
    assert osd_meter.parse_meter(roh) is None, grund


@pytest.mark.parametrize("roh", [
    "ZLZ1:.0.1m",      # aus "LZ1: -0.1m": das Minus wurde zum Punkt
    "L:Z:03...:mm.",
])
def test_mehr_als_ein_punkt_verwirft_die_lesung(roh):
    # Zwei Dezimalpunkte kann kein gueltiger Meterstand haben. Frueher wurde der
    # fuehrende Punkt abgestreift und aus -0,1 wurde 0,1.
    assert osd_meter.parse_meter(roh) is None


def test_eine_einzelne_einheit_ohne_ziffernrest_bleibt_gueltig(roh="LZ2: 14.1m"):
    # Die Regel darf nur Ziffern hinter der Einheit treffen, nicht die Einheit selbst.
    assert osd_meter.parse_meter(roh) == pytest.approx(14.1)


# --- Groessenabhaengige Zeichenfindung ---------------------------------------

def test_die_skala_bleibt_bei_sd_hoehe_neutral():
    # 18 Pixel ist der Bezugsfall. Nach unten wird nie skaliert, damit die
    # SD-Lesungen bitgenau erhalten bleiben.
    assert osd_meter.glyphen_skala([18, 18, 18]) == pytest.approx(1.0)
    assert osd_meter.glyphen_skala([9, 10, 11]) == pytest.approx(1.0)
    assert osd_meter.glyphen_skala([]) == pytest.approx(1.0)


def test_doppelt_so_hohe_zeichen_verdoppeln_die_schranken():
    # HD: dieselben Zeichen, doppelte Hoehe. Ohne Anpassung lagen Nachbarfenster
    # und Doppelpunkt-Abstand zu eng, und Dezimalpunkt und Einheit gingen verloren.
    assert osd_meter.glyphen_skala([36] * 5) == pytest.approx(2.0)


def test_die_skala_ist_nach_oben_begrenzt():
    # Ein einzelnes riesiges Stoerobjekt darf die Schranken nicht ins Uferlose ziehen.
    assert osd_meter.glyphen_skala([500] * 5) == pytest.approx(4.0)


def test_parse_meter_punktlos_nur_im_ein_dezimalen_layout():
    assert osd_meter.parse_meter("01", stil="dunkel") == pytest.approx(0.1)
    assert osd_meter.parse_meter("01", stil="hell") is None


def test_parse_meter_verwirft_verlesenes_z_als_ziffer_nach_l():
    # HD-Befund 2026-08-08: Auf 1080p kippt die Vorlage Z zu 1; aus "LZ 3.2m"
    # wurde "L132" und der Parser las 13,2 — alle sieben Werte exakt eine
    # Zehnerpotenz zu hoch. Nach einem L ist eine Ziffer (ausser der bekannten
    # Z-Variante "2") eine Verlesung: None, nicht raten.
    assert osd_meter.parse_meter("L132") is None   # Wahrheit 3,2
    assert osd_meter.parse_meter("L107") is None   # Wahrheit 0,7
    assert osd_meter.parse_meter("L145") is None   # Wahrheit 4,5
    assert osd_meter.parse_meter("L1 3.2") is None
    assert osd_meter.parse_meter("L132", format="vierziffern") is None


def test_parse_meter_lz_praefix_bleibt_unveraendert_lesbar():
    assert osd_meter.parse_meter("LZ 3.2") == pytest.approx(3.2)
    assert osd_meter.parse_meter("LZ3.2") == pytest.approx(3.2)
    assert osd_meter.parse_meter("L2: 14.1m") == pytest.approx(14.1)
    assert osd_meter.parse_meter("LZ2: 14.1m") == pytest.approx(14.1)
    assert osd_meter.parse_meter("L232") == pytest.approx(3.2)


def test_parse_meter_lz_praefix_mit_plus_und_fuehrenden_nullen():
    assert osd_meter.parse_meter("LZ1: +0000.80 m") == pytest.approx(0.8)
    assert osd_meter.parse_meter("L21:+0010.40m") == pytest.approx(10.4)
    assert osd_meter.parse_meter("+0001.20", format="vierziffern") == pytest.approx(1.2)


def test_format_vierziffern_laesst_nur_die_vierziffern_form_durch():
    assert osd_meter.parse_meter(
        "LZ2: 0000.30 m", format="vierziffern") == pytest.approx(0.30)
    assert osd_meter.parse_meter(
        "LZ2: 0007.00", format="vierziffern") == pytest.approx(7.0)
    assert osd_meter.parse_meter("LZ2: 14.1m", format="vierziffern") is None
    assert osd_meter.parse_meter("01", format="vierziffern") is None


def test_format_ein_dezimal_kennt_das_layout_und_erlaubt_punktlos():
    assert osd_meter.parse_meter(
        "LZ2: 14.1m", format="ein_dezimal") == pytest.approx(14.1)
    assert osd_meter.parse_meter(
        "01", stil="hell", format="ein_dezimal") == pytest.approx(0.1)
    assert osd_meter.parse_meter("LZ2: 0007.00", format="ein_dezimal") is None


def test_unbekanntes_format_ist_ein_fehler_kein_stiller_rueckfall():
    with pytest.raises(ValueError):
        osd_meter.parse_meter("LZ2: 14.1m", format="sechsziffern")


def _osd_bild(text: str) -> Image.Image:
    """Dunkler Text auf hellem Kasten in der Meterzone (dominanter Stil)."""
    bild = Image.new("RGB", (640, 480), (120, 120, 120))
    d = ImageDraw.Draw(bild)
    font = ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", 22)
    d.rectangle([400, 405, 632, 448], fill=(255, 255, 255))
    d.text((408, 412), text, fill=(0, 0, 0), font=font)
    return bild


@braucht_templates
def test_lese_meter_liest_dominanten_kasten_stil():
    ergebnis = osd_meter.lese_meter(_osd_bild("LZ2: 14.1m"), _templates)
    assert ergebnis["meter"] == pytest.approx(14.1)
    assert ergebnis["stil"] == "dunkel"


@braucht_templates
def test_lese_meter_ohne_osd_gibt_none_statt_null():
    bild = Image.new("RGB", (640, 480), (120, 120, 120))
    assert osd_meter.lese_meter(bild, _templates)["meter"] is None


@braucht_templates
def test_lese_meter_vierziffern_mit_und_ohne_lock():
    bild = _osd_bild("LZ2: 0000.30 m")
    assert osd_meter.lese_meter(bild, _templates)["meter"] == pytest.approx(0.30)
    assert osd_meter.lese_meter(
        bild, _templates, format="vierziffern")["meter"] == pytest.approx(0.30)


def test_lese_meter_nutzt_engen_vierziffern_rueckfall(monkeypatch):
    monkeypatch.setattr(
        osd_meter, "_lese_vierziffern_mit_tesseract",
        lambda _bild: (0.8, "LZ1:+0000.80m"))
    ergebnis = osd_meter.lese_meter(
        Image.new("RGB", (640, 480), (120, 120, 120)), _templates)
    assert ergebnis["meter"] == pytest.approx(0.8)
    assert ergebnis["leseweg"] == "tesseract_vierziffern"


def test_ein_dezimal_format_ruft_vierziffern_rueckfall_nicht_auf(monkeypatch):
    def nicht_aufrufen(_bild):
        raise AssertionError("Vierziffern-Rueckfall wurde unerwartet aufgerufen")

    monkeypatch.setattr(osd_meter, "_lese_vierziffern_mit_tesseract", nicht_aufrufen)
    ergebnis = osd_meter.lese_meter(
        Image.new("RGB", (640, 480), (120, 120, 120)), _templates,
        format="ein_dezimal")
    assert ergebnis["meter"] is None


def test_vierziffern_rueckfall_startet_nur_bei_schmaler_zeichenzeile():
    import numpy as np

    leer = np.zeros((80, 240), dtype="uint8")
    zeile = leer.copy()
    for x in range(20, 180, 14):
        zeile[30:46, x:x + 6] = 255
    voll = np.full((80, 240), 255, dtype="uint8")
    assert not osd_meter._ist_vierziffern_kandidat(leer)
    assert osd_meter._ist_vierziffern_kandidat(zeile)
    assert not osd_meter._ist_vierziffern_kandidat(voll)


def test_request_akzeptiert_genau_die_bekannten_formate():
    # Drift-Schutz: Schema-Literal und osd_meter.FORMATE bleiben gleich.
    assert set(osd_meter.FORMATE) == {"auto", "ein_dezimal", "vierziffern"}
    for wert in osd_meter.FORMATE:
        anfrage = BccTestYoloRequest(image_base64="eA==", meter_format=wert)
        assert anfrage.meter_format == wert
    assert BccTestYoloRequest(image_base64="eA==").meter_format is None


def test_request_weist_unbekanntes_format_ab():
    from pydantic import ValidationError

    with pytest.raises(ValidationError):
        BccTestYoloRequest(image_base64="eA==", meter_format="sechsziffern")


def test_response_traegt_meter_value_additiv():
    assert BccTestYoloResponse().meter_value is None
    payload = BccTestYoloResponse(meter_value=14.1).model_dump()
    assert payload["meter_value"] == pytest.approx(14.1)


def test_wrapper_meterlesung_geht_in_die_antwort():
    from sidecar.models import bcc_test_wrapper

    bild = _osd_bild("LZ2: 14.1m")
    assert bcc_test_wrapper._lese_meter_sicher(bild, None) == pytest.approx(14.1)


def test_wrapper_meterlesung_laesst_die_erkennung_nie_ausfallen():
    from sidecar.models import bcc_test_wrapper

    # Ein Lesefehler (hier: ungueltiges Format direkt am Helfer) wird zu None,
    # nicht zum Abbruch — auf dem HTTP-Weg verhindert das Schema diesen Fall.
    bild = _osd_bild("LZ2: 14.1m")
    assert bcc_test_wrapper._lese_meter_sicher(bild, "sechsziffern") is None


def test_bruchstueck_ohne_beschriftung_wird_nicht_geliefert():
    """Nackte Ziffern duerfen keinen Meterstand ergeben.

    Auf 897 beschrifteten Archivbildern waren 58 von 61 solcher Lesungen grob
    falsch: Ohne Beschriftung und Einheit steht die Stellenzahl nicht fest und
    der Dezimalpunkt wird geraten (`0,58` wurde zu `5,80`).
    """
    from sidecar.osd_meter import _zeichenfolge_ist_vollstaendig

    for bruchstueck in (":?40?.", ":?63.", "058", "266", "2?73", "m.94.", ""):
        assert not _zeichenfolge_ist_vollstaendig(bruchstueck), bruchstueck


def test_vollstaendige_lesung_bleibt_gueltig():
    """Beschriftung plus Einheit genuegt; ein Stoerzeichen danach schadet nicht.

    `L:::0007.00m.7` ist eine richtige Goldlesung von 7,00 m. Eine Pruefung auf
    das Zeilenende hatte genau diesen belegten Wert verworfen.
    """
    from sidecar.osd_meter import _zeichenfolge_ist_vollstaendig

    for vollstaendig in ("LZ2:6?7m", "LZ2:11?2m", ":27?9m", "L:::0007.00m.7"):
        assert _zeichenfolge_ist_vollstaendig(vollstaendig), vollstaendig


def test_beschriftung_ohne_trenner_vor_zahl_ist_mehrdeutig():
    """`L211.7m1.` verlor in HD2 die erste Ziffer: 13,7 wurde zu 11,7 m."""
    from sidecar.osd_meter import _zeichenfolge_ist_vollstaendig

    assert not _zeichenfolge_ist_vollstaendig("L211.7m1.")


def test_zwei_dezimal_verlangt_die_einheit():
    """Ohne gelesenes "m" darf kein Wert entstehen.

    Die Einheit ist die einzige Sperre gegen Datumsbruchstuecke. Ein
    angeschnittenes Datumsfeld liefert Texte wie ".10.24" oder "16.24"; ohne
    Einheitspflicht waeren das die Meterstaende 10,24 m und 16,24 m. Am
    2026-08-09 auf 897 Archivbildern belegt.
    """
    from sidecar.osd_meter import _parse_zwei_dezimal

    for datum in (".10.24", "16.24", "06.24", "05.09", "07.11.23", "05.09.2023"):
        assert _parse_zwei_dezimal(datum) is None, datum


def test_zwei_dezimal_liest_punkt_und_komma():
    """Beide Schreibweisen kommen im Archiv vor."""
    from sidecar.osd_meter import _parse_zwei_dezimal

    assert _parse_zwei_dezimal("0.20m") == pytest.approx(0.20)
    assert _parse_zwei_dezimal("1,54m") == pytest.approx(1.54)
    assert _parse_zwei_dezimal("22,20 m") == pytest.approx(22.20)


def test_zwei_dezimal_faengt_kein_bruchstueck():
    """Vollstring-Anker: Zusatzzeichen vor oder nach der Zahl sind verdaechtig."""
    from sidecar.osd_meter import _parse_zwei_dezimal

    for muell in ("020m", "0.2m", "0.203m", "1234.56m", "", "m", "0.20"):
        assert _parse_zwei_dezimal(muell) is None, muell


def test_zwei_dezimal_bleibt_im_plausiblen_bereich():
    from sidecar.osd_meter import _parse_zwei_dezimal

    assert _parse_zwei_dezimal("400.00m") == pytest.approx(400.0)
    assert _parse_zwei_dezimal("999.99m") is None


def test_schwellenband_liegt_tief():
    """Ab etwa 0,48 des 95. Perzentils kippt die punktierte Null zur Acht.

    Gemessen am 2026-08-09 ueber elf Anteile auf drei Bildern: 0,20 wurde zu
    020, 22,20 zu 22,28 und 0,30 zu 0,38. Ein hoeheres Band gab dem Fehler die
    Mehrheit im Quorum.
    """
    from sidecar.osd_meter import ZWEI_DEZIMAL_ANTEILE, ZWEI_DEZIMAL_QUORUM

    assert max(ZWEI_DEZIMAL_ANTEILE) <= 0.46
    assert len(ZWEI_DEZIMAL_ANTEILE) == 5
    assert ZWEI_DEZIMAL_QUORUM == 3


# ---------------------------------------------------------------------------
# Vorzeichenstrich (2026-08-17): Der Zwei-Dezimal-Pfad kann ein Minus
# strukturell nicht ausdruecken - es steht nicht in ZWEI_DEZIMAL_WHITELIST, und
# der flache Strich faellt sowohl durch die Zeichenpruefung (verlangt h>=6) als
# auch durch die Satzzeichenpruefung (verlangt Grundlinie). Gemessen auf
# osd_mix_v1: -0,01 wurde als 0,01 geliefert und -2,41 als 2,41, jeweils mit
# vollem Quorum. Ein falscher Wert ist teurer als zehn fehlende, also verwerfen.
# ---------------------------------------------------------------------------

def _maske_mit_zeile(strich: tuple[int, int, int, int] | None = None):
    """Kuenstliche Maske: fuenf Ziffernbloecke, optional ein Strich davor.

    Reine Geometrie, kein Bild und kein Tesseract - der Veto arbeitet auf
    genau dieser Maske.
    """
    import numpy as np

    maske = np.zeros((40, 120), dtype="uint8")
    for k in range(5):                      # Ziffern: x=40,50,...  y=10..24
        maske[10:24, 40 + k * 10:46 + k * 10] = 255
    if strich is not None:
        x, y, breite, hoehe = strich
        maske[y:y + hoehe, x:x + breite] = 255
    return maske


def test_vorzeichenstrich_wird_erkannt():
    """Minus: flach, breit, links der Ziffern, auf halber Zeichenhoehe."""
    from sidecar.osd_meter import _hat_vorzeichenstrich

    # Ziffern beginnen bei x=40; Strich endet bei x=32 -> Abstand 8 (wie gemessen)
    assert _hat_vorzeichenstrich(_maske_mit_zeile((28, 16, 4, 2))) is True


def test_ohne_strich_kein_veto():
    from sidecar.osd_meter import _hat_vorzeichenstrich

    assert _hat_vorzeichenstrich(_maske_mit_zeile()) is False


def test_strich_direkt_an_der_ziffer_ist_kein_vorzeichen():
    """Abstand 0-2 heisst: Bruchstueck der Ziffer selbst, nicht eigenes Zeichen.
    Belegt an f0098 (Soll 5,6, Abstand 2) - ein Veto dort kostete einen
    richtigen Wert."""
    from sidecar.osd_meter import _hat_vorzeichenstrich

    assert _hat_vorzeichenstrich(_maske_mit_zeile((37, 16, 3, 1))) is False


def test_flacher_fleck_am_oberen_rand_ist_kein_vorzeichen():
    """Ein Minus sitzt nie am Oberrand der Zeichenhoehe. Belegt an f0101
    (Soll 0,15, Lage 0,07)."""
    from sidecar.osd_meter import _hat_vorzeichenstrich

    assert _hat_vorzeichenstrich(_maske_mit_zeile((28, 10, 6, 2))) is False


def test_hoher_fleck_ist_kein_vorzeichen():
    """Nur flach zaehlt - ein hoher Block links ist ein Zeichen oder Rauschen."""
    from sidecar.osd_meter import _hat_vorzeichenstrich

    assert _hat_vorzeichenstrich(_maske_mit_zeile((28, 12, 4, 8))) is False


def test_strich_rechts_der_zahl_ist_kein_vorzeichen():
    from sidecar.osd_meter import _hat_vorzeichenstrich

    assert _hat_vorzeichenstrich(_maske_mit_zeile((100, 16, 4, 2))) is False


def test_ohne_erkennbare_zeile_kein_veto():
    """Ohne mindestens drei Zeichen gibt es keine Zeile, an der ein Strich
    gemessen werden koennte - dann entscheidet der Veto nichts."""
    import numpy as np
    from sidecar.osd_meter import _hat_vorzeichenstrich

    maske = np.zeros((40, 120), dtype="uint8")
    maske[10:24, 40:46] = 255
    maske[16:18, 28:32] = 255
    assert _hat_vorzeichenstrich(maske) is False


# ---------------------------------------------------------------------------
# Vierziffern-Mehrheit (2026-08-17). Der Pfad nahm den ERSTEN vollstaendigen
# Treffer einer einzigen Schwelle. Eine verlesene Ziffer wurde dadurch mit
# voller Zuversicht geliefert: LZ1:+0021.70m ergab 24,7 (gemessen, f0067 in
# osd_mix_v1). Der Zwei-Dezimal-Pfad hat gegen genau diesen Fehler ein Quorum
# ueber fuenf Schwellen - dem Vierziffern-Pfad fehlte es.
#
# Gemessen ueber die 30 Bilder aller vier Goldsaetze, die diesen Pfad nutzen:
# Mehrheit ohne Mindestzahl bringt richtig 28 -> 29 und falsch 2 -> 1. Eine
# Mindestzahl von 2 Stimmen brachte falsch auf 0, kostete aber 8 richtige Werte
# (10 bei 3 Stimmen) - nach der Regel "ein falscher Wert ist teurer als zehn
# fehlende" gerade kein Gewinn mehr.
# ---------------------------------------------------------------------------

def test_mehrheit_waehlt_den_haeufigsten_wert():
    from sidecar.osd_meter import _mehrheit

    assert _mehrheit({24.7: 1, 21.7: 3}) == pytest.approx(21.7)


def test_mehrheit_ohne_stimmen_ist_none():
    from sidecar.osd_meter import _mehrheit

    assert _mehrheit({}) is None


def test_mehrheit_nimmt_auch_eine_einzelne_stimme():
    """Eine Mindeststimmenzahl kostete 8 belegte Goldwerte - Einzelstimmen
    sind auf diesem Pfad zu 10 von 11 Faellen richtig."""
    from sidecar.osd_meter import _mehrheit

    assert _mehrheit({3.2: 1}) == pytest.approx(3.2)


def test_mehrheit_entscheidet_gleichstand_deterministisch():
    """Gleichstand darf nicht von der Aufzaehlungsreihenfolge abhaengen -
    sonst ist eine Messung nicht wiederholbar."""
    from sidecar.osd_meter import _mehrheit

    assert _mehrheit({5.0: 2, 9.0: 2}) == _mehrheit({9.0: 2, 5.0: 2})


def test_vierziffern_faecher_liefert_mehr_schwellen_als_zuvor():
    """Ohne mehrere Schwellen gibt es nichts zu vergleichen."""
    import numpy as np
    from sidecar.osd_meter import _vierziffern_masken, _zeilenkandidaten

    # Helle Zeichenzeile auf dunklem Grund, genug Zeichen fuer den Kandidatentest
    z = np.zeros((40, 200, 3), dtype="uint8")
    for k in range(12):
        z[12:28, 8 + k * 15:14 + k * 15] = 230

    assert len(_vierziffern_masken(z)) > len(_zeilenkandidaten(z))


def test_verlesene_ziffer_wird_von_der_mehrheit_ueberstimmt(monkeypatch):
    """Der Kern des Fehlers: Die erste Schwelle darf nicht allein entscheiden.

    Nachgestellt ist f0067 - eine Schwelle liest 24.7, drei lesen 21.7.
    """
    import numpy as np
    from PIL import Image
    from sidecar import osd_meter

    monkeypatch.setattr(osd_meter, "_tesseract_pfad", lambda: "tesseract")
    monkeypatch.setattr(osd_meter, "_vierziffern_masken",
                        lambda _z: [np.zeros((10, 10), dtype="uint8")] * 4)
    texte = iter([": +0024.70m", ": +0021.70m", ": +0021.70m", ": +0021.70m"])
    monkeypatch.setattr(osd_meter, "_tesseract_aufrufen",
                        lambda *_a, **_k: next(texte, ""))

    meter, _text = osd_meter._lese_vierziffern_mit_tesseract(
        Image.new("RGB", (720, 576), (0, 0, 0)))

    assert meter == pytest.approx(21.7)


def test_vierziffern_bricht_ab_sobald_zwei_schwellen_uebereinstimmen(monkeypatch):
    """Der Pfad laeuft je Bild des Bogen-Copiloten. Zwei uebereinstimmende
    Stimmen genuegen; danach werden keine weiteren Prozesse gestartet."""
    import numpy as np
    from PIL import Image
    from sidecar import osd_meter

    aufrufe = []
    monkeypatch.setattr(osd_meter, "_tesseract_pfad", lambda: "tesseract")
    monkeypatch.setattr(osd_meter, "_vierziffern_masken",
                        lambda _z: [np.zeros((10, 10), dtype="uint8")] * 8)

    def falscher_aufruf(*_a, **_k):
        aufrufe.append(1)
        return ": +0007.00m"

    monkeypatch.setattr(osd_meter, "_tesseract_aufrufen", falscher_aufruf)

    meter, _text = osd_meter._lese_vierziffern_mit_tesseract(
        Image.new("RGB", (720, 576), (0, 0, 0)))

    assert meter == pytest.approx(7.0)
    assert len(aufrufe) == 2, f"{len(aufrufe)} Tesseract-Laeufe statt 2"
