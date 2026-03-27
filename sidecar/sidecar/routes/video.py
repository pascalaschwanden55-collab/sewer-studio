"""Video-Processing-Endpoint: NVDEC Hardware-Dekodierung + YOLO in einem Schritt.

POST /process/video
  Request:  { "video_path": "...", "step_seconds": 3.0, "confidence": 0.25,
              "enhance": false, "enhance_target_height": 1080 }
  Response: NDJSON-Stream (eine JSON-Zeile pro Frame)

Jede Zeile:
  { "timestamp_sec": 3.0, "frame_index": 1,
    "is_relevant": true, "frame_class": "relevant",
    "detections": [...],                  # nur wenn is_relevant
    "image_base64": "...",                # nur wenn is_relevant (fuer DINO/SAM in C#)
    "image_width": 1920, "image_height": 1080,
    "yolo_ms": 4.2, "backend": "nvdec" }

Vorteile ggue. Frame-by-Frame HTTP:
- NVDEC laeuft auf dediziertem Hardware-Decoder (kein VRAM-Verbrauch)
- Kein PNG-Encode/Base64 fuer irrelevante Frames (80-90% aller Frames)
- YOLO laeuft auf CUDA-Tensoren ohne CPU-Roundtrip
"""

from __future__ import annotations

import base64
import io
import json
import logging
import time
from pathlib import Path

import numpy as np
from fastapi import APIRouter, HTTPException
from fastapi.responses import StreamingResponse
from PIL import Image
from pydantic import BaseModel, Field

from ..models import yolo_wrapper, video_decoder
from ..models.vsr_wrapper import enhance_frame, should_enhance
from ..config import settings

router = APIRouter()
logger = logging.getLogger(__name__)


class VideoProcessRequest(BaseModel):
    video_path: str
    step_seconds: float = Field(default=3.0, ge=0.5, le=60.0)
    confidence: float = Field(default=0.25, ge=0.0, le=1.0)
    # VSR: Frame vor YOLO auf target_height hochskalieren
    enhance: bool = False
    enhance_target_height: int = Field(default=1080, ge=360, le=4320)
    # Maximale Bildbreite nach Dekodierung (Skalierung, 0 = kein Limit)
    max_width: int = Field(default=1280, ge=0, le=3840)


def _frame_to_base64(img_rgb: np.ndarray) -> str:
    """RGB numpy-Array → Base64-kodiertes JPEG."""
    pil = Image.fromarray(img_rgb)
    buf = io.BytesIO()
    pil.save(buf, format="JPEG", quality=85)
    return base64.b64encode(buf.getvalue()).decode()


def _resize_if_needed(img_rgb: np.ndarray, max_width: int) -> np.ndarray:
    """Skaliert Frame auf max_width herunter wenn noetig."""
    if max_width <= 0:
        return img_rgb
    h, w = img_rgb.shape[:2]
    if w <= max_width:
        return img_rgb
    scale = max_width / w
    new_w = max_width
    new_h = int(h * scale)
    pil = Image.fromarray(img_rgb)
    return np.array(pil.resize((new_w, new_h), Image.LANCZOS))


async def _process_video_stream(req: VideoProcessRequest):
    """Generator: liefert NDJSON-Zeilen fuer jeden Frame."""
    video_path = req.video_path

    if not Path(video_path).exists():
        error = {"error": f"Video nicht gefunden: {video_path}"}
        yield json.dumps(error) + "\n"
        return

    # Videodauer ermitteln
    duration = video_decoder.get_video_duration(video_path)
    if duration <= 0:
        error = {"error": "Videodauer konnte nicht ermittelt werden"}
        yield json.dumps(error) + "\n"
        return

    # Gesamt-Frame-Anzahl schaetzen fuer progress
    total_frames = int(duration / req.step_seconds) + 1

    # Metadaten-Header
    header = {
        "type": "header",
        "video_path": video_path,
        "duration_sec": round(duration, 2),
        "total_frames_estimate": total_frames,
        "step_seconds": req.step_seconds,
        "nvdec_available": video_decoder.is_nvdec_available(),
    }
    yield json.dumps(header) + "\n"

    frame_index = 0

    try:
        for ts, frame_rgb, backend in video_decoder.decode_frames(
            video_path, req.step_seconds
        ):
            frame_index += 1
            t0 = time.perf_counter()

            # ── Optionales VSR-Upscaling ──
            if req.enhance and should_enhance(frame_rgb):
                frame_rgb = enhance_frame(frame_rgb, req.enhance_target_height)

            # ── Groessen-Normierung ──
            frame_rgb = _resize_if_needed(frame_rgb, req.max_width)
            h, w = frame_rgb.shape[:2]

            # ── YOLO Detection ──
            frame_b64 = _frame_to_base64(frame_rgb)
            yolo_result = yolo_wrapper.detect(frame_b64, req.confidence)
            yolo_ms = round((time.perf_counter() - t0) * 1000, 1)

            # ── Frame-Ergebnis ──
            result: dict = {
                "type": "frame",
                "timestamp_sec": round(ts, 3),
                "frame_index": frame_index,
                "is_relevant": yolo_result.is_relevant,
                "frame_class": yolo_result.frame_class,
                "image_width": w,
                "image_height": h,
                "yolo_ms": yolo_ms,
                "backend": backend,
            }

            if yolo_result.is_relevant:
                # Nur fuer relevante Frames: Bounding Boxes + Bild fuer C# (DINO/SAM/Qwen)
                result["detections"] = [
                    {
                        "x1": d.x1, "y1": d.y1, "x2": d.x2, "y2": d.y2,
                        "class_name": d.class_name, "confidence": d.confidence,
                    }
                    for d in yolo_result.detections
                ]
                result["image_base64"] = frame_b64

            yield json.dumps(result) + "\n"

    except Exception as e:
        logger.exception("Fehler bei Video-Verarbeitung: %s", video_path)
        error = {"type": "error", "error": str(e), "frame_index": frame_index}
        yield json.dumps(error) + "\n"

    # Abschluss-Footer
    footer = {
        "type": "footer",
        "frames_processed": frame_index,
    }
    yield json.dumps(footer) + "\n"


@router.post("/process/video")
async def process_video(req: VideoProcessRequest) -> StreamingResponse:
    """NVDEC + YOLO Video-Pipeline als NDJSON-Stream.

    Dekodiert das Video per Hardware (NVDEC) oder Software-Fallback (PyAV),
    fuehrt YOLO-Prescreening durch und streamt die Ergebnisse.
    Relevante Frames enthalten image_base64 fuer nachgelagerte DINO/SAM/Qwen-Analyse.
    """
    if not Path(req.video_path).exists():
        raise HTTPException(
            status_code=404,
            detail=f"Video nicht gefunden: {req.video_path}",
        )

    return StreamingResponse(
        _process_video_stream(req),
        media_type="application/x-ndjson",
    )
