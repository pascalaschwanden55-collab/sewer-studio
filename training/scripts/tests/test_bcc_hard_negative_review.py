from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from unittest import mock

from tools.EvalVisibilityReview.bcc_release_holdout_review_server import (
    BccHardNegativeReviewStore,
)


MODULE_PATH = Path(__file__).resolve().parents[1] / "bcc_hard_negative_review.py"
SPEC = importlib.util.spec_from_file_location("bcc_hard_negative_review", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class BccHardNegativeReviewTests(unittest.TestCase):
    def test_klassenkarte_bindet_exakt_15_klassen_und_vsa_bytes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            vsa = root / "vsa.json"
            vsa.write_text('{"catalog":"test"}', encoding="utf-8")
            vsa_sha = self._sha(vsa.read_bytes())
            class_map = root / "class_map.json"
            names = [f"class_{index}" for index in range(14)] + ["BCC_bogen"]
            class_map.write_text(
                json.dumps(
                    {
                        "version": 3,
                        "vsa_manifest_hash": vsa_sha,
                        "classes": {
                            name: index for index, name in enumerate(names)
                        },
                    }
                ),
                encoding="utf-8",
            )

            binding = MODULE.load_class_map(class_map, vsa)

            self.assertEqual(3, binding.version)
            self.assertEqual(tuple(names), binding.ordered_names)
            self.assertEqual(vsa_sha, binding.vsa_manifest_hash)

            class_map.write_text(
                json.dumps(
                    {
                        "version": 3,
                        "vsa_manifest_hash": vsa_sha,
                        "classes": {"BCC_bogen": 14},
                    }
                ),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(ValueError, "15"):
                MODULE.load_class_map(class_map, vsa)

    def test_auswahl_nimmt_genau_den_haertesten_trigger_je_haltung(self) -> None:
        photo_a = self._photo("a" * 64, "100-200", "100|200")
        photo_b = self._photo("b" * 64, "100-200", "100|200")
        photo_c = self._photo("c" * 64, "300-400", "300|400")
        photos = {
            "100|200": [photo_a, photo_b],
            "300|400": [photo_c],
        }
        predictions = {
            "model-a": {
                photo_a.image_sha256: self._prediction(
                    photo_a.image_sha256, True, 0.40
                ),
                photo_b.image_sha256: self._prediction(
                    photo_b.image_sha256, True, 0.80
                ),
                photo_c.image_sha256: self._prediction(
                    photo_c.image_sha256, False, None
                ),
            },
            "model-b": {
                photo_a.image_sha256: self._prediction(
                    photo_a.image_sha256, True, 0.50
                ),
                photo_b.image_sha256: self._prediction(
                    photo_b.image_sha256, True, 0.70
                ),
                photo_c.image_sha256: self._prediction(
                    photo_c.image_sha256, False, None
                ),
            },
        }

        with mock.patch.object(
            Path,
            "stat",
            return_value=mock.Mock(st_size=1_104),
        ):
            selected = MODULE.select_hardest_per_holding(photos, predictions)

        self.assertEqual(1, len(selected))
        self.assertEqual(photo_b.image_sha256, selected[0].image_sha256)
        self.assertEqual("100|200", selected[0].physical_holding_key)

    def test_publish_ist_atomar_ueberschreibt_nie_und_belaesst_original(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "training").mkdir()
            source = root / "source.jpg"
            source.write_bytes(b"\xff\xd8\xff\xe0" + b"x" * 1_100)
            image_sha = self._sha(source.read_bytes())
            item = MODULE.QueueItem(
                item_id=f"bcc-hn-{image_sha[:16]}",
                source_path=source,
                image_sha256=image_sha,
                holding_key="100-200",
                physical_holding_key="100|200",
                source_ref="3" * 64,
                inspection_date="2026-04-20",
                size_bytes=source.stat().st_size,
                image_format="jpg",
                predictions=(
                    {
                        "model_id": "model-a",
                        "predicted_bcc": True,
                        "bcc_detection_count": 1,
                        "max_bcc_confidence": 0.8,
                    },
                ),
            )
            semantic = self._semantic(item)
            queue_id = self._sha(MODULE._canonical_json_bytes(semantic))
            class_map_path = root / "class_map.json"
            class_map_path.write_text("{}", encoding="utf-8")
            vsa_path = root / "vsa.json"
            vsa_path.write_text("{}", encoding="utf-8")
            plan = MODULE.QueuePlan(
                knowledge_root=root,
                base_model_path=root / "base.pt",
                class_map=MODULE.ClassMapBinding(
                    class_map_path,
                    "1" * 64,
                    3,
                    "2" * 64,
                    tuple(semantic["class_names"]),
                ),
                vsa_manifest_path=vsa_path,
                created_utc=datetime(2026, 7, 28, 20, 0, tzinfo=timezone.utc),
                sources=(),
                source_specs=(),
                protected_sets=(),
                protection_snapshot={},
                model_scope=tuple(semantic["model_scope"]),
                items=(item,),
                semantic_payload=semantic,
                queue_id=queue_id,
                target_root=(
                    root
                    / "training"
                    / "hard_negative_review"
                    / "queues"
                    / f"bcc_hn_{queue_id[:12]}"
                ),
                scanned_photos=1,
                clean_holdings=1,
                blocked_same_hash=0,
                blocked_same_holding=0,
            )
            original = source.read_bytes()

            with mock.patch.object(MODULE, "_assert_plan_inputs_unchanged"):
                target = MODULE.publish_queue(plan)

            self.assertEqual(original, source.read_bytes())
            self.assertTrue((target / "_manifest.json").is_file())
            self.assertTrue((target / "images" / item.target_file_name).is_file())
            store = BccHardNegativeReviewStore(
                target,
                root / "reviews" / "review.json",
                "Besitzer",
            )
            self.assertEqual(1, store.state()["total"])
            with mock.patch.object(MODULE, "_assert_plan_inputs_unchanged"):
                with self.assertRaises(FileExistsError):
                    MODULE.publish_queue(plan)

    def test_review_publish_nimmt_nur_all_class_clear_und_bindet_belege(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "training").mkdir()
            base_model = root / "base.pt"
            base_model.write_bytes(b"base-model")
            class_map_path = root / "class_map.json"
            class_map_path.write_text("{}", encoding="utf-8")
            class_map_sha = self._sha(class_map_path.read_bytes())
            vsa_path = root / "vsa.json"
            vsa_path.write_text("{}", encoding="utf-8")
            class_names = tuple(
                [f"class-{index}" for index in range(14)] + ["BCC_bogen"]
            )
            binding = MODULE.ClassMapBinding(
                class_map_path,
                class_map_sha,
                3,
                "2" * 64,
                class_names,
            )
            queue_items = tuple(
                self._queue_item(root, index)
                for index in range(3)
            )
            semantic = self._semantic(*queue_items, class_map_sha=class_map_sha)
            queue_id = self._sha(MODULE._canonical_json_bytes(semantic))
            queue_plan = MODULE.QueuePlan(
                knowledge_root=root,
                base_model_path=base_model,
                class_map=binding,
                vsa_manifest_path=vsa_path,
                created_utc=datetime(2026, 7, 28, 20, 0, tzinfo=timezone.utc),
                sources=(),
                source_specs=(),
                protected_sets=(),
                protection_snapshot={},
                model_scope=tuple(semantic["model_scope"]),
                items=queue_items,
                semantic_payload=semantic,
                queue_id=queue_id,
                target_root=(
                    root
                    / "training"
                    / "hard_negative_review"
                    / "queues"
                    / f"bcc_hn_{queue_id[:12]}"
                ),
                scanned_photos=3,
                clean_holdings=3,
                blocked_same_hash=0,
                blocked_same_holding=0,
            )
            with mock.patch.object(MODULE, "_assert_plan_inputs_unchanged"):
                queue_root = MODULE.publish_queue(queue_plan)

            review_path = (
                root
                / "training"
                / "hard_negative_review"
                / "reviews"
                / "review.json"
            )
            timestamps = iter(
                [
                    "2026-07-28T20:01:00Z",
                    "2026-07-28T20:02:00Z",
                    "2026-07-28T20:03:00Z",
                    "2026-07-28T20:04:00Z",
                ]
            )
            store = BccHardNegativeReviewStore(
                queue_root,
                review_path,
                "Besitzer",
                now_utc=lambda: next(timestamps),
            )
            store.prepare_output()
            store.set_decision(queue_items[0].item_id, "all_classes_clear")
            store.set_decision(queue_items[1].item_id, "mapped_object_visible")
            store.set_decision(queue_items[2].item_id, "all_classes_clear")

            with mock.patch.object(
                MODULE,
                "_assert_negative_set_protection",
                return_value=binding,
            ):
                plan = MODULE.build_negative_set_plan(
                    root,
                    base_model,
                    queue_root,
                    review_path,
                    class_map_path=class_map_path,
                    vsa_manifest_path=vsa_path,
                    created_utc=datetime(2026, 7, 28, 20, 5, tzinfo=timezone.utc),
                )
                target = MODULE.publish_negative_set(plan)

            self.assertEqual(2, len(plan.items))
            self.assertEqual({"train", "validation"}, {item.split for item in plan.items})
            self.assertNotIn(
                queue_items[1].image_sha256,
                {item.image_sha256 for item in plan.items},
            )
            manifest = json.loads(
                (target / "_manifest.json").read_text(encoding="utf-8")
            )
            self.assertEqual(
                plan.set_id,
                self._sha(MODULE._canonical_json_bytes(manifest["semantic"])),
            )
            self.assertEqual(6, manifest["hashes_count"])
            self.assertEqual(
                {
                    "receipts/review.json",
                    "receipts/queue_manifest.json",
                    "receipts/queue_candidates.json",
                    "receipts/class_map.json",
                },
                {
                    relative
                    for relative in manifest["hashes"]
                    if relative.startswith("receipts/")
                },
            )
            self.assertEqual(
                review_path.read_bytes(),
                (target / "receipts" / "review.json").read_bytes(),
            )
            with mock.patch.object(
                MODULE,
                "_assert_negative_set_protection",
                return_value=binding,
            ):
                with self.assertRaises(FileExistsError):
                    MODULE.publish_negative_set(plan)

    def test_unvollstaendiges_review_darf_nicht_veroeffentlicht_werden(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "training").mkdir()
            base_model = root / "base.pt"
            base_model.write_bytes(b"base-model")
            class_map_path = root / "class_map.json"
            class_map_path.write_text("{}", encoding="utf-8")
            class_map_sha = self._sha(class_map_path.read_bytes())
            vsa_path = root / "vsa.json"
            vsa_path.write_text("{}", encoding="utf-8")
            binding = MODULE.ClassMapBinding(
                class_map_path,
                class_map_sha,
                3,
                "2" * 64,
                tuple([f"class-{index}" for index in range(14)] + ["BCC_bogen"]),
            )
            queue_item = self._queue_item(root, 0)
            semantic = self._semantic(queue_item, class_map_sha=class_map_sha)
            queue_id = self._sha(MODULE._canonical_json_bytes(semantic))
            queue_plan = MODULE.QueuePlan(
                knowledge_root=root,
                base_model_path=base_model,
                class_map=binding,
                vsa_manifest_path=vsa_path,
                created_utc=datetime(2026, 7, 28, 20, 0, tzinfo=timezone.utc),
                sources=(),
                source_specs=(),
                protected_sets=(),
                protection_snapshot={},
                model_scope=tuple(semantic["model_scope"]),
                items=(queue_item,),
                semantic_payload=semantic,
                queue_id=queue_id,
                target_root=(
                    root
                    / "training"
                    / "hard_negative_review"
                    / "queues"
                    / f"bcc_hn_{queue_id[:12]}"
                ),
                scanned_photos=1,
                clean_holdings=1,
                blocked_same_hash=0,
                blocked_same_holding=0,
            )
            with mock.patch.object(MODULE, "_assert_plan_inputs_unchanged"):
                queue_root = MODULE.publish_queue(queue_plan)
            review_path = (
                root
                / "training"
                / "hard_negative_review"
                / "reviews"
                / "review.json"
            )
            store = BccHardNegativeReviewStore(
                queue_root,
                review_path,
                "Besitzer",
                now_utc=lambda: "2026-07-28T20:01:00Z",
            )
            store.prepare_output()

            with mock.patch.object(
                MODULE,
                "_assert_negative_set_protection",
                return_value=binding,
            ):
                with self.assertRaisesRegex(ValueError, "noch nicht vollstaendig"):
                    MODULE.build_negative_set_plan(
                        root,
                        base_model,
                        queue_root,
                        review_path,
                        class_map_path=class_map_path,
                        vsa_manifest_path=vsa_path,
                    )

    def _photo(
        self,
        digest: str,
        holding: str,
        physical: str,
    ):
        return MODULE.holdout_tools.SourcePhoto(
            source_id="4" * 64,
            source_path=Path(f"{digest[:4]}.jpg"),
            image_sha256=digest,
            holding_key=holding,
            physical_holding_key=physical,
            inspection_date="2026-04-20",
            source_code="BABAA",
        )

    @staticmethod
    def _prediction(item_id: str, positive: bool, confidence: float | None):
        return MODULE.evaluation_tools.RawPrediction(
            item_id=item_id,
            predicted_positive=positive,
            detection_count=1 if positive else 0,
            max_confidence=confidence,
            inference_time_ms=1.0,
            technical_error=None,
        )

    def _queue_item(self, root: Path, index: int):
        source = root / f"source-{index}.jpg"
        source.write_bytes(
            b"\xff\xd8\xff\xe0"
            + bytes([65 + index]) * 1_100
        )
        image_sha = self._sha(source.read_bytes())
        return MODULE.QueueItem(
            item_id=f"bcc-hn-{image_sha[:16]}",
            source_path=source,
            image_sha256=image_sha,
            holding_key=f"{100 + index}-{200 + index}",
            physical_holding_key=f"{100 + index}|{200 + index}",
            source_ref=f"{index + 3}" * 64,
            inspection_date="2026-04-20",
            size_bytes=source.stat().st_size,
            image_format="jpg",
            predictions=(
                {
                    "model_id": "model-a",
                    "predicted_bcc": True,
                    "bcc_detection_count": 1,
                    "max_bcc_confidence": 0.8,
                },
            ),
        )

    @staticmethod
    def _semantic(*items, class_map_sha: str = "1" * 64) -> dict[str, object]:
        class_names = [f"class-{index}" for index in range(14)] + ["BCC_bogen"]
        model_scope = [
            {
                "candidate_id": "model-a",
                "candidate_manifest_sha256": "5" * 64,
                "weights_sha256": "6" * 64,
            }
        ]
        return {
            "schema_version": "1.0",
            "purpose": "bcc_hard_negative_review_queue",
            "pilot": "BCC_bogen",
            "role": "training_candidate_review",
            "class_map_version": 3,
            "class_map_sha256": class_map_sha,
            "vsa_manifest_hash": "2" * 64,
            "class_names": class_names,
            "protected_sets": [],
            "protection_snapshot": {},
            "model_scope": model_scope,
            "selection_rule": {
                "one_image_per_physical_holding": True,
                "requires_current_model_bcc_trigger": True,
                "review_target": (
                    "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse"
                ),
            },
            "sources": [],
            "items": [MODULE._semantic_item(item) for item in items],
        }

    @staticmethod
    def _sha(payload: bytes) -> str:
        return hashlib.sha256(payload).hexdigest()


if __name__ == "__main__":
    unittest.main()
