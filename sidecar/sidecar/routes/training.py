"""Training endpoints (dataset export, YOLO train jobs, model reload)."""

from __future__ import annotations

import base64
import io
import logging
import random
import threading
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

from fastapi import APIRouter, HTTPException
from PIL import Image

from ..config import settings
from ..gpu_manager import ModelSlot, gpu_manager
from ..models import yolo_wrapper
from ..schemas.segmentation import (
    ModelReloadRequest,
    ModelReloadResponse,
    TrainingExportRequest,
    TrainingExportResponse,
    YoloDatasetQuality,
    YoloTrainJobResponse,
    YoloTrainJobStatusResponse,
    YoloTrainMetrics,
    YoloTrainRequest,
)

router = APIRouter()
logger = logging.getLogger(__name__)


@dataclass
class _TrainJobState:
    job_id: str
    status: str = "queued"
    message: str = ""
    error: str | None = None
    model_path: str | None = None
    metrics: dict | None = None
    dataset_quality: dict | None = None
    epochs_completed: int = 0
    started_utc: datetime | None = None
    finished_utc: datetime | None = None


_jobs: dict[str, _TrainJobState] = {}
_jobs_lock = threading.Lock()
_active_job_id: str | None = None


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


def _resolve_path(path_like: str) -> Path:
    path = Path(path_like).expanduser()
    if not path.is_absolute():
        path = Path.cwd() / path
    return path.resolve()


def _update_job(job_id: str, **changes) -> _TrainJobState | None:
    with _jobs_lock:
        job = _jobs.get(job_id)
        if job is None:
            return None
        for key, value in changes.items():
            setattr(job, key, value)
        return job


def _get_job(job_id: str) -> _TrainJobState | None:
    with _jobs_lock:
        return _jobs.get(job_id)


def _job_to_response(job: _TrainJobState) -> YoloTrainJobStatusResponse:
    return YoloTrainJobStatusResponse(
        job_id=job.job_id,
        status=job.status,
        message=job.message,
        error=job.error,
        model_path=job.model_path,
        metrics=YoloTrainMetrics(**job.metrics) if job.metrics else None,
        dataset_quality=YoloDatasetQuality(**job.dataset_quality)
        if job.dataset_quality
        else None,
        epochs_completed=job.epochs_completed,
        started_utc=job.started_utc,
        finished_utc=job.finished_utc,
    )


def _extract_metric(raw: dict, *keys: str) -> float:
    for key in keys:
        if key in raw:
            try:
                return float(raw[key])
            except Exception:
                continue
    return 0.0


def _extract_train_metrics(model) -> dict:
    metrics_dict: dict = {}
    try:
        trainer = getattr(model, "trainer", None)
        trainer_metrics = getattr(trainer, "metrics", None)
        if trainer_metrics is not None:
            results_dict = getattr(trainer_metrics, "results_dict", None)
            if isinstance(results_dict, dict):
                metrics_dict = dict(results_dict)
        if not metrics_dict and hasattr(model, "metrics"):
            candidate = getattr(model, "metrics")
            if isinstance(candidate, dict):
                metrics_dict = dict(candidate)
    except Exception:
        pass

    precision = _extract_metric(
        metrics_dict, "metrics/precision(B)", "metrics/precision"
    )
    recall = _extract_metric(metrics_dict, "metrics/recall(B)", "metrics/recall")
    f1 = _extract_metric(metrics_dict, "metrics/f1(B)", "metrics/f1")
    map50 = _extract_metric(
        metrics_dict, "metrics/mAP50(B)", "metrics/mAP50", "metrics/mAP_0.5"
    )
    map50_95 = _extract_metric(
        metrics_dict, "metrics/mAP50-95(B)", "metrics/mAP50-95", "metrics/mAP_0.5:0.95"
    )

    return {
        "precision": round(precision, 4),
        "recall": round(recall, 4),
        "f1": round(f1, 4),
        "map50": round(map50, 4),
        "map50_95": round(map50_95, 4),
    }


def _is_fallback_box(
    x_center: float, y_center: float, width: float, height: float
) -> bool:
    return (
        abs(x_center - 0.5) <= 0.02
        and abs(y_center - 0.5) <= 0.02
        and width >= 0.75
        and height >= 0.75
    )


