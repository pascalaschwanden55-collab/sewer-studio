"""Rohrachsen-Erkennung Endpunkte fuer Knick-Detektion."""

import asyncio

from fastapi import APIRouter
from ..schemas.pipe_axis import (
    PipeAxisRequest, PipeAxisResult,
    PipeAxisBatchRequest, PipeAxisBatchResponse,
)
from ..models import pipe_axis

router = APIRouter()


@router.post("/analyze/pipe-axis", response_model=PipeAxisResult)
async def analyze_pipe_axis(req: PipeAxisRequest) -> PipeAxisResult:
    """Einzelframe: Fluchtpunkt + Rohrmitte + Muffen-Erkennung."""
    return pipe_axis.analyze_pipe_axis(req.image_base64)


@router.post("/analyze/pipe-axis/batch", response_model=PipeAxisBatchResponse)
async def analyze_pipe_axis_batch(req: PipeAxisBatchRequest) -> PipeAxisBatchResponse:
    """Batch: Mehrere Frames auf einmal analysieren.

    Audit 2026-05-13 M4: Frame-CPU-Arbeit (OpenCV) ueber ``asyncio.to_thread``
    auslagern, damit der Event-Loop zwischen den Frames andere Requests
    (z.B. /health, /detect/yolo) bedienen kann. Bisherige List-Comprehension
    blockierte den Worker fuer die gesamte Batch-Laufzeit.

    Reihenfolge sequenziell — kein ``asyncio.gather``: cv2-Operationen sind zwar
    thread-safe, aber wir wollen das vorhandene Fehler-/Latenz-Verhalten 1:1
    erhalten. API-Shape und Reihenfolge der Results sind unveraendert.
    """
    import time
    t0 = time.perf_counter()
    results: list[PipeAxisResult] = []
    for f in req.frames:
        result = await asyncio.to_thread(pipe_axis.analyze_pipe_axis, f.image_base64)
        results.append(result)
    total_ms = (time.perf_counter() - t0) * 1000
    return PipeAxisBatchResponse(results=results, total_time_ms=round(total_ms, 1))
