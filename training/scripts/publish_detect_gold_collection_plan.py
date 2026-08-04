#!/usr/bin/env python3
"""Publiziert aus einer abgeschlossenen Fehlfall-Review nur Aggregat-Ziele."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import detect_gold_error_review as review_tools


SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
BASE_REVIEW_FIELDS = {
    "schema_version",
    "purpose",
    "queue_id",
    "queue_manifest_sha256",
    "candidates_sha256",
    "reviewer",
    "updated_at_utc",
    "decisions",
}
OPTIONAL_REVIEW_FIELDS = {
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
    "role",
}


def _is_reparse(path: Path) -> bool:
    return review_tools._is_reparse(path)


def _path_is_within(path: Path, root: Path) -> bool:
    try:
        Path(os.path.abspath(path)).relative_to(Path(os.path.abspath(root)))
        return True
    except ValueError:
        return False


def _load_object(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    value, body = review_tools._load_object(path, label)
    return value, body


def _load_array(path: Path, label: str) -> tuple[list[dict[str, Any]], bytes]:
    if not path.is_file() or _is_reparse(path):
        raise ValueError(f"{label} fehlt oder ist unsicher.")
    body = path.read_bytes()
    value = review_tools.strict_json_bytes(body, label)
    if not isinstance(value, list) or any(not isinstance(item, dict) for item in value):
        raise ValueError(f"{label} muss eine Liste aus Objekten sein.")
    return [dict(item) for item in value], body


def _require_sha(value: object, label: str) -> str:
    text = str(value or "")
    if not SHA256_PATTERN.fullmatch(text):
        raise ValueError(f"{label} ist keine SHA-256-Pruefsumme.")
    return text


def _sha(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


@dataclass(frozen=True)
class LoadedInputs:
    queue: dict[str, object]
    review: dict[str, object]
    manifest_path: Path
    candidates_path: Path
    review_path: Path
    manifest_bytes: bytes
    candidates_bytes: bytes
    review_bytes: bytes


def load_inputs(
    knowledge_root: Path,
    queue_root: Path,
    review_path: Path,
    reviewer: str,
) -> LoadedInputs:
    root = Path(os.path.abspath(knowledge_root))
    queue = Path(os.path.abspath(queue_root))
    review_file = Path(os.path.abspath(review_path))
    workflow_root = root / "eval_review" / "detect_gold_failure_review"
    if (
        not root.is_dir()
        or _is_reparse(root)
        or not queue.is_dir()
        or _is_reparse(queue)
        or not _path_is_within(queue, workflow_root / "queues")
        or not _path_is_within(review_file, workflow_root / "reviews")
    ):
        raise ValueError("Knowledge-Root, Queue oder Review-Pfad ist unsicher.")
    if {item.name for item in queue.iterdir()} != {
        "_manifest.json",
        "_candidates.json",
    }:
        raise ValueError("Queue besitzt fremde oder fehlende Dateien.")
    manifest_path = queue / "_manifest.json"
    candidates_path = queue / "_candidates.json"
    manifest, manifest_bytes = _load_object(manifest_path, "Queue-Manifest")
    candidates, candidates_bytes = _load_array(
        candidates_path,
        "Queue-Kandidaten",
    )
    review, review_bytes = _load_object(review_file, "Fehlfall-Review")

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
    if set(manifest) != expected_manifest_fields:
        raise ValueError("Queue-Manifest hat fremde oder fehlende Felder.")
    if (
        manifest.get("schema_version") != review_tools.SCHEMA_VERSION
        or manifest.get("purpose") != review_tools.QUEUE_PURPOSE
        or manifest.get("role") != review_tools.QUEUE_ROLE
        or manifest.get("frozen") is not True
    ):
        raise ValueError("Queue ist nicht als reine Diagnose eingefroren.")
    policy = manifest.get("policy")
    if policy != {
        "training_eligible": False,
        "training_export_allowed": False,
        "source_mutation_allowed": False,
        "image_copies_created": False,
    }:
        raise ValueError("Queue-Policy erlaubt Training oder Mutation.")
    semantic = review_tools.queue_semantic_payload(manifest, candidates)
    queue_id = _sha(review_tools.canonical_json_bytes(semantic))
    if manifest.get("queue_id") != queue_id:
        raise ValueError("Queue-ID stimmt nicht.")

    if not BASE_REVIEW_FIELDS <= set(review) or set(review) - BASE_REVIEW_FIELDS - OPTIONAL_REVIEW_FIELDS:
        raise ValueError("Review hat fremde oder fehlende Felder.")
    if (
        review.get("schema_version") != review_tools.SCHEMA_VERSION
        or review.get("purpose") != review_tools.REVIEW_PURPOSE
        or review.get("queue_id") != queue_id
        or review.get("reviewer") != reviewer
    ):
        raise ValueError("Review gehoert zu einer anderen Queue oder Person.")
    manifest_sha = _sha(manifest_bytes)
    candidates_sha = _sha(candidates_bytes)
    if (
        review.get("queue_manifest_sha256") != manifest_sha
        or review.get("candidates_sha256") != candidates_sha
    ):
        raise ValueError("Review ist nicht an Queue-Manifest und Kandidaten gebunden.")
    bindings = manifest.get("bindings")
    if not isinstance(bindings, dict):
        raise ValueError("Queue-Bindings fehlen.")
    for field in OPTIONAL_REVIEW_FIELDS - {"role"}:
        if field in review:
            _require_sha(review.get(field), field)
            if field in bindings and review.get(field) != bindings.get(field):
                raise ValueError(f"Review-Binding {field} stimmt nicht.")
    if review.get("role", review_tools.QUEUE_ROLE) != review_tools.QUEUE_ROLE:
        raise ValueError("Review-Rolle ist ungueltig.")

    queue_document: dict[str, object] = {
        "schema_version": manifest["schema_version"],
        "purpose": manifest["purpose"],
        "queue_id": queue_id,
        "cases": candidates,
    }
    review_document = dict(review)
    review_tools.build_collection_plan(
        queue_document,
        review_document,
        queue_sha256=manifest_sha,
        review_sha256=_sha(review_bytes),
    )
    return LoadedInputs(
        queue=queue_document,
        review=review_document,
        manifest_path=manifest_path,
        candidates_path=candidates_path,
        review_path=review_file,
        manifest_bytes=manifest_bytes,
        candidates_bytes=candidates_bytes,
        review_bytes=review_bytes,
    )


def _assert_unchanged(inputs: LoadedInputs) -> None:
    expected = (
        (inputs.manifest_path, inputs.manifest_bytes),
        (inputs.candidates_path, inputs.candidates_bytes),
        (inputs.review_path, inputs.review_bytes),
    )
    for path, body in expected:
        if not path.is_file() or _is_reparse(path) or path.read_bytes() != body:
            raise ValueError(f"Gebundene Eingabe wurde parallel veraendert: {path}")


def build_plan(inputs: LoadedInputs) -> tuple[dict[str, object], bytes, str]:
    plan = review_tools.build_collection_plan(
        inputs.queue,
        inputs.review,
        queue_sha256=_sha(inputs.manifest_bytes),
        review_sha256=_sha(inputs.review_bytes),
    )
    body = review_tools.pretty_json_bytes(plan)
    plan_id = _sha(review_tools.canonical_json_bytes(plan))
    return plan, body, plan_id


def publish_plan(target: Path, body: bytes, inputs: LoadedInputs) -> Path:
    absolute = Path(os.path.abspath(target))
    if absolute.exists() or absolute.is_symlink():
        if absolute.is_file() and not _is_reparse(absolute) and absolute.read_bytes() == body:
            return absolute
        raise FileExistsError(f"Sammelplan-Ziel existiert mit anderem Inhalt: {absolute}")
    parent = absolute.parent
    parent.mkdir(parents=True, exist_ok=True)
    if _is_reparse(parent) or os.path.normcase(os.path.realpath(parent)) != os.path.normcase(
        str(parent)
    ):
        raise ValueError("Sammelplan-Ordner ist unsicher.")
    _assert_unchanged(inputs)
    temporary = parent / f".{absolute.name}.{uuid.uuid4().hex}.tmp"
    try:
        with temporary.open("xb") as stream:
            stream.write(body)
            stream.flush()
            os.fsync(stream.fileno())
        if temporary.read_bytes() != body:
            raise OSError("Sammelplan-Staging konnte nicht verifiziert werden.")
        _assert_unchanged(inputs)
        if absolute.exists() or absolute.is_symlink():
            if absolute.is_file() and absolute.read_bytes() == body:
                return absolute
            raise FileExistsError("Sammelplan-Ziel wurde parallel angelegt.")
        os.replace(temporary, absolute)
    finally:
        try:
            temporary.unlink(missing_ok=True)
        except OSError:
            pass
    return absolute


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Aggregierten Sammelplan aus vollstaendiger Fehlfall-Review bauen"
    )
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--queue", type=Path, required=True)
    parser.add_argument("--review", type=Path, required=True)
    parser.add_argument("--reviewer", required=True)
    parser.add_argument("--execute", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    try:
        args = parse_args(argv)
        inputs = load_inputs(
            args.knowledge_root,
            args.queue,
            args.review,
            args.reviewer,
        )
        plan, body, plan_id = build_plan(inputs)
        target = (
            Path(os.path.abspath(args.knowledge_root))
            / "eval_review"
            / "detect_gold_failure_review"
            / "collection_plans"
            / f"detect_gold_collection_{plan_id[:12]}.json"
        )
        print(f"Sammelplan: {plan_id}")
        print(f"Bestaetigte Modellfehler: {plan['counts']['confirmed_model_error']}")
        print(f"Ziel: {target}")
        if not args.execute:
            print("Nur geprueft. Mit --execute wird der aggregierte Plan geschrieben.")
            return 0
        published = publish_plan(target, body, inputs)
        print(f"Aggregierter Sammelplan veroeffentlicht: {published}")
        return 0
    except (OSError, ValueError, FileExistsError) as error:
        print(f"FEHLER: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
