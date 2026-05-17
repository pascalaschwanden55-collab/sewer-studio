"""SAM segmentation endpoint."""

import asyncio

from fastapi import APIRouter
from ..schemas.segmentation import (
    SamRequest,
    SamResponse,
    SamBatchRequest,
    SamBatchResponse,
    SamBatchResultItem,
)
from ..models import sam_wrapper

router = APIRouter()


@router.post("/segment/sam", response_model=SamResponse)
async def segment_sam(req: SamRequest) -> SamResponse:
    return await asyncio.to_thread(
        sam_wrapper.segment,
        image_base64=req.image_base64,
        bounding_boxes=req.bounding_boxes,
        pipe_diameter_mm=req.pipe_diameter_mm,
        point_prompts=req.point_prompts_safe,
        ring_scan=req.ring_scan,
    )


@router.post("/segment/sam/batch", response_model=SamBatchResponse)
async def segment_sam_batch(req: SamBatchRequest) -> SamBatchResponse:
    """Batch-SAM: mehrere Bilder mit je N Boxen segmentieren.
    Innerhalb jedes Bildes sind die Boxen gebatched (ein Forward Pass).

    Audit 2026-05-13 (M4-Folge): Pro-Item-Inference ueber ``asyncio.to_thread``
    auslagern, damit der Event-Loop zwischen den Bildern andere Requests
    (z.B. /health) bedienen kann. Reihenfolge bleibt sequenziell — kein
    ``asyncio.gather``: SAM teilt einen GPU-Model-State, paralleler Zugriff
    waere unsicher. API-Shape und Latenzverhalten sind unveraendert.
    """
    import time

    t0 = time.perf_counter()

    results = []
    for item in req.items:
        resp = await asyncio.to_thread(
            sam_wrapper.segment,
            image_base64=item.image_base64,
            bounding_boxes=item.bounding_boxes,
            pipe_diameter_mm=item.pipe_diameter_mm,
        )
        results.append(SamBatchResultItem(frame_id=item.frame_id, result=resp))

    total_ms = (time.perf_counter() - t0) * 1000
    return SamBatchResponse(
        results=results,
        total_inference_time_ms=round(total_ms, 1),
    )
