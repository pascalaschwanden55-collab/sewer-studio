from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "prepare_bcc_pilot.py"
SPEC = importlib.util.spec_from_file_location("prepare_bcc_pilot", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class PrepareBccPilotTests(unittest.TestCase):
    def test_build_and_execute_uses_only_complete_personal_bcc_gold(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            gold = root / "gold_frames" / "BCC - Bogen"
            gold.mkdir(parents=True)
            samples = []
            for index in range(30):
                frame = gold / f"gold_{index:02d}.jpg"
                frame.write_bytes(f"frame-{index}".encode())
                samples.append(self._sample(index, frame))

            duplicate = self._sample(99, gold / "gold_00.jpg")
            duplicate["SampleId"] = "sample-newer"
            duplicate["ConfirmedAtUtc"] = "2026-07-24T20:00:00Z"
            samples.append(duplicate)
            samples.append(
                self._sample(100, gold / "gold_01.jpg")
                | {"SampleId": "auto", "SourceType": "BatchImport"}
            )
            (root / "training_samples.json").write_text(
                json.dumps(samples),
                encoding="utf-8",
            )
            self._eval_set(root, "eval_visible_clean_eval_set")
            self._eval_set(root, "eval_unclean_or_hidden_eval_set")

            preparation = MODULE.build_preparation(root, "Besitzer")

            self.assertEqual(30, len(preparation.selected_samples))
            self.assertIn("sample-00", preparation.duplicate_sample_ids)
            self.assertIn(
                "sample-newer",
                {sample.sample_id for sample in preparation.selected_samples},
            )
            self.assertGreaterEqual(preparation.validation_images, 6)
            self.assertEqual(
                30,
                preparation.train_images + preparation.validation_images,
            )

            MODULE.execute_preparation(
                preparation,
                "Besitzer",
                datetime(2026, 7, 24, 17, 30, tzinfo=timezone.utc),
            )
            registry = json.loads(preparation.registry_path.read_text(encoding="utf-8"))
            self.assertEqual("approved", registry["approval_status"])
            self.assertEqual(30, len(registry["approved_sample_ids"]))
            self.assertEqual(2, len(registry["protected_sets"]))
            self.assertTrue(preparation.audit_path.is_file())

    def test_negatives_dir_feeds_registry_and_audit_when_present(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self._make_gold_root(root, 30)
            self._eval_set(root, "eval_visible_clean_eval_set")
            negatives = root / "negatives"
            negatives.mkdir()
            first = negatives / "normal_01.png"
            first.write_bytes(b"\x89PNG" + b"a" * 2000)
            second = negatives / "normal_02.jpg"
            second.write_bytes(b"\xff\xd8\xff" + b"b" * 2000)
            (negatives / "notizen.txt").write_text("kein bild")  # wird ignoriert

            preparation = MODULE.build_preparation(root, "Besitzer", negatives)

            self.assertEqual(2, len(preparation.negative_images))
            hashes = {entry["sha256"] for entry in preparation.negative_images}
            self.assertEqual(
                {MODULE._sha256_file(first), MODULE._sha256_file(second)},
                hashes,
            )

            MODULE.execute_preparation(
                preparation,
                "Besitzer",
                datetime(2026, 7, 24, 17, 30, tzinfo=timezone.utc),
            )
            registry = json.loads(preparation.registry_path.read_text(encoding="utf-8"))
            self.assertEqual(2, len(registry["negative_images"]))
            audit = json.loads(preparation.audit_path.read_text(encoding="utf-8"))
            self.assertEqual(2, audit["negative_images"])

    def test_missing_negatives_dir_keeps_registry_without_the_field(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self._make_gold_root(root, 30)
            self._eval_set(root, "eval_visible_clean_eval_set")

            preparation = MODULE.build_preparation(
                root, "Besitzer", root / "negatives_fehlt"
            )

            self.assertEqual((), preparation.negative_images)
            MODULE.execute_preparation(
                preparation,
                "Besitzer",
                datetime(2026, 7, 24, 17, 30, tzinfo=timezone.utc),
            )
            registry = json.loads(preparation.registry_path.read_text(encoding="utf-8"))
            self.assertNotIn("negative_images", registry)

    @staticmethod
    def _make_gold_root(root: Path, count: int) -> None:
        gold = root / "gold_frames" / "BCC - Bogen"
        gold.mkdir(parents=True)
        samples = []
        for index in range(count):
            frame = gold / f"gold_{index:02d}.jpg"
            frame.write_bytes(f"frame-{index}".encode())
            samples.append(PrepareBccPilotTests._sample(index, frame))
        (root / "training_samples.json").write_text(
            json.dumps(samples),
            encoding="utf-8",
        )

    @staticmethod
    def _sample(index: int, frame: Path) -> dict[str, object]:
        return {
            "SampleId": f"sample-{index:02d}",
            "CaseId": f"foto-{index:02d}",
            "Code": "BCCAY",
            "FramePath": str(frame),
            "Status": 1,
            "SourceType": "ManualCoding",
            "HumanConfirmed": True,
            "Corrected": False,
            "ConfirmedByUser": "Besitzer",
            "ConfirmedAtUtc": f"2026-07-24T17:{index:02d}:00Z",
            "MatchLevel": "ReviewApproved",
            "HasBbox": True,
            "HasSamMask": True,
        }

    @staticmethod
    def _eval_set(root: Path, name: str) -> None:
        set_root = root / "eval_set" / "subsets" / name
        set_root.mkdir(parents=True)
        (set_root / "_manifest.json").write_text(
            json.dumps({"frozen": True}),
            encoding="utf-8",
        )


if __name__ == "__main__":
    unittest.main()
