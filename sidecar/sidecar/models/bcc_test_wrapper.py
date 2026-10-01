"""Getrennte Vorschau-Inferenz fuer einen nicht produktiven BCC-Kandidaten.

Der Client kann keinen Modellpfad vorgeben. Der Wrapper liest nur direkte
Unterordner des konfigurierten Kandidaten-Roots und prueft Manifest, Status,
Pilot, Mindestdatenmenge und SHA-256 der Gewichte. Das produktive YOLO-Modell
im Slot ``YOLO`` wird nicht ersetzt. Bei VRAM-Mangel darf der allgemeine
LRU-Manager es voruebergehend entladen; der produktive Artefaktzeiger bleibt
unveraendert und das Modell wird bei Bedarf wieder geladen.
"""

from __future__ import annotations

import hashlib
import json
import logging
import math
import os
import re
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path

import numpy as np

from .. import osd_meter
from ..config import settings
from ..gpu_manager import ModelSlot, ModelUnloadedError, gpu_manager
from ..schemas.detection import BccTestYoloResponse, YoloDetection
from . import osd_model_wrapper, yolo_wrapper, yolo_test_slot

logger = logging.getLogger(__name__)


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


# Gemeinsam mit lernstufe_wrapper — beide belegen den Slot YOLO_TEST (Audit S-H1).
_predict_lock = yolo_test_slot.PREDICT_LOCK
_CANDIDATE_ID_PATTERN = re.compile(
    r"^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$"
)
_EXPECTED_CLASS_NAMES = {
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
    14: "BCC_bogen",
}


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
    candidate_id = resolved_child.name
    if _CANDIDATE_ID_PATTERN.fullmatch(candidate_id) is None:
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
        candidate_id=candidate_id,
        weights_path=weights_path,
        weights_sha256=expected_sha.lower(),
        map50=map50,
        epochs_completed=epochs_completed,
        created_utc=created_utc,
    )


def list_candidates() -> list[BccCandidate]:
    """Liefert manifest- und hashgepruefte direkte Kandidaten, neueste zuerst."""

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

    return sorted(
        candidates,
        key=lambda item: (
            item.created_utc,
            item.epochs_completed,
            item.map50,
            item.candidate_id,
        ),
        reverse=True,
    )


def select_candidate(
    candidate_id: str | None = None,
    candidate_sha256: str | None = None,
) -> BccCandidate:
    """Waehlt den angehefteten oder kompatibel den automatisch besten Kandidaten."""

    if (candidate_id is None) != (candidate_sha256 is None):
        raise BccTestCandidateError(
            "BCC-Kandidaten-ID und SHA-256 muessen gemeinsam angegeben werden."
        )

    candidates = list_candidates()
    if candidate_id is not None:
        if (
            _CANDIDATE_ID_PATTERN.fullmatch(candidate_id) is None
            or not _is_sha256(candidate_sha256)
        ):
            raise BccTestCandidateError(
                "Die angeforderte BCC-Kandidaten-ID ist ungueltig."
            )

        requested_sha = str(candidate_sha256).lower()
        selected = next(
            (
                item
                for item in candidates
                if item.candidate_id == candidate_id
                and item.weights_sha256 == requested_sha
            ),
            None,
        )
        if selected is None:
            raise BccTestCandidateError(
                "Der angeforderte BCC-Testkandidat ist nicht sicher verfuegbar."
            )
        return selected

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

    # YOLO darf den veraenderbaren Kandidatenpfad nicht erneut oeffnen. Wir
    # kopieren genau einen gelesenen Byte-Strom in eine private Temp-Datei,
    # pruefen dessen Hash und laden ausschliesslich diese Momentaufnahme.
    try:
        with tempfile.TemporaryDirectory(prefix="sewerstudio_bcc_") as temp_dir:
            snapshot_path = Path(temp_dir) / "candidate.pt"
            digest = hashlib.sha256()
            with candidate.weights_path.open("rb") as source, snapshot_path.open("xb") as target:
                for chunk in iter(lambda: source.read(1 << 20), b""):
                    digest.update(chunk)
                    target.write(chunk)
            if digest.hexdigest().lower() != candidate.weights_sha256:
                raise BccTestCandidateError(
                    "Der Hash des BCC-Testmodells hat sich vor dem Laden geaendert."
                )

            model = YOLO(str(snapshot_path))
            names = _normalized_names(model.names)
            if names != _EXPECTED_CLASS_NAMES:
                raise BccTestCandidateError(
                    "Das BCC-Testmodell hat nicht die freigegebene 15er-Klassenkarte."
                )
            if _sha256(snapshot_path).lower() != candidate.weights_sha256:
                raise BccTestCandidateError(
                    "Die private BCC-Modellkopie hat sich waehrend des Ladens geaendert."
                )
            model.to(device)
            return model, None
    except BccTestCandidateError:
        raise
    except OSError as exc:
        raise BccTestCandidateError(
            "Die gepruefte BCC-Modellkopie konnte nicht sicher erstellt werden."
        ) from exc


