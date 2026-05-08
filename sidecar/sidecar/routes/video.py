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

import asyncio
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


def _decode_frames_blocking(
    video_path: str,
    step_seconds: float,
    frame_queue: asyncio.Queue,
    loop: asyncio.AbstractEventLoop,
    worker_count: int,
) -> None:
    frame_index = 0
    try:
        for ts, frame_rgb, backend in video_decoder.decode_frames(
            video_path, step_seconds
        ):
            frame_index += 1
            asyncio.run_coroutine_threadsafe(
                frame_queue.put((frame_index, ts, frame_rgb, backend)), loop
            ).result()
    finally:
        for _ in range(worker_count):
            asyncio.run_coroutine_threadsafe(frame_queue.put(None), loop).result()


async def _decode_frames_to_queue(
    video_path: str, step_seconds: float, frame_queue: asyncio.Queue, worker_count: int
) -> None:
    loop = asyncio.get_running_loop()
    await asyncio.to_thread(
        _decode_frames_blocking,
        video_path,
        step_seconds,
        frame_queue,
        loop,
        worker_count,
    )


def _process_frame(
    req: VideoProcessRequest, frame_item: tuple[int, float, np.ndarray, str]
) -> dict:
    frame_index, ts, frame_rgb, backend = frame_item
    t0 = time.perf_counter()

    if req.enhance and should_enhance(frame_rgb):
        frame_rgb = enhance_frame(frame_rgb, req.enhance_target_height)

    frame_rgb = _resize_if_needed(frame_rgb, req.max_width)
    h, w = frame_rgb.shape[:2]

    yolo_result = yolo_wrapper.detect_image(frame_rgb, req.confidence)
    yolo_ms = round((time.perf_counter() - t0) * 1000, 1)

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
        result["detections"] = [
            {
                "x1": d.x1,
                "y1": d.y1,
                "x2": d.x2,
                "y2": d.y2,
                "class_name": d.class_name,
                "confidence": d.confidence,
            }
            for d in yolo_result.detections
        ]
        result["image_base64"] = _frame_to_base64(frame_rgb)

    return result


async def _process_worker(
    req: VideoProcessRequest,
    frame_queue: asyncio.Queue,
    result_queue: asyncio.Queue,
) -> None:
    while True:
        frame_item = await frame_queue.get()
        if frame_item is None:
            await result_queue.put(None)
            frame_queue.task_done()
            break

        try:
            result = await asyncio.to_thread(_process_frame, req, frame_item)
            await result_queue.put(result)
        except Exception as exc:
            logger.exception("Worker-Fehler bei Frame-Verarbeitung: %s", exc)
            await result_queue.put(
                {"type": "error", "error": str(exc), "frame_index": frame_item[0]}
            )
        finally:
            frame_queue.task_done()


async def _process_video_stream(req: VideoProcessRequest):
    """Generator: liefert NDJSON-Zeilen fuer jeden Frame."""
    video_path = req.video_path

    if not Path(video_path).exists():
        error = {"error": f"Video nicht gefunden: {video_path}"}
        yield json.dumps(error) + "\n"
        return

    duration = video_decoder.get_video_duration(video_path)
    if duration <= 0:
        error = {"error": "Videodauer konnte nicht ermittelt werden"}
        yield json.dumps(error) + "\n"
        return

    total_frames = int(duration / req.step_seconds) + 1
    worker_count = max(1, settings.video_worker_count)

    header = {
        "type": "header",
        "video_path": video_path,
        "duration_sec": round(duration, 2),
        "total_frames_estimate": total_frames,
        "step_seconds": req.step_seconds,
        "nvdec_available": video_decoder.is_nvdec_available(),
        "video_worker_count": worker_count,
    }
    yield json.dumps(header) + "\n"

    frame_queue: asyncio.Queue = asyncio.Queue(maxsize=settings.video_queue_maxsize)
    result_queue: asyncio.Queue = asyncio.Queue(maxsize=settings.video_queue_maxsize)

    decode_task = asyncio.create_task(
        _decode_frames_to_queue(video_path, req.step_seconds, frame_queue, worker_count)
    )
    workers = [
        asyncio.create_task(_process_worker(req, frame_queue, result_queue))
        for _ in range(worker_count)
    ]

    finished_workers = 0
    frames_processed = 0
    while finished_workers < worker_count:
        result = await result_queue.get()
        if result is None:
            finished_workers += 1
            continue
        frames_processed += 1
        yield json.dumps(result) + "\n"

    await decode_task
    await asyncio.gather(*workers)

    footer = {
        "type": "footer",
        "frames_processed": frames_processed,
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
