#!/usr/bin/env python3
"""Diagnostiziert einen Detect-Kandidaten auf einem reviewten Release-Holdout.

Der Lauf ist absichtlich keine Modellfreigabe. Zuerst werden auf allen
eingefrorenen Bildern labelblind Vorhersagen erzeugt und hashgebunden
gespeichert. Erst nach dem erneuten Einlesen dieses Belegs wird die getrennte
menschliche Review geladen und mit festem IoU=0,5 ausgewertet.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, Sequence


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[1]
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

import detect_gold_holdout_scoring as scoring
import detect_release_holdout_status as status_tools
import evaluate_detect_gold_holdout as inference_tools
import prepare_detect_release_holdout as holdout_tools
from tools.EvalVisibilityReview import (
    detect_release_holdout_review_server as review_server,
)


SCHEMA_VERSION = "1.0"
PREDICTION_PURPOSE = "detect_release_holdout_diagnostic_predictions"
EVALUATION_PURPOSE = "detect_release_holdout_diagnostic_evaluation"
EVALUATION_STATUS = "diagnostic_only_coverage_incomplete"
DEVICE_PATTERN = re.compile(r"(?:cpu|cuda(?::\d+)?)")
ACTIVE_CLASS_MAP_PATH = (
    PROJECT_ROOT / "training" / "class_maps" / "detect_class_map_v3.json"
)
ACTIVE_VSA_MANIFEST_PATH = (
    PROJECT_ROOT
    / "src"
    / "AuswertungPro.Next.UI"
    / "Data"
    / "vsa_kek_2020_catalog_manifest.json"
)


@dataclass(frozen=True)
class EvaluationContext:
    knowledge_root: Path
    holdout_root: Path
    candidate_root: Path
    candidate_id: str
    candidate_manifest_path: Path
    candidate_manifest_sha256: str
    weights_path: Path
    weights_sha256: str
    base_model_path: Path
    base_model_sha256: str
    holdout_id: str
    manifest_sha256: str
    candidates_sha256: str
    class_map_version: int
    class_map_sha256: str
    vsa_manifest_sha256: str
    classes: tuple[str, ...]
    images: tuple[review_server.VerifiedImage, ...]
    development_comparison: bool = False
    reference_binding: dict | None = None

    def bindings(self) -> dict[str, object]:
        return {
            "holdout_id": self.holdout_id,
            "manifest_sha256": self.manifest_sha256,
            "candidates_sha256": self.candidates_sha256,
            "candidate_id": self.candidate_id,
            "candidate_manifest_sha256": self.candidate_manifest_sha256,
            "candidate_weights_sha256": self.weights_sha256,
            "base_model_sha256": self.base_model_sha256,
            "class_map_version": self.class_map_version,
            "class_map_sha256": self.class_map_sha256,
            "vsa_manifest_hash": self.vsa_manifest_sha256,
            "vsa_manifest_sha256": self.vsa_manifest_sha256,
        }


@dataclass(frozen=True)
class ScoringSelection:
    truths: tuple[scoring.GroundTruth, ...]
    predictions: tuple[scoring.Prediction, ...]
    positive_image_ids: frozenset[str]
    negative_image_ids: frozenset[str]
    excluded_image_ids: frozenset[str]
    excluded_technical_errors: tuple[dict[str, str], ...]


def sha256_file(path: Path) -> str:
    return inference_tools.sha256_file(path)


def _same_path(left: Path, right: Path) -> bool:
    return os.path.normcase(os.path.abspath(left)) == os.path.normcase(
        os.path.abspath(right)
    )


def _is_path_below(path: Path, root: Path) -> bool:
    try:
        Path(os.path.abspath(path)).relative_to(Path(os.path.abspath(root)))
        return True
    except ValueError:
        return False


def _require_direct_candidate(candidate: Path, knowledge_root: Path) -> Path:
    candidates_root = Path(os.path.abspath(knowledge_root)) / "training" / "models" / "candidates"
    absolute = Path(os.path.abspath(candidate))
    expected = candidates_root / absolute.name
    if (
        not candidates_root.is_dir()
        or not absolute.is_dir()
        or absolute.name in {"", ".", ".."}
        or not _same_path(absolute, expected)
        or os.path.normcase(os.path.realpath(absolute))
        != os.path.normcase(str(absolute))
    ):
        raise ValueError(
            "Der Kandidat muss ein sicherer direkter Unterordner von "
            "knowledge/training/models/candidates sein."
        )
    return absolute


def load_context(
    knowledge_root: Path,
    holdout_root: Path,
    candidate_root: Path,
    development_comparison: bool = False,
) -> EvaluationContext:
    knowledge = Path(os.path.abspath(knowledge_root))
    holdout = Path(os.path.abspath(holdout_root))
    candidate = _require_direct_candidate(candidate_root, knowledge)
    snapshot = review_server._validate_holdout(holdout)
    binding = holdout_tools.validate_candidate(candidate, ACTIVE_CLASS_MAP_PATH)

    expected_binding = {
        "candidate_id": snapshot.candidate_id,
        "manifest_sha256": snapshot.candidate_manifest_sha256,
        "weights_sha256": snapshot.candidate_weights_sha256,
        "class_map_version": snapshot.class_map_version,
        "class_map_sha256": snapshot.class_map_sha256,
        "vsa_manifest_hash": snapshot.vsa_manifest_hash,
    }
    actual_binding = {
        "candidate_id": binding.candidate_id,
        "manifest_sha256": binding.manifest_sha256,
        "weights_sha256": binding.weights_sha256,
        "class_map_version": binding.class_map_version,
        "class_map_sha256": binding.class_map_sha256,
        "vsa_manifest_hash": binding.vsa_manifest_hash,
    }
    if actual_binding != expected_binding:
        if not development_comparison:
            raise ValueError("Kandidat und eingefrorener Holdout sind nicht exakt gebunden.")
        # Entwicklungsvergleich: nur ein ausdruecklich nicht freigegebener
        # Kandidat darf gegen einen fremd gebundenen Holdout gemessen werden.
        candidate_manifest_doc = json.loads(
            (candidate / "candidate_manifest.json").read_bytes().decode("utf-8-sig")
        )
        if str(candidate_manifest_doc.get("candidate_status")) != "not_deployed":
            raise ValueError(
                "Entwicklungsvergleich verlangt einen Kandidaten mit Status not_deployed."
            )
    base_models_root = PROJECT_ROOT / "sidecar" / "models"
    if not _is_path_below(binding.base_model_path, base_models_root):
        raise ValueError("Das gebundene Basismodell liegt ausserhalb sidecar/models.")

    vsa_manifest_path = holdout_tools._safe_file(
        ACTIVE_VSA_MANIFEST_PATH,
        PROJECT_ROOT,
        "Aktives VSA-Manifest",
    )
    vsa_sha256 = sha256_file(vsa_manifest_path)
    if vsa_sha256 != snapshot.vsa_manifest_sha256:
        raise ValueError("Das aktive VSA-Manifest stimmt nicht mit dem Holdout ueberein.")

    candidate_manifest = candidate / "candidate_manifest.json"
    weights_path = candidate / "best.pt"
    if actual_binding != expected_binding and development_comparison:
        # Entwicklungsvergleich: Integritaet des Kandidaten gegen sein eigenes
        # Manifest (validate_candidate hat die Gewichtsbindung bereits geprueft).
        if (
            sha256_file(candidate_manifest) != binding.manifest_sha256
            or sha256_file(weights_path) != binding.weights_sha256
        ):
            raise ValueError("Kandidatenmanifest oder Gewicht wurde veraendert.")
    elif (
        sha256_file(candidate_manifest) != snapshot.candidate_manifest_sha256
        or sha256_file(weights_path) != snapshot.candidate_weights_sha256
    ):
        raise ValueError("Kandidatenmanifest oder Gewicht wurde veraendert.")
    classes = tuple(item.name for item in snapshot.classes)
    if classes != tuple(name for _, name, _ in holdout_tools.CLASS_LABELS):
        raise ValueError("Holdout und feste Klassenkarte besitzen andere Klassen.")

    return EvaluationContext(
        knowledge_root=knowledge,
        holdout_root=holdout,
        candidate_root=candidate,
        candidate_id=binding.candidate_id,
        candidate_manifest_path=candidate_manifest,
        candidate_manifest_sha256=binding.manifest_sha256,
        weights_path=weights_path,
        weights_sha256=binding.weights_sha256,
        base_model_path=binding.base_model_path,
        base_model_sha256=binding.base_model_sha256,
        holdout_id=snapshot.holdout_id,
        manifest_sha256=snapshot.manifest_sha256,
        candidates_sha256=snapshot.candidates_sha256,
        class_map_version=snapshot.class_map_version,
        class_map_sha256=snapshot.class_map_sha256,
        vsa_manifest_sha256=snapshot.vsa_manifest_sha256,
        classes=classes,
        images=snapshot.images,
        development_comparison=development_comparison and actual_binding != expected_binding,
        reference_binding=(
            expected_binding if development_comparison and actual_binding != expected_binding
            else None
        ),
    )


def load_image_snapshots(
    context: EvaluationContext,
) -> tuple[inference_tools.ImageSnapshot, ...]:
    snapshots: list[inference_tools.ImageSnapshot] = []
    seen: set[str] = set()
    for image in context.images:
        if image.candidate_id in seen:
            raise ValueError("Der Holdout enthaelt doppelte Bild-IDs.")
        seen.add(image.candidate_id)
        image_bytes = image.path.read_bytes()
        if (
            len(image_bytes) != image.size_bytes
            or hashlib.sha256(image_bytes).hexdigest() != image.sha256
        ):
            raise ValueError(f"Bildbytes von {image.candidate_id} wurden veraendert.")
        snapshots.append(
            inference_tools.ImageSnapshot(
                image_id=image.candidate_id,
                image_sha256=image.sha256,
                image_bytes=image_bytes,
            )
        )
    if not snapshots:
        raise ValueError("Der Detect-Release-Holdout ist leer.")
    return tuple(sorted(snapshots, key=lambda item: item.image_id))


def load_completed_review(
    context: EvaluationContext,
    review_path: Path,
) -> tuple[dict[str, Any], dict[str, Any], bytes]:
    status = status_tools.evaluate_holdout_status(context.holdout_root, review_path)
    if status.get("total") != len(context.images) or status.get("open") != 0:
        raise ValueError("Die Detect-Release-Review ist noch nicht vollstaendig.")
    if status.get("bindings", {}).get("manifest_sha256") != context.manifest_sha256:
        raise ValueError("Status und eingefrorener Holdout sind nicht gebunden.")

    snapshot = review_server._validate_holdout(context.holdout_root)
    review, review_bytes = status_tools._load_bound_review(
        context.holdout_root,
        Path(os.path.abspath(review_path)),
        snapshot,
    )
    _validate_status_review_binding(status, review_bytes)
    image_ids = {image.candidate_id for image in context.images}
    if set(review["decisions"]) != image_ids:
        raise ValueError("Die Review braucht genau eine Entscheidung je Holdout-Bild.")
    return status, review, review_bytes


def _validate_status_review_binding(
    status: Mapping[str, Any],
    review_bytes: bytes,
) -> None:
    bindings = status.get("bindings")
    expected_sha256 = (
        bindings.get("review_sha256") if isinstance(bindings, Mapping) else None
    )
    actual_sha256 = hashlib.sha256(review_bytes).hexdigest()
    if expected_sha256 != actual_sha256:
        raise ValueError(
            "Die Review wurde zwischen Statuspruefung und Laden veraendert."
        )


def build_scoring_selection(
    context: EvaluationContext,
    decisions: Mapping[str, Mapping[str, Any]],
    image_predictions: Sequence[inference_tools.ImagePrediction],
) -> ScoringSelection:
    image_ids = {image.candidate_id for image in context.images}
    if set(decisions) != image_ids:
        raise ValueError("Review und Holdout besitzen verschiedene Bildmengen.")
    by_id = {item.image_id: item for item in image_predictions}
    if len(by_id) != len(image_predictions) or set(by_id) != image_ids:
        raise ValueError("Vorhersagebeleg und Holdout besitzen verschiedene Bildmengen.")

    positive_ids = frozenset(
        image_id
        for image_id, decision in decisions.items()
        if decision.get("decision") == "positive"
    )
    negative_ids = frozenset(
        image_id
        for image_id, decision in decisions.items()
        if decision.get("decision") == "negative"
    )
    excluded_ids = frozenset(
        image_id
        for image_id, decision in decisions.items()
        if decision.get("decision") == "exclude"
    )
    if positive_ids | negative_ids | excluded_ids != image_ids:
        raise ValueError("Review enthaelt eine unbekannte Entscheidung.")

    included_errors = [
        {"image_id": image_id, "reason": str(by_id[image_id].technical_error)}
        for image_id in sorted(positive_ids | negative_ids)
        if by_id[image_id].technical_error is not None
    ]
    if included_errors:
        raise ValueError(
            f"{len(included_errors)} technische Fehler auf gewerteten Bildern; "
            "sie duerfen nicht als Negativtreffer zaehlen."
        )
    excluded_errors = tuple(
        {"image_id": image_id, "reason": str(by_id[image_id].technical_error)}
        for image_id in sorted(excluded_ids)
        if by_id[image_id].technical_error is not None
    )

    truths = tuple(
        scoring.GroundTruth(
            image_id=image_id,
            sample_id=f"{image_id}:{annotation['id']}",
            class_id=int(annotation["class_id"]),
            class_name=str(annotation["class_name"]),
            box=scoring.Box(
                float(annotation["box"]["x_center"]),
                float(annotation["box"]["y_center"]),
                float(annotation["box"]["width"]),
                float(annotation["box"]["height"]),
            ),
        )
        for image_id in sorted(positive_ids)
        for annotation in decisions[image_id]["annotations"]
    )
    if not truths:
        raise ValueError("Die Review enthaelt keine positive Ground-Truth-Box.")

    predictions = tuple(
        scoring.Prediction(
            image_id=image_id,
            prediction_id=detection.prediction_id,
            class_id=detection.class_id,
            class_name=detection.class_name,
            confidence=detection.confidence,
            box=detection.box,
        )
        for image_id in sorted(positive_ids | negative_ids)
        for detection in by_id[image_id].detections
    )
    return ScoringSelection(
        truths=truths,
        predictions=predictions,
        positive_image_ids=positive_ids,
        negative_image_ids=negative_ids,
        excluded_image_ids=excluded_ids,
        excluded_technical_errors=excluded_errors,
    )


def compute_negative_false_alarm_metrics(
    negative_image_ids: Sequence[str] | frozenset[str],
    predictions: Sequence[scoring.Prediction],
    classes: Sequence[str],
) -> dict[str, Any]:
    negatives = frozenset(negative_image_ids)
    detections = [item for item in predictions if item.image_id in negatives]
    alarm_ids = sorted({item.image_id for item in detections})
    total = len(negatives)
    alarm_count = len(alarm_ids)
    true_negative_count = total - alarm_count
    by_class = Counter(item.class_name for item in detections)
    return {
        "measured": total > 0,
        "negative_images": total,
        "true_negative_images": true_negative_count,
        "false_alarm_images": alarm_count,
        "false_alarm_image_ids": alarm_ids,
        "image_false_alarm_rate": alarm_count / total if total else None,
        "image_specificity": true_negative_count / total if total else None,
        "false_alarm_detections": len(detections),
        "false_alarm_detections_per_negative_image": (
            len(detections) / total if total else None
        ),
        "detections_by_class": {
            class_name: by_class.get(class_name, 0) for class_name in classes
        },
    }


def build_report(
    context: EvaluationContext,
    status: Mapping[str, Any],
    metrics: Mapping[str, Any],
    negative_metrics: Mapping[str, Any],
    selection: ScoringSelection,
    *,
    review_sha256: str,
    ledger_sha256: str,
    prediction_receipt_sha256: str,
    created_utc: str,
    protocol: Mapping[str, Any],
    runtime_versions: Mapping[str, str],
) -> dict[str, Any]:
    dataset_status = str(status["dataset_status"])
    evaluation_status = (
        EVALUATION_STATUS
        if dataset_status == "coverage_incomplete"
        else f"diagnostic_only_{dataset_status}"
    )
    release_reason = (
        "Die menschliche Review ist abgeschlossen, aber die festgelegte "
        "Klassen- und Negativabdeckung ist noch unvollstaendig."
        if dataset_status == "coverage_incomplete"
        else (
            "Dieser Diagnoseweg trifft auch bei ausreichender Datensatzabdeckung "
            "keine Release- oder Aktivierungsentscheidung."
        )
    )
    return {
        "schema_version": SCHEMA_VERSION,
        "purpose": EVALUATION_PURPOSE,
        "created_utc": created_utc,
        "warning": (
            "DIAGNOSE AUF UNVOLLSTAENDIG ABGEDECKTEM HOLDOUT. KEINE "
            "MODELLFREIGABE, KEIN TRAINING UND KEINE AKTIVIERUNG."
        ),
        "status": evaluation_status,
        "evaluation_role": "diagnostic_only",
        "development_comparison": (
            {
                "enabled": True,
                "note": "Entwicklungsvergleich mit einem nicht zum Holdout gehoerenden "
                        "Kandidaten (Status not_deployed); keine Abnahme.",
                "reference_binding": dict(context.reference_binding or {}),
            }
            if context.development_comparison
            else {"enabled": False}
        ),
        "bindings": {
            **context.bindings(),
            "review_sha256": review_sha256,
            "prediction_ledger_sha256": ledger_sha256,
            "prediction_receipt_sha256": prediction_receipt_sha256,
        },
        "protocol": dict(protocol),
        "runtime": dict(runtime_versions),
        "holdout": {
            "total_images": len(context.images),
            "evaluated_images": len(selection.positive_image_ids)
            + len(selection.negative_image_ids),
            "positive_images": len(selection.positive_image_ids),
            "negative_images": len(selection.negative_image_ids),
            "excluded_images": len(selection.excluded_image_ids),
            "ground_truth_instances": len(selection.truths),
            "excluded_technical_errors": list(selection.excluded_technical_errors),
            "dataset_status": dataset_status,
            "class_coverage": status["class_coverage"],
            "requirements": status["requirements"],
            "shortfalls": status["shortfalls"],
            "positive_physical_holdings": status["positive_physical_holdings"],
            "negative_physical_holdings": status["negative_physical_holdings"],
        },
        "metrics": {
            "object_detection": dict(metrics),
            "negative_false_alarms": dict(negative_metrics),
        },
        "release_assessment": {
            "status": "not_release_qualified",
            "release_qualified": False,
            "auto_activation_allowed": False,
            "model_activated": False,
            "model_pointer_changed": False,
            "reason": release_reason,
        },
        "limitations": [
            "Der Lauf verwendet eine feste Konfidenzschwelle; mAP und ein Schwellenlauf werden nicht berechnet.",
            "Klassen unter der Mindestabdeckung sind nur diagnostisch und nicht releasefaehig bewertet.",
            "Der Trainingsbestand des vortrainierten Basismodells ist nicht vollstaendig inventarisiert.",
        ],
    }


def _runtime_versions() -> dict[str, str]:
    runtime = inference_tools._runtime_versions()
    old_script_hash = runtime.pop("evaluation_script_sha256", None)
    if old_script_hash is not None:
        runtime["inference_script_sha256"] = old_script_hash
    runtime.update(
        {
            "diagnostic_script_sha256": sha256_file(Path(__file__).resolve()),
            "status_script_sha256": sha256_file(Path(status_tools.__file__).resolve()),
            "review_validation_script_sha256": sha256_file(
                Path(review_server.__file__).resolve()
            ),
        }
    )
    return runtime


def _assert_inputs_unchanged(
    context: EvaluationContext,
    review_path: Path,
    expected_review_bytes: bytes,
    ledger_path: Path,
    ledger_sha256: str,
) -> None:
    current = load_context(
        context.knowledge_root,
        context.holdout_root,
        context.candidate_root,
        development_comparison=context.development_comparison,
    )
    if current != context:
        raise ValueError("Kandidat oder Holdout wurde waehrend der Diagnose veraendert.")
    if review_server._read_limited(Path(os.path.abspath(review_path))) != expected_review_bytes:
        raise ValueError("Die Review wurde waehrend der Diagnose veraendert.")
    if sha256_file(ledger_path) != ledger_sha256:
        raise ValueError("Der Vorhersagebeleg wurde vor dem Bericht veraendert.")


def _parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Detect-Kandidat labelblind auf einem vollstaendig reviewten "
            "Mehrklassen-Holdout diagnostizieren."
        )
    )
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--holdout", type=Path, required=True)
    parser.add_argument("--review", type=Path, required=True)
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--device", default="cuda:0")
    parser.add_argument(
        "--development-comparison",
        action="store_true",
        help="Entwicklungsvergleich: misst auch einen nicht zum Holdout gehoerenden "
             "Kandidaten (nur Status not_deployed), niemals eine Abnahme.",
    )
    args = parser.parse_args(argv)
    if DEVICE_PATTERN.fullmatch(args.device) is None:
        parser.error("--device muss cpu oder cuda[:N] sein.")
    return args


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv if argv is not None else sys.argv[1:])
    try:
        context = load_context(
            args.knowledge_root,
            args.holdout,
            args.candidate,
            development_comparison=args.development_comparison,
        )
        snapshots = load_image_snapshots(context)
        print(
            f"Labelblinde Inferenz bereit: {len(snapshots)} eingefrorene Bilder, "
            f"Kandidat {context.candidate_id}."
        )
        print(
            f"Festes Protokoll: conf={inference_tools.CONFIDENCE_THRESHOLD}, "
            f"imgsz={inference_tools.IMAGE_SIZE}, "
            f"IoU={inference_tools.IOU_THRESHOLD}, Geraet={args.device}."
        )

        def show_progress(index: int, total: int) -> None:
            if index == 1 or index % 10 == 0 or index == total:
                print(f"Inferenz: {index}/{total} Bilder", flush=True)

        predictions, runtime_protocol = inference_tools.run_candidate_inference(
            context,
            snapshots,
            device=args.device,
            progress=show_progress,
        )
        inference_tools.validate_prediction_matrix(snapshots, predictions)
        created = datetime.now(timezone.utc)
        created_text = created.isoformat().replace("+00:00", "Z")
        runtime = _runtime_versions()
        ledger = inference_tools.build_prediction_ledger(
            context,
            snapshots,
            predictions,
            created_utc=created_text,
            runtime_protocol=runtime_protocol,
            runtime_versions=runtime,
            purpose=PREDICTION_PURPOSE,
        )
        reports_root = context.knowledge_root / "training" / "reports"
        if not reports_root.is_dir():
            raise ValueError(f"Berichtsordner fehlt: {reports_root}")
        stamp = created.strftime("%Y%m%d_%H%M%S_%f")
        run_name = f"{context.candidate_id}_{context.holdout_id}_{stamp}"
        ledger_path = reports_root / f"detect_release_predictions_{run_name}.json"
        inference_tools.atomic_write_json_new(ledger_path, ledger)
        ledger_sha256 = sha256_file(ledger_path)
        print(f"Labelblinder Vorhersagebeleg: {ledger_path}")
        print(f"Vorhersagebeleg SHA-256: {ledger_sha256}")

        sealed_predictions, sealed_protocol = inference_tools.load_prediction_ledger(
            ledger_path,
            ledger_sha256,
            context,
            snapshots,
            expected_purpose=PREDICTION_PURPOSE,
        )

        # Die Review wird absichtlich erst nach dem versiegelten Ledger geladen.
        status, review, review_bytes = load_completed_review(context, args.review)
        selection = build_scoring_selection(
            context,
            review["decisions"],
            sealed_predictions,
        )
        class_names = {index: name for index, name in enumerate(context.classes)}
        metrics = scoring.score_predictions(
            selection.truths,
            selection.predictions,
            class_names,
            iou_threshold=inference_tools.IOU_THRESHOLD,
        )
        negative_metrics = compute_negative_false_alarm_metrics(
            selection.negative_image_ids,
            selection.predictions,
            context.classes,
        )
        review_sha256 = hashlib.sha256(review_bytes).hexdigest()
        _assert_inputs_unchanged(
            context,
            args.review,
            review_bytes,
            ledger_path,
            ledger_sha256,
        )
        report = build_report(
            context,
            status,
            metrics,
            negative_metrics,
            selection,
            review_sha256=review_sha256,
            ledger_sha256=ledger_sha256,
            prediction_receipt_sha256=str(ledger["prediction_receipt_sha256"]),
            created_utc=created_text,
            protocol=sealed_protocol,
            runtime_versions=runtime,
        )
        report_path = reports_root / f"detect_release_diagnostic_{run_name}.json"
        inference_tools.atomic_write_json_new(report_path, report)

        micro = metrics["micro"]
        print(
            f"Objekt-Ergebnis: TP {micro['tp']}, FP {micro['fp']}, "
            f"FN {micro['fn']}, Precision {micro['precision']:.1%}, "
            f"Recall {micro['recall']:.1%}, F1 {micro['f1']:.1%}."
        )
        print(
            f"Negativbilder mit Fehlalarm: "
            f"{negative_metrics['false_alarm_images']}/"
            f"{negative_metrics['negative_images']}."
        )
        print(f"Status: {report['status']}; keine Modellfreigabe.")
        print("Kein Modell wurde trainiert, aktiviert oder ersetzt.")
        print(f"Diagnosebericht: {report_path}")
        return 0
    except (OSError, ValueError, FileExistsError, RuntimeError) as error:
        print(f"FEHLER: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
