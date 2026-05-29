"""Tests fuer sicheres Bild-Dekodieren in Inferenz-Endpunkten."""

import base64
import io

from fastapi import HTTPException
from fastapi.testclient import TestClient
from PIL import Image
import pytest


def _png_base64(width: int = 8, height: int = 8) -> str:
    img = Image.new("RGBA", (width, height), (10, 20, 30, 255))
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


def test_decode_image_safe_returns_rgb_image():
    from sidecar.models.image_decode import decode_image_safe

    img = decode_image_safe(_png_base64(), max_bytes=1024 * 1024, max_pixels=1_000)

    assert img.mode == "RGB"
    assert img.size == (8, 8)


def test_decode_image_safe_rejects_base64_text_over_size_limit():
    from sidecar.models.image_decode import decode_image_safe

    with pytest.raises(HTTPException) as exc:
        decode_image_safe("A" * 32, max_bytes=4, max_pixels=1_000)

    assert exc.value.status_code == 413


def test_decode_image_safe_rejects_invalid_base64():
    from sidecar.models.image_decode import decode_image_safe

    with pytest.raises(HTTPException) as exc:
        decode_image_safe("not@@base64", max_bytes=1024, max_pixels=1_000)

    assert exc.value.status_code == 400


def test_decode_image_safe_rejects_non_image_bytes():
    from sidecar.models.image_decode import decode_image_safe

    data = base64.b64encode(b"this is not an image").decode()

    with pytest.raises(HTTPException) as exc:
        decode_image_safe(data, max_bytes=1024, max_pixels=1_000)

    assert exc.value.status_code == 400


def test_decode_image_safe_rejects_too_many_pixels():
    from sidecar.models.image_decode import decode_image_safe

    with pytest.raises(HTTPException) as exc:
        decode_image_safe(_png_base64(3, 3), max_bytes=1024 * 1024, max_pixels=8)

    assert exc.value.status_code == 400


def test_yolo_detect_rejects_invalid_base64_with_400(monkeypatch):
    from sidecar.main import app
    from sidecar.models import yolo_wrapper

    monkeypatch.setattr(yolo_wrapper, "_get_yolo_model", lambda: object())

    client = TestClient(app)
    resp = client.post(
        "/detect/yolo",
        json={
            "image_base64": "not@@base64",
            "confidence_threshold": 0.25,
        },
    )

    assert resp.status_code == 400
