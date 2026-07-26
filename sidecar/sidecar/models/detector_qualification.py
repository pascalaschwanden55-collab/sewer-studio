"""Fail-closed qualification check for the production YOLO detector."""

from __future__ import annotations

import hashlib
import json
import logging
import re
import threading
from pathlib import Path
from typing import Any

from ..config import settings

logger = logging.getLogger(__name__)

_SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
_hash_cache: dict[tuple[str, int, int], str] = {}
_hash_cache_lock = threading.Lock()


def evaluate_active_detector() -> dict[str, Any]:
    """Evaluate the qualification marker against the actually selected artifact."""

    try:
        from . import yolo_wrapper

        artifact = yolo_wrapper.get_active_detector_artifact()
        marker_path = Path(settings.models_dir) / "model_qualification.json"
        return evaluate_detector_qualification(artifact, marker_path)
    except Exception:
        logger.exception("Unexpected detector qualification check failure")
        return _result(
            qualified=False,
            status="qualification_check_failed",
            reason="Detektor-Qualifikation konnte nicht geprueft werden.",
            artifact={},
        )


def evaluate_detector_qualification(
    artifact: dict[str, Any],
    marker_path: Path,
) -> dict[str, Any]:
    """Validate marker structure and bind it to one exact model file.

    A missing, unreadable, malformed or non-matching marker always blocks the
    production detector. PT, TensorRT and ONNX artifacts are independent:
    qualification of one file never authorizes another file implicitly.
    """

    if artifact.get("resolution_error"):
        return _result(
            qualified=False,
            status="active_artifact_missing",
            reason="Das konfigurierte Detektor-Modell wurde nicht gefunden.",
            artifact=artifact,
        )

    if not artifact.get("using_custom_weights"):
        return _result(
            qualified=False,
            status="fallback_not_qualified",
            reason="Das allgemeine YOLO-Fallback ist kein qualifizierter Kanal-Detektor.",
            artifact=artifact,
        )

    raw_path = artifact.get("path")
    if not isinstance(raw_path, str) or not raw_path.strip():
        return _result(
            qualified=False,
            status="active_artifact_missing",
            reason="Das aktive Detektor-Artefakt ist nicht eindeutig aufgeloest.",
            artifact=artifact,
        )

    artifact_path = Path(raw_path)
    if not artifact_path.is_file():
        return _result(
            qualified=False,
            status="active_artifact_missing",
            reason="Die aktive Detektor-Datei fehlt oder ist keine Datei.",
            artifact=artifact,
        )

    artifact = {
        **artifact,
        "file_name": artifact_path.name,
        "backend": _backend_for(artifact_path.name),
    }

    if not marker_path.exists():
        return _result(
            qualified=False,
            status="status_file_missing",
            reason="Die Qualifikationsdatei model_qualification.json fehlt.",
            artifact=artifact,
        )
    if not marker_path.is_file():
        return _result(
            qualified=False,
            status="status_file_unreadable",
            reason="Die Qualifikationsdatei ist keine lesbare Datei.",
            artifact=artifact,
        )

    try:
        marker_data = json.loads(marker_path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError):
        return _result(
            qualified=False,
            status="status_file_unreadable",
            reason="Die Qualifikationsdatei ist unlesbar oder kein gueltiges JSON.",
            artifact=artifact,
        )

    try:
        detector_marker = _validated_detector_marker(marker_data)
    except ValueError as exc:
        return _result(
            qualified=False,
            status="status_file_invalid",
            reason=f"Die Qualifikationsdatei ist falsch aufgebaut: {exc}",
            artifact=artifact,
        )

    artifact_name = artifact_path.name
    marker_artifact = next(
        (
            item
            for item in detector_marker["artifacts"]
            if item["file_name"].casefold() == artifact_name.casefold()
        ),
        None,
    )
    if marker_artifact is None:
        return _result(
            qualified=False,
            status="artifact_not_listed",
            reason=(
                f"Das aktive Modell '{artifact_name}' ist in der "
                "Qualifikationsdatei nicht freigegeben."
            ),
            artifact=artifact,
            marked_utc=detector_marker.get("marked_utc"),
        )

    try:
        disk_sha256 = _sha256_cached(artifact_path)
    except OSError:
        return _result(
            qualified=False,
            status="artifact_unreadable",
            reason=f"Die aktive Modell-Datei '{artifact_name}' ist nicht lesbar.",
            artifact=artifact,
            marked_utc=detector_marker.get("marked_utc"),
        )
    except RuntimeError:
        return _result(
            qualified=False,
            status="artifact_changed_while_hashing",
            reason=(
                f"Die aktive Modell-Datei '{artifact_name}' wurde waehrend "
                "der Pruefung veraendert."
            ),
            artifact=artifact,
            marked_utc=detector_marker.get("marked_utc"),
        )

    loaded_sha256 = artifact.get("sha256")
    if artifact.get("loaded"):
        if (
            not isinstance(loaded_sha256, str)
            or not _SHA256_PATTERN.fullmatch(loaded_sha256.lower())
        ):
            return _result(
                qualified=False,
                status="active_artifact_identity_missing",
                reason="Die SHA-256 des geladenen Detektors ist nicht bekannt.",
                artifact=artifact,
                marked_utc=detector_marker.get("marked_utc"),
            )
        actual_sha256 = loaded_sha256.lower()
        if disk_sha256 != actual_sha256:
            return _result(
                qualified=False,
                status="artifact_changed_since_load",
                reason=(
                    f"Die Modell-Datei '{artifact_name}' wurde seit dem Laden "
                    "veraendert."
                ),
                artifact={**artifact, "sha256": actual_sha256},
                marked_utc=detector_marker.get("marked_utc"),
            )
    else:
        actual_sha256 = disk_sha256

    artifact_with_hash = {**artifact, "sha256": actual_sha256}
    if actual_sha256 != marker_artifact["sha256"]:
        return _result(
            qualified=False,
            status="artifact_hash_mismatch",
            reason=(
                f"SHA-256 des aktiven Modells '{artifact_name}' stimmt nicht "
                "mit der Qualifikationsdatei ueberein."
            ),
            artifact=artifact_with_hash,
            marked_utc=detector_marker.get("marked_utc"),
        )

    qualified = detector_marker["qualified"]
    return _result(
        qualified=qualified,
        status="qualified" if qualified else "unqualified",
        reason=detector_marker.get("reason"),
        artifact=artifact_with_hash,
        marked_utc=detector_marker.get("marked_utc"),
    )


