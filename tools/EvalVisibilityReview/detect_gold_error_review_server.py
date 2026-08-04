from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import sys
import webbrowser
from pathlib import Path
from typing import Any, Sequence


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
TRAINING_SCRIPTS = REPOSITORY_ROOT / "training" / "scripts"
if str(TRAINING_SCRIPTS) not in sys.path:
    sys.path.insert(0, str(TRAINING_SCRIPTS))

import detect_gold_error_review as queue_tools

try:
    from tools.EvalVisibilityReview.bcc_release_holdout_review_server import (
        BccReleaseHoldoutReviewStore,
        _VerifiedImage,
        _image_content_type,
        _is_reparse_point,
        _path_is_within,
        _required_text,
        _validate_image_signature,
        create_server,
    )
except ModuleNotFoundError:
    from bcc_release_holdout_review_server import (  # type: ignore[no-redef]
        BccReleaseHoldoutReviewStore,
        _VerifiedImage,
        _image_content_type,
        _is_reparse_point,
        _path_is_within,
        _required_text,
        _validate_image_signature,
        create_server,
    )


REVIEW_PURPOSE = "detect_gold_failure_review"
VALID_DECISIONS = frozenset(
    {"confirmed_model_error", "gold_suspect", "exclude_uncertain"}
)
VALID_CASE_TYPES = frozenset({"wrong_class", "missed", "extra_prediction"})
EXPECTED_QUEUE_FILES = frozenset({"_manifest.json", "_candidates.json"})
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
SAFE_ID_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
PROVENANCE_BINDING_FIELDS = frozenset(
    {
        "candidate_id",
        "candidate_manifest_sha256",
        "weights_sha256",
        "dataset_plan_id",
        "dataset_manifest_sha256",
        "dataset_receipt_sha256",
        "registry_sha256",
        "detect_all_receipt_sha256",
        "base_gold_audit_sha256",
        "base_training_samples_sha256",
        "current_gold_audit_sha256",
        "current_training_samples_sha256",
        "class_map_sha256",
        "migration_sha256",
        "vsa_manifest_sha256",
        "base_model_training_inventory_available",
    }
)
QUEUE_BINDING_FIELDS = frozenset(
    {
        *PROVENANCE_BINDING_FIELDS,
        "evaluation_report_path",
        "evaluation_report_sha256",
        "prediction_ledger_path",
        "prediction_ledger_sha256",
        "prediction_receipt_sha256",
        "confidence_threshold",
        "image_size",
        "iou_threshold",
        "queue_builder_sha256",
        "current_gold_audit_path",
        "current_training_samples_path",
    }
)
REVIEW_BINDING_FIELDS = (
    "evaluation_report_sha256",
    "prediction_ledger_sha256",
    "prediction_receipt_sha256",
    "candidate_manifest_sha256",
    "weights_sha256",
    "current_gold_audit_sha256",
    "current_training_samples_sha256",
    "class_map_sha256",
    "migration_sha256",
    "vsa_manifest_sha256",
)


def _load_json(path: Path, label: str) -> tuple[object, bytes]:
    if not path.is_file() or _is_reparse_point(path):
        raise ValueError(f"{label} fehlt oder ist unsicher.")
    body = path.read_bytes()
    return queue_tools.strict_json_bytes(body, label), body


def _sha_file(path: Path) -> str:
    return queue_tools.sha256_file(path)


def _require_sha(value: object, label: str) -> str:
    text = str(value or "")
    if not SHA256_PATTERN.fullmatch(text):
        raise ValueError(f"{label} ist keine gueltige SHA-256-Pruefsumme.")
    return text


def _require_safe_relative(value: object, label: str) -> Path:
    text = str(value or "")
    path = Path(text)
    if (
        not text
        or path.is_absolute()
        or path.drive
        or any(part in {"", ".", ".."} for part in path.parts)
    ):
        raise ValueError(f"{label} ist kein sicherer relativer Pfad.")
    return path


def _resolve_below(
    root: Path,
    relative_value: object,
    label: str,
    *,
    required_root: Path | None = None,
) -> Path:
    relative = _require_safe_relative(relative_value, label)
    path = Path(os.path.abspath(root / relative))
    boundary = Path(os.path.abspath(required_root or root))
    if not _path_is_within(path, boundary):
        raise ValueError(f"{label} liegt ausserhalb des erlaubten Ordners.")
    if (
        not path.is_file()
        or _is_reparse_point(path)
        or os.path.normcase(os.path.realpath(path)) != os.path.normcase(str(path))
    ):
        raise ValueError(f"{label} fehlt oder ist verknuepft.")
    return path


