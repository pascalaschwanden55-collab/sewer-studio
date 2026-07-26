import hashlib
import json

from fastapi.testclient import TestClient


def _write_candidate(
    root,
    candidate_id: str,
    *,
    map50: float,
    epochs: int = 40,
    status: str = "not_deployed",
    pilot: str = "BCC_bogen",
    expected_sha: str | None = None,
):
    candidate = root / candidate_id
    candidate.mkdir()
    weights = candidate / "best.pt"
    weights.write_bytes(f"weights-{candidate_id}".encode())
    actual_sha = hashlib.sha256(weights.read_bytes()).hexdigest()
    manifest = {
        "schema_version": "1.0",
        "candidate_status": status,
        "pilot": pilot,
        "created_utc": "2026-07-24T12:00:00+00:00",
        "dataset": {"images": 48},
        "training": {
            "epochs_completed": epochs,
            "results": {"metrics/mAP50(B)": map50},
        },
        "weights": {"candidate_sha256": expected_sha or actual_sha},
    }
    (candidate / "candidate_manifest.json").write_text(
        json.dumps(manifest),
        encoding="utf-8",
    )
    return actual_sha


def test_select_candidate_waehlt_bestes_gueltiges_not_deployed_modell(
    tmp_path,
    monkeypatch,
):
    from sidecar.config import settings
    from sidecar.models import bcc_test_wrapper

    _write_candidate(tmp_path, "bcc_weak", map50=0.25, epochs=5)
    best_sha = _write_candidate(tmp_path, "bcc_full40", map50=0.76, epochs=40)
    _write_candidate(tmp_path, "bcc_active", map50=0.99, status="deployed")
    _write_candidate(tmp_path, "bcc_bad_hash", map50=0.98, expected_sha="0" * 64)
    _write_candidate(tmp_path, "other_pilot", map50=0.97, pilot="BAB_riss")
    monkeypatch.setattr(
        settings,
        "training_model_candidates_root",
        str(tmp_path),
        raising=False,
    )

    selected = bcc_test_wrapper.select_candidate()

    assert selected.candidate_id == "bcc_full40"
    assert selected.weights_sha256 == best_sha
    assert selected.epochs_completed == 40


def test_select_candidate_ohne_gueltiges_modell_sperrt_fail_closed(
    tmp_path,
    monkeypatch,
):
    import pytest

    from sidecar.config import settings
    from sidecar.models import bcc_test_wrapper

    _write_candidate(tmp_path, "bcc_bad_hash", map50=0.8, expected_sha="f" * 64)
    monkeypatch.setattr(
        settings,
        "training_model_candidates_root",
        str(tmp_path),
        raising=False,
    )

    with pytest.raises(
        bcc_test_wrapper.BccTestCandidateError,
        match="Kein gültiges",
    ):
        bcc_test_wrapper.select_candidate()


def test_bcc_test_route_ist_getrennt_vom_produktiven_yolo(monkeypatch):
    from sidecar.main import app
    from sidecar.models import bcc_test_wrapper
    from sidecar.schemas.detection import BccTestYoloResponse, YoloDetection

    called = {}

    def fake_detect(image_base64: str, confidence_threshold: float):
        called["image"] = image_base64
        called["threshold"] = confidence_threshold
        return BccTestYoloResponse(
            available=True,
            is_relevant=True,
            detections=[
                YoloDetection(
                    x1=10,
                    y1=20,
                    x2=100,
                    y2=200,
                    class_name="BCC_bogen",
                    confidence=0.88,
                )
            ],
            frame_class="relevant",
            inference_time_ms=12.5,
            candidate_id="bcc_full40",
            candidate_sha256="a" * 64,
            model_name="bcc_full40",
            device="cuda:0",
        )

    monkeypatch.setattr(bcc_test_wrapper, "detect", fake_detect)

    response = TestClient(app).post(
        "/detect/yolo/bcc-test",
        json={"image_base64": "abc", "confidence_threshold": 0.3},
    )

    assert response.status_code == 200
    payload = response.json()
    assert payload["candidate_id"] == "bcc_full40"
    assert payload["detections"][0]["class_name"] == "BCC_bogen"
    assert called == {"image": "abc", "threshold": 0.3}


def test_bcc_test_route_meldet_fehlenden_kandidaten_ohne_500(monkeypatch):
    from sidecar.main import app
    from sidecar.models import bcc_test_wrapper

    def unavailable(*_args, **_kwargs):
        raise bcc_test_wrapper.BccTestCandidateError(
            "Kein gültiges, nicht aktives BCC-Testmodell gefunden."
        )

    monkeypatch.setattr(bcc_test_wrapper, "detect", unavailable)

    response = TestClient(app).post(
        "/detect/yolo/bcc-test",
        json={"image_base64": "abc", "confidence_threshold": 0.25},
    )

    assert response.status_code == 200
    assert response.json()["available"] is False
    assert "nicht aktives" in response.json()["error"]
