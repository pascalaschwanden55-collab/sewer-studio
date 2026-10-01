from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import sys
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "detect_gold_error_review.py"
SPEC = importlib.util.spec_from_file_location(
    "detect_gold_error_review_collection_plan",
    SCRIPT_PATH,
)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


QUEUE_SHA256 = "a" * 64
REVIEW_SHA256 = "b" * 64


def _case(
    case_id: str,
    error_type: str,
    *,
    expected: tuple[int, str] | None,
    predicted: tuple[int, str] | None,
) -> dict[str, object]:
    return {
        "id": case_id,
        "error_type": error_type,
        "expected_class_id": expected[0] if expected else None,
        "expected_class_name": expected[1] if expected else None,
        "predicted_class_id": predicted[0] if predicted else None,
        "predicted_class_name": predicted[1] if predicted else None,
        "image_path": rf"C:\PRIVATE\{case_id}.jpg",
        "image_sha256": hashlib.sha256(case_id.encode("utf-8")).hexdigest(),
        "sample_id": f"sample-secret-{case_id}",
        "prediction_id": f"prediction-secret-{case_id}",
    }


def _queue() -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "purpose": "detect_gold_error_review_queue",
        "queue_id": "c" * 64,
        "cases": [
            _case(
                "missed-a",
                "missed",
                expected=(0, "klasse_a"),
                predicted=None,
            ),
            _case(
                "wrong-b-c",
                "wrong_class",
                expected=(1, "klasse_b"),
                predicted=(2, "klasse_c"),
            ),
            _case(
                "extra-negative-c",
                "extra_prediction",
                expected=None,
                predicted=(2, "klasse_c"),
            ),
            _case(
                "extra-confusion-a-c",
                "extra_prediction",
                expected=(0, "klasse_a"),
                predicted=(2, "klasse_c"),
            ),
            _case(
                "suspect-b",
                "missed",
                expected=(1, "klasse_b"),
                predicted=None,
            ),
            _case(
                "excluded-d",
                "extra_prediction",
                expected=None,
                predicted=(3, "klasse_d"),
            ),
        ],
    }


def _review() -> dict[str, object]:
    decisions = {
        "missed-a": "confirmed_model_error",
        "wrong-b-c": "confirmed_model_error",
        "extra-negative-c": "confirmed_model_error",
        "extra-confusion-a-c": "confirmed_model_error",
        "suspect-b": "gold_suspect",
        "excluded-d": "exclude_uncertain",
    }
    return {
        "schema_version": "1.0",
        "purpose": "detect_gold_error_review",
        "queue_id": "c" * 64,
        "decisions": {
            case_id: {
                "decision": decision,
                "comment": f"comment-secret-{case_id}",
                "reviewed_at_utc": "2026-08-02T12:00:00Z",
            }
            for case_id, decision in decisions.items()
        },
    }


def _build(
    queue: dict[str, object] | None = None,
    review: dict[str, object] | None = None,
) -> dict[str, object]:
    return MODULE.build_collection_plan(
        queue or _queue(),
        review or _review(),
        queue_sha256=QUEUE_SHA256,
        review_sha256=REVIEW_SHA256,
    )


def _all_keys(value: object) -> set[str]:
    if isinstance(value, dict):
        return set(value) | {
            nested
            for child in value.values()
            for nested in _all_keys(child)
        }
    if isinstance(value, list):
        return {nested for child in value for nested in _all_keys(child)}
    return set()