def _validate_box(value: object, label: str) -> dict[str, float]:
    if not isinstance(value, dict) or set(value) != {
        "x_center",
        "y_center",
        "width",
        "height",
    }:
        raise ValueError(f"{label} ist ungueltig.")
    result: dict[str, float] = {}
    for field in ("x_center", "y_center", "width", "height"):
        raw = value.get(field)
        if isinstance(raw, bool) or not isinstance(raw, (int, float)):
            raise ValueError(f"{label} enthaelt keine gueltigen Zahlen.")
        number = float(raw)
        if not math.isfinite(number):
            raise ValueError(f"{label} enthaelt keine endlichen Zahlen.")
        result[field] = number
    if result["width"] <= 0.0 or result["height"] <= 0.0:
        raise ValueError(f"{label} besitzt keine positive Flaeche.")
    if (
        result["x_center"] - result["width"] / 2.0 < -1e-9
        or result["y_center"] - result["height"] / 2.0 < -1e-9
        or result["x_center"] + result["width"] / 2.0 > 1.0 + 1e-9
        or result["y_center"] + result["height"] / 2.0 > 1.0 + 1e-9
    ):
        raise ValueError(f"{label} liegt ausserhalb des Bildes.")
    return result


def _validate_ground_truth(value: object) -> dict[str, object]:
    if not isinstance(value, dict):
        raise ValueError("Gold-Eintrag ist ungueltig.")
    allowed = {"sample_id", "code", "description", "class_id", "class_name", "box"}
    required = {"sample_id", "code", "class_id", "class_name", "box"}
    if not required <= set(value) or set(value) - allowed:
        raise ValueError("Gold-Eintrag hat fremde oder fehlende Felder.")
    class_id = value.get("class_id")
    if isinstance(class_id, bool) or not isinstance(class_id, int) or class_id < 0:
        raise ValueError("Gold-Klassen-ID ist ungueltig.")
    sample_id = _required_text(value.get("sample_id"), "Sample-ID", 256)
    code = _required_text(value.get("code"), "VSA-Code", 128)
    class_name = _required_text(value.get("class_name"), "Gold-Klasse", 128)
    description = str(value.get("description") or "").strip()
    if len(description) > 2_000:
        raise ValueError("Gold-Klartext ist zu lang.")
    return {
        "sample_id": sample_id,
        "code": code,
        "description": description,
        "class_id": class_id,
        "class_name": class_name,
        "box": _validate_box(value.get("box"), "Gold-Box"),
    }


def _validate_prediction(value: object) -> dict[str, object]:
    if not isinstance(value, dict) or set(value) != {
        "prediction_id",
        "class_id",
        "class_name",
        "confidence",
        "box",
    }:
        raise ValueError("KI-Vorhersage ist ungueltig.")
    class_id = value.get("class_id")
    confidence = value.get("confidence")
    if isinstance(class_id, bool) or not isinstance(class_id, int) or class_id < 0:
        raise ValueError("KI-Klassen-ID ist ungueltig.")
    if (
        isinstance(confidence, bool)
        or not isinstance(confidence, (int, float))
        or not math.isfinite(float(confidence))
        or not 0.0 <= float(confidence) <= 1.0
    ):
        raise ValueError("KI-Konfidenz ist ungueltig.")
    return {
        "prediction_id": _required_text(
            value.get("prediction_id"), "Prediction-ID", 256
        ),
        "class_id": class_id,
        "class_name": _required_text(value.get("class_name"), "KI-Klasse", 128),
        "confidence": float(confidence),
        "box": _validate_box(value.get("box"), "KI-Box"),
    }


def _validate_protocol_bindings(bindings: dict[str, object]) -> None:
    confidence = bindings.get("confidence_threshold")
    image_size = bindings.get("image_size")
    iou = bindings.get("iou_threshold")
    if confidence is not None and confidence != 0.25:
        raise ValueError("Queue verwendet eine andere Konfidenzschwelle.")
    if image_size is not None and image_size != 1280:
        raise ValueError("Queue verwendet eine andere Bildgroesse.")
    if iou is not None and iou != 0.5:
        raise ValueError("Queue verwendet eine andere IoU-Schwelle.")


def _find_file_by_hash(roots: Sequence[Path], expected_sha: str, label: str) -> Path:
    matches: list[Path] = []
    for root in roots:
        if not root.is_dir() or _is_reparse_point(root):
            continue
        for path in sorted(root.glob("*.json"), key=lambda item: item.name.casefold()):
            if path.is_file() and not _is_reparse_point(path) and _sha_file(path) == expected_sha:
                matches.append(path)
    if not matches:
        raise ValueError(f"{label} mit gebundener SHA wurde nicht gefunden.")
    return matches[0]


