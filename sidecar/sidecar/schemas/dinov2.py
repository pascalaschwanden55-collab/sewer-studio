"""Pydantic DTOs for DINOv2 Foundation-Encoder + Linear-Heads classification.

V4.2 Phase 3: Ersetzt die tote Grounding-DINO-Kaskade im Codier-Modus.
DINOv2 liefert gefrorene Features, pro VSA-Hauptcode ein kleiner Linear-Head
klassifiziert {not_present / mild / severe}.
"""

from __future__ import annotations

from pydantic import BaseModel, Field


class DinoV2Request(BaseModel):
    image_base64: str
    # Optional: VSA-Codes die ueberprueft werden sollen. Leer = alle geladenen Heads.
    target_codes: list[str] | None = None


class DinoV2Prediction(BaseModel):
    vsa_code: str
    severity_class: str = Field(description="not_present | mild | severe")
    confidence: float = Field(ge=0.0, le=1.0)
    # Alle Klassen-Scores (Softmax-Output)
    scores: dict[str, float] = {}


class DinoV2Response(BaseModel):
    predictions: list[DinoV2Prediction] = []
    heads_loaded: list[str] = []
    encoder_inference_time_ms: float = 0.0
    heads_inference_time_ms: float = 0.0
    total_time_ms: float = 0.0
    # V4.2 Nachbesserung B: Versionierung fuer Ursachenanalyse.
    encoder_version: str = ""
    heads_manifest_hash: str = ""
