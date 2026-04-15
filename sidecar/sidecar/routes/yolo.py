"""YOLO pre-screening, classification and viewtype endpoints."""

from fastapi import APIRouter, HTTPException
from ..schemas.detection import (
    YoloRequest, YoloResponse,
    YoloClassifyRequest, YoloClassifyResponse, YoloClassifyPrediction,
    YoloBatchRequest, YoloBatchResponse, YoloBatchResultItem,
)
from ..models import yolo_wrapper
from pydantic import BaseModel

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


# ── Aufnahmetechnik-Klassifikator (viewtype) ─────────────────────────────

class ViewTypeRequest(BaseModel):
    image_base64: str

class ViewTypePrediction(BaseModel):
    view_type: str        # "axial", "schacht", "uebergang"
    confidence: float
    all_scores: dict[str, float]

class ViewTypeResponse(BaseModel):
    prediction: ViewTypePrediction
    inference_time_ms: float

# Modell lazy laden (nur beim ersten Aufruf)
_viewtype_model = None

def _get_viewtype_model():
    """Laedt das Aufnahmetechnik-Modell (3MB, einmalig)."""
    global _viewtype_model
    if _viewtype_model is None:
        from pathlib import Path
        model_path = Path(r"C:\KI_BRAIN\yolo_viewtype_runs\viewtype_v1\weights\best.pt")
        if not model_path.exists():
            raise FileNotFoundError(f"Viewtype-Modell nicht gefunden: {model_path}")
        from ultralytics import YOLO
        _viewtype_model = YOLO(str(model_path))
    return _viewtype_model


@router.post("/classify/viewtype", response_model=ViewTypeResponse)
async def classify_viewtype(req: ViewTypeRequest) -> ViewTypeResponse:
    """Aufnahmetechnik-Klassifikation: axial / schacht / uebergang."""
    import time
    import base64
    import numpy as np
    from PIL import Image
    import io

    t0 = time.perf_counter()
    try:
        model = _get_viewtype_model()
        img_bytes = base64.b64decode(req.image_base64)
        img = Image.open(io.BytesIO(img_bytes)).convert("RGB")

        results = model.predict(img, verbose=False)
        probs = results[0].probs

        # Top-Klasse
        top_idx = int(probs.top1)
        top_conf = float(probs.top1conf)
        class_name = model.names[top_idx]

        # Alle Scores
        all_scores = {model.names[i]: round(float(probs.data[i]), 4) for i in range(len(model.names))}

    except FileNotFoundError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc

    elapsed_ms = (time.perf_counter() - t0) * 1000

    return ViewTypeResponse(
        prediction=ViewTypePrediction(
            view_type=class_name,
            confidence=round(top_conf, 4),
            all_scores=all_scores,
        ),
        inference_time_ms=round(elapsed_ms, 1),
    )
