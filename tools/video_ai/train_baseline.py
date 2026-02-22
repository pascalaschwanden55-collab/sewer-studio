"""
Lightweight baseline trainer for AuswertungPro training workflows.
The model predicts the majority class from the train split.
"""

import json
from collections import Counter
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List


def train_baseline_model(dataset_dir: str, output_dir: str, model_name: str = "baseline_majority") -> Dict[str, Any]:
    """
    Train a majority-class baseline model and write simple model artifacts.

    Args:
        dataset_dir: Directory with train.jsonl, val.jsonl, test.jsonl
        output_dir: Target directory for model artifacts
        model_name: Human-readable model name/version

    Returns:
        dict with training summary and evaluation metrics
    """
    dataset_path = Path(dataset_dir)
    model_dir = Path(output_dir)

    if not dataset_path.exists():
        return {"success": False, "error": f"Dataset directory not found: {dataset_path}"}

    train_samples = _read_jsonl(dataset_path / "train.jsonl")
    val_samples = _read_jsonl(dataset_path / "val.jsonl")
    test_samples = _read_jsonl(dataset_path / "test.jsonl")

    if not train_samples:
        return {"success": False, "error": "train.jsonl is missing or empty"}

    train_labels = [_normalize_label(sample.get("label")) for sample in train_samples]
    label_counts = Counter(train_labels)
    majority_label = label_counts.most_common(1)[0][0]

    train_metrics = _evaluate_split(train_samples, majority_label)
    val_metrics = _evaluate_split(val_samples, majority_label)
    test_metrics = _evaluate_split(test_samples, majority_label)

    model_dir.mkdir(parents=True, exist_ok=True)

    model_artifact = {
        "model_name": model_name,
        "model_type": "majority_class_baseline",
        "majority_label": majority_label,
        "created_at_utc": datetime.utcnow().isoformat(),
        "label_distribution_train": dict(label_counts),
    }

    metrics_artifact = {
        "model_name": model_name,
        "majority_label": majority_label,
        "splits": {
            "train": train_metrics,
            "val": val_metrics,
            "test": test_metrics,
        },
    }

    with open(model_dir / "model_baseline.json", "w", encoding="utf-8") as f:
        json.dump(model_artifact, f, indent=2, ensure_ascii=False)

    with open(model_dir / "metrics.json", "w", encoding="utf-8") as f:
        json.dump(metrics_artifact, f, indent=2, ensure_ascii=False)

    with open(model_dir / "label_map.json", "w", encoding="utf-8") as f:
        json.dump({"labels": sorted(label_counts.keys())}, f, indent=2, ensure_ascii=False)

    return {
        "success": True,
        "output_dir": str(model_dir),
        "model_name": model_name,
        "majority_label": majority_label,
        "train_samples": len(train_samples),
        "val_samples": len(val_samples),
        "test_samples": len(test_samples),
        "train_accuracy": train_metrics["accuracy"],
        "val_accuracy": val_metrics["accuracy"],
        "test_accuracy": test_metrics["accuracy"],
    }


def _read_jsonl(path: Path) -> List[Dict[str, Any]]:
    if not path.exists():
        return []

    rows: List[Dict[str, Any]] = []
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError:
                continue
    return rows


def _normalize_label(raw_label: Any) -> str:
    if raw_label is None:
        return "UNKNOWN"
    label = str(raw_label).strip().upper()
    return label if label else "UNKNOWN"


def _evaluate_split(samples: List[Dict[str, Any]], predicted_label: str) -> Dict[str, Any]:
    if not samples:
        return {
            "sample_count": 0,
            "accuracy": 0.0,
            "macro_f1": 0.0,
            "per_class": {},
        }

    truth = [_normalize_label(s.get("label")) for s in samples]
    classes = sorted(set(truth) | {predicted_label})
    total = len(truth)
    correct = sum(1 for label in truth if label == predicted_label)
    accuracy = correct / total if total else 0.0

    per_class: Dict[str, Dict[str, Any]] = {}
    f1_values: List[float] = []

    for class_name in classes:
        tp = sum(1 for label in truth if label == class_name and predicted_label == class_name)
        fp = sum(1 for label in truth if label != class_name and predicted_label == class_name)
        fn = sum(1 for label in truth if label == class_name and predicted_label != class_name)

        precision = tp / (tp + fp) if (tp + fp) > 0 else 0.0
        recall = tp / (tp + fn) if (tp + fn) > 0 else 0.0
        f1 = (2 * precision * recall / (precision + recall)) if (precision + recall) > 0 else 0.0
        f1_values.append(f1)

        per_class[class_name] = {
            "support": sum(1 for label in truth if label == class_name),
            "precision": precision,
            "recall": recall,
            "f1": f1,
        }

    macro_f1 = sum(f1_values) / len(f1_values) if f1_values else 0.0

    return {
        "sample_count": total,
        "accuracy": accuracy,
        "macro_f1": macro_f1,
        "per_class": per_class,
    }


def main() -> int:
    import argparse

    parser = argparse.ArgumentParser(description="Train a majority-class baseline model")
    parser.add_argument("dataset_dir", help="Directory containing train/val/test JSONL files")
    parser.add_argument("-o", "--output", required=True, help="Output model directory")
    parser.add_argument("--model-name", default="baseline_majority", help="Model name/version")
    args = parser.parse_args()

    result = train_baseline_model(
        dataset_dir=args.dataset_dir,
        output_dir=args.output,
        model_name=args.model_name,
    )
    print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0 if result.get("success") else 1


if __name__ == "__main__":
    raise SystemExit(main())
