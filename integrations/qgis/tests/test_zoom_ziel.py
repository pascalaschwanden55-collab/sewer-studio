"""Prueft die reine Zoom-Rechnung des QGIS-Plugins.

Diese Rechnung liegt bewusst in einem eigenen Modul OHNE PyQGIS-Import,
damit sie ohne installiertes QGIS geprueft werden kann.
"""
import math
import sys
import unittest
from pathlib import Path

QGIS_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(QGIS_ROOT))

from sewerstudio_bridge.zoom_ziel import (  # noqa: E402
    ZOOM_MASSSTAB,
    zoom_ziel,
)


class ZoomZielTests(unittest.TestCase):
    def test_haltung_zoomt_auf_die_mitte_der_ausdehnung(self):
        ziel = zoom_ziel(2680000.0, 1190000.0, 2680040.0, 1190030.0)

        self.assertIsNotNone(ziel)
        self.assertAlmostEqual(2680020.0, ziel.mitte_x)
        self.assertAlmostEqual(1190015.0, ziel.mitte_y)

    def test_schacht_ohne_ausdehnung_behaelt_seinen_punkt(self):
        # Ein Schacht ist ein Punkt: xmin == xmax. Die Mitte ist der Punkt selbst.
        ziel = zoom_ziel(2680000.0, 1190000.0, 2680000.0, 1190000.0)

        self.assertIsNotNone(ziel)
        self.assertAlmostEqual(2680000.0, ziel.mitte_x)
        self.assertAlmostEqual(1190000.0, ziel.mitte_y)

    def test_massstab_ist_immer_1_zu_100(self):
        kurz = zoom_ziel(2680000.0, 1190000.0, 2680005.0, 1190005.0)
        lang = zoom_ziel(2680000.0, 1190000.0, 2680400.0, 1190300.0)

        self.assertEqual(100.0, ZOOM_MASSSTAB)
        self.assertEqual(100.0, kurz.massstab)
        self.assertEqual(100.0, lang.massstab)

    def test_unbrauchbare_koordinaten_ergeben_kein_ziel(self):
        # Kein Zoom ist besser als ein Sprung an eine unsinnige Stelle.
        self.assertIsNone(zoom_ziel(math.nan, 1190000.0, 2680040.0, 1190030.0))
        self.assertIsNone(zoom_ziel(2680000.0, 1190000.0, math.inf, 1190030.0))


if __name__ == "__main__":
    unittest.main()
