"""Smoke-Tests fuer Batch-Endpunkte (braucht laufenden Sidecar)."""

import base64
import os
import pytest
import httpx

SIDECAR_URL = os.environ.get("SIDECAR_URL", "http://localhost:8100")
TEST_IMAGE_DIR = os.environ.get("TEST_IMAGE_DIR", r"C:\KI_BRAIN\fewshot_images")


def _get_test_image_b64() -> str:
    """Erstes PNG-Bild aus fewshot_images als base64."""
    images = [f for f in os.listdir(TEST_IMAGE_DIR) if f.endswith(".png")]
    if not images:
        pytest.skip("Keine Test-Bilder in " + TEST_IMAGE_DIR)
    path = os.path.join(TEST_IMAGE_DIR, images[0])
    with open(path, "rb") as f:
        return base64.b64encode(f.read()).decode()


@pytest.fixture(scope="module")
def image_b64():
    return _get_test_image_b64()


@pytest.fixture(scope="module")
def client():
    with httpx.Client(base_url=SIDECAR_URL, timeout=60) as c:
        yield c


class TestYoloBatch:
    def test_batch_returns_correct_count(self, client, image_b64):
        resp = client.post("/detect/yolo/batch", json={
            "items": [
                {"image_base64": image_b64, "frame_id": "f1"},
                {"image_base64": image_b64, "frame_id": "f2"},
            ],
            "confidence_threshold": 0.25,
        })
        assert resp.status_code == 200
        data = resp.json()
        assert len(data["results"]) == 2
        assert data["results"][0]["frame_id"] == "f1"
        assert data["results"][1]["frame_id"] == "f2"
        assert data["total_inference_time_ms"] > 0

    def test_batch_single_item(self, client, image_b64):
        resp = client.post("/detect/yolo/batch", json={
            "items": [{"image_base64": image_b64, "frame_id": "single"}],
            "confidence_threshold": 0.25,
        })
        assert resp.status_code == 200
        assert len(resp.json()["results"]) == 1


class TestDinoBatch:
    def test_batch_returns_results(self, client, image_b64):
        resp = client.post("/detect/dino/batch", json={
            "items": [
                {"image_base64": image_b64, "frame_id": "d1"},
            ],
            "box_threshold": 0.30,
            "text_threshold": 0.25,
        })
        assert resp.status_code == 200
        data = resp.json()
        assert len(data["results"]) == 1
        assert data["results"][0]["frame_id"] == "d1"


class TestSamBatch:
    def test_batch_with_yolo_boxes(self, client, image_b64):
        # Erst YOLO fuer Boxen
        yolo_resp = client.post("/detect/yolo", json={
            "image_base64": image_b64,
            "confidence_threshold": 0.25,
        })
        detections = yolo_resp.json().get("detections", [])
        boxes = [
            {"x1": d["x1"], "y1": d["y1"], "x2": d["x2"], "y2": d["y2"],
             "label": d["class_name"]}
            for d in detections[:3]
        ]

        resp = client.post("/segment/sam/batch", json={
            "items": [
                {"image_base64": image_b64, "bounding_boxes": boxes, "frame_id": "s1"},
            ],
        })
        assert resp.status_code == 200
        data = resp.json()
        assert len(data["results"]) == 1
        assert data["results"][0]["frame_id"] == "s1"

    def test_batch_empty_boxes(self, client, image_b64):
        resp = client.post("/segment/sam/batch", json={
            "items": [
                {"image_base64": image_b64, "bounding_boxes": [], "frame_id": "empty"},
            ],
        })
        assert resp.status_code == 200
