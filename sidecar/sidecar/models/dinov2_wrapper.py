"""V4.2 Phase 3: DINOv2 Foundation-Encoder + Linear-Classifier-Heads.

Ersetzt die tote Grounding-DINO-Kaskade im Codier-Modus:
- DINOv2 ViT-L liefert gefrorene Bild-Features (1024-dim CLS token)
- Pro VSA-Hauptcode (BAB, BAC, BCA, ...) ein kleiner Linear-Head
  klassifiziert {not_present / mild / severe}.
- Heads werden aus `models/linear_heads/{code}.pt` lazy geladen.

Design-Entscheidung: DINOv2 wird NICHT im pre-warm geladen —
Codier-Modus laedt on-demand, sobald der erste Request kommt.
So bleibt der Sidecar-Start schnell und der VRAM-Peak niedrig,
solange DINOv2 nicht benutzt wird.
"""

from __future__ import annotations

import base64
import hashlib
import io
import logging
import threading
import time
from pathlib import Path

import torch
import torch.nn as nn
from PIL import Image

from ..config import settings
from ..gpu_manager import gpu_manager, ModelSlot
from ..schemas.dinov2 import DinoV2Prediction, DinoV2Response

logger = logging.getLogger(__name__)

# ── Head-State (separat vom Foundation-Encoder) ─────────────────────────────

# Linear-Heads: {vsa_code: nn.Linear} — CPU-resident, klein genug um immer geladen zu sein.
_heads: dict[str, nn.Linear] = {}
_heads_lock = threading.Lock()
_heads_loaded = False

# Klassen-Reihenfolge muss mit Training-Skript konsistent sein.
HEAD_CLASSES = ["not_present", "mild", "severe"]

# DINOv2 ViT-L liefert 1024-dim CLS-Features.
DINOV2_FEATURE_DIM = 1024

# HuggingFace-Modell-ID (Meta AI DINOv2 ViT-L/14).
DINOV2_MODEL_ID = "facebook/dinov2-large"


def _heads_dir() -> Path:
    """Verzeichnis mit den trainierten Linear-Heads."""
    return Path(settings.models_dir) / "linear_heads"


_heads_manifest_hash: str = ""


def _compute_heads_manifest_hash(d: Path) -> str:
    """
    V4.2 Nachbesserung B: Stabiler Hash ueber (Name, Groesse, Mtime) aller Head-Dateien.
    Aendert sich bei jedem Retrain → klare Versions-Signatur ohne Dateien einzulesen.
    """
    if not d.exists():
        return ""
    parts = []
    for pt in sorted(d.glob("*.pt")):
        stat = pt.stat()
        parts.append(f"{pt.name}:{stat.st_size}:{int(stat.st_mtime)}")
    if not parts:
        return ""
    h = hashlib.sha256("|".join(parts).encode("utf-8")).hexdigest()
    return h[:12]


def _load_heads_if_needed() -> None:
    """Laedt alle {code}.pt Heads aus dem heads_dir in den Speicher (einmalig)."""
    global _heads_loaded, _heads_manifest_hash
    if _heads_loaded:
        return
    with _heads_lock:
        if _heads_loaded:
            return
        d = _heads_dir()
        if not d.exists():
            logger.info("DINOv2 Linear-Heads: Verzeichnis %s existiert nicht — keine Heads geladen", d)
            _heads_loaded = True
            return

        count = 0
        for pt in d.glob("*.pt"):
            code = pt.stem.upper()
            try:
                head = nn.Linear(DINOV2_FEATURE_DIM, len(HEAD_CLASSES))
                state = torch.load(pt, map_location="cpu", weights_only=True)
                head.load_state_dict(state)
                head.eval()
                _heads[code] = head
                count += 1
            except Exception as exc:
                logger.warning("DINOv2-Head %s konnte nicht geladen werden: %s", code, exc)
        _heads_manifest_hash = _compute_heads_manifest_hash(d)
        logger.info("DINOv2 Linear-Heads geladen: %d (%s), manifest_hash=%s",
                    count, ", ".join(sorted(_heads.keys())), _heads_manifest_hash)
        _heads_loaded = True


# ── DINOv2 Foundation-Encoder ───────────────────────────────────────────────


def _resolve_device() -> str:
    device = getattr(settings, "effective_dinov2_device", None) or settings.effective_dino_device
    if device.startswith("cuda") and not torch.cuda.is_available():
        return "cpu"
    return device


def _load_dinov2_on(device: str) -> tuple[object, object]:
    """Laedt DINOv2 ViT-L/14 + Processor via transformers AutoModel."""
    from transformers import AutoImageProcessor, AutoModel

    logger.info("Lade DINOv2 (%s) auf %s ...", DINOV2_MODEL_ID, device)
    t0 = time.perf_counter()
    processor = AutoImageProcessor.from_pretrained(DINOV2_MODEL_ID)
    model = AutoModel.from_pretrained(DINOV2_MODEL_ID).to(device)
    model.eval()
    logger.info("DINOv2 geladen in %.1fs", time.perf_counter() - t0)
    return model, processor


