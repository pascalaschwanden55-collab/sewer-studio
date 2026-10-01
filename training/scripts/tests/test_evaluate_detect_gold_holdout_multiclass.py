from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "evaluate_detect_gold_holdout.py"
SPEC = importlib.util.spec_from_file_location(
    "evaluate_detect_gold_holdout_multiclass",
    SCRIPT_PATH,
)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


IMAGE_ID = "1" * 64
IMAGE_SHA256 = "2" * 64


class ProvenanceStub:
    classes = ("klasse_a", "klasse_b")
    excluded_holdings = ()

    def __init__(self) -> None:
        instance = SimpleNamespace(
            sample_id="gold-1",
            class_id=0,
            class_name="klasse_a",
            box=SimpleNamespace(
                x_center=0.5,
                y_center=0.5,
                width=0.4,
                height=0.4,
            ),
        )
        image = SimpleNamespace(
            image_id=IMAGE_ID,
            physical_holding_key="100-200",
            instances=(instance,),
        )
        self.all_test_images = (image,)
        self.eligible_images = (image,)

    @property
    def raw_instance_count(self) -> int:
        return 1

    @property
    def raw_image_count(self) -> int:
        return 1

    @property
    def eligible_instance_count(self) -> int:
        return 1

    @property
    def eligible_image_count(self) -> int:
        return 1

    @property
    def eligible_holding_count(self) -> int:
        return 1

    def bindings(self) -> dict[str, object]:
        return {
            "candidate_id": "candidate-a",
            "candidate_manifest_sha256": "3" * 64,
            "weights_sha256": "4" * 64,
            "base_model_training_inventory_available": False,
        }


def snapshot() -> MODULE.ImageSnapshot:
    return MODULE.ImageSnapshot(IMAGE_ID, IMAGE_SHA256, b"image")


def detection(
    *,
    confidence: float = 0.9,
    prediction_id: str = "p0001",
) -> MODULE.RawDetection:
    return MODULE.RawDetection(
        prediction_id=prediction_id,
        class_id=0,
        class_name="klasse_a",
        confidence=confidence,
        box=MODULE.scoring.Box(0.5, 0.5, 0.4, 0.4),
    )


def prediction(*, technical_error: str | None = None) -> MODULE.ImagePrediction:
    return MODULE.ImagePrediction(
        image_id=IMAGE_ID,
        detections=() if technical_error is not None else (detection(),),
        inference_time_ms=0.0 if technical_error is not None else 12.5,
        technical_error=technical_error,
    )


def ledger_payload(
    provenance: ProvenanceStub,
    predictions: list[MODULE.ImagePrediction],
) -> dict[str, object]:
    return MODULE.build_prediction_ledger(
        provenance,
        [snapshot()],
        predictions,
        created_utc="2026-08-02T12:00:00Z",
        runtime_protocol={"device": "cuda:0"},
        runtime_versions={"python": "3.12"},
    )


