"""Security regression tests for training export."""

import base64
import io

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
