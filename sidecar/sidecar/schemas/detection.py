"""Pydantic DTOs for YOLO and Grounding DINO endpoints."""

from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, model_validator


# ── YOLO ────────────────────────────────────────────────────────────────────

class YoloRequest(BaseModel):
    image_base64: str
    confidence_threshold: float = Field(default=0.25, ge=0.0, le=1.0)


class BccTestYoloRequest(BaseModel):
    """Getrennter BCC-Testvertrag ohne frei waehlbaren Modellpfad."""

    model_config = ConfigDict(extra="forbid")

    image_base64: str
    confidence_threshold: float = Field(default=0.25, ge=0.0, le=1.0)
    candidate_id: str | None = Field(
        default=None,
        pattern=r"^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$",
    )
    candidate_sha256: str | None = Field(
        default=None,
        pattern=r"^[0-9a-fA-F]{64}$",
    )
    # Optionaler Format-Lock fuer die OSD-Meterlesung desselben Bildes.
    # Werte spiegeln sidecar.osd_meter.FORMATE; None = auto (bisheriges Verhalten).
    meter_format: Literal["auto", "ein_dezimal", "vierziffern"] | None = None

    @model_validator(mode="after")
    def validate_candidate_pin(self) -> "BccTestYoloRequest":
        if (self.candidate_id is None) != (self.candidate_sha256 is None):
            raise ValueError(
                "candidate_id und candidate_sha256 muessen gemeinsam angegeben werden."
            )
        return self


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
    frame_usable: bool = True
    quality_reason: str | None = None
    # Rohe OSD-Meterlesung desselben Bildes; None = nicht lesbar, niemals "0,0".
    meter_value: float | None = None


class BccTestCandidateInfo(BaseModel):
    """Pfadfreie Metadaten eines manifest- und hashgeprueften Testkandidaten."""

    candidate_id: str
    candidate_sha256: str
    map50: float
    epochs_completed: int
    created_utc: str


class BccTestCandidatesResponse(BaseModel):
    available: bool = False
    error: str | None = None
    candidates: list[BccTestCandidateInfo] = Field(default_factory=list)


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


class LernstufeInfo(BaseModel):
    """Eine freigegebene Lernstufe, wie der Client sie auswaehlen darf."""

    model_config = ConfigDict(extra="forbid")

    klasse: str
    gewicht_sha256: str
    freigabe_sha256: str
    # Gemessen an frischen Videos mit vorher festgeschriebener Regel.
    precision: float
    recall: float
    regel: str


class LernstufenResponse(BaseModel):
    model_config = ConfigDict(extra="forbid")

    lernstufen: list[LernstufeInfo]


class LernstufeRequest(BaseModel):
    """Klasse und erwarteter Gewicht-Hash. Kein Modellpfad vom Client."""

    model_config = ConfigDict(extra="forbid")

    image_base64: str
    klasse: str = Field(pattern=r"^[a-z][a-z_]{0,31}$")
    gewicht_sha256: str = Field(pattern=r"^[0-9a-fA-F]{64}$")
    imgsz: int = Field(default=640, ge=64, le=2048)


class LernstufeResponse(BaseModel):
    """Nur eine Konfidenz fuer das GANZE Bild — diese Modelle liefern keine Box."""

    model_config = ConfigDict(extra="forbid")

    klasse: str
    konfidenz: float
    gewicht_sha256: str
    freigabe_sha256: str
    precision: float
    recall: float
    device: str | None = None
    inference_time_ms: float
