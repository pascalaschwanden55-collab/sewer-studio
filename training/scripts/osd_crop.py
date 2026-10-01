"""Gemeinsamer Zuschnitt der OSD-Zone unten rechts (Fix-Runde 1, Aufgabe 3).

EIN Weg fuehrt vom vollen Frame zum Ausschnitt, den Ernte, Modell-Leser und -
fuer seine Zielgroesse - der kuenstliche Bildgenerator verwenden.

Vorher rundeten osd_ernte.py (int()) und osd_modell_leser.py (round())
unterschiedlich. Gemessen:

                harvest int()      inference round()
    SD  720x576     274x93 @ y483      274x92 @ y484
    HD 1280x720     487x116 @ y604     486x115 @ y605
    HD 1920x1080    730x173 @ y907     identisch

Auf zwei von drei Gold-Aufloesungen verschob das den Ausschnitt zwischen
Training (Ernte) und Messung (Modell-Leser) um eine Bildzeile - dieselbe
Form von Fehler, die dieses Subsystem eigentlich beheben soll, nur eine
Ebene tiefer im Zuschnitt statt in der Zeichenfindung.

round() ist die richtige Wahl, nicht int(): osd_meter.glyphenmaske() (dort,
UNVERAENDERT) berechnet die Zonengrenzen intern bereits mit round() -
sidecar/sidecar/osd_meter.py Zeile ~101 (`round(zone[0] * w)` usw.). Ein
davon abweichender Zuschnitt hier wuerde die von boxen_aus_maske()
gelieferten Vollbildkoordinaten falsch auf den Ausschnitt umrechnen.
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

_WURZEL = Path(__file__).resolve().parents[2]
if str(_WURZEL / "sidecar") not in sys.path:
    sys.path.insert(0, str(_WURZEL / "sidecar"))

from sidecar import osd_modell


def zonen_box(breite: int, hoehe: int) -> tuple[int, int, int, int]:
    """Pixelkasten (links, oben, rechts, unten) der Zone "unten_rechts" fuer
    ein Bild dieser Groesse - dieselbe Rundung wie osd_meter.glyphenmaske()."""
    return osd_modell.zonen_box(breite, hoehe)


def schneide_zone(bild: Image.Image) -> tuple[Image.Image, tuple[int, int]]:
    """Schneidet die Zone unten rechts aus `bild`.

    Rueckgabe: (Ausschnitt, Versatz (x, y)). Der Versatz wird gebraucht, um
    Vollbildkoordinaten (z.B. aus osd_meter.boxen_aus_maske()) auf den
    Ausschnitt umzurechnen.
    """
    return osd_modell.schneide_zone(bild)
