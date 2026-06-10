"""Grounding DINO detection endpoint."""

import time

from fastapi import APIRouter
from ..schemas.detection import DinoRequest, DinoResponse
from ..models import dino_wrapper
from ..telemetry import write_event

router = APIRouter()


# Bewusst sync (def): blockierende GPU-Inferenz darf den Event-Loop
# (und damit /health) nicht blockieren — siehe routes/yolo.py.
@router.post("/detect/dino", response_model=DinoResponse)
def detect_dino(req: DinoRequest) -> DinoResponse:
    started = time.perf_counter()
    response = dino_wrapper.detect(
        image_base64=req.image_base64,
        text_prompt=req.text_prompt,
        box_threshold=req.box_threshold,
        text_threshold=req.text_threshold,
    )
    write_event("dino_detect", {
        "roundtrip_ms": round((time.perf_counter() - started) * 1000, 1),
        "inference_time_ms": response.inference_time_ms,
        "box_threshold": req.box_threshold,
        "text_threshold": req.text_threshold,
        "detection_count": len(response.detections or []),
        "degraded": response.degraded,
        "error_code": response.error_code,
    })
    return response
