"""Tests for SAM segmentation endpoint."""

import base64
import io

import pytest
from PIL import Image
from fastapi.testclient import TestClient


def _make_test_image(w: int = 320, h: int = 240) -> str:
    img = Image.new("RGB", (w, h), (200, 200, 200))
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


@pytest.fixture
def client():
    from sidecar.main import app
    return TestClient(app)


def test_sam_endpoint(client):
    """Smoke test: SAM endpoint responds with correct schema."""
    img_b64 = _make_test_image()
    resp = client.post("/segment/sam", json={
        "image_base64": img_b64,
        "bounding_boxes": [
            {"x1": 10, "y1": 10, "x2": 100, "y2": 100, "label": "crack", "confidence": 0.9}
        ],
        "pipe_diameter_mm": 300,
    })
    assert resp.status_code == 200
    data = resp.json()
    assert "masks" in data
    assert "image_width" in data
    assert "image_height" in data
