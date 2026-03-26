"""YOLO pre-screening and classification endpoints."""

from fastapi import APIRouter
from ..schemas.detection import (
    YoloRequest, YoloResponse,
    YoloClassifyRequest, YoloClassifyResponse, YoloClassifyPrediction,
)
from ..models import yolo_wrapper

router = APIRouter()


@router.post("/detect/yolo", response_model=YoloResponse)
async def detect_yolo(req: YoloRequest) -> YoloResponse:
    return yolo_wrapper.detect(
        image_base64=req.image_base64,
        confidence_threshold=req.confidence_threshold,
    )


@router.post("/classify/yolo", response_model=YoloClassifyResponse)
async def classify_yolo(req: YoloClassifyRequest) -> YoloClassifyResponse:
    """Whole-Frame-Klassifikation: BCD/BCE/BCA/BCC/BAB/... erkennen."""
    import time
    t0 = time.perf_counter()
    preds = yolo_wrapper.classify(req.image_base64, top_k=req.top_k)
    elapsed_ms = (time.perf_counter() - t0) * 1000

    predictions = [
        YoloClassifyPrediction(class_name=name, confidence=conf)
        for name, conf, _ in preds
    ]

    return YoloClassifyResponse(
        predictions=predictions,
        inference_time_ms=round(elapsed_ms, 1),
    )
