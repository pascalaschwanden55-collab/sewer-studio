"""Laufzeitteil des trainierten OSD-Zeichenlesers.

Hier liegt bewusst NUR das, was ohne geladenes Modell prueffaehig ist:
Normierung des Ausschnitts und der Zusammenbau erkannter Zeichen zu einer
Zeichenkette. Die Deutung der Kette macht unveraendert osd_meter.parse_meter.

Zur Normierung: Die Abstandsschranken des alten Vorlagenlesers standen als feste
Pixelwerte da, eingestellt auf SD mit rund 18 Pixel hohen Ziffern. Auf HD sind
dieselben Zeichen doppelt so gross und der Leser verlor Dezimalpunkt und Einheit
("LZ1: 3.2m" wurde "L132"). Wer den Ausschnitt vor der Inferenz auf eine feste
Hoehe bringt, kann diesen Fehler gar nicht erst machen. (Fix-Runde 1 zu
Aufgabe 8, 2026-08-16: das gilt NUR fuer genau diesen einen Fehler - feste
Pixelschranken auf Zeichenhoehe geeicht. Es heisst NICHT, dass SD und HD
danach fuer das Modell gleich aussehen; ein Leser, der hier aufhoert, waere
falsch informiert - siehe die Richtigstellung direkt im Anschluss.)

Richtigstellung (Fix-Runde 1 zu Aufgabe 7, 2026-08-16): Hier stand vorher, der
HD-Ausfall vom 2026-08-14 koenne dadurch "bauartbedingt nicht wiederkehren".
Das ist zu weitgehend. Nachgerechnet, was bei imgsz=320 tatsaechlich als
Ziffernhoehe beim Modell ankommt (Ultralytics letterboxt seitenverhaeltnistreu;
die Verkettung aus Normierung + Letterbox landet dabei am selben Punkt wie
Letterbox allein - der befuerchtete Unterschied zwischen Trainings- und
Inferenzaufbereitung betraegt 0,01 px, da ist nichts zu reparieren):

                     ohne Normierung   mit Normierung
    SD  576 (273x92)        21,1 px          21,1 px
    HD  720 (486x115)       14,8 px          14,8 px
    HD 1080 (729x172)       14,8 px          14,8 px

Die Normierung bewirkt fuer die Ziffernhoehe am Modell also NICHTS: SD und HD
sehen weiterhin verschieden aus, weil sich die ZONEN-Ausschnitte im
Seitenverhaeltnis unterscheiden (SD rund 5:4, HD rund 16:9) und Ultralytics'
eigenes Letterboxing anhand der laengeren Seite skaliert - nicht weil sich die
Zeichengroesse unterscheidet. Was die Normierung tatsaechlich leistet: eine
feste, von Ultralytics' Letterbox-Verhalten unabhaengige Eingangsgroesse (ohne
sie haette der Skalierungsfaktor im Letterboxing selbst je nach roher
Ausschnittsgroesse variiert). Echte Gleichheit der Ziffernhoehe ueber
verschiedene Bildseitenverhaeltnisse braeuchte zusaetzlich ein festes
Ziel-Seitenverhaeltnis mit Polsterung (Padding); das bleibt fuer Stufe 1
bewusst offen.

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
#
# Richtigstellung (Fix-Runde 1 zu Aufgabe 8, 2026-08-16): Hier stand vorher,
# ZIEL_HOEHE=96 lege die Ziffernhoehe AM MODELL fest (rund 96 * 0,196 = 18,8
# px). Das ist falsch - wie der Docstring oben zeigt, bestimmt Ultralytics'
# eigenes Letterboxing bei imgsz die tatsaechlich ankommende Ziffernhoehe,
# nicht ZIEL_HOEHE. Der Wert 96 bleibt trotzdem: Die reale SD-Zone
# (osd_meter.ZONEN["unten_rechts"] auf 720x576 -> rund 274x92 px) ist damit
# schon fast 96 px hoch - SD wird in diesem Zwischenschritt also so gut wie
# NICHT hochskaliert (Faktor rund 1,04), waehrend HD-Zonen (rund 115 px bei
# 720p, rund 173 px bei 1080p) auf 96 heruntergerechnet werden. Kleiner als
# 96 war trotzdem abzulehnen: Der fruehere Wert 32 haette die Ziffer schon in
# DIESEM Zwischenschritt auf rund 6,3 px gedrueckt (unter
# osd_meter.GLYPHE_MIN_H, 8) - verlorene Bildinformation, die kein
# nachfolgendes Letterboxing zurueckholt, egal was danach mit imgsz passiert.
ZIEL_HOEHE = 96

# Unter drei Zeichen ist keine sinnvolle Meterangabe moeglich.
TOR_MINDESTZEICHEN = 3

# IoU-Schwelle fuer die Dublettenunterdrueckung (VORLAEUFIG, wie ZIEL_HOEHE -
# noch nicht am echten Datensatz bestaetigt, siehe dort). Zwei Boxen mit
# IoU >= diesem Wert gelten als dasselbe Zeichen, die schwaechere faellt weg
# - das ist die Konstante, die entscheidet, ob ein Zeichen STILL aus der
# Zeichenfolge verschwindet: bei falscher Wahl faellt ein echtes,
# eigenstaendiges Zeichen (z.B. der Dezimalpunkt) weg, ohne dass irgendein
# Tor das bemerkt. 0,5 ist der uebliche NMS-Schwellenwert und trennt
# zuverlaessig eine echte Doppeldetektion (Ueberlappung meist > 0,7) von
# zwei eng benachbarten, aber eigenstaendigen Zeichen wie Ziffer und
# Dezimalpunkt (Ueberlappung dort nahe 0, siehe Testfaelle).
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


def zonen_box(breite: int, hoehe: int) -> tuple[int, int, int, int]:
    """Pixelkasten der unteren rechten OSD-Zone.

    Die Rundung entspricht exakt ``osd_meter.glyphenmaske``. Training,
    Messung und Sidecar-Laufzeit muessen dadurch denselben Ausschnitt sehen.
    """
    links, oben, rechts, unten = osd_meter.ZONEN["unten_rechts"]
    return (
        round(links * breite),
        round(oben * hoehe),
        round(rechts * breite),
        round(unten * hoehe),
    )


def schneide_zone(bild: Image.Image) -> tuple[Image.Image, tuple[int, int]]:
    """Schneidet die OSD-Zone aus und liefert Ausschnitt sowie Versatz."""
    kasten = zonen_box(*bild.size)
    return bild.crop(kasten), (kasten[0], kasten[1])


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


def ergebnis_aus_erkennungen(
    erkennungen: list[tuple[int, float, float, float, float, float]],
    stil: str,
    schwelle: float,
    format: str | None = None,
) -> dict:
    """Baut aus Modellboxen dieselbe rohe Lesung wie der Vorlagenleser."""
    folge, kleinste_sicherheit = zu_zeichenfolge(erkennungen)
    konfidenz_min = kleinste_sicherheit if erkennungen else None

    meter = None
    leseweg = None
    if (len(folge) >= TOR_MINDESTZEICHEN
            and konfidenz_min is not None
            and konfidenz_min >= schwelle):
        wert = osd_meter.parse_meter(folge, stil, format)
        if wert is not None:
            meter = wert
            leseweg = "modell"

    return {
        "meter": meter,
        "zeichenfolge": folge,
        "stil": stil,
        "leseweg": leseweg,
        "konfidenz_min": konfidenz_min,
    }
