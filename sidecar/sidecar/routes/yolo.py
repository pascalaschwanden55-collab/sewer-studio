"""YOLO pre-screening and classification endpoints."""

import time

from fastapi import APIRouter
from ..schemas.detection import (
    YoloRequest, YoloResponse,
    YoloClassifyRequest, YoloClassifyResponse, YoloClassifyPrediction,
)
from ..models import yolo_wrapper
from ..telemetry import write_event, write_yolo_detection

router = APIRouter()


# Bewusst sync (def): FastAPI fuehrt sync-Handler im Threadpool aus.
# Als async def wuerde die blockierende GPU-Inferenz den Event-Loop
# und damit auch /health waehrend jeder Analyse blockieren.
@router.post("/detect/yolo", response_model=YoloResponse)
def detect_yolo(req: YoloRequest) -> YoloResponse:
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
def classify_yolo(req: YoloClassifyRequest) -> YoloClassifyResponse:
    """Whole-Frame-Klassifikation: BCD/BCE/BCA/BCC/BAB/... erkennen.

    Enthaelt das Frame-Quality-Gate: unbrauchbare Frames (schwarz, ueberbelichtet,
    strukturlos, unscharf) kommen mit usable=False zurueck, ohne Klassifikation.
    """
    t0 = time.perf_counter()
    preds, usable, quality_reason = yolo_wrapper.classify_with_quality(
        req.image_base64, top_k=req.top_k)
    elapsed_ms = (time.perf_counter() - t0) * 1000

    predictions = [
        YoloClassifyPrediction(class_name=name, confidence=conf)
        for name, conf, _ in preds
    ]
    meta = yolo_wrapper.classifier_metadata()

    write_event("yolo_classify", {
        "roundtrip_ms": round(elapsed_ms, 1),
        "top_k": req.top_k,
        "usable": usable,
        "quality_reason": quality_reason,
        "top1_class": predictions[0].class_name if predictions else None,
        "top1_confidence": predictions[0].confidence if predictions else None,
        "model_name": meta.get("name"),
        "model_source": meta.get("source"),
        "imgsz": meta.get("imgsz"),
        "preprocessing": meta.get("preprocessing"),
        "device": meta.get("device"),
    })

    return YoloClassifyResponse(
        predictions=predictions,
        inference_time_ms=round(elapsed_ms, 1),
        usable=usable,
        quality_reason=quality_reason,
        model_name=meta.get("name") or "",
        model_source=meta.get("source") or "",
        model_sha256=meta.get("sha256") or "",
        imgsz=int(meta.get("imgsz") or 0),
        preprocessing=meta.get("preprocessing") or "",
        device=meta.get("device") or "",
    )
