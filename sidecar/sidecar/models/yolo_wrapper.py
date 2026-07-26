"""YOLO model wrapper for pre-screening and detection."""

from __future__ import annotations

import json
import time
import logging
import threading
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image

from ..config import settings
from ..gpu_manager import gpu_manager, ModelSlot
from ..schemas.detection import YoloDetection, YoloResponse
from .image_decode import decode_image_safe

logger = logging.getLogger(__name__)

# Flag: True when custom sewer-specific weights are loaded, False for COCO fallback.
_using_custom_weights = False
_resolved_model_path: str | None = None
_resolved_model_sha256: str | None = None
_tensorrt_class_names: dict[int, str] = {}
_tensorrt_names_warning_paths: set[str] = set()
_tensorrt_class_warning_keys: set[tuple[str, int]] = set()

# CPU-mode singleton (bypasses GpuModelManager when YOLO runs on CPU)
_cpu_model = None
_cpu_lock = threading.Lock()
# Serialisiert YOLO-Detect-Inferenz (Gesamtaudit P7): parallele Threadpool-Requests
# auf demselben Ultralytics-Modell koennen sich sonst verschraenken (Race/OOM).
_yolo_predict_lock = threading.Lock()
_gpu_utilization_lock = threading.Lock()
_gpu_utilization_cached_at = 0.0
_gpu_utilization_cached_value: float | None = None
_GPU_UTILIZATION_TTL_SECONDS = 2.0


def _resolve_yolo_model_path() -> tuple[str, bool]:
    # Plan: models/yolo26m/<weights>.pt
    yolo_dir = Path(settings.models_dir) / "yolo26m"
    model_path = yolo_dir / settings.yolo_model_name

    if not model_path.exists():
        # Try flat path: models/<yolo_model_name>
        model_path = Path(settings.models_dir) / settings.yolo_model_name

    if model_path.exists():
        return str(model_path), True

    if settings.require_custom_yolo:
        raise FileNotFoundError(
            "Custom YOLO weights are required but were not found. "
            f"Expected '{settings.yolo_model_name}' in '{yolo_dir}' or '{settings.models_dir}'."
        )

    return "yolo11m.pt", False


def get_active_detector_artifact() -> dict:
    """Resolve the artifact that the standard YOLO endpoint would actually use."""

    if _resolved_model_path is not None:
        model_path = _resolved_model_path
        using_custom = _using_custom_weights
        resolution_error = None
        loaded = True
    else:
        loaded = False
        try:
            model_path, using_custom = _resolve_yolo_model_path()
            resolution_error = None
        except FileNotFoundError:
            model_path = str(
                Path(settings.models_dir) / "yolo26m" / settings.yolo_model_name
            )
            using_custom = True
            resolution_error = "configured model artifact not found"

    model_name = Path(model_path).name
    return {
        "path": model_path,
        "file_name": model_name,
        "backend": _model_backend(model_name),
        "using_custom_weights": using_custom,
        "loaded": loaded,
        "resolution_error": resolution_error,
        "sha256": _resolved_model_sha256 if loaded else None,
    }


def get_runtime_status() -> dict:
    """Return current YOLO runtime/configuration information for diagnostics."""
    yolo_dir = Path(settings.models_dir) / "yolo26m"
    candidate_nested = yolo_dir / settings.yolo_model_name
    candidate_flat = Path(settings.models_dir) / settings.yolo_model_name
    custom_exists = candidate_nested.exists() or candidate_flat.exists()

    status = {
        "configured_model_name": settings.yolo_model_name,
        "configured_model_backend": _configured_backend(),
        "require_custom_yolo": settings.require_custom_yolo,
        "custom_weights_present": custom_exists,
        "using_custom_weights": _using_custom_weights,
        "resolved_model_path": _resolved_model_path,
        "fallback_model_name": None if _using_custom_weights or settings.require_custom_yolo else "yolo11m.pt",
        "device": _resolve_device(),
    }

    if candidate_nested.exists():
        status["custom_model_path"] = str(candidate_nested)
    elif candidate_flat.exists():
        status["custom_model_path"] = str(candidate_flat)
    else:
        status["custom_model_path"] = str(candidate_nested)

    return status


