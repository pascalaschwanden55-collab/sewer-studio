from types import SimpleNamespace

from sidecar.routes import warmup


def _qualification(*, qualified: bool) -> dict:
    return {
        "qualified": qualified,
        "status": "qualified" if qualified else "status_file_missing",
        "reason": None if qualified else "Qualifikationsdatei fehlt.",
        "artifact": {
            "file_name": "detector.engine",
            "sha256": "a" * 64,
            "backend": "tensorrt",
            "loaded": False,
        },
        "marked_utc": "2026-07-25T00:00:00Z",
    }


def test_warmup_loads_all_sidecar_models_including_classifier(monkeypatch):
    calls: list[str] = []

    monkeypatch.setattr(
        warmup.detector_qualification,
        "evaluate_active_detector",
        lambda: _qualification(qualified=True),
    )
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
    assert result["warmup"]["yolo"] == "ok"
    assert result["warmup_details"]["yolo"] == {
        "status": "loaded",
        "reason_code": None,
        "qualification_status": "qualified",
        "reason": None,
    }
    assert result["warmup"]["classifier"] == "ok"
    assert "classifier" in result["loaded"]


def test_warmup_does_not_mark_classifier_loaded_when_no_classifier_model(monkeypatch):
    monkeypatch.setattr(
        warmup.detector_qualification,
        "evaluate_active_detector",
        lambda: _qualification(qualified=True),
    )
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


def test_warmup_does_not_mark_degraded_dino_as_loaded(monkeypatch):
    monkeypatch.setattr(
        warmup.detector_qualification,
        "evaluate_active_detector",
        lambda: _qualification(qualified=True),
    )
    monkeypatch.setattr(warmup.yolo_wrapper, "detect", lambda *_args, **_kwargs: None)
    monkeypatch.setattr(warmup.yolo_wrapper, "classify", lambda *_args, **_kwargs: [])
    monkeypatch.setattr(warmup.yolo_wrapper, "get_classifier_status", lambda: {"loaded": True})
    monkeypatch.setattr(
        warmup.dino_wrapper,
        "detect",
        lambda *_args, **_kwargs: SimpleNamespace(
            degraded=True,
            error="Gewichte fehlen",
        ),
    )
    monkeypatch.setattr(warmup.sam_wrapper, "_resolve_device", lambda: "cpu")
    monkeypatch.setattr(warmup.sam_wrapper, "_load_sam_on", lambda _device: (object(), None))
    monkeypatch.setattr(
        warmup.gpu_manager,
        "ensure_loaded",
        lambda _slot, _device, loader: SimpleNamespace(model=loader()[0], processor=None),
    )

    result = warmup.warmup()

    assert result["warmup"]["dino"].startswith("fehler:")
    assert "dino" not in result["loaded"]


def test_warmup_skips_unqualified_yolo_but_warms_other_models(monkeypatch):
    calls: list[str] = []

    monkeypatch.setattr(
        warmup.detector_qualification,
        "evaluate_active_detector",
        lambda: _qualification(qualified=False),
    )
    monkeypatch.setattr(
        warmup.yolo_wrapper,
        "detect",
        lambda *_args, **_kwargs: (_ for _ in ()).throw(
            AssertionError("unqualified YOLO must not be loaded")
        ),
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

    assert calls == ["classifier", "dino", "sam"]
    assert result["warmup"]["yolo"] == "uebersprungen"
    assert result["warmup_details"]["yolo"] == {
        "status": "skipped",
        "reason_code": "detector_not_qualified",
        "qualification_status": "status_file_missing",
        "reason": "Qualifikationsdatei fehlt.",
    }
    assert "yolo" not in result["loaded"]
    assert {"classifier", "dino", "sam"}.issubset(result["loaded"])
