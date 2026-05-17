"""Pure-Function-Tests fuer YOLO-Wrapper-Helfer.

Audit 2026-05-17: sichert den Codex-Patch fuer die BCC→BCA-Heuristik
gegen Regression.

``_correct_lateral_connection_label`` korrigiert YOLO-BCC-Labels (Bogen)
zu BCA (Anschluss), wenn das Bild zwei dunkle Oeffnungen zeigt
(typische Seitenanschluss-Frames).

Die Bild-Heuristik ``_looks_like_lateral_connection`` ist schwer mit
synthetischen Bildern zu kalibrieren. Wir mocken sie und testen so die
isolierte Korrektur-Logik.
"""

from __future__ import annotations

from PIL import Image

from sidecar.models import yolo_wrapper
from sidecar.models.yolo_wrapper import _correct_lateral_connection_label
from sidecar.schemas.detection import YoloDetection


def _dummy_image() -> Image.Image:
    return Image.new("RGB", (640, 480), (180, 180, 180))


def _bcc_detection(conf: float = 0.95) -> YoloDetection:
    return YoloDetection(
        x1=100.0, y1=100.0, x2=300.0, y2=300.0,
        class_name="BCC",
        confidence=conf,
    )


def _baf_detection() -> YoloDetection:
    return YoloDetection(
        x1=200.0, y1=200.0, x2=400.0, y2=400.0,
        class_name="BAF",
        confidence=0.80,
    )


def test_correct_bcc_zu_bca_wenn_heuristik_true(monkeypatch):
    monkeypatch.setattr(yolo_wrapper, "_looks_like_lateral_connection", lambda _img: True)

    corrected, was_changed = _correct_lateral_connection_label(_dummy_image(), [_bcc_detection()])

    assert was_changed is True
    assert corrected[0].class_name == "BCA"
    assert corrected[0].confidence <= 0.92   # cap
    # BBox bleibt unveraendert
    assert corrected[0].x1 == 100.0
    assert corrected[0].x2 == 300.0


def test_bcc_bleibt_wenn_heuristik_false(monkeypatch):
    monkeypatch.setattr(yolo_wrapper, "_looks_like_lateral_connection", lambda _img: False)

    corrected, was_changed = _correct_lateral_connection_label(_dummy_image(), [_bcc_detection()])

    assert was_changed is False
    assert corrected[0].class_name == "BCC"
    assert corrected[0].confidence == 0.95


def test_andere_labels_werden_nicht_angetastet(monkeypatch):
    monkeypatch.setattr(yolo_wrapper, "_looks_like_lateral_connection", lambda _img: True)

    detections = [_baf_detection(), _bcc_detection()]
    corrected, was_changed = _correct_lateral_connection_label(_dummy_image(), detections)

    assert was_changed is True
    # BAF bleibt BAF mit voller Confidence
    assert corrected[0].class_name == "BAF"
    assert corrected[0].confidence == 0.80
    # BCC wurde zu BCA mit Cap
    assert corrected[1].class_name == "BCA"
    assert corrected[1].confidence <= 0.92


def test_confidence_cap_nur_wenn_drueber(monkeypatch):
    monkeypatch.setattr(yolo_wrapper, "_looks_like_lateral_connection", lambda _img: True)

    # Niedrige Confidence darf nicht hochgesetzt werden — nur deckeln, nicht aufwerten.
    low_conf = YoloDetection(x1=0, y1=0, x2=10, y2=10, class_name="BCC", confidence=0.42)
    corrected, _ = _correct_lateral_connection_label(_dummy_image(), [low_conf])

    assert corrected[0].confidence == 0.42  # bleibt unter Cap


def test_leere_detection_liste_keine_aenderung():
    # Heuristik wird gar nicht erst aufgerufen wenn keine Detections da.
    corrected, was_changed = _correct_lateral_connection_label(_dummy_image(), [])
    assert corrected == []
    assert was_changed is False


def test_kleines_bild_keine_korrektur():
    """Heuristik schuetzt sich selber bei < 100x100 → was_changed=False."""
    small = Image.new("RGB", (50, 50), (180, 180, 180))
    corrected, was_changed = _correct_lateral_connection_label(small, [_bcc_detection()])
    assert was_changed is False
    assert corrected[0].class_name == "BCC"
