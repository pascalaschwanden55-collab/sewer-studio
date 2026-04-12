"""Hybrid Detection Wrapper: Grounding DINO (Primary) + Florence-2 (Shadow/Lernmodus).

DINO liefert die produktiven Detektionen.
Florence-2 laeuft still im Hintergrund mit, sieht DINOs Ergebnisse als Ground-Truth
und sammelt Trainingspaare fuer spaeteres Fine-Tuning.

GPU-Slot: ModelSlot.DINO (fuer DINO, persistent).
Florence-2 wird separat im RAM/VRAM gehalten (on-demand).

Architektur:
  Request → DINO detect() → Ergebnis sofort zurueck an C#
                ↓ (async, non-blocking)
           Florence-2 Shadow → Vergleich → Trainingspaar speichern
"""

from __future__ import annotations

import asyncio
import base64
import io
import json
import os
import time
import logging
import threading
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from datetime import datetime

import numpy as np
from PIL import Image

from ..config import settings
from ..gpu_manager import gpu_manager, ModelSlot
from ..schemas.detection import DinoDetection, DinoResponse

logger = logging.getLogger(__name__)

# ── Florence-2 Shadow State ──────────────────────────────────────────────

_florence2_model = None
_florence2_processor = None
_florence2_lock = threading.Lock()
_shadow_enabled: bool = True
_shadow_log_dir = Path(os.environ.get(
    "SEWER_FLORENCE2_SHADOW_DIR",
    str(Path(settings.models_dir).parent / "florence2_shadow_log")
))

# Zaehler fuer Shadow-Statistik
_shadow_stats = {"total": 0, "match": 0, "mismatch": 0, "errors": 0}
_shadow_executor: ThreadPoolExecutor | None = None
_shadow_executor_lock = threading.Lock()


# ══════════════════════════════════════════════════════════════════════════
# DINO (Primary) — Grounding DINO 1.5
# ══════════════════════════════════════════════════════════════════════════

def _find_dino_files() -> tuple[str, str]:
    """Locate Grounding DINO config and weights in models_dir."""
    models = Path(settings.models_dir) / "grounding_dino_1.5"
    config_candidates = list(models.glob("*config*.py")) + list(models.glob("*cfg*.py"))
    weight_candidates = list(models.glob("*.pth")) + list(models.glob("*.pt"))

    if not config_candidates or not weight_candidates:
        raise FileNotFoundError(
            f"Grounding DINO config/weights nicht gefunden in {models}. "
            "Bitte GroundingDINO config (.py) und weights (.pth) dort ablegen."
        )
    return str(config_candidates[0]), str(weight_candidates[0])


def _resolve_device() -> str:
    """Determine the effective device for DINO inference."""
    device = settings.effective_dino_device
    if device.startswith("cuda") and not _cuda_available():
        return "cpu"
    return device


def _resolve_florence2_device() -> str:
    """Determine the effective device for Florence-2 Shadow."""
    device = settings.florence2_shadow_device or settings.gpu_device
    if device.startswith("cuda") and not _cuda_available():
        logger.warning(
            "Florence-2 Shadow configured for %s but CUDA unavailable, falling back to cpu",
            device,
        )
        return "cpu"
    return device


def _get_shadow_executor() -> ThreadPoolExecutor:
    global _shadow_executor
    if _shadow_executor is not None:
        return _shadow_executor

    with _shadow_executor_lock:
        if _shadow_executor is None:
            workers = max(1, min(8, settings.shadow_worker_count))
            _shadow_executor = ThreadPoolExecutor(
                max_workers=workers,
                thread_name_prefix="florence2-shadow",
            )
            logger.info("Florence-2 Shadow executor created with %d workers", workers)
    return _shadow_executor


def _load_dino_on(device: str):
    """Load Grounding DINO model onto *device*. Returns (model, None)."""
    try:
        from groundingdino.util.inference import load_model
    except ImportError:
        raise ImportError(
            "groundingdino-py ist nicht installiert. "
            "Install mit: pip install groundingdino-py"
        )

    config_path, weights_path = _find_dino_files()
    model = load_model(config_path, weights_path, device=device)
    return model, None


# Alias fuer training.py / lora_training.py Kompatibilitaet
_load_florence2_on = _load_dino_on


def _cuda_available() -> bool:
    try:
        import torch
        return torch.cuda.is_available()
    except Exception:
        return False