def _resolve_device() -> str:
    """Determine the effective device for YOLO inference."""
    device = settings.effective_yolo_device
    if _configured_backend() == "tensorrt":
        return device

    if device.startswith("cuda") and not _cuda_available():
        logger.warning("YOLO configured for %s but CUDA unavailable, falling back to cpu", device)
        return "cpu"
    return device


def _configured_backend() -> str:
    suffix = Path(settings.yolo_model_name).suffix.lower()
    if suffix == ".engine":
        return "tensorrt"
    if suffix in {".pt", ".pth"}:
        return "pytorch"
    if suffix == ".onnx":
        return "onnx"
    return suffix.lstrip(".") or "unknown"


def _load_yolo_on(device: str):
    """Load YOLO model onto *device*. Returns (model, None)."""
    global _using_custom_weights, _resolved_model_path, _resolved_model_sha256
    global _tensorrt_class_names
    from ultralytics import YOLO

    model_path, using_custom = _resolve_yolo_model_path()
    model_file = Path(model_path)
    artifact_sha256 = _sha256_of(model_file) if using_custom else None
    tensorrt_class_names = _load_tensorrt_class_names(model_file)

    if using_custom:
        logger.info("Loading custom YOLO weights from %s onto %s", model_path, device)
    else:
        logger.warning(
            "Custom YOLO weights not found – downloading yolo11m.pt as fallback. "
            "Using image-quality pre-screening instead of defect detection. "
            "Fine-tune and place custom weights in models/yolo26m/ for sewer-specific detection."
        )

    model = YOLO(str(model_path))
    if _model_backend(model_file.name) != "tensorrt":
        model.to(device)

    if using_custom and _sha256_of(model_file) != artifact_sha256:
        raise RuntimeError("YOLO model artifact changed while loading")

    _using_custom_weights = using_custom
    _resolved_model_path = model_path
    _resolved_model_sha256 = artifact_sha256
    _tensorrt_class_names = tensorrt_class_names
    return model, None


def _load_tensorrt_class_names(model_path: str | Path) -> dict[int, str]:
    """Load YOLO class names for a TensorRT engine sidecar file."""
    path = Path(model_path)
    if path.suffix.lower() != ".engine":
        return {}

    names_path = path.with_suffix(".names.json")
    warning_key = str(names_path)
    if not names_path.exists():
        if warning_key not in _tensorrt_names_warning_paths:
            logger.warning(
                "TensorRT class-name file missing: %s. Detection labels will fall back to classN.",
                names_path,
            )
            _tensorrt_names_warning_paths.add(warning_key)
        return {}

    try:
        payload = json.loads(names_path.read_text(encoding="utf-8-sig"))
        raw_names = payload.get("names", {})
        if isinstance(raw_names, list):
            return {
                index: str(name)
                for index, name in enumerate(raw_names)
                if str(name).strip()
            }

        if isinstance(raw_names, dict):
            class_names: dict[int, str] = {}
            for key, value in raw_names.items():
                name = str(value).strip()
                if not name:
                    continue
                class_names[int(key)] = name
            return class_names
    except Exception as exc:
        logger.warning(
            "TensorRT class-name file could not be read: %s (%s). Detection labels will fall back to classN.",
            names_path,
            exc,
        )

    return {}


def _class_name_for_id(
    cls_id: int,
    result_names: dict | None,
    class_names: dict[int, str] | None = None,
) -> str:
    mapped_names = _tensorrt_class_names if class_names is None else class_names
    if cls_id in mapped_names:
        return mapped_names[cls_id]

    result_name = _name_from_result(cls_id, result_names)
    if result_name and not _is_generic_class_name(cls_id, result_name):
        return result_name

    fallback = f"class{cls_id}"
    if Path(_resolved_model_path or settings.yolo_model_name).suffix.lower() == ".engine" or class_names is not None:
        _warn_class_name_fallback(cls_id, fallback)
    return fallback


