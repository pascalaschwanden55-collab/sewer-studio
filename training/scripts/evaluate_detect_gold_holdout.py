#!/usr/bin/env python3
"""Bewertet einen Detect-Gold-Kandidaten auf einem positiven Gold-Holdout.

Das feste Protokoll verwendet ``conf=0.25``, ``imgsz=1280`` und ``IoU=0.5``.
Das Werkzeug trainiert, exportiert und aktiviert nichts. Es schreibt zuerst einen
labelblinden Vorhersagebeleg und wertet erst dessen erneut gepruefte Bytes gegen
die getrennt geladene Gold-Referenz aus.
"""

from __future__ import annotations

import argparse
import base64
import gc
import hashlib
import json
import math
import os
import platform
import re
import socket
import sys
import tempfile
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import detect_gold_holdout_provenance as provenance_tools
import detect_gold_holdout_scoring as scoring


SCHEMA_VERSION = "1.0"
PREDICTION_PURPOSE = "detect_gold_positive_holdout_predictions"
EVALUATION_PURPOSE = "detect_gold_positive_holdout_evaluation"
EVALUATION_STATUS = "positive_holdout_only_not_release_qualified"
CONFIDENCE_THRESHOLD = 0.25
IMAGE_SIZE = 1280
IOU_THRESHOLD = 0.5
DEVICE_PATTERN = re.compile(r"(?:cpu|cuda(?::\d+)?)")


@dataclass(frozen=True)
class ImageSnapshot:
    image_id: str
    image_sha256: str
    image_bytes: bytes


@dataclass(frozen=True)
class RawDetection:
    prediction_id: str
    class_id: int
    class_name: str
    confidence: float
    box: scoring.Box


@dataclass(frozen=True)
class ImagePrediction:
    image_id: str
    detections: tuple[RawDetection, ...]
    inference_time_ms: float
    technical_error: str | None


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def strict_json_from_bytes(data: bytes, label: str) -> Any:
    def reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"{label} enthaelt den doppelten Schluessel {key}.")
            result[key] = value
        return result

    try:
        return json.loads(data.decode("utf-8"), object_pairs_hook=reject_duplicate_keys)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{label} ist kein gueltiges UTF-8-JSON.") from error