def detect(
    image_base64: str,
    text_prompt: str | None,
    box_threshold: float,
    text_threshold: float,
) -> DinoResponse:
    """Run Grounding DINO Detection (Primary) + Florence-2 Shadow (async).

    DINO-Ergebnis wird sofort zurueckgegeben.
    Florence-2 laeuft im Hintergrund und loggt den Vergleich.
    """
    device = _resolve_device()
    state = gpu_manager.ensure_loaded(
        ModelSlot.DINO, device, lambda: _load_dino_on(device)
    )
    model = state.model
    prompt = text_prompt or settings.dino_labels

    raw = base64.b64decode(image_base64)
    img = Image.open(io.BytesIO(raw)).convert("RGB")
    img_array = np.array(img)

    t0 = time.perf_counter()

    try:
        from groundingdino.util.inference import predict
        import torch
        from torchvision import transforms

        transform = transforms.Compose([
            transforms.ToTensor(),
            transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
        ])
        img_tensor = transform(img)

        with torch.cuda.amp.autocast(enabled=device.startswith("cuda")):
            boxes, logits, phrases = predict(
                model=model,
                image=img_tensor,
                caption=prompt,
                box_threshold=box_threshold,
                text_threshold=text_threshold,
            )
    except Exception as exc:
        logger.error("DINO Inference fehlgeschlagen: %s", exc)
        return DinoResponse(
            detections=[],
            inference_time_ms=round((time.perf_counter() - t0) * 1000, 1),
        )

    elapsed_ms = (time.perf_counter() - t0) * 1000
    h, w = img_array.shape[:2]

    detections: list[DinoDetection] = []
    for box, logit, phrase in zip(boxes, logits, phrases):
        cx, cy, bw, bh = box.tolist()
        x1 = (cx - bw / 2) * w
        y1 = (cy - bh / 2) * h
        x2 = (cx + bw / 2) * w
        y2 = (cy + bh / 2) * h
        detections.append(DinoDetection(
            x1=round(x1, 1),
            y1=round(y1, 1),
            x2=round(x2, 1),
            y2=round(y2, 1),
            label=phrase.strip(),
            confidence=round(float(logit), 4),
            phrase=phrase.strip(),
        ))

    dino_response = DinoResponse(
        detections=detections,
        inference_time_ms=round(elapsed_ms, 1),
    )

    # Florence-2 Shadow: async im Hintergrund, blockiert NICHT den Response
    _shadow_stats["_request_count"] = _shadow_stats.get("_request_count", 0) + 1
    shadow_every_n = settings.shadow_every_n
    if _shadow_enabled and detections and _shadow_stats["_request_count"] % shadow_every_n == 0:
        executor = _get_shadow_executor()
        executor.submit(
            _florence2_shadow_compare,
            image_base64,
            prompt,
            detections,
            w,
            h,
        )

    return dino_response


# ══════════════════════════════════════════════════════════════════════════
# Florence-2 (Shadow / Lernmodus)
# ══════════════════════════════════════════════════════════════════════════

def _ensure_florence2_loaded():
    """Laedt Florence-2 lazy beim ersten Shadow-Call."""
    global _florence2_model, _florence2_processor

    if _florence2_model is not None:
        return

    with _florence2_lock:
        if _florence2_model is not None:
            return

        try:
            from transformers import AutoModelForCausalLM, AutoProcessor
            import torch

            device = _resolve_florence2_device()
            if device == "cpu":
                torch.set_num_threads(settings.cpu_threads)
                torch.set_num_interop_threads(settings.cpu_threads)

            # Zuerst -ft Version versuchen, dann Standard
            for subdir in ["florence-2-ft", "florence-2"]:
                model_dir = Path(settings.models_dir) / subdir
                if model_dir.exists() and any(model_dir.glob("*.safetensors")):
                    break
            else:
                logger.warning("Florence-2 Shadow: Kein Modell gefunden — Shadow deaktiviert")
                return

            logger.info("Florence-2 Shadow: Lade %s auf %s...", model_dir.name, device)
            dtype = torch.float16 if device.startswith("cuda") else torch.float32
            _florence2_model = AutoModelForCausalLM.from_pretrained(
                str(model_dir), trust_remote_code=True,
                torch_dtype=dtype,
            ).to(device).eval()
            _florence2_processor = AutoProcessor.from_pretrained(
                str(model_dir), trust_remote_code=True
            )
            logger.info("Florence-2 Shadow geladen (%s) auf %s", model_dir.name, device)

        except Exception as exc:
            logger.warning("Florence-2 Shadow laden fehlgeschlagen: %s", exc)


