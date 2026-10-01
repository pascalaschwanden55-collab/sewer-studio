#!/usr/bin/env python3
"""Vergleicht eingefrorene BCC-Kandidaten auf einem fertigen Blind-Holdout.

Das Protokoll ist absichtlich fest: Konfidenz 0.25, Bildgroesse 1280 und nur
Klasse 14 ``BCC_bogen``. Das Werkzeug trainiert, exportiert und aktiviert
nichts. Es schreibt zuerst einen labelblinden Vorhersagebeleg und wertet diesen
danach gegen die getrennte menschliche Review aus.
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
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, Sequence

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import bcc_release_holdout as holdout_tools


SCHEMA_VERSION = "1.0"
PREDICTION_PURPOSE = "bcc_release_holdout_predictions"
EVALUATION_PURPOSE = "bcc_release_holdout_binary_evaluation"
PILOT_NAME = "BCC_bogen"
TARGET_CLASS_ID = 14
TARGET_CLASS_NAME = "BCC_bogen"
CONFIDENCE_THRESHOLD = 0.25
IMAGE_SIZE = 1280
EXPECTED_CLASS_NAMES = {
    0: "BCA_anschluss",
    1: "BAB_riss",
    2: "BAC_bruch",
    3: "BAA_verformung",
    4: "BAF_oberflaeche",
    5: "BAH_schadanschluss",
    6: "BAI_dichtung",
    7: "BAJ_verbindung",
    8: "BBA_wurzeln",
    9: "BBB_anhaftung",
    10: "BBC_ablagerung",
    11: "BBD_boden",
    12: "BBF_infiltration",
    13: "SONST_schaden",
    14: TARGET_CLASS_NAME,
}
CANDIDATE_ID_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$")


@dataclass(frozen=True)
class BlindCase:
    item_id: str
    image_path: Path
    image_sha256: str


@dataclass(frozen=True)
class ImageSnapshot:
    item_id: str
    image_sha256: str
    image_bytes: bytes


@dataclass(frozen=True)
class CandidateBinding:
    candidate_id: str
    candidate_manifest_path: Path
    candidate_manifest_sha256: str
    weights_path: Path
    weights_sha256: str
    dataset_plan_id: str
    dataset_manifest_sha256: str
    map50: float
    epochs_completed: int | None
    created_utc: str
    production_manifest_eligible: bool
    production_manifest_reason: str
    diagnostic_marker_present: bool
    diagnostic_marker_sha256: str | None

    @property
    def production_eligible(self) -> bool:
        return (
            self.production_manifest_eligible
            and not self.diagnostic_marker_present
        )

    @property
    def comparison_role(self) -> str:
        return "release_candidate" if self.production_eligible else "diagnostic_only"


@dataclass(frozen=True)
class RawPrediction:
    item_id: str
    predicted_positive: bool | None
    detection_count: int
    max_confidence: float | None
    inference_time_ms: float
    technical_error: str | None


@dataclass(frozen=True)
class ScoredPrediction:
    item_id: str
    expected_positive: bool
    predicted_positive: bool
    detection_count: int
    max_confidence: float | None


@dataclass(frozen=True)
class EvaluationContext:
    holdout_root: Path
    review_path: Path
    holdout_id: str
    holdout_manifest_sha256: str
    holdout_candidates_sha256: str
    review_sha256: str
    review_bytes: bytes
    candidate_scope_sha256: str
    frozen_candidate_scope: tuple[dict[str, Any], ...]
    cases: tuple[BlindCase, ...]
    review_labels: tuple[tuple[str, bool], ...]
    excluded_item_ids: tuple[str, ...]
    positive_images: int
    negative_images: int
    excluded_images: int


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
    """Liest JSON ohne stilles Akzeptieren doppelter Schluessel."""

    def reject_duplicate_keys(
        pairs: list[tuple[str, Any]],
    ) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(
                    f"{label} enthaelt den doppelten Schluessel {key!r}."
                )
            result[key] = value
        return result

    try:
        text = data.decode("utf-8-sig")
        return json.loads(text, object_pairs_hook=reject_duplicate_keys)
    except ValueError:
        raise
    except (UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{label} ist kein gueltiges JSON.") from error


def _require_object(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError(f"{label} muss ein JSON-Objekt sein.")
    return value


def _require_array(value: Any, label: str) -> list[Any]:
    if not isinstance(value, list):
        raise ValueError(f"{label} muss ein JSON-Array sein.")
    return value


def _require_sha256(value: Any, label: str) -> str:
    return holdout_tools._require_sha256(value, label)


def _safe_number(value: Any, label: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{label} muss eine endliche Zahl sein.")
    number = float(value)
    if not math.isfinite(number):
        raise ValueError(f"{label} muss eine endliche Zahl sein.")
    return number


def _production_manifest_status(
    manifest: Mapping[str, Any],
) -> tuple[bool, str]:
    dataset = manifest.get("dataset")
    training = manifest.get("training")
    if not isinstance(dataset, dict) or not isinstance(training, dict):
        return False, "dataset oder training fehlt"
    images = dataset.get("images")
    if isinstance(images, bool) or not isinstance(images, int) or images < 30:
        return False, "dataset.images ist kleiner als 30"
    epochs = training.get("epochs_completed")
    if isinstance(epochs, bool) or not isinstance(epochs, int) or epochs < 1:
        return False, "training.epochs_completed fehlt oder ist ungueltig"
    results = training.get("results")
    if not isinstance(results, dict):
        return False, "training.results fehlt"
    try:
        map50 = _safe_number(
            results.get("metrics/mAP50(B)"),
            "training.results.metrics/mAP50(B)",
        )
    except ValueError:
        return False, "metrics/mAP50(B) fehlt oder ist ungueltig"
    if not 0.0 <= map50 <= 1.0:
        return False, "metrics/mAP50(B) liegt ausserhalb 0..1"
    return True, "manifest_erfuellt_sidecar_mindestvertrag"


def load_candidate_bindings(
    knowledge_root: Path,
    frozen_scope: Sequence[Mapping[str, Any]],
) -> list[CandidateBinding]:
    """Bindet exakt die im Holdout eingefrorenen Kandidaten an lokale Bytes."""

    knowledge = Path(os.path.abspath(knowledge_root))
    candidate_root = knowledge / "training" / "models" / "candidates"
    candidate_root = holdout_tools._safe_existing_path(
        candidate_root,
        knowledge,
        expect_file=False,
    )
    bindings: list[CandidateBinding] = []
    seen_ids: set[str] = set()

    for index, raw_scope in enumerate(frozen_scope):
        scope = _require_object(dict(raw_scope), f"candidate_scope[{index}]")
        candidate_id = str(scope.get("candidate_id") or "")
        if (
            CANDIDATE_ID_PATTERN.fullmatch(candidate_id) is None
            or candidate_id in seen_ids
        ):
            raise ValueError(
                f"candidate_scope[{index}] enthaelt keine eindeutige sichere ID."
            )
        seen_ids.add(candidate_id)

        candidate_dir = holdout_tools._safe_existing_path(
            candidate_root / candidate_id,
            candidate_root,
            expect_file=False,
        )
        manifest_path = holdout_tools._safe_existing_path(
            candidate_dir / "candidate_manifest.json",
            candidate_dir,
            expect_file=True,
        )
        weights_path = holdout_tools._safe_existing_path(
            candidate_dir / "best.pt",
            candidate_dir,
            expect_file=True,
        )
        expected_manifest_sha = _require_sha256(
            scope.get("candidate_manifest_sha256"),
            f"Manifest-SHA {candidate_id}",
        )
        manifest_bytes = manifest_path.read_bytes()
        actual_manifest_sha = hashlib.sha256(manifest_bytes).hexdigest()
        if actual_manifest_sha != expected_manifest_sha:
            raise ValueError(
                f"Manifest-SHA des Kandidaten {candidate_id} hat sich geaendert."
            )
        expected_weights_sha = _require_sha256(
            scope.get("weights_sha256"),
            f"Gewichts-SHA {candidate_id}",
        )
        actual_weights_sha = sha256_file(weights_path)
        if actual_weights_sha != expected_weights_sha:
            raise ValueError(
                f"Gewichts-SHA des Kandidaten {candidate_id} hat sich geaendert."
            )

        manifest = _require_object(
            strict_json_from_bytes(
                manifest_bytes,
                f"Kandidatenmanifest {candidate_id}",
            ),
            f"Kandidatenmanifest {candidate_id}",
        )
        if manifest.get("schema_version") != "1.0":
            raise ValueError(f"{candidate_id} braucht Kandidatenschema 1.0.")
        if manifest.get("candidate_status") != "not_deployed":
            raise ValueError(f"{candidate_id} ist nicht mehr not_deployed.")
        if manifest.get("pilot") != PILOT_NAME:
            raise ValueError(f"{candidate_id} ist kein BCC-Pilot.")
        weights = _require_object(
            manifest.get("weights"),
            f"weights im Manifest {candidate_id}",
        )
        if (
            _require_sha256(
                weights.get("candidate_sha256"),
                f"Manifest-Gewichts-SHA {candidate_id}",
            )
            != expected_weights_sha
        ):
            raise ValueError(
                f"Manifest und Holdout widersprechen sich bei {candidate_id}."
            )
        dataset = _require_object(
            manifest.get("dataset"),
            f"dataset im Manifest {candidate_id}",
        )
        training = _require_object(
            manifest.get("training"),
            f"training im Manifest {candidate_id}",
        )
        results = _require_object(
            training.get("results"),
            f"training.results im Manifest {candidate_id}",
        )
        map50 = _safe_number(
            results.get("metrics/mAP50(B)"),
            f"mAP50 im Manifest {candidate_id}",
        )
        if not 0.0 <= map50 <= 1.0:
            raise ValueError(f"mAP50 im Manifest {candidate_id} ist ungueltig.")
        epochs = training.get("epochs_completed")
        epochs_completed = (
            epochs
            if isinstance(epochs, int) and not isinstance(epochs, bool)
            else None
        )
        eligible, reason = _production_manifest_status(manifest)

        marker_path = candidate_dir / "MARKER_aufgehoben.txt"
        marker_present = False
        marker_sha256: str | None = None
        if marker_path.exists() or marker_path.is_symlink():
            safe_marker = holdout_tools._safe_existing_path(
                marker_path,
                candidate_dir,
                expect_file=True,
            )
            marker_present = True
            marker_sha256 = sha256_file(safe_marker)
            reason = f"{reason}; Kandidat ist als aufgehoben markiert"

        bindings.append(
            CandidateBinding(
                candidate_id=candidate_id,
                candidate_manifest_path=manifest_path,
                candidate_manifest_sha256=actual_manifest_sha,
                weights_path=weights_path,
                weights_sha256=actual_weights_sha,
                dataset_plan_id=str(
                    scope.get("dataset_plan_id")
                    or dataset.get("plan_id")
                    or ""
                ),
                dataset_manifest_sha256=str(
                    scope.get("dataset_manifest_sha256")
                    or dataset.get("manifest_sha256")
                    or ""
                ),
                map50=map50,
                epochs_completed=epochs_completed,
                created_utc=str(manifest.get("created_utc") or ""),
                production_manifest_eligible=eligible,
                production_manifest_reason=reason,
                diagnostic_marker_present=marker_present,
                diagnostic_marker_sha256=marker_sha256,
            )
        )

    if len(bindings) != len(frozen_scope):
        raise ValueError("Nicht alle eingefrorenen Kandidaten wurden gebunden.")
    return bindings


def _review_snapshot(
    review_bytes: bytes,
    *,
    case_ids: set[str],
    holdout_id: str,
    manifest_sha256: str,
    candidates_sha256: str,
) -> tuple[tuple[tuple[str, bool], ...], tuple[str, ...]]:
    review = _require_object(
        strict_json_from_bytes(review_bytes, "BCC-Review"),
        "BCC-Review",
    )
    expected_fields = {
        "schema_version",
        "purpose",
        "holdout_id",
        "manifest_sha256",
        "candidates_sha256",
        "reviewer",
        "updated_at_utc",
        "decisions",
    }
    if set(review) != expected_fields:
        raise ValueError("BCC-Review enthaelt fehlende oder fremde Felder.")
    if review.get("schema_version") != holdout_tools.REVIEW_SCHEMA:
        raise ValueError("BCC-Review hat ein ungueltiges Schema.")
    if review.get("purpose") != "bcc_release_holdout_review":
        raise ValueError("Datei ist kein BCC-Holdout-Review.")
    if str(review.get("holdout_id") or "") != holdout_id:
        raise ValueError("BCC-Review gehoert zu einem anderen Holdout.")
    if (
        _require_sha256(review.get("manifest_sha256"), "Review Manifest-SHA")
        != manifest_sha256
        or _require_sha256(
            review.get("candidates_sha256"),
            "Review Candidates-SHA",
        )
        != candidates_sha256
    ):
        raise ValueError("BCC-Review ist nicht an diesen Holdout gebunden.")
    holdout_tools._require_review_text(
        review.get("reviewer"),
        "Review-Reviewer",
        allow_empty=False,
        maximum=120,
    )
    holdout_tools._require_review_timestamp(
        review.get("updated_at_utc"),
        "Review-Aktualisierungszeitpunkt",
    )
    decisions = _require_object(review.get("decisions"), "Review decisions")
    if set(decisions) != case_ids:
        raise ValueError(
            "BCC-Review muss genau eine Entscheidung je Holdout-Bild enthalten."
        )

    labels: list[tuple[str, bool]] = []
    excluded: list[str] = []
    for item_id in sorted(case_ids):
        decision = _require_object(
            decisions[item_id],
            f"Review-Entscheidung {item_id}",
        )
        if set(decision) != {"decision", "comment", "reviewed_at_utc"}:
            raise ValueError(
                f"Review-Entscheidung {item_id} hat fremde oder fehlende Felder."
            )
        holdout_tools._require_review_text(
            decision.get("comment"),
            f"Review-Kommentar {item_id}",
            allow_empty=True,
            maximum=2000,
        )
        holdout_tools._require_review_timestamp(
            decision.get("reviewed_at_utc"),
            f"Review-Zeitpunkt {item_id}",
        )
        value = str(decision.get("decision") or "").strip().casefold()
        if value == "positive":
            labels.append((item_id, True))
        elif value == "negative":
            labels.append((item_id, False))
        elif value == "exclude":
            excluded.append(item_id)
        else:
            raise ValueError(
                f"Review-Entscheidung fuer {item_id} ist ungueltig."
            )
    return tuple(labels), tuple(excluded)


def _load_context(
    knowledge_root: Path,
    base_model_path: Path,
    holdout_root: Path,
    review_path: Path,
) -> EvaluationContext:
    status = holdout_tools.evaluate_holdout_status(
        knowledge_root,
        base_model_path,
        holdout_root,
        review_path,
    )
    if status.get("dataset_status") != "ready_for_binary_evaluation":
        raise ValueError(
            "Der Holdout ist nicht ready_for_binary_evaluation."
        )

    holdout = Path(os.path.abspath(holdout_root))
    review = Path(os.path.abspath(review_path))
    review = holdout_tools._safe_existing_path(
        review,
        review.parent,
        expect_file=True,
    )
    manifest, candidates = holdout_tools._validate_holdout_files(holdout)
    manifest_sha256 = sha256_file(holdout / "_manifest.json")
    candidates_sha256 = sha256_file(holdout / "_candidates.json")
    holdout_id = _require_sha256(manifest.get("holdout_id"), "holdout_id")
    frozen_scope = tuple(
        _require_object(item, f"candidate_scope[{index}]")
        for index, item in enumerate(
            _require_array(manifest.get("candidate_scope"), "candidate_scope")
        )
    )
    cases: list[BlindCase] = []
    for index, candidate in enumerate(candidates):
        item_id = str(candidate.get("id") or "")
        file_name = str(candidate.get("frame_path") or "")
        image_sha = _require_sha256(
            candidate.get("source_sha256"),
            f"source_sha256 von Kandidat {index}",
        )
        if not item_id or Path(file_name).name != file_name:
            raise ValueError("Holdout-Kandidat enthaelt einen unsicheren Bildnamen.")
        image_path = holdout_tools._safe_existing_path(
            holdout / "images" / file_name,
            holdout / "images",
            expect_file=True,
        )
        cases.append(BlindCase(item_id, image_path, image_sha))

    case_ids = {case.item_id for case in cases}
    if (
        len(cases) != len(case_ids)
        or len(cases) != int(status["total_images"])
    ):
        raise ValueError("Status und Holdout-Bildmenge widersprechen sich.")
    review_bytes = review.read_bytes()
    review_sha256 = hashlib.sha256(review_bytes).hexdigest()
    review_labels, excluded_ids = _review_snapshot(
        review_bytes,
        case_ids=case_ids,
        holdout_id=holdout_id,
        manifest_sha256=manifest_sha256,
        candidates_sha256=candidates_sha256,
    )
    positive_images = sum(label for _, label in review_labels)
    negative_images = len(review_labels) - positive_images
    if (
        review_sha256 != sha256_file(review)
        or positive_images != int(status["positive_images"])
        or negative_images != int(status["negative_images"])
        or len(excluded_ids) != int(status["excluded_images"])
    ):
        raise ValueError(
            "Review wurde waehrend der Statuspruefung veraendert."
        )
    return EvaluationContext(
        holdout_root=holdout,
        review_path=review,
        holdout_id=holdout_id,
        holdout_manifest_sha256=manifest_sha256,
        holdout_candidates_sha256=candidates_sha256,
        review_sha256=review_sha256,
        review_bytes=review_bytes,
        candidate_scope_sha256=_require_sha256(
            manifest.get("candidate_scope_sha256"),
            "candidate_scope_sha256",
        ),
        frozen_candidate_scope=frozen_scope,
        cases=tuple(sorted(cases, key=lambda item: item.item_id)),
        review_labels=review_labels,
        excluded_item_ids=excluded_ids,
        positive_images=positive_images,
        negative_images=negative_images,
        excluded_images=len(excluded_ids),
    )


def load_image_snapshots(cases: Sequence[BlindCase]) -> list[ImageSnapshot]:
    snapshots: list[ImageSnapshot] = []
    for case in cases:
        image_bytes = case.image_path.read_bytes()
        actual_sha = hashlib.sha256(image_bytes).hexdigest()
        if actual_sha != case.image_sha256:
            raise ValueError(
                f"Bildbytes von {case.item_id} stimmen nicht mit dem Holdout ueberein."
            )
        snapshots.append(
            ImageSnapshot(case.item_id, case.image_sha256, image_bytes)
        )
    return snapshots


def _safe_runtime_error(prefix: str, error: Exception) -> str:
    if error.__class__.__name__ == "BccTestCandidateError":
        message = str(error).strip()
        if message and not any(char in message for char in ("\\", "/", ":")):
            return f"{prefix}:{message}"
    return f"{prefix}:{error.__class__.__name__}"


def _load_runtime_modules():
    repository_root = Path(__file__).resolve().parents[2]
    sidecar_root = repository_root / "sidecar"
    sidecar_text = str(sidecar_root)
    if sidecar_text not in sys.path:
        sys.path.insert(0, sidecar_text)
    try:
        from sidecar.models import bcc_test_wrapper, yolo_wrapper
    except ImportError as error:
        raise RuntimeError(
            "Die KI-Laufzeit fehlt. Bitte dieses Werkzeug mit "
            r".\sidecar\.venv\Scripts\python.exe starten."
        ) from error

    if dict(bcc_test_wrapper._EXPECTED_CLASS_NAMES) != EXPECTED_CLASS_NAMES:
        raise ValueError(
            "Die BCC-Klassenkarte des Sidecars stimmt nicht mit dem "
            "Auswertungsprotokoll ueberein."
        )
    return bcc_test_wrapper, yolo_wrapper


def _assert_sidecar_offline(yolo_wrapper: Any) -> None:
    host = str(yolo_wrapper.settings.host)
    port = int(yolo_wrapper.settings.port)
    connect_host = "127.0.0.1" if host.lower() == "localhost" else host
    try:
        with socket.create_connection((connect_host, port), timeout=0.5):
            pass
    except OSError:
        return
    raise ValueError(
        "Der Sidecar laeuft bereits. Fuer den direkten, reproduzierbaren "
        "Kandidatenvergleich muss er beendet sein; SewerStudio wird nicht "
        "automatisch gestoppt."
    )


def _runtime_protocol_settings(yolo_wrapper: Any) -> dict[str, Any]:
    settings = yolo_wrapper.settings
    return {
        "frame_min_brightness": float(settings.frame_min_brightness),
        "frame_max_brightness": float(settings.frame_max_brightness),
        "frame_min_std": float(settings.frame_min_std),
        "frame_min_edge_var": float(settings.frame_min_edge_var),
        "inference_max_image_bytes": int(settings.inference_max_image_bytes),
        "max_image_pixels": int(settings.max_image_pixels),
    }


def run_candidate_inference(
    binding: CandidateBinding,
    snapshots: Sequence[ImageSnapshot],
    *,
    device: str | None = None,
) -> tuple[list[RawPrediction], str]:
    """Fuehrt ausschliesslich ``predict(save=False)`` auf privaten Bytes aus."""

    bcc_test_wrapper, yolo_wrapper = _load_runtime_modules()
    selected_device = device or bcc_test_wrapper._resolve_device()
    candidate = bcc_test_wrapper.BccCandidate(
        candidate_id=binding.candidate_id,
        weights_path=binding.weights_path,
        weights_sha256=binding.weights_sha256,
        map50=binding.map50,
        epochs_completed=binding.epochs_completed or 0,
        created_utc=binding.created_utc,
    )
    try:
        model, _ = bcc_test_wrapper._load_candidate(
            candidate,
            selected_device,
        )
    except Exception as error:
        reason = _safe_runtime_error("candidate_load_failed", error)
        return (
            [
                RawPrediction(
                    snapshot.item_id,
                    None,
                    0,
                    None,
                    0.0,
                    reason,
                )
                for snapshot in snapshots
            ],
            selected_device,
        )

    predictions: list[RawPrediction] = []
    try:
        for snapshot in snapshots:
            try:
                encoded = base64.b64encode(snapshot.image_bytes).decode("ascii")
                with yolo_wrapper.decode_image(encoded) as image:
                    usable, quality_reason = yolo_wrapper._is_frame_usable(
                        image
                    )
                    if not usable:
                        predictions.append(
                            RawPrediction(
                                snapshot.item_id,
                                None,
                                0,
                                None,
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
                        verbose=False,
                        save=False,
                    )
                inference_ms = (time.perf_counter() - started) * 1000.0
                detections = bcc_test_wrapper._extract_bcc_detections(results)
                confidences = [
                    float(detection.confidence) for detection in detections
                ]
                predictions.append(
                    RawPrediction(
                        snapshot.item_id,
                        bool(detections),
                        len(detections),
                        max(confidences) if confidences else None,
                        round(inference_ms, 3),
                        None,
                    )
                )
            except Exception as error:
                predictions.append(
                    RawPrediction(
                        snapshot.item_id,
                        None,
                        0,
                        None,
                        0.0,
                        _safe_runtime_error("inference_failed", error),
                    )
                )
    finally:
        del model
        gc.collect()
        try:
            import torch

            if torch.cuda.is_available():
                torch.cuda.empty_cache()
        except Exception:
            pass
    return predictions, selected_device


def _raw_prediction_payload(prediction: RawPrediction) -> dict[str, Any]:
    return {
        "image_id": prediction.item_id,
        "predicted_positive": prediction.predicted_positive,
        "bcc_detection_count": prediction.detection_count,
        "max_bcc_confidence": prediction.max_confidence,
        "inference_time_ms": prediction.inference_time_ms,
        "technical_error": prediction.technical_error,
    }


def prediction_receipt_sha256(
    predictions_by_candidate: Mapping[str, Sequence[RawPrediction]],
) -> str:
    payload = [
        {
            "candidate_id": candidate_id,
            "predictions": [
                _raw_prediction_payload(prediction)
                for prediction in sorted(
                    predictions_by_candidate[candidate_id],
                    key=lambda item: item.item_id,
                )
            ],
        }
        for candidate_id in sorted(predictions_by_candidate)
    ]
    return hashlib.sha256(canonical_json_bytes(payload)).hexdigest()


def validate_prediction_matrix(
    bindings: Sequence[CandidateBinding],
    snapshots: Sequence[ImageSnapshot],
    predictions_by_candidate: Mapping[str, Sequence[RawPrediction]],
) -> None:
    expected_candidates = [binding.candidate_id for binding in bindings]
    if set(predictions_by_candidate) != set(expected_candidates):
        raise ValueError(
            "Vorhersagebeleg enthaelt nicht exakt alle eingefrorenen Kandidaten."
        )
    expected_images = {snapshot.item_id for snapshot in snapshots}
    if len(expected_images) != len(snapshots):
        raise ValueError("Holdout enthaelt doppelte Bild-IDs.")
    for candidate_id in expected_candidates:
        predictions = predictions_by_candidate[candidate_id]
        actual_ids = [prediction.item_id for prediction in predictions]
        if (
            len(actual_ids) != len(expected_images)
            or len(set(actual_ids)) != len(actual_ids)
            or set(actual_ids) != expected_images
        ):
            raise ValueError(
                f"{candidate_id} hat nicht exakt eine Vorhersage je Holdout-Bild."
            )


def build_prediction_ledger(
    context: EvaluationContext,
    bindings: Sequence[CandidateBinding],
    snapshots: Sequence[ImageSnapshot],
    predictions_by_candidate: Mapping[str, Sequence[RawPrediction]],
    *,
    devices_by_candidate: Mapping[str, str],
    frame_quality_settings: Mapping[str, Any],
    created_utc: str,
    runtime_versions: Mapping[str, str],
) -> dict[str, Any]:
    validate_prediction_matrix(bindings, snapshots, predictions_by_candidate)
    if set(devices_by_candidate) != {
        binding.candidate_id for binding in bindings
    }:
        raise ValueError("Geraetebindung ist fuer die Kandidaten unvollstaendig.")
    devices = set(devices_by_candidate.values())
    if len(devices) != 1:
        raise ValueError("Alle Kandidaten muessen auf demselben Geraet laufen.")
    device = next(iter(devices))
    image_hashes = {
        snapshot.item_id: snapshot.image_sha256 for snapshot in snapshots
    }
    return {
        "schema_version": SCHEMA_VERSION,
        "purpose": PREDICTION_PURPOSE,
        "created_utc": created_utc,
        "warning": (
            "NUR AUSWERTUNG. NIE FUER TRAINING, FEW-SHOT ODER "
            "AUTOMATISCHE AKTIVIERUNG VERWENDEN."
        ),
        "bindings": {
            "holdout_id": context.holdout_id,
            "manifest_sha256": context.holdout_manifest_sha256,
            "candidates_sha256": context.holdout_candidates_sha256,
            "candidate_scope_sha256": context.candidate_scope_sha256,
        },
        "protocol": {
            "class_id": TARGET_CLASS_ID,
            "class_name": TARGET_CLASS_NAME,
            "confidence_threshold": CONFIDENCE_THRESHOLD,
            "image_size": IMAGE_SIZE,
            "threshold_sweep": False,
            "positive_rule": "mindestens_eine_bcc_box",
            "technical_errors_count_as_negative": False,
            "device": device,
            "frame_quality_gate": dict(frame_quality_settings),
        },
        "runtime": dict(runtime_versions),
        "images": [
            {
                "image_id": item_id,
                "image_sha256": image_hashes[item_id],
            }
            for item_id in sorted(image_hashes)
        ],
        "candidates": [
            {
                "candidate_id": binding.candidate_id,
                "candidate_manifest_sha256": (
                    binding.candidate_manifest_sha256
                ),
                "weights_sha256": binding.weights_sha256,
                "dataset_plan_id": binding.dataset_plan_id,
                "dataset_manifest_sha256": binding.dataset_manifest_sha256,
                "comparison_role": binding.comparison_role,
                "sidecar_manifest_eligible": (
                    binding.production_manifest_eligible
                ),
                "selection_eligible": binding.production_eligible,
                "production_manifest_eligible": (
                    binding.production_manifest_eligible
                ),
                "diagnostic_marker_present": (
                    binding.diagnostic_marker_present
                ),
                "diagnostic_marker_sha256": (
                    binding.diagnostic_marker_sha256
                ),
                "device": devices_by_candidate[binding.candidate_id],
                "predictions": [
                    _raw_prediction_payload(prediction)
                    for prediction in sorted(
                        predictions_by_candidate[binding.candidate_id],
                        key=lambda item: item.item_id,
                    )
                ],
            }
            for binding in bindings
        ],
        "prediction_receipt_sha256": prediction_receipt_sha256(
            predictions_by_candidate
        ),
    }


def load_prediction_ledger(
    ledger_path: Path,
    expected_file_sha256: str,
    context: EvaluationContext,
    bindings: Sequence[CandidateBinding],
    snapshots: Sequence[ImageSnapshot],
) -> tuple[dict[str, list[RawPrediction]], str]:
    """Liest genau den versiegelten, labelblinden Beleg fuer das Scoring."""

    ledger_bytes = ledger_path.read_bytes()
    if hashlib.sha256(ledger_bytes).hexdigest() != expected_file_sha256:
        raise ValueError("Vorhersagebeleg-SHA stimmt nicht.")
    ledger = _require_object(
        strict_json_from_bytes(ledger_bytes, "Vorhersagebeleg"),
        "Vorhersagebeleg",
    )
    expected_top_fields = {
        "schema_version",
        "purpose",
        "created_utc",
        "warning",
        "bindings",
        "protocol",
        "runtime",
        "images",
        "candidates",
        "prediction_receipt_sha256",
    }
    if set(ledger) != expected_top_fields:
        raise ValueError(
            "Vorhersagebeleg enthaelt fremde oder fehlende Felder."
        )
    if (
        ledger.get("schema_version") != SCHEMA_VERSION
        or ledger.get("purpose") != PREDICTION_PURPOSE
    ):
        raise ValueError("Vorhersagebeleg hat Typ oder Schema gewechselt.")

    ledger_bindings = _require_object(
        ledger.get("bindings"),
        "Vorhersagebeleg bindings",
    )
    if ledger_bindings != {
        "holdout_id": context.holdout_id,
        "manifest_sha256": context.holdout_manifest_sha256,
        "candidates_sha256": context.holdout_candidates_sha256,
        "candidate_scope_sha256": context.candidate_scope_sha256,
    }:
        raise ValueError("Vorhersagebeleg gehoert zu anderen Eingaben.")
    protocol = _require_object(
        ledger.get("protocol"),
        "Vorhersagebeleg protocol",
    )
    if (
        protocol.get("class_id") != TARGET_CLASS_ID
        or protocol.get("class_name") != TARGET_CLASS_NAME
        or protocol.get("confidence_threshold") != CONFIDENCE_THRESHOLD
        or protocol.get("image_size") != IMAGE_SIZE
        or protocol.get("threshold_sweep") is not False
        or protocol.get("technical_errors_count_as_negative") is not False
    ):
        raise ValueError("Vorhersagebeleg verwendet ein anderes Protokoll.")
    device = str(protocol.get("device") or "")
    if not device:
        raise ValueError("Vorhersagebeleg nennt kein Inferenzgeraet.")
    _require_object(
        protocol.get("frame_quality_gate"),
        "Vorhersagebeleg frame_quality_gate",
    )
    _require_object(ledger.get("runtime"), "Vorhersagebeleg runtime")

    expected_images = {
        snapshot.item_id: snapshot.image_sha256 for snapshot in snapshots
    }
    image_rows = _require_array(ledger.get("images"), "Vorhersagebeleg images")
    actual_images: dict[str, str] = {}
    for row in image_rows:
        image_row = _require_object(row, "Vorhersagebeleg image")
        if set(image_row) != {"image_id", "image_sha256"}:
            raise ValueError("Vorhersagebeleg-Bildzeile ist ungueltig.")
        image_id = str(image_row.get("image_id") or "")
        if image_id in actual_images:
            raise ValueError("Vorhersagebeleg enthaelt eine doppelte Bild-ID.")
        actual_images[image_id] = _require_sha256(
            image_row.get("image_sha256"),
            f"Bild-SHA {image_id}",
        )
    if actual_images != expected_images:
        raise ValueError("Vorhersagebeleg enthaelt andere Bildbytes.")

    binding_by_id = {binding.candidate_id: binding for binding in bindings}
    candidate_rows = _require_array(
        ledger.get("candidates"),
        "Vorhersagebeleg candidates",
    )
    predictions_by_candidate: dict[str, list[RawPrediction]] = {}
    for raw_candidate in candidate_rows:
        candidate_row = _require_object(
            raw_candidate,
            "Vorhersagebeleg-Kandidat",
        )
        expected_candidate_fields = {
            "candidate_id",
            "candidate_manifest_sha256",
            "weights_sha256",
            "dataset_plan_id",
            "dataset_manifest_sha256",
            "comparison_role",
            "sidecar_manifest_eligible",
            "selection_eligible",
            "production_manifest_eligible",
            "diagnostic_marker_present",
            "diagnostic_marker_sha256",
            "device",
            "predictions",
        }
        if set(candidate_row) != expected_candidate_fields:
            raise ValueError("Vorhersagebeleg-Kandidat ist ungueltig.")
        candidate_id = str(candidate_row.get("candidate_id") or "")
        if candidate_id in predictions_by_candidate or candidate_id not in binding_by_id:
            raise ValueError(
                "Vorhersagebeleg enthaelt eine fremde oder doppelte Kandidaten-ID."
            )
        binding = binding_by_id[candidate_id]
        if (
            candidate_row.get("candidate_manifest_sha256")
            != binding.candidate_manifest_sha256
            or candidate_row.get("weights_sha256") != binding.weights_sha256
            or candidate_row.get("dataset_plan_id") != binding.dataset_plan_id
            or candidate_row.get("dataset_manifest_sha256")
            != binding.dataset_manifest_sha256
            or candidate_row.get("comparison_role") != binding.comparison_role
            or candidate_row.get("sidecar_manifest_eligible")
            is not binding.production_manifest_eligible
            or candidate_row.get("selection_eligible")
            is not binding.production_eligible
            or candidate_row.get("production_manifest_eligible")
            is not binding.production_manifest_eligible
            or candidate_row.get("diagnostic_marker_present")
            is not binding.diagnostic_marker_present
            or candidate_row.get("diagnostic_marker_sha256")
            != binding.diagnostic_marker_sha256
            or str(candidate_row.get("device") or "") != device
        ):
            raise ValueError(
                f"Vorhersagebeleg-Bindung von {candidate_id} stimmt nicht."
            )

        predictions: list[RawPrediction] = []
        for raw_prediction in _require_array(
            candidate_row.get("predictions"),
            f"Vorhersagen {candidate_id}",
        ):
            prediction = _require_object(
                raw_prediction,
                f"Vorhersage {candidate_id}",
            )
            if set(prediction) != {
                "image_id",
                "predicted_positive",
                "bcc_detection_count",
                "max_bcc_confidence",
                "inference_time_ms",
                "technical_error",
            }:
                raise ValueError("Vorhersagezeile enthaelt fremde Felder.")
            predicted = prediction.get("predicted_positive")
            if predicted is not None and not isinstance(predicted, bool):
                raise ValueError("predicted_positive muss Boolean oder null sein.")
            count = prediction.get("bcc_detection_count")
            if isinstance(count, bool) or not isinstance(count, int) or count < 0:
                raise ValueError("bcc_detection_count ist ungueltig.")
            raw_confidence = prediction.get("max_bcc_confidence")
            confidence = (
                None
                if raw_confidence is None
                else _safe_number(raw_confidence, "max_bcc_confidence")
            )
            if confidence is not None and not 0.0 <= confidence <= 1.0:
                raise ValueError("max_bcc_confidence liegt ausserhalb 0..1.")
            inference_ms = _safe_number(
                prediction.get("inference_time_ms"),
                "inference_time_ms",
            )
            if inference_ms < 0.0:
                raise ValueError("inference_time_ms ist negativ.")
            technical_error = prediction.get("technical_error")
            if technical_error is not None:
                technical_error = holdout_tools._require_review_text(
                    technical_error,
                    "technical_error",
                    allow_empty=False,
                    maximum=500,
                )
            if (
                (technical_error is None and not isinstance(predicted, bool))
                or (technical_error is not None and predicted is not None)
                or (predicted is True and (count < 1 or confidence is None))
                or (predicted is False and (count != 0 or confidence is not None))
            ):
                raise ValueError("Vorhersagezeile ist intern widerspruechlich.")
            predictions.append(
                RawPrediction(
                    item_id=str(prediction.get("image_id") or ""),
                    predicted_positive=predicted,
                    detection_count=count,
                    max_confidence=confidence,
                    inference_time_ms=inference_ms,
                    technical_error=technical_error,
                )
            )
        predictions_by_candidate[candidate_id] = predictions

    validate_prediction_matrix(bindings, snapshots, predictions_by_candidate)
    receipt = prediction_receipt_sha256(predictions_by_candidate)
    if (
        _require_sha256(
            ledger.get("prediction_receipt_sha256"),
            "prediction_receipt_sha256",
        )
        != receipt
    ):
        raise ValueError("Vorhersagebeleg-Receipt stimmt nicht.")
    return predictions_by_candidate, device


def load_review_labels(
    context: EvaluationContext,
) -> dict[str, bool]:
    if hashlib.sha256(context.review_bytes).hexdigest() != context.review_sha256:
        raise ValueError("Gebundene Review-Momentaufnahme ist widerspruechlich.")
    return dict(context.review_labels)


def wilson_interval(successes: int, total: int) -> dict[str, float] | None:
    if total <= 0:
        return None
    z = 1.959963984540054
    proportion = successes / total
    denominator = 1.0 + (z * z / total)
    center = (proportion + z * z / (2.0 * total)) / denominator
    margin = (
        z
        * math.sqrt(
            (proportion * (1.0 - proportion) / total)
            + (z * z / (4.0 * total * total))
        )
        / denominator
    )
    return {
        "lower": max(0.0, center - margin),
        "upper": min(1.0, center + margin),
    }


def _divide(numerator: int, denominator: int) -> float | None:
    return numerator / denominator if denominator else None


def compute_binary_metrics(
    outcomes: Sequence[ScoredPrediction],
) -> dict[str, Any]:
    true_positive = sum(
        item.expected_positive and item.predicted_positive for item in outcomes
    )
    false_negative = sum(
        item.expected_positive and not item.predicted_positive
        for item in outcomes
    )
    false_positive = sum(
        not item.expected_positive and item.predicted_positive
        for item in outcomes
    )
    true_negative = sum(
        not item.expected_positive and not item.predicted_positive
        for item in outcomes
    )
    positives = true_positive + false_negative
    negatives = true_negative + false_positive
    sensitivity = _divide(true_positive, positives)
    specificity = _divide(true_negative, negatives)
    precision = _divide(true_positive, true_positive + false_positive)
    negative_predictive_value = _divide(
        true_negative,
        true_negative + false_negative,
    )
    accuracy = _divide(
        true_positive + true_negative,
        positives + negatives,
    )
    f1 = _divide(
        2 * true_positive,
        2 * true_positive + false_positive + false_negative,
    )
    balanced_accuracy = (
        (sensitivity + specificity) / 2.0
        if sensitivity is not None and specificity is not None
        else None
    )
    return {
        "images": len(outcomes),
        "positive_images": positives,
        "negative_images": negatives,
        "true_positive": true_positive,
        "false_negative": false_negative,
        "false_positive": false_positive,
        "true_negative": true_negative,
        "sensitivity": sensitivity,
        "sensitivity_wilson_95": wilson_interval(
            true_positive,
            positives,
        ),
        "specificity": specificity,
        "specificity_wilson_95": wilson_interval(
            true_negative,
            negatives,
        ),
        "false_positive_rate": _divide(false_positive, negatives),
        "false_negative_rate": _divide(false_negative, positives),
        "precision": precision,
        "precision_wilson_95": wilson_interval(
            true_positive,
            true_positive + false_positive,
        ),
        "negative_predictive_value": negative_predictive_value,
        "f1": f1,
        "accuracy": accuracy,
        "balanced_accuracy": balanced_accuracy,
    }


def score_candidate(
    *,
    candidate_id: str,
    weights_sha256: str,
    production_eligible: bool,
    raw_predictions: Sequence[RawPrediction],
    labels: Mapping[str, bool],
) -> dict[str, Any]:
    by_id = {prediction.item_id: prediction for prediction in raw_predictions}
    if len(by_id) != len(raw_predictions) or not set(labels).issubset(by_id):
        raise ValueError(
            f"Vorhersageumfang von {candidate_id} passt nicht zur Review."
        )
    technical_errors = [
        {
            "image_id": prediction.item_id,
            "reason": prediction.technical_error,
        }
        for prediction in sorted(raw_predictions, key=lambda item: item.item_id)
        if prediction.item_id in labels
        if prediction.technical_error is not None
        or prediction.predicted_positive is None
    ]
    excluded_technical_errors = [
        {
            "image_id": prediction.item_id,
            "reason": prediction.technical_error,
        }
        for prediction in sorted(raw_predictions, key=lambda item: item.item_id)
        if prediction.item_id not in labels
        if prediction.technical_error is not None
        or prediction.predicted_positive is None
    ]
    if technical_errors:
        return {
            "candidate_id": candidate_id,
            "weights_sha256": weights_sha256,
            "production_eligible": production_eligible,
            "evaluation_status": "incomplete",
            "technical_error_count": len(technical_errors),
            "technical_errors": technical_errors,
            "excluded_technical_error_count": len(
                excluded_technical_errors
            ),
            "excluded_technical_errors": excluded_technical_errors,
            "metrics": None,
            "items": [],
        }

    scored = [
        ScoredPrediction(
            item_id=item_id,
            expected_positive=labels[item_id],
            predicted_positive=bool(by_id[item_id].predicted_positive),
            detection_count=by_id[item_id].detection_count,
            max_confidence=by_id[item_id].max_confidence,
        )
        for item_id in sorted(labels)
    ]
    return {
        "candidate_id": candidate_id,
        "weights_sha256": weights_sha256,
        "production_eligible": production_eligible,
        "evaluation_status": "complete",
        "technical_error_count": 0,
        "technical_errors": [],
        "excluded_technical_error_count": len(excluded_technical_errors),
        "excluded_technical_errors": excluded_technical_errors,
        "metrics": compute_binary_metrics(scored),
        "items": [
            {
                "image_id": item.item_id,
                "expected_positive": item.expected_positive,
                "predicted_positive": item.predicted_positive,
                "outcome": (
                    "true_positive"
                    if item.expected_positive and item.predicted_positive
                    else "false_negative"
                    if item.expected_positive
                    else "false_positive"
                    if item.predicted_positive
                    else "true_negative"
                ),
                "bcc_detection_count": item.detection_count,
                "max_bcc_confidence": item.max_confidence,
            }
            for item in scored
        ],
    }


def rank_candidates(candidate_results: Sequence[Mapping[str, Any]]) -> list[str]:
    eligible = [
        candidate
        for candidate in candidate_results
        if candidate.get("production_eligible") is True
        and candidate.get("evaluation_status") == "complete"
        and isinstance(candidate.get("metrics"), dict)
    ]
    eligible.sort(
        key=lambda candidate: (
            -float(candidate["metrics"]["balanced_accuracy"]),
            -float(candidate["metrics"]["specificity"]),
            -float(candidate["metrics"]["sensitivity"]),
            str(candidate["candidate_id"]),
        )
    )
    return [str(candidate["candidate_id"]) for candidate in eligible]


def pareto_front(candidate_results: Sequence[Mapping[str, Any]]) -> list[str]:
    eligible = [
        candidate
        for candidate in candidate_results
        if candidate.get("production_eligible") is True
        and candidate.get("evaluation_status") == "complete"
        and isinstance(candidate.get("metrics"), dict)
    ]
    front: list[str] = []
    for candidate in eligible:
        metrics = candidate["metrics"]
        fp = int(metrics["false_positive"])
        fn = int(metrics["false_negative"])
        dominated = any(
            other is not candidate
            and int(other["metrics"]["false_positive"]) <= fp
            and int(other["metrics"]["false_negative"]) <= fn
            and (
                int(other["metrics"]["false_positive"]) < fp
                or int(other["metrics"]["false_negative"]) < fn
            )
            for other in eligible
        )
        if not dominated:
            front.append(str(candidate["candidate_id"]))
    return sorted(front)


def build_report(
    *,
    holdout_id: str,
    holdout_manifest_sha256: str,
    holdout_candidates_sha256: str,
    review_sha256: str,
    candidate_scope_sha256: str,
    device: str,
    prediction_receipt_sha256: str,
    prediction_ledger_sha256: str,
    frame_quality_settings: Mapping[str, Any],
    candidate_results: Sequence[Mapping[str, Any]],
    positive_images: int,
    negative_images: int,
    excluded_images: int,
    created_utc: str,
    runtime_versions: Mapping[str, str],
) -> dict[str, Any]:
    if not candidate_results:
        raise ValueError("Ein leerer Kandidatenvergleich ist ungueltig.")
    all_complete = all(
        candidate.get("evaluation_status") == "complete"
        for candidate in candidate_results
    )
    ranking = rank_candidates(candidate_results)
    pareto = pareto_front(candidate_results)
    assessment_status = (
        "comparison_complete_not_release_qualified"
        if all_complete
        else "comparison_incomplete"
    )
    return {
        "schema_version": SCHEMA_VERSION,
        "purpose": EVALUATION_PURPOSE,
        "created_utc": created_utc,
        "warning": (
            "DIESER HOLDOUT DIENT NUR DEM KANDIDATENVERGLEICH. "
            "ER DARF NICHT FUER TRAINING ODER AUTOMATISCHE AKTIVIERUNG "
            "VERWENDET WERDEN."
        ),
        "bindings": {
            "holdout_id": holdout_id,
            "manifest_sha256": holdout_manifest_sha256,
            "candidates_sha256": holdout_candidates_sha256,
            "review_sha256": review_sha256,
            "candidate_scope_sha256": candidate_scope_sha256,
            "prediction_receipt_sha256": prediction_receipt_sha256,
            "prediction_ledger_sha256": prediction_ledger_sha256,
        },
        "protocol": {
            "class_id": TARGET_CLASS_ID,
            "class_name": TARGET_CLASS_NAME,
            "confidence_threshold": CONFIDENCE_THRESHOLD,
            "image_size": IMAGE_SIZE,
            "threshold_sweep": False,
            "positive_rule": "mindestens_eine_bcc_box",
            "technical_errors_count_as_negative": False,
            "ranking_metric": "balanced_accuracy",
            "ranking_tiebreakers": ["specificity", "sensitivity", "candidate_id"],
            "device": device,
            "frame_quality_gate": dict(frame_quality_settings),
        },
        "runtime": dict(runtime_versions),
        "dataset": {
            "total_holdout_images": (
                positive_images + negative_images + excluded_images
            ),
            "scored_images": positive_images + negative_images,
            "positive_images": positive_images,
            "negative_images": negative_images,
            "excluded_images": excluded_images,
            "one_image_per_holding": True,
            "localization_measured": False,
        },
        "candidates": list(candidate_results),
        "comparison": {
            "balanced_accuracy_ranking": ranking,
            "pareto_front_by_false_positives_and_false_negatives": pareto,
            "observed_front_runner": pareto[0] if len(pareto) == 1 else None,
        },
        "release_assessment": {
            "status": assessment_status,
            "auto_activation_allowed": False,
            "numeric_release_gate_defined": False,
            "fresh_confirmation_holdout_required": True,
            "reason": (
                "Die Auswahl aus vier bereits verglichenen Kandidaten braucht "
                "vor einer Aktivierung einen frischen Bestaetigungsbestand."
            ),
        },
        "limitations": [
            "Keine menschlichen Boxen: Lokalisation, Box-IoU und mAP sind nicht gemessen.",
            "Ein aus mehreren Kandidaten ausgewaehlter Spitzenreiter ist auf diesem Holdout optimistisch.",
            "Nicht protokollierte fruehere manuelle Modelltests sind rueckwirkend nicht vollstaendig beweisbar.",
        ],
    }


def _assert_safe_new_target(target: Path) -> None:
    absolute = Path(os.path.abspath(target))
    parent = absolute.parent
    if not parent.is_dir():
        raise ValueError(f"Berichtsordner fehlt: {parent}")
    resolved_parent = Path(os.path.realpath(parent))
    if os.path.normcase(str(parent)) != os.path.normcase(str(resolved_parent)):
        raise ValueError("Berichtsordner enthaelt eine Verknuepfung.")
    if holdout_tools._is_reparse_point(parent):
        raise ValueError("Berichtsordner ist eine Verknuepfung.")
    if absolute.exists() or absolute.is_symlink():
        raise FileExistsError(f"Bericht existiert bereits: {absolute}")


def atomic_write_json_new(target: Path, payload: Mapping[str, Any]) -> None:
    """Schreibt im Zielordner atomar und ueberschreibt nie bewusst."""

    target = Path(os.path.abspath(target))
    _assert_safe_new_target(target)
    temporary = target.parent / f".{target.name}.{uuid.uuid4().hex}.tmp"
    data = (
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=False)
        + "\n"
    ).encode("utf-8")
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
    repository_root = Path(__file__).resolve().parents[2]
    for name, relative in (
        (
            "bcc_test_wrapper_sha256",
            Path("sidecar/sidecar/models/bcc_test_wrapper.py"),
        ),
        (
            "yolo_wrapper_sha256",
            Path("sidecar/sidecar/models/yolo_wrapper.py"),
        ),
    ):
        path = repository_root / relative
        if path.is_file():
            versions[name] = sha256_file(path)
    return versions


def _verify_inputs_unchanged(
    original: EvaluationContext,
    knowledge_root: Path,
    base_model_path: Path,
) -> None:
    current = _load_context(
        knowledge_root,
        base_model_path,
        original.holdout_root,
        original.review_path,
    )
    fields = (
        "holdout_id",
        "holdout_manifest_sha256",
        "holdout_candidates_sha256",
        "review_sha256",
        "candidate_scope_sha256",
        "positive_images",
        "negative_images",
        "excluded_images",
        "review_labels",
        "excluded_item_ids",
    )
    if any(getattr(original, field) != getattr(current, field) for field in fields):
        raise ValueError(
            "Holdout oder Review wurde waehrend der Auswertung veraendert."
        )
    original_cases = [
        (case.item_id, case.image_sha256) for case in original.cases
    ]
    current_cases = [
        (case.item_id, case.image_sha256) for case in current.cases
    ]
    if original_cases != current_cases:
        raise ValueError(
            "Die Holdout-Bilder wurden waehrend der Auswertung veraendert."
        )


def _parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Vier eingefrorene BCC-Kandidaten mit festem Protokoll vergleichen."
        )
    )
    parser.add_argument(
        "--knowledge-root",
        type=Path,
        default=Path(r"C:\KI_BRAIN"),
    )
    parser.add_argument(
        "--base-model",
        type=Path,
        default=holdout_tools._default_base_model(),
    )
    parser.add_argument("--holdout", type=Path, required=True)
    parser.add_argument("--review", type=Path, required=True)
    parser.add_argument(
        "--device",
        default=None,
        help="Optional: cpu oder cuda[:N]. Ohne Angabe gilt die Sidecar-Einstellung.",
    )
    args = parser.parse_args(argv)
    if args.device is not None and re.fullmatch(
        r"(?:cpu|cuda(?::\d+)?)",
        args.device,
    ) is None:
        parser.error("--device muss cpu oder cuda[:N] sein.")
    return args


def _print_candidate_result(result: Mapping[str, Any]) -> None:
    candidate_id = result["candidate_id"]
    if result["evaluation_status"] != "complete":
        print(
            f"{candidate_id}: UNVOLLSTAENDIG "
            f"({result['technical_error_count']} technische Fehler)"
        )
        return
    metrics = result["metrics"]
    role = (
        "Freigabekandidat"
        if result["production_eligible"]
        else "nur Diagnose"
    )
    print(
        f"{candidate_id} ({role}): "
        f"TP {metrics['true_positive']}, FN {metrics['false_negative']}, "
        f"TN {metrics['true_negative']}, FP {metrics['false_positive']}, "
        f"Recall {metrics['sensitivity']:.1%}, "
        f"Spezifitaet {metrics['specificity']:.1%}, "
        f"Balanced Accuracy {metrics['balanced_accuracy']:.1%}"
    )


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv if argv is not None else sys.argv[1:])
    try:
        context = _load_context(
            args.knowledge_root,
            args.base_model,
            args.holdout,
            args.review,
        )
        bindings = load_candidate_bindings(
            args.knowledge_root,
            context.frozen_candidate_scope,
        )
        snapshots = load_image_snapshots(context.cases)
        _, yolo_wrapper = _load_runtime_modules()
        _assert_sidecar_offline(yolo_wrapper)
        frame_quality_settings = _runtime_protocol_settings(yolo_wrapper)
        print(
            f"Holdout bereit: {len(snapshots)} Bilder "
            f"({context.positive_images} positiv / "
            f"{context.negative_images} negativ), "
            f"{len(bindings)} eingefrorene Kandidaten."
        )
        print(
            f"Festes Protokoll: conf={CONFIDENCE_THRESHOLD}, "
            f"imgsz={IMAGE_SIZE}, Klasse {TARGET_CLASS_ID} {TARGET_CLASS_NAME}."
        )

        predictions_by_candidate: dict[str, list[RawPrediction]] = {}
        devices_by_candidate: dict[str, str] = {}
        for index, binding in enumerate(bindings, start=1):
            print(
                f"[{index}/{len(bindings)}] {binding.candidate_id} "
                f"({binding.comparison_role}) ..."
            )
            predictions, actual_device = run_candidate_inference(
                binding,
                snapshots,
                device=args.device,
            )
            devices_by_candidate[binding.candidate_id] = actual_device
            predictions_by_candidate[binding.candidate_id] = predictions
            errors = sum(
                prediction.technical_error is not None
                for prediction in predictions
            )
            positives = sum(
                prediction.predicted_positive is True
                for prediction in predictions
            )
            print(
                f"  Vorhersagen: {positives} positiv, "
                f"{errors} technische Fehler."
            )

        validate_prediction_matrix(
            bindings,
            snapshots,
            predictions_by_candidate,
        )
        if len(set(devices_by_candidate.values())) != 1:
            raise ValueError(
                "Die Kandidaten liefen nicht auf demselben Inferenzgeraet."
            )
        selected_device = next(iter(devices_by_candidate.values()))
        created = datetime.now(timezone.utc)
        created_text = created.isoformat().replace("+00:00", "Z")
        runtime = _runtime_versions()
        ledger = build_prediction_ledger(
            context,
            bindings,
            snapshots,
            predictions_by_candidate,
            devices_by_candidate=devices_by_candidate,
            frame_quality_settings=frame_quality_settings,
            created_utc=created_text,
            runtime_versions=runtime,
        )
        reports_root = Path(os.path.abspath(args.knowledge_root)) / "training" / "reports"
        reports_root = holdout_tools._safe_existing_path(
            reports_root,
            Path(os.path.abspath(args.knowledge_root)),
            expect_file=False,
        )
        stamp = created.strftime("%Y%m%d_%H%M%S_%f")
        run_name = f"{context.holdout_id[:12]}_{stamp}"
        ledger_path = reports_root / (
            f"bcc_release_holdout_predictions_{run_name}.json"
        )
        atomic_write_json_new(ledger_path, ledger)
        ledger_file_sha = sha256_file(ledger_path)
        print(f"Labelblinder Vorhersagebeleg: {ledger_path}")
        print(f"Vorhersagebeleg SHA-256: {ledger_file_sha}")
        sealed_predictions, sealed_device = load_prediction_ledger(
            ledger_path,
            ledger_file_sha,
            context,
            bindings,
            snapshots,
        )
        if sealed_device != selected_device:
            raise ValueError("Geraetebindung im Vorhersagebeleg stimmt nicht.")

        _verify_inputs_unchanged(
            context,
            args.knowledge_root,
            args.base_model,
        )
        rebound = load_candidate_bindings(
            args.knowledge_root,
            context.frozen_candidate_scope,
        )
        if [
            (
                item.candidate_id,
                item.candidate_manifest_sha256,
                item.weights_sha256,
                item.production_manifest_eligible,
                item.production_manifest_reason,
                item.diagnostic_marker_present,
                item.diagnostic_marker_sha256,
            )
            for item in rebound
        ] != [
            (
                item.candidate_id,
                item.candidate_manifest_sha256,
                item.weights_sha256,
                item.production_manifest_eligible,
                item.production_manifest_reason,
                item.diagnostic_marker_present,
                item.diagnostic_marker_sha256,
            )
            for item in bindings
        ]:
            raise ValueError(
                "Kandidaten wurden waehrend der Auswertung veraendert."
            )

        labels = load_review_labels(context)
        results: list[dict[str, Any]] = []
        for binding in bindings:
            scored = score_candidate(
                candidate_id=binding.candidate_id,
                weights_sha256=binding.weights_sha256,
                production_eligible=binding.production_eligible,
                raw_predictions=sealed_predictions[binding.candidate_id],
                labels=labels,
            )
            scored.update(
                {
                    "candidate_manifest_sha256": (
                        binding.candidate_manifest_sha256
                    ),
                    "comparison_role": binding.comparison_role,
                    "production_manifest_reason": (
                        binding.production_manifest_reason
                    ),
                    "diagnostic_marker_present": (
                        binding.diagnostic_marker_present
                    ),
                    "diagnostic_marker_sha256": (
                        binding.diagnostic_marker_sha256
                    ),
                    "sidecar_manifest_eligible": (
                        binding.production_manifest_eligible
                    ),
                    "selection_eligible": binding.production_eligible,
                    "device": devices_by_candidate[binding.candidate_id],
                }
            )
            results.append(scored)

        for result in results:
            _print_candidate_result(result)
        if any(
            result["evaluation_status"] != "complete" for result in results
        ):
            print(
                "Ergebnisstatus: comparison_incomplete; "
                "kein endgueltiger Auswertungsbericht geschrieben."
            )
            print("Kein Modell wurde trainiert, aktiviert oder ersetzt.")
            return 2
        if (
            sha256_file(context.review_path) != context.review_sha256
            or sha256_file(ledger_path) != ledger_file_sha
        ):
            raise ValueError(
                "Review oder Vorhersagebeleg wurde vor dem "
                "Berichtsschreiben veraendert."
            )

        report = build_report(
            holdout_id=context.holdout_id,
            holdout_manifest_sha256=context.holdout_manifest_sha256,
            holdout_candidates_sha256=context.holdout_candidates_sha256,
            review_sha256=context.review_sha256,
            candidate_scope_sha256=context.candidate_scope_sha256,
            device=selected_device,
            prediction_receipt_sha256=ledger[
                "prediction_receipt_sha256"
            ],
            prediction_ledger_sha256=ledger_file_sha,
            frame_quality_settings=frame_quality_settings,
            candidate_results=results,
            positive_images=context.positive_images,
            negative_images=context.negative_images,
            excluded_images=context.excluded_images,
            created_utc=created_text,
            runtime_versions=runtime,
        )
        report_path = reports_root / (
            f"bcc_release_holdout_evaluation_{run_name}.json"
        )
        atomic_write_json_new(report_path, report)

        print(
            "Ergebnisstatus: "
            f"{report['release_assessment']['status']}"
        )
        print("Kein Modell wurde trainiert, aktiviert oder ersetzt.")
        print(f"Auswertungsbericht: {report_path}")
        return 0
    except (OSError, ValueError, FileExistsError, RuntimeError) as error:
        print(f"FEHLER: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
