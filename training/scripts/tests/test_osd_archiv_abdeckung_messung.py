import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image

from training.scripts.osd_archiv_abdeckung_messung import (
    atomar_neu_schreiben,
    gleichmaessige_indizes,
    messe_eintrag,
    zusammenfassung,
)


class OsdArchivAbdeckungMessungTests(unittest.TestCase):
    def test_indizes_decken_anfang_und_ende_ohne_duplikate_ab(self):
        self.assertEqual([0, 25, 50, 74, 99], gleichmaessige_indizes(100, 5))

    def test_ein_defektes_video_stoppt_andere_haltungen_nicht(self):
        with tempfile.TemporaryDirectory() as temp:
            video = Path(temp) / "test.mp4"
            video.write_bytes(b"video")

            def fehler(_video, _proben):
                raise ValueError("defekt")

            wert = messe_eintrag(
                {"haltung": "A-B", "gruppe": "sd", "zustand": "geprueft", "video": str(video)},
                20,
                {},
                fehler,
            )

        self.assertEqual("fehler", wert["zustand"])
        self.assertIn("defekt", wert["grund"])

    def test_messung_zaehlt_nur_gelieferte_werte(self):
        with tempfile.TemporaryDirectory() as temp:
            video = Path(temp) / "test.mp4"
            video.write_bytes(b"video")
            bilder = [Image.new("RGB", (10, 10)) for _ in range(3)]
            rueckgaben = [
                {"meter": 1.0, "leseweg": "vorlagen", "zeichenfolge": "1.0m", "tesseract_text": ""},
                {"meter": None, "leseweg": None, "zeichenfolge": "", "tesseract_text": ""},
                {"meter": 2.0, "leseweg": "tesseract_vierziffern", "zeichenfolge": "", "tesseract_text": "0002.00m"},
            ]
            with patch("training.scripts.osd_archiv_abdeckung_messung.osd_meter.lese_meter",
                       side_effect=rueckgaben):
                wert = messe_eintrag(
                    {"haltung": "A-B", "gruppe": "sd", "zustand": "geprueft", "video": str(video)},
                    3,
                    {},
                    lambda _video, _proben: (bilder, 30),
                )

        self.assertEqual(2, wert["gelesen"])
        self.assertEqual(0.6667, wert["abdeckung"])
        self.assertEqual([1.0, 2.0], [x["meter"] for x in wert["lesungen"]])

    def test_zusammenfassung_trennt_gruppen_und_fehler(self):
        wert = zusammenfassung([
            {"gruppe": "sd", "zustand": "geprueft", "extrahiert": 20,
             "gelesen": 14, "abdeckung": 0.7},
            {"gruppe": "sd", "zustand": "fehler"},
            {"gruppe": "hd", "zustand": "geprueft", "extrahiert": 10,
             "gelesen": 0, "abdeckung": 0.0},
        ])
        self.assertEqual(1, wert["sd"]["haltungen"])
        self.assertEqual(0.7, wert["sd"]["frame_abdeckung"])
        self.assertEqual(1, wert["hd"]["haltungen_ohne_lesung"])

    def test_vorhandener_bericht_wird_nie_ueberschrieben(self):
        with tempfile.TemporaryDirectory() as temp:
            ziel = Path(temp) / "bericht.json"
            ziel.write_text("alt", encoding="utf-8")
            with self.assertRaises(FileExistsError):
                atomar_neu_schreiben(ziel, {"neu": True})
            self.assertEqual("alt", ziel.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
