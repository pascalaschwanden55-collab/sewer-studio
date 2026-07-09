from fastapi.testclient import TestClient


def _client():
    from sidecar.main import app

    return TestClient(app)


def test_health_degraded_when_dino_or_sam_weights_missing(tmp_path, monkeypatch):
    from sidecar.config import settings

    monkeypatch.setattr(settings, "models_dir", str(tmp_path), raising=False)

    response = _client().get("/health")

    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "degraded"
    assert data["models_present"] == {"dino": False, "sam": False}


def test_health_ok_when_required_weights_exist(tmp_path, monkeypatch):
    from sidecar.config import settings

    dino_dir = tmp_path / "grounding_dino_swinb"
    sam_dir = tmp_path / "sam2.1"
    dino_dir.mkdir()
    sam_dir.mkdir()
    (dino_dir / "model.pth").write_bytes(b"dino")
    (sam_dir / "sam2.1_hiera_large.pt").write_bytes(b"sam")
    monkeypatch.setattr(settings, "models_dir", str(tmp_path), raising=False)

    response = _client().get("/health")

    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "ok"
    assert data["models_present"] == {"dino": True, "sam": True}
