#!/usr/bin/env python3
"""Erzeugt eine strikt diagnostische Review-Queue aus Detect-Gold-Fehlfaellen.

Die Queue kopiert keine Bilder und besitzt keinen Schreibweg zu Gold, KB,
Trainingsdaten, Registry oder Modell. Sie verweist nur auf bereits geschuetzte
Goldbilder und bindet Bericht, labelblinden Vorhersagebeleg und Provenienz per
SHA-256.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import sys
import uuid
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, Sequence


SCRIPT_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_DIR.parents[1]
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import detect_gold_holdout_provenance as provenance_tools
import detect_gold_holdout_scoring as scoring
import evaluate_detect_gold_holdout as evaluation_tools


SCHEMA_VERSION = "1.0"
QUEUE_PURPOSE = "detect_gold_failure_review_queue"
LEGACY_COLLECTION_QUEUE_PURPOSE = "detect_gold_error_review_queue"
REVIEW_PURPOSE = "detect_gold_failure_review"
LEGACY_REVIEW_PURPOSE = "detect_gold_error_review"
COLLECTION_PURPOSE = "detect_gold_targeted_collection_plan"
QUEUE_ROLE = "diagnostic_only"
VALID_CASE_TYPES = frozenset({"wrong_class", "missed", "extra_prediction"})
VALID_REVIEW_DECISIONS = frozenset(
    {"confirmed_model_error", "gold_suspect", "exclude_uncertain"}
)
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
QUEUE_PREFIX = "detect_gold_failure_"


def canonical_json_bytes(value: object) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def pretty_json_bytes(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode(
        "utf-8"
    )


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _reject_constant(value: str) -> None:
    raise ValueError(f"Ungueltige JSON-Zahl: {value}")


def _pairs_without_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Doppeltes JSON-Feld: {key}")
        result[key] = value
    return result


def strict_json_bytes(data: bytes, label: str) -> object:
    try:
        text = data.decode("utf-8-sig")
        return json.loads(
            text,
            object_pairs_hook=_pairs_without_duplicates,
            parse_constant=_reject_constant,
        )
    except (UnicodeError, json.JSONDecodeError, ValueError) as error:
        raise ValueError(f"{label} ist kein gueltiges, eindeutiges JSON.") from error


def _load_object(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    if not path.is_file() or _is_reparse(path):
        raise ValueError(f"{label} fehlt oder ist unsicher: {path}")
    data = path.read_bytes()
    value = strict_json_bytes(data, label)
    if not isinstance(value, dict):
        raise ValueError(f"{label} muss ein JSON-Objekt sein.")
    return value, data


def _require_sha256(value: object, label: str) -> str:
    text = str(value or "")
    if not SHA256_PATTERN.fullmatch(text):
        raise ValueError(f"{label} ist keine SHA-256-Pruefsumme.")
    return text


def _is_reparse(path: Path) -> bool:
    try:
        return provenance_tools.prepare_tools._is_reparse_or_symlink(path)
    except OSError:
        return True


def _path_is_within(path: Path, root: Path) -> bool:
    try:
        Path(os.path.abspath(path)).relative_to(Path(os.path.abspath(root)))
        return True
    except ValueError:
        return False


def _safe_relative_path(path: Path, root: Path, label: str) -> str:
    absolute = Path(os.path.abspath(path))
    absolute_root = Path(os.path.abspath(root))
    if not _path_is_within(absolute, absolute_root):
        raise ValueError(f"{label} liegt ausserhalb des Knowledge-Roots.")
    if os.path.normcase(os.path.realpath(absolute)) != os.path.normcase(str(absolute)):
        raise ValueError(f"{label} ist verknuepft oder unsicher.")
    return absolute.relative_to(absolute_root).as_posix()


def _utc_text(value: datetime) -> str:
    if value.tzinfo is None or value.utcoffset() is None:
        raise ValueError("Erstellungszeitpunkt braucht eine Zeitzone.")
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def _box_payload(box: scoring.Box | provenance_tools.HoldoutBox) -> dict[str, float]:
    return {
        "x_center": float(box.x_center),
        "y_center": float(box.y_center),
        "width": float(box.width),
        "height": float(box.height),
    }


def _ground_truth_payload(
    instance: provenance_tools.HoldoutInstance,
    descriptions: Mapping[str, str],
) -> dict[str, object]:
    return {
        "sample_id": instance.sample_id,
        "code": instance.code,
        "description": descriptions.get(instance.sample_id, ""),
        "class_id": instance.class_id,
        "class_name": instance.class_name,
        "box": _box_payload(instance.box),
    }


def _prediction_payload(
    prediction: evaluation_tools.RawDetection,
) -> dict[str, object]:
    return {
        "prediction_id": prediction.prediction_id,
        "class_id": prediction.class_id,
        "class_name": prediction.class_name,
        "confidence": prediction.confidence,
        "box": _box_payload(prediction.box),
    }


def _load_descriptions(provenance: object) -> dict[str, str]:
    samples_path = getattr(provenance, "current_samples_path", None)
    expected_sha = getattr(provenance, "current_samples_sha256", None)
    if not isinstance(samples_path, Path) or not isinstance(expected_sha, str):
        return {}
    data = samples_path.read_bytes()
    if sha256_bytes(data) != expected_sha:
        raise ValueError("Aktuelle Trainingssamples wurden parallel veraendert.")
    rows = strict_json_bytes(data, "training_samples.json")
    if not isinstance(rows, list):
        raise ValueError("training_samples.json muss eine Liste sein.")
    result: dict[str, str] = {}
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("training_samples.json enthaelt eine ungueltige Zeile.")
        sample_id = str(row.get("SampleId") or "")
        if not sample_id:
            continue
        description = str(
            row.get("Beschreibung")
            or row.get("SourceReferenceDescription")
            or ""
        ).strip()
        result[sample_id] = description[:2_000]
    return result


def _validate_report_and_ledger(
    report_path: Path,
    ledger_path: Path,
    provenance: object,
) -> tuple[
    dict[str, Any],
    bytes,
    list[evaluation_tools.ImagePrediction],
    dict[str, Any],
    dict[str, Any],
    bytes,
]:
    report, report_bytes = _load_object(report_path, "Auswertungsbericht")
    ledger_document, ledger_bytes = _load_object(
        ledger_path,
        "Labelblinder Vorhersagebeleg",
    )
    ledger_sha = sha256_bytes(ledger_bytes)
    expected_bindings = dict(provenance.bindings())
    report_bindings = report.get("bindings")
    if not isinstance(report_bindings, dict):
        raise ValueError("Bericht besitzt keine gueltigen Bindings.")
    expected_report_bindings = {
        **expected_bindings,
        "prediction_ledger_sha256": ledger_sha,
        "prediction_receipt_sha256": ledger_document.get(
            "prediction_receipt_sha256"
        ),
    }
    if report_bindings != expected_report_bindings:
        raise ValueError("Bericht, Ledger und Provenienz-Bindings widersprechen sich.")
    if (
        report.get("schema_version") != evaluation_tools.SCHEMA_VERSION
        or report.get("purpose") != evaluation_tools.EVALUATION_PURPOSE
        or report.get("status") != evaluation_tools.EVALUATION_STATUS
    ):
        raise ValueError("Auswertungsbericht hat ein falsches Schema oder Status.")
    if (
        ledger_document.get("schema_version") != evaluation_tools.SCHEMA_VERSION
        or ledger_document.get("purpose") != evaluation_tools.PREDICTION_PURPOSE
        or ledger_document.get("bindings") != expected_bindings
    ):
        raise ValueError("Ledger hat ein falsches Schema oder andere Eingaben.")

    snapshots = evaluation_tools.load_image_snapshots(provenance)
    sealed_predictions, protocol = evaluation_tools.load_prediction_ledger(
        ledger_path,
        ledger_sha,
        provenance,
        snapshots,
    )
    technical = [
        item for item in sealed_predictions if item.technical_error is not None
    ]
    if technical:
        raise ValueError(
            "Technische Inferenzfehler duerfen keine diagnostische Queue erzeugen."
        )
    if report.get("protocol") != protocol:
        raise ValueError("Bericht und Ledger verwenden ein anderes Protokoll.")

    truths, predictions = evaluation_tools._to_scoring_inputs(
        provenance,
        sealed_predictions,
    )
    metrics = scoring.score_predictions(
        truths,
        predictions,
        {index: name for index, name in enumerate(provenance.classes)},
        iou_threshold=evaluation_tools.IOU_THRESHOLD,
    )
    if report.get("metrics") != metrics:
        raise ValueError("Bericht-Metriken stimmen nicht mit dem gebundenen Ledger.")
    assessment = report.get("release_assessment")
    if (
        not isinstance(assessment, dict)
        or assessment.get("release_qualified") is not False
        or assessment.get("auto_activation_allowed") is not False
        or assessment.get("model_activated") is not False
    ):
        raise ValueError("Bericht behauptet eine unzulaessige Modellfreigabe.")
    return (
        report,
        report_bytes,
        sealed_predictions,
        protocol,
        metrics,
        ledger_bytes,
    )


@dataclass(frozen=True)
class SourceSnapshot:
    path: Path
    sha256: str
    size_bytes: int


@dataclass(frozen=True)
class QueuePlan:
    queue_id: str
    semantic_payload: dict[str, Any]
    target_root: Path
    manifest: dict[str, Any]
    candidates: tuple[dict[str, Any], ...]
    source_snapshots: tuple[SourceSnapshot, ...]


def _source_snapshots(
    provenance: object,
    report_path: Path,
    ledger_path: Path,
) -> tuple[SourceSnapshot, ...]:
    paths: set[Path] = {Path(report_path), Path(ledger_path)}
    for attribute in (
        "candidate_manifest_path",
        "weights_path",
        "dataset_manifest",
        "registry_path",
        "detect_all_receipt_path",
        "base_audit_path",
        "current_audit_path",
        "current_samples_path",
        "class_map_path",
        "migration_path",
    ):
        value = getattr(provenance, attribute, None)
        if isinstance(value, Path):
            paths.add(value)
    for image in provenance.eligible_images:
        paths.add(Path(image.image_path))
    snapshots: list[SourceSnapshot] = []
    for path in sorted(paths, key=lambda value: os.path.normcase(str(value))):
        if not path.is_file() or _is_reparse(path):
            raise ValueError(f"Gebundene Quelldatei fehlt oder ist unsicher: {path}")
        snapshots.append(SourceSnapshot(path, sha256_file(path), path.stat().st_size))
    return tuple(snapshots)


def _case_id(case_type: str, image_id: str, anchor_id: str) -> str:
    digest = sha256_bytes(
        canonical_json_bytes([case_type, image_id, anchor_id])
    )
    return f"dgf-{digest[:24]}"


def queue_semantic_payload(
    manifest: Mapping[str, object],
    candidates: Sequence[Mapping[str, object]],
) -> dict[str, object]:
    """Liefert die einzige kanonische Grundlage fuer die Queue-ID."""

    semantic = {
        field: manifest[field]
        for field in (
            "schema_version",
            "purpose",
            "role",
            "frozen",
            "bindings",
            "policy",
            "summary",
        )
    }
    semantic["candidates"] = sorted(
        (dict(item) for item in candidates),
        key=lambda item: str(item["id"]),
    )
    return semantic


def build_queue_plan(
    report_path: str | Path,
    ledger_path: str | Path,
    provenance: object,
    knowledge_root: str | Path,
    *,
    created_utc: datetime | None = None,
) -> QueuePlan:
    root = Path(os.path.abspath(knowledge_root))
    report_file = Path(os.path.abspath(report_path))
    ledger_file = Path(os.path.abspath(ledger_path))
    if not root.is_dir() or _is_reparse(root):
        raise ValueError("Knowledge-Root fehlt oder ist unsicher.")
    reports_root = root / "training" / "reports"
    if not _path_is_within(report_file, reports_root) or not _path_is_within(
        ledger_file,
        reports_root,
    ):
        raise ValueError("Bericht und Ledger muessen im gebundenen Berichtsordner liegen.")

    (
        report,
        report_bytes,
        sealed_predictions,
        protocol,
        metrics,
        ledger_bytes,
    ) = _validate_report_and_ledger(
        report_file,
        ledger_file,
        provenance,
    )
    descriptions = _load_descriptions(provenance)
    images_by_id = {image.image_id: image for image in provenance.eligible_images}
    predictions_by_image = {
        image.image_id: {item.prediction_id: item for item in image.detections}
        for image in sealed_predictions
    }
    exact_truth_ids = {
        str(item["sample_id"]) for item in metrics["exact_matches"]
    }
    geometry_by_truth = {
        str(item["sample_id"]): item for item in metrics["geometry"]["matches"]
    }
    geometry_prediction_keys = {
        (str(item["image_id"]), str(item["prediction_id"]))
        for item in metrics["geometry"]["matches"]
    }

    candidates: list[dict[str, Any]] = []
    for image in sorted(provenance.eligible_images, key=lambda item: item.image_id):
        frame_path = _safe_relative_path(
            Path(image.image_path),
            root,
            f"Goldbild {image.image_id}",
        )
        all_ground_truths = [
            _ground_truth_payload(instance, descriptions)
            for instance in sorted(image.instances, key=lambda item: item.sample_id)
        ]
        image_predictions = predictions_by_image.get(image.image_id, {})
        all_predictions = [
            _prediction_payload(item)
            for item in sorted(
                image_predictions.values(),
                key=lambda value: value.prediction_id,
            )
        ]
        for instance in sorted(image.instances, key=lambda item: item.sample_id):
            if instance.sample_id in exact_truth_ids:
                continue
            geometry = geometry_by_truth.get(instance.sample_id)
            prediction = None
            iou = None
            case_type = "missed"
            if geometry is not None:
                prediction = image_predictions.get(str(geometry["prediction_id"]))
                if prediction is None:
                    raise ValueError("Geometrie-Matching verweist auf unbekannte Prediction.")
                iou = float(geometry["iou"])
                case_type = "wrong_class"
            ground_truth = _ground_truth_payload(instance, descriptions)
            prediction_payload = (
                _prediction_payload(prediction) if prediction is not None else None
            )
            candidates.append(
                {
                    "id": _case_id(case_type, image.image_id, instance.sample_id),
                    "image_id": image.image_id,
                    "frame_path": frame_path,
                    "source_sha256": image.image_sha256,
                    "holding_key": image.holding_key,
                    "physical_holding_key": image.physical_holding_key,
                    "case_type": case_type,
                    "error_type": case_type,
                    "expected_class_id": instance.class_id,
                    "expected_class_name": instance.class_name,
                    "predicted_class_id": (
                        prediction.class_id if prediction is not None else None
                    ),
                    "predicted_class_name": (
                        prediction.class_name if prediction is not None else None
                    ),
                    "sample_id": instance.sample_id,
                    "prediction_id": (
                        prediction.prediction_id if prediction is not None else None
                    ),
                    "ground_truth": ground_truth,
                    "prediction": prediction_payload,
                    "gold_instances": all_ground_truths,
                    "predictions": all_predictions,
                    "iou": iou,
                    "status": "pending_review",
                }
            )

        for prediction in sorted(
            image_predictions.values(), key=lambda value: value.prediction_id
        ):
            if (image.image_id, prediction.prediction_id) in geometry_prediction_keys:
                continue
            payload = _prediction_payload(prediction)
            candidates.append(
                {
                    "id": _case_id(
                        "extra_prediction",
                        image.image_id,
                        prediction.prediction_id,
                    ),
                    "image_id": image.image_id,
                    "frame_path": frame_path,
                    "source_sha256": image.image_sha256,
                    "holding_key": image.holding_key,
                    "physical_holding_key": image.physical_holding_key,
                    "case_type": "extra_prediction",
                    "error_type": "extra_prediction",
                    "expected_class_id": None,
                    "expected_class_name": None,
                    "predicted_class_id": prediction.class_id,
                    "predicted_class_name": prediction.class_name,
                    "sample_id": None,
                    "prediction_id": prediction.prediction_id,
                    "ground_truth": None,
                    "prediction": payload,
                    "gold_instances": all_ground_truths,
                    "predictions": all_predictions,
                    "iou": None,
                    "status": "pending_review",
                }
            )

    candidates.sort(
        key=lambda item: (
            int(item["expected_class_id"])
            if item["expected_class_id"] is not None
            else 10_000,
            {"wrong_class": 0, "missed": 1, "extra_prediction": 2}[
                str(item["case_type"])
            ],
            str(item["image_id"]),
            str(item["id"]),
        )
    )
    if not candidates:
        raise ValueError("Der gebundene Bericht enthaelt keine Fehlfaelle.")
    if len({str(item["id"]) for item in candidates}) != len(candidates):
        raise ValueError("Die diagnostische Queue besitzt doppelte Fall-IDs.")

    counts = Counter(str(item["case_type"]) for item in candidates)
    summary = {
        "cases": len(candidates),
        "images": len({str(item["image_id"]) for item in candidates}),
        "wrong_class": counts["wrong_class"],
        "missed": counts["missed"],
        "extra_prediction": counts["extra_prediction"],
    }
    report_sha = sha256_bytes(report_bytes)
    ledger_sha = sha256_bytes(ledger_bytes)
    bindings = {
        **dict(provenance.bindings()),
        "evaluation_report_path": _safe_relative_path(
            report_file, root, "Auswertungsbericht"
        ),
        "evaluation_report_sha256": report_sha,
        "prediction_ledger_path": _safe_relative_path(
            ledger_file, root, "Vorhersagebeleg"
        ),
        "prediction_ledger_sha256": ledger_sha,
        "prediction_receipt_sha256": report["bindings"][
            "prediction_receipt_sha256"
        ],
        "confidence_threshold": protocol["confidence_threshold"],
        "image_size": protocol["image_size"],
        "iou_threshold": protocol["iou_threshold"],
        "queue_builder_sha256": sha256_file(Path(__file__).resolve()),
    }
    for attribute, binding_name in (
        ("current_audit_path", "current_gold_audit_path"),
        ("current_samples_path", "current_training_samples_path"),
    ):
        value = getattr(provenance, attribute, None)
        if isinstance(value, Path) and _path_is_within(value, root):
            bindings[binding_name] = _safe_relative_path(value, root, binding_name)

    policy = {
        "training_eligible": False,
        "training_export_allowed": False,
        "source_mutation_allowed": False,
        "image_copies_created": False,
    }
    semantic_source = {
        "schema_version": SCHEMA_VERSION,
        "purpose": QUEUE_PURPOSE,
        "role": QUEUE_ROLE,
        "frozen": True,
        "bindings": bindings,
        "policy": policy,
        "summary": summary,
    }
    semantic = queue_semantic_payload(semantic_source, candidates)
    queue_id = sha256_bytes(canonical_json_bytes(semantic))
    created = _utc_text(created_utc or datetime.now(timezone.utc))
    manifest = {
        "schema_version": SCHEMA_VERSION,
        "purpose": QUEUE_PURPOSE,
        "role": QUEUE_ROLE,
        "frozen": True,
        "created_utc": created,
        "warning": (
            "NUR DIAGNOSE. NIE ALS TRAINING, GOLD, FEW-SHOT ODER "
            "MODELLFREIGABE VERWENDEN."
        ),
        "bindings": bindings,
        "policy": policy,
        "summary": summary,
        "queue_id": queue_id,
    }
    target = (
        root
        / "eval_review"
        / "detect_gold_failure_review"
        / "queues"
        / f"{QUEUE_PREFIX}{queue_id[:12]}"
    )
    return QueuePlan(
        queue_id=queue_id,
        semantic_payload=semantic,
        target_root=target,
        manifest=manifest,
        candidates=tuple(candidates),
        source_snapshots=_source_snapshots(
            provenance,
            report_file,
            ledger_file,
        ),
    )


def _assert_sources_unchanged(plan: QueuePlan) -> None:
    for item in plan.source_snapshots:
        if (
            not item.path.is_file()
            or _is_reparse(item.path)
            or item.path.stat().st_size != item.size_bytes
            or sha256_file(item.path) != item.sha256
        ):
            raise ValueError(f"Gebundene Quelle wurde veraendert: {item.path}")


def _write_new_file(path: Path, data: bytes) -> None:
    with path.open("xb") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())


def _existing_queue_matches(
    target: Path,
    manifest_bytes: bytes,
    candidates_bytes: bytes,
) -> bool:
    try:
        return (
            target.is_dir()
            and not _is_reparse(target)
            and {item.name for item in target.iterdir()}
            == {"_manifest.json", "_candidates.json"}
            and (target / "_manifest.json").read_bytes() == manifest_bytes
            and (target / "_candidates.json").read_bytes() == candidates_bytes
        )
    except OSError:
        return False


def publish_queue(plan: QueuePlan) -> Path:
    _assert_sources_unchanged(plan)
    manifest_bytes = pretty_json_bytes(plan.manifest)
    candidates_bytes = pretty_json_bytes(list(plan.candidates))
    target = Path(os.path.abspath(plan.target_root))
    if target.exists() or target.is_symlink():
        if _existing_queue_matches(target, manifest_bytes, candidates_bytes):
            return target
        raise FileExistsError(f"Queue-Ziel existiert bereits: {target}")

    parent = target.parent
    parent.mkdir(parents=True, exist_ok=True)
    if _is_reparse(parent) or os.path.normcase(os.path.realpath(parent)) != os.path.normcase(
        str(parent)
    ):
        raise ValueError("Queue-Ausgabeordner ist unsicher.")
    staging = parent / f".{target.name}.{uuid.uuid4().hex}.staging"
    staging.mkdir()
    try:
        _write_new_file(staging / "_manifest.json", manifest_bytes)
        _write_new_file(staging / "_candidates.json", candidates_bytes)
        if (
            (staging / "_manifest.json").read_bytes() != manifest_bytes
            or (staging / "_candidates.json").read_bytes() != candidates_bytes
        ):
            raise OSError("Queue-Staging konnte nicht verifiziert werden.")
        _assert_sources_unchanged(plan)
        if target.exists() or target.is_symlink():
            raise FileExistsError(f"Queue-Ziel wurde parallel angelegt: {target}")
        os.rename(staging, target)
    finally:
        if staging.exists():
            for name in ("_manifest.json", "_candidates.json"):
                try:
                    (staging / name).unlink(missing_ok=True)
                except OSError:
                    pass
            try:
                staging.rmdir()
            except OSError:
                pass
    return target


def _case_field(case: Mapping[str, object], name: str) -> object:
    if name in case:
        return case[name]
    aliases = {
        "error_type": "case_type",
    }
    alias = aliases.get(name)
    return case.get(alias) if alias else None


def _class_pair(
    case: Mapping[str, object],
    prefix: str,
) -> tuple[int, str] | None:
    raw_id = _case_field(case, f"{prefix}_class_id")
    raw_name = _case_field(case, f"{prefix}_class_name")
    if raw_id is None and raw_name is None:
        return None
    if isinstance(raw_id, bool) or not isinstance(raw_id, int) or raw_id < 0:
        raise ValueError(f"{prefix}-Klassen-ID ist ungueltig.")
    name = str(raw_name or "")
    if not name:
        raise ValueError(f"{prefix}-Klassenname fehlt.")
    return raw_id, name


def build_collection_plan(
    queue: Mapping[str, object],
    review: Mapping[str, object],
    *,
    queue_sha256: str,
    review_sha256: str,
) -> dict[str, object]:
    queue_sha = _require_sha256(queue_sha256, "Queue-SHA")
    review_sha = _require_sha256(review_sha256, "Review-SHA")
    if queue.get("schema_version") != SCHEMA_VERSION or queue.get("purpose") not in {
        QUEUE_PURPOSE,
        LEGACY_COLLECTION_QUEUE_PURPOSE,
    }:
        raise ValueError("Queue-Schema oder Zweck ist ungueltig.")
    if review.get("schema_version") != SCHEMA_VERSION or review.get("purpose") not in {
        REVIEW_PURPOSE,
        LEGACY_REVIEW_PURPOSE,
    }:
        raise ValueError("Review-Schema oder Zweck ist ungueltig.")
    if review.get("queue_id") != queue.get("queue_id"):
        raise ValueError("Review gehoert zu einer anderen Queue.")
    raw_cases = queue.get("cases")
    raw_decisions = review.get("decisions")
    if not isinstance(raw_cases, list) or not isinstance(raw_decisions, dict):
        raise ValueError("Queue oder Review enthaelt keine gueltigen Faelle.")
    cases: dict[str, Mapping[str, object]] = {}
    for value in raw_cases:
        if not isinstance(value, dict):
            raise ValueError("Queue enthaelt einen ungueltigen Fall.")
        case_id = str(value.get("id") or "")
        if not case_id or case_id in cases:
            raise ValueError("Queue enthaelt leere oder doppelte Fall-IDs.")
        cases[case_id] = value
    if set(raw_decisions) != set(cases):
        raise ValueError("Die Review ist nicht vollstaendig.")

    positive: dict[tuple[int, str], Counter[str]] = defaultdict(Counter)
    negative: Counter[tuple[int, str]] = Counter()
    confusion: Counter[tuple[int, str, int, str]] = Counter()
    audit: Counter[tuple[str, int | None, str | None, int | None, str | None]] = Counter()
    decision_counts: Counter[str] = Counter()

    for case_id in sorted(cases):
        case = cases[case_id]
        raw_decision = raw_decisions[case_id]
        if not isinstance(raw_decision, dict):
            raise ValueError("Review-Entscheidung ist ungueltig.")
        decision = str(raw_decision.get("decision") or "")
        if decision not in VALID_REVIEW_DECISIONS:
            raise ValueError("Review-Entscheidung ist ungueltig.")
        decision_counts[decision] += 1
        error_type = str(_case_field(case, "error_type") or "")
        if error_type not in VALID_CASE_TYPES:
            raise ValueError("Queue enthaelt einen unbekannten Fehlertyp.")
        expected = _class_pair(case, "expected")
        predicted = _class_pair(case, "predicted")

        if decision == "exclude_uncertain":
            continue
        if decision == "gold_suspect":
            audit[
                (
                    error_type,
                    expected[0] if expected else None,
                    expected[1] if expected else None,
                    predicted[0] if predicted else None,
                    predicted[1] if predicted else None,
                )
            ] += 1
            continue
        if error_type == "missed":
            if expected is None:
                raise ValueError("Bestaetigter Gold-Fehler besitzt keine Sollklasse.")
            positive[expected][error_type] += 1
        elif error_type == "wrong_class":
            if expected is None:
                raise ValueError("Bestaetigter Gold-Fehler besitzt keine Sollklasse.")
            if predicted is None:
                raise ValueError("Verwechslungsfall besitzt keine Vorhersageklasse.")
            positive[expected][error_type] += 1
            confusion[(expected[0], expected[1], predicted[0], predicted[1])] += 1
        elif expected is None:
            if predicted is None:
                raise ValueError("Zusatzvorhersage besitzt keine Vorhersageklasse.")
            negative[predicted] += 1
        else:
            if predicted is None:
                raise ValueError("Verwechslungsfall besitzt keine Vorhersageklasse.")
            confusion[(expected[0], expected[1], predicted[0], predicted[1])] += 1

    positive_rows = [
        {
            "class_id": class_id,
            "class_name": class_name,
            "count": counts["missed"] + counts["wrong_class"],
            "reasons": {
                "missed": counts["missed"],
                "wrong_class": counts["wrong_class"],
            },
        }
        for (class_id, class_name), counts in sorted(positive.items())
    ]
    negative_rows = [
        {"class_id": key[0], "class_name": key[1], "count": count}
        for key, count in sorted(negative.items())
    ]
    confusion_rows = [
        {
            "expected_class_id": key[0],
            "expected_class_name": key[1],
            "predicted_class_id": key[2],
            "predicted_class_name": key[3],
            "count": count,
        }
        for key, count in sorted(confusion.items())
    ]
    audit_rows = [
        {
            "error_type": key[0],
            "expected_class_id": key[1],
            "expected_class_name": key[2],
            "predicted_class_id": key[3],
            "predicted_class_name": key[4],
            "count": count,
        }
        for key, count in sorted(
            audit.items(),
            key=lambda item: tuple("" if value is None else str(value) for value in item[0]),
        )
    ]
    return {
        "schema_version": SCHEMA_VERSION,
        "purpose": COLLECTION_PURPOSE,
        "mode": "aggregate_only",
        "warning": (
            "Dieser Holdout wurde fuer die Modellentwicklung ausgewertet und darf "
            "nach darauf basierenden Aenderungen nicht erneut als unabhaengige "
            "Release-Abnahme verwendet werden. Nur neue Bilder sammeln; keine "
            "Holdout-Bilder ins Training uebernehmen."
        ),
        "bindings": {
            "queue_sha256": queue_sha,
            "review_sha256": review_sha,
        },
        "counts": {
            "reviewed": len(cases),
            "confirmed_model_error": decision_counts["confirmed_model_error"],
            "gold_suspect": decision_counts["gold_suspect"],
            "exclude_uncertain": decision_counts["exclude_uncertain"],
        },
        "positive_class_targets": positive_rows,
        "negative_class_targets": negative_rows,
        "confusion_targets": confusion_rows,
        "annotation_audit": audit_rows,
    }


def _parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Sichere diagnostische Queue fuer Detect-Gold-Fehlfaelle"
    )
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--gold-audit", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--ledger", type=Path, required=True)
    parser.add_argument(
        "--execute",
        action="store_true",
        help="Queue neu und atomar veroeffentlichen.",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    provenance = provenance_tools.load_and_validate(
        args.knowledge_root,
        args.candidate,
        args.gold_audit,
    )
    plan = build_queue_plan(
        args.report,
        args.ledger,
        provenance,
        args.knowledge_root,
    )
    summary = plan.manifest["summary"]
    print(f"Diagnose-Queue: {plan.queue_id}")
    print(
        "Fehlfaelle: "
        f"{summary['cases']} "
        f"(verpasst {summary['missed']}, falsche Klasse {summary['wrong_class']}, "
        f"zusaetzliche Box {summary['extra_prediction']})"
    )
    print(f"Ziel: {plan.target_root}")
    if not args.execute:
        print("Nur geprueft. Mit --execute wird die neue Queue geschrieben.")
        return 0
    target = publish_queue(plan)
    print(f"Queue veroeffentlicht: {target}")
    print("Keine Bilder kopiert; Gold, KB und Training blieben unveraendert.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
