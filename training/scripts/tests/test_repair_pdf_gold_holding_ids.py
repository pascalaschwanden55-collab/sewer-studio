"""Fokussierte Tests fuer repair_pdf_gold_holding_ids.py.

Ein Fall je Gruppe, darunter zwingend einer, der nach der (verhinderten)
Reparatur auf eine geschuetzte Haltung faellt (Vorflug -> Dekontamination).
Der Bildbeweis wird injiziert, es laufen keine echten PDF-Proben.
"""
from __future__ import annotations

import importlib.util
import json
import sqlite3
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

SCRIPTS_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS_DIR))

MODULE_PATH = SCRIPTS_DIR / "repair_pdf_gold_holding_ids.py"
SPEC = importlib.util.spec_from_file_location("repair_pdf_gold_holding_ids", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def make_sample(sample_id, case_id, code, frame_name, pdf_name, eligible=True):
    return {
        "SampleId": sample_id,
        "CaseId": case_id,
        "Code": code,
        "Status": 1,
        "HumanConfirmed": True,
        "ConfirmedByUser": "Besitzer",
        "HasBbox": True,
        "HasSamMask": True,
        "Signature": f"{case_id}|{code}|0.0|0.0",
        "FramePath": frame_name,
        "Notes": f"PDF-Operateurreferenz: {pdf_name}; SHA-256={'a' * 64}; Seite=1",
        "TrainingEligible": eligible,
        "TrainingEligibilityReason": None,
    }


class RepairPdfGoldHoldingIdsTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        (self.root / "gold_frames").mkdir()
        (self.root / "training").mkdir()
        self.samples_path = self.root / "training_samples.json"
        self.teacher_path = self.root / "teacher_annotations.json"
        self.registry_path = self.root / "training" / "export_registry_v1.json"
        self.db_path = self.root / "KnowledgeBase.db"

    def tearDown(self):
        self.tmp.cleanup()

    def _write_world(self, samples, registry_ids):
        for sample in samples:
            frame = self.root / "gold_frames" / Path(sample["FramePath"]).name
            frame.write_bytes(b"\xff\xd8" + sample["SampleId"].encode() + b"\xff\xd9")
            sample["FramePath"] = str(frame)
        teachers = [
            {"sourceSampleId": s["SampleId"], "haltungName": s["CaseId"], "code": s["Code"]}
            for s in samples
        ]
        self.samples_path.write_text(json.dumps(samples), encoding="utf-8")
        self.teacher_path.write_text(json.dumps(teachers), encoding="utf-8")
        self.registry_path.write_text(json.dumps({
            "schema_version": 1,
            "approved_sample_ids": registry_ids,
        }), encoding="utf-8")
        if self.db_path.exists():
            self.db_path.unlink()
        connection = sqlite3.connect(str(self.db_path))
        try:
            connection.execute("CREATE TABLE Samples (SampleId TEXT PRIMARY KEY, CaseId TEXT)")
            for s in samples:
                connection.execute(
                    "INSERT INTO Samples (SampleId, CaseId) VALUES (?, ?)",
                    (s["SampleId"], s["CaseId"]))
            connection.commit()
        finally:
            connection.close()

    def _classify_all(self, world):
        """world: liste von (sample, resolution, prover_antworten, protection)."""
        classified = []
        for sample, resolution, prover_answers, protection in world:
            def prover(_frame, _pdf, modus, answers=prover_answers):
                return answers.get(modus, False)
            classified.append(MODULE.classify_sample(
                sample_id=sample["SampleId"],
                case_id=sample["CaseId"],
                pdf_name=sample["Notes"].split("PDF-Operateurreferenz: ")[1].split(";")[0],
                frame_path=Path(sample["FramePath"]),
                resolution=resolution,
                protection_keys=protection,
                image_prover=prover,
            ))
        return classified

    def _run_execute(self, samples, registry_ids, classified):
        self._write_world(samples, registry_ids)
        plan = MODULE.build_execute_plan(self.root, classified)
        backup = MODULE.execute_plan(plan, datetime.now(timezone.utc))
        new_samples = {s["SampleId"]: s for s in json.loads(self.samples_path.read_text(encoding="utf-8"))}
        new_registry = json.loads(self.registry_path.read_text(encoding="utf-8"))
        connection = sqlite3.connect(str(self.db_path))
        try:
            db_rows = dict(connection.execute("SELECT SampleId, CaseId FROM Samples").fetchall())
        finally:
            connection.close()
        new_teachers = {t["sourceSampleId"]: t for t in json.loads(self.teacher_path.read_text(encoding="utf-8"))}
        return plan, backup, new_samples, new_teachers, new_registry, db_rows

    def test_gruppe_1_bytebeweis_wird_repariert(self):
        s = make_sample("wb_g1", "999001-90327", "BABAA", "g1.jpg", "20231123_06.887943-90327.pdf")
        self._write_world([s], ["wb_g1"])
        resolution = MODULE.SourceResolution(Path("quelle.pdf"), "06.887943-90327", None)
        classified = self._classify_all([(s, resolution, {"roh": True}, {})])
        self.assertEqual(classified[0]["gruppe"], "gruppe_1_mit_bildbeleg")
        plan, backup, new_samples, teachers, registry, db = self._run_execute(
            [s], ["wb_g1"], classified)
        self.assertEqual(len(plan.repairs), 1)
        self.assertEqual(new_samples["wb_g1"]["CaseId"], "06.887943-90327")
        self.assertEqual(new_samples["wb_g1"]["Signature"], "06.887943-90327|BABAA|0.0|0.0")
        self.assertIn("PDF-Bildbeleg", new_samples["wb_g1"]["Notes"])
        self.assertEqual(teachers["wb_g1"]["haltungName"], "06.887943-90327")
        self.assertEqual(db["wb_g1"], "06.887943-90327")
        self.assertIn("wb_g1", registry["approved_sample_ids"])
        self.assertTrue((backup / "repair_result.json").exists())

    def test_gruppe_4_normalisiererbeweis_wird_repariert(self):
        s = make_sample("wb_g4", "60602-58932", "BAFC", "g4.png", "20220101_60604-60603.pdf")
        self._write_world([s], ["wb_g4"])
        resolution = MODULE.SourceResolution(Path("quelle.pdf"), "60604-60603", None)
        classified = self._classify_all([(s, resolution, {"normalisiert": True}, {})])
        self.assertEqual(classified[0]["gruppe"], "gruppe_4_normalisierer")
        plan, _backup, new_samples, _teachers, _registry, db = self._run_execute(
            [s], ["wb_g4"], classified)
        self.assertEqual(len(plan.repairs), 1)
        self.assertEqual(new_samples["wb_g4"]["CaseId"], "60604-60603")
        self.assertEqual(db["wb_g4"], "60604-60603")

    def test_geschuetzte_zielhaltung_wird_dekontaminiert_statt_repariert(self):
        s = make_sample("wb_prot", "9117-10300", "BAAA", "p1.jpg", "20220101_07.148371-10300.pdf")
        resolution = MODULE.SourceResolution(Path("quelle.pdf"), "07.148371-10300", None)
        protection = {MODULE.comparison_key("07.148371-10300"): {"eval_set:holdout"}}
        classified = self._classify_all([(s, resolution, {"roh": True}, protection)])
        self.assertEqual(classified[0]["gruppe"], "preflight_dekontamination")
        plan, _backup, new_samples, _teachers, registry, db = self._run_execute(
            [s], ["wb_prot"], classified)
        self.assertEqual(len(plan.repairs), 0)
        self.assertEqual(len(plan.decontaminations), 1)
        # Keine Reparatur: CaseId bleibt, aber das Sample verlaesst den Trainingsweg.
        self.assertEqual(new_samples["wb_prot"]["CaseId"], "9117-10300")
        self.assertIs(new_samples["wb_prot"]["TrainingEligible"], False)
        self.assertEqual(
            new_samples["wb_prot"]["TrainingEligibilityReason"],
            "eval-holdout-contamination-precaution")
        self.assertNotIn("wb_prot", registry["approved_sample_ids"])
        self.assertEqual(db["wb_prot"], "9117-10300")

    def test_quarantaene_bleibt_unberuehrt(self):
        s = make_sample("wb_q", "06.8360-2835", "BABAA", "q1.jpg", "20220101_06.8360-2835.pdf")
        resolution = MODULE.SourceResolution(Path("quelle.pdf"), "06.691078-691070", None)
        classified = self._classify_all([(s, resolution, {"roh": True}, {})])
        self.assertEqual(classified[0]["gruppe"], "gruppe_3_quarantaene")
        plan, _backup, new_samples, _teachers, _registry, db = self._run_execute(
            [s], ["wb_q"], classified)
        self.assertEqual(len(plan.repairs), 0)
        self.assertEqual(len(plan.decontaminations), 0)
        self.assertEqual(new_samples["wb_q"]["CaseId"], "06.8360-2835")
        self.assertEqual(db["wb_q"], "06.8360-2835")

    def test_bereits_korrekt_bleibt_unberuehrt(self):
        s = make_sample("wb_ok", "9906-9906", "BCCAB", "ok.jpg", "20251110_9906-9906.pdf")
        resolution = MODULE.SourceResolution(Path("quelle.pdf"), "9906-9906", None)
        classified = self._classify_all([(s, resolution, {}, {})])
        self.assertEqual(classified[0]["gruppe"], "bereits_korrekt")
        plan, _backup, new_samples, _t, _r, db = self._run_execute([s], ["wb_ok"], classified)
        self.assertEqual(len(plan.repairs), 0)
        self.assertEqual(new_samples["wb_ok"]["CaseId"], "9906-9906")
        self.assertEqual(db["wb_ok"], "9906-9906")

    def test_signaturkollision_sperrt_den_lauf(self):
        s1 = make_sample("wb_a", "999001-90327", "BABAA", "a.jpg", "20231123_06.887943-90327.pdf")
        s2 = make_sample("wb_b", "06.887943-90327", "BABAA", "b.jpg", "20200101_x.pdf")
        # s1 wuerde auf die Signatur von s2 wechseln -> harte Sperre.
        resolution = MODULE.SourceResolution(Path("quelle.pdf"), "06.887943-90327", None)
        self._write_world([s1, s2], ["wb_a", "wb_b"])
        classified = self._classify_all([(s1, resolution, {"roh": True}, {})])
        with self.assertRaises(ValueError):
            MODULE.build_execute_plan(self.root, classified)


if __name__ == "__main__":
    unittest.main()
