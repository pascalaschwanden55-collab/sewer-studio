"""
Dataset builder for training video analysis models.
Combines XTF events with video frames to create training samples.
"""

import json
import random
import hashlib
from pathlib import Path
from typing import Dict, List, Any, Optional, Set
from dataclasses import dataclass, asdict
from datetime import datetime


@dataclass
class TrainingSample:
    sample_id: str
    video_id: str
    holding_id: str
    label: str
    severity: Optional[int]
    start_m: float
    end_m: float
    start_time_s: Optional[float]
    end_time_s: Optional[float]
    keyframes: List[str]
    source: str
    xtf_id: Optional[str]
    split: str  # "train", "val", "test"


def build_dataset(
    events_dir: str,
    keyframes_dir: str,
    output_dir: str,
    train_split: float = 0.7,
    val_split: float = 0.15,
    test_split: float = 0.15,
    negative_ratio: float = 0.3,
    min_samples_per_class: int = 10,
    seed: int = 42
) -> Dict[str, Any]:
    """
    Build training dataset from events and keyframes.
    
    Args:
        events_dir: Directory containing event JSONL files
        keyframes_dir: Directory containing keyframes
        output_dir: Output directory for dataset
        train_split: Proportion for training
        val_split: Proportion for validation
        test_split: Proportion for testing
        negative_ratio: Ratio of NONE samples to add
        min_samples_per_class: Minimum samples per class warning threshold
        seed: Random seed for reproducibility
    
    Returns:
        dict with dataset building results
    """
    random.seed(seed)

    split_sum = train_split + val_split + test_split
    if train_split < 0 or val_split < 0 or test_split < 0:
        return {"success": False, "error": "Split ratios must be >= 0"}
    if abs(split_sum - 1.0) > 1e-6:
        return {"success": False, "error": f"Split ratios must sum to 1.0 (got {split_sum:.4f})"}
    
    events_dir = Path(events_dir)
    keyframes_dir = Path(keyframes_dir)
    output_dir = Path(output_dir)
    
    if not events_dir.exists():
        return {"success": False, "error": f"Events directory not found: {events_dir}"}
    
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Collect all events
    all_events = []
    holdings: Set[str] = set()
    
    for events_file in events_dir.glob("*.jsonl"):
        with open(events_file, "r", encoding="utf-8") as f:
            for line in f:
                if line.strip():
                    event = json.loads(line)
                    all_events.append(event)
                    holdings.add(event.get("holding_id", "unknown"))
    
    if not all_events:
        return {"success": False, "error": "No events found"}
    
    # Group by holding for stratified splitting
    holdings_list = list(holdings)
    random.shuffle(holdings_list)
    
    n_holdings = len(holdings_list)
    n_train = int(n_holdings * train_split)
    n_val = int(n_holdings * val_split)

    # Keep at least one holding for train if data exists.
    if n_holdings > 0 and n_train == 0:
        n_train = 1
    if n_train + n_val > n_holdings:
        n_val = max(0, n_holdings - n_train)
    
    train_holdings = set(holdings_list[:n_train])
    val_holdings = set(holdings_list[n_train:n_train + n_val])
    test_holdings = set(holdings_list[n_train + n_val:])
    
    # Create samples
    samples: List[TrainingSample] = []
    missing_keyframes_count = 0
    
    for event in all_events:
        event_id = str(event.get("xtf_id") or event.get("event_id") or "").strip()
        holding_id = event.get("holding_id", "unknown")
        label = _normalize_label(event.get("type_code") or event.get("label"))
        
        # Determine split
        if holding_id in train_holdings:
            split = "train"
        elif holding_id in val_holdings:
            split = "val"
        else:
            split = "test"
        
        # Find keyframes
        keyframe_dir = keyframes_dir / event_id
        keyframes = []
        if keyframe_dir.exists():
            keyframes = [str(p) for p in sorted(keyframe_dir.glob("keyframe_*"))]
        else:
            missing_keyframes_count += 1
        
        # Generate sample ID
        if not event_id:
            event_id = hashlib.md5(f"{holding_id}_{label}_{event.get('start_m')}_{event.get('end_m')}".encode()).hexdigest()[:12]

        sample_id = hashlib.md5(f"{event_id}_{holding_id}_{label}".encode()).hexdigest()[:12]
        
        sample = TrainingSample(
            sample_id=sample_id,
            video_id=event.get("video_id", ""),
            holding_id=holding_id,
            label=label,
            severity=event.get("severity"),
            start_m=_first_not_none(event.get("start_m"), event.get("station_m"), 0.0),
            end_m=_first_not_none(event.get("end_m"), event.get("start_m"), event.get("station_m"), 0.0),
            start_time_s=event.get("start_time_s"),
            end_time_s=event.get("end_time_s"),
            keyframes=keyframes,
            source=event.get("source", "xtf_auto"),
            xtf_id=event.get("xtf_id"),
            split=split
        )
        
        samples.append(sample)

    # Balance negative class (NONE) if requested.
    downsampled_none_count = 0
    if negative_ratio >= 0:
        balanced: List[TrainingSample] = []
        for split_name in ("train", "val", "test"):
            split_samples = [s for s in samples if s.split == split_name]
            none_samples = [s for s in split_samples if s.label == "NONE"]
            positive_samples = [s for s in split_samples if s.label != "NONE"]

            if negative_ratio == 0:
                downsampled_none_count += len(none_samples)
                balanced.extend(positive_samples)
                continue

            if not positive_samples or not none_samples:
                balanced.extend(split_samples)
                continue

            target_none = int(len(positive_samples) * negative_ratio)
            if target_none < len(none_samples):
                random.shuffle(none_samples)
                kept_none = none_samples[:target_none]
                downsampled_none_count += len(none_samples) - len(kept_none)
                balanced.extend(positive_samples + kept_none)
            else:
                balanced.extend(split_samples)

        samples = balanced

    label_counts: Dict[str, int] = {}
    for sample in samples:
        label_counts[sample.label] = label_counts.get(sample.label, 0) + 1
    
    # Write dataset files
    train_samples = [s for s in samples if s.split == "train"]
    val_samples = [s for s in samples if s.split == "val"]
    test_samples = [s for s in samples if s.split == "test"]
    
    def write_jsonl(path: Path, data: List[TrainingSample]):
        with open(path, "w", encoding="utf-8") as f:
            for s in data:
                f.write(json.dumps(asdict(s), ensure_ascii=False) + "\n")
    
    write_jsonl(output_dir / "train.jsonl", train_samples)
    write_jsonl(output_dir / "val.jsonl", val_samples)
    write_jsonl(output_dir / "test.jsonl", test_samples)
    
    # Check class balance
    warnings = []
    for label, count in label_counts.items():
        if count < min_samples_per_class:
            warnings.append(f"Class '{label}' has only {count} samples (min: {min_samples_per_class})")
    if missing_keyframes_count > 0:
        warnings.append(f"{missing_keyframes_count} events without keyframe directory")
    
    # Write metadata
    metadata = {
        "created_at": datetime.utcnow().isoformat(),
        "seed": seed,
        "splits": {
            "train": len(train_samples),
            "val": len(val_samples),
            "test": len(test_samples)
        },
        "holdings": {
            "train": len(train_holdings),
            "val": len(val_holdings),
            "test": len(test_holdings)
        },
        "label_distribution": label_counts,
        "warnings": warnings,
        "missing_keyframes_count": missing_keyframes_count,
        "downsampled_none_count": downsampled_none_count,
    }
    
    with open(output_dir / "metadata.json", "w", encoding="utf-8") as f:
        json.dump(metadata, f, indent=2, ensure_ascii=False)
    
    return {
        "success": True,
        "output_dir": str(output_dir),
        "total_samples": len(samples),
        "train_samples": len(train_samples),
        "val_samples": len(val_samples),
        "test_samples": len(test_samples),
        "label_distribution": label_counts,
        "warnings": warnings,
        "missing_keyframes_count": missing_keyframes_count,
        "downsampled_none_count": downsampled_none_count,
    }


