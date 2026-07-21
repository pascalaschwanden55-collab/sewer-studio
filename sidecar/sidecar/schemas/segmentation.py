"""Pydantic DTOs for SAM segmentation endpoint."""

from __future__ import annotations

import base64
import binascii
import hashlib
import json
import math
import re
from typing import Literal

from pydantic import (
    BaseModel,
    ConfigDict,
    Field,
    StrictInt,
    StrictStr,
    field_validator,
    model_validator,
)
from .detection import BoundingBox


class SamRequest(BaseModel):
    image_base64: str
    # Mengen-Limit als Fail-Fast: 422 statt langsamer GPU-Last / SAM-OOM bei absurd vielen
    # Boxen. Reale Frames haben <20 Boxen; 256 ist eine grosszuegige Obergrenze.
    bounding_boxes: list[BoundingBox] = Field(default_factory=list, max_length=256)
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
    # Ehrlichkeits-Felder: requested_boxes = angefragte Boxen, skipped_boxes = still
    # uebersprungene (Fehler/ausserhalb Bild/leere Maske). degraded=True, sobald Boxen
    # verloren gingen ODER ein Inferenzfehler auftrat -> Aufrufer muss das als Warnung
    # behandeln, NICHT als vollstaendige Segmentierung.
    degraded: bool = False
    requested_boxes: int = 0
    skipped_boxes: int = 0
    # Teilmenge von skipped_boxes: Masken, die am Score-Gate (sam_min_score) scheiterten
    low_score_boxes: int = 0
    error: str | None = None
    # Geometrisches Bogen-Veto (VSA-KEK BCC) aus demselben Frame: bend_shift = horizontale
    # Fluchtpunkt-Verschiebung (-0.5..+0.5), is_bend = |shift| >= Schwelle. Robuster als
    # SAM/DINO-Labels, die Boegen nicht erkennen. C# nutzt is_bend, um einen Bogen NICHT
    # faelschlich als BCE Rohrende zu codieren.
    bend_shift: float = 0.0
    is_bend: bool = False
    vanish_x: float = 0.5
    vanish_y: float = 0.5


# ── Training Export v2 ──────────────────────────────────────────────────────

_SHA256_PATTERN = re.compile(r"^[0-9a-fA-F]{64}$")
_CLASS_NAME_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$")
_TARGET_FILE_PATTERN = re.compile(
    r"^img_(?P<sha256>[0-9a-f]{64})\.(?P<extension>jpg|jpeg|png|bmp|webp)$"
)
_MAX_MANIFEST_BYTES = 8 * 1024 * 1024


def _normalize_sha256(value: str, label: str) -> str:
    if not _SHA256_PATTERN.fullmatch(value):
        raise ValueError(f"{label} muss ein SHA-256-Hexwert mit 64 Zeichen sein")
    return value.lower()


def _reject_duplicate_json_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"doppelter JSON-Schluessel im Manifest: {key}")
        result[key] = value
    return result


class _StrictTrainingModel(BaseModel):
    model_config = ConfigDict(extra="forbid")


class TrainingExportLabel(_StrictTrainingModel):
    class_id: StrictInt = Field(ge=0)
    x_center: float = Field(ge=0.0, le=1.0)
    y_center: float = Field(ge=0.0, le=1.0)
    width: float = Field(gt=0.0, le=1.0)
    height: float = Field(gt=0.0, le=1.0)

    @field_validator("x_center", "y_center", "width", "height", mode="before")
    @classmethod
    def require_finite_number(cls, value: object) -> float:
        if isinstance(value, bool) or not isinstance(value, (int, float)):
            raise ValueError("YOLO-Koordinaten muessen Zahlen sein")
        number = float(value)
        if not math.isfinite(number):
            raise ValueError("YOLO-Koordinaten muessen endlich sein")
        return number

    @model_validator(mode="after")
    def require_box_inside_image(self) -> "TrainingExportLabel":
        epsilon = 1e-12
        if (
            self.x_center - self.width / 2.0 < -epsilon
            or self.x_center + self.width / 2.0 > 1.0 + epsilon
            or self.y_center - self.height / 2.0 < -epsilon
            or self.y_center + self.height / 2.0 > 1.0 + epsilon
        ):
            raise ValueError("YOLO-Box muss vollstaendig innerhalb des Bildes liegen")
        return self


