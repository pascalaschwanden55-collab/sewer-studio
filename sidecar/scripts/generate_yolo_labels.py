"""Generiert YOLO-Detection-Labels fuer extrahierte Trainings-Frames.

Laeuft DINO+SAM ueber alle Referenz-Frames und erzeugt YOLO-Format Labels.
Nutzt den laufenden Sidecar auf localhost:8100.

Verwendung:
    python scripts/generate_yolo_labels.py

Eingabe: C:\KI_BRAIN\training_frames\_frame_index.json
Ausgabe: C:\KI_BRAIN\yolo_detection_dataset\
"""

import json
import base64
import requests
import sys
from pathlib import Path
from collections import Counter

SIDECAR_URL = "http://localhost:8100"
FRAME_INDEX = Path(r"C:\KI_BRAIN\training_frames\_frame_index.json")
OUTPUT_ROOT = Path(r"C:\KI_BRAIN\yolo_detection_dataset")

# YOLO-Klassen (gleich wie bestehendes yolo26m Modell)
# Mapping von VSA-Hauptcode zu YOLO-Klasse
VSA_TO_YOLO = {
    "BAA": 0,  # deformation
    "BAB": 1,  # crack
    "BAC": 2,  # fracture
    "BAF": 3,  # surface_damage
    "BAG": 4,  # intruding_connection
    "BAI": 5,  # seal_defect
    "BAJ": 6,  # displaced_joint
    "BBA": 7,  # roots
    "BBB": 8,  # deposits_attached
    "BBC": 9,  # deposits_settled
    "BBF": 10,  # infiltration
}

CLASS_NAMES = {
    0: "deformation",
    1: "crack",
    2: "fracture",
    3: "surface_damage",
    4: "intruding_connection",
    5: "seal_defect",
    6: "displaced_joint",
    7: "roots",
    8: "deposits_attached",
    9: "deposits_settled",
    10: "infiltration",
}


def get_yolo_class(code_main: str) -> int | None:
    """Mappt VSA-Hauptcode auf YOLO-Klassen-ID."""
    # Exakter Match
    if code_main in VSA_TO_YOLO:
        return VSA_TO_YOLO[code_main]
    # Prefix-Match (z.B. "BAH" → surface_damage ist fragwuerdig, lieber None)
    return None


def detect_dino(image_b64: str) -> list[dict]:
    """DINO Open-Vocabulary Detection ueber Sidecar."""
    resp = requests.post(
        f"{SIDECAR_URL}/detect/dino",
        json={
            "image_base64": image_b64,
            "text_prompt": "crack. fracture. root. deposit. infiltration. joint. deformation. corrosion. connection. obstruction. damage.",
            "box_threshold": 0.20,
            "text_threshold": 0.15,
        },
        timeout=30,
    )
    resp.raise_for_status()
    return resp.json().get("detections", [])


def segment_sam(image_b64: str, boxes: list[dict]) -> dict:
    """SAM Segmentierung ueber Sidecar."""
    sam_boxes = [
        {
            "x1": b["x1"],
            "y1": b["y1"],
            "x2": b["x2"],
            "y2": b["y2"],
            "label": b.get("label", ""),
            "confidence": b.get("confidence", 0.5),
        }
        for b in boxes
    ]
    resp = requests.post(
        f"{SIDECAR_URL}/segment/sam",
        json={
            "image_base64": image_b64,
            "bounding_boxes": sam_boxes,
        },
        timeout=30,
    )
    resp.raise_for_status()
    return resp.json()


def to_yolo_label(x1, y1, x2, y2, img_w, img_h, class_id):
    """Konvertiert Pixel-Koordinaten zu YOLO-Format (normalisiert)."""
    cx = ((x1 + x2) / 2) / img_w
    cy = ((y1 + y2) / 2) / img_h
    w = (x2 - x1) / img_w
    h = (y2 - y1) / img_h
    return f"{class_id} {cx:.6f} {cy:.6f} {w:.6f} {h:.6f}"


