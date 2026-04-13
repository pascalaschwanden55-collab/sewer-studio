"""YOLO pre-screening and classification endpoints."""

from fastapi import APIRouter, HTTPException
from ..schemas.detection import (
    YoloRequest, YoloResponse,
    YoloClassifyRequest, YoloClassifyResponse, YoloClassifyPrediction,
    YoloBatchRequest, YoloBatchResponse, YoloBatchResultItem,
)
from ..models import yolo_wrapper

router = APIRouter()


@router.post("/detect/yolo", response_model=YoloResponse)
async def detect_yolo(req: YoloRequest) -> YoloResponse:
    try:
        return yolo_wrapper.detect(
            image_base64=req.image_base64,
            confidence_threshold=req.confidence_threshold,
        )
    except RuntimeError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc


@router.post("/classify/yolo", response_model=YoloClassifyResponse)
async def classify_yolo(req: YoloClassifyRequest) -> YoloClassifyResponse:
    """Whole-Frame-Klassifikation: BCD/BCE/BCA/BCC/BAB/... erkennen."""
    import time
    t0 = time.perf_counter()
    try:
        preds = yolo_wrapper.classify(req.image_base64, top_k=req.top_k)
    except RuntimeError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc
    elapsed_ms = (time.perf_counter() - t0) * 1000

    predictions = [
        YoloClassifyPrediction(class_name=name, confidence=conf)
        for name, conf, _ in preds
    ]

    return YoloClassifyResponse(
        predictions=predictions,
        inference_time_ms=round(elapsed_ms, 1),
    )


@router.post("/detect/yolo/batch", response_model=YoloBatchResponse)
async def detect_yolo_batch(req: YoloBatchRequest) -> YoloBatchResponse:
    """Batch-YOLO: mehrere Bilder in einem Forward Pass."""
    import time
    t0 = time.perf_counter()
    try:
        results = yolo_wrapper.detect_batch(
            images_b64=[item.image_base64 for item in req.items],
            confidence_threshold=req.confidence_threshold,
            frame_ids=[item.frame_id for item in req.items],
        )
    except RuntimeError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc

    total_ms = (time.perf_counter() - t0) * 1000
    return YoloBatchResponse(
        results=[
            YoloBatchResultItem(frame_id=fid, result=resp)
            for fid, resp in results
        ],
        total_inference_time_ms=round(total_ms, 1),
    )
