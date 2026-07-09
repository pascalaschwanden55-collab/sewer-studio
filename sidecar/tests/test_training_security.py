"""Security regression tests for training export."""

import base64
import io
import inspect

from fastapi.testclient import TestClient
from PIL import Image


def _make_test_image(w: int = 32, h: int = 32) -> str:
    img = Image.new("RGB", (w, h), (100, 100, 100))
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


def _request(output_dir: str, image_base64: str) -> dict:
    return {
        "samples": [
            {
                "image_base64": image_base64,
                "labels": [
                    {
                        "class_name": "BABAC",
                        "x_center": 0.5,
                        "y_center": 0.5,
                        "width": 0.2,
                        "height": 0.1,
                    }
                ],
            }
        ],
        "output_dir": output_dir,
        "train_split": 1.0,
    }


def _sample(class_name: str, image_base64: str | None = None) -> dict:
    return {
        "image_base64": image_base64 or _make_test_image(),
        "labels": [
            {
                "class_name": class_name,
                "x_center": 0.5,
                "y_center": 0.5,
                "width": 0.2,
                "height": 0.1,
            }
        ],
    }


def test_training_export_allows_relative_output_inside_sandbox(tmp_path, monkeypatch):
    from sidecar.main import app
    from sidecar.routes import training

    root = tmp_path / "exports"
    monkeypatch.setattr(training.settings, "training_export_root", str(root), raising=False)

    client = TestClient(app)
    resp = client.post(
        "/training/export-yolo",
        json=_request("case-a", _make_test_image()),
    )

    assert resp.status_code == 200
    data_yaml = root / "case-a" / "data.yaml"
    assert data_yaml.exists()
    assert resp.json()["data_yaml_path"] == str(data_yaml.resolve())


def test_training_export_rejects_output_dir_outside_sandbox(tmp_path, monkeypatch):
    from sidecar.main import app
    from sidecar.routes import training

    root = tmp_path / "exports"
    outside = tmp_path / "outside"
    monkeypatch.setattr(training.settings, "training_export_root", str(root), raising=False)

    client = TestClient(app)
    resp = client.post(
        "/training/export-yolo",
        json=_request(str(outside), _make_test_image()),
    )

    assert resp.status_code == 400
    assert not outside.exists()


def test_training_export_rejects_parent_directory_traversal(tmp_path, monkeypatch):
    from sidecar.main import app
    from sidecar.routes import training

    root = tmp_path / "exports"
    outside = tmp_path / "outside"
    monkeypatch.setattr(training.settings, "training_export_root", str(root), raising=False)

    client = TestClient(app)
    resp = client.post(
        "/training/export-yolo",
        json=_request("../outside", _make_test_image()),
    )

    assert resp.status_code == 400
    assert not outside.exists()


def test_training_export_rejects_images_over_size_limit(tmp_path, monkeypatch):
    from sidecar.main import app
    from sidecar.routes import training

    root = tmp_path / "exports"
    monkeypatch.setattr(training.settings, "training_export_root", str(root), raising=False)
    monkeypatch.setattr(training.settings, "training_max_image_bytes", 10, raising=False)

    client = TestClient(app)
    resp = client.post(
        "/training/export-yolo",
        json=_request("too-large", _make_test_image()),
    )

    assert resp.status_code == 413
    assert not (root / "too-large").exists()


def test_training_export_rejects_invalid_base64_image(tmp_path, monkeypatch):
    from sidecar.main import app
    from sidecar.routes import training

    root = tmp_path / "exports"
    monkeypatch.setattr(training.settings, "training_export_root", str(root), raising=False)

    client = TestClient(app)
    resp = client.post(
        "/training/export-yolo",
        json=_request("bad-image", "this is not base64"),
    )

    assert resp.status_code == 400
    assert not (root / "bad-image").exists()


def test_training_export_rejects_more_than_500_samples():
    from sidecar.main import app

    image = _make_test_image()
    client = TestClient(app)
    resp = client.post(
        "/training/export-yolo",
        json={
            "samples": [_sample("BBA", image) for _ in range(501)],
            "output_dir": "too-many",
            "train_split": 1.0,
        },
    )

    assert resp.status_code == 422


def test_training_export_route_is_sync_threadpool_handler():
    from sidecar.routes import training

    assert not inspect.iscoroutinefunction(training.export_yolo)


def test_training_export_overwrite_removes_old_generated_dataset_files(tmp_path, monkeypatch):
    from sidecar.main import app
    from sidecar.routes import training

    root = tmp_path / "exports"
    out = root / "case-a"
    stale_img = out / "images" / "train" / "sample_999999.jpg"
    stale_lbl = out / "labels" / "train" / "sample_999999.txt"
    stale_img.parent.mkdir(parents=True)
    stale_lbl.parent.mkdir(parents=True)
    stale_img.write_bytes(b"old")
    stale_lbl.write_text("99 0.5 0.5 0.1 0.1", encoding="utf-8")
    monkeypatch.setattr(training.settings, "training_export_root", str(root), raising=False)

    client = TestClient(app)
    resp = client.post(
        "/training/export-yolo",
        json={
            "samples": [_sample("BBA")],
            "output_dir": "case-a",
            "train_split": 1.0,
        },
    )

    assert resp.status_code == 200
    assert not stale_img.exists()
    assert not stale_lbl.exists()
    label_files = list((out / "labels").rglob("*.txt"))
    assert len(label_files) == 1
    assert label_files[0].read_text(encoding="utf-8").startswith("0 ")


def test_training_export_overwrite_false_rejects_existing_generated_dataset(tmp_path, monkeypatch):
    from sidecar.main import app
    from sidecar.routes import training

    root = tmp_path / "exports"
    existing = root / "case-a" / "labels" / "train" / "sample_000000.txt"
    existing.parent.mkdir(parents=True)
    existing.write_text("0 0.5 0.5 0.1 0.1", encoding="utf-8")
    monkeypatch.setattr(training.settings, "training_export_root", str(root), raising=False)

    client = TestClient(app)
    resp = client.post(
        "/training/export-yolo",
        json={
            "samples": [_sample("BBA")],
            "output_dir": "case-a",
            "train_split": 1.0,
            "overwrite": False,
        },
    )

    assert resp.status_code == 409
    assert existing.exists()


def test_training_export_split_is_deterministic():
    from sidecar.routes.training import _split_indices

    assert _split_indices(12, 0.75) == _split_indices(12, 0.75)
