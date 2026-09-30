"""Prueft die gemeinsame Regel "physische Haltung" gegen die Beispieldatei,
die auch der C#-Test (HaltungsidentitaetTests) liest."""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

SCRIPT_ROOT = Path(__file__).resolve().parents[1]
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

import gold_stock_audit  # noqa: E402
import haltungsidentitaet  # noqa: E402

BEISPIELE = (
    Path(__file__).resolve().parents[3]
    / "tests" / "Fixtures" / "Haltungsidentitaet" / "beispiele.json"
)


def _lade() -> dict:
    return json.loads(BEISPIELE.read_text(encoding="utf-8"))


class HaltungsidentitaetTests(unittest.TestCase):
    def test_beispiele_gegen_python_regel(self) -> None:
        faelle = _lade()["faelle"]
        self.assertGreaterEqual(len(faelle), 10)
        for fall in faelle:
            with self.subTest(fall["id"]):
                normalisiert = gold_stock_audit.normalize_holding_key(fall["eingabe"])
                self.assertEqual(fall["python"]["normalisiert"], normalisiert)
                if normalisiert is None:
                    self.assertIsNone(fall["python"]["physisch"])
                    self.assertFalse(fall["gleiche_haltung"])
                    continue
                physisch = haltungsidentitaet.physischer_schluessel(normalisiert)
                self.assertEqual(fall["python"]["physisch"], physisch)
                eval_physisch = haltungsidentitaet.physischer_schluessel(
                    fall["eval_schluessel"]
                )
                self.assertEqual(fall["gleiche_haltung"], physisch == eval_physisch)

    def test_ungueltige_schluessel_werfen(self) -> None:
        for text in _lade()["ungueltig_fuer_physischen_schluessel"]:
            with self.subTest(text), self.assertRaises(ValueError):
                haltungsidentitaet.physischer_schluessel(text)

    def test_aufrufer_nutzen_die_gemeinsame_regel(self) -> None:
        import bcc_release_holdout

        self.assertEqual("100|200", gold_stock_audit._physical_holding_key("200-100"))
        self.assertEqual("100|200", bcc_release_holdout._physical_holding_key("200-100"))
        # Streng: nicht normalisierter Eintrag bleibt Fehler beim Aufrufer.
        with self.assertRaises(ValueError):
            gold_stock_audit._physical_holding_key("06.100-200")


if __name__ == "__main__":
    unittest.main()
