from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "bcc_pdf_recall_bericht.py"
SPEC = importlib.util.spec_from_file_location("bcc_pdf_recall_bericht", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
modul = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(modul)


class BccPdfRecallBerichtTests(unittest.TestCase):
    def test_gruppen_ergebnisse_haben_getrennte_dateinamen(self) -> None:
        self.assertEqual("messung_conf040_gesamt.json", modul.ergebnis_dateiname(0.4, None))
        self.assertEqual("messung_conf040_sd.json", modul.ergebnis_dateiname(0.4, "sd"))
        self.assertEqual("messung_conf040_hd.json", modul.ergebnis_dateiname(0.4, "hd"))

    def test_laden_filtert_auch_nicht_ausgewertete_nach_haelfte_und_gruppe(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            wurzel = Path(temp)
            bestand = wurzel / "messbestand.json"
            lauf = wurzel / "lauf"
            haltungen = lauf / "haltungen"
            haltungen.mkdir(parents=True)
            bestand.write_text(
                json.dumps(
                    {
                        "sd": {"eintraege": [
                            {"haltung": "SD-M", "haelfte": "messung"},
                            {"haltung": "SD-K", "haelfte": "kalibrierung"},
                        ]},
                        "hd": {"eintraege": [
                            {"haltung": "HD-M", "haelfte": "messung"},
                        ]},
                    }
                ),
                encoding="utf-8",
            )
            for name, gruppe in (("SD-M", "sd"), ("SD-K", "sd"), ("HD-M", "hd")):
                (haltungen / f"{name}.json").write_text(
                    json.dumps({"haltung": name, "gruppe": gruppe,
                                "zustand": "nicht ausgewertet", "grund": "Test"}),
                    encoding="utf-8",
                )

            ergebnisse, offen = modul.laden("messung", "sd", bestand, lauf)

            self.assertEqual([], ergebnisse)
            self.assertEqual(["SD-M"], [eintrag["haltung"] for eintrag in offen])

    def test_vergleichsbeleg_kennzeichnet_bestand_als_nicht_unabhaengig(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            bestand = Path(temp) / "bestand.json"
            bestand.write_text("{}", encoding="utf-8")
            ergebnis = {
                "schwelle": 0.4, "stark_ab": 0.7, "toleranz_s": 15.0,
                "haltungen": 39, "soll_boegen": 85, "getroffen": 66,
                "recall": 0.7765, "vorschlaege": 154,
            }

            beleg = modul.vergleichsbeleg(ergebnis, None, bestand)

            self.assertEqual("bcc_pdf_vergleichsbestand_v1", beleg["schema"])
            self.assertIn("keine unabhaengige Modellfreigabe", beleg["verwendung"])
            self.assertEqual(85, beleg["soll_boegen"])
            self.assertEqual(66, beleg["getroffen"])
            self.assertEqual(64, len(beleg["messbestand_sha256"]))


if __name__ == "__main__":
    unittest.main()