def process_frame(frame: dict, images_dir: Path, labels_dir: Path, stats: Counter):
    """Verarbeitet einen einzelnen Referenz-Frame."""
    png_path = Path(frame["png_pfad"])
    if not png_path.exists():
        # Relativer Pfad
        png_path = Path(r"C:\KI_BRAIN\training_frames") / png_path.name
    if not png_path.exists():
        stats["missing"] += 1
        return

    code_main = frame.get("code_main", "")
    if not code_main:
        stats["no_code"] += 1
        return

    yolo_class = get_yolo_class(code_main)
    if yolo_class is None:
        stats["unmapped"] += 1
        return

    # Bild laden
    img_bytes = png_path.read_bytes()
    img_b64 = base64.b64encode(img_bytes).decode()

    # DINO Detection
    try:
        detections = detect_dino(img_b64)
    except Exception as e:
        stats["dino_error"] += 1
        return

    if not detections:
        stats["no_detection"] += 1
        return

    # Bildgroesse aus erster Detection oder SAM
    try:
        sam_result = segment_sam(img_b64, detections)
        img_w = sam_result.get("image_width", 720)
        img_h = sam_result.get("image_height", 576)
    except Exception:
        img_w, img_h = 720, 576

    # Beste Detection nehmen (hoechste Confidence)
    best = max(detections, key=lambda d: d.get("confidence", 0))

    # YOLO Label schreiben
    label_line = to_yolo_label(
        best["x1"], best["y1"], best["x2"], best["y2"], img_w, img_h, yolo_class
    )

    # Bild kopieren + Label schreiben
    dest_img = images_dir / png_path.name
    dest_lbl = labels_dir / png_path.with_suffix(".txt").name

    if not dest_img.exists():
        import shutil

        shutil.copy2(str(png_path), str(dest_img))

    dest_lbl.write_text(label_line + "\n")
    stats["success"] += 1
    stats[f"class_{CLASS_NAMES.get(yolo_class, '?')}"] += 1


def main():
    # Health Check
    try:
        r = requests.get(f"{SIDECAR_URL}/health", timeout=5)
        r.raise_for_status()
        print(f"Sidecar OK: {SIDECAR_URL}")
    except Exception as e:
        print(f"FEHLER: Sidecar nicht erreichbar auf {SIDECAR_URL}: {e}")
        sys.exit(1)

    # Frames laden
    with open(FRAME_INDEX, encoding="utf-8-sig") as f:
        all_frames = json.load(f)

    # Nur Referenz-Frames mit Schaden in Axialsicht
    frames = [
        f
        for f in all_frames
        if f["is_reference_frame"]
        and f.get("defekt_klasse")
        and f["szene_klasse"] == "axial"
        and get_yolo_class(f.get("code_main", "")) is not None
    ]

    print(f"Frames gesamt: {len(all_frames)}")
    print(f"Referenz-Frames fuer YOLO: {len(frames)}")

    # Train/Val Split (80/20)
    import random

    random.seed(42)
    random.shuffle(frames)
    split = int(len(frames) * 0.8)

    for split_name, split_frames in [
        ("train", frames[:split]),
        ("val", frames[split:]),
    ]:
        images_dir = OUTPUT_ROOT / split_name / "images"
        labels_dir = OUTPUT_ROOT / split_name / "labels"
        images_dir.mkdir(parents=True, exist_ok=True)
        labels_dir.mkdir(parents=True, exist_ok=True)

        stats = Counter()
        total = len(split_frames)

        for i, frame in enumerate(split_frames):
            if (i + 1) % 10 == 0 or i == 0:
                print(
                    f"  {split_name}: [{i + 1}/{total}] {frame.get('code_main', '?')}",
                    end="\r",
                )
            process_frame(frame, images_dir, labels_dir, stats)

        print(f"\n  {split_name}: {dict(stats)}")

    # data.yaml schreiben
    yaml_content = f"""path: {OUTPUT_ROOT}
train: train/images
val: val/images

nc: {len(CLASS_NAMES)}
names:
"""
    for idx in sorted(CLASS_NAMES.keys()):
        yaml_content += f"  {idx}: {CLASS_NAMES[idx]}\n"

    (OUTPUT_ROOT / "data.yaml").write_text(yaml_content)
    print(f"\nDataset: {OUTPUT_ROOT}")
    print(f"data.yaml: {OUTPUT_ROOT / 'data.yaml'}")

    # Negativ-Frames (ohne Label, leeres .txt)
    neg_frames = [
        f
        for f in all_frames
        if f["is_reference_frame"]
        and not f.get("defekt_klasse")
        and f["szene_klasse"] == "axial"
    ]
    if neg_frames:
        neg_dir_img = OUTPUT_ROOT / "train" / "images"
        neg_dir_lbl = OUTPUT_ROOT / "train" / "labels"
        neg_count = 0
        for nf in neg_frames[:50]:  # Max 50 Negativ-Frames
            png_path = Path(nf["png_pfad"])
            if not png_path.exists():
                png_path = Path(r"C:\KI_BRAIN\training_frames") / png_path.name
            if png_path.exists():
                import shutil

                dest = neg_dir_img / png_path.name
                if not dest.exists():
                    shutil.copy2(str(png_path), str(dest))
                # Leeres Label = kein Objekt
                (neg_dir_lbl / png_path.with_suffix(".txt").name).write_text("")
                neg_count += 1
        print(f"Negativ-Frames (leer): {neg_count}")


if __name__ == "__main__":
    main()
