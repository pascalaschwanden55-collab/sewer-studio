#!/usr/bin/env python3
"""Erzeugt einen blinden, kandidatenunabhaengigen BCC-Pruefbestand.

Der Builder liest Kundenquellen ausschliesslich. Er verwendet vorhandene
menschliche Inspektionscodes nur fuer eine verdeckte, ausgewogene Vorauswahl.
Die sichtbare Wahrheit entsteht erst im getrennten Blind-Review. Der Builder
erteilt nie eine Modellfreigabe.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import shutil
import stat
import sys
import uuid
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import date, datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Iterable, Sequence


SCRIPT_ROOT = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_ROOT.parents[1]
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))
if str(REPOSITORY_ROOT) not in sys.path:
    sys.path.insert(0, str(REPOSITORY_ROOT))

import gold_stock_audit as negative_source_tools


PILOT_NAME = "BCC_bogen"
QUEUE_SCHEMA = "1.0"
REVIEW_SCHEMA = "1.0"
HOLDOUT_ROLE = "acceptance"
IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png"}
MIN_IMAGE_BYTES = 1024
HOLDING_PATTERN = re.compile(r"\d[\d.]*[-/]\d[\d.]*")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
SELECTION_SALT = "bcc-release-holdout-v1"
DEFAULT_MINIMUM_POSITIVE = 20
DEFAULT_MINIMUM_NEGATIVE = 20
DEFAULT_QUEUE_POSITIVE = 30
DEFAULT_QUEUE_NEGATIVE = 30
LEGACY_UNBOUND_CANDIDATE_MANIFESTS = {
    "bcc_bogen_30ec62ed706f": (
        "82d8d4485194f37eb0c3ba5ffba1a8f764e0d245558d7981968f22efb3622138"
    ),
    "bcc_bogen_30ec62ed706f_full40": (
        "14e0b2338280f8778b11315f20a1c762fb29116693572455d80eb39a31b667c5"
    ),
    "bcc_bogen_af8020b688ac_v3_negatives": (
        "faa57183cb53887b2bfb462b1301bf621b0439c52f8d200a745dfc367043853f"
    ),
    "bcc_bogen_b50b37ab8a4f": (
        "fede3afc981b98d19e72eb438777b23bb5cf4c517541fabb6f5a196fff537623"
    ),
}


@dataclass(frozen=True)
class SourceSpec:
    project_root: Path
    xtf_path: Path


@dataclass(frozen=True)
class SourcePhoto:
    source_id: str
    source_path: Path
    image_sha256: str
    holding_key: str
    physical_holding_key: str
    inspection_date: str
    source_code: str

    @property
    def is_bcc_hint(self) -> bool:
        return self.source_code.upper().startswith("BCC")


@dataclass(frozen=True)
class HoldoutItem:
    item_id: str
    source_path: Path
    image_sha256: str
    holding_key: str
    physical_holding_key: str
    inspection_date: str
    source_id: str
    hidden_hint: str

    @property
    def target_file_name(self) -> str:
        suffix = self.source_path.suffix.casefold()
        return f"img_{self.image_sha256}{suffix}"


@dataclass(frozen=True)
class ContaminationSnapshot:
    image_hashes: frozenset[str]
    holding_aliases: frozenset[str]
    candidates: tuple[dict[str, Any], ...]
    evidence: tuple[dict[str, Any], ...]
    base_model_sha256: str
    candidate_scope_sha256: str
    image_hashes_sha256: str
    holding_aliases_sha256: str


@dataclass(frozen=True)
class DatasetContamination:
    plan_id: str
    manifest_sha256: str
    image_hashes: frozenset[str]
    holding_aliases: frozenset[str]


@dataclass(frozen=True)
class HoldoutPlan:
    knowledge_root: Path
    base_model_path: Path
    created_utc: datetime
    minimum_inspection_date: date
    minimum_positive: int
    minimum_negative: int
    queue_positive: int
    queue_negative: int
    items: tuple[HoldoutItem, ...]
    sources: tuple[dict[str, Any], ...]
    source_specs: tuple[SourceSpec, ...]
    contamination: ContaminationSnapshot
    holdout_id: str
    target_root: Path
    blocked_same_hash: int
    blocked_same_holding: int


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _sha256_lines(values: Iterable[str]) -> str:
    payload = "\n".join(sorted({value.casefold() for value in values}))
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def _canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def _pretty_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=False) + "\n"
    ).encode("utf-8")


def _load_json(path: Path) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"JSON ist nicht sicher lesbar: {path}: {error}") from error


def _require_object(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError(f"{label} muss ein JSON-Objekt sein.")
    return value


def _require_array(value: Any, label: str) -> list[Any]:
    if not isinstance(value, list):
        raise ValueError(f"{label} muss ein JSON-Array sein.")
    return value


def _require_sha256(value: Any, label: str) -> str:
    text = str(value or "").strip().casefold()
    if not SHA256_PATTERN.fullmatch(text):
        raise ValueError(f"{label} ist kein gueltiger SHA-256.")
    return text


def _require_int_at_least(value: Any, label: str, minimum: int) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        raise ValueError(f"{label} muss mindestens {minimum} sein.")
    return value


def _require_review_text(
    value: Any,
    label: str,
    *,
    allow_empty: bool,
    maximum: int,
) -> str:
    if not isinstance(value, str):
        raise ValueError(f"{label} muss Text sein.")
    text = value.strip()
    if not allow_empty and not text:
        raise ValueError(f"{label} fehlt.")
    if len(text) > maximum or any(
        (ord(character) < 32 and character not in "\n\t")
        or ord(character) == 127
        for character in text
    ):
        raise ValueError(f"{label} ist ungueltig.")
    return text


def _require_review_timestamp(value: Any, label: str) -> str:
    text = _require_review_text(
        value,
        label,
        allow_empty=False,
        maximum=64,
    )
    try:
        parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError(f"{label} ist kein gueltiger ISO-Zeitpunkt.") from error
    if parsed.tzinfo is None:
        raise ValueError(f"{label} braucht eine Zeitzone.")
    return text


def _is_reparse_point(path: Path) -> bool:
    try:
        info = os.lstat(path)
    except OSError as error:
        raise ValueError(f"Pfad ist nicht sicher lesbar: {path}: {error}") from error
    if stat.S_ISLNK(info.st_mode):
        return True
    attributes = getattr(info, "st_file_attributes", 0)
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return bool(attributes & reparse_flag)


def _safe_existing_path(path: Path, root: Path, *, expect_file: bool) -> Path:
    absolute_root = Path(os.path.abspath(root))
    absolute_path = Path(os.path.abspath(path))
    resolved_root = Path(os.path.realpath(absolute_root))
    if os.path.normcase(str(resolved_root)) != os.path.normcase(str(absolute_root)):
        raise ValueError(
            f"Wurzel enthaelt eine Verknuepfung in ihrer Ahnenkette: {absolute_root}"
        )
    try:
        common = Path(os.path.commonpath((absolute_root, absolute_path)))
    except ValueError as error:
        raise ValueError(f"Pfad liegt nicht in der erlaubten Wurzel: {path}") from error
    if os.path.normcase(str(common)) != os.path.normcase(str(absolute_root)):
        raise ValueError(f"Pfad liegt nicht in der erlaubten Wurzel: {path}")

    current = absolute_root
    if not current.exists() or _is_reparse_point(current):
        raise ValueError(f"Wurzel fehlt oder ist eine Verknuepfung: {absolute_root}")
    for part in absolute_path.relative_to(absolute_root).parts:
        current = current / part
        if not current.exists():
            raise ValueError(f"Pfad fehlt: {current}")
        if _is_reparse_point(current):
            raise ValueError(f"Verknuepfungen sind nicht erlaubt: {current}")

    if expect_file and not absolute_path.is_file():
        raise ValueError(f"Datei fehlt: {absolute_path}")
    if not expect_file and not absolute_path.is_dir():
        raise ValueError(f"Ordner fehlt: {absolute_path}")
    return absolute_path


def _safe_child_directory(path: Path, root: Path) -> Path:
    return _safe_existing_path(path, root, expect_file=False)


def _validate_image(path: Path) -> None:
    if path.suffix.casefold() not in IMAGE_SUFFIXES:
        raise ValueError(f"Nicht unterstuetztes Bildformat: {path}")
    if path.stat().st_size < MIN_IMAGE_BYTES:
        raise ValueError(f"Bild ist zu klein oder beschaedigt: {path}")
    with path.open("rb") as stream:
        prefix = stream.read(8)
    is_jpeg = prefix.startswith(b"\xff\xd8\xff")
    is_png = prefix == b"\x89PNG\r\n\x1a\n"
    if not is_jpeg and not is_png:
        raise ValueError(f"Bildsignatur ist ungueltig: {path}")


def normalize_holding_key(value: Any) -> str | None:
    """Entspricht EvalContaminationGuard.NormalizeHaltungKey fuer Zahlenpaare."""
    text = str(value or "").strip()
    if not text:
        return None
    match = HOLDING_PATTERN.search(text)
    if not match:
        return text
    parts = re.split(r"[-/]", match.group(0), maxsplit=1)
    if len(parts) != 2:
        return match.group(0)

    def strip_area_prefix(part: str) -> str:
        dot = part.rfind(".")
        return part[dot + 1 :] if 0 <= dot < len(part) - 1 else part

    left = strip_area_prefix(parts[0])
    right = strip_area_prefix(parts[1])
    return f"{left}-{right}" if left and right else None


def _numeric_holding_key(value: Any) -> str | None:
    text = str(value or "").strip()
    if not HOLDING_PATTERN.search(text):
        return None
    normalized = normalize_holding_key(text)
    return normalized if normalized and HOLDING_PATTERN.fullmatch(normalized) else None


def _physical_holding_key(value: Any) -> str:
    normalized = _numeric_holding_key(value)
    if normalized is None:
        raise ValueError(f"Keine belastbare Haltungsidentitaet: {value}")
    left, right = normalized.split("-", maxsplit=1)
    return "|".join(sorted((left.casefold(), right.casefold())))


def _holding_aliases(value: Any) -> set[str]:
    normalized = _numeric_holding_key(value)
    if normalized is None:
        return set()
    left, right = normalized.split("-", maxsplit=1)
    return {
        normalized.casefold(),
        f"{right}-{left}".casefold(),
        _physical_holding_key(normalized),
    }


def _read_training_samples(
    knowledge_root: Path,
) -> tuple[dict[str, dict[str, Any]], list[dict[str, Any]]]:
    path = knowledge_root / "training_samples.json"
    if not path.is_file():
        raise ValueError(f"TrainingSample-Inventar fehlt: {path}")
    path = _safe_existing_path(path, knowledge_root, expect_file=True)
    rows = _require_array(_load_json(path), str(path))
    by_id: dict[str, dict[str, Any]] = {}
    typed: list[dict[str, Any]] = []
    for index, raw in enumerate(rows):
        item = _require_object(raw, f"training_samples.json[{index}]")
        sample_id = str(item.get("SampleId") or "").strip()
        if not sample_id:
            raise ValueError(f"training_samples.json[{index}] besitzt keine SampleId.")
        if sample_id in by_id:
            raise ValueError(f"SampleId ist mehrfach vorhanden: {sample_id}")
        by_id[sample_id] = item
        typed.append(item)
    return by_id, typed


def _scan_negative_pool(
    knowledge_root: Path,
) -> tuple[dict[str, str], set[str], dict[str, Any]]:
    root = knowledge_root / "training" / "negatives" / "bcc_pilot"
    sets_root = knowledge_root / "training" / "negatives" / "sets"
    reviewed_sets: list[Path] = []
    if sets_root.exists() or sets_root.is_symlink():
        safe_sets_root = _safe_existing_path(
            sets_root,
            knowledge_root,
            expect_file=False,
        )
        for entry in sorted(
            safe_sets_root.iterdir(),
            key=lambda item: (item.name.casefold(), item.name),
        ):
            if (
                not entry.is_dir()
                or _is_reparse_point(entry)
                or not re.fullmatch(r"bcc_hn_[0-9a-f]{12}", entry.name)
            ):
                raise ValueError(
                    f"Unerwarteter Eintrag im Negativsatz-Ordner: {entry}"
                )
            reviewed_sets.append(
                _safe_existing_path(
                    entry,
                    safe_sets_root,
                    expect_file=False,
                )
            )

    # Der Trainingspublisher lehnt bewusst jede doppelte Negativquelle ab. Fuer
    # den Kontaminationsschutz muessen jedoch auch historische, spaeter durch
    # eine korrigierte Review desselben Queue-Bestands ersetzte Saetze lesbar
    # bleiben: hier werden alle Saetze einzeln streng validiert und danach nur
    # fuer die Schutzmenge vereinigt. Widerspruechliche Haltungszuordnungen
    # bleiben weiterhin ein harter Fehler.
    legacy_images, _ = negative_source_tools.read_training_negative_sources(
        knowledge_root,
        root,
        (),
        minimum_legacy_bytes=MIN_IMAGE_BYTES,
    )
    negative_images = list(legacy_images)
    set_provenance: list[dict[str, Any]] = []
    no_legacy_root = knowledge_root / "training" / "negatives" / ".no-legacy"
    for reviewed_set in reviewed_sets:
        set_images, provenances = (
            negative_source_tools.read_training_negative_sources(
                knowledge_root,
                no_legacy_root,
                (reviewed_set,),
                minimum_legacy_bytes=MIN_IMAGE_BYTES,
            )
        )
        negative_images.extend(set_images)
        set_provenance.extend(provenances)
    by_hash: dict[str, str] = {}
    aliases: set[str] = set()
    legacy_files = 0
    reviewed_files = 0
    for image in negative_images:
        digest = _require_sha256(image.get("sha256"), "Negativbild-Hash")
        stored_path = Path(str(image.get("path") or ""))
        image_path = (
            stored_path
            if stored_path.is_absolute()
            else knowledge_root / stored_path
        )
        source_type = str(image.get("source_type") or "").strip()
        if source_type == "reviewed_negative_set":
            image_path = _safe_existing_path(
                image_path,
                knowledge_root,
                expect_file=True,
            )
            holding = _numeric_holding_key(image.get("holding_key"))
        elif not source_type:
            safe_legacy_root = _safe_existing_path(
                root,
                knowledge_root,
                expect_file=False,
            )
            image_path = _safe_existing_path(
                image_path,
                safe_legacy_root,
                expect_file=True,
            )
            holding = _numeric_holding_key(image_path.stem)
        else:
            raise ValueError(f"Unbekannte Negativbild-Quelle: {source_type}")
        _validate_image(image_path)
        if _sha256_file(image_path) != digest:
            raise ValueError(f"Negativbild-Hash stimmt nicht: {image_path}")
        if holding is None:
            raise ValueError(
                f"Negativbild besitzt keine belastbare Haltung: {image_path.name}"
            )
        previous = by_hash.get(digest)
        if previous is not None and previous != holding:
            raise ValueError(
                "Dasselbe Negativbild ist verschiedenen Haltungen zugeordnet."
            )
        if previous is not None:
            continue
        by_hash[digest] = holding
        if source_type == "reviewed_negative_set":
            reviewed_files += 1
        else:
            legacy_files += 1
        aliases.update(_holding_aliases(holding))

    evidence = {
        "kind": "negative_pool",
        "files": len(negative_images),
        "legacy_files": legacy_files,
        "reviewed_set_files": reviewed_files,
        "reviewed_sets": [
            {
                "set_id": item["set_id"],
                "manifest_sha256": item["manifest_sha256"],
                "images": item["images"],
            }
            for item in set_provenance
        ],
        "root_name": root.name,
    }
    return by_hash, aliases, evidence


def _resolve_training_sample_holding(
    sample_id: str,
    expected_image_sha256: str,
    samples_by_id: dict[str, dict[str, Any]],
) -> str:
    sample = samples_by_id.get(sample_id)
    if sample is None:
        raise ValueError(
            f"Alte Kandidaten-Altlinie verweist auf unbekanntes Sample: {sample_id}"
        )
    frame_text = str(sample.get("FramePath") or "").strip()
    if not frame_text:
        raise ValueError(f"Altlinie {sample_id} besitzt keinen Bildpfad.")
    frame = Path(frame_text)
    if not frame.is_file() or _sha256_file(frame) != expected_image_sha256:
        raise ValueError(
            f"Altlinie {sample_id} stimmt beim Bildhash nicht mit dem Kandidaten ueberein."
        )
    holding = _numeric_holding_key(sample.get("CaseId"))
    if holding is None:
        raise ValueError(f"Altlinie {sample_id} besitzt keine echte Haltung.")
    return holding


def _dataset_source_ids(image: dict[str, Any]) -> set[str]:
    result: set[str] = set()
    labels = image.get("labels")
    if not isinstance(labels, list):
        return result
    for label in labels:
        if not isinstance(label, dict):
            continue
        sources = label.get("sources")
        if not isinstance(sources, list):
            continue
        for source in sources:
            if not isinstance(source, dict):
                continue
            source_id = str(source.get("source_id") or "").strip()
            source_type = str(source.get("source_type") or "").strip().casefold()
            if source_id and source_type == "training_sample":
                result.add(source_id)
    return result


def _find_all_files_safely(root: Path, allowed_root: Path) -> list[Path]:
    safe_root = _safe_existing_path(root, allowed_root, expect_file=False)
    files: list[Path] = []
    pending = [safe_root]
    while pending:
        directory = pending.pop()
        for entry in sorted(
            directory.iterdir(),
            key=lambda item: (item.name.casefold(), item.name),
        ):
            if _is_reparse_point(entry):
                raise ValueError(f"Verknuepfung ist nicht erlaubt: {entry}")
            if entry.is_dir():
                pending.append(entry)
            elif entry.is_file():
                files.append(entry)
            else:
                raise ValueError(f"Unsicherer Dateisystemeintrag: {entry}")
    return sorted(files, key=lambda item: str(item).casefold())


def _read_receipt_artifacts(
    receipt: dict[str, Any],
    key: str,
    dataset_root: Path,
) -> dict[str, str]:
    rows = _require_array(receipt.get(key), f"Receipt-{key}")
    result: dict[str, str] = {}
    for index, raw in enumerate(rows):
        item = _require_object(raw, f"Receipt-{key}[{index}]")
        relative = str(item.get("path") or "").strip()
        relative_path = Path(relative)
        if (
            not relative
            or "\\" in relative
            or relative_path.is_absolute()
            or ".." in relative_path.parts
            or relative_path.as_posix() != relative
        ):
            raise ValueError(f"Receipt-{key}[{index}] besitzt einen unsicheren Pfad.")
        if relative in result:
            raise ValueError(f"Receipt-{key} enthaelt den Pfad mehrfach: {relative}")
        digest = _require_sha256(
            item.get("sha256"),
            f"Receipt-{key}[{index}].sha256",
        )
        artifact = _safe_existing_path(
            dataset_root / relative_path,
            dataset_root,
            expect_file=True,
        )
        if _sha256_file(artifact) != digest:
            raise ValueError(f"Receipt-Artefakt wurde veraendert: {relative}")
        result[relative] = digest
    return result


def _validate_dataset_configuration(
    dataset_root: Path,
    receipt: dict[str, Any],
) -> None:
    data_yaml = _safe_existing_path(
        dataset_root / "data.yaml",
        dataset_root,
        expect_file=True,
    )
    classes = _safe_existing_path(
        dataset_root / "classes.txt",
        dataset_root,
        expect_file=True,
    )
    if _sha256_file(data_yaml) != _require_sha256(
        receipt.get("data_yaml_sha256"),
        f"Receipt data_yaml_sha256 {dataset_root.name}",
    ):
        raise ValueError(
            f"Dataset-data.yaml stimmt nicht mit dem Receipt: {dataset_root.name}"
        )
    if _sha256_file(classes) != _require_sha256(
        receipt.get("classes_sha256"),
        f"Receipt classes_sha256 {dataset_root.name}",
    ):
        raise ValueError(
            f"Dataset-classes.txt stimmt nicht mit dem Receipt: {dataset_root.name}"
        )

    try:
        yaml_text = data_yaml.read_text(encoding="utf-8-sig")
        class_names = [
            line.strip()
            for line in classes.read_text(encoding="utf-8-sig").splitlines()
            if line.strip()
        ]
    except (OSError, UnicodeError) as error:
        raise ValueError(
            f"Dataset-Konfiguration ist nicht sicher lesbar: {dataset_root.name}"
        ) from error
    top_level: dict[str, str] = {}
    for line in yaml_text.splitlines():
        if not line or line[0].isspace() or ":" not in line:
            continue
        key, value = line.split(":", maxsplit=1)
        key = key.strip()
        if key in top_level:
            raise ValueError(
                f"Dataset-data.yaml enthaelt einen doppelten Schluessel: {key}"
            )
        top_level[key] = value.strip().strip("'\"")
    required_paths = {
        "path": ".",
        "train": "images/train",
        "val": "images/val",
    }
    if any(top_level.get(key) != value for key, value in required_paths.items()):
        raise ValueError(
            "Dataset-data.yaml darf nur lokale kanonische Bildpfade nutzen: "
            f"{dataset_root.name}"
        )
    try:
        declared_classes = int(top_level.get("nc", ""))
    except ValueError as error:
        raise ValueError(
            f"Dataset-data.yaml besitzt keine gueltige Klassenzahl: {dataset_root.name}"
        ) from error
    receipt_class_count = _require_int_at_least(
        receipt.get("class_count"),
        f"Receipt class_count {dataset_root.name}",
        1,
    )
    if (
        declared_classes != receipt_class_count
        or len(class_names) != receipt_class_count
    ):
        raise ValueError(
            f"Dataset-Klassen und Receipt stimmen nicht ueberein: {dataset_root.name}"
        )


def _expected_yolo_label_bytes(
    image: dict[str, Any],
    label: str,
) -> bytes:
    rows = _require_array(image.get("labels"), f"Labels von {label}")
    lines: list[str] = []
    for index, raw in enumerate(rows):
        item = _require_object(raw, f"{label}.labels[{index}]")
        class_id = item.get("class_id")
        if isinstance(class_id, bool) or not isinstance(class_id, int) or class_id < 0:
            raise ValueError(f"{label}.labels[{index}] hat keine gueltige Klasse.")
        box = _require_object(
            item.get("bounding_box"),
            f"{label}.labels[{index}].bounding_box",
        )
        values: list[float] = []
        for field in ("x_center", "y_center", "width", "height"):
            raw_value = box.get(field)
            if (
                isinstance(raw_value, bool)
                or not isinstance(raw_value, (int, float))
                or not math.isfinite(float(raw_value))
                or not 0.0 <= float(raw_value) <= 1.0
            ):
                raise ValueError(
                    f"{label}.labels[{index}].{field} ist ungueltig."
                )
            values.append(float(raw_value))
        lines.append(
            f"{class_id} {values[0]:.6f} {values[1]:.6f} "
            f"{values[2]:.6f} {values[3]:.6f}"
        )
    return (("\n".join(lines) + "\n") if lines else "").encode("utf-8")


def _validate_training_dataset(
    dataset_root: Path,
    samples_by_id: dict[str, dict[str, Any]],
    negative_by_hash: dict[str, str],
) -> DatasetContamination:
    """Prueft einen Trainings-Export samt Receipt, Bildern und Labels vollstaendig."""
    plan_id = _require_sha256(dataset_root.name, "Dataset-Ordnername")
    dataset_manifest_path = _safe_existing_path(
        dataset_root / "manifest.json", dataset_root, expect_file=True
    )
    manifest_sha = _sha256_file(dataset_manifest_path)
    receipt_path = _safe_existing_path(
        dataset_root / "_export_receipt.json", dataset_root, expect_file=True
    )
    receipt = _require_object(_load_json(receipt_path), f"Receipt {plan_id}")
    if (
        str(receipt.get("schema_version") or "") != "2.0"
        or _require_sha256(receipt.get("plan_id"), f"Receipt plan_id {plan_id}")
        != plan_id
        or _require_sha256(
            receipt.get("plan_sha256"), f"Receipt plan_sha256 {plan_id}"
        )
        != plan_id
        or _require_sha256(
            receipt.get("manifest_sha256"), f"Receipt manifest_sha256 {plan_id}"
        )
        != manifest_sha
    ):
        raise ValueError(f"Export-Receipt passt nicht zum Dataset: {plan_id}")
    _validate_dataset_configuration(dataset_root, receipt)

    dataset_manifest = _require_object(
        _load_json(dataset_manifest_path), f"Dataset-Manifest {plan_id}"
    )
    if _require_sha256(dataset_manifest.get("plan_id"), "Dataset plan_id") != plan_id:
        raise ValueError(f"Dataset-Plan-ID stimmt nicht: {plan_id}")
    images = _require_array(
        dataset_manifest.get("images"), f"images von Dataset {plan_id}"
    )
    expected_receipt_images: dict[str, str] = {}
    expected_receipt_labels: dict[str, bytes] = {}
    image_hashes: set[str] = set()
    aliases: set[str] = set()
    for index, raw_image in enumerate(images):
        image = _require_object(raw_image, f"Dataset-Bild {plan_id}[{index}]")
        image_sha = _require_sha256(
            image.get("image_sha256"), f"image_sha256 {plan_id}[{index}]"
        )
        target = str(image.get("target") or "").strip().casefold()
        if target == "train":
            split = "train"
        elif target == "validation":
            split = "val"
        else:
            raise ValueError(
                f"Dataset-Bild {plan_id}[{index}] besitzt keinen sicheren Split."
            )
        target_file_name = str(image.get("target_file_name") or "").strip()
        if (
            not target_file_name
            or Path(target_file_name).name != target_file_name
            or Path(target_file_name).suffix.casefold() not in IMAGE_SUFFIXES
        ):
            raise ValueError(
                f"Dataset-Bild {plan_id}[{index}] besitzt keinen sicheren Dateinamen."
            )
        receipt_image_path = f"images/{split}/{target_file_name}"
        receipt_label_path = f"labels/{split}/{Path(target_file_name).stem}.txt"
        if (
            receipt_image_path in expected_receipt_images
            or receipt_label_path in expected_receipt_labels
        ):
            raise ValueError(f"Dataset {plan_id} enthaelt doppelte Zieldateien.")
        expected_receipt_images[receipt_image_path] = image_sha
        expected_receipt_labels[receipt_label_path] = _expected_yolo_label_bytes(
            image,
            f"Dataset-Bild {plan_id}[{index}]",
        )
        image_hashes.add(image_sha)
        holding = _numeric_holding_key(image.get("holding_key"))
        if image.get("is_negative") is True:
            holding = negative_by_hash.get(image_sha)
            if holding is None:
                raise ValueError(
                    "Negative Dataset-Linie ist keiner gehashten "
                    f"Originalhaltung zuordenbar: {image_sha}"
                )
        else:
            source_ids = _dataset_source_ids(image)
            if not source_ids:
                raise ValueError(
                    "Dataset-Linie ist nicht auf ein "
                    f"TrainingSample zurueckfuehrbar: {image_sha}"
                )
            sample_holdings = {
                _resolve_training_sample_holding(
                    source_id,
                    image_sha,
                    samples_by_id,
                )
                for source_id in source_ids
            }
            physical_sample_holdings = {
                _physical_holding_key(value) for value in sample_holdings
            }
            if len(physical_sample_holdings) != 1:
                raise ValueError(
                    "Mehrere Labels desselben Dataset-Bilds verweisen auf "
                    f"verschiedene physische Haltungen: {image_sha}"
                )
            sample_holding = min(sample_holdings, key=str.casefold)
            if (
                holding is not None
                and _physical_holding_key(holding)
                != _physical_holding_key(sample_holding)
            ):
                raise ValueError(
                    "Dataset-Haltung stimmt nicht mit dem TrainingSample "
                    f"ueberein: {image_sha}"
                )
            holding = sample_holding
        aliases.update(_holding_aliases(holding))

    receipt_images = _read_receipt_artifacts(
        receipt,
        "images",
        dataset_root,
    )
    receipt_labels = _read_receipt_artifacts(
        receipt,
        "labels",
        dataset_root,
    )
    if receipt_images != expected_receipt_images:
        raise ValueError(f"Receipt-Bilder decken Dataset {plan_id} nicht exakt ab.")
    if set(receipt_labels) != set(expected_receipt_labels):
        raise ValueError(f"Receipt-Labels decken Dataset {plan_id} nicht exakt ab.")
    for relative, expected_bytes in expected_receipt_labels.items():
        label_path = _safe_existing_path(
            dataset_root / Path(relative),
            dataset_root,
            expect_file=True,
        )
        if label_path.read_bytes() != expected_bytes:
            raise ValueError(
                f"Dataset-Labelinhalt stimmt nicht mit dem Manifest: {relative}"
            )
    actual_dataset_images = {
        path.relative_to(dataset_root).as_posix()
        for path in _find_all_files_safely(dataset_root / "images", dataset_root)
    }
    actual_dataset_labels = {
        path.relative_to(dataset_root).as_posix()
        for path in _find_all_files_safely(dataset_root / "labels", dataset_root)
    }
    if (
        actual_dataset_images != set(receipt_images)
        or actual_dataset_labels != set(receipt_labels)
    ):
        raise ValueError(
            f"Dataset-Dateimenge stimmt nicht exakt mit dem Receipt: {plan_id}"
        )
    return DatasetContamination(
        plan_id=plan_id,
        manifest_sha256=manifest_sha,
        image_hashes=frozenset(image_hashes),
        holding_aliases=frozenset(aliases),
    )


def _scan_unbound_training_datasets(
    safe_datasets_root: Path,
    seen_dataset_ids: set[str],
    samples_by_id: dict[str, dict[str, Any]],
    negative_by_hash: dict[str, str],
) -> tuple[set[str], set[str], int]:
    image_hashes: set[str] = set()
    aliases: set[str] = set()
    scanned = 0
    for entry in sorted(
        safe_datasets_root.iterdir(),
        key=lambda item: (item.name.casefold(), item.name),
    ):
        if _is_reparse_point(entry):
            raise ValueError(f"Verknuepfung im Dataset-Ordner ist nicht erlaubt: {entry}")
        if entry.name == ".staging":
            if not entry.is_dir():
                raise ValueError("Dataset-.staging ist kein sicherer Ordner.")
            continue
        if not entry.is_dir() or SHA256_PATTERN.fullmatch(entry.name) is None:
            raise ValueError(f"Unbekannter Eintrag im Dataset-Ordner: {entry.name}")
        if entry.name in seen_dataset_ids:
            continue
        dataset_root = _safe_child_directory(entry, safe_datasets_root)
        validated = _validate_training_dataset(
            dataset_root,
            samples_by_id,
            negative_by_hash,
        )
        image_hashes.update(validated.image_hashes)
        aliases.update(validated.holding_aliases)
        seen_dataset_ids.add(validated.plan_id)
        scanned += 1
    return image_hashes, aliases, scanned


def _scan_candidates(
    knowledge_root: Path,
    base_model_path: Path,
    samples_by_id: dict[str, dict[str, Any]],
    negative_by_hash: dict[str, str],
) -> tuple[set[str], set[str], tuple[dict[str, Any], ...], dict[str, Any]]:
    candidates_root = knowledge_root / "training" / "models" / "candidates"
    datasets_root = knowledge_root / "training" / "datasets"
    image_hashes: set[str] = set()
    aliases: set[str] = set()
    scope: list[dict[str, Any]] = []
    bound_configuration_candidates = 0
    base_sha = _sha256_file(base_model_path)

    if datasets_root.exists():
        safe_datasets_root = _safe_existing_path(
            datasets_root,
            knowledge_root,
            expect_file=False,
        )
    else:
        safe_datasets_root = None

    if not candidates_root.is_dir():
        seen_dataset_ids: set[str] = set()
        orphan_datasets = 0
        if safe_datasets_root is not None:
            orphan_hashes, orphan_aliases, orphan_datasets = (
                _scan_unbound_training_datasets(
                    safe_datasets_root,
                    seen_dataset_ids,
                    samples_by_id,
                    negative_by_hash,
                )
            )
            image_hashes.update(orphan_hashes)
            aliases.update(orphan_aliases)
        return image_hashes, aliases, (), {
            "kind": "candidate_datasets",
            "candidates": 0,
            "datasets": len(seen_dataset_ids),
            "unbound_datasets": orphan_datasets,
            "bound_configuration_candidates": 0,
            "legacy_unbound_configuration_candidates": 0,
        }

    safe_candidates_root = _safe_existing_path(
        candidates_root, knowledge_root, expect_file=False
    )
    if safe_datasets_root is None:
        raise ValueError("Kandidaten sind vorhanden, aber der Dataset-Ordner fehlt.")
    seen_dataset_ids: set[str] = set()
    for candidate_root in sorted(
        (path for path in safe_candidates_root.iterdir() if path.is_dir()),
        key=lambda item: item.name.casefold(),
    ):
        candidate_root = _safe_child_directory(candidate_root, safe_candidates_root)
        manifest_path = candidate_root / "candidate_manifest.json"
        weights_path = candidate_root / "best.pt"
        if not manifest_path.is_file():
            if weights_path.exists():
                raise ValueError(
                    f"Kandidatengewicht ohne nachvollziehbares Manifest: {candidate_root}"
                )
            continue
        manifest_path = _safe_existing_path(
            manifest_path, candidate_root, expect_file=True
        )
        candidate_manifest_sha256 = _sha256_file(manifest_path)
        manifest = _require_object(
            _load_json(manifest_path), f"Kandidatenmanifest {candidate_root.name}"
        )
        pilot = str(manifest.get("pilot") or "").strip()
        candidate_kind = str(manifest.get("candidate_kind") or "").strip()
        if pilot != PILOT_NAME and candidate_kind != "detect_gold":
            raise ValueError(
                "Kandidatenmanifest im gemeinsamen Kandidatenordner hat einen "
                "fehlenden oder fremden Pilot und ist auch kein "
                f"candidate_kind=detect_gold: "
                f"{candidate_root.name}"
            )
        weights_path = _safe_existing_path(weights_path, candidate_root, expect_file=True)
        weights = _require_object(
            manifest.get("weights"), f"weights von {candidate_root.name}"
        )
        expected_weight_sha = _require_sha256(
            weights.get("candidate_sha256"),
            f"candidate_sha256 von {candidate_root.name}",
        )
        if _sha256_file(weights_path) != expected_weight_sha:
            raise ValueError(f"Kandidatengewicht wurde veraendert: {candidate_root.name}")
        candidate_base_sha = _require_sha256(
            weights.get("base_sha256"),
            f"base_sha256 von {candidate_root.name}",
        )
        if candidate_base_sha != base_sha:
            raise ValueError(
                f"Basismodell von {candidate_root.name} ist lokal nicht nachvollziehbar."
            )

        dataset_info = _require_object(
            manifest.get("dataset"), f"dataset von {candidate_root.name}"
        )
        plan_id = _require_sha256(
            dataset_info.get("plan_id"), f"plan_id von {candidate_root.name}"
        )
        expected_manifest_sha = _require_sha256(
            dataset_info.get("manifest_sha256"),
            f"manifest_sha256 von {candidate_root.name}",
        )
        dataset_root = datasets_root / plan_id
        dataset_root = _safe_existing_path(
            dataset_root, safe_datasets_root, expect_file=False
        )
        dataset_manifest_path = _safe_existing_path(
            dataset_root / "manifest.json", dataset_root, expect_file=True
        )
        if _sha256_file(dataset_manifest_path) != expected_manifest_sha:
            raise ValueError(f"Trainingsmanifest wurde veraendert: {plan_id}")
        receipt_path = _safe_existing_path(
            dataset_root / "_export_receipt.json", dataset_root, expect_file=True
        )
        bound_dataset_fields = (
            "receipt_sha256",
            "data_yaml_sha256",
            "classes_sha256",
        )
        present_bound_fields = {
            field for field in bound_dataset_fields if dataset_info.get(field) is not None
        }
        if present_bound_fields and present_bound_fields != set(bound_dataset_fields):
            raise ValueError(
                f"Kandidatenmanifest bindet Dataset-Konfiguration unvollstaendig: "
                f"{candidate_root.name}"
            )
        if not present_bound_fields:
            if (
                LEGACY_UNBOUND_CANDIDATE_MANIFESTS.get(candidate_root.name)
                != candidate_manifest_sha256
            ):
                raise ValueError(
                    "Kandidatenmanifest bindet Receipt, data.yaml und "
                    f"classes.txt nicht: {candidate_root.name}"
                )
        else:
            bound_paths = {
                "receipt_sha256": receipt_path,
                "data_yaml_sha256": dataset_root / "data.yaml",
                "classes_sha256": dataset_root / "classes.txt",
            }
            for field, path in bound_paths.items():
                expected = _require_sha256(
                    dataset_info.get(field),
                    f"{field} von {candidate_root.name}",
                )
                if _sha256_file(path) != expected:
                    raise ValueError(
                        "Kandidatenmanifest stimmt nicht mit dem gebundenen "
                        f"Dataset-Artefakt ueberein: {path.name}"
                    )
            bound_configuration_candidates += 1

        if plan_id in seen_dataset_ids:
            scope.append(
                {
                    "candidate_id": candidate_root.name,
                    "candidate_manifest_sha256": candidate_manifest_sha256,
                    "weights_sha256": expected_weight_sha,
                    "dataset_plan_id": plan_id,
                    "dataset_manifest_sha256": expected_manifest_sha,
                }
            )
            continue

        validated_dataset = _validate_training_dataset(
            dataset_root,
            samples_by_id,
            negative_by_hash,
        )
        if validated_dataset.manifest_sha256 != expected_manifest_sha:
            raise ValueError(f"Trainingsmanifest wurde veraendert: {plan_id}")
        image_hashes.update(validated_dataset.image_hashes)
        aliases.update(validated_dataset.holding_aliases)

        scope.append(
            {
                "candidate_id": candidate_root.name,
                "candidate_manifest_sha256": candidate_manifest_sha256,
                "weights_sha256": expected_weight_sha,
                "dataset_plan_id": plan_id,
                "dataset_manifest_sha256": expected_manifest_sha,
            }
        )
        seen_dataset_ids.add(plan_id)

    orphan_hashes, orphan_aliases, orphan_datasets = _scan_unbound_training_datasets(
        safe_datasets_root,
        seen_dataset_ids,
        samples_by_id,
        negative_by_hash,
    )
    image_hashes.update(orphan_hashes)
    aliases.update(orphan_aliases)
    scope.sort(key=lambda item: item["candidate_id"].casefold())
    return image_hashes, aliases, tuple(scope), {
        "kind": "candidate_datasets",
        "candidates": len(scope),
        "datasets": len(seen_dataset_ids),
        "unbound_datasets": orphan_datasets,
        "bound_configuration_candidates": bound_configuration_candidates,
        "legacy_unbound_configuration_candidates": (
            len(scope) - bound_configuration_candidates
        ),
    }


def _scan_training_samples(
    samples: Sequence[dict[str, Any]],
) -> tuple[set[str], set[str], dict[str, Any]]:
    hashes: set[str] = set()
    aliases: set[str] = set()
    readable = 0
    for sample in samples:
        holding = _numeric_holding_key(sample.get("CaseId"))
        if holding is not None:
            aliases.update(_holding_aliases(holding))
        frame_text = str(sample.get("FramePath") or "").strip()
        if not frame_text:
            raise ValueError("TrainingSample besitzt keinen Bildpfad.")
        frame = Path(frame_text)
        if not frame.is_file() or frame.suffix.casefold() not in IMAGE_SUFFIXES:
            raise ValueError(f"TrainingSample-Bild fehlt oder ist ungueltig: {frame}")
        frame = _safe_existing_path(frame, frame.parent, expect_file=True)
        _validate_image(frame)
        hashes.add(_sha256_file(frame))
        readable += 1
    return hashes, aliases, {
        "kind": "training_samples",
        "rows": len(samples),
        "readable_images": readable,
    }


def _read_candidates_array(path: Path) -> list[dict[str, Any]]:
    document = _load_json(path)
    if isinstance(document, dict):
        document = document.get("candidates")
    rows = _require_array(document, str(path))
    return [
        _require_object(row, f"{path.name}[{index}]")
        for index, row in enumerate(rows)
    ]


def _find_named_files_safely(root: Path, file_name: str) -> list[Path]:
    found: list[Path] = []
    pending = [root]
    while pending:
        directory = pending.pop()
        for entry in sorted(
            directory.iterdir(),
            key=lambda item: (item.name.casefold(), item.name),
        ):
            if _is_reparse_point(entry):
                raise ValueError(f"Eval-Verknuepfung ist nicht erlaubt: {entry}")
            if entry.is_dir():
                pending.append(entry)
            elif entry.is_file() and entry.name == file_name:
                found.append(entry)
    return sorted(found, key=lambda item: str(item).casefold())


def _validate_frozen_eval_manifest(
    set_root: Path,
    candidates_path: Path,
) -> tuple[tuple[Path, ...], bool]:
    manifest_path = set_root / "_manifest.json"
    images_root = set_root / "images"
    if not manifest_path.exists():
        image_paths: tuple[Path, ...] = ()
        if images_root.is_dir():
            image_paths = tuple(
                path
                for path in _find_all_files_safely(images_root, set_root)
                if path.suffix.casefold() in IMAGE_SUFFIXES
            )
        return image_paths, False

    manifest_path = _safe_existing_path(
        manifest_path,
        set_root,
        expect_file=True,
    )
    manifest = _require_object(
        _load_json(manifest_path),
        f"Eval-Manifest {set_root}",
    )
    if manifest.get("frozen") is not True:
        raise ValueError(f"Eval-Manifest ist nicht frozen=true: {set_root}")
    if str(manifest.get("hash_algorithm") or "").casefold() != "sha256":
        raise ValueError(f"Eval-Manifest verwendet nicht SHA-256: {set_root}")
    entries = _require_object(manifest.get("hashes"), f"Eval-Hashes {set_root}")
    if _require_int_at_least(
        manifest.get("hashes_count"),
        f"Eval-Hashzahl {set_root}",
        1,
    ) != len(entries):
        raise ValueError(f"Eval-Hashzahl stimmt nicht: {set_root}")

    declared_paths: set[str] = set()
    for relative, raw_entry in entries.items():
        relative_path = Path(str(relative))
        if (
            not isinstance(relative, str)
            or not relative
            or "\\" in relative
            or relative_path.is_absolute()
            or ".." in relative_path.parts
            or relative_path.as_posix() != relative
        ):
            raise ValueError(f"Eval-Manifest enthaelt unsicheren Hashpfad: {set_root}")
        if relative in declared_paths:
            raise ValueError(f"Eval-Manifest enthaelt doppelten Hashpfad: {relative}")
        declared_paths.add(relative)
        entry = _require_object(raw_entry, f"Eval-Hash {relative}")
        expected_digest = _require_sha256(
            entry.get("sha256"),
            f"Eval-Hash {relative}",
        )
        expected_size = _require_int_at_least(
            entry.get("size_bytes"),
            f"Eval-Dateigroesse {relative}",
            0,
        )
        artifact = _safe_existing_path(
            set_root / relative_path,
            set_root,
            expect_file=True,
        )
        if (
            artifact.stat().st_size != expected_size
            or _sha256_file(artifact) != expected_digest
        ):
            raise ValueError(
                f"Eingefrorene Eval-Datei stimmt nicht mit dem Manifest: {artifact}"
            )

    if "_candidates.json" not in declared_paths:
        raise ValueError(f"Eval-Manifest hasht _candidates.json nicht: {set_root}")
    if _sha256_file(candidates_path) != _require_sha256(
        _require_object(
            entries["_candidates.json"],
            "Eval-Kandidatenhash",
        ).get("sha256"),
        "Eval-Kandidatenhash",
    ):
        raise ValueError(f"Eval-Kandidaten stimmen nicht mit dem Manifest: {set_root}")

    image_paths: tuple[Path, ...] = ()
    if images_root.is_dir():
        image_paths = tuple(_find_all_files_safely(images_root, set_root))
    if any(path.suffix.casefold() not in IMAGE_SUFFIXES for path in image_paths):
        raise ValueError(f"Eval-Bildordner enthaelt fremde Dateien: {set_root}")
    actual_image_paths = {
        path.relative_to(set_root).as_posix() for path in image_paths
    }
    declared_image_paths = {
        relative for relative in declared_paths if relative.startswith("images/")
    }
    if actual_image_paths != declared_image_paths:
        raise ValueError(f"Eval-Manifest deckt die Bilder nicht exakt ab: {set_root}")

    labels_root = set_root / "labels"
    actual_label_paths: set[str] = set()
    if labels_root.is_dir():
        actual_label_paths = {
            path.relative_to(set_root).as_posix()
            for path in _find_all_files_safely(labels_root, set_root)
        }
    declared_label_paths = {
        relative for relative in declared_paths if relative.startswith("labels/")
    }
    if actual_label_paths != declared_label_paths:
        raise ValueError(f"Eval-Manifest deckt die Labels nicht exakt ab: {set_root}")
    return image_paths, True


def _scan_eval_sets(
    knowledge_root: Path,
    exclude_eval_root: Path | None,
) -> tuple[set[str], set[str], dict[str, Any]]:
    eval_root = knowledge_root / "eval_set"
    hashes: set[str] = set()
    aliases: set[str] = set()
    sets = 0
    verified_sets = 0
    legacy_sets = 0
    image_files = 0
    if not eval_root.is_dir():
        return hashes, aliases, {
            "kind": "eval_sets",
            "sets": 0,
            "verified_sets": 0,
            "legacy_sets_without_manifest": 0,
            "image_files": 0,
            "images": 0,
        }
    safe_eval_root = _safe_existing_path(
        eval_root,
        knowledge_root,
        expect_file=False,
    )
    excluded = (
        os.path.normcase(os.path.abspath(exclude_eval_root))
        if exclude_eval_root is not None
        else None
    )
    for candidates_path in _find_named_files_safely(
        safe_eval_root,
        "_candidates.json",
    ):
        set_root = _safe_existing_path(
            candidates_path.parent,
            safe_eval_root,
            expect_file=False,
        )
        if excluded is not None and os.path.normcase(os.path.abspath(set_root)) == excluded:
            continue
        candidates_path = _safe_existing_path(
            candidates_path,
            set_root,
            expect_file=True,
        )
        rows = _read_candidates_array(candidates_path)
        for item in rows:
            holding = _numeric_holding_key(
                item.get("haltung_key") or item.get("holding_key")
            )
            if holding is not None:
                aliases.update(_holding_aliases(holding))
        image_paths, verified = _validate_frozen_eval_manifest(
            set_root,
            candidates_path,
        )
        if verified:
            verified_sets += 1
        else:
            legacy_sets += 1
        for image in image_paths:
            _validate_image(image)
            hashes.add(_sha256_file(image))
            image_files += 1
        sets += 1
    return hashes, aliases, {
        "kind": "eval_sets",
        "sets": sets,
        "verified_sets": verified_sets,
        "legacy_sets_without_manifest": legacy_sets,
        "image_files": image_files,
        "images": len(hashes),
    }


def _holding_from_evidence_path(path_text: str) -> str | None:
    if not path_text:
        return None
    try:
        stem = Path(path_text).stem
    except (OSError, ValueError):
        return None
    return _numeric_holding_key(stem)


def _walk_report_objects(value: Any) -> Iterable[dict[str, Any]]:
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from _walk_report_objects(child)
    elif isinstance(value, list):
        for child in value:
            yield from _walk_report_objects(child)


def _read_report_image(
    knowledge_root: Path,
    path_text: str,
    *,
    expected_sha256: str | None = None,
) -> tuple[str, str | None]:
    image_path = Path(path_text)
    if not image_path.is_absolute():
        raise ValueError(f"Collapse-Beleg besitzt keinen absoluten Bildpfad: {path_text}")
    image_path = _safe_existing_path(
        image_path,
        knowledge_root,
        expect_file=True,
    )
    _validate_image(image_path)
    digest = _sha256_file(image_path)
    if expected_sha256 is not None and digest != expected_sha256:
        raise ValueError(f"Collapse-Belegbild stimmt nicht mit seinem Hash: {image_path}")
    return digest, _holding_from_evidence_path(path_text)


def _scan_collapse_reports(
    knowledge_root: Path,
    samples_by_id: dict[str, dict[str, Any]],
) -> tuple[set[str], set[str], dict[str, Any]]:
    reports_root = knowledge_root / "training" / "reports"
    hashes: set[str] = set()
    aliases: set[str] = set()
    reports = 0
    verified_reports = 0
    legacy_reports = 0
    legacy_referenced_images = 0
    legacy_resolved_names = 0
    legacy_unverifiable_names = 0
    if not reports_root.is_dir():
        return hashes, aliases, {
            "kind": "collapse_reports",
            "reports": 0,
            "verified_reports": 0,
            "legacy_reports": 0,
            "legacy_referenced_images": 0,
            "legacy_resolved_names": 0,
            "legacy_unverifiable_names": 0,
            "images": 0,
        }
    safe_reports_root = _safe_existing_path(
        reports_root,
        knowledge_root,
        expect_file=False,
    )
    report_paths = [
        path
        for path in _find_all_files_safely(safe_reports_root, knowledge_root)
        if path.name.startswith("collapse_check_")
        and path.suffix.casefold() == ".json"
    ]
    negative_name_index: dict[str, list[Path]] = {}
    negative_root = knowledge_root / "training" / "negatives" / "bcc_pilot"
    if negative_root.is_dir():
        safe_negative_root = _safe_existing_path(
            negative_root,
            knowledge_root,
            expect_file=False,
        )
        for image_path in _find_all_files_safely(
            safe_negative_root,
            knowledge_root,
        ):
            if image_path.suffix.casefold() in IMAGE_SUFFIXES:
                negative_name_index.setdefault(
                    image_path.name.casefold(),
                    [],
                ).append(image_path)
    for report_path in report_paths:
        report = _require_object(_load_json(report_path), str(report_path))
        if str(report.get("werkzeug") or "") != "model_collapse_check":
            continue
        provenance = report.get("provenienz")
        if not isinstance(provenance, dict):
            seen_legacy_paths: set[str] = set()
            for item in _walk_report_objects(report):
                path_text = str(item.get("bild") or "").strip()
                if not path_text or path_text in seen_legacy_paths:
                    continue
                seen_legacy_paths.add(path_text)
                digest, holding = _read_report_image(
                    knowledge_root,
                    path_text,
                )
                hashes.add(digest)
                if holding is not None:
                    aliases.update(_holding_aliases(holding))
                sample_id = str(item.get("sample_id") or "").strip()
                if sample_id:
                    sample = samples_by_id.get(sample_id)
                    if sample is None:
                        raise ValueError(
                            "Legacy-Collapse-Beleg verweist auf unbekanntes "
                            f"Sample: {sample_id}"
                        )
                    sample_holding = _numeric_holding_key(sample.get("CaseId"))
                    if sample_holding is not None:
                        aliases.update(_holding_aliases(sample_holding))
                legacy_referenced_images += 1
            for item in _walk_report_objects(report):
                if "dateien" not in item:
                    continue
                names = item["dateien"]
                if not isinstance(names, list):
                    raise ValueError(
                        "Legacy-Collapse-Feld 'dateien' muss ein Array sein."
                    )
                for raw_name in names:
                    if not isinstance(raw_name, str) or not raw_name.strip():
                        raise ValueError(
                            "Legacy-Collapse-Feld 'dateien' enthaelt keinen "
                            "gueltigen Dateinamen."
                        )
                    name = raw_name.strip()
                    matches = (
                        negative_name_index.get(name.casefold(), [])
                        if Path(name).name == name
                        else []
                    )
                    if len(matches) != 1:
                        legacy_unverifiable_names += 1
                        continue
                    digest, holding = _read_report_image(
                        knowledge_root,
                        str(matches[0]),
                    )
                    hashes.add(digest)
                    if holding is not None:
                        aliases.update(_holding_aliases(holding))
                    legacy_resolved_names += 1
            reports += 1
            legacy_reports += 1
            continue
        for key in ("pruefbestand", "gold_referenz", "negativ_pool"):
            rows = provenance.get(key)
            if rows is None:
                continue
            for index, raw in enumerate(_require_array(rows, f"{report_path}:{key}")):
                item = _require_object(raw, f"{report_path}:{key}[{index}]")
                digest = _require_sha256(
                    item.get("sha256"), f"{report_path.name}:{key}[{index}]"
                )
                path_text = str(item.get("bild") or "").strip()
                if not path_text:
                    raise ValueError(
                        f"Collapse-Beleg besitzt keinen Bildpfad: {report_path.name}"
                    )
                current_digest, path_holding = _read_report_image(
                    knowledge_root,
                    path_text,
                    expected_sha256=digest,
                )
                if current_digest != digest:
                    raise ValueError(
                        f"Collapse-Beleg wurde veraendert: {report_path.name}"
                    )
                hashes.add(digest)
                holding: str | None = None
                sample_id = str(item.get("sample_id") or "").strip()
                if sample_id:
                    sample = samples_by_id.get(sample_id)
                    if sample is None:
                        raise ValueError(
                            f"Collapse-Beleg verweist auf unbekanntes Sample: {sample_id}"
                        )
                    holding = _numeric_holding_key(sample.get("CaseId"))
                if holding is None:
                    holding = path_holding
                if holding is not None:
                    aliases.update(_holding_aliases(holding))
        reports += 1
        verified_reports += 1
    if legacy_unverifiable_names:
        raise ValueError(
            "Legacy-Collapse-Berichte enthalten nicht eindeutig aufloesbare "
            f"Bildnamen: {legacy_unverifiable_names}"
        )
    return hashes, aliases, {
        "kind": "collapse_reports",
        "reports": reports,
        "verified_reports": verified_reports,
        "legacy_reports": legacy_reports,
        "legacy_referenced_images": legacy_referenced_images,
        "legacy_resolved_names": legacy_resolved_names,
        "legacy_unverifiable_names": legacy_unverifiable_names,
        "images": len(hashes),
    }


def scan_contamination(
    knowledge_root: Path,
    base_model_path: Path,
    *,
    exclude_eval_root: Path | None = None,
) -> ContaminationSnapshot:
    root = Path(os.path.abspath(knowledge_root))
    base_model = Path(os.path.abspath(base_model_path))
    if not root.is_dir():
        raise ValueError(f"KnowledgeRoot fehlt: {root}")
    root = _safe_existing_path(root, root, expect_file=False)
    if not base_model.is_file():
        raise ValueError(f"Basismodell fehlt: {base_model}")
    base_model = _safe_existing_path(
        base_model,
        base_model.parent,
        expect_file=True,
    )
    samples_by_id, samples = _read_training_samples(root)
    negative_by_hash, negative_aliases, negative_evidence = _scan_negative_pool(root)
    candidate_hashes, candidate_aliases, candidates, candidate_evidence = (
        _scan_candidates(
            root,
            base_model,
            samples_by_id,
            negative_by_hash,
        )
    )
    sample_hashes, sample_aliases, sample_evidence = _scan_training_samples(samples)
    eval_hashes, eval_aliases, eval_evidence = _scan_eval_sets(
        root, exclude_eval_root
    )
    report_hashes, report_aliases, report_evidence = _scan_collapse_reports(
        root, samples_by_id
    )

    image_hashes = (
        candidate_hashes
        | set(negative_by_hash)
        | sample_hashes
        | eval_hashes
        | report_hashes
    )
    aliases = (
        candidate_aliases
        | negative_aliases
        | sample_aliases
        | eval_aliases
        | report_aliases
    )
    scope_sha = hashlib.sha256(_canonical_json_bytes(list(candidates))).hexdigest()
    return ContaminationSnapshot(
        image_hashes=frozenset(image_hashes),
        holding_aliases=frozenset(aliases),
        candidates=candidates,
        evidence=(
            candidate_evidence,
            sample_evidence,
            negative_evidence,
            eval_evidence,
            report_evidence,
        ),
        base_model_sha256=_sha256_file(base_model),
        candidate_scope_sha256=scope_sha,
        image_hashes_sha256=_sha256_lines(image_hashes),
        holding_aliases_sha256=_sha256_lines(aliases),
    )


def _local_name(element: ET.Element) -> str:
    return element.tag.rsplit("}", maxsplit=1)[-1]


def _child_text(element: ET.Element, name: str) -> str:
    for child in element:
        if _local_name(child) == name:
            return (child.text or "").strip()
    return ""


def _child_ref(element: ET.Element, name: str) -> str:
    for child in element:
        if _local_name(child) == name:
            return str(child.attrib.get("REF") or "").strip()
    return ""


def _parse_inspection_date(value: str, label: str) -> date:
    try:
        return datetime.strptime(value.strip(), "%Y%m%d").date()
    except ValueError as error:
        raise ValueError(f"Ungueltiges Inspektionsdatum {label}: {value}") from error


def _read_xtf_source(
    spec: SourceSpec,
    minimum_inspection_date: date,
) -> tuple[list[SourcePhoto], dict[str, Any]]:
    project_root = _safe_existing_path(
        Path(spec.project_root), Path(spec.project_root), expect_file=False
    )
    xtf_path = _safe_existing_path(Path(spec.xtf_path), project_root, expect_file=True)
    try:
        tree = ET.parse(xtf_path)
    except (ET.ParseError, OSError) as error:
        raise ValueError(f"XTF ist nicht lesbar: {xtf_path}: {error}") from error

    investigations: dict[str, tuple[str, date]] = {}
    damages: dict[str, tuple[str, str, date]] = {}
    elements = list(tree.getroot().iter())
    for element in elements:
        if _local_name(element).endswith(".KEK.Untersuchung"):
            object_id = str(element.attrib.get("TID") or "").strip()
            holding = _numeric_holding_key(_child_text(element, "Bezeichnung"))
            raw_date = _child_text(element, "Zeitpunkt")
            if object_id and holding and raw_date:
                investigations[object_id] = (
                    holding,
                    _parse_inspection_date(raw_date, object_id),
                )
    for element in elements:
        if _local_name(element).endswith(".KEK.Kanalschaden"):
            object_id = str(element.attrib.get("TID") or "").strip()
            investigation_id = _child_ref(element, "UntersuchungRef")
            code = _child_text(element, "KanalSchadencode").upper()
            investigation = investigations.get(investigation_id)
            if object_id and code and investigation is not None:
                damages[object_id] = (code, investigation[0], investigation[1])

    source_sha = _sha256_file(xtf_path)
    source_id = f"{project_root.name}-{source_sha[:12]}"
    photos: list[SourcePhoto] = []
    unresolved = 0
    too_old = 0
    for element in elements:
        if not _local_name(element).endswith(".KEK.Datei"):
            continue
        if _child_text(element, "Art").casefold() != "foto":
            continue
        damage = damages.get(_child_text(element, "Objekt"))
        if damage is None:
            continue
        code, holding, inspection_date = damage
        if inspection_date < minimum_inspection_date:
            too_old += 1
            continue
        file_name = _child_text(element, "Bezeichnung")
        relative_root = _child_text(element, "Relativpfad")
        if (
            not file_name
            or Path(file_name).name != file_name
            or Path(relative_root).is_absolute()
            or ".." in Path(relative_root).parts
        ):
            unresolved += 1
            continue
        image_path = project_root / relative_root / file_name
        try:
            image_path = _safe_existing_path(
                image_path, project_root, expect_file=True
            )
            _validate_image(image_path)
        except ValueError:
            unresolved += 1
            continue
        photos.append(
            SourcePhoto(
                source_id=source_id,
                source_path=image_path,
                image_sha256=_sha256_file(image_path),
                holding_key=holding,
                physical_holding_key=_physical_holding_key(holding),
                inspection_date=inspection_date.isoformat(),
                source_code=code,
            )
        )

    evidence = {
        "source_id": source_id,
        "xtf_sha256": source_sha,
        "xtf_name": xtf_path.name,
        "project_name": project_root.name,
        "linked_photos": len(photos),
        "unresolved_photos": unresolved,
        "too_old_photos": too_old,
    }
    return photos, evidence


def _stable_group_order(kind: str, group_key: str) -> str:
    return hashlib.sha256(
        f"{SELECTION_SALT}|{kind}|{group_key}".encode("utf-8")
    ).hexdigest()


def _choose_balanced(
    groups: dict[str, list[SourcePhoto]],
    count: int,
    *,
    kind: str,
) -> list[SourcePhoto]:
    by_source: dict[str, list[tuple[str, SourcePhoto]]] = {}
    for physical_key, photos in groups.items():
        preferred = (
            [photo for photo in photos if photo.is_bcc_hint]
            if kind == "positive_hint"
            else photos
        )
        selected = min(
            preferred,
            key=lambda photo: (
                photo.image_sha256,
                str(photo.source_path).casefold(),
            ),
        )
        by_source.setdefault(selected.source_id, []).append((physical_key, selected))
    for entries in by_source.values():
        entries.sort(key=lambda pair: _stable_group_order(kind, pair[0]))

    chosen: list[SourcePhoto] = []
    source_ids = sorted(by_source, key=str.casefold)
    while len(chosen) < count:
        progressed = False
        for source_id in source_ids:
            entries = by_source[source_id]
            if not entries:
                continue
            _, photo = entries.pop(0)
            chosen.append(photo)
            progressed = True
            if len(chosen) == count:
                break
        if not progressed:
            break
    if len(chosen) < count:
        raise ValueError(
            f"Zu wenig frische {kind}-Haltungen: {len(chosen)}/{count}."
        )
    return chosen


def _source_cutoff(base_model_path: Path) -> date:
    modified = datetime.fromtimestamp(
        base_model_path.stat().st_mtime, tz=timezone.utc
    ).date()
    return modified + timedelta(days=1)


def _holdout_semantic_payload(
    *,
    base_model_sha256: str,
    minimum_inspection_date: str,
    candidate_scope_sha256: str,
    minimum_positive: int,
    minimum_negative: int,
    items: Sequence[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "schema_version": QUEUE_SCHEMA,
        "purpose": "bcc_release_holdout",
        "pilot": PILOT_NAME,
        "role": HOLDOUT_ROLE,
        "base_model_sha256": base_model_sha256,
        "minimum_inspection_date": minimum_inspection_date,
        "candidate_scope_sha256": candidate_scope_sha256,
        "minimum_positive": minimum_positive,
        "minimum_negative": minimum_negative,
        "items": list(items),
    }


def build_holdout_plan(
    knowledge_root: Path,
    base_model_path: Path,
    sources: Sequence[SourceSpec],
    *,
    queue_positive: int = DEFAULT_QUEUE_POSITIVE,
    queue_negative: int = DEFAULT_QUEUE_NEGATIVE,
    minimum_positive: int = DEFAULT_MINIMUM_POSITIVE,
    minimum_negative: int = DEFAULT_MINIMUM_NEGATIVE,
    created_utc: datetime | None = None,
) -> HoldoutPlan:
    if not sources:
        raise ValueError("Mindestens eine neue, menschlich inspizierte Quelle fehlt.")
    for label, value in (
        ("queue_positive", queue_positive),
        ("queue_negative", queue_negative),
        ("minimum_positive", minimum_positive),
        ("minimum_negative", minimum_negative),
    ):
        if value < 1:
            raise ValueError(f"{label} muss mindestens 1 sein.")
    if queue_positive < minimum_positive or queue_negative < minimum_negative:
        raise ValueError("Die Pruefliste muss mindestens so gross wie das Mindestziel sein.")

    root = Path(os.path.abspath(knowledge_root))
    base_model = Path(os.path.abspath(base_model_path))
    timestamp = created_utc or datetime.now(timezone.utc)
    if timestamp.tzinfo is None:
        raise ValueError("created_utc braucht eine Zeitzone.")
    cutoff = _source_cutoff(base_model)
    contamination = scan_contamination(root, base_model)

    all_photos: list[SourcePhoto] = []
    source_evidence: list[dict[str, Any]] = []
    normalized_sources = tuple(
        SourceSpec(
            Path(os.path.abspath(spec.project_root)),
            Path(os.path.abspath(spec.xtf_path)),
        )
        for spec in sources
    )
    for spec in normalized_sources:
        photos, evidence = _read_xtf_source(spec, cutoff)
        all_photos.extend(photos)
        source_evidence.append(evidence)
    if not all_photos:
        raise ValueError("Die Quellen enthalten keine nutzbaren verknuepften Fotos.")

    by_hash: dict[str, SourcePhoto] = {}
    for photo in all_photos:
        previous = by_hash.get(photo.image_sha256)
        if previous is not None:
            if (
                previous.physical_holding_key != photo.physical_holding_key
                or previous.is_bcc_hint != photo.is_bcc_hint
            ):
                raise ValueError(
                    "Dasselbe Quellbild besitzt widerspruechliche Herkunftsdaten: "
                    f"{photo.image_sha256}"
                )
            continue
        by_hash[photo.image_sha256] = photo

    clean: list[SourcePhoto] = []
    blocked_hash = 0
    blocked_holding = 0
    for photo in by_hash.values():
        if photo.image_sha256 in contamination.image_hashes:
            blocked_hash += 1
            continue
        if _holding_aliases(photo.holding_key) & contamination.holding_aliases:
            blocked_holding += 1
            continue
        clean.append(photo)

    grouped: dict[str, list[SourcePhoto]] = {}
    for photo in clean:
        grouped.setdefault(photo.physical_holding_key, []).append(photo)
    positive_groups = {
        key: photos
        for key, photos in grouped.items()
        if any(photo.is_bcc_hint for photo in photos)
    }
    negative_groups = {
        key: photos
        for key, photos in grouped.items()
        if not any(photo.is_bcc_hint for photo in photos)
    }
    positives = _choose_balanced(
        positive_groups, queue_positive, kind="positive_hint"
    )
    negatives = _choose_balanced(
        negative_groups, queue_negative, kind="negative_hint"
    )

    items: list[HoldoutItem] = []
    for photo, hidden_hint in (
        *((photo, "positive_hint") for photo in positives),
        *((photo, "negative_hint") for photo in negatives),
    ):
        item_id = "bcc-rh-" + hashlib.sha256(
            f"{photo.image_sha256}|{photo.physical_holding_key}".encode("utf-8")
        ).hexdigest()[:16]
        items.append(
            HoldoutItem(
                item_id=item_id,
                source_path=photo.source_path,
                image_sha256=photo.image_sha256,
                holding_key=photo.holding_key,
                physical_holding_key=photo.physical_holding_key,
                inspection_date=photo.inspection_date,
                source_id=photo.source_id,
                hidden_hint=hidden_hint,
            )
        )
    items.sort(key=lambda item: item.item_id)
    semantic = _holdout_semantic_payload(
        base_model_sha256=contamination.base_model_sha256,
        minimum_inspection_date=cutoff.isoformat(),
        candidate_scope_sha256=contamination.candidate_scope_sha256,
        minimum_positive=minimum_positive,
        minimum_negative=minimum_negative,
        items=[
            {
                "id": item.item_id,
                "image_sha256": item.image_sha256,
                "holding_key": item.holding_key,
                "physical_holding_key": item.physical_holding_key,
            }
            for item in items
        ],
    )
    holdout_id = hashlib.sha256(_canonical_json_bytes(semantic)).hexdigest()
    target = (
        root
        / "eval_set"
        / "subsets"
        / f"bcc_release_holdout_{holdout_id[:12]}"
    )
    return HoldoutPlan(
        knowledge_root=root,
        base_model_path=base_model,
        created_utc=timestamp.astimezone(timezone.utc),
        minimum_inspection_date=cutoff,
        minimum_positive=minimum_positive,
        minimum_negative=minimum_negative,
        queue_positive=queue_positive,
        queue_negative=queue_negative,
        items=tuple(items),
        sources=tuple(source_evidence),
        source_specs=normalized_sources,
        contamination=contamination,
        holdout_id=holdout_id,
        target_root=target,
        blocked_same_hash=blocked_hash,
        blocked_same_holding=blocked_holding,
    )


def _atomic_write(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    try:
        with temporary.open("xb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def _copy_verified(source: Path, destination: Path, expected_sha256: str) -> None:
    digest = hashlib.sha256()
    with source.open("rb") as input_stream, destination.open("xb") as output_stream:
        for block in iter(lambda: input_stream.read(1024 * 1024), b""):
            digest.update(block)
            output_stream.write(block)
        output_stream.flush()
        os.fsync(output_stream.fileno())
    if digest.hexdigest() != expected_sha256:
        raise ValueError(f"Quellbild wurde waehrend der Kopie veraendert: {source}")
    if _sha256_file(source) != expected_sha256:
        raise ValueError(f"Quellbild wurde nach der Kopie veraendert: {source}")
    if _sha256_file(destination) != expected_sha256:
        raise ValueError(f"Holdout-Kopie ist fehlerhaft: {destination}")


def _manifest_hash_entries(staging: Path) -> dict[str, dict[str, Any]]:
    files = [staging / "_candidates.json"]
    files.extend(sorted((staging / "images").iterdir(), key=lambda item: item.name))
    result: dict[str, dict[str, Any]] = {}
    for path in files:
        relative = path.relative_to(staging).as_posix()
        result[relative] = {
            "sha256": _sha256_file(path),
            "size_bytes": path.stat().st_size,
        }
    return result


def _candidate_scope_public(
    candidates: Sequence[dict[str, Any]],
) -> list[dict[str, Any]]:
    return [
        {
            "candidate_id": item["candidate_id"],
            "candidate_manifest_sha256": item["candidate_manifest_sha256"],
            "weights_sha256": item["weights_sha256"],
            "dataset_plan_id": item["dataset_plan_id"],
            "dataset_manifest_sha256": item["dataset_manifest_sha256"],
        }
        for item in candidates
    ]


def _assert_contamination_unchanged(
    plan: HoldoutPlan,
    *,
    exclude_eval_root: Path | None = None,
) -> None:
    current = scan_contamination(
        plan.knowledge_root,
        plan.base_model_path,
        exclude_eval_root=exclude_eval_root,
    )
    frozen = plan.contamination
    if (
        current.base_model_sha256 != frozen.base_model_sha256
        or current.candidate_scope_sha256 != frozen.candidate_scope_sha256
        or current.image_hashes_sha256 != frozen.image_hashes_sha256
        or current.holding_aliases_sha256 != frozen.holding_aliases_sha256
    ):
        raise ValueError(
            "Der Kontaminationsbestand hat sich seit der Planung geaendert."
        )
    for item in plan.items:
        if (
            item.image_sha256 in current.image_hashes
            or _holding_aliases(item.holding_key) & current.holding_aliases
        ):
            raise ValueError(
                "Ein geplantes Holdout-Bild ist inzwischen kontaminiert."
            )


def _assert_sources_unchanged(plan: HoldoutPlan) -> None:
    if len(plan.source_specs) != len(plan.sources):
        raise ValueError("Die geplanten XTF-Quellen sind intern widerspruechlich.")
    for spec, evidence in zip(plan.source_specs, plan.sources, strict=True):
        project_root = _safe_existing_path(
            Path(spec.project_root),
            Path(spec.project_root),
            expect_file=False,
        )
        xtf_path = _safe_existing_path(
            Path(spec.xtf_path),
            project_root,
            expect_file=True,
        )
        expected_sha = _require_sha256(
            evidence.get("xtf_sha256"),
            f"XTF-Hash {xtf_path.name}",
        )
        if (
            _sha256_file(xtf_path) != expected_sha
            or str(evidence.get("xtf_name") or "") != xtf_path.name
            or str(evidence.get("project_name") or "") != project_root.name
        ):
            raise ValueError(
                f"Eine XTF-Quelle wurde seit der Planung veraendert: {xtf_path}"
            )


def publish_holdout(plan: HoldoutPlan) -> Path:
    target = plan.target_root
    expected_subsets_root = plan.knowledge_root / "eval_set" / "subsets"
    expected_target = expected_subsets_root / f"bcc_release_holdout_{plan.holdout_id[:12]}"
    if os.path.normcase(str(target)) != os.path.normcase(str(expected_target)):
        raise ValueError("Holdout-Ziel passt nicht zum geprueften Plan.")
    subsets_root = expected_subsets_root
    if target.exists():
        raise FileExistsError(f"Vorhandener Holdout wird nie ueberschrieben: {target}")
    _assert_sources_unchanged(plan)
    _assert_contamination_unchanged(plan)
    knowledge_root = _safe_existing_path(
        plan.knowledge_root,
        plan.knowledge_root,
        expect_file=False,
    )
    eval_root = knowledge_root / "eval_set"
    if not eval_root.exists():
        eval_root.mkdir()
    eval_root = _safe_existing_path(
        eval_root,
        knowledge_root,
        expect_file=False,
    )
    if not subsets_root.exists():
        subsets_root.mkdir()
    subsets_root = _safe_existing_path(
        subsets_root,
        knowledge_root,
        expect_file=False,
    )
    staging = subsets_root / f".bcc-holdout-staging-{uuid.uuid4().hex}"
    staging.mkdir()
    try:
        images_root = staging / "images"
        images_root.mkdir()
        for item in plan.items:
            source_root = item.source_path.parent
            source = _safe_existing_path(
                item.source_path, source_root, expect_file=True
            )
            _validate_image(source)
            if _sha256_file(source) != item.image_sha256:
                raise ValueError(f"Quellbild wurde nach der Planung veraendert: {source}")
            _copy_verified(
                source,
                images_root / item.target_file_name,
                item.image_sha256,
            )

        candidates = [
            {
                "id": item.item_id,
                "frame_path": item.target_file_name,
                "haltung_key": item.holding_key,
                "kategorie": "bcc_blind_review",
                "status": "pending_review",
                "source_sha256": item.image_sha256,
            }
            for item in plan.items
        ]
        _atomic_write(
            staging / "_candidates.json",
            _pretty_json_bytes(candidates),
        )
        hashes = _manifest_hash_entries(staging)
        source_counts: dict[str, int] = {}
        for item in plan.items:
            source_counts[item.source_id] = source_counts.get(item.source_id, 0) + 1
        manifest = {
            "schema_version": QUEUE_SCHEMA,
            "purpose": "bcc_release_holdout",
            "name": "SewerStudio BCC Release Holdout",
            "holdout_id": plan.holdout_id,
            "pilot": PILOT_NAME,
            "role": HOLDOUT_ROLE,
            "created_utc": plan.created_utc.isoformat().replace("+00:00", "Z"),
            "frozen": True,
            "warning": (
                "DIESES EVAL-SET DARF NICHT FUER TRAINING, FEW-SHOT "
                "ODER KANDIDATENAUSWAHL VOR DEM BLIND-REVIEW VERWENDET WERDEN"
            ),
            "dataset_status": "review_incomplete",
            "release_status": "not_evaluated",
            "evaluation_scope": "binary_bcc_presence_and_false_alarms",
            "localization_measured": False,
            "candidates_count": len(candidates),
            "images_count": len(candidates),
            "labels_count": 0,
            "holdings_count": len(
                {item.physical_holding_key for item in plan.items}
            ),
            "minimum_positive_holdings": plan.minimum_positive,
            "minimum_negative_holdings": plan.minimum_negative,
            "minimum_inspection_date": plan.minimum_inspection_date.isoformat(),
            "base_model_sha256": plan.contamination.base_model_sha256,
            "candidate_scope_sha256": plan.contamination.candidate_scope_sha256,
            "candidate_scope": _candidate_scope_public(
                plan.contamination.candidates
            ),
            "contamination_proof": {
                "known_image_hashes": len(plan.contamination.image_hashes),
                "known_image_hashes_sha256": (
                    plan.contamination.image_hashes_sha256
                ),
                "known_holding_aliases": len(
                    plan.contamination.holding_aliases
                ),
                "known_holding_aliases_sha256": (
                    plan.contamination.holding_aliases_sha256
                ),
                "blocked_same_hash": plan.blocked_same_hash,
                "blocked_same_holding": plan.blocked_same_holding,
                "evidence": list(plan.contamination.evidence),
            },
            "source_provenance": list(plan.sources),
            "item_provenance": [
                {
                    "id": item.item_id,
                    "source_id": item.source_id,
                    "inspection_date": item.inspection_date,
                }
                for item in plan.items
            ],
            "selection": {
                "blind_review": True,
                "one_image_per_normalized_physical_holding": True,
                "source_distribution": dict(
                    sorted(source_counts.items(), key=lambda pair: pair[0].casefold())
                ),
            },
            "limitations": [
                (
                    "Die Vorauswahl nutzt menschliche Inspektionsmetadaten nur "
                    "verdeckt; jedes Bild braucht eine neue Blindentscheidung."
                ),
                (
                    "Nicht protokollierte fruehere manuelle Modelltests sind "
                    "rueckwirkend nicht beweisbar."
                ),
                (
                    "Die lokale Herkunft des urspruenglichen Basismodells ist "
                    "nur durch Artefakt-Hash und zeitlich spaetere Inspektionen "
                    "abgesichert."
                ),
                (
                    "Ohne menschliche Boxen misst der Satz keine Lokalisation "
                    "und kein mAP."
                ),
            ],
            "hash_algorithm": "sha256",
            "hashes_count": len(hashes),
            "hashes_generated_utc": plan.created_utc.isoformat().replace(
                "+00:00", "Z"
            ),
            "hashes": hashes,
        }
        _atomic_write(staging / "_manifest.json", _pretty_json_bytes(manifest))
        if target.exists():
            raise FileExistsError(
                f"Vorhandener Holdout wird nie ueberschrieben: {target}"
            )
        _safe_existing_path(
            subsets_root,
            knowledge_root,
            expect_file=False,
        )
        _safe_existing_path(staging, subsets_root, expect_file=False)
        _assert_sources_unchanged(plan)
        _assert_contamination_unchanged(
            plan,
            exclude_eval_root=staging,
        )
        os.replace(staging, target)
        return target
    finally:
        if staging.exists():
            shutil.rmtree(staging)


def _validate_holdout_files(holdout_root: Path) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    holdout_root = _safe_existing_path(
        Path(holdout_root), Path(holdout_root), expect_file=False
    )
    manifest_path = holdout_root / "_manifest.json"
    candidates_path = holdout_root / "_candidates.json"
    manifest_path = _safe_existing_path(
        manifest_path, holdout_root, expect_file=True
    )
    candidates_path = _safe_existing_path(
        candidates_path, holdout_root, expect_file=True
    )
    images_root = _safe_existing_path(
        holdout_root / "images", holdout_root, expect_file=False
    )
    allowed_root_entries = {"_manifest.json", "_candidates.json", "images"}
    if {path.name for path in holdout_root.iterdir()} != allowed_root_entries:
        raise ValueError("Holdout enthaelt unerwartete Dateien oder Ordner.")
    image_paths = sorted(images_root.iterdir(), key=lambda item: item.name.casefold())
    if any(not path.is_file() or _is_reparse_point(path) for path in image_paths):
        raise ValueError("Holdout-Bildordner enthaelt unsichere Eintraege.")

    manifest = _require_object(_load_json(manifest_path), str(manifest_path))
    candidates = _read_candidates_array(candidates_path)
    if manifest.get("frozen") is not True:
        raise ValueError("Holdout-Manifest ist nicht frozen=true.")
    if str(manifest.get("pilot") or "") != PILOT_NAME:
        raise ValueError("Holdout gehoert nicht zum BCC-Pilot.")
    if str(manifest.get("role") or "") != HOLDOUT_ROLE:
        raise ValueError("Holdout besitzt nicht die Rolle acceptance.")
    if str(manifest.get("hash_algorithm") or "").casefold() != "sha256":
        raise ValueError("Holdout verwendet nicht SHA-256.")
    if int(manifest.get("candidates_count") or -1) != len(candidates):
        raise ValueError("Holdout-Kandidatenzahl stimmt nicht.")
    hashes = _require_object(manifest.get("hashes"), "Holdout hashes")
    if int(manifest.get("hashes_count") or -1) != len(hashes):
        raise ValueError("Holdout-Hashzahl stimmt nicht.")
    expected_hash_paths = {"_candidates.json"} | {
        f"images/{path.name}" for path in image_paths
    }
    if set(hashes) != expected_hash_paths:
        raise ValueError("Holdout-Hashabdeckung ist nicht vollstaendig.")
    for relative, raw_entry in hashes.items():
        if (
            not isinstance(relative, str)
            or Path(relative).is_absolute()
            or ".." in Path(relative).parts
        ):
            raise ValueError("Holdout enthaelt einen unsicheren Hashpfad.")
        entry = _require_object(raw_entry, f"Hash {relative}")
        expected = _require_sha256(entry.get("sha256"), f"Hash {relative}")
        path = holdout_root / Path(relative)
        if not path.is_file() or _sha256_file(path) != expected:
            raise ValueError(f"Holdout-Datei stimmt nicht mit dem Manifest: {relative}")
        if int(entry.get("size_bytes") or -1) != path.stat().st_size:
            raise ValueError(f"Holdout-Dateigroesse stimmt nicht: {relative}")
    image_names = {path.name for path in image_paths}
    candidate_names: set[str] = set()
    candidate_ids: set[str] = set()
    for index, candidate in enumerate(candidates):
        candidate_id = str(candidate.get("id") or "").strip()
        if not candidate_id or candidate_id in candidate_ids:
            raise ValueError(f"Holdout-Kandidat {index} besitzt keine eindeutige ID.")
        candidate_ids.add(candidate_id)
        frame = str(candidate.get("frame_path") or "").strip()
        if not frame or Path(frame).name != frame or frame not in image_names:
            raise ValueError(f"Holdout-Kandidat {index} verweist auf kein Bild.")
        if frame in candidate_names:
            raise ValueError(f"Holdout-Bild ist mehrfach referenziert: {frame}")
        expected_source_sha = _require_sha256(
            candidate.get("source_sha256"),
            f"source_sha256 von Holdout-Kandidat {index}",
        )
        if _sha256_file(images_root / frame) != expected_source_sha:
            raise ValueError(
                f"Holdout-Kandidat {index} stimmt nicht mit seinem Bildhash."
            )
        if _numeric_holding_key(candidate.get("haltung_key")) is None:
            raise ValueError(
                f"Holdout-Kandidat {index} besitzt keine belastbare Haltung."
            )
        candidate_names.add(frame)
    if image_names != candidate_names:
        raise ValueError("Holdout-Bilder und Kandidaten sind nicht deckungsgleich.")
    return manifest, candidates


def evaluate_holdout_status(
    knowledge_root: Path,
    base_model_path: Path,
    holdout_root: Path,
    review_path: Path,
) -> dict[str, Any]:
    knowledge = Path(os.path.abspath(knowledge_root))
    subsets_root = knowledge / "eval_set" / "subsets"
    holdout = Path(os.path.abspath(holdout_root))
    if os.path.normcase(str(holdout.parent)) != os.path.normcase(str(subsets_root)):
        raise ValueError(
            "Holdout liegt nicht direkt in der erwarteten Eval-Subset-Wurzel."
        )
    subsets_root = _safe_existing_path(
        subsets_root,
        knowledge,
        expect_file=False,
    )
    holdout = _safe_existing_path(holdout, subsets_root, expect_file=False)
    manifest, candidates = _validate_holdout_files(holdout)
    current = scan_contamination(
        knowledge,
        Path(base_model_path),
        exclude_eval_root=holdout,
    )
    frozen_base_sha = _require_sha256(
        manifest.get("base_model_sha256"),
        "base_model_sha256",
    )
    if current.base_model_sha256 != frozen_base_sha:
        raise ValueError("Das Basismodell stimmt nicht mehr mit dem Holdout ueberein.")
    frozen_scope = [
        _require_object(item, f"candidate_scope[{index}]")
        for index, item in enumerate(
            _require_array(manifest.get("candidate_scope"), "candidate_scope")
        )
    ]
    frozen_scope_sha = hashlib.sha256(
        _canonical_json_bytes(frozen_scope)
    ).hexdigest()
    if frozen_scope_sha != _require_sha256(
        manifest.get("candidate_scope_sha256"),
        "candidate_scope_sha256",
    ):
        raise ValueError(
            "Der eingefrorene Kandidatenumfang ist intern widerspruechlich."
        )
    frozen_proof = _require_object(
        manifest.get("contamination_proof"),
        "contamination_proof",
    )
    if (
        current.candidate_scope_sha256 != frozen_scope_sha
        or len(current.image_hashes)
        != _require_int_at_least(
            frozen_proof.get("known_image_hashes"),
            "known_image_hashes",
            0,
        )
        or current.image_hashes_sha256
        != _require_sha256(
            frozen_proof.get("known_image_hashes_sha256"),
            "known_image_hashes_sha256",
        )
        or len(current.holding_aliases)
        != _require_int_at_least(
            frozen_proof.get("known_holding_aliases"),
            "known_holding_aliases",
            0,
        )
        or current.holding_aliases_sha256
        != _require_sha256(
            frozen_proof.get("known_holding_aliases_sha256"),
            "known_holding_aliases_sha256",
        )
    ):
        raise ValueError(
            "Der eingefrorene Kontaminationsbestand wurde erweitert, "
            "verkleinert oder veraendert."
        )
    minimum_positive = _require_int_at_least(
        manifest.get("minimum_positive_holdings"),
        "Mindestzahl positiver Haltungen",
        DEFAULT_MINIMUM_POSITIVE,
    )
    minimum_negative = _require_int_at_least(
        manifest.get("minimum_negative_holdings"),
        "Mindestzahl negativer Haltungen",
        DEFAULT_MINIMUM_NEGATIVE,
    )
    minimum_inspection_date = str(
        manifest.get("minimum_inspection_date") or ""
    ).strip()
    try:
        date.fromisoformat(minimum_inspection_date)
    except ValueError as error:
        raise ValueError(
            "minimum_inspection_date ist kein gueltiges ISO-Datum."
        ) from error
    semantic_items = [
        {
            "id": str(candidate.get("id") or ""),
            "image_sha256": _require_sha256(
                candidate.get("source_sha256"),
                "source_sha256 im Holdout",
            ),
            "holding_key": str(candidate.get("haltung_key") or ""),
            "physical_holding_key": _physical_holding_key(
                candidate.get("haltung_key")
            ),
        }
        for candidate in sorted(
            candidates,
            key=lambda item: str(item.get("id") or ""),
        )
    ]
    semantic = _holdout_semantic_payload(
        base_model_sha256=frozen_base_sha,
        minimum_inspection_date=minimum_inspection_date,
        candidate_scope_sha256=frozen_scope_sha,
        minimum_positive=minimum_positive,
        minimum_negative=minimum_negative,
        items=semantic_items,
    )
    expected_holdout_id = hashlib.sha256(
        _canonical_json_bytes(semantic)
    ).hexdigest()
    manifest_holdout_id = _require_sha256(
        manifest.get("holdout_id"),
        "holdout_id",
    )
    if expected_holdout_id != manifest_holdout_id:
        raise ValueError("Die semantische Holdout-ID stimmt nicht.")
    if holdout.name != f"bcc_release_holdout_{manifest_holdout_id[:12]}":
        raise ValueError("Der Holdout-Ordner passt nicht zur Holdout-ID.")
    current_by_id = {
        str(item.get("candidate_id") or ""): item
        for item in current.candidates
    }
    for frozen_candidate in frozen_scope:
        candidate_id = str(frozen_candidate.get("candidate_id") or "")
        if (
            not candidate_id
            or candidate_id not in current_by_id
            or _canonical_json_bytes(frozen_candidate)
            != _canonical_json_bytes(current_by_id[candidate_id])
        ):
            raise ValueError(
                "Ein beim Einfrieren vorhandener Kandidat fehlt oder wurde veraendert."
            )
    candidate_scope_expanded = False
    for candidate in candidates:
        digest = _require_sha256(
            candidate.get("source_sha256"), "source_sha256 im Holdout"
        )
        holding = candidate.get("haltung_key")
        if digest in current.image_hashes:
            raise ValueError("Holdout-Bild ist inzwischen kontaminiert.")
        if _holding_aliases(holding) & current.holding_aliases:
            raise ValueError("Holdout-Haltung ist inzwischen kontaminiert.")

    review_file = Path(os.path.abspath(review_path))
    try:
        review_inside_holdout = (
            os.path.normcase(os.path.commonpath((review_file, holdout)))
            == os.path.normcase(str(holdout))
        )
    except ValueError:
        review_inside_holdout = False
    if review_inside_holdout:
        raise ValueError("Review-Datei muss ausserhalb des Holdouts liegen.")
    review_file = _safe_existing_path(
        review_file,
        review_file.parent,
        expect_file=True,
    )
    review = _require_object(_load_json(review_file), str(review_file))
    expected_review_fields = {
        "schema_version",
        "purpose",
        "holdout_id",
        "manifest_sha256",
        "candidates_sha256",
        "reviewer",
        "updated_at_utc",
        "decisions",
    }
    if set(review) != expected_review_fields:
        raise ValueError("Review enthaelt fehlende oder fremde Felder.")
    if str(review.get("schema_version") or "") != REVIEW_SCHEMA:
        raise ValueError(f"Review braucht Schema {REVIEW_SCHEMA}.")
    if str(review.get("purpose") or "") != "bcc_release_holdout_review":
        raise ValueError("Datei ist kein BCC-Holdout-Review.")
    if str(review.get("holdout_id") or "") != str(manifest.get("holdout_id") or ""):
        raise ValueError("Review gehoert zu einem anderen Holdout.")
    if _require_sha256(review.get("manifest_sha256"), "Review Manifest-SHA") != _sha256_file(
        holdout / "_manifest.json"
    ):
        raise ValueError("Review ist nicht an dieses Manifest gebunden.")
    if _require_sha256(
        review.get("candidates_sha256"), "Review Candidates-SHA"
    ) != _sha256_file(holdout / "_candidates.json"):
        raise ValueError("Review ist nicht an diese Kandidaten gebunden.")
    _require_review_text(
        review.get("reviewer"),
        "Review-Reviewer",
        allow_empty=False,
        maximum=120,
    )
    _require_review_timestamp(
        review.get("updated_at_utc"),
        "Review-Aktualisierungszeitpunkt",
    )
    decisions = _require_object(review.get("decisions"), "Review decisions")
    candidate_ids = {str(item.get("id") or "") for item in candidates}
    if not set(decisions).issubset(candidate_ids):
        raise ValueError("Review enthaelt unbekannte Bild-IDs.")

    positive: list[dict[str, Any]] = []
    negative: list[dict[str, Any]] = []
    excluded = 0
    missing = 0
    by_id = {str(item.get("id") or ""): item for item in candidates}
    for candidate_id in sorted(candidate_ids):
        raw = decisions.get(candidate_id)
        if raw is None:
            missing += 1
            continue
        decision = _require_object(raw, f"Entscheidung {candidate_id}")
        if set(decision) != {"decision", "comment", "reviewed_at_utc"}:
            raise ValueError(
                f"Entscheidung {candidate_id} enthaelt fehlende oder fremde Felder."
            )
        value = str(decision.get("decision") or "").strip().casefold()
        _require_review_text(
            decision.get("comment"),
            f"Kommentar {candidate_id}",
            allow_empty=True,
            maximum=2000,
        )
        _require_review_timestamp(
            decision.get("reviewed_at_utc"),
            f"Review-Zeitpunkt {candidate_id}",
        )
        if value == "positive":
            positive.append(by_id[candidate_id])
        elif value == "negative":
            negative.append(by_id[candidate_id])
        elif value == "exclude":
            excluded += 1
        else:
            raise ValueError(f"Ungueltige Review-Entscheidung: {candidate_id}")

    positive_holdings = {
        _physical_holding_key(item.get("haltung_key")) for item in positive
    }
    negative_holdings = {
        _physical_holding_key(item.get("haltung_key")) for item in negative
    }
    if missing:
        dataset_status = "review_incomplete"
    elif (
        len(positive_holdings) >= minimum_positive
        and len(negative_holdings) >= minimum_negative
    ):
        dataset_status = "ready_for_binary_evaluation"
    else:
        dataset_status = "blocked"
    return {
        "schema_version": "1.0",
        "holdout_id": manifest["holdout_id"],
        "dataset_status": dataset_status,
        "release_status": "not_evaluated",
        "evaluation_scope": "binary_bcc_presence_and_false_alarms",
        "localization_measured": False,
        "total_images": len(candidates),
        "reviewed_images": len(candidates) - missing,
        "missing_reviews": missing,
        "excluded_images": excluded,
        "positive_images": len(positive),
        "positive_holdings": len(positive_holdings),
        "minimum_positive_holdings": minimum_positive,
        "negative_images": len(negative),
        "negative_holdings": len(negative_holdings),
        "minimum_negative_holdings": minimum_negative,
        "candidate_scope_sha256": current.candidate_scope_sha256,
        "candidate_scope_expanded": candidate_scope_expanded,
        "frozen_candidates": len(frozen_scope),
        "current_candidates": len(current.candidates),
    }


def _default_base_model() -> Path:
    return (
        Path(__file__).resolve().parents[2]
        / "sidecar"
        / "models"
        / "yolo26m"
        / "yolo26m.pt"
    )


def _parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Blinden BCC-Release-Holdout vorbereiten oder pruefen."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)
    prepare = subparsers.add_parser(
        "prepare", help="Frische XTF-Fotoquellen pruefen und einfrieren."
    )
    prepare.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    prepare.add_argument("--base-model", type=Path, default=_default_base_model())
    prepare.add_argument(
        "--source",
        nargs=2,
        action="append",
        metavar=("PROJECT_ROOT", "XTF_PATH"),
        required=True,
    )
    prepare.add_argument("--queue-positive", type=int, default=DEFAULT_QUEUE_POSITIVE)
    prepare.add_argument("--queue-negative", type=int, default=DEFAULT_QUEUE_NEGATIVE)
    prepare.add_argument(
        "--minimum-positive", type=int, default=DEFAULT_MINIMUM_POSITIVE
    )
    prepare.add_argument(
        "--minimum-negative", type=int, default=DEFAULT_MINIMUM_NEGATIVE
    )
    prepare.add_argument(
        "--execute",
        action="store_true",
        help="Nach erfolgreicher Pruefung atomar unter eval_set/subsets schreiben.",
    )

    status_parser = subparsers.add_parser(
        "status", help="Blind-Review und aktuelle Kontamination erneut pruefen."
    )
    status_parser.add_argument(
        "--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN")
    )
    status_parser.add_argument("--base-model", type=Path, default=_default_base_model())
    status_parser.add_argument("--holdout", type=Path, required=True)
    status_parser.add_argument("--review", type=Path, required=True)
    return parser.parse_args(argv)


def _print_plan(plan: HoldoutPlan, *, executed: bool) -> None:
    print(f"Holdout-ID: {plan.holdout_id}")
    print(f"Neue Bilder: {len(plan.items)}")
    print(
        "Verdeckte Vorauswahl: "
        f"{plan.queue_positive} BCC-Hinweise / "
        f"{plan.queue_negative} Nicht-BCC-Hinweise"
    )
    print(
        "Bekannte Sperrmenge: "
        f"{len(plan.contamination.image_hashes)} Bild-Hashes / "
        f"{len(plan.contamination.holding_aliases)} Haltungs-Aliase"
    )
    print(
        f"Ausgeschlossen: {plan.blocked_same_hash} gleiche Bilder / "
        f"{plan.blocked_same_holding} gleiche Haltungen"
    )
    print(f"Ziel: {plan.target_root}")
    if executed:
        print("Status: review_incomplete; keine Modellfreigabe.")
    else:
        print("Dry-Run: Es wurde nichts geschrieben.")


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv if argv is not None else sys.argv[1:])
    try:
        if args.command == "prepare":
            if (
                args.minimum_positive < DEFAULT_MINIMUM_POSITIVE
                or args.minimum_negative < DEFAULT_MINIMUM_NEGATIVE
            ):
                raise ValueError(
                    "Ein Release-Holdout braucht mindestens 20 positive und "
                    "20 negative Haltungen."
                )
            source_specs = tuple(
                SourceSpec(Path(project_root), Path(xtf_path))
                for project_root, xtf_path in args.source
            )
            plan = build_holdout_plan(
                args.knowledge_root,
                args.base_model,
                source_specs,
                queue_positive=args.queue_positive,
                queue_negative=args.queue_negative,
                minimum_positive=args.minimum_positive,
                minimum_negative=args.minimum_negative,
            )
            if args.execute:
                publish_holdout(plan)
            _print_plan(plan, executed=args.execute)
            return 0
        status = evaluate_holdout_status(
            args.knowledge_root,
            args.base_model,
            args.holdout,
            args.review,
        )
        print(json.dumps(status, ensure_ascii=False, indent=2))
        return 0 if status["dataset_status"] == "ready_for_binary_evaluation" else 2
    except (OSError, ValueError, FileExistsError) as error:
        print(f"FEHLER: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
