from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = (
    Path(__file__).resolve().parents[1] / "evaluate_bcc_release_holdout.py"
)
SPEC = importlib.util.spec_from_file_location(
    "evaluate_bcc_release_holdout",
    MODULE_PATH,
)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class EvaluateBccReleaseHoldoutTests(unittest.TestCase):
    def test_binary_metrics_count_both_classes(self) -> None:
        outcomes = [
            MODULE.ScoredPrediction("p1", True, True, 1, 0.91),
            MODULE.ScoredPrediction("p2", True, False, 0, None),
            MODULE.ScoredPrediction("n1", False, True, 2, 0.80),
            MODULE.ScoredPrediction("n2", False, False, 0, None),
        ]

        metrics = MODULE.compute_binary_metrics(outcomes)

        self.assertEqual(1, metrics["true_positive"])
        self.assertEqual(1, metrics["false_negative"])
        self.assertEqual(1, metrics["false_positive"])
        self.assertEqual(1, metrics["true_negative"])
        self.assertAlmostEqual(0.5, metrics["sensitivity"])
        self.assertAlmostEqual(0.5, metrics["specificity"])
        self.assertAlmostEqual(0.5, metrics["balanced_accuracy"])
        self.assertEqual(
            MODULE.wilson_interval(1, 2),
            metrics["sensitivity_wilson_95"],
        )

    def test_technical_error_never_becomes_negative_result(self) -> None:
        raw = [
            MODULE.RawPrediction("p1", True, 1, 0.9, 12.0, None),
            MODULE.RawPrediction(
                "n1",
                False,
                0,
                None,
                0.0,
                "frame_unusable:too_dark",
            ),
        ]

        result = MODULE.score_candidate(
            candidate_id="candidate-a",
            weights_sha256="a" * 64,
            production_eligible=True,
            raw_predictions=raw,
            labels={"p1": True, "n1": False},
        )

        self.assertEqual("incomplete", result["evaluation_status"])
        self.assertIsNone(result["metrics"])
        self.assertEqual(1, result["technical_error_count"])
        self.assertEqual(
            "frame_unusable:too_dark",
            result["technical_errors"][0]["reason"],
        )

    def test_ranking_excludes_incomplete_and_non_production_candidates(self) -> None:
        candidates = [
            self._candidate_result(
                "best-but-legacy",
                balanced_accuracy=0.99,
                specificity=0.99,
                sensitivity=0.99,
                production_eligible=False,
            ),
            self._candidate_result(
                "eligible-b",
                balanced_accuracy=0.90,
                specificity=0.95,
                sensitivity=0.85,
            ),
            self._candidate_result(
                "eligible-a",
                balanced_accuracy=0.90,
                specificity=0.90,
                sensitivity=0.90,
            ),
            {
                **self._candidate_result(
                    "incomplete",
                    balanced_accuracy=1.0,
                    specificity=1.0,
                    sensitivity=1.0,
                ),
                "evaluation_status": "incomplete",
            },
        ]

        ranking = MODULE.rank_candidates(candidates)

        self.assertEqual(["eligible-b", "eligible-a"], ranking)

    def test_prediction_receipt_is_order_independent(self) -> None:
        first = {
            "candidate-b": [
                MODULE.RawPrediction("item-2", False, 0, None, 2.0, None),
                MODULE.RawPrediction("item-1", True, 1, 0.8, 1.0, None),
            ],
            "candidate-a": [
                MODULE.RawPrediction("item-1", False, 0, None, 3.0, None),
            ],
        }
        second = {
            "candidate-a": list(first["candidate-a"]),
            "candidate-b": list(reversed(first["candidate-b"])),
        }

        self.assertEqual(
            MODULE.prediction_receipt_sha256(first),
            MODULE.prediction_receipt_sha256(second),
        )

    def test_written_prediction_ledger_is_the_scored_source(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "ledger.json"
            binding = MODULE.CandidateBinding(
                candidate_id="candidate-a",
                candidate_manifest_path=Path("manifest.json"),
                candidate_manifest_sha256="a" * 64,
                weights_path=Path("best.pt"),
                weights_sha256="b" * 64,
                dataset_plan_id="plan",
                dataset_manifest_sha256="c" * 64,
                map50=0.5,
                epochs_completed=40,
                created_utc="2026-07-28T12:00:00Z",
                production_manifest_eligible=True,
                production_manifest_reason="ok",
                diagnostic_marker_present=False,
                diagnostic_marker_sha256=None,
            )
            cases = (
                MODULE.BlindCase("i1", Path("i1.jpg"), "1" * 64),
                MODULE.BlindCase("i2", Path("i2.jpg"), "2" * 64),
            )
            context = MODULE.EvaluationContext(
                holdout_root=Path("holdout"),
                review_path=Path("review.json"),
                holdout_id="f" * 64,
                holdout_manifest_sha256="d" * 64,
                holdout_candidates_sha256="e" * 64,
                review_sha256="9" * 64,
                review_bytes=b"review",
                candidate_scope_sha256="8" * 64,
                frozen_candidate_scope=(),
                cases=cases,
                review_labels=(("i1", True), ("i2", False)),
                excluded_item_ids=(),
                positive_images=1,
                negative_images=1,
                excluded_images=0,
            )
            snapshots = [
                MODULE.ImageSnapshot("i1", "1" * 64, b"one"),
                MODULE.ImageSnapshot("i2", "2" * 64, b"two"),
            ]
            predictions = {
                "candidate-a": [
                    MODULE.RawPrediction("i1", True, 1, 0.8, 1.0, None),
                    MODULE.RawPrediction("i2", False, 0, None, 2.0, None),
                ]
            }
            ledger = MODULE.build_prediction_ledger(
                context,
                [binding],
                snapshots,
                predictions,
                devices_by_candidate={"candidate-a": "cuda:0"},
                frame_quality_settings={"frame_min_brightness": 4.0},
                created_utc="2026-07-28T20:00:00Z",
                runtime_versions={"python": "3.12"},
            )
            MODULE.atomic_write_json_new(target, ledger)
            file_sha = MODULE.sha256_file(target)

            loaded, device = MODULE.load_prediction_ledger(
                target,
                file_sha,
                context,
                [binding],
                snapshots,
            )

            self.assertEqual("cuda:0", device)
            self.assertEqual(predictions, loaded)

    def test_candidate_binding_rejects_changed_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            candidate_root = root / "training" / "models" / "candidates"
            candidate = candidate_root / "candidate-a"
            candidate.mkdir(parents=True)
            weights = candidate / "best.pt"
            weights.write_bytes(b"weights")
            manifest = self._manifest(
                weights_sha256=hashlib.sha256(b"weights").hexdigest()
            )
            manifest_path = candidate / "candidate_manifest.json"
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            frozen = [
                {
                    "candidate_id": "candidate-a",
                    "candidate_manifest_sha256": "0" * 64,
                    "weights_sha256": hashlib.sha256(b"weights").hexdigest(),
                }
            ]

            with self.assertRaisesRegex(ValueError, "Manifest-SHA"):
                MODULE.load_candidate_bindings(root, frozen)

    def test_candidate_binding_marks_legacy_manifest_not_production_eligible(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            candidate_root = root / "training" / "models" / "candidates"
            candidate = candidate_root / "candidate-a"
            candidate.mkdir(parents=True)
            weights = candidate / "best.pt"
            weights.write_bytes(b"weights")
            weights_sha = hashlib.sha256(b"weights").hexdigest()
            manifest = self._manifest(weights_sha256=weights_sha)
            manifest["training"].pop("epochs_completed")
            manifest_path = candidate / "candidate_manifest.json"
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            frozen = [
                {
                    "candidate_id": "candidate-a",
                    "candidate_manifest_sha256": MODULE.sha256_file(
                        manifest_path
                    ),
                    "weights_sha256": weights_sha,
                }
            ]

            bindings = MODULE.load_candidate_bindings(root, frozen)

            self.assertEqual(1, len(bindings))
            self.assertFalse(bindings[0].production_manifest_eligible)
            self.assertIn(
                "epochs_completed",
                bindings[0].production_manifest_reason,
            )

    def test_atomic_report_refuses_existing_target(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "report.json"
            target.write_text("bestehend", encoding="utf-8")

            with self.assertRaises(FileExistsError):
                MODULE.atomic_write_json_new(target, {"status": "neu"})

            self.assertEqual("bestehend", target.read_text(encoding="utf-8"))

    def test_report_contains_no_source_paths_or_hidden_hints(self) -> None:
        report = MODULE.build_report(
            holdout_id="f" * 64,
            holdout_manifest_sha256="a" * 64,
            holdout_candidates_sha256="b" * 64,
            review_sha256="c" * 64,
            candidate_scope_sha256="d" * 64,
            device="cuda:0",
            prediction_receipt_sha256="e" * 64,
            prediction_ledger_sha256="1" * 64,
            frame_quality_settings={
                "frame_min_brightness": 4.0,
                "frame_max_brightness": 250.0,
            },
            candidate_results=[
                self._candidate_result(
                    "candidate-a",
                    balanced_accuracy=1.0,
                    specificity=1.0,
                    sensitivity=1.0,
                )
            ],
            positive_images=29,
            negative_images=31,
            excluded_images=0,
            created_utc="2026-07-28T20:00:00Z",
            runtime_versions={"python": "3.12"},
        )
        serialized = json.dumps(report, ensure_ascii=False)

        self.assertNotIn("frame_path", serialized)
        self.assertNotIn("haltung_key", serialized)
        self.assertNotIn("hidden_hint", serialized)
        self.assertEqual(0.25, report["protocol"]["confidence_threshold"])
        self.assertEqual(1280, report["protocol"]["image_size"])
        self.assertFalse(report["release_assessment"]["auto_activation_allowed"])

    def test_prediction_matrix_requires_every_candidate_and_image_once(
        self,
    ) -> None:
        binding = MODULE.CandidateBinding(
            candidate_id="candidate-a",
            candidate_manifest_path=Path("manifest.json"),
            candidate_manifest_sha256="a" * 64,
            weights_path=Path("best.pt"),
            weights_sha256="b" * 64,
            dataset_plan_id="plan",
            dataset_manifest_sha256="c" * 64,
            map50=0.5,
            epochs_completed=40,
            created_utc="2026-07-28T12:00:00Z",
            production_manifest_eligible=True,
            production_manifest_reason="ok",
            diagnostic_marker_present=False,
            diagnostic_marker_sha256=None,
        )
        snapshots = [
            MODULE.ImageSnapshot("i1", "1" * 64, b"one"),
            MODULE.ImageSnapshot("i2", "2" * 64, b"two"),
        ]
        duplicate = [
            MODULE.RawPrediction("i1", False, 0, None, 1.0, None),
            MODULE.RawPrediction("i1", False, 0, None, 1.0, None),
        ]

        with self.assertRaisesRegex(ValueError, "exakt eine Vorhersage"):
            MODULE.validate_prediction_matrix(
                [binding],
                snapshots,
                {"candidate-a": duplicate},
            )

        with self.assertRaisesRegex(ValueError, "eingefrorenen Kandidaten"):
            MODULE.validate_prediction_matrix(
                [binding],
                snapshots,
                {},
            )

    def test_excluded_image_error_is_reported_but_not_scored(self) -> None:
        raw = [
            MODULE.RawPrediction("p1", True, 1, 0.9, 10.0, None),
            MODULE.RawPrediction(
                "excluded",
                None,
                0,
                None,
                0.0,
                "frame_unusable:too_dark",
            ),
        ]

        result = MODULE.score_candidate(
            candidate_id="candidate-a",
            weights_sha256="a" * 64,
            production_eligible=True,
            raw_predictions=raw,
            labels={"p1": True},
        )

        self.assertEqual("complete", result["evaluation_status"])
        self.assertEqual(0, result["technical_error_count"])
        self.assertEqual(1, result["excluded_technical_error_count"])
        self.assertEqual(1, result["metrics"]["images"])

    def test_wilson_interval_has_known_reference_value(self) -> None:
        interval = MODULE.wilson_interval(1, 2)

        self.assertIsNotNone(interval)
        self.assertAlmostEqual(0.0945312057, interval["lower"], places=9)
        self.assertAlmostEqual(0.9054687943, interval["upper"], places=9)
        self.assertIsNone(MODULE.wilson_interval(0, 0))

    def test_strict_json_rejects_duplicate_keys(self) -> None:
        with self.assertRaisesRegex(ValueError, "doppelten Schluessel"):
            MODULE.strict_json_from_bytes(
                b'{"purpose":"first","purpose":"second"}',
                "Test",
            )

    def test_empty_report_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "leerer Kandidatenvergleich"):
            MODULE.build_report(
                holdout_id="f" * 64,
                holdout_manifest_sha256="a" * 64,
                holdout_candidates_sha256="b" * 64,
                review_sha256="c" * 64,
                candidate_scope_sha256="d" * 64,
                device="cuda:0",
                prediction_receipt_sha256="e" * 64,
                prediction_ledger_sha256="1" * 64,
                frame_quality_settings={},
                candidate_results=[],
                positive_images=29,
                negative_images=31,
                excluded_images=0,
                created_utc="2026-07-28T20:00:00Z",
                runtime_versions={"python": "3.12"},
            )

    @staticmethod
    def _candidate_result(
        candidate_id: str,
        *,
        balanced_accuracy: float,
        specificity: float,
        sensitivity: float,
        production_eligible: bool = True,
    ) -> dict:
        return {
            "candidate_id": candidate_id,
            "weights_sha256": candidate_id.ljust(64, "0")[:64],
            "production_eligible": production_eligible,
            "evaluation_status": "complete",
            "technical_error_count": 0,
            "technical_errors": [],
            "metrics": {
                "balanced_accuracy": balanced_accuracy,
                "specificity": specificity,
                "sensitivity": sensitivity,
                "false_positive": 0,
                "false_negative": 0,
            },
            "items": [],
        }

    @staticmethod
    def _manifest(*, weights_sha256: str) -> dict:
        return {
            "schema_version": "1.0",
            "candidate_status": "not_deployed",
            "pilot": "BCC_bogen",
            "created_utc": "2026-07-28T12:00:00Z",
            "dataset": {"images": 48},
            "training": {
                "epochs_completed": 40,
                "results": {"metrics/mAP50(B)": 0.5},
            },
            "weights": {"candidate_sha256": weights_sha256},
        }


if __name__ == "__main__":
    unittest.main()
