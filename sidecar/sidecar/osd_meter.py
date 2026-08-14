"""Guenstiger OSD-Meterleser (Ziffern-OCR ohne Bildmodell).

Liest den Meterstand aus der OSD-Ecke (z. B. „LZ2: 14.1m", „LZ2: 0000.30 m"):
Glyphen segmentieren (helle/dunkle Kleinkomponenten), dann Ziffern per
Template-Matching gegen gerenderte Referenzglyphen klassifizieren.
Der enge Vierziffern-Rueckfall nutzt ein bereits installiertes Tesseract, wenn
die Vorlagenlesung scheitert; er installiert nichts und bleibt optional.

Heimat der Leser-Logik ist dieses Modul — der Prototyp
`training/scripts/osd_meter_leser.py` delegiert hierher, damit Diagnose und
Sidecar nicht auseinanderlaufen. Der aktuelle Diagnosekandidat vom 2026-08-09
liefert im menschlich gelabelten SD-Goldbestand 82/82 Werte richtig. Im
HD- und HD2-Goldbestand liefert er keinen Wert. Die Archivabdeckung ist noch
zu niedrig; deshalb bleibt er ausdruecklich `diagnostic_not_deployed`.

Der optionale Format-Lock (`format=`) erzwingt das Zahlenlayout eines Videos.
Gelernt und gesetzt wird er vom Aufrufer (Integrationslogik), nie geraten —
die gescheiterte Sechs-Ziffern-Ratelei vom 2026-08-08 kommt nicht wieder.
"""

from __future__ import annotations

import re
import shutil
import subprocess
from functools import lru_cache
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

# Meterzone: unten rechts (grosszuegig)
ZONEN = {
    "unten_rechts": (0.62, 0.84, 1.00, 1.00),
}

# Zonen des Tesseract-Rueckfalls, in dieser Reihenfolge versucht. Aus der
# menschlichen Sichtung von 40 Haltungen: 38-mal unten rechts, 2-mal unten
# links, 0-mal oben. Unten rechts bleibt deshalb der erste Versuch.
# Nur unten rechts. Die zweite Zone unten links wurde am 2026-08-09 wieder
# entfernt: Dort steht in vielen Videos das Aufnahmedatum, und Tesseract las
# dann "05.09.2023" statt des Meterstands. Die 2 von 40 Haltungen mit dem
# Meterstand unten links wiegen dieses Risiko nicht auf.
TESSERACT_ZONEN = (
    (0.62, 0.84, 1.00, 1.00),
)
HELLIGKEIT = 150
GLYPHE_MIN_H, GLYPHE_MAX_H = 8, 40
TEMPLATE_H = 28

# Ziffernhoehe des SD-Bezugsfalls. Alle Pixelschranken der Zeichenfindung sind
# darauf eingestellt; groessere Aufloesungen werden ueber glyphen_skala daran
# angeglichen, statt fuer jeden Stil eigene Werte zu pflegen.
REFERENZ_GLYPHE_H = 18
# Bewusst OHNE Minus: Als eigenes Zeichen aufgenommen, passten auf HD zu viele
# andere Striche darauf — es kostete sieben richtige Lesungen und rettete eine.
# Der negative Zaehlerstand vor dem Rohranfang wird stattdessen als mehrdeutig
# erkannt und nicht gelesen (siehe Doppelpunkt-Regel in parse_meter).
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

TESSERACT_WHITELIST = "LZ0123456789:+-.m"


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


def glyphen_skala(hoehen: list[int]) -> float:
    """Wie viel groesser die Zeichen sind als im SD-Bezugsfall.

    Alle Abstandsschranken hier waren feste Pixelwerte, eingestellt auf SD mit
    rund 18 Pixel hohen Ziffern. Auf HD sind dieselben Zeichen doppelt so gross —
    dann liegen Nachbarfenster, Doppelpunkt-Abstand und Zeilentoleranz zu eng, und
    der Leser verliert Dezimalpunkt und Einheit. Deshalb werden die Schranken an
    der tatsaechlichen Zeichenhoehe ausgerichtet.

    Nach unten wird NIE skaliert: Fuer SD bleibt alles bitgenau wie bisher, die
    Aenderung kann dort also nichts verschlechtern.
    """
    kandidaten = sorted(h for h in hoehen if h >= GLYPHE_MIN_H)
    if not kandidaten:
        return 1.0

    # Oberes Fuenftel: Die Ziffern sind die hohen Zeichen; kleinere Bestandteile
    # (Teilstriche, Rauschen) sollen den Bezug nicht nach unten ziehen.
    bezug = kandidaten[int(len(kandidaten) * 0.8)] if len(kandidaten) > 1 else kandidaten[0]
    return max(1.0, min(4.0, bezug / REFERENZ_GLYPHE_H))


