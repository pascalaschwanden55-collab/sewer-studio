"""V4.2 Phase 3: DINOv2 Foundation-Encoder + Linear-Heads classification endpoint."""

from fastapi import APIRouter

from ..models import dinov2_wrapper
from ..schemas.dinov2 import DinoV2Request, DinoV2Response

router = APIRouter()


@router.post("/classify/dinov2", response_model=DinoV2Response)
async def classify_dinov2(req: DinoV2Request) -> DinoV2Response:
    """
    Klassifiziert ein Frame via DINOv2-Features + pro-Code Linear-Heads.
    Antwortet mit leerer predictions-Liste wenn noch keine Heads trainiert sind —
    der C#-Client faellt dann auf den Qwen-Fallback zurueck.
    """
    return dinov2_wrapper.classify(req.image_base64, req.target_codes)


@router.post("/classify/dinov2/reload")
async def reload_dinov2_heads():
    """Laedt alle Linear-Heads neu (nach Training-Update)."""
    return dinov2_wrapper.reload_heads()
