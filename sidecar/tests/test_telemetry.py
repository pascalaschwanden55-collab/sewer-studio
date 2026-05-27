import json

from fastapi.testclient import TestClient


def test_detect_yolo_writes_telemetry_jsonl(tmp_path, monkeypatch):
    from sidecar.config import settings
    from sidecar.main import app
    from sidecar.routes import yolo as yolo_route
    from sidecar.schemas.detection import YoloDetection, YoloResponse

    monkeypatch.setattr(settings, "telemetry_enabled", True, raising=False)
    monkeypatch.setattr(settings, "telemetry_dir", str(tmp_path), raising=False)

    def fake_detect(image_base64: str, confidence_threshold: float) -> YoloResponse:
        assert image_base64 == "test-image"
        assert confidence_threshold == 0.7
        return YoloResponse(
            is_relevant=True,
            detections=[
                YoloDetection(
                    x1=1,
                    y1=2,
                    x2=3,
                    y2=4,
                    class_name="roots",
                    confidence=0.91,
                )
            ],
            frame_class="relevant",
            inference_time_ms=12.3,
            model_name="yolo26m.engine",
            model_backend="tensorrt",
            device="cuda:0",
            queue_wait_ms=1.2,
            vram_allocated_gb=3.4,
            vram_total_gb=31.5,
            gpu_utilization_percent=44.0,
        )

    monkeypatch.setattr(yolo_route.yolo_wrapper, "detect", fake_detect)

    client = TestClient(app)
    response = client.post(
        "/detect/yolo",
        json={
            "image_base64": "test-image",
            "confidence_threshold": 0.7,
        },
    )

    assert response.status_code == 200

    telemetry_path = tmp_path / "sidecar.jsonl"
    lines = telemetry_path.read_text(encoding="utf-8").splitlines()
    assert len(lines) == 1

    event = json.loads(lines[0])
    assert event["model_name"] == "yolo26m.engine"
    assert event["backend"] == "tensorrt"
    assert event["roundtrip_ms"] >= 0
    assert event["inference_time_ms"] == 12.3
    assert event["gpu_utilization_percent"] == 44.0
    assert event["vram_allocated_gb"] == 3.4
    assert event["vram_total_gb"] == 31.5
    assert event["detection_count"] == 1
    assert event["confidence_threshold"] == 0.7
