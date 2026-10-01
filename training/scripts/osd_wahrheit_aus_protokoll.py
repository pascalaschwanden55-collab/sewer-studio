"""Baut schwach beschriftete OSD-Bilder aus PDF-Meter und Videozaehlerstand.

Kundenoriginale werden nur gelesen. Gleiche Haltungen bleiben im selben
Trainings-, Validierungs- oder Pruefteil. Bytegleiche Bilder werden nur einmal
verwendet. Der Bestand entsteht zuerst in einem Arbeitsordner und wird erst nach
vollstaendiger Erstellung veroeffentlicht; ein vorhandenes Ziel bleibt erhalten.

Die PDF-Werte sind automatisch zugeordnete schwache Labels, keine menschliche
Ablesung. Zeitversatz und einzelne falsche PDF-Zuordnungen bleiben moeglich.
Darum bleibt der Bestand als "qa_offen" gekennzeichnet.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import uuid
from pathlib import Path
from typing import Sequence

sys.path.insert(0, str(Path(__file__).resolve().parent))

PDF_SCAN = Path(r"C:\Sewer-Studio_KI_4.5\.tmp\bcc-code-scan-20260809\bcc_positions_guarded.json")
ZIEL = Path(r"C:\KI_BRAIN\training\diagnostics\osd_wahrheit_protokoll_v1")
KUNDEN_ROOT = Path(r"D:\Haltungen")
FFMPEG = Path(
    r"C:\Users\Besitzer\AppData\Local\Microsoft\WinGet\Packages"
    r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-8.0.1-full_build\bin\ffmpeg.exe"
)

SICHERE_ZUORDNUNG = {"exact_stem_same_folder", "single_in_pdf_folder"}
SPLITS = ("train", "validation", "test")


def sha256_datei(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1 << 20), b""):
            hasher.update(block)
    return hasher.hexdigest()


def liegt_unter(pfad: Path, wurzel: Path) -> bool:
    try:
        pfad.resolve().relative_to(wurzel.resolve())
        return True
    except ValueError:
        return False


def haltungsvarianten(haltung: str) -> set[str]:
    normal = haltung.strip().lower()
    teile = normal.split("-", 1)
    return {normal, f"{teile[1]}-{teile[0]}"} if len(teile) == 2 else {normal}


def physische_haltung(haltung: str) -> str:
    """Beide Fahrtrichtungen erhalten denselben Gruppenschluessel."""
    return min(haltungsvarianten(haltung))


def gesperrte_haltungen_laden(pfad: Path | None) -> set[str]:
    if pfad is None:
        return set()
    if not pfad.is_file():
        raise SystemExit(f"Ausschlussdatei fehlt: {pfad}")
    daten = json.loads(pfad.read_text(encoding="utf-8-sig"))
    werte = daten.get("gesperrt") or daten.get("haltungen") or []
    gesperrt: set[str] = set()
    for wert in werte:
        gesperrt.update(haltungsvarianten(str(wert)))
    return gesperrt


def faelle_aus_scan(scan: dict, gesperrt: set[str]) -> tuple[list[dict], dict[str, int]]:
    faelle: list[dict] = []
    zaehler = {
        "haltung_gesperrt": 0,
        "video_unsicher": 0,
        "position_unvollstaendig": 0,
    }
    for eintrag in scan.get("ergebnisse") or []:
        haltung = str(eintrag.get("haltung") or "").strip()
        if haltungsvarianten(haltung) & gesperrt:
            zaehler["haltung_gesperrt"] += 1
            continue
        for position in eintrag.get("positionen") or []:
            if position.get("video_match") not in SICHERE_ZUORDNUNG:
                zaehler["video_unsicher"] += 1
                continue
            if (position.get("video_counter_seconds") is None
                    or position.get("meter_start") is None
                    or not position.get("video_path")):
                zaehler["position_unvollstaendig"] += 1
                continue
            faelle.append({"haltung": haltung, **position})
    faelle.sort(key=lambda f: (
        f["haltung"], float(f["video_counter_seconds"]), str(f.get("code") or ""),
        str(f.get("source_pdf") or ""), int(f.get("source_page") or 0)))
    return faelle, zaehler


def split_fuer_haltung(haltung: str, saat: str) -> str:
    schluessel = physische_haltung(haltung)
    wert = int(hashlib.sha256(f"{saat}|{schluessel}".encode()).hexdigest()[:8], 16) % 100
    if wert < 70:
        return "train"
    if wert < 85:
        return "validation"
    return "test"


def bildname(fall: dict) -> str:
    roh = "|".join((
        str(fall.get("haltung") or ""),
        str(fall.get("video_path") or ""),
        f"{float(fall['video_counter_seconds']):.3f}",
        str(fall.get("code") or ""),
        str(fall.get("source_pdf") or ""),
        str(fall.get("source_page") or ""),
        f"{float(fall['meter_start']):.3f}",
    ))
    kurz = hashlib.sha256(roh.encode("utf-8")).hexdigest()[:16]
    sicher = "".join(zeichen if zeichen.isalnum() else "_" for zeichen in fall["haltung"])
    return f"{sicher}_{kurz}.jpg"


def ffmpeg_finden(vorgabe: Path | None) -> Path:
    if vorgabe is not None:
        if vorgabe.is_file():
            return vorgabe
        raise SystemExit(f"ffmpeg fehlt: {vorgabe}")
    gefunden = shutil.which("ffmpeg")
    if gefunden:
        return Path(gefunden)
    if FFMPEG.is_file():
        return FFMPEG
    raise SystemExit("ffmpeg nicht gefunden; bitte mit --ffmpeg angeben.")


def bild_extrahieren(ffmpeg: Path, video: Path, sekunde: float, ziel: Path) -> bool:
    lauf = subprocess.run(
        [str(ffmpeg), "-v", "error", "-y", "-ss", f"{sekunde:.3f}", "-i", str(video),
         "-frames:v", "1", "-q:v", "2", str(ziel)],
        capture_output=True,
        text=True,
        timeout=2 * 60,
    )
    return lauf.returncode == 0 and ziel.is_file() and ziel.stat().st_size > 0


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scan", type=Path, default=PDF_SCAN)
    parser.add_argument("--ziel", type=Path, default=ZIEL)
    parser.add_argument("--kunden-root", type=Path, default=KUNDEN_ROOT)
    parser.add_argument("--ffmpeg", type=Path, default=None)
    parser.add_argument("--grenze", type=int, default=0)
    parser.add_argument("--ausschluss", type=Path, default=None,
                        help="JSON mit gesperrten Trainings-/Eval-Haltungen")
    parser.add_argument("--saat", default="osd-protokoll-split-v1")
    args = parser.parse_args(argv)

    if not args.scan.is_file():
        raise SystemExit(f"PDF-Scan fehlt: {args.scan}")
    if args.grenze < 0:
        raise SystemExit("--grenze darf nicht negativ sein.")
    if liegt_unter(args.ziel, args.kunden_root):
        raise SystemExit("Das Ziel darf nicht im Kundenordner liegen.")
    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits und wird nicht ueberschrieben: {args.ziel}")

    from osd_meter_leser import lese_meter, rendere_templates
    try:
        import cv2  # noqa: F401
    except ModuleNotFoundError as fehler:
        raise SystemExit(
            "OpenCV fehlt. Bitte mit sidecar\\.venv\\Scripts\\python.exe starten.") from fehler

    scan = json.loads(args.scan.read_text(encoding="utf-8-sig"))
    gesperrt = gesperrte_haltungen_laden(args.ausschluss)
    faelle, ausgeschlossen = faelle_aus_scan(scan, gesperrt)
    gesamt_geeignet = len(faelle)
    if args.grenze:
        faelle = faelle[:args.grenze]
    if not faelle:
        raise SystemExit("Keine geeigneten PDF-/Video-Positionen gefunden.")

    ffmpeg = ffmpeg_finden(args.ffmpeg)
    templates = rendere_templates()
    staging = args.ziel.with_name(f".{args.ziel.name}.staging-{uuid.uuid4().hex}")
    for split in SPLITS:
        (staging / "bilder" / split).mkdir(parents=True, exist_ok=True)

    eintraege: list[dict] = []
    bild_hashes: set[str] = set()
    zaehler = {"gleich": 0, "abweichend": 0, "nicht_gelesen": 0,
               "kein_bild": 0, "bildduplikat": 0}
    try:
        for index, fall in enumerate(faelle, start=1):
            video = Path(str(fall["video_path"]))
            if not video.is_file():
                zaehler["kein_bild"] += 1
                continue
            split = split_fuer_haltung(fall["haltung"], args.saat)
            relativ = Path("bilder") / split / bildname(fall)
            bild = staging / relativ
            sekunde = float(fall["video_counter_seconds"])
            if not bild_extrahieren(ffmpeg, video, sekunde, bild):
                zaehler["kein_bild"] += 1
                continue
            bild_hash = sha256_datei(bild)
            if bild_hash in bild_hashes:
                bild.unlink()
                zaehler["bildduplikat"] += 1
                continue
            bild_hashes.add(bild_hash)

            gelesen = lese_meter(bild, templates)
            soll = round(float(fall["meter_start"]), 2)
            ist = gelesen.get("meter")
            if ist is None:
                urteil = "nicht_gelesen"
            elif abs(float(ist) - soll) <= 0.011:
                urteil = "gleich"
            else:
                urteil = "abweichend"
            zaehler[urteil] += 1

            eintraege.append({
                "id": bild.stem,
                "haltung": fall["haltung"],
                "physische_haltung": physische_haltung(fall["haltung"]),
                "split": split,
                "code": fall.get("code"),
                "video": str(video),
                "sekunde": sekunde,
                "soll_meter": soll,
                "gelesen_meter": ist,
                "zeichenfolge": gelesen.get("zeichenfolge"),
                "leser_urteil": urteil,
                "bild": str(relativ).replace("\\", "/"),
                "bild_sha256": bild_hash,
                "source_pdf": fall.get("source_pdf"),
                "source_page": fall.get("source_page"),
            })
            if index % 25 == 0:
                print(f"  {index}/{len(faelle)} ...", flush=True)

        split_zaehler = {
            split: {
                "bilder": sum(1 for e in eintraege if e["split"] == split),
                "haltungen": len({e["physische_haltung"] for e in eintraege
                                    if e["split"] == split}),
            }
            for split in SPLITS
        }
        bericht = {
            "schema": "osd_wahrheit_protokoll_v2",
            "status": "qa_offen",
            "hinweis": "PDF-Sollwerte sind automatisch zugeordnet; vor Training Sichtprobe abschliessen.",
            "quelle_scan": str(args.scan),
            "quelle_scan_sha256": sha256_datei(args.scan),
            "split_regel": "70/15/15 deterministisch nach Haltung; gleiche Haltung nie in zwei Teilen",
            "split_saat": args.saat,
            "geeignete_positionen": gesamt_geeignet,
            "verarbeitete_positionen": len(faelle),
            "teilbestand": bool(args.grenze),
            "faelle": len(eintraege),
            "zusammenfassung": zaehler,
            "ausgeschlossen": ausgeschlossen,
            "splits": split_zaehler,
            "eintraege": eintraege,
        }
        (staging / "wahrheit.json").write_text(
            json.dumps(bericht, indent=2, ensure_ascii=False), encoding="utf-8")
        (staging / "wahrheit.sha256").write_text(
            sha256_datei(staging / "wahrheit.json") + "\n", encoding="utf-8")
        staging.replace(args.ziel)
    except Exception:
        shutil.rmtree(staging, ignore_errors=True)
        raise

    print(f"\n{len(eintraege)} eindeutige Bilder mit Sollwert")
    print(f"Bytegleiche Bilder entfernt: {zaehler['bildduplikat']}")
    print(f"Bestand: {args.ziel / 'wahrheit.json'}")
    print("Status: qa_offen - vor dem Training eine Sichtprobe pruefen.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
