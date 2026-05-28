"""YOLO pre-screening and classification endpoints."""

import time

from fastapi import APIRouter
from ..schemas.detection import (
    YoloRequest, YoloResponse,
    YoloClassifyRequest, YoloClassifyResponse, YoloClassifyPrediction,
)
from ..models import yolo_wrapper
from ..telemetry import write_yolo_detection

router = APIRouter()


@router.post("/detect/yolo", response_model=YoloResponse)
async def detect_yolo(req: YoloRequest) -> YoloResponse:
    started = time.perf_counter()
    response = yolo_wrapper.detect(
        image_base64=req.image_base64,
        confidence_threshold=req.confidence_threshold,
    )
    elapsed_ms = (time.perf_counter() - started) * 1000
    write_yolo_detection(
        response,
        confidence_threshold=req.confidence_threshold,
        roundtrip_ms=elapsed_ms,
    )
    return response


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
