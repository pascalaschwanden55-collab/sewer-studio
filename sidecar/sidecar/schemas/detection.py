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
