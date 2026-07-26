"""Health-Expose der Busy-/Waechter-Felder (Paket 3/A). Kein echtes Modell noetig."""

from fastapi.testclient import TestClient


def test_health_enthaelt_busy_und_watchdog_felder(tmp_path, monkeypatch):
    from sidecar.config import settings
    from sidecar.gpu_manager import ModelSlot, gpu_manager
    from sidecar.main import app

    monkeypatch.setattr(settings, "models_dir", str(tmp_path), raising=False)
    gpu_manager.ensure_loaded(ModelSlot.DINO, "cpu", lambda: (object(), None))
    lease = gpu_manager.acquire_busy(ModelSlot.DINO)
    try:
        response = TestClient(app).get("/health")

        assert response.status_code == 200
        gpu = response.json()["gpu"]
        assert gpu["loaded_models"]["dino"]["busy"] is True
        assert gpu["busy_slots"]["dino"] >= 0.0
        assert set(gpu["watchdog"].keys()) == {"enabled", "limit_sec"}
    finally:
        gpu_manager.release_busy(ModelSlot.DINO, lease)
        gpu_manager.unload(ModelSlot.DINO)


def test_health_meldet_freien_slot_als_nicht_busy(tmp_path, monkeypatch):
    from sidecar.config import settings
    from sidecar.gpu_manager import ModelSlot, gpu_manager
    from sidecar.main import app

    monkeypatch.setattr(settings, "models_dir", str(tmp_path), raising=False)
    gpu_manager.ensure_loaded(ModelSlot.SAM, "cpu", lambda: (object(), "predictor"))
    try:
        response = TestClient(app).get("/health")

        assert response.status_code == 200
        gpu = response.json()["gpu"]
        assert gpu["loaded_models"]["sam"]["busy"] is False
        assert gpu["busy_slots"] == {}
    finally:
        gpu_manager.unload(ModelSlot.SAM)
