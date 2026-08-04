from __future__ import annotations

import hashlib
import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "evaluate_detect_release_holdout.py"
SPEC = importlib.util.spec_from_file_location(
    "evaluate_detect_release_holdout_tests",
    SCRIPT_PATH,
)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


CLASSES = ("klasse_a", "klasse_b")
POSITIVE_ID = "image-positive"
NEGATIVE_ID = "image-negative"
EXCLUDED_ID = "image-excluded"


def verified_image(image_id: str) -> MODULE.review_server.VerifiedImage:
    payload = image_id.encode("utf-8")
    return MODULE.review_server.VerifiedImage(
        candidate_id=image_id,
        relative_path=f"images/{image_id}.png",
        path=Path(f"{image_id}.png"),
        sha256=hashlib.sha256(payload).hexdigest(),
        size_bytes=len(payload),
        width=100,
        height=100,
        operator_references=(),
    )


def context(*image_ids: str) -> MODULE.EvaluationContext:
    return MODULE.EvaluationContext(
        knowledge_root=Path("knowledge"),
        holdout_root=Path("holdout"),
        candidate_root=Path("candidate"),
        candidate_id="candidate-a",
        candidate_manifest_path=Path("candidate/candidate_manifest.json"),
        candidate_manifest_sha256="1" * 64,
        weights_path=Path("candidate/best.pt"),
        weights_sha256="2" * 64,
        base_model_path=Path("models/base.pt"),
        base_model_sha256="3" * 64,
        holdout_id="holdout-a",
        manifest_sha256="4" * 64,
        candidates_sha256="5" * 64,
        class_map_version=3,
        class_map_sha256="6" * 64,
        vsa_manifest_sha256="7" * 64,
        classes=CLASSES,
        images=tuple(verified_image(image_id) for image_id in image_ids),
    )


def annotation(annotation_id: str = "a1", class_id: int = 0) -> dict[str, object]:
    return {
        "id": annotation_id,
        "class_id": class_id,
        "class_name": CLASSES[class_id],
        "box": {
            "x_center": 0.5,
            "y_center": 0.5,
            "width": 0.4,
            "height": 0.4,
        },
    }


def decisions() -> dict[str, dict[str, object]]:
    return {
        POSITIVE_ID: {
            "decision": "positive",
            "annotations": [annotation()],
        },
        NEGATIVE_ID: {
            "decision": "negative",
            "annotations": [],
        },
        EXCLUDED_ID: {
            "decision": "exclude",
            "annotations": [],
        },
    }


def detection(
    prediction_id: str,
    class_id: int,
) -> MODULE.inference_tools.RawDetection:
    return MODULE.inference_tools.RawDetection(
        prediction_id=prediction_id,
        class_id=class_id,
        class_name=CLASSES[class_id],
        confidence=0.9,
        box=MODULE.scoring.Box(0.5, 0.5, 0.4, 0.4),
    )


def image_prediction(
    image_id: str,
    *detections: MODULE.inference_tools.RawDetection,
    technical_error: str | None = None,
) -> MODULE.inference_tools.ImagePrediction:
    return MODULE.inference_tools.ImagePrediction(
        image_id=image_id,
        detections=tuple(detections),
        inference_time_ms=0.0 if technical_error is not None else 5.0,
        technical_error=technical_error,
    )