def _name_from_result(cls_id: int, result_names: dict | None) -> str | None:
    if not result_names:
        return None

    if cls_id in result_names:
        return str(result_names[cls_id])

    key = str(cls_id)
    if key in result_names:
        return str(result_names[key])

    return None


def _is_generic_class_name(cls_id: int, name: str) -> bool:
    normalized = name.strip().lower()
    return normalized in {str(cls_id), f"class{cls_id}".lower()}


def _warn_class_name_fallback(cls_id: int, fallback: str) -> None:
    model_key = _resolved_model_path or settings.yolo_model_name
    warning_key = (model_key, cls_id)
    if warning_key in _tensorrt_class_warning_keys:
        return

    logger.warning(
        "TensorRT class id %s has no YOLO name mapping; falling back to %s.",
        cls_id,
        fallback,
    )
    _tensorrt_class_warning_keys.add(warning_key)


def _get_yolo_model():
    """Get the YOLO model, loading if necessary.

    CPU path: module-level singleton bypassing the GPU manager.
    GPU path: uses gpu_manager.ensure_loaded for persistent slot.
    """
    global _cpu_model
    device = _resolve_device()

    if device == "cpu":
        if _cpu_model is not None:
            return _cpu_model
        with _cpu_lock:
            if _cpu_model is not None:
                return _cpu_model
            model, _ = _load_yolo_on(device)
            _cpu_model = model
            return _cpu_model
    else:
        state = gpu_manager.ensure_loaded(
            ModelSlot.YOLO, device, lambda: _load_yolo_on(device)
        )
        return state.model


def _cuda_available() -> bool:
    try:
        import torch
        return torch.cuda.is_available()
    except Exception:
        return False


def _response_telemetry(queue_wait_ms: float = 0.0) -> dict:
    status = gpu_manager.get_status()
    model_name = Path(_resolved_model_path).name if _resolved_model_path else settings.yolo_model_name
    return {
        "model_name": model_name,
        "model_backend": _model_backend(model_name),
        "device": _resolve_device(),
        "queue_wait_ms": round(queue_wait_ms, 1),
        "vram_allocated_gb": status.get("vram_allocated_gb"),
        "vram_total_gb": status.get("vram_total_gb"),
        "gpu_utilization_percent": _gpu_utilization_percent(),
    }


def _model_backend(model_name: str) -> str:
    suffix = Path(model_name).suffix.lower()
    if suffix == ".engine":
        return "tensorrt"
    if suffix in {".pt", ".pth"}:
        return "pytorch"
    if suffix == ".onnx":
        return "onnx"
    return suffix.lstrip(".") or "unknown"


def _gpu_utilization_percent() -> float | None:
    if not _resolve_device().startswith("cuda"):
        return None

    global _gpu_utilization_cached_at, _gpu_utilization_cached_value
    now = time.monotonic()
    with _gpu_utilization_lock:
        if now - _gpu_utilization_cached_at < _GPU_UTILIZATION_TTL_SECONDS:
            return _gpu_utilization_cached_value

        value: float | None = None
        _gpu_utilization_cached_at = now

        try:
            result = subprocess.run(
                [
                    "nvidia-smi",
                    "--query-gpu=utilization.gpu",
                    "--format=csv,noheader,nounits",
                ],
                capture_output=True,
                text=True,
                timeout=0.5,
                check=False,
            )
            if result.returncode == 0:
                first_line = result.stdout.strip().splitlines()[0]
                value = float(first_line.strip())
        except Exception:
            value = None

        _gpu_utilization_cached_value = value
        return value