def boxen_aus_maske(maske, stil: str) -> list[tuple[int, int, int, int]]:
    import cv2

    n, _lab, stats, _ = cv2.connectedComponentsWithStats(maske, 8)
    komponenten = [tuple(int(v) for v in stats[i][:5]) for i in range(1, n)]
    skala = glyphen_skala([bh for _x, _y, _bw, bh, _f in komponenten])

    def sk(wert: int) -> int:
        return max(wert, int(round(wert * skala)))

    boxen = []
    punkte = []
    min_flaeche = 3 if stil in ("dunkel", "dunkel_video") else 8
    min_h = GLYPHE_MIN_H - 2 if stil in ("dunkel", "dunkel_video") else GLYPHE_MIN_H
    for x, y, bw, bh, flaeche in komponenten:
        if 2 <= bw <= sk(7) and 2 <= bh <= sk(7) and flaeche >= 3:
            punkte.append((x, y, x + bw, y + bh))  # Dezimalpunkt/Doppelpunkt-Punkt
        elif (min_h <= bh <= sk(GLYPHE_MAX_H) and 2 <= bw <= sk(26)
                and min_flaeche <= flaeche <= bw * bh * 0.95):
            boxen.append((x, y, x + bw, y + bh))
    # vertikal gestapelte Punkte zu einem Doppelpunkt zusammenfassen;
    # Punkte ohne Glyphen-Nachbarn im Abstandsfenster sind Rauschen und fallen weg.
    punkte.sort(key=lambda b: (b[0], b[1]))
    nachbarfenster = sk(22)
    def hat_nachbarn(p: tuple[int, int, int, int]) -> bool:
        return any(abs(g[0] - p[0]) <= nachbarfenster for g in boxen)
    punkte = [p for p in punkte if hat_nachbarn(p) and p[3] - p[1] >= 2]
    verbraucht: set[int] = set()
    for i, p in enumerate(punkte):
        if i in verbraucht:
            continue
        for j in range(i + 1, len(punkte)):
            q = punkte[j]
            if (j not in verbraucht and abs(p[0] - q[0]) <= sk(3)
                    and sk(3) <= q[1] - p[3] <= sk(10)):
                boxen.append((p[0], p[1], max(p[2], q[2]), q[3]))  # ':'
                verbraucht.add(j)
                break
        else:
            boxen.append(p)  # '.'
        verbraucht.add(i)
    # Zeilenkohärenz: OSD-Text steht in einer Zeile; was zu weit vom Median der
    # Zeichenmitten abweicht, ist Textur und faellt weg.
    if len(boxen) >= 4:
        mitten = sorted((b[1] + b[3]) / 2 for b in boxen)
        median = mitten[len(mitten) // 2]
        zeilentoleranz = sk(8)
        gefiltert = [b for b in boxen if abs((b[1] + b[3]) / 2 - median) <= zeilentoleranz]
        if len(gefiltert) >= 3:
            boxen = gefiltert
    boxen.sort(key=lambda b: b[0])
    return boxen


def parse_meter(roh: str, stil: str = "dunkel", format: str | None = None,
                erlaube_zwei_dezimal: bool = False) -> float | None:
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

    # Nach der Einheit darf keine Ziffer mehr stehen. Bisher wurde alles hinter dem
    # ersten "m" stillschweigend weggeworfen — aus 'LZ:::6.4m3' wurde so 6,4, obwohl
    # der Sollwert 26,4 war: Die fuehrende 2 war zu ':' verlesen und die verirrte 3
    # verriet, dass die ganze Erkennung unzuverlaessig war. 20 Meter daneben, und
    # nichts deutete darauf hin. Ein Rest mit Ziffer heisst jetzt: verwerfen.
    kern, trenner, rest = roh.partition("m")
    if trenner and any(c.isdigit() for c in rest):
        return None

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

    hatte_label = kern.startswith("L")
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
    # Ein fuehrendes Z (oder sein verlesenes "2") gehoert nur dann zur
    # Beschriftung, wenn es auch einen Beschriftungskontext gibt: entweder ein
    # bereits abgetrenntes "L" ("L232" = LZ + 32 = 3,2) oder ein unmittelbar
    # folgender Doppelpunkt. Ohne diese Bedingung verlor jede Zahl OHNE
    # Beschriftung, die mit 2 beginnt, ihre erste Stelle: "22.20" wurde zu 2,20.
    # Sichtbar wurde das erst mit der Form "zwei Nachkommastellen"; vorher fiel
    # eine solche Zahl schon eine Stufe frueher durch.
    if kern[:1] in ("Z", "2") and (hatte_label or ":" in kern[1:3]):
        kern = kern[1:]
    if kern.startswith(":"):
        kern = kern[1:]
    elif ":" in kern:
        kern = kern.split(":", 1)[1]
    # Mehr als ein Punkt heisst: mindestens ein Zeichen wurde verlesen. Genau so
    # entstand aus dem Minus von "LZ1: -0.1m" die Folge ".0.1" — der fuehrende
    # Punkt wurde abgestreift und aus -0,1 wurde 0,1. Ein falscher Meterstand
    # wandert unbemerkt ins Protokoll; ein fehlender faellt auf.
    if kern.count(".") >= 2:
        return None
    kern = kern.strip("LZ: ?. +")
    if not kern or any(c.isalpha() for c in kern):
        return None

    treffer = None
    if format in (FORMAT_AUTO, FORMAT_EIN_DEZIMAL):
        treffer = re.fullmatch(r"(\d{1,3})[.?](\d)", kern)          # 14.1 / 2?1
    if treffer is None and format in (FORMAT_AUTO, FORMAT_VIERZIFFERN):
        treffer = re.fullmatch(r"(\d{4})[.?](\d{1,2})", kern)       # 0007.00
    if treffer is None and erlaube_zwei_dezimal:
        # Zwei Nachkommastellen: '0.20', '1.54', '22.20'. Diese Form fehlte, und
        # genau daran scheiterten alle vier Bilder, die am 2026-08-09 mit blossem
        # Auge geprueft wurden (1,54 m und 22,20 m auf 88218-88316, 0,20 m auf
        # 7623-7622, 0,30 m im Graukasten).
        #
        # Ausdruecklich NUR fuer OCR-Text freigeschaltet. Auf dem Vorlagenweg
        # kostete sie drei belegte Goldwerte: `LZ2:1?61m` wurde zu 1,61 statt
        # 1,9, `L.Z:031.10m.` zu 31,1 statt 1,4. Der Zeichenerkenner verliert
        # dort Stellen, und die zusaetzliche Form gab dem Ergebnis einen Weg
        # nach draussen. Der Dezimalpunkt muss echt sein: ein `?` ist ein nicht
        # erkanntes Zeichen, kein Punkt.
        treffer = re.fullmatch(r"(\d{1,3})\.(\d{2})", kern)         # 1.54 / 22.20
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


@lru_cache(maxsize=1)
def _tesseract_pfad() -> str | None:
    """Vorhandenes lokales Tesseract finden; niemals etwas installieren."""
    return shutil.which("tesseract")


def _zeichenfolge_ist_vollstaendig(zeichenfolge: str) -> bool:
    """Traegt die Vorlagenlesung Beschriftung UND Einheit?

    Die Einblendung lautet vollstaendig `LZ2: 14.1m`. Erkennt der Vorlagenweg
    Anker und Einheit, steht die Stellenzahl fest und der Dezimalpunkt ist
    gesetzt. Fehlen beide und es bleiben nackte Ziffern wie `058` oder `2?73`,
    muss die Stellenzahl geraten werden — genau dort entstehen die Fehler
    `0,58 -> 5,80` und `2,73 -> 7,30`.

    Die Einheit muss nur VORKOMMEN, nicht am Ende stehen: Der Zeichenerkenner
    haengt gelegentlich ein Stoerzeichen an (`L:::0007.00m.7` ist eine richtige
    Lesung von 7,00 m). Eine Pruefung auf das Zeilenende verwarf genau diesen
    belegten Goldwert.

    Nach dem erkannten Label `L2`/`LZ2` muss dagegen ein Trenner stehen. Folgt
    sofort eine weitere Ziffer, ist nicht erkennbar, ob die erste Wertziffer
    verloren ging. Beleg: `L211.7m1.` wurde als 11,7 m geliefert, im Bild stehen
    aber 13,7 m. Dieser Fall muss unlesbar bleiben statt geraten zu werden.
    """
    zeichenfolge = zeichenfolge or ""
    if "m" not in zeichenfolge:
        return False
    if re.search(r"L(?:Z2|2)\d", zeichenfolge):
        return False
    return LABEL_ZEICHEN in zeichenfolge or ":" in zeichenfolge


def _ist_vierziffern_kandidat(binaer: np.ndarray) -> bool:
    """Nur eine schmale helle Zeichenzeile darf den Prozessstart ausloesen."""
    import cv2

    heller_anteil = float((binaer > 0).mean())
    if not 0.015 <= heller_anteil <= 0.4:
        return False
    anzahl, _labels, stats, _zentren = cv2.connectedComponentsWithStats(
        (binaer > 0).astype("uint8"), 8)
    zeichen = sum(
        1 for i in range(1, anzahl)
        if 6 <= stats[i, 3] <= 40 and 2 <= stats[i, 2] <= 30 and stats[i, 4] >= 3)
    return 8 <= zeichen <= 30


def _tesseract_aufrufen(executable: str, binaer: np.ndarray,
                        whitelist: str | None = None,
                        skalieren: bool = True) -> str:
    """Ein Tesseract-Lauf auf einer fertigen Schwarz-Weiss-Zeile.

    `skalieren=False` fuer Aufrufer, die schon selbst vergroessert haben —
    sonst wird zweimal skaliert.
    """
    import cv2

    if skalieren:
        binaer = cv2.resize(binaer, None, fx=3, fy=3, interpolation=cv2.INTER_CUBIC)
    ok, png = cv2.imencode(".png", binaer)
    if not ok:
        return ""

    startinfo = None
    if hasattr(subprocess, "STARTUPINFO"):
        startinfo = subprocess.STARTUPINFO()
        startinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    try:
        ergebnis = subprocess.run(
            [executable, "stdin", "stdout", "--psm", "7", "-l", "eng",
             "-c", f"tessedit_char_whitelist={whitelist or TESSERACT_WHITELIST}"],
            input=png.tobytes(), stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
            timeout=2.0, check=False, startupinfo=startinfo,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
    except (OSError, subprocess.SubprocessError):
        return ""
    if ergebnis.returncode != 0:
        return ""
    return " ".join(ergebnis.stdout.decode("utf-8", errors="ignore").split())


def _zeilenkandidaten(ausschnitt: np.ndarray) -> list[np.ndarray]:
    """Schwarz-weisse Zeichenzeilen aus einem Zonenausschnitt, Text immer weiss.

    Beide Polaritaeten, weil das Archiv beide kennt: Auf 40 menschlich
    gesichteten Haltungen standen 18-mal helle Zeichen auf dunklem Grund und
    18-mal dunkle Zeichen auf hellem Kasten. Eine feste Helligkeitsschwelle
    sieht immer nur die eine Haelfte.
    """
    import cv2

    if ausschnitt.size == 0:
        return []
    grau = cv2.cvtColor(ausschnitt, cv2.COLOR_RGB2GRAY)
    kandidaten = []
    # 1) heller Text auf dunklem Grund (bisheriger Weg, unveraendert)
    _s, hell = cv2.threshold(grau, 180, 255, cv2.THRESH_BINARY)
    kandidaten.append(hell)
    # 2) dunkler Text auf hellem Kasten — invertiert, Schwelle aus dem Bild
    #    selbst (Otsu), weil die Kastenhelligkeit je Geraet schwankt.
    _s, dunkel = cv2.threshold(grau, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    kandidaten.append(dunkel)
    return [k for k in kandidaten if _ist_vierziffern_kandidat(k)]


def _lese_vierziffern_mit_tesseract(bild: Image.Image) -> tuple[float | None, str]:
    """Enger Rueckfallweg fuer die vollstaendige Form `LZ1: + 0000.00 m`.

    Tesseract sieht die echte Geraeteschrift besser als die Arial-Vorlagen. Nur
    ein vollstaendiges Vierziffern-Ergebnis wird akzeptiert; alles andere bleibt
    "nicht gelesen". Fehlt Tesseract, laeuft der bisherige Leser unveraendert
    weiter.

    Versucht werden beide Polaritaeten und beide unteren Ecken. Abgebrochen
    wird beim ersten vollstaendigen Ergebnis, damit im Regelfall genau ein
    Prozess startet.
    """
    executable = _tesseract_pfad()
    if executable is None:
        return None, ""

    arr = np.asarray(bild.convert("RGB"))
    h, w = arr.shape[:2]
    letzter_text = ""
    for zone in TESSERACT_ZONEN:
        x0, y0, x1, y1 = zone
        ausschnitt = arr[round(y0 * h):round(y1 * h), round(x0 * w):round(x1 * w)]
        for binaer in _zeilenkandidaten(ausschnitt):
            text = _tesseract_aufrufen(executable, binaer)
            if not text:
                continue
            letzter_text = text
            # Bewusst fest `vierziffern`. Eine Lockerung auf `auto` mit der
            # Zwei-Dezimal-Form wurde am 2026-08-09 gemessen und wieder
            # entfernt: Sie brachte 8 zusaetzliche Werte, davon 6 grob falsch,
            # und oeffnete zugleich den Weg, ein Datum ("05.09.2023") als
            # Meterstand zu lesen.
            meter = parse_meter(text, stil="hell", format=FORMAT_VIERZIFFERN)
            if meter is not None:
                return meter, text
    return None, letzter_text


# --- Zusatzweg: zwei Nachkommastellen auf hellem OSD-Kasten -----------------
#
# Vier der fuenf am 2026-08-09 einzeln untersuchten Archivstile sind derselbe
# Fall: dunkle Ziffern auf hellem Kasten unten rechts, Form `NN.NN m` oder
# `NN,NN m`. Sie scheitern alle an derselben Stelle — an der EINEN globalen
# Otsu-Schwelle. Liegt sie zu hoch, verschmilzt der Mittelpunkt der punktierten
# Geraetenull mit ihrem Ring, und Tesseract liest daraus eine 6 oder 8. Genau
# daher stammen die mit dem Auge bestaetigten Grobfehler 0,20 -> 8,26,
# 0,00 -> 8,00, 0,43 -> 6,43 und 9,00 -> 9,06.
#
# Dieser Weg ist ausdruecklich ADDITIV. Er laeuft nur, wenn Vorlagenweg UND
# Vierziffern-Rueckfall nichts liefern. Als Ersatz gemessen kostete jedes
# Rezept 10 bis 11 belegte Goldwerte, und zwar strukturell: Der SD-Goldbestand
# traegt `LZ2: 0000.30 m`, und eine Form mit hoechstens drei Vorkommastellen
# kann eine vierstellige Zahl nie treffen.
# Schwellenband, gemessen am 2026-08-09 ueber elf Anteile auf drei Bildern:
# Der richtige Wert steht durchgaengig im TIEFEN Band. Ab etwa 0,48 kippt die
# punktierte Geraetenull zur Acht (0,20 -> 020, 22,20 -> 22,28, 0,30 -> 0,38).
# Ein hoeher liegendes Band gab dem Fehler sogar die Mehrheit.
ZWEI_DEZIMAL_ANTEILE = (0.30, 0.34, 0.38, 0.42, 0.46)
ZWEI_DEZIMAL_QUORUM = 3
ZWEI_DEZIMAL_WHITELIST = "0123456789.,m"
# Vollstring-Anker MIT Einheitspflicht. Die Einheit ist die einzige Sperre
# gegen Datumsbruchstuecke: Ein angeschnittenes Datumsfeld liefert Texte wie
# `.10.24`, `16.24` oder `06.24`, die ohne diese Pflicht als Meterstaende
# 10,24 / 16,24 / 6,24 durchgingen. Das Komma gehoert dazu, weil eine
# Geraetefamilie `22,20 m` schreibt.
ZWEI_DEZIMAL_FORM = re.compile(r"(\d{1,3})[.,](\d{2})\s*m")


def _zwei_dezimal_masken(ausschnitt: np.ndarray) -> list[np.ndarray]:
    """Schwellenfaecher statt einer Schwelle, Text jeweils weiss.

    Die Schwellen sind Anteile des 95. Perzentils der Zone, nicht feste
    Grauwerte — die Kastenhelligkeit schwankt je Geraet und Bildinhalt. Bei den
    niedrigen Schwellen ist der Mittelpunkt der punktierten Null gar nicht erst
    Vordergrund; genau darauf beruht das Quorum weiter unten.
    """
    import cv2

    if ausschnitt.size == 0:
        return []
    grau = cv2.cvtColor(ausschnitt, cv2.COLOR_RGB2GRAY)
    bezug = float(np.percentile(grau, 95))
    if bezug < 60.0:
        return []
    masken = []
    for anteil in ZWEI_DEZIMAL_ANTEILE:
        schwelle = int(round(bezug * anteil))
        if not 1 <= schwelle <= 254:
            continue
        _s, maske = cv2.threshold(grau, schwelle, 255, cv2.THRESH_BINARY_INV)
        masken.append(maske)
    return masken


def _zwei_dezimal_zeile(maske: np.ndarray) -> np.ndarray | None:
    """Isoliert die Zeichenzeile und wirft alles andere weg.

    Ohne diesen Schritt liefern Rohrkante und Textur Phantomzeichen. Behalten
    wird nur, was in Groesse und Zeilenlage zusammenpasst.
    """
    import cv2

    anzahl, marken, stats, _zentren = cv2.connectedComponentsWithStats(
        (maske > 0).astype("uint8"), 8)
    kandidaten = [
        i for i in range(1, anzahl)
        if 6 <= stats[i, 3] <= 40 and 2 <= stats[i, 2] <= 30 and stats[i, 4] >= 3
    ]
    if len(kandidaten) < 3:
        return None
    mitten = sorted(stats[i, 1] + stats[i, 3] / 2 for i in kandidaten)
    median = mitten[len(mitten) // 2]
    zeile = [i for i in kandidaten if abs(stats[i, 1] + stats[i, 3] / 2 - median) <= 8]
    if len(zeile) < 3:
        return None

    # Der Dezimalpunkt ist kleiner als jedes Zeichen und faellt durch die
    # Groessenpruefung. Ohne ihn wird aus "0.20" die Zahl "020" und aus
    # "22,20" die Zahl "2220" — beides fiel dann zu Recht durch den Parser.
    # Behalten wird ein kleiner Fleck nur, wenn er auf der Zeile sitzt und
    # dicht neben einem echten Zeichen steht. Der Innenpunkt der punktierten
    # Geraetenull wird dabei nicht mitgenommen: Er liegt VOLLSTAENDIG in der
    # Box seines Zeichens, ein echter Dezimalpunkt liegt zwischen zwei Zeichen.
    def liegt_in_zeichen(i: int) -> bool:
        x, y, bw, bh = stats[i, 0], stats[i, 1], stats[i, 2], stats[i, 3]
        for j in zeile:
            zx, zy, zbw, zbh = stats[j, 0], stats[j, 1], stats[j, 2], stats[j, 3]
            if zx <= x and zy <= y and x + bw <= zx + zbw and y + bh <= zy + zbh:
                return True
        return False

    unterkante = max(stats[i, 1] + stats[i, 3] for i in zeile)
    satzzeichen = [
        i for i in range(1, anzahl)
        if i not in zeile
        and 1 <= stats[i, 2] <= 5 and 1 <= stats[i, 3] <= 6 and 2 <= stats[i, 4] <= 20
        # auf der Grundlinie, nicht irgendwo im Bild
        and abs(stats[i, 1] + stats[i, 3] - unterkante) <= 4
        and any(abs(stats[i, 0] - stats[j, 0]) <= 25 for j in zeile)
        and not liegt_in_zeichen(i)
    ]

    rein = np.zeros_like(maske)
    for i in zeile + satzzeichen:
        rein[marken == i] = 255
    return rein


def _parse_zwei_dezimal(text: str) -> float | None:
    """Nur `NN.NN m` / `NN,NN m` auf der ganzen Zeile — sonst nichts.

    Bewusst ein eigener, enger Parser statt einer Lockerung von `parse_meter`:
    Die dortige Zwei-Dezimal-Form ohne Einheitspflicht las am 2026-08-09
    Datumsbruchstuecke als Meterstaende.
    """
    treffer = ZWEI_DEZIMAL_FORM.fullmatch(" ".join((text or "").split()))
    if treffer is None:
        return None
    wert = float(f"{int(treffer.group(1))}.{treffer.group(2)}")
    return wert if 0.0 <= wert <= 400.0 else None


def _lese_zwei_dezimal_mit_tesseract(bild: Image.Image) -> tuple[float | None, str]:
    """Zusatzweg fuer den hellen OSD-Kasten. Nur bei Quorum ein Ergebnis.

    Gueltig ist nur ein Wert, den mindestens drei der fuenf Schwellen gleich
    lesen. Das ist die Sperre gegen den Punktnull-Fehler: Er tritt nur in einem
    Teil des Schwellenbands auf und findet dort keine Mehrheit.
    """
    executable = _tesseract_pfad()
    if executable is None:
        return None, ""

    import cv2

    arr = np.asarray(bild.convert("RGB"))
    h, w = arr.shape[:2]
    x0, y0, x1, y1 = TESSERACT_ZONEN[0]
    ausschnitt = arr[round(y0 * h):round(y1 * h), round(x0 * w):round(x1 * w)]

    stimmen: dict[float, int] = {}
    letzter_text = ""
    for maske in _zwei_dezimal_masken(ausschnitt):
        zeile = _zwei_dezimal_zeile(maske)
        if zeile is None:
            continue
        gross = cv2.resize(zeile, None, fx=3, fy=3, interpolation=cv2.INTER_CUBIC)
        gross = cv2.copyMakeBorder(gross, 14, 14, 14, 14, cv2.BORDER_CONSTANT, value=0)
        text = _tesseract_aufrufen(executable, gross,
                                   whitelist=ZWEI_DEZIMAL_WHITELIST, skalieren=False)
        if not text:
            continue
        letzter_text = text
        wert = _parse_zwei_dezimal(text)
        if wert is not None:
            stimmen[wert] = stimmen.get(wert, 0) + 1

    if not stimmen:
        return None, letzter_text
    wert, anzahl = max(stimmen.items(), key=lambda p: (p[1], -p[0]))
    if anzahl < ZWEI_DEZIMAL_QUORUM:
        return None, letzter_text
    return wert, letzter_text


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
    leseweg = "vorlagen" if meter is not None else None
    tesseract_text = ""
    if meter is None and format != FORMAT_EIN_DEZIMAL:
        meter, tesseract_text = _lese_vierziffern_mit_tesseract(bild)
        if meter is not None:
            leseweg = "tesseract_vierziffern"
    elif (meter is not None
          and format != FORMAT_EIN_DEZIMAL
          and not _zeichenfolge_ist_vollstaendig(zeichenfolge)):
        # Bruchstueck-Lesung: Der Vorlagenweg hat Ziffern erkannt, aber weder
        # Beschriftung noch Einheit. Dann steht die Stellenzahl nicht fest und
        # der Dezimalpunkt wird geraten — auf 897 beschrifteten Archivbildern
        # waren 58 von 61 solcher Werte grob falsch (5 % richtig), gegen 67 %
        # bei vollstaendigem Muster. Deshalb hier nachfragen und lieber nichts
        # liefern als raten. Eine vollstaendige Lesung wird nie angetastet:
        # Der gepruefte Goldbestand laeuft ausschliesslich ueber diesen Zweig.
        ersatz, tesseract_text = _lese_vierziffern_mit_tesseract(bild)
        meter = ersatz
        leseweg = "tesseract_vierziffern" if ersatz is not None else None

    if meter is None and format in (None, FORMAT_AUTO):
        # Letzter Weg: heller OSD-Kasten mit zwei Nachkommastellen. Strikt
        # additiv — er laeuft nur, wenn beide Wege davor nichts geliefert
        # haben, und er feuert auf allen 94 menschlich abgelesenen Goldbildern
        # null Mal. Ein Format-Lock sperrt ihn, denn er kennt nur diese eine
        # Form.
        zwei, zwei_text = _lese_zwei_dezimal_mit_tesseract(bild)
        if zwei is not None:
            meter = zwei
            leseweg = "tesseract_zwei_dezimal"
            tesseract_text = zwei_text or tesseract_text

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
        "leseweg": leseweg,
        "tesseract_text": tesseract_text,
    }
