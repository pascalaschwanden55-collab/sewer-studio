#!/usr/bin/env python3
"""Prueft die Abdeckung eines menschlich reviewten Detect-Release-Holdouts.

Das Werkzeug ist absichtlich schreibfrei. Es startet kein Modell und veraendert
weder Holdout, Review, Training noch Gold-Daten.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from pathlib import Path
from typing import Any, Sequence


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from tools.EvalVisibilityReview import (  # noqa: E402
    detect_release_holdout_review_server as review_server,
)


SCHEMA_VERSION = "1.0"
PURPOSE = "detect_release_holdout_status"
DEFAULT_MIN_INSTANCES_PER_CLASS = 20
DEFAULT_MIN_NEGATIVE_IMAGES = 75
DEFAULT_MIN_NEGATIVE_PHYSICAL_HOLDINGS = 30


def evaluate_holdout_status(
    holdout_root: str | Path,
    review_path: str | Path,
    *,
    min_instances_per_class: int = DEFAULT_MIN_INSTANCES_PER_CLASS,
    min_negative_images: int = DEFAULT_MIN_NEGATIVE_IMAGES,
    min_negative_physical_holdings: int = (
        DEFAULT_MIN_NEGATIVE_PHYSICAL_HOLDINGS
    ),
) -> dict[str, Any]:
    """Validiert Holdout und Review und liefert den schreibfreien Status."""

    requirements = _validate_requirements(
        min_instances_per_class,
        min_negative_images,
        min_negative_physical_holdings,
    )
    holdout = Path(os.path.abspath(holdout_root))
    review = Path(os.path.abspath(review_path))

    snapshot_before = review_server._validate_holdout(holdout)
    candidate_rows = _load_bound_candidates(holdout, snapshot_before)
    review_document, review_bytes = _load_bound_review(
        holdout,
        review,
        snapshot_before,
    )

    decisions = review_document["decisions"]
    physical_by_id = {
        str(row["id"]): str(row["physical_holding_key"])
        for row in candidate_rows
    }
    class_instances = {item.name: 0 for item in snapshot_before.classes}
    class_images: dict[str, set[str]] = {
        item.name: set() for item in snapshot_before.classes
    }
    decision_counts = {name: 0 for name in ("positive", "negative", "exclude")}
    positive_holdings: set[str] = set()
    negative_holdings: set[str] = set()

    for candidate_id, raw_decision in decisions.items():
        decision = raw_decision["decision"]
        decision_counts[decision] += 1
        if decision == "positive":
            positive_holdings.add(physical_by_id[candidate_id])
            for annotation in raw_decision["annotations"]:
                class_name = annotation["class_name"]
                class_instances[class_name] += 1
                class_images[class_name].add(candidate_id)
        elif decision == "negative":
            negative_holdings.add(physical_by_id[candidate_id])

    total = len(snapshot_before.images)
    reviewed = len(decisions)
    open_images = total - reviewed
    coverage_rows: list[dict[str, Any]] = []
    shortfalls: list[dict[str, Any]] = []
    for detect_class in snapshot_before.classes:
        instances = class_instances[detect_class.name]
        image_count = len(class_images[detect_class.name])
        complete = instances >= requirements["min_instances_per_class"]
        coverage_rows.append(
            {
                **detect_class.public(),
                "instances": instances,
                "images": image_count,
                "minimum_instances": requirements["min_instances_per_class"],
                "complete": complete,
            }
        )
        if not complete:
            shortfalls.append(
                {
                    "metric": "class_instances",
                    "class_id": detect_class.class_id,
                    "class_name": detect_class.name,
                    "actual": instances,
                    "required": requirements["min_instances_per_class"],
                }
            )

    negative_images = decision_counts["negative"]
    if negative_images < requirements["min_negative_images"]:
        shortfalls.append(
            {
                "metric": "negative_images",
                "actual": negative_images,
                "required": requirements["min_negative_images"],
            }
        )
    if len(negative_holdings) < requirements["min_negative_physical_holdings"]:
        shortfalls.append(
            {
                "metric": "negative_physical_holdings",
                "actual": len(negative_holdings),
                "required": requirements["min_negative_physical_holdings"],
            }
        )

    if open_images:
        dataset_status = "review_incomplete"
    elif shortfalls:
        dataset_status = "coverage_incomplete"
    else:
        dataset_status = "ready_for_detect_evaluation"

    # Eine Aenderung waehrend der Auswertung darf nie als gueltiger Status gelten.
    snapshot_after = review_server._validate_holdout(holdout)
    if snapshot_after != snapshot_before:
        raise ValueError("Der Holdout wurde waehrend der Statuspruefung veraendert.")
    current_review_bytes = review_server._read_limited(review)
    if current_review_bytes != review_bytes:
        raise ValueError("Die Review wurde waehrend der Statuspruefung veraendert.")

    return {
        "schema_version": SCHEMA_VERSION,
        "purpose": PURPOSE,
        "holdout_id": snapshot_before.holdout_id,
        "dataset_status": dataset_status,
        "release_status": "not_evaluated",
        "evaluation_scope": "detect_multiclass_presence_and_localization",
        "total": total,
        "reviewed": reviewed,
        "open": open_images,
        "positive": decision_counts["positive"],
        "negative": negative_images,
        "exclude": decision_counts["exclude"],
        "positive_physical_holdings": len(positive_holdings),
        "negative_physical_holdings": len(negative_holdings),
        "instances_by_class": {
            item.name: class_instances[item.name] for item in snapshot_before.classes
        },
        "images_by_class": {
            item.name: len(class_images[item.name]) for item in snapshot_before.classes
        },
        "class_coverage": coverage_rows,
        "requirements": {
            **requirements,
            "release_minimum_instances_per_class": (
                DEFAULT_MIN_INSTANCES_PER_CLASS
            ),
            "release_minimum_negative_images": DEFAULT_MIN_NEGATIVE_IMAGES,
            "release_minimum_negative_physical_holdings": (
                DEFAULT_MIN_NEGATIVE_PHYSICAL_HOLDINGS
            ),
        },
        "shortfalls": shortfalls,
        "bindings": {
            **snapshot_before.bindings(),
            "review_sha256": hashlib.sha256(review_bytes).hexdigest(),
        },
    }


def _validate_requirements(
    min_instances_per_class: object,
    min_negative_images: object,
    min_negative_physical_holdings: object,
) -> dict[str, int]:
    values = (
        (
            "min_instances_per_class",
            min_instances_per_class,
            DEFAULT_MIN_INSTANCES_PER_CLASS,
        ),
        ("min_negative_images", min_negative_images, DEFAULT_MIN_NEGATIVE_IMAGES),
        (
            "min_negative_physical_holdings",
            min_negative_physical_holdings,
            DEFAULT_MIN_NEGATIVE_PHYSICAL_HOLDINGS,
        ),
    )
    result: dict[str, int] = {}
    for name, raw_value, release_minimum in values:
        if isinstance(raw_value, bool) or not isinstance(raw_value, int):
            raise ValueError(f"{name} muss eine Ganzzahl sein.")
        if raw_value < release_minimum:
            raise ValueError(
                f"{name} darf den Release-Mindestwert {release_minimum} "
                "nicht unterschreiten."
            )
        result[name] = raw_value
    return result


def _load_bound_candidates(
    holdout: Path,
    snapshot: review_server.HoldoutSnapshot,
) -> list[dict[str, Any]]:
    payload = review_server._read_limited(holdout / "_candidates.json")
    if hashlib.sha256(payload).hexdigest() != snapshot.candidates_sha256:
        raise ValueError("Die Kandidatenliste wurde seit der Holdout-Pruefung veraendert.")
    document = review_server._load_json_bytes(payload, "Kandidatenliste")
    if not isinstance(document, dict):
        raise ValueError("Die Kandidatenliste ist kein JSON-Objekt.")
    raw_candidates = document.get("candidates")
    if not isinstance(raw_candidates, list):
        raise ValueError("Die Kandidatenliste enthaelt keine Kandidatenliste.")
    rows = [row for row in raw_candidates if isinstance(row, dict)]
    if len(rows) != len(snapshot.images):
        raise ValueError("Kandidatenliste und validierter Holdout widersprechen sich.")
    expected_ids = [image.candidate_id for image in snapshot.images]
    if [row.get("id") for row in rows] != expected_ids:
        raise ValueError("Kandidatenreihenfolge und validierter Holdout widersprechen sich.")
    return rows


def _load_bound_review(
    holdout: Path,
    review_path: Path,
    snapshot: review_server.HoldoutSnapshot,
) -> tuple[dict[str, Any], bytes]:
    review_server._validate_output_location(review_path, holdout)
    payload = review_server._read_limited(review_path)
    document = review_server._load_json_bytes(payload, "Detect-Release-Review")
    if not isinstance(document, dict) or set(document) != review_server._REVIEW_FIELDS:
        raise ValueError("Die Detect-Release-Review hat ein falsches Schema.")
    if document.get("schema_version") != review_server.SCHEMA_VERSION:
        raise ValueError("Die Detect-Release-Review hat eine falsche Schema-Version.")
    if document.get("purpose") != review_server.REVIEW_PURPOSE:
        raise ValueError("Die Datei ist keine Detect-Release-Review.")
    if document.get("holdout_id") != snapshot.holdout_id:
        raise ValueError("Die Review gehoert zu einem anderen Holdout.")
    for field, expected in snapshot.bindings().items():
        if document.get(field) != expected:
            raise ValueError(f"Die Review ist nicht an {field} gebunden.")

    review_server._required_text(
        document.get("reviewer"),
        "Reviewer",
        review_server.MAX_REVIEWER_CHARACTERS,
    )
    review_server._require_utc_timestamp(
        document.get("updated_at_utc"),
        "Review-Aktualisierungszeitpunkt",
    )
    raw_decisions = document.get("decisions")
    if not isinstance(raw_decisions, dict):
        raise ValueError("Review-Entscheidungen muessen ein Objekt sein.")
    classes_by_id = {item.class_id: item for item in snapshot.classes}
    candidate_ids = {image.candidate_id for image in snapshot.images}
    if not set(raw_decisions).issubset(candidate_ids):
        raise ValueError("Review enthaelt unbekannte Bild-IDs.")
    decisions: dict[str, dict[str, object]] = {}
    for candidate_id, raw_decision in raw_decisions.items():
        decisions[candidate_id] = review_server._validate_saved_decision(
            raw_decision,
            classes_by_id,
        )
    document["decisions"] = decisions
    return document, payload


def _parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Prueft eine externe Detect-Release-Review und deren Klassenabdeckung."
        )
    )
    parser.add_argument("--holdout", type=Path, required=True)
    parser.add_argument("--review", type=Path, required=True)
    parser.add_argument(
        "--min-instances-per-class",
        type=int,
        default=DEFAULT_MIN_INSTANCES_PER_CLASS,
        help="Mindestens 20; hoehere Release-Anforderung ist erlaubt.",
    )
    parser.add_argument(
        "--min-negative-images",
        type=int,
        default=DEFAULT_MIN_NEGATIVE_IMAGES,
        help="Mindestens 75; hoehere Release-Anforderung ist erlaubt.",
    )
    parser.add_argument(
        "--min-negative-holdings",
        type=int,
        default=DEFAULT_MIN_NEGATIVE_PHYSICAL_HOLDINGS,
        help="Mindestens 30 physische Haltungen; hoeher ist erlaubt.",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    try:
        status = evaluate_holdout_status(
            args.holdout,
            args.review,
            min_instances_per_class=args.min_instances_per_class,
            min_negative_images=args.min_negative_images,
            min_negative_physical_holdings=args.min_negative_holdings,
        )
        print(json.dumps(status, ensure_ascii=False, indent=2))
        return 0 if status["dataset_status"] == "ready_for_detect_evaluation" else 2
    except (OSError, ValueError) as error:
        print(f"FEHLER: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