def _reset_gpu_utilization_cache_for_tests() -> None:
    global _gpu_utilization_cached_at, _gpu_utilization_cached_value
    with _gpu_utilization_lock:
        _gpu_utilization_cached_at = 0.0
        _gpu_utilization_cached_value = None


def decode_image(image_base64: str) -> Image.Image:
    """Decode a base64-encoded image to PIL Image."""
    return decode_image_safe(
        image_base64,
        max_bytes=settings.inference_max_image_bytes,
        max_pixels=settings.max_image_pixels,
    )


def _is_frame_usable(img: Image.Image) -> tuple[bool, str]:
    """Check if a frame is usable for analysis using image quality heuristics.

    Filters out:
    - Completely black/dark frames (lens cap, no signal)
    - Completely white/overexposed frames
    - Very low variance frames (solid color, no texture)

    Returns (is_usable, reason).
    """
    arr = np.array(img, dtype=np.float32)

    # Convert to grayscale for analysis
    gray = arr.mean(axis=2)
    mean_brightness = gray.mean()
    std_brightness = gray.std()

    # Too dark (lens cap, black frame, no signal) -- Schwelle bewusst niedrig,
    # damit dunkle, aber inhaltlich gueltige Kanal-Frames erhalten bleiben.
    if mean_brightness < settings.frame_min_brightness:
        return False, "too_dark"

    # Too bright (overexposed, white frame)
    if mean_brightness > settings.frame_max_brightness:
        return False, "too_bright"

    # Too uniform (solid color, no texture = likely no pipe content)
    if std_brightness < settings.frame_min_std:
        return False, "too_uniform"

    # Check edge density using Laplacian-like filter for blur detection
    # A very blurry frame has low edge variance
    from scipy.ndimage import laplace
    edges = laplace(gray)
    edge_var = edges.var()

    if edge_var < settings.frame_min_edge_var:
        return False, "too_blurry"

    return True, "ok"


def detect(image_base64: str, confidence_threshold: float) -> YoloResponse:
    """Run YOLO detection on a base64-encoded image.

    Behavior depends on model type:
    - Custom sewer weights: True defect detection via YOLO.
    - COCO fallback (yolo11m): Image-quality pre-screening that filters out
      dark/blank/blurry frames. YOLO detections are still returned for info,
      but is_relevant is based on image quality, not COCO class detections.
    """
    model = _get_yolo_model()

    img = decode_image(image_base64)

    # Image-quality pre-screening (always run, fast)
    usable, quality_reason = _is_frame_usable(img)

    if not usable:
        # Frame is not usable at all – skip without running YOLO inference
        return YoloResponse(
            is_relevant=False,
            detections=[],
            frame_class=quality_reason,
            inference_time_ms=0.0,
            **_response_telemetry(),
        )

    # Ultralytics-Predict ist nicht thread-sicher; FastAPI fuehrt sync-Routen im
    # Threadpool aus -> parallele Requests serialisieren (Gesamtaudit P7, wie SAM).
    # Wartezeit am Lock als queue_wait_ms messen; inference_time_ms bleibt reine Predict-Zeit.
    t_queue = time.perf_counter()
    with _yolo_predict_lock:
        queue_wait_ms = (time.perf_counter() - t_queue) * 1000
        t0 = time.perf_counter()
        results = model.predict(
            source=np.array(img),
            conf=confidence_threshold,
            imgsz=settings.yolo_imgsz,
            verbose=False,
        )
    elapsed_ms = (time.perf_counter() - t0) * 1000

    detections: list[YoloDetection] = []
    frame_class = "empty"

    if results and len(results) > 0:
        result = results[0]
        boxes = result.boxes
        if boxes is not None and len(boxes) > 0:
            frame_class = "relevant"
            for box in boxes:
                xyxy = box.xyxy[0].cpu().numpy()
                cls_id = int(box.cls[0].cpu().item())
                conf = float(box.conf[0].cpu().item())
                cls_name = _class_name_for_id(cls_id, result.names)
                detections.append(YoloDetection(
                    x1=float(xyxy[0]),
                    y1=float(xyxy[1]),
                    x2=float(xyxy[2]),
                    y2=float(xyxy[3]),
                    class_name=cls_name,
                    confidence=conf,
                ))

    if _using_custom_weights:
        # Custom weights: relevance = has defect detections
        is_relevant = len(detections) > 0
    else:
        # COCO fallback: frame passed quality check → relevant for DINO analysis.
        # COCO detections are informational only.
        is_relevant = True
        frame_class = "pipe_content" if frame_class == "empty" else frame_class

    return YoloResponse(
        is_relevant=is_relevant,
        detections=detections,
        frame_class=frame_class,
        inference_time_ms=round(elapsed_ms, 1),
        **_response_telemetry(queue_wait_ms),
    )


