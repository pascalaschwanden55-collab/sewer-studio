import unittest

from training.scripts.osd_layout_review_bericht import anteil, auswerten, lage


class OsdLayoutReviewBerichtTests(unittest.TestCase):
    def test_lage_wird_aus_dem_menschlichen_klick_bestimmt(self):
        self.assertEqual("oben_links", lage(0.1, 0.2))
        self.assertEqual("unten_rechts", lage(0.9, 0.8))

    def test_anteil_enthaelt_unsicherheitsbereich(self):
        wert = anteil(38, 40)
        self.assertEqual(0.95, wert["anteil"])
        self.assertEqual([0.835, 0.9862], wert["wilson_95"])

    def test_bericht_zaehlt_sichtbare_stile_und_fehlende_meter(self):
        queue = {"faelle": [{"fall_id": "a"}, {"fall_id": "b"}]}
        review = {"queue_sha256": "hash", "entscheidungen": {
            "a": {"meter_sichtbar": True, "x": 0.9, "y": 0.8,
                  "polaritaet": "hell_auf_dunkel", "farbe": "gelb",
                  "format": "praefix_oder_nullen"},
            "b": {"meter_sichtbar": False},
        }}
        bericht = auswerten(queue, review, "hash")
        self.assertEqual(1, bericht["meter_sichtbar"])
        self.assertEqual(1, bericht["kein_meter_sichtbar"])
        self.assertEqual({"unten_rechts": 1}, bericht["lage"])
        self.assertEqual({"gelb": 1}, bericht["farbe"])

    def test_unvollstaendige_review_wird_nicht_ausgewertet(self):
        queue = {"faelle": [{"fall_id": "a"}, {"fall_id": "b"}]}
        review = {"queue_sha256": "hash", "entscheidungen": {"a": {}}}
        with self.assertRaisesRegex(ValueError, "unvollstaendig"):
            auswerten(queue, review, "hash")


if __name__ == "__main__":
    unittest.main()
