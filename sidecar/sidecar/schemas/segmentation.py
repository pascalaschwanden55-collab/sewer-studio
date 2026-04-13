"""Pydantic DTOs for SAM segmentation endpoint."""

from __future__ import annotations

from datetime import datetime
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


class YoloTrainRequest(BaseModel):
    dataset_path: str
    epochs: int = Field(default=50, ge=1, le=5000)
    imgsz: int = Field(default=640, ge=64, le=4096)
    batch: int = -1
    base_model: str = "yolo11m.pt"
    project: str = "runs/train"
    amp: bool = True
    max_fallback_ratio: float = Field(default=0.35, ge=0.0, le=1.0)


class YoloTrainMetrics(BaseModel):
    precision: float = 0.0
    recall: float = 0.0
    f1: float = 0.0
    map50: float = 0.0
    map50_95: float = 0.0


class YoloDatasetQuality(BaseModel):
    total_samples: int = 0
    total_labels: int = 0
    fallback_labels: int = 0
    fallback_ratio: float = 0.0
    distinct_classes: int = 0


class YoloTrainJobResponse(BaseModel):
    job_id: str
    status: str = "queued"
    message: str = ""


class YoloTrainJobStatusResponse(BaseModel):
    job_id: str
    status: str
    message: str = ""
    error: str | None = None
    model_path: str | None = None
    metrics: YoloTrainMetrics | None = None
    dataset_quality: YoloDatasetQuality | None = None
    epochs_completed: int = 0
    started_utc: datetime | None = None
    finished_utc: datetime | None = None


class ModelReloadRequest(BaseModel):
    model_path: str
    wait_timeout_sec: float = Field(default=30.0, ge=0.0, le=300.0)


class ModelReloadResponse(BaseModel):
    status: str = "ok"
    resolved_model_path: str = ""
    tensorrt_active: bool = False


# ── LoRA Training ──────────────────────────────────────────────────────────


class LoraTrainSample(BaseModel):
    """Ein Trainings-Sample fuer Qwen LoRA: Bild + erwartete JSON-Antwort."""
    image_base64: str
    prompt: str = "Analysiere dieses Kanalbild."
    expected_response: str = Field(description="Erwartete JSON-Antwort mit Schadenscodes")


class LoraTrainRequest(BaseModel):
    samples: list[LoraTrainSample] = []
    base_model: str = "Qwen/Qwen2.5-VL-7B-Instruct"
    lora_rank: int = Field(default=16, ge=4, le=128)
    lora_alpha: int = Field(default=32, ge=4, le=256)
    epochs: int = Field(default=3, ge=1, le=50)
    learning_rate: float = Field(default=2e-4, ge=1e-6, le=1e-2)
    batch_size: int = Field(default=1, ge=1, le=16)
    max_seq_length: int = Field(default=4096, ge=512, le=32768)
    output_dir: str = "runs/lora"


class LoraTrainMetrics(BaseModel):
    train_loss: float = 0.0
    eval_loss: float = 0.0
    epochs_completed: int = 0
    samples_trained: int = 0


class LoraTrainJobResponse(BaseModel):
    job_id: str
    status: str = "queued"
    message: str = ""


class LoraTrainJobStatusResponse(BaseModel):
    job_id: str
    status: str
    message: str = ""
    error: str | None = None
    adapter_path: str | None = None
    metrics: LoraTrainMetrics | None = None
    started_utc: datetime | None = None
    finished_utc: datetime | None = None


class LoraDeployRequest(BaseModel):
    adapter_path: str
    base_model: str = "qwen3-vl:8b"
    model_name: str = "qwen3-vl:8b-lora"
    ollama_base_url: str = "http://localhost:11434"


class LoraDeployResponse(BaseModel):
    status: str = "ok"
    model_name: str = ""
    message: str = ""


# ── SAM Batch ──────────────────────────────────────────────────────────

class SamBatchItem(BaseModel):
    image_base64: str
    bounding_boxes: list[BoundingBox] = []
    frame_id: str = ""
    pipe_diameter_mm: int | None = None


class SamBatchRequest(BaseModel):
    items: list[SamBatchItem] = Field(..., min_length=1, max_length=16)


class SamBatchResultItem(BaseModel):
    frame_id: str = ""
    result: SamResponse


class SamBatchResponse(BaseModel):
    results: list[SamBatchResultItem] = []
    total_inference_time_ms: float = 0.0
