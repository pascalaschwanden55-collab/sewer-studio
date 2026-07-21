"""Sicherheits- und Vertragspruefungen fuer den geplanten YOLO-Export v2."""

from __future__ import annotations

import base64
import hashlib
import io
import json
import os
from pathlib import Path

import pytest
from fastapi.testclient import TestClient
from PIL import Image
from pydantic import ValidationError


PLAN_ID = "a" * 64
PLAN_SHA256 = PLAN_ID
VSA_MANIFEST_HASH = "c" * 64
REGISTRY_HASH = "d" * 64
CLASSES = ["BCA_anschluss", "BAB_riss", "SONST_schaden"]


def _make_image_bytes(
    color: tuple[int, int, int] = (100, 100, 100),
    image_format: str = "PNG",
) -> bytes:
    image = Image.new("RGB", (32, 24), color)
    buffer = io.BytesIO()
    image.save(buffer, format=image_format)
    return buffer.getvalue()


def _sample(
    raw: bytes | None = None,
    *,
    split: str = "train",
    extension: str = "png",
    labels: list[dict] | None = None,
) -> dict:
    image_bytes = raw if raw is not None else _make_image_bytes()
    image_sha256 = hashlib.sha256(image_bytes).hexdigest()
    return {
        "image_sha256": image_sha256,
        "image_base64": base64.b64encode(image_bytes).decode("ascii"),
        "split": split,
        "target_file_name": f"img_{image_sha256}.{extension}",
        "labels": labels
        if labels is not None
        else [
            {
                "class_id": 1,
                "x_center": 0.5,
                "y_center": 0.5,
                "width": 0.2,
                "height": 0.1,
            }
        ],
    }


def _manifest_bytes(
    plan_id: str = PLAN_ID,
    samples: list[dict] | None = None,
    classes: list[str] | None = None,
) -> bytes:
    planned_samples = samples if samples is not None else [_sample()]
    planned_classes = classes if classes is not None else list(CLASSES)
    images = []
    for sample in planned_samples:
        images.append(
            {
                "image_sha256": sample["image_sha256"],
                "holding_key": "100-200",
                "target": "train" if sample["split"] == "train" else "validation",
                "target_file_name": sample["target_file_name"],
                "labels": [
                    {
                        "class_id": label["class_id"],
                        "class_name": planned_classes[label["class_id"]],
                        "bounding_box": {
                            "x_center": label["x_center"],
                            "y_center": label["y_center"],
                            "width": label["width"],
                            "height": label["height"],
                        },
                        "sources": [
                            {"source_type": "training_sample", "source_id": "fixture"}
                        ],
                    }
                    for label in sample["labels"]
                ],
            }
        )
    manifest = {
        "schema_version": "2.0",
        "plan_id": plan_id,
        "class_map_version": 2,
        "vsa_manifest_hash": VSA_MANIFEST_HASH,
        "registry_hash": REGISTRY_HASH,
        "classes": planned_classes,
        "images": images,
    }
    return (
        json.dumps(manifest, ensure_ascii=False, sort_keys=True) + "\n"
    ).encode("utf-8")


def _request(
    *,
    plan_id: str = PLAN_ID,
    plan_sha256: str = PLAN_SHA256,
    samples: list[dict] | None = None,
) -> dict:
    planned_samples = samples if samples is not None else [_sample()]
    manifest = _manifest_bytes(plan_id, planned_samples)
    return {
        "schema_version": "2.0",
        "plan_id": plan_id,
        "plan_sha256": plan_sha256,
        "class_map_version": 2,
        "vsa_manifest_hash": VSA_MANIFEST_HASH,
        "registry_hash": REGISTRY_HASH,
        "classes": list(CLASSES),
        "manifest_json_base64": base64.b64encode(manifest).decode("ascii"),
        "manifest_sha256": hashlib.sha256(manifest).hexdigest(),
        "samples": planned_samples,
    }


def _refresh_manifest(request: dict) -> None:
    manifest = _manifest_bytes(
        request["plan_id"],
        request["samples"],
        request["classes"],
    )
    request["manifest_json_base64"] = base64.b64encode(manifest).decode("ascii")
    request["manifest_sha256"] = hashlib.sha256(manifest).hexdigest()


def _client() -> TestClient:
    from sidecar.main import app

    return TestClient(app)


