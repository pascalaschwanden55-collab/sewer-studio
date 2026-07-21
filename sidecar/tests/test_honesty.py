"""GPU-freie Tests fuer das degraded-Verhalten (Audit-Befund #1: keine stillen Befundverluste).

Diese Tests laden KEIN echtes Modell und brauchen KEINE GPU: gpu_manager.ensure_loaded
wird gestubt, die eigentliche Inferenz (groundingdino.predict bzw. SAM2ImagePredictor) gefaket.
Geprueft wird ausschliesslich der Ehrlichkeits-Vertrag:
  - kein Befund            -> degraded=False, leere Liste erlaubt
  - Modell-/Inferenzfehler -> degraded=True (NICHT als sauberer Negativbefund getarnt)
  - uebersprungene Boxen   -> degraded=True mit skipped_boxes > 0
"""

import base64
import io
import sys
import types
from types import SimpleNamespace

import numpy as np
import pytest
from PIL import Image
from fastapi.testclient import TestClient


def _img(w: int = 320, h: int = 240) -> str:
    buf = io.BytesIO()
    Image.new("RGB", (w, h), (128, 128, 128)).save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


@pytest.fixture
def client():
    from sidecar.main import app
    return TestClient(app)


# ── DINO ─────────────────────────────────────────────────────────────────────

def _fake_dino(monkeypatch, predict_impl):
    """ensure_loaded stubben + groundingdino.util.inference.predict faken (paketunabhaengig)."""
    from sidecar.gpu_manager import gpu_manager
    monkeypatch.setattr(
        gpu_manager, "ensure_loaded",
        lambda slot, device, loader: SimpleNamespace(model=object(), processor=None),
    )
    pkg = types.ModuleType("groundingdino")
    util = types.ModuleType("groundingdino.util")
    inf = types.ModuleType("groundingdino.util.inference")
    inf.predict = predict_impl
    pkg.util = util
    util.inference = inf
    monkeypatch.setitem(sys.modules, "groundingdino", pkg)
    monkeypatch.setitem(sys.modules, "groundingdino.util", util)
    monkeypatch.setitem(sys.modules, "groundingdino.util.inference", inf)


def test_dino_degraded_true_on_inference_error(client, monkeypatch):
    def _boom(**kw):
        raise RuntimeError("boom")
    _fake_dino(monkeypatch, _boom)

    resp = client.post("/detect/dino", json={"image_base64": _img()})
    assert resp.status_code == 200
    data = resp.json()
    assert data["degraded"] is True
    assert data["detections"] == []
    assert data["error_code"] == "dino_inference_failed"
    assert data["error"]


def test_dino_degraded_false_on_clean_empty(client, monkeypatch):
    def _empty(**kw):
        return [], [], []
    _fake_dino(monkeypatch, _empty)

    resp = client.post("/detect/dino", json={"image_base64": _img()})
    assert resp.status_code == 200
    data = resp.json()
    assert data["degraded"] is False          # kein Befund != Fehler
    assert data["detections"] == []
    assert data["error"] is None


def test_dino_oom_wird_als_503_statt_degraded(monkeypatch):
    # Ein OOM-Fehler darf NICHT als degraded-200 verschluckt werden — er muss den zentralen
    # Handler erreichen (VRAM-Erholung + 503), sonst bleibt der Sidecar im OOM-Zustand.
    from sidecar.main import app
    def _oom(**kw):
        raise RuntimeError("CUDA out of memory")
    _fake_dino(monkeypatch, _oom)

    # raise_server_exceptions=False: Handler-Response (503) statt Re-Raise im Test.
    resp = TestClient(app, raise_server_exceptions=False).post(
        "/detect/dino", json={"image_base64": _img()})
    assert resp.status_code == 503
    assert "out of memory" in resp.json()["detail"].lower()


# ── SAM ──────────────────────────────────────────────────────────────────────

def _fake_sam(monkeypatch, predictor):
    from sidecar.gpu_manager import gpu_manager
    monkeypatch.setattr(
        gpu_manager, "ensure_loaded",
        lambda slot, device, loader: SimpleNamespace(model=object(), processor=predictor),
    )


def _box(x1=10, y1=10, x2=100, y2=100, label="a"):
    return {"x1": x1, "y1": y1, "x2": x2, "y2": y2, "label": label, "confidence": 0.9}


