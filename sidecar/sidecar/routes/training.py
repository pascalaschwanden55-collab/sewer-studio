"""Sicherer, plan-gesteuerter Export eines YOLO-Datensatzes."""

from __future__ import annotations

import base64
import binascii
import hashlib
import io
import json
import logging
import os
import shutil
import threading
import uuid
from pathlib import Path
from typing import Literal

from fastapi import APIRouter, HTTPException, status
from PIL import Image

from ..config import settings
from ..schemas.segmentation import (
    TrainingExportRequest,
    TrainingExportResponse,
    TrainingSample,
)


router = APIRouter()
logger = logging.getLogger(__name__)

_STAGING_DIRECTORY_NAME = ".staging"
_MANIFEST_FILE_NAME = "manifest.json"
_RECEIPT_FILE_NAME = "_export_receipt.json"
_CLASSES_FILE_NAME = "classes.txt"
_DATA_YAML_FILE_NAME = "data.yaml"
_EXPORT_LOCK = threading.Lock()
_IMAGE_FORMAT_EXTENSIONS: dict[str, set[str]] = {
    "JPEG": {"jpg", "jpeg"},
    "PNG": {"png"},
    "BMP": {"bmp"},
    "WEBP": {"webp"},
}


@router.post("/training/export-yolo", response_model=TrainingExportResponse)
def export_yolo(req: TrainingExportRequest) -> TrainingExportResponse:
    """Fuehrt ausschliesslich den bereits in C# festgelegten Exportplan aus."""
    try:
        # Der Sidecar laeuft als ein Prozess. Das Schloss verhindert, dass ein
        # automatischer HTTP-Retry denselben Zielordner parallel publiziert.
        with _EXPORT_LOCK:
            return _execute_plan(req)
    except HTTPException:
        raise
    except Exception as exc:
        logger.exception("Geplanter YOLO-Export ist sicher fehlgeschlagen")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="training export failed without publishing a partial dataset",
        ) from exc


def _execute_plan(req: TrainingExportRequest) -> TrainingExportResponse:
    root = _resolve_export_root()
    target = root / req.plan_id
    _require_direct_child(target, root, "Exportziel")

    manifest_bytes = req.decoded_manifest_bytes()
    classes_bytes = _build_classes_bytes(req)
    data_yaml_bytes = _build_data_yaml_bytes(req)
    expected_receipt = _build_receipt(
        req,
        manifest_bytes,
        classes_bytes,
        data_yaml_bytes,
    )

    if target.exists() or target.is_symlink():
        _require_same_existing_plan(target, req)
        _validate_all_request_images(req)
        _validate_complete_dataset(target, expected_receipt)
        return _response(req, target, "already_complete")

    staging_root = _resolve_staging_root(root)
    stage = staging_root / f"{req.plan_id}.{uuid.uuid4().hex}.tmp"
    _require_direct_child(stage, staging_root, "Arbeitsordner")
    stage.mkdir(parents=False, exist_ok=False)

    try:
        _write_staged_dataset(
            req,
            stage,
            manifest_bytes,
            classes_bytes,
            data_yaml_bytes,
            expected_receipt,
        )
        _validate_complete_dataset(stage, expected_receipt)

        try:
            if target.exists() or target.is_symlink():
                _require_same_existing_plan(target, req)
                _validate_complete_dataset(target, expected_receipt)
                return _response(req, target, "already_complete")
            os.rename(stage, target)
        except OSError:
            # Ein gleichzeitiger identischer Request kann das Ziel zuerst publiziert
            # haben. Auch dann wird niemals geloescht oder ueberschrieben.
            if not (target.exists() or target.is_symlink()):
                raise
            _require_same_existing_plan(target, req)
            _validate_complete_dataset(target, expected_receipt)
            return _response(req, target, "already_complete")

        return _response(req, target, "created")
    finally:
        _remove_own_stage(stage, staging_root)


def _resolve_export_root() -> Path:
    root = Path(os.path.abspath(Path(settings.training_export_root).expanduser()))
    try:
        root.mkdir(parents=True, exist_ok=True)
    except OSError as exc:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="training export root is not writable",
        ) from exc
    if not root.is_dir():
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="training export root is not a directory",
        )
    unsafe_component = _find_unsafe_path_component(root)
    if unsafe_component is not None:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="training export root contains a link or junction",
        )
    return root


