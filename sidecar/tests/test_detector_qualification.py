"""Focused tests for fail-closed detector qualification."""

import hashlib
import json
from pathlib import Path

import pytest

from sidecar.models import detector_qualification


def _artifact(path: Path, *, backend: str | None = None) -> dict:
    suffix_backend = {
        ".engine": "tensorrt",
        ".onnx": "onnx",
    }.get(path.suffix.lower(), "pytorch")
    return {
        "path": str(path),
        "file_name": path.name,
        "backend": backend or suffix_backend,
        "using_custom_weights": True,
        "loaded": False,
        "resolution_error": None,
    }


def _write_marker(
    marker_path: Path,
    *,
    artifacts: list[Path],
    qualified: bool = True,
) -> None:
    marker_path.write_text(
        json.dumps(
            {
                "schema_version": 1,
                "detector": {
                    "qualified": qualified,
                    "reason": None if qualified else "Nicht freigegeben.",
                    "marked_utc": "2026-07-25T00:00:00Z",
                    "artifacts": [
                        {
                            "file_name": path.name,
                            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                            "backend": _artifact(path)["backend"],
                        }
                        for path in artifacts
                    ],
                },
            }
        ),
        encoding="utf-8",
    )


def test_missing_status_file_fails_closed(tmp_path: Path):
    weights = tmp_path / "detector.pt"
    weights.write_bytes(b"weights")

    result = detector_qualification.evaluate_detector_qualification(
        _artifact(weights),
        tmp_path / "model_qualification.json",
    )

    assert result["qualified"] is False
    assert result["status"] == "status_file_missing"


def test_malformed_json_fails_closed(tmp_path: Path):
    weights = tmp_path / "detector.pt"
    weights.write_bytes(b"weights")
    marker = tmp_path / "model_qualification.json"
    marker.write_text("{ kaputt", encoding="utf-8")

    result = detector_qualification.evaluate_detector_qualification(
        _artifact(weights), marker
    )

    assert result["qualified"] is False
    assert result["status"] == "status_file_unreadable"


@pytest.mark.parametrize(
    "marker_data",
    [
        {},
        {"schema_version": True, "detector": {}},
        {"schema_version": 1},
        {"schema_version": 1, "detector": {"qualified": True}},
        {
            "schema_version": 1,
            "detector": {
                "qualified": "yes",
                "artifacts": [
                    {"file_name": "detector.pt", "sha256": "0" * 64}
                ],
            },
        },
    ],
)
def test_wrong_marker_structure_fails_closed(tmp_path: Path, marker_data: dict):
    weights = tmp_path / "detector.pt"
    weights.write_bytes(b"weights")
    marker = tmp_path / "model_qualification.json"
    marker.write_text(json.dumps(marker_data), encoding="utf-8")

    result = detector_qualification.evaluate_detector_qualification(
        _artifact(weights), marker
    )

    assert result["qualified"] is False
    assert result["status"] == "status_file_invalid"


def test_exact_file_name_and_sha256_are_required(tmp_path: Path):
    weights = tmp_path / "detector.pt"
    weights.write_bytes(b"weights-v1")
    marker = tmp_path / "model_qualification.json"
    _write_marker(marker, artifacts=[weights])
    weights.write_bytes(b"weights-v2")

    result = detector_qualification.evaluate_detector_qualification(
        _artifact(weights), marker
    )

    assert result["qualified"] is False
    assert result["status"] == "artifact_hash_mismatch"
    assert result["artifact"]["sha256"] == hashlib.sha256(b"weights-v2").hexdigest()


def test_pt_marker_does_not_implicitly_authorize_engine(tmp_path: Path):
    weights = tmp_path / "detector.pt"
    engine = tmp_path / "detector.engine"
    weights.write_bytes(b"pt")
    engine.write_bytes(b"engine")
    marker = tmp_path / "model_qualification.json"
    _write_marker(marker, artifacts=[weights])

    result = detector_qualification.evaluate_detector_qualification(
        _artifact(engine), marker
    )

    assert result["qualified"] is False
    assert result["status"] == "artifact_not_listed"


def test_engine_is_qualified_only_with_its_own_sha256(tmp_path: Path):
    weights = tmp_path / "detector.pt"
    engine = tmp_path / "detector.engine"
    weights.write_bytes(b"pt")
    engine.write_bytes(b"engine")
    marker = tmp_path / "model_qualification.json"
    _write_marker(marker, artifacts=[weights, engine])

    result = detector_qualification.evaluate_detector_qualification(
        _artifact(engine), marker
    )

    assert result["qualified"] is True
    assert result["status"] == "qualified"
    assert result["artifact"]["backend"] == "tensorrt"
    assert result["artifact"]["sha256"] == hashlib.sha256(b"engine").hexdigest()


def test_explicit_unqualified_marker_keeps_matching_artifact_blocked(tmp_path: Path):
    weights = tmp_path / "detector.pt"
    weights.write_bytes(b"weights")
    marker = tmp_path / "model_qualification.json"
    _write_marker(marker, artifacts=[weights], qualified=False)

    result = detector_qualification.evaluate_detector_qualification(
        _artifact(weights), marker
    )

    assert result["qualified"] is False
    assert result["status"] == "unqualified"
    assert result["reason"] == "Nicht freigegeben."


def test_loaded_model_is_bound_to_loaded_sha_not_replaced_disk_file(tmp_path: Path):
    weights = tmp_path / "detector.pt"
    weights.write_bytes(b"loaded-version")
    marker = tmp_path / "model_qualification.json"
    _write_marker(marker, artifacts=[weights])
    loaded_sha256 = hashlib.sha256(weights.read_bytes()).hexdigest()
    weights.write_bytes(b"replaced-version")
    loaded_artifact = {
        **_artifact(weights),
        "loaded": True,
        "sha256": loaded_sha256,
    }

    result = detector_qualification.evaluate_detector_qualification(
        loaded_artifact, marker
    )

    assert result["qualified"] is False
    assert result["status"] == "artifact_changed_since_load"
