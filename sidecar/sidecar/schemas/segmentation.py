"""Pydantic DTOs for SAM segmentation endpoint."""

from __future__ import annotations

from pydantic import BaseModel, Field
from .detection import BoundingBox


class SamPoint(BaseModel):
    """Punkt-Prompt fuer SAM: x/y in Pixel, label=1 positiv, label=0 negativ."""
    x: float
    y: float
    label: int = 1  # 1=positiv (das will ich), 0=negativ (das nicht)


class RingScanParams(BaseModel):
    """Ring-Scan: SAM tastet den Annulus-Bereich (Rohrwand) systematisch ab."""
    center_x: float  # Pixel
    center_y: float  # Pixel
    inner_radius: float  # Pixel
    outer_radius: float  # Pixel
    num_angles: int = 24  # Winkelschritte (alle 15°)
    num_radii: int = 3  # Radiale Schritte zwischen inner und outer
    min_score: float = 0.25  # Mindest-Confidence (tief, da Rohrwand subtil)
    min_area_pixels: int = 100  # Mindest-Maskenflaeche
    iou_threshold: float = 0.4  # NMS IoU-Schwelle


class SamRequest(BaseModel):
    image_base64: str
    bounding_boxes: list[BoundingBox] = []
    point_prompts: list[SamPoint] | None = None
    pipe_diameter_mm: int | None = None
    ring_scan: RingScanParams | None = None

    @property
    def point_prompts_safe(self) -> list[SamPoint]:
        """Gibt immer eine Liste zurueck, auch wenn null/None gesendet wurde."""
        return self.point_prompts or []


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
