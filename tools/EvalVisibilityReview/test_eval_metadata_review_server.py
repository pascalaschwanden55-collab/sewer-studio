import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from tools.EvalVisibilityReview.eval_metadata_review_server import EvalMetadataReviewStore


class EvalMetadataReviewStoreTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.root = Path(self._temp.name)
        self.eval_root = self.root / "eval_set"
        self.image_root = self.eval_root / "images"
        self.image_root.mkdir(parents=True)
        self.output = self.root / "review" / "event_metadata_review.json"

    def tearDown(self):
        self._temp.cleanup()

    def test_store_laesst_original_unveraendert_und_laesst_nur_schaeden(self):
        self._write_eval_set(
            [
                self._candidate("damage-a", "BAIZ", "H-1", 1.2),
                self._candidate("structure", "BCD", "H-1", 0.0),
                self._candidate("damage-b", "BBCC", "H-2", 3.4),
            ]
        )
        candidates = self.eval_root / "_candidates.json"
        before = self._sha256(candidates)

        store = EvalMetadataReviewStore(self.eval_root, self.output)
        store.prepare_output()

        self.assertEqual(before, self._sha256(candidates))
        self.assertEqual(2, store.state()["total"])
        self.assertTrue(self.output.exists())
        saved = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual(before, saved["source_candidates_sha256"])
        self.assertEqual(["damage-a", "damage-b"], [row["id"] for row in saved["reviews"]])
        self.assertNotIn("structure", self.output.read_text(encoding="utf-8"))

    def test_review_braucht_stufe_und_ereignis_id(self):
        self._write_eval_set([self._candidate("damage-a", "BAIZ", "H-1", 1.2)])
        store = EvalMetadataReviewStore(self.eval_root, self.output)

        with self.assertRaisesRegex(ValueError, "Schadensstufe"):
            store.set_review("damage-a", None, "H-1-BAIZ-01", None, None, "Pascal", "")

        with self.assertRaisesRegex(ValueError, "Ereignis-ID"):
            store.set_review("damage-a", 4, " ", None, None, "Pascal", "")

        self.assertFalse(self.output.exists())

    def test_review_speichert_normalisierte_fachangaben_atomar(self):
        self._write_eval_set([self._candidate("damage-a", "BAIZ", "H-1", 1.2)])
        store = EvalMetadataReviewStore(
            self.eval_root,
            self.output,
            now_utc=lambda: "2026-07-27T08:00:00+00:00",
        )

        state = store.set_review(
            "damage-a",
            4,
            " H-1-BAIZ-01 ",
            1.0,
            1.5,
            " Pascal ",
            " deutlich ",
        )

        self.assertEqual(1, state["done"])
        self.assertEqual(0, state["open"])
        saved = json.loads(self.output.read_text(encoding="utf-8"))
        review = saved["reviews"][0]
        self.assertEqual(4, review["expected_severity"])
        self.assertEqual("H-1-BAIZ-01", review["event_id"])
        self.assertEqual(1.0, review["meter_start"])
        self.assertEqual(1.5, review["meter_end"])
        self.assertEqual("Pascal", review["reviewed_by"])
        self.assertEqual("deutlich", review["comment"])
        self.assertEqual("2026-07-27T08:00:00+00:00", review["reviewed_at_utc"])

    def test_review_blockiert_unvollstaendigen_oder_unpassenden_meterbereich(self):
        self._write_eval_set([self._candidate("damage-a", "BAIZ", "H-1", 1.2)])
        store = EvalMetadataReviewStore(self.eval_root, self.output)

        with self.assertRaisesRegex(ValueError, "gemeinsam"):
            store.set_review("damage-a", 3, "event-1", 1.0, None, "Pascal", "")

        with self.assertRaisesRegex(ValueError, "aufsteigend"):
            store.set_review("damage-a", 3, "event-1", 2.0, 1.0, "Pascal", "")

        with self.assertRaisesRegex(ValueError, "ausserhalb"):
            store.set_review("damage-a", 3, "event-1", 2.0, 3.0, "Pascal", "")

    def test_store_setzt_bestehende_pruefung_fort(self):
        self._write_eval_set(
            [
                self._candidate("damage-a", "BAIZ", "H-1", 1.2),
                self._candidate("damage-b", "BBCC", "H-2", 3.4),
            ]
        )
        first = EvalMetadataReviewStore(
            self.eval_root,
            self.output,
            now_utc=lambda: "2026-07-27T08:00:00+00:00",
        )
        first.set_review("damage-a", 2, "event-a", None, None, "Pascal", "")

        resumed = EvalMetadataReviewStore(self.eval_root, self.output)
        state = resumed.state()

        self.assertEqual(1, state["done"])
        self.assertEqual(1, state["open"])
        reviewed = next(row for row in state["items"] if row["id"] == "damage-a")
        self.assertEqual(2, reviewed["expected_severity"])
        self.assertEqual("event-a", reviewed["event_id"])
        self.assertEqual("damage-b", state["current"]["id"])

    def test_store_blockiert_fremden_oder_veralteten_ausgabestand(self):
        self._write_eval_set([self._candidate("damage-a", "BAIZ", "H-1", 1.2)])
        self.output.parent.mkdir(parents=True)
        self.output.write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "source_candidates_sha256": "falscher-hash",
                    "reviews": [],
                }
            ),
            encoding="utf-8",
        )

        with self.assertRaisesRegex(ValueError, "anderen Eval-Stand"):
            EvalMetadataReviewStore(self.eval_root, self.output)

    def test_ausgabe_darf_nicht_im_eingefrorenen_eval_ordner_liegen(self):
        self._write_eval_set([self._candidate("damage-a", "BAIZ", "H-1", 1.2)])

        with self.assertRaisesRegex(ValueError, "ausserhalb"):
            EvalMetadataReviewStore(
                self.eval_root,
                self.eval_root / "event_metadata_review.json",
            )

    def _write_eval_set(self, candidates):
        for candidate in candidates:
            image_name = Path(candidate["frame_path"]).name
            (self.image_root / image_name).write_bytes(b"png-" + candidate["id"].encode())
        (self.eval_root / "_candidates.json").write_text(
            json.dumps(candidates, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )

    @staticmethod
    def _candidate(case_id, code, holding, meter):
        return {
            "id": case_id,
            "frame_path": f"C:\\source\\{case_id}.png",
            "haltung_key": holding,
            "meter": meter,
            "code_full": code,
            "code_main": code[:3],
            "kategorie": "damage" if code.startswith(("BA", "BB")) else "structure",
        }

    @staticmethod
    def _sha256(path):
        return hashlib.sha256(path.read_bytes()).hexdigest()


if __name__ == "__main__":
    unittest.main()
