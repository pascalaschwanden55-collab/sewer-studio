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


class TestPipeAxisBatch:
    """Audit 2026-05-13 M4: Batch-Endpunkt nutzt asyncio.to_thread."""

    def test_batch_returns_results_in_input_order(self, client, image_b64):
        resp = client.post("/analyze/pipe-axis/batch", json={
            "frames": [
                {"image_base64": image_b64, "pipe_diameter_mm": 300},
                {"image_base64": image_b64, "pipe_diameter_mm": 300},
                {"image_base64": image_b64, "pipe_diameter_mm": 300},
            ],
        })
        assert resp.status_code == 200
        data = resp.json()
        assert len(data["results"]) == 3
        assert data["total_time_ms"] > 0
        # Alle Ergebnisse haben das Pflicht-Schema (Smoke-Check)
        for r in data["results"]:
            assert "vanishing_x" in r
            assert "vanishing_y" in r
            assert "confidence" in r

    def test_batch_single_frame(self, client, image_b64):
        resp = client.post("/analyze/pipe-axis/batch", json={
            "frames": [{"image_base64": image_b64, "pipe_diameter_mm": 300}],
        })
        assert resp.status_code == 200
        assert len(resp.json()["results"]) == 1

    def test_health_responds_while_batch_runs(self, client, image_b64):
        """Event-Loop bleibt waehrend Batch ansprechbar (Audit M4):
        Ein 30-Frame-Batch laeuft, parallel muss /health unter 2s antworten.
        Vor M4 blockierte der List-Comprehension-Pfad den Worker.
        """
        import threading
        import time

        result = {"health_ms": None, "health_status": None}

        def hit_health():
            time.sleep(0.2)  # Batch ein paar Frames laufen lassen
            t0 = time.perf_counter()
            r = client.get("/health")
            result["health_ms"] = (time.perf_counter() - t0) * 1000
            result["health_status"] = r.status_code

        t = threading.Thread(target=hit_health, daemon=True)
        t.start()

        batch_resp = client.post("/analyze/pipe-axis/batch", json={
            "frames": [{"image_base64": image_b64, "pipe_diameter_mm": 300}
                       for _ in range(30)],
        })
        t.join(timeout=60)

        assert batch_resp.status_code == 200
        assert result["health_status"] == 200, "Health-Endpunkt war waehrend Batch nicht erreichbar"
        # Health darf nicht durch Batch ausgebremst werden — < 2s ist grosszuegig.
        assert result["health_ms"] is not None
        assert result["health_ms"] < 2000, f"Health-Latenz {result['health_ms']:.0f}ms zu hoch"
