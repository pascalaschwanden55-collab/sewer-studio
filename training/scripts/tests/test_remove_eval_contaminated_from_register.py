"""Tests fuer remove_eval_contaminated_from_register.py (Audit T2, 02.10.2026).

Das Skript veraendert ``training_samples.json`` und ``export_registry_v1.json``.
Gehalten werden die Zusagen aus seinem Docstring: Standardlauf schreibfrei,
Sicherung vor dem Schreiben, Eval-Kontamination wird aus Register und
Trainingsberechtigung entfernt, Fehler lassen die Originale unveraendert.
Nur synthetische Daten im Temp-Ordner, nichts unter dem echten Wissensordner.
"""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "remove_eval_contaminated_from_register.py"
sys.path.insert(0, str(SCRIPT_PATH.parent))
SPEC = importlib.util.spec_from_file_location("remove_eval_contaminated_from_register", SCRIPT_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class RemoveEvalContaminatedTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / "brain"
        (self.root / "training").mkdir(parents=True)
        self.samples_path = self.root / "training_samples.json"
        self.registry_path = self.root / "training" / "export_registry_v1.json"
        self.samples = [
            {"SampleId": "wb_a", "CaseId": "1111-2222", "Code": "BCAAA",
             "TrainingEligible": True, "TrainingEligibilityReason": None},
            {"SampleId": "wb_b", "CaseId": "3333-4444", "Code": "BABAA",
             "TrainingEligible": True, "TrainingEligibilityReason": None},
            {"SampleId": "wb_c", "CaseId": "5555-6666", "Code": "BBAAA",
             "TrainingEligible": True, "TrainingEligibilityReason": None},
        ]
        self.registry = {"approved_sample_ids": ["wb_a", "wb_b", "wb_c"], "protected_sets": []}
        self._write(self.samples_path, self.samples)
        self._write(self.registry_path, self.registry)

    def tearDown(self):
        self.temp.cleanup()

    @staticmethod
    def _write(path, value):
        path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def _main(self, *extra):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = MODULE.main(["--knowledge-root", str(self.root), *extra])
        return code, out.getvalue(), err.getvalue()

    def _repairs_dir(self):
        return self.root / "training" / "repairs"

    def test_standardlauf_ist_schreibfrei(self):
        samples_before = self.samples_path.read_bytes()
        registry_before = self.registry_path.read_bytes()

        code, out, _ = self._main("--sample-ids", "wb_a", "wb_b")

        self.assertEqual(0, code)
        self.assertIn("PRUEFLAUF", out)
        self.assertIn("Keine Datei wurde veraendert", out)
        self.assertEqual(samples_before, self.samples_path.read_bytes())
        self.assertEqual(registry_before, self.registry_path.read_bytes())
        self.assertFalse(self._repairs_dir().exists())

    def test_ausfuehrung_entkontaminiert_sichert_vorher_und_laesst_andere_unberuehrt(self):
        samples_before = self.samples_path.read_bytes()
        registry_before = self.registry_path.read_bytes()

        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=False):
            code, _, err = self._main("--execute", "--sample-ids", "wb_a", "wb_b")

        self.assertEqual(0, code, err)
        samples = {s["SampleId"]: s for s in json.loads(self.samples_path.read_text(encoding="utf-8"))}
        registry = json.loads(self.registry_path.read_text(encoding="utf-8"))
        for sid in ("wb_a", "wb_b"):
            self.assertIs(False, samples[sid]["TrainingEligible"])
            self.assertEqual("eval-holdout-contamination-precaution",
                             samples[sid]["TrainingEligibilityReason"])
        # Nicht genannte Samples bleiben unveraendert.
        self.assertIs(True, samples["wb_c"]["TrainingEligible"])
        self.assertIsNone(samples["wb_c"]["TrainingEligibilityReason"])
        self.assertEqual(["wb_c"], registry["approved_sample_ids"])
        self.assertEqual([], registry["protected_sets"])

        backups = list(self._repairs_dir().iterdir())
        self.assertEqual(1, len(backups))
        backup = backups[0]
        self.assertEqual(samples_before, (backup / "training_samples.before.json").read_bytes())
        self.assertEqual(registry_before, (backup / "export_registry_v1.before.json").read_bytes())
        receipt = json.loads((backup / "receipt.json").read_text(encoding="utf-8"))
        self.assertEqual(2, receipt["samples_geaendert"])
        self.assertEqual(2, receipt["register_entfernt"])
        self.assertEqual(3, receipt["register_vorher"])
        self.assertEqual(1, receipt["register_nachher"])
        self.assertEqual(MODULE._sha256_bytes(samples_before),
                         receipt["hashes"]["training_samples_vorher"])
        self.assertEqual(MODULE._sha256_bytes(self.samples_path.read_bytes()),
                         receipt["hashes"]["training_samples_nachher"])

    def test_keine_temporaeren_dateien_nach_dem_schreiben(self):
        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=False):
            self._main("--execute", "--sample-ids", "wb_a")

        self.assertEqual([], [p.name for p in self.root.rglob("*.tmp")])

    def test_unbekanntes_sample_sperrt_vor_jeder_schreibung(self):
        samples_before = self.samples_path.read_bytes()
        registry_before = self.registry_path.read_bytes()

        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=False):
            code, _, err = self._main("--execute", "--sample-ids", "wb_a", "wb_gibtsnicht")

        self.assertEqual(1, code)
        self.assertIn("GESPERRT", err)
        self.assertEqual(samples_before, self.samples_path.read_bytes())
        self.assertEqual(registry_before, self.registry_path.read_bytes())
        self.assertFalse(self._repairs_dir().exists())

    def test_laufendes_programm_sperrt_vor_jeder_schreibung(self):
        samples_before = self.samples_path.read_bytes()
        registry_before = self.registry_path.read_bytes()

        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=True):
            code, _, err = self._main("--execute", "--sample-ids", "wb_a")

        self.assertEqual(1, code)
        self.assertIn("SewerStudio laeuft", err)
        self.assertEqual(samples_before, self.samples_path.read_bytes())
        self.assertEqual(registry_before, self.registry_path.read_bytes())
        self.assertFalse(self._repairs_dir().exists())

    def test_parallele_aenderung_zwischen_plan_und_ausfuehrung_sperrt(self):
        plan = MODULE.build_plan(self.root, ["wb_a"])
        changed = list(self.samples)
        changed[2] = dict(changed[2], Notes="parallel")
        self._write(self.samples_path, changed)
        changed_bytes = self.samples_path.read_bytes()

        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=False):
            with self.assertRaisesRegex(ValueError, "parallel veraendert"):
                MODULE.execute_plan(plan)

        self.assertEqual(changed_bytes, self.samples_path.read_bytes())
        self.assertFalse(self._repairs_dir().exists())

    def test_schreibfehler_stellt_beide_dateien_wieder_her(self):
        samples_before = self.samples_path.read_bytes()
        registry_before = self.registry_path.read_bytes()
        plan = MODULE.build_plan(self.root, ["wb_a"])
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

        self.assertEqual(samples_before, self.samples_path.read_bytes())
        self.assertEqual(registry_before, self.registry_path.read_bytes())

    def test_sample_ohne_registereintrag_wird_trotzdem_entkontaminiert(self):
        self.registry["approved_sample_ids"] = ["wb_b", "wb_c"]
        self._write(self.registry_path, self.registry)

        with mock.patch.object(MODULE, "_sewerstudio_running", return_value=False):
            code, _, err = self._main("--execute", "--sample-ids", "wb_a")

        self.assertEqual(0, code, err)
        samples = {s["SampleId"]: s for s in json.loads(self.samples_path.read_text(encoding="utf-8"))}
        self.assertIs(False, samples["wb_a"]["TrainingEligible"])
        registry = json.loads(self.registry_path.read_text(encoding="utf-8"))
        self.assertEqual(["wb_b", "wb_c"], registry["approved_sample_ids"])

    def test_standard_ids_sind_eine_feste_nichtleere_liste_ohne_doppelte(self):
        self.assertTrue(MODULE.DEFAULT_SAMPLE_IDS)
        self.assertEqual(len(MODULE.DEFAULT_SAMPLE_IDS), len(set(MODULE.DEFAULT_SAMPLE_IDS)))
        self.assertEqual("eval-holdout-contamination-precaution", MODULE.REASON)


if __name__ == "__main__":
    unittest.main()
