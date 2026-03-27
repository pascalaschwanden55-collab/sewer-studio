"""Grounding DINO detection endpoint."""

from fastapi import APIRouter
from ..schemas.detection import DinoRequest, DinoResponse
from ..models import dino_wrapper

router = APIRouter()


@router.post("/detect/dino", response_model=DinoResponse)
async def detect_dino(req: DinoRequest) -> DinoResponse:
    return dino_wrapper.detect(
        image_base64=req.image_base64,
        text_prompt=req.text_prompt,
        box_threshold=req.box_threshold,
        text_threshold=req.text_threshold,
    )
