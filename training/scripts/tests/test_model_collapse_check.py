from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock


MODULE_PATH = Path(__file__).resolve().parents[1] / "model_collapse_check.py"
SPEC = importlib.util.spec_from_file_location("model_collapse_check", MODULE_PATH)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def _box(cx: float, cy: float, w: float, h: float, conf: float = 0.9):
    return MODULE.Box(cx=cx, cy=cy, w=w, h=h, conf=conf)


def _varied_boxes():
    return [
        _box(0.10, 0.10, 0.06, 0.06),
        _box(0.30, 0.10, 0.08, 0.10),
        _box(0.50, 0.10, 0.10, 0.08),
        _box(0.70, 0.10, 0.12, 0.06),
        _box(0.90, 0.10, 0.06, 0.12),
        _box(0.10, 0.80, 0.14, 0.08),
        _box(0.30, 0.80, 0.08, 0.14),
        _box(0.50, 0.80, 0.16, 0.10),
        _box(0.70, 0.80, 0.10, 0.16),
        _box(0.90, 0.80, 0.12, 0.12),
    ]


class _ListValue:
    def __init__(self, value):
        self._value = value

    def tolist(self):
        return self._value


class ModelCollapseCheckTests(unittest.TestCase):
    def test_collapsed_boxes_fail(self) -> None:
        boxes = [
            _box(0.480 + index * 0.0001, 0.350, 0.520, 0.690)
            for index in range(9)
        ] + [None]
        metrics = MODULE.collapse_metrics(boxes, iou_dup=0.90)

        self.assertEqual(10, metrics.bilder)
        self.assertEqual(9, metrics.mit_vorhersage)
        self.assertEqual(36, metrics.paare_gesamt)
        self.assertEqual(36, metrics.paare_identisch)

        verdict = MODULE.decide_verdict(metrics)
        self.assertEqual(MODULE.VerdictStatus.FAIL, verdict.status)
        self.assertEqual(1, MODULE.verdict_exit_code(verdict))
        self.assertTrue(any("Bildpaare" in grund for grund in verdict.gruende))

    def test_low_spread_at_high_detection_rate_fails(self) -> None:
        boxes = [
            _box(0.500 + index * 0.001, 0.500 + index * 0.0005, 0.200, 0.200)
            for index in range(10)
        ]
        metrics = MODULE.collapse_metrics(boxes, iou_dup=0.9999)

        self.assertLess(metrics.paar_anteil, 0.5)
        self.assertLess(
            max(metrics.std_cx, metrics.std_cy, metrics.std_w, metrics.std_h),
            0.02,
        )

        verdict = MODULE.decide_verdict(metrics)
        self.assertEqual(MODULE.VerdictStatus.FAIL, verdict.status)
        self.assertTrue(any("Streuung" in grund for grund in verdict.gruende))

    def test_varying_boxes_pass(self) -> None:
        metrics = MODULE.collapse_metrics(_varied_boxes(), iou_dup=0.90)

        self.assertEqual(0, metrics.paare_identisch)
        self.assertGreater(metrics.std_cx, 0.02)

        verdict = MODULE.decide_verdict(metrics)
        self.assertEqual(MODULE.VerdictStatus.PASS, verdict.status)
        self.assertEqual(0, MODULE.verdict_exit_code(verdict))
        self.assertIn("Qualitaetsfreigabe", verdict.hinweis)

    def test_no_detections_are_inconclusive(self) -> None:
        metrics = MODULE.collapse_metrics([None] * 10, iou_dup=0.90)

        verdict = MODULE.decide_verdict(metrics)

        self.assertEqual(MODULE.VerdictStatus.INCONCLUSIVE, verdict.status)
        self.assertEqual(2, MODULE.verdict_exit_code(verdict))
        self.assertTrue(any("Erkennungen" in grund for grund in verdict.gruende))

    def test_too_few_test_images_are_inconclusive(self) -> None:
        metrics = MODULE.collapse_metrics(_varied_boxes()[:4], iou_dup=0.90)

        verdict = MODULE.decide_verdict(metrics)

        self.assertEqual(MODULE.VerdictStatus.INCONCLUSIVE, verdict.status)
        self.assertTrue(any("Pruefbilder" in grund for grund in verdict.gruende))

    def test_too_few_detections_are_inconclusive(self) -> None:
        boxes = _varied_boxes()[:4] + [None] * 6
        metrics = MODULE.collapse_metrics(boxes, iou_dup=0.90)

        verdict = MODULE.decide_verdict(metrics)

        self.assertEqual(MODULE.VerdictStatus.INCONCLUSIVE, verdict.status)
        self.assertTrue(any("Erkennungen" in grund for grund in verdict.gruende))

    def test_inference_error_is_never_pass(self) -> None:
        metrics = MODULE.collapse_metrics(_varied_boxes(), iou_dup=0.90)

        verdict = MODULE.decide_verdict(metrics, inference_error_count=1)

        self.assertEqual(MODULE.VerdictStatus.INCONCLUSIVE, verdict.status)
        self.assertEqual(2, MODULE.verdict_exit_code(verdict))
        self.assertTrue(any("Inferenzfehler" in grund for grund in verdict.gruende))

    def test_main_returns_exit_2_for_inconclusive_result(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            weights = root / "best.pt"
            weights.write_bytes(b"weights")
            sample = MODULE.GoldSample(
                sample_id="s1",
                frame_path=root / "gold.jpg",
                box=_box(0.5, 0.5, 0.2, 0.2),
            )
            args = SimpleNamespace(
                weights=weights,
                knowledge_root=root,
                samples_json=root / "samples.json",
                negatives_dir=root / "pool",
                images_dir=root / "images",
                conf=0.25,
                iou_dup=0.90,
                imgsz=1280,
                dataset=None,
                limit=None,
                report=False,
                min_test_images=MODULE.MIN_TEST_IMAGES,
                min_detections=MODULE.MIN_DETECTIONS,
                min_detection_rate=MODULE.MIN_DETECTION_RATE,
            )
            with (
                mock.patch.object(MODULE, "_parse_args", return_value=args),
                mock.patch.object(
                    MODULE,
                    "load_images",
                    return_value=[Path(f"test_{index}.jpg") for index in range(10)],
                ),
                mock.patch.object(
                    MODULE,
                    "load_gold_samples",
                    return_value=([sample], 0),
                ),
                mock.patch.object(MODULE, "load_negatives", return_value=[]),
                mock.patch.object(MODULE, "_sha256_file", return_value="a" * 64),
                mock.patch.object(MODULE, "build_provenance", return_value={}),
                mock.patch.object(MODULE, "load_model", return_value=object()),
                mock.patch.object(
                    MODULE,
                    "predict_best_boxes",
                    side_effect=[
                        ([None] * 10, []),
                        ([None], []),
                        ([], []),
                    ],
                ),
                mock.patch("builtins.print"),
            ):
                exit_code = MODULE.main()

        self.assertEqual(2, exit_code)

    def test_pool_activation_counting_does_not_claim_false_alarms(self) -> None:
        predictions = [
            ("pool_01.jpg", _box(0.5, 0.5, 0.3, 0.3)),
            ("pool_02.jpg", None),
            ("pool_03.png", _box(0.2, 0.2, 0.1, 0.1)),
            ("pool_04.jpg", None),
        ]
        metrics = MODULE.pool_activation_metrics(predictions)

        self.assertEqual(4, metrics.bilder)
        self.assertEqual(2, metrics.aktivierungen)
        self.assertAlmostEqual(0.5, metrics.rate)
        self.assertEqual(("pool_01.jpg", "pool_03.png"), metrics.dateien)

    def test_gold_iou_rechnung(self) -> None:
        gold = [
            _box(0.5, 0.5, 0.4, 0.4),
            _box(0.5, 0.5, 0.4, 0.4),
            _box(0.2, 0.2, 0.2, 0.2),
            _box(0.2, 0.2, 0.2, 0.2),
        ]
        predictions = [
            _box(0.5, 0.5, 0.4, 0.4),
            _box(0.7, 0.5, 0.4, 0.4),
            None,
            _box(0.8, 0.8, 0.2, 0.2),
        ]
        metrics = MODULE.gold_metrics(predictions, gold)

        self.assertEqual(4, metrics.samples)
        self.assertAlmostEqual(1.0, metrics.ious[0])
        self.assertAlmostEqual(1.0 / 3.0, metrics.ious[1])
        self.assertAlmostEqual(0.0, metrics.ious[2])
        self.assertAlmostEqual(0.0, metrics.ious[3])
        self.assertAlmostEqual(0.25, metrics.trefferquote)

    def test_imgsz_and_minimum_defaults(self) -> None:
        with mock.patch.object(sys, "argv", ["prog", "--weights", "x.pt"]):
            args = MODULE._parse_args()

        self.assertEqual(1280, args.imgsz)
        self.assertEqual(MODULE.MIN_TEST_IMAGES, args.min_test_images)
        self.assertEqual(MODULE.MIN_DETECTIONS, args.min_detections)
        self.assertEqual(MODULE.MIN_DETECTION_RATE, args.min_detection_rate)
        self.assertEqual(
            args.knowledge_root / "eval_set" / "images",
            args.images_dir,
        )

    def test_help_laesst_sich_ohne_formatfehler_anzeigen(self) -> None:
        with (
            mock.patch.object(sys, "argv", ["prog", "--help"]),
            mock.patch("sys.stdout"),
            self.assertRaises(SystemExit) as raised,
        ):
            MODULE._parse_args()

        self.assertEqual(0, raised.exception.code)

    def test_predict_uses_explicit_imgsz(self) -> None:
        result = SimpleNamespace(
            boxes=SimpleNamespace(
                xywhn=_ListValue([[0.5, 0.5, 0.2, 0.2]]),
                conf=_ListValue([0.75]),
            )
        )

        class Model:
            def __init__(self):
                self.calls = []

            def predict(self, **kwargs):
                self.calls.append(kwargs)
                return [result]

        model = Model()
        predictions, errors = MODULE.predict_best_boxes(
            model,
            [Path("image.jpg")],
            conf=0.25,
            imgsz=1280,
        )

        self.assertEqual([], errors)
        self.assertEqual(1, len(predictions))
        self.assertEqual(1280, model.calls[0]["imgsz"])

    def test_map_validation_uses_same_explicit_imgsz(self) -> None:
        class Model:
            def __init__(self):
                self.calls = []

            def val(self, **kwargs):
                self.calls.append(kwargs)
                box = SimpleNamespace(
                    ap_class_index=[],
                    mp=0.1,
                    mr=0.2,
                    map50=0.3,
                    map=0.4,
                )
                return SimpleNamespace(box=box, names={})

        with tempfile.TemporaryDirectory() as temporary:
            dataset = Path(temporary)
            (dataset / "data.yaml").write_text("path: .\n", encoding="utf-8")
            model = Model()
            with mock.patch.object(MODULE, "_write_runtime_yaml"):
                result = MODULE.run_map_validation(model, dataset, imgsz=1280)

        self.assertEqual(1280, model.calls[0]["imgsz"])
        self.assertEqual(1280, result["imgsz"])

    def test_provenance_hashes_and_reports_known_overlap(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            test_image = root / "test.jpg"
            gold_image = root / "gold.jpg"
            pool_image = root / "pool.jpg"
            test_image.write_bytes(b"same")
            gold_image.write_bytes(b"same")
            pool_image.write_bytes(b"other")
            dataset = root / "dataset"
            dataset.mkdir()
            shared_hash = MODULE._sha256_file(test_image)
            (dataset / "manifest.json").write_text(
                json.dumps({"images": [{"image_sha256": shared_hash}]}),
                encoding="utf-8",
            )
            sample = MODULE.GoldSample(
                sample_id="s1",
                frame_path=gold_image,
                box=_box(0.5, 0.5, 0.2, 0.2),
            )

            provenance = MODULE.build_provenance(
                images=[test_image],
                samples=[sample],
                negative_pool=[pool_image],
                dataset=dataset,
            )

        overlaps = provenance["ueberschneidungen"]
        self.assertEqual(1, overlaps["pruefbestand_gold_referenz"]["anzahl"])
        self.assertEqual(0, overlaps["pruefbestand_negativ_pool"]["anzahl"])
        self.assertEqual(1, overlaps["pruefbestand_datensatz_manifest"]["anzahl"])
        self.assertTrue(
            provenance["datensatz_manifest"]["manifest_gefunden"]
        )

    def test_report_is_explicitly_geometry_only_and_tri_state(self) -> None:
        args = SimpleNamespace(
            conf=0.25,
            iou_dup=0.90,
            imgsz=1280,
            limit=None,
            images_dir=Path("eval_set/images"),
            min_test_images=MODULE.MIN_TEST_IMAGES,
            min_detections=MODULE.MIN_DETECTIONS,
            min_detection_rate=MODULE.MIN_DETECTION_RATE,
        )
        predictions = _varied_boxes()
        collapse = MODULE.collapse_metrics(predictions, iou_dup=0.90)
        sample = MODULE.GoldSample(
            sample_id="s1",
            frame_path=Path("gold_01.jpg"),
            box=_box(0.5, 0.5, 0.4, 0.4),
        )
        gold = MODULE.gold_metrics([sample.box], [sample.box])
        pool_activations = MODULE.pool_activation_metrics([("pool_01.jpg", None)])
        verdict = MODULE.decide_verdict(collapse, inference_error_count=1)

        report = MODULE.build_report(
            weights=Path("best.pt"),
            weights_sha256="a" * 64,
            args=args,
            images=[Path(f"test_{index}.jpg") for index in range(10)],
            image_predictions=predictions,
            samples=[sample],
            fehlende_bilder=0,
            negative_pool=[Path("pool_01.jpg")],
            gold_predictions=[sample.box],
            inferenz_fehler=["test_01.jpg: defekt"],
            collapse=collapse,
            gold=gold,
            pool_activations=pool_activations,
            map_result=None,
            provenance={"ueberschneidungen": {}},
            verdict=verdict,
        )

        self.assertEqual("2.0", report["schema_version"])
        self.assertTrue(report["pruefart"]["keine_qualitaetsfreigabe"])
        self.assertFalse(report["pruefart"]["zusatzmessungen_im_verdikt"])
        self.assertIn("pruefbestand", report)
        self.assertIn(
            "negativ_pool_aktivierungen",
            report["zusatzmessungen_nicht_im_verdikt"],
        )
        self.assertNotIn("fehlalarme", report)
        self.assertEqual("INCONCLUSIVE", report["verdikt"]["status"])
        self.assertEqual(2, report["verdikt"]["exit_code"])

    def test_box_iou_grundfaelle(self) -> None:
        a = _box(0.5, 0.5, 0.4, 0.4)
        self.assertAlmostEqual(1.0, MODULE.box_iou(a, a))
        self.assertAlmostEqual(0.0, MODULE.box_iou(a, _box(0.9, 0.9, 0.1, 0.1)))

    def test_best_box_nimmt_hoechste_konfidenz(self) -> None:
        boxes = [
            _box(0.1, 0.1, 0.1, 0.1, conf=0.3),
            _box(0.5, 0.5, 0.2, 0.2, conf=0.8),
            _box(0.9, 0.9, 0.1, 0.1, conf=0.5),
        ]
        self.assertIsNone(MODULE.best_box([]))
        self.assertAlmostEqual(0.5, MODULE.best_box(boxes).cx)


if __name__ == "__main__":
    unittest.main()
