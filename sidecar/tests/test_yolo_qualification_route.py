"""The production YOLO route must obey detector qualification."""

from sidecar.routes import yolo
from sidecar.schemas.detection import (
    BccTestYoloResponse,
    YoloDetection,
    YoloRequest,
    YoloResponse,
)


def _qualification(*, qualified: bool) -> dict:
    return {
        "qualified": qualified,
        "status": "qualified" if qualified else "unqualified",
        "reason": None if qualified else "Altmodell nicht freigegeben.",
        "artifact": {
            "file_name": "detector.engine",
            "sha256": "a" * 64,
            "backend": "tensorrt",
            "loaded": False,
        },
        "marked_utc": "2026-07-25T00:00:00Z",
    }


def test_unqualified_standard_detector_returns_no_boxes_and_is_not_called(monkeypatch):
    detect_called = False

    def fail_if_called(*args, **kwargs):
        nonlocal detect_called
        detect_called = True
        raise AssertionError("unqualified detector must not run")

    monkeypatch.setattr(
        yolo.detector_qualification,
        "evaluate_active_detector",
        lambda: _qualification(qualified=False),
    )
    monkeypatch.setattr(yolo.yolo_wrapper, "detect", fail_if_called)
    monkeypatch.setattr(yolo.yolo_wrapper, "decode_image", lambda value: object())
    monkeypatch.setattr(
        yolo.yolo_wrapper,
        "get_runtime_status",
        lambda: {"device": "cuda:0"},
    )
    monkeypatch.setattr(yolo, "write_yolo_detection", lambda *args, **kwargs: None)

    response = yolo.detect_yolo(YoloRequest(image_base64="valid"))

    assert detect_called is False
    assert response.detections == []
    assert response.is_relevant is True
    assert response.frame_class == "detector_unqualified"
    assert response.detector_qualified is False
    assert response.detector_qualification_status == "unqualified"
    assert response.detector_artifact_sha256 == "a" * 64


def test_qualified_standard_detector_keeps_productive_boxes(monkeypatch):
    detection = YoloDetection(
        x1=1,
        y1=2,
        x2=30,
        y2=40,
        class_name="deformation",
        confidence=0.9,
    )
    monkeypatch.setattr(
        yolo.detector_qualification,
        "evaluate_active_detector",
        lambda: _qualification(qualified=True),
    )
    monkeypatch.setattr(
        yolo.yolo_wrapper,
        "detect",
        lambda **kwargs: YoloResponse(
            is_relevant=True,
            detections=[detection],
            frame_class="relevant",
        ),
    )
    monkeypatch.setattr(yolo, "write_yolo_detection", lambda *args, **kwargs: None)

    response = yolo.detect_yolo(YoloRequest(image_base64="valid"))

    assert len(response.detections) == 1
    assert response.detections[0].class_name == "deformation"
    assert response.detector_qualified is True
    assert response.detector_qualification_status == "qualified"


def test_bcc_test_endpoint_does_not_use_standard_qualification(monkeypatch):
    monkeypatch.setattr(
        yolo.detector_qualification,
        "evaluate_active_detector",
        lambda: (_ for _ in ()).throw(
            AssertionError("BCC test must stay separate from standard qualification")
        ),
    )
    monkeypatch.setattr(
        yolo.bcc_test_wrapper,
        "detect",
        lambda **kwargs: BccTestYoloResponse(
            available=True,
            candidate_id="bcc-candidate",
        ),
    )
    monkeypatch.setattr(yolo, "write_event", lambda *args, **kwargs: None)

    response = yolo.detect_yolo_bcc_test(YoloRequest(image_base64="valid"))

    assert response.available is True
    assert response.candidate_id == "bcc-candidate"
