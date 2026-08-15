"""Laufzeitteil des trainierten OSD-Zeichenlesers.

Hier liegt bewusst NUR das, was ohne geladenes Modell prueffaehig ist:
Normierung des Ausschnitts und der Zusammenbau erkannter Zeichen zu einer
Zeichenkette. Die Deutung der Kette macht unveraendert osd_meter.parse_meter.

Zur Normierung: Die Abstandsschranken des alten Vorlagenlesers standen als feste
Pixelwerte da, eingestellt auf SD mit rund 18 Pixel hohen Ziffern. Auf HD sind
dieselben Zeichen doppelt so gross und der Leser verlor Dezimalpunkt und Einheit
("LZ1: 3.2m" wurde "L132"). Wer den Ausschnitt vor der Inferenz auf eine feste
Hoehe bringt, kann diesen Fehler gar nicht erst machen.
"""

from __future__ import annotations

from PIL import Image

from . import osd_meter

# Zielhoehe des normierten Ausschnitts. Rund die doppelte SD-Ziffernhoehe
# (REFERENZ_GLYPHE_H = 18), damit auch kleine Zeichen genug Pixel behalten.
ZIEL_HOEHE = 32

# Unter drei Zeichen ist keine sinnvolle Meterangabe moeglich.
TOR_MINDESTZEICHEN = 3

# Zwei Boxen, deren Mitten naeher als dieser Anteil der Ausschnittsbreite
# beieinanderliegen, gelten als dasselbe Zeichen.
_MINDESTABSTAND = 0.02


def normiere_ausschnitt(bild: Image.Image, ziel_hoehe: int = ZIEL_HOEHE) -> Image.Image:
    """Bringt den Ausschnitt auf feste Hoehe, Seitenverhaeltnis bleibt."""
    breite, hoehe = bild.size
    if hoehe <= 0 or breite <= 0:
        return bild
    faktor = ziel_hoehe / hoehe
    return bild.resize((max(1, round(breite * faktor)), ziel_hoehe), Image.BICUBIC)


def zu_zeichenfolge(
    erkennungen: list[tuple[int, float, float, float, float, float]],
) -> tuple[str, float]:
    """Setzt Erkennungen von links nach rechts zu einer Zeichenkette zusammen.

    Rueckgabe: (Zeichenkette, kleinste Sicherheit). Ohne Erkennung ("", 0.0).
    """
    if not erkennungen:
        return "", 0.0

    # Staerkste zuerst, damit bei Ueberlappung die schwaechere Box faellt.
    nach_staerke = sorted(erkennungen, key=lambda e: e[5], reverse=True)
    behalten: list[tuple[int, float, float, float, float, float]] = []
    for kandidat in nach_staerke:
        if any(abs(kandidat[1] - fest[1]) < _MINDESTABSTAND for fest in behalten):
            continue
        behalten.append(kandidat)

    behalten.sort(key=lambda e: e[1])

    folge = ""
    for klasse, _x, _y, _b, _h, _s in behalten:
        if 0 <= klasse < len(osd_meter.ZEICHEN):
            folge += osd_meter.ZEICHEN[klasse]
        else:
            # Unbekannte Klasse: Lieber gar nichts als ein geratenes Zeichen.
            return "", 0.0

    return folge, min(e[5] for e in behalten)
