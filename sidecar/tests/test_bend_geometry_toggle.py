import base64
import io
from types import SimpleNamespace

import numpy as np
from PIL import Image


def _make_test_image(w: int = 32, h: int = 24) -> str:
    img = Image.new("RGB", (w, h), (120, 120, 120))
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


def test_yolo_classify_bend_geometry_disabled_returns_neutral_values(monkeypatch):
    from sidecar.config import settings
    from sidecar.routes import yolo
    from sidecar.schemas.detection import YoloClassifyRequest

    monkeypatch.setattr(settings, "bend_geometry_enabled", False, raising=False)
    monkeypatch.setattr(settings, "bend_veto_enabled", False, raising=False)
    monkeypatch.setattr(
        yolo.yolo_wrapper,
        "classify_with_quality",
        lambda image_base64, top_k=5: ([("BCE", 0.9, 0)], True, "ok"),
    )
    monkeypatch.setattr(yolo.yolo_wrapper, "classifier_metadata", lambda: {})
    monkeypatch.setattr(yolo, "write_event", lambda *args, **kwargs: None)
    monkeypatch.setattr(
        yolo,
        "decode_image_safe",
        lambda *args, **kwargs: (_ for _ in ()).throw(AssertionError("bend decode called")),
    )

    response = yolo.classify_yolo(
        YoloClassifyRequest(image_base64="not-a-real-image", top_k=5)
    )

    assert response.bend_shift == 0.0
    assert response.is_bend is False
    assert response.vanish_x == 0.5
    assert response.vanish_y == 0.5


def test_yolo_classify_bend_veto_enabled_runs_without_sam_geometry(monkeypatch):
    from sidecar.config import settings
    from sidecar.routes import yolo
    from sidecar.schemas.detection import YoloClassifyRequest

    monkeypatch.setattr(settings, "bend_geometry_enabled", False, raising=False)
    monkeypatch.setattr(settings, "bend_veto_enabled", True, raising=False)
    monkeypatch.setattr(
        yolo.yolo_wrapper,
        "classify_with_quality",
        lambda image_base64, top_k=5: ([("BCE", 0.9, 0)], True, "ok"),
    )
    monkeypatch.setattr(yolo.yolo_wrapper, "classifier_metadata", lambda: {})
    monkeypatch.setattr(yolo, "write_event", lambda *args, **kwargs: None)
    monkeypatch.setattr(yolo, "decode_image_safe", lambda *args, **kwargs: Image.new("RGB", (32, 24)))
    monkeypatch.setattr(
        yolo,
        "analyze_bend",
        lambda img: SimpleNamespace(shift=0.18, is_bend=True, vanish_x=0.62, vanish_y=0.41),
    )

    response = yolo.classify_yolo(
        YoloClassifyRequest(image_base64="not-a-real-image", top_k=5)
    )

    assert response.bend_shift == 0.18
    assert response.is_bend is True
    assert response.vanish_x == 0.62
    assert response.vanish_y == 0.41


def test_sam_segment_bend_geometry_disabled_returns_neutral_values(monkeypatch):
    from sidecar.config import settings
    from sidecar.models import sam_wrapper
    from sidecar.schemas.detection import BoundingBox

    class FakePredictor:
        def set_image(self, img_array):
            self.shape = img_array.shape[:2]

        def predict(self, point_coords=None, point_labels=None, box=None, multimask_output=False):
            h, w = self.shape
            mask = np.zeros((h, w), dtype=bool)
            mask[4:12, 6:18] = True
            return np.array([mask]), np.array([0.95]), None

    monkeypatch.setattr(settings, "bend_geometry_enabled", False, raising=False)
    monkeypatch.setattr(settings, "sam_min_score", 0.0, raising=False)
    monkeypatch.setattr(sam_wrapper, "_resolve_device", lambda: "cpu")
    monkeypatch.setattr(
        sam_wrapper.gpu_manager,
        "ensure_loaded",
        lambda slot, device, loader: SimpleNamespace(processor=FakePredictor()),
    )
    monkeypatch.setattr(
        sam_wrapper,
        "analyze_bend",
        lambda *args, **kwargs: (_ for _ in ()).throw(AssertionError("bend analysis called")),
    )

    response = sam_wrapper.segment(
        _make_test_image(),
        [BoundingBox(x1=1, y1=1, x2=20, y2=20, label="crack", confidence=0.9)],
        pipe_diameter_mm=300,
    )

    assert len(response.masks) == 1
    assert response.bend_shift == 0.0
    assert response.is_bend is False
    assert response.vanish_x == 0.5
    assert response.vanish_y == 0.5
