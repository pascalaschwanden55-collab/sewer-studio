"""V4.2 Phase 3.2: Trainiert Linear-Classifier-Heads auf DINOv2-Features.

Idee:
  - DINOv2 ViT-L liefert gefrorene 1024-dim CLS-Features pro Bild.
  - Pro VSA-Hauptcode (BAB, BAC, BCA, ...) ein kleiner nn.Linear(1024, 3) Head,
    der {not_present / mild / severe} klassifiziert.
  - Trainingsdaten kommen aus TeacherAnnotation-JSON (Knowledge/teacher_annotations.json),
    die durch die Review-Queue in Phase 1.5 wachsen.

Hinweis:
  Dieses Skript ist ein ehrlicher Startpunkt, keine SOTA-Pipeline.
  Die Defaults sind konservativ gewaehlt (wenig Daten -> wenig Overfitting).
  Pascal kann Hyperparameter nach Bedarf anpassen.

CLI:
  python tools/train_linear_heads.py \
      --annotations C:/KI_BRAIN/Knowledge/teacher_annotations.json \
      --heads-dir   C:/KI_BRAIN/models/linear_heads \
      --min-samples 5 \
      --epochs 50
"""

from __future__ import annotations

import argparse
import json
import logging
import random
from collections import defaultdict
from pathlib import Path

import torch
import torch.nn as nn
from PIL import Image

logger = logging.getLogger("train_linear_heads")
logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")

# Klassenreihenfolge muss konsistent mit sidecar/models/dinov2_wrapper.py sein.
HEAD_CLASSES = ["not_present", "mild", "severe"]
DINOV2_FEATURE_DIM = 1024
DINOV2_MODEL_ID = "facebook/dinov2-large"


def load_annotations(path: Path) -> list[dict]:
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


def severity_to_class(severity: int | None) -> str:
    """Mapping Severity 1-5 auf die drei Head-Klassen."""
    if severity is None or severity <= 1:
        return "mild"  # Grundgeruest/leichte Befunde landen als mild
    if severity <= 3:
        return "mild"
    return "severe"


def main_code(vsa_code: str) -> str:
    """Erste drei Zeichen als Hauptcode (BAB, BAC, BCA, ...)."""
    code = (vsa_code or "").strip().upper()
    return code[:3] if len(code) >= 3 else code


@torch.no_grad()
def extract_features_for(annotations: list[dict], device: str) -> list[tuple[str, torch.Tensor]]:
    """Extrahiert DINOv2-Features fuer alle Annotationen mit gueltigem Frame.
    Gibt Liste (main_code, feature_tensor) zurueck.
    """
    from transformers import AutoImageProcessor, AutoModel

    logger.info("Lade DINOv2 (%s) auf %s ...", DINOV2_MODEL_ID, device)
    processor = AutoImageProcessor.from_pretrained(DINOV2_MODEL_ID)
    model = AutoModel.from_pretrained(DINOV2_MODEL_ID).to(device)
    model.eval()

    samples: list[tuple[str, torch.Tensor]] = []
    for a in annotations:
        frame_path = a.get("FullFramePath") or a.get("CroppedRegionPath")
        if not frame_path or not Path(frame_path).exists():
            continue
        code = main_code(a.get("VsaCode", ""))
        if not code:
            continue
        try:
            img = Image.open(frame_path).convert("RGB")
        except Exception as exc:
            logger.warning("Bild %s konnte nicht geladen werden: %s", frame_path, exc)
            continue
        inputs = processor(images=img, return_tensors="pt").to(device)
        outputs = model(**inputs)
        cls = outputs.last_hidden_state[:, 0, :].cpu().float().squeeze(0)
        samples.append((code, cls))

    logger.info("Features extrahiert: %d Samples ueber %d Codes",
                len(samples), len({c for c, _ in samples}))
    return samples


def build_dataset_for_code(
    target_code: str,
    all_samples: list[tuple[str, torch.Tensor, str]],
    rng: random.Random,
) -> tuple[torch.Tensor, torch.Tensor]:
    """
    Baut ein Trainings-Dataset fuer EINEN Code:
      - Positive (mild/severe) aus Samples mit target_code
      - Negative (not_present) aus Samples anderer Codes (gleich viele wie positive)
    """
    positives = [(feat, cls_name) for code, feat, cls_name in all_samples if code == target_code]
    others = [feat for code, feat, _ in all_samples if code != target_code]
    if not positives:
        return torch.empty(0, DINOV2_FEATURE_DIM), torch.empty(0, dtype=torch.long)

    n_neg = min(len(positives), len(others))
    negatives = rng.sample(others, n_neg) if n_neg > 0 else []

    features = torch.stack(
        [feat for feat, _ in positives] + negatives
    )
    labels = torch.tensor(
        [HEAD_CLASSES.index(cls_name) for _, cls_name in positives]
        + [HEAD_CLASSES.index("not_present")] * len(negatives),
        dtype=torch.long,
    )
    return features, labels


