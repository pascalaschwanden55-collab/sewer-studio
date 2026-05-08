"""Route fuer Visual ChangeNet — Aenderungserkennung zwischen Inspektionen."""

from __future__ import annotations

import logging

from fastapi import APIRouter
from pydantic import BaseModel

from ..models.changenet_wrapper import detect_changes

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/analyze")


class ChangeDetectionRequest(BaseModel):
    """Request fuer Aenderungserkennung."""

    image_old_base64: str
    image_new_base64: str
    threshold: int = 30


class ChangeDetectionResponse(BaseModel):
    """Response mit Aenderungs-Overlay und Statistik."""

    change_overlay_base64: str
    image_width: int
    image_height: int
    change_percent: float
    worse_percent: float
    better_percent: float
    total_pixels: int
    changed_pixels: int
    inference_time_ms: float
    model_used: str


@router.post("/change-detection", response_model=ChangeDetectionResponse)
async def change_detection(request: ChangeDetectionRequest) -> ChangeDetectionResponse:
    """Vergleicht zwei Inspektionsbilder und erkennt Aenderungen."""
    logger.info("ChangeDetection Request: threshold=%d", request.threshold)
    result = detect_changes(
        image_old_base64=request.image_old_base64,
        image_new_base64=request.image_new_base64,
        threshold=request.threshold,
    )
    return ChangeDetectionResponse(**result)