def _first_not_none(*values: Any) -> Any:
    for value in values:
        if value is not None:
            return value
    return None


def _normalize_label(raw_label: Any) -> str:
    if raw_label is None:
        return "UNKNOWN"
    label = str(raw_label).strip().upper()
    return label if label else "UNKNOWN"


def main():
    """CLI entry point."""
    import argparse
    
    parser = argparse.ArgumentParser(description="Build training dataset from events and keyframes")
    parser.add_argument("events_dir", help="Directory containing event JSONL files")
    parser.add_argument("keyframes_dir", help="Directory containing keyframes")
    parser.add_argument("-o", "--output", required=True, help="Output directory")
    parser.add_argument("--train-split", type=float, default=0.7, help="Training split ratio")
    parser.add_argument("--val-split", type=float, default=0.15, help="Validation split ratio")
    parser.add_argument("--test-split", type=float, default=0.15, help="Test split ratio")
    parser.add_argument("--negative-ratio", type=float, default=0.3, help="Target NONE-to-positive ratio per split")
    parser.add_argument("--min-samples-per-class", type=int, default=10, help="Warning threshold for class count")
    parser.add_argument("--seed", type=int, default=42, help="Random seed")
    
    args = parser.parse_args()
    
    result = build_dataset(
        events_dir=args.events_dir,
        keyframes_dir=args.keyframes_dir,
        output_dir=args.output,
        train_split=args.train_split,
        val_split=args.val_split,
        test_split=args.test_split,
        negative_ratio=args.negative_ratio,
        min_samples_per_class=args.min_samples_per_class,
        seed=args.seed
    )
    
    print(json.dumps(result, indent=2))
    return 0 if result.get("success") else 1


if __name__ == "__main__":
    exit(main())
