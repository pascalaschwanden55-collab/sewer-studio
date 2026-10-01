from __future__ import annotations

import hashlib
import importlib.util
import json
import re
import sys
import tempfile
import unittest
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace

from PIL import Image


SCRIPTS_ROOT = Path(__file__).resolve().parents[1]
if str(SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_ROOT))

import detect_gold_holdout_provenance as provenance_tools
import detect_gold_holdout_scoring as scoring
import evaluate_detect_gold_holdout as evaluation_tools


TARGET_PATH = SCRIPTS_ROOT / "detect_gold_error_review.py"
_TARGET_MODULE = None


def _load_target_module():
    global _TARGET_MODULE
    if _TARGET_MODULE is not None:
        return _TARGET_MODULE
    if not TARGET_PATH.is_file():
        raise FileNotFoundError(
            "TDD: training/scripts/detect_gold_error_review.py fehlt noch."
        )
    spec = importlib.util.spec_from_file_location("detect_gold_error_review", TARGET_PATH)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    _TARGET_MODULE = module
    return module


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _write_json(path: Path, payload: object) -> None:
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def _box(x: float = 0.5, y: float = 0.5) -> provenance_tools.HoldoutBox:
    return provenance_tools.HoldoutBox(x, y, 0.2, 0.2)


def _scoring_box(x: float = 0.5, y: float = 0.5) -> scoring.Box:
    return scoring.Box(x, y, 0.2, 0.2)


@dataclass
class ProvenanceStub:
    eligible_images: tuple[provenance_tools.HoldoutImage, ...]
    _bindings: dict[str, object]

    classes = ("klasse_a", "klasse_b")
    excluded_holdings: tuple[object, ...] = ()

    @property
    def all_test_images(self):
        return self.eligible_images

    @property
    def raw_instance_count(self) -> int:
        return sum(len(image.instances) for image in self.eligible_images)

    @property
    def raw_image_count(self) -> int:
        return len(self.eligible_images)

    @property
    def eligible_instance_count(self) -> int:
        return self.raw_instance_count

    @property
    def eligible_image_count(self) -> int:
        return self.raw_image_count

    @property
    def eligible_holding_count(self) -> int:
        return len({image.physical_holding_key for image in self.eligible_images})

    def bindings(self) -> dict[str, object]:
        return dict(self._bindings)


@dataclass
class Fixture:
    knowledge_root: Path
    report_path: Path
    ledger_path: Path
    provenance: ProvenanceStub
    source_bytes: dict[Path, bytes]


def _bindings() -> dict[str, object]:
    return {
        "candidate_id": "candidate-a",
        "candidate_manifest_sha256": "1" * 64,
        "weights_sha256": "2" * 64,
        "dataset_plan_id": "3" * 64,
        "dataset_manifest_sha256": "4" * 64,
        "dataset_receipt_sha256": "5" * 64,
        "registry_sha256": "6" * 64,
        "detect_all_receipt_sha256": "7" * 64,
        "base_gold_audit_sha256": "8" * 64,
        "base_training_samples_sha256": "9" * 64,
        "current_gold_audit_sha256": "a" * 64,
        "current_training_samples_sha256": "b" * 64,
        "class_map_sha256": "c" * 64,
        "migration_sha256": "d" * 64,
        "vsa_manifest_sha256": "e" * 64,
        "base_model_training_inventory_available": False,
    }


def _make_image(
    root: Path,
    index: int,
    class_id: int,
    sample_id: str,
    *,
    x: float = 0.5,
) -> tuple[provenance_tools.HoldoutImage, bytes]:
    path = root / "gold_frames" / f"klasse-{class_id}" / f"image-{index}.png"
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.new(
        "RGB",
        (8, 8),
        color=(20 * index, 30 * index, 40 * index),
    ).save(path, format="PNG")
    payload = path.read_bytes()
    image_sha = _sha256(payload)
    instance = provenance_tools.HoldoutInstance(
        sample_id=sample_id,
        code=f"CODE{class_id}",
        class_id=class_id,
        class_name=ProvenanceStub.classes[class_id],
        box=_box(x),
        source_type="ManualCoding",
    )
    return (
        provenance_tools.HoldoutImage(
            image_id=image_sha,
            image_path=path,
            image_sha256=image_sha,
            holding_key=f"{100 + index}-{200 + index}",
            physical_holding_key=f"{100 + index}|{200 + index}",
            instances=(instance,),
        ),
        payload,
    )