def _validate_upstream_bindings(
    knowledge_root: Path,
    bindings: dict[str, object],
) -> None:
    if not PROVENANCE_BINDING_FIELDS <= set(bindings) or set(bindings) - QUEUE_BINDING_FIELDS:
        raise ValueError("Queue-Bindings haben fremde oder fehlende Felder.")
    for field in PROVENANCE_BINDING_FIELDS - {
        "candidate_id",
        "base_model_training_inventory_available",
    }:
        _require_sha(bindings.get(field), field)
    for field in (
        "evaluation_report_sha256",
        "prediction_ledger_sha256",
        "prediction_receipt_sha256",
        "queue_builder_sha256",
    ):
        if field in bindings:
            _require_sha(bindings.get(field), field)
    if bindings.get("base_model_training_inventory_available") is not False:
        raise ValueError("Basis-Modellinventar-Bindung ist ungueltig.")
    _validate_protocol_bindings(bindings)

    reports_root = knowledge_root / "training" / "reports"
    report_path = _resolve_below(
        knowledge_root,
        bindings.get("evaluation_report_path"),
        "Auswertungsbericht",
        required_root=reports_root,
    )
    ledger_path = _resolve_below(
        knowledge_root,
        bindings.get("prediction_ledger_path"),
        "Vorhersage-Ledger",
        required_root=reports_root,
    )
    if _sha_file(report_path) != bindings.get("evaluation_report_sha256"):
        raise ValueError("Auswertungsbericht-SHA stimmt nicht.")
    if _sha_file(ledger_path) != bindings.get("prediction_ledger_sha256"):
        raise ValueError("Vorhersage-Ledger-SHA stimmt nicht.")
    report, _ = _load_json(report_path, "Auswertungsbericht")
    ledger, _ = _load_json(ledger_path, "Vorhersage-Ledger")
    if not isinstance(report, dict) or not isinstance(ledger, dict):
        raise ValueError("Bericht oder Ledger ist ungueltig.")
    provenance_bindings = {
        field: bindings[field] for field in PROVENANCE_BINDING_FIELDS
    }
    if (
        report.get("purpose") != "detect_gold_positive_holdout_evaluation"
        or ledger.get("purpose") != "detect_gold_positive_holdout_predictions"
        or ledger.get("bindings") != provenance_bindings
        or ledger.get("prediction_receipt_sha256")
        != bindings.get("prediction_receipt_sha256")
    ):
        raise ValueError("Bericht oder Vorhersagebeleg hat andere Bindings.")
    expected_report_bindings = {
        **provenance_bindings,
        "prediction_ledger_sha256": bindings["prediction_ledger_sha256"],
        "prediction_receipt_sha256": bindings["prediction_receipt_sha256"],
    }
    if report.get("bindings") != expected_report_bindings:
        raise ValueError("Auswertungsbericht hat andere Bindings.")
    predictions = ledger.get("predictions")
    if not isinstance(predictions, list) or any(
        not isinstance(row, dict) or row.get("technical_error") is not None
        for row in predictions
    ):
        raise ValueError("Vorhersagebeleg enthaelt technische Fehler.")

    candidate_id = str(bindings.get("candidate_id") or "")
    if not SAFE_ID_PATTERN.fullmatch(candidate_id):
        raise ValueError("Kandidaten-ID ist ungueltig.")
    candidates_root = knowledge_root / "training" / "models" / "candidates"
    candidate_dir = Path(os.path.abspath(candidates_root / candidate_id))
    if (
        candidate_dir.parent != Path(os.path.abspath(candidates_root))
        or not candidate_dir.is_dir()
        or _is_reparse_point(candidate_dir)
    ):
        raise ValueError("Kandidatenordner fehlt oder ist unsicher.")
    candidate_manifest = candidate_dir / "candidate_manifest.json"
    if _sha_file(candidate_manifest) != bindings.get("candidate_manifest_sha256"):
        raise ValueError("Kandidatenmanifest-SHA stimmt nicht.")
    candidate_document, _ = _load_json(candidate_manifest, "Kandidatenmanifest")
    if not isinstance(candidate_document, dict):
        raise ValueError("Kandidatenmanifest ist ungueltig.")
    weights = candidate_document.get("weights")
    if not isinstance(weights, dict) or weights.get("candidate_sha256") != bindings.get(
        "weights_sha256"
    ):
        raise ValueError("Gewicht-SHA im Kandidatenmanifest stimmt nicht.")
    candidate_path = weights.get("candidate_path")
    if candidate_path:
        weights_path = Path(os.path.abspath(str(candidate_path)))
    else:
        weights_path = candidate_dir / "best.pt"
    if (
        weights_path.parent != candidate_dir
        or not weights_path.is_file()
        or _is_reparse_point(weights_path)
        or _sha_file(weights_path) != bindings.get("weights_sha256")
    ):
        raise ValueError("Gebundene Modellgewichte fehlen oder stimmen nicht.")

    samples_relative = bindings.get(
        "current_training_samples_path", "training_samples.json"
    )
    samples_path = _resolve_below(
        knowledge_root,
        samples_relative,
        "training_samples.json",
        required_root=knowledge_root,
    )
    if _sha_file(samples_path) != bindings.get("current_training_samples_sha256"):
        raise ValueError("Aktuelle Trainingssample-SHA stimmt nicht.")

    audit_relative = bindings.get("current_gold_audit_path")
    if audit_relative:
        audit_path = _resolve_below(
            knowledge_root,
            audit_relative,
            "Gold-Audit",
            required_root=reports_root,
        )
    else:
        audit_path = _find_file_by_hash(
            (reports_root,),
            str(bindings["current_gold_audit_sha256"]),
            "Gold-Audit",
        )
    if _sha_file(audit_path) != bindings.get("current_gold_audit_sha256"):
        raise ValueError("Gold-Audit-SHA stimmt nicht.")

    class_map_sha = str(bindings["class_map_sha256"])
    _find_file_by_hash(
        (
            knowledge_root / "training" / "class_maps",
            REPOSITORY_ROOT / "training" / "class_maps",
        ),
        class_map_sha,
        "Klassenkarte",
    )


