#!/usr/bin/env python3
"""Fail-closed Provenienz fuer den positiven Mehrklassen-Gold-Holdout."""

from __future__ import annotations

import hashlib
import os
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import prepare_detect_gold as prepare_tools
import train_detect_gold as train_tools


RECEIPT_FIELDS = {
    "schema_version",
    "purpose",
    "approval_status",
    "approved_by",
    "approved_utc",
    "source_samples_path",
    "source_samples_sha256",
    "source_gold_audit_path",
    "source_gold_audit_sha256",
    "class_map_path",
    "class_map_version",
    "class_map_sha256",
    "vsa_manifest_sha256",
    "migration_path",
    "migration_version",
    "migration_sha256",
    "personal_gold_approval",
    "previous_registry_sha256",
    "archived_registry_path",
    "new_registry_sha256",
    "previous_receipt_sha256",
    "archived_receipt_path",
    "selected_images",
    "train_images",
    "validation_images",
    "discarded_images",
    "test_images_excluded",
    "negative_images",
    "negative_sets",
    "class_counts",
    "discarded_sample_ids",
    "test_sample_ids_excluded",
    "samples",
}


@dataclass(frozen=True)
class HoldoutBox:
    x_center: float
    y_center: float
    width: float
    height: float


@dataclass(frozen=True)
class HoldoutInstance:
    sample_id: str
    code: str
    class_id: int
    class_name: str
    box: HoldoutBox
    source_type: str


@dataclass(frozen=True)
class HoldoutImage:
    image_id: str
    image_path: Path
    image_sha256: str
    holding_key: str
    physical_holding_key: str
    instances: tuple[HoldoutInstance, ...]


@dataclass(frozen=True)
class ExcludedHolding:
    physical_holding_key: str
    holding_keys: tuple[str, ...]
    test_sample_ids: tuple[str, ...]
    test_image_sha256: tuple[str, ...]
    dataset_image_sha256: tuple[str, ...]
    dataset_sample_ids: tuple[str, ...]
    reasons: tuple[str, ...]


@dataclass(frozen=True)
class DetectGoldHoldoutProvenance:
    knowledge_root: Path
    candidate_dir: Path
    candidate_id: str
    candidate_manifest_path: Path
    candidate_manifest_sha256: str
    weights_path: Path
    weights_sha256: str
    dataset_root: Path
    dataset_plan_id: str
    dataset_manifest_sha256: str
    dataset_receipt_sha256: str
    registry_path: Path
    registry_sha256: str
    detect_all_receipt_path: Path
    detect_all_receipt_sha256: str
    base_audit_path: Path
    base_audit_sha256: str
    base_samples_sha256: str
    current_audit_path: Path
    current_audit_sha256: str
    current_samples_path: Path
    current_samples_sha256: str
    class_map_path: Path
    class_map_sha256: str
    migration_path: Path
    migration_sha256: str
    vsa_manifest_sha256: str
    approved_by: str
    classes: tuple[str, ...]
    all_test_images: tuple[HoldoutImage, ...]
    eligible_images: tuple[HoldoutImage, ...]
    excluded_holdings: tuple[ExcludedHolding, ...]

    @property
    def raw_instance_count(self) -> int:
        return sum(len(image.instances) for image in self.all_test_images)

    @property
    def raw_image_count(self) -> int:
        return len(self.all_test_images)

    @property
    def eligible_instance_count(self) -> int:
        return sum(len(image.instances) for image in self.eligible_images)

    @property
    def eligible_image_count(self) -> int:
        return len(self.eligible_images)

    @property
    def eligible_holding_count(self) -> int:
        return len({image.physical_holding_key for image in self.eligible_images})

    def bindings(self) -> dict[str, Any]:
        return {
            "candidate_id": self.candidate_id,
            "candidate_manifest_sha256": self.candidate_manifest_sha256,
            "weights_sha256": self.weights_sha256,
            "dataset_plan_id": self.dataset_plan_id,
            "dataset_manifest_sha256": self.dataset_manifest_sha256,
            "dataset_receipt_sha256": self.dataset_receipt_sha256,
            "registry_sha256": self.registry_sha256,
            "detect_all_receipt_sha256": self.detect_all_receipt_sha256,
            "base_gold_audit_sha256": self.base_audit_sha256,
            "base_training_samples_sha256": self.base_samples_sha256,
            "current_gold_audit_sha256": self.current_audit_sha256,
            "current_training_samples_sha256": self.current_samples_sha256,
            "class_map_sha256": self.class_map_sha256,
            "migration_sha256": self.migration_sha256,
            "vsa_manifest_sha256": self.vsa_manifest_sha256,
            "base_model_training_inventory_available": False,
        }


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _same_path(left: Path, right: Path) -> bool:
    return os.path.normcase(os.path.abspath(left)) == os.path.normcase(
        os.path.abspath(right)
    )