def _florence2_shadow_compare(
    image_base64: str,
    text_prompt: str,
    dino_detections: list[DinoDetection],
    img_w: int,
    img_h: int,
) -> None:
    """Florence-2 analysiert dasselbe Bild und vergleicht mit DINO.

    Ergebnis wird als Trainingspaar gespeichert:
    - Bild (base64)
    - DINO-Detektionen (Ground Truth)
    - Florence-2-Detektionen (Prediction)
    - Match/Mismatch Score
    """
    try:
        _ensure_florence2_loaded()
        if _florence2_model is None:
            return

        import torch

        raw = base64.b64decode(image_base64)
        img = Image.open(io.BytesIO(raw)).convert("RGB")

        task = "<CAPTION_TO_PHRASE_GROUNDING>"
        grounding_text = text_prompt or settings.dino_labels
        inputs = _florence2_processor(
            text=task + grounding_text, images=img, return_tensors="pt"
        )
        device = _resolve_florence2_device()
        dtype = next(_florence2_model.parameters()).dtype
        inputs = {
            k: v.to(device=device, dtype=dtype) if v.is_floating_point() else v.to(device)
            for k, v in inputs.items()
        }

        with torch.inference_mode():
            ids = _florence2_model.generate(
                **inputs, max_new_tokens=1024, num_beams=3, do_sample=False
            )

        text = _florence2_processor.batch_decode(ids, skip_special_tokens=False)[0]
        result = _florence2_processor.post_process_generation(
            text, task=task, image_size=(img.width, img.height)
        )
        od = result.get(task, {})
        f2_labels = od.get("labels", [])
        f2_bboxes = od.get("bboxes", [])

        # Vergleich: wie viele DINO-Labels hat Florence-2 auch gefunden?
        dino_labels_set = {d.label.lower() for d in dino_detections}
        f2_labels_set = {l.lower().strip() for l in f2_labels}
        overlap = dino_labels_set & f2_labels_set
        match_ratio = len(overlap) / max(len(dino_labels_set), 1)

        _shadow_stats["total"] += 1
        if match_ratio >= 0.5:
            _shadow_stats["match"] += 1
        else:
            _shadow_stats["mismatch"] += 1

        # Trainingspaar speichern (alle 10 Frames loggen, bei Mismatch immer)
        should_log = match_ratio < 0.5 or _shadow_stats["total"] % 10 == 0
        if should_log:
            _save_training_pair(
                dino_detections, f2_labels, f2_bboxes,
                img_w, img_h, match_ratio, image_base64,
            )

        if _shadow_stats["total"] % 50 == 0:
            logger.info(
                "Florence-2 Shadow Stats: %d total, %d match (%.0f%%), %d mismatch, %d errors",
                _shadow_stats["total"],
                _shadow_stats["match"],
                _shadow_stats["match"] / max(_shadow_stats["total"], 1) * 100,
                _shadow_stats["mismatch"],
                _shadow_stats["errors"],
            )

    except Exception as exc:
        _shadow_stats["errors"] += 1
        if _shadow_stats["errors"] <= 5:
            logger.debug("Florence-2 Shadow Fehler: %s", exc)


def _save_training_pair(
    dino_dets: list[DinoDetection],
    f2_labels: list[str],
    f2_bboxes: list,
    img_w: int,
    img_h: int,
    match_ratio: float,
    image_base64: str,
) -> None:
    """Speichert ein Trainingspaar fuer spaeteres Florence-2 Fine-Tuning."""
    _shadow_log_dir.mkdir(parents=True, exist_ok=True)

    pair = {
        "timestamp": datetime.now().isoformat(),
        "image_size": [img_w, img_h],
        "match_ratio": round(match_ratio, 3),
        "dino_detections": [
            {"label": d.label, "confidence": d.confidence,
             "bbox": [d.x1, d.y1, d.x2, d.y2]}
            for d in dino_dets
        ],
        "florence2_labels": f2_labels,
        "florence2_bboxes": f2_bboxes,
    }

    # Bild nur bei Mismatch speichern (Platz sparen)
    if match_ratio < 0.3:
        img_path = _shadow_log_dir / f"frame_{_shadow_stats['total']:06d}.jpg"
        raw = base64.b64decode(image_base64)
        img = Image.open(io.BytesIO(raw)).convert("RGB")
        img.save(str(img_path), quality=85)
        pair["image_file"] = img_path.name

    # JSONL Log (eine Zeile pro Paar)
    log_path = _shadow_log_dir / "training_pairs.jsonl"
    with open(log_path, "a", encoding="utf-8") as f:
        f.write(json.dumps(pair, ensure_ascii=False) + "\n")


def get_shadow_stats() -> dict:
    """Gibt aktuelle Shadow-Statistik zurueck (fuer Health-Endpoint)."""
    return {
        "shadow_enabled": _shadow_enabled,
        "shadow_model": "florence-2-ft" if _florence2_model is not None else "not_loaded",
        **_shadow_stats,
        "match_rate": round(
            _shadow_stats["match"] / max(_shadow_stats["total"], 1) * 100, 1
        ),
    }
