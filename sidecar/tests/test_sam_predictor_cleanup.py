"""SAM releases request-specific image embeddings after each inference."""

import base64
import io
from types import SimpleNamespace

import numpy as np
import pytest
from PIL import Image

from sidecar.models import sam_wrapper
from sidecar.schemas.detection import BoundingBox


def _image_base64() -> str:
    image = Image.new("RGB", (24, 24), (100, 100, 100))
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    return base64.b64encode(buffer.getvalue()).decode("ascii")


def _box() -> BoundingBox:
    return BoundingBox(x1=2, y1=2, x2=20, y2=20, label="test")


def _setup_predictor(monkeypatch, predictor):
    monkeypatch.setattr(sam_wrapper, "_resolve_device", lambda: "cpu")
    monkeypatch.setattr(
        sam_wrapper.gpu_manager,
        "ensure_loaded",
        lambda slot, device, loader: SimpleNamespace(processor=predictor),
    )


def test_segment_resets_predictor_after_all_boxes(monkeypatch):
    events = []

    class FakePredictor:
        def set_image(self, image):
            events.append("set_image")

        def predict(self, **kwargs):
            events.append("predict")
            mask = np.ones((24, 24), dtype=bool)
            return np.array([mask]), np.array([0.99]), None

        def reset_predictor(self):
            events.append("reset")

    _setup_predictor(monkeypatch, FakePredictor())
    response = sam_wrapper.segment(_image_base64(), [_box(), _box()])

    assert len(response.masks) == 2
    assert events == ["set_image", "predict", "predict", "reset"]


@pytest.mark.parametrize("failure_stage", ["set_image", "predict"])
def test_segment_resets_predictor_when_inference_fails(monkeypatch, failure_stage):
    events = []

    class FakePredictor:
        def set_image(self, image):
            events.append("set_image")
            if failure_stage == "set_image":
                raise RuntimeError("CUDA out of memory: set_image")

        def predict(self, **kwargs):
            events.append("predict")
            raise RuntimeError("CUDA out of memory: predict")

        def reset_predictor(self):
            events.append("reset")

    _setup_predictor(monkeypatch, FakePredictor())

    with pytest.raises(RuntimeError, match=f"CUDA out of memory: {failure_stage}"):
        sam_wrapper.segment(_image_base64(), [_box()])

    assert events == (["set_image"] if failure_stage == "set_image" else ["set_image", "predict"]) + ["reset"]


def test_reset_failure_does_not_replace_inference_failure(monkeypatch, caplog):
    class FakePredictor:
        def set_image(self, image):
            raise RuntimeError("CUDA out of memory")

        def reset_predictor(self):
            raise ValueError("reset failed")

    _setup_predictor(monkeypatch, FakePredictor())

    with pytest.raises(RuntimeError, match="CUDA out of memory"):
        sam_wrapper.segment(_image_base64(), [_box()])

    assert "reset failed" in caplog.text
