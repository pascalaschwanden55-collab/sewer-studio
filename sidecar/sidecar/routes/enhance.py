"""Frame-Enhancement-Endpoint: Video Super Resolution fuer einzelne Frames.

POST /enhance
  Request:  { "image_base64": "...", "target_height": 1080, "denoise": true }
  Response: { "enhanced_base64": "...", "processing_time_ms": 45.2,
              "input_width": 1024, "input_height": 576,
              "output_width": 1820, "output_height": 1080,
              "scale_factor": 1.875, "backend": "realesrgan" }

Kann standalone ohne NVDEC-Pipeline genutzt werden:
  C# extrahiert Frame per FFmpeg → POST /enhance → POST /detect/yolo
"""

from __future__ import annotations

import base64
import io
import logging
import time

import numpy as np
from fastapi import APIRouter
from PIL import Image
from pydantic import BaseModel, Field

from ..models.vsr_wrapper import enhance_frame, _vsr_backend

router = APIRouter()
logger = logging.getLogger(__name__)


class EnhanceRequest(BaseModel):
    image_base64: str
    target_height: int = Field(default=1080, ge=360, le=4320)
    denoise: bool = True


class EnhanceResponse(BaseModel):
    enhanced_base64: str
    processing_time_ms: float
    input_width: int
    input_height: int
    output_width: int
    output_height: int
    scale_factor: float
    backend: str


@router.post("/enhance", response_model=EnhanceResponse)
async def enhance_image(req: EnhanceRequest) -> EnhanceResponse:
    """Video Super Resolution fuer einen einzelnen Frame.

    Skaliert niedrigaufloesende Frames (z.B. PAL 768x576) auf target_height hoch.
    Nutzt Real-ESRGAN wenn verfuegbar, sonst Lanczos-Bicubic.
    """
    # Bild dekodieren
    raw = base64.b64decode(req.image_base64)
    img = Image.open(io.BytesIO(raw)).convert("RGB")
    img_np = np.array(img)

    input_h, input_w = img_np.shape[:2]

    t0 = time.perf_counter()
    enhanced_np = enhance_frame(img_np, req.target_height, req.denoise)
    elapsed_ms = (time.perf_counter() - t0) * 1000

    output_h, output_w = enhanced_np.shape[:2]

    # Ergebnis kodieren
    pil_out = Image.fromarray(enhanced_np)
    buf = io.BytesIO()
    pil_out.save(buf, format="JPEG", quality=90)
    enhanced_b64 = base64.b64encode(buf.getvalue()).decode()

    scale_factor = output_h / input_h if input_h > 0 else 1.0

    logger.debug(
        "Enhanced %dx%d → %dx%d (%.1fx) in %.1fms via %s",
        input_w,
        input_h,
        output_w,
        output_h,
        scale_factor,
        elapsed_ms,
        _vsr_backend,
    )

    return EnhanceResponse(
        enhanced_base64=enhanced_b64,
        processing_time_ms=round(elapsed_ms, 1),
        input_width=input_w,
        input_height=input_h,
        output_width=output_w,
        output_height=output_h,
        scale_factor=round(scale_factor, 3),
        backend=_vsr_backend,
    )
