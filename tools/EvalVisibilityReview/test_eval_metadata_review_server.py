import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from tools.EvalVisibilityReview.eval_metadata_review_server import (
    INDEX_HTML,
    EvalMetadataReviewStore,
)


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

    def test_store_liefert_code_und_klartext_aus_aktivem_katalog(self):
        self._write_eval_set(
            [self._candidate("damage-a", "BAJA", "H-1", 1.2)]
        )
        catalog_path = self._write_catalog(
            {
                "BAJA": "Breite Rohrverbindung",
            }
        )

        store = EvalMetadataReviewStore(
            self.eval_root,
            self.output,
            catalog_path=catalog_path,
        )

        item = store.state()["items"][0]
        self.assertEqual("BAJA", item["expected_code"])
        self.assertEqual("Breite Rohrverbindung", item["expected_title"])

    def test_falscher_code_kann_mit_katalogklartext_korrigiert_werden(self):
        self._write_eval_set(
            [self._candidate("damage-a", "BAJA", "H-1", 1.2)]
        )
        catalog_path = self._write_catalog(
            {
                "BAJA": "Breite Rohrverbindung",
                "BAIZ": "Einragendes Dichtungsmaterial",
            }
        )
        store = EvalMetadataReviewStore(
            self.eval_root,
            self.output,
            catalog_path=catalog_path,
            now_utc=lambda: "2026-07-27T15:30:00+00:00",
        )

        state = store.set_review(
            "damage-a",
            3,
            "H-1-BAIZ-01",
            None,
            None,
            "Pascal",
            "Vorgabe war falsch.",
            code_decision="corrected",
            corrected_code="baiz",
        )

        item = state["items"][0]
        self.assertTrue(item["complete"])
        self.assertEqual("corrected", item["code_decision"])
        self.assertEqual("BAIZ", item["effective_code"])
        self.assertEqual("Einragendes Dichtungsmaterial", item["effective_title"])
        self.assertFalse(item["excluded_from_damage_eval"])

    def test_kein_passender_schaden_ist_ohne_stufe_und_ereignis_abgeschlossen(self):
        self._write_eval_set(
            [self._candidate("damage-a", "BAJA", "H-1", 1.2)]
        )
        store = EvalMetadataReviewStore(
            self.eval_root,
            self.output,
            now_utc=lambda: "2026-07-27T15:30:00+00:00",
        )

        state = store.set_review(
            "damage-a",
            None,
            None,
            None,
            None,
            "Pascal",
            "Kein passender Schaden sichtbar.",
            code_decision="no_damage",
        )

        item = state["items"][0]
        self.assertTrue(item["complete"])
        self.assertEqual("no_damage", item["code_decision"])
        self.assertIsNone(item["effective_code"])
        self.assertTrue(item["excluded_from_damage_eval"])
        self.assertIsNone(item["expected_severity"])
        self.assertIsNone(item["event_id"])

    def test_korrektur_akzeptiert_nur_ba_oder_bb_code_aus_dem_katalog(self):
        self._write_eval_set(
            [self._candidate("damage-a", "BAJA", "H-1", 1.2)]
        )
        catalog_path = self._write_catalog(
            {
                "BAJA": "Breite Rohrverbindung",
                "BCC": "Anschluss",
            }
        )
        store = EvalMetadataReviewStore(
            self.eval_root,
            self.output,
            catalog_path=catalog_path,
        )

        with self.assertRaisesRegex(ValueError, "BA- oder BB-Schadencode"):
            store.set_review(
                "damage-a",
                3,
                "event-1",
                None,
                None,
                "Pascal",
                "",
                code_decision="corrected",
                corrected_code="BCC",
            )

    def test_bestehender_ereigniskonflikt_bleibt_zur_korrektur_offen(self):
        self._write_eval_set(
            [
                self._candidate("damage-a", "BAJA", "H-1", 1.2),
                self._candidate("damage-b", "BAIZ", "H-1", 2.2),
            ]
        )
        first = EvalMetadataReviewStore(
            self.eval_root,
            self.output,
            now_utc=lambda: "2026-07-27T15:30:00+00:00",
        )
        first.set_review("damage-a", 2, "event-1", None, None, "Pascal", "")
        saved = json.loads(self.output.read_text(encoding="utf-8"))
        saved["reviews"][1].update(
            {
                "code_decision": "matches",
                "expected_severity": 2,
                "event_id": "event-1",
                "reviewed_by": "Pascal",
                "reviewed_at_utc": "2026-07-27T15:31:00+00:00",
            }
        )
        self.output.write_text(
            json.dumps(saved, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )

        resumed = EvalMetadataReviewStore(self.eval_root, self.output)
        state = resumed.state()

        self.assertEqual(0, state["done"])
        self.assertEqual(2, state["open"])
        self.assertEqual(2, state["conflicting_reviews"])
        self.assertTrue(all(item["event_conflict"] for item in state["items"]))

    def test_pruefplatz_erklaert_die_wirkung_der_schadensstufe(self):
        self.assertIn("Stufe 4 und 5", INDEX_HTML)
        self.assertIn("weder den Code noch die Zustandsklasse", INDEX_HTML)
        self.assertIn("Anderer Schadencode", INDEX_HTML)
        self.assertIn("kein passender Schaden sichtbar", INDEX_HTML)

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

    def _write_catalog(self, entries):
        catalog_path = self.root / "vsa_catalog.json"
        catalog_path.write_text(
            json.dumps(
                {
                    "codes": [
                        {
                            "code": code,
                            "title": title,
                        }
                        for code, title in entries.items()
                    ]
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        return catalog_path

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