class TrainingSample(_StrictTrainingModel):
    image_sha256: StrictStr
    image_base64: StrictStr = Field(min_length=1)
    split: Literal["train", "val"]
    target_file_name: StrictStr
    labels: list[TrainingExportLabel] = Field(max_length=256)

    @field_validator("image_sha256")
    @classmethod
    def normalize_image_sha256(cls, value: str) -> str:
        return _normalize_sha256(value, "image_sha256")

    @model_validator(mode="after")
    def require_hash_bound_target_name(self) -> "TrainingSample":
        match = _TARGET_FILE_PATTERN.fullmatch(self.target_file_name)
        if match is None or match.group("sha256") != self.image_sha256:
            raise ValueError(
                "target_file_name muss exakt img_<image_sha256>.<jpg|jpeg|png|bmp|webp> sein"
            )

        seen_labels: set[tuple[int, float, float, float, float]] = set()
        for label in self.labels:
            key = (
                label.class_id,
                label.x_center,
                label.y_center,
                label.width,
                label.height,
            )
            if key in seen_labels:
                raise ValueError("Ein Bild darf keine identischen YOLO-Labels doppelt enthalten")
            seen_labels.add(key)
        return self


class TrainingExportRequest(_StrictTrainingModel):
    schema_version: Literal["2.0"]
    plan_id: StrictStr
    plan_sha256: StrictStr
    class_map_version: StrictInt = Field(gt=0)
    vsa_manifest_hash: StrictStr
    registry_hash: StrictStr
    classes: list[StrictStr] = Field(min_length=1, max_length=1024)
    manifest_json_base64: StrictStr = Field(min_length=1)
    manifest_sha256: StrictStr
    samples: list[TrainingSample] = Field(min_length=1, max_length=500)

    @field_validator(
        "plan_id",
        "plan_sha256",
        "vsa_manifest_hash",
        "registry_hash",
        "manifest_sha256",
    )
    @classmethod
    def normalize_hash_fields(cls, value: str, info) -> str:
        return _normalize_sha256(value, info.field_name)

    @field_validator("classes")
    @classmethod
    def require_stable_unique_classes(cls, classes: list[str]) -> list[str]:
        seen: set[str] = set()
        for class_name in classes:
            if not _CLASS_NAME_PATTERN.fullmatch(class_name):
                raise ValueError(f"ungueltiger Klassenname: {class_name!r}")
            normalized = class_name.casefold()
            if normalized in seen:
                raise ValueError(f"doppelter Klassenname: {class_name}")
            seen.add(normalized)
        return classes

    @model_validator(mode="after")
    def validate_plan_content(self) -> "TrainingExportRequest":
        if self.plan_sha256 != self.plan_id:
            raise ValueError("plan_sha256 muss dem in C# berechneten plan_id entsprechen")

        manifest = self.decoded_manifest_bytes()
        if len(manifest) > _MAX_MANIFEST_BYTES:
            raise ValueError("manifest_json_base64 ist groesser als 8 MiB")
        if hashlib.sha256(manifest).hexdigest() != self.manifest_sha256:
            raise ValueError("manifest_sha256 passt nicht zu manifest_json_base64")

        try:
            parsed_manifest = json.loads(
                manifest.decode("utf-8"),
                object_pairs_hook=_reject_duplicate_json_keys,
            )
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise ValueError("manifest_json_base64 enthaelt kein gueltiges UTF-8-JSON") from exc
        if not isinstance(parsed_manifest, dict):
            raise ValueError("Das Manifest muss ein JSON-Objekt sein")

        if parsed_manifest.get("schema_version") != self.schema_version:
            raise ValueError("schema_version im Manifest stimmt nicht mit dem Request ueberein")
        manifest_plan_id = parsed_manifest.get("plan_id")
        if not isinstance(manifest_plan_id, str) or manifest_plan_id.lower() != self.plan_id:
            raise ValueError("plan_id im Manifest stimmt nicht mit dem Request ueberein")
        manifest_plan_sha256 = parsed_manifest.get("plan_sha256")
        if (
            manifest_plan_sha256 is not None
            and str(manifest_plan_sha256).lower() != self.plan_sha256
        ):
            raise ValueError("plan_sha256 im Manifest stimmt nicht mit dem Request ueberein")
        if parsed_manifest.get("class_map_version") != self.class_map_version:
            raise ValueError("class_map_version im Manifest stimmt nicht mit dem Request ueberein")
        if parsed_manifest.get("vsa_manifest_hash") != self.vsa_manifest_hash:
            raise ValueError("vsa_manifest_hash im Manifest stimmt nicht mit dem Request ueberein")
        if parsed_manifest.get("registry_hash") != self.registry_hash:
            raise ValueError("registry_hash im Manifest stimmt nicht mit dem Request ueberein")
        if parsed_manifest.get("classes") != self.classes:
            raise ValueError("Klassenliste im Manifest stimmt nicht mit dem Request ueberein")

        manifest_images = parsed_manifest.get("images")
        if not isinstance(manifest_images, list) or len(manifest_images) != len(self.samples):
            raise ValueError("Bildliste im Manifest stimmt nicht mit dem Request ueberein")

        seen_hashes: set[str] = set()
        seen_targets: set[str] = set()
        for sample, manifest_image in zip(self.samples, manifest_images, strict=True):
            if sample.image_sha256 in seen_hashes:
                raise ValueError(
                    f"Bildhash {sample.image_sha256} kommt mehrfach vor; Labels vorher zusammenfuehren"
                )
            seen_hashes.add(sample.image_sha256)

            target_key = sample.target_file_name.casefold()
            if target_key in seen_targets:
                raise ValueError(f"doppelter Zieldateiname: {sample.target_file_name}")
            seen_targets.add(target_key)

            for label in sample.labels:
                if label.class_id >= len(self.classes):
                    raise ValueError(
                        f"class_id {label.class_id} liegt ausserhalb der festen Klassenliste"
                    )
            self._validate_manifest_image(manifest_image, sample)
        return self

    def _validate_manifest_image(
        self,
        manifest_image: object,
        sample: TrainingSample,
    ) -> None:
        if not isinstance(manifest_image, dict):
            raise ValueError("Ein Bild im Manifest ist kein JSON-Objekt")
        if manifest_image.get("image_sha256") != sample.image_sha256:
            raise ValueError("Bildhash im Manifest stimmt nicht mit dem Request ueberein")
        if manifest_image.get("target_file_name") != sample.target_file_name:
            raise ValueError("Zieldateiname im Manifest stimmt nicht mit dem Request ueberein")

        manifest_target = manifest_image.get("target")
        expected_split = "train" if manifest_target == "train" else "val" if manifest_target == "validation" else None
        if expected_split != sample.split:
            raise ValueError("Split im Manifest stimmt nicht mit dem Request ueberein")

        manifest_labels = manifest_image.get("labels")
        if not isinstance(manifest_labels, list) or len(manifest_labels) != len(sample.labels):
            raise ValueError("Labels im Manifest stimmen nicht mit dem Request ueberein")
        for manifest_label, request_label in zip(manifest_labels, sample.labels, strict=True):
            if not isinstance(manifest_label, dict):
                raise ValueError("Ein Label im Manifest ist kein JSON-Objekt")
            if manifest_label.get("class_id") != request_label.class_id:
                raise ValueError("Klassen-ID im Manifest stimmt nicht mit dem Request ueberein")
            if manifest_label.get("class_name") != self.classes[request_label.class_id]:
                raise ValueError("Klassenname im Manifest stimmt nicht mit der Klassenliste ueberein")

            manifest_box = manifest_label.get("bounding_box")
            if not isinstance(manifest_box, dict):
                raise ValueError("BoundingBox im Manifest fehlt")
            expected_box = {
                "x_center": request_label.x_center,
                "y_center": request_label.y_center,
                "width": request_label.width,
                "height": request_label.height,
            }
            for field_name, expected_value in expected_box.items():
                value = manifest_box.get(field_name)
                if (
                    isinstance(value, bool)
                    or not isinstance(value, (int, float))
                    or not math.isfinite(float(value))
                    or float(value) != expected_value
                ):
                    raise ValueError(
                        f"BoundingBox-Feld {field_name} im Manifest stimmt nicht mit dem Request ueberein"
                    )

    def decoded_manifest_bytes(self) -> bytes:
        try:
            return base64.b64decode(self.manifest_json_base64, validate=True)
        except (binascii.Error, ValueError) as exc:
            raise ValueError("manifest_json_base64 ist kein gueltiges Base64") from exc


class TrainingExportResponse(_StrictTrainingModel):
    schema_version: Literal["2.0"]
    plan_id: StrictStr
    plan_sha256: StrictStr
    status: Literal["created", "already_complete"]
    total_samples: StrictInt = Field(ge=0)
    train_count: StrictInt = Field(ge=0)
    val_count: StrictInt = Field(ge=0)
    class_count: StrictInt = Field(ge=0)
    dataset_path: StrictStr
    data_yaml_path: StrictStr
    manifest_path: StrictStr
    written_image_sha256: list[StrictStr]

    @field_validator("plan_id", "plan_sha256")
    @classmethod
    def normalize_response_hashes(cls, value: str, info) -> str:
        return _normalize_sha256(value, info.field_name)
