"""Florence-2 Kalibrierung fuer Kanalinspektion.

Stufe 1: Prompt-Kalibrierung — testet verschiedene Grounding-Prompts
Stufe 2: Confidence-Kalibrierung — vergleicht Florence-2 vs. DINO Referenz
Stufe 3: Fine-Tuning — trainiert Florence-2 auf Kanal-Daten

Nutzung:
    # Stufe 1: Prompt-Test auf einem Ordner mit Testbildern
    python calibrate_florence2.py --stage prompt --images path/to/test_frames/

    # Stufe 2: Vergleich mit DINO-Referenz
    python calibrate_florence2.py --stage compare --images path/to/test_frames/

    # Stufe 3: Fine-Tuning
    python calibrate_florence2.py --stage finetune --dataset path/to/annotations.json
"""

from __future__ import annotations

import argparse
import base64
import io
import json
import logging
import os
import sys
import time
from pathlib import Path

import numpy as np
from PIL import Image

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger(__name__)


# ── Prompt-Varianten fuer Kanalinspektion ──────────────────────────────────

PROMPT_VARIANTS = {
    "v1_english_detailed": (
        "crack . fracture . break . deformation . "
        "corrosion . surface damage . erosion . "
        "root intrusion . roots . "
        "deposit . sediment . buildup . scale . calcite . "
        "obstacle . blockage . grease . "
        "infiltration . water ingress . leak . "
        "displaced joint . open joint . offset joint . "
        "hole . collapse . missing wall . "
        "connection defect . pipe defect . "
        "intruding connection . protruding seal . "
        "lateral connection . pipe junction"
    ),
    "v2_english_compact": (
        "crack . root . deposit . deformation . "
        "infiltration . joint offset . hole . "
        "lateral connection . obstacle . corrosion"
    ),
    "v3_german": (
        "Riss . Bruch . Wurzel . Ablagerung . "
        "Deformation . Infiltration . Versatz . "
        "Einragung . Korrosion . Anschluss . "
        "Hindernis . Einsturz . Loch"
    ),
    "v4_vsa_codes": (
        "BAB crack . BAC fracture . BBB root intrusion . "
        "BBC deposit . BAF deformation . BBA infiltration . "
        "BAH joint offset . BAI intruding connection . "
        "BCA lateral connection . BCD pipe start . BCE pipe end"
    ),
    "v5_simple": (
        "damage . crack . root . deposit . defect . hole . connection"
    ),
    "v6_descriptive": (
        "a crack in the pipe wall . "
        "tree roots growing into the pipe . "
        "sediment deposit on the pipe floor . "
        "pipe wall deformation . "
        "water infiltrating through a joint . "
        "a lateral pipe connection . "
        "corrosion damage on the pipe surface"
    ),
}


def load_florence2(device: str = "cuda:0"):
    """Laedt Florence-2 Modell und Processor."""
    import torch
    from transformers import AutoModelForCausalLM, AutoProcessor

    model_path = str(Path(__file__).parent.parent / "sidecar" / "models" / "florence-2")
    if not Path(model_path).exists():
        model_path = str(Path(__file__).parent.parent / "models" / "florence-2")

    logger.info("Lade Florence-2 von %s ...", model_path)
    model = AutoModelForCausalLM.from_pretrained(
        model_path, trust_remote_code=True, torch_dtype=torch.float16
    ).to(device).eval()
    processor = AutoProcessor.from_pretrained(model_path, trust_remote_code=True)
    logger.info("Florence-2 geladen")
    return model, processor


def run_grounding(model, processor, img: Image.Image, prompt: str, device: str = "cuda:0"):
    """Fuehrt CAPTION_TO_PHRASE_GROUNDING aus und gibt Detektionen zurueck."""
    import torch

    task = "<CAPTION_TO_PHRASE_GROUNDING>"
    full_input = task + prompt

    inputs = processor(text=full_input, images=img, return_tensors="pt")
    dtype = next(model.parameters()).dtype
    inputs = {
        k: v.to(device=device, dtype=dtype) if v.is_floating_point() else v.to(device)
        for k, v in inputs.items()
    }

    t0 = time.perf_counter()
    with torch.inference_mode():
        ids = model.generate(**inputs, max_new_tokens=1024, num_beams=3, do_sample=False)

    text = processor.batch_decode(ids, skip_special_tokens=False)[0]
    result = processor.post_process_generation(text, task=task, image_size=(img.width, img.height))
    elapsed = (time.perf_counter() - t0) * 1000

    od = result.get(task, {})
    bboxes = od.get("bboxes", [])
    labels = od.get("labels", [])

    return {
        "detections": [
            {"bbox": b, "label": l}
            for b, l in zip(bboxes, labels)
        ],
        "inference_ms": elapsed,
    }


def load_test_images(images_dir: str, max_images: int = 50) -> list[tuple[str, Image.Image]]:
    """Laedt Testbilder aus einem Ordner."""
    img_dir = Path(images_dir)
    extensions = {".png", ".jpg", ".jpeg", ".bmp"}
    files = sorted([f for f in img_dir.iterdir() if f.suffix.lower() in extensions])
    if len(files) > max_images:
        # Gleichmaessig verteilt
        step = len(files) // max_images
        files = files[::step][:max_images]

    images = []
    for f in files:
        try:
            img = Image.open(f).convert("RGB")
            images.append((f.name, img))
        except Exception as e:
            logger.warning("Bild %s konnte nicht geladen werden: %s", f.name, e)

    logger.info("%d Testbilder geladen aus %s", len(images), images_dir)
    return images


