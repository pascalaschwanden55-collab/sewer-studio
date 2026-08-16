"""Laufzeitteil des trainierten OSD-Zeichenlesers.

Hier liegt bewusst NUR das, was ohne geladenes Modell prueffaehig ist:
Normierung des Ausschnitts und der Zusammenbau erkannter Zeichen zu einer
Zeichenkette. Die Deutung der Kette macht unveraendert osd_meter.parse_meter.

Zur Normierung: Die Abstandsschranken des alten Vorlagenlesers standen als feste
Pixelwerte da, eingestellt auf SD mit rund 18 Pixel hohen Ziffern. Auf HD sind
dieselben Zeichen doppelt so gross und der Leser verlor Dezimalpunkt und Einheit
("LZ1: 3.2m" wurde "L132"). Wer den Ausschnitt vor der Inferenz auf eine feste
Hoehe bringt, kann diesen Fehler gar nicht erst machen.

Fix-Runde 1 (2026-08-16): Zwei Bruchstellen im urspruenglichen Brief korrigiert
- beide untergruben genau den Zweck dieser Datei:
  1. ZIEL_HOEHE war mit 32 zu klein bemessen (Herleitung siehe unten bei der
     Konstante) und druckte die Ziffer unter osd_meter.GLYPHE_MIN_H.
  2. Die Dublettenunterdrueckung verglich nur den Mittenabstand zweier Boxen,
     ohne deren Breite zu kennen. Ein eng an seiner Ziffer sitzender Punkt
     (bei OSD-Schriften ueblich) fiel darunter und wurde als Dublette
     verworfen - aus "0000.30" wurde so "000030": ein FALSCHER Wert mit
     voller Sicherheit, den weder die Mindestsicherheit noch die
     Unbekannt-Regel faengt, weil das Zeichen nicht unsicher ist, sondern
     schlicht fehlt. Ersetzt durch echte Box-Ueberlappung (IoU).
"""

from __future__ import annotations

from PIL import Image

from . import osd_meter

# Zielhoehe des normierten Ausschnitts.
#
# Herleitung (VORLAEUFIG - Spec Abschnitt 11 laesst den Wert ausdruecklich
# "empirisch in Stufe 1 zu bestimmen" offen; hier nur eine erste Schaetzung,
# die am echten Datensatz noch bestaetigt wird):
# Die OSD-Zone ist bewusst grosszuegig geschnitten (Zonenanteil 0,16 der
# Videohoehe, osd_meter.ZONEN["unten_rechts"]); die Ziffer selbst fuellt davon
# gemessen nur rund ein Fuenftel (Verhaeltnis Ziffer/Zone 0,196, konstant
# ueber SD 576p, HD 720p und HD 1080p). Bei ZIEL_HOEHE=96 landet die Ziffer
# nach der Normierung bei rund 96 * 0,196 = 18,8 px - auf der gemessenen
# Referenz osd_meter.REFERENZ_GLYPHE_H (18 px). Der fruehere Wert 32 druckte
# die Ziffer auf rund 6,3 px, unter osd_meter.GLYPHE_MIN_H (8) und damit
# unwiederbringlich unlesbar, noch bevor das Modell das Bild sieht.
ZIEL_HOEHE = 96

# Unter drei Zeichen ist keine sinnvolle Meterangabe moeglich.
TOR_MINDESTZEICHEN = 3

# IoU-Schwelle fuer die Dublettenunterdrueckung: Zwei Boxen mit IoU >= diesem
# Wert gelten als dasselbe Zeichen, die schwaechere faellt weg. 0,5 ist der
# uebliche NMS-Schwellenwert und trennt zuverlaessig eine echte Doppel-
# detektion (Ueberlappung meist > 0,7) von zwei eng benachbarten, aber
# eigenstaendigen Zeichen wie Ziffer und Dezimalpunkt (Ueberlappung dort nahe
# 0, siehe Testfaelle).
_IOU_SCHWELLE = 0.5


def _iou(a: tuple[int, float, float, float, float, float],
         b: tuple[int, float, float, float, float, float]) -> float:
    """Intersection over Union zweier Boxen im Format (klasse, x,y,b,h,sicherheit).

    x/breite und y/hoehe sind normierte YOLO-Koordinaten (relativ zur Breite
    bzw. Hoehe desselben Ausschnitts). Beide Boxen teilen denselben
    Ausschnitt, daher kuerzt sich der gemeinsame Skalierungsfaktor in
    Intersection/Union exakt heraus - die IoU auf den normierten Werten ist
    identisch zur IoU in echten Pixeln.
    """
    _, ax, ay, ab, ah, _ = a
    _, bx, by, bb, bh, _ = b
    a_x0, a_x1 = ax - ab / 2, ax + ab / 2
    a_y0, a_y1 = ay - ah / 2, ay + ah / 2
    b_x0, b_x1 = bx - bb / 2, bx + bb / 2
    b_y0, b_y1 = by - bh / 2, by + bh / 2

    schnitt_b = max(0.0, min(a_x1, b_x1) - max(a_x0, b_x0))
    schnitt_h = max(0.0, min(a_y1, b_y1) - max(a_y0, b_y0))
    schnitt = schnitt_b * schnitt_h
    if schnitt <= 0.0:
        return 0.0
    union = ab * ah + bb * bh - schnitt
    return schnitt / union if union > 0.0 else 0.0


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
        if any(_iou(kandidat, fest) >= _IOU_SCHWELLE for fest in behalten):
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