def test_planned_export_writes_exact_split_ids_names_classes_and_manifest(
    tmp_path,
    monkeypatch,
):
    from sidecar.config import settings

    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)
    request = _request(samples=[_sample(split="val")])

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == 200
    body = response.json()
    assert body == {
        "schema_version": "2.0",
        "plan_id": PLAN_ID,
        "plan_sha256": PLAN_SHA256,
        "status": "created",
        "total_samples": 1,
        "train_count": 0,
        "val_count": 1,
        "class_count": len(CLASSES),
        "dataset_path": str((tmp_path / PLAN_ID).resolve()),
        "data_yaml_path": str((tmp_path / PLAN_ID / "data.yaml").resolve()),
        "manifest_path": str((tmp_path / PLAN_ID / "manifest.json").resolve()),
        "written_image_sha256": [request["samples"][0]["image_sha256"]],
    }

    dataset = tmp_path / PLAN_ID
    target_name = request["samples"][0]["target_file_name"]
    assert (dataset / "images" / "val" / target_name).read_bytes() == base64.b64decode(
        request["samples"][0]["image_base64"]
    )
    assert (dataset / "labels" / "val" / f"{Path(target_name).stem}.txt").read_bytes() == (
        b"1 0.500000 0.500000 0.200000 0.100000\n"
    )
    assert (dataset / "classes.txt").read_bytes() == (
        "\n".join(CLASSES) + "\n"
    ).encode("utf-8")
    assert (dataset / "data.yaml").read_bytes() == (
        "path: .\n"
        "train: images/train\n"
        "val: images/val\n"
        "nc: 3\n"
        "names:\n"
        "  0: BCA_anschluss\n"
        "  1: BAB_riss\n"
        "  2: SONST_schaden\n"
    ).encode("utf-8")
    assert (dataset / "manifest.json").read_bytes() == base64.b64decode(
        request["manifest_json_base64"]
    )


def test_planned_export_creates_zero_byte_label_for_background_image(tmp_path, monkeypatch):
    from sidecar.config import settings

    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)
    request = _request(samples=[_sample(labels=[])])

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == 200
    target_name = request["samples"][0]["target_file_name"]
    label = tmp_path / PLAN_ID / "labels" / "train" / f"{Path(target_name).stem}.txt"
    assert label.read_bytes() == b""


@pytest.mark.parametrize("legacy_field", ["output_dir", "train_split", "overwrite"])
def test_planned_export_rejects_legacy_request_fields(legacy_field):
    request = _request()
    request[legacy_field] = "legacy"

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == 422


def test_planned_export_rejects_legacy_class_name_label():
    request = _request()
    request["samples"][0]["labels"][0]["class_name"] = "BAB"

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == 422


@pytest.mark.parametrize(
    ("mutate", "expected_status"),
    [
        (lambda request: request.update(schema_version="1.0"), 422),
        (lambda request: request.update(extra=True), 422),
        (lambda request: request.update(classes=["BAB_riss", "bab_RISS"]), 422),
        (lambda request: request.update(classes=[]), 422),
        (lambda request: request.update(manifest_sha256="0" * 64), 422),
        (lambda request: request["samples"][0].update(split="exclude"), 422),
        (lambda request: request["samples"][0]["labels"][0].update(class_id=99), 422),
        (lambda request: request["samples"][0]["labels"][0].update(width=0.0), 422),
        (lambda request: request["samples"][0]["labels"][0].update(x_center=0.05, width=0.2), 422),
    ],
)
def test_planned_export_rejects_invalid_contract(mutate, expected_status):
    request = _request()
    mutate(request)

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == expected_status


def test_planned_export_rejects_non_finite_box_in_schema():
    from sidecar.schemas.segmentation import TrainingExportRequest

    request = _request()
    request["samples"][0]["labels"][0]["x_center"] = float("nan")

    with pytest.raises(ValidationError):
        TrainingExportRequest.model_validate(request)


def test_planned_export_rejects_plan_sha256_different_from_plan_id():
    request = _request(plan_sha256="e" * 64)

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == 422