class DetectGoldCollectionPlanTests(unittest.TestCase):
    def test_unvollstaendige_review_wird_abgelehnt(self) -> None:
        review = _review()
        del review["decisions"]["missed-a"]

        with self.assertRaisesRegex(ValueError, "vollstaendig|vollständig"):
            _build(review=review)

    def test_bestaetigte_falsche_klasse_ohne_vorhersage_wird_abgelehnt(self) -> None:
        queue = _queue()
        wrong_class = next(
            case for case in queue["cases"] if case["id"] == "wrong-b-c"
        )
        wrong_class["predicted_class_id"] = None
        wrong_class["predicted_class_name"] = None

        with self.assertRaisesRegex(ValueError, "Vorhersageklasse"):
            _build(queue=queue)

    def test_nur_bestaetigte_modellfehler_erzeugen_getrennte_sammelziele(self) -> None:
        plan = _build()

        self.assertEqual(
            {
                "reviewed": 6,
                "confirmed_model_error": 4,
                "gold_suspect": 1,
                "exclude_uncertain": 1,
            },
            plan["counts"],
        )
        positive = {
            (row["class_id"], row["class_name"]): row
            for row in plan["positive_class_targets"]
        }
        self.assertEqual({(0, "klasse_a"), (1, "klasse_b")}, set(positive))
        self.assertEqual(
            {"missed": 1, "wrong_class": 0},
            positive[(0, "klasse_a")]["reasons"],
        )
        self.assertEqual(
            {"missed": 0, "wrong_class": 1},
            positive[(1, "klasse_b")]["reasons"],
        )

        self.assertEqual(
            [{"class_id": 2, "class_name": "klasse_c", "count": 1}],
            plan["negative_class_targets"],
        )
        self.assertEqual(
            [
                {
                    "expected_class_id": 0,
                    "expected_class_name": "klasse_a",
                    "predicted_class_id": 2,
                    "predicted_class_name": "klasse_c",
                    "count": 1,
                },
                {
                    "expected_class_id": 1,
                    "expected_class_name": "klasse_b",
                    "predicted_class_id": 2,
                    "predicted_class_name": "klasse_c",
                    "count": 1,
                },
            ],
            plan["confusion_targets"],
        )
        self.assertEqual(
            [
                {
                    "error_type": "missed",
                    "expected_class_id": 1,
                    "expected_class_name": "klasse_b",
                    "predicted_class_id": None,
                    "predicted_class_name": None,
                    "count": 1,
                }
            ],
            plan["annotation_audit"],
        )
        serialized_targets = json.dumps(
            {
                "positive": plan["positive_class_targets"],
                "negative": plan["negative_class_targets"],
                "confusion": plan["confusion_targets"],
                "audit": plan["annotation_audit"],
            },
            ensure_ascii=False,
        )
        self.assertNotIn("klasse_d", serialized_targets)

    def test_plan_ist_aggregate_only_gebunden_und_enthaelt_keine_falldaten(self) -> None:
        queue = _queue()
        review = _review()

        plan = _build(queue, review)
        serialized = json.dumps(plan, ensure_ascii=False, sort_keys=True)

        self.assertEqual("aggregate_only", plan["mode"])
        self.assertEqual(
            {
                "queue_sha256": QUEUE_SHA256,
                "review_sha256": REVIEW_SHA256,
            },
            plan["bindings"],
        )
        self.assertTrue(
            {"image_path", "image_sha256", "sample_id", "prediction_id", "id"}
            .isdisjoint(_all_keys(plan))
        )
        for case in queue["cases"]:
            for field in (
                "id",
                "image_path",
                "image_sha256",
                "sample_id",
                "prediction_id",
            ):
                self.assertNotIn(str(case[field]), serialized)
        for decision in review["decisions"].values():
            self.assertNotIn(str(decision["comment"]), serialized)
        warning = str(plan["warning"]).casefold()
        self.assertIn("holdout", warning)
        self.assertIn("release", warning)
        self.assertIn("nicht", warning)

    def test_planbildung_mutiert_nichts_und_ist_reihenfolgeunabhaengig(self) -> None:
        queue = _queue()
        review = _review()
        queue_before = copy.deepcopy(queue)
        review_before = copy.deepcopy(review)

        first = _build(queue, review)
        reversed_queue = copy.deepcopy(queue)
        reversed_queue["cases"] = list(reversed(reversed_queue["cases"]))
        reversed_review = copy.deepcopy(review)
        reversed_review["decisions"] = dict(
            reversed(list(reversed_review["decisions"].items()))
        )
        second = _build(reversed_queue, reversed_review)

        self.assertEqual(queue_before, queue)
        self.assertEqual(review_before, review)
        self.assertEqual(first, second)


if __name__ == "__main__":
    unittest.main()
