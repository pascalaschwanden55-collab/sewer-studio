"""Rohrachsen-Erkennung Endpunkte fuer Knick-Detektion."""

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
    """Batch: Mehrere Frames auf einmal analysieren."""
    import time
    t0 = time.perf_counter()
    results = [pipe_axis.analyze_pipe_axis(f.image_base64) for f in req.frames]
    total_ms = (time.perf_counter() - t0) * 1000
    return PipeAxisBatchResponse(results=results, total_time_ms=round(total_ms, 1))
