import hashlib
import importlib.util
import json
from pathlib import Path
import sqlite3
import sys
import tempfile
import unittest
from unittest import mock


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "repair_gold_holding_ids.py"
sys.path.insert(0, str(SCRIPT_PATH.parent))
SPEC = importlib.util.spec_from_file_location("repair_gold_holding_ids", SCRIPT_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class RepairGoldHoldingIdsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / "brain"
        self.sources = Path(self.temp.name) / "sources"
        self.gold = self.root / "gold_frames" / "BCA - Anschluss"
        self.repairs = self.root / "training" / "repairs"
        self.gold.mkdir(parents=True)
        self.sources.mkdir()
        self.repairs.mkdir(parents=True)
        self.image_bytes = b"unveraendertes-testbild"
        image_hash = hashlib.sha256(self.image_bytes).hexdigest()
        self.gold_image = self.gold / f"gold_{image_hash}.jpg"
        self.gold_image.write_bytes(self.image_bytes)
        self.source_image = self.sources / "07.123-456_p2_img4.jpg"
        self.source_image.write_bytes(self.image_bytes)
        self.sample = {
            "SampleId": "wb_test",
            "CaseId": "foto_20260802_1",
            "Code": "BCAAA",
            "FramePath": str(self.gold_image),
            "Status": 1,
            "HumanConfirmed": True,
            "ConfirmedByUser": "Besitzer",
            "HasBbox": True,
            "HasSamMask": True,
            "Signature": "foto_20260802_1|BCAAA|0.0|0.0|b:0.5,0.5,0.2,0.2",
            "Notes": "",
        }
        self.teacher = {
            "annotationId": "teacher_test",
            "sourceSampleId": "wb_test",
            "haltungName": "foto_20260802_1",
            "vsaCode": "BCAAA",
        }
        self.samples_path = self.root / "training_samples.json"
        self.teacher_path = self.root / "teacher_annotations.json"
        self._write_json(self.samples_path, [self.sample])
        self._write_json(self.teacher_path, [self.teacher])
        self.database_path = self.root / "KnowledgeBase.db"
        connection = sqlite3.connect(self.database_path)
        connection.execute(
            "CREATE TABLE Samples (SampleId TEXT PRIMARY KEY, CaseId TEXT NOT NULL)"
        )
        connection.execute(
            "INSERT INTO Samples (SampleId, CaseId) VALUES (?, ?)",
            ("wb_test", "foto_20260802_1"),
        )
        connection.commit()
        connection.close()

    def tearDown(self):
        self.temp.cleanup()

    @staticmethod
    def _write_json(path, value):
        path.write_text(
            json.dumps(value, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    def test_prueflauf_findet_bytegleiche_haltung_ohne_schreiben(self):
        samples_before = self.samples_path.read_bytes()
        teacher_before = self.teacher_path.read_bytes()

        plan = MODULE.build_plan(self.root, self.sources, "Besitzer")

        repair = self.assertEqual(1, len(plan.repairs)) or plan.repairs[0]
        self.assertEqual("123-456", repair.new_case_id)
        self.assertEqual(samples_before, self.samples_path.read_bytes())
        self.assertEqual(teacher_before, self.teacher_path.read_bytes())
        self.assertEqual([], list(self.repairs.iterdir()))

    def test_ausfuehrung_aktualisiert_drei_speicher_und_sichert_vorherstand(self):
        plan = MODULE.build_plan(self.root, self.sources, "Besitzer")

        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=False):
            backup_dir = MODULE.execute_plan(
                plan,
                MODULE.datetime(2026, 8, 2, 20, 30, tzinfo=MODULE.timezone.utc),
            )

        sample = json.loads(self.samples_path.read_text(encoding="utf-8"))[0]
        teacher = json.loads(self.teacher_path.read_text(encoding="utf-8"))[0]
        connection = sqlite3.connect(self.database_path)
        try:
            database_case = connection.execute(
                "SELECT CaseId FROM Samples WHERE SampleId = 'wb_test'"
            ).fetchone()[0]
        finally:
            connection.close()
        self.assertEqual("123-456", sample["CaseId"])
        self.assertTrue(sample["Signature"].startswith("123-456|"))
        self.assertIn("Bild-SHA-Match", sample["Notes"])
        self.assertEqual("123-456", teacher["haltungName"])
        self.assertEqual("123-456", database_case)
        self.assertTrue((backup_dir / "training_samples.before.json").is_file())
        self.assertTrue((backup_dir / "teacher_annotations.before.json").is_file())
        self.assertTrue((backup_dir / "KnowledgeBase.before.db").is_file())
        result = json.loads((backup_dir / "repair_result.json").read_text(encoding="utf-8"))
        self.assertTrue(result["verified"])
        self.assertEqual(self.image_bytes, self.source_image.read_bytes())

    def test_mehrere_bytegleiche_quellnamen_stoppen_vor_mutation(self):
        second = self.sources / "999-888_p2_img4.jpg"
        second.write_bytes(self.image_bytes)
        samples_before = self.samples_path.read_bytes()

        with self.assertRaisesRegex(ValueError, "2 statt genau einer"):
            MODULE.build_plan(self.root, self.sources, "Besitzer")

        self.assertEqual(samples_before, self.samples_path.read_bytes())
        self.assertEqual([], list(self.repairs.iterdir()))

    def test_neue_signaturkollision_stoppt_vor_mutation(self):
        second = dict(self.sample)
        second["SampleId"] = "wb_other"
        second["CaseId"] = "123-456"
        second["Signature"] = "123-456|BCAAA|0.0|0.0|b:0.5,0.5,0.2,0.2"
        second["HumanConfirmed"] = False
        self._write_json(self.samples_path, [self.sample, second])
        samples_before = self.samples_path.read_bytes()

        with self.assertRaisesRegex(ValueError, "kollidiert"):
            MODULE.build_plan(self.root, self.sources, "Besitzer")

        self.assertEqual(samples_before, self.samples_path.read_bytes())
        self.assertEqual([], list(self.repairs.iterdir()))


if __name__ == "__main__":
    unittest.main()
