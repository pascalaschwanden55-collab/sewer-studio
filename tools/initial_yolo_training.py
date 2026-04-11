#!/usr/bin/env python3
"""
Initialer YOLO-Trainingslauf: Extrahiert Ground-Truth aus PDF-Protokollen,
ordnet Frames aus Videos zu und erzeugt ein YOLO-Trainings-Dataset.

Verwendung:
    python tools/initial_yolo_training.py --haltungen D:/Haltungen --output D:/yolo_dataset

Ablauf:
    1. Scanne alle Haltungen (Video + PDF Paare)
    2. Parse PDF → Codes + Meterstellen
    3. Extrahiere Frames via FFmpeg (lineare Interpolation Meter → Zeit)
    4. Erzeuge YOLO-Labels (10 Defektkategorien)
    5. Starte Training via Sidecar API
"""

import argparse
import json
import logging
import os
import re
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path
from random import Random
from typing import Optional

import requests

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger("initial_training")

# ═══════════════════════════════════════════════════════════════════════
# Defekt-Taxonomie: VSA-Code → YOLO-Klasse (identisch mit C#)
# ═══════════════════════════════════════════════════════════════════════

YOLO_CLASSES = {
    0: "crack",          # BAB
    1: "fracture",       # BAC
    2: "deformation",    # BAA, BAF
    3: "displacement",   # BAH
    4: "intrusion",      # BAI, BBD
    5: "root",           # BBB
    6: "deposit",        # BBC, BBA
    7: "infiltration",   # BBF, BBG
    8: "connection",     # BCA
    9: "structural_other",  # Rest
}

VSA_TO_YOLO = {
    "BAB": 0, "BAC": 1, "BAA": 2, "BAF": 2, "BAH": 3,
    "BAI": 4, "BBD": 4, "BBB": 5, "BBC": 6, "BBA": 6,
    "BBF": 7, "BBG": 7, "BCA": 8,
    "BAD": 9, "BAE": 9, "BAG": 9, "BAJ": 9, "BAK": 9,
    "BBE": 9, "BBH": 9, "BCB": 9,
}

# Steuercodes: nicht visuell erkennbar → kein YOLO-Training
STEUERCODES = {"BCD", "BCE", "BCC", "BDB", "BDC", "BDD", "BDE", "BDF"}


def vsa_to_yolo_class(code: str) -> Optional[int]:
    """Mappt VSA-Code (z.B. 'BAB.B.A') auf YOLO-Klasse (0-9)."""
    if not code:
        return None
    # Praefix extrahieren: 'BAB.B.A' → 'BAB', 'BCCYA' → 'BCC'
    prefix = code[:3].upper()
    if prefix in STEUERCODES:
        return None
    return VSA_TO_YOLO.get(prefix)


# ═══════════════════════════════════════════════════════════════════════
# PDF-Parsing (Format 2: Abwasser Uri)
# ═══════════════════════════════════════════════════════════════════════

@dataclass
class ProtocolEntry:
    meter: float
    code: str
    description: str
    video_time: Optional[str] = None  # "00:01:23"
    foto: Optional[str] = None
    zustand: Optional[int] = None


# Regex fuer Format 2 (POSITION [m] SK CODE BEOBACHTUNG VIDEO)
RE_METER = re.compile(r"^\s*(\d{1,4}[.,]\d{1,2})\s+")
# VSA-Codes: 3-5 Buchstaben, optional mit Punkten (BAB, BABBB, BAB.B.B, BCAXB)
RE_CODE = re.compile(r"\b(B[A-D][A-Z](?:[A-Z.]{0,4}))\b")
RE_VIDEO_TIME = re.compile(r"(\d{2}:\d{2}:\d{2})")
RE_FOTO = re.compile(r"(\S+\.jpg)", re.IGNORECASE)
RE_ZUSTAND = re.compile(r"\b([1-4])\s*$")

# Regex fuer Format 1 (Fretz/IBAK: Meter Code Beschreibung MPEG Foto Stufe)
RE_FORMAT1 = re.compile(
    r"^\s*(\d{1,4}[.,]\d{1,2})\s+"   # Meter
    r"(\d{1,2})\s+"                    # Zustandsstufe (oft am Anfang)
    r"(B[A-D][A-Z]{1,4})\s+"          # Code
)


