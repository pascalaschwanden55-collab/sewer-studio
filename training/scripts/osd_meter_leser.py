#!/usr/bin/env python3
"""Prototyp: guenstiger OSD-Meterleser (Diagnose, keine Abhaengigkeiten im Produkt).

Liest den Meterstand aus der OSD-Ecke (z. B. „LZ2: 14.1m", „0.10") ohne
Bildmodell: Glyphen segmentieren (helle Kleinkomponenten), dann Ziffern per
Template-Matching gegen gerenderte Referenzglyphen klassifizieren.

--debug schreibt die Zone mit eingezeichneten Zeichenboxen und erkannter
Zeichenfolge je Bild nach <out>/debug/.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

# Meterzone: unten rechts (grosszuegig), dazu unten links fuer Layouts ohne LZ-Box
ZONEN = {
    "unten_rechts": (0.62, 0.84, 1.00, 1.00),
}
HELLIGKEIT = 150
GLYPHE_MIN_H, GLYPHE_MAX_H = 8, 40
TEMPLATE_H = 28
ZEICHEN = "0123456789.mLZ:"


def glyphenmaske(bild: Image.Image) -> tuple["object", str]:
    """Binaermaske mutmasslicher OSD-Zeichen im Meterbereich + Stil.

    Dunkler Text auf hellem Kasten: adaptiv gegen die lokale Umgebung
    (heller Hintergrund nötig), funktioniert auf jeder Bildhelligkeit.
    Heller Text direkt auf dem Video: helle Kleinkomponenten. Rueckgabe ist
    die Vollbild-Maske; die Segmentierung erfolgt in lese_meter.
    """
    import cv2
    import numpy as np

    arr = np.asarray(bild.convert("RGB"))
    hell = arr.max(axis=2)
    grau = arr.mean(axis=2).astype(np.uint8)
    h, w = grau.shape
    rahmen = np.zeros((h, w), "uint8")
    for zone in ZONEN.values():
        x0, y0 = round(zone[0] * w), round(zone[1] * h)
        rahmen[y0:round(zone[3] * h), x0:round(zone[2] * w)] = 1

    # 1) dunkler Text auf hellem Grund (adaptiv, lokal)
    lokal = cv2.GaussianBlur(grau.astype(np.float32), (0, 0), 6)
    dunkel = ((grau.astype(np.float32) < lokal * 0.62) & (grau < 125)
              & (lokal > 110) & (rahmen == 1)).astype("uint8")
    n, _lab, stats, _ = cv2.connectedComponentsWithStats(dunkel, 8)
    treffer = sum(
        1 for i in range(1, n)
        if GLYPHE_MIN_H - 2 <= stats[i, 3] <= GLYPHE_MAX_H
        and 2 <= stats[i, 2] <= 26 and stats[i, 4] >= 3)
    if treffer >= 3:
        return dunkel, "dunkel"

    # 2) heller Text auf Video — nur wenn die Treffer eine Zeile bilden,
    # sonst sind es Wasserreflexe am Bildrand.
    hell_text = ((hell > HELLIGKEIT) & (rahmen == 1)).astype("uint8")
    n, _lab, stats, _ = cv2.connectedComponentsWithStats(hell_text, 8)
    mitten = [
        (stats[i, 1] + stats[i, 3] / 2)
        for i in range(1, n)
        if GLYPHE_MIN_H <= stats[i, 3] <= GLYPHE_MAX_H
        and 2 <= stats[i, 2] <= 26 and stats[i, 4] >= 8
    ]
    if len(mitten) >= 3 and max(mitten) - min(mitten) <= 14:
        return hell_text, "hell"

    # 3) dunkler Text direkt auf dem Video (kein Kasten)
    dunkel_video = ((grau.astype(np.float32) < lokal * 0.55) & (grau < 100)
                    & (rahmen == 1)).astype("uint8")
    return dunkel_video, "dunkel_video"


def boxen_aus_maske(maske, stil: str) -> list[tuple[int, int, int, int]]:
    import cv2

    n, _lab, stats, _ = cv2.connectedComponentsWithStats(maske, 8)
    boxen = []
    punkte = []
    for i in range(1, n):
        x, y, bw, bh, flaeche = stats[i]
        min_flaeche = 3 if stil in ("dunkel", "dunkel_video") else 8
        min_h = GLYPHE_MIN_H - 2 if stil in ("dunkel", "dunkel_video") else GLYPHE_MIN_H
        if 2 <= bw <= 7 and 2 <= bh <= 7 and flaeche >= 3:
            punkte.append((x, y, x + bw, y + bh))  # Dezimalpunkt/Doppelpunkt-Punkt
        elif (min_h <= bh <= GLYPHE_MAX_H and 2 <= bw <= 26
                and min_flaeche <= flaeche <= bw * bh * 0.95):
            boxen.append((x, y, x + bw, y + bh))
    # vertikal gestapelte Punkte zu einem Doppelpunkt zusammenfassen;
    # Punkte ohne Glyphen-Nachbarn (±22 px) sind Rauschen und fallen weg.
    punkte.sort(key=lambda b: (b[0], b[1]))
    def hat_nachbarn(p: tuple[int, int, int, int]) -> bool:
        return any(abs(g[0] - p[0]) <= 22 for g in boxen)
    punkte = [p for p in punkte if hat_nachbarn(p) and p[3] - p[1] >= 2]
    verbraucht: set[int] = set()
    for i, p in enumerate(punkte):
        if i in verbraucht:
            continue
        for j in range(i + 1, len(punkte)):
            q = punkte[j]
            if j not in verbraucht and abs(p[0] - q[0]) <= 3 and 3 <= q[1] - p[3] <= 10:
                boxen.append((p[0], p[1], max(p[2], q[2]), q[3]))  # ':'
                verbraucht.add(j)
                break
        else:
            boxen.append(p)  # '.'
        verbraucht.add(i)
    # Zeilenkohärenz: OSD-Text steht in einer Zeile; alles, was mehr als 8 px
    # vom Median der Zeichenmitten abweicht, ist Textur und faellt weg.
    if len(boxen) >= 4:
        mitten = sorted((b[1] + b[3]) / 2 for b in boxen)
        median = mitten[len(mitten) // 2]
        gefiltert = [b for b in boxen if abs((b[1] + b[3]) / 2 - median) <= 8]
        if len(gefiltert) >= 3:
            boxen = gefiltert
    boxen.sort(key=lambda b: b[0])
    return boxen


def parse_meter(roh: str, stil: str = "dunkel") -> float | None:
    """Formatwissen nutzen: 'LZ2: 14.1m', 'LZ2: 0.4m', '0000.30 m'.

    Fail-closed per Formvalidator: Nach dem Praefix muss der Kern vollstaendig
    einer der bekannten Formen genuegen — sonst None. Die punktlose Form
    ('01' -> 0.1) gilt nur im Ein-Dezimalen-Layout (dunkel); im Vierziffern-
    Layout waere sie eine Ratelei (aus '0000.30' wird sonst '3.0').
    """
    kern = roh.split("m")[0]
    if kern.startswith("L"):
        kern = kern[1:]
    if kern[:1] in ("Z", "2"):
        kern = kern[1:]
    if kern.startswith(":"):
        kern = kern[1:]
    elif ":" in kern:
        kern = kern.split(":", 1)[1]
    kern = kern.strip("LZ: ?.")
    if not kern or any(c.isalpha() for c in kern):
        return None

    treffer = re.fullmatch(r"(\d{1,3})[.?](\d)", kern)          # 14.1 / 2?1
    if treffer is None:
        treffer = re.fullmatch(r"(\d{4})[.?](\d{1,2})", kern)   # 0007.00
    if treffer is not None:
        wert = float(f"{int(treffer.group(1))}.{treffer.group(2)}")
    else:
        if stil != "dunkel" or re.fullmatch(r"\d{2,3}", kern) is None:
            return None
        wert = float(f"{kern[:-1]}.{kern[-1]}")
    return wert if 0.0 <= wert <= 400.0 else None


def plausibilisiere_sequenz(
    lesungen: list[tuple[float, float | None]],
    max_m_pro_s: float = 5.0,
    fenster_s: float = 10.0,
) -> list[tuple[float, float | None]]:
    """Sequenz-Plausibilitaet pro Video: Ein Wert, der mit keinem Nachbarn in
    der Zeit vertraeglich ist (Kamera faehrt nicht 130 m in einer Sekunde),
    oder der ueber der robusten Videoschlange liegt, wird None — genau wie ein
    unlesbarer Frame. Ein-Frame-Lesung ist zustandslos; die Plausibilitaet
    gehoert der Sequenz.

    lesungen: [(sekunde, meter|None)] aufsteigend. Rueckgabe: gleiche Form.
    """
    punkte = [(s, m) for s, m in lesungen if m is not None]
    if len(punkte) < 2:
        return list(lesungen)
    werte = sorted(m for _, m in punkte)
    median = werte[len(werte) // 2]
    decke = max(4.0 * median, 30.0)  # Haltungslaenge bleibt unbekannt — grosszuegig

    def verdaechtig(index: int, sekunde: float, meter: float) -> bool:
        if meter > decke:
            return True
        nachbarn = [
            (s2, m2) for j, (s2, m2) in enumerate(punkte)
            if j != index and 0 < abs(sekunde - s2) <= fenster_s]
        if not nachbarn:
            return False  # ohne zeitnahen Kontext wird nichts verworfen
        return all(abs(meter - m2) > max_m_pro_s * abs(sekunde - s2)
                   for s2, m2 in nachbarn)

    zu_none = {
        s for i, (s, m) in enumerate(punkte) if m is not None and verdaechtig(i, s, m)}
    return [(s, None if s in zu_none else m) for s, m in lesungen]


def rendere_templates() -> dict[str, list[np.ndarray]]:
    templates: dict[str, list[np.ndarray]] = {}
    for schrift in (r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\arial.ttf"):
        try:
            font = ImageFont.truetype(schrift, TEMPLATE_H)
        except OSError:
            continue
        for zeichen in ZEICHEN:
            img = Image.new("L", (TEMPLATE_H * 2, TEMPLATE_H * 2), 0)
            d = ImageDraw.Draw(img)
            d.text((4, 4), zeichen, fill=255, font=font)
            arr = np.asarray(img)
            ys, xs = np.nonzero(arr > 100)
            if len(ys) == 0:
                continue
            crop = arr[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
            skaliert = np.asarray(
                Image.fromarray(crop).resize(
                    (max(1, round(crop.shape[1] * TEMPLATE_H / crop.shape[0])), TEMPLATE_H),
                    Image.BILINEAR))
            templates.setdefault(zeichen, []).append((skaliert > 100).astype(np.float32))
    return templates


def klassifiziere(glyph: np.ndarray, templates: dict[str, list[np.ndarray]]) -> tuple[str, float]:
    """Jaccard-Aehnlichkeit nach Hoehennormierung; beste Uebereinstimmung."""
    h, w = glyph.shape
    if h < 4 or w < 2:
        return "", 0.0
    norm = np.asarray(
        Image.fromarray((glyph * 255).astype("uint8")).resize(
            (max(1, round(w * TEMPLATE_H / h)), TEMPLATE_H), Image.BILINEAR)) > 100
    norm = norm.astype(np.float32)
    beste_zeichen, beste_wert = "", 0.0
    for zeichen, varianten in templates.items():
        for templ in varianten:
            breite = min(norm.shape[1], templ.shape[1])
            if breite < 2:
                continue
            a = norm[:, :breite]
            b = templ[:, :breite]
            schnitt = float((a * b).sum())
            vereinigung = float(((a + b) > 0).sum())
            wert = schnitt / vereinigung if vereinigung else 0.0
            if wert > beste_wert:
                beste_zeichen, beste_wert = zeichen, wert
    return beste_zeichen, beste_wert


def lese_meter(pfad: Path, templates, debug_dir: Path | None = None):
    with Image.open(pfad) as img:
        img.load()
        bild = img.convert("RGB")
    maske, stil = glyphenmaske(bild)
    boxen = boxen_aus_maske(maske, stil)
    zeichenfolge = ""
    konfidenzen = []
    for (x0, y0, x1, y1) in boxen:
        glyph = maske[y0:y1, x0:x1].astype("float32")
        zeichen, wert = klassifiziere(glyph, templates)
        zeichenfolge += zeichen or "?"
        konfidenzen.append(round(wert, 3))
    treffer = parse_meter(zeichenfolge, stil)
    meter = treffer

    if debug_dir is not None:
        debug_dir.mkdir(parents=True, exist_ok=True)
        d = ImageDraw.Draw(bild)
        for (x0, y0, x1, y1) in boxen:
            d.rectangle([x0, y0, x1, y1], outline=(255, 80, 80))
        d.rectangle([10, 10, 430, 34], fill=(0, 0, 0))
        d.text((14, 14), f"{zeichenfolge}  ->  {meter}  [{stil}]", fill=(0, 255, 0))
        bild.save(debug_dir / pfad.name)
    return {
        "bild": pfad.name,
        "zeichenfolge": zeichenfolge,
        "meter": meter,
        "stil": stil,
        "glyphen": len(boxen),
        "konfidenz_min": min(konfidenzen) if konfidenzen else None,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("bilder", type=Path, nargs="+")
    parser.add_argument("--debug", type=Path, default=None)
    args = parser.parse_args(argv)

    templates = rendere_templates()
    ergebnisse = []
    for pfad in args.bilder:
        e = lese_meter(pfad, templates, args.debug)
        ergebnisse.append(e)
        print(f"{e['bild'][:44]:46s} '{e['zeichenfolge']}'  meter={e['meter']}  "
              f"glyphen={e['glyphen']}  kmin={e['konfidenz_min']}")
    if args.debug:
        (args.debug / "ergebnis.json").write_text(
            json.dumps(ergebnisse, ensure_ascii=False, indent=1), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