def _fixture(root: Path, *, technical_error: bool = False) -> Fixture:
    reports = root / "training" / "reports"
    reports.mkdir(parents=True)
    images_with_bytes = (
        _make_image(root, 1, 0, "gold-wrong"),
        _make_image(root, 2, 1, "gold-missed"),
        _make_image(root, 3, 0, "gold-exact", x=0.25),
    )
    images = tuple(item[0] for item in images_with_bytes)
    source_bytes = {
        item.image_path: payload for item, payload in images_with_bytes
    }
    provenance = ProvenanceStub(images, _bindings())

    wrong = evaluation_tools.RawDetection(
        "p0001",
        1,
        "klasse_b",
        0.80,
        _scoring_box(),
    )
    exact = evaluation_tools.RawDetection(
        "p0001",
        0,
        "klasse_a",
        0.90,
        _scoring_box(0.25),
    )
    extra = evaluation_tools.RawDetection(
        "p0002",
        1,
        "klasse_b",
        0.70,
        _scoring_box(0.80),
    )
    predictions = [
        evaluation_tools.ImagePrediction(images[0].image_id, (wrong,), 1.0, None),
        evaluation_tools.ImagePrediction(
            images[1].image_id,
            (),
            0.0,
            "inference_failed:RuntimeError" if technical_error else None,
        ),
        evaluation_tools.ImagePrediction(
            images[2].image_id,
            (exact, extra),
            1.0,
            None,
        ),
    ]
    snapshots = [
        evaluation_tools.ImageSnapshot(image.image_id, image.image_sha256, payload)
        for image, payload in images_with_bytes
    ]
    protocol = {
        "device": "cpu",
        "decoded_image_color_order": "RGB",
        "model_numpy_color_order": "BGR",
        "channel_conversion": "PIL_RGB_to_contiguous_BGR",
    }
    ledger = evaluation_tools.build_prediction_ledger(
        provenance,
        snapshots,
        predictions,
        created_utc="2026-08-02T12:00:00Z",
        runtime_protocol=protocol,
        runtime_versions={"python": "test"},
    )
    ledger_path = reports / "predictions.json"
    _write_json(ledger_path, ledger)
    ledger_sha = _sha256(ledger_path.read_bytes())

    truths, sealed_predictions = evaluation_tools._to_scoring_inputs(
        provenance,
        predictions,
    )
    metrics = scoring.score_predictions(
        truths,
        sealed_predictions,
        {0: "klasse_a", 1: "klasse_b"},
        iou_threshold=evaluation_tools.IOU_THRESHOLD,
    )
    report = evaluation_tools.build_report(
        provenance,
        metrics,
        ledger_sha256=ledger_sha,
        prediction_receipt_sha256=str(ledger["prediction_receipt_sha256"]),
        created_utc="2026-08-02T12:00:00Z",
        protocol=ledger["protocol"],
        runtime_versions={"python": "test"},
    )
    report_path = reports / "evaluation.json"
    _write_json(report_path, report)
    return Fixture(root, report_path, ledger_path, provenance, source_bytes)


