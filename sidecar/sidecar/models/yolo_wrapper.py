"""YOLO model wrapper for pre-screening and detection."""

from __future__ import annotations

import base64
import io
import json
import logging
import threading
import time
from contextlib import contextmanager
from pathlib import Path

import numpy as np
from PIL import Image

from ..config import settings
from ..gpu_manager import ModelSlot, gpu_manager
from ..schemas.detection import YoloDetection, YoloResponse

logger = logging.getLogger(__name__)

# True when custom sewer-specific weights are loaded, False for COCO fallback.
_using_custom_weights = False
_resolved_model_path: str | None = None
_tensorrt_active = False
_runtime_model_override: str | None = None

# CPU-mode singleton (bypasses GpuModelManager when YOLO runs on CPU)
_cpu_model = None
_cpu_lock = threading.Lock()

# Coordination between inference and maintenance (training/reload)
_state_cv = threading.Condition()
_active_inference = 0
_maintenance_reason: str | None = None


def _resolve_path(path_like: str, *, base: Path | None = None) -> Path:
    candidate = Path(path_like)
    if not candidate.is_absolute() and base is not None:
        candidate = base / candidate
    try:
        return candidate.resolve()
    except Exception:
        return candidate


def _yolo_dir() -> Path:
    # Ordner basierend auf Modellname: "yolo26l-seg.pt" → "yolo26l-seg/"
    model_stem = Path(settings.yolo_model_name).stem
    model_dir = Path(settings.models_dir) / model_stem
    # Fallback auf yolo26m wenn Ordner nicht existiert
    if not model_dir.exists():
        model_dir = Path(settings.models_dir) / "yolo26m"
    return model_dir


def _resolve_active_model_from_file() -> Path | None:
    """Read active model pointer from models/<yolo_dir>/active.json if present."""
    active_file = _yolo_dir() / "active.json"
    if not active_file.exists():
        return None
    try:
        payload = json.loads(active_file.read_text(encoding="utf-8"))
        model_name = str(payload.get("active_model", "")).strip()
        if not model_name:
            return None
        candidate = _resolve_path(model_name, base=_yolo_dir())
        return candidate if candidate.exists() else None
    except Exception as exc:
        logger.warning("Unable to read active model pointer %s: %s", active_file, exc)
        return None


def _resolve_candidate_model_path(path_like: str) -> Path:
    raw = Path(path_like)
    if raw.is_absolute():
        return raw.resolve()
    nested = _resolve_path(path_like, base=_yolo_dir())
    if nested.exists():
        return nested
    return _resolve_path(path_like, base=Path(settings.models_dir))


def _begin_maintenance(reason: str, timeout_sec: float = 0.0) -> None:
    """Block new inferences and wait until active ones are finished."""
    global _maintenance_reason
    deadline = time.monotonic() + max(timeout_sec, 0.0)
    with _state_cv:
        if _maintenance_reason is not None:
            raise RuntimeError(f"YOLO maintenance already active ({_maintenance_reason})")
        _maintenance_reason = reason
        while _active_inference > 0:
            remaining = deadline - time.monotonic()
            if timeout_sec <= 0.0 or remaining <= 0.0:
                _maintenance_reason = None
                _state_cv.notify_all()
                raise TimeoutError(
                    f"YOLO maintenance '{reason}' blocked by active inference ({_active_inference})"
                )
            _state_cv.wait(timeout=min(remaining, 0.25))


def _end_maintenance(reason: str) -> None:
    global _maintenance_reason
    with _state_cv:
        if _maintenance_reason == reason:
            _maintenance_reason = None
            _state_cv.notify_all()


@contextmanager
def _inference_guard():
    global _active_inference
    with _state_cv:
        if _maintenance_reason is not None:
            raise RuntimeError(
                f"YOLO temporary unavailable: {_maintenance_reason} is running"
            )
        _active_inference += 1
    try:
        yield
    finally:
        with _state_cv:
            _active_inference = max(0, _active_inference - 1)
            if _active_inference == 0:
                _state_cv.notify_all()


def get_inference_state() -> dict:
    with _state_cv:
        return {
            "active_inference": _active_inference,
            "maintenance_reason": _maintenance_reason,
        }


def has_active_inference() -> bool:
    with _state_cv:
        return _active_inference > 0


