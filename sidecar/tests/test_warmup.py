from types import SimpleNamespace

from sidecar.routes import warmup


def test_warmup_loads_all_sidecar_models_including_classifier(monkeypatch):
    calls: list[str] = []

    monkeypatch.setattr(
        warmup.yolo_wrapper,
        "detect",
        lambda image_base64, confidence_threshold: calls.append("yolo"),
    )
    monkeypatch.setattr(
        warmup.yolo_wrapper,
        "classify",
        lambda image_base64, top_k=5: calls.append("classifier") or [],
    )
    monkeypatch.setattr(
        warmup.yolo_wrapper,
        "get_classifier_status",
        lambda: {"loaded": True},
    )
    monkeypatch.setattr(
        warmup.dino_wrapper,
        "detect",
        lambda image_base64, prompts, box_threshold, text_threshold: calls.append("dino"),
    )
    monkeypatch.setattr(warmup.sam_wrapper, "_resolve_device", lambda: "cpu")
    monkeypatch.setattr(
        warmup.sam_wrapper,
        "_load_sam_on",
        lambda device: calls.append("sam") or (object(), None),
    )
    monkeypatch.setattr(
        warmup.gpu_manager,
        "ensure_loaded",
        lambda slot, device, loader: SimpleNamespace(model=loader()[0], processor=None),
    )

    result = warmup.warmup()

    assert calls == ["yolo", "classifier", "dino", "sam"]
    assert result["warmup"]["classifier"] == "ok"
    assert "classifier" in result["loaded"]


def test_warmup_does_not_mark_classifier_loaded_when_no_classifier_model(monkeypatch):
    monkeypatch.setattr(warmup.yolo_wrapper, "detect", lambda image_base64, confidence_threshold: None)
    monkeypatch.setattr(warmup.yolo_wrapper, "classify", lambda image_base64, top_k=5: [])
    monkeypatch.setattr(warmup.yolo_wrapper, "get_classifier_status", lambda: {"loaded": False})
    monkeypatch.setattr(warmup.dino_wrapper, "detect", lambda image_base64, prompts, box_threshold, text_threshold: None)
    monkeypatch.setattr(warmup.sam_wrapper, "_resolve_device", lambda: "cpu")
    monkeypatch.setattr(warmup.sam_wrapper, "_load_sam_on", lambda device: (object(), None))
    monkeypatch.setattr(
        warmup.gpu_manager,
        "ensure_loaded",
        lambda slot, device, loader: SimpleNamespace(model=loader()[0], processor=None),
    )

    result = warmup.warmup()

    assert result["warmup"]["classifier"].startswith("fehler:")
    assert "classifier" not in result["loaded"]
