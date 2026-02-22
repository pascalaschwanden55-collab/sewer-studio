"""
OCR for extracting chainage (meter) readings from video frame overlays.
Uses Tesseract OCR with ROI (Region of Interest) cropping.
"""

import json
import re
import os
from pathlib import Path
from typing import Optional, List, Dict, Any
from dataclasses import dataclass, asdict

try:
    from PIL import Image
    import pytesseract
except ImportError:
    Image = None
    pytesseract = None


@dataclass
class OcrResult:
    frame_path: str
    frame_number: int
    time_s: float
    chainage_m: Optional[float]
    ocr_confidence: float
    raw_ocr_text: str
    is_valid: bool
    quality_issue: Optional[str] = None


def run_ocr_chainage(
    frames_dir: str,
    output_path: str,
    roi_config: Optional[Dict[str, Any]] = None,
    fps: float = 3.0,
    pattern: str = r"(\d+)[.,](\d+)",
    smooth_window: int = 5,
    monotonic_check: bool = True
) -> Dict[str, Any]:
    """
    Run OCR on extracted frames to get chainage readings.
    
    Args:
        frames_dir: Directory containing extracted frames
        output_path: Output JSONL file path
        roi_config: ROI configuration {x, y, width, height}
        fps: Frames per second (for time calculation)
        pattern: Regex pattern to match meter readings
        smooth_window: Window size for median smoothing
        monotonic_check: Check for monotonic increase in chainage
    
    Returns:
        dict with OCR results summary
    """
    if Image is None or pytesseract is None:
        return {"success": False, "error": "PIL and pytesseract are required. Install with: pip install pillow pytesseract"}
    
    frames_dir = Path(frames_dir)
    output_path = Path(output_path)
    
    if not frames_dir.exists():
        return {"success": False, "error": f"Frames directory not found: {frames_dir}"}
    
    # Default ROI (bottom-left corner where meter typically appears)
    if roi_config is None:
        roi_config = {"x": 10, "y": 680, "width": 200, "height": 40}
    
    # Find all frame files
    frames = sorted(frames_dir.glob("frame_*.jpg")) + sorted(frames_dir.glob("frame_*.png"))
    
    if not frames:
        return {"success": False, "error": "No frames found in directory"}
    
    results: List[OcrResult] = []
    
    for i, frame_path in enumerate(frames):
        try:
            img = Image.open(frame_path)
            
            # Crop to ROI
            roi = roi_config
            crop_box = (roi["x"], roi["y"], roi["x"] + roi["width"], roi["y"] + roi["height"])
            
            # Check if crop is within image bounds
            if crop_box[2] > img.width or crop_box[3] > img.height:
                # Adjust ROI if needed
                crop_box = (
                    min(roi["x"], img.width - 10),
                    min(roi["y"], img.height - 10),
                    min(roi["x"] + roi["width"], img.width),
                    min(roi["y"] + roi["height"], img.height)
                )
            
            cropped = img.crop(crop_box)
            
            # Run OCR
            ocr_text = pytesseract.image_to_string(
                cropped,
                config="--psm 7 -c tessedit_char_whitelist=0123456789.,"
            ).strip()
            
            # Extract meter value
            chainage_m = None
            confidence = 0.0
            
            match = re.search(pattern, ocr_text)
            if match:
                try:
                    # Handle both . and , as decimal separator
                    integer_part = match.group(1)
                    decimal_part = match.group(2)
                    chainage_m = float(f"{integer_part}.{decimal_part}")
                    confidence = 0.8  # Base confidence for valid match
                except ValueError:
                    pass
            
            time_s = i / fps
            
            result = OcrResult(
                frame_path=str(frame_path),
                frame_number=i,
                time_s=time_s,
                chainage_m=chainage_m,
                ocr_confidence=confidence,
                raw_ocr_text=ocr_text,
                is_valid=chainage_m is not None
            )
            results.append(result)
            
        except Exception as e:
            results.append(OcrResult(
                frame_path=str(frame_path),
                frame_number=i,
                time_s=i / fps,
                chainage_m=None,
                ocr_confidence=0.0,
                raw_ocr_text="",
                is_valid=False,
                quality_issue=str(e)
            ))
    
    # Apply smoothing and quality checks
    results = _smooth_chainage(results, smooth_window)
    
    if monotonic_check:
        results = _check_monotonicity(results)
    
    # Write results
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        for r in results:
            f.write(json.dumps(asdict(r), ensure_ascii=False) + "\n")
    
    # Calculate quality metrics
    valid_count = sum(1 for r in results if r.is_valid)
    total_count = len(results)
    ocr_quality = valid_count / total_count if total_count > 0 else 0.0
    
    return {
        "success": True,
        "frames_dir": str(frames_dir),
        "output_path": str(output_path),
        "total_frames": total_count,
        "valid_frames": valid_count,
        "ocr_quality": ocr_quality,
        "roi_config": roi_config
    }


