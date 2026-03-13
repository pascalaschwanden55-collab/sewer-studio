"""YOLO pre-screening endpoint."""

from fastapi import APIRouter
from ..schemas.detection import YoloRequest, YoloResponse
from ..models import yolo_wrapper

router = APIRouter()


@router.post("/detect/yolo", response_model=YoloResponse)
async def detect_yolo(req: YoloRequest) -> YoloResponse:
    return yolo_wrapper.detect(
        image_base64=req.image_base64,
        confidence_threshold=req.confidence_threshold,
    )
