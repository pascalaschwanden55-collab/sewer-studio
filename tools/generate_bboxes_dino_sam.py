#!/usr/bin/env python3
"""
BBox-Generierung mit DINO+SAM: Laeuft ueber bestehende Trainingsframes
und erzeugt echte Bounding-Boxen statt Full-Frame-Platzhalter.

Ablauf:
1. Liest das bestehende YOLO-Dataset (images/ + labels/)
2. Fuer jedes Bild: DINO Open-Vocabulary-Detection mit Defekt-Prompts
3. Fuer jeden DINO-Treffer: SAM-Segmentierung → praezise BBox
4. Schreibt neue YOLO-Labels mit echten BBoxen

Verwendung:
    python tools/generate_bboxes_dino_sam.py --dataset D:/yolo_sewer_v2 --output D:/yolo_sewer_v3
"""

import argparse
import base64
import json
import logging
import os
import shutil
import sys
import time
from pathlib import Path

import requests

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
log = logging.getLogger("bbox_gen")

SIDECAR = "http://localhost:8100"

# YOLO-Klassen → DINO-Suchbegriffe (Open-Vocabulary)
# DINO braucht natuerlichsprachige Prompts
YOLO_TO_DINO_PROMPTS = {
    0: ["crack", "riss", "fracture line"],
    1: ["broken pipe", "collapse", "bruch"],
    2: ["deformation", "oval pipe", "verformung"],
    3: ["displaced joint", "offset joint", "versatz"],
    4: ["intruding connection", "protruding pipe", "einragend"],
    5: ["root", "roots", "wurzel"],
    6: ["deposit", "sediment", "ablagerung", "inkrustation"],
    7: ["infiltration", "water ingress", "leak"],
    8: ["lateral connection", "branch pipe", "anschluss"],
    9: ["defect", "damage", "schaden"],
}

# YOLO-Klassennamen
YOLO_CLASSES = {
    0: "crack", 1: "fracture", 2: "deformation", 3: "displacement",
    4: "intrusion", 5: "root", 6: "deposit", 7: "infiltration",
    8: "connection", 9: "structural_other",
}


def dino_detect(image_b64: str, prompts: list[str], confidence: float = 0.20) -> list[dict]:
    """DINO Open-Vocabulary-Detection via Sidecar."""
    try:
        r = requests.post(f"{SIDECAR}/detect/dino", json={
            "image_base64": image_b64,
            "labels": prompts,
            "confidence": confidence,
        }, timeout=30)
        r.raise_for_status()
        return r.json().get("detections", [])
    except Exception as e:
        log.debug("DINO fehlgeschlagen: %s", e)
        return []


def sam_segment(image_b64: str, bboxes: list[dict]) -> list[dict]:
    """SAM-Segmentierung fuer gegebene Bounding-Boxen."""
    if not bboxes:
        return []
    try:
        sam_boxes = [{"x1": b["x1"], "y1": b["y1"], "x2": b["x2"], "y2": b["y2"]}
                     for b in bboxes]
        r = requests.post(f"{SIDECAR}/segment/sam", json={
            "image_base64": image_b64,
            "bounding_boxes": sam_boxes,
        }, timeout=30)
        r.raise_for_status()
        return r.json().get("masks", [])
    except Exception as e:
        log.debug("SAM fehlgeschlagen: %s", e)
        return []


def bbox_to_yolo_format(bbox: dict, img_w: int, img_h: int) -> str:
    """Konvertiert absolute BBox (x1,y1,x2,y2) zu YOLO-Format (cx,cy,w,h normiert)."""
    x1, y1, x2, y2 = bbox["x1"], bbox["y1"], bbox["x2"], bbox["y2"]
    cx = (x1 + x2) / 2 / img_w
    cy = (y1 + y2) / 2 / img_h
    w = (x2 - x1) / img_w
    h = (y2 - y1) / img_h
    # Clamp auf [0, 1]
    cx = max(0, min(1, cx))
    cy = max(0, min(1, cy))
    w = max(0.01, min(1, w))
    h = max(0.01, min(1, h))
    return f"{cx:.6f} {cy:.6f} {w:.6f} {h:.6f}"


