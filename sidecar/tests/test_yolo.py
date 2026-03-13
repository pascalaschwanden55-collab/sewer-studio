"""Tests for YOLO pre-screening endpoint."""

import base64
import io

import pytest
from PIL import Image
from fastapi.testclient import TestClient


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
    assert data["status"] == "ok"
    assert "gpu" in data
    assert "current_model" in data["gpu"]
    assert "yolo" in data
    assert "configured_model_name" in data["yolo"]
    assert "require_custom_yolo" in data["yolo"]
    assert "using_custom_weights" in data["yolo"]


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
