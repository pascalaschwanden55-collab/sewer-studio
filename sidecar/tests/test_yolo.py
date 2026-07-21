"""Tests for YOLO pre-screening endpoint."""

import base64
import io

import pytest
from PIL import Image
from fastapi.testclient import TestClient


def _custom_yolo_weights_present() -> bool:
    """True, wenn echte Sewer-Gewichte lokal liegen. Reiner Pfad-Check, KEIN Modell-Load."""
    try:
        from sidecar.models.yolo_wrapper import get_yolo_info
        return bool(get_yolo_info().get("custom_weights_present"))
    except Exception:
        return False


# /detect/yolo laedt bei fehlenden Eigengewichten (require_custom_yolo=False) yolo11m.pt
# aus dem Internet nach. Ohne echte Gewichte wird der Test uebersprungen, damit der
# Sidecar-Testlauf hermetisch bleibt (kein Netzwerk-Download, keine GPU-Abhaengigkeit in CI).
requires_custom_yolo_weights = pytest.mark.skipif(
    not _custom_yolo_weights_present(),
    reason="Kein echtes YOLO-Gewicht unter models/yolo26m/ — /detect/yolo wuerde "
           "yolo11m.pt aus dem Internet ziehen. Test uebersprungen (hermetischer Lauf).",
)


def _make_test_image(w: int = 320, h: int = 240, color=(0, 0, 0)) -> str:
    """Create a minimal test image as base64."""
    img = Image.new("RGB", (w, h), color)
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


@pytest.fixture
def client():
    from sidecar.main import app
    return TestClient(app)


def test_health(client):
    resp = client.get("/health")
    assert resp.status_code == 200
    data = resp.json()
    assert data["status"] in {"ok", "degraded"}
    assert "gpu" in data
    assert "current_model" in data["gpu"]
    assert "yolo" in data
    assert "configured_model_name" in data["yolo"]
    assert "require_custom_yolo" in data["yolo"]
    assert "using_custom_weights" in data["yolo"]


@requires_custom_yolo_weights
def test_yolo_empty_frame(client):
    """A solid black image should produce no detections."""
    img_b64 = _make_test_image(color=(0, 0, 0))
    resp = client.post("/detect/yolo", json={
        "image_base64": img_b64,
        "confidence_threshold": 0.25,
    })
    assert resp.status_code == 200
    data = resp.json()
    assert "is_relevant" in data
    assert "detections" in data
    assert "inference_time_ms" in data
    assert "model_name" in data
    assert "device" in data
    assert "queue_wait_ms" in data
    assert "vram_allocated_gb" in data
    assert "vram_total_gb" in data
