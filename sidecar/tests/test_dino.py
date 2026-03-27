"""Tests for Grounding DINO detection endpoint."""

import base64
import io

import pytest
from PIL import Image
from fastapi.testclient import TestClient


def _make_test_image(w: int = 320, h: int = 240) -> str:
    img = Image.new("RGB", (w, h), (128, 128, 128))
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


@pytest.fixture
def client():
    from sidecar.main import app
    return TestClient(app)


def test_dino_endpoint(client):
    """Smoke test: DINO endpoint responds with correct schema."""
    img_b64 = _make_test_image()
    resp = client.post("/detect/dino", json={
        "image_base64": img_b64,
        "box_threshold": 0.30,
        "text_threshold": 0.25,
    })
    assert resp.status_code == 200
    data = resp.json()
    assert "detections" in data
    assert "inference_time_ms" in data
