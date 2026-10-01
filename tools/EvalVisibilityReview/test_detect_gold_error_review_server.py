from __future__ import annotations

import copy
import hashlib
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from tools.EvalVisibilityReview.detect_gold_error_review_server import (
    DetectGoldErrorReviewStore,
)


class DetectGoldErrorReviewStoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary = tempfile.TemporaryDirectory()
        self.root = Path(self._temporary.name)
        self.knowledge_root = self.root / "KI_BRAIN"
        self.now = lambda: "2026-08-02T16:30:00Z"
        self._write_fixture()

    def tearDown(self) -> None:
        self._temporary.cleanup()

    def test_gueltige_queue_zeigt_overlays_und_speichert_vollstaendig_gebunden(
        self,
    ) -> None:
        protected_before = self._protected_snapshot()
        manifest_before = (self.queue / "_manifest.json").read_bytes()
        candidates_before = (self.queue / "_candidates.json").read_bytes()
        store = self._store()

        state = store.state()
        item = state["items"][0]
        self.assertEqual("wrong_class", item["case_type"])
        self.assertEqual("BAB_riss", item["ground_truth"]["class_name"])
        self.assertEqual("BBC_ablagerung", item["prediction"]["class_name"])
        self.assertEqual(
            {"x_center": 0.4, "y_center": 0.5, "width": 0.2, "height": 0.3},
            item["ground_truth"]["box"],
        )
        self.assertEqual(
            {"x_center": 0.42, "y_center": 0.5, "width": 0.2, "height": 0.3},
            item["prediction"]["box"],
        )
        self.assertEqual(f"/image?id={self.case_id}", item["image_url"])
        image, content_type = store.image_bytes_for(self.case_id)
        self.assertEqual(self.image_payload, image)
        self.assertEqual("image/jpeg", content_type)

        store.prepare_output()
        real_replace = os.replace
        with mock.patch(
            "tools.EvalVisibilityReview.detect_gold_error_review_server.os.replace",
            side_effect=real_replace,
        ) as replace:
            result = store.set_decision(
                self.case_id,
                "confirmed_model_error",
                "Gold und sichtbare Situation stimmen.",
            )

        saved = self._read_json(self.output)
        self.assertEqual("1.0", saved["schema_version"])
        self.assertEqual("detect_gold_failure_review", saved["purpose"])
        self.assertEqual(self.queue_id, saved["queue_id"])
        self.assertEqual(
            self._sha(manifest_before),
            saved["queue_manifest_sha256"],
        )
        self.assertEqual(
            self._sha(candidates_before),
            saved["candidates_sha256"],
        )
        for field in (
            "evaluation_report_sha256",
            "prediction_ledger_sha256",
            "candidate_manifest_sha256",
            "weights_sha256",
            "current_gold_audit_sha256",
            "class_map_sha256",
        ):
            with self.subTest(binding=field):
                self.assertEqual(self.bindings[field], saved[field])
        self.assertEqual("Besitzer", saved["reviewer"])
        self.assertEqual(
            {
                "decision": "confirmed_model_error",
                "comment": "Gold und sichtbare Situation stimmen.",
                "reviewed_at_utc": "2026-08-02T16:30:00Z",
            },
            saved["decisions"][self.case_id],
        )
        self.assertEqual(1, result["counts"]["confirmed_model_error"])
        self.assertTrue(replace.called, "Review muss atomar ersetzt werden.")
        self.assertEqual(protected_before, self._protected_snapshot())

    def test_nur_drei_diagnoseentscheidungen_sind_zulaessig(self) -> None:
        store = self._store()
        store.prepare_output()

        for decision in (
            "confirmed_model_error",
            "gold_suspect",
            "exclude_uncertain",
        ):
            with self.subTest(decision=decision):
                state = store.set_decision(self.case_id, decision)
                self.assertEqual(decision, state["items"][0]["decision"])

        for decision in ("wrong_class", "positive", "approved", ""):
            with self.subTest(invalid=decision):
                with self.assertRaisesRegex(ValueError, "ungueltig|Entscheidung"):
                    store.set_decision(self.case_id, decision)

    def test_ausgabe_muss_ausserhalb_der_queue_liegen_und_reviewer_ist_pflicht(
        self,
    ) -> None:
        with self.assertRaisesRegex(ValueError, "ausserhalb|Ausgabe"):
            self._store(output=self.queue / "review.json")

        with self.assertRaisesRegex(ValueError, "Reviewer"):
            self._store(reviewer="   ")

    def test_manifest_policy_und_queue_id_werden_fail_closed_validiert(self) -> None:
        original = self._read_json(self.queue / "_manifest.json")
        candidates = self._read_json(self.queue / "_candidates.json")
        mutations = {
            "falscher_zweck": lambda value: value.__setitem__("purpose", "training_queue"),
            "falsche_rolle": lambda value: value.__setitem__("role", "training"),
            "nicht_eingefroren": lambda value: value.__setitem__("frozen", False),
            "training_erlaubt": lambda value: value["policy"].__setitem__(
                "training_eligible", True
            ),
            "quellmutation_erlaubt": lambda value: value["policy"].__setitem__(
                "source_mutation_allowed", True
            ),
            "fremdes_feld": lambda value: value.__setitem__("auto_publish", True),
        }

        for name, mutate in mutations.items():
            with self.subTest(name=name):
                changed = copy.deepcopy(original)
                mutate(changed)
                changed["queue_id"] = self._queue_id(changed, candidates)
                self._write_json(self.queue / "_manifest.json", changed)
                with self.assertRaises((ValueError, OSError)):
                    self._store()

        self._write_json(self.queue / "_manifest.json", original)
        changed = copy.deepcopy(original)
        changed["queue_id"] = "f" * 64
        self._write_json(self.queue / "_manifest.json", changed)
        with self.assertRaisesRegex(ValueError, "Queue|queue|ID"):
            self._store()

    def test_kandidatenpfad_bildhash_und_overlaydaten_werden_strikt_validiert(
        self,
    ) -> None:
        original_candidates = self._read_json(self.queue / "_candidates.json")
        original_manifest = self._read_json(self.queue / "_manifest.json")
        outside = self.root / "outside.jpg"
        outside.write_bytes(self.image_payload)

        def outside_path(rows: list[dict[str, object]]) -> None:
            rows[0]["frame_path"] = "../outside.jpg"

        def wrong_hash(rows: list[dict[str, object]]) -> None:
            rows[0]["source_sha256"] = "f" * 64

        def wrong_case_type(rows: list[dict[str, object]]) -> None:
            rows[0]["case_type"] = "true_positive"

        def wrong_status(rows: list[dict[str, object]]) -> None:
            rows[0]["status"] = "approved"

        def invalid_box(rows: list[dict[str, object]]) -> None:
            rows[0]["ground_truth"]["box"]["width"] = 1.5

        mutations = {
            "ausserhalb_gold_frames": outside_path,
            "falscher_bildhash": wrong_hash,
            "unbekannter_falltyp": wrong_case_type,
            "nicht_offen": wrong_status,
            "ungueltige_box": invalid_box,
        }
        for name, mutate in mutations.items():
            with self.subTest(name=name):
                rows = copy.deepcopy(original_candidates)
                mutate(rows)
                manifest = copy.deepcopy(original_manifest)
                manifest["queue_id"] = self._queue_id(manifest, rows)
                self._write_json(self.queue / "_candidates.json", rows)
                self._write_json(self.queue / "_manifest.json", manifest)
                with self.assertRaises((ValueError, OSError)):
                    self._store()

        self._write_json(self.queue / "_candidates.json", original_candidates)
        self._write_json(self.queue / "_manifest.json", original_manifest)

    def test_bildbytes_werden_auch_beim_abruf_erneut_hashgeprueft(self) -> None:
        store = self._store()
        self.gold_image.write_bytes(self.gold_image.read_bytes() + b"changed")

        with self.assertRaisesRegex(ValueError, "Hash|veraendert|Bild"):
            store.image_bytes_for(self.case_id)
        with self.assertRaises((KeyError, ValueError)):
            store.image_bytes_for("../training_samples.json")

    def test_report_ledger_und_hashbindungen_duerfen_nicht_driften(self) -> None:
        report_before = self.report_path.read_bytes()
        ledger_before = self.ledger_path.read_bytes()

        self.report_path.write_bytes(report_before + b" ")
        with self.assertRaisesRegex(ValueError, "Bericht|Report|SHA|Hash"):
            self._store()
        self.report_path.write_bytes(report_before)

        self.ledger_path.write_bytes(ledger_before + b" ")
        with self.assertRaisesRegex(ValueError, "Ledger|Vorhersage|SHA|Hash"):
            self._store()
        self.ledger_path.write_bytes(ledger_before)

        manifest = self._read_json(self.queue / "_manifest.json")
        manifest["bindings"]["weights_sha256"] = "kein-sha"
        manifest["queue_id"] = self._queue_id(
            manifest,
            self._read_json(self.queue / "_candidates.json"),
        )
        self._write_json(self.queue / "_manifest.json", manifest)
        with self.assertRaisesRegex(ValueError, "weights_sha256|Gewicht|SHA"):
            self._store()

    def test_parallele_aenderung_der_review_datei_wird_nicht_ueberschrieben(
        self,
    ) -> None:
        first = self._store()
        second = self._store()
        first.prepare_output()

        with self.assertRaisesRegex(ValueError, "parallel|veraendert"):
            second.prepare_output()

        saved = self._read_json(self.output)
        self.assertEqual({}, saved["decisions"])

    def _store(
        self,
        *,
        output: Path | None = None,
        reviewer: str = "Besitzer",
    ) -> DetectGoldErrorReviewStore:
        return DetectGoldErrorReviewStore(
            knowledge_root=self.knowledge_root,
            queue_root=self.queue,
            output_path=output or self.output,
            reviewer=reviewer,
            now_utc=self.now,
        )

    def _write_fixture(self) -> None:
        self.knowledge_root.mkdir(parents=True)
        self.case_id = "dgf-wrong-class-a"
        self.image_payload = b"\xff\xd8\xff\xe0" + b"x" * 2_048
        image_sha = self._sha(self.image_payload)
        self.gold_image = (
            self.knowledge_root
            / "gold_frames"
            / "BAB - Riss"
            / f"gold_{image_sha}.jpg"
        )
        self.gold_image.parent.mkdir(parents=True)
        self.gold_image.write_bytes(self.image_payload)

        training = self.knowledge_root / "training"
        reports = training / "reports"
        candidate_dir = training / "models" / "candidates" / "candidate-a"
        class_map_dir = training / "class_maps"
        reports.mkdir(parents=True)
        candidate_dir.mkdir(parents=True)
        class_map_dir.mkdir(parents=True)

        self.weights_path = candidate_dir / "best.pt"
        self.weights_path.write_bytes(b"detect-gold-weights-v1")
        self.candidate_manifest_path = candidate_dir / "candidate_manifest.json"
        self._write_json(
            self.candidate_manifest_path,
            {
                "schema_version": "1.0",
                "candidate_status": "not_deployed",
                "candidate_kind": "detect_gold",
                "weights": {"candidate_sha256": self._sha_file(self.weights_path)},
            },
        )
        self.audit_path = reports / "gold_stock_audit.json"
        self._write_json(
            self.audit_path,
            {
                "schema_version": "1.1",
                "bericht": "gold_stock_audit",
                "samples": [{"sample_id": "sample-a", "image_sha256": image_sha}],
            },
        )
        self.class_map_path = class_map_dir / "detect_classes_v3.json"
        self._write_json(
            self.class_map_path,
            {
                "schema_version": "1.0",
                "version": 3,
                "classes": ["BAB_riss", "BBC_ablagerung"],
            },
        )
        self.training_samples_path = self.knowledge_root / "training_samples.json"
        self._write_json(
            self.training_samples_path,
            [
                {
                    "SampleId": "sample-a",
                    "FramePath": str(self.gold_image),
                    "Code": "BABBB",
                    "Role": "test",
                }
            ],
        )

        provenance_bindings = {
            "candidate_id": "candidate-a",
            "candidate_manifest_sha256": self._sha_file(
                self.candidate_manifest_path
            ),
            "weights_sha256": self._sha_file(self.weights_path),
            "dataset_plan_id": "1" * 64,
            "dataset_manifest_sha256": "2" * 64,
            "dataset_receipt_sha256": "3" * 64,
            "registry_sha256": "4" * 64,
            "detect_all_receipt_sha256": "5" * 64,
            "base_gold_audit_sha256": "6" * 64,
            "base_training_samples_sha256": "7" * 64,
            "current_gold_audit_sha256": self._sha_file(self.audit_path),
            "current_training_samples_sha256": self._sha_file(
                self.training_samples_path
            ),
            "class_map_sha256": self._sha_file(self.class_map_path),
            "migration_sha256": "8" * 64,
            "vsa_manifest_sha256": "9" * 64,
            "base_model_training_inventory_available": False,
        }
        prediction_rows = [
            {
                "image_id": image_sha,
                "detections": [
                    {
                        "prediction_id": "prediction-a",
                        "class_id": 1,
                        "class_name": "BBC_ablagerung",
                        "confidence": 0.91,
                        "box": {
                            "x_center": 0.42,
                            "y_center": 0.5,
                            "width": 0.2,
                            "height": 0.3,
                        },
                    }
                ],
                "inference_time_ms": 12.5,
                "technical_error": None,
            }
        ]
        prediction_receipt = self._sha(self._canonical_bytes(prediction_rows))
        self.ledger_path = reports / "detect_gold_holdout_predictions_test.json"
        self._write_json(
            self.ledger_path,
            {
                "schema_version": "1.0",
                "purpose": "detect_gold_positive_holdout_predictions",
                "created_utc": "2026-08-02T16:00:00Z",
                "warning": "Nur Auswertung. Nicht fuer Training verwenden.",
                "bindings": provenance_bindings,
                "protocol": {
                    "confidence_threshold": 0.25,
                    "image_size": 1280,
                    "iou_threshold": 0.5,
                    "threshold_sweep": False,
                    "technical_errors_count_as_negative": False,
                    "device": "cpu",
                },
                "runtime": {},
                "images": [{"image_id": image_sha, "image_sha256": image_sha}],
                "predictions": prediction_rows,
                "prediction_receipt_sha256": prediction_receipt,
            },
        )
        self.report_path = reports / "detect_gold_holdout_evaluation_test.json"
        report_bindings = {
            **provenance_bindings,
            "prediction_ledger_sha256": self._sha_file(self.ledger_path),
            "prediction_receipt_sha256": prediction_receipt,
        }
        self._write_json(
            self.report_path,
            {
                "schema_version": "1.0",
                "purpose": "detect_gold_positive_holdout_evaluation",
                "created_utc": "2026-08-02T16:00:01Z",
                "warning": "Positiver Holdout. Nicht fuer Training verwenden.",
                "status": "positive_holdout_only_not_release_qualified",
                "bindings": report_bindings,
                "protocol": {
                    "confidence_threshold": 0.25,
                    "image_size": 1280,
                    "iou_threshold": 0.5,
                    "threshold_sweep": False,
                    "technical_errors_count_as_negative": False,
                    "device": "cpu",
                },
                "runtime": {},
                "holdout": {
                    "raw_mapped_instances": 1,
                    "raw_mapped_images": 1,
                    "eligible_instances": 1,
                    "eligible_images": 1,
                    "eligible_physical_holdings": 1,
                    "clean_negative_images": 0,
                    "positive_only": True,
                    "class_distribution": {
                        "BAB_riss": 1,
                        "BBC_ablagerung": 0,
                    },
                    "excluded_holdings": [],
                },
                "metrics": {
                    "iou_threshold": 0.5,
                    "images": 1,
                    "ground_truth_instances": 1,
                    "predictions": 1,
                    "micro": {
                        "tp": 0,
                        "fp": 1,
                        "fn": 1,
                        "precision": 0.0,
                        "recall": 0.0,
                        "f1": 0.0,
                    },
                    "macro": {
                        "classes": 1,
                        "precision": 0.0,
                        "recall": 0.0,
                        "f1": 0.0,
                    },
                    "per_class": [
                        {
                            "class_id": 0,
                            "class_name": "BAB_riss",
                            "support": 1,
                            "measured": True,
                            "tp": 0,
                            "fp": 0,
                            "fn": 1,
                            "precision": 0.0,
                            "recall": 0.0,
                            "f1": 0.0,
                        },
                        {
                            "class_id": 1,
                            "class_name": "BBC_ablagerung",
                            "support": 0,
                            "measured": False,
                            "tp": 0,
                            "fp": 1,
                            "fn": 0,
                            "precision": 0.0,
                            "recall": 0.0,
                            "f1": 0.0,
                        },
                    ],
                    "exact_matches": [],
                    "geometry": {
                        "matched": 1,
                        "unmatched_ground_truth": 0,
                        "unmatched_predictions": 0,
                        "confusion": [
                            {
                                "expected_class": "BAB_riss",
                                "predicted_class": "BBC_ablagerung",
                                "count": 1,
                            }
                        ],
                        "matches": [
                            {
                                "image_id": image_sha,
                                "sample_id": "sample-a",
                                "prediction_id": "prediction-a",
                                "expected_class": "BAB_riss",
                                "predicted_class": "BBC_ablagerung",
                                "iou": 0.72,
                                "confidence": 0.91,
                            }
                        ],
                    },
                },
                "release_assessment": {
                    "status": "positive_holdout_only_not_release_qualified",
                    "release_qualified": False,
                    "auto_activation_allowed": False,
                    "model_activated": False,
                    "fresh_negative_holdout_required": True,
                    "reason": "Der positive Holdout ist kein Release-Nachweis.",
                },
                "limitations": ["Nur positiver Holdout."],
            },
        )

        self.bindings = {
            **report_bindings,
            "evaluation_report_path": self.report_path.relative_to(
                self.knowledge_root
            ).as_posix(),
            "evaluation_report_sha256": self._sha_file(self.report_path),
            "prediction_ledger_path": self.ledger_path.relative_to(
                self.knowledge_root
            ).as_posix(),
        }
        candidates = [
            {
                "id": self.case_id,
                "image_id": image_sha,
                "frame_path": self.gold_image.relative_to(
                    self.knowledge_root
                ).as_posix(),
                "source_sha256": image_sha,
                "holding_key": "100-200",
                "physical_holding_key": "100|200",
                "case_type": "wrong_class",
                "ground_truth": {
                    "sample_id": "sample-a",
                    "code": "BABBB",
                    "class_id": 0,
                    "class_name": "BAB_riss",
                    "box": {
                        "x_center": 0.4,
                        "y_center": 0.5,
                        "width": 0.2,
                        "height": 0.3,
                    },
                },
                "prediction": {
                    "prediction_id": "prediction-a",
                    "class_id": 1,
                    "class_name": "BBC_ablagerung",
                    "confidence": 0.91,
                    "box": {
                        "x_center": 0.42,
                        "y_center": 0.5,
                        "width": 0.2,
                        "height": 0.3,
                    },
                },
                "iou": 0.72,
                "status": "pending_review",
            }
        ]
        manifest = {
            "schema_version": "1.0",
            "purpose": "detect_gold_failure_review_queue",
            "role": "diagnostic_only",
            "frozen": True,
            "created_utc": "2026-08-02T16:05:00Z",
            "warning": "Nur Diagnose. Keine Trainingsfreigabe.",
            "bindings": self.bindings,
            "policy": {
                "training_eligible": False,
                "export_allowed": False,
                "source_mutation_allowed": False,
                "image_copies_created": False,
            },
            "summary": {
                "total": 1,
                "wrong_class": 1,
                "missed": 0,
                "extra_prediction": 0,
            },
        }
        self.queue_id = self._queue_id(manifest, candidates)
        manifest["queue_id"] = self.queue_id
        self.queue = (
            training
            / "failure_review"
            / "queues"
            / f"detect_gold_failure_{self.queue_id[:12]}"
        )
        self.queue.mkdir(parents=True)
        self._write_json(self.queue / "_manifest.json", manifest)
        self._write_json(self.queue / "_candidates.json", candidates)
        self.output = (
            training
            / "failure_review"
            / "reviews"
            / f"detect_gold_failure_{self.queue_id[:12]}_review.json"
        )

    def _protected_snapshot(self) -> dict[str, bytes]:
        protected = [
            self.training_samples_path,
            self.gold_image,
            self.report_path,
            self.ledger_path,
            self.candidate_manifest_path,
            self.weights_path,
            self.audit_path,
            self.class_map_path,
            self.queue / "_manifest.json",
            self.queue / "_candidates.json",
        ]
        return {str(path): path.read_bytes() for path in protected}

    @classmethod
    def _queue_id(
        cls,
        manifest: dict[str, object],
        candidates: list[dict[str, object]],
    ) -> str:
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
        semantic["candidates"] = sorted(candidates, key=lambda item: str(item["id"]))
        return cls._sha(cls._canonical_bytes(semantic))

    @staticmethod
    def _canonical_bytes(value: object) -> bytes:
        return json.dumps(
            value,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")

    @staticmethod
    def _sha(payload: bytes) -> str:
        return hashlib.sha256(payload).hexdigest()

    @classmethod
    def _sha_file(cls, path: Path) -> str:
        return cls._sha(path.read_bytes())

    @staticmethod
    def _write_json(path: Path, value: object) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(
            (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        )

    @staticmethod
    def _read_json(path: Path):
        return json.loads(path.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
