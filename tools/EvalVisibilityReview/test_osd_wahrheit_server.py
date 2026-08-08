from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from tools.EvalVisibilityReview.osd_wahrheit_server import WahrheitStore


class WahrheitStoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary = tempfile.TemporaryDirectory()
        self.wurzel = Path(self._temporary.name)
        (self.wurzel / "frames").mkdir()
        for nummer in (1, 2, 3):
            (self.wurzel / "frames" / f"f{nummer:04d}.jpg").write_bytes(b"bild")
        (self.wurzel / "wahrheit.txt").write_text(
            "# Meterstand je Nummer eintragen\n0001 =\n0002 =\n0003 =\n", encoding="utf-8")
        (self.wurzel / "leser_ergebnisse.json").write_text(
            json.dumps([
                {"nr": 1, "haltung": "36051-33461", "gelesen": 12.5},
                {"nr": 2, "haltung": "36051-33461", "gelesen": None},
                {"nr": 3, "haltung": "10261-10262", "gelesen": 0.3},
            ]),
            encoding="utf-8")

    def tearDown(self) -> None:
        self._temporary.cleanup()

    def _store(self) -> WahrheitStore:
        return WahrheitStore(self.wurzel)

    def test_der_erste_offene_eintrag_wird_gezeigt(self) -> None:
        stand = self._store().stand()

        self.assertEqual(3, stand["gesamt"])
        self.assertEqual(3, stand["offen"])
        self.assertEqual(1, stand["aktuell"]["nr"])
        self.assertEqual("36051-33461", stand["aktuell"]["haltung"])

    def test_die_lesung_des_programms_wird_nie_mitgeliefert(self) -> None:
        # Sie waere eine Vorgabe statt einer Pruefung. Genau diese Beeinflussung
        # hat bei den Boegen eine 33-Prozent-Sichtpruefung unbemerkt gelassen.
        stand = self._store().stand()

        self.assertNotIn("gelesen", stand["aktuell"])
        self.assertNotIn("sequenz", stand["aktuell"])
        self.assertNotIn("roh", stand["aktuell"])

    def test_ein_wert_wird_gespeichert_und_die_kopfzeile_bleibt(self) -> None:
        self._store().eintragen(1, "12.5")

        inhalt = (self.wurzel / "wahrheit.txt").read_text(encoding="utf-8")
        self.assertTrue(inhalt.startswith("# Meterstand je Nummer eintragen"))
        self.assertIn("0001 = 12.5", inhalt)
        self.assertIn("0002 =", inhalt)
        self.assertFalse((self.wurzel / "wahrheit.txt.tmp").exists())

    def test_komma_wird_wie_punkt_gelesen(self) -> None:
        self._store().eintragen(1, "0,3")

        self.assertIn("0001 = 0.3", (self.wurzel / "wahrheit.txt").read_text(encoding="utf-8"))

    def test_unleserlich_wird_als_fragezeichen_festgehalten(self) -> None:
        self._store().eintragen(2, "?")

        self.assertIn("0002 = ?", (self.wurzel / "wahrheit.txt").read_text(encoding="utf-8"))

    def test_ein_text_wird_abgewiesen_statt_gespeichert(self) -> None:
        with self.assertRaises(ValueError):
            self._store().eintragen(1, "geht nicht")

        self.assertIn("0001 =\n", (self.wurzel / "wahrheit.txt").read_text(encoding="utf-8"))

    def test_eine_unbekannte_nummer_wird_abgewiesen(self) -> None:
        with self.assertRaises(ValueError):
            self._store().eintragen(99, "1.0")

    def test_nach_dem_eintragen_folgt_der_naechste_offene(self) -> None:
        store = self._store()

        stand = store.eintragen(1, "12.5")

        self.assertEqual(2, stand["offen"])
        self.assertEqual(2, stand["aktuell"]["nr"])

    def test_ein_neustart_setzt_beim_ersten_offenen_fort(self) -> None:
        self._store().eintragen(1, "12.5")
        self._store().eintragen(2, "?")

        self.assertEqual(3, self._store().stand()["aktuell"]["nr"])

    def test_eine_bereits_eingetragene_nummer_laesst_sich_gezielt_ansehen(self) -> None:
        store = self._store()
        store.eintragen(1, "12.5")

        stand = store.stand(1)

        self.assertEqual(1, stand["aktuell"]["nr"])
        self.assertEqual("12.5", stand["aktuell"]["wert"])

    def test_der_bildpfad_bleibt_im_bildordner(self) -> None:
        store = self._store()

        self.assertIsNotNone(store.bild_pfad(1))
        self.assertIsNone(store.bild_pfad(99))


if __name__ == "__main__":
    unittest.main()