def _class_weights(labels: torch.Tensor) -> torch.Tensor:
    """
    V4.2 Nachbesserung C: Klassen-Gewichte aus inverser Klassen-Frequenz.
    Ohne das lernt ein Head bei ungleichen Klassen schnell 'not_present' zu aggressiv.
    """
    counts = torch.bincount(labels, minlength=len(HEAD_CLASSES)).float()
    # Kleines Epsilon damit leere Klassen nicht zu Inf werden.
    counts = torch.clamp(counts, min=1.0)
    weights = counts.sum() / (counts * len(HEAD_CLASSES))
    return weights


def train_head(features: torch.Tensor, labels: torch.Tensor, epochs: int, lr: float) -> tuple[nn.Linear, float, list[int]]:
    """Trainiert einen Head und liefert (model, train_accuracy, support_pro_klasse)."""
    head = nn.Linear(DINOV2_FEATURE_DIM, len(HEAD_CLASSES))
    opt = torch.optim.AdamW(head.parameters(), lr=lr, weight_decay=1e-3)
    weights = _class_weights(labels)
    loss_fn = nn.CrossEntropyLoss(weight=weights)

    head.train()
    for epoch in range(epochs):
        opt.zero_grad()
        logits = head(features)
        loss = loss_fn(logits, labels)
        loss.backward()
        opt.step()
    head.eval()

    with torch.no_grad():
        preds = head(features).argmax(dim=-1)
        acc = float((preds == labels).float().mean().item())
        support = torch.bincount(labels, minlength=len(HEAD_CLASSES)).tolist()
    return head, acc, support


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--annotations", required=True, help="Pfad zur teacher_annotations.json")
    ap.add_argument("--heads-dir", required=True, help="Zielverzeichnis fuer {code}.pt")
    ap.add_argument("--min-samples", type=int, default=5,
                    help="Minimale Anzahl positiver Samples pro Code (Default 5)")
    ap.add_argument("--epochs", type=int, default=50)
    ap.add_argument("--lr", type=float, default=1e-3)
    ap.add_argument("--device", default="cuda" if torch.cuda.is_available() else "cpu")
    ap.add_argument("--seed", type=int, default=42)
    args = ap.parse_args()

    rng = random.Random(args.seed)
    torch.manual_seed(args.seed)

    annotations_path = Path(args.annotations)
    heads_dir = Path(args.heads_dir)
    heads_dir.mkdir(parents=True, exist_ok=True)

    annotations = load_annotations(annotations_path)
    logger.info("Annotationen gelesen: %d aus %s", len(annotations), annotations_path)

    # Gruppieren + Klassen zuweisen (mild/severe via Severity).
    enriched = []
    for a in annotations:
        code = main_code(a.get("VsaCode", ""))
        if not code:
            continue
        cls_name = severity_to_class(a.get("Severity"))
        enriched.append((code, a, cls_name))

    by_code: dict[str, list] = defaultdict(list)
    for code, a, cls_name in enriched:
        by_code[code].append((a, cls_name))

    # Features extrahieren (nur fuer Annotationen mit gueltigem Frame).
    samples_all = extract_features_for([a for _, a, _ in enriched], args.device)
    # Samples mit Klassenlabel anreichern:
    # Reihenfolge von extract_features_for ist dieselbe wie im Input, aber einige Bilder
    # koennen weggefallen sein. Wir bauen daher neu via Lookup.
    feat_index = 0
    indexed_samples: list[tuple[str, torch.Tensor, str]] = []
    for code, a, cls_name in enriched:
        frame_path = a.get("FullFramePath") or a.get("CroppedRegionPath")
        if not frame_path or not Path(frame_path).exists():
            continue
        # feat_index zaehlt nur Samples die tatsaechlich extrahiert wurden.
        if feat_index >= len(samples_all):
            break
        ex_code, feat = samples_all[feat_index]
        if ex_code == code:
            indexed_samples.append((code, feat, cls_name))
        feat_index += 1

    # Pro Code trainieren.
    manifest: dict[str, dict] = {}
    for code, entries in by_code.items():
        if len(entries) < args.min_samples:
            logger.info("Code %s: nur %d Samples (< %d) — skip",
                        code, len(entries), args.min_samples)
            continue

        features, labels = build_dataset_for_code(code, indexed_samples, rng)
        if features.shape[0] < 2 * args.min_samples:
            logger.info("Code %s: zu wenig Features nach Filter (%d) — skip",
                        code, features.shape[0])
            continue

        head, acc, support = train_head(features, labels, args.epochs, args.lr)
        out_path = heads_dir / f"{code}.pt"
        torch.save(head.state_dict(), out_path)
        manifest[code] = {
            "samples": int(features.shape[0]),
            "train_accuracy": round(acc, 4),
            "support_per_class": dict(zip(HEAD_CLASSES, support)),
            "class_weighted": True,
            "path": str(out_path),
        }
        logger.info("Head %s trainiert: %d Samples (support=%s), Train-Acc %.3f -> %s",
                    code, features.shape[0], support, acc, out_path)

    # Manifest schreiben.
    (heads_dir / "manifest.json").write_text(
        json.dumps({"heads": manifest, "classes": HEAD_CLASSES,
                    "feature_dim": DINOV2_FEATURE_DIM,
                    "encoder": DINOV2_MODEL_ID}, indent=2),
        encoding="utf-8",
    )
    logger.info("Fertig: %d Heads trainiert, Manifest in %s",
                len(manifest), heads_dir / "manifest.json")


if __name__ == "__main__":
    main()
