"""Guenstiger OSD-Meterleser (Ziffern-OCR ohne Bildmodell).

Liest den Meterstand aus der OSD-Ecke (z. B. „LZ2: 14.1m", „LZ2: 0000.30 m"):
Glyphen segmentieren (helle/dunkle Kleinkomponenten), dann Ziffern per
Template-Matching gegen gerenderte Referenzglyphen klassifizieren.

Heimat der Leser-Logik ist dieses Modul — der Prototyp
`training/scripts/osd_meter_leser.py` delegiert hierher, damit Diagnose und
Sidecar nicht auseinanderlaufen. Validiert am 2026-08-08 gegen 95 menschlich
abgelesene Frames: 71/71 gelieferte Werte richtig (100 %), Abdeckung 76 %
(dominanter Stil 91 %, Vierziffern-Layout 31 %).

Der optionale Format-Lock (`format=`) erzwingt das Zahlenlayout eines Videos.
Gelernt und gesetzt wird er vom Aufrufer (Integrationslogik), nie geraten —
die gescheiterte Sechs-Ziffern-Ratelei vom 2026-08-08 kommt nicht wieder.
"""

from __future__ import annotations

import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

# Meterzone: unten rechts (grosszuegig)
ZONEN = {
    "unten_rechts": (0.62, 0.84, 1.00, 1.00),
}
HELLIGKEIT = 150
GLYPHE_MIN_H, GLYPHE_MAX_H = 8, 40
TEMPLATE_H = 28
ZEICHEN = "0123456789.mLZ:"

# Die Beschriftung beginnt mit einem L ("LZ", "LZ2"). Der Zeichenerkenner setzt
# gelegentlich ein Stoerzeichen davor; bis hierhin wird danach gesucht. Weiter
# hinten gehoert ein L nicht mehr zur Beschriftung.
LABEL_ZEICHEN = "L"
LABEL_SUCHFENSTER = 4

# Bekannte Zahlenlayouts des Meterstands.
FORMAT_AUTO = "auto"
FORMAT_EIN_DEZIMAL = "ein_dezimal"
FORMAT_VIERZIFFERN = "vierziffern"
FORMATE = (FORMAT_AUTO, FORMAT_EIN_DEZIMAL, FORMAT_VIERZIFFERN)


def glyphenmaske(bild: Image.Image) -> tuple["object", str]:
    """Binaermaske mutmasslicher OSD-Zeichen im Meterbereich + Stil.

    Dunkler Text auf hellem Kasten: adaptiv gegen die lokale Umgebung
    (heller Hintergrund nötig), funktioniert auf jeder Bildhelligkeit.
    Heller Text direkt auf dem Video: helle Kleinkomponenten. Rueckgabe ist
    die Vollbild-Maske; die Segmentierung erfolgt in lese_meter.
    """
    import cv2

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