def _validate_candidate_row(
    value: object,
    knowledge_root: Path,
) -> tuple[dict[str, object], _VerifiedImage]:
    if not isinstance(value, dict):
        raise ValueError("Queue-Kandidat ist ungueltig.")
    required = {
        "id",
        "image_id",
        "frame_path",
        "source_sha256",
        "holding_key",
        "physical_holding_key",
        "case_type",
        "ground_truth",
        "prediction",
        "iou",
        "status",
    }
    optional = {
        "error_type",
        "expected_class_id",
        "expected_class_name",
        "predicted_class_id",
        "predicted_class_name",
        "sample_id",
        "prediction_id",
        "gold_instances",
        "predictions",
    }
    if not required <= set(value) or set(value) - required - optional:
        raise ValueError("Queue-Kandidat hat fremde oder fehlende Felder.")
    case_id = str(value.get("id") or "")
    if not SAFE_ID_PATTERN.fullmatch(case_id):
        raise ValueError("Queue-Fall-ID ist ungueltig.")
    case_type = str(value.get("case_type") or "")
    if case_type not in VALID_CASE_TYPES or value.get("status") != "pending_review":
        raise ValueError("Queue-Falltyp oder Status ist ungueltig.")
    if value.get("error_type") not in (None, case_type):
        raise ValueError("Fehlertyp-Alias widerspricht dem Falltyp.")

    gold = (
        None
        if value.get("ground_truth") is None
        else _validate_ground_truth(value.get("ground_truth"))
    )
    prediction = (
        None
        if value.get("prediction") is None
        else _validate_prediction(value.get("prediction"))
    )
    if case_type == "wrong_class" and (gold is None or prediction is None):
        raise ValueError("Falsche Klasse braucht Gold- und KI-Box.")
    if case_type == "missed" and (gold is None or prediction is not None):
        raise ValueError("Verpasster Fall braucht nur eine Gold-Box.")
    if case_type == "extra_prediction" and (gold is not None or prediction is None):
        raise ValueError("Zusatzvorhersage braucht nur eine KI-Box.")
    raw_iou = value.get("iou")
    if case_type == "wrong_class":
        if (
            isinstance(raw_iou, bool)
            or not isinstance(raw_iou, (int, float))
            or not 0.0 <= float(raw_iou) <= 1.0
        ):
            raise ValueError("IoU des Verwechslungsfalls ist ungueltig.")
        iou: float | None = float(raw_iou)
    else:
        if raw_iou is not None:
            raise ValueError("Nur ein Geometrietreffer darf IoU besitzen.")
        iou = None

    source_sha = _require_sha(value.get("source_sha256"), "Bild-SHA")
    if value.get("image_id") != source_sha:
        raise ValueError("Bild-ID und Bild-SHA widersprechen sich.")
    gold_root = knowledge_root / "gold_frames"
    image_path = _resolve_below(
        knowledge_root,
        value.get("frame_path"),
        "Goldbild",
        required_root=gold_root,
    )
    body = image_path.read_bytes()
    if hashlib.sha256(body).hexdigest() != source_sha:
        raise ValueError("Goldbild-Hash stimmt nicht.")
    _validate_image_signature(body, image_path.suffix)

    if "gold_instances" in value:
        raw_gold = value.get("gold_instances")
        if not isinstance(raw_gold, list):
            raise ValueError("Gold-Overlayliste ist ungueltig.")
        for item in raw_gold:
            _validate_ground_truth(item)
    if "predictions" in value:
        raw_predictions = value.get("predictions")
        if not isinstance(raw_predictions, list):
            raise ValueError("KI-Overlayliste ist ungueltig.")
        for item in raw_predictions:
            _validate_prediction(item)

    public = dict(value)
    public["ground_truth"] = gold
    public["prediction"] = prediction
    public["iou"] = iou
    return public, _VerifiedImage(case_id, image_path, source_sha, len(body))