def _inspect_dataset_quality(data_yaml_path: Path) -> dict:
    dataset_root = data_yaml_path.parent
    image_count = 0
    for split in ("train", "val"):
        image_dir = dataset_root / "images" / split
        if not image_dir.exists():
            continue
        image_count += sum(
            1
            for p in image_dir.iterdir()
            if p.is_file() and p.suffix.lower() in {".jpg", ".jpeg", ".png", ".bmp"}
        )

    total_labels = 0
    fallback_labels = 0
    classes: set[int] = set()
    for split in ("train", "val"):
        label_dir = dataset_root / "labels" / split
        if not label_dir.exists():
            continue
        for label_file in label_dir.glob("*.txt"):
            try:
                lines = label_file.read_text(encoding="utf-8").splitlines()
            except Exception:
                continue
            for line in lines:
                parts = line.strip().split()
                if len(parts) != 5:
                    continue
                try:
                    class_id = int(float(parts[0]))
                    x_center = float(parts[1])
                    y_center = float(parts[2])
                    width = float(parts[3])
                    height = float(parts[4])
                except Exception:
                    continue
                classes.add(class_id)
                total_labels += 1
                if _is_fallback_box(x_center, y_center, width, height):
                    fallback_labels += 1

    fallback_ratio = (fallback_labels / total_labels) if total_labels > 0 else 1.0
    return {
        "total_samples": image_count,
        "total_labels": total_labels,
        "fallback_labels": fallback_labels,
        "fallback_ratio": round(fallback_ratio, 4),
        "distinct_classes": len(classes),
    }


def _unload_models_for_training() -> None:
    logger.info("YOLO training: unloading YOLO + DINO + SAM for VRAM protection")
    try:
        yolo_wrapper.unload_current_model()
    except Exception as exc:
        logger.warning("Could not unload YOLO before training: %s", exc)
    for slot in (ModelSlot.DINO, ModelSlot.SAM):
        try:
            gpu_manager.unload(slot)
        except Exception as exc:
            logger.warning("Could not unload %s before training: %s", slot.value, exc)


def _restore_models_after_training() -> None:
    logger.info("YOLO training: restoring DINO + SAM after training")
    try:
        from ..models.dino_wrapper import (
            _load_dino_on,
            _resolve_device as _resolve_dino_device,
        )

        dino_device = _resolve_dino_device()
        gpu_manager.ensure_loaded(
            ModelSlot.DINO, dino_device, lambda: _load_dino_on(dino_device)
        )
    except Exception as exc:
        logger.warning("Could not restore DINO after training: %s", exc)

    try:
        from ..models.sam_wrapper import (
            _load_sam2_on,
            _resolve_device as _resolve_sam_device,
        )

        sam_device = _resolve_sam_device()
        gpu_manager.ensure_loaded(
            ModelSlot.SAM, sam_device, lambda: _load_sam2_on(sam_device)
        )
    except Exception as exc:
        logger.warning("Could not restore SAM after training: %s", exc)