class EvaluateDetectReleaseHoldoutTests(unittest.TestCase):
    def test_scoring_selection_wertet_positive_und_negative_und_ignoriert_exclude(
        self,
    ) -> None:
        evaluation_context = context(POSITIVE_ID, NEGATIVE_ID, EXCLUDED_ID)
        predictions = [
            image_prediction(POSITIVE_ID, detection("p-positive", 0)),
            image_prediction(NEGATIVE_ID, detection("p-negative", 1)),
            image_prediction(EXCLUDED_ID, detection("p-excluded", 0)),
        ]

        selection = MODULE.build_scoring_selection(
            evaluation_context,
            decisions(),
            predictions,
        )
        metrics = MODULE.scoring.score_predictions(
            selection.truths,
            selection.predictions,
            {0: CLASSES[0], 1: CLASSES[1]},
            iou_threshold=0.5,
        )

        self.assertEqual({POSITIVE_ID}, set(selection.positive_image_ids))
        self.assertEqual({NEGATIVE_ID}, set(selection.negative_image_ids))
        self.assertEqual({EXCLUDED_ID}, set(selection.excluded_image_ids))
        self.assertEqual(
            {POSITIVE_ID, NEGATIVE_ID},
            {item.image_id for item in selection.predictions},
        )
        self.assertEqual(
            (1, 1, 0),
            tuple(metrics["micro"][key] for key in ("tp", "fp", "fn")),
        )
        by_class = {item["class_name"]: item for item in metrics["per_class"]}
        self.assertEqual((1, 0, 0), tuple(by_class[CLASSES[0]][key] for key in ("tp", "fp", "fn")))
        self.assertEqual((0, 1, 0), tuple(by_class[CLASSES[1]][key] for key in ("tp", "fp", "fn")))

    def test_technischer_fehler_auf_gewertetem_bild_stoppt_fail_closed(self) -> None:
        evaluation_context = context(POSITIVE_ID, NEGATIVE_ID, EXCLUDED_ID)

        for failed_image_id in (POSITIVE_ID, NEGATIVE_ID):
            with self.subTest(failed_image_id=failed_image_id):
                predictions = [
                    image_prediction(
                        image_id,
                        technical_error=(
                            "inference_failed:RuntimeError"
                            if image_id == failed_image_id
                            else None
                        ),
                    )
                    for image_id in (POSITIVE_ID, NEGATIVE_ID, EXCLUDED_ID)
                ]

                with self.assertRaisesRegex(ValueError, "technische Fehler"):
                    MODULE.build_scoring_selection(
                        evaluation_context,
                        decisions(),
                        predictions,
                    )

    def test_technischer_fehler_auf_exclude_wird_nur_gemeldet(self) -> None:
        evaluation_context = context(POSITIVE_ID, NEGATIVE_ID, EXCLUDED_ID)
        predictions = [
            image_prediction(POSITIVE_ID, detection("p-positive", 0)),
            image_prediction(NEGATIVE_ID),
            image_prediction(
                EXCLUDED_ID,
                technical_error="inference_failed:RuntimeError",
            ),
        ]

        selection = MODULE.build_scoring_selection(
            evaluation_context,
            decisions(),
            predictions,
        )

        self.assertEqual(
            (
                {
                    "image_id": EXCLUDED_ID,
                    "reason": "inference_failed:RuntimeError",
                },
            ),
            selection.excluded_technical_errors,
        )
        self.assertNotIn(
            EXCLUDED_ID,
            {item.image_id for item in selection.predictions},
        )

    def test_negativbild_metrik_zaehlt_bilder_und_detektionen_getrennt(self) -> None:
        predictions = [
            MODULE.scoring.Prediction(
                image_id="negative-a",
                prediction_id="p1",
                class_id=0,
                class_name=CLASSES[0],
                confidence=0.9,
                box=MODULE.scoring.Box(0.5, 0.5, 0.4, 0.4),
            ),
            MODULE.scoring.Prediction(
                image_id="negative-a",
                prediction_id="p2",
                class_id=1,
                class_name=CLASSES[1],
                confidence=0.8,
                box=MODULE.scoring.Box(0.25, 0.25, 0.2, 0.2),
            ),
            MODULE.scoring.Prediction(
                image_id="positive-a",
                prediction_id="ignored",
                class_id=0,
                class_name=CLASSES[0],
                confidence=0.7,
                box=MODULE.scoring.Box(0.5, 0.5, 0.2, 0.2),
            ),
        ]

        metrics = MODULE.compute_negative_false_alarm_metrics(
            {"negative-a", "negative-b"},
            predictions,
            CLASSES,
        )

        self.assertTrue(metrics["measured"])
        self.assertEqual(2, metrics["negative_images"])
        self.assertEqual(1, metrics["true_negative_images"])
        self.assertEqual(1, metrics["false_alarm_images"])
        self.assertEqual(["negative-a"], metrics["false_alarm_image_ids"])
        self.assertEqual(0.5, metrics["image_false_alarm_rate"])
        self.assertEqual(0.5, metrics["image_specificity"])
        self.assertEqual(2, metrics["false_alarm_detections"])
        self.assertEqual(1.0, metrics["false_alarm_detections_per_negative_image"])
        self.assertEqual({CLASSES[0]: 1, CLASSES[1]: 1}, metrics["detections_by_class"])

    def test_negativbild_metrik_ist_ohne_negative_ungeprueft_und_null(self) -> None:
        metrics = MODULE.compute_negative_false_alarm_metrics(
            [],
            [],
            CLASSES,
        )

        self.assertFalse(metrics["measured"])
        self.assertEqual(0, metrics["negative_images"])
        self.assertEqual(0, metrics["false_alarm_images"])
        self.assertEqual(0, metrics["false_alarm_detections"])
        self.assertIsNone(metrics["image_false_alarm_rate"])
        self.assertIsNone(metrics["image_specificity"])
        self.assertIsNone(metrics["false_alarm_detections_per_negative_image"])
        self.assertEqual({CLASSES[0]: 0, CLASSES[1]: 0}, metrics["detections_by_class"])

    def test_bericht_bleibt_auch_bei_bereitem_datensatz_diagnostic_only(self) -> None:
        evaluation_context = context(POSITIVE_ID, NEGATIVE_ID, EXCLUDED_ID)
        selection = MODULE.build_scoring_selection(
            evaluation_context,
            decisions(),
            [
                image_prediction(POSITIVE_ID, detection("p-positive", 0)),
                image_prediction(NEGATIVE_ID),
                image_prediction(EXCLUDED_ID),
            ],
        )
        object_metrics = MODULE.scoring.score_predictions(
            selection.truths,
            selection.predictions,
            {0: CLASSES[0], 1: CLASSES[1]},
        )
        negative_metrics = MODULE.compute_negative_false_alarm_metrics(
            selection.negative_image_ids,
            selection.predictions,
            CLASSES,
        )
        status = {
            "dataset_status": "ready_for_detect_evaluation",
            "class_coverage": [],
            "requirements": {},
            "shortfalls": [],
            "positive_physical_holdings": 1,
            "negative_physical_holdings": 1,
        }

        report = MODULE.build_report(
            evaluation_context,
            status,
            object_metrics,
            negative_metrics,
            selection,
            review_sha256="8" * 64,
            ledger_sha256="9" * 64,
            prediction_receipt_sha256="a" * 64,
            created_utc="2026-08-03T12:00:00Z",
            protocol={"device": "cpu"},
            runtime_versions={"python": "test"},
        )

        self.assertEqual(MODULE.EVALUATION_PURPOSE, report["purpose"])
        self.assertEqual(
            "diagnostic_only_ready_for_detect_evaluation",
            report["status"],
        )
        self.assertEqual("diagnostic_only", report["evaluation_role"])
        self.assertEqual(
            "ready_for_detect_evaluation",
            report["holdout"]["dataset_status"],
        )
        self.assertFalse(report["release_assessment"]["release_qualified"])
        self.assertFalse(report["release_assessment"]["auto_activation_allowed"])
        self.assertFalse(report["release_assessment"]["model_activated"])
        self.assertFalse(report["release_assessment"]["model_pointer_changed"])

    def test_status_und_review_bytes_muessen_dieselbe_fassung_sein(self) -> None:
        review_bytes = b"review-v1"
        status = {
            "bindings": {
                "review_sha256": hashlib.sha256(review_bytes).hexdigest(),
            }
        }

        MODULE._validate_status_review_binding(status, review_bytes)
        with self.assertRaisesRegex(ValueError, "zwischen Statuspruefung"):
            MODULE._validate_status_review_binding(status, b"review-v2")

    def test_ledger_custom_purpose_roundtrip(self) -> None:
        image_id = "ledger-image"
        image_bytes = b"ledger-image-bytes"
        image_sha256 = hashlib.sha256(image_bytes).hexdigest()
        evaluation_context = context(image_id)
        snapshot = MODULE.inference_tools.ImageSnapshot(
            image_id=image_id,
            image_sha256=image_sha256,
            image_bytes=image_bytes,
        )
        expected = image_prediction(image_id, detection("p1", 0))
        ledger = MODULE.inference_tools.build_prediction_ledger(
            evaluation_context,
            [snapshot],
            [expected],
            created_utc="2026-08-03T12:00:00Z",
            runtime_protocol={"device": "cpu"},
            runtime_versions={"python": "test"},
            purpose=MODULE.PREDICTION_PURPOSE,
        )

        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "predictions.json"
            MODULE.inference_tools.atomic_write_json_new(target, ledger)
            ledger_sha256 = MODULE.sha256_file(target)
            loaded, protocol = MODULE.inference_tools.load_prediction_ledger(
                target,
                ledger_sha256,
                evaluation_context,
                [snapshot],
                expected_purpose=MODULE.PREDICTION_PURPOSE,
            )

        self.assertEqual(MODULE.PREDICTION_PURPOSE, ledger["purpose"])
        self.assertEqual(1, len(loaded))
        self.assertEqual(
            MODULE.inference_tools._prediction_payload(expected),
            MODULE.inference_tools._prediction_payload(loaded[0]),
        )
        self.assertEqual("cpu", protocol["device"])
        self.assertFalse(protocol["technical_errors_count_as_negative"])


if __name__ == "__main__":
    unittest.main()