def _validate_failure_queue(
    knowledge_root: Path,
    queue_root: Path,
) -> tuple[
    str,
    str,
    str,
    tuple[_VerifiedImage, ...],
    dict[str, object],
    dict[str, dict[str, object]],
]:
    root = Path(os.path.abspath(queue_root))
    if (
        not root.is_dir()
        or _is_reparse_point(root)
        or {item.name for item in root.iterdir()} != EXPECTED_QUEUE_FILES
    ):
        raise ValueError("Diagnose-Queue fehlt oder besitzt fremde Dateien.")
    manifest_value, manifest_bytes = _load_json(root / "_manifest.json", "Queue-Manifest")
    candidates_value, candidates_bytes = _load_json(
        root / "_candidates.json", "Queue-Kandidaten"
    )
    if not isinstance(manifest_value, dict) or not isinstance(candidates_value, list):
        raise ValueError("Diagnose-Queue ist ungueltig.")
    expected_manifest_fields = {
        "schema_version",
        "purpose",
        "role",
        "frozen",
        "created_utc",
        "warning",
        "bindings",
        "policy",
        "summary",
        "queue_id",
    }
    if set(manifest_value) != expected_manifest_fields:
        raise ValueError("Queue-Manifest hat fremde oder fehlende Felder.")
    if (
        manifest_value.get("schema_version") != queue_tools.SCHEMA_VERSION
        or manifest_value.get("purpose") != queue_tools.QUEUE_PURPOSE
        or manifest_value.get("role") != queue_tools.QUEUE_ROLE
        or manifest_value.get("frozen") is not True
    ):
        raise ValueError("Queue-Zweck, Rolle oder Frozen-Status ist ungueltig.")
    policy = manifest_value.get("policy")
    allowed_policies = (
        {
            "training_eligible": False,
            "training_export_allowed": False,
            "source_mutation_allowed": False,
            "image_copies_created": False,
        },
        {
            "training_eligible": False,
            "export_allowed": False,
            "source_mutation_allowed": False,
            "image_copies_created": False,
        },
    )
    if policy not in allowed_policies:
        raise ValueError("Queue-Policy erlaubt Training oder Quellenmutation.")
    bindings = manifest_value.get("bindings")
    if not isinstance(bindings, dict):
        raise ValueError("Queue-Bindings fehlen.")
    _validate_upstream_bindings(knowledge_root, bindings)

    verified: list[_VerifiedImage] = []
    rows_by_id: dict[str, dict[str, object]] = {}
    for value in candidates_value:
        row, image = _validate_candidate_row(value, knowledge_root)
        if image.candidate_id in rows_by_id:
            raise ValueError("Queue besitzt doppelte Fall-IDs.")
        rows_by_id[image.candidate_id] = row
        verified.append(image)
    if not verified:
        raise ValueError("Diagnose-Queue ist leer.")
    counts = {
        case_type: sum(
            1 for item in rows_by_id.values() if item["case_type"] == case_type
        )
        for case_type in VALID_CASE_TYPES
    }
    summary = manifest_value.get("summary")
    valid_summaries = (
        {
            "cases": len(verified),
            "images": len({item.sha256 for item in verified}),
            **counts,
        },
        {"total": len(verified), **counts},
    )
    if summary not in valid_summaries:
        raise ValueError("Queue-Zusammenfassung stimmt nicht.")

    semantic = queue_tools.queue_semantic_payload(
        manifest_value,
        candidates_value,
    )
    expected_queue_id = hashlib.sha256(queue_tools.canonical_json_bytes(semantic)).hexdigest()
    queue_id = str(manifest_value.get("queue_id") or "")
    if queue_id != expected_queue_id:
        raise ValueError("Queue-ID stimmt nicht mit dem Inhalt ueberein.")
    return (
        queue_id,
        hashlib.sha256(manifest_bytes).hexdigest(),
        hashlib.sha256(candidates_bytes).hexdigest(),
        tuple(verified),
        dict(bindings),
        rows_by_id,
    )


