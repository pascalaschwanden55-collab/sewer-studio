"""Pydantic DTOs fuer Rohrachsen-Erkennung (Knick-Detektion)."""

from __future__ import annotations

from pydantic import BaseModel, Field


class PipeAxisRequest(BaseModel):
    """Einzelnes Frame zur Rohrachsen-Analyse."""
    image_base64: str
    pipe_diameter_mm: int | None = None


class PipeAxisResult(BaseModel):
    """Erkannter Fluchtpunkt und Rohroeffnung eines Frames."""
    vanishing_x: float = Field(description="Fluchtpunkt X (normiert 0..1)")
    vanishing_y: float = Field(description="Fluchtpunkt Y (normiert 0..1)")
    pipe_center_x: float = Field(description="Rohrmitte X (normiert 0..1)")
    pipe_center_y: float = Field(description="Rohrmitte Y (normiert 0..1)")
    pipe_radius_x: float = Field(description="Rohrradius X (normiert)")
    pipe_radius_y: float = Field(description="Rohrradius Y (normiert)")
    confidence: float = Field(description="Erkennungs-Konfidenz 0..1")
    has_joint: bool = Field(default=False, description="Rohrverbindung (Muffe) erkannt")
    inference_time_ms: float = 0.0


class PipeAxisBatchRequest(BaseModel):
    """Mehrere Frames auf einmal analysieren (Batch fuer Video)."""
    frames: list[PipeAxisRequest] = []


class PipeAxisBatchResponse(BaseModel):
    """Batch-Ergebnis mit allen Frame-Achsen."""
    results: list[PipeAxisResult] = []
    total_time_ms: float = 0.0
