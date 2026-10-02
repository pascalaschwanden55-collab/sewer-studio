"""Tests fuer repair_inbox_gold_holding_ids.py (Audit T2, 02.10.2026).

Das Skript schreibt gold_inbox-Pseudo-CaseIds auf die per Kandidaten-Hash
belegte Haltung um (Samples, Teacher-Datei, SQLite). Gehalten werden: Pruefen
schreibfrei, Sicherung vor dem Schreiben, Eval-Vorflug (Ziel in geschuetzter
Haltung wird dekontaminiert statt repariert), harte Sperren bei Kollision oder
Abweichung, Rueckrollen bei Fehlern. Nur synthetische Daten im Temp-Ordner,
nichts unter dem echten Wissensordner.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sqlite3
import sys
import tempfile
import unittest
from unittest import mock


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "repair_inbox_gold_holding_ids.py"
sys.path.insert(0, str(SCRIPT_PATH.parent))
SPEC = importlib.util.spec_from_file_location("repair_inbox_gold_holding_ids", SCRIPT_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

BILD_SHA = hashlib.sha256(b"inbox-testbild").hexdigest()
ALTE_CASE_ID = "gold_inbox_abc123"
NEUE_HALTUNG = "1234-5678"


class RepairInboxGoldHoldingIdsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        base = Path(self.temp.name)
        self.root = base / "brain"
        self.root.mkdir()
        self.samples_path = self.root / "training_samples.json"
        self.teacher_path = self.root / "teacher_annotations.json"
        self.database_path = self.root / "KnowledgeBase.db"
        self.measurement_path = base / "messung.json"
        self.sample = {
            "SampleId": "wb_inbox",
            "CaseId": ALTE_CASE_ID,
            "Code": "BCAAA",
            "FramePath": str(self.root / "gold_frames" / f"gold_{BILD_SHA}.jpg"),
            "Signature": f"{ALTE_CASE_ID}|BCAAA|0.0|0.0",
            "Notes": "",
            "TrainingEligible": True,
        }
        self.teacher = {"annotationId": "t1", "sourceSampleId": "wb_inbox", "haltungName": ""}
        self._write(self.samples_path, [self.sample])
        self._write(self.teacher_path, [self.teacher])
        self._write_measurement([{"bild_sha256": BILD_SHA, "haltung": NEUE_HALTUNG, "quelle": "klasse_a"}])
        connection = sqlite3.connect(self.database_path)
        connection.execute("CREATE TABLE Samples (SampleId TEXT PRIMARY KEY, CaseId TEXT NOT NULL)")
        connection.execute("INSERT INTO Samples VALUES (?, ?)", ("wb_inbox", ALTE_CASE_ID))
        connection.commit()
        connection.close()

    def tearDown(self):
        self.temp.cleanup()

    @staticmethod
    def _write(path, value):
        path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def _write_measurement(self, kandidaten):
        self._write(self.measurement_path, {"klassen": {"BCA": {"kandidaten": kandidaten}}})

    def _db_case(self):
        connection = sqlite3.connect(self.database_path)
        try:
            return connection.execute(
                "SELECT CaseId FROM Samples WHERE SampleId = 'wb_inbox'").fetchone()[0]
        finally:
            connection.close()

    def _stand(self):
        return (self.samples_path.read_bytes(), self.teacher_path.read_bytes(), self._db_case())

    def _protect_with_eval(self, key):
        (self.root / "eval_set" / "holdout1").mkdir(parents=True)
        self._write(self.root / "eval_set" / "holdout1" / "_candidates.json",
                    {"candidates": [{"haltung_key": key}]})

    def _plan(self, explicit=None):
        return MODULE.build_plan(self.root, self.measurement_path, explicit)

    def _main(self, *extra):
        out, err = io.StringIO(), io.StringIO()
        argv = ["--knowledge-root", str(self.root), "--measurement", str(self.measurement_path), *extra]
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = MODULE.main(argv)
        return code, out.getvalue(), err.getvalue()

    def _ausfuehren(self, plan):
        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=False):
            return MODULE.execute_plan(plan)

    # --- Kernverhalten ---------------------------------------------------

    def test_prueflauf_plant_reparatur_und_schreibt_nichts(self):
        vorher = self._stand()

        code, out, _ = self._main()

        self.assertEqual(0, code)
        self.assertIn("PRUEFLAUF", out)
        self.assertIn("Reparaturen: 1", out)
        self.assertIn("Keine Datei wurde veraendert", out)
        self.assertEqual(vorher, self._stand())
        self.assertFalse((self.root / "training").exists())

    def test_ausfuehrung_zieht_samples_teacher_und_datenbank_gleich_und_sichert(self):
        samples_vorher = self.samples_path.read_bytes()
        teacher_vorher = self.teacher_path.read_bytes()

        backup = self._ausfuehren(self._plan())

        sample = json.loads(self.samples_path.read_text(encoding="utf-8"))[0]
        teacher = json.loads(self.teacher_path.read_text(encoding="utf-8"))[0]
        self.assertEqual(NEUE_HALTUNG, sample["CaseId"])
        self.assertEqual(f"{NEUE_HALTUNG}|BCAAA|0.0|0.0", sample["Signature"])
        self.assertIn("Kandidaten-Byte-Match klasse_a", sample["Notes"])
        self.assertIs(True, sample["TrainingEligible"])
        self.assertEqual(NEUE_HALTUNG, teacher["haltungName"])
        self.assertEqual(NEUE_HALTUNG, self._db_case())
        self.assertEqual(samples_vorher, (backup / "training_samples.before.json").read_bytes())
        self.assertEqual(teacher_vorher, (backup / "teacher_annotations.before.json").read_bytes())
        gesicherte_db = sqlite3.connect(backup / "KnowledgeBase.before.db")
        try:
            self.assertEqual(
                ALTE_CASE_ID,
                gesicherte_db.execute("SELECT CaseId FROM Samples").fetchone()[0])
        finally:
            gesicherte_db.close()
        beleg = json.loads((backup / "repair_result.json").read_text(encoding="utf-8"))
        self.assertEqual(1, beleg["repariert"])
        self.assertEqual(0, beleg["dekontaminiert"])
        self.assertEqual(MODULE._sha256_bytes(self.samples_path.read_bytes()),
                         beleg["output_hashes"]["training_samples"])
        self.assertEqual([], [p.name for p in self.root.glob(".*.tmp")])

    def test_main_execute_meldet_beleg_und_exit_null(self):
        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=False):
            code, out, err = self._main("--execute")

        self.assertEqual(0, code, err)
        self.assertIn("Ausgefuehrt und geprueft", out)
        self.assertEqual(NEUE_HALTUNG, self._db_case())

    def test_explizite_reparaturliste_ersetzt_die_kandidatensuche(self):
        self._write_measurement([])
        explizit = [{"sample_id": "wb_inbox", "new_case_id": "4444-5555", "beleg": "von Hand belegt"}]

        plan = self._plan(explizit)
        backup = self._ausfuehren(plan)

        sample = json.loads(self.samples_path.read_text(encoding="utf-8"))[0]
        self.assertEqual("4444-5555", sample["CaseId"])
        self.assertIn("von Hand belegt", sample["Notes"])
        self.assertTrue((backup / "repair_result.json").is_file())

    def test_explizite_zielhaltung_ohne_schachtpaar_wird_abgelehnt(self):
        explizit = [{"sample_id": "wb_inbox", "new_case_id": "Freitext", "beleg": "x"}]

        with self.assertRaisesRegex(ValueError, "nicht belastbar"):
            self._plan(explizit)

    # --- Eval-Schutz (Vorflug) -------------------------------------------

    def test_ziel_in_geschuetzter_eval_haltung_wird_dekontaminiert_nicht_repariert(self):
        self._protect_with_eval(NEUE_HALTUNG)

        plan = self._plan()
        self.assertEqual([], plan["repairs"])
        self.assertEqual(["wb_inbox"], plan["decontaminations"])
        backup = self._ausfuehren(plan)

        sample = json.loads(self.samples_path.read_text(encoding="utf-8"))[0]
        self.assertEqual(ALTE_CASE_ID, sample["CaseId"])  # nicht auf die Eval-Haltung umgeschrieben
        self.assertIs(False, sample["TrainingEligible"])
        self.assertEqual("eval-holdout-contamination-precaution", sample["TrainingEligibilityReason"])
        self.assertEqual(ALTE_CASE_ID, self._db_case())
        beleg = json.loads((backup / "repair_result.json").read_text(encoding="utf-8"))
        self.assertEqual(["wb_inbox"], beleg["decontaminations"])

    def test_eval_schutz_gilt_auch_in_gegenrichtung(self):
        self._protect_with_eval("5678-1234")

        plan = self._plan()

        self.assertEqual([], plan["repairs"])
        self.assertEqual(["wb_inbox"], plan["decontaminations"])

    def test_nur_negativsatz_ist_keine_eval_sperre(self):
        negativ = self.root / "training" / "negatives" / "satz1"
        negativ.mkdir(parents=True)
        self._write(negativ / "manifest.json", {"images": [{"holding_key": NEUE_HALTUNG}]})

        plan = self._plan()

        self.assertEqual(1, len(plan["repairs"]))
        self.assertEqual([], plan["decontaminations"])

    # --- Sperren und Fehlerfaelle ---------------------------------------

    def test_ohne_kandidatentreffer_bleibt_das_sample_unveraendert_und_offen(self):
        self._write_measurement([{"bild_sha256": "f" * 64, "haltung": NEUE_HALTUNG, "quelle": "q"}])

        plan = self._plan()

        self.assertEqual([], plan["repairs"])
        self.assertEqual({"kein_kandidaten_treffer": 1}, plan["skipped"])

    def test_mehrdeutiger_bildhash_wird_nicht_repariert(self):
        self._write_measurement([
            {"bild_sha256": BILD_SHA, "haltung": NEUE_HALTUNG, "quelle": "a"},
            {"bild_sha256": BILD_SHA, "haltung": "9999-1111", "quelle": "b"},
        ])

        plan = self._plan()

        self.assertEqual([], plan["repairs"])
        self.assertEqual({"kein_kandidaten_treffer": 1}, plan["skipped"])

    def test_keine_reparaturziele_sperrt(self):
        self._write(self.samples_path, [dict(self.sample, CaseId="1111-2222")])
        vorher = self._stand()

        code, _, err = self._main("--execute")

        self.assertEqual(1, code)
        self.assertIn("Keine Reparaturziele", err)
        self.assertEqual(vorher, self._stand())

    def test_signaturkollision_sperrt_vor_jeder_schreibung(self):
        anderes = {"SampleId": "wb_andere", "CaseId": NEUE_HALTUNG, "Code": "BCAAA",
                   "Signature": f"{NEUE_HALTUNG}|BCAAA|0.0|0.0"}
        self._write(self.samples_path, [self.sample, anderes])
        vorher = self._stand()

        code, _, err = self._main("--execute")

        self.assertEqual(1, code)
        self.assertIn("kollidiert", err)
        self.assertEqual(vorher, self._stand())

    def test_abweichende_datenbank_haltung_sperrt(self):
        connection = sqlite3.connect(self.database_path)
        connection.execute("UPDATE Samples SET CaseId = 'etwas_anderes'")
        connection.commit()
        connection.close()
        vorher = self._stand()

        code, _, err = self._main("--execute")

        self.assertEqual(1, code)
        self.assertIn("Datenbank-Haltung", err)
        self.assertEqual(vorher, self._stand())

    def test_abweichende_teacher_haltung_sperrt(self):
        self._write(self.teacher_path, [dict(self.teacher, haltungName="7777-8888")])
        vorher = self._stand()

        code, _, err = self._main("--execute")

        self.assertEqual(1, code)
        self.assertIn("Teacher-Haltung", err)
        self.assertEqual(vorher, self._stand())

    def test_teacher_verknuepfung_muss_eindeutig_sein(self):
        self._write(self.teacher_path, [self.teacher, dict(self.teacher, annotationId="t2")])

        with self.assertRaisesRegex(ValueError, "2 Verknuepfungen"):
            self._plan()

    def test_signatur_muss_zur_case_id_passen(self):
        self._write(self.samples_path, [dict(self.sample, Signature="fremd|BCAAA|0.0|0.0")])

        with self.assertRaisesRegex(ValueError, "Signatur"):
            self._plan()

    def test_laufendes_programm_sperrt_ausfuehrung_ohne_sicherungsordner(self):
        plan = self._plan()
        vorher = self._stand()

        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=True):
            with self.assertRaisesRegex(ValueError, "SewerStudio laeuft"):
                MODULE.execute_plan(plan)

        self.assertEqual(vorher, self._stand())
        self.assertFalse((self.root / "training").exists())

    def test_parallele_aenderung_zwischen_plan_und_ausfuehrung_sperrt(self):
        plan = self._plan()
        self._write(self.samples_path, [dict(self.sample, Notes="parallel")])
        veraendert = self.samples_path.read_bytes()

        with self.assertRaisesRegex(ValueError, "parallel veraendert"):
            self._ausfuehren(plan)

        self.assertEqual(veraendert, self.samples_path.read_bytes())

    def test_datenbank_aenderung_nach_dem_plan_rollt_alle_dateien_zurueck(self):
        plan = self._plan()
        connection = sqlite3.connect(self.database_path)
        connection.execute("UPDATE Samples SET CaseId = 'zwischendurch'")
        connection.commit()
        connection.close()
        samples_vorher = self.samples_path.read_bytes()
        teacher_vorher = self.teacher_path.read_bytes()

        with self.assertRaisesRegex(ValueError, "Datenbank wurde vor Reparatur"):
            self._ausfuehren(plan)

        self.assertEqual(samples_vorher, self.samples_path.read_bytes())
        self.assertEqual(teacher_vorher, self.teacher_path.read_bytes())
        self.assertEqual("zwischendurch", self._db_case())

    def test_schreibfehler_stellt_dateien_und_datenbank_wieder_her(self):
        plan = self._plan()
        samples_vorher = self.samples_path.read_bytes()
        teacher_vorher = self.teacher_path.read_bytes()
        echte_schreibung = MODULE._atomic_write
        aufrufe = []

        def zweiter_aufruf_scheitert(path, data):
            aufrufe.append(path.name)
            if len(aufrufe) == 2:
                raise OSError("Platte voll (Test)")
            echte_schreibung(path, data)

        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=False), \
                mock.patch.object(MODULE, "_atomic_write", side_effect=zweiter_aufruf_scheitert):
            with self.assertRaises(OSError):
                MODULE.execute_plan(plan)

        self.assertEqual(samples_vorher, self.samples_path.read_bytes())
        self.assertEqual(teacher_vorher, self.teacher_path.read_bytes())
        self.assertEqual(ALTE_CASE_ID, self._db_case())


if __name__ == "__main__":
    unittest.main()
