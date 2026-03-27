"""End-to-end pipeline test."""

import base64
import io

import pytest
from PIL import Image
from fastapi.testclient import TestClient


def _make_test_image(w: int = 640, h: int = 480) -> str:
    img = Image.new("RGB", (w, h), (100, 100, 100))
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


@pytest.fixture
def client():
    from sidecar.main import app
    return TestClient(app)


def test_full_pipeline_health_then_yolo(client):
    """Verify health -> YOLO flow works sequentially."""
    # Health check
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.json()["status"] == "ok"

    # YOLO detection
    img_b64 = _make_test_image()
    resp = client.post("/detect/yolo", json={
        "image_base64": img_b64,
        "confidence_threshold": 0.25,
    })
    assert resp.status_code == 200
    data = resp.json()
    assert isinstance(data["detections"], list)


def test_training_export(client):
    """Smoke test: training export creates valid response."""
    img_b64 = _make_test_image(w=100, h=100)
    resp = client.post("/training/export-yolo", json={
        "samples": [
            {
                "image_base64": img_b64,
                "labels": [
                    {"class_name": "crack", "x_center": 0.5, "y_center": 0.5, "width": 0.2, "height": 0.1}
                ],
            }
        ],
        "output_dir": "./test_export_tmp",
        "train_split": 0.8,
    })
    assert resp.status_code == 200
    data = resp.json()
    assert data["total_samples"] == 1
    assert len(data["classes_used"]) > 0
