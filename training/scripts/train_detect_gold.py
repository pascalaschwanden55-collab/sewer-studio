r"""Trainiert einen getrennten YOLO-Detect-Kandidaten aus geprueftem Gold.

Das Skript akzeptiert ausschliesslich einen unveraenderlichen, plan-gesteuerten
Export unter ``<KnowledgeRoot>/training/datasets/<plan_id>``. Es prueft den
Exportbeleg, den Plan, alle Datei-Hashes, die aktive 15er-Klassenkarte und jedes
YOLO-Label erneut. Produktive Gewichte oder Modellzeiger werden nie veraendert.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import re
import shutil
import socket
import subprocess
import urllib.error
import urllib.request
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


ACTIVE_CLASS_MAP_VERSION = 3
ACTIVE_CLASSES = (
    "BCA_anschluss",
    "BAB_riss",
    "BAC_bruch",
    "BAA_verformung",
    "BAF_oberflaeche",
    "BAH_schadanschluss",
    "BAI_dichtung",
    "BAJ_verbindung",
    "BBA_wurzeln",
    "BBB_anhaftung",
    "BBC_ablagerung",
    "BBD_boden",
    "BBF_infiltration",
    "SONST_schaden",
    "BCC_bogen",
)
EXPECTED_PLAN_SCHEMA_VERSION = "2.0"
MINIMUM_IMAGES = 30
MINIMUM_FREE_VRAM_MB = 28_000
SIDECAR_HEALTH_URL = "http://127.0.0.1:8100/health"
SUPPORTED_IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}
SHA256_PATTERN = re.compile(r"[0-9a-f]{64}")

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
ACTIVE_CLASS_MAP_PATH = (
    REPOSITORY_ROOT / "training" / "class_maps" / "detect_class_map_v3.json"
)


@dataclass(frozen=True)
class ActiveClassMap:
    version: int
    classes: tuple[str, ...]
    vsa_manifest_hash: str
    sha256: str


@dataclass(frozen=True)
class LabelRow:
    class_id: int
    x_center: float
    y_center: float
    width: float
    height: float


@dataclass(frozen=True)
class DatasetFiles:
    images: dict[str, dict[str, Path]]
    labels: dict[str, dict[str, Path]]
    image_relative_paths: frozenset[str]
    label_relative_paths: frozenset[str]


@dataclass(frozen=True)
class ValidatedDataset:
    root: Path
    data_yaml: Path
    manifest: Path
    plan_id: str
    image_count: int
    train_count: int
    validation_count: int
    instance_count: int
    instances_per_class: dict[str, int]
    manifest_sha256: str
    receipt_sha256: str
    data_yaml_sha256: str
    classes_sha256: str
    class_map_version: int
    class_map_sha256: str
    vsa_manifest_hash: str


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _require_sha256(value: object, label: str) -> str:
    if not isinstance(value, str) or not SHA256_PATTERN.fullmatch(value):
        raise ValueError(f"{label} ist kein kanonischer SHA-256.")
    return value


def _is_within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except (OSError, ValueError):
        return False


def _load_json_object(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{path} ist kein lesbares JSON-Objekt.") from error
    if not isinstance(value, dict):
        raise ValueError(f"{path} muss ein JSON-Objekt enthalten.")
    return value


def load_active_class_map(path: Path = ACTIVE_CLASS_MAP_PATH) -> ActiveClassMap:
    if path.is_symlink():
        raise ValueError(f"Aktive Detect-Klassenkarte ist eine Verknuepfung: {path}")
    class_map_path = path.resolve()
    if not class_map_path.is_file():
        raise ValueError(f"Aktive Detect-Klassenkarte fehlt oder ist unsicher: {class_map_path}")
    document = _load_json_object(class_map_path)
    if document.get("version") != ACTIVE_CLASS_MAP_VERSION:
        raise ValueError(
            f"Erwartet wird Detect-Klassenkarte v{ACTIVE_CLASS_MAP_VERSION}."
        )
    raw_classes = document.get("classes")
    expected_mapping = {
        class_name: class_id for class_id, class_name in enumerate(ACTIVE_CLASSES)
    }
    if raw_classes != expected_mapping:
        raise ValueError("Die aktive Detect-Klassenkarte entspricht nicht den festen IDs 0..14.")
    vsa_manifest_hash = _require_sha256(
        document.get("vsa_manifest_hash"),
        "VSA-Manifest-Hash der aktiven Klassenkarte",
    )
    return ActiveClassMap(
        version=ACTIVE_CLASS_MAP_VERSION,
        classes=ACTIVE_CLASSES,
        vsa_manifest_hash=vsa_manifest_hash,
        sha256=_sha256_file(class_map_path),
    )


def discover_dataset(dataset_root: Path) -> Path:
    root = dataset_root.resolve()
    if not root.is_dir():
        raise ValueError(f"Datensatzwurzel fehlt: {root}")
    candidates = [
        directory
        for directory in root.iterdir()
        if directory.is_dir()
        and not directory.is_symlink()
        and not directory.name.startswith(".")
        and all(
            (directory / name).is_file()
            for name in (
                "manifest.json",
                "_export_receipt.json",
                "data.yaml",
                "classes.txt",
            )
        )
    ]
    if not candidates:
        raise ValueError("Kein plan-gesteuerter Detect-Gold-Datensatz wurde gefunden.")
    return max(candidates, key=lambda directory: directory.stat().st_mtime_ns)


def _unquote_yaml_scalar(value: str) -> str:
    stripped = value.strip()
    if (
        len(stripped) >= 2
        and stripped[0] in ("'", '"')
        and stripped[-1] == stripped[0]
    ):
        return stripped[1:-1]
    return stripped


def _validate_data_yaml(path: Path, classes: tuple[str, ...]) -> None:
    top_level: dict[str, str] = {}
    names: dict[int, str] = {}
    inside_names = False
    for line_number, line in enumerate(
        path.read_text(encoding="utf-8-sig").splitlines(),
        start=1,
    ):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if line[0].isspace():
            if not inside_names:
                raise ValueError(
                    f"Unerwarteter eingerueckter data.yaml-Eintrag, Zeile {line_number}."
                )
            match = re.fullmatch(r"\s{2}([0-9]+):\s*(.+?)\s*", line)
            if not match:
                raise ValueError(f"Ungueltiger Klassenname in data.yaml, Zeile {line_number}.")
            class_id = int(match.group(1))
            if class_id in names:
                raise ValueError(f"Doppelte Klassen-ID in data.yaml: {class_id}")
            names[class_id] = _unquote_yaml_scalar(match.group(2))
            continue

        inside_names = False
        if ":" not in line:
            raise ValueError(f"Ungueltige data.yaml, Zeile {line_number}.")
        key, raw_value = line.split(":", maxsplit=1)
        key = key.strip()
        if key in top_level:
            raise ValueError(f"data.yaml enthaelt den Schluessel mehrfach: {key}")
        value = _unquote_yaml_scalar(raw_value)
        top_level[key] = value
        inside_names = key == "names"

    if set(top_level) != {"path", "train", "val", "nc", "names"}:
        raise ValueError("data.yaml darf nur path, train, val, nc und names enthalten.")
    if (
        top_level["path"] != "."
        or top_level["train"] != "images/train"
        or top_level["val"] != "images/val"
    ):
        raise ValueError(
            "data.yaml darf nur '.', 'images/train' und 'images/val' verwenden."
        )
    if top_level["names"]:
        raise ValueError("data.yaml muss die Klassennamen eingerueckt unter 'names:' fuehren.")
    try:
        class_count = int(top_level["nc"])
    except ValueError as error:
        raise ValueError("data.yaml enthaelt keine gueltige Klassenzahl.") from error
    expected_names = {class_id: name for class_id, name in enumerate(classes)}
    if class_count != len(classes) or names != expected_names:
        raise ValueError("data.yaml entspricht nicht der aktiven 15er-Klassenkarte.")


def _require_safe_directory(path: Path, dataset_root: Path, label: str) -> Path:
    if not path.is_dir() or path.is_symlink() or not _is_within(path, dataset_root):
        raise ValueError(f"Unsicherer oder fehlender Datensatzordner: {label}")
    return path.resolve()


def _list_regular_files(directory: Path, dataset_root: Path, label: str) -> list[Path]:
    files: list[Path] = []
    for entry in directory.iterdir():
        if (
            entry.is_symlink()
            or not entry.is_file()
            or not _is_within(entry, dataset_root)
            or entry.resolve().parent != directory.resolve()
        ):
            raise ValueError(f"Unsicherer oder unerwarteter Eintrag unter {label}: {entry.name}")
        files.append(entry)
    return files


def _collect_dataset_files(root: Path) -> DatasetFiles:
    allowed_root_entries = {
        "images",
        "labels",
        "classes.txt",
        "data.yaml",
        "manifest.json",
        "_export_receipt.json",
    }
    if {entry.name for entry in root.iterdir()} != allowed_root_entries:
        raise ValueError("Der Datensatz enthaelt unerwartete oder fehlende Haupteintraege.")

    images: dict[str, dict[str, Path]] = {}
    labels: dict[str, dict[str, Path]] = {}
    image_relative_paths: set[str] = set()
    label_relative_paths: set[str] = set()
    for category in ("images", "labels"):
        category_root = _require_safe_directory(root / category, root, category)
        allowed_category_entries = {"train", "val"}
        if category == "labels":
            allowed_category_entries.update({"train.cache", "val.cache"})
        actual_category_entries = {entry.name for entry in category_root.iterdir()}
        if not actual_category_entries.issubset(allowed_category_entries):
            raise ValueError(f"Unerwarteter Eintrag im Datensatzordner '{category}'.")
        if not {"train", "val"}.issubset(actual_category_entries):
            raise ValueError(f"Datensatzordner '{category}' hat unvollstaendige Splits.")
        if category == "labels":
            for cache_name in actual_category_entries & {"train.cache", "val.cache"}:
                cache_path = category_root / cache_name
                if (
                    cache_path.is_symlink()
                    or not cache_path.is_file()
                    or not _is_within(cache_path, root)
                ):
                    raise ValueError(f"Unsicherer Ultralytics-Cache: {cache_path}")

        for split in ("train", "val"):
            split_root = _require_safe_directory(
                category_root / split,
                root,
                f"{category}/{split}",
            )
            by_stem: dict[str, Path] = {}
            for path in _list_regular_files(split_root, root, f"{category}/{split}"):
                if category == "images":
                    if path.suffix.lower() not in SUPPORTED_IMAGE_EXTENSIONS:
                        raise ValueError(f"Nicht unterstuetztes Bildformat: {path.name}")
                elif path.suffix.lower() != ".txt":
                    raise ValueError(f"Unerwartete Labeldatei: {path.name}")
                stem_key = path.stem.lower()
                if stem_key in by_stem:
                    raise ValueError(
                        f"Doppelter Dateistamm im Split '{split}': {path.stem}"
                    )
                by_stem[stem_key] = path
                relative = path.relative_to(root).as_posix()
                if category == "images":
                    image_relative_paths.add(relative)
                else:
                    label_relative_paths.add(relative)
            if category == "images":
                images[split] = by_stem
            else:
                labels[split] = by_stem

    for split in ("train", "val"):
        if set(images[split]) != set(labels[split]):
            raise ValueError(f"Bilder und Labels stimmen im Split '{split}' nicht ueberein.")
    return DatasetFiles(
        images=images,
        labels=labels,
        image_relative_paths=frozenset(image_relative_paths),
        label_relative_paths=frozenset(label_relative_paths),
    )


def _validate_receipt_entries(
    dataset: Path,
    receipt: dict[str, Any],
    category: str,
    expected_relative_paths: frozenset[str],
) -> dict[str, str]:
    entries = receipt.get(category)
    if not isinstance(entries, list):
        raise ValueError(f"Der Datensatzbeleg enthaelt keine gueltige Liste '{category}'.")
    hashes: dict[str, str] = {}
    expected_prefix = f"{category}/"
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) != {"path", "sha256"}:
            raise ValueError(f"Ungueltiger {category}-Eintrag im Datensatzbeleg.")
        relative = entry.get("path")
        if (
            not isinstance(relative, str)
            or not relative.startswith(expected_prefix)
            or "\\" in relative
            or relative.startswith("/")
        ):
            raise ValueError(f"Unsicherer {category}-Pfad im Datensatzbeleg: {relative}")
        expected_hash = _require_sha256(
            entry.get("sha256"),
            f"SHA-256 fuer {relative}",
        )
        if relative in hashes:
            raise ValueError(f"Doppelter Dateipfad im Datensatzbeleg: {relative}")
        unresolved_target = dataset / Path(relative)
        target = unresolved_target.resolve()
        if (
            not _is_within(target, dataset)
            or not target.is_file()
            or unresolved_target.is_symlink()
        ):
            raise ValueError(f"Unsichere oder fehlende Datensatzdatei: {relative}")
        if _sha256_file(target) != expected_hash:
            raise ValueError(f"Datensatzdatei wurde nach dem Export veraendert: {relative}")
        hashes[relative] = expected_hash
    if set(hashes) != set(expected_relative_paths):
        raise ValueError(
            f"Die {category}-Liste im Datensatzbeleg ist nicht vollstaendig."
        )
    return hashes


def _validate_receipt(
    dataset: Path,
    manifest_path: Path,
    data_yaml: Path,
    classes_path: Path,
    files: DatasetFiles,
) -> tuple[dict[str, Any], dict[str, str], dict[str, str]]:
    receipt_path = dataset / "_export_receipt.json"
    receipt = _load_json_object(receipt_path)
    for field, target, label in (
        ("manifest_sha256", manifest_path, "manifest.json"),
        ("data_yaml_sha256", data_yaml, "data.yaml"),
        ("classes_sha256", classes_path, "classes.txt"),
    ):
        expected_hash = _require_sha256(receipt.get(field), f"{label}-Hash")
        if expected_hash != _sha256_file(target):
            raise ValueError(f"Der Datensatzbeleg passt nicht mehr zu {label}.")
    image_hashes = _validate_receipt_entries(
        dataset,
        receipt,
        "images",
        files.image_relative_paths,
    )
    label_hashes = _validate_receipt_entries(
        dataset,
        receipt,
        "labels",
        files.label_relative_paths,
    )
    return receipt, image_hashes, label_hashes


def _validate_bbox(values: Iterable[float], label: str) -> tuple[float, float, float, float]:
    x_center, y_center, width, height = tuple(values)
    if (
        not all(math.isfinite(value) for value in (x_center, y_center, width, height))
        or not 0 <= x_center <= 1
        or not 0 <= y_center <= 1
        or not 0 < width <= 1
        or not 0 < height <= 1
        or x_center - width / 2 < -1e-6
        or y_center - height / 2 < -1e-6
        or x_center + width / 2 > 1 + 1e-6
        or y_center + height / 2 > 1 + 1e-6
    ):
        raise ValueError(f"BBox ausserhalb des Bildes: {label}")
    return x_center, y_center, width, height


def _parse_label_file(path: Path) -> list[LabelRow]:
    rows: list[LabelRow] = []
    seen: set[tuple[int, float, float, float, float]] = set()
    for line_number, line in enumerate(
        path.read_text(encoding="utf-8-sig").splitlines(),
        start=1,
    ):
        fields = line.split()
        if len(fields) != 5:
            raise ValueError(f"Ungueltiges YOLO-Label: {path}, Zeile {line_number}")
        try:
            class_id = int(fields[0])
            coordinates = tuple(float(value) for value in fields[1:])
        except ValueError as error:
            raise ValueError(
                f"Ungueltige Zahl im YOLO-Label: {path}, Zeile {line_number}"
            ) from error
        if class_id not in range(len(ACTIVE_CLASSES)):
            raise ValueError(f"Klassen-ID ausserhalb 0..14: {path}, Zeile {line_number}")
        x_center, y_center, width, height = _validate_bbox(
            coordinates,
            f"{path}, Zeile {line_number}",
        )
        key = (class_id, x_center, y_center, width, height)
        if key in seen:
            raise ValueError(f"Doppeltes YOLO-Label: {path}, Zeile {line_number}")
        seen.add(key)
        rows.append(LabelRow(*key))
    return rows


def _manifest_label_row(
    value: object,
    classes: tuple[str, ...],
    image_name: str,
    label_index: int,
) -> LabelRow:
    if not isinstance(value, dict):
        raise ValueError(f"Ungueltiges Plan-Label auf Bild {image_name}.")
    class_id = value.get("class_id")
    if isinstance(class_id, bool) or not isinstance(class_id, int):
        raise ValueError(f"Ungueltige Klassen-ID im Plan auf Bild {image_name}.")
    if class_id not in range(len(classes)):
        raise ValueError(f"Klassen-ID ausserhalb 0..14 im Plan auf Bild {image_name}.")
    if value.get("class_name") != classes[class_id]:
        raise ValueError(f"Klassen-ID und Klassenname passen im Plan nicht zusammen.")
    bbox = value.get("bounding_box")
    if not isinstance(bbox, dict):
        raise ValueError(f"BBox fehlt im Plan auf Bild {image_name}.")
    try:
        coordinates = tuple(
            float(bbox[key]) for key in ("x_center", "y_center", "width", "height")
        )
    except (KeyError, TypeError, ValueError) as error:
        raise ValueError(f"Ungueltige Plan-BBox auf Bild {image_name}.") from error
    x_center, y_center, width, height = _validate_bbox(
        coordinates,
        f"Planbild {image_name}, Label {label_index}",
    )
    return LabelRow(class_id, x_center, y_center, width, height)


def _label_rows_equal(left: LabelRow, right: LabelRow) -> bool:
    return left.class_id == right.class_id and all(
        math.isclose(first, second, rel_tol=0.0, abs_tol=1e-6)
        for first, second in (
            (left.x_center, right.x_center),
            (left.y_center, right.y_center),
            (left.width, right.width),
            (left.height, right.height),
        )
    )


def _validate_manifest_images(
    manifest: dict[str, Any],
    root: Path,
    files: DatasetFiles,
    classes: tuple[str, ...],
    receipt_image_hashes: dict[str, str],
) -> dict[str, int]:
    planned_images = manifest.get("images")
    if not isinstance(planned_images, list):
        raise ValueError("manifest.json enthaelt keine gueltige Bilderliste.")
    expected_image_count = sum(len(files.images[split]) for split in ("train", "val"))
    if len(planned_images) != expected_image_count:
        raise ValueError("Die Bilderliste in manifest.json ist nicht vollstaendig.")

    seen_images: set[str] = set()
    seen_hashes: set[str] = set()
    instance_counts = {class_name: 0 for class_name in classes}
    for planned in planned_images:
        if not isinstance(planned, dict):
            raise ValueError("Ungueltiger Bildeintrag in manifest.json.")
        target = planned.get("target")
        if target not in ("train", "validation"):
            raise ValueError("Ungueltiger Split in manifest.json.")
        split = "train" if target == "train" else "val"
        file_name = planned.get("target_file_name")
        if (
            not isinstance(file_name, str)
            or not file_name
            or "/" in file_name
            or "\\" in file_name
            or Path(file_name).name != file_name
        ):
            raise ValueError(f"Unsicherer Zieldateiname in manifest.json: {file_name}")
        image_sha256 = _require_sha256(
            planned.get("image_sha256"),
            f"Bild-Hash fuer {file_name}",
        )
        if not file_name.lower().startswith(f"img_{image_sha256}."):
            raise ValueError(f"Zieldateiname und Bild-Hash passen nicht zusammen: {file_name}")
        relative_image = f"images/{split}/{file_name}"
        if relative_image not in files.image_relative_paths:
            raise ValueError(f"Planbild fehlt im Datensatz: {relative_image}")
        if relative_image in seen_images or image_sha256 in seen_hashes:
            raise ValueError(f"Doppeltes Planbild: {file_name}")
        seen_images.add(relative_image)
        seen_hashes.add(image_sha256)
        image_path = root / relative_image
        if (
            receipt_image_hashes.get(relative_image) != image_sha256
            or _sha256_file(image_path) != image_sha256
        ):
            raise ValueError(f"Plan, Receipt und Bildbytes stimmen nicht ueberein: {file_name}")

        label_path = root / "labels" / split / f"{Path(file_name).stem}.txt"
        rows = _parse_label_file(label_path)
        planned_labels = planned.get("labels")
        if not isinstance(planned_labels, list):
            raise ValueError(f"Plan-Labels fehlen fuer Bild {file_name}.")
        negative_flag = planned.get("is_negative", False)
        if not isinstance(negative_flag, bool):
            raise ValueError(f"Negativkennzeichen ist ungueltig: {file_name}")
        if negative_flag:
            if planned_labels or rows or label_path.stat().st_size != 0:
                raise ValueError(
                    f"Geprueftes Negativbild muss eine exakt leere Labeldatei haben: {file_name}"
                )
        elif not planned_labels or not rows:
            raise ValueError(
                f"Leeres Label ist nicht als geprueftes Negativ markiert: {file_name}"
            )
        if len(planned_labels) != len(rows):
            raise ValueError(f"Plan und YOLO-Label unterscheiden sich: {file_name}")
        for label_index, (planned_label, row) in enumerate(
            zip(planned_labels, rows, strict=True),
            start=1,
        ):
            planned_row = _manifest_label_row(
                planned_label,
                classes,
                file_name,
                label_index,
            )
            if not _label_rows_equal(planned_row, row):
                raise ValueError(f"Plan und YOLO-Label unterscheiden sich: {file_name}")
            instance_counts[classes[row.class_id]] += 1

    if seen_images != set(files.image_relative_paths):
        raise ValueError("manifest.json bindet nicht alle Datensatzbilder.")
    return instance_counts


def _validate_manifest_header(
    manifest: dict[str, Any],
    root: Path,
    class_map: ActiveClassMap,
) -> str:
    if manifest.get("schema_version") != EXPECTED_PLAN_SCHEMA_VERSION:
        raise ValueError("Unbekannte oder fehlende Exportplan-Version.")
    plan_id = _require_sha256(manifest.get("plan_id"), "Plan-ID")
    if root.name != plan_id:
        raise ValueError("Datensatzordner und unveraenderlicher Plan stimmen nicht ueberein.")
    if manifest.get("class_map_version") != class_map.version:
        raise ValueError("manifest.json verwendet nicht die aktive Detect-Klassenkarte.")
    if manifest.get("classes") != list(class_map.classes):
        raise ValueError("manifest.json entspricht nicht der aktiven 15er-Klassenkarte.")
    if manifest.get("vsa_manifest_hash") != class_map.vsa_manifest_hash:
        raise ValueError("manifest.json ist an ein anderes VSA-Manifest gebunden.")
    _require_sha256(manifest.get("registry_hash"), "Exportregister-Hash")
    if not isinstance(manifest.get("inventory_run_id"), str) or not str(
        manifest["inventory_run_id"]
    ).strip():
        raise ValueError("Inventar-Run-ID fehlt in manifest.json.")
    source_hashes = manifest.get("source_snapshot_hashes")
    if (
        not isinstance(source_hashes, dict)
        or not source_hashes
        or any(not isinstance(name, str) or not name.strip() for name in source_hashes)
    ):
        raise ValueError("Quellen-Hashes fehlen in manifest.json.")
    for source_name, source_hash in source_hashes.items():
        _require_sha256(source_hash, f"Quellen-Hash '{source_name}'")
    protected_sets = manifest.get("protected_sets")
    if not isinstance(protected_sets, list) or not protected_sets:
        raise ValueError("Schutz-Set-Referenz fehlt in manifest.json.")
    for protected_set in protected_sets:
        if (
            not isinstance(protected_set, dict)
            or not isinstance(protected_set.get("set_id"), str)
            or not protected_set["set_id"].strip()
        ):
            raise ValueError("Ungueltige Schutz-Set-Referenz in manifest.json.")
        _require_sha256(
            protected_set.get("manifest_sha256"),
            f"Schutz-Set '{protected_set['set_id']}'",
        )
    return plan_id


def _validate_receipt_metadata(
    receipt: dict[str, Any],
    manifest: dict[str, Any],
    class_map: ActiveClassMap,
    plan_id: str,
    train_count: int,
    validation_count: int,
) -> None:
    expected_values = {
        "schema_version": EXPECTED_PLAN_SCHEMA_VERSION,
        "plan_id": plan_id,
        "plan_sha256": plan_id,
        "class_count": len(class_map.classes),
        "class_map_version": class_map.version,
        "vsa_manifest_hash": class_map.vsa_manifest_hash,
        "registry_hash": manifest["registry_hash"],
        "total_samples": train_count + validation_count,
        "train_count": train_count,
        "val_count": validation_count,
    }
    for field, expected in expected_values.items():
        if receipt.get(field) != expected:
            raise ValueError(f"Der Datensatzbeleg hat einen falschen Wert fuer '{field}'.")


def _validate_manifest_instance_counts(
    manifest: dict[str, Any],
    actual_counts: dict[str, int],
) -> None:
    raw_counts = manifest.get("instances_per_class")
    if not isinstance(raw_counts, dict):
        raise ValueError("Instanzzahlen fehlen in manifest.json.")
    expected_nonzero = {
        class_name: count for class_name, count in actual_counts.items() if count > 0
    }
    if (
        set(raw_counts) - set(ACTIVE_CLASSES)
        or any(
            isinstance(value, bool) or not isinstance(value, int) or value < 1
            for value in raw_counts.values()
        )
        or raw_counts != expected_nonzero
    ):
        raise ValueError(
            "Die Instanzzahlen in manifest.json stimmen nicht mit allen Labels ueberein."
        )


def validate_dataset(dataset: Path, dataset_root: Path) -> ValidatedDataset:
    if dataset.is_symlink():
        raise ValueError(f"Der Datensatzordner ist eine Verknuepfung: {dataset}")
    root = dataset.resolve()
    allowed_root = dataset_root.resolve()
    if (
        root.parent != allowed_root
        or not root.is_dir()
        or root.is_symlink()
        or not SHA256_PATTERN.fullmatch(root.name)
    ):
        raise ValueError(
            f"Der Datensatz muss ein direkter Planordner unter {allowed_root} sein."
        )

    manifest_path = root / "manifest.json"
    receipt_path = root / "_export_receipt.json"
    data_yaml = root / "data.yaml"
    classes_path = root / "classes.txt"
    for required in (manifest_path, receipt_path, data_yaml, classes_path):
        if (
            not required.is_file()
            or required.is_symlink()
            or required.resolve().parent != root
        ):
            raise ValueError(f"Pflichtdatei fehlt oder ist unsicher: {required}")

    class_map = load_active_class_map()
    classes = tuple(classes_path.read_text(encoding="utf-8-sig").splitlines())
    if classes != class_map.classes:
        raise ValueError("classes.txt entspricht nicht der aktiven 15er-Klassenkarte.")
    _validate_data_yaml(data_yaml, class_map.classes)

    manifest = _load_json_object(manifest_path)
    plan_id = _validate_manifest_header(manifest, root, class_map)
    files = _collect_dataset_files(root)
    receipt, receipt_image_hashes, _ = _validate_receipt(
        root,
        manifest_path,
        data_yaml,
        classes_path,
        files,
    )
    actual_counts = _validate_manifest_images(
        manifest,
        root,
        files,
        class_map.classes,
        receipt_image_hashes,
    )
    _validate_manifest_instance_counts(manifest, actual_counts)

    train_count = len(files.images["train"])
    validation_count = len(files.images["val"])
    image_count = train_count + validation_count
    if image_count < MINIMUM_IMAGES or train_count < 1 or validation_count < 1:
        raise ValueError(
            f"Der Detect-Gold-Datensatz ist zu klein oder unvollstaendig: "
            f"{train_count} Train, {validation_count} Pruefung."
        )
    _validate_receipt_metadata(
        receipt,
        manifest,
        class_map,
        plan_id,
        train_count,
        validation_count,
    )

    return ValidatedDataset(
        root=root,
        data_yaml=data_yaml,
        manifest=manifest_path,
        plan_id=plan_id,
        image_count=image_count,
        train_count=train_count,
        validation_count=validation_count,
        instance_count=sum(actual_counts.values()),
        instances_per_class=actual_counts,
        manifest_sha256=_sha256_file(manifest_path),
        receipt_sha256=_sha256_file(receipt_path),
        data_yaml_sha256=_sha256_file(data_yaml),
        classes_sha256=_sha256_file(classes_path),
        class_map_version=class_map.version,
        class_map_sha256=class_map.sha256,
        vsa_manifest_hash=class_map.vsa_manifest_hash,
    )


def sidecar_running(timeout: float = 1.5) -> bool:
    try:
        with urllib.request.urlopen(SIDECAR_HEALTH_URL, timeout=timeout) as response:
            return 200 <= response.status < 300
    except urllib.error.HTTPError:
        return True
    except Exception:
        try:
            with socket.create_connection(("127.0.0.1", 8100), timeout=timeout):
                return True
        except OSError:
            return False


def gpu_free_vram_mb() -> int | None:
    executable = shutil.which("nvidia-smi")
    if not executable:
        return None
    try:
        output = subprocess.run(
            [
                executable,
                "--query-gpu=memory.free",
                "--format=csv,noheader,nounits",
            ],
            capture_output=True,
            text=True,
            timeout=5,
            check=True,
        ).stdout.strip().splitlines()
        return int(output[0].strip()) if output else None
    except (OSError, ValueError, subprocess.SubprocessError):
        return None


def ensure_training_resources() -> int:
    if sidecar_running():
        raise RuntimeError(
            "SewerStudio-Sidecar laeuft. Bitte SewerStudio schliessen; "
            "der Detect-Gold-Trainer stoppt es niemals automatisch."
        )
    free_vram = gpu_free_vram_mb()
    if free_vram is None:
        raise RuntimeError("Freier GPU-Speicher konnte nicht sicher gemessen werden.")
    if free_vram < MINIMUM_FREE_VRAM_MB:
        raise RuntimeError(
            f"Zu wenig freier GPU-Speicher: {free_vram} MB statt "
            f"mindestens {MINIMUM_FREE_VRAM_MB} MB."
        )
    return free_vram


def _write_runtime_yaml(path: Path, dataset: ValidatedDataset) -> None:
    lines = [
        f"path: {dataset.root.as_posix()}",
        "train: images/train",
        "val: images/val",
        f"nc: {len(ACTIVE_CLASSES)}",
        "names:",
        *(f"  {index}: {name}" for index, name in enumerate(ACTIVE_CLASSES)),
    ]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def _remove_ultralytics_label_caches(dataset: ValidatedDataset) -> None:
    labels_root = (dataset.root / "labels").resolve()
    for name in ("train.cache", "val.cache"):
        cache_path = (labels_root / name).resolve()
        if cache_path.parent != labels_root:
            raise RuntimeError(f"Unsicherer Cachepfad: {cache_path}")
        unresolved = labels_root / name
        if unresolved.is_symlink():
            raise RuntimeError(f"Cachepfad ist eine Verknuepfung: {unresolved}")
        if unresolved.is_file():
            unresolved.unlink()


def _completed_epochs(results_csv: Path) -> int:
    if not results_csv.is_file():
        return 0
    with results_csv.open("r", encoding="utf-8-sig", newline="") as stream:
        return sum(1 for _ in csv.DictReader(stream))


def _json_safe(value: Any) -> Any:
    if value is None or isinstance(value, (str, int, float, bool)):
        return value
    if isinstance(value, dict):
        return {str(key): _json_safe(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [_json_safe(item) for item in value]
    if hasattr(value, "item"):
        return _json_safe(value.item())
    return str(value)


def train(
    dataset: ValidatedDataset,
    base_weights: Path,
    candidates_root: Path,
    epochs: int,
    patience: int,
    candidate_tag: str | None,
    batch: int = 3,
    workers: int = 0,
    cache: str | bool = False,
    seed: int = 42,
) -> Path:
    if base_weights.is_symlink():
        raise ValueError(f"Basisgewicht ist eine Verknuepfung: {base_weights}")
    weights = base_weights.resolve()
    if not weights.is_file():
        raise ValueError(f"Basisgewicht fehlt oder ist unsicher: {weights}")
    if epochs < 1:
        raise ValueError("epochs muss mindestens 1 sein.")
    if patience < 0:
        raise ValueError("patience darf nicht negativ sein.")
    # Die Stapelgroesse veraendert nur die Trainingsdynamik, nicht die Daten.
    # Eine obere Grenze verhindert, dass ein Tippfehler den VRAM sprengt.
    if batch < 1 or batch > 64:
        raise ValueError("batch muss zwischen 1 und 64 liegen.")
    if workers < 0 or workers > 32:
        raise ValueError("workers muss zwischen 0 und 32 liegen.")
    if cache not in (False, "ram", "disk"):
        raise ValueError("cache erlaubt nur False, 'ram' oder 'disk'.")
    if seed < 0:
        raise ValueError("seed darf nicht negativ sein.")
    normalized_tag = (candidate_tag or "").strip().lower()
    if normalized_tag and not re.fullmatch(r"[a-z0-9][a-z0-9_-]{0,31}", normalized_tag):
        raise ValueError("candidate-tag darf nur a-z, 0-9, _ und - enthalten.")

    free_vram = ensure_training_resources()
    base_weights_sha256 = _sha256_file(weights)
    candidate_name = f"detect_gold_{dataset.plan_id[:12]}"
    if normalized_tag:
        candidate_name += f"_{normalized_tag}"
    resolved_candidates_root = candidates_root.resolve()
    candidate_root = resolved_candidates_root / candidate_name
    if candidate_root.exists():
        raise FileExistsError(
            f"Der Kandidatenordner existiert bereits und wird nicht ueberschrieben: "
            f"{candidate_root}"
        )
    resolved_candidates_root.mkdir(parents=True, exist_ok=True)
    candidate_root.mkdir()
    runtime_yaml = candidate_root / "data.runtime.yaml"
    _write_runtime_yaml(runtime_yaml, dataset)
    _remove_ultralytics_label_caches(dataset)

    from ultralytics import YOLO

    model = YOLO(str(weights))
    try:
        result = model.train(
            data=str(runtime_yaml),
            epochs=epochs,
            imgsz=1280,
            batch=batch,
            workers=workers,
            patience=patience,
            device=0,
            seed=seed,
            deterministic=True,
            cache=cache,
            close_mosaic=5,
            flipud=0.0,
            fliplr=0.0,
            hsv_h=0.01,
            hsv_s=0.3,
            hsv_v=0.3,
            project=str(candidate_root),
            name="run",
            exist_ok=False,
            plots=True,
            verbose=True,
        )
    finally:
        _remove_ultralytics_label_caches(dataset)

    if _sha256_file(weights) != base_weights_sha256:
        raise RuntimeError("Das produktive Basisgewicht wurde waehrend des Trainings veraendert.")
    trained_weights = candidate_root / "run" / "weights" / "best.pt"
    if not trained_weights.is_file() or trained_weights.is_symlink():
        raise RuntimeError(f"Training endete ohne sicheres best.pt: {trained_weights}")
    candidate_weights = candidate_root / "best.pt"
    shutil.copy2(trained_weights, candidate_weights)
    candidate_manifest = {
        "schema_version": "1.0",
        "candidate_status": "not_deployed",
        "candidate_kind": "detect_gold",
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "dataset": {
            "plan_id": dataset.plan_id,
            "manifest_sha256": dataset.manifest_sha256,
            "receipt_sha256": dataset.receipt_sha256,
            "data_yaml_sha256": dataset.data_yaml_sha256,
            "classes_sha256": dataset.classes_sha256,
            "class_map_version": dataset.class_map_version,
            "class_map_sha256": dataset.class_map_sha256,
            "vsa_manifest_hash": dataset.vsa_manifest_hash,
            "images": dataset.image_count,
            "train_images": dataset.train_count,
            "validation_images": dataset.validation_count,
            "instances": dataset.instance_count,
            "instances_per_class": dataset.instances_per_class,
        },
        "training": {
            "epochs_requested": epochs,
            "epochs_completed": _completed_epochs(candidate_root / "run" / "results.csv"),
            "patience": patience,
            "image_size": 1280,
            "batch": batch,
            "workers": workers,
            "cache": cache,
            "seed": seed,
            "deterministic": True,
            "free_vram_mb_at_start": free_vram,
            "results": _json_safe(getattr(result, "results_dict", None)),
        },
        "weights": {
            "base_path": str(weights),
            "base_sha256": base_weights_sha256,
            "candidate_path": str(candidate_weights),
            "candidate_sha256": _sha256_file(candidate_weights),
        },
    }
    (candidate_root / "candidate_manifest.json").write_text(
        json.dumps(candidate_manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return candidate_root


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--dataset", type=Path)
    parser.add_argument(
        "--base-weights",
        type=Path,
        default=REPOSITORY_ROOT / "sidecar" / "models" / "yolo26m" / "yolo26m.pt",
    )
    parser.add_argument("--epochs", type=int, default=40)
    parser.add_argument(
        "--patience",
        type=int,
        default=10,
        help=(
            "Early-Stopping-Geduld in Epochen. 0 bleibt als ausdrueckliche "
            "Ultralytics-Option erlaubt."
        ),
    )
    parser.add_argument(
        "--batch",
        type=int,
        default=3,
        help=(
            "Stapelgroesse. Der Standard 3 stammt vom kleinen BCC-Pilot und "
            "nutzt eine 32-GB-Karte nur zu einem Drittel aus."
        ),
    )
    parser.add_argument(
        "--workers",
        type=int,
        default=0,
        help=(
            "Parallele Ladeprozesse. 0 laesst den Hauptprozess jedes Bild "
            "selbst dekodieren und ist meist der eigentliche Engpass."
        ),
    )
    parser.add_argument(
        "--cache",
        choices=("off", "ram", "disk"),
        default="off",
        help=(
            "Bildzwischenspeicher. 'ram' vermeidet das erneute Dekodieren in "
            "jeder Epoche. Veraendert die Daten nicht."
        ),
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=42,
        help=(
            "Trainings-Seed (deterministic=True). Standard 42 bleibt "
            "unveraendert; andere Werte erzeugen Seed-Serien fuer "
            "Rauschmessungen."
        ),
    )
    parser.add_argument(
        "--candidate-tag",
        help="Optionaler Zusatz fuer einen getrennten Wiederholungskandidaten.",
    )
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="Prueft Datensatz, Sidecar und GPU, startet aber kein Training.",
    )
    arguments = parser.parse_args()

    dataset_root = arguments.knowledge_root / "training" / "datasets"
    dataset_path = arguments.dataset or discover_dataset(dataset_root)
    validated = validate_dataset(dataset_path, dataset_root)
    nonzero_classes = sum(
        1 for count in validated.instances_per_class.values() if count > 0
    )
    print(
        f"Detect-Gold-Datensatz geprueft: {validated.image_count} Bilder "
        f"({validated.train_count} Train, {validated.validation_count} Pruefung), "
        f"{validated.instance_count} BBoxen in {nonzero_classes} Klassen."
    )

    if arguments.check_only:
        free_vram = ensure_training_resources()
        print(
            f"Trainingsstatus: BEREIT | Sidecar: aus | "
            f"freier VRAM: {free_vram} MB"
        )
        return 0

    candidate = train(
        validated,
        arguments.base_weights,
        arguments.knowledge_root / "training" / "models" / "candidates",
        arguments.epochs,
        arguments.patience,
        arguments.candidate_tag,
        batch=arguments.batch,
        workers=arguments.workers,
        cache=False if arguments.cache == "off" else arguments.cache,
        seed=arguments.seed,
    )
    print(f"Detect-Gold-Kandidat fertig (nicht aktiviert): {candidate}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
