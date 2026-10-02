"""Tests fuer import_gold_labels.py (Audit T2, 02.10.2026).

Das Skript haengt Altbestand-Labels als persoenliche Goldsamples an
``training_samples.json``. Gehalten werden: Vorschau schreibfrei, Eval-Schutz
(Bild-Hash und Haltung in beiden Richtungen), Duplikatschutz, fail-closed
Pruefungen, Sicherung vor dem Schreiben, Quelle bleibt unveraendert.
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

from PIL import Image


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "import_gold_labels.py"
sys.path.insert(0, str(SCRIPT_PATH.parent))
SPEC = importlib.util.spec_from_file_location("import_gold_labels", SCRIPT_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

BREITE = 10
HOEHE = 10
VOLLE_MASKE = f"1,{BREITE * HOEHE}"


class ImportGoldLabelsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        base = Path(self.temp.name)
        self.root = base / "brain"
        self.alt = base / "alt" / "gold_labels"
        self.gold = self.root / "gold_frames" / "BCA - Anschluss"
        self.eval_images = self.root / "eval_set" / "images"
        for folder in (self.gold, self.eval_images, self.alt / "BCA"):
            folder.mkdir(parents=True)
        self.samples_path = self.root / "training_samples.json"
        self.bestand = [{"SampleId": "wb_vorhanden", "CaseId": "9999-8888", "FramePath": ""}]
        self._write(self.samples_path, self.bestand)
        self.zaehler = 0

    def tearDown(self):
        self.temp.cleanup()

    @staticmethod
    def _write(path, value):
        path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def _bild(self, ordner, name, farbe):
        pfad = ordner / name
        Image.new("RGB", (BREITE, HOEHE), farbe).save(pfad, format="JPEG")
        return pfad

    def _label(self, frame, haltung="1234-5678", **ueberschreiben):
        self.zaehler += 1
        label = {
            "frame": frame,
            "code": "BCAAA",
            "haltung": haltung,
            "box_norm": [0.5, 0.5, 1.0, 1.0],
            "mask_rle": VOLLE_MASKE,
            "protocol_time": 12.5,
        }
        label.update(ueberschreiben)
        pfad = self.alt / "BCA" / f"label_{self.zaehler}.json"
        self._write(pfad, label)
        return pfad

    def _build(self):
        return MODULE.build_samples(self.alt, self.root, "2026-10-02T00:00:00.0000000Z", None)

    def _main(self, *extra):
        out, err = io.StringIO(), io.StringIO()
        argv = ["import_gold_labels.py", "--knowledge-root", str(self.root),
                "--alt-root", str(self.alt), *extra]
        with mock.patch.object(sys, "argv", argv), \
                contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = MODULE.main()
        return code, out.getvalue(), err.getvalue()

    # --- Kernverhalten ---------------------------------------------------

    def test_gueltiges_label_wird_persoenliches_goldsample(self):
        bild = self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        self._label("frame_a.jpg")

        existing, created, skipped = self._build()

        self.assertEqual(1, len(existing))
        self.assertEqual({}, skipped)
        self.assertEqual(1, len(created))
        sample = created[0]
        self.assertEqual("1234-5678", sample["CaseId"])
        self.assertEqual("BCAAA", sample["Code"])
        self.assertEqual(str(bild), sample["FramePath"])
        self.assertEqual("ManualCoding", sample["SourceType"])
        self.assertEqual("ReviewApproved", sample["MatchLevel"])
        self.assertIs(True, sample["HumanConfirmed"])
        self.assertIs(True, sample["HasBbox"])
        self.assertIs(True, sample["HasSamMask"])
        self.assertEqual("Besitzer", sample["ConfirmedByUser"])
        self.assertEqual(BREITE * HOEHE, sample["SamMaskAreaPixels"])

    def test_vorschau_schreibt_nichts(self):
        self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        self._label("frame_a.jpg")
        vorher = self.samples_path.read_bytes()

        code, out, _ = self._main()

        self.assertEqual(0, code)
        self.assertIn("Neu verwendbar: 1", out)
        self.assertIn("Nur Vorschau", out)
        self.assertEqual(vorher, self.samples_path.read_bytes())
        self.assertEqual([], list(self.root.glob("*.bak*")))

    def test_apply_sichert_vorher_haengt_an_und_aendert_quelle_nicht(self):
        self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        label_pfad = self._label("frame_a.jpg")
        vorher = self.samples_path.read_bytes()
        label_vorher = label_pfad.read_bytes()

        with mock.patch.object(MODULE, "_sewerstudio_laeuft", return_value=False):
            code, _, err = self._main("--apply")

        self.assertEqual(0, code, err)
        gesichert = list(self.root.glob("training_samples.json.bak_vor_goldlabels_*"))
        self.assertEqual(1, len(gesichert))
        self.assertEqual(vorher, gesichert[0].read_bytes())
        neu = json.loads(self.samples_path.read_text(encoding="utf-8"))
        self.assertEqual(2, len(neu))
        self.assertEqual("wb_vorhanden", neu[0]["SampleId"])  # Bestand bleibt vorn und unveraendert
        self.assertEqual(self.bestand[0], neu[0])
        self.assertEqual("1234-5678", neu[1]["CaseId"])
        self.assertEqual(label_vorher, label_pfad.read_bytes())
        self.assertEqual([], [p.name for p in self.root.glob(".*.tmp")])

    def test_apply_bricht_ab_wenn_programm_laeuft(self):
        self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        self._label("frame_a.jpg")
        vorher = self.samples_path.read_bytes()

        with mock.patch.object(MODULE, "_sewerstudio_laeuft", return_value=True):
            code, _, err = self._main("--apply")

        self.assertEqual(2, code)
        self.assertIn("SewerStudio.exe laeuft", err)
        self.assertEqual(vorher, self.samples_path.read_bytes())
        self.assertEqual([], list(self.root.glob("*.bak*")))

    def test_limit_begrenzt_die_neuen_samples(self):
        for index in range(3):
            self._bild(self.gold, f"frame_{index}.jpg", (index * 40, 20, 20))
            self._label(f"frame_{index}.jpg")

        _, created, _ = MODULE.build_samples(self.alt, self.root, "t", 2)

        self.assertEqual(2, len(created))

    # --- Eval-Schutz ------------------------------------------------------

    def test_eval_haltung_wird_nicht_importiert(self):
        self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        self._bild(self.eval_images, "eval_1234-5678_p1.jpg", (10, 200, 10))
        self._label("frame_a.jpg", haltung="1234-5678")

        _, created, skipped = self._build()

        self.assertEqual([], created)
        self.assertEqual({"eval_geschuetzt": 1}, skipped)

    def test_eval_haltung_in_gegenrichtung_wird_nicht_importiert(self):
        self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        self._bild(self.eval_images, "eval_5678-1234_p1.jpg", (10, 200, 10))
        self._label("frame_a.jpg", haltung="1234-5678")

        _, created, skipped = self._build()

        self.assertEqual([], created)
        self.assertEqual({"eval_geschuetzt": 1}, skipped)

    def test_eval_haltung_aus_kandidatenliste_wird_nicht_importiert(self):
        self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        self._write(self.root / "eval_set" / "_candidates.json",
                    {"candidates": [{"haltung_key": "5678-1234"}]})
        self._label("frame_a.jpg", haltung="1234-5678")

        _, created, skipped = self._build()

        self.assertEqual([], created)
        self.assertEqual({"eval_geschuetzt": 1}, skipped)

    def test_bild_mit_eval_hash_wird_nicht_importiert_auch_bei_anderer_haltung(self):
        gold_bild = self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        (self.eval_images / "ohne_haltung.jpg").write_bytes(gold_bild.read_bytes())
        self._label("frame_a.jpg", haltung="1234-5678")

        _, created, skipped = self._build()

        self.assertEqual([], created)
        self.assertEqual({"eval_bild": 1}, skipped)

    def test_fremde_haltung_neben_eval_haltung_wird_importiert(self):
        self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        self._bild(self.eval_images, "eval_7777-6666_p1.jpg", (10, 200, 10))
        self._label("frame_a.jpg", haltung="1234-5678")

        _, created, skipped = self._build()

        self.assertEqual(1, len(created))
        self.assertEqual({}, skipped)

    # --- Duplikate und fail-closed-Pruefungen ----------------------------

    def test_bild_das_schon_im_bestand_ist_gilt_als_duplikat(self):
        bild = self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        self._write(self.samples_path, [{"SampleId": "wb_alt", "FramePath": str(bild)}])
        self._label("frame_a.jpg")

        _, created, skipped = self._build()

        self.assertEqual([], created)
        self.assertEqual({"duplikat": 1}, skipped)

    def test_zwei_labels_auf_gleichen_bildbytes_ergeben_nur_ein_sample(self):
        erstes = self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        (self.gold / "frame_b.jpg").write_bytes(erstes.read_bytes())
        self._label("frame_a.jpg")
        self._label("frame_b.jpg")

        _, created, skipped = self._build()

        self.assertEqual(1, len(created))
        self.assertEqual({"duplikat": 1}, skipped)

    def test_label_ordner_leer_wird_ignoriert(self):
        self._bild(self.gold, "frame_a.jpg", (200, 10, 10))
        (self.alt / "LEER").mkdir()
        self._write(self.alt / "LEER" / "x.json", {"frame": "frame_a.jpg"})

        _, created, skipped = self._build()

        self.assertEqual([], created)
        self.assertEqual({}, skipped)

    def test_fehlerfaelle_werden_gezaehlt_und_nicht_importiert(self):
        self._bild(self.gold, "ok.jpg", (200, 10, 10))
        self._bild(self.gold, "b1.jpg", (10, 10, 200))
        self._bild(self.gold, "b2.jpg", (10, 100, 100))
        self._bild(self.gold, "b3.jpg", (100, 100, 10))
        self._bild(self.gold, "b4.jpg", (50, 50, 50))
        self._bild(self.gold, "b5.jpg", (150, 150, 150))
        self._bild(self.gold, "b6.jpg", (90, 30, 150))
        (self.alt / "BCA" / "kaputt.json").write_text("{nicht json", encoding="utf-8")
        self._label("ok.jpg")
        self._label("fehlt.jpg")                                          # bild_nicht_in_gold_frames
        self._label("b1.jpg", code="ZZZAA")                               # code_nicht_im_goldkatalog
        self._label("b2.jpg", haltung="Freitext ohne Schachtpaar")        # haltung_ungueltig
        self._label("b3.jpg", box_norm=[0.9, 0.9, 0.5, 0.5])              # box_ausserhalb
        self._label("b4.jpg", mask_rle="1,99")                            # maske_format
        self._label("b5.jpg", box_norm=[0.25, 0.25, 0.2, 0.2])            # maske_ausserhalb_box
        self._label("b6.jpg", mask_rle="")                                # label_unvollstaendig

        _, created, skipped = self._build()

        self.assertEqual(["ok.jpg"], [Path(s["FramePath"]).name for s in created])
        self.assertEqual({
            "json_unlesbar": 1,
            "bild_nicht_in_gold_frames": 1,
            "code_nicht_im_goldkatalog": 1,
            "haltung_ungueltig": 1,
            "box_ausserhalb": 1,
            "maske_format": 1,
            "maske_ausserhalb_box": 1,
            "label_unvollstaendig": 1,
        }, skipped)

    def test_fehlende_trainingsdatei_ist_ein_harter_fehler(self):
        self.samples_path.unlink()

        with self.assertRaises(FileNotFoundError):
            self._build()
        self.assertFalse(self.samples_path.exists())

    # --- Bausteine -------------------------------------------------------

    def test_atomar_schreiben_ersetzt_vollstaendig_und_laesst_nichts_zurueck(self):
        ziel = self.root / "x.json"
        ziel.write_bytes(b"alt")

        MODULE._atomar_schreiben(ziel, b"neu")

        self.assertEqual(b"neu", ziel.read_bytes())
        self.assertEqual([], [p.name for p in self.root.glob(".x.json.*")])

    def test_rle_pruefung_verlangt_summe_gleich_breite_mal_hoehe(self):
        self.assertIsNotNone(MODULE._parse_rle("1,100", 10, 10))
        self.assertIsNone(MODULE._parse_rle("1,99", 10, 10))
        self.assertIsNone(MODULE._parse_rle("2,100", 10, 10))
        self.assertIsNone(MODULE._parse_rle("0,100", 10, 10))  # keine Maskenpixel
        self.assertIsNone(MODULE._parse_rle("1,x", 10, 10))


if __name__ == "__main__":
    unittest.main()
