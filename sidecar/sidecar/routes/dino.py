"""Grounding DINO detection endpoint."""

from fastapi import APIRouter
from ..schemas.detection import (
    DinoRequest,
    DinoResponse,
    DinoBatchRequest,
    DinoBatchResponse,
    DinoBatchResultItem,
)
from ..models import dino_wrapper

router = APIRouter()


@router.post("/detect/dino", response_model=DinoResponse)
async def detect_dino(req: DinoRequest) -> DinoResponse:
    return dino_wrapper.detect(
        image_base64=req.image_base64,
        text_prompt=req.text_prompt,
        box_threshold=req.box_threshold,
        text_threshold=req.text_threshold,
    )


@router.post("/detect/dino/batch", response_model=DinoBatchResponse)
async def detect_dino_batch(req: DinoBatchRequest) -> DinoBatchResponse:
    """Batch-DINO: mehrere Bilder grounding."""
    import time

    t0 = time.perf_counter()

    results = []
    for item in req.items:
        resp = dino_wrapper.detect(
            image_base64=item.image_base64,
            text_prompt=item.text_prompt,
            box_threshold=req.box_threshold,
            text_threshold=req.text_threshold,
        )
        results.append(DinoBatchResultItem(frame_id=item.frame_id, result=resp))

    total_ms = (time.perf_counter() - t0) * 1000
    return DinoBatchResponse(
        results=results,
        total_inference_time_ms=round(total_ms, 1),
    )