def _resolve_staging_root(root: Path) -> Path:
    staging_root = root / _STAGING_DIRECTORY_NAME
    if staging_root.exists() or staging_root.is_symlink():
        if (
            staging_root.is_symlink()
            or _is_junction(staging_root)
            or not staging_root.is_dir()
            or staging_root.resolve() != staging_root
        ):
            raise HTTPException(
                status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
                detail="training staging root is not a safe directory",
            )
        return staging_root

    staging_root.mkdir(parents=False, exist_ok=False)
    return staging_root


def _require_direct_child(path: Path, parent: Path, label: str) -> None:
    if path.parent != parent or path == parent:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"{label} must be a direct child of the configured training root",
        )


def _require_same_existing_plan(target: Path, req: TrainingExportRequest) -> None:
    if (
        target.is_symlink()
        or _is_junction(target)
        or not target.is_dir()
        or target.resolve() != target
    ):
        raise _conflict("existing training target is not a safe dataset directory")

    receipt_path = target / _RECEIPT_FILE_NAME
    receipt = _read_json_object(receipt_path)
    if receipt is None:
        raise _conflict("existing training target has no valid export receipt")
    if receipt.get("plan_id") != req.plan_id:
        raise _conflict("existing training target belongs to another plan")
    if receipt.get("plan_sha256") != req.plan_sha256:
        raise _conflict("plan_id already exists with another plan_sha256")


def _validate_all_request_images(req: TrainingExportRequest) -> None:
    for sample in req.samples:
        _validated_image_bytes(sample)


def _write_staged_dataset(
    req: TrainingExportRequest,
    stage: Path,
    manifest_bytes: bytes,
    classes_bytes: bytes,
    data_yaml_bytes: bytes,
    expected_receipt: dict[str, object],
) -> None:
    for category in ("images", "labels"):
        for split in ("train", "val"):
            (stage / category / split).mkdir(parents=True, exist_ok=False)

    for sample in req.samples:
        raw = _validated_image_bytes(sample)
        image_path = stage / "images" / sample.split / sample.target_file_name
        label_path = stage / "labels" / sample.split / _label_file_name(sample)
        _write_bytes(image_path, raw)
        _write_bytes(label_path, _build_label_bytes(sample))

    _write_bytes(stage / _CLASSES_FILE_NAME, classes_bytes)
    _write_bytes(stage / _DATA_YAML_FILE_NAME, data_yaml_bytes)
    _write_bytes(stage / _MANIFEST_FILE_NAME, manifest_bytes)
    # Der Beleg kommt zuletzt. Ein unvollstaendiger Arbeitsordner kann deshalb nie
    # versehentlich als fertiger Datensatz gelten.
    _write_bytes(stage / _RECEIPT_FILE_NAME, _json_bytes(expected_receipt))


def _validated_image_bytes(sample: TrainingSample) -> bytes:
    max_bytes = max(1, int(settings.training_max_image_bytes))
    max_base64_chars = ((max_bytes + 2) // 3) * 4
    if len(sample.image_base64) > max_base64_chars:
        raise HTTPException(
            status_code=status.HTTP_413_CONTENT_TOO_LARGE,
            detail="image exceeds size limit",
        )

    try:
        raw = base64.b64decode(sample.image_base64, validate=True)
    except (binascii.Error, ValueError) as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="image is not valid base64",
        ) from exc
    if len(raw) > max_bytes:
        raise HTTPException(
            status_code=status.HTTP_413_CONTENT_TOO_LARGE,
            detail="image exceeds size limit",
        )
    if hashlib.sha256(raw).hexdigest() != sample.image_sha256:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="image_sha256 does not match image_base64",
        )

    try:
        with Image.open(io.BytesIO(raw)) as image:
            image_format = (image.format or "").upper()
            width, height = image.size
            if (
                width <= 0
                or height <= 0
                or width * height > max(1, int(settings.max_image_pixels))
            ):
                raise HTTPException(
                    status_code=status.HTTP_400_BAD_REQUEST,
                    detail="image exceeds pixel limit",
                )
            image.load()
    except HTTPException:
        raise
    except Exception as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="image is not a supported image",
        ) from exc

    extension = sample.target_file_name.rsplit(".", 1)[1]
    if extension not in _IMAGE_FORMAT_EXTENSIONS.get(image_format, set()):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="target file extension does not match the image format",
        )
    return raw