class DetectGoldErrorReviewTests(unittest.TestCase):
    def setUp(self) -> None:
        self.module = _load_target_module()

    def _build_plan(
        self,
        fixture: Fixture,
        *,
        created_utc: datetime | None = None,
        provenance: ProvenanceStub | None = None,
    ):
        return self.module.build_queue_plan(
            fixture.report_path,
            fixture.ledger_path,
            provenance or fixture.provenance,
            fixture.knowledge_root,
            created_utc=created_utc
            or datetime(2026, 8, 2, 14, 0, tzinfo=timezone.utc),
        )

    def test_queue_klassifiziert_jeden_fehler_einmal_und_ist_deterministisch(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = _fixture(Path(temporary))
            first = self._build_plan(fixture)
            reversed_provenance = ProvenanceStub(
                tuple(reversed(fixture.provenance.eligible_images)),
                fixture.provenance.bindings(),
            )
            second = self._build_plan(
                fixture,
                provenance=reversed_provenance,
                created_utc=datetime(2026, 8, 3, 9, 0, tzinfo=timezone.utc),
            )

            self.assertRegex(first.queue_id, r"^[0-9a-f]{64}$")
            self.assertEqual(first.queue_id, second.queue_id)
            self.assertEqual(first.semantic_payload, second.semantic_payload)
            self.assertEqual(
                first.queue_id,
                _sha256(self.module.canonical_json_bytes(first.semantic_payload)),
            )
            self.assertEqual(
                sorted(item["id"] for item in first.semantic_payload["candidates"]),
                [item["id"] for item in first.semantic_payload["candidates"]],
            )

            target = self.module.publish_queue(first)
            manifest = json.loads(
                (target / "_manifest.json").read_text(encoding="utf-8")
            )
            candidates = json.loads(
                (target / "_candidates.json").read_text(encoding="utf-8")
            )

        self.assertEqual("1.0", manifest["schema_version"])
        self.assertEqual("detect_gold_failure_review_queue", manifest["purpose"])
        self.assertEqual("diagnostic_only", manifest["role"])
        self.assertIs(True, manifest["frozen"])
        self.assertEqual(first.queue_id, manifest["queue_id"])
        self.assertIs(False, manifest["policy"]["training_eligible"])
        self.assertIs(False, manifest["policy"]["training_export_allowed"])
        self.assertIs(False, manifest["policy"]["source_mutation_allowed"])
        self.assertIs(False, manifest["policy"]["image_copies_created"])
        self.assertEqual(
            {
                "cases": 3,
                "images": 3,
                "wrong_class": 1,
                "missed": 1,
                "extra_prediction": 1,
            },
            manifest["summary"],
        )
        self.assertEqual(
            Counter({"wrong_class": 1, "missed": 1, "extra_prediction": 1}),
            Counter(item["case_type"] for item in candidates),
        )
        self.assertEqual(len(candidates), len({item["id"] for item in candidates}))
        self.assertTrue(all(item["status"] == "pending_review" for item in candidates))
        self.assertTrue(
            all(not Path(item["frame_path"]).is_absolute() for item in candidates)
        )
        wrong = next(item for item in candidates if item["case_type"] == "wrong_class")
        self.assertEqual("klasse_a", wrong["ground_truth"]["class_name"])
        self.assertEqual("klasse_b", wrong["prediction"]["class_name"])
        self.assertEqual(1.0, wrong["iou"])
        missed = next(item for item in candidates if item["case_type"] == "missed")
        self.assertIsNotNone(missed["ground_truth"])
        self.assertIsNone(missed["prediction"])
        extra = next(
            item for item in candidates if item["case_type"] == "extra_prediction"
        )
        self.assertIsNone(extra["ground_truth"])
        self.assertIsNotNone(extra["prediction"])

    def test_technischer_fehler_stoppt_die_diagnostische_queue(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = _fixture(Path(temporary), technical_error=True)

            with self.assertRaisesRegex(ValueError, "technisch|Technisch"):
                self._build_plan(fixture)

    def test_manipulierter_report_ledger_oder_provenienzbindung_stoppt(self) -> None:
        mutations = ("report", "ledger", "provenance")
        for mutation in mutations:
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as temporary:
                fixture = _fixture(Path(temporary))
                provenance = fixture.provenance
                if mutation == "report":
                    report = json.loads(fixture.report_path.read_text(encoding="utf-8"))
                    report["metrics"]["micro"]["tp"] += 1
                    _write_json(fixture.report_path, report)
                elif mutation == "ledger":
                    ledger = json.loads(fixture.ledger_path.read_text(encoding="utf-8"))
                    ledger["predictions"][0]["detections"][0]["confidence"] = 0.79
                    _write_json(fixture.ledger_path, ledger)
                else:
                    changed = fixture.provenance.bindings()
                    changed["weights_sha256"] = "f" * 64
                    provenance = ProvenanceStub(
                        fixture.provenance.eligible_images,
                        changed,
                    )

                with self.assertRaisesRegex(
                    ValueError,
                    "Bericht|Ledger|Beleg|Binding|Metrik|Receipt|SHA|Eingaben",
                ):
                    self._build_plan(fixture, provenance=provenance)

    def test_vorhandenes_abweichendes_ziel_wird_nie_ueberschrieben(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = _fixture(Path(temporary))
            plan = self._build_plan(fixture)
            plan.target_root.mkdir(parents=True)
            sentinel = plan.target_root / "sentinel.txt"
            sentinel.write_bytes(b"fremder-bestand")

            with self.assertRaises(FileExistsError):
                self.module.publish_queue(plan)

            self.assertEqual(b"fremder-bestand", sentinel.read_bytes())
            self.assertEqual([sentinel], list(plan.target_root.iterdir()))

    def test_publish_kopiert_keine_bilder_und_mutiert_keine_trainingsquelle(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = _fixture(Path(temporary))
            training_samples = fixture.knowledge_root / "training_samples.json"
            training_samples.write_bytes(b"[]\n")
            protected = {
                fixture.report_path: fixture.report_path.read_bytes(),
                fixture.ledger_path: fixture.ledger_path.read_bytes(),
                training_samples: training_samples.read_bytes(),
                **fixture.source_bytes,
            }

            target = self.module.publish_queue(self._build_plan(fixture))

            self.assertEqual(
                {"_manifest.json", "_candidates.json"},
                {path.name for path in target.iterdir()},
            )
            self.assertFalse((target / "images").exists())
            for path, expected in protected.items():
                self.assertEqual(expected, path.read_bytes(), str(path))

            manifest = json.loads(
                (target / "_manifest.json").read_text(encoding="utf-8")
            )
            bindings = manifest["bindings"]
            self.assertEqual(
                _sha256(fixture.report_path.read_bytes()),
                bindings["evaluation_report_sha256"],
            )
            self.assertEqual(
                _sha256(fixture.ledger_path.read_bytes()),
                bindings["prediction_ledger_sha256"],
            )
            self.assertEqual(
                fixture.provenance.bindings()["candidate_manifest_sha256"],
                bindings["candidate_manifest_sha256"],
            )
            self.assertEqual(
                fixture.provenance.bindings()["current_gold_audit_sha256"],
                bindings["current_gold_audit_sha256"],
            )


if __name__ == "__main__":
    unittest.main()
