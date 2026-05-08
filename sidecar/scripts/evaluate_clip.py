"""NV-CLIP Evaluation Script — Phase 6.

Vergleicht nomic-embed-text (Text-only) vs. NV-CLIP (Bild-Embeddings)
fuer die KnowledgeBase-Retrieval.

Nutzung:
    python -m sidecar.scripts.evaluate_clip \
        --kb-path path/to/KnowledgeBase.db \
        --samples 100 \
        --queries 20

Ergebnis wird in CLIP_EVALUATION_REPORT.md gespeichert.
"""

from __future__ import annotations

import argparse
import logging
import sys
import time
from pathlib import Path

import numpy as np

logging.basicConfig(
    level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s"
)
logger = logging.getLogger(__name__)


def _load_clip_model(device: str = "cuda:0"):
    """Laedt NV-CLIP / OpenCLIP Modell."""
    try:
        import open_clip

        model, _, preprocess = open_clip.create_model_and_transforms(
            "ViT-L-14", pretrained="datacomp_xl_s13b_b90k"
        )
        model = model.to(device).eval()
        tokenizer = open_clip.get_tokenizer("ViT-L-14")
        return model, preprocess, tokenizer
    except ImportError:
        logger.error(
            "open_clip nicht installiert. Install: pip install open_clip_torch"
        )
        sys.exit(1)


def _embed_image_clip(
    model, preprocess, image_path: str, device: str = "cuda:0"
) -> np.ndarray:
    """Embeddet ein Bild mit CLIP."""
    import torch
    from PIL import Image

    img = preprocess(Image.open(image_path).convert("RGB")).unsqueeze(0).to(device)
    with torch.inference_mode():
        features = model.encode_image(img)
        features = features / features.norm(dim=-1, keepdim=True)
    return features.cpu().numpy().flatten()


def _embed_text_clip(model, tokenizer, text: str, device: str = "cuda:0") -> np.ndarray:
    """Embeddet Text mit CLIP."""
    import torch

    tokens = tokenizer([text]).to(device)
    with torch.inference_mode():
        features = model.encode_text(tokens)
        features = features / features.norm(dim=-1, keepdim=True)
    return features.cpu().numpy().flatten()


def _cosine_similarity(a: np.ndarray, b: np.ndarray) -> float:
    """Cosine Similarity zwischen zwei Vektoren."""
    dot = np.dot(a, b)
    norm = np.linalg.norm(a) * np.linalg.norm(b)
    if norm == 0:
        return 0.0
    return float(dot / norm)


def _precision_at_k(retrieved: list[str], relevant: set[str], k: int = 3) -> float:
    """Precision@k: Anteil relevanter Ergebnisse in den Top-k."""
    top_k = retrieved[:k]
    hits = sum(1 for r in top_k if r in relevant)
    return hits / k


def main():
    parser = argparse.ArgumentParser(
        description="NV-CLIP vs. nomic-embed-text Evaluation"
    )
    parser.add_argument(
        "--kb-path",
        type=str,
        default="KnowledgeBase.db",
        help="Pfad zur KnowledgeBase SQLite",
    )
    parser.add_argument(
        "--samples", type=int, default=100, help="Anzahl KB-Samples fuer Embedding"
    )
    parser.add_argument(
        "--queries", type=int, default=20, help="Anzahl Goldstandard-Queries"
    )
    parser.add_argument("--device", type=str, default="cuda:0")
    parser.add_argument("--output", type=str, default="CLIP_EVALUATION_REPORT.md")
    args = parser.parse_args()

    logger.info("=== NV-CLIP Evaluation ===")
    logger.info(
        "KB: %s, Samples: %d, Queries: %d", args.kb_path, args.samples, args.queries
    )

    # Goldstandard-Queries (VSA-Codes mit erwarteten Labels)
    gold_queries = [
        {"query": "Riss laengs in Betonrohr", "expected_codes": {"BAB_A", "BAB"}},
        {"query": "Wurzeleinwuchs stark", "expected_codes": {"BBB", "BBB_C"}},
        {"query": "Querversatz am Muffenstoss", "expected_codes": {"BAH_B", "BAH"}},
        {"query": "Korrosion Sohle", "expected_codes": {"BAA", "BAA_A"}},
        {"query": "Seitlicher Anschluss einragend", "expected_codes": {"BCA", "BAI"}},
        {"query": "Ablagerung Sand", "expected_codes": {"BBC_A", "BBC"}},
        {"query": "Deformation vertikal", "expected_codes": {"BAF_A", "BAF"}},
        {"query": "Infiltration tropfend", "expected_codes": {"BBA", "BDB"}},
        {"query": "Bruch total Rohr eingestuerzt", "expected_codes": {"BAC_B", "BAC"}},
        {"query": "Inkrustation Kalk", "expected_codes": {"BBA", "BBA_A"}},
        {"query": "Rohr deformiert horizontal", "expected_codes": {"BAF_B", "BAF"}},
        {"query": "Muffenversatz offen", "expected_codes": {"BAH", "BAH_A"}},
        {"query": "Hindernis im Rohr", "expected_codes": {"BBE", "BBD"}},
        {"query": "Rohranfang Schacht sichtbar", "expected_codes": {"BCD"}},
        {"query": "Rohrende erreicht", "expected_codes": {"BCE"}},
        {"query": "Bogen Richtungsaenderung", "expected_codes": {"BCC"}},
        {"query": "Riss quer diagonal", "expected_codes": {"BAB_B", "BAB_C"}},
        {"query": "Scherbe im Rohr", "expected_codes": {"BBE", "BAC"}},
        {"query": "Fettablagerung", "expected_codes": {"BBC", "BBE"}},
        {"query": "Wassereinlauf seitlich", "expected_codes": {"BCA", "BDB"}},
    ]

    report_lines = [
        "# NV-CLIP Evaluation Report",
        "",
        f"**Datum:** {time.strftime('%Y-%m-%d %H:%M')}",
        f"**KB:** {args.kb_path}",
        f"**Samples:** {args.samples}",
        f"**Queries:** {len(gold_queries)}",
        "",
        "## Ergebnis",
        "",
        "| Methode | Precision@3 (Mittel) | Latenz (ms/Query) |",
        "|---------|---------------------|-------------------|",
    ]

    # TODO: Wenn KB vorhanden und CLIP installiert → tatsaechliche Evaluation
    # Placeholder fuer Ergebnis
    report_lines.append("| nomic-embed-text | *ausstehend* | *ausstehend* |")
    report_lines.append("| NV-CLIP (Bild) | *ausstehend* | *ausstehend* |")
    report_lines.append("")
    report_lines.append("## Empfehlung")
    report_lines.append("")
    report_lines.append("*Evaluation muss mit realen KB-Daten durchgefuehrt werden.*")
    report_lines.append(
        "*CLIP-Modell auf RTX 5090 installieren: `pip install open_clip_torch`*"
    )
    report_lines.append("")
    report_lines.append("## Goldstandard-Queries")
    report_lines.append("")
    for i, q in enumerate(gold_queries, 1):
        report_lines.append(
            f"{i}. **{q['query']}** → erwartet: {', '.join(q['expected_codes'])}"
        )

    output_path = Path(args.output)
    output_path.write_text("\n".join(report_lines), encoding="utf-8")
    logger.info("Report geschrieben: %s", output_path)


if __name__ == "__main__":
    main()
