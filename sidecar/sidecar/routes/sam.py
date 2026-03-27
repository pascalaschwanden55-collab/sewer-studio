"""SAM segmentation endpoint."""

from fastapi import APIRouter
from ..schemas.segmentation import SamRequest, SamResponse
from ..models import sam_wrapper

router = APIRouter()


@router.post("/segment/sam", response_model=SamResponse)
async def segment_sam(req: SamRequest) -> SamResponse:
    return sam_wrapper.segment(
        image_base64=req.image_base64,
        bounding_boxes=req.bounding_boxes,
        pipe_diameter_mm=req.pipe_diameter_mm,
    )
