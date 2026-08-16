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
if str(_SIDECAR) not in sys.path:
    sys.path.insert(0, str(_SIDECAR))

from sidecar import osd_meter, osd_modell  # noqa: E402  (Pfad muss vorher stehen)

# Ultralytics' eigener Dokumentations-Default fuer predict(). Explizit
# hingeschrieben statt implizit uebernommen: Ein kuenftiger Ultralytics-Wechsel
# des Bibliotheks-Defaults darf die Kalibrierung nicht lautlos verschieben.
# Gleicher Wert wie bei den anderen Kandidaten-Aufrufern dieses Projekts
# (BCC-Testendpunkt, beide Holdout-Auswerter). Das ist die YOLO-interne
# Box-Vorfilterung, NICHT die Zeichensicherheits-Schwelle: Die kommt erst
# danach explizit ueber "schwelle" - siehe Docstring von baue_modell_leser.
_YOLO_CONF = 0.25


def _sha256(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1 << 20), b""):
            hasher.update(block)
    return hasher.hexdigest()


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


def baue_modell_leser(kandidat: Path, schwelle: float) -> Callable[[Path], dict]:
    """Baut die geteilte Lesefunktion fuer genau diesen Kandidaten.

    Prueft das Gewicht gegen den im Manifest gebundenen SHA-256 BEVOR das
    Modell geladen wird - ein abweichendes Gewicht darf nie unbemerkt laufen.
    Das YOLO-Modell wird genau einmal geladen; die zurueckgegebene Funktion
    liest beliebig viele Bilder mit demselben geladenen Modell.

    Rueckgabe je Bild (Form wie osd_meter.lese_meter):
    {"meter", "zeichenfolge", "stil", "leseweg", "konfidenz_min"}.
    "meter" ist nur dann gesetzt, wenn die Zeichenfolge das Mindesttor
    (osd_modell.TOR_MINDESTZEICHEN) erreicht, die kleinste Zeichensicherheit
    mindestens die uebergebene Schwelle betraegt UND parse_meter daraus einen
    Wert ableiten kann. "stil" ist immer "dunkel": Der Modell-Weg kennt keine
    Maskenheuristik wie der Vorlagenleser und deutet die Folge deshalb immer
    ueber den dunkel-Zweig von parse_meter (siehe Ruling zu Aufgabe 7).
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
    x0, y0, x1, y1 = osd_meter.ZONEN["unten_rechts"]

    def lese(bild_pfad: Path) -> dict:
        with Image.open(bild_pfad) as roh:
            roh.load()
            bild = roh.convert("RGB")
        breite, hoehe = bild.size
        ausschnitt = bild.crop((
            round(x0 * breite), round(y0 * hoehe),
            round(x1 * breite), round(y1 * hoehe),
        ))
        normiert = osd_modell.normiere_ausschnitt(ausschnitt)

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

        folge, kleinste_sicherheit = osd_modell.zu_zeichenfolge(erkennungen)
        konfidenz_min = kleinste_sicherheit if erkennungen else None

        meter = None
        leseweg = None
        if (len(folge) >= osd_modell.TOR_MINDESTZEICHEN
                and konfidenz_min is not None and konfidenz_min >= schwelle):
            wert = osd_meter.parse_meter(folge, "dunkel")
            if wert is not None:
                meter = wert
                leseweg = "modell"

        return {
            "meter": meter,
            "zeichenfolge": folge,
            "stil": "dunkel",
            "leseweg": leseweg,
            "konfidenz_min": konfidenz_min,
        }

    return lese
