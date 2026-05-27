"""Pydantic DTOs for YOLO and Grounding DINO endpoints."""

from __future__ import annotations

from pydantic import BaseModel, Field


# ── YOLO ────────────────────────────────────────────────────────────────────

class YoloRequest(BaseModel):
    image_base64: str
    confidence_threshold: float = Field(default=0.25, ge=0.0, le=1.0)


class YoloDetection(BaseModel):
    x1: float
    y1: float
    x2: float
    y2: float
    class_name: str
    confidence: float


class YoloResponse(BaseModel):
    is_relevant: bool
    detections: list[YoloDetection] = []
    frame_class: str = "unknown"
    inference_time_ms: float = 0.0
    model_name: str | None = None
    model_backend: str | None = None
    device: str | None = None
    queue_wait_ms: float = 0.0
    vram_allocated_gb: float | None = None
    vram_total_gb: float | None = None
    gpu_utilization_percent: float | None = None


# ── Grounding DINO ──────────────────────────────────────────────────────────

class DinoRequest(BaseModel):
    image_base64: str
    text_prompt: str | None = None
    box_threshold: float = Field(default=0.30, ge=0.0, le=1.0)
    text_threshold: float = Field(default=0.25, ge=0.0, le=1.0)


class DinoDetection(BaseModel):
    x1: float
    y1: float
    x2: float
    y2: float
    label: str
    confidence: float
    phrase: str = ""


class DinoResponse(BaseModel):
    detections: list[DinoDetection] = []
    inference_time_ms: float = 0.0


# ── Bounding Box (shared input for SAM) ────────────────────────────────────

class BoundingBox(BaseModel):
    x1: float
    y1: float
    x2: float
    y2: float
    label: str = ""
    confidence: float = 1.0


# ── YOLO Classify ─────────────────────────────────────────────────────────

class YoloClassifyRequest(BaseModel):
    image_base64: str
    top_k: int = Field(default=5, ge=1, le=20)


class YoloClassifyPrediction(BaseModel):
    class_name: str
    confidence: float


class YoloClassifyResponse(BaseModel):
    predictions: list[YoloClassifyPrediction] = []
    inference_time_ms: float = 0.0