class DetectGoldErrorReviewStore(BccReleaseHoldoutReviewStore):
    """Rein diagnostischer Review-Speicher fuer Mehrklassen-Fehlfaelle."""

    review_purpose = REVIEW_PURPOSE
    valid_decisions = VALID_DECISIONS
    identity_field = "queue_id"
    manifest_binding_field = "queue_manifest_sha256"
    candidate_binding_field = "candidates_sha256"

    def __init__(
        self,
        knowledge_root: str | Path,
        queue_root: str | Path,
        output_path: str | Path,
        reviewer: object,
        now_utc=None,
    ) -> None:
        self.knowledge_root = Path(os.path.abspath(knowledge_root))
        if not self.knowledge_root.is_dir() or _is_reparse_point(self.knowledge_root):
            raise ValueError("Knowledge-Root fehlt oder ist unsicher.")
        self._queue_bindings: dict[str, object] = {}
        self._rows_by_id: dict[str, dict[str, object]] = {}
        super().__init__(queue_root, output_path, reviewer, now_utc)

    def _validate_source(
        self,
        source_root: Path,
    ) -> tuple[str, str, str, tuple[_VerifiedImage, ...]]:
        (
            queue_id,
            manifest_sha,
            candidates_sha,
            images,
            bindings,
            rows_by_id,
        ) = _validate_failure_queue(self.knowledge_root, source_root)
        self._queue_bindings = bindings
        self._rows_by_id = rows_by_id
        return queue_id, manifest_sha, candidates_sha, images

    def _load_additional_review_bindings(self) -> dict[str, str]:
        values = {
            field: _require_sha(self._queue_bindings.get(field), field)
            for field in REVIEW_BINDING_FIELDS
        }
        values["role"] = queue_tools.QUEUE_ROLE
        return values

    def _public_row(self, candidate_id: str) -> dict[str, object]:
        row = self._rows_by_id[candidate_id]
        decision = self._decisions.get(candidate_id)
        return {
            **row,
            "decision": decision["decision"] if decision else None,
            "comment": decision["comment"] if decision else "",
            "image_url": f"/image?id={candidate_id}",
        }

    def html_template(self) -> str:
        return INDEX_HTML