def _smooth_chainage(results: List[OcrResult], window: int) -> List[OcrResult]:
    """Apply median smoothing to chainage values to reduce OCR noise."""
    if window < 3:
        return results
    
    chainages = [r.chainage_m for r in results]
    smoothed = []
    
    half_window = window // 2
    
    for i, r in enumerate(results):
        if r.chainage_m is None:
            smoothed.append(r)
            continue
        
        # Get window of values
        start = max(0, i - half_window)
        end = min(len(chainages), i + half_window + 1)
        window_values = [c for c in chainages[start:end] if c is not None]
        
        if window_values:
            # Use median
            window_values.sort()
            median = window_values[len(window_values) // 2]
            
            # Update result with smoothed value
            new_result = OcrResult(
                frame_path=r.frame_path,
                frame_number=r.frame_number,
                time_s=r.time_s,
                chainage_m=median,
                ocr_confidence=r.ocr_confidence,
                raw_ocr_text=r.raw_ocr_text,
                is_valid=r.is_valid,
                quality_issue=r.quality_issue
            )
            smoothed.append(new_result)
        else:
            smoothed.append(r)
    
    return smoothed


def _check_monotonicity(results: List[OcrResult]) -> List[OcrResult]:
    """Check that chainage increases monotonically (with tolerance)."""
    tolerance = 0.5  # meters
    last_valid = None
    
    for r in results:
        if r.chainage_m is None:
            continue
        
        if last_valid is not None:
            # Allow small decreases due to OCR errors
            if r.chainage_m < last_valid - tolerance:
                r.is_valid = False
                r.quality_issue = f"Non-monotonic: {r.chainage_m}m after {last_valid}m"
            else:
                last_valid = max(last_valid, r.chainage_m)
        else:
            last_valid = r.chainage_m
    
    return results


def main():
    """CLI entry point."""
    import argparse
    
    parser = argparse.ArgumentParser(description="Run OCR on video frames for chainage extraction")
    parser.add_argument("frames_dir", help="Directory containing extracted frames")
    parser.add_argument("-o", "--output", required=True, help="Output JSONL file")
    parser.add_argument("--roi-x", type=int, default=10, help="ROI X position")
    parser.add_argument("--roi-y", type=int, default=680, help="ROI Y position")
    parser.add_argument("--roi-width", type=int, default=200, help="ROI width")
    parser.add_argument("--roi-height", type=int, default=40, help="ROI height")
    parser.add_argument("--fps", type=float, default=3.0, help="Frame rate")
    parser.add_argument("--no-smooth", action="store_true", help="Disable smoothing")
    parser.add_argument("--no-monotonic", action="store_true", help="Disable monotonicity check")
    
    args = parser.parse_args()
    
    roi_config = {
        "x": args.roi_x,
        "y": args.roi_y,
        "width": args.roi_width,
        "height": args.roi_height
    }
    
    result = run_ocr_chainage(
        frames_dir=args.frames_dir,
        output_path=args.output,
        roi_config=roi_config,
        fps=args.fps,
        smooth_window=0 if args.no_smooth else 5,
        monotonic_check=not args.no_monotonic
    )
    
    print(json.dumps(result, indent=2))
    return 0 if result.get("success") else 1


if __name__ == "__main__":
    exit(main())
