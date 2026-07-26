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
    detector_qualified: bool | None = None
    detector_qualification_status: str = "not_checked"
    detector_qualification_reason: str | None = None
    detector_artifact_sha256: str | None = None


class BccTestYoloResponse(BaseModel):
    """Nur-lesende Antwort des getrennten BCC-Trainingskandidaten."""

    available: bool = False
    error: str | None = None
    is_relevant: bool = False
    detections: list[YoloDetection] = []
    frame_class: str = "unknown"
    inference_time_ms: float = 0.0
    candidate_id: str = ""
    candidate_sha256: str = ""
    model_name: str = ""
    device: str = ""


# ── Grounding DINO ──────────────────────────────────────────────────────────

class DinoRequest(BaseModel):
    image_base64: str
    text_prompt: str | None = None
    # Defaults konsistent zu config.py (0.25/0.20) — der C#-Client sendet die
    # Schwellen ohnehin explizit; A/B auf 57er-clean 2026-06-10 bestaetigt.
    box_threshold: float = Field(default=0.25, ge=0.0, le=1.0)
    text_threshold: float = Field(default=0.20, ge=0.0, le=1.0)


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
    # Ehrlichkeits-Felder: leere detections bei degraded=False bedeutet "kein Befund";
    # degraded=True bedeutet "Modell-/Inferenzfehler" -> Aufrufer muss das als Warnung
    # behandeln, NICHT als sauberen Negativbefund.
    degraded: bool = False
    error: str | None = None
    error_code: str | None = None


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
    # Frame-Quality-Gate: usable=False bedeutet schwarz/ueberbelichtet/strukturlos/
    # unscharf -> Frame gar nicht erst durch DINO/SAM/Qwen schicken.
    usable: bool = True
    quality_reason: str = "ok"
    # Modell-Governance: welches cls-Modell hat geantwortet (active.json-Weg).
    # Leere Werte = kein Modell geladen.
    model_name: str = ""
    model_source: str = ""        # active.json | configured | legacy_fallback
    classifier_loaded: bool = False
    model_sha256: str = ""
    imgsz: int = 0
    preprocessing: str = ""       # letterbox | default
    device: str = ""
    # Geometrisches Bogen-Veto (VSA-KEK BCC) aus demselben Frame. Da der Klassifikator
    # keine Bogen-Klasse hat und Boegen als BCE meldet, liefert die Fluchtpunkt-Geometrie
    # das Korrektiv: is_bend=True -> Frame NICHT als BCE Rohrende codieren.
    bend_shift: float = 0.0
    is_bend: bool = False
    bend_veto_failed: bool = False
    vanish_x: float = 0.5
    vanish_y: float = 0.5
