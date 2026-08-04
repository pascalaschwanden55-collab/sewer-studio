from __future__ import annotations

import importlib.util
import sys
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "detect_gold_holdout_scoring.py"
SPEC = importlib.util.spec_from_file_location("detect_gold_holdout_scoring", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

CLASSES = {0: "A", 1: "B"}


def box(x: float = 0.5, y: float = 0.5, width: float = 0.4, height: float = 0.4):
    return MODULE.Box(x, y, width, height)


def truth(image: str, sample: str, class_id: int, value=None):
    return MODULE.GroundTruth(
        image,
        sample,
        class_id,
        CLASSES[class_id],
        value or box(),
    )


def prediction(image: str, item: str, class_id: int, value=None, confidence=0.9):
    return MODULE.Prediction(
        image,
        item,
        class_id,
        CLASSES[class_id],
        confidence,
        value or box(),
    )


class DetectGoldHoldoutScoringTests(unittest.TestCase):
    def test_exakter_treffer(self) -> None:
        report = MODULE.score_predictions(
            [truth("img", "s1", 0)],
            [prediction("img", "p1", 0)],
            CLASSES,
        )

        self.assertEqual({"tp": 1, "fp": 0, "fn": 0}, {
            key: report["micro"][key] for key in ("tp", "fp", "fn")
        })
        self.assertEqual(1.0, report["micro"]["f1"])
        self.assertEqual(1, report["geometry"]["matched"])

    def test_falsche_klasse_ist_fp_und_fn_aber_geometrische_konfusion(self) -> None:
        report = MODULE.score_predictions(
            [truth("img", "s1", 0)],
            [prediction("img", "p1", 1)],
            CLASSES,
        )

        self.assertEqual((0, 1, 1), tuple(report["micro"][key] for key in ("tp", "fp", "fn")))
        self.assertEqual(
            [{"expected_class": "A", "predicted_class": "B", "count": 1}],
            report["geometry"]["confusion"],
        )

    def test_doppelte_prediction_zaehlt_einmal_als_fp(self) -> None:
        report = MODULE.score_predictions(
            [truth("img", "s1", 0)],
            [prediction("img", "p1", 0), prediction("img", "p2", 0)],
            CLASSES,
        )

        self.assertEqual((1, 1, 0), tuple(report["micro"][key] for key in ("tp", "fp", "fn")))

    def test_fehlender_treffer_zaehlt_fn(self) -> None:
        report = MODULE.score_predictions(
            [truth("img", "s1", 0)],
            [],
            CLASSES,
        )

        self.assertEqual((0, 0, 1), tuple(report["micro"][key] for key in ("tp", "fp", "fn")))
        self.assertEqual(1, report["geometry"]["unmatched_ground_truth"])

    def test_mehrere_boxen_auf_einem_bild_werden_einzeln_gematcht(self) -> None:
        left = box(0.25, 0.5, 0.2, 0.3)
        right = box(0.75, 0.5, 0.2, 0.3)
        report = MODULE.score_predictions(
            [truth("img", "s1", 0, left), truth("img", "s2", 1, right)],
            [prediction("img", "p1", 0, left), prediction("img", "p2", 1, right)],
            CLASSES,
        )

        self.assertEqual((2, 0, 0), tuple(report["micro"][key] for key in ("tp", "fp", "fn")))
        self.assertEqual(2, report["macro"]["classes"])

    def test_matching_maximiert_trefferzahl_statt_einzel_iou(self) -> None:
        report = MODULE.score_predictions(
            [
                truth("img", "s1", 0, box(0.4, 0.5, 0.4, 0.4)),
                truth("img", "s2", 0, box(0.6, 0.5, 0.4, 0.4)),
            ],
            [
                prediction("img", "p1", 0, box(0.47, 0.5, 0.4, 0.4)),
                prediction("img", "p2", 0, box(0.3, 0.5, 0.4, 0.4)),
            ],
            CLASSES,
        )

        self.assertEqual(
            (2, 0, 0),
            tuple(report["micro"][key] for key in ("tp", "fp", "fn")),
        )


if __name__ == "__main__":
    unittest.main()
