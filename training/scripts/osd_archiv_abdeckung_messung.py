"""Misst die OSD-Abdeckung auf einer bestehenden, festen Videoauswahl neu.

Die Videos werden nur gelesen. Alte Berichte werden nie ueberschrieben. Der neue
Bericht bindet Leser, Quelldatei und Video-Metadaten, damit spaeter klar bleibt,
welcher Stand wirklich gemessen wurde.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable, Sequence

from PIL import Image

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "sidecar"))
from sidecar import osd_meter  # noqa: E402

BildLeser = Callable[[Path, int], tuple[list[Image.Image], int]]


def sha256(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1024 * 1024), b""):
            hasher.update(block)
    return hasher.hexdigest()


def gleichmaessige_indizes(anzahl_frames: int, proben: int) -> list[int]:
    if anzahl_frames <= 0 or proben <= 0:
        return []
    if anzahl_frames <= proben:
        return list(range(anzahl_frames))
    return [round(i * (anzahl_frames - 1) / (proben - 1)) for i in range(proben)]


def video_bilder_lesen(video: Path, proben: int) -> tuple[list[Image.Image], int]:
    import cv2

    capture = cv2.VideoCapture(str(video))
    try:
        framezahl = int(capture.get(cv2.CAP_PROP_FRAME_COUNT))
        if not capture.isOpened() or framezahl <= 0:
            raise ValueError("Video konnte nicht geoeffnet oder gezaehlt werden")
        bilder: list[Image.Image] = []
        for index in gleichmaessige_indizes(framezahl, proben):
            capture.set(cv2.CAP_PROP_POS_FRAMES, index)
            ok, frame = capture.read()
            if not ok:
                continue
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            bilder.append(Image.fromarray(rgb))
        return bilder, framezahl
    finally:
        capture.release()


def messe_eintrag(
    eintrag: dict,
    proben: int,
    templates: dict,
    bild_leser: BildLeser = video_bilder_lesen,
) -> dict:
    basis = {"haltung": eintrag.get("haltung"), "gruppe": eintrag.get("gruppe")}
    if eintrag.get("zustand") != "geprueft":
        return {**basis, "zustand": eintrag.get("zustand", "nicht ausgewaehlt")}

    video = Path(str(eintrag.get("video") or ""))
    if not video.is_file():
        return {**basis, "zustand": "fehler", "grund": "Video fehlt", "video": str(video)}

    try:
        bilder, framezahl = bild_leser(video, proben)
        lesungen = [osd_meter.lese_meter(bild, templates) for bild in bilder]
    except Exception as ex:  # Ein defektes Video darf den ganzen Bestand nicht stoppen.
        return {**basis, "zustand": "fehler", "grund": str(ex), "video": str(video)}

    geliefert = [wert for wert in lesungen if wert["meter"] is not None]
    stat = video.stat()
    return {
        **basis,
        "zustand": "geprueft",
        "video": str(video),
        "video_groesse": stat.st_size,
        "video_geaendert_utc": datetime.fromtimestamp(
            stat.st_mtime, tz=timezone.utc).isoformat(),
        "video_frames": framezahl,
        "geprueft": proben,
        "extrahiert": len(bilder),
        "gelesen": len(geliefert),
        "abdeckung": round(len(geliefert) / max(1, len(bilder)), 4),
        "lesungen": [
            {
                "probe": index + 1,
                "meter": wert["meter"],
                "leseweg": wert["leseweg"],
                "zeichenfolge": wert["zeichenfolge"],
                "tesseract_text": wert["tesseract_text"],
            }
            for index, wert in enumerate(lesungen) if wert["meter"] is not None
        ],
    }


def zusammenfassung(ergebnisse: list[dict]) -> dict:
    ausgabe = {}
    for gruppe in sorted({str(e.get("gruppe")) for e in ergebnisse if e.get("gruppe")}):
        geprueft = [e for e in ergebnisse if e.get("gruppe") == gruppe
                    and e.get("zustand") == "geprueft"]
        extrahiert = sum(int(e.get("extrahiert", 0)) for e in geprueft)
        gelesen = sum(int(e.get("gelesen", 0)) for e in geprueft)
        ausgabe[gruppe] = {
            "haltungen": len(geprueft),
            "extrahiert": extrahiert,
            "gelesen": gelesen,
            "frame_abdeckung": round(gelesen / max(1, extrahiert), 4),
            "haltungen_mindestens_70_prozent": sum(
                float(e.get("abdeckung", 0)) >= 0.7 for e in geprueft),
            "haltungen_ohne_lesung": sum(int(e.get("gelesen", 0)) == 0 for e in geprueft),
        }
    return ausgabe


def bericht_bauen(quelle: Path, proben: int, bild_leser: BildLeser = video_bilder_lesen) -> dict:
    daten = json.loads(quelle.read_text(encoding="utf-8-sig"))
    eintraege = list(daten.get("ergebnisse") or [])
    if not eintraege:
        raise ValueError("Die Quelle enthaelt keine Haltungen")
    templates = osd_meter.get_templates()
    ergebnisse = [messe_eintrag(e, proben, templates, bild_leser) for e in eintraege]
    return {
        "schema": "osd_archiv_abdeckung_v2",
        "status": "diagnostic_not_deployed",
        "erstellt_utc": datetime.now(timezone.utc).isoformat(),
        "reader_sha256": sha256(REPO / "sidecar" / "sidecar" / "osd_meter.py"),
        "quelle": str(quelle),
        "quelle_sha256": sha256(quelle),
        "proben_je_haltung": proben,
        "video_byte_hashes": "nicht_berechnet",
        "zusammenfassung": zusammenfassung(ergebnisse),
        "ergebnisse": ergebnisse,
    }


def atomar_neu_schreiben(ziel: Path, bericht: dict) -> None:
    if ziel.exists():
        raise FileExistsError(f"Zieldatei existiert bereits: {ziel}")
    ziel.parent.mkdir(parents=True, exist_ok=True)
    handle, temp_name = tempfile.mkstemp(prefix=f".{ziel.name}.", suffix=".tmp", dir=ziel.parent)
    os.close(handle)
    temp = Path(temp_name)
    try:
        temp.write_text(json.dumps(bericht, indent=2, ensure_ascii=False), encoding="utf-8")
        os.replace(temp, ziel)
    finally:
        temp.unlink(missing_ok=True)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--quelle", type=Path, required=True)
    parser.add_argument("--ziel", type=Path, required=True)
    parser.add_argument("--proben", type=int, default=20)
    args = parser.parse_args(argv)
    if not 1 <= args.proben <= 100:
        raise SystemExit("--proben muss zwischen 1 und 100 liegen")
    bericht = bericht_bauen(args.quelle, args.proben)
    atomar_neu_schreiben(args.ziel, bericht)
    print(json.dumps(bericht["zusammenfassung"], indent=2, ensure_ascii=False))
    print(f"Bericht: {args.ziel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
