from __future__ import annotations

import importlib.util
import sys
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "detect_gold_holdout_provenance.py"
SPEC = importlib.util.spec_from_file_location("detect_gold_holdout_provenance", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def audit(sample_id: str, *, code: str = "BABBB", role: str = "test"):
    return {
        "sample_id": sample_id,
        "case_id": "100-200",
        "haltung_key": "100-200",
        "code": code,
        "hauptcode": code[:3],
        "image_sha256": "a" * 64,
        "rolle": role,
        "gruppe": "haltung:100-200",
    }


class DetectGoldHoldoutProvenanceTests(unittest.TestCase):
    def test_stabiler_test_schnitt_wird_gebunden(self) -> None:
        result = MODULE._stable_test_entries(
            [audit("s1"), audit("train", role="train")],
            [audit("s1"), audit("neu")],
            ["s1"],
        )
        self.assertEqual(["s1"], [item["sample_id"] for item in result])

    def test_veraenderte_testmetadaten_stoppen(self) -> None:
        changed = audit("s1", code="BACB")
        with self.assertRaisesRegex(ValueError, "veraendert"):
            MODULE._stable_test_entries([audit("s1")], [changed], ["s1"])

    def test_physische_haltung_wird_auch_ueber_gegenrichtung_ausgeschlossen(self) -> None:
        image = MODULE.HoldoutImage(
            image_id="a" * 64,
            image_path=Path("bild.jpg"),
            image_sha256="a" * 64,
            holding_key="100-200",
            physical_holding_key="100|200",
            instances=(
                MODULE.HoldoutInstance(
                    "s1",
                    "BABBB",
                    1,
                    "BAB_riss",
                    MODULE.HoldoutBox(0.5, 0.5, 0.4, 0.4),
                    "ManualCoding",
                ),
            ),
        )
        dataset = {
            "images": [
                {
                    "image_sha256": "b" * 64,
                    "holding_key": "200-100",
                    "labels": [],
                    "is_negative": True,
                }
            ]
        }

        eligible, excluded = MODULE._dataset_contamination([image], dataset)

        self.assertEqual((), eligible)
        self.assertEqual("100|200", excluded[0].physical_holding_key)
        self.assertIn(
            "physical_holding_overlap_including_reverse_direction",
            excluded[0].reasons,
        )


if __name__ == "__main__":
    unittest.main()