@pytest.mark.parametrize(
    "mutate_manifest",
    [
        lambda manifest: manifest.update(class_map_version=99),
        lambda manifest: manifest.update(vsa_manifest_hash="e" * 64),
        lambda manifest: manifest.update(registry_hash="e" * 64),
        lambda manifest: manifest.update(classes=["BAB_riss"]),
        lambda manifest: manifest["images"][0].update(target="validation"),
        lambda manifest: manifest["images"][0].update(target_file_name=f"img_{'e' * 64}.png"),
        lambda manifest: manifest["images"][0]["labels"][0].update(class_id=0),
        lambda manifest: manifest["images"][0]["labels"][0]["bounding_box"].update(width=0.3),
    ],
)
def test_planned_export_rejects_request_that_differs_from_manifest(mutate_manifest):
    request = _request()
    manifest = json.loads(base64.b64decode(request["manifest_json_base64"]))
    mutate_manifest(manifest)
    manifest_bytes = (
        json.dumps(manifest, ensure_ascii=False, sort_keys=True) + "\n"
    ).encode("utf-8")
    request["manifest_json_base64"] = base64.b64encode(manifest_bytes).decode("ascii")
    request["manifest_sha256"] = hashlib.sha256(manifest_bytes).hexdigest()

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == 422


@pytest.mark.parametrize(
    "target_name",
    [
        "../image.png",
        "..\\image.png",
        "C:\\image.png",
        "image.png",
        f"img_{'A' * 64}.png",
        f"img_{'a' * 64}.exe",
    ],
)
def test_planned_export_rejects_unsafe_or_noncanonical_target_name(target_name):
    request = _request()
    request["samples"][0]["target_file_name"] = target_name

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == 422


def test_planned_export_rejects_duplicate_image_hash_and_target():
    sample = _sample()
    request = _request(samples=[sample, dict(sample)])

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == 422


def test_planned_export_rejects_more_than_500_samples():
    sample = _sample()
    request = _request(samples=[dict(sample) for _ in range(501)])

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == 422


@pytest.mark.parametrize("failure", ["bad-base64", "wrong-hash", "not-image", "wrong-extension"])
def test_planned_export_rejects_invalid_image_before_publish(failure, tmp_path, monkeypatch):
    from sidecar.config import settings

    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)
    request = _request()
    sample = request["samples"][0]
    if failure == "bad-base64":
        sample["image_base64"] = "not@@base64"
    elif failure == "wrong-hash":
        sample["image_base64"] = base64.b64encode(_make_image_bytes((1, 2, 3))).decode("ascii")
    elif failure == "not-image":
        raw = b"this is not an image"
        sha256 = hashlib.sha256(raw).hexdigest()
        sample.update(
            image_sha256=sha256,
            image_base64=base64.b64encode(raw).decode("ascii"),
            target_file_name=f"img_{sha256}.png",
        )
    else:
        sample["target_file_name"] = f"img_{sample['image_sha256']}.jpg"
    _refresh_manifest(request)

    response = _client().post("/training/export-yolo", json=request)

    assert response.status_code == 400
    assert not (tmp_path / PLAN_ID).exists()
    staging = tmp_path / ".staging"
    assert not staging.exists() or not any(staging.iterdir())


def test_planned_export_rejects_image_over_configured_size(tmp_path, monkeypatch):
    from sidecar.config import settings

    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)
    monkeypatch.setattr(settings, "training_max_image_bytes", 10, raising=False)

    response = _client().post("/training/export-yolo", json=_request())

    assert response.status_code == 413
    assert not (tmp_path / PLAN_ID).exists()


def test_planned_export_failure_removes_stage_and_leaves_no_final_dataset(
    tmp_path,
    monkeypatch,
):
    from sidecar.config import settings
    from sidecar.routes import training

    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)
    samples = [
        _sample(_make_image_bytes((1, 2, 3))),
        _sample(_make_image_bytes((4, 5, 6)), split="val"),
    ]
    request = _request(samples=samples)
    original_write = training._write_bytes
    image_writes = 0

    def fail_on_second_image(path: Path, data: bytes) -> None:
        nonlocal image_writes
        if path.parent.parent.name == "images":
            image_writes += 1
            if image_writes == 2:
                raise OSError("simulierter Schreibfehler")
        original_write(path, data)

    monkeypatch.setattr(training, "_write_bytes", fail_on_second_image)
    from sidecar.main import app

    response = TestClient(app, raise_server_exceptions=False).post(
        "/training/export-yolo",
        json=request,
    )

    assert response.status_code == 500
    assert not (tmp_path / PLAN_ID).exists()
    staging = tmp_path / ".staging"
    assert staging.exists()
    assert not any(staging.iterdir())


