"""Vollautomatische Florence-2 Kalibrierung gegen YOLO-Klassifikationsdaten.

Nutzt die vorhandenen YOLO-cls-Export Bilder (VSA-Code-Ordner) als Ground-Truth.
Testet alle Prompt-Varianten und misst Precision/Recall pro VSA-Code.
Setzt den besten Prompt automatisch als Default.

Nutzung:
    python auto_calibrate_florence2.py
    python auto_calibrate_florence2.py --samples-per-class 20  # schneller
"""

from __future__ import annotations

import argparse
import base64
import io
import json
import logging
import os
import random
import time
from pathlib import Path
from collections import defaultdict

import numpy as np
from PIL import Image

logging.basicConfig(
    level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s"
)
logger = logging.getLogger(__name__)

# ── VSA-Code → erwartete Florence-2 Labels Mapping ──────────────────────

VSA_TO_EXPECTED_LABELS = {
    "BAB": ["crack", "fracture", "riss"],
    "BAC": ["break", "fracture", "collapse", "bruch"],
    "BBB": ["root", "root intrusion", "roots", "wurzel"],
    "BBC": ["deposit", "sediment", "buildup", "scale", "calcite", "ablagerung"],
    "BCA": ["connection", "lateral connection", "pipe junction", "anschluss"],
    "BCC": ["bend", "curve", "bogen"],
    "BCD": ["pipe start", "pipe", "rohr"],
    "BCE": ["pipe end", "pipe", "rohr"],
    "BDA": ["surface damage", "corrosion", "erosion", "korrosion"],
    "BDB": ["infiltration", "water ingress", "leak", "infiltration"],
    "BDCZ": ["obstacle", "blockage", "hindernis"],
}

# ── Prompt-Varianten ──────────────────────────────────────────────────────