def _resolve_yolo_model_path() -> tuple[str, bool]:
    """Resolve YOLO weights/engine path.

    Priority:
    1) Runtime override (set by /model/reload)
    2) Persistent active pointer (models/yolo26m/active.json)
    3) Configured model name (SEWER_SIDECAR_YOLO_MODEL_NAME)
    4) Fallback yolo11m.pt (unless strict custom mode)
    """
    yolo_dir = _yolo_dir()

    if _runtime_model_override:
        override = _resolve_candidate_model_path(_runtime_model_override)
        if override.exists():
            if settings.yolo_use_tensorrt:
                engine = override.with_suffix(".engine")
                if engine.exists():
                    logger.info("TensorRT engine for runtime override found: %s", engine)
                    return str(engine), True
            return str(override), True
        logger.warning("Runtime override does not exist anymore: %s", override)

    active_model = _resolve_active_model_from_file()
    if active_model is not None:
        if settings.yolo_use_tensorrt:
            engine = active_model.with_suffix(".engine")
            if engine.exists():
                logger.info("TensorRT engine for active model found: %s", engine)
                return str(engine), True
        return str(active_model), True

    model_path = yolo_dir / settings.yolo_model_name
    if not model_path.exists():
        model_path = Path(settings.models_dir) / settings.yolo_model_name

    if model_path.exists():
        if settings.yolo_use_tensorrt:
            engine_path = model_path.with_suffix(".engine")
            if engine_path.exists():
                logger.info("TensorRT engine found: %s", engine_path)
                return str(engine_path), True
        return str(model_path), True

    if settings.require_custom_yolo:
        raise FileNotFoundError(
            "Custom YOLO weights are required but were not found. "
            f"Expected '{settings.yolo_model_name}' in '{yolo_dir}' or '{settings.models_dir}'."
        )

    fallback = "yolo11m.pt"
    if settings.yolo_use_tensorrt:
        fallback_engine = Path(fallback).with_suffix(".engine")
        if fallback_engine.exists():
            logger.info("TensorRT engine for fallback found: %s", fallback_engine)
            return str(fallback_engine), False
    return fallback, False


def _try_export_tensorrt(pt_path: str) -> str | None:
    """Export .pt model to TensorRT engine (.engine).

    Unterstuetzt FP16 (Standard) und FP4 (NVFP4, RTX 50xx).
    Precision wird ueber settings.yolo_precision gesteuert:
      - "fp16": Standard FP16 (Default)
      - "fp4":  NVFP4 native 4-bit (RTX 5090+, VRAM ~0.5GB statt ~1.5GB)
    """
    try:
        from ultralytics import YOLO

        pt = Path(pt_path)
        precision = getattr(settings, "yolo_precision", "fp16")
        suffix = f".{precision}.engine" if precision != "fp16" else ".engine"
        engine_path = pt.with_suffix(suffix)
        if engine_path.exists():
            return str(engine_path)

        use_fp16 = precision in ("fp16", "fp4")
        logger.info(
            "TensorRT export started: %s -> %s (precision=%s)",
            pt.name,
            engine_path.name,
            precision,
        )

        model = YOLO(pt_path)
        export_kwargs = {"format": "engine", "half": use_fp16}
        # NVFP4: TensorRT 10+ mit INT4/FP4 Quantisierung
        if precision == "fp4":
            export_kwargs["int8"] = True  # TensorRT INT4 via INT8-Pfad
        exported = model.export(**export_kwargs)

        if exported and Path(exported).exists():
            logger.info(
                "TensorRT export completed: %s (%.1f MB)",
                exported,
                Path(exported).stat().st_size / 1e6,
            )
            return str(exported)

        logger.warning("TensorRT export returned no valid file")
        return None
    except ImportError:
        logger.info("TensorRT not installed - using PyTorch model")
        return None
    except Exception as exc:
        logger.warning("TensorRT export failed (%s) - using PyTorch model", exc)
        return None


