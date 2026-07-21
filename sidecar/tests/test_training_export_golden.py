"""Gemeinsame Golden-Fixture fuer den C#- und Sidecar-Exportweg."""

from __future__ import annotations

import base64
import hashlib
import json
from pathlib import Path

from fastapi.testclient import TestClient


_PROJECT_ROOT = Path(__file__).resolve().parents[2]
_FIXTURE_PATH = (
    _PROJECT_ROOT
    / "tests"
    / "Fixtures"
    / "TrainingExport"
    / "ap03-export-golden-v1.json"
)


def test_sidecar_writes_exact_shared_golden_fixture(tmp_path, monkeypatch):
    fixture = json.loads(_FIXTURE_PATH.read_text(encoding="utf-8"))
    expected = fixture["expected"]
    assert expected["files"], "Die gemeinsame Golden-Fixture hat keine erwarteten Dateien."

    expected_files = {entry["path"]: entry for entry in expected["files"]}
    manifest_bytes = base64.b64decode(expected_files["manifest.json"]["base64"])
    manifest = json.loads(manifest_bytes)
    image_bytes_by_hash = _image_bytes_by_hash(fixture)
    samples = [_sample_from_manifest(image, image_bytes_by_hash) for image in manifest["images"]]
    request = {
        "schema_version": manifest["schema_version"],
        "plan_id": expected["plan_id"],
        "plan_sha256": expected["plan_id"],
        "class_map_version": manifest["class_map_version"],
        "vsa_manifest_hash": manifest["vsa_manifest_hash"],
        "registry_hash": manifest["registry_hash"],
        "classes": manifest["classes"],
        "manifest_json_base64": base64.b64encode(manifest_bytes).decode("ascii"),
        "manifest_sha256": hashlib.sha256(manifest_bytes).hexdigest(),
        "samples": samples,
    }

    from sidecar.config import settings
    from sidecar.main import app

    monkeypatch.setattr(settings, "training_export_root", str(tmp_path), raising=False)
    response = TestClient(app).post("/training/export-yolo", json=request)

    assert response.status_code == 200, response.text
    assert response.json()["status"] == "created"
    dataset = tmp_path / expected["plan_id"]
    actual_files = {
        path.relative_to(dataset).as_posix(): path.read_bytes()
        for path in dataset.rglob("*")
        if path.is_file()
    }
    assert set(actual_files) == set(expected_files)
    for relative_path, expected_file in expected_files.items():
        actual = actual_files[relative_path]
        assert hashlib.sha256(actual).hexdigest() == expected_file["sha256"]
        assert actual == base64.b64decode(expected_file["base64"])


def _image_bytes_by_hash(fixture: dict) -> dict[str, bytes]:
    result: dict[str, bytes] = {}
    for candidate in fixture["candidates"]:
        raw = base64.b64decode(candidate["image_base64"])
        result[hashlib.sha256(raw).hexdigest()] = raw
    return result


def _sample_from_manifest(image: dict, image_bytes_by_hash: dict[str, bytes]) -> dict:
    image_hash = image["image_sha256"]
    raw = image_bytes_by_hash[image_hash]
    return {
        "image_sha256": image_hash,
        "image_base64": base64.b64encode(raw).decode("ascii"),
        "split": "train" if image["target"] == "train" else "val",
        "target_file_name": image["target_file_name"],
        "labels": [
            {
                "class_id": label["class_id"],
                "x_center": label["bounding_box"]["x_center"],
                "y_center": label["bounding_box"]["y_center"],
                "width": label["bounding_box"]["width"],
                "height": label["bounding_box"]["height"],
            }
            for label in image["labels"]
        ],
    }
