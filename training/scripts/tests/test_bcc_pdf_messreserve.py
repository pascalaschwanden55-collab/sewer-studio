from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "bcc_pdf_messreserve.py"
SPEC = importlib.util.spec_from_file_location("bcc_pdf_messreserve", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
modul = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(modul)


class BccPdfMessreserveTests(unittest.TestCase):
    def test_altbestand_ausschluss_gegenrichtung_und_fehlercode_werden_gesperrt(self) -> None:
        sichtung = {"eintraege": [
            self._eintrag("A-B"),
            self._eintrag("D-C"),
            self._eintrag("E-F", codes=["BCC.YB"]),
            self._eintrag("G-H"),
        ]}
        messbestand = {
            "sd": {"eintraege": [{"haltung": "B-A"}]},
            "hd": {"eintraege": []},
        }
        ausschluss = {"gesperrt": ["C-D"]}

        kandidaten = modul.kandidaten_laden(sichtung, messbestand, ausschluss)

        self.assertEqual(["G-H"], [e["haltung"] for e in kandidaten])

    def test_auswahl_ist_wiederholbar_und_eindeutig(self) -> None:
        kandidaten = [self._eintrag(f"H-{i}") | {"physische_haltung": f"h-{i}"}
                      for i in range(10)]

        a = modul.auswaehlen(kandidaten, 5, "saat")
        b = modul.auswaehlen(list(reversed(kandidaten)), 5, "saat")

        self.assertEqual(a, b)
        self.assertEqual(5, len({e["physische_haltung"] for e in a}))

    def test_zu_grosse_auswahl_wird_nicht_geraten(self) -> None:
        with self.assertRaisesRegex(ValueError, "Nur 0 geeignete"):
            modul.auswaehlen([], 1, "saat")

    @staticmethod
    def _eintrag(haltung: str, codes: list[str] | None = None) -> dict:
        return {
            "haltung": haltung,
            "befunde": 2,
            "codes": codes or ["BCCAY"],
            "video": "video.mp4",
            "breite": 720,
            "hoehe": 576,
            "dauer_s": 100.0,
            "art": "SD",
        }


if __name__ == "__main__":
    unittest.main()
