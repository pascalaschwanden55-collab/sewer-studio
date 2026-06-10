"""SAM segmentation endpoint."""

import time

from fastapi import APIRouter
from ..schemas.segmentation import SamRequest, SamResponse
from ..models import sam_wrapper
from ..telemetry import write_event

router = APIRouter()


# Bewusst sync (def): blockierende GPU-Inferenz darf den Event-Loop
# (und damit /health) nicht blockieren — siehe routes/yolo.py.
@router.post("/segment/sam", response_model=SamResponse)
def segment_sam(req: SamRequest) -> SamResponse:
    started = time.perf_counter()
    response = sam_wrapper.segment(
        image_base64=req.image_base64,
        bounding_boxes=req.bounding_boxes,
        pipe_diameter_mm=req.pipe_diameter_mm,
    )
    write_event("sam_segment", {
        "roundtrip_ms": round((time.perf_counter() - started) * 1000, 1),
        "inference_time_ms": response.inference_time_ms,
        "requested_boxes": response.requested_boxes,
        "skipped_boxes": response.skipped_boxes,
        "low_score_boxes": response.low_score_boxes,
        "mask_count": len(response.masks or []),
        "degraded": response.degraded,
    })
    return response
