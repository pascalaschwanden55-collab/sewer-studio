"""Smoke-Tests fuer den /enhance Frame-Super-Resolution Endpoint.

Phase 5.1: Coverage-Lift fuer bisher ungetestete Routes.
Wir mocken den VSR-Wrapper damit weder Real-ESRGAN-Gewichte noch GPU
geladen werden — der Test prueft nur dass die Route gemountet ist und
das Antwort-Schema valide ist.
"""

from __future__ import annotations

import base64
import io

import numpy as np
import pytest
from PIL import Image
from fastapi.testclient import TestClient


def _make_test_image(w: int = 320, h: int = 240, color=(128, 128, 128)) -> str:
    img = Image.new("RGB", (w, h), color)
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


@pytest.fixture
def client(monkeypatch):
    """TestClient mit gemocktem VSR-Backend (kein Modell-Load).

    Wir patchen `enhance_frame` so, dass es einfach das Eingangs-Array
    auf target_height hochskaliert (Lanczos via PIL) — schnell und ohne
    Real-ESRGAN-Abhaengigkeit. `_vsr_backend` setzen wir auf "bicubic-stub"
    damit der Endpoint einen lesbaren Backend-Namen zurueckgibt.
    """
    # Wichtig: vor dem Import der Route patchen, da `from .vsr_wrapper import enhance_frame`
    # beim Modul-Load die Funktion bindet. Wir muessen das Modul-Symbol patchen.
    from sidecar.routes import enhance as enhance_route

    def _fake_enhance(img_rgb: np.ndarray, target_height: int = 1080, denoise: bool = True):
        h, w = img_rgb.shape[:2]
        if h >= target_height:
            return img_rgb
        scale = target_height / h
        new_w = int(w * scale)
        new_h = int(h * scale)
        pil = Image.fromarray(img_rgb)
        return np.array(pil.resize((new_w, new_h), Image.LANCZOS))

    monkeypatch.setattr(enhance_route, "enhance_frame", _fake_enhance, raising=True)
    monkeypatch.setattr(enhance_route, "_vsr_backend", "bicubic-stub", raising=True)

    from sidecar.main import app

    return TestClient(app)


def test_enhance_returns_valid_schema(client):
    """POST /enhance liefert ein Bild und das vollstaendige Antwort-Schema."""
    img_b64 = _make_test_image(w=320, h=240)
    resp = client.post(
        "/enhance",
        json={
            "image_base64": img_b64,
            "target_height": 720,
            "denoise": False,
        },
    )

    assert resp.status_code == 200, resp.text
    data = resp.json()

    # Schema-Keys sind alle vorhanden
    expected_keys = {
        "enhanced_base64",
        "processing_time_ms",
        "input_width",
        "input_height",
        "output_width",
        "output_height",
        "scale_factor",
        "backend",
    }
    assert expected_keys.issubset(data.keys()), f"Fehlende Keys: {expected_keys - data.keys()}"

    # Werte plausibel
    assert data["input_width"] == 320
    assert data["input_height"] == 240
    assert data["output_height"] == 720  # auf target_height hochskaliert
    assert data["scale_factor"] >= 1.0
    assert data["backend"] == "bicubic-stub"
    assert isinstance(data["enhanced_base64"], str) and len(data["enhanced_base64"]) > 0


def test_enhance_no_upscale_when_already_large(client):
    """Wenn input_height >= target_height, gibt der Endpoint das Bild unveraendert zurueck."""
    img_b64 = _make_test_image(w=1920, h=1080)
    resp = client.post(
        "/enhance",
        json={
            "image_base64": img_b64,
            "target_height": 720,  # kleiner als input
            "denoise": True,
        },
    )

    assert resp.status_code == 200, resp.text
    data = resp.json()
    assert data["input_height"] == 1080
    assert data["output_height"] == 1080  # unveraendert
    assert data["scale_factor"] == 1.0