def parse_meter(roh: str, stil: str = "dunkel", format: str | None = None) -> float | None:
    """Formatwissen nutzen: 'LZ2: 14.1m', 'LZ2: 0.4m', '0000.30 m'.

    Fail-closed per Formvalidator: Nach dem Praefix muss der Kern vollstaendig
    einer der bekannten Formen genuegen — sonst None.

    `format` ist der Format-Lock des Aufrufers (Integrationslogik, z. B. pro
    Video gelernt):
    - None/"auto": beide Formen erlaubt (bisheriges Verhalten); die punktlose
      Form ('01' -> 0.1) nur im Ein-Dezimalen-Layout (Stil dunkel).
    - "ein_dezimal": nur die Ein-Dezimalen-Formen; punktlos dann unabhaengig
      vom erkannten Stil erlaubt — das Layout ist ja bekannt.
    - "vierziffern": nur die Vierziffern-Form ('0000.30'); alles andere None.
    Ein unbekannter Wert ist ein Fehler, kein stiller Rueckfall auf auto.

    Praefix-Regel: "LZ"/"LZ2" ist eine Beschriftung, keine Zahl. Steht nach
    einem L sofort eine Ziffer (ausser dem bekannten verlesenen Z "2"), ist
    das eine Verlesung des Z und die Folge wird verworfen.
    """
    if format is None:
        format = FORMAT_AUTO
    if format not in FORMATE:
        raise ValueError(f"Unbekanntes OSD-Meterformat: {format!r}")

    kern = roh.split("m")[0]

    # Fuehrende Stoerzeichen abschneiden, bevor die Praefixregel greift. Der
    # Zeichenerkenner setzt auf HD-Schrift gelegentlich ein Zeichen VOR die
    # Beschriftung ("2L111", "??L122", "???.L10.1"). Steht das L dann nicht mehr
    # am Anfang, wurde die Regel darunter komplett uebersprungen und der Parser
    # las 11,1 statt 1,1 — belegt 2026-08-08 an zwei von fuenf HD-Lesungen.
    # Bewusst nur im vorderen Bereich gesucht: Ein L weiter hinten gehoert nicht
    # zur Beschriftung, und ein blindes Abschneiden bis dorthin wuerde eine
    # gueltige Zahl zerstoeren.
    l_stelle = kern.find(LABEL_ZEICHEN)
    if 0 < l_stelle <= LABEL_SUCHFENSTER:
        kern = kern[l_stelle:]

    if kern.startswith("L"):
        kern = kern[1:]
        # "LZ"/"LZ2" ist eine Beschriftung, keine Zahl: Nach dem L darf nur das
        # Z folgen (oder sein verlesenes "2"). Jede andere Ziffer ist ein
        # verlesenes Z — auf HD-Schrift (1080p, feine Striche) kippt die
        # Vorlage Z zu 1, und aus "LZ 3.2m" wurde sonst "132" = 13,2 statt
        # 3,2 (belegt 2026-08-08: sieben Werte, alle exakt eine Zehnerpotenz
        # zu hoch; die Sequenzpruefung faengt das nicht, weil alle Nachbarn
        # gemeinsam verschoben sind). Verwerfen, nicht raten.
        if kern[:1].isdigit() and kern[:1] != "2":
            return None
    if kern[:1] in ("Z", "2"):
        kern = kern[1:]
    if kern.startswith(":"):
        kern = kern[1:]
    elif ":" in kern:
        kern = kern.split(":", 1)[1]
    kern = kern.strip("LZ: ?.")
    if not kern or any(c.isalpha() for c in kern):
        return None

    treffer = None
    if format in (FORMAT_AUTO, FORMAT_EIN_DEZIMAL):
        treffer = re.fullmatch(r"(\d{1,3})[.?](\d)", kern)          # 14.1 / 2?1
    if treffer is None and format in (FORMAT_AUTO, FORMAT_VIERZIFFERN):
        treffer = re.fullmatch(r"(\d{4})[.?](\d{1,2})", kern)       # 0007.00
    if treffer is not None:
        wert = float(f"{int(treffer.group(1))}.{treffer.group(2)}")
    else:
        if format == FORMAT_VIERZIFFERN:
            return None
        if format == FORMAT_AUTO and stil != "dunkel":
            return None
        if re.fullmatch(r"\d{2,3}", kern) is None:
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


_templates_cache: dict[str, list[np.ndarray]] | None = None


def get_templates() -> dict[str, list[np.ndarray]]:
    """Einmalig gerenderte Referenzglyphen — nicht pro Anfrage neu rendern."""
    global _templates_cache
    if _templates_cache is None:
        _templates_cache = rendere_templates()
    return _templates_cache


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


def lese_meter(
    bild: Image.Image,
    templates: dict[str, list[np.ndarray]],
    format: str | None = None,
    debug_dir: Path | None = None,
    debug_name: str | None = None,
) -> dict:
    """Liest den Meterstand aus einem PIL-Bild.

    Rueckgabe: dict mit meter (float|None), stil, zeichenfolge, glyphen,
    konfidenz_min. Ein None bei meter heisst "nicht lesbar", nie "0,0".
    """
    maske, stil = glyphenmaske(bild)
    boxen = boxen_aus_maske(maske, stil)
    zeichenfolge = ""
    konfidenzen = []
    for (x0, y0, x1, y1) in boxen:
        glyph = maske[y0:y1, x0:x1].astype("float32")
        zeichen, wert = klassifiziere(glyph, templates)
        zeichenfolge += zeichen or "?"
        konfidenzen.append(round(wert, 3))
    meter = parse_meter(zeichenfolge, stil, format)

    if debug_dir is not None and debug_name is not None:
        debug_dir.mkdir(parents=True, exist_ok=True)
        d = ImageDraw.Draw(bild)
        for (x0, y0, x1, y1) in boxen:
            d.rectangle([x0, y0, x1, y1], outline=(255, 80, 80))
        d.rectangle([10, 10, 430, 34], fill=(0, 0, 0))
        d.text((14, 14), f"{zeichenfolge}  ->  {meter}  [{stil}]", fill=(0, 255, 0))
        bild.save(debug_dir / debug_name)
    return {
        "zeichenfolge": zeichenfolge,
        "meter": meter,
        "stil": stil,
        "glyphen": len(boxen),
        "konfidenz_min": min(konfidenzen) if konfidenzen else None,
    }