def get_runtime_status() -> dict:
    """Return current YOLO runtime/configuration information for diagnostics."""
    yolo_dir = _yolo_dir()
    candidate_nested = yolo_dir / settings.yolo_model_name
    candidate_flat = Path(settings.models_dir) / settings.yolo_model_name
    custom_exists = candidate_nested.exists() or candidate_flat.exists()
    active_model = _resolve_active_model_from_file()
    inference_state = get_inference_state()

    status = {
        "configured_model_name": settings.yolo_model_name,
        "require_custom_yolo": settings.require_custom_yolo,
        "custom_weights_present": custom_exists,
        "using_custom_weights": _using_custom_weights,
        "resolved_model_path": _resolved_model_path,
        "fallback_model_name": None
        if _using_custom_weights or settings.require_custom_yolo
        else "yolo11m.pt",
        "device": _resolve_device(),
        "tensorrt_active": _tensorrt_active,
        "tensorrt_enabled": settings.yolo_use_tensorrt,
        "runtime_model_override": _runtime_model_override,
        "active_model_path": str(active_model) if active_model else None,
        "active_inference": inference_state["active_inference"],
        "maintenance_reason": inference_state["maintenance_reason"],
    }

    if candidate_nested.exists():
        status["custom_model_path"] = str(candidate_nested)
    elif candidate_flat.exists():
        status["custom_model_path"] = str(candidate_flat)
    else:
        status["custom_model_path"] = str(candidate_nested)

    return status


def _resolve_device() -> str:
    """Determine effective device for YOLO inference."""
    device = settings.effective_yolo_device
    if device.startswith("cuda") and not _cuda_available():
        logger.warning("YOLO configured for %s but CUDA unavailable, falling back to cpu", device)
        return "cpu"
    return device


def _load_yolo_on(device: str):
    """Load YOLO model onto *device*. Returns (model, None)."""
    global _using_custom_weights, _resolved_model_path, _tensorrt_active
    from ultralytics import YOLO

    model_path, using_custom = _resolve_yolo_model_path()
    _using_custom_weights = using_custom
    _tensorrt_active = False

    if (
        settings.yolo_use_tensorrt
        and model_path.endswith(".pt")
        and device.startswith("cuda")
    ):
        engine_path = _try_export_tensorrt(model_path)
        if engine_path:
            model_path = engine_path

    _resolved_model_path = model_path

    if model_path.endswith(".engine"):
        logger.info("Loading TensorRT engine: %s on %s", model_path, device)
        _tensorrt_active = True
    elif using_custom:
        logger.info("Loading YOLO PyTorch model: %s on %s", model_path, device)
    else:
        logger.warning(
            "Custom YOLO weights not found - downloading yolo11m.pt as fallback. "
            "Using image-quality pre-screening instead of defect detection."
        )

    model = YOLO(str(model_path))
    if not model_path.endswith(".engine"):
        model.to(device)
    return model, None


def _get_yolo_model():
    """Get YOLO model, loading if necessary."""
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

    state = gpu_manager.ensure_loaded(
        ModelSlot.YOLO,
        device,
        lambda: _load_yolo_on(device),
    )
    return state.model


def unload_current_model() -> None:
    """Unload YOLO from CPU/GPU slots to free VRAM."""
    global _cpu_model, _resolved_model_path, _tensorrt_active
    with _cpu_lock:
        _cpu_model = None
    gpu_manager.unload(ModelSlot.YOLO)
    _resolved_model_path = None
    _tensorrt_active = False


def begin_training_guard(timeout_sec: float = 0.0) -> None:
    """Reserve exclusive YOLO access for training."""
    _begin_maintenance("training", timeout_sec=timeout_sec)


def end_training_guard() -> None:
    _end_maintenance("training")


def reload_model(model_path: str, wait_timeout_sec: float = 30.0) -> dict:
    """Hot-swap YOLO weights and eagerly load them."""
    global _runtime_model_override
    target = _resolve_candidate_model_path(model_path)
    if not target.exists():
        raise FileNotFoundError(f"YOLO model not found: {target}")
    if target.suffix.lower() not in {".pt", ".engine"}:
        raise ValueError("Model path must point to a .pt or .engine file")

    _begin_maintenance("reload", timeout_sec=wait_timeout_sec)
    try:
        _runtime_model_override = str(target)
        unload_current_model()
        _get_yolo_model()
        return get_runtime_status()
    finally:
        _end_maintenance("reload")


def _cuda_available() -> bool:
    try:
        import torch

        return torch.cuda.is_available()
    except Exception:
        return False


def decode_image(image_base64: str) -> Image.Image:
    raw = base64.b64decode(image_base64)
    return Image.open(io.BytesIO(raw)).convert("RGB")


