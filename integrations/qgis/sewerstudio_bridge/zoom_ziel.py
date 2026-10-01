"""Reine Zoom-Rechnung fuer die SewerStudio-Bruecke.

Bewusst OHNE PyQGIS-Import: Dadurch laesst sich die Rechnung ohne
installiertes QGIS pruefen. Das Plugin selbst setzt das Ergebnis
anschliessend auf der Karte um.
"""
import math
from collections import namedtuple

# Fester Anzeigemassstab beim Klick auf eine Haltung oder einen Schacht
# in SewerStudio (Entscheid Pascal 09.09.2026): immer 1:100, damit jedes
# Bauteil gleich gross erscheint und sich Groessen vergleichen lassen.
# Eine lange Haltung ragt dabei bewusst ueber den Bildrand hinaus.
ZOOM_MASSSTAB = 100.0

ZoomZiel = namedtuple("ZoomZiel", ("mitte_x", "mitte_y", "massstab"))


def zoom_ziel(xmin, ymin, xmax, ymax, massstab=ZOOM_MASSSTAB):
    """Mittelpunkt der Ausdehnung samt festem Massstab.

    Liefert ``None``, wenn eine Koordinate keine endliche Zahl ist — kein
    Zoom ist besser als ein Sprung an eine unsinnige Stelle der Karte.
    """
    werte = (xmin, ymin, xmax, ymax)
    for wert in werte:
        if wert is None or not math.isfinite(wert):
            return None

    return ZoomZiel((xmin + xmax) / 2.0, (ymin + ymax) / 2.0, massstab)