def decode_caesar(text: str, shift: int = 29) -> str:
    """Decodiert Caesar-verschluesselte PDF-Texte (Font-Encoding-Problem).
    Viele WinCan/IBAK-PDFs verwenden eine Custom-Font-Map die effektiv
    eine ASCII-Verschiebung von -29 auf alle druckbaren Zeichen anwendet.
    Ziffern (0-9) werden zu Steuerzeichen (ASCII 19-28) und muessen
    ebenfalls zurueck-verschoben werden."""
    result = []
    for c in text:
        code = ord(c)
        if 65 <= code <= 90 or 97 <= code <= 122:  # A-Z, a-z → immer verschieben
            result.append(chr(code + shift))
        elif 33 <= code <= 64:  # !-@ → nur wenn Ergebnis ein Buchstabe oder Ziffer ist
            decoded = code + shift
            if (65 <= decoded <= 90 or 97 <= decoded <= 122 or
                    48 <= decoded <= 57):  # auch Ziffern akzeptieren
                result.append(chr(decoded))
            else:
                result.append(c)
        elif 19 <= code <= 28:  # Steuerzeichen die verschobene Ziffern sind (48-57 - 29 = 19-28)
            result.append(chr(code + shift))  # → Ziffern 0-9
        elif 3 <= code <= 6:  # Steuerzeichen die verschobene Satzzeichen sein koennten
            decoded = code + shift
            if 32 <= decoded < 127:
                result.append(chr(decoded))
            else:
                result.append(c)
        else:
            result.append(c)  # Space, Zeilenumbrueche beibehalten
    return "".join(result)


