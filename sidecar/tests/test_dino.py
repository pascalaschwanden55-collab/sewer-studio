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


@pytest.mark.gpu
def test_dino_endpoint(client):
    """Smoke test: DINO endpoint responds with correct schema.

    GPU-markiert: /detect/dino laedt das echte Grounding-DINO-Modell -> langsam, kann ohne
    GPU haengen. Default-Lauf (pytest tests) ueberspringt das; explizit: pytest -m gpu.
    """
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


def test_dino_model_unloaded_liefert_503_statt_degraded(client, monkeypatch):
    """Unload-Race (model=None nach ensure_loaded) liegt jetzt INNERHALB des
    Inferenz-try (Lease umfasst Laden+Inferenz, Paket 2): der Fehler darf dort
    nicht als degraded-200 verschluckt werden, sondern muss als 503
    model_unloaded an den C#-Client gehen (Retry loest Nachladen aus)."""
    import sys
    import types

    from sidecar.gpu_manager import ModelSlot, gpu_manager
    from sidecar.models import dino_wrapper

    monkeypatch.setattr(dino_wrapper, "_load_dino_on", lambda device: (None, None))
    monkeypatch.setattr(dino_wrapper, "_resolve_device", lambda: "cpu")

    package = types.ModuleType("groundingdino")
    util = types.ModuleType("groundingdino.util")
    inference = types.ModuleType("groundingdino.util.inference")
    inference.predict = lambda **_kw: ([], [], [])
    package.util = util
    util.inference = inference
    monkeypatch.setitem(sys.modules, "groundingdino", package)
    monkeypatch.setitem(sys.modules, "groundingdino.util", util)
    monkeypatch.setitem(sys.modules, "groundingdino.util.inference", inference)

    try:
        resp = client.post("/detect/dino", json={
            "image_base64": _make_test_image(),
            "box_threshold": 0.30,
            "text_threshold": 0.25,
        })
        assert resp.status_code == 503
        assert resp.json()["code"] == "model_unloaded"
    finally:
        gpu_manager.unload(ModelSlot.DINO)


def test_dino_vram_mangel_liefert_503_statt_degraded(client, monkeypatch):
    """Ein bereits beim Laden erkannter VRAM-Mangel muss den zentralen
    Kapazitaetsfehler-Vertrag erreichen und darf nicht als leeres 200 enden."""
    import sys
    import types

    from sidecar.gpu_manager import InsufficientVramError, ModelSlot, gpu_manager

    package = types.ModuleType("groundingdino")
    util = types.ModuleType("groundingdino.util")
    inference = types.ModuleType("groundingdino.util.inference")
    inference.predict = lambda **_kw: ([], [], [])
    package.util = util
    util.inference = inference
    monkeypatch.setitem(sys.modules, "groundingdino", package)
    monkeypatch.setitem(sys.modules, "groundingdino.util", util)
    monkeypatch.setitem(sys.modules, "groundingdino.util.inference", inference)

    def fail_to_load(*_args, **_kwargs):
        raise InsufficientVramError(
            ModelSlot.DINO,
            free_gb=1.0,
            required_gb=5.0,
            reserved_gb=1.5,
        )

    monkeypatch.setattr(gpu_manager, "ensure_loaded", fail_to_load)

    resp = client.post("/detect/dino", json={
        "image_base64": _make_test_image(),
        "box_threshold": 0.30,
        "text_threshold": 0.25,
    })

    assert resp.status_code == 503
    assert resp.json()["code"] == "insufficient_vram"
