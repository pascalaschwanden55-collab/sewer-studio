#!/usr/bin/env python3
"""
Smoke-Test: Eine Haltung durch die Pipeline schicken und pruefen
ob der DetectionAggregator sinnvolle Ergebnisse liefert.

Verwendet den Sidecar direkt (YOLO-Detection) und vergleicht
mit dem PDF-Protokoll als Ground-Truth.

Verwendung:
    python tools/smoke_test_pipeline.py --haltung D:/Haltungen/149570-28945
"""

import argparse
import base64
import json
import logging
import os
import subprocess
import sys
import time
from pathlib import Path

import requests

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
log = logging.getLogger("smoke_test")

SIDECAR = "http://localhost:8100"

# YOLO-Klassen (identisch mit YoloDefectTaxonomy)
YOLO_CLASSES = {
    0: "crack", 1: "fracture", 2: "deformation", 3: "displacement",
    4: "intrusion", 5: "root", 6: "deposit", 7: "infiltration",
    8: "connection", 9: "structural_other",
}


def check_sidecar():
    """Sidecar Health-Check."""
    try:
        r = requests.get(f"{SIDECAR}/health", timeout=5)
        data = r.json()
        yolo = data.get("yolo", {})
        log.info("Sidecar OK — YOLO: %s (custom=%s)",
                 yolo.get("resolved_model_path", "?"),
                 yolo.get("using_custom_weights", False))
        return True
    except Exception as e:
        log.error("Sidecar nicht erreichbar: %s", e)
        return False


def get_video_duration(video_path: str) -> float:
    """Video-Dauer in Sekunden."""
    try:
        result = subprocess.check_output(
            ["ffprobe", "-v", "quiet", "-print_format", "json", "-show_format", video_path],
            stderr=subprocess.DEVNULL, timeout=30,
        )
        return float(json.loads(result)["format"]["duration"])
    except Exception:
        return 0.0


def extract_frame_bytes(video_path: str, time_sec: float) -> bytes | None:
    """Extrahiert einen Frame als JPEG-Bytes."""
    try:
        result = subprocess.run(
            ["ffmpeg", "-y", "-ss", f"{time_sec:.2f}", "-i", video_path,
             "-frames:v", "1", "-f", "image2", "-vcodec", "mjpeg", "pipe:1"],
            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, timeout=30,
        )
        if result.returncode == 0 and len(result.stdout) > 1000:
            return result.stdout
        return None
    except Exception:
        return None


def yolo_detect(frame_bytes: bytes, confidence: float = 0.25) -> list[dict]:
    """YOLO-Detection via Sidecar."""
    b64 = base64.b64encode(frame_bytes).decode()
    try:
        r = requests.post(f"{SIDECAR}/detect/yolo",
                          json={"image_base64": b64, "confidence": confidence},
                          timeout=30)
        r.raise_for_status()
        data = r.json()
        return data.get("detections", [])
    except Exception as e:
        log.warning("YOLO-Detection fehlgeschlagen: %s", e)
        return []