def parse_pdf(pdf_path: str) -> tuple[list[ProtocolEntry], Optional[float]]:
    """Parst ein PDF-Inspektionsprotokoll. Gibt Eintraege + Haltungslaenge zurueck."""
    try:
        raw_text = subprocess.check_output(
            ["pdftotext", "-layout", pdf_path, "-"],
            stderr=subprocess.DEVNULL,
            timeout=30,
        ).decode("utf-8", errors="replace")
    except Exception as e:
        log.debug("pdftotext fehlgeschlagen fuer %s: %s", pdf_path, e)
        return [], None

    # Pruefen ob Text lesbar ist oder Caesar-Decodierung braucht
    # Lesbare PDFs enthalten Woerter wie "Haltung", "Rohranfang", "POSITION", "BCD"
    if re.search(r"(Haltung|POSITION|Rohranfang|BCD|Inspektion)", raw_text):
        text = raw_text
    else:
        # Caesar +29 Decodierung versuchen
        decoded = decode_caesar(raw_text, shift=29)
        if re.search(r"(Haltung|POSITION|Rohranfang|BCD|Inspektion|Leitung)", decoded):
            text = decoded
            log.debug("  Caesar-Decodierung (+29) erfolgreich")
        else:
            log.debug("  PDF nicht parsebar (weder klartext noch Caesar)")
            return [], None

    entries: list[ProtocolEntry] = []
    haltungslaenge: Optional[float] = None

    # Haltungslaenge extrahieren
    m_hl = re.search(r"(?:HL|Haltungsl|Insp\.?L|Inspektionslnge)[^0-9]*(\d{1,4}[.,]\d{1,2})\s*m", text)
    if m_hl:
        haltungslaenge = float(m_hl.group(1).replace(",", "."))

    # ── Format-Erkennung ──
    # Format A: Tabellarisch (Meter am Zeilenanfang + Code auf gleicher Zeile)
    # Format B: Formular (Caesar-decodierte PDFs: "Entf...m" / "Zustand" / "Position" Bloecke)

    lines = text.split("\n")

    # Versuche zuerst Format A (tabellarisch)
    for line in lines:
        line_stripped = line.strip()
        if not line_stripped:
            continue

        m_meter = RE_METER.match(line_stripped)
        if not m_meter:
            continue

        meter = float(m_meter.group(1).replace(",", "."))

        m_code = RE_CODE.search(line_stripped)
        if not m_code:
            continue

        code = m_code.group(1).upper()
        description = line_stripped[m_code.end():].strip()

        m_time = RE_VIDEO_TIME.search(line_stripped)
        video_time = m_time.group(1) if m_time else None

        m_foto = RE_FOTO.search(line_stripped)
        foto = m_foto.group(1) if m_foto else None

        entries.append(ProtocolEntry(
            meter=meter, code=code, description=description,
            video_time=video_time, foto=foto,
        ))

    if entries:
        return entries, haltungslaenge

    # ── Format B: Formular-Layout (Caesar-decodierte PDFs) ──
    # Zwei Sub-Formate:
    #   B1: Tabelle "Foto  Video  Entfm  Zustand  V  Beschreibung" mit Codes darunter
    #   B2: Einzelbloecke "EntfgegenFlier / Zustand / Position"

    # B1: Tabelle suchen
    # Muster: Zeilen mit nur einem VSA-Code + optionaler Beschreibung, evtl. Meter davor
    re_table_line = re.compile(
        r"^\s*(?:(\d{1,4}[.,]\d{1,2})\s+)?"  # optionaler Meter
        r"(B[A-D][A-Z]{1,5})\s*"              # VSA-Code
        r"(.*?)$"                              # optionale Beschreibung
    )
    current_meter = 0.0
    in_table = False
    for i, line in enumerate(lines):
        stripped = line.strip()
        # Tabellenkopf erkennen
        if re.search(r"Foto\s+Video\s+Entf|Entfm\s+Zustand", stripped):
            in_table = True
            continue
        # Tabelle endet bei Leerzeile nach Abschnitt oder neuem Header
        if in_table and not stripped:
            continue
        if in_table and re.search(r"ObjektID|Gedrucktam|Haltung\s", stripped):
            in_table = False
            continue

        if in_table:
            m_tbl = re_table_line.match(stripped)
            if m_tbl:
                if m_tbl.group(1):
                    current_meter = float(m_tbl.group(1).replace(",", "."))
                code = m_tbl.group(2).upper()
                desc = m_tbl.group(3).strip() if m_tbl.group(3) else ""
                entries.append(ProtocolEntry(
                    meter=current_meter, code=code, description=desc,
                ))

    if entries:
        return entries, haltungslaenge

    # B2: Einzelblock-Format (Bildteil)
    # Bloecke: "EntfgegenFlier [meter] m" / "Zustand [CODE]" / "Position [CODE]"
    re_entf = re.compile(r"Entf(?:gegen|in)Flie?r?\s+(\d{1,4}[.,]\d{1,2})\s*m", re.IGNORECASE)
    re_zustand = re.compile(r"Zustand\s+(B[A-D][A-Z]{1,5})", re.IGNORECASE)
    re_position = re.compile(r"Position\s+(B[A-D][A-Z]{1,5})", re.IGNORECASE)

    current_meter = 0.0
    for line in lines:
        stripped = line.strip()

        m_entf = re_entf.search(stripped)
        if m_entf:
            current_meter = float(m_entf.group(1).replace(",", "."))
            continue

        m_zust = re_zustand.search(stripped)
        if m_zust:
            entries.append(ProtocolEntry(
                meter=current_meter, code=m_zust.group(1).upper(), description="",
            ))
            continue

        m_pos = re_position.search(stripped)
        if m_pos:
            code = m_pos.group(1).upper()
            # Nur hinzufuegen wenn nicht schon als Zustand erfasst
            if not entries or entries[-1].code != code:
                entries.append(ProtocolEntry(
                    meter=current_meter, code=code, description="",
                ))

    return entries, haltungslaenge


def time_str_to_seconds(t: str) -> float:
    """Konvertiert 'HH:MM:SS' zu Sekunden."""
    parts = t.split(":")
    if len(parts) == 3:
        return int(parts[0]) * 3600 + int(parts[1]) * 60 + int(parts[2])
    elif len(parts) == 2:
        return int(parts[0]) * 60 + int(parts[1])
    return 0.0


# ═══════════════════════════════════════════════════════════════════════
# Frame-Extraktion via FFmpeg
# ═══════════════════════════════════════════════════════════════════════

