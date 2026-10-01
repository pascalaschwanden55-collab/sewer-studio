from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from tools.EvalVisibilityReview.bcc_copilot_review_server import (
    URTEILE,
    CopilotReviewStore,
)


class CopilotReviewStoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary = tempfile.TemporaryDirectory()
        self.root = Path(self._temporary.name)
        self.lauf = self.root / "lauf"
        self.output = self.root / "reviews" / "durchgang.json"
        self._durchgang_schreiben()

    def tearDown(self) -> None:
        self._temporary.cleanup()

    def _durchgang_schreiben(self, vorschlaege: list[dict] | None = None) -> None:
        (self.lauf / "clips").mkdir(parents=True, exist_ok=True)
        vorschlaege = vorschlaege or [
            {
                "nummer": 1, "clip": "vorschlag_001.mp4", "stufe": "stark",
                "max_conf": 0.86, "peak_zeit": 214.0,
                "meter_min": 9.42, "meter_max": 9.42,
            },
            {
                "nummer": 2, "clip": "vorschlag_002.mp4", "stufe": "schwach",
                "max_conf": 0.55, "peak_zeit": 118.0,
                "meter_min": None, "meter_max": None,
            },
        ]
        for vorschlag in vorschlaege:
            (self.lauf / "clips" / vorschlag["clip"]).write_bytes(b"clip")
        (self.lauf / "vorschlaege.json").write_text(
            json.dumps({
                "schema_version": 1,
                "haltung": "36051-33461",
                "video": r"D:\Videos\H_33461-36051.mpg",
                "kandidat": "bcc_nc15_seed46_20260808",
                "gewicht_sha256": "a" * 64,
                "min_confidence": 0.5,
                "strong_confidence": 0.8,
                "vorschlaege": vorschlaege,
            }, ensure_ascii=False),
            encoding="utf-8",
        )

    def _store(self) -> CopilotReviewStore:
        return CopilotReviewStore(self.lauf, self.output, "Pascal")

    def test_der_erste_offene_vorschlag_wird_gezeigt(self) -> None:
        stand = self._store().stand()

        self.assertEqual(2, stand["gesamt"])
        self.assertEqual(2, stand["offen"])
        self.assertEqual(1, stand["naechster"]["nummer"])

    def test_ein_gelesener_meterstand_wird_als_meter_gezeigt(self) -> None:
        self.assertEqual("Meter 9.42", self._store().stand()["naechster"]["ort"])

    def test_ohne_meterstand_wird_die_videozeit_gezeigt_statt_einer_null(self) -> None:
        # Eine erfundene 0,0 saehe aus wie eine Messung.
        store = self._store()
        store.entscheiden("1", "bestaetigt", "", "")

        ort = store.stand()["naechster"]["ort"]
        self.assertIn("Sekunde 118", ort)
        self.assertIn("nicht lesbar", ort)

    def test_bestaetigen_haelt_den_vorgeschlagenen_code_fest(self) -> None:
        store = self._store()

        stand = store.entscheiden("1", "bestaetigt", "", "")

        self.assertEqual(1, stand["zaehlung"]["bestaetigt"])
        gespeichert = json.loads(self.output.read_text(encoding="utf-8"))["entscheidungen"]["1"]
        self.assertEqual("BCC", gespeichert["richtiger_code"])
        self.assertEqual(9.42, gespeichert["meter_min"])

    def test_eine_korrektur_haelt_den_richtigen_code_fest(self) -> None:
        # Pascals Fall bei Meter 3,0: das Modell meldete Bogen, richtig war BAJC.
        store = self._store()

        store.entscheiden("1", "korrigiert", " bajc ", "Rohrverbindung mit Knick")

        gespeichert = json.loads(self.output.read_text(encoding="utf-8"))["entscheidungen"]["1"]
        self.assertEqual("BCC", gespeichert["vorgeschlagener_code"])
        self.assertEqual("BAJC", gespeichert["richtiger_code"])
        self.assertEqual("Rohrverbindung mit Knick", gespeichert["kommentar"])

    def test_eine_korrektur_ohne_code_wird_abgewiesen(self) -> None:
        with self.assertRaises(ValueError):
            self._store().entscheiden("1", "korrigiert", "   ", "")
        self.assertFalse(self.output.exists())

    def test_die_herkunft_wird_immer_mitgeschrieben(self) -> None:
        # Beim Entscheiden war ein Modellvorschlag sichtbar. Ohne diese Angabe
        # liesse sich spaeter nicht trennen, was der Mensch selbst gefunden hat.
        self._store().entscheiden("1", "verworfen", "", "")

        gespeichert = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertTrue(gespeichert["vorschlag_sichtbar"])
        self.assertEqual("bcc_nc15_seed46_20260808", gespeichert["kandidat"])
        self.assertEqual("a" * 64, gespeichert["gewicht_sha256"])

    def test_ein_neustart_setzt_die_pruefung_fort(self) -> None:
        self._store().entscheiden("1", "bestaetigt", "", "")

        stand = self._store().stand()

        self.assertEqual(1, stand["offen"])
        self.assertEqual(2, stand["naechster"]["nummer"])

    def test_die_letzte_entscheidung_laesst_sich_zuruecknehmen(self) -> None:
        store = self._store()
        store.entscheiden("1", "bestaetigt", "", "")

        stand = store.zuruecknehmen()

        self.assertEqual(2, stand["offen"])
        self.assertEqual(0, stand["zaehlung"]["bestaetigt"])

    def test_eine_pruefung_eines_anderen_durchgangs_wird_nicht_fortgesetzt(self) -> None:
        self._store().entscheiden("1", "bestaetigt", "", "")
        self._durchgang_schreiben([
            {"nummer": 1, "clip": "vorschlag_001.mp4", "stufe": "stark",
             "max_conf": 0.9, "peak_zeit": 5.0, "meter_min": 1.0, "meter_max": 1.0},
        ])

        with self.assertRaises(SystemExit):
            self._store()

    def test_unbekannte_urteile_und_vorschlaege_werden_abgewiesen(self) -> None:
        store = self._store()
        with self.assertRaises(ValueError):
            store.entscheiden("1", "vielleicht", "", "")
        with self.assertRaises(ValueError):
            store.entscheiden("99", "bestaetigt", "", "")

    def test_der_clip_pfad_bleibt_im_clip_ordner(self) -> None:
        store = self._store()
        self.assertIsNotNone(store.clip_pfad("1"))
        self.assertIsNone(store.clip_pfad("99"))

        store.vorschlaege[0]["clip"] = "../../ausserhalb.mp4"
        self.assertIsNone(store.clip_pfad("1"))

    def test_erlaubte_urteile_sind_genau_drei(self) -> None:
        self.assertEqual(("bestaetigt", "korrigiert", "verworfen"), URTEILE)


if __name__ == "__main__":
    unittest.main()
