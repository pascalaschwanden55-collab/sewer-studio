"""Gemeinsamer Modell-Inferenzpfad des trainierten OSD-Zeichenlesers.

Genau EIN Weg fuehrt von einem Bildpfad zu einer Meterlesung. Aufgabe 7
(Schwellenkalibrierung) und Aufgabe 8 (Goldmessung) verbrauchen ihn beide -
wer diesen Weg zweimal schreibt, riskiert dass beide Messungen leise
auseinanderlaufen (unterschiedliches Cropping, unterschiedliche Normierung,
unterschiedliche parse_meter-Aufrufe), ohne dass das je auffaellt.

Ablauf: Bild oeffnen -> OSD-Zone unten rechts zuschneiden
(sidecar/sidecar/osd_meter.py ZONEN, UNVERAENDERT) -> auf feste Zeichenhoehe
normieren (osd_modell.normiere_ausschnitt) -> mit dem Kandidaten inferieren ->
Boxen zu Erkennungstupeln wandeln -> osd_modell.zu_zeichenfolge baut daraus
die Zeichenkette -> osd_meter.parse_meter deutet sie. Die Deutung selbst wird
hier nicht angefasst.

FAIL-CLOSED: Ein technischer Fehler bei der Inferenz (defektes Bild,
CUDA-Fehler, ein fehlendes Modul, ...) wird NICHT als "nicht gelesen"
verschluckt. Die Projektregel lautet: ein falscher Wert ist teurer als zehn
fehlende - aber ein still verschlucktes None in einer MESSUNG ist ein
simpler Fehlschlag, der die Zahlen unbemerkt schoent. Deshalb wird hier
nirgends try/except um die Inferenz gelegt; jede Ausnahme wird durchgereicht.
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path
from typing import Callable

from PIL import Image

_WURZEL = Path(__file__).resolve().parents[2]
_SIDECAR = _WURZEL / "sidecar"
_SKRIPTE = Path(__file__).resolve().parent
if str(_SIDECAR) not in sys.path:
    sys.path.insert(0, str(_SIDECAR))
if str(_SKRIPTE) not in sys.path:
    sys.path.insert(0, str(_SKRIPTE))

from sidecar import osd_meter, osd_modell  # noqa: E402  (Pfad muss vorher stehen)
import osd_crop  # noqa: E402  (nur fuer den Modul-SHA-256 in code_hashes())

# YOLO-interne Box-Vorfilterung in predict() - NICHT die Zeichensicherheits-
# Schwelle "schwelle" (die kommt erst danach, siehe _ergebnis_aus_erkennungen).
#
# Fix-Runde 1 zu Aufgabe 7 (2026-08-16): Stand hier zuerst auf 0.25 (Ultralytics'
# eigener Default, wie bei den anderen Kandidaten-Aufrufern dieses Projekts).
# Das war falsch: Bei 0.25 hat JEDE zustandekommende Lesung zwangslaeufig
# konfidenz_min >= 0.25, weil predict() alles darunter schon vorher verwirft.
# GRUNDSCHWELLE in osd_schwelle_kalibrieren.py ist ebenfalls 0.25 - findet die
# Kalibrierung keinen groben Fehler (auf 88 Bildern gut moeglich), friert sie
# genau diesen Wert ein, und "konfidenz_min >= schwelle" ist danach trivial
# erfuellt: Das Tor prueft dann nichts mehr, was der Boxfilter nicht schon
# vorher weggenommen hat. Dazu verschwindet ein schwaches Zeichen unter 0.25
# komplett statt die Mindestsicherheit zu senken - die Zeichenfolge wird
# kuerzer und kann TOR_MINDESTZEICHEN trotzdem mit kuenstlich hoher Sicherheit
# erreichen (fail-open, nicht fail-closed). Deshalb jetzt deutlich unter jede
# plausible Schwelle: Die rohe Sicherheit soll fuer die Kalibrierung ueberhaupt
# sichtbar werden (das war der Sinn von schwelle=0.0 beim Faelle-Erzeugen).
# test_yolo_conf_bleibt_unter_der_grundschwelle() (test_osd_modell_leser.py)
# schreibt _YOLO_CONF < GRUNDSCHWELLE fest, damit die beiden nie wieder
# zusammenfallen.
_YOLO_CONF = 0.05


def _sha256(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1 << 20), b""):
            hasher.update(block)
    return hasher.hexdigest()


def zuschnitt_fuer_leser(bild: Image.Image) -> Image.Image:
    """Der Zuschnitt, den lese() vor der Normierung verwendet.

    Oeffentlich (statt in lese() vergraben), damit er unabhaengig vom
    Modell/Ultralytics direkt gegen osd_ernte.zonen_ausschnitt() geprueft
    werden kann (Fix-Runde 1, Aufgabe 3: Ernte und Leser muessen bytegleich
    zuschneiden, siehe osd_crop.py).
    """
    return osd_modell.schneide_zone(bild)[0]


def code_hashes() -> dict[str, str]:
    """SHA-256 der Module, die eine Lesung tatsaechlich bestimmen (Aufgabe 4).

    Gewicht + Schwelle binden nur das MODELL. ZIEL_HOEHE, _IOU_SCHWELLE,
    TOR_MINDESTZEICHEN und _YOLO_CONF (osd_modell.py / dieses Modul), der
    Zuschnitt (osd_crop.py - rundet die ZONEN-Bruchteile aus osd_meter.py auf
    ein Pixelrechteck; DIESE Rundung entscheidet den Ausschnitt, nicht
    osd_meter.py selbst), die RGB->BGR-Kanalumkehr direkt vor der Inferenz
    (yolo_wrapper.py, _pil_rgb_to_ultralytics_bgr) sowie glyphenmaske und
    parse_meter (osd_meter.py) aendern die Lesung mit demselben Gewicht
    ebenso - ohne das ist ein Bericht/eine eingefrorene Schwelle nicht auf
    den Code zurueckfuehrbar, der sie erzeugt hat.

    Fix-Runde 1 (2026-08-16): osd_crop.py fehlte hier trotz der Cropping-
    Konsolidierung (siehe dort) und dieser Docstring zeigte faelschlich auf
    osd_meter.py - eine erneute Rundungsaenderung waere unbemerkt geblieben.
    Gleichzeitig ergaenzt: yolo_wrapper.py war im Lesepfad ebenso ungebunden,
    obwohl genau seine Kanalumkehr schonmal an anderer Stelle im Projekt
    kaputt war (BCC-Endpunkt, siehe CLAUDE.md, Fund 2026-08-09) - derselbe
    Fehler ist hier ebenso moeglich und waere ohne diese Bindung ebenso
    unsichtbar.
    """
    from sidecar.models import yolo_wrapper  # nur fuer den Hash, kein Torch noetig

    return {
        "osd_modell_leser.py": _sha256(Path(__file__)),
        "osd_modell.py": _sha256(Path(osd_modell.__file__)),
        "osd_meter.py": _sha256(Path(osd_meter.__file__)),
        "osd_crop.py": _sha256(Path(osd_crop.__file__)),
        "yolo_wrapper.py": _sha256(Path(yolo_wrapper.__file__)),
    }


def _lade_laufzeit():
    """Ultralytics und der BGR-Helfer - erst bei Bedarf geladen.

    So bleibt dieses Modul fuer reine Logiktests (z. B. die SHA-256-Pruefung)
    importierbar, ohne dass Torch/Ultralytics ueberhaupt beruehrt werden.
    Derselbe BGR-Helfer wie im Sidecar (yolo_wrapper._pil_rgb_to_ultralytics_bgr)
    wird wiederverwendet: Ultralytics behandelt NumPy-Eingaben als BGR, und
    dieser Kanalwechsel darf laut Projektregel nicht ein zweites Mal verloren
    gehen (siehe CLAUDE.md, Fund vom 2026-08-09 am BCC-Endpunkt).
    """
    try:
        from sidecar.models import yolo_wrapper
        from ultralytics import YOLO
    except ImportError as fehler:
        raise RuntimeError(
            "Die KI-Laufzeit fehlt. Bitte mit "
            r".\sidecar\.venv\Scripts\python.exe starten."
        ) from fehler
    return YOLO, yolo_wrapper


def _ergebnis_aus_erkennungen(
    erkennungen: list[tuple[int, float, float, float, float, float]],
    stil: str,
    schwelle: float,
    format: str | None = None,
) -> dict:
    """Reine Logik: aus Erkennungen + Stil + Schwelle wird das Ergebnis-Dict.

    Kein Bild, kein Modell - dadurch ohne YOLO testbar (siehe
    test_osd_modell_leser.py). Bild-I/O und Inferenz bleiben in
    baue_modell_leser.lese(); dort wird nur diese Funktion aufgerufen.

    "meter" ist nur dann gesetzt, wenn die Zeichenfolge das Mindesttor
    (osd_modell.TOR_MINDESTZEICHEN) erreicht, die kleinste Zeichensicherheit
    mindestens die uebergebene Schwelle betraegt UND parse_meter daraus einen
    Wert ableiten kann.
    """
    return osd_modell.ergebnis_aus_erkennungen(
        erkennungen, stil, schwelle, format)


def baue_modell_bildleser(
    kandidat: Path,
    schwelle: float,
) -> Callable[[Image.Image, str | None], dict]:
    """Baut die Lesefunktion fuer bereits geladene PIL-Bilder.

    Prueft das Gewicht gegen den im Manifest gebundenen SHA-256 BEVOR das
    Modell geladen wird. Das YOLO-Modell wird genau einmal geladen; die
    zurueckgegebene Funktion liest beliebig viele Bilder mit demselben Modell.

    Rueckgabe je Bild (Form wie osd_meter.lese_meter):
    {"meter", "zeichenfolge", "stil", "leseweg", "konfidenz_min"} - siehe
    _ergebnis_aus_erkennungen fuer die genauen Regeln.

    "stil" (Fix-Runde 1 zu Aufgabe 7, 2026-08-16 - stand vorher fest auf
    "dunkel", das war geraten statt ermittelt): kommt aus derselben Heuristik
    wie der Vorlagenleser, osd_meter.glyphenmaske(bild) auf dem VOLLEN,
    ungeschnittenen Frame - glyphenmaske wendet ZONEN selbst als Bruchteil der
    uebergebenen Bildgroesse an, ein bereits zugeschnittener Ausschnitt wuerde
    die Zone ein zweites Mal verkleinern. parse_meter entscheidet je nach Stil
    strenger oder lockerer (siehe dort); den Stil weiterhin fest zu "dunkel"
    zu setzen waere fuer die rund die Haelfte der Videos mit anderem Stil
    (helle Schrift auf dunklem Grund oder umgekehrt, siehe die 40er-Sichtung)
    unnoetig streng oder unnoetig locker gewesen.
    """
    kandidat = Path(kandidat)
    manifest = json.loads(
        (kandidat / "manifest.json").read_text(encoding="utf-8-sig"))
    gewicht_pfad = kandidat / manifest["gewicht_datei"]
    erwartet = manifest["gewicht_sha256"]
    tatsaechlich = _sha256(gewicht_pfad)
    if tatsaechlich != erwartet:
        raise ValueError(
            f"Gewichts-SHA-256 stimmt nicht: erwartet {erwartet}, "
            f"gefunden {tatsaechlich} ({gewicht_pfad}).")

    YOLO, yolo_wrapper = _lade_laufzeit()
    modell = YOLO(str(gewicht_pfad))
    imgsz = int(manifest["imgsz"])

    def lese(bild: Image.Image, format: str | None = None) -> dict:
        bild = bild.convert("RGB")
        ausschnitt = zuschnitt_fuer_leser(bild)
        normiert = osd_modell.normiere_ausschnitt(ausschnitt)

        # Auf dem VOLLEN Frame, nicht auf "ausschnitt": glyphenmaske wendet
        # ZONEN selbst als Bruchteil der uebergebenen Bildgroesse an (siehe
        # Docstring oben). Die zurueckgegebene Maske wird hier nicht
        # gebraucht - die Boxen kommen vom YOLO-Kandidaten, nicht aus der
        # Connected-Components-Heuristik des Vorlagenlesers.
        _, stil = osd_meter.glyphenmaske(bild)

        ergebnisse = modell.predict(
            source=yolo_wrapper._pil_rgb_to_ultralytics_bgr(normiert),
            imgsz=imgsz, conf=_YOLO_CONF, verbose=False, save=False,
        )
        boxen = ergebnisse[0].boxes
        erkennungen: list[tuple[int, float, float, float, float, float]] = []
        if boxen is not None:
            for klasse, (x_mitte, y_mitte, box_b, box_h), sicherheit in zip(
                    boxen.cls.tolist(), boxen.xywhn.tolist(), boxen.conf.tolist()):
                erkennungen.append(
                    (int(klasse), x_mitte, y_mitte, box_b, box_h, float(sicherheit)))

        return _ergebnis_aus_erkennungen(erkennungen, stil, schwelle, format)

    return lese


def baue_modell_leser(kandidat: Path, schwelle: float) -> Callable[[Path], dict]:
    """Kompatible Dateipfad-Fassade fuer Kalibrierung und Messwerkzeuge."""
    bild_lesen = baue_modell_bildleser(kandidat, schwelle)

    def lese(bild_pfad: Path) -> dict:
        with Image.open(bild_pfad) as roh:
            roh.load()
            bild = roh.convert("RGB")
        return bild_lesen(bild, None)

    return lese
