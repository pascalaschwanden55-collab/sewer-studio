"""Getrennte Vorschau-Inferenz fuer einen nicht produktiven BCC-Kandidaten.

Der Client kann keinen Modellpfad vorgeben. Der Wrapper liest nur direkte
Unterordner des konfigurierten Kandidaten-Roots und prueft Manifest, Status,
Pilot, Mindestdatenmenge und SHA-256 der Gewichte. Das produktive YOLO-Modell
im Slot ``YOLO`` wird weder ersetzt noch entladen.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import threading
import time
from dataclasses import dataclass
from pathlib import Path

import numpy as np

from ..config import settings
from ..gpu_manager import ModelSlot, gpu_manager
from ..schemas.detection import BccTestYoloResponse, YoloDetection
from . import yolo_wrapper


class BccTestCandidateError(RuntimeError):
    """Erwarteter, benutzerfreundlich meldbarer Kandidatenfehler."""


@dataclass(frozen=True)
class BccCandidate:
    candidate_id: str
    weights_path: Path
    weights_sha256: str
    map50: float
    epochs_completed: int
    created_utc: str


_predict_lock = threading.Lock()
_loaded_candidate_sha256: str | None = None


def _is_link_or_junction(path: Path) -> bool:
    if path.is_symlink():
        return True
    is_junction = getattr(os.path, "isjunction", None)
    return bool(is_junction and is_junction(path))


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _is_sha256(value: object) -> bool:
    if not isinstance(value, str) or len(value) != 64:
        return False
    return all(char in "0123456789abcdefABCDEF" for char in value)


def _number(value: object) -> float | None:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return None
    converted = float(value)
    return converted if math.isfinite(converted) else None


def _read_candidate(child: Path, root: Path) -> BccCandidate | None:
    if not child.is_dir() or _is_link_or_junction(child):
        return None

    try:
        resolved_child = child.resolve(strict=True)
    except OSError:
        return None
    if resolved_child.parent != root:
        return None

    manifest_path = resolved_child / "candidate_manifest.json"
    weights_path = resolved_child / "best.pt"
    if (
        not manifest_path.is_file()
        or not weights_path.is_file()
        or _is_link_or_junction(manifest_path)
        or _is_link_or_junction(weights_path)
    ):
        return None

    try:
        payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError):
        return None
    if not isinstance(payload, dict):
        return None
    if payload.get("schema_version") != "1.0":
        return None
    if payload.get("candidate_status") != "not_deployed":
        return None
    if payload.get("pilot") != "BCC_bogen":
        return None

    dataset = payload.get("dataset")
    training = payload.get("training")
    weights = payload.get("weights")
    if not isinstance(dataset, dict) or not isinstance(training, dict) or not isinstance(weights, dict):
        return None

    images = dataset.get("images")
    epochs_completed = training.get("epochs_completed")
    if isinstance(images, bool) or not isinstance(images, int) or images < 30:
        return None
    if isinstance(epochs_completed, bool) or not isinstance(epochs_completed, int) or epochs_completed < 1:
        return None

    results = training.get("results")
    if not isinstance(results, dict):
        return None
    map50 = _number(results.get("metrics/mAP50(B)"))
    if map50 is None or not 0.0 <= map50 <= 1.0:
        return None

    expected_sha = weights.get("candidate_sha256")
    if not _is_sha256(expected_sha):
        return None
    try:
        actual_sha = _sha256(weights_path)
    except OSError:
        return None
    if actual_sha.lower() != expected_sha.lower():
        return None

    created_utc = payload.get("created_utc")
    if not isinstance(created_utc, str):
        created_utc = ""

    return BccCandidate(
        candidate_id=resolved_child.name,
        weights_path=weights_path,
        weights_sha256=expected_sha.lower(),
        map50=map50,
        epochs_completed=epochs_completed,
        created_utc=created_utc,
    )


def select_candidate() -> BccCandidate:
    """Waehlt deterministisch den besten gueltigen, nicht aktiven BCC-Kandidaten."""

    configured_root = Path(settings.training_model_candidates_root)
    try:
        root = configured_root.resolve(strict=True)
    except OSError as exc:
        raise BccTestCandidateError("Der BCC-Testmodell-Ordner ist nicht verfügbar.") from exc
    if not root.is_dir() or _is_link_or_junction(root):
        raise BccTestCandidateError("Der BCC-Testmodell-Ordner ist nicht sicher lesbar.")

    candidates: list[BccCandidate] = []
    try:
        children = list(root.iterdir())
    except OSError as exc:
        raise BccTestCandidateError("Der BCC-Testmodell-Ordner ist nicht lesbar.") from exc

    for child in children:
        candidate = _read_candidate(child, root)
        if candidate is not None:
            candidates.append(candidate)

    if not candidates:
        raise BccTestCandidateError(
            "Kein gültiges, nicht aktives BCC-Testmodell gefunden."
        )

    return max(
        candidates,
        key=lambda item: (
            item.map50,
            item.epochs_completed,
            item.created_utc,
            item.candidate_id,
        ),
    )


def _resolve_device() -> str:
    device = settings.effective_yolo_device
    if device.startswith("cuda") and not yolo_wrapper._cuda_available():
        return "cpu"
    return device


def _normalized_names(raw_names: object) -> dict[int, str]:
    if isinstance(raw_names, list):
        return {index: str(value) for index, value in enumerate(raw_names)}
    if isinstance(raw_names, dict):
        try:
            return {int(key): str(value) for key, value in raw_names.items()}
        except (TypeError, ValueError):
            return {}
    return {}


def _load_candidate(candidate: BccCandidate, device: str):
    from ultralytics import YOLO

    model = YOLO(str(candidate.weights_path))
    names = _normalized_names(model.names)
    if len(names) != 15 or names.get(14) != "BCC_bogen":
        raise BccTestCandidateError(
            "Das BCC-Testmodell hat nicht die freigegebene 15er-Klassenkarte."
        )
    model.to(device)
    return model, None


def _ensure_candidate_model(candidate: BccCandidate, device: str):
    global _loaded_candidate_sha256

    if _loaded_candidate_sha256 != candidate.weights_sha256:
        gpu_manager.unload(ModelSlot.YOLO_TEST)
        _loaded_candidate_sha256 = None

    state = gpu_manager.ensure_loaded(
        ModelSlot.YOLO_TEST,
        device,
        lambda: _load_candidate(candidate, device),
    )
    _loaded_candidate_sha256 = candidate.weights_sha256
    return state.model


def detect(image_base64: str, confidence_threshold: float) -> BccTestYoloResponse:
    """Fuehrt nur auf ausdruecklichen Aufruf eine BCC-Kandidaten-Erkennung aus."""

    image = yolo_wrapper.decode_image(image_base64)
    usable, quality_reason = yolo_wrapper._is_frame_usable(image)
    candidate = select_candidate()
    device = _resolve_device()

    if not usable:
        return BccTestYoloResponse(
            available=True,
            is_relevant=False,
            detections=[],
            frame_class=quality_reason,
            candidate_id=candidate.candidate_id,
            candidate_sha256=candidate.weights_sha256,
            model_name=candidate.candidate_id,
            device=device,
        )

    with _predict_lock:
        model = _ensure_candidate_model(candidate, device)
        started = time.perf_counter()
        results = model.predict(
            source=np.array(image),
            conf=confidence_threshold,
            imgsz=settings.yolo_imgsz,
            verbose=False,
        )
        inference_time_ms = (time.perf_counter() - started) * 1000

    detections: list[YoloDetection] = []
    if results:
        result = results[0]
        boxes = result.boxes
        names = _normalized_names(result.names)
        if boxes is not None:
            for box in boxes:
                xyxy = box.xyxy[0].cpu().numpy()
                class_id = int(box.cls[0].cpu().item())
                confidence = float(box.conf[0].cpu().item())
                detections.append(
                    YoloDetection(
                        x1=float(xyxy[0]),
                        y1=float(xyxy[1]),
                        x2=float(xyxy[2]),
                        y2=float(xyxy[3]),
                        class_name=names.get(class_id, f"class{class_id}"),
                        confidence=confidence,
                    )
                )

    return BccTestYoloResponse(
        available=True,
        is_relevant=bool(detections),
        detections=detections,
        frame_class="relevant" if detections else "empty",
        inference_time_ms=round(inference_time_ms, 1),
        candidate_id=candidate.candidate_id,
        candidate_sha256=candidate.weights_sha256,
        model_name=candidate.candidate_id,
        device=device,
    )