def test_planned_export_same_complete_plan_is_idempotent_without_rewrite(
    tmp_path,
    monkeypatch,
):
    from sidecar.config import settings
    from sidecar.routes import training

    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)
    request = _request()
    client = _client()
    assert client.post("/training/export-yolo", json=request).status_code == 200

    def reject_write(_path: Path, _data: bytes) -> None:
        raise AssertionError("Ein idempotenter Lauf darf nichts neu schreiben.")

    monkeypatch.setattr(training, "_write_bytes", reject_write)
    response = client.post("/training/export-yolo", json=request)

    assert response.status_code == 200
    assert response.json()["status"] == "already_complete"


def test_planned_export_same_id_different_content_is_conflict_and_preserves_target(
    tmp_path,
    monkeypatch,
):
    from sidecar.config import settings

    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)
    client = _client()
    request = _request()
    assert client.post("/training/export-yolo", json=request).status_code == 200
    label = next((tmp_path / PLAN_ID / "labels").rglob("*.txt"))
    before = label.read_bytes()

    conflicting = _request(samples=[_sample(_make_image_bytes((1, 2, 3)))])
    response = client.post("/training/export-yolo", json=conflicting)

    assert response.status_code == 409
    assert label.read_bytes() == before


def test_planned_export_corrupt_existing_target_is_conflict_and_not_repaired(
    tmp_path,
    monkeypatch,
):
    from sidecar.config import settings

    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)
    client = _client()
    request = _request()
    assert client.post("/training/export-yolo", json=request).status_code == 200
    label = next((tmp_path / PLAN_ID / "labels").rglob("*.txt"))
    label.write_text("kaputt", encoding="utf-8")

    response = client.post("/training/export-yolo", json=request)

    assert response.status_code == 409
    assert label.read_text(encoding="utf-8") == "kaputt"


def test_planned_export_never_deletes_or_changes_preexisting_target(tmp_path, monkeypatch):
    from sidecar.config import settings

    target = tmp_path / PLAN_ID
    target.mkdir()
    sentinel = target / "customer-note.txt"
    sentinel.write_text("unveraendert", encoding="utf-8")
    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)

    response = _client().post("/training/export-yolo", json=_request())

    assert response.status_code == 409
    assert sentinel.read_text(encoding="utf-8") == "unveraendert"
    assert list(target.iterdir()) == [sentinel]


def test_planned_export_rejects_extra_root_entry_and_nested_directory(tmp_path, monkeypatch):
    from sidecar.config import settings

    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)
    client = _client()
    request = _request()
    assert client.post("/training/export-yolo", json=request).status_code == 200
    dataset = tmp_path / PLAN_ID
    extra_file = dataset / "customer-note.txt"
    extra_file.write_text("unveraendert", encoding="utf-8")

    assert client.post("/training/export-yolo", json=request).status_code == 409
    assert extra_file.read_text(encoding="utf-8") == "unveraendert"

    extra_file.unlink()
    extra_directory = dataset / "images" / "train" / "unerwartet"
    extra_directory.mkdir()
    assert client.post("/training/export-yolo", json=request).status_code == 409
    assert extra_directory.is_dir()


def test_planned_export_rejects_existing_symlink_target(tmp_path, monkeypatch):
    from sidecar.config import settings

    root = tmp_path / "exports"
    outside = tmp_path / "outside"
    outside.mkdir()
    root.mkdir()
    target = root / PLAN_ID
    try:
        os.symlink(outside, target, target_is_directory=True)
    except (OSError, NotImplementedError):
        pytest.skip("Symlinks sind auf diesem System nicht verfuegbar.")

    monkeypatch.setattr(settings, "training_export_root", str(root), raising=False)
    response = _client().post("/training/export-yolo", json=_request())

    assert response.status_code == 409
    assert not any(outside.iterdir())


def test_planned_export_rejects_symlink_export_root(tmp_path, monkeypatch):
    from sidecar.config import settings

    outside = tmp_path / "outside"
    outside.mkdir()
    linked_root = tmp_path / "linked-exports"
    try:
        os.symlink(outside, linked_root, target_is_directory=True)
    except (OSError, NotImplementedError):
        pytest.skip("Symlinks sind auf diesem System nicht verfuegbar.")

    monkeypatch.setattr(settings, "training_export_root", str(linked_root), raising=False)
    response = _client().post("/training/export-yolo", json=_request())

    assert response.status_code == 500
    assert not any(outside.iterdir())


def test_training_export_route_is_sync_threadpool_handler():
    import inspect

    from sidecar.routes import training

    assert not inspect.iscoroutinefunction(training.export_yolo)
