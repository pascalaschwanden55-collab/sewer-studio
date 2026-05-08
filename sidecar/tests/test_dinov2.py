"""Smoke-Tests fuer den /classify/dinov2 Endpoint.

Phase 5.1: Coverage-Lift fuer bisher ungetestete Routes.
Wir mocken den DINOv2-Wrapper damit kein Foundation-Encoder
und keine Linear-Heads geladen werden — der Test prueft nur, dass
die Route gemountet ist und das Antwort-Schema valide ist.
"""

from __future__ import annotations

import base64
import io

import pytest
from PIL import Image
from fastapi.testclient import TestClient

from sidecar.schemas.dinov2 import DinoV2Prediction, DinoV2Response


def _make_test_image(w: int = 224, h: int = 224) -> str:
    img = Image.new("RGB", (w, h), (96, 96, 96))
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


@pytest.fixture
def client(monkeypatch):
    """TestClient mit gemocktem dinov2_wrapper (keine Heads, kein Encoder-Load)."""
    # Wir patchen die im Routen-Modul gebundenen Symbole
    from sidecar.routes import dinov2 as dinov2_route

    fake_response = DinoV2Response(
        predictions=[
            DinoV2Prediction(
                vsa_code="BAB",
                severity_class="mild",
                confidence=0.71,
                scores={"not_present": 0.10, "mild": 0.71, "severe": 0.19},
            )
        ],
        heads_loaded=["BAB"],
        encoder_inference_time_ms=12.3,
        heads_inference_time_ms=0.4,
        total_time_ms=12.7,
        encoder_version="facebook/dinov2-large",
        heads_manifest_hash="deadbeef0001",
    )

    class _FakeWrapper:
        @staticmethod
        def classify(image_base64: str, target_codes=None):
            return fake_response

        @staticmethod
        def reload_heads():
            return {
                "heads_loaded": ["BAB"],
                "count": 1,
                "encoder_version": "facebook/dinov2-large",
                "heads_manifest_hash": "deadbeef0001",
            }

    monkeypatch.setattr(dinov2_route, "dinov2_wrapper", _FakeWrapper, raising=True)

    from sidecar.main import app

    return TestClient(app)


def test_dinov2_classify_returns_valid_schema(client):
    """POST /classify/dinov2 liefert gueltiges Schema."""
    img_b64 = _make_test_image()
    resp = client.post(
        "/classify/dinov2",
        json={
            "image_base64": img_b64,
            "target_codes": ["BAB"],
        },
    )
    assert resp.status_code == 200, resp.text
    data = resp.json()

    assert "predictions" in data
    assert "heads_loaded" in data
    assert "total_time_ms" in data
    assert "encoder_version" in data
    assert "heads_manifest_hash" in data

    assert data["heads_loaded"] == ["BAB"]
    assert len(data["predictions"]) == 1
    pred = data["predictions"][0]
    assert pred["vsa_code"] == "BAB"
    assert pred["severity_class"] in ("not_present", "mild", "severe")
    assert 0.0 <= pred["confidence"] <= 1.0
    assert set(pred["scores"].keys()) == {"not_present", "mild", "severe"}


def test_dinov2_reload_endpoint(client):
    """POST /classify/dinov2/reload triggert reload_heads und antwortet mit Manifest."""
    resp = client.post("/classify/dinov2/reload")
    assert resp.status_code == 200, resp.text
    data = resp.json()
    assert data["count"] == 1
    assert "BAB" in data["heads_loaded"]
    assert data["encoder_version"] == "facebook/dinov2-large"
    assert data["heads_manifest_hash"] == "deadbeef0001"
