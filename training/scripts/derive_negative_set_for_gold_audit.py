#!/usr/bin/env python3
"""Leitet einen audit-sicheren, unveraenderlichen Negativsatz ab.

Das Werkzeug veraendert weder den bestehenden Negativsatz noch dessen Review.
Es entfernt nur Bilder, deren Haltung im aktuellen Gold-Audit als Testhaltung
reserviert ist oder deren neu berechnete Split-Rolle dem Audit widerspricht.
Eine bytegleiche Gold-/Negativ-Kollision bleibt immer ein harter Fehler.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, Sequence


SCRIPT_ROOT = Path(__file__).resolve().parent
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

import bcc_hard_negative_review as negative_tools
import gold_stock_audit as gold_audit_tools
import prepare_detect_gold as detect_tools


AUTOMATIC_REVIEWER = "Automatischer Testsatzschutz (freigegeben: {approved_by})"
TEST_HOLDING_REASON = "audit_test_holding"
ROLE_CONFLICT_REASON = "audit_split_role_conflict"


@dataclass(frozen=True)
class AuditBinding:
    path: Path
    sha256: str
    created_utc: datetime
    samples_sha256: str
    registry_sha256: str
    image_hashes: frozenset[str]
    physical_roles: tuple[tuple[str, str], ...]

    def role_by_physical_holding(self) -> dict[str, str]:
        return dict(self.physical_roles)


@dataclass(frozen=True)
class DerivedNegativeItem:
    source_path: Path
    semantic: dict[str, Any]


@dataclass(frozen=True)
class ExcludedNegativeItem:
    review_item_id: str
    image_sha256: str
    holding_key: str
    physical_holding_key: str
    negative_split: str
    audit_role: str
    reason: str


@dataclass(frozen=True)
class DerivedNegativeSetPlan:
    knowledge_root: Path
    source_set_root: Path
    source_manifest_sha256: str
    gold_audit: AuditBinding
    approved_by: str
    created_utc: datetime
    review_document: dict[str, Any]
    review_bytes: bytes
    items: tuple[DerivedNegativeItem, ...]
    excluded: tuple[ExcludedNegativeItem, ...]
    semantic_payload: dict[str, Any]
    set_id: str
    target_root: Path


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _require_nonempty(value: Any, label: str) -> str:
    text = str(value or "").strip()
    if not text:
        raise ValueError(f"{label} fehlt.")
    return text


def _parse_utc_timestamp(value: Any) -> datetime:
    text = _require_nonempty(value, "UTC-Zeitpunkt")
    normalized = text[:-1] + "+00:00" if text.endswith("Z") else text
    try:
        parsed = datetime.fromisoformat(normalized)
    except ValueError as error:
        raise ValueError("Der UTC-Zeitpunkt ist kein gueltiger ISO-Zeitpunkt.") from error
    if parsed.tzinfo is None or parsed.utcoffset() != timezone.utc.utcoffset(parsed):
        raise ValueError("Der Zeitpunkt muss in UTC angegeben sein.")
    return parsed.astimezone(timezone.utc)


def _require_utc_datetime(value: datetime, label: str) -> datetime:
    if value.tzinfo is None or value.utcoffset() != timezone.utc.utcoffset(value):
        raise ValueError(f"{label} muss in UTC angegeben sein.")
    return value.astimezone(timezone.utc)


def _require_plain_input_file(path: Path, expected_root: Path, label: str) -> Path:
    root = Path(os.path.abspath(expected_root))
    requested = Path(os.path.abspath(path))
    safe_path = detect_tools._require_safe_storage_path(requested, root, label)
    if not safe_path.is_file():
        raise ValueError(f"{label} fehlt: {safe_path}")
    try:
        resolved_root = root.resolve(strict=True)
        resolved_path = safe_path.resolve(strict=True)
    except OSError as error:
        raise ValueError(f"{label} ist nicht sicher aufloesbar: {safe_path}") from error
    if not detect_tools._is_within(resolved_path, resolved_root):
        raise ValueError(f"{label} liegt nicht innerhalb von {resolved_root}: {safe_path}")
    return resolved_path


def _load_validated_gold_audit(
    knowledge_root: Path,
    gold_audit_path: Path,
    approved_by: str,
) -> AuditBinding:
    root = Path(os.path.abspath(knowledge_root))
    if not root.is_dir() or detect_tools._is_reparse_or_symlink(root):
        raise ValueError(f"Der Knowledge-Root fehlt oder ist ein Link/Junction: {root}")
    root = root.resolve(strict=True)

    samples_path = _require_plain_input_file(
        root / "training_samples.json",
        root,
        "training_samples.json",
    )
    raw_samples, samples_bytes = detect_tools._load_json_array_with_bytes(
        samples_path,
        "training_samples.json",
    )
    samples_sha256 = _sha256_bytes(samples_bytes)
    (
        _registry,
        _registry_bytes,
        registry_sha256,
        _protected_sets,
    ) = detect_tools._read_existing_registry(root, approved_by)
    (
        _classes,
        _class_map_bytes,
        _class_map_sha256,
        vsa_manifest_sha256,
    ) = detect_tools._read_active_class_map()
    safe_audit_path = _require_plain_input_file(
        gold_audit_path,
        root / "training" / "reports",
        "Gold-Audit",
    )
    audit, audit_bytes = detect_tools._validate_audit_header(
        root,
        approved_by,
        safe_audit_path,
        registry_sha256,
        samples_sha256,
        vsa_manifest_sha256,
    )
    audit_sha256 = _sha256_bytes(audit_bytes)

    source_by_id: dict[str, dict[str, Any]] = {}
    for source in raw_samples:
        sample_id = _require_nonempty(source.get("SampleId"), "SampleId")
        if sample_id in source_by_id:
            raise ValueError(f"SampleId ist mehrfach vorhanden: {sample_id}")
        source_by_id[sample_id] = source

    image_roles: dict[str, str] = {}
    holding_roles: dict[str, str] = {}
    physical_roles: dict[str, str] = {}
    seen_sample_ids: set[str] = set()
    for entry in audit["samples"]:
        sample_id = _require_nonempty(entry.get("sample_id"), "Audit-SampleId")
        case_id = _require_nonempty(entry.get("case_id"), f"CaseId von {sample_id}")
        holding_key = _require_nonempty(
            entry.get("haltung_key"),
            f"Haltung von {sample_id}",
        )
        role = str(entry.get("rolle") or "").strip().casefold()
        group_key = _require_nonempty(entry.get("gruppe"), f"Gruppe von {sample_id}")
        image_sha256 = detect_tools._require_sha256(
            entry.get("image_sha256"),
            f"Bild-Hash von {sample_id}",
        )
        if sample_id in seen_sample_ids:
            raise ValueError(f"Audit-SampleId ist mehrfach vorhanden: {sample_id}")
        seen_sample_ids.add(sample_id)
        if (
            role not in detect_tools.ALLOWED_AUDIT_ROLES
            or role != detect_tools._expected_split_role(group_key)
            or gold_audit_tools.normalize_holding_key(case_id) != holding_key
        ):
            raise ValueError(
                f"Gold-Audit-Sample {sample_id} besitzt keine belastbare "
                "Haltung oder Split-Rolle."
            )
        physical = detect_tools._physical_holding_key(holding_key)
        if holding_roles.setdefault(holding_key, role) != role:
            raise ValueError(f"Haltung {holding_key} hat widerspruechliche Audit-Rollen.")
        if physical_roles.setdefault(physical, role) != role:
            raise ValueError(
                "Gegenrichtungen derselben physischen Haltung besitzen "
                "widerspruechliche Audit-Rollen."
            )
        if image_roles.setdefault(image_sha256, role) != role:
            raise ValueError("Dasselbe Goldbild liegt in mehreren Split-Rollen.")

        source = source_by_id.get(sample_id)
        if source is None:
            raise ValueError(f"Audit-Sample fehlt in training_samples.json: {sample_id}")
        if (
            str(source.get("CaseId") or "").strip() != case_id
            or gold_audit_tools.normalized_code(source.get("Code"))
            != gold_audit_tools.normalized_code(entry.get("code"))
        ):
            raise ValueError(
                f"Goldsample {sample_id} weicht bei Code oder CaseId vom Audit ab."
            )
        frame_path, _source_type = detect_tools._verify_personal_source(
            source,
            approved_by,
            sample_id,
        )
        gold_root = (root / "gold_frames").resolve()
        resolved_frame = detect_tools._require_plain_path_below(
            frame_path,
            gold_root,
            f"Goldbild {sample_id}",
        )
        if detect_tools._sha256_file(resolved_frame) != image_sha256:
            raise ValueError(f"Bild-Hash von Goldsample {sample_id} weicht vom Audit ab.")

    resolved_audit = safe_audit_path
    if resolved_audit.read_bytes() != audit_bytes:
        raise ValueError("Der Gold-Audit wurde waehrend der Pruefung geaendert.")
    if samples_path.read_bytes() != samples_bytes:
        raise ValueError("training_samples.json wurde waehrend der Pruefung geaendert.")

    return AuditBinding(
        path=resolved_audit,
        sha256=audit_sha256,
        created_utc=_parse_utc_timestamp(audit["zeitstempel_utc"]),
        samples_sha256=samples_sha256,
        registry_sha256=registry_sha256,
        image_hashes=frozenset(image_roles),
        physical_roles=tuple(sorted(physical_roles.items())),
    )


def _load_source_set(
    knowledge_root: Path,
    source_set_root: Path,
) -> tuple[
    Path,
    bytes,
    dict[str, Any],
    list[dict[str, Any]],
    dict[str, Any],
]:
    root = Path(os.path.abspath(knowledge_root)).resolve(strict=True)
    source = Path(os.path.abspath(source_set_root))
    negative_images, provenance = gold_audit_tools._read_reviewed_negative_set(
        root,
        source,
    )
    safe_source = gold_audit_tools._safe_negative_set_root(root, source)
    manifest_bytes = (safe_source / "_manifest.json").read_bytes()
    manifest = gold_audit_tools._strict_json_bytes(
        manifest_bytes,
        "Ausgangs-Negativsatz-Manifest",
    )
    if not isinstance(manifest, dict):
        raise ValueError("Das Ausgangs-Negativsatz-Manifest ist kein JSON-Objekt.")
    if provenance.get("manifest_sha256") != _sha256_bytes(manifest_bytes):
        raise ValueError("Der Ausgangs-Negativsatz wurde waehrend der Pruefung geaendert.")
    return safe_source, manifest_bytes, manifest, negative_images, provenance


def _stable_conflict_filter(
    negative_images: Sequence[Mapping[str, Any]],
    audit: AuditBinding,
) -> tuple[dict[str, str], tuple[ExcludedNegativeItem, ...]]:
    for image in negative_images:
        image_sha256 = detect_tools._require_sha256(
            image.get("sha256"),
            "Bild-Hash eines Negativbilds",
        )
        if image_sha256 in audit.image_hashes:
            raise ValueError(
                "Negativbild ist bytegleich mit einem Gold-Audit-Bild; ein "
                "identisches Goldbild darf nie als all_classes_clear gelten."
            )

    active = {str(image["sha256"]): dict(image) for image in negative_images}
    if len(active) != len(negative_images):
        raise ValueError("Der Ausgangs-Negativsatz enthaelt doppelte Bild-Hashes.")
    roles = audit.role_by_physical_holding()
    excluded: dict[str, ExcludedNegativeItem] = {}

    while True:
        split_by_holding, _validation_count = negative_tools._negative_split_map(
            [str(image["physical_holding_key"]) for image in active.values()]
        )
        newly_excluded: list[tuple[str, ExcludedNegativeItem]] = []
        for image_sha256, image in active.items():
            physical = str(image.get("physical_holding_key") or "")
            holding = str(image.get("holding_key") or "")
            if physical != detect_tools._physical_holding_key(holding):
                raise ValueError("Ein Negativbild besitzt keine belastbare Haltung.")
            split = split_by_holding[physical]
            negative_role = "val" if split == "validation" else "train"
            audit_role = roles.get(physical)
            if audit_role == "test":
                reason = TEST_HOLDING_REASON
            elif audit_role is not None and audit_role != negative_role:
                reason = ROLE_CONFLICT_REASON
            else:
                continue
            newly_excluded.append(
                (
                    image_sha256,
                    ExcludedNegativeItem(
                        review_item_id=str(image["review_item_id"]),
                        image_sha256=image_sha256,
                        holding_key=holding,
                        physical_holding_key=physical,
                        negative_split=split,
                        audit_role=str(audit_role),
                        reason=reason,
                    ),
                )
            )
        if not newly_excluded:
            break
        for image_sha256, exclusion in newly_excluded:
            excluded[image_sha256] = exclusion
            del active[image_sha256]
        if not active:
            raise ValueError(
                "Nach dem Audit-Testsatzschutz bleibt kein Trainingsnegativ uebrig."
            )

    final_splits, _validation_count = negative_tools._negative_split_map(
        [str(image["physical_holding_key"]) for image in active.values()]
    )
    return final_splits, tuple(
        excluded[key] for key in sorted(excluded, key=str.casefold)
    )


def _guard_comment(
    exclusion: ExcludedNegativeItem,
    audit_sha256: str,
    source_manifest_sha256: str,
    approved_by: str,
) -> str:
    if exclusion.reason == TEST_HOLDING_REASON:
        reason = "Konflikt mit einer eingefrorenen Audit-Testhaltung"
    else:
        reason = (
            "Split-Rollenkonflikt "
            f"(Negativ={exclusion.negative_split}, Audit={exclusion.audit_role})"
        )
    return (
        f"Automatischer Testsatzschutz: {reason}. "
        f"Gold-Audit SHA-256 {audit_sha256}. "
        f"Ausgangssatz-Manifest SHA-256 {source_manifest_sha256}. "
        f"Freigegeben durch {approved_by}."
    )


def _build_derived_review(
    source_review: Mapping[str, Any],
    excluded: Sequence[ExcludedNegativeItem],
    *,
    approved_by: str,
    audit_sha256: str,
    source_manifest_sha256: str,
    updated_utc: datetime,
) -> tuple[dict[str, Any], bytes, dict[str, int]]:
    review = json.loads(json.dumps(source_review, ensure_ascii=False))
    decisions = review.get("decisions")
    if not isinstance(decisions, dict):
        raise ValueError("Der Review-Beleg besitzt keine Entscheidungen.")
    timestamp = updated_utc.isoformat().replace("+00:00", "Z")
    for exclusion in excluded:
        decision = decisions.get(exclusion.review_item_id)
        if not isinstance(decision, dict) or decision.get("decision") != "all_classes_clear":
            raise ValueError(
                "Ein auszuschliessendes Negativbild ist nicht als all_classes_clear gebunden."
            )
        original_comment = str(decision.get("comment") or "").strip()
        guard = _guard_comment(
            exclusion,
            audit_sha256,
            source_manifest_sha256,
            approved_by,
        )
        decision["decision"] = "exclude_uncertain"
        decision["comment"] = (
            f"{original_comment}\n\n{guard}" if original_comment else guard
        )
        decision["reviewed_at_utc"] = timestamp
    review["reviewer"] = AUTOMATIC_REVIEWER.format(approved_by=approved_by)
    review["updated_at_utc"] = timestamp

    counts = {decision: 0 for decision in negative_tools.REVIEW_DECISION_ORDER}
    for decision in decisions.values():
        value = str(decision.get("decision") or "")
        if value not in counts:
            raise ValueError(f"Der Review-Beleg enthaelt eine unbekannte Entscheidung: {value}")
        counts[value] += 1
    review_bytes = negative_tools._pretty_json_bytes(review)
    return review, review_bytes, counts


def build_plan_from_validated_inputs(
    knowledge_root: Path,
    source_set_root: Path,
    source_manifest_bytes: bytes,
    source_manifest: Mapping[str, Any],
    negative_images: Sequence[Mapping[str, Any]],
    audit: AuditBinding,
    approved_by: str,
    created_utc: datetime,
) -> DerivedNegativeSetPlan:
    created_utc = _require_utc_datetime(created_utc, "Ableitungszeitpunkt")
    source_manifest_sha256 = _sha256_bytes(source_manifest_bytes)
    semantic = source_manifest.get("semantic")
    if not isinstance(semantic, dict) or not isinstance(semantic.get("images"), list):
        raise ValueError("Der Ausgangs-Negativsatz besitzt keinen semantischen Bildbeleg.")
    semantic_by_hash = {
        str(image.get("image_sha256") or ""): image
        for image in semantic["images"]
        if isinstance(image, dict)
    }
    final_splits, excluded = _stable_conflict_filter(negative_images, audit)
    if not excluded:
        raise ValueError(
            "Der Ausgangs-Negativsatz hat keinen Konflikt mit Audit-Testhaltungen "
            "oder Split-Rollen; ein abgeleiteter Satz ist nicht noetig."
        )
    excluded_hashes = {item.image_sha256 for item in excluded}

    source_review_path = source_set_root / "receipts" / "review.json"
    source_review = negative_tools._strict_json(
        source_review_path,
        "Review-Beleg des Ausgangssatzes",
    )
    if not isinstance(source_review, dict):
        raise ValueError("Der Review-Beleg des Ausgangssatzes ist kein JSON-Objekt.")
    review, review_bytes, decision_counts = _build_derived_review(
        source_review,
        excluded,
        approved_by=approved_by,
        audit_sha256=audit.sha256,
        source_manifest_sha256=source_manifest_sha256,
        updated_utc=created_utc,
    )

    items: list[DerivedNegativeItem] = []
    semantic_images: list[dict[str, Any]] = []
    for raw in sorted(negative_images, key=lambda image: str(image["sha256"])):
        image_sha256 = str(raw["sha256"])
        if image_sha256 in excluded_hashes:
            continue
        source_semantic = semantic_by_hash.get(image_sha256)
        if source_semantic is None:
            raise ValueError("Ein validiertes Negativbild fehlt im semantischen Beleg.")
        image_semantic = dict(source_semantic)
        physical = str(image_semantic["physical_holding_key"])
        image_semantic["split"] = final_splits[physical]
        source_path = source_set_root / "images" / str(image_semantic["file_name"])
        items.append(DerivedNegativeItem(source_path, image_semantic))
        semantic_images.append(image_semantic)

    if not items:
        raise ValueError("Der abgeleitete Negativsatz waere leer.")
    validation_count = sum(
        item.semantic["split"] == "validation" for item in items
    )
    derived_semantic = {
        "schema_version": semantic["schema_version"],
        "purpose": semantic["purpose"],
        "pilot": semantic["pilot"],
        "role": semantic["role"],
        "queue": dict(semantic["queue"]),
        "review": {
            "purpose": semantic["review"]["purpose"],
            "review_sha256": _sha256_bytes(review_bytes),
            "receipt_path": "receipts/review.json",
            "reviewed_images": len(review["decisions"]),
            "decision_counts": decision_counts,
        },
        "class_map_version": semantic["class_map_version"],
        "class_map_sha256": semantic["class_map_sha256"],
        "class_map_receipt_path": "receipts/class_map.json",
        "vsa_manifest_hash": semantic["vsa_manifest_hash"],
        "class_names": list(semantic["class_names"]),
        "protected_sets": list(semantic["protected_sets"]),
        "protection_snapshot": dict(semantic["protection_snapshot"]),
        "split_rule": {
            "name": "stable_rank_v1",
            "salt": negative_tools.NEGATIVE_SPLIT_SALT,
            "one_image_per_physical_holding": True,
            "validation_count": validation_count,
            "train_count": len(items) - validation_count,
        },
        "images": semantic_images,
    }
    set_id = _sha256_bytes(negative_tools._canonical_json_bytes(derived_semantic))
    target_root = (
        Path(os.path.abspath(knowledge_root))
        / "training"
        / "negatives"
        / "sets"
        / f"bcc_hn_{set_id[:12]}"
    )
    return DerivedNegativeSetPlan(
        knowledge_root=Path(os.path.abspath(knowledge_root)).resolve(strict=True),
        source_set_root=source_set_root,
        source_manifest_sha256=source_manifest_sha256,
        gold_audit=audit,
        approved_by=approved_by,
        created_utc=created_utc,
        review_document=review,
        review_bytes=review_bytes,
        items=tuple(items),
        excluded=excluded,
        semantic_payload=derived_semantic,
        set_id=set_id,
        target_root=target_root,
    )


def build_plan(
    knowledge_root: Path,
    source_set_root: Path,
    gold_audit_path: Path,
    approved_by: str,
    *,
    created_utc: datetime | None = None,
) -> DerivedNegativeSetPlan:
    user = _require_nonempty(approved_by, "Freigebende Person")
    audit = _load_validated_gold_audit(knowledge_root, gold_audit_path, user)
    (
        safe_source,
        source_manifest_bytes,
        source_manifest,
        negative_images,
        _provenance,
    ) = _load_source_set(knowledge_root, source_set_root)
    return build_plan_from_validated_inputs(
        knowledge_root,
        safe_source,
        source_manifest_bytes,
        source_manifest,
        negative_images,
        audit,
        user,
        created_utc or datetime.now(timezone.utc),
    )


def _assert_inputs_unchanged(plan: DerivedNegativeSetPlan) -> None:
    current = build_plan(
        plan.knowledge_root,
        plan.source_set_root,
        plan.gold_audit.path,
        plan.approved_by,
        created_utc=plan.created_utc,
    )
    if current != plan:
        raise ValueError(
            "Ausgangssatz, Gold-Audit oder gebundene Trainingsdaten wurden veraendert."
        )


def publish(plan: DerivedNegativeSetPlan) -> Path:
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
    _assert_inputs_unchanged(plan)

    training_root = negative_tools._ensure_safe_directory(
        plan.knowledge_root,
        "training",
    )
    negatives_root = negative_tools._ensure_safe_directory(training_root, "negatives")
    sets_root = negative_tools._ensure_safe_directory(negatives_root, "sets")
    staging = sets_root / f".bcc-hn-audit-guard-staging-{uuid.uuid4().hex}"
    staging.mkdir()
    try:
        images_root = staging / "images"
        receipts_root = staging / "receipts"
        images_root.mkdir()
        receipts_root.mkdir()

        for item in plan.items:
            expected_sha = str(item.semantic["image_sha256"])
            source = negative_tools.holdout_tools._safe_existing_path(
                item.source_path,
                plan.source_set_root / "images",
                expect_file=True,
            )
            if (
                source.stat().st_size != int(item.semantic["size_bytes"])
                or negative_tools.holdout_tools._sha256_file(source) != expected_sha
            ):
                raise ValueError("Ein Negativbild wurde vor der Ableitung veraendert.")
            negative_tools.holdout_tools._copy_verified(
                source,
                images_root / str(item.semantic["file_name"]),
                expected_sha,
            )

        (receipts_root / "review.json").write_bytes(plan.review_bytes)
        receipt_hashes = {
            "queue_manifest.json": str(
                plan.semantic_payload["queue"]["queue_manifest_sha256"]
            ),
            "queue_candidates.json": str(
                plan.semantic_payload["queue"]["candidates_sha256"]
            ),
            "class_map.json": str(plan.semantic_payload["class_map_sha256"]),
        }
        for name, expected_sha in receipt_hashes.items():
            source = negative_tools.holdout_tools._safe_existing_path(
                plan.source_set_root / "receipts" / name,
                plan.source_set_root / "receipts",
                expect_file=True,
            )
            negative_tools.holdout_tools._copy_verified(
                source,
                receipts_root / name,
                expected_sha,
            )

        hashes = negative_tools._negative_set_hash_entries(staging)
        manifest = {
            "schema_version": negative_tools.SCHEMA_VERSION,
            "purpose": negative_tools.NEGATIVE_SET_PURPOSE,
            "set_id": plan.set_id,
            "pilot": negative_tools.PILOT_NAME,
            "role": negative_tools.NEGATIVE_SET_ROLE,
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
        (staging / "_manifest.json").write_bytes(
            negative_tools._pretty_json_bytes(manifest)
        )
        if plan.target_root.exists() or plan.target_root.is_symlink():
            raise FileExistsError(f"Negativsatzziel existiert bereits: {plan.target_root}")
        os.rename(staging, plan.target_root)
    finally:
        negative_tools._remove_private_staging(
            staging,
            sets_root,
            ".bcc-hn-audit-guard-staging-",
        )
    return plan.target_root


def _parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Leitet aus einem streng reviewten Negativsatz einen "
            "audit-sicheren, unveraenderlichen Satz ab."
        )
    )
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--source-set", type=Path, required=True)
    parser.add_argument("--gold-audit", type=Path, required=True)
    parser.add_argument("--approved-by", required=True)
    parser.add_argument(
        "--execute",
        action="store_true",
        help="Den neuen unveraenderlichen Negativsatz wirklich schreiben.",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    plan = build_plan(
        args.knowledge_root,
        args.source_set,
        args.gold_audit,
        args.approved_by,
    )
    train_count = sum(item.semantic["split"] == "train" for item in plan.items)
    validation_count = len(plan.items) - train_count
    print(f"Ausgangssatz: {plan.source_set_root}")
    print(f"Ausgeschlossen durch Testsatzschutz: {len(plan.excluded)}")
    for item in plan.excluded:
        print(
            f"- {item.holding_key}: {item.reason} "
            f"(Negativ={item.negative_split}, Audit={item.audit_role})"
        )
    print(f"Neuer Satz: {len(plan.items)} Bilder; Train {train_count}; Validation {validation_count}")
    print(f"Negativsatz-ID: {plan.set_id}")
    if not args.execute:
        print("Nur geprueft; mit --execute wird der unveraenderliche Satz geschrieben.")
        return 0
    target = publish(plan)
    print(f"Negativsatz geschrieben: {target}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