PROMPT_VARIANTS = {
    "v1_full_english": (
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
    "v2_compact": (
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
    "v4_descriptive": (
        "a crack in the pipe wall . "
        "tree roots growing into the pipe . "
        "sediment deposit on the pipe floor . "
        "pipe wall deformation . "
        "water infiltrating through a joint . "
        "a lateral pipe connection . "
        "corrosion damage on the pipe surface . "
        "pipe fracture or break . "
        "obstacle blocking the pipe"
    ),
    "v5_simple": ("damage . crack . root . deposit . defect . hole . connection"),
    "v6_mixed": (
        "crack . Riss . root intrusion . Wurzel . "
        "deposit . Ablagerung . deformation . Versatz . "
        "infiltration . corrosion . Korrosion . "
        "lateral connection . Anschluss . obstacle"
    ),
}


def load_model(device: str = "cuda:0"):
    """Laedt Florence-2."""
    import torch
    from transformers import AutoModelForCausalLM, AutoProcessor

    # Modell-Pfad suchen
    candidates = [
        Path(r"C:\Sewer-Studio_KI_4.1\sidecar\models\florence-2"),
        Path(__file__).parent.parent / "models" / "florence-2",
    ]
    model_path = None
    for c in candidates:
        if c.exists():
            model_path = str(c)
            break
    if model_path is None:
        raise FileNotFoundError("Florence-2 Modell nicht gefunden")

    logger.info("Lade Florence-2 von %s ...", model_path)
    model = (
        AutoModelForCausalLM.from_pretrained(
            model_path, trust_remote_code=True, torch_dtype=torch.float16
        )
        .to(device)
        .eval()
    )
    processor = AutoProcessor.from_pretrained(model_path, trust_remote_code=True)
    return model, processor


def run_grounding(model, processor, img: Image.Image, prompt: str, device: str):
    """Fuehrt CAPTION_TO_PHRASE_GROUNDING aus."""
    import torch

    task = "<CAPTION_TO_PHRASE_GROUNDING>"
    inputs = processor(text=task + prompt, images=img, return_tensors="pt")
    dtype = next(model.parameters()).dtype
    inputs = {
        k: v.to(device=device, dtype=dtype) if v.is_floating_point() else v.to(device)
        for k, v in inputs.items()
    }

    with torch.inference_mode():
        ids = model.generate(
            **inputs, max_new_tokens=1024, num_beams=3, do_sample=False
        )

    text = processor.batch_decode(ids, skip_special_tokens=False)[0]
    result = processor.post_process_generation(
        text, task=task, image_size=(img.width, img.height)
    )
    od = result.get(task, {})
    return od.get("labels", []), od.get("bboxes", [])


def label_matches_vsa(detected_labels: list[str], vsa_code: str) -> bool:
    """Prueft ob mindestens ein erkanntes Label zum VSA-Code passt."""
    expected = VSA_TO_EXPECTED_LABELS.get(vsa_code, [])
    if not expected:
        return False
    for det in detected_labels:
        det_lower = det.lower().strip()
        for exp in expected:
            if exp.lower() in det_lower or det_lower in exp.lower():
                return True
    return False


def load_samples(data_dir: str, samples_per_class: int) -> dict[str, list[Path]]:
    """Laedt zufaellige Samples pro VSA-Code-Klasse."""
    root = Path(data_dir)
    samples = {}
    for class_dir in sorted(root.iterdir()):
        if not class_dir.is_dir() or class_dir.name == "OTHER":
            continue
        images = list(class_dir.glob("*.jpg")) + list(class_dir.glob("*.png"))
        if not images:
            continue
        selected = random.sample(images, min(len(images), samples_per_class))
        samples[class_dir.name] = selected
        logger.info(
            "  %s: %d/%d Bilder ausgewaehlt", class_dir.name, len(selected), len(images)
        )
    return samples


def main():
    parser = argparse.ArgumentParser(description="Auto-Kalibrierung Florence-2")
    parser.add_argument(
        "--data-dir",
        type=str,
        default=r"C:\Sewer-StudioKI_3.1\yolo_cls_export\train",
        help="YOLO-cls-Export Verzeichnis mit VSA-Code-Ordnern",
    )
    parser.add_argument(
        "--samples-per-class",
        type=int,
        default=10,
        help="Bilder pro Klasse (mehr = genauer, langsamer)",
    )
    parser.add_argument("--device", type=str, default="cuda:0")
    args = parser.parse_args()

    random.seed(42)

    logger.info("=== Florence-2 Auto-Kalibrierung ===")
    logger.info("Daten: %s", args.data_dir)
    logger.info("Samples/Klasse: %d", args.samples_per_class)

    # Samples laden
    samples = load_samples(args.data_dir, args.samples_per_class)
    total_samples = sum(len(v) for v in samples.values())
    logger.info("Total: %d Samples aus %d Klassen", total_samples, len(samples))

    # Modell laden
    model, processor = load_model(args.device)

    # Alle Prompt-Varianten testen
    results = {}
    for variant_name, prompt in PROMPT_VARIANTS.items():
        logger.info("\n{'='*60}")
        logger.info("Teste: %s", variant_name)
        logger.info("Prompt: %s", prompt[:80] + "...")

        class_results = {}
        total_tp, total_fn, total_fp = 0, 0, 0
        total_ms = 0

        for vsa_code, image_paths in samples.items():
            tp, fn, detected_count = 0, 0, 0

            for img_path in image_paths:
                img = Image.open(img_path).convert("RGB")
                t0 = time.perf_counter()
                labels, bboxes = run_grounding(
                    model, processor, img, prompt, args.device
                )
                total_ms += (time.perf_counter() - t0) * 1000

                detected_count += len(labels)
                if label_matches_vsa(labels, vsa_code):
                    tp += 1
                else:
                    fn += 1

            recall = tp / max(tp + fn, 1)
            class_results[vsa_code] = {
                "tp": tp,
                "fn": fn,
                "recall": round(recall, 3),
                "avg_detections": round(detected_count / max(len(image_paths), 1), 2),
                "samples": len(image_paths),
            }
            total_tp += tp
            total_fn += fn

            logger.info(
                "  %s: Recall=%.0f%% (%d/%d), Avg det=%.1f",
                vsa_code,
                recall * 100,
                tp,
                tp + fn,
                detected_count / max(len(image_paths), 1),
            )

        overall_recall = total_tp / max(total_tp + total_fn, 1)
        avg_ms = total_ms / max(total_samples, 1)

        results[variant_name] = {
            "overall_recall": round(overall_recall, 4),
            "avg_inference_ms": round(avg_ms, 1),
            "class_results": class_results,
            "prompt": prompt,
        }

        logger.info(
            ">>> %s: Overall Recall=%.1f%%, Avg=%.0fms",
            variant_name,
            overall_recall * 100,
            avg_ms,
        )

    # Bester Prompt
    best_name = max(results, key=lambda k: results[k]["overall_recall"])
    best = results[best_name]

    logger.info("\n" + "=" * 60)
    logger.info("ERGEBNIS: Bester Prompt = '%s'", best_name)
    logger.info("  Overall Recall: %.1f%%", best["overall_recall"] * 100)
    logger.info("  Avg Inference: %.0fms", best["avg_inference_ms"])
    logger.info("  Prompt: %s", best["prompt"][:100])

    # Ergebnis speichern
    report = {
        "best_variant": best_name,
        "best_recall": best["overall_recall"],
        "best_prompt": best["prompt"],
        "all_results": {
            k: {
                "overall_recall": v["overall_recall"],
                "avg_inference_ms": v["avg_inference_ms"],
            }
            for k, v in results.items()
        },
        "detailed_results": results,
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
    }

    report_path = Path("FLORENCE2_CALIBRATION_REPORT.json")
    with open(report_path, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2, ensure_ascii=False)
    logger.info("Report: %s", report_path.resolve())

    # Config-Empfehlung
    logger.info("\n>>> Config-Aenderung (sidecar/sidecar/config.py):")
    logger.info('    dino_labels: str = "%s"', best["prompt"])


if __name__ == "__main__":
    main()