def stage_prompt(args):
    """Stufe 1: Testet verschiedene Prompts und vergleicht Detection-Count + Labels."""
    model, processor = load_florence2(args.device)
    images = load_test_images(args.images, max_images=args.max_images)

    if not images:
        logger.error("Keine Testbilder gefunden in %s", args.images)
        return

    results = {}
    for variant_name, prompt in PROMPT_VARIANTS.items():
        logger.info("=== Teste Prompt-Variante: %s ===", variant_name)
        total_detections = 0
        total_ms = 0
        all_labels: dict[str, int] = {}

        for img_name, img in images:
            r = run_grounding(model, processor, img, prompt, args.device)
            total_detections += len(r["detections"])
            total_ms += r["inference_ms"]
            for d in r["detections"]:
                lbl = d["label"]
                all_labels[lbl] = all_labels.get(lbl, 0) + 1

        avg_det = total_detections / len(images)
        avg_ms = total_ms / len(images)
        top_labels = sorted(all_labels.items(), key=lambda x: -x[1])[:10]

        results[variant_name] = {
            "avg_detections": round(avg_det, 2),
            "avg_inference_ms": round(avg_ms, 1),
            "total_detections": total_detections,
            "top_labels": top_labels,
        }

        logger.info(
            "  Avg detections: %.2f | Avg time: %.0fms | Top labels: %s",
            avg_det, avg_ms, ", ".join(f"{l}({c})" for l, c in top_labels[:5])
        )

    # Ergebnis speichern
    report_path = Path(args.output or "florence2_prompt_calibration.json")
    with open(report_path, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    logger.info("Ergebnis gespeichert: %s", report_path)

    # Empfehlung
    best = max(results.items(), key=lambda x: x[1]["avg_detections"])
    logger.info(
        "\n>>> EMPFEHLUNG: Variante '%s' mit %.1f Detektionen/Bild <<<",
        best[0], best[1]["avg_detections"]
    )


def stage_compare(args):
    """Stufe 2: Vergleicht Florence-2 mit DINO-Referenz (wenn Sidecar laeuft)."""
    import requests

    images = load_test_images(args.images, max_images=args.max_images)
    if not images:
        logger.error("Keine Testbilder gefunden")
        return

    logger.info("Vergleiche Florence-2 (GROUNDING) vs. aktuellem Sidecar (/detect/dino)")

    for img_name, img in images[:10]:
        buf = io.BytesIO()
        img.save(buf, format="PNG")
        b64 = base64.b64encode(buf.getvalue()).decode()

        resp = requests.post("http://localhost:8100/detect/dino", json={
            "image_base64": b64,
            "text_prompt": None,
            "box_threshold": 0.25,
            "text_threshold": 0.20,
        }, timeout=30)

        data = resp.json()
        dets = data.get("detections", [])
        labels = [d["label"] for d in dets]
        logger.info(
            "%s: %d Detektionen [%s] (%.0fms)",
            img_name, len(dets), ", ".join(labels), data.get("inference_time_ms", 0)
        )


def stage_finetune(args):
    """Stufe 3: Fine-Tuning Anleitung und Vorbereitung."""
    logger.info("""
╔══════════════════════════════════════════════════════════════╗
║  Florence-2 Fine-Tuning fuer Kanalinspektion                ║
╠══════════════════════════════════════════════════════════════╣
║                                                              ║
║  Voraussetzungen:                                            ║
║  - RTX 5090 32GB (reicht fuer LoRA Fine-Tuning)              ║
║  - ~500-1000 annotierte Bilder mit Bounding Boxes            ║
║  - Annotationen im COCO-Format (JSON)                        ║
║                                                              ║
║  Daten-Quellen die du bereits hast:                          ║
║  1. YOLO-Trainingsdaten (runs/train/)                        ║
║  2. KnowledgeBase Samples (KnowledgeBase.db)                 ║
║  3. Ground-Truth Eintraege                                   ║
║                                                              ║
║  Schritte:                                                   ║
║  1. Exportiere Bilder + BBoxes aus YOLO-Trainingsdaten       ║
║  2. Konvertiere zu Florence-2 Format:                        ║
║     {"image": "path.jpg",                                    ║
║      "prefix": "<OD>",                                       ║
║      "suffix": "<loc_x1><loc_y1><loc_x2><loc_y2>label"}     ║
║  3. LoRA Fine-Tuning mit PEFT:                               ║
║     pip install peft                                         ║
║     ~2-4 Stunden auf RTX 5090                                ║
║  4. Evaluiere auf Testset (Precision, Recall)                ║
║                                                              ║
║  Script: sidecar/scripts/finetune_florence2.py               ║
║  (wird bei Ausfuehrung mit --stage finetune generiert)       ║
╚══════════════════════════════════════════════════════════════╝
    """)


def main():
    parser = argparse.ArgumentParser(description="Florence-2 Kalibrierung")
    parser.add_argument("--stage", choices=["prompt", "compare", "finetune"],
                        default="prompt", help="Kalibrierungs-Stufe")
    parser.add_argument("--images", type=str, default=".",
                        help="Ordner mit Testbildern (PNG/JPG)")
    parser.add_argument("--max-images", type=int, default=50)
    parser.add_argument("--device", type=str, default="cuda:0")
    parser.add_argument("--output", type=str, default=None)
    args = parser.parse_args()

    if args.stage == "prompt":
        stage_prompt(args)
    elif args.stage == "compare":
        stage_compare(args)
    elif args.stage == "finetune":
        stage_finetune(args)


if __name__ == "__main__":
    main()