INDEX_HTML = r"""<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>Detect-Gold · Fehlfall-Prüfung</title>
  <style>
    :root { color-scheme: dark; font-family: Segoe UI, Arial, sans-serif; }
    * { box-sizing: border-box; }
    body { margin: 0; background: #0f1724; color: #eef4ff; }
    header { margin: 10px; padding: 16px 20px; background:#1d2939; border:1px solid #35465d; border-radius:14px; display:flex; justify-content:space-between; gap:20px; align-items:center; }
    h1 { margin:0; font-size:22px; } .muted { color:#aebed2; }
    .warning { color:#ffd27b; margin-top:5px; font-size:13px; }
    main { display:grid; grid-template-columns:minmax(0,1fr) 360px; gap:14px; padding:10px; height:calc(100vh - 112px); }
    .viewer,.panel { background:#1d2939; border:1px solid #35465d; border-radius:14px; padding:16px; min-height:0; }
    .viewer { display:flex; align-items:center; justify-content:center; overflow:auto; }
    #stage { position:relative; display:inline-block; line-height:0; max-width:100%; max-height:100%; }
    #photo { display:block; max-width:100%; max-height:calc(100vh - 160px); object-fit:contain; }
    #overlay { position:absolute; inset:0; pointer-events:none; }
    .box { position:absolute; border:3px solid; filter:drop-shadow(0 0 2px #000); }
    .box.gold { border-color:#24df7c; } .box.gold.focus { border-color:#ffd23f; border-width:4px; }
    .box.pred { border-color:#4ea1ff; } .box.pred.focus { border-color:#ff4d5d; border-width:4px; }
    .tag { position:absolute; left:0; top:0; transform:translateY(-100%); line-height:1.2; padding:3px 5px; color:white; background:#111c; font-size:12px; white-space:nowrap; }
    .panel { overflow:auto; display:flex; flex-direction:column; gap:10px; }
    .type { font-size:18px; font-weight:700; }
    .card { padding:10px; border-radius:10px; background:#121d2d; border:1px solid #35465d; line-height:1.45; }
    .legend { display:flex; gap:13px; flex-wrap:wrap; font-size:13px; }
    .dot { display:inline-block; width:12px; height:12px; margin-right:5px; border:2px solid; }
    textarea { width:100%; min-height:78px; resize:vertical; background:#101827; color:#eef4ff; border:1px solid #52647b; border-radius:9px; padding:9px; }
    button { border:0; border-radius:9px; padding:11px 10px; font-weight:700; cursor:pointer; color:white; }
    .ok { background:#0ea96f; } .suspect { background:#d98400; } .exclude { background:#59677a; }
    .nav { display:grid; grid-template-columns:1fr 1fr; gap:8px; } .nav button { background:#526176; }
    select { background:#101827; color:#eef4ff; border:1px solid #52647b; padding:8px; border-radius:8px; }
    #error { color:#ff8c95; min-height:20px; }
    @media (max-width:900px) { main { grid-template-columns:1fr; height:auto; } .viewer { min-height:55vh; } }
  </style>
</head>
<body>
  <header>
    <div><h1>Detect-Gold · Fehlfall-Prüfung</h1><div class="muted">Reviewer: __REVIEWER__</div><div class="warning">Nur Diagnose – keine Übernahme in Gold oder Training.</div></div>
    <div id="progress">Laden …</div>
  </header>
  <main>
    <section class="viewer"><div id="stage"><img id="photo" alt="Prüfbild"><div id="overlay"></div></div></section>
    <aside class="panel">
      <select id="filter"><option value="all">Alle Fehlertypen</option><option value="wrong_class">Falsche Klasse</option><option value="missed">Verpasst</option><option value="extra_prediction">Zusätzliche KI-Box</option></select>
      <div class="type" id="type"></div>
      <div class="card" id="details"></div>
      <div class="legend"><span><i class="dot" style="border-color:#24df7c"></i>Gold</span><span><i class="dot" style="border-color:#4ea1ff"></i>KI</span><span><i class="dot" style="border-color:#ffd23f"></i>Fokus Gold</span><span><i class="dot" style="border-color:#ff4d5d"></i>Fokus KI</span></div>
      <textarea id="comment" maxlength="2000" placeholder="Optionaler Kommentar"></textarea>
      <button class="ok" data-decision="confirmed_model_error">1 · Gold korrekt – KI-Fehler bestätigt</button>
      <button class="suspect" data-decision="gold_suspect">2 · Gold oder Box fraglich</button>
      <button class="exclude" data-decision="exclude_uncertain">3 · Unklar – ausschließen</button>
      <div class="nav"><button id="prev">← Vorheriges</button><button id="next">Nächstes →</button></div>
      <div id="error"></div>
      <div class="muted">Tasten: 1 / 2 / 3, Pfeil links / rechts</div>
    </aside>
  </main>
<script>
let reviewState = null, visible = [], index = 0;
const $ = id => document.getElementById(id);
const labels = {wrong_class:'Falsche Klasse', missed:'Gold-Situation verpasst', extra_prediction:'Zusätzliche KI-Box'};
function boxText(x){ return x ? `${x.class_name}${x.code ? ` · ${x.code}` : ''}` : '—'; }
function addBox(box, kind, focus, label){
  if(!box) return;
  const el=document.createElement('div'); el.className=`box ${kind}${focus?' focus':''}`;
  el.style.left=`${(box.x_center-box.width/2)*100}%`; el.style.top=`${(box.y_center-box.height/2)*100}%`;
  el.style.width=`${box.width*100}%`; el.style.height=`${box.height*100}%`;
  const tag=document.createElement('span'); tag.className='tag'; tag.textContent=label; el.appendChild(tag); $('overlay').appendChild(el);
}
function applyFilter(){
  const wanted=$('filter').value;
  visible=reviewState.items.filter(x=>wanted==='all'||x.case_type===wanted);
  if(!visible.length){ index=0; renderEmpty(); return; }
  const current=reviewState.current && visible.findIndex(x=>x.id===reviewState.current.id);
  index=current>=0?current:Math.min(index,visible.length-1); render();
}
function renderEmpty(){ $('photo').removeAttribute('src'); $('overlay').innerHTML=''; $('type').textContent='Keine Fälle in diesem Filter'; $('details').textContent=''; }
function render(){
  if(!visible.length){renderEmpty();return;} const item=visible[index];
  $('photo').src=item.image_url; $('overlay').innerHTML=''; $('comment').value=item.comment||'';
  $('type').textContent=`${labels[item.case_type]} · ${index+1}/${visible.length}`;
  const expected=item.ground_truth?boxText(item.ground_truth):'kein zugeordnetes Gold';
  const predicted=item.prediction?boxText(item.prediction):'keine KI-Box';
  const desc=item.ground_truth&&item.ground_truth.description?`\nKlartext: ${item.ground_truth.description}`:'';
  $('details').textContent=`Soll: ${expected}\nKI: ${predicted}${desc}${item.iou==null?'':`\nIoU: ${Number(item.iou).toFixed(3)}`}`;
  const golds=item.gold_instances|| (item.ground_truth?[item.ground_truth]:[]);
  const preds=item.predictions|| (item.prediction?[item.prediction]:[]);
  golds.forEach(x=>addBox(x.box,'gold',item.ground_truth&&x.sample_id===item.ground_truth.sample_id,`Gold ${boxText(x)}`));
  preds.forEach(x=>addBox(x.box,'pred',item.prediction&&x.prediction_id===item.prediction.prediction_id,`KI ${boxText(x)} ${Math.round(x.confidence*100)}%`));
  $('progress').textContent=`${reviewState.done} / ${reviewState.total} geprüft · ${reviewState.open} offen`;
  $('error').textContent='';
}
async function loadState(){ const r=await fetch('/api/state',{cache:'no-store'}); reviewState=await r.json(); applyFilter(); }
async function save(decision){
  if(!visible.length)return; const item=visible[index]; $('error').textContent='Speichern …';
  const r=await fetch('/api/review',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({id:item.id,decision,comment:$('comment').value,revision:reviewState.revision})});
  const data=await r.json(); if(!r.ok){ $('error').textContent=data.error||'Speichern fehlgeschlagen.'; if(r.status===409) await loadState(); return; }
  reviewState=data; const next=reviewState.items.find(x=>!x.decision && ( $('filter').value==='all'||x.case_type===$('filter').value)); applyFilter(); if(next){const p=visible.findIndex(x=>x.id===next.id);if(p>=0){index=p;render();}}
}
document.querySelectorAll('[data-decision]').forEach(b=>b.addEventListener('click',()=>save(b.dataset.decision)));
$('prev').onclick=()=>{if(visible.length){index=(index-1+visible.length)%visible.length;render();}};
$('next').onclick=()=>{if(visible.length){index=(index+1)%visible.length;render();}};
$('filter').onchange=applyFilter;
document.addEventListener('keydown',e=>{if(e.target.tagName==='TEXTAREA')return;if(e.key==='1')save('confirmed_model_error');if(e.key==='2')save('gold_suspect');if(e.key==='3')save('exclude_uncertain');if(e.key==='ArrowLeft')$('prev').click();if(e.key==='ArrowRight')$('next').click();});
loadState().catch(e=>$('error').textContent=String(e));
</script>
</body></html>"""