def get_video_duration(video_path: str) -> float:
    """Gibt die Video-Dauer in Sekunden zurueck."""
    try:
        result = subprocess.check_output(
            ["ffprobe", "-v", "quiet", "-print_format", "json",
             "-show_format", video_path],
            stderr=subprocess.DEVNULL,
            timeout=30,
        )
        info = json.loads(result)
        return float(info["format"]["duration"])
    except Exception:
        return 0.0


def extract_frame(video_path: str, time_sec: float, output_path: str) -> bool:
    """Extrahiert einen einzelnen Frame als PNG."""
    try:
        subprocess.run(
            ["ffmpeg", "-y", "-ss", f"{time_sec:.2f}", "-i", video_path,
             "-frames:v", "1", "-q:v", "2", output_path],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            timeout=30,
        )
        return os.path.exists(output_path) and os.path.getsize(output_path) > 1000
    except Exception:
        return False


# ═══════════════════════════════════════════════════════════════════════
# Dataset-Generierung
# ═══════════════════════════════════════════════════════════════════════

@dataclass
class DatasetStats:
    total: int = 0
    train: int = 0
    val: int = 0
    skipped: int = 0
    class_counts: dict = field(default_factory=dict)
    haltungen_processed: int = 0
    haltungen_skipped: int = 0
    haltungen_failed: int = 0


def discover_haltungen(root: str) -> list[tuple[str, str, str]]:
    """Findet Video+PDF-Paare. Gibt (haltung_id, video_path, pdf_path) zurueck."""
    results = []
    video_exts = {".mpg", ".mp4", ".avi", ".mpeg", ".mov", ".mkv"}

    for entry in sorted(os.listdir(root)):
        dirpath = os.path.join(root, entry)
        if not os.path.isdir(dirpath):
            continue

        videos = [f for f in os.listdir(dirpath)
                  if os.path.splitext(f)[1].lower() in video_exts]
        pdfs = [f for f in os.listdir(dirpath)
                if f.lower().endswith(".pdf")]

        if videos and pdfs:
            # Neuestes Video und PDF nehmen
            video = max(videos, key=lambda f: os.path.getmtime(os.path.join(dirpath, f)))
            pdf = max(pdfs, key=lambda f: os.path.getmtime(os.path.join(dirpath, f)))
            results.append((
                entry,
                os.path.join(dirpath, video),
                os.path.join(dirpath, pdf),
            ))

    return results


