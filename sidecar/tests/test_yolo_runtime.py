import json
import logging
from pathlib import Path

import pytest

from sidecar.config import settings
from sidecar.models import yolo_wrapper


@pytest.fixture
def restore_yolo_settings():
    original_models_dir = settings.models_dir
    original_model_name = settings.yolo_model_name
    original_require = settings.require_custom_yolo
    original_cls_model_path = settings.yolo_cls_model_path
    try:
        yield
    finally:
        settings.models_dir = original_models_dir
        settings.yolo_model_name = original_model_name
        settings.require_custom_yolo = original_require
        settings.yolo_cls_model_path = original_cls_model_path


def test_resolve_yolo_model_path_uses_custom_weights(tmp_path: Path, restore_yolo_settings):
    yolo_dir = tmp_path / "yolo26m"
    yolo_dir.mkdir(parents=True)
    weights = yolo_dir / "custom.pt"
    weights.write_bytes(b"weights")

    settings.models_dir = str(tmp_path)
    settings.yolo_model_name = "custom.pt"
    settings.require_custom_yolo = True

    model_path, using_custom = yolo_wrapper._resolve_yolo_model_path()

    assert model_path == str(weights)
    assert using_custom is True


def test_resolve_yolo_model_path_strict_mode_raises_without_weights(tmp_path: Path, restore_yolo_settings):
    settings.models_dir = str(tmp_path)
    settings.yolo_model_name = "missing.pt"
    settings.require_custom_yolo = True

    with pytest.raises(FileNotFoundError):
        yolo_wrapper._resolve_yolo_model_path()


def test_resolve_cls_model_path_uses_configured_weights(tmp_path: Path, restore_yolo_settings):
    weights = tmp_path / "manual1286.pt"
    weights.write_bytes(b"weights")

    settings.yolo_cls_model_path = str(weights)

    model_path = yolo_wrapper._resolve_cls_model_path()

    assert model_path == str(weights)


def test_resolve_cls_model_path_ignores_missing_configured_weights(tmp_path: Path, restore_yolo_settings):
    settings.models_dir = str(tmp_path)
    settings.yolo_cls_model_path = str(tmp_path / "missing.pt")

    model_path = yolo_wrapper._resolve_cls_model_path()

    assert model_path is None


def test_tensorrt_names_json_maps_class_ids_to_yolo_names(tmp_path: Path, restore_yolo_settings):
    engine_path = tmp_path / "yolo26m.engine"
    engine_path.write_bytes(b"engine")
    engine_path.with_suffix(".names.json").write_text(
        json.dumps(
            {
                "source_weights_sha256": "abc123",
                "names": {
                    "2": "deformation",
                    "8": "roots",
                },
            }
        ),
        encoding="utf-8",
    )

    class_names = yolo_wrapper._load_tensorrt_class_names(engine_path)

    assert class_names == {2: "deformation", 8: "roots"}
    assert yolo_wrapper._class_name_for_id(8, {8: "class8"}, class_names) == "roots"


def test_missing_tensorrt_names_json_warns_and_uses_class_fallback(
    tmp_path: Path,
    restore_yolo_settings,
    caplog: pytest.LogCaptureFixture,
):
    engine_path = tmp_path / "yolo26m.engine"
    engine_path.write_bytes(b"engine")
    caplog.set_level(logging.WARNING)

    class_names = yolo_wrapper._load_tensorrt_class_names(engine_path)
    fallback_name = yolo_wrapper._class_name_for_id(9, {9: "class9"}, class_names)

    assert class_names == {}
    assert fallback_name == "class9"
    assert "TensorRT class-name file missing" in caplog.text
    assert "falling back to class9" in caplog.text
