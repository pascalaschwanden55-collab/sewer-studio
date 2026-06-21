from pathlib import Path


def test_sam_auto_backend_prefers_sam2_1_when_weights_exist(tmp_path, monkeypatch):
    from sidecar.config import settings
    from sidecar.models import sam_wrapper

    sam21_dir = tmp_path / "sam2.1"
    sam21_dir.mkdir()
    sam21_weights = sam21_dir / "sam2.1_hiera_large.pt"
    sam21_weights.write_bytes(b"sam21")

    monkeypatch.setattr(settings, "models_dir", str(tmp_path))
    monkeypatch.setattr(settings, "sam_backend", "auto", raising=False)
    monkeypatch.setattr(settings, "sam2_weights_path", "", raising=False)

    backend, weights = sam_wrapper._resolve_sam_backend()

    assert backend == "sam2.1"
    assert Path(weights) == sam21_weights


def test_sam_auto_backend_fails_without_sam2_1_weights(tmp_path, monkeypatch):
    from sidecar.config import settings
    from sidecar.models import sam_wrapper

    sam1_dir = tmp_path / "sam3"
    sam1_dir.mkdir()
    (sam1_dir / "sam_vit_h_4b8939.pth").write_bytes(b"sam1")
    old_sam2_dir = tmp_path / "sam2"
    old_sam2_dir.mkdir()
    (old_sam2_dir / "sam2_hiera_large.pt").write_bytes(b"sam2")

    monkeypatch.setattr(settings, "models_dir", str(tmp_path))
    monkeypatch.setattr(settings, "sam_backend", "auto", raising=False)
    monkeypatch.setattr(settings, "sam2_weights_path", "", raising=False)

    try:
        sam_wrapper._resolve_sam_backend()
    except FileNotFoundError as exc:
        assert "SAM 2.1 weights not found" in str(exc)
    else:
        raise AssertionError("older SAM weights must not be used as fallback")


def test_dino_auto_model_dir_prefers_local_swinb_when_available(tmp_path, monkeypatch):
    from sidecar.config import settings
    from sidecar.models import dino_wrapper

    swint = tmp_path / "grounding_dino_1.5"
    swinb = tmp_path / "grounding_dino_swinb"
    swint.mkdir()
    swinb.mkdir()
    (swint / "GroundingDINO_SwinT_OGC.cfg.py").write_text("# swint")
    (swint / "groundingdino_swint_ogc.pth").write_bytes(b"swint")
    swinb_cfg = swinb / "GroundingDINO_SwinB.cfg.py"
    swinb_weights = swinb / "groundingdino_swinb_cogcoor.pth"
    swinb_cfg.write_text("# swinb")
    swinb_weights.write_bytes(b"swinb")

    monkeypatch.setattr(settings, "models_dir", str(tmp_path))
    monkeypatch.setattr(settings, "dino_model_dir", "auto", raising=False)

    config, weights = dino_wrapper._find_dino_files()

    assert Path(config) == swinb_cfg
    assert Path(weights) == swinb_weights


def test_dino_auto_model_dir_falls_back_to_existing_swint(tmp_path, monkeypatch):
    from sidecar.config import settings
    from sidecar.models import dino_wrapper

    swint = tmp_path / "grounding_dino_1.5"
    swint.mkdir()
    swint_cfg = swint / "GroundingDINO_SwinT_OGC.cfg.py"
    swint_weights = swint / "groundingdino_swint_ogc.pth"
    swint_cfg.write_text("# swint")
    swint_weights.write_bytes(b"swint")

    monkeypatch.setattr(settings, "models_dir", str(tmp_path))
    monkeypatch.setattr(settings, "dino_model_dir", "auto", raising=False)

    config, weights = dino_wrapper._find_dino_files()

    assert Path(config) == swint_cfg
    assert Path(weights) == swint_weights