def _run_training_job(job_id: str, req: YoloTrainRequest, data_yaml_path: Path) -> None:
    global _active_job_id
    try:
        _update_job(
            job_id,
            status="running",
            started_utc=_utc_now(),
            message="YOLO training started",
        )

        quality = _inspect_dataset_quality(data_yaml_path)
        if quality["total_samples"] == 0:
            raise ValueError("Dataset empty: no images found beside data.yaml")
        if quality["total_labels"] == 0:
            raise ValueError("Dataset invalid: no YOLO labels found")
        if quality["fallback_ratio"] > req.max_fallback_ratio:
            raise ValueError(
                "Dataset rejected by quality gate: fallback ratio "
                f"{quality['fallback_ratio']:.1%} > {req.max_fallback_ratio:.1%}"
            )
        _update_job(job_id, dataset_quality=quality)

        _unload_models_for_training()

        from ultralytics import YOLO

        base_model = req.base_model
        if base_model.endswith(".pt") or base_model.endswith(".engine"):
            base_path = _resolve_path(base_model)
            if base_path.exists():
                base_model = str(base_path)

        project_path = _resolve_path(req.project)
        project_path.mkdir(parents=True, exist_ok=True)

        model = YOLO(base_model)

        train_kwargs = {
            "data": str(data_yaml_path),
            "epochs": req.epochs,
            "imgsz": req.imgsz,
            "batch": req.batch,
            "project": str(project_path),
            "amp": req.amp,
            "exist_ok": False,
            "verbose": True,
        }
        if settings.effective_yolo_device:
            train_kwargs["device"] = settings.effective_yolo_device

        logger.info(
            "Starting YOLO train job %s: data=%s, epochs=%d, imgsz=%d, batch=%d, base=%s",
            job_id,
            data_yaml_path,
            req.epochs,
            req.imgsz,
            req.batch,
            base_model,
        )
        model.train(**train_kwargs)

        trainer = getattr(model, "trainer", None)
        save_dir = Path(getattr(trainer, "save_dir", project_path))
        best_path = save_dir / "weights" / "best.pt"
        last_path = save_dir / "weights" / "last.pt"
        model_path = best_path if best_path.exists() else last_path
        if not model_path.exists():
            raise RuntimeError(
                f"Training completed but no weights found in {save_dir / 'weights'}"
            )

        epoch_raw = getattr(trainer, "epoch", req.epochs - 1)
        try:
            epochs_completed = int(epoch_raw) + 1
        except Exception:
            epochs_completed = req.epochs
        epochs_completed = max(1, min(epochs_completed, req.epochs))

        metrics = _extract_train_metrics(model)
        resolved_model = str(model_path.resolve())

        # End training guard before reload so reload_model can acquire its own lock
        try:
            yolo_wrapper.end_training_guard()
        except Exception:
            pass

        # Auto-reload: hot-swap inference model with new best weights
        try:
            logger.info(
                "YOLO train job %s: auto-reloading inference model with %s",
                job_id,
                resolved_model,
            )
            yolo_wrapper.reload_model(model_path=resolved_model, wait_timeout_sec=30.0)
            logger.info(
                "YOLO train job %s: inference model reloaded successfully", job_id
            )
        except Exception as reload_exc:
            logger.warning(
                "YOLO train job %s: auto-reload failed (%s), manual /model/reload required",
                job_id,
                reload_exc,
            )

        _update_job(
            job_id,
            status="completed",
            message="YOLO training completed",
            model_path=resolved_model,
            metrics=metrics,
            epochs_completed=epochs_completed,
            finished_utc=_utc_now(),
        )
        logger.info("YOLO train job %s completed: %s", job_id, model_path)
    except Exception as exc:
        logger.exception("YOLO train job %s failed", job_id)
        _update_job(
            job_id,
            status="failed",
            message="YOLO training failed",
            error=str(exc),
            finished_utc=_utc_now(),
        )
    finally:
        _restore_models_after_training()
        try:
            yolo_wrapper.end_training_guard()
        except Exception:
            pass
        with _jobs_lock:
            if _active_job_id == job_id:
                _active_job_id = None


@router.post("/training/export-yolo", response_model=TrainingExportResponse)
async def export_yolo(req: TrainingExportRequest) -> TrainingExportResponse:
    """Export training samples to YOLO format (images + labels + data.yaml)."""
    out = Path(req.output_dir)
    img_train = out / "images" / "train"
    img_val = out / "images" / "val"
    lbl_train = out / "labels" / "train"
    lbl_val = out / "labels" / "val"

    for d in [img_train, img_val, lbl_train, lbl_val]:
        d.mkdir(parents=True, exist_ok=True)

    class_set: set[str] = set()
    for sample in req.samples:
        for lbl in sample.labels:
            class_set.add(lbl.get("class_name", "defect"))
    class_list = sorted(class_set)
    class_map = {name: idx for idx, name in enumerate(class_list)}

    indices = list(range(len(req.samples)))
    random.shuffle(indices)
    split_idx = int(len(indices) * req.train_split)
    train_indices = set(indices[:split_idx])

    train_count = 0
    val_count = 0

    skipped = 0
    for i, sample in enumerate(req.samples):
        is_train = i in train_indices
        img_dir = img_train if is_train else img_val
        lbl_dir = lbl_train if is_train else lbl_val

        try:
            raw = base64.b64decode(sample.image_base64)
            img = Image.open(io.BytesIO(raw)).convert("RGB")
        except Exception as exc:
            logger.error("export_yolo: sample %d has invalid image data: %s", i, exc)
            skipped += 1
            continue

        img_path = img_dir / f"sample_{i:06d}.jpg"
        img.save(str(img_path), "JPEG", quality=95)

        lbl_path = lbl_dir / f"sample_{i:06d}.txt"
        lines: list[str] = []
        for lbl in sample.labels:
            cls_name = lbl.get("class_name", "defect")
            cls_idx = class_map.get(cls_name, 0)
            xc = lbl.get("x_center", 0.5)
            yc = lbl.get("y_center", 0.5)
            w = lbl.get("width", 0.1)
            h = lbl.get("height", 0.1)
            lines.append(f"{cls_idx} {xc:.6f} {yc:.6f} {w:.6f} {h:.6f}")
        lbl_path.write_text("\n".join(lines), encoding="utf-8")

        if is_train:
            train_count += 1
        else:
            val_count += 1

    if skipped > 0:
        logger.warning(
            "export_yolo: %d of %d samples skipped due to invalid image data",
            skipped,
            len(req.samples),
        )

    data_yaml = out / "data.yaml"
    yaml_lines = [
        f"path: {out.resolve()}",
        "train: images/train",
        "val: images/val",
        f"nc: {len(class_list)}",
        f"names: {class_list}",
    ]
    data_yaml.write_text("\n".join(yaml_lines), encoding="utf-8")

    return TrainingExportResponse(
        total_samples=len(req.samples),
        train_count=train_count,
        val_count=val_count,
        classes_used=class_list,
        data_yaml_path=str(data_yaml.resolve()),
    )