def process_dataset(input_dir: str, output_dir: str, min_confidence: float = 0.20):
    """Verarbeitet ein bestehendes YOLO-Dataset und generiert echte BBoxen."""

    stats = {"total": 0, "with_bbox": 0, "fallback": 0, "skipped": 0}

    for split in ("train", "val"):
        img_dir = Path(input_dir) / "images" / split
        lbl_dir = Path(input_dir) / "labels" / split
        out_img = Path(output_dir) / "images" / split
        out_lbl = Path(output_dir) / "labels" / split

        out_img.mkdir(parents=True, exist_ok=True)
        out_lbl.mkdir(parents=True, exist_ok=True)

        if not img_dir.exists():
            continue

        images = sorted(img_dir.glob("*.jpg")) + sorted(img_dir.glob("*.png"))
        log.info("Verarbeite %d Bilder in %s/%s", len(images), input_dir, split)

        for idx, img_path in enumerate(images):
            stats["total"] += 1
            label_path = lbl_dir / (img_path.stem + ".txt")

            if not label_path.exists():
                stats["skipped"] += 1
                continue

            # Bestehende Klasse lesen
            label_text = label_path.read_text().strip()
            if not label_text:
                stats["skipped"] += 1
                continue
            parts = label_text.split()
            class_id = int(parts[0])

            # Bild laden
            img_bytes = img_path.read_bytes()
            img_b64 = base64.b64encode(img_bytes).decode()

            # Bildgroesse ermitteln (aus JPEG-Header oder Annahme)
            try:
                from PIL import Image
                import io
                img = Image.open(io.BytesIO(img_bytes))
                img_w, img_h = img.size
            except Exception:
                img_w, img_h = 640, 480  # Fallback

            # DINO-Prompts fuer diese Klasse
            prompts = YOLO_TO_DINO_PROMPTS.get(class_id, ["defect", "damage"])

            # DINO-Detection
            dino_dets = dino_detect(img_b64, prompts, confidence=min_confidence)

            new_label = None

            if dino_dets:
                # Beste Detection nehmen (hoechste Confidence)
                best = max(dino_dets, key=lambda d: d.get("confidence", 0))

                # SAM fuer praezisere BBox
                sam_masks = sam_segment(img_b64, [best])

                if sam_masks and sam_masks[0].get("bbox"):
                    # SAM-BBox verwenden (praeziser)
                    sam_bbox = sam_masks[0]["bbox"]
                    bbox_dict = {"x1": sam_bbox[0], "y1": sam_bbox[1],
                                 "x2": sam_bbox[2], "y2": sam_bbox[3]}
                    yolo_bbox = bbox_to_yolo_format(bbox_dict, img_w, img_h)
                    new_label = f"{class_id} {yolo_bbox}"
                    stats["with_bbox"] += 1
                else:
                    # DINO-BBox als Fallback
                    bbox_dict = {"x1": best["x1"], "y1": best["y1"],
                                 "x2": best["x2"], "y2": best["y2"]}
                    yolo_bbox = bbox_to_yolo_format(bbox_dict, img_w, img_h)
                    new_label = f"{class_id} {yolo_bbox}"
                    stats["with_bbox"] += 1
            else:
                # Kein DINO-Treffer → Full-Frame-BBox beibehalten (Fallback)
                new_label = label_text
                stats["fallback"] += 1

            # Bild kopieren + neues Label schreiben
            shutil.copy2(img_path, out_img / img_path.name)
            (out_lbl / (img_path.stem + ".txt")).write_text(new_label + "\n")

            if (idx + 1) % 50 == 0:
                log.info("  [%s] %d/%d — %d mit BBox, %d Fallback",
                         split, idx + 1, len(images),
                         stats["with_bbox"], stats["fallback"])

    # data.yaml kopieren
    src_yaml = Path(input_dir) / "data.yaml"
    if src_yaml.exists():
        yaml_text = src_yaml.read_text()
        # Pfad anpassen
        yaml_text = yaml_text.replace(str(input_dir), str(output_dir))
        (Path(output_dir) / "data.yaml").write_text(yaml_text)

    return stats


def main():
    parser = argparse.ArgumentParser(
        description="BBox-Generierung mit DINO+SAM fuer YOLO-Training")
    parser.add_argument("--dataset", required=True,
                        help="Bestehendes YOLO-Dataset (mit Full-Frame-BBoxen)")
    parser.add_argument("--output", required=True,
                        help="Output-Verzeichnis fuer neues Dataset mit echten BBoxen")
    parser.add_argument("--confidence", type=float, default=0.20,
                        help="DINO-Confidence-Schwelle (default: 0.20)")
    parser.add_argument("--sidecar", default=SIDECAR,
                        help="Sidecar-URL")
    args = parser.parse_args()

    global SIDECAR
    SIDECAR = args.sidecar

    # Health-Check
    try:
        r = requests.get(f"{SIDECAR}/health", timeout=5)
        health = r.json()
        gpu = health.get("gpu", {})
        models = gpu.get("loaded_models", {})
        if "dino" not in models or "sam" not in models:
            log.error("DINO und/oder SAM nicht geladen! Sidecar braucht beide Modelle.")
            sys.exit(1)
        log.info("Sidecar OK — DINO + SAM geladen")
    except Exception as e:
        log.error("Sidecar nicht erreichbar: %s", e)
        sys.exit(1)

    log.info("=" * 60)
    log.info("BBox-Generierung mit DINO+SAM")
    log.info("Input:  %s", args.dataset)
    log.info("Output: %s", args.output)
    log.info("=" * 60)

    t0 = time.time()
    stats = process_dataset(args.dataset, args.output, args.confidence)
    elapsed = time.time() - t0

    log.info("=" * 60)
    log.info("Fertig in %.1f Minuten", elapsed / 60)
    log.info("  Total:    %d Bilder", stats["total"])
    log.info("  Mit BBox: %d (DINO+SAM)", stats["with_bbox"])
    log.info("  Fallback: %d (Full-Frame)", stats["fallback"])
    log.info("  Skipped:  %d", stats["skipped"])
    log.info("  BBox-Rate: %.0f%%",
             stats["with_bbox"] / max(stats["total"], 1) * 100)
    log.info("=" * 60)


if __name__ == "__main__":
    main()
