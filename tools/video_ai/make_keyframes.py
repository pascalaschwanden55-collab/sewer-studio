"""
Keyframe generation for video events.
Extracts representative frames for each detected event.
"""

import json
import shutil
from pathlib import Path
from typing import Dict, List, Any, Optional
from dataclasses import dataclass


def generate_keyframes(
    events_path: str,
    ocr_meta_path: str,
    frames_dir: str,
    output_dir: str,
    per_event: int = 3,
    margin_m: float = 0.3
) -> Dict[str, Any]:
    """
    Generate keyframes for each event based on OCR chainage data.
    
    Args:
        events_path: Path to events JSONL file
        ocr_meta_path: Path to OCR metadata JSONL file
        frames_dir: Directory containing extracted frames
        output_dir: Directory for output keyframes
        per_event: Number of keyframes per event
        margin_m: Margin in meters for frame selection
    
    Returns:
        dict with generation results
    """
    events_path = Path(events_path)
    ocr_meta_path = Path(ocr_meta_path)
    frames_dir = Path(frames_dir)
    output_dir = Path(output_dir)
    
    if not events_path.exists():
        return {"success": False, "error": f"Events file not found: {events_path}"}
    if not ocr_meta_path.exists():
        return {"success": False, "error": f"OCR meta file not found: {ocr_meta_path}"}
    if not frames_dir.exists():
        return {"success": False, "error": f"Frames directory not found: {frames_dir}"}
    
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Load events
    events = []
    with open(events_path, "r", encoding="utf-8") as f:
        for line in f:
            if line.strip():
                events.append(json.loads(line))
    
    # Load OCR metadata
    ocr_frames = []
    with open(ocr_meta_path, "r", encoding="utf-8") as f:
        for line in f:
            if line.strip():
                ocr_frames.append(json.loads(line))
    
    # Filter to valid frames with chainage
    valid_frames = [f for f in ocr_frames if f.get("is_valid") and f.get("chainage_m") is not None]
    
    if not valid_frames:
        return {"success": False, "error": "No valid frames with chainage data"}
    
    # Build chainage -> frame mapping
    chainage_to_frames = {}
    for frame in valid_frames:
        chainage = frame["chainage_m"]
        if chainage not in chainage_to_frames:
            chainage_to_frames[chainage] = []
        chainage_to_frames[chainage].append(frame)
    
    results = []
    
    for event in events:
        event_id = event.get("xtf_id") or event.get("event_id") or f"event_{len(results)}"
        
        # Get event range
        start_m = event.get("start_m") or event.get("station_m", 0)
        end_m = event.get("end_m") or start_m
        
        if start_m is None:
            continue
        
        # Expand range with margin
        search_start = start_m - margin_m
        search_end = end_m + margin_m
        
        # Find frames in range
        matching_frames = []
        for frame in valid_frames:
            chainage = frame["chainage_m"]
            if search_start <= chainage <= search_end:
                matching_frames.append(frame)
        
        if not matching_frames:
            # Try to find closest frame
            closest = min(valid_frames, key=lambda f: abs(f["chainage_m"] - start_m))
            matching_frames = [closest]
        
        # Select representative frames
        if len(matching_frames) <= per_event:
            selected = matching_frames
        else:
            # Evenly distribute
            step = len(matching_frames) / per_event
            indices = [int(i * step) for i in range(per_event)]
            selected = [matching_frames[i] for i in indices]
        
        # Copy keyframes to output
        event_dir = output_dir / event_id
        event_dir.mkdir(exist_ok=True)
        
        keyframe_paths = []
        for i, frame in enumerate(selected):
            src_path = Path(frame["frame_path"])
            if src_path.exists():
                dst_name = f"keyframe_{i:02d}_{frame['chainage_m']:.2f}m{src_path.suffix}"
                dst_path = event_dir / dst_name
                shutil.copy2(src_path, dst_path)
                keyframe_paths.append(str(dst_path))
        
        results.append({
            "event_id": event_id,
            "label": event.get("type_code") or event.get("label", "UNKNOWN"),
            "start_m": start_m,
            "end_m": end_m,
            "keyframe_count": len(keyframe_paths),
            "keyframe_paths": keyframe_paths
        })
    
    # Write summary
    summary_path = output_dir / "keyframes_summary.json"
    with open(summary_path, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    
    return {
        "success": True,
        "events_processed": len(events),
        "keyframes_generated": sum(r["keyframe_count"] for r in results),
        "output_dir": str(output_dir),
        "summary_path": str(summary_path)
    }


def main():
    """CLI entry point."""
    import argparse
    
    parser = argparse.ArgumentParser(description="Generate keyframes for video events")
    parser.add_argument("events", help="Path to events JSONL file")
    parser.add_argument("ocr_meta", help="Path to OCR metadata JSONL file")
    parser.add_argument("frames_dir", help="Directory containing extracted frames")
    parser.add_argument("-o", "--output", required=True, help="Output directory")
    parser.add_argument("--per-event", type=int, default=3, help="Keyframes per event")
    parser.add_argument("--margin", type=float, default=0.3, help="Margin in meters")
    
    args = parser.parse_args()
    
    result = generate_keyframes(
        events_path=args.events,
        ocr_meta_path=args.ocr_meta,
        frames_dir=args.frames_dir,
        output_dir=args.output,
        per_event=args.per_event,
        margin_m=args.margin
    )
    
    print(json.dumps(result, indent=2))
    return 0 if result.get("success") else 1


if __name__ == "__main__":
    exit(main())