# ── YOLO Classify (Whole-Frame-Klassifikator) ──────────────────────────

_cls_model = None
_cls_meta: dict | None = None
_cls_lock = threading.Lock()
# Serialisiert cls-Inferenz (Gesamtaudit P7) — _cls_lock selbst schuetzt nur das Laden.
_cls_predict_lock = threading.Lock()


def _sha256_of(path: Path) -> str:
    import hashlib
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def _resolve_cls_model() -> dict | None:
    """Aufloesung des cls-Modells als Metadaten-Dict.

    Reihenfolge:
      1) models/active.json (Eintrag "classifier") — der reale Promotions-Weg.
         SHA-256 wird gegen die Datei verifiziert; Mismatch = FEHLER, kein Laden.
      2) settings.yolo_cls_model_path — expliziter manueller Override.
      3) Legacy-Kandidaten (grundgeruest) — mit DEUTLICHER Warnung statt still.
    Liefert {path, source, sha256, imgsz, preprocessing} oder None.
    """
    # 1) active.json — einziger Schreiber ist der model-promotion-warden
    active_path = Path(settings.models_dir) / "active.json"
    if active_path.is_file():
        try:
            data = json.loads(active_path.read_text(encoding="utf-8"))
        except Exception as exc:
            logger.error("active.json nicht lesbar (%s) — Klassifikator bleibt AUS.", exc)
            return None
        entry = data.get("classifier")
        if entry and entry.get("weights_path"):
            weights = Path(entry["weights_path"])
            if not weights.is_absolute():
                weights = Path(settings.models_dir) / weights
            if not weights.is_file():
                logger.error("active.json zeigt auf fehlende Gewichte: %s — Klassifikator bleibt AUS.", weights)
                return None
            sha = _sha256_of(weights)
            expected = (entry.get("sha256") or "").lower()
            if expected and sha.lower() != expected:
                logger.error(
                    "active.json SHA-256-Mismatch fuer %s (erwartet %s…, ist %s…) — Klassifikator bleibt AUS.",
                    weights, expected[:8], sha[:8])
                return None
            return {
                "path": str(weights),
                "source": "active.json",
                "name": entry.get("name") or weights.parent.parent.name,
                "sha256": sha,
                "imgsz": int(entry.get("imgsz") or settings.yolo_cls_imgsz),
                "preprocessing": entry.get("preprocessing") or "letterbox",
            }

    # 2) Expliziter Override per Env/Settings
    if settings.yolo_cls_model_path:
        p = Path(settings.yolo_cls_model_path)
        if not p.is_file():
            logger.error("yolo_cls_model_path zeigt auf fehlende Datei: %s — Klassifikator bleibt AUS.", p)
            return None
        return {
            "path": str(p),
            "source": "configured",
            "name": p.parent.parent.name if p.parent.name == "weights" else p.stem,
            "sha256": _sha256_of(p),
            "imgsz": settings.yolo_cls_imgsz,
            "preprocessing": settings.yolo_cls_preprocessing,
        }

    # 3) Legacy-Fallback (Grundgeruest-Laeufe) — NICHT mehr still
    project_root = Path(__file__).resolve().parent.parent.parent.parent
    candidates = [
        Path(settings.models_dir) / "yolo_cls_best.pt",
        project_root / "yolo_cls_runs" / "grundgeruest_v2" / "weights" / "best.pt",
        project_root / "yolo_cls_runs" / "grundgeruest_v1" / "weights" / "best.pt",
    ]
    for p in candidates:
        if p.exists():
            logger.warning(
                "KEIN models/active.json — Legacy-Fallback auf %s. Der Promotions-Weg "
                "(model-promotion-warden -> active.json) ist fuer dieses Modell nie gelaufen.", p)
            return {
                "path": str(p),
                "source": "legacy_fallback",
                "name": p.parent.parent.name,
                "sha256": _sha256_of(p),
                # Grundgeruest-Laeufe wurden MIT Ultralytics-Crop trainiert ->
                # bisheriges predict-Verhalten beibehalten (kein Letterbox).
                "imgsz": 0,
                "preprocessing": "default",
            }
    logger.warning("Kein YOLO-cls Modell gefunden (weder active.json noch Fallback) — Klassifikator AUS.")
    return None


