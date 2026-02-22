"""
Main CLI entry point for video_ai tools.
Usage: python -m tools.video_ai <command> [options]
"""

import argparse
import sys
import json


def main():
    parser = argparse.ArgumentParser(
        prog="video_ai",
        description="AuswertungPro Video AI Tools - Frame extraction, OCR, and dataset generation"
    )
    
    subparsers = parser.add_subparsers(dest="command", help="Available commands")
    
    # Extract frames command
    extract_parser = subparsers.add_parser("extract", help="Extract frames from video")
    extract_parser.add_argument("video", help="Path to video file")
    extract_parser.add_argument("-o", "--output", required=True, help="Output directory")
    extract_parser.add_argument("--fps", type=float, default=3.0, help="Frames per second")
    extract_parser.add_argument("--no-scene-cuts", action="store_true", help="Disable scene cut detection")
    
    # OCR command
    ocr_parser = subparsers.add_parser("ocr", help="Run OCR on frames for chainage extraction")
    ocr_parser.add_argument("frames_dir", help="Directory containing extracted frames")
    ocr_parser.add_argument("-o", "--output", required=True, help="Output JSONL file")
    ocr_parser.add_argument("--roi-x", type=int, default=10, help="ROI X position")
    ocr_parser.add_argument("--roi-y", type=int, default=680, help="ROI Y position")
    ocr_parser.add_argument("--roi-width", type=int, default=200, help="ROI width")
    ocr_parser.add_argument("--roi-height", type=int, default=40, help="ROI height")
    
    # XTF parsing command
    xtf_parser = subparsers.add_parser("xtf", help="Parse XTF file to extract damage events")
    xtf_parser.add_argument("xtf_file", help="Path to XTF file")
    xtf_parser.add_argument("-o", "--output", help="Output JSONL file")
    xtf_parser.add_argument("--summary", action="store_true", help="Only show summary")
    
    # Keyframe generation command
    keyframes_parser = subparsers.add_parser("keyframes", help="Generate keyframes for events")
    keyframes_parser.add_argument("events", help="Path to events JSONL file")
    keyframes_parser.add_argument("ocr_meta", help="Path to OCR metadata JSONL file")
    keyframes_parser.add_argument("frames_dir", help="Directory containing extracted frames")
    keyframes_parser.add_argument("-o", "--output", required=True, help="Output directory")
    keyframes_parser.add_argument("--per-event", type=int, default=3, help="Keyframes per event")
    
    # Dataset building command
    dataset_parser = subparsers.add_parser("dataset", help="Build training dataset")
    dataset_parser.add_argument("events_dir", help="Directory containing event JSONL files")
    dataset_parser.add_argument("keyframes_dir", help="Directory containing keyframes")
    dataset_parser.add_argument("-o", "--output", required=True, help="Output directory")
    dataset_parser.add_argument("--train-split", type=float, default=0.7, help="Training split ratio")
    dataset_parser.add_argument("--val-split", type=float, default=0.15, help="Validation split ratio")
    dataset_parser.add_argument("--test-split", type=float, default=0.15, help="Test split ratio")
    dataset_parser.add_argument("--negative-ratio", type=float, default=0.3, help="Target NONE-to-positive ratio")
    dataset_parser.add_argument("--min-samples-per-class", type=int, default=10, help="Warning threshold")
    dataset_parser.add_argument("--seed", type=int, default=42, help="Random seed")

    # Training command (baseline)
    train_parser = subparsers.add_parser("train", help="Train a baseline model from dataset")
    train_parser.add_argument("dataset_dir", help="Directory containing train/val/test JSONL files")
    train_parser.add_argument("-o", "--output", required=True, help="Output model directory")
    train_parser.add_argument("--model-name", default="baseline_majority", help="Model name/version")
    
    # Pipeline command (full processing)
    pipeline_parser = subparsers.add_parser("pipeline", help="Run full processing pipeline")
    pipeline_parser.add_argument("video", help="Path to video file")
    pipeline_parser.add_argument("xtf", help="Path to XTF file")
    pipeline_parser.add_argument("-o", "--output", required=True, help="Output directory")
    pipeline_parser.add_argument("--holding", help="Holding ID filter")
    
    args = parser.parse_args()
    
    if args.command is None:
        parser.print_help()
        return 1
    
    if args.command == "extract":
        from .extract_frames import extract_frames
        result = extract_frames(
            video_path=args.video,
            output_dir=args.output,
            fps=args.fps,
            scene_cuts=not args.no_scene_cuts
        )
        
    elif args.command == "ocr":
        from .ocr_chainage import run_ocr_chainage
        roi_config = {
            "x": args.roi_x,
            "y": args.roi_y,
            "width": args.roi_width,
            "height": args.roi_height
        }
        result = run_ocr_chainage(
            frames_dir=args.frames_dir,
            output_path=args.output,
            roi_config=roi_config
        )
        
    elif args.command == "xtf":
        from .xtf_to_events import parse_xtf_to_events
        result = parse_xtf_to_events(
            xtf_path=args.xtf_file,
            output_path=args.output
        )
        if args.summary:
            result = {k: v for k, v in result.items() if k != "events"}
        
    elif args.command == "keyframes":
        from .make_keyframes import generate_keyframes
        result = generate_keyframes(
            events_path=args.events,
            ocr_meta_path=args.ocr_meta,
            frames_dir=args.frames_dir,
            output_dir=args.output,
            per_event=args.per_event
        )
        
    elif args.command == "dataset":
        from .dataset_builder import build_dataset
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

    elif args.command == "train":
        from .train_baseline import train_baseline_model
        result = train_baseline_model(
            dataset_dir=args.dataset_dir,
            output_dir=args.output,
            model_name=args.model_name
        )
        
    elif args.command == "pipeline":
        result = run_pipeline(args)
    
    else:
        print(f"Unknown command: {args.command}")
        return 1
    
    print(json.dumps(result, indent=2))
    return 0 if result.get("success") else 1


