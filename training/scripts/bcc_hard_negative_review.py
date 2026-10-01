#!/usr/bin/env python3
"""Bereitet eine eingefrorene, blinde BCC-Hard-Negative-Pruefliste vor.

Die Vorauswahl darf Modellfehler und XTF-Hinweise verwenden. Im Browser werden
diese Signale nie angezeigt. Ein Trainingsnegativ entsteht erst nach einem
vollstaendigen menschlichen Review, das das gesamte Bild gegen die gebundene
15er-Klassenkarte beurteilt.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, Sequence


SCRIPT_ROOT = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_ROOT.parents[1]
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))
if str(REPOSITORY_ROOT) not in sys.path:
    sys.path.insert(0, str(REPOSITORY_ROOT))

import bcc_release_holdout as holdout_tools
import evaluate_bcc_release_holdout as evaluation_tools


SCHEMA_VERSION = "1.0"
QUEUE_PURPOSE = "bcc_hard_negative_review_queue"
REVIEW_PURPOSE = "bcc_hard_negative_review"
NEGATIVE_SET_PURPOSE = "bcc_reviewed_negative_set"
PILOT_NAME = "BCC_bogen"
QUEUE_ROLE = "training_candidate_review"
NEGATIVE_SET_ROLE = "training_negative_set"
SELECTION_SALT = "bcc-hard-negative-review-v1"
NEGATIVE_SPLIT_SALT = "bcc-hard-negative-split-v1"
REVIEW_DECISION_ORDER = (
    "all_classes_clear",
    "mapped_object_visible",
    "exclude_uncertain",
)
REVIEW_DECISIONS = frozenset(REVIEW_DECISION_ORDER)
DEFAULT_CLASS_MAP = (
    REPOSITORY_ROOT / "training" / "class_maps" / "detect_class_map_v3.json"
)
DEFAULT_VSA_MANIFEST = (
    REPOSITORY_ROOT
    / "src"
    / "AuswertungPro.Next.UI"
    / "Data"
    / "vsa_kek_2020_catalog_manifest.json"
)


@dataclass(frozen=True)
class ClassMapBinding:
    path: Path
    sha256: str
    version: int
    vsa_manifest_hash: str
    ordered_names: tuple[str, ...]


@dataclass(frozen=True)
class QueueItem:
    item_id: str
    source_path: Path
    image_sha256: str
    holding_key: str
    physical_holding_key: str
    source_ref: str
    inspection_date: str
    size_bytes: int
    image_format: str
    predictions: tuple[dict[str, Any], ...]

    @property
    def target_file_name(self) -> str:
        return f"img_{self.image_sha256}{self.source_path.suffix.casefold()}"


@dataclass(frozen=True)
class QueuePlan:
    knowledge_root: Path
    base_model_path: Path
    class_map: ClassMapBinding
    vsa_manifest_path: Path
    created_utc: datetime
    sources: tuple[dict[str, Any], ...]
    source_specs: tuple[holdout_tools.SourceSpec, ...]
    protected_sets: tuple[dict[str, Any], ...]
    protection_snapshot: dict[str, Any]
    model_scope: tuple[dict[str, Any], ...]
    items: tuple[QueueItem, ...]
    semantic_payload: dict[str, Any]
    queue_id: str
    target_root: Path
    scanned_photos: int
    clean_holdings: int
    blocked_same_hash: int
    blocked_same_holding: int


@dataclass(frozen=True)
class NegativeSetItem:
    item_id: str
    source_path: Path
    target_file_name: str
    image_sha256: str
    holding_key: str
    physical_holding_key: str
    split: str
    review_item_id: str
    source_ref: str
    inspection_date: str
    size_bytes: int
    image_format: str


@dataclass(frozen=True)
class NegativeSetPlan:
    knowledge_root: Path
    base_model_path: Path
    queue_root: Path
    review_path: Path
    class_map_path: Path
    vsa_manifest_path: Path
    created_utc: datetime
    queue_id: str
    queue_manifest_sha256: str
    candidates_sha256: str
    review_sha256: str
    items: tuple[NegativeSetItem, ...]
    semantic_payload: dict[str, Any]
    set_id: str
    target_root: Path


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


def _strict_json(path: Path, label: str) -> Any:
    def reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"{label} enthaelt ein doppeltes Feld: {key}")
            result[key] = value
        return result

    try:
        return json.loads(
            path.read_text(encoding="utf-8-sig"),
            object_pairs_hook=reject_duplicates,
        )
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{label} ist nicht sicher lesbar: {path}") from error


def load_class_map(
    class_map_path: Path,
    vsa_manifest_path: Path,
) -> ClassMapBinding:
    class_map = holdout_tools._safe_existing_path(
        Path(class_map_path),
        Path(class_map_path).parent,
        expect_file=True,
    )
    vsa_manifest = holdout_tools._safe_existing_path(
        Path(vsa_manifest_path),
        Path(vsa_manifest_path).parent,
        expect_file=True,
    )
    document = _strict_json(class_map, "Detect-Klassenkarte")
    if not isinstance(document, dict) or set(document) != {
        "version",
        "vsa_manifest_hash",
        "classes",
    }:
        raise ValueError("Die Detect-Klassenkarte hat fehlende oder fremde Felder.")
    version = document.get("version")
    if isinstance(version, bool) or version != 3:
        raise ValueError("Die Hard-Negative-Pruefung braucht class_map v3.")
    expected_vsa = holdout_tools._require_sha256(
        document.get("vsa_manifest_hash"),
        "VSA-Manifest-Hash",
    )
    actual_vsa = holdout_tools._sha256_file(vsa_manifest)
    if actual_vsa != expected_vsa:
        raise ValueError("Die Klassenkarte gehoert nicht zum vorhandenen VSA-Manifest.")
    classes = document.get("classes")
    if not isinstance(classes, dict) or len(classes) != 15:
        raise ValueError("Die Hard-Negative-Pruefung braucht genau 15 Detect-Klassen.")
    ids: dict[int, str] = {}
    for name, raw_id in classes.items():
        if (
            not isinstance(name, str)
            or not name.strip()
            or isinstance(raw_id, bool)
            or not isinstance(raw_id, int)
            or raw_id in ids
        ):
            raise ValueError("Die Detect-Klassenkarte besitzt ungueltige Klassen.")
        ids[raw_id] = name
    if set(ids) != set(range(15)) or ids[14] != PILOT_NAME:
        raise ValueError("Die 15er-Klassenkarte ist nicht die freigegebene BCC-Karte.")
    return ClassMapBinding(
        path=class_map,
        sha256=holdout_tools._sha256_file(class_map),
        version=version,
        vsa_manifest_hash=expected_vsa,
        ordered_names=tuple(ids[index] for index in range(15)),
    )


def _source_ref(xtf_sha256: str) -> str:
    return hashlib.sha256(
        f"{SELECTION_SALT}|source|{xtf_sha256}".encode("utf-8")
    ).hexdigest()


def snapshot_protected_sets(knowledge_root: Path) -> tuple[dict[str, Any], ...]:
    eval_root = knowledge_root / "eval_set"
    if not eval_root.is_dir():
        return ()
    eval_root = holdout_tools._safe_existing_path(
        eval_root,
        knowledge_root,
        expect_file=False,
    )
    result: list[dict[str, Any]] = []
    for candidates_path in holdout_tools._find_named_files_safely(
        eval_root,
        "_candidates.json",
    ):
        set_root = holdout_tools._safe_existing_path(
            candidates_path.parent,
            eval_root,
            expect_file=False,
        )
        candidates = holdout_tools._safe_existing_path(
            candidates_path,
            set_root,
            expect_file=True,
        )
        manifest_path = set_root / "_manifest.json"
        manifest_sha: str | None = None
        manifest_status = "legacy_absent"
        if manifest_path.exists() or manifest_path.is_symlink():
            manifest_path = holdout_tools._safe_existing_path(
                manifest_path,
                set_root,
                expect_file=True,
            )
            document = _strict_json(manifest_path, "Eval-Manifest")
            if not isinstance(document, dict):
                raise ValueError(f"Eval-Manifest ist kein Objekt: {manifest_path}")
            manifest_sha = holdout_tools._sha256_file(manifest_path)
            manifest_status = "present"
        candidates_sha = holdout_tools._sha256_file(candidates)
        set_id = hashlib.sha256(
            _canonical_json_bytes(
                {
                    "manifest_sha256": manifest_sha,
                    "candidates_sha256": candidates_sha,
                }
            )
        ).hexdigest()
        result.append(
            {
                "set_id": set_id,
                "manifest_status": manifest_status,
                "manifest_sha256": manifest_sha,
                "candidates_sha256": candidates_sha,
            }
        )
    result.sort(key=lambda item: item["set_id"])
    return tuple(result)


def _protection_snapshot(
    knowledge_root: Path,
    contamination: holdout_tools.ContaminationSnapshot,
) -> dict[str, Any]:
    samples = holdout_tools._safe_existing_path(
        knowledge_root / "training_samples.json",
        knowledge_root,
        expect_file=True,
    )
    registry = holdout_tools._safe_existing_path(
        knowledge_root / "training" / "export_registry_v1.json",
        knowledge_root,
        expect_file=True,
    )
    return {
        "training_samples_sha256": holdout_tools._sha256_file(samples),
        "export_registry_sha256": holdout_tools._sha256_file(registry),
        "known_image_hashes": len(contamination.image_hashes),
        "known_image_hashes_sha256": contamination.image_hashes_sha256,
        "known_holding_aliases": len(contamination.holding_aliases),
        "known_holding_aliases_sha256": contamination.holding_aliases_sha256,
        "candidate_scope_sha256": contamination.candidate_scope_sha256,
        "base_model_sha256": contamination.base_model_sha256,
    }


def _select_model_scope(
    contamination: holdout_tools.ContaminationSnapshot,
    candidate_ids: Sequence[str],
) -> tuple[dict[str, Any], ...]:
    requested = tuple(candidate_ids)
    if not requested or len(set(requested)) != len(requested):
        raise ValueError("Mindestens eine eindeutige Kandidaten-ID ist erforderlich.")
    by_id = {
        str(item.get("candidate_id") or ""): dict(item)
        for item in contamination.candidates
    }
    missing = [candidate_id for candidate_id in requested if candidate_id not in by_id]
    if missing:
        raise ValueError(
            "Unbekannte oder nicht geschuetzte Kandidaten-ID: " + ", ".join(missing)
        )
    return tuple(by_id[candidate_id] for candidate_id in requested)


def _collect_clean_photos(
    source_specs: Sequence[holdout_tools.SourceSpec],
    cutoff: Any,
    contamination: holdout_tools.ContaminationSnapshot,
) -> tuple[
    dict[str, list[holdout_tools.SourcePhoto]],
    tuple[dict[str, Any], ...],
    int,
    int,
]:
    all_photos: list[holdout_tools.SourcePhoto] = []
    source_evidence: list[dict[str, Any]] = []
    source_refs: dict[str, str] = {}
    for spec in source_specs:
        photos, evidence = holdout_tools._read_xtf_source(spec, cutoff)
        xtf_sha = holdout_tools._require_sha256(
            evidence.get("xtf_sha256"),
            "XTF-Hash",
        )
        safe_ref = _source_ref(xtf_sha)
        source_refs[str(evidence["source_id"])] = safe_ref
        source_evidence.append(
            {
                "source_ref": safe_ref,
                "xtf_sha256": xtf_sha,
                "linked_photos": int(evidence["linked_photos"]),
                "unresolved_photos": int(evidence["unresolved_photos"]),
                "too_old_photos": int(evidence["too_old_photos"]),
            }
        )
        all_photos.extend(photos)

    unique_by_hash: dict[str, holdout_tools.SourcePhoto] = {}
    for photo in all_photos:
        previous = unique_by_hash.setdefault(photo.image_sha256, photo)
        if previous.physical_holding_key != photo.physical_holding_key:
            raise ValueError(
                "Gleiche Bildbytes sind verschiedenen physischen Haltungen zugeordnet."
            )

    grouped: dict[str, list[holdout_tools.SourcePhoto]] = {}
    for photo in unique_by_hash.values():
        grouped.setdefault(photo.physical_holding_key, []).append(photo)

    clean_groups: dict[str, list[holdout_tools.SourcePhoto]] = {}
    blocked_hash = 0
    blocked_holding = 0
    for physical, photos in grouped.items():
        if any(photo.is_bcc_hint for photo in photos):
            continue
        if any(
            photo.image_sha256 in contamination.image_hashes for photo in photos
        ):
            blocked_hash += 1
            continue
        aliases = set().union(
            *(holdout_tools._holding_aliases(photo.holding_key) for photo in photos)
        )
        if aliases & contamination.holding_aliases:
            blocked_holding += 1
            continue
        clean_groups[physical] = sorted(
            photos,
            key=lambda photo: (
                photo.image_sha256,
                photo.source_path.name.casefold(),
            ),
        )

    remapped: dict[str, list[holdout_tools.SourcePhoto]] = {}
    for physical, photos in clean_groups.items():
        remapped[physical] = [
            holdout_tools.SourcePhoto(
                source_id=source_refs[photo.source_id],
                source_path=photo.source_path,
                image_sha256=photo.image_sha256,
                holding_key=photo.holding_key,
                physical_holding_key=photo.physical_holding_key,
                inspection_date=photo.inspection_date,
                source_code=photo.source_code,
            )
            for photo in photos
        ]
    source_evidence.sort(key=lambda item: item["source_ref"])
    return (
        remapped,
        tuple(source_evidence),
        blocked_hash,
        blocked_holding,
    )


def select_hardest_per_holding(
    photos_by_holding: Mapping[str, Sequence[holdout_tools.SourcePhoto]],
    predictions_by_model: Mapping[
        str,
        Mapping[str, evaluation_tools.RawPrediction],
    ],
) -> tuple[QueueItem, ...]:
    selected: list[QueueItem] = []
    for physical_holding in sorted(photos_by_holding):
        ranked: list[tuple[float, str, holdout_tools.SourcePhoto, tuple[dict[str, Any], ...]]] = []
        for photo in photos_by_holding[physical_holding]:
            predictions: list[dict[str, Any]] = []
            confidences: list[float] = []
            triggered = False
            for model_id in sorted(predictions_by_model):
                prediction = predictions_by_model[model_id].get(photo.image_sha256)
                if prediction is None or prediction.technical_error is not None:
                    predictions = []
                    break
                triggered = triggered or prediction.predicted_positive is True
                confidence = prediction.max_confidence
                if confidence is not None:
                    confidences.append(float(confidence))
                predictions.append(
                    {
                        "model_id": model_id,
                        "predicted_bcc": bool(prediction.predicted_positive),
                        "bcc_detection_count": prediction.detection_count,
                        "max_bcc_confidence": confidence,
                    }
                )
            if not predictions or not triggered:
                continue
            score = max(confidences, default=0.0)
            ranked.append(
                (
                    -score,
                    photo.image_sha256,
                    photo,
                    tuple(predictions),
                )
            )
        if not ranked:
            continue
        _, _, photo, predictions = min(ranked)
        item_id = f"bcc-hn-{photo.image_sha256[:16]}"
        selected.append(
            QueueItem(
                item_id=item_id,
                source_path=photo.source_path,
                image_sha256=photo.image_sha256,
                holding_key=photo.holding_key,
                physical_holding_key=photo.physical_holding_key,
                source_ref=photo.source_id,
                inspection_date=photo.inspection_date,
                size_bytes=photo.source_path.stat().st_size,
                image_format=photo.source_path.suffix.casefold().lstrip("."),
                predictions=predictions,
            )
        )
    selected.sort(
        key=lambda item: hashlib.sha256(
            f"{SELECTION_SALT}|order|{item.physical_holding_key}".encode("utf-8")
        ).hexdigest()
    )
    return tuple(selected)


def _semantic_item(item: QueueItem) -> dict[str, Any]:
    return {
        "id": item.item_id,
        "image_sha256": item.image_sha256,
        "holding_key": item.holding_key,
        "physical_holding_key": item.physical_holding_key,
        "source_ref": item.source_ref,
        "inspection_date": item.inspection_date,
        "size_bytes": item.size_bytes,
        "image_format": item.image_format,
        "predictions": list(item.predictions),
    }


def build_queue_plan(
    knowledge_root: Path,
    base_model_path: Path,
    sources: Sequence[holdout_tools.SourceSpec],
    candidate_ids: Sequence[str],
    *,
    class_map_path: Path = DEFAULT_CLASS_MAP,
    vsa_manifest_path: Path = DEFAULT_VSA_MANIFEST,
    device: str | None = None,
    created_utc: datetime | None = None,
) -> QueuePlan:
    if not sources:
        raise ValueError("Mindestens eine menschlich inspizierte XTF-Quelle fehlt.")
    knowledge = Path(os.path.abspath(knowledge_root))
    base_model = Path(os.path.abspath(base_model_path))
    knowledge = holdout_tools._safe_existing_path(
        knowledge,
        knowledge,
        expect_file=False,
    )
    base_model = holdout_tools._safe_existing_path(
        base_model,
        base_model.parent,
        expect_file=True,
    )
    class_map = load_class_map(class_map_path, vsa_manifest_path)
    contamination = holdout_tools.scan_contamination(knowledge, base_model)
    model_scope = _select_model_scope(contamination, candidate_ids)
    protected_sets = snapshot_protected_sets(knowledge)
    protection = _protection_snapshot(knowledge, contamination)
    cutoff = holdout_tools._source_cutoff(base_model)
    photos_by_holding, source_evidence, blocked_hash, blocked_holding = (
        _collect_clean_photos(sources, cutoff, contamination)
    )
    snapshots: list[evaluation_tools.ImageSnapshot] = []
    for photos in photos_by_holding.values():
        for photo in photos:
            payload = photo.source_path.read_bytes()
            if hashlib.sha256(payload).hexdigest() != photo.image_sha256:
                raise ValueError(f"Quellbild wurde waehrend der Auswahl veraendert.")
            snapshots.append(
                evaluation_tools.ImageSnapshot(
                    item_id=photo.image_sha256,
                    image_sha256=photo.image_sha256,
                    image_bytes=payload,
                )
            )
    snapshots.sort(key=lambda item: item.item_id)
    bindings = evaluation_tools.load_candidate_bindings(knowledge, model_scope)
    _, yolo_wrapper = evaluation_tools._load_runtime_modules()
    evaluation_tools._assert_sidecar_offline(yolo_wrapper)
    predictions_by_model: dict[
        str,
        dict[str, evaluation_tools.RawPrediction],
    ] = {}
    for binding in bindings:
        predictions, _ = evaluation_tools.run_candidate_inference(
            binding,
            snapshots,
            device=device,
        )
        errors = [
            prediction
            for prediction in predictions
            if prediction.technical_error is not None
        ]
        if errors:
            raise ValueError(
                f"{binding.candidate_id}: {len(errors)} technische Inferenzfehler."
            )
        predictions_by_model[binding.candidate_id] = {
            prediction.item_id: prediction for prediction in predictions
        }
    items = select_hardest_per_holding(
        photos_by_holding,
        predictions_by_model,
    )
    if not items:
        raise ValueError("Kein frischer, vom Modell ausgeloester Kandidat wurde gefunden.")
    if len({item.physical_holding_key for item in items}) != len(items):
        raise ValueError("Die Pruefliste enthaelt mehr als ein Bild je physischer Haltung.")

    semantic = {
        "schema_version": SCHEMA_VERSION,
        "purpose": QUEUE_PURPOSE,
        "pilot": PILOT_NAME,
        "role": QUEUE_ROLE,
        "class_map_version": class_map.version,
        "class_map_sha256": class_map.sha256,
        "vsa_manifest_hash": class_map.vsa_manifest_hash,
        "class_names": list(class_map.ordered_names),
        "protected_sets": list(protected_sets),
        "protection_snapshot": protection,
        "model_scope": list(model_scope),
        "selection_rule": {
            "one_image_per_physical_holding": True,
            "requires_current_model_bcc_trigger": True,
            "review_target": (
                "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse"
            ),
        },
        "sources": list(source_evidence),
        "items": [_semantic_item(item) for item in items],
    }
    queue_id = hashlib.sha256(_canonical_json_bytes(semantic)).hexdigest()
    target = (
        knowledge
        / "training"
        / "hard_negative_review"
        / "queues"
        / f"bcc_hn_{queue_id[:12]}"
    )
    return QueuePlan(
        knowledge_root=knowledge,
        base_model_path=base_model,
        class_map=class_map,
        vsa_manifest_path=Path(os.path.abspath(vsa_manifest_path)),
        created_utc=created_utc or datetime.now(timezone.utc),
        sources=source_evidence,
        source_specs=tuple(sources),
        protected_sets=protected_sets,
        protection_snapshot=protection,
        model_scope=model_scope,
        items=items,
        semantic_payload=semantic,
        queue_id=queue_id,
        target_root=target,
        scanned_photos=sum(len(group) for group in photos_by_holding.values()),
        clean_holdings=len(photos_by_holding),
        blocked_same_hash=blocked_hash,
        blocked_same_holding=blocked_holding,
    )


def _ensure_safe_directory(parent: Path, child_name: str) -> Path:
    parent = holdout_tools._safe_existing_path(
        parent,
        parent,
        expect_file=False,
    )
    child = parent / child_name
    if not child.exists():
        child.mkdir()
    return holdout_tools._safe_existing_path(
        child,
        parent,
        expect_file=False,
    )


def _remove_private_staging(staging: Path, parent: Path, prefix: str) -> None:
    if not staging.exists() and not staging.is_symlink():
        return
    if (
        staging.parent != parent
        or not staging.name.startswith(prefix)
        or holdout_tools._is_reparse_point(staging)
        or os.path.normcase(os.path.realpath(staging.parent))
        != os.path.normcase(str(parent))
    ):
        raise ValueError(f"Unsicherer temporaerer Ordner bleibt erhalten: {staging}")
    shutil.rmtree(staging)


def _assert_plan_inputs_unchanged(plan: QueuePlan) -> None:
    current_class_map = load_class_map(plan.class_map.path, plan.vsa_manifest_path)
    if current_class_map != plan.class_map:
        raise ValueError("Klassenkarte oder VSA-Manifest wurde nach der Planung veraendert.")
    contamination = holdout_tools.scan_contamination(
        plan.knowledge_root,
        plan.base_model_path,
    )
    if _protection_snapshot(plan.knowledge_root, contamination) != plan.protection_snapshot:
        raise ValueError("Der Trainings-/Kontaminationsbestand wurde veraendert.")
    if snapshot_protected_sets(plan.knowledge_root) != plan.protected_sets:
        raise ValueError("Ein geschuetzter Eval-Bestand wurde veraendert.")
    if _select_model_scope(
        contamination,
        [str(item["candidate_id"]) for item in plan.model_scope],
    ) != plan.model_scope:
        raise ValueError("Ein Auswahlmodell wurde veraendert.")
    expected_xtf_hashes = {str(item["xtf_sha256"]) for item in plan.sources}
    current_xtf_hashes: set[str] = set()
    for spec in plan.source_specs:
        xtf = holdout_tools._safe_existing_path(
            spec.xtf_path,
            spec.project_root,
            expect_file=True,
        )
        current_xtf_hashes.add(holdout_tools._sha256_file(xtf))
    if current_xtf_hashes != expected_xtf_hashes:
        raise ValueError("Eine XTF-Quelle wurde nach der Planung veraendert.")


def publish_queue(plan: QueuePlan) -> Path:
    expected = (
        plan.knowledge_root
        / "training"
        / "hard_negative_review"
        / "queues"
        / f"bcc_hn_{plan.queue_id[:12]}"
    )
    if os.path.normcase(str(plan.target_root)) != os.path.normcase(str(expected)):
        raise ValueError("Das Ziel passt nicht zur geprueften Prueflisten-ID.")
    if plan.target_root.exists() or plan.target_root.is_symlink():
        raise FileExistsError(
            f"Vorhandene Hard-Negative-Pruefliste wird nie ueberschrieben: "
            f"{plan.target_root}"
        )
    _assert_plan_inputs_unchanged(plan)

    training_root = _ensure_safe_directory(plan.knowledge_root, "training")
    review_root = _ensure_safe_directory(training_root, "hard_negative_review")
    queues_root = _ensure_safe_directory(review_root, "queues")
    staging = queues_root / f".bcc-hn-staging-{uuid.uuid4().hex}"
    staging.mkdir()
    try:
        images_root = staging / "images"
        images_root.mkdir()
        for item in plan.items:
            source = holdout_tools._safe_existing_path(
                item.source_path,
                item.source_path.parent,
                expect_file=True,
            )
            holdout_tools._validate_image(source)
            if holdout_tools._sha256_file(source) != item.image_sha256:
                raise ValueError("Ein Quellbild wurde nach der Planung veraendert.")
            holdout_tools._copy_verified(
                source,
                images_root / item.target_file_name,
                item.image_sha256,
            )

        candidates = [
            {
                "id": item.item_id,
                "frame_path": item.target_file_name,
                "category": "all_class_background_review",
                "status": "pending_review",
                "source_sha256": item.image_sha256,
            }
            for item in plan.items
        ]
        (staging / "_candidates.json").write_bytes(
            _pretty_json_bytes(candidates)
        )
        hashes = holdout_tools._manifest_hash_entries(staging)
        manifest = {
            "schema_version": SCHEMA_VERSION,
            "purpose": QUEUE_PURPOSE,
            "queue_id": plan.queue_id,
            "pilot": PILOT_NAME,
            "role": QUEUE_ROLE,
            "created_utc": plan.created_utc.isoformat().replace("+00:00", "Z"),
            "frozen": True,
            "dataset_status": "review_incomplete",
            "warning": (
                "NUR all_classes_clear DARF SPAETER ALS TRAININGSNEGATIV "
                "VEROEFFENTLICHT WERDEN"
            ),
            "review_target": (
                "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse"
            ),
            "class_map_version": plan.class_map.version,
            "class_map_sha256": plan.class_map.sha256,
            "vsa_manifest_hash": plan.class_map.vsa_manifest_hash,
            "class_names": list(plan.class_map.ordered_names),
            "protected_sets": list(plan.protected_sets),
            "protection_snapshot": plan.protection_snapshot,
            "selection_rule": {
                "one_image_per_physical_holding": True,
                "requires_current_model_bcc_trigger": True,
                "reviewer_sees_model_signals": False,
            },
            "sources": list(plan.sources),
            "candidates_count": len(candidates),
            "images_count": len(candidates),
            "holdings_count": len(plan.items),
            "hash_algorithm": "sha256",
            "hashes_count": len(hashes),
            "hashes": hashes,
            "semantic": plan.semantic_payload,
            "selection_receipt": {
                "models": list(plan.model_scope),
                "items": [_semantic_item(item) for item in plan.items],
            },
        }
        (staging / "_manifest.json").write_bytes(_pretty_json_bytes(manifest))
        if plan.target_root.exists() or plan.target_root.is_symlink():
            raise FileExistsError(f"Prueflistenziel existiert bereits: {plan.target_root}")
        os.rename(staging, plan.target_root)
    finally:
        _remove_private_staging(staging, queues_root, ".bcc-hn-staging-")
    return plan.target_root


def _require_utc_timestamp(value: Any, label: str) -> str:
    text = str(value or "").strip()
    if not text.endswith("Z"):
        raise ValueError(f"{label} ist kein UTC-Zeitstempel.")
    try:
        parsed = datetime.fromisoformat(text[:-1] + "+00:00")
    except ValueError as error:
        raise ValueError(f"{label} ist kein UTC-Zeitstempel.") from error
    if parsed.utcoffset() != timezone.utc.utcoffset(parsed):
        raise ValueError(f"{label} ist kein UTC-Zeitstempel.")
    return text


def _load_bound_review(
    queue_root: Path,
    review_path: Path,
) -> tuple[
    str,
    str,
    str,
    tuple[Any, ...],
    dict[str, Any],
    dict[str, Any],
    dict[str, int],
]:
    from tools.EvalVisibilityReview.bcc_release_holdout_review_server import (
        _validate_hard_negative_queue,
    )

    queue = Path(os.path.abspath(queue_root))
    queue_id, manifest_sha, candidates_sha, images = (
        _validate_hard_negative_queue(queue)
    )
    review_file = Path(os.path.abspath(review_path))
    review = _strict_json(review_file, "Hard-Negative-Review")
    if not isinstance(review, dict):
        raise ValueError("Das Hard-Negative-Review ist kein JSON-Objekt.")
    expected_fields = {
        "schema_version",
        "purpose",
        "queue_id",
        "queue_manifest_sha256",
        "candidates_sha256",
        "class_map_sha256",
        "reviewer",
        "updated_at_utc",
        "decisions",
    }
    if set(review) != expected_fields:
        raise ValueError("Das Hard-Negative-Review hat fehlende oder fremde Felder.")
    manifest = _strict_json(queue / "_manifest.json", "Hard-Negative-Manifest")
    if (
        review.get("schema_version") != SCHEMA_VERSION
        or review.get("purpose") != REVIEW_PURPOSE
        or review.get("queue_id") != queue_id
        or review.get("queue_manifest_sha256") != manifest_sha
        or review.get("candidates_sha256") != candidates_sha
        or review.get("class_map_sha256") != manifest.get("class_map_sha256")
    ):
        raise ValueError("Review und eingefrorene Pruefliste sind nicht fest verbunden.")
    reviewer = review.get("reviewer")
    if not isinstance(reviewer, str) or not reviewer.strip():
        raise ValueError("Das Hard-Negative-Review besitzt keinen Reviewer.")
    _require_utc_timestamp(
        review.get("updated_at_utc"),
        "Aktualisierungszeit des Hard-Negative-Reviews",
    )
    decisions = review.get("decisions")
    if not isinstance(decisions, dict):
        raise ValueError("Das Review enthaelt keine Entscheidungen.")
    image_ids = {image.candidate_id for image in images}
    if not set(decisions).issubset(image_ids):
        raise ValueError("Das Review enthaelt unbekannte Bild-IDs.")
    counts = {decision: 0 for decision in REVIEW_DECISION_ORDER}
    for item_id, raw in decisions.items():
        if not isinstance(raw, dict) or set(raw) != {
            "decision",
            "comment",
            "reviewed_at_utc",
        }:
            raise ValueError(f"Entscheidung {item_id} ist ungueltig.")
        decision = raw.get("decision")
        if decision not in REVIEW_DECISIONS:
            raise ValueError(f"Entscheidung {item_id} ist nicht erlaubt.")
        if not isinstance(raw.get("comment"), str):
            raise ValueError(f"Kommentar von Entscheidung {item_id} ist ungueltig.")
        _require_utc_timestamp(
            raw.get("reviewed_at_utc"),
            f"Review-Zeit von Entscheidung {item_id}",
        )
        counts[str(decision)] += 1
    return (
        queue_id,
        manifest_sha,
        candidates_sha,
        images,
        manifest,
        review,
        counts,
    )


def review_status(queue_root: Path, review_path: Path) -> dict[str, Any]:
    (
        queue_id,
        _,
        _,
        images,
        _,
        review,
        counts,
    ) = _load_bound_review(queue_root, review_path)
    decisions = review["decisions"]
    image_ids = {image.candidate_id for image in images}
    missing = len(image_ids - set(decisions))
    return {
        "schema_version": SCHEMA_VERSION,
        "queue_id": queue_id,
        "dataset_status": (
            "ready_for_negative_set_publish" if missing == 0 else "review_incomplete"
        ),
        "total_images": len(image_ids),
        "reviewed_images": len(decisions),
        "missing_reviews": missing,
        "counts": counts,
    }


def _negative_split_map(
    physical_holding_keys: Sequence[str],
) -> tuple[dict[str, str], int]:
    unique = set(physical_holding_keys)
    if len(unique) != len(physical_holding_keys):
        raise ValueError("Ein Negativsatz darf nur ein Bild je physischer Haltung enthalten.")
    ranked = sorted(
        unique,
        key=lambda holding: (
            hashlib.sha256(
                f"{NEGATIVE_SPLIT_SALT}|{holding}".encode("utf-8")
            ).hexdigest(),
            holding,
        ),
    )
    validation_count = 0 if len(ranked) < 2 else max(1, (len(ranked) + 2) // 5)
    validation = set(ranked[:validation_count])
    return (
        {
            holding: "validation" if holding in validation else "train"
            for holding in ranked
        },
        validation_count,
    )


def _assert_negative_set_protection(
    knowledge_root: Path,
    base_model_path: Path,
    queue_manifest: Mapping[str, Any],
    class_map_path: Path,
    vsa_manifest_path: Path,
) -> ClassMapBinding:
    semantic = queue_manifest.get("semantic")
    if not isinstance(semantic, dict):
        raise ValueError("Die Pruefliste besitzt keinen semantischen Beleg.")
    class_map = load_class_map(class_map_path, vsa_manifest_path)
    if (
        semantic.get("class_map_version") != class_map.version
        or semantic.get("class_map_sha256") != class_map.sha256
        or semantic.get("vsa_manifest_hash") != class_map.vsa_manifest_hash
        or semantic.get("class_names") != list(class_map.ordered_names)
    ):
        raise ValueError("Klassenkarte oder VSA-Manifest passt nicht mehr zur Pruefliste.")

    contamination = holdout_tools.scan_contamination(
        knowledge_root,
        base_model_path,
    )
    if (
        _protection_snapshot(knowledge_root, contamination)
        != semantic.get("protection_snapshot")
    ):
        raise ValueError(
            "Der Trainings-/Kontaminationsbestand wurde seit der Pruefliste veraendert."
        )
    if list(snapshot_protected_sets(knowledge_root)) != semantic.get("protected_sets"):
        raise ValueError(
            "Ein geschuetzter Eval-Bestand wurde seit der Pruefliste veraendert."
        )
    model_scope = semantic.get("model_scope")
    if not isinstance(model_scope, list) or not model_scope:
        raise ValueError("Die Pruefliste besitzt keine gebundenen Auswahlmodelle.")
    current_models = _select_model_scope(
        contamination,
        [str(item.get("candidate_id") or "") for item in model_scope],
    )
    if list(current_models) != model_scope:
        raise ValueError("Ein Auswahlmodell wurde seit der Pruefliste veraendert.")
    return class_map


def build_negative_set_plan(
    knowledge_root: Path,
    base_model_path: Path,
    queue_root: Path,
    review_path: Path,
    *,
    class_map_path: Path = DEFAULT_CLASS_MAP,
    vsa_manifest_path: Path = DEFAULT_VSA_MANIFEST,
    created_utc: datetime | None = None,
) -> NegativeSetPlan:
    knowledge = Path(os.path.abspath(knowledge_root))
    knowledge = holdout_tools._safe_existing_path(
        knowledge,
        knowledge,
        expect_file=False,
    )
    base_model = Path(os.path.abspath(base_model_path))
    base_model = holdout_tools._safe_existing_path(
        base_model,
        base_model.parent,
        expect_file=True,
    )
    queues_root = holdout_tools._safe_existing_path(
        knowledge / "training" / "hard_negative_review" / "queues",
        knowledge,
        expect_file=False,
    )
    queue = holdout_tools._safe_existing_path(
        Path(os.path.abspath(queue_root)),
        queues_root,
        expect_file=False,
    )
    reviews_root = holdout_tools._safe_existing_path(
        knowledge / "training" / "hard_negative_review" / "reviews",
        knowledge,
        expect_file=False,
    )
    review_file = holdout_tools._safe_existing_path(
        Path(os.path.abspath(review_path)),
        reviews_root,
        expect_file=True,
    )
    (
        queue_id,
        queue_manifest_sha,
        candidates_sha,
        verified_images,
        queue_manifest,
        review,
        counts,
    ) = _load_bound_review(queue, review_file)
    decisions = review["decisions"]
    missing = len(verified_images) - len(decisions)
    if missing:
        raise ValueError(
            f"Das Hard-Negative-Review ist noch nicht vollstaendig: {missing} offen."
        )
    if counts["all_classes_clear"] < 1:
        raise ValueError("Das Review hat kein vollstaendig klassenfreies Bild freigegeben.")

    class_map = _assert_negative_set_protection(
        knowledge,
        base_model,
        queue_manifest,
        Path(os.path.abspath(class_map_path)),
        Path(os.path.abspath(vsa_manifest_path)),
    )
    semantic_queue = queue_manifest["semantic"]
    receipt = queue_manifest.get("selection_receipt")
    if not isinstance(receipt, dict) or not isinstance(receipt.get("items"), list):
        raise ValueError("Die Pruefliste besitzt keinen gueltigen Auswahlbeleg.")
    receipt_by_id = {
        str(item.get("id") or ""): item
        for item in receipt["items"]
        if isinstance(item, dict)
    }
    verified_by_id = {image.candidate_id: image for image in verified_images}
    accepted_ids = sorted(
        (
            item_id
            for item_id, decision in decisions.items()
            if decision["decision"] == "all_classes_clear"
        ),
        key=str.casefold,
    )
    physical_keys = [
        str(receipt_by_id[item_id]["physical_holding_key"])
        for item_id in accepted_ids
    ]
    split_by_holding, validation_count = _negative_split_map(physical_keys)

    items: list[NegativeSetItem] = []
    for review_item_id in accepted_ids:
        receipt_item = receipt_by_id.get(review_item_id)
        verified = verified_by_id.get(review_item_id)
        if receipt_item is None or verified is None:
            raise ValueError(f"Freigegebenes Bild ist nicht an die Pruefliste gebunden.")
        physical = str(receipt_item["physical_holding_key"])
        image_sha = str(receipt_item["image_sha256"])
        image_format = str(receipt_item["image_format"]).casefold()
        target_name = f"img_{image_sha}.{image_format}"
        items.append(
            NegativeSetItem(
                item_id=f"bcc-neg-{image_sha}",
                source_path=verified.path,
                target_file_name=target_name,
                image_sha256=image_sha,
                holding_key=str(receipt_item["holding_key"]),
                physical_holding_key=physical,
                split=split_by_holding[physical],
                review_item_id=review_item_id,
                source_ref=str(receipt_item["source_ref"]),
                inspection_date=str(receipt_item["inspection_date"]),
                size_bytes=int(receipt_item["size_bytes"]),
                image_format=image_format,
            )
        )
    items.sort(key=lambda item: item.image_sha256)

    semantic_images = [
        {
            "id": item.item_id,
            "file_name": item.target_file_name,
            "image_sha256": item.image_sha256,
            "size_bytes": item.size_bytes,
            "image_format": item.image_format,
            "holding_key": item.holding_key,
            "physical_holding_key": item.physical_holding_key,
            "split": item.split,
            "review_item_id": item.review_item_id,
            "review_decision": "all_classes_clear",
            "source_ref": item.source_ref,
            "inspection_date": item.inspection_date,
        }
        for item in items
    ]
    review_sha = holdout_tools._sha256_file(review_file)
    semantic = {
        "schema_version": SCHEMA_VERSION,
        "purpose": NEGATIVE_SET_PURPOSE,
        "pilot": PILOT_NAME,
        "role": NEGATIVE_SET_ROLE,
        "queue": {
            "queue_id": queue_id,
            "queue_manifest_sha256": queue_manifest_sha,
            "queue_manifest_receipt_path": "receipts/queue_manifest.json",
            "candidates_sha256": candidates_sha,
            "candidates_receipt_path": "receipts/queue_candidates.json",
        },
        "review": {
            "purpose": REVIEW_PURPOSE,
            "review_sha256": review_sha,
            "receipt_path": "receipts/review.json",
            "reviewed_images": len(decisions),
            "decision_counts": counts,
        },
        "class_map_version": class_map.version,
        "class_map_sha256": class_map.sha256,
        "class_map_receipt_path": "receipts/class_map.json",
        "vsa_manifest_hash": class_map.vsa_manifest_hash,
        "class_names": list(class_map.ordered_names),
        "protected_sets": list(semantic_queue["protected_sets"]),
        "protection_snapshot": dict(semantic_queue["protection_snapshot"]),
        "split_rule": {
            "name": "stable_rank_v1",
            "salt": NEGATIVE_SPLIT_SALT,
            "one_image_per_physical_holding": True,
            "validation_count": validation_count,
            "train_count": len(items) - validation_count,
        },
        "images": semantic_images,
    }
    set_id = hashlib.sha256(_canonical_json_bytes(semantic)).hexdigest()
    target = (
        knowledge
        / "training"
        / "negatives"
        / "sets"
        / f"bcc_hn_{set_id[:12]}"
    )
    return NegativeSetPlan(
        knowledge_root=knowledge,
        base_model_path=base_model,
        queue_root=queue,
        review_path=review_file,
        class_map_path=class_map.path,
        vsa_manifest_path=Path(os.path.abspath(vsa_manifest_path)),
        created_utc=created_utc or datetime.now(timezone.utc),
        queue_id=queue_id,
        queue_manifest_sha256=queue_manifest_sha,
        candidates_sha256=candidates_sha,
        review_sha256=review_sha,
        items=tuple(items),
        semantic_payload=semantic,
        set_id=set_id,
        target_root=target,
    )


def _assert_negative_set_inputs_unchanged(plan: NegativeSetPlan) -> None:
    current = build_negative_set_plan(
        plan.knowledge_root,
        plan.base_model_path,
        plan.queue_root,
        plan.review_path,
        class_map_path=plan.class_map_path,
        vsa_manifest_path=plan.vsa_manifest_path,
        created_utc=plan.created_utc,
    )
    if (
        current.set_id != plan.set_id
        or current.semantic_payload != plan.semantic_payload
        or current.items != plan.items
    ):
        raise ValueError("Pruefliste, Review oder Schutzbestand wurde veraendert.")


def _negative_set_hash_entries(staging: Path) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    files = list((staging / "images").iterdir())
    files.extend((staging / "receipts").iterdir())
    for path in sorted(files, key=lambda item: item.relative_to(staging).as_posix()):
        relative = path.relative_to(staging).as_posix()
        result[relative] = {
            "sha256": holdout_tools._sha256_file(path),
            "size_bytes": path.stat().st_size,
        }
    return result


def publish_negative_set(plan: NegativeSetPlan) -> Path:
    expected = (
        plan.knowledge_root
        / "training"
        / "negatives"
        / "sets"
        / f"bcc_hn_{plan.set_id[:12]}"
    )
    if os.path.normcase(str(plan.target_root)) != os.path.normcase(str(expected)):
        raise ValueError("Das Ziel passt nicht zur geprueften Negativsatz-ID.")
    if plan.target_root.exists() or plan.target_root.is_symlink():
        raise FileExistsError(
            f"Vorhandener Negativsatz wird nie ueberschrieben: {plan.target_root}"
        )
    _assert_negative_set_inputs_unchanged(plan)

    training_root = _ensure_safe_directory(plan.knowledge_root, "training")
    negatives_root = _ensure_safe_directory(training_root, "negatives")
    sets_root = _ensure_safe_directory(negatives_root, "sets")
    staging = sets_root / f".bcc-hn-set-staging-{uuid.uuid4().hex}"
    staging.mkdir()
    try:
        images_root = staging / "images"
        images_root.mkdir()
        for item in plan.items:
            source = holdout_tools._safe_existing_path(
                item.source_path,
                plan.queue_root / "images",
                expect_file=True,
            )
            holdout_tools._validate_image(source)
            if (
                source.stat().st_size != item.size_bytes
                or holdout_tools._sha256_file(source) != item.image_sha256
            ):
                raise ValueError("Ein Review-Bild wurde vor der Veroeffentlichung veraendert.")
            holdout_tools._copy_verified(
                source,
                images_root / item.target_file_name,
                item.image_sha256,
            )

        receipts_root = staging / "receipts"
        receipts_root.mkdir()
        receipt_sources = (
            (
                plan.review_path,
                receipts_root / "review.json",
                plan.review_sha256,
            ),
            (
                plan.queue_root / "_manifest.json",
                receipts_root / "queue_manifest.json",
                plan.queue_manifest_sha256,
            ),
            (
                plan.queue_root / "_candidates.json",
                receipts_root / "queue_candidates.json",
                plan.candidates_sha256,
            ),
            (
                plan.class_map_path,
                receipts_root / "class_map.json",
                str(plan.semantic_payload["class_map_sha256"]),
            ),
        )
        for source_path, destination, expected_sha in receipt_sources:
            source = holdout_tools._safe_existing_path(
                source_path,
                source_path.parent,
                expect_file=True,
            )
            holdout_tools._copy_verified(source, destination, expected_sha)

        hashes = _negative_set_hash_entries(staging)
        manifest = {
            "schema_version": SCHEMA_VERSION,
            "purpose": NEGATIVE_SET_PURPOSE,
            "set_id": plan.set_id,
            "pilot": PILOT_NAME,
            "role": NEGATIVE_SET_ROLE,
            "created_utc": plan.created_utc.isoformat().replace("+00:00", "Z"),
            "frozen": True,
            "dataset_status": "ready_for_training",
            "hash_algorithm": "sha256",
            "images_count": len(plan.items),
            "holdings_count": len(plan.items),
            "hashes_count": len(hashes),
            "hashes": hashes,
            "semantic": plan.semantic_payload,
        }
        (staging / "_manifest.json").write_bytes(_pretty_json_bytes(manifest))
        if plan.target_root.exists() or plan.target_root.is_symlink():
            raise FileExistsError(f"Negativsatzziel existiert bereits: {plan.target_root}")
        os.rename(staging, plan.target_root)
    finally:
        _remove_private_staging(staging, sets_root, ".bcc-hn-set-staging-")
    return plan.target_root


def _default_base_model() -> Path:
    return (
        REPOSITORY_ROOT / "sidecar" / "models" / "yolo26m" / "yolo26m.pt"
    )


def _parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Sichere BCC-Hard-Negative-Pruefliste vorbereiten oder pruefen."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)
    prepare = subparsers.add_parser(
        "prepare",
        help="Frische Modell-Fehlalarme suchen und eine blinde Pruefliste einfrieren.",
    )
    prepare.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    prepare.add_argument("--base-model", type=Path, default=_default_base_model())
    prepare.add_argument("--class-map", type=Path, default=DEFAULT_CLASS_MAP)
    prepare.add_argument("--vsa-manifest", type=Path, default=DEFAULT_VSA_MANIFEST)
    prepare.add_argument(
        "--source",
        nargs=2,
        action="append",
        metavar=("PROJECT_ROOT", "XTF_PATH"),
        required=True,
    )
    prepare.add_argument("--candidate", action="append", required=True)
    prepare.add_argument("--device")
    prepare.add_argument(
        "--execute",
        action="store_true",
        help="Die gepruefte Queue wirklich schreiben.",
    )

    status = subparsers.add_parser(
        "status",
        help="Vollstaendigkeit und Bindung eines Reviews pruefen.",
    )
    status.add_argument("--queue", type=Path, required=True)
    status.add_argument("--review", type=Path, required=True)

    publish = subparsers.add_parser(
        "publish",
        help="Nur vollstaendig gepruefte, klassenfreie Bilder sicher veroeffentlichen.",
    )
    publish.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    publish.add_argument("--base-model", type=Path, default=_default_base_model())
    publish.add_argument("--class-map", type=Path, default=DEFAULT_CLASS_MAP)
    publish.add_argument("--vsa-manifest", type=Path, default=DEFAULT_VSA_MANIFEST)
    publish.add_argument("--queue", type=Path, required=True)
    publish.add_argument("--review", type=Path, required=True)
    publish.add_argument(
        "--execute",
        action="store_true",
        help="Den geprueften, unveraenderlichen Negativsatz wirklich schreiben.",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    if args.command == "status":
        status = review_status(args.queue, args.review)
        print(json.dumps(status, ensure_ascii=False, indent=2))
        return 0

    if args.command == "publish":
        plan = build_negative_set_plan(
            args.knowledge_root,
            args.base_model,
            args.queue,
            args.review,
            class_map_path=args.class_map,
            vsa_manifest_path=args.vsa_manifest,
        )
        train_count = sum(item.split == "train" for item in plan.items)
        validation_count = len(plan.items) - train_count
        print(f"Freigegebene klassenfreie Bilder: {len(plan.items)}")
        print(f"Train: {train_count}; Validation: {validation_count}")
        print(f"Negativsatz-ID: {plan.set_id}")
        if not args.execute:
            print(
                "Nur geprueft; mit --execute wird der unveraenderliche "
                "Negativsatz geschrieben."
            )
            return 0
        target = publish_negative_set(plan)
        print(f"Negativsatz geschrieben: {target}")
        return 0

    source_specs = tuple(
        holdout_tools.SourceSpec(Path(project), Path(xtf))
        for project, xtf in args.source
    )
    plan = build_queue_plan(
        args.knowledge_root,
        args.base_model,
        source_specs,
        tuple(args.candidate),
        class_map_path=args.class_map,
        vsa_manifest_path=args.vsa_manifest,
        device=args.device,
    )
    print(f"Frische, reine Nicht-BCC-Haltungen: {plan.clean_holdings}")
    print(f"Gepruefte Quellbilder: {plan.scanned_photos}")
    print(f"Ausgewaehlte Hard-Negative-Kandidaten: {len(plan.items)}")
    print(f"Prueflisten-ID: {plan.queue_id}")
    if not args.execute:
        print("Nur geprueft; mit --execute wird die unveraenderliche Queue geschrieben.")
        return 0
    target = publish_queue(plan)
    print(f"Pruefliste geschrieben: {target}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
