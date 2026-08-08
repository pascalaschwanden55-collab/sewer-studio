"""YOLO pre-screening and classification endpoints."""

import time
import logging

import numpy as np
from fastapi import APIRouter
from ..schemas.detection import (
    YoloRequest, YoloResponse,
    BccTestYoloRequest, BccTestYoloResponse,
    BccTestCandidateInfo, BccTestCandidatesResponse,
    YoloClassifyRequest, YoloClassifyResponse, YoloClassifyPrediction,
)
from ..models import bcc_test_wrapper, detector_qualification, yolo_wrapper
from ..models.bend_geometry import analyze_bend
from ..config import settings
from ..telemetry import write_event, write_yolo_detection

logger = logging.getLogger(__name__)
router = APIRouter()


# Bewusst sync (def): FastAPI fuehrt sync-Handler im Threadpool aus.
# Als async def wuerde die blockierende GPU-Inferenz den Event-Loop
# und damit auch /health waehrend jeder Analyse blockieren.
@router.post("/detect/yolo", response_model=YoloResponse)
def detect_yolo(req: YoloRequest) -> YoloResponse:
    started = time.perf_counter()
    qualification = detector_qualification.evaluate_active_detector()
    artifact = qualification["artifact"]
    if not qualification["qualified"]:
        # Eingabe weiterhin validieren, aber das gesperrte Modell weder laden noch
        # ausfuehren. is_relevant=True laesst den sicheren DINO/SAM-Pfad weiterlaufen.
        yolo_wrapper.decode_image(req.image_base64)
        runtime = yolo_wrapper.get_runtime_status()
        response = YoloResponse(
            is_relevant=True,
            detections=[],
            frame_class="detector_unqualified",
            inference_time_ms=0.0,
            model_name=artifact.get("file_name") or settings.yolo_model_name,
            model_backend=artifact.get("backend"),
            device=runtime.get("device"),
            detector_qualified=False,
            detector_qualification_status=qualification["status"],
            detector_qualification_reason=qualification["reason"],
            detector_artifact_sha256=artifact.get("sha256"),
        )
    else:
        response = yolo_wrapper.detect(
            image_base64=req.image_base64,
            confidence_threshold=req.confidence_threshold,
        ).model_copy(
            update={
                "detector_qualified": True,
                "detector_qualification_status": qualification["status"],
                "detector_qualification_reason": qualification["reason"],
                "detector_artifact_sha256": artifact.get("sha256"),
            }
        )
    elapsed_ms = (time.perf_counter() - started) * 1000
    write_yolo_detection(
        response,
        confidence_threshold=req.confidence_threshold,
        roundtrip_ms=elapsed_ms,
    )
    return response


@router.post("/detect/yolo/bcc-test", response_model=BccTestYoloResponse)
def detect_yolo_bcc_test(req: BccTestYoloRequest) -> BccTestYoloResponse:
    """Prueft ein Foto mit einem manifest- und hashgeprueften BCC-Kandidaten."""

    started = time.perf_counter()
    try:
        response = bcc_test_wrapper.detect(
            image_base64=req.image_base64,
            confidence_threshold=req.confidence_threshold,
            candidate_id=req.candidate_id,
            candidate_sha256=req.candidate_sha256,
            meter_format=req.meter_format,
        )
    except bcc_test_wrapper.BccTestCandidateError as exc:
        logger.warning("BCC-Testmodell nicht verfuegbar: %s", exc)
        response = BccTestYoloResponse(available=False, error=str(exc))

    write_event("bcc_test_detection", {
        "roundtrip_ms": round((time.perf_counter() - started) * 1000, 1),
        "available": response.available,
        "detection_count": len(response.detections),
        "candidate_id": response.candidate_id or None,
        "requested_candidate_id": req.candidate_id,
        "candidate_sha256": response.candidate_sha256 or None,
        "device": response.device or None,
        "confidence_threshold": req.confidence_threshold,
    })
    return response


@router.get(
    "/detect/yolo/bcc-test/candidates",
    response_model=BccTestCandidatesResponse,
)
def get_yolo_bcc_test_candidates() -> BccTestCandidatesResponse:
    """Liefert pfadfreie Metadaten manifest- und hashgepruefter Kandidaten."""

    try:
        candidates = bcc_test_wrapper.list_candidates()
        response = BccTestCandidatesResponse(
            available=True,
            candidates=[
                BccTestCandidateInfo(
                    candidate_id=item.candidate_id,
                    candidate_sha256=item.weights_sha256,
                    map50=item.map50,
                    epochs_completed=item.epochs_completed,
                    created_utc=item.created_utc,
                )
                for item in candidates
            ],
        )
    except bcc_test_wrapper.BccTestCandidateError as exc:
        logger.warning("BCC-Testkandidaten nicht verfuegbar: %s", exc)
        response = BccTestCandidatesResponse(available=False, error=str(exc))

    write_event(
        "bcc_test_candidates",
        {
            "available": response.available,
            "candidate_count": len(response.candidates),
        },
    )
    return response


@router.post("/classify/yolo", response_model=YoloClassifyResponse)
def classify_yolo(req: YoloClassifyRequest) -> YoloClassifyResponse:
    """Whole-Frame-Klassifikation: BCD/BCE/BCA/BCC/BAB/... erkennen.

    Enthaelt das Frame-Quality-Gate: unbrauchbare Frames (schwarz, ueberbelichtet,
    strukturlos, unscharf) kommen mit usable=False zurueck, ohne Klassifikation.
    """
    t0 = time.perf_counter()
    img = yolo_wrapper.decode_image(req.image_base64)
    preds, usable, quality_reason = yolo_wrapper.classify_image_with_quality(
        img, top_k=req.top_k)
    elapsed_ms = (time.perf_counter() - t0) * 1000

    # Geometrisches Bogen-Veto aus demselben Frame. Dieser leichte Veto bleibt aktiv,
    # auch wenn das alte SAM/Bogen-Overlay deaktiviert ist.
    bend_shift, is_bend, vanish_x, vanish_y = 0.0, False, 0.5, 0.5
    bend_veto_failed = False
    if settings.bend_veto_enabled or settings.bend_geometry_enabled:
        try:
            _bend = analyze_bend(np.array(img))
            bend_shift, is_bend = round(_bend.shift, 4), _bend.is_bend
            vanish_x, vanish_y = round(_bend.vanish_x, 4), round(_bend.vanish_y, 4)
        except Exception:
            bend_veto_failed = True
            logger.warning("Bogen-Geometrie im classify fehlgeschlagen", exc_info=True)

    predictions = [
        YoloClassifyPrediction(class_name=name, confidence=conf)
        for name, conf, _ in preds
    ]
    meta = yolo_wrapper.classifier_metadata()
    classifier_loaded = bool(meta)

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
        "classifier_loaded": classifier_loaded,
        "bend_shift": bend_shift,
        "is_bend": is_bend,
        "bend_veto_failed": bend_veto_failed,
    })

    return YoloClassifyResponse(
        predictions=predictions,
        inference_time_ms=round(elapsed_ms, 1),
        usable=usable,
        quality_reason=quality_reason,
        model_name=meta.get("name") or "",
        model_source=meta.get("source") or "",
        classifier_loaded=classifier_loaded,
        model_sha256=meta.get("sha256") or "",
        imgsz=int(meta.get("imgsz") or 0),
        preprocessing=meta.get("preprocessing") or "",
        device=meta.get("device") or "",
        bend_shift=bend_shift,
        is_bend=is_bend,
        bend_veto_failed=bend_veto_failed,
        vanish_x=vanish_x,
        vanish_y=vanish_y,
    )