def _require_object(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError(f"{label} muss ein Objekt sein.")
    return value


def _require_array(value: Any, label: str) -> list[Any]:
    if not isinstance(value, list):
        raise ValueError(f"{label} muss eine Liste sein.")
    return value


def _require_number(value: Any, label: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{label} muss eine Zahl sein.")
    number = float(value)
    if not math.isfinite(number):
        raise ValueError(f"{label} muss endlich sein.")
    return number


def load_image_snapshots(
    provenance: provenance_tools.DetectGoldHoldoutProvenance,
) -> tuple[ImageSnapshot, ...]:
    snapshots: list[ImageSnapshot] = []
    seen: set[str] = set()
    for image in provenance.eligible_images:
        if image.image_id in seen:
            raise ValueError("Der positive Holdout enthaelt doppelte Bild-IDs.")
        seen.add(image.image_id)
        image_bytes = image.image_path.read_bytes()
        actual_sha = hashlib.sha256(image_bytes).hexdigest()
        if actual_sha != image.image_sha256 or image.image_id != image.image_sha256:
            raise ValueError(
                f"Bildbytes von {image.image_id} stimmen nicht mit Gold ueberein."
            )
        snapshots.append(ImageSnapshot(image.image_id, image.image_sha256, image_bytes))
    if not snapshots:
        raise ValueError("Der positive Holdout enthaelt keine sicheren Bilder.")
    return tuple(sorted(snapshots, key=lambda item: item.image_id))


def _load_runtime_modules() -> tuple[Any, Any]:
    repository_root = Path(__file__).resolve().parents[2]
    sidecar_root = repository_root / "sidecar"
    if str(sidecar_root) not in sys.path:
        sys.path.insert(0, str(sidecar_root))
    try:
        from sidecar.models import yolo_wrapper
        from ultralytics import YOLO
    except ImportError as error:
        raise RuntimeError(
            "Die KI-Laufzeit fehlt. Bitte mit "
            r".\sidecar\.venv\Scripts\python.exe starten."
        ) from error
    return YOLO, yolo_wrapper


def assert_sidecar_offline(yolo_wrapper: Any) -> None:
    host = str(yolo_wrapper.settings.host)
    port = int(yolo_wrapper.settings.port)
    connect_host = "127.0.0.1" if host.lower() == "localhost" else host
    try:
        with socket.create_connection((connect_host, port), timeout=0.5):
            pass
    except OSError:
        return
    raise ValueError(
        "Der Sidecar laeuft bereits. Fuer die direkte Kandidatenpruefung muss "
        "er beendet sein; SewerStudio wird nicht automatisch gestoppt."
    )


def normalize_model_names(value: Any) -> dict[int, str]:
    if isinstance(value, (list, tuple)):
        return {index: str(name) for index, name in enumerate(value)}
    if isinstance(value, dict):
        result: dict[int, str] = {}
        for key, name in value.items():
            try:
                class_id = int(key)
            except (TypeError, ValueError) as error:
                raise ValueError("Modellklassen besitzen keine ganzzahligen IDs.") from error
            if class_id in result:
                raise ValueError("Modellklassen besitzen doppelte IDs.")
            result[class_id] = str(name)
        return dict(sorted(result.items()))
    raise ValueError("Das Modell nennt keine gueltige Klassenkarte.")


def validate_model_contract(model: Any, expected_names: Mapping[int, str]) -> None:
    if str(getattr(model, "task", "") or "") != "detect":
        raise ValueError("Kandidatengewicht ist kein Detect-Modell.")
    if normalize_model_names(getattr(model, "names", None)) != dict(expected_names):
        raise ValueError("Modellklassen stimmen nicht mit der Gold-Klassenkarte ueberein.")


def _safe_runtime_error(prefix: str, error: Exception) -> str:
    return f"{prefix}:{error.__class__.__name__}"


def _box_from_xyxy(
    xyxy: Sequence[float],
    *,
    image_width: int,
    image_height: int,
) -> scoring.Box:
    if len(xyxy) != 4 or image_width <= 0 or image_height <= 0:
        raise ValueError("Vorhersagebox oder Bildabmessung ist ungueltig.")
    x1, y1, x2, y2 = (_require_number(value, "Vorhersagebox") for value in xyxy)
    x1 = min(max(x1, 0.0), float(image_width))
    x2 = min(max(x2, 0.0), float(image_width))
    y1 = min(max(y1, 0.0), float(image_height))
    y2 = min(max(y2, 0.0), float(image_height))
    if x2 <= x1 or y2 <= y1:
        raise ValueError("Vorhersagebox besitzt keine Flaeche.")
    return scoring.Box(
        x_center=((x1 + x2) / 2.0) / image_width,
        y_center=((y1 + y2) / 2.0) / image_height,
        width=(x2 - x1) / image_width,
        height=(y2 - y1) / image_height,
    )


def _extract_detections(
    results: Any,
    *,
    class_names: Mapping[int, str],
    image_width: int,
    image_height: int,
) -> tuple[RawDetection, ...]:
    if not isinstance(results, (list, tuple)) or len(results) != 1:
        raise ValueError("YOLO lieferte nicht genau ein Bildergebnis.")
    result = results[0]
    orig_shape = tuple(getattr(result, "orig_shape", ()))
    if orig_shape != (image_height, image_width):
        raise ValueError("YOLO-Ergebnis besitzt andere Bildabmessungen.")
    boxes = getattr(result, "boxes", None)
    if boxes is None:
        raise ValueError("YOLO-Ergebnis ist kein Detect-Ergebnis.")
    xyxy = boxes.xyxy.detach().cpu().tolist()
    confidences = boxes.conf.detach().cpu().tolist()
    class_ids = boxes.cls.detach().cpu().tolist()
    if not (len(xyxy) == len(confidences) == len(class_ids)):
        raise ValueError("YOLO lieferte widerspruechliche Boxlisten.")
    unsorted: list[tuple[int, str, float, scoring.Box]] = []
    for raw_box, raw_confidence, raw_class_id in zip(
        xyxy,
        confidences,
        class_ids,
        strict=True,
    ):
        class_number = _require_number(raw_class_id, "Klassen-ID")
        class_id = int(class_number)
        if class_number != class_id or class_id not in class_names:
            raise ValueError("YOLO lieferte eine unbekannte Klassen-ID.")
        confidence = _require_number(raw_confidence, "Konfidenz")
        if not 0.0 <= confidence <= 1.0:
            raise ValueError("YOLO lieferte eine Konfidenz ausserhalb 0..1.")
        box = _box_from_xyxy(
            raw_box,
            image_width=image_width,
            image_height=image_height,
        )
        unsorted.append((class_id, class_names[class_id], confidence, box))
    unsorted.sort(
        key=lambda item: (
            -item[2],
            item[0],
            item[3].x_center,
            item[3].y_center,
            item[3].width,
            item[3].height,
        )
    )
    return tuple(
        RawDetection(
            prediction_id=f"p{index:04d}",
            class_id=class_id,
            class_name=class_name,
            confidence=confidence,
            box=box,
        )
        for index, (class_id, class_name, confidence, box) in enumerate(
            unsorted,
            start=1,
        )
    )


def run_candidate_inference(
    provenance: provenance_tools.DetectGoldHoldoutProvenance,
    snapshots: Sequence[ImageSnapshot],
    *,
    device: str,
    progress: Callable[[int, int], None] | None = None,
) -> tuple[list[ImagePrediction], dict[str, Any]]:
    """Fuehrt nur ``predict(save=False)`` auf privaten Bild-/Gewichtsbytes aus."""

    YOLO, yolo_wrapper = _load_runtime_modules()
    assert_sidecar_offline(yolo_wrapper)
    expected_names = {index: name for index, name in enumerate(provenance.classes)}
    weights_bytes = provenance.weights_path.read_bytes()
    if hashlib.sha256(weights_bytes).hexdigest() != provenance.weights_sha256:
        raise ValueError("Kandidatengewicht wurde vor der Inferenz veraendert.")

    predictions: list[ImagePrediction] = []
    model = None
    with tempfile.TemporaryDirectory(prefix="detect_gold_holdout_") as temporary:
        private_weights = Path(temporary) / "candidate.pt"
        with private_weights.open("xb") as stream:
            stream.write(weights_bytes)
            stream.flush()
            os.fsync(stream.fileno())
        if sha256_file(private_weights) != provenance.weights_sha256:
            raise ValueError("Private Gewichtskopie ist nicht bytegleich.")
        try:
            model = YOLO(str(private_weights))
            validate_model_contract(model, expected_names)
            for index, snapshot in enumerate(snapshots, start=1):
                try:
                    encoded = base64.b64encode(snapshot.image_bytes).decode("ascii")
                    with yolo_wrapper.decode_image(encoded) as image:
                        usable, quality_reason = yolo_wrapper._is_frame_usable(image)
                        if not usable:
                            predictions.append(
                                ImagePrediction(
                                    snapshot.image_id,
                                    (),
                                    0.0,
                                    f"frame_unusable:{quality_reason}",
                                )
                            )
                            continue
                        started = time.perf_counter()
                        results = model.predict(
                            source=yolo_wrapper._pil_rgb_to_ultralytics_bgr(image),
                            conf=CONFIDENCE_THRESHOLD,
                            imgsz=IMAGE_SIZE,
                            device=device,
                            verbose=False,
                            save=False,
                        )
                        inference_ms = (time.perf_counter() - started) * 1000.0
                        detections = _extract_detections(
                            results,
                            class_names=expected_names,
                            image_width=image.width,
                            image_height=image.height,
                        )
                    predictions.append(
                        ImagePrediction(
                            snapshot.image_id,
                            detections,
                            round(inference_ms, 3),
                            None,
                        )
                    )
                except Exception as error:
                    predictions.append(
                        ImagePrediction(
                            snapshot.image_id,
                            (),
                            0.0,
                            _safe_runtime_error("inference_failed", error),
                        )
                    )
                finally:
                    if progress is not None:
                        progress(index, len(snapshots))
        finally:
            if model is not None:
                del model
            gc.collect()
            try:
                import torch

                if torch.cuda.is_available():
                    torch.cuda.empty_cache()
            except Exception:
                pass
    runtime_protocol = {
        "device": device,
        "decoded_image_color_order": "RGB",
        "model_numpy_color_order": "BGR",
        "channel_conversion": "PIL_RGB_to_contiguous_BGR",
        "frame_min_brightness": float(yolo_wrapper.settings.frame_min_brightness),
        "frame_max_brightness": float(yolo_wrapper.settings.frame_max_brightness),
        "frame_min_std": float(yolo_wrapper.settings.frame_min_std),
        "frame_min_edge_var": float(yolo_wrapper.settings.frame_min_edge_var),
        "inference_max_image_bytes": int(yolo_wrapper.settings.inference_max_image_bytes),
        "max_image_pixels": int(yolo_wrapper.settings.max_image_pixels),
    }
    return predictions, runtime_protocol


def _detection_payload(detection: RawDetection) -> dict[str, Any]:
    return {
        "prediction_id": detection.prediction_id,
        "class_id": detection.class_id,
        "class_name": detection.class_name,
        "confidence": detection.confidence,
        "box": {
            "x_center": detection.box.x_center,
            "y_center": detection.box.y_center,
            "width": detection.box.width,
            "height": detection.box.height,
        },
    }


def _prediction_payload(prediction: ImagePrediction) -> dict[str, Any]:
    return {
        "image_id": prediction.image_id,
        "detections": [_detection_payload(item) for item in prediction.detections],
        "inference_time_ms": prediction.inference_time_ms,
        "technical_error": prediction.technical_error,
    }


def prediction_receipt_sha256(predictions: Sequence[ImagePrediction]) -> str:
    return hashlib.sha256(
        canonical_json_bytes(
            [_prediction_payload(item) for item in sorted(predictions, key=lambda x: x.image_id)]
        )
    ).hexdigest()


def validate_prediction_matrix(
    snapshots: Sequence[ImageSnapshot],
    predictions: Sequence[ImagePrediction],
) -> None:
    expected = {snapshot.image_id for snapshot in snapshots}
    actual = [prediction.image_id for prediction in predictions]
    if len(expected) != len(snapshots):
        raise ValueError("Holdout enthaelt doppelte Bild-IDs.")
    if len(actual) != len(expected) or len(set(actual)) != len(actual) or set(actual) != expected:
        raise ValueError("Es liegt nicht genau eine Vorhersage je Holdout-Bild vor.")
    for prediction in predictions:
        if prediction.inference_time_ms < 0.0 or not math.isfinite(prediction.inference_time_ms):
            raise ValueError("Inferenzzeit ist ungueltig.")
        if prediction.technical_error is not None and prediction.detections:
            raise ValueError("Technischer Fehler darf keine Detektionen enthalten.")
        ids = [item.prediction_id for item in prediction.detections]
        if len(ids) != len(set(ids)):
            raise ValueError("Ein Bild besitzt doppelte Prediction-IDs.")


def build_prediction_ledger(
    provenance: provenance_tools.DetectGoldHoldoutProvenance,
    snapshots: Sequence[ImageSnapshot],
    predictions: Sequence[ImagePrediction],
    *,
    created_utc: str,
    runtime_protocol: Mapping[str, Any],
    runtime_versions: Mapping[str, str],
    purpose: str = PREDICTION_PURPOSE,
) -> dict[str, Any]:
    validate_prediction_matrix(snapshots, predictions)
    if not purpose or len(purpose) > 200:
        raise ValueError("Vorhersagebeleg-Purpose ist ungueltig.")
    return {
        "schema_version": SCHEMA_VERSION,
        "purpose": purpose,
        "created_utc": created_utc,
        "warning": (
            "NUR AUSWERTUNG. NIE FUER TRAINING, FEW-SHOT ODER "
            "AUTOMATISCHE AKTIVIERUNG VERWENDEN."
        ),
        "bindings": provenance.bindings(),
        "protocol": {
            "confidence_threshold": CONFIDENCE_THRESHOLD,
            "image_size": IMAGE_SIZE,
            "iou_threshold": IOU_THRESHOLD,
            "threshold_sweep": False,
            "technical_errors_count_as_negative": False,
            **dict(runtime_protocol),
        },
        "runtime": dict(runtime_versions),
        "images": [
            {"image_id": item.image_id, "image_sha256": item.image_sha256}
            for item in sorted(snapshots, key=lambda value: value.image_id)
        ],
        "predictions": [
            _prediction_payload(item)
            for item in sorted(predictions, key=lambda value: value.image_id)
        ],
        "prediction_receipt_sha256": prediction_receipt_sha256(predictions),
    }


def _parse_detection(
    value: Any,
    *,
    image_id: str,
    class_names: Mapping[int, str],
) -> RawDetection:
    row = _require_object(value, "Vorhersagedetektion")
    if set(row) != {"prediction_id", "class_id", "class_name", "confidence", "box"}:
        raise ValueError("Vorhersagedetektion hat fremde oder fehlende Felder.")
    prediction_id = str(row.get("prediction_id") or "")
    class_id = row.get("class_id")
    if isinstance(class_id, bool) or not isinstance(class_id, int):
        raise ValueError("Vorhersageklasse ist ungueltig.")
    class_name = str(row.get("class_name") or "")
    if not prediction_id or class_names.get(class_id) != class_name:
        raise ValueError("Prediction-ID oder Klasse ist ungueltig.")
    confidence = _require_number(row.get("confidence"), "Vorhersagekonfidenz")
    if not CONFIDENCE_THRESHOLD <= confidence <= 1.0:
        raise ValueError(
            "Vorhersagekonfidenz liegt unter der festen Schwelle oder ueber 1."
        )
    box_row = _require_object(row.get("box"), "Vorhersagebox")
    if set(box_row) != {"x_center", "y_center", "width", "height"}:
        raise ValueError("Vorhersagebox hat fremde oder fehlende Felder.")
    box = scoring.Box(
        _require_number(box_row.get("x_center"), "x_center"),
        _require_number(box_row.get("y_center"), "y_center"),
        _require_number(box_row.get("width"), "width"),
        _require_number(box_row.get("height"), "height"),
    )
    return RawDetection(prediction_id, class_id, class_name, confidence, box)


def load_prediction_ledger(
    ledger_path: Path,
    expected_file_sha256: str,
    provenance: provenance_tools.DetectGoldHoldoutProvenance,
    snapshots: Sequence[ImageSnapshot],
    *,
    expected_purpose: str = PREDICTION_PURPOSE,
) -> tuple[list[ImagePrediction], dict[str, Any]]:
    ledger_bytes = ledger_path.read_bytes()
    if hashlib.sha256(ledger_bytes).hexdigest() != expected_file_sha256:
        raise ValueError("Vorhersagebeleg-SHA stimmt nicht.")
    ledger = _require_object(
        strict_json_from_bytes(ledger_bytes, "Vorhersagebeleg"),
        "Vorhersagebeleg",
    )
    if set(ledger) != {
        "schema_version",
        "purpose",
        "created_utc",
        "warning",
        "bindings",
        "protocol",
        "runtime",
        "images",
        "predictions",
        "prediction_receipt_sha256",
    }:
        raise ValueError("Vorhersagebeleg hat fremde oder fehlende Felder.")
    if (
        ledger.get("schema_version") != SCHEMA_VERSION
        or ledger.get("purpose") != expected_purpose
    ):
        raise ValueError("Vorhersagebeleg hat Typ oder Schema gewechselt.")
    if _require_object(ledger.get("bindings"), "bindings") != provenance.bindings():
        raise ValueError("Vorhersagebeleg gehoert zu anderen Eingaben.")
    protocol = _require_object(ledger.get("protocol"), "protocol")
    if (
        protocol.get("confidence_threshold") != CONFIDENCE_THRESHOLD
        or protocol.get("image_size") != IMAGE_SIZE
        or protocol.get("iou_threshold") != IOU_THRESHOLD
        or protocol.get("threshold_sweep") is not False
        or protocol.get("technical_errors_count_as_negative") is not False
        or not str(protocol.get("device") or "")
    ):
        raise ValueError("Vorhersagebeleg verwendet ein anderes Protokoll.")
    _require_object(ledger.get("runtime"), "runtime")
    expected_images = {
        snapshot.image_id: snapshot.image_sha256 for snapshot in snapshots
    }
    actual_images: dict[str, str] = {}
    for value in _require_array(ledger.get("images"), "images"):
        row = _require_object(value, "Bildzeile")
        if set(row) != {"image_id", "image_sha256"}:
            raise ValueError("Bildzeile ist ungueltig.")
        image_id = str(row.get("image_id") or "")
        image_sha = str(row.get("image_sha256") or "")
        if image_id in actual_images or not re.fullmatch(r"[0-9a-f]{64}", image_sha):
            raise ValueError("Bildzeile besitzt eine ungueltige oder doppelte ID.")
        actual_images[image_id] = image_sha
    if actual_images != expected_images:
        raise ValueError("Vorhersagebeleg enthaelt andere Bildbytes.")

    class_names = {index: name for index, name in enumerate(provenance.classes)}
    predictions: list[ImagePrediction] = []
    for value in _require_array(ledger.get("predictions"), "predictions"):
        row = _require_object(value, "Vorhersagezeile")
        if set(row) != {"image_id", "detections", "inference_time_ms", "technical_error"}:
            raise ValueError("Vorhersagezeile hat fremde oder fehlende Felder.")
        image_id = str(row.get("image_id") or "")
        detections = tuple(
            _parse_detection(item, image_id=image_id, class_names=class_names)
            for item in _require_array(row.get("detections"), "detections")
        )
        technical_error = row.get("technical_error")
        if technical_error is not None:
            if not isinstance(technical_error, str) or not technical_error or len(technical_error) > 500:
                raise ValueError("Technischer Fehlertext ist ungueltig.")
        predictions.append(
            ImagePrediction(
                image_id=image_id,
                detections=detections,
                inference_time_ms=_require_number(row.get("inference_time_ms"), "Inferenzzeit"),
                technical_error=technical_error,
            )
        )
    validate_prediction_matrix(snapshots, predictions)
    expected_receipt = prediction_receipt_sha256(predictions)
    if ledger.get("prediction_receipt_sha256") != expected_receipt:
        raise ValueError("Vorhersagebeleg-Receipt stimmt nicht.")
    return predictions, protocol


def _assert_safe_new_target(target: Path) -> None:
    absolute = Path(os.path.abspath(target))
    parent = absolute.parent
    if not parent.is_dir() or os.path.normcase(str(parent)) != os.path.normcase(os.path.realpath(parent)):
        raise ValueError(f"Berichtsordner fehlt oder ist verknuepft: {parent}")
    if provenance_tools.prepare_tools._is_reparse_or_symlink(parent):
        raise ValueError("Berichtsordner ist eine Verknuepfung.")
    if absolute.exists() or absolute.is_symlink():
        raise FileExistsError(f"Bericht existiert bereits: {absolute}")


def atomic_write_json_new(target: Path, payload: Mapping[str, Any]) -> None:
    target = Path(os.path.abspath(target))
    _assert_safe_new_target(target)
    temporary = target.parent / f".{target.name}.{uuid.uuid4().hex}.tmp"
    data = (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    try:
        with temporary.open("xb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        _assert_safe_new_target(target)
        os.rename(temporary, target)
    finally:
        try:
            temporary.unlink(missing_ok=True)
        except OSError:
            pass


def _runtime_versions() -> dict[str, str]:
    versions = {
        "python": platform.python_version(),
        "platform": platform.platform(),
        "evaluation_script_sha256": sha256_file(Path(__file__).resolve()),
        "provenance_script_sha256": sha256_file(
            Path(provenance_tools.__file__).resolve()
        ),
        "scoring_script_sha256": sha256_file(Path(scoring.__file__).resolve()),
    }
    try:
        import numpy
        import PIL
        import torch
        import ultralytics

        versions.update(
            {
                "numpy": numpy.__version__,
                "pillow": PIL.__version__,
                "torch": torch.__version__,
                "torch_cuda": str(torch.version.cuda or ""),
                "ultralytics": ultralytics.__version__,
            }
        )
    except Exception as error:
        versions["runtime_version_error"] = error.__class__.__name__
    return versions


def _to_scoring_inputs(
    provenance: provenance_tools.DetectGoldHoldoutProvenance,
    predictions: Sequence[ImagePrediction],
) -> tuple[list[scoring.GroundTruth], list[scoring.Prediction]]:
    truths = [
        scoring.GroundTruth(
            image_id=image.image_id,
            sample_id=instance.sample_id,
            class_id=instance.class_id,
            class_name=instance.class_name,
            box=scoring.Box(
                instance.box.x_center,
                instance.box.y_center,
                instance.box.width,
                instance.box.height,
            ),
        )
        for image in provenance.eligible_images
        for instance in image.instances
    ]
    sealed = [
        scoring.Prediction(
            image_id=image.image_id,
            prediction_id=detection.prediction_id,
            class_id=detection.class_id,
            class_name=detection.class_name,
            confidence=detection.confidence,
            box=detection.box,
        )
        for image in predictions
        for detection in image.detections
        if image.technical_error is None
    ]
    return truths, sealed


def build_report(
    provenance: provenance_tools.DetectGoldHoldoutProvenance,
    metrics: Mapping[str, Any],
    *,
    ledger_sha256: str,
    prediction_receipt_sha256: str,
    created_utc: str,
    protocol: Mapping[str, Any],
    runtime_versions: Mapping[str, str],
) -> dict[str, Any]:
    distribution = {name: 0 for name in provenance.classes}
    for image in provenance.eligible_images:
        for instance in image.instances:
            distribution[instance.class_name] += 1
    return {
        "schema_version": SCHEMA_VERSION,
        "purpose": EVALUATION_PURPOSE,
        "created_utc": created_utc,
        "warning": (
            "POSITIVER HOLDOUT OHNE SAUBERE NEGATIVBILDER. NICHT FUER TRAINING "
            "ODER AUTOMATISCHE AKTIVIERUNG VERWENDEN."
        ),
        "status": EVALUATION_STATUS,
        "bindings": {
            **provenance.bindings(),
            "prediction_ledger_sha256": ledger_sha256,
            "prediction_receipt_sha256": prediction_receipt_sha256,
        },
        "protocol": dict(protocol),
        "runtime": dict(runtime_versions),
        "holdout": {
            "raw_mapped_instances": provenance.raw_instance_count,
            "raw_mapped_images": provenance.raw_image_count,
            "eligible_instances": provenance.eligible_instance_count,
            "eligible_images": provenance.eligible_image_count,
            "eligible_physical_holdings": provenance.eligible_holding_count,
            "clean_negative_images": 0,
            "positive_only": True,
            "class_distribution": distribution,
            "excluded_holdings": [
                {
                    "physical_holding_key": item.physical_holding_key,
                    "holding_keys": list(item.holding_keys),
                    "test_sample_ids": list(item.test_sample_ids),
                    "test_image_sha256": list(item.test_image_sha256),
                    "dataset_image_sha256": list(item.dataset_image_sha256),
                    "dataset_sample_ids": list(item.dataset_sample_ids),
                    "reasons": list(item.reasons),
                }
                for item in provenance.excluded_holdings
            ],
        },
        "metrics": dict(metrics),
        "release_assessment": {
            "status": EVALUATION_STATUS,
            "release_qualified": False,
            "auto_activation_allowed": False,
            "model_activated": False,
            "fresh_negative_holdout_required": True,
            "reason": (
                "Der Test misst Treffer und Lokalisation auf positiven Goldbildern, "
                "aber keine Falschalarme auf sauberen Negativbildern."
            ),
        },
        "limitations": [
            "Kein sauberer, neuer Negativ-Holdout: Spezifitaet und Bild-Falschalarme sind nicht messbar.",
            "Kein Schwellenlauf: mAP ist in diesem festen Protokoll nicht berechnet.",
            "Der Trainingsbestand des vortrainierten Basismodells ist nicht inventarisiert; nur SewerStudio-Fine-Tuning-Ueberschneidungen sind pruefbar.",
            "Klassen ohne positive Goldfaelle koennen in diesem Lauf nicht beurteilt werden.",
        ],
    }


def _parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Detect-Gold-Kandidat auf einem geschuetzten positiven Holdout pruefen."
    )
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--current-audit", type=Path, required=True)
    parser.add_argument("--device", default="cuda:0")
    args = parser.parse_args(argv)
    if DEVICE_PATTERN.fullmatch(args.device) is None:
        parser.error("--device muss cpu oder cuda[:N] sein.")
    return args


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv if argv is not None else sys.argv[1:])
    try:
        provenance = provenance_tools.load_and_validate(
            args.knowledge_root,
            args.candidate,
            args.current_audit,
        )
        snapshots = load_image_snapshots(provenance)
        print(
            f"Holdout bereit: {provenance.eligible_instance_count} Goldfaelle auf "
            f"{len(snapshots)} Bildern aus {provenance.eligible_holding_count} Haltungen."
        )
        print(
            f"Festes Protokoll: conf={CONFIDENCE_THRESHOLD}, imgsz={IMAGE_SIZE}, "
            f"IoU={IOU_THRESHOLD}, Geraet={args.device}."
        )

        def show_progress(index: int, total: int) -> None:
            if index == 1 or index % 10 == 0 or index == total:
                print(f"Inferenz: {index}/{total} Bilder", flush=True)

        predictions, runtime_protocol = run_candidate_inference(
            provenance,
            snapshots,
            device=args.device,
            progress=show_progress,
        )
        validate_prediction_matrix(snapshots, predictions)
        created = datetime.now(timezone.utc)
        created_text = created.isoformat().replace("+00:00", "Z")
        runtime = _runtime_versions()
        ledger = build_prediction_ledger(
            provenance,
            snapshots,
            predictions,
            created_utc=created_text,
            runtime_protocol=runtime_protocol,
            runtime_versions=runtime,
        )
        reports_root = Path(os.path.abspath(args.knowledge_root)) / "training" / "reports"
        if not reports_root.is_dir():
            raise ValueError(f"Berichtsordner fehlt: {reports_root}")
        stamp = created.strftime("%Y%m%d_%H%M%S_%f")
        run_name = f"{provenance.candidate_id}_{stamp}"
        ledger_path = reports_root / f"detect_gold_holdout_predictions_{run_name}.json"
        atomic_write_json_new(ledger_path, ledger)
        ledger_sha = sha256_file(ledger_path)
        print(f"Labelblinder Vorhersagebeleg: {ledger_path}")
        print(f"Vorhersagebeleg SHA-256: {ledger_sha}")
        sealed_predictions, sealed_protocol = load_prediction_ledger(
            ledger_path,
            ledger_sha,
            provenance,
            snapshots,
        )
        technical_errors = [
            {"image_id": item.image_id, "reason": item.technical_error}
            for item in sealed_predictions
            if item.technical_error is not None
        ]
        if technical_errors:
            print(
                f"Ergebnis unvollstaendig: {len(technical_errors)} technische Fehler. "
                "Sie werden nicht als Negativtreffer gewertet."
            )
            print("Kein Auswertungsbericht geschrieben; kein Modell aktiviert.")
            return 2

        rebound = provenance_tools.load_and_validate(
            args.knowledge_root,
            args.candidate,
            args.current_audit,
        )
        if rebound != provenance:
            raise ValueError("Kandidat oder Holdout wurde waehrend der Auswertung veraendert.")
        if sha256_file(ledger_path) != ledger_sha:
            raise ValueError("Vorhersagebeleg wurde vor dem Scoring veraendert.")
        truths, scored_predictions = _to_scoring_inputs(provenance, sealed_predictions)
        class_names = {index: name for index, name in enumerate(provenance.classes)}
        metrics = scoring.score_predictions(
            truths,
            scored_predictions,
            class_names,
            iou_threshold=IOU_THRESHOLD,
        )
        report = build_report(
            provenance,
            metrics,
            ledger_sha256=ledger_sha,
            prediction_receipt_sha256=str(ledger["prediction_receipt_sha256"]),
            created_utc=created_text,
            protocol=sealed_protocol,
            runtime_versions=runtime,
        )
        report_path = reports_root / f"detect_gold_holdout_evaluation_{run_name}.json"
        atomic_write_json_new(report_path, report)
        micro = metrics["micro"]
        print(
            f"Ergebnis: TP {micro['tp']}, FP {micro['fp']}, FN {micro['fn']}, "
            f"Precision {micro['precision']:.1%}, Recall {micro['recall']:.1%}, "
            f"F1 {micro['f1']:.1%}."
        )
        print(f"Status: {EVALUATION_STATUS}")
        print("Kein Modell wurde trainiert, aktiviert oder ersetzt.")
        print(f"Auswertungsbericht: {report_path}")
        return 0
    except (OSError, ValueError, FileExistsError, RuntimeError) as error:
        print(f"FEHLER: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
