import unittest

from training.scripts.osd_prefix_fallback_bericht import (
    gelieferter_gold_fall,
    ist_zielstil,
    statistik,
)


class OsdPrefixFallbackBerichtTests(unittest.TestCase):
    def test_statistik_trennt_nicht_gelesen_richtig_und_falsch(self):
        wert = statistik([(None, 1.0), (1.0, 1.0), (2.0, 3.0), (4.0, None)])
        self.assertEqual({
            "bilder": 4, "geliefert": 3, "richtig_1cm": 1,
            "falsch_oder_unpruefbar": 2}, wert)

    def test_zielstil_verlangt_alle_fuenf_merkmale(self):
        urteil = {"x": 0.9, "y": 0.9, "polaritaet": "hell_auf_dunkel",
                  "farbe": "weiss_grau", "format": "praefix_oder_nullen"}
        self.assertTrue(ist_zielstil(urteil))
        self.assertFalse(ist_zielstil({**urteil, "farbe": "gelb"}))

    def test_gold_detail_zeigt_falschen_wert_und_leseweg(self):
        fall = gelieferter_gold_fall(
            {"datei": "f1.jpg", "haltung": "A-B", "menschlich_lesbar": True, "meter": 2.3},
            {"meter": 23.0, "leseweg": "vorlagen", "zeichenfolge": "LZ23.0m",
             "tesseract_text": ""},
        )
        self.assertEqual(23.0, fall["gelesen_meter"])
        self.assertFalse(fall["richtig_1cm"])
        self.assertEqual("vorlagen", fall["leseweg"])

    def test_nicht_gelesener_gold_fall_bleibt_aus_dem_detail(self):
        self.assertIsNone(gelieferter_gold_fall(
            {"datei": "f1.jpg", "menschlich_lesbar": True, "meter": 2.3},
            {"meter": None},
        ))


if __name__ == "__main__":
    unittest.main()
