from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "osd_layout_review_queue.py"
SPEC = importlib.util.spec_from_file_location("osd_layout_review_queue", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
modul = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(modul)


class OsdLayoutReviewQueueTests(unittest.TestCase):
    def test_vorherige_haltungen_und_doppelte_haltung_werden_ausgeschlossen(self) -> None:
        eintraege = [
            {"id": "b", "physische_haltung": "h1"},
            {"id": "a", "physische_haltung": "h1"},
            {"id": "c", "physische_haltung": "h2"},
            {"id": "d", "physische_haltung": "h3"},
        ]

        auswahl = modul.auswaehlen(eintraege, {"h2"}, 2, "saat")

        self.assertEqual({"h1", "h3"}, {e["physische_haltung"] for e in auswahl})

    def test_auswahl_ist_unabhaengig_von_eingabereihenfolge(self) -> None:
        eintraege = [{"id": str(i), "physische_haltung": f"h{i}"} for i in range(6)]

        a = modul.auswaehlen(eintraege, set(), 3, "saat")
        b = modul.auswaehlen(list(reversed(eintraege)), set(), 3, "saat")

        self.assertEqual(a, b)


if __name__ == "__main__":
    unittest.main()