def _build_label_bytes(sample: TrainingSample) -> bytes:
    if not sample.labels:
        return b""

    lines = []
    for label in sample.labels:
        values = (
            _stable_coordinate(label.x_center),
            _stable_coordinate(label.y_center),
            _stable_coordinate(label.width),
            _stable_coordinate(label.height),
        )
        lines.append(
            f"{label.class_id} {values[0]:.6f} {values[1]:.6f} "
            f"{values[2]:.6f} {values[3]:.6f}"
        )
    return ("\n".join(lines) + "\n").encode("utf-8")


def _stable_coordinate(value: float) -> float:
    return 0.0 if value == 0.0 else value


def _build_classes_bytes(req: TrainingExportRequest) -> bytes:
    return ("\n".join(req.classes) + "\n").encode("utf-8")


def _build_data_yaml_bytes(req: TrainingExportRequest) -> bytes:
    lines = [
        "path: .",
        "train: images/train",
        "val: images/val",
        f"nc: {len(req.classes)}",
        "names:",
    ]
    lines.extend(f"  {class_id}: {class_name}" for class_id, class_name in enumerate(req.classes))
    return ("\n".join(lines) + "\n").encode("utf-8")


def _build_receipt(
    req: TrainingExportRequest,
    manifest_bytes: bytes,
    classes_bytes: bytes,
    data_yaml_bytes: bytes,
) -> dict[str, object]:
    images = []
    labels = []
    for sample in req.samples:
        image_relative = f"images/{sample.split}/{sample.target_file_name}"
        label_relative = f"labels/{sample.split}/{_label_file_name(sample)}"
        images.append({"path": image_relative, "sha256": sample.image_sha256})
        labels.append(
            {
                "path": label_relative,
                "sha256": hashlib.sha256(_build_label_bytes(sample)).hexdigest(),
            }
        )

    return {
        "schema_version": "2.0",
        "plan_id": req.plan_id,
        "plan_sha256": req.plan_sha256,
        "class_map_version": req.class_map_version,
        "vsa_manifest_hash": req.vsa_manifest_hash,
        "registry_hash": req.registry_hash,
        "manifest_sha256": hashlib.sha256(manifest_bytes).hexdigest(),
        "classes_sha256": hashlib.sha256(classes_bytes).hexdigest(),
        "data_yaml_sha256": hashlib.sha256(data_yaml_bytes).hexdigest(),
        "total_samples": len(req.samples),
        "train_count": sum(sample.split == "train" for sample in req.samples),
        "val_count": sum(sample.split == "val" for sample in req.samples),
        "class_count": len(req.classes),
        "images": sorted(images, key=lambda item: str(item["path"])),
        "labels": sorted(labels, key=lambda item: str(item["path"])),
    }


def _validate_complete_dataset(
    dataset: Path,
    expected_receipt: dict[str, object],
) -> None:
    if dataset.is_symlink() or not dataset.is_dir() or dataset.resolve() != dataset:
        raise _conflict("training dataset directory is unsafe or incomplete")

    expected_root_entries = {
        "images",
        "labels",
        _CLASSES_FILE_NAME,
        _DATA_YAML_FILE_NAME,
        _MANIFEST_FILE_NAME,
        _RECEIPT_FILE_NAME,
    }
    if {entry.name for entry in dataset.iterdir()} != expected_root_entries:
        raise _conflict("training dataset root entries do not match the plan")

    actual_receipt = _read_json_object(dataset / _RECEIPT_FILE_NAME)
    if actual_receipt != expected_receipt:
        raise _conflict("training dataset receipt is missing, corrupt or does not match")

    _require_expected_flat_files(dataset, expected_receipt, "images")
    _require_expected_flat_files(dataset, expected_receipt, "labels")

    _require_file_hash(
        dataset,
        _CLASSES_FILE_NAME,
        str(expected_receipt["classes_sha256"]),
    )
    _require_file_hash(
        dataset,
        _DATA_YAML_FILE_NAME,
        str(expected_receipt["data_yaml_sha256"]),
    )
    _require_file_hash(
        dataset,
        _MANIFEST_FILE_NAME,
        str(expected_receipt["manifest_sha256"]),
    )

    for entry in expected_receipt["images"]:
        _require_file_hash(dataset, str(entry["path"]), str(entry["sha256"]))
    for entry in expected_receipt["labels"]:
        _require_file_hash(dataset, str(entry["path"]), str(entry["sha256"]))