def run_smoke_test(haltung_dir: str, frame_step: float = 1.5):
    """Fuehrt den Smoke-Test fuer eine Haltung durch."""

    # Video und PDF finden
    video_exts = {".mpg", ".mp4", ".avi", ".mpeg"}
    videos = [f for f in os.listdir(haltung_dir)
              if os.path.splitext(f)[1].lower() in video_exts]
    pdfs = [f for f in os.listdir(haltung_dir) if f.lower().endswith(".pdf")]

    if not videos:
        log.error("Kein Video gefunden in %s", haltung_dir)
        return
    if not pdfs:
        log.warning("Kein PDF gefunden — kein Ground-Truth-Vergleich moeglich")

    video_path = os.path.join(haltung_dir, videos[0])
    duration = get_video_duration(video_path)
    if duration <= 0:
        log.error("Video-Dauer nicht ermittelbar: %s", video_path)
        return

    log.info("Video: %s (%.1f Sekunden)", videos[0], duration)

    # Ground-Truth aus PDF (optional)
    if pdfs:
        sys.path.insert(0, str(Path(__file__).parent))
        from initial_yolo_training import parse_pdf, vsa_to_yolo_class
        entries, hl = parse_pdf(os.path.join(haltung_dir, pdfs[0]))
        defects = [e for e in entries if vsa_to_yolo_class(e.code) is not None]
        log.info("PDF Ground-Truth: %d Eintraege, %d Defekte", len(entries), len(defects))
        for d in defects:
            log.info("  GT: %.1fm  %s", d.meter, d.code)
    else:
        defects = []

    # Frames extrahieren und YOLO laufen lassen
    total_frames = int(duration / frame_step)
    all_detections = []
    log.info("Analysiere %d Frames (Schritt: %.1fs)...", total_frames, frame_step)

    for i in range(total_frames):
        t = i * frame_step
        frame_bytes = extract_frame_bytes(video_path, t)
        if frame_bytes is None:
            continue

        dets = yolo_detect(frame_bytes, confidence=0.3)
        for det in dets:
            all_detections.append({
                "time": t,
                "class": det.get("class_name", det.get("label", "?")),
                "confidence": det.get("confidence", 0),
                "meter_est": t / duration * (hl or duration * 0.1),
            })

        if (i + 1) % 20 == 0:
            log.info("  Frame %d/%d — %d Detektionen bisher", i + 1, total_frames, len(all_detections))

    log.info("=" * 60)
    log.info("Ergebnis:")
    log.info("  Frames analysiert:    %d", total_frames)
    log.info("  Roh-Detektionen:      %d", len(all_detections))

    # Klassen-Verteilung
    class_counts: dict[str, int] = {}
    for d in all_detections:
        cls = d["class"]
        class_counts[cls] = class_counts.get(cls, 0) + 1

    if class_counts:
        log.info("  Klassen-Verteilung:")
        for cls, count in sorted(class_counts.items(), key=lambda x: -x[1]):
            log.info("    %-20s: %d", cls, count)

    # Erwartung: Der DetectionAggregator wuerde diese ~N Detektionen
    # auf ~M Events verdichten (M << N)
    events = []
    if len(all_detections) > 0:
        # Einfache Simulation: Zusammenhaengende Frames gleicher Klasse = 1 Event
        events = []
        last_cls = None
        last_time = -10
        for d in sorted(all_detections, key=lambda x: x["time"]):
            if d["class"] != last_cls or d["time"] - last_time > frame_step * 3:
                events.append({"class": d["class"], "time_start": d["time"],
                               "time_end": d["time"], "count": 1,
                               "peak_conf": d["confidence"]})
                last_cls = d["class"]
            else:
                events[-1]["time_end"] = d["time"]
                events[-1]["count"] += 1
                events[-1]["peak_conf"] = max(events[-1]["peak_conf"], d["confidence"])
            last_time = d["time"]

        # Filter: mindestens 3 Frames
        events = [e for e in events if e["count"] >= 3]

        log.info("  Aggregierte Events:   %d (aus %d Roh-Detektionen)", len(events), len(all_detections))
        log.info("  Verdichtungsfaktor:   %.0fx", len(all_detections) / max(len(events), 1))
        for evt in events:
            log.info("    %s @ %.1f-%.1fs (Peak=%.2f, %d Frames)",
                     evt["class"], evt["time_start"], evt["time_end"],
                     evt["peak_conf"], evt["count"])

    if defects:
        log.info("  PDF-Defekte:          %d", len(defects))
        log.info("  Differenz:            %+d",
                 len([e for e in events if e["count"] >= 3]) - len(defects))

    log.info("=" * 60)


def main():
    global SIDECAR

    parser = argparse.ArgumentParser(description="Smoke-Test fuer SewerStudio KI-Pipeline")
    parser.add_argument("--haltung", required=True, help="Pfad zum Haltungs-Ordner")
    parser.add_argument("--step", type=float, default=1.5, help="Frame-Schritt in Sekunden")
    parser.add_argument("--sidecar", default=SIDECAR, help="Sidecar-URL")
    args = parser.parse_args()

    SIDECAR = args.sidecar

    if not check_sidecar():
        sys.exit(1)

    run_smoke_test(args.haltung, args.step)


if __name__ == "__main__":
    main()
