from pathlib import Path

import pytest

from sidecar.config import settings
from sidecar.models import yolo_wrapper


@pytest.fixture
def restore_yolo_settings():
    original_models_dir = settings.models_dir
    original_model_name = settings.yolo_model_name
    original_require = settings.require_custom_yolo
    try:
        yield
    finally:
        settings.models_dir = original_models_dir
        settings.yolo_model_name = original_model_name
        settings.require_custom_yolo = original_require


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
