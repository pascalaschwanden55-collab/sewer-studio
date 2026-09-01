import hashlib
import json
import os

from fastapi.testclient import TestClient


def _client():
    from sidecar.main import app

    return TestClient(app)


def test_health_runs_detector_hash_check_outside_event_loop(monkeypatch):
    from sidecar.routes import health as health_route

    calls = []

    async def recording_to_thread(function, *args, **kwargs):
        calls.append(function)
        return function(*args, **kwargs)

    monkeypatch.setattr(health_route.asyncio, "to_thread", recording_to_thread)
    monkeypatch.setattr(
        health_route.detector_qualification,
        "evaluate_active_detector",
        lambda: {"qualified": False, "status": "test"},
    )

    response = _client().get("/health")

    assert response.status_code == 200
    assert calls == [health_route.detector_qualification.evaluate_active_detector]


def test_health_degraded_when_dino_or_sam_weights_missing(tmp_path, monkeypatch):
    from sidecar.config import settings

    monkeypatch.setattr(settings, "models_dir", str(tmp_path), raising=False)

    response = _client().get("/health")

    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "degraded"
    assert data["process_id"] == os.getpid()
    assert data["models_present"] == {"dino": False, "sam": False}
    assert "dino" in data["status_detail"] and "sam" in data["status_detail"]


def _write_dino_sam_weights(tmp_path):
    dino_dir = tmp_path / "grounding_dino_swinb"
    sam_dir = tmp_path / "sam2.1"
    dino_dir.mkdir()
    sam_dir.mkdir()
    (dino_dir / "model.pth").write_bytes(b"dino")
    (sam_dir / "sam2.1_hiera_large.pt").write_bytes(b"sam")


def _configure_detector(tmp_path, monkeypatch, *, qualified, write_marker=True):
    from sidecar.config import settings
    from sidecar.models import yolo_wrapper

    weights_dir = tmp_path / "yolo26m"
    weights_dir.mkdir(exist_ok=True)
    weights = weights_dir / "detector.pt"
    weights.write_bytes(b"detector-weights")
    sha256 = hashlib.sha256(weights.read_bytes()).hexdigest()

    monkeypatch.setattr(settings, "models_dir", str(tmp_path), raising=False)
    monkeypatch.setattr(settings, "yolo_model_name", weights.name, raising=False)
    monkeypatch.setattr(settings, "require_custom_yolo", True, raising=False)
    monkeypatch.setattr(
        yolo_wrapper,
        "get_active_detector_artifact",
        lambda: {
            "path": str(weights),
            "file_name": weights.name,
            "backend": "pytorch",
            "using_custom_weights": True,
            "loaded": False,
            "resolution_error": None,
        },
    )

    if write_marker:
        reason = None if qualified else "Altmodell: BBox-Kollaps."
        (tmp_path / "model_qualification.json").write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "detector": {
                        "qualified": qualified,
                        "reason": reason,
                        "marked_utc": "2026-07-25T00:00:00Z",
                        "artifacts": [
                            {
                                "file_name": weights.name,
                                "sha256": sha256,
                                "backend": "pytorch",
                            }
                        ],
                    },
                }
            ),
            encoding="utf-8",
        )


def test_health_ok_when_required_weights_exist(tmp_path, monkeypatch):
    from sidecar.models import yolo_wrapper

    _write_dino_sam_weights(tmp_path)
    _configure_detector(tmp_path, monkeypatch, qualified=True)
    # "ok" erst, wenn auch der Klassifikator geladen ist — hier simuliert.
    monkeypatch.setattr(
        yolo_wrapper, "get_classifier_status", lambda: {"loaded": True, "name": "cls"})

    response = _client().get("/health")

    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "ok"
    assert data["status_detail"] == "all_models_ready"
    assert data["models_present"] == {"dino": True, "sam": True}


def test_health_degraded_when_classifier_not_loaded(tmp_path, monkeypatch):
    from sidecar.models import yolo_wrapper

    _write_dino_sam_weights(tmp_path)
    _configure_detector(tmp_path, monkeypatch, qualified=True)
    # DINO/SAM vorhanden, aber Klassifikator nicht geladen -> degraded (Warnung).
    monkeypatch.setattr(
        yolo_wrapper,
        "get_classifier_status",
        lambda: {"loaded": False, "active_json_present": False, "override_configured": False})

    response = _client().get("/health")

    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "degraded"
    assert data["status_detail"] == "classifier_not_loaded"
    assert data["models_present"] == {"dino": True, "sam": True}


def test_health_detector_qualification_fails_closed_when_status_file_missing(
    tmp_path, monkeypatch
):
    from sidecar.models import yolo_wrapper

    _write_dino_sam_weights(tmp_path)
    _configure_detector(tmp_path, monkeypatch, qualified=True, write_marker=False)
    monkeypatch.setattr(
        yolo_wrapper, "get_classifier_status", lambda: {"loaded": True, "name": "cls"})

    response = _client().get("/health")

    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "degraded"
    assert data["status_detail"] == "detector_unqualified:status_file_missing"
    assert data["detector_qualification"]["qualified"] is False
    assert data["detector_qualification"]["status"] == "status_file_missing"


def test_health_detector_qualification_unqualified_from_status_file(tmp_path, monkeypatch):
    from sidecar.models import yolo_wrapper

    _write_dino_sam_weights(tmp_path)
    _configure_detector(tmp_path, monkeypatch, qualified=False)
    monkeypatch.setattr(
        yolo_wrapper, "get_classifier_status", lambda: {"loaded": True, "name": "cls"})

    response = _client().get("/health")

    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "degraded"
    assert data["status_detail"] == "detector_unqualified:unqualified"
    assert data["detector_qualification"]["qualified"] is False
    assert data["detector_qualification"]["status"] == "unqualified"
    assert data["detector_qualification"]["reason"] == "Altmodell: BBox-Kollaps."
    assert data["detector_qualification"]["artifact"]["sha256"]


def test_health_detector_qualification_broken_status_file_is_unqualified(tmp_path, monkeypatch):
    from sidecar.models import yolo_wrapper

    _write_dino_sam_weights(tmp_path)
    _configure_detector(tmp_path, monkeypatch, qualified=True, write_marker=False)
    (tmp_path / "model_qualification.json").write_text("{ kaputt", encoding="utf-8")
    monkeypatch.setattr(
        yolo_wrapper, "get_classifier_status", lambda: {"loaded": True, "name": "cls"})

    response = _client().get("/health")

    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "degraded"
    assert data["status_detail"] == "detector_unqualified:status_file_unreadable"
    assert data["detector_qualification"]["qualified"] is False
    assert data["detector_qualification"]["status"] == "status_file_unreadable"