def _resolve_cls_device() -> str:
    device = settings.effective_cls_device
    if device.startswith("cuda") and not _cuda_available_cls():
        return "cpu"
    return device


def _cuda_available_cls() -> bool:
    try:
        import torch
        return torch.cuda.is_available()
    except Exception:
        return False


def classifier_metadata() -> dict:
    """Metadaten des geladenen cls-Modells (fuer Response/Telemetrie); leer wenn keins."""
    return dict(_cls_meta) if _cls_meta else {}


def _ensure_nocrop_module() -> None:
    """no-crop-Checkpoints picklen Transforms aus dem Trainings-Modul 'nocrop_patch'.

    Damit torch.load sie ausserhalb des Trainings-Verzeichnisses laden kann,
    registrieren wir die identische Sidecar-Kopie unter genau diesem Namen.
    """
    import sys as _sys
    if "nocrop_patch" in _sys.modules:
        return
    from . import nocrop_compat
    _sys.modules["nocrop_patch"] = nocrop_compat


def _get_cls_model():
    """Lazy-load des Classify-Modells (Device via SEWER_SIDECAR_YOLO_CLS_DEVICE)."""
    global _cls_model, _cls_meta
    if _cls_model is not None:
        return _cls_model
    with _cls_lock:
        if _cls_model is not None:
            return _cls_model
        meta = _resolve_cls_model()
        if meta is None:
            return None
        _ensure_nocrop_module()
        from ultralytics import YOLO
        model = YOLO(meta["path"])
        device = _resolve_cls_device()
        model.to(device)
        meta["device"] = device
        logger.info(
            "YOLO-cls Modell geladen: %s (quelle=%s, sha256=%s…, imgsz=%s, preprocessing=%s, device=%s)",
            meta["name"], meta["source"], meta["sha256"][:12], meta["imgsz"] or "default",
            meta["preprocessing"], device)
        _cls_meta = meta
        _cls_model = model
        return _cls_model


def classify_with_quality(
    image_base64: str, top_k: int = 5
) -> tuple[list[tuple[str, float, float]], bool, str]:
    """Quality-Gate + Whole-Frame-Klassifikation mit einem einzigen Decode.

    Liefert (predictions, usable, quality_reason). Unbrauchbare Frames
    (schwarz/ueberbelichtet/strukturlos/unscharf) werden NICHT klassifiziert —
    der Aufrufer kann sie verwerfen, bevor DINO/SAM/Qwen Zeit kosten.
    """
    img = decode_image(image_base64)
    return classify_image_with_quality(img, top_k)


