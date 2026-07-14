"""GPU-free smoke tests for the DINO and SAM model loaders.

The heavy third-party libraries are replaced with tiny stand-ins.  This keeps
the normal test run fast while still protecting our file selection, CPU device
routing and loader return contract.
"""

import sys
import types
from pathlib import Path


def test_dino_loader_uses_selected_files_and_cpu(tmp_path, monkeypatch):
    from sidecar.config import settings
    from sidecar.models import dino_wrapper

    model_dir = tmp_path / "grounding_dino_swinb"
    model_dir.mkdir()
    config_path = model_dir / "GroundingDINO_SwinB_cfg.py"
    weights_path = model_dir / "groundingdino_swinb.pth"
    config_path.write_text("# test config", encoding="utf-8")
    weights_path.write_bytes(b"test weights")
    monkeypatch.setattr(settings, "models_dir", str(tmp_path))
    monkeypatch.setattr(settings, "dino_model_dir", "auto", raising=False)

    calls = []
    loaded_model = object()

    def load_model(config, weights, *, device):
        calls.append((Path(config), Path(weights), device))
        return loaded_model

    package = types.ModuleType("groundingdino")
    util = types.ModuleType("groundingdino.util")
    inference = types.ModuleType("groundingdino.util.inference")
    inference.load_model = load_model
    package.util = util
    util.inference = inference
    monkeypatch.setitem(sys.modules, "groundingdino", package)
    monkeypatch.setitem(sys.modules, "groundingdino.util", util)
    monkeypatch.setitem(sys.modules, "groundingdino.util.inference", inference)

    model, processor = dino_wrapper._load_dino_on("cpu")

    assert model is loaded_model
    assert processor is None
    assert calls == [(config_path, weights_path, "cpu")]


def test_sam_loader_uses_sam21_config_and_cpu(tmp_path, monkeypatch):
    from sidecar.config import settings
    from sidecar.models import sam_wrapper

    model_dir = tmp_path / "sam2.1"
    model_dir.mkdir()
    weights_path = model_dir / "sam2.1_hiera_large.pt"
    weights_path.write_bytes(b"test weights")
    monkeypatch.setattr(settings, "models_dir", str(tmp_path))
    monkeypatch.setattr(settings, "sam_backend", "auto", raising=False)
    monkeypatch.setattr(settings, "sam2_weights_path", "", raising=False)
    monkeypatch.setattr(settings, "sam2_model_cfg", "auto", raising=False)

    calls = []
    loaded_model = object()

    def build_sam2(config, weights, *, device):
        calls.append((config, Path(weights), device))
        return loaded_model

    class Predictor:
        def __init__(self, model):
            self.model = model

    package = types.ModuleType("sam2")
    build_module = types.ModuleType("sam2.build_sam")
    predictor_module = types.ModuleType("sam2.sam2_image_predictor")
    build_module.build_sam2 = build_sam2
    predictor_module.SAM2ImagePredictor = Predictor
    package.build_sam = build_module
    package.sam2_image_predictor = predictor_module
    monkeypatch.setitem(sys.modules, "sam2", package)
    monkeypatch.setitem(sys.modules, "sam2.build_sam", build_module)
    monkeypatch.setitem(sys.modules, "sam2.sam2_image_predictor", predictor_module)

    model, predictor = sam_wrapper._load_sam_on("cpu")

    assert model is loaded_model
    assert predictor.model is loaded_model
    assert predictor._sewer_sam_backend == "sam2.1"
    assert calls == [("configs/sam2.1/sam2.1_hiera_l.yaml", weights_path, "cpu")]