def _require_direct_plain_directory(path: Path, parent: Path, label: str) -> Path:
    absolute_parent = Path(os.path.abspath(parent))
    absolute = Path(os.path.abspath(path))
    if (
        absolute.parent != absolute_parent
        or not absolute.is_dir()
        or prepare_tools._is_reparse_or_symlink(absolute)
        or prepare_tools._is_reparse_or_symlink(absolute_parent)
    ):
        raise ValueError(f"{label} ist kein direkter, sicherer Unterordner: {absolute}")
    return absolute


def _require_document(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    document, data = prepare_tools._load_json_object_with_bytes(path, label)
    if prepare_tools._is_reparse_or_symlink(path):
        raise ValueError(f"{label} darf keine Verknuepfung sein.")
    return document, data


def _validate_candidate(
    knowledge_root: Path,
    candidate_dir: Path,
) -> tuple[
    dict[str, Any],
    bytes,
    train_tools.ValidatedDataset,
    Path,
    str,
]:
    candidates_root = knowledge_root / "training" / "models" / "candidates"
    candidate = _require_direct_plain_directory(
        candidate_dir,
        candidates_root,
        "Kandidatenordner",
    )
    manifest_path = candidate / "candidate_manifest.json"
    manifest, manifest_bytes = _require_document(
        manifest_path,
        "Detect-Gold-Kandidatenmanifest",
    )
    if set(manifest) != {
        "schema_version",
        "candidate_status",
        "candidate_kind",
        "created_utc",
        "dataset",
        "training",
        "weights",
    }:
        raise ValueError("Kandidatenmanifest hat fremde oder fehlende Felder.")
    if (
        manifest.get("schema_version") != "1.0"
        or manifest.get("candidate_status") != "not_deployed"
        or manifest.get("candidate_kind") != "detect_gold"
    ):
        raise ValueError("Nur ein nicht aktivierter Detect-Gold-Kandidat ist zulaessig.")
    prepare_tools._require_utc(manifest.get("created_utc"), "Kandidatenzeitpunkt")

    dataset_block = manifest.get("dataset")
    if not isinstance(dataset_block, dict):
        raise ValueError("Kandidatenmanifest enthaelt keinen Datensatzbeleg.")
    plan_id = prepare_tools._require_sha256(
        dataset_block.get("plan_id"),
        "Dataset plan_id",
    )
    datasets_root = knowledge_root / "training" / "datasets"
    dataset = train_tools.validate_dataset(datasets_root / plan_id, datasets_root)
    expected_dataset = {
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
    }
    if dataset_block != expected_dataset:
        raise ValueError("Kandidatenmanifest und validierter Datensatz widersprechen sich.")

    weights = manifest.get("weights")
    if not isinstance(weights, dict) or set(weights) != {
        "base_path",
        "base_sha256",
        "candidate_path",
        "candidate_sha256",
    }:
        raise ValueError("Kandidatenmanifest enthaelt keinen gueltigen Gewichtsbeleg.")
    weights_path = candidate / "best.pt"
    if (
        not _same_path(Path(str(weights.get("candidate_path") or "")), weights_path)
        or not weights_path.is_file()
        or prepare_tools._is_reparse_or_symlink(weights_path)
    ):
        raise ValueError("Kandidatengewicht liegt nicht sicher im Kandidatenordner.")
    weights_sha = prepare_tools._require_sha256(
        weights.get("candidate_sha256"),
        "Kandidatengewicht-SHA",
    )
    if _sha256_file(weights_path) != weights_sha:
        raise ValueError("Kandidatengewicht stimmt nicht mit seinem Manifest ueberein.")
    base_models_root = train_tools.REPOSITORY_ROOT / "sidecar" / "models"
    base_path = prepare_tools._require_plain_path_below(
        Path(str(weights.get("base_path") or "")),
        base_models_root,
        "Gebundenes Basisgewicht",
    )
    base_sha = prepare_tools._require_sha256(
        weights.get("base_sha256"),
        "Basisgewicht-SHA",
    )
    if base_path.suffix.casefold() != ".pt" or _sha256_file(base_path) != base_sha:
        raise ValueError("Gebundenes Basisgewicht fehlt oder wurde veraendert.")
    if manifest_path.read_bytes() != manifest_bytes:
        raise ValueError("Kandidatenmanifest wurde parallel veraendert.")
    return manifest, manifest_bytes, dataset, weights_path, weights_sha


def _validate_receipt_chain(
    knowledge_root: Path,
    dataset: train_tools.ValidatedDataset,
    approved_by: str,
) -> tuple[
    dict[str, Any],
    Path,
    str,
    dict[str, Mapping[str, Any]],
    tuple[str, ...],
    Path,
    str,
    str,
    Path,
    str,
    str,
]:
    registry_path = knowledge_root / "training" / "export_registry_v1.json"
    registry_sha = _sha256_file(registry_path)
    dataset_manifest, _ = _require_document(dataset.manifest, "Datasetmanifest")
    if dataset_manifest.get("registry_hash") != registry_sha:
        raise ValueError("Datensatz bindet nicht das aktive DETECT_ALL-Register.")

    receipt_path = (
        knowledge_root / "training" / "pilots" / "DETECT_ALL" / "registry_setup_v1.json"
    )
    receipt, receipt_bytes = _require_document(receipt_path, "DETECT_ALL-Beleg")
    if set(receipt) != RECEIPT_FIELDS:
        raise ValueError("DETECT_ALL-Beleg hat fremde oder fehlende Felder.")
    if (
        receipt.get("schema_version") != "1.0"
        or receipt.get("purpose") != "detect_all_registry_preparation"
        or receipt.get("approval_status") != "approved"
        or str(receipt.get("approved_by") or "").strip().casefold()
        != approved_by.casefold()
    ):
        raise ValueError("DETECT_ALL-Beleg ist nicht persoenlich freigegeben.")
    if (
        prepare_tools._require_sha256(
            receipt.get("new_registry_sha256"),
            "DETECT_ALL Register-SHA",
        )
        != registry_sha
    ):
        raise ValueError("DETECT_ALL-Beleg bindet nicht das aktive Register.")

    class_map, _, class_map_sha, vsa_sha = prepare_tools._read_active_class_map()
    class_map_path = prepare_tools.ACTIVE_CLASS_MAP_PATH.resolve()
    if (
        not _same_path(Path(str(receipt.get("class_map_path") or "")), class_map_path)
        or receipt.get("class_map_version") != prepare_tools.CLASS_MAP_VERSION
        or receipt.get("class_map_sha256") != class_map_sha
        or receipt.get("vsa_manifest_sha256") != vsa_sha
    ):
        raise ValueError("DETECT_ALL-Beleg bindet nicht die aktive Klassenkarte.")

    base_audit_path = Path(str(receipt.get("source_gold_audit_path") or ""))
    base_audit, base_audit_bytes = _require_document(base_audit_path, "Basis-Gold-Audit")
    base_audit_sha = hashlib.sha256(base_audit_bytes).hexdigest()
    if receipt.get("source_gold_audit_sha256") != base_audit_sha:
        raise ValueError("DETECT_ALL-Beleg bindet nicht den Basis-Gold-Audit.")
    base_inputs = base_audit.get("eingaben")
    base_samples_sha = prepare_tools._require_sha256(
        receipt.get("source_samples_sha256"),
        "Basis-Sample-SHA",
    )
    if (
        base_audit.get("schema_version") != "1.1"
        or base_audit.get("bericht") != "gold_stock_audit"
        or not isinstance(base_inputs, dict)
        or base_inputs.get("samples_sha256") != base_samples_sha
        or not _same_path(
            Path(str(receipt.get("source_samples_path") or "")),
            knowledge_root / "training_samples.json",
        )
    ):
        raise ValueError("Basis-Audit und DETECT_ALL-Beleg widersprechen sich.")
    base_samples = base_audit.get("samples")
    if not isinstance(base_samples, list) or any(
        not isinstance(item, dict) for item in base_samples
    ):
        raise ValueError("Basis-Gold-Audit enthaelt keine Samples.")
    base_codes = {
        prepare_tools.gold_audit_tools.normalized_code(item.get("code"))
        for item in base_samples
    }
    teacher_rows, _, migration_sha, _, source_codes = (
        prepare_tools._read_active_migration(
            class_map,
            vsa_sha,
            approved_by,
            base_audit_sha,
            base_samples_sha,
            base_codes,
        )
    )
    migration_path = prepare_tools.ACTIVE_MIGRATION_PATH.resolve()
    if (
        not _same_path(Path(str(receipt.get("migration_path") or "")), migration_path)
        or receipt.get("migration_version") != prepare_tools.MIGRATION_VERSION
        or receipt.get("migration_sha256") != migration_sha
        or receipt.get("class_map_sha256") != dataset.class_map_sha256
        or receipt.get("vsa_manifest_sha256") != dataset.vsa_manifest_hash
    ):
        raise ValueError("Migration, Datensatz und DETECT_ALL-Beleg widersprechen sich.")
    personal = receipt.get("personal_gold_approval")
    if not isinstance(personal, dict) or (
        personal.get("gold_audit_sha256") != base_audit_sha
        or personal.get("training_samples_sha256") != base_samples_sha
        or tuple(personal.get("source_codes") or ()) != source_codes
    ):
        raise ValueError("Persoenliche Goldfreigabe im DETECT_ALL-Beleg stimmt nicht.")

    return (
        receipt,
        receipt_path,
        hashlib.sha256(receipt_bytes).hexdigest(),
        teacher_rows,
        tuple(str(item) for item in receipt.get("test_sample_ids_excluded") or ()),
        base_audit_path,
        base_audit_sha,
        base_samples_sha,
        class_map_path,
        class_map_sha,
        vsa_sha,
    )


def _stable_test_entries(
    base_samples: Sequence[Mapping[str, Any]],
    current_samples: Sequence[Mapping[str, Any]],
    excluded_ids: Sequence[str],
) -> list[Mapping[str, Any]]:
    base_test = {
        str(item.get("sample_id") or ""): item
        for item in base_samples
        if item.get("rolle") == "test"
    }
    current_test = {
        str(item.get("sample_id") or ""): item
        for item in current_samples
        if item.get("rolle") == "test"
    }
    if "" in base_test or len(base_test) != sum(
        item.get("rolle") == "test" for item in base_samples
    ):
        raise ValueError("Basis-Audit besitzt leere oder doppelte Test-Sample-IDs.")
    if set(excluded_ids) != set(base_test):
        raise ValueError("DETECT_ALL-Beleg schliesst nicht exakt alle Basis-Test-IDs aus.")
    fields = ("sample_id", "case_id", "haltung_key", "code", "hauptcode", "image_sha256", "rolle", "gruppe")
    stable: list[Mapping[str, Any]] = []
    for sample_id, base in sorted(base_test.items()):
        current = current_test.get(sample_id)
        if current is None or any(base.get(field) != current.get(field) for field in fields):
            raise ValueError(
                f"Test-Sample {sample_id} fehlt oder wurde seit dem Basis-Audit veraendert."
            )
        stable.append(current)
    return stable


def _dataset_contamination(
    images: Sequence[HoldoutImage],
    dataset_manifest: Mapping[str, Any],
) -> tuple[tuple[HoldoutImage, ...], tuple[ExcludedHolding, ...]]:
    dataset_images = dataset_manifest.get("images")
    if not isinstance(dataset_images, list) or any(
        not isinstance(item, dict) for item in dataset_images
    ):
        raise ValueError("Datasetmanifest enthaelt keine gueltigen Bilder.")
    hashes: set[str] = set()
    holdings: dict[str, set[str]] = {}
    source_ids: dict[str, set[str]] = {}
    for item in dataset_images:
        image_sha = prepare_tools._require_sha256(
            item.get("image_sha256"),
            "Dataset-Bild-SHA",
        )
        holding = str(item.get("holding_key") or "")
        physical = prepare_tools._physical_holding_key(holding)
        hashes.add(image_sha)
        holdings.setdefault(physical, set()).add(image_sha)
        for label in item.get("labels") or ():
            if not isinstance(label, dict):
                raise ValueError("Datasetlabel ist ungueltig.")
            for source in label.get("sources") or ():
                if isinstance(source, dict) and source.get("source_type") == "training_sample":
                    source_ids.setdefault(physical, set()).add(
                        str(source.get("source_id") or "")
                    )

    contaminated: dict[str, dict[str, set[str]]] = {}
    for image in images:
        reasons: set[str] = set()
        dataset_hashes: set[str] = set()
        dataset_samples: set[str] = set()
        if image.image_sha256 in hashes:
            reasons.add("exact_image_sha256_overlap")
            dataset_hashes.add(image.image_sha256)
        if image.physical_holding_key in holdings:
            reasons.add("physical_holding_overlap_including_reverse_direction")
            dataset_hashes.update(holdings[image.physical_holding_key])
            dataset_samples.update(source_ids.get(image.physical_holding_key, set()))
        image_sample_ids = {instance.sample_id for instance in image.instances}
        all_dataset_samples = set().union(*source_ids.values()) if source_ids else set()
        if image_sample_ids & all_dataset_samples:
            reasons.add("training_sample_id_overlap")
            dataset_samples.update(image_sample_ids & all_dataset_samples)
        if reasons:
            entry = contaminated.setdefault(
                image.physical_holding_key,
                {
                    "holding_keys": set(),
                    "test_sample_ids": set(),
                    "test_hashes": set(),
                    "dataset_hashes": set(),
                    "dataset_samples": set(),
                    "reasons": set(),
                },
            )
            entry["holding_keys"].add(image.holding_key)
            entry["test_sample_ids"].update(image_sample_ids)
            entry["test_hashes"].add(image.image_sha256)
            entry["dataset_hashes"].update(dataset_hashes)
            entry["dataset_samples"].update(value for value in dataset_samples if value)
            entry["reasons"].update(reasons)

    excluded_physical = set(contaminated)
    eligible = tuple(
        image for image in images if image.physical_holding_key not in excluded_physical
    )
    excluded = tuple(
        ExcludedHolding(
            physical_holding_key=physical,
            holding_keys=tuple(sorted(values["holding_keys"])),
            test_sample_ids=tuple(sorted(values["test_sample_ids"])),
            test_image_sha256=tuple(sorted(values["test_hashes"])),
            dataset_image_sha256=tuple(sorted(values["dataset_hashes"])),
            dataset_sample_ids=tuple(sorted(values["dataset_samples"])),
            reasons=tuple(sorted(values["reasons"])),
        )
        for physical, values in sorted(contaminated.items())
    )
    return eligible, excluded


def load_and_validate(
    knowledge_root: Path,
    candidate_dir: Path,
    current_audit_path: Path,
) -> DetectGoldHoldoutProvenance:
    root = Path(os.path.abspath(knowledge_root))
    if not root.is_dir() or prepare_tools._is_reparse_or_symlink(root):
        raise ValueError("Knowledge-Root fehlt oder ist unsicher.")
    candidate_manifest, candidate_bytes, dataset, weights_path, weights_sha = (
        _validate_candidate(root, candidate_dir)
    )
    approved_by = "Besitzer"
    (
        receipt,
        receipt_path,
        receipt_sha,
        teacher_rows,
        excluded_test_ids,
        base_audit_path,
        base_audit_sha,
        base_samples_sha,
        class_map_path,
        class_map_sha,
        vsa_sha,
    ) = _validate_receipt_chain(root, dataset, approved_by)

    registry_path = root / "training" / "export_registry_v1.json"
    registry_sha = _sha256_file(registry_path)
    samples_path = root / "training_samples.json"
    raw_samples, samples_bytes = prepare_tools._load_json_array_with_bytes(
        samples_path,
        "Aktuelle training_samples.json",
    )
    samples_sha = hashlib.sha256(samples_bytes).hexdigest()
    current_audit, current_audit_bytes = prepare_tools._validate_audit_header(
        root,
        approved_by,
        current_audit_path,
        registry_sha,
        samples_sha,
        vsa_sha,
    )
    current_audit_sha = hashlib.sha256(current_audit_bytes).hexdigest()
    base_audit, _ = _require_document(base_audit_path, "Basis-Gold-Audit")
    stable_test = _stable_test_entries(
        base_audit.get("samples") or (),
        current_audit.get("samples") or (),
        excluded_test_ids,
    )
    source_by_id = {
        str(item.get("SampleId") or ""): item for item in raw_samples
    }
    if "" in source_by_id or len(source_by_id) != len(raw_samples):
        raise ValueError("training_samples.json besitzt leere oder doppelte SampleIds.")
    classes = train_tools.load_active_class_map().classes
    class_ids = {name: index for index, name in enumerate(classes)}
    gold_root = root / "gold_frames"
    grouped: dict[str, dict[str, Any]] = {}
    for entry in stable_test:
        code = prepare_tools.gold_audit_tools.normalized_code(entry.get("code"))
        decision = teacher_rows.get(code)
        if decision is None or decision.get("approval_status") != "approved":
            raise ValueError(f"Testcode {code} ist nicht freigegeben.")
        action = decision.get("proposed_action")
        if action == "discard":
            continue
        if action != "map":
            raise ValueError(f"Testcode {code} ist nicht eindeutig gemappt.")
        class_name = str(decision.get("proposed_target") or "")
        if class_name not in class_ids:
            raise ValueError(f"Testcode {code} mappt auf eine unbekannte Klasse.")
        sample_id = str(entry.get("sample_id") or "")
        source = source_by_id.get(sample_id)
        if source is None:
            raise ValueError(f"Test-Sample fehlt in training_samples.json: {sample_id}")
        if (
            prepare_tools.gold_audit_tools.normalized_code(source.get("Code")) != code
            or str(source.get("CaseId") or "") != str(entry.get("case_id") or "")
        ):
            raise ValueError(f"Test-Sample {sample_id} weicht vom Audit ab.")
        frame_path, source_type = prepare_tools._verify_personal_source(
            source,
            approved_by,
            sample_id,
        )
        frame_path = prepare_tools._require_plain_path_below(
            frame_path,
            gold_root,
            f"Goldbild {sample_id}",
        )
        image_sha = _sha256_file(frame_path)
        if image_sha != entry.get("image_sha256"):
            raise ValueError(f"Bild-SHA von Test-Sample {sample_id} stimmt nicht.")
        holding = str(entry.get("haltung_key") or "")
        physical = prepare_tools._physical_holding_key(holding)
        values = (
            float(source.get("BboxXCenter")),
            float(source.get("BboxYCenter")),
            float(source.get("BboxWidth")),
            float(source.get("BboxHeight")),
        )
        box = HoldoutBox(*values)
        group = grouped.setdefault(
            image_sha,
            {
                "path": frame_path,
                "holding": holding,
                "physical": physical,
                "instances": [],
            },
        )
        if (
            group["holding"] != holding
            or group["physical"] != physical
            or _sha256_file(Path(group["path"])) != image_sha
        ):
            raise ValueError("Dasselbe Testbild besitzt widerspruechliche Haltungen.")
        group["instances"].append(
            HoldoutInstance(
                sample_id=sample_id,
                code=code,
                class_id=class_ids[class_name],
                class_name=class_name,
                box=box,
                source_type=source_type,
            )
        )

    all_images = tuple(
        HoldoutImage(
            image_id=image_sha,
            image_path=Path(values["path"]),
            image_sha256=image_sha,
            holding_key=str(values["holding"]),
            physical_holding_key=str(values["physical"]),
            instances=tuple(
                sorted(values["instances"], key=lambda item: item.sample_id)
            ),
        )
        for image_sha, values in sorted(grouped.items())
    )
    dataset_manifest, _ = _require_document(dataset.manifest, "Datasetmanifest")
    eligible, excluded = _dataset_contamination(all_images, dataset_manifest)
    if not eligible:
        raise ValueError("Nach Kontaminationsschutz bleibt kein Testbild uebrig.")
    if samples_path.read_bytes() != samples_bytes:
        raise ValueError("training_samples.json wurde parallel veraendert.")
    if Path(current_audit_path).read_bytes() != current_audit_bytes:
        raise ValueError("Aktueller Gold-Audit wurde parallel veraendert.")
    if (candidate_dir / "candidate_manifest.json").read_bytes() != candidate_bytes:
        raise ValueError("Kandidatenmanifest wurde parallel veraendert.")

    return DetectGoldHoldoutProvenance(
        knowledge_root=root,
        candidate_dir=Path(os.path.abspath(candidate_dir)),
        candidate_id=Path(candidate_dir).name,
        candidate_manifest_path=Path(candidate_dir) / "candidate_manifest.json",
        candidate_manifest_sha256=hashlib.sha256(candidate_bytes).hexdigest(),
        weights_path=weights_path,
        weights_sha256=weights_sha,
        dataset_root=dataset.root,
        dataset_plan_id=dataset.plan_id,
        dataset_manifest_sha256=dataset.manifest_sha256,
        dataset_receipt_sha256=dataset.receipt_sha256,
        registry_path=registry_path,
        registry_sha256=registry_sha,
        detect_all_receipt_path=receipt_path,
        detect_all_receipt_sha256=receipt_sha,
        base_audit_path=base_audit_path,
        base_audit_sha256=base_audit_sha,
        base_samples_sha256=base_samples_sha,
        current_audit_path=Path(os.path.abspath(current_audit_path)),
        current_audit_sha256=current_audit_sha,
        current_samples_path=samples_path,
        current_samples_sha256=samples_sha,
        class_map_path=class_map_path,
        class_map_sha256=class_map_sha,
        migration_path=prepare_tools.ACTIVE_MIGRATION_PATH.resolve(),
        migration_sha256=str(receipt.get("migration_sha256") or ""),
        vsa_manifest_sha256=vsa_sha,
        approved_by=approved_by,
        classes=classes,
        all_test_images=all_images,
        eligible_images=eligible,
        excluded_holdings=excluded,
    )


load_detect_gold_holdout_provenance = load_and_validate