def classify_image_with_quality(
    img: Image.Image, top_k: int = 5
) -> tuple[list[tuple[str, float, float]], bool, str]:
    """Quality-Gate + Whole-Frame-Klassifikation fuer ein bereits dekodiertes Bild."""
    usable, reason = _is_frame_usable(img)
    if not usable:
        return [], False, reason
    return _classify_image(img, top_k), True, "ok"


def classify(image_base64: str, top_k: int = 5) -> list[tuple[str, float, float]]:
    """Whole-Frame-Klassifikation: Gibt Top-K Klassen mit Konfidenz zurueck."""
    img = decode_image(image_base64)
    return _classify_image(img, top_k)


def _letterbox_rgb(img: Image.Image, size: int) -> Image.Image:
    """Proportional skalieren + schwarz padden (kein Crop, keine Verzerrung).

    Identisch zu training/vsa_classifier/nocrop_patch.letterbox_pil — Pflicht
    fuer Paritaet zwischen Sidecar-Inferenz und eval_cls.py (Ultralytics wuerde
    sonst Resize+CenterCrop fahren und die seitlichen Rand-Schaeden abschneiden).
    """
    if img.mode != "RGB":
        img = img.convert("RGB")
    w, h = img.size
    if w == size and h == size:
        return img
    scale = min(size / w, size / h)
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    img = img.resize((nw, nh), Image.BILINEAR)
    canvas = Image.new("RGB", (size, size), (0, 0, 0))
    canvas.paste(img, ((size - nw) // 2, (size - nh) // 2))
    return canvas


def get_classifier_status() -> dict:
    """Klassifikator-Status fuer /health (Gesamtaudit P2; schliesst auch den Audit-Punkt
    'cls-Metadaten fehlen in /health'). Geladen: name@sha des aktiven Modells. Nicht
    geladen: nur Konfigurationslage — bewusst KEIN SHA-Hashing pro Health-Poll."""
    meta = _cls_meta
    if meta:
        return {
            "loaded": True,
            "name": meta.get("name"),
            "sha256_12": (meta.get("sha256") or "")[:12],
            "source": meta.get("source"),
            "imgsz": meta.get("imgsz"),
            "preprocessing": meta.get("preprocessing"),
        }

    active = Path(settings.models_dir) / "active.json"
    return {
        "loaded": False,
        "active_json_present": active.is_file(),
        "override_configured": bool(settings.yolo_cls_model_path),
    }


def _classify_image(img: Image.Image, top_k: int) -> list[tuple[str, float, float]]:
    model = _get_cls_model()
    if model is None:
        return []
    meta = _cls_meta or {}

    t0 = time.perf_counter()
    # Inferenz serialisieren (Gesamtaudit P7) — _cls_lock schuetzt nur das Laden.
    with _cls_predict_lock:
        if meta.get("preprocessing") == "letterbox":
            # Wie eval_cls.py --no-crop: Letterbox in PIL, dann RGB->BGR-Array
            # (predict behandelt numpy-Eingaben als BGR und konvertiert intern).
            imgsz = int(meta.get("imgsz") or settings.yolo_cls_imgsz)
            lb = _letterbox_rgb(img, imgsz)
            src = np.asarray(lb)[:, :, ::-1]
            results = model.predict(source=src, imgsz=imgsz, verbose=False)
        else:
            # Legacy (Grundgeruest, mit Crop trainiert): bisheriges Verhalten
            results = model.predict(source=np.array(img), verbose=False)
    elapsed_ms = (time.perf_counter() - t0) * 1000

    if not results or len(results) == 0:
        return []

    probs = results[0].probs
    if probs is None:
        return []

    # Top-K Indizes nach Konfidenz sortiert
    top_indices = probs.data.topk(min(top_k, len(probs.data))).indices.cpu().tolist()
    predictions = []
    for idx in top_indices:
        name = model.names.get(idx, str(idx))
        conf = float(probs.data[idx].cpu().item())
        if conf > 0.01:  # Nur relevante Klassen
            predictions.append((name, conf, elapsed_ms))

    return predictions