def _require_expected_flat_files(
    dataset: Path,
    receipt: dict[str, object],
    category: Literal["images", "labels"],
) -> None:
    expected_paths = {
        str(entry["path"])
        for entry in receipt[category]
    }
    actual_paths: set[str] = set()
    category_root = dataset / category
    if not _is_safe_directory(category_root, dataset):
        raise _conflict(f"training dataset {category} directory is unsafe or missing")

    root_entries = {entry.name for entry in category_root.iterdir()}
    if root_entries != {"train", "val"}:
        raise _conflict(f"training dataset {category} splits are incomplete")

    for split in ("train", "val"):
        split_root = category_root / split
        if not _is_safe_directory(split_root, dataset):
            raise _conflict(f"training dataset {category}/{split} is unsafe or missing")
        for entry in split_root.iterdir():
            if not _is_safe_file(entry, dataset):
                raise _conflict(f"training dataset contains an unsafe {category} entry")
            actual_paths.add(f"{category}/{split}/{entry.name}")

    if actual_paths != expected_paths:
        raise _conflict(f"training dataset {category} files do not match the plan")


def _require_file_hash(dataset: Path, relative_path: str, expected_sha256: str) -> None:
    path = dataset.joinpath(*relative_path.split("/"))
    if not _is_safe_file(path, dataset):
        raise _conflict(f"training dataset file is missing or unsafe: {relative_path}")
    if _file_sha256(path) != expected_sha256:
        raise _conflict(f"training dataset file hash does not match: {relative_path}")


def _is_safe_directory(path: Path, dataset: Path) -> bool:
    try:
        return (
            not path.is_symlink()
            and not _is_junction(path)
            and path.is_dir()
            and path.resolve() == path
            and (path == dataset or dataset in path.parents)
        )
    except OSError:
        return False


def _is_safe_file(path: Path, dataset: Path) -> bool:
    try:
        return (
            not path.is_symlink()
            and path.is_file()
            and path.resolve() == path
            and dataset in path.parents
        )
    except OSError:
        return False


def _read_json_object(path: Path) -> dict[str, object] | None:
    if not path.is_file() or path.is_symlink():
        return None
    try:
        value = json.loads(
            path.read_text(encoding="utf-8"),
            object_pairs_hook=_unique_json_object,
        )
    except (OSError, UnicodeDecodeError, json.JSONDecodeError, ValueError):
        return None
    return value if isinstance(value, dict) else None


def _unique_json_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def _label_file_name(sample: TrainingSample) -> str:
    return f"{Path(sample.target_file_name).stem}.txt"


def _file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _json_bytes(value: dict[str, object]) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def _write_bytes(path: Path, data: bytes) -> None:
    path.write_bytes(data)


def _is_junction(path: Path) -> bool:
    check = getattr(path, "is_junction", None)
    return bool(check()) if check is not None else False


def _find_unsafe_path_component(path: Path) -> Path | None:
    current = path
    try:
        while True:
            if (current.exists() or current.is_symlink()) and (
                current.is_symlink() or _is_junction(current)
            ):
                return current
            parent = current.parent
            if parent == current:
                return None
            current = parent
    except OSError:
        return path


def _remove_own_stage(stage: Path, staging_root: Path) -> None:
    if stage.parent != staging_root:
        logger.error("Unsicherer Arbeitsordner wurde nicht entfernt: %s", stage)
        return
    try:
        if _is_junction(stage):
            stage.rmdir()
        elif stage.is_symlink():
            stage.unlink()
        elif stage.exists() and stage.resolve() == stage:
            shutil.rmtree(stage)
        elif stage.exists():
            logger.error("Unsicherer Arbeitsordner wurde nicht rekursiv entfernt: %s", stage)
    except OSError:
        logger.exception("Arbeitsordner konnte nicht entfernt werden: %s", stage)


def _response(
    req: TrainingExportRequest,
    target: Path,
    export_status: Literal["created", "already_complete"],
) -> TrainingExportResponse:
    return TrainingExportResponse(
        schema_version="2.0",
        plan_id=req.plan_id,
        plan_sha256=req.plan_sha256,
        status=export_status,
        total_samples=len(req.samples),
        train_count=sum(sample.split == "train" for sample in req.samples),
        val_count=sum(sample.split == "val" for sample in req.samples),
        class_count=len(req.classes),
        dataset_path=str(target.resolve()),
        data_yaml_path=str((target / _DATA_YAML_FILE_NAME).resolve()),
        manifest_path=str((target / _MANIFEST_FILE_NAME).resolve()),
        written_image_sha256=[sample.image_sha256 for sample in req.samples],
    )


def _conflict(detail: str) -> HTTPException:
    return HTTPException(status_code=status.HTTP_409_CONFLICT, detail=detail)