class EvaluateDetectGoldHoldoutMulticlassTests(unittest.TestCase):
    def test_labelblinder_ledger_roundtrip_enthaelt_keine_goldlabels(self) -> None:
        provenance = ProvenanceStub()
        expected = [prediction()]
        ledger = ledger_payload(provenance, expected)

        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "predictions.json"
            MODULE.atomic_write_json_new(target, ledger)
            file_sha256 = MODULE.sha256_file(target)

            loaded, protocol = MODULE.load_prediction_ledger(
                target,
                file_sha256,
                provenance,
                [snapshot()],
            )
            serialized = target.read_text(encoding="utf-8")

        self.assertEqual(expected, loaded)
        self.assertEqual("cuda:0", protocol["device"])
        self.assertNotIn("sample_id", serialized)
        self.assertNotIn("ground_truth", serialized)
        self.assertNotIn("gold-1", serialized)
        self.assertFalse(protocol["technical_errors_count_as_negative"])

    def test_manipulierter_ledger_wird_auch_mit_neuem_dateihash_abgelehnt(self) -> None:
        provenance = ProvenanceStub()
        ledger = ledger_payload(provenance, [prediction()])
        ledger["predictions"][0]["detections"][0]["confidence"] = 0.1

        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "predictions.json"
            target.write_text(
                json.dumps(ledger, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8",
            )
            manipulated_sha256 = hashlib.sha256(target.read_bytes()).hexdigest()

            with self.assertRaisesRegex(ValueError, "Schwelle|Receipt"):
                MODULE.load_prediction_ledger(
                    target,
                    manipulated_sha256,
                    provenance,
                    [snapshot()],
                )

    def test_technischer_fehler_darf_keine_detektion_enthalten(self) -> None:
        bad_prediction = MODULE.ImagePrediction(
            image_id=IMAGE_ID,
            detections=(detection(),),
            inference_time_ms=0.0,
            technical_error="inference_failed:RuntimeError",
        )

        with self.assertRaisesRegex(ValueError, "Technischer Fehler"):
            MODULE.validate_prediction_matrix([snapshot()], [bad_prediction])

        error_prediction = prediction(
            technical_error="inference_failed:RuntimeError",
        )
        ledger = ledger_payload(ProvenanceStub(), [error_prediction])

        self.assertEqual([], ledger["predictions"][0]["detections"])
        self.assertEqual(
            "inference_failed:RuntimeError",
            ledger["predictions"][0]["technical_error"],
        )
        self.assertFalse(ledger["protocol"]["technical_errors_count_as_negative"])

    def test_modellklassen_werden_normalisiert_und_boxen_streng_geprueft(self) -> None:
        self.assertEqual(
            {0: "klasse_a", 1: "klasse_b"},
            MODULE.normalize_model_names(["klasse_a", "klasse_b"]),
        )
        self.assertEqual(
            {0: "klasse_a", 1: "klasse_b"},
            MODULE.normalize_model_names({"1": "klasse_b", "0": "klasse_a"}),
        )
        with self.assertRaisesRegex(ValueError, "doppelte IDs"):
            MODULE.normalize_model_names({1: "klasse_a", "1": "klasse_b"})

        clamped = MODULE._box_from_xyxy(
            [-10.0, -20.0, 50.0, 60.0],
            image_width=100,
            image_height=100,
        )
        self.assertEqual(MODULE.scoring.Box(0.25, 0.3, 0.5, 0.6), clamped)
        with self.assertRaisesRegex(ValueError, "keine Flaeche"):
            MODULE._box_from_xyxy(
                [20.0, 10.0, 20.0, 30.0],
                image_width=100,
                image_height=100,
            )
        with self.assertRaisesRegex(ValueError, "endlich"):
            MODULE._box_from_xyxy(
                [0.0, 0.0, float("inf"), 30.0],
                image_width=100,
                image_height=100,
            )

    def test_nur_detect_modell_und_passende_ergebnisgroesse_sind_erlaubt(self) -> None:
        expected_names = {0: "klasse_a", 1: "klasse_b"}
        MODULE.validate_model_contract(
            SimpleNamespace(task="detect", names=expected_names),
            expected_names,
        )
        with self.assertRaisesRegex(ValueError, "kein Detect-Modell"):
            MODULE.validate_model_contract(
                SimpleNamespace(task="classify", names=expected_names),
                expected_names,
            )
        with self.assertRaisesRegex(ValueError, "Bildabmessungen"):
            MODULE._extract_detections(
                [SimpleNamespace(orig_shape=(99, 100), boxes=SimpleNamespace())],
                class_names=expected_names,
                image_width=100,
                image_height=100,
            )
        with self.assertRaisesRegex(ValueError, "kein Detect-Ergebnis"):
            MODULE._extract_detections(
                [SimpleNamespace(orig_shape=(100, 100), boxes=None)],
                class_names=expected_names,
                image_width=100,
                image_height=100,
            )

    def test_bericht_bleibt_positiver_holdout_ohne_freigabe(self) -> None:
        report = MODULE.build_report(
            ProvenanceStub(),
            {
                "micro": {
                    "tp": 1,
                    "fp": 0,
                    "fn": 0,
                    "precision": 1.0,
                    "recall": 1.0,
                    "f1": 1.0,
                }
            },
            ledger_sha256="5" * 64,
            prediction_receipt_sha256="6" * 64,
            created_utc="2026-08-02T12:00:00Z",
            protocol={"device": "cuda:0"},
            runtime_versions={"python": "3.12"},
        )

        self.assertEqual(MODULE.EVALUATION_STATUS, report["status"])
        self.assertEqual(
            "positive_holdout_only_not_release_qualified",
            report["release_assessment"]["status"],
        )
        self.assertFalse(report["release_assessment"]["release_qualified"])
        self.assertFalse(report["release_assessment"]["auto_activation_allowed"])
        self.assertTrue(report["release_assessment"]["fresh_negative_holdout_required"])
        self.assertTrue(report["holdout"]["positive_only"])
        self.assertEqual(0, report["holdout"]["clean_negative_images"])
        limitations = " ".join(report["limitations"])
        self.assertIn("Negativ-Holdout", limitations)
        self.assertIn("mAP", limitations)
        self.assertIn("Basismodells", limitations)


if __name__ == "__main__":
    unittest.main()