def detect_image(image: Image.Image | np.ndarray, confidence_threshold: float) -> YoloResponse:
    """Run YOLO detection on a PIL image or numpy RGB array."""
    if isinstance(image, np.ndarray):
        image = Image.fromarray(image)

    with _inference_guard():
        model = _get_yolo_model()
        usable, quality_reason = _is_frame_usable(image)

        if not usable:
            return YoloResponse(
                is_relevant=False,
                detections=[],
                frame_class=quality_reason,
                inference_time_ms=0.0,
            )

        t0 = time.perf_counter()
        results = model.predict(
            source=np.array(image),
            conf=confidence_threshold,
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
                all_xyxy = boxes.xyxy.cpu().numpy()
                all_cls = boxes.cls.cpu().numpy().astype(int)
                all_conf = boxes.conf.cpu().numpy()

                for i in range(len(boxes)):
                    cls_id = int(all_cls[i])
                    conf = float(all_conf[i])
                    cls_name = result.names.get(cls_id, str(cls_id))
                    detections.append(
                        YoloDetection(
                            x1=float(all_xyxy[i, 0]),
                            y1=float(all_xyxy[i, 1]),
                            x2=float(all_xyxy[i, 2]),
                            y2=float(all_xyxy[i, 3]),
                            class_name=cls_name,
                            confidence=conf,
                        )
                    )

        if _using_custom_weights:
            is_relevant = len(detections) > 0
        else:
            is_relevant = True
            frame_class = "pipe_content" if frame_class == "empty" else frame_class

        return YoloResponse(
            is_relevant=is_relevant,
            detections=detections,
            frame_class=frame_class,
            inference_time_ms=round(elapsed_ms, 1),
        )


_rejection_stats: dict[str, int] = {
    "too_dark": 0, "too_bright": 0, "too_uniform": 0, "too_blurry": 0, "ok": 0
}


def get_rejection_stats() -> dict[str, int]:
    """Verwerfungs-Statistik fuer Telemetrie (z.B. /health Endpunkt)."""
    total = sum(_rejection_stats.values())
    rejected = total - _rejection_stats["ok"]
    return {
        **_rejection_stats,
        "total_frames": total,
        "rejected_frames": rejected,
        "rejection_rate_pct": round(rejected / total * 100, 1) if total > 0 else 0.0,
    }


def reset_rejection_stats() -> None:
    """Statistik zuruecksetzen (z.B. bei neuem Video)."""
    for k in _rejection_stats:
        _rejection_stats[k] = 0


def _is_frame_usable(img: Image.Image) -> tuple[bool, str]:
    """Check if a frame is usable for analysis using quality heuristics."""
    arr = np.array(img, dtype=np.float32)
    gray = arr.mean(axis=2)
    mean_brightness = gray.mean()
    std_brightness = gray.std()

    if mean_brightness < 5:
        _rejection_stats["too_dark"] += 1
        logger.debug("Frame verworfen: zu dunkel (brightness=%.1f)", mean_brightness)
        return False, "too_dark"
    if mean_brightness > 245:
        _rejection_stats["too_bright"] += 1
        logger.debug("Frame verworfen: zu hell (brightness=%.1f)", mean_brightness)
        return False, "too_bright"
    if std_brightness < 3:
        _rejection_stats["too_uniform"] += 1
        logger.debug("Frame verworfen: zu gleichfoermig (std=%.1f)", std_brightness)
        return False, "too_uniform"

    from scipy.ndimage import laplace

    edges = laplace(gray)
    edge_var = edges.var()
    if edge_var < 3:
        _rejection_stats["too_blurry"] += 1
        logger.debug("Frame verworfen: zu unscharf (edge_var=%.1f)", edge_var)
        return False, "too_blurry"

    _rejection_stats["ok"] += 1
    return True, "ok"


def detect(image_base64: str, confidence_threshold: float) -> YoloResponse:
    """Run YOLO detection on a base64-encoded image."""
    return detect_image(decode_image(image_base64), confidence_threshold)


def detect_batch(
    images_b64: list[str],
    confidence_threshold: float,
    frame_ids: list[str] | None = None,
) -> list[tuple[str, YoloResponse]]:
    """Batch-YOLO: mehrere Bilder in einem Forward Pass.
    Gibt Liste von (frame_id, YoloResponse) zurueck.
    """
    if frame_ids is None:
        frame_ids = [str(i) for i in range(len(images_b64))]

    images = [decode_image(b64) for b64 in images_b64]

    with _inference_guard():
        model = _get_yolo_model()
        t0 = time.perf_counter()

        # Qualitaetscheck pro Bild — unbrauchbare Frames ueberspringen
        usable_indices = []
        usable_images = []
        results_out: list[tuple[str, YoloResponse]] = [None] * len(images)  # type: ignore

        for i, img in enumerate(images):
            ok, reason = _is_frame_usable(img)
            if ok:
                usable_indices.append(i)
                usable_images.append(np.array(img))
            else:
                results_out[i] = (frame_ids[i], YoloResponse(
                    is_relevant=False, detections=[], frame_class=reason, inference_time_ms=0.0))

        if usable_images:
            # Batch-Predict: ultralytics unterstuetzt Liste als source
            batch_results = model.predict(source=usable_images, conf=confidence_threshold, verbose=False)
            elapsed_ms = (time.perf_counter() - t0) * 1000

            for batch_idx, orig_idx in enumerate(usable_indices):
                result = batch_results[batch_idx]
                detections: list[YoloDetection] = []
                frame_class = "empty"

                boxes = result.boxes
                if boxes is not None and len(boxes) > 0:
                    frame_class = "relevant"
                    all_xyxy = boxes.xyxy.cpu().numpy()
                    all_cls = boxes.cls.cpu().numpy().astype(int)
                    all_conf = boxes.conf.cpu().numpy()

                    for j in range(len(boxes)):
                        cls_id = int(all_cls[j])
                        conf = float(all_conf[j])
                        cls_name = result.names.get(cls_id, str(cls_id))
                        detections.append(YoloDetection(
                            x1=float(all_xyxy[j, 0]), y1=float(all_xyxy[j, 1]),
                            x2=float(all_xyxy[j, 2]), y2=float(all_xyxy[j, 3]),
                            class_name=cls_name, confidence=conf))

                if _using_custom_weights:
                    is_relevant = len(detections) > 0
                else:
                    is_relevant = True
                    frame_class = "pipe_content" if frame_class == "empty" else frame_class

                results_out[orig_idx] = (frame_ids[orig_idx], YoloResponse(
                    is_relevant=is_relevant, detections=detections,
                    frame_class=frame_class,
                    inference_time_ms=round(elapsed_ms / len(usable_images), 1)))
        else:
            elapsed_ms = (time.perf_counter() - t0) * 1000

        return results_out


# YOLO classify (whole-frame classifier)
_cls_model = None
_cls_lock = threading.Lock()


def _resolve_cls_model_path() -> str | None:
    project_root = Path(__file__).resolve().parent.parent.parent.parent
    candidates = [
        Path(settings.models_dir) / "yolo_cls_best.pt",
        project_root / "yolo_cls_runs" / "grundgeruest_v2" / "weights" / "best.pt",
        project_root / "yolo_cls_runs" / "grundgeruest_v1" / "weights" / "best.pt",
    ]
    for candidate in candidates:
        if candidate.exists():
            return str(candidate)
    return None


def _get_cls_model():
    global _cls_model
    if _cls_model is not None:
        return _cls_model
    with _cls_lock:
        if _cls_model is not None:
            return _cls_model
        path = _resolve_cls_model_path()
        if path is None:
            return None
        from ultralytics import YOLO

        _cls_model = YOLO(path)
        _cls_model.to("cpu")
        logger.info("YOLO classify model loaded: %s", path)
        return _cls_model


def classify(image_base64: str, top_k: int = 5) -> list[tuple[str, float]]:
    """Whole-frame classification: return top-k classes with confidence."""
    with _inference_guard():
        model = _get_cls_model()
        if model is None:
            return []

        img = decode_image(image_base64)
        t0 = time.perf_counter()
        results = model.predict(source=np.array(img), verbose=False)
        elapsed_ms = (time.perf_counter() - t0) * 1000

        if not results or len(results) == 0:
            return []

        probs = results[0].probs
        if probs is None:
            return []

        top_indices = probs.data.topk(min(top_k, len(probs.data))).indices.cpu().tolist()
        predictions = []
        for idx in top_indices:
            name = model.names.get(idx, str(idx))
            conf = float(probs.data[idx].cpu().item())
            if conf > 0.01:
                predictions.append((name, conf, elapsed_ms))
        return predictions