@router.post("/training/train-yolo", response_model=YoloTrainJobResponse)
async def train_yolo(req: YoloTrainRequest) -> YoloTrainJobResponse:
    """Start asynchronous YOLO training job in a background thread."""
    global _active_job_id
    data_yaml = _resolve_path(req.dataset_path)
    if not data_yaml.exists():
        raise HTTPException(
            status_code=404, detail=f"dataset_path not found: {data_yaml}"
        )

    with _jobs_lock:
        if _active_job_id is not None:
            active = _jobs.get(_active_job_id)
            if active is not None and active.status in {"queued", "running"}:
                raise HTTPException(
                    status_code=409,
                    detail=f"training job already running: {_active_job_id}",
                )

    try:
        # Hard safety gate: only train when no active analysis is running.
        yolo_wrapper.begin_training_guard(timeout_sec=0.1)
    except TimeoutError as exc:
        raise HTTPException(
            status_code=409,
            detail="active YOLO inference detected - training can only start when analysis is idle",
        ) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc

    job_id = str(uuid.uuid4())
    state = _TrainJobState(
        job_id=job_id,
        status="queued",
        message="YOLO training queued",
    )

    with _jobs_lock:
        _jobs[job_id] = state
        _active_job_id = job_id

    worker = threading.Thread(
        target=_run_training_job,
        args=(job_id, req, data_yaml),
        daemon=True,
        name=f"yolo-train-{job_id[:8]}",
    )
    try:
        worker.start()
    except Exception:
        with _jobs_lock:
            _jobs.pop(job_id, None)
            if _active_job_id == job_id:
                _active_job_id = None
        yolo_wrapper.end_training_guard()
        raise

    return YoloTrainJobResponse(
        job_id=job_id,
        status="queued",
        message="YOLO training started in background",
    )


@router.get("/training/jobs/{job_id}", response_model=YoloTrainJobStatusResponse)
async def get_train_job(job_id: str) -> YoloTrainJobStatusResponse:
    job = _get_job(job_id)
    if job is None:
        raise HTTPException(status_code=404, detail=f"job not found: {job_id}")
    return _job_to_response(job)


@router.post("/model/reload", response_model=ModelReloadResponse)
async def reload_model(req: ModelReloadRequest) -> ModelReloadResponse:
    """Hot-swap active YOLO model without sidecar restart."""
    try:
        status = yolo_wrapper.reload_model(
            model_path=req.model_path,
            wait_timeout_sec=req.wait_timeout_sec,
        )
    except TimeoutError as exc:
        raise HTTPException(
            status_code=409,
            detail="reload blocked by active inference",
        ) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc
    except FileNotFoundError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc

    return ModelReloadResponse(
        status="ok",
        resolved_model_path=str(status.get("resolved_model_path", "")),
        tensorrt_active=bool(status.get("tensorrt_active", False)),
    )