def test_sam_degraded_when_all_boxes_fail(client, monkeypatch):
    class FailPredictor:
        def set_image(self, arr):
            pass

        def predict(self, **kw):
            raise RuntimeError("boom")

    _fake_sam(monkeypatch, FailPredictor())
    resp = client.post("/segment/sam", json={
        "image_base64": _img(),
        "bounding_boxes": [_box(), _box(20, 20, 120, 120, "b")],
    })
    assert resp.status_code == 200
    data = resp.json()
    assert data["requested_boxes"] == 2
    assert data["skipped_boxes"] == 2
    assert data["degraded"] is True
    assert data["masks"] == []


def test_sam_oom_wird_als_503_statt_uebersprungen(monkeypatch):
    # OOM in der Box-Schleife muss den zentralen Handler erreichen (503), statt die Box nur als
    # uebersprungen zu zaehlen und den Sidecar im OOM-Zustand zu belassen.
    from sidecar.main import app
    class OomPredictor:
        def set_image(self, arr):
            pass

        def predict(self, **kw):
            raise RuntimeError("CUDA out of memory")

    _fake_sam(monkeypatch, OomPredictor())
    # raise_server_exceptions=False: Handler-Response (503) statt Re-Raise im Test.
    resp = TestClient(app, raise_server_exceptions=False).post("/segment/sam", json={
        "image_base64": _img(),
        "bounding_boxes": [_box()],
    })
    assert resp.status_code == 503
    assert "out of memory" in resp.json()["detail"].lower()


def test_sam_not_degraded_on_clean_success(client, monkeypatch):
    class OkPredictor:
        def set_image(self, arr):
            pass

        def predict(self, **kw):
            mask = np.zeros((240, 320), dtype=bool)
            mask[20:40, 20:40] = True
            return mask[None, :, :], np.array([0.9]), None

    _fake_sam(monkeypatch, OkPredictor())
    resp = client.post("/segment/sam", json={
        "image_base64": _img(),
        "bounding_boxes": [_box()],
    })
    assert resp.status_code == 200
    data = resp.json()
    assert data["requested_boxes"] == 1
    assert data["skipped_boxes"] == 0
    assert data["degraded"] is False
    assert len(data["masks"]) == 1


def test_sam_low_score_mask_wird_verworfen(client, monkeypatch):
    """Score-Gate: Maske unter sam_min_score -> skipped/low_score/degraded, kein Befund."""
    class LowScorePredictor:
        def set_image(self, arr):
            pass

        def predict(self, **kw):
            mask = np.zeros((240, 320), dtype=bool)
            mask[20:40, 20:40] = True
            return mask[None, :, :], np.array([0.2]), None

    from sidecar.config import settings
    monkeypatch.setattr(settings, "sam_min_score", 0.5)
    _fake_sam(monkeypatch, LowScorePredictor())
    resp = client.post("/segment/sam", json={
        "image_base64": _img(),
        "bounding_boxes": [_box()],
    })
    assert resp.status_code == 200
    data = resp.json()
    assert data["requested_boxes"] == 1
    assert data["skipped_boxes"] == 1
    assert data["low_score_boxes"] == 1
    assert data["degraded"] is True
    assert data["masks"] == []


def test_sam_score_gate_aus_behaelt_altverhalten(client, monkeypatch):
    """sam_min_score=0.0 schaltet das Gate ab: auch unsichere Masken kommen durch."""
    class LowScorePredictor:
        def set_image(self, arr):
            pass

        def predict(self, **kw):
            mask = np.zeros((240, 320), dtype=bool)
            mask[20:40, 20:40] = True
            return mask[None, :, :], np.array([0.2]), None

    from sidecar.config import settings
    monkeypatch.setattr(settings, "sam_min_score", 0.0)
    _fake_sam(monkeypatch, LowScorePredictor())
    resp = client.post("/segment/sam", json={
        "image_base64": _img(),
        "bounding_boxes": [_box()],
    })
    assert resp.status_code == 200
    data = resp.json()
    assert data["skipped_boxes"] == 0
    assert data["low_score_boxes"] == 0
    assert data["degraded"] is False
    assert len(data["masks"]) == 1


def test_sam_empty_boxes_is_clean_not_degraded(client, monkeypatch):
    class UnusedPredictor:
        def set_image(self, arr):
            pass

        def predict(self, **kw):
            raise AssertionError("predict darf bei 0 Boxen nicht aufgerufen werden")

    _fake_sam(monkeypatch, UnusedPredictor())
    resp = client.post("/segment/sam", json={"image_base64": _img(), "bounding_boxes": []})
    assert resp.status_code == 200
    data = resp.json()
    assert data["requested_boxes"] == 0
    assert data["skipped_boxes"] == 0
    assert data["degraded"] is False
    assert data["masks"] == []
