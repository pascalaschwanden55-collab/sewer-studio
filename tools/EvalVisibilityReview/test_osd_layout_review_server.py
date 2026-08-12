from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from tools.EvalVisibilityReview.osd_layout_review_server import OsdLayoutReviewStore


class OsdLayoutReviewStoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "bilder").mkdir()
        bild = self.root / "bilder" / "bild.jpg"
        bild.write_bytes(b"bild")
        (self.root / "queue.json").write_text(json.dumps({"faelle": [{
            "fall_id": "f1", "haltung": "H1", "bild": "bild.jpg",
            "bild_sha256": hashlib.sha256(b"bild").hexdigest(),
        }]}), encoding="utf-8")
        self.output = self.root / "review.json"

    def tearDown(self) -> None:
        self.temp.cleanup()

    def store(self) -> OsdLayoutReviewStore:
        return OsdLayoutReviewStore(self.root, self.output, "Pascal")

    def test_klickpunkt_und_drei_stilmerkmale_werden_atomar_gespeichert(self) -> None:
        stand = self.store().entscheiden(
            "f1", True, 0.8, 0.9, "hell_auf_dunkel", "gelb", "praefix_oder_nullen")
        self.assertEqual(0, stand["offen"])
        daten = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual(0.8, daten["entscheidungen"]["f1"]["x"])
        self.assertEqual("gelb", daten["entscheidungen"]["f1"]["farbe"])
        self.assertFalse(self.output.with_suffix(".json.tmp").exists())

    def test_sichtbarer_meter_braucht_klick_und_alle_merkmale(self) -> None:
        store = self.store()
        with self.assertRaises(ValueError):
            store.entscheiden("f1", True, None, None, "", "", "")
        with self.assertRaises(ValueError):
            store.entscheiden("f1", True, 2, 0.5, "hell_auf_dunkel", "gelb", "andere")

    def test_kein_meter_braucht_keine_erfundene_position(self) -> None:
        self.store().entscheiden("f1", False)
        daten = json.loads(self.output.read_text(encoding="utf-8"))["entscheidungen"]["f1"]
        self.assertIsNone(daten["x"])
        self.assertEqual("nicht_anwendbar", daten["format"])

    def test_veraendertes_bild_sperrt_die_review(self) -> None:
        (self.root / "bilder" / "bild.jpg").write_bytes(b"anders")
        with self.assertRaises(SystemExit):
            self.store()

    def test_fremder_fall_in_vorhandener_review_wird_abgewiesen(self) -> None:
        queue_hash = hashlib.sha256((self.root / "queue.json").read_bytes()).hexdigest()
        self.output.write_text(json.dumps({
            "queue_sha256": queue_hash,
            "entscheidungen": {"fremd": {"meter_sichtbar": False}},
        }), encoding="utf-8")
        with self.assertRaises(SystemExit):
            self.store()


if __name__ == "__main__":
    unittest.main()
