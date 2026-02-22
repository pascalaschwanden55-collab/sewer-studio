"""
Frame extraction from video using FFmpeg.
Extracts frames at specified FPS and optionally at scene cuts.
"""

import subprocess
import json
import os
from pathlib import Path
from typing import Optional


def extract_frames(
    video_path: str,
    output_dir: str,
    fps: float = 3.0,
    scene_cuts: bool = True,
    scene_threshold: float = 0.3,
    output_format: str = "jpg",
    quality: int = 85
) -> dict:
    """
    Extract frames from video file.
    
    Args:
        video_path: Path to input video file
        output_dir: Directory for output frames
        fps: Frames per second to extract
        scene_cuts: Also extract frames at scene cuts
        scene_threshold: Scene cut detection threshold (0.0-1.0)
        output_format: Output image format (jpg, png)
        quality: JPEG quality (1-100)
    
    Returns:
        dict with extraction results (frame_count, paths, duration_s, etc.)
    """
    video_path = Path(video_path)
    output_dir = Path(output_dir)
    
    if not video_path.exists():
        return {"success": False, "error": f"Video not found: {video_path}"}
    
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Get video info
    probe_cmd = [
        "ffprobe", "-v", "quiet",
        "-print_format", "json",
        "-show_format", "-show_streams",
        str(video_path)
    ]
    
    try:
        probe_result = subprocess.run(probe_cmd, capture_output=True, text=True, check=True)
        video_info = json.loads(probe_result.stdout)
        duration = float(video_info.get("format", {}).get("duration", 0))
    except (subprocess.CalledProcessError, json.JSONDecodeError, KeyError) as e:
        return {"success": False, "error": f"Failed to probe video: {e}"}
    
    # Extract frames at fixed FPS
    frame_pattern = str(output_dir / f"frame_%06d.{output_format}")
    
    extract_cmd = [
        "ffmpeg", "-i", str(video_path),
        "-vf", f"fps={fps}",
        "-q:v", str(max(1, min(31, 32 - int(quality * 31 / 100)))),
        "-y",  # Overwrite
        frame_pattern
    ]
    
    try:
        subprocess.run(extract_cmd, capture_output=True, check=True)
    except subprocess.CalledProcessError as e:
        return {"success": False, "error": f"Frame extraction failed: {e.stderr.decode()[:500]}"}
    
    # Count extracted frames
    frames = sorted(output_dir.glob(f"frame_*.{output_format}"))
    
    # Extract scene cut frames if requested
    scene_frames = []
    if scene_cuts:
        scene_dir = output_dir / "scene_cuts"
        scene_dir.mkdir(exist_ok=True)
        
        scene_cmd = [
            "ffmpeg", "-i", str(video_path),
            "-vf", f"select='gt(scene,{scene_threshold})',showinfo",
            "-vsync", "vfr",
            "-q:v", str(max(1, min(31, 32 - int(quality * 31 / 100)))),
            "-y",
            str(scene_dir / f"scene_%04d.{output_format}")
        ]
        
        try:
            subprocess.run(scene_cmd, capture_output=True, check=True)
            scene_frames = sorted(scene_dir.glob(f"scene_*.{output_format}"))
        except subprocess.CalledProcessError:
            pass  # Scene detection is optional
    
    return {
        "success": True,
        "video_path": str(video_path),
        "output_dir": str(output_dir),
        "frame_count": len(frames),
        "scene_cut_count": len(scene_frames),
        "fps": fps,
        "duration_s": duration,
        "frame_paths": [str(f) for f in frames],
        "scene_frame_paths": [str(f) for f in scene_frames]
    }


def main():
    """CLI entry point."""
    import argparse
    
    parser = argparse.ArgumentParser(description="Extract frames from video")
    parser.add_argument("video", help="Path to video file")
    parser.add_argument("-o", "--output", required=True, help="Output directory")
    parser.add_argument("--fps", type=float, default=3.0, help="Frames per second")
    parser.add_argument("--no-scene-cuts", action="store_true", help="Disable scene cut detection")
    parser.add_argument("--threshold", type=float, default=0.3, help="Scene cut threshold")
    parser.add_argument("--format", choices=["jpg", "png"], default="jpg", help="Output format")
    parser.add_argument("--quality", type=int, default=85, help="JPEG quality (1-100)")
    
    args = parser.parse_args()
    
    result = extract_frames(
        video_path=args.video,
        output_dir=args.output,
        fps=args.fps,
        scene_cuts=not args.no_scene_cuts,
        scene_threshold=args.threshold,
        output_format=args.format,
        quality=args.quality
    )
    
    print(json.dumps(result, indent=2))
    return 0 if result.get("success") else 1


if __name__ == "__main__":
    exit(main())