def generate_dataset(
    haltungen_root: str,
    output_dir: str,
    train_split: float = 0.8,
    max_haltungen: int = 0,
) -> DatasetStats:
    """Hauptfunktion: Scannt Haltungen, parst PDFs, extrahiert Frames, erzeugt YOLO-Dataset."""

    stats = DatasetStats()
    rng = Random(42)

    # Verzeichnisse erstellen
    for split in ("train", "val"):
        os.makedirs(os.path.join(output_dir, "images", split), exist_ok=True)
        os.makedirs(os.path.join(output_dir, "labels", split), exist_ok=True)

    haltungen = discover_haltungen(haltungen_root)
    total = len(haltungen)
    if max_haltungen > 0:
        haltungen = haltungen[:max_haltungen]

    log.info("Gefunden: %d Haltungen (verarbeite %d)", total, len(haltungen))

    for idx, (haltung_id, video_path, pdf_path) in enumerate(haltungen):
        log.info("[%d/%d] %s", idx + 1, len(haltungen), haltung_id)

        # 1. PDF parsen
        entries, haltungslaenge = parse_pdf(pdf_path)
        if not entries:
            log.debug("  Keine Eintraege geparst → uebersprungen")
            stats.haltungen_skipped += 1
            continue

        # Nur Defekt-Eintraege (keine Steuercodes)
        defect_entries = [e for e in entries if vsa_to_yolo_class(e.code) is not None]
        if not defect_entries:
            log.debug("  Keine Defekte im Protokoll → uebersprungen")
            stats.haltungen_skipped += 1
            continue

        # 2. Video-Dauer ermitteln
        duration = get_video_duration(video_path)
        if duration <= 0:
            log.warning("  Video-Dauer nicht ermittelbar → uebersprungen")
            stats.haltungen_failed += 1
            continue

        # Haltungslaenge schaetzen falls nicht aus PDF
        if not haltungslaenge:
            max_meter = max((e.meter for e in entries), default=0)
            haltungslaenge = max_meter if max_meter > 0 else duration * 0.1

        # Pruefen ob alle Meter 0.0 sind (Caesar-PDFs ohne Zahlen)
        all_meters_zero = all(e.meter == 0.0 for e in entries)

        # 3. Fuer jeden Defekt-Eintrag: Frame extrahieren
        temp_dir = os.path.join(os.path.dirname(video_path), "self_training_frames")
        os.makedirs(temp_dir, exist_ok=True)

        extracted = 0
        total_entries = len(entries)  # Alle Eintraege (nicht nur Defekte) fuer Positionsberechnung
        for entry_idx, entry in enumerate(defect_entries):
            yolo_class = vsa_to_yolo_class(entry.code)
            if yolo_class is None:
                stats.skipped += 1
                continue

            # Zeit berechnen: Video-Zeitcode oder lineare Interpolation
            if entry.video_time:
                time_sec = time_str_to_seconds(entry.video_time)
            elif all_meters_zero and total_entries > 1:
                # Caesar-PDFs: Alle Eintraege finden, Position dieses Eintrags bestimmen
                try:
                    pos_in_all = entries.index(entry)
                except ValueError:
                    pos_in_all = entry_idx
                # Gleichmaessig ueber Videodauer verteilen (10% Rand an Start/Ende)
                usable = duration * 0.8
                time_sec = duration * 0.1 + (pos_in_all / max(total_entries - 1, 1)) * usable
            else:
                # Lineare Interpolation: meter / haltungslaenge * duration
                time_sec = (entry.meter / max(haltungslaenge, 0.1)) * duration

            # Sicherheitscheck
            time_sec = max(0.5, min(time_sec, duration - 0.5))

            # Frame extrahieren
            frame_name = f"{stats.total:06d}.jpg"
            is_train = rng.random() < train_split
            split = "train" if is_train else "val"
            frame_path = os.path.join(output_dir, "images", split, frame_name)

            if not extract_frame(video_path, time_sec, frame_path):
                stats.skipped += 1
                continue

            # YOLO-Label schreiben (Full-Frame BBox)
            label_path = os.path.join(output_dir, "labels", split, f"{stats.total:06d}.txt")
            with open(label_path, "w") as f:
                f.write(f"{yolo_class} 0.5 0.5 1.0 1.0\n")

            stats.total += 1
            stats.class_counts[yolo_class] = stats.class_counts.get(yolo_class, 0) + 1
            if is_train:
                stats.train += 1
            else:
                stats.val += 1
            extracted += 1

        if extracted > 0:
            stats.haltungen_processed += 1
            log.info("  %d Frames extrahiert (%d Defekte im Protokoll)",
                     extracted, len(defect_entries))
        else:
            stats.haltungen_skipped += 1

    # data.yaml schreiben
    yaml_content = f"path: {output_dir}\n"
    yaml_content += "train: images/train\n"
    yaml_content += "val: images/val\n"
    yaml_content += f"nc: {len(YOLO_CLASSES)}\n"
    yaml_content += "names:\n"
    for cid, cname in YOLO_CLASSES.items():
        yaml_content += f"  {cid}: {cname}\n"

    with open(os.path.join(output_dir, "data.yaml"), "w") as f:
        f.write(yaml_content)

    return stats


# ═══════════════════════════════════════════════════════════════════════
# Training via Sidecar starten
# ═══════════════════════════════════════════════════════════════════════

def start_training(dataset_path: str, sidecar_url: str = "http://localhost:8100",
                   epochs: int = 100, imgsz: int = 640) -> Optional[str]:
    """Startet YOLO-Training via Sidecar API."""
    # Sidecar erwartet den Pfad zur data.yaml, nicht zum Verzeichnis
    data_yaml = os.path.join(dataset_path, "data.yaml") if not dataset_path.endswith(".yaml") else dataset_path
    try:
        resp = requests.post(
            f"{sidecar_url}/training/train-yolo",
            json={
                "dataset_path": data_yaml,
                "epochs": epochs,
                "imgsz": imgsz,
                "batch": -1,  # auto
                "base_model": "yolo11m.pt",
                "amp": True,
                "max_fallback_ratio": 1.0,  # Full-Frame-BBoxen fuer initialen Lauf erlauben
            },
            timeout=30,
        )
        resp.raise_for_status()
        data = resp.json()
        job_id = data.get("job_id")
        log.info("Training gestartet: Job-ID = %s", job_id)
        return job_id
    except Exception as e:
        log.error("Training-Start fehlgeschlagen: %s", e)
        return None


