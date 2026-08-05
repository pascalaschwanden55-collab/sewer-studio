"""Fokussierte Tests fuer proto_hard_negative_review.py.

Deckt ab: Codefilter, Quoten, Ein-Bild-je-Haltung (inkl. Gegenrichtung),
Schutzfilter, Blindheit der Pruefansicht, Publish-Sperre fuer
mapped_object_visible, Split-Regel und die Store-Vertragspruefung.
"""
from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

SCRIPTS_DIR = Path(__file__).resolve().parents[1]
REPO_ROOT = SCRIPTS_DIR.parents[1]
sys.path.insert(0, str(SCRIPTS_DIR))

MODULE_PATH = SCRIPTS_DIR / "proto_hard_negative_review.py"
SPEC = importlib.util.spec_from_file_location("proto_hard_negative_review", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

SERVER_PATH = REPO_ROOT / "tools" / "EvalVisibilityReview" / "bcc_release_holdout_review_server.py"
SPEC_SERVER = importlib.util.spec_from_file_location("bcc_release_holdout_review_server", SERVER_PATH)
assert SPEC_SERVER is not None and SPEC_SERVER.loader is not None
SERVER = importlib.util.module_from_spec(SPEC_SERVER)
sys.modules[SPEC_SERVER.name] = SERVER
SPEC_SERVER.loader.exec_module(SERVER)

CLASS_MAP = REPO_ROOT / "training" / "class_maps" / "detect_class_map_v3.json"
VSA_MANIFEST = REPO_ROOT / "src" / "AuswertungPro.Next.UI" / "Data" / "vsa_kek_2020_catalog_manifest.json"


def _photo(path: Path, seed: int) -> None:
    img = Image.new("RGB", (96, 96))
    img.putdata([
        ((seed * (i % 17 + 3)) % 255, (seed * (i % 29 + 5)) % 255, (seed * (i % 41 + 7)) % 255)
        for i in range(96 * 96)
    ])
    img.save(path, "JPEG", quality=92)


def _befund(code: str, foto: str, von: str, bis: str = "") -> dict:
    return {
        "quelle": "xtf",
        "haltung_von": von,
        "haltung_bis": bis,
        "code": code,
        "meter": "1.0",
        "uhrlage": "12-12",
        "videozaehler": "",
        "datei_name": foto,
        "quell_datei": "quelle.xtf",
    }


class ProtoHardNegativeTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.images = self.root / "bilder"
        self.images.mkdir()
        self.knowledge = self.root / "wissen"
        (self.knowledge / "training").mkdir(parents=True)
        # Mindestens ein geschuetzter Bestand, sonst fail-closed.
        (self.knowledge / "eval_set").mkdir()
        self.class_map = MODULE.load_class_map(CLASS_MAP, VSA_MANIFEST)

    def tearDown(self):
        self.tmp.cleanup()

    def _add_photo(self, name: str, seed: int) -> Path:
        path = self.images / name
        _photo(path, seed)
        return path

    def _select(self, befunde):
        index = {p.name.casefold(): [p] for p in self.images.iterdir()}
        return MODULE.select_candidates(befunde, index, set(), {})

    def test_codefilter_detect_wird_uebersprungen(self):
        self._add_photo("d1.jpg", 1)
        self._add_photo("n1.jpg", 2)
        selected, stats = self._select([
            _befund("BAHCA", "d1.jpg", "100-200"),
            _befund("BCDYA", "n1.jpg", "300-400"),
        ])
        self.assertEqual(len(selected), 1)
        self.assertEqual(selected[0]["code"], "BCDYA")
        self.assertEqual(stats.get("detect_code_uebersprungen"), 1)

    def test_quote_begrenzt_je_gruppe(self):
        for i in range(6):
            self._add_photo(f"bcd{i}.jpg", 10 + i)
        befunde = [_befund("BCDYA", f"bcd{i}.jpg", f"{1000 + i}-{2000 + i}") for i in range(6)]
        selected, stats = self._select_quota(befunde, {"rohranfang_ende": 3, "wasser_betrieb": 0, "bauteil_sonstige": 0})
        self.assertEqual(len(selected), 3)
        self.assertEqual(stats["gruppe_rohranfang_ende"], 3)
        self.assertEqual(stats.get("quote_voll"), 3)

    def _select_quota(self, befunde, quotas):
        index = {p.name.casefold(): [p] for p in self.images.iterdir()}
        return MODULE.select_candidates(befunde, index, set(), {}, quotas=quotas)

    def test_ein_bild_je_haltung_auch_gegenrichtung(self):
        self._add_photo("a.jpg", 20)
        self._add_photo("b.jpg", 21)
        selected, _stats = self._select([
            _befund("BCDYA", "a.jpg", "100-200"),
            _befund("BCEYB", "b.jpg", "200-100"),  # Gegenrichtung = gleiche Haltung
        ])
        self.assertEqual(len(selected), 1)

    def test_schutzfilter_sperrt_geschuetzte_haltung(self):
        self._add_photo("p.jpg", 30)
        index = {p.name.casefold(): [p] for p in self.images.iterdir()}
        schutz = {MODULE.comparison_key("300-400"): {"eval_set:test"}}
        selected, stats = MODULE.select_candidates(
            [_befund("BCDYA", "p.jpg", "400-300")], index, set(), schutz)
        self.assertEqual(len(selected), 0)
        self.assertEqual(stats.get("geschuetzt"), 1)

    def test_byte_schutz_sperrt_eval_bytes_bei_unbekannter_haltung(self):
        # Genau der durchgerutschte Fall: Bytes liegen im Eval-Bestand,
        # die Haltung ist nicht als A-B-Schluessel lesbar.
        eval_dir = self.knowledge / "eval_set" / "subsets" / "holdout_x"
        eval_dir.mkdir(parents=True)
        eval_bild = self.images / "eval.jpg"
        _photo(eval_bild, 77)
        import shutil as _shutil
        _shutil.copy2(eval_bild, eval_dir / "eval.jpg")
        protected, counts = MODULE.load_protected_image_hashes(self.knowledge)
        self.assertEqual(counts["eval_set"], 1)
        index = {p.name.casefold(): [p] for p in self.images.iterdir()}
        selected, stats = MODULE.select_candidates(
            [_befund("BCDYA", "eval.jpg", "unbekannt")], index, set(), {}, protected)
        self.assertEqual(len(selected), 0)
        self.assertEqual(stats.get("byte_geschuetzt"), 1)

    def test_leere_protected_sets_sperren_den_plan_fail_closed(self):
        leeres_wissen = self.root / "leer"
        (leeres_wissen / "training").mkdir(parents=True)
        self._add_photo("x.jpg", 88)
        selected, _ = self._select([_befund("BCDYA", "x.jpg", "111-222")])
        with self.assertRaises(ValueError):
            MODULE.build_queue_plan(leeres_wissen, selected, self.class_map)

    def _publish_test_queue(self):
        self._add_photo("q1.jpg", 40)
        self._add_photo("q2.jpg", 41)
        selected, _ = self._select([
            _befund("BCDYA", "q1.jpg", "500-600"),
            _befund("AEDXA", "q2.jpg", "700-800"),
        ])
        plan = MODULE.build_queue_plan(self.knowledge, selected, self.class_map)
        return MODULE.publish_queue(plan)

    def test_queue_publiziert_blind_und_store_konform(self):
        queue_root = self._publish_test_queue()
        candidates = json.loads((queue_root / "_candidates.json").read_text(encoding="utf-8"))
        self.assertEqual(len(candidates), 2)
        for eintrag in candidates:
            self.assertEqual(
                set(eintrag),
                {"id", "frame_path", "category", "status", "source_sha256"},
                "Die Pruefansicht darf weder Code noch Herkunft enthalten.",
            )
        # Store-Vertrag (echte Validierung aus dem Pruefplatz).
        queue_id, _manifest_sha, _cand_sha, _images = SERVER._validate_hard_negative_queue(queue_root)
        manifest = json.loads((queue_root / "_manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["purpose"], "proto_hard_negative_review_queue")
        self.assertEqual(queue_id, manifest["queue_id"])
        self.assertIs(manifest["selection_rule"]["model_involved"], False)

    def _write_bound_review(self, queue_root: Path, decisions: dict, name: str = "review.json") -> Path:
        queue_manifest = json.loads((queue_root / "_manifest.json").read_text(encoding="utf-8"))
        import hashlib as _hashlib
        review_path = self.root / name
        review_path.write_text(json.dumps({
            "schema_version": "1.0",
            "purpose": "bcc_hard_negative_review",
            "queue_id": queue_manifest["queue_id"],
            "queue_manifest_sha256": _hashlib.sha256(
                (queue_root / "_manifest.json").read_bytes()).hexdigest(),
            "candidates_sha256": _hashlib.sha256(
                (queue_root / "_candidates.json").read_bytes()).hexdigest(),
            "class_map_sha256": queue_manifest["class_map_sha256"],
            "reviewer": "test",
            "updated_at_utc": "2026-08-05T00:00:00Z",
            "decisions": decisions,
        }), encoding="utf-8")
        return review_path

    def test_publish_set_nur_all_classes_clear(self):
        queue_root = self._publish_test_queue()
        candidates = json.loads((queue_root / "_candidates.json").read_text(encoding="utf-8"))
        decisions = {
            candidates[0]["id"]: {"decision": "all_classes_clear"},
            candidates[1]["id"]: {"decision": "mapped_object_visible"},
        }
        review_path = self._write_bound_review(queue_root, decisions)
        plan = MODULE.build_set_plan(self.knowledge, queue_root, review_path, CLASS_MAP)
        self.assertEqual(len(plan["items"]), 1)
        self.assertEqual(plan["items"][0]["item_id"], candidates[0]["id"])
        set_root = MODULE.publish_set(plan)
        manifest = json.loads((set_root / "_manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["purpose"], "proto_reviewed_negative_set")
        for image in manifest["semantic"]["images"]:
            self.assertEqual(image["review_decision"], "all_classes_clear")

    def test_publish_set_sperrt_unvollstaendige_review(self):
        queue_root = self._publish_test_queue()
        review_path = self._write_bound_review(queue_root, {})
        with self.assertRaises(ValueError):
            MODULE.build_set_plan(self.knowledge, queue_root, review_path, CLASS_MAP)

    def test_split_regel_80_20_und_eine_haltung_pro_bild(self):
        photos = [self._add_photo(f"s{i}.jpg", 60 + i) for i in range(10)]
        selected, _ = self._select([
            _befund("BCDYA", p.name, f"{1000 + i}-{2000 + i}") for i, p in enumerate(photos)
        ])
        plan = MODULE.build_queue_plan(self.knowledge, selected, self.class_map)
        queue_root = MODULE.publish_queue(plan)
        candidates = json.loads((queue_root / "_candidates.json").read_text(encoding="utf-8"))
        review_path = self._write_bound_review(
            queue_root, {c["id"]: {"decision": "all_classes_clear"} for c in candidates})
        set_plan = MODULE.build_set_plan(self.knowledge, queue_root, review_path, CLASS_MAP)
        splits = [item["split"] for item in set_plan["items"]]
        self.assertEqual(splits.count("validation"), 2)
        self.assertEqual(splits.count("train"), 8)
        haltungen = [item["holding_key"] for item in set_plan["items"]]
        self.assertEqual(len(set(haltungen)), len(haltungen))


if __name__ == "__main__":
    unittest.main()