def _ensure_encoder_loaded():
    """Stellt sicher, dass DINOv2 on-demand geladen ist."""
    device = _resolve_device()
    state = gpu_manager.ensure_loaded(
        ModelSlot.DINOV2,
        device,
        lambda: _load_dinov2_on(device),
    )
    return state.model, state.processor, state.device


@torch.no_grad()
def _extract_features(image: Image.Image) -> torch.Tensor:
    """Extrahiert 1024-dim CLS-Features aus einem Bild mit DINOv2."""
    model, processor, device = _ensure_encoder_loaded()
    inputs = processor(images=image, return_tensors="pt").to(device)
    outputs = model(**inputs)
    # last_hidden_state: [1, tokens, 1024] — CLS-Token an Position 0.
    cls = outputs.last_hidden_state[:, 0, :]  # [1, 1024]
    return cls.cpu().float()


# ── Public API ──────────────────────────────────────────────────────────────


def classify(image_base64: str, target_codes: list[str] | None = None) -> DinoV2Response:
    """
    Haupt-Endpunkt: Extrahiert Features + klassifiziert pro Head.
    Wenn keine Heads geladen sind (vor Phase 3.2 Training), wird eine leere
    Antwort zurueckgegeben — der Aufrufer faellt dann auf Qwen zurueck.
    """
    t_total = time.perf_counter()

    _load_heads_if_needed()
    heads_loaded = sorted(_heads.keys())
    if not _heads:
        # Keine Heads trainiert → leere Antwort. Encoder gar nicht erst laden.
        return DinoV2Response(
            predictions=[],
            heads_loaded=[],
            encoder_inference_time_ms=0.0,
            heads_inference_time_ms=0.0,
            total_time_ms=round((time.perf_counter() - t_total) * 1000, 1),
            encoder_version=DINOV2_MODEL_ID,
            heads_manifest_hash=_heads_manifest_hash,
        )

    # Filter auf angefragte Codes, sonst alle.
    active_codes = list(_heads.keys())
    if target_codes:
        wanted = {c.upper() for c in target_codes}
        active_codes = [c for c in active_codes if c in wanted]
    if not active_codes:
        return DinoV2Response(
            predictions=[],
            heads_loaded=heads_loaded,
            total_time_ms=round((time.perf_counter() - t_total) * 1000, 1),
            encoder_version=DINOV2_MODEL_ID,
            heads_manifest_hash=_heads_manifest_hash,
        )

    # Bild laden.
    try:
        raw = base64.b64decode(image_base64)
        image = Image.open(io.BytesIO(raw)).convert("RGB")
    except Exception as exc:
        logger.warning("DINOv2: Bild-Dekodierung fehlgeschlagen: %s", exc)
        return DinoV2Response(
            predictions=[],
            heads_loaded=heads_loaded,
            total_time_ms=round((time.perf_counter() - t_total) * 1000, 1),
            encoder_version=DINOV2_MODEL_ID,
            heads_manifest_hash=_heads_manifest_hash,
        )

    # Features extrahieren.
    t_enc = time.perf_counter()
    features = _extract_features(image)
    encoder_ms = (time.perf_counter() - t_enc) * 1000

    # Pro Code klassifizieren.
    t_heads = time.perf_counter()
    predictions: list[DinoV2Prediction] = []
    softmax = nn.Softmax(dim=-1)
    with torch.no_grad():
        for code in active_codes:
            head = _heads[code]
            logits = head(features)  # [1, 3]
            probs = softmax(logits).squeeze(0).tolist()
            idx = int(torch.argmax(logits, dim=-1).item())
            predictions.append(DinoV2Prediction(
                vsa_code=code,
                severity_class=HEAD_CLASSES[idx],
                confidence=float(probs[idx]),
                scores={cls: float(probs[i]) for i, cls in enumerate(HEAD_CLASSES)},
            ))
    heads_ms = (time.perf_counter() - t_heads) * 1000

    return DinoV2Response(
        predictions=predictions,
        heads_loaded=heads_loaded,
        encoder_inference_time_ms=round(encoder_ms, 1),
        heads_inference_time_ms=round(heads_ms, 1),
        total_time_ms=round((time.perf_counter() - t_total) * 1000, 1),
    )


def reload_heads() -> dict:
    """Entlaedt und laedt alle Linear-Heads neu (nach Training-Update)."""
    global _heads_loaded, _heads_manifest_hash
    with _heads_lock:
        _heads.clear()
        _heads_loaded = False
        _heads_manifest_hash = ""
    _load_heads_if_needed()
    return {
        "heads_loaded": sorted(_heads.keys()),
        "count": len(_heads),
        "encoder_version": DINOV2_MODEL_ID,
        "heads_manifest_hash": _heads_manifest_hash,
    }
