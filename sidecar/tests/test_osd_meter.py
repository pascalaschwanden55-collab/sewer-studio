"""Tests fuer sidecar.osd_meter: Formvalidator, Format-Lock, Ganzbild-Lesung.

Beleglage siehe docs/quality/OSD-METERLESER-VALIDIERUNG-2026-08-08.md:
71/71 gelieferte Werte richtig; die gescheiterte Sechs-Ziffern-Ratelei
('0.00.300' -> 3.0) kommt nicht wieder — sie ist hier als Test verankert.
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