def run_server(
    knowledge_root: Path,
    queue_root: Path,
    output_path: Path,
    reviewer: str,
    port: int = 8775,
    open_browser: bool = False,
) -> None:
    store = DetectGoldErrorReviewStore(
        knowledge_root,
        queue_root,
        output_path,
        reviewer,
    )
    state = store.prepare_output()
    server = create_server(store, port)
    actual_port = server.server_address[1]
    url = f"http://127.0.0.1:{actual_port}/"
    print(f"Detect-Gold-Fehlfallpruefung: {url}")
    print(f"Prueffaelle: {state['total']}; offen: {state['open']}")
    print(f"Review-Ausgabe: {store.output_path}")
    print("Stoppen mit Strg+C")
    if open_browser:
        webbrowser.open(url, new=1, autoraise=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Lokale diagnostische Detect-Gold-Fehlfallpruefung"
    )
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--queue", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--reviewer", required=True)
    parser.add_argument("--port", type=int, default=8775)
    parser.add_argument("--prepare-only", action="store_true")
    parser.add_argument("--open-browser", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    if args.prepare_only:
        store = DetectGoldErrorReviewStore(
            args.knowledge_root,
            args.queue,
            args.output,
            args.reviewer,
        )
        state = store.prepare_output()
        print(f"Review vorbereitet: {store.output_path}")
        print(f"Prueffaelle: {state['total']}; offen: {state['open']}")
        return 0
    run_server(
        args.knowledge_root,
        args.queue,
        args.output,
        args.reviewer,
        args.port,
        args.open_browser,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