def _ensure_candidate_model(candidate: BccCandidate, device: str):
    """Sorgt dafuer, dass GENAU dieser Kandidat im Test-Slot liegt.

    Den Kandidatenwechsel entscheidet der gpu_manager anhand der Inhaltskennung
    (Gewichts-SHA-256). Das ist wichtig, weil sich `lernstufe_wrapper` denselben
    Slot teilt: Eine Modulvariable hier wuerde dessen Wechsel nicht sehen und das
    fremde Modell inferieren lassen (Audit 2026-08-14, S-H1).
    """
    state = gpu_manager.ensure_loaded(
        ModelSlot.YOLO_TEST,
        device,
        lambda: _load_candidate(candidate, device),
        content_id=candidate.weights_sha256,
    )
    return state.model


def _extract_bcc_detections(results: object) -> list[YoloDetection]:
    """Gibt im BCC-Pilot nur die gepruefte Pilotklasse mit fester ID 14 aus."""

    detections: list[YoloDetection] = []
    if not results:
        return detections

    result = results[0]
    boxes = result.boxes
    if boxes is None:
        return detections

    for box in boxes:
        class_id = int(box.cls[0].cpu().item())
        if class_id != 14:
            continue
        xyxy = box.xyxy[0].cpu().numpy()
        confidence = float(box.conf[0].cpu().item())
        detections.append(
            YoloDetection(
                x1=float(xyxy[0]),
                y1=float(xyxy[1]),
                x2=float(xyxy[2]),
                y2=float(xyxy[3]),
                class_name=_EXPECTED_CLASS_NAMES[14],
                confidence=confidence,
            )
        )
    return detections


def _lese_meter_sicher(image, meter_format: str | None) -> float | None:
    """Rohe OSD-Meterlesung desselben Bildes.

    Rein lesend und zustandslos pro Bild; die Sequenz-Plausibilitaet gehoert
    dem Aufrufer (Thin-AI: C# entscheidet). Ein Lesefehler darf die Erkennung
    nie gefaehrden — der Wert ist dann eben nicht lesbar (None).
    """

    try:
        modell_leser = (
            osd_model_wrapper.lese
            if settings.osd_model_fallback_enabled
            else None
        )
        return osd_meter.lese_meter(
            image,
            osd_meter.get_templates(),
            format=meter_format,
            modell_leser=modell_leser,
        )["meter"]
    except Exception:
        logger.warning("OSD-Meterlesung fehlgeschlagen", exc_info=True)
        return None


def detect(
    image_base64: str,
    confidence_threshold: float,
    candidate_id: str | None = None,
    candidate_sha256: str | None = None,
    meter_format: str | None = None,
) -> BccTestYoloResponse:
    """Fuehrt nur auf ausdruecklichen Aufruf eine BCC-Kandidaten-Erkennung aus."""

    image = yolo_wrapper.decode_image(image_base64)
    meter_value = _lese_meter_sicher(image, meter_format)
    usable, quality_reason = yolo_wrapper._is_frame_usable(image)
    candidate = select_candidate(candidate_id, candidate_sha256)
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
            frame_usable=False,
            quality_reason=quality_reason,
            meter_value=meter_value,
        )

    # Das Lock teilen sich beide Nutzer des Slots YOLO_TEST; damit kann sich
    # kein fremder Modellwechsel zwischen Laden und Inferenz schieben.
    with _predict_lock:
        # Fremden Inhalt VOR der Lease raeumen (eigene Lease wuerde das unload
        # sonst sperren). Der Vergleich laeuft ueber die Inhaltskennung des Slots,
        # nicht ueber eine Modulvariable — sonst bliebe ein Wechsel des
        # Lernstufen-Wrappers unbemerkt (Audit S-H1).
        gpu_manager.discard_foreign_content(
            ModelSlot.YOLO_TEST, candidate.weights_sha256)
        # Danach Laden + Inferenz UNTER der Lease (Paket 2): das geladene
        # Kandidaten-Modell ist vom ensure_loaded bis zum Inferenzende vor
        # Eviction geschuetzt, und wartende Requests koennen die Busy-Uhr nicht
        # verschieben.
        with gpu_manager.busy_slot(ModelSlot.YOLO_TEST):
            model = _ensure_candidate_model(candidate, device)
            if model is None:
                # Unload-Race (Paket 3/B): Slot wurde zwischen ensure_loaded und Zugriff
                # entladen -> kontrollierter 503 statt AttributeError/500.
                raise ModelUnloadedError(ModelSlot.YOLO_TEST.value)
            started = time.perf_counter()
            # Ultralytics behandelt NumPy-Eingaben als BGR; ein PIL-RGB-Array
            # wuerde Rot und Blau still vertauschen (belegt 2026-08-02). Der
            # gemeinsame Helfer gilt auch hier.
            results = model.predict(
                source=yolo_wrapper._pil_rgb_to_ultralytics_bgr(image),
                conf=confidence_threshold,
                imgsz=settings.yolo_imgsz,
                verbose=False,
            )
            inference_time_ms = (time.perf_counter() - started) * 1000

    detections = _extract_bcc_detections(results)

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
        meter_value=meter_value,
    )