def _validated_detector_marker(marker_data: Any) -> dict[str, Any]:
    if (
        not isinstance(marker_data, dict)
        or type(marker_data.get("schema_version")) is not int
        or marker_data.get("schema_version") != 1
    ):
        raise ValueError("schema_version muss 1 sein")

    detector = marker_data.get("detector")
    if not isinstance(detector, dict):
        raise ValueError("detector fehlt")

    qualified = detector.get("qualified")
    if type(qualified) is not bool:
        raise ValueError("detector.qualified muss true oder false sein")

    reason = detector.get("reason")
    if reason is not None and not isinstance(reason, str):
        raise ValueError("detector.reason muss Text oder null sein")
    if not qualified and (not isinstance(reason, str) or not reason.strip()):
        raise ValueError("ein gesperrter Detektor braucht einen Grund")

    artifacts = detector.get("artifacts")
    if not isinstance(artifacts, list) or not artifacts:
        raise ValueError("detector.artifacts fehlt oder ist leer")

    validated_artifacts: list[dict[str, str]] = []
    seen_names: set[str] = set()
    for index, entry in enumerate(artifacts):
        if not isinstance(entry, dict):
            raise ValueError(f"detector.artifacts[{index}] ist kein Objekt")

        file_name = entry.get("file_name")
        sha256 = entry.get("sha256")
        if (
            not isinstance(file_name, str)
            or not file_name.strip()
            or Path(file_name).name != file_name
        ):
            raise ValueError(f"detector.artifacts[{index}].file_name ist ungueltig")
        if not isinstance(sha256, str) or not _SHA256_PATTERN.fullmatch(sha256.lower()):
            raise ValueError(f"detector.artifacts[{index}].sha256 ist ungueltig")

        normalized_name = file_name.casefold()
        if normalized_name in seen_names:
            raise ValueError(f"Modell-Datei '{file_name}' ist doppelt eingetragen")
        seen_names.add(normalized_name)

        backend = entry.get("backend")
        expected_backend = _backend_for(file_name)
        if expected_backend not in {"pytorch", "tensorrt", "onnx"}:
            raise ValueError(f"Modell-Datei '{file_name}' hat kein erlaubtes Format")
        if backend is not None and backend != expected_backend:
            raise ValueError(
                f"Backend fuer '{file_name}' muss '{expected_backend}' sein"
            )

        validated_artifacts.append(
            {
                "file_name": file_name,
                "sha256": sha256.lower(),
                "backend": expected_backend,
            }
        )

    return {
        "qualified": qualified,
        "reason": reason.strip() if isinstance(reason, str) else None,
        "marked_utc": detector.get("marked_utc"),
        "artifacts": validated_artifacts,
    }


def _backend_for(file_name: str) -> str:
    suffix = Path(file_name).suffix.lower()
    if suffix == ".engine":
        return "tensorrt"
    if suffix in {".pt", ".pth"}:
        return "pytorch"
    if suffix == ".onnx":
        return "onnx"
    return suffix.lstrip(".") or "unknown"


def _sha256_cached(path: Path) -> str:
    resolved = path.resolve(strict=True)
    before = resolved.stat()
    cache_key = (str(resolved), before.st_size, before.st_mtime_ns)

    with _hash_cache_lock:
        cached = _hash_cache.get(cache_key)
    if cached is not None:
        return cached

    digest = hashlib.sha256()
    with resolved.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)

    after = resolved.stat()
    if before.st_size != after.st_size or before.st_mtime_ns != after.st_mtime_ns:
        raise RuntimeError("artifact changed while hashing")

    value = digest.hexdigest()
    with _hash_cache_lock:
        _hash_cache.clear()
        _hash_cache[cache_key] = value
    return value


def _result(
    *,
    qualified: bool,
    status: str,
    reason: str | None,
    artifact: dict[str, Any],
    marked_utc: Any = None,
) -> dict[str, Any]:
    file_name = artifact.get("file_name")
    backend = artifact.get("backend")
    sha256 = artifact.get("sha256")
    return {
        "qualified": qualified,
        "status": status,
        "reason": reason,
        "artifact": {
            "file_name": file_name if isinstance(file_name, str) else None,
            "sha256": sha256 if isinstance(sha256, str) else None,
            "backend": backend if isinstance(backend, str) else None,
            "loaded": bool(artifact.get("loaded")),
        },
        "marked_utc": marked_utc if isinstance(marked_utc, str) else None,
    }
