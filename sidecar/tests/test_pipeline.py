"""End-to-end pipeline test."""

import base64
import hashlib
import io
import json

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
    assert resp.json()["status"] in {"ok", "degraded"}

    # YOLO detection
    img_b64 = _make_test_image()
    resp = client.post("/detect/yolo", json={
        "image_base64": img_b64,
        "confidence_threshold": 0.25,
    })
    assert resp.status_code == 200
    data = resp.json()
    assert isinstance(data["detections"], list)


def test_training_export(client, tmp_path, monkeypatch):
    """Smoke-Test: Ein fertiger Plan wird ohne eigene Entscheidungen ausgefuehrt."""
    from sidecar.config import settings
    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)

    img_b64 = _make_test_image(w=100, h=100)
    image_bytes = base64.b64decode(img_b64)
    image_sha256 = hashlib.sha256(image_bytes).hexdigest()
    plan_id = "a" * 64
    plan_sha256 = plan_id
    sample = {
        "image_sha256": image_sha256,
        "image_base64": img_b64,
        "split": "train",
        "target_file_name": f"img_{image_sha256}.png",
        "labels": [
            {"class_id": 0, "x_center": 0.5, "y_center": 0.5, "width": 0.2, "height": 0.1}
        ],
    }
    manifest = (
        json.dumps(
            {
                "schema_version": "2.0",
                "plan_id": plan_id,
                "class_map_version": 2,
                "vsa_manifest_hash": "c" * 64,
                "registry_hash": "d" * 64,
                "classes": ["BAB_riss"],
                "images": [
                    {
                        "image_sha256": image_sha256,
                        "target": "train",
                        "target_file_name": f"img_{image_sha256}.png",
                        "labels": [
                            {
                                "class_id": 0,
                                "class_name": "BAB_riss",
                                "bounding_box": {
                                    "x_center": 0.5,
                                    "y_center": 0.5,
                                    "width": 0.2,
                                    "height": 0.1,
                                },
                            }
                        ],
                    }
                ],
            },
            sort_keys=True,
        )
        + "\n"
    ).encode()
    resp = client.post("/training/export-yolo", json={
        "schema_version": "2.0",
        "plan_id": plan_id,
        "plan_sha256": plan_sha256,
        "class_map_version": 2,
        "vsa_manifest_hash": "c" * 64,
        "registry_hash": "d" * 64,
        "classes": ["BAB_riss"],
        "manifest_json_base64": base64.b64encode(manifest).decode(),
        "manifest_sha256": hashlib.sha256(manifest).hexdigest(),
        "samples": [sample],
    })
    assert resp.status_code == 200
    data = resp.json()
    assert data["total_samples"] == 1
    assert data["class_count"] == 1
    assert data["status"] == "created"
