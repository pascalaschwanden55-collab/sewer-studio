"""Pydantic DTOs for SAM segmentation endpoint."""

from __future__ import annotations

from pydantic import BaseModel, Field
from .detection import BoundingBox


class SamRequest(BaseModel):
    image_base64: str
    bounding_boxes: list[BoundingBox] = []
    pipe_diameter_mm: int | None = None


class MaskResult(BaseModel):
    label: str = ""
    confidence: float = 0.0
    bbox: list[float] = Field(default_factory=list, description="[x1,y1,x2,y2]")
    mask_rle: str = Field(default="", description="Run-length-encoded mask")
    mask_area_pixels: int = 0
    image_area_pixels: int = 0
    height_pixels: int = 0
    width_pixels: int = 0
    centroid_x: float = 0.0
    centroid_y: float = 0.0


class SamResponse(BaseModel):
    masks: list[MaskResult] = []
    image_width: int = 0
    image_height: int = 0
    inference_time_ms: float = 0.0


# ── Training Export ─────────────────────────────────────────────────────────

class TrainingSample(BaseModel):
    image_base64: str
    labels: list[dict] = Field(
        default_factory=list,
        description="[{class_name, x_center, y_center, width, height}] (YOLO normalized)"
    )


class TrainingExportRequest(BaseModel):
    samples: list[TrainingSample] = []
    output_dir: str = "./training_export"
    train_split: float = Field(default=0.8, ge=0.1, le=1.0)


class TrainingExportResponse(BaseModel):
    total_samples: int = 0
    train_count: int = 0
    val_count: int = 0
    classes_used: list[str] = []
    data_yaml_path: str = ""