def run_pipeline(args) -> dict:
    """Run the full processing pipeline."""
    from pathlib import Path
    from .extract_frames import extract_frames
    from .ocr_chainage import run_ocr_chainage
    from .xtf_to_events import parse_xtf_to_events
    from .make_keyframes import generate_keyframes
    
    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)
    
    results = {"steps": []}
    
    # Step 1: Extract frames
    print("Step 1/4: Extracting frames...")
    frames_dir = output_dir / "frames"
    frame_result = extract_frames(args.video, str(frames_dir))
    results["steps"].append({"step": "extract_frames", "result": frame_result})
    
    if not frame_result.get("success"):
        return {"success": False, "error": "Frame extraction failed", **results}
    
    # Step 2: Run OCR
    print("Step 2/4: Running OCR...")
    ocr_path = output_dir / "ocr_meta.jsonl"
    ocr_result = run_ocr_chainage(str(frames_dir), str(ocr_path))
    results["steps"].append({"step": "ocr_chainage", "result": ocr_result})
    
    if not ocr_result.get("success"):
        return {"success": False, "error": "OCR failed", **results}
    
    # Step 3: Parse XTF
    print("Step 3/4: Parsing XTF...")
    events_path = output_dir / "events.jsonl"
    xtf_result = parse_xtf_to_events(args.xtf, str(events_path), args.holding)
    results["steps"].append({"step": "parse_xtf", "result": {k: v for k, v in xtf_result.items() if k != "events"}})
    
    if not xtf_result.get("success"):
        return {"success": False, "error": "XTF parsing failed", **results}
    
    # Step 4: Generate keyframes
    print("Step 4/4: Generating keyframes...")
    keyframes_dir = output_dir / "keyframes"
    keyframe_result = generate_keyframes(
        str(events_path),
        str(ocr_path),
        str(frames_dir),
        str(keyframes_dir)
    )
    results["steps"].append({"step": "generate_keyframes", "result": keyframe_result})
    
    if not keyframe_result.get("success"):
        return {"success": False, "error": "Keyframe generation failed", **results}
    
    return {
        "success": True,
        "output_dir": str(output_dir),
        "frame_count": frame_result.get("frame_count", 0),
        "ocr_quality": ocr_result.get("ocr_quality", 0),
        "event_count": xtf_result.get("event_count", 0),
        "keyframe_count": keyframe_result.get("keyframes_generated", 0),
        **results
    }


if __name__ == "__main__":
    sys.exit(main())