def poll_training(job_id: str, sidecar_url: str = "http://localhost:8100"):
    """Wartet auf Training-Abschluss."""
    while True:
        try:
            resp = requests.get(f"{sidecar_url}/training/jobs/{job_id}", timeout=10)
            data = resp.json()
            status = data.get("status", "unknown")
            log.info("Training-Status: %s — %s", status, data.get("message", ""))
            if status in ("completed", "failed", "error"):
                return data
        except Exception as e:
            log.warning("Status-Abfrage fehlgeschlagen: %s", e)
        time.sleep(30)


# ═══════════════════════════════════════════════════════════════════════
# Main
# ═══════════════════════════════════════════════════════════════════════

def main():
    parser = argparse.ArgumentParser(
        description="Initialer YOLO-Trainingslauf aus vorprotokollierten Haltungen")
    parser.add_argument("--haltungen", required=True,
                        help="Pfad zum Haltungen-Ordner (z.B. D:\\Haltungen)")
    parser.add_argument("--output", required=True,
                        help="Pfad fuer YOLO-Dataset-Output (z.B. D:\\yolo_dataset)")
    parser.add_argument("--max", type=int, default=0,
                        help="Max Haltungen (0 = alle)")
    parser.add_argument("--epochs", type=int, default=100,
                        help="Trainings-Epochen (default: 100)")
    parser.add_argument("--train-split", type=float, default=0.8,
                        help="Train/Val Split (default: 0.8)")
    parser.add_argument("--sidecar", default="http://localhost:8100",
                        help="Sidecar-URL (default: http://localhost:8100)")
    parser.add_argument("--skip-training", action="store_true",
                        help="Nur Dataset generieren, kein Training starten")
    parser.add_argument("--verbose", "-v", action="store_true")
    args = parser.parse_args()

    if args.verbose:
        logging.getLogger().setLevel(logging.DEBUG)

    log.info("=" * 60)
    log.info("Initialer YOLO-Trainingslauf")
    log.info("Haltungen:   %s", args.haltungen)
    log.info("Output:      %s", args.output)
    log.info("Max:         %s", args.max or "alle")
    log.info("=" * 60)

    # Phase 1: Dataset generieren
    t0 = time.time()
    stats = generate_dataset(
        args.haltungen,
        args.output,
        train_split=args.train_split,
        max_haltungen=args.max,
    )
    elapsed = time.time() - t0

    log.info("=" * 60)
    log.info("Dataset-Generierung abgeschlossen in %.1f Minuten", elapsed / 60)
    log.info("  Haltungen: %d verarbeitet, %d uebersprungen, %d fehlgeschlagen",
             stats.haltungen_processed, stats.haltungen_skipped, stats.haltungen_failed)
    log.info("  Frames:    %d total (%d train, %d val), %d uebersprungen",
             stats.total, stats.train, stats.val, stats.skipped)
    log.info("  Klassen-Verteilung:")
    for cid in sorted(stats.class_counts):
        log.info("    %d %-20s: %d", cid, YOLO_CLASSES[cid], stats.class_counts[cid])
    log.info("=" * 60)

    if stats.total < 50:
        log.error("Zu wenige Frames (%d) fuer sinnvolles Training. Abbruch.", stats.total)
        sys.exit(1)

    # Phase 2: Training starten
    if args.skip_training:
        log.info("--skip-training: Training uebersprungen.")
        return

    log.info("Starte YOLO-Training (%d Epochen)...", args.epochs)
    job_id = start_training(
        args.output,
        sidecar_url=args.sidecar,
        epochs=args.epochs,
    )

    if job_id:
        log.info("Warte auf Training-Abschluss...")
        result = poll_training(job_id, args.sidecar)
        log.info("Training beendet: %s", json.dumps(result, indent=2))
    else:
        log.error("Training konnte nicht gestartet werden.")
        sys.exit(1)


if __name__ == "__main__":
    main()
