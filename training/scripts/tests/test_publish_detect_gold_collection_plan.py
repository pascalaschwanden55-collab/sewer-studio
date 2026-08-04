from __future__ import annotations

import contextlib
import hashlib
import importlib.util
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


SCRIPT_PATH = (
    Path(__file__).resolve().parents[1]
    / "publish_detect_gold_collection_plan.py"
)
SPEC = importlib.util.spec_from_file_location(
    "publish_detect_gold_collection_plan_tests",
    SCRIPT_PATH,
)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def _json_bytes(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def _canonical_bytes(value: object) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


class CollectionPlanFixture:
    def __init__(self, root: Path) -> None:
        self.knowledge_root = root / "KI_BRAIN"
        self.knowledge_root.mkdir(parents=True)
        self.reviewer = "Besitzer"
        self.candidates = [
            {
                "id": "case-missed-a",
                "error_type": "missed",
                "expected_class_id": 0,
                "expected_class_name": "klasse_a",
                "predicted_class_id": None,
                "predicted_class_name": None,
                "image_path": "private/gold-a.jpg",
                "image_sha256": "1" * 64,
                "sample_id": "sample-secret-a",
                "prediction_id": None,
            },
            {
                "id": "case-extra-b",
                "error_type": "extra_prediction",
                "expected_class_id": None,
                "expected_class_name": None,
                "predicted_class_id": 1,
                "predicted_class_name": "klasse_b",
                "image_path": "private/gold-b.jpg",
                "image_sha256": "2" * 64,
                "sample_id": None,
                "prediction_id": "prediction-secret-b",
            },
        ]
        self.bindings = {
            "evaluation_report_sha256": "3" * 64,
            "prediction_ledger_sha256": "4" * 64,
            "candidate_manifest_sha256": "5" * 64,
            "weights_sha256": "6" * 64,
            "current_gold_audit_sha256": "7" * 64,
            "class_map_sha256": "8" * 64,
        }
        self.manifest = {
            "schema_version": "1.0",
            "purpose": "detect_gold_failure_review_queue",
            "role": "diagnostic_only",
            "frozen": True,
            "created_utc": "2026-08-02T17:00:00Z",
            "warning": "Nur Diagnose.",
            "bindings": self.bindings,
            "policy": {
                "training_eligible": False,
                "training_export_allowed": False,
                "source_mutation_allowed": False,
                "image_copies_created": False,
            },
            "summary": {
                "cases": 2,
                "images": 2,
                "wrong_class": 0,
                "missed": 1,
                "extra_prediction": 1,
            },
        }
        semantic = {
            field: self.manifest[field]
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
            self.candidates,
            key=lambda item: item["id"],
        )
        self.queue_id = _sha256(_canonical_bytes(semantic))
        self.manifest["queue_id"] = self.queue_id
        self.queue = (
            self.knowledge_root
            / "eval_review"
            / "detect_gold_failure_review"
            / "queues"
            / f"detect_gold_failure_{self.queue_id[:12]}"
        )
        self.queue.mkdir(parents=True)
        self.manifest_path = self.queue / "_manifest.json"
        self.candidates_path = self.queue / "_candidates.json"
        self.manifest_path.write_bytes(_json_bytes(self.manifest))
        self.candidates_path.write_bytes(_json_bytes(self.candidates))
        self.manifest_sha256 = _sha256(self.manifest_path.read_bytes())
        self.candidates_sha256 = _sha256(self.candidates_path.read_bytes())
        self.review = {
            "schema_version": "1.0",
            "purpose": "detect_gold_failure_review",
            "queue_id": self.queue_id,
            "queue_manifest_sha256": self.manifest_sha256,
            "candidates_sha256": self.candidates_sha256,
            "reviewer": self.reviewer,
            "updated_at_utc": "2026-08-02T17:15:00Z",
            "decisions": {
                "case-missed-a": {
                    "decision": "confirmed_model_error",
                    "comment": "sichtbar",
                    "reviewed_at_utc": "2026-08-02T17:10:00Z",
                },
                "case-extra-b": {
                    "decision": "exclude_uncertain",
                    "comment": "unklar",
                    "reviewed_at_utc": "2026-08-02T17:11:00Z",
                },
            },
        }
        self.review_path = (
            self.knowledge_root
            / "eval_review"
            / "detect_gold_failure_review"
            / "reviews"
            / "review.json"
        )
        self.review_path.parent.mkdir(parents=True)
        self.review_path.write_bytes(_json_bytes(self.review))
        self.plans_root = (
            self.knowledge_root
            / "eval_review"
            / "detect_gold_failure_review"
            / "collection_plans"
        )

    def args(self, *, execute: bool = False, reviewer: str | None = None) -> list[str]:
        values = [
            "--knowledge-root",
            str(self.knowledge_root),
            "--queue",
            str(self.queue),
            "--review",
            str(self.review_path),
            "--reviewer",
            reviewer if reviewer is not None else self.reviewer,
        ]
        if execute:
            values.append("--execute")
        return values

    def source_snapshot(self) -> dict[Path, bytes]:
        return {
            path: path.read_bytes()
            for path in (
                self.manifest_path,
                self.candidates_path,
                self.review_path,
            )
        }


def _run(args: list[str]) -> int:
    with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(
        io.StringIO()
    ):
        return MODULE.main(args)


class PublishDetectGoldCollectionPlanTests(unittest.TestCase):
    def test_standard_prueft_nur_und_execute_publiziert_atomar_idempotent(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = CollectionPlanFixture(Path(temporary))
            sources_before = fixture.source_snapshot()

            self.assertEqual(0, _run(fixture.args()))
            self.assertFalse(fixture.plans_root.exists())
            self.assertEqual(sources_before, fixture.source_snapshot())

            real_replace = os.replace
            with mock.patch.object(MODULE.os, "replace", wraps=real_replace) as replace:
                self.assertEqual(0, _run(fixture.args(execute=True)))
            plans = list(fixture.plans_root.glob("*.json"))
            self.assertEqual(1, len(plans))
            plan_path = plans[0]
            plan_before = plan_path.read_bytes()
            plan = json.loads(plan_before)
            self.assertEqual("aggregate_only", plan["mode"])
            self.assertEqual(fixture.manifest_sha256, plan["bindings"]["queue_sha256"])
            self.assertEqual(
                _sha256(fixture.review_path.read_bytes()),
                plan["bindings"]["review_sha256"],
            )
            replace.assert_called_once()
            temporary_path, destination = map(Path, replace.call_args.args)
            self.assertEqual(plan_path, destination)
            self.assertEqual(plan_path.parent, temporary_path.parent)
            self.assertEqual(sources_before, fixture.source_snapshot())

            with mock.patch.object(MODULE.os, "replace", wraps=real_replace) as replace:
                self.assertEqual(0, _run(fixture.args(execute=True)))
            self.assertFalse(replace.called)
            self.assertEqual(plan_before, plan_path.read_bytes())
            self.assertEqual(sources_before, fixture.source_snapshot())

            plan_path.write_text("abweichender bestehender Inhalt", encoding="utf-8")
            divergent = plan_path.read_bytes()
            self.assertEqual(1, _run(fixture.args(execute=True)))
            self.assertEqual(divergent, plan_path.read_bytes())
            self.assertEqual(sources_before, fixture.source_snapshot())

    def test_planname_und_bytes_sind_nur_von_queue_und_review_abhaengig(self) -> None:
        with tempfile.TemporaryDirectory() as first_root, tempfile.TemporaryDirectory() as second_root:
            first = CollectionPlanFixture(Path(first_root))
            second = CollectionPlanFixture(Path(second_root))

            self.assertEqual(0, _run(first.args(execute=True)))
            self.assertEqual(0, _run(second.args(execute=True)))
            first_plan = next(first.plans_root.glob("*.json"))
            second_plan = next(second.plans_root.glob("*.json"))

            self.assertEqual(first_plan.name, second_plan.name)
            self.assertEqual(first_plan.read_bytes(), second_plan.read_bytes())

    def test_manipulation_und_falsche_bindungen_werden_ohne_ausgabe_blockiert(self) -> None:
        mutations = {
            "candidates_sha": lambda fixture: fixture.candidates_path.write_bytes(
                fixture.candidates_path.read_bytes() + b" "
            ),
            "manifest_sha": lambda fixture: fixture.manifest_path.write_bytes(
                fixture.manifest_path.read_bytes() + b" "
            ),
            "queue_id": self._change_review_queue_id,
        }
        for name, mutate in mutations.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                fixture = CollectionPlanFixture(Path(temporary))
                mutate(fixture)
                protected = fixture.source_snapshot()

                self.assertEqual(1, _run(fixture.args(execute=True)))
                self.assertFalse(fixture.plans_root.exists())
                self.assertEqual(protected, fixture.source_snapshot())

        with tempfile.TemporaryDirectory() as temporary:
            fixture = CollectionPlanFixture(Path(temporary))
            protected = fixture.source_snapshot()

            self.assertEqual(
                1,
                _run(fixture.args(execute=True, reviewer="Andere Person")),
            )
            self.assertFalse(fixture.plans_root.exists())
            self.assertEqual(protected, fixture.source_snapshot())

    def test_teilreview_wird_auch_im_schreibfreien_lauf_blockiert(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = CollectionPlanFixture(Path(temporary))
            review = json.loads(fixture.review_path.read_text(encoding="utf-8"))
            del review["decisions"]["case-extra-b"]
            fixture.review_path.write_bytes(_json_bytes(review))
            sources_before = fixture.source_snapshot()

            self.assertEqual(1, _run(fixture.args()))
            self.assertFalse(fixture.plans_root.exists())
            self.assertEqual(sources_before, fixture.source_snapshot())

    @staticmethod
    def _change_review_queue_id(fixture: CollectionPlanFixture) -> None:
        review = json.loads(fixture.review_path.read_text(encoding="utf-8"))
        review["queue_id"] = "f" * 64
        fixture.review_path.write_bytes(_json_bytes(review))


if __name__ == "__main__":
    unittest.main()
