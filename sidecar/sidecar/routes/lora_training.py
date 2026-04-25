"""LoRA fine-tuning endpoints for Qwen Vision-Language models.

Trains a LoRA adapter using unsloth, then deploys it via Ollama Modelfile.
Training runs in a background thread — same pattern as YOLO training.
"""

from __future__ import annotations

import json
import logging
import subprocess
import threading
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from tempfile import TemporaryDirectory

from fastapi import APIRouter, HTTPException

from ..gpu_manager import ModelSlot, gpu_manager
from ..schemas.segmentation import (
    LoraDeployRequest,
    LoraDeployResponse,
    LoraTrainJobResponse,
    LoraTrainJobStatusResponse,
    LoraTrainMetrics,
    LoraTrainRequest,
)

router = APIRouter()
logger = logging.getLogger(__name__)


@dataclass
class _LoraJobState:
    job_id: str
    status: str = "queued"
    message: str = ""
    error: str | None = None
    adapter_path: str | None = None
    metrics: dict | None = None
    started_utc: datetime | None = None
    finished_utc: datetime | None = None


_jobs: dict[str, _LoraJobState] = {}
_jobs_lock = threading.Lock()
_active_job_id: str | None = None


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


def _update_job(job_id: str, **changes) -> _LoraJobState | None:
    with _jobs_lock:
        job = _jobs.get(job_id)
        if job is None:
            return None
        for key, value in changes.items():
            setattr(job, key, value)
        return job


def _get_job(job_id: str) -> _LoraJobState | None:
    with _jobs_lock:
        return _jobs.get(job_id)


def _unload_models_for_training() -> None:
    """VRAM freigeben: DINO + SAM + YOLO entladen."""
    logger.info("LoRA training: unloading all GPU models for VRAM")
    from ..models import yolo_wrapper

    try:
        yolo_wrapper.unload_current_model()
    except Exception as exc:
        logger.warning("Could not unload YOLO: %s", exc)
    for slot in (ModelSlot.DINO, ModelSlot.SAM):
        try:
            gpu_manager.unload(slot)
        except Exception as exc:
            logger.warning("Could not unload %s: %s", slot.value, exc)


def _restore_models_after_training() -> None:
    """DINO + SAM nach Training wieder laden."""
    logger.info("LoRA training: restoring DINO + SAM")
    try:
        from ..models.dino_wrapper import _load_dino_on, _resolve_device as _dino_dev

        dev = _dino_dev()
        gpu_manager.ensure_loaded(ModelSlot.DINO, dev, lambda: _load_dino_on(dev))
    except Exception as exc:
        logger.warning("Could not restore DINO: %s", exc)
    try:
        from ..models.sam_wrapper import _load_sam2_on, _resolve_device as _sam_dev

        dev = _sam_dev()
        gpu_manager.ensure_loaded(ModelSlot.SAM, dev, lambda: _load_sam2_on(dev))
    except Exception as exc:
        logger.warning("Could not restore SAM: %s", exc)


def _prepare_dataset(samples: list, output_dir: Path) -> Path:
    """Konvertiert LoRA-Samples in JSONL-Format fuer unsloth/SFTTrainer."""
    output_dir.mkdir(parents=True, exist_ok=True)
    dataset_path = output_dir / "train.jsonl"

    with open(dataset_path, "w", encoding="utf-8") as f:
        for i, sample in enumerate(samples):
            entry = {
                "messages": [
                    {
                        "role": "user",
                        "content": sample.prompt,
                        "images": [f"sample_{i:06d}.jpg"],
                    },
                    {
                        "role": "assistant",
                        "content": sample.expected_response,
                    },
                ]
            }
            f.write(json.dumps(entry, ensure_ascii=False) + "\n")

            # Bild speichern
            import base64
            from io import BytesIO

            from PIL import Image

            raw = base64.b64decode(sample.image_base64)
            img = Image.open(BytesIO(raw)).convert("RGB")
            img.save(str(output_dir / f"sample_{i:06d}.jpg"), "JPEG", quality=95)

    logger.info("LoRA dataset prepared: %d samples -> %s", len(samples), dataset_path)
    return dataset_path


def _run_lora_training(job_id: str, req: LoraTrainRequest) -> None:
    """Hintergrund-Thread: LoRA-Training mit unsloth."""
    global _active_job_id
    try:
        _update_job(
            job_id,
            status="running",
            started_utc=_utc_now(),
            message="LoRA training gestartet",
        )

        if len(req.samples) < 10:
            raise ValueError(f"Zu wenig Samples ({len(req.samples)}), Minimum 10")

        output_dir = Path(req.output_dir).resolve()
        output_dir.mkdir(parents=True, exist_ok=True)

        # Dataset vorbereiten
        _update_job(job_id, message="Dataset wird vorbereitet...")
        dataset_dir = output_dir / "dataset"
        _prepare_dataset(req.samples, dataset_dir)

        # GPU-Modelle entladen fuer VRAM
        _update_job(job_id, message="GPU-Modelle werden entladen...")
        _unload_models_for_training()

        # LoRA-Training mit unsloth
        _update_job(job_id, message="LoRA-Training laeuft...")

        try:
            from unsloth import FastVisionModel
        except ImportError:
            raise RuntimeError(
                "unsloth nicht installiert. Installiere mit: "
                "pip install unsloth[colab-new]"
            )

        model, tokenizer = FastVisionModel.from_pretrained(
            req.base_model,
            load_in_4bit=True,
            dtype=None,
        )

        model = FastVisionModel.get_peft_model(
            model,
            r=req.lora_rank,
            lora_alpha=req.lora_alpha,
            target_modules=[
                "q_proj", "k_proj", "v_proj", "o_proj",
                "gate_proj", "up_proj", "down_proj",
            ],
            lora_dropout=0.05,
        )

        # Dataset laden
        from datasets import load_dataset

        dataset = load_dataset(
            "json",
            data_files=str(dataset_dir / "train.jsonl"),
            split="train",
        )

        from trl import SFTTrainer, SFTConfig

        trainer = SFTTrainer(
            model=model,
            tokenizer=tokenizer,
            train_dataset=dataset,
            args=SFTConfig(
                output_dir=str(output_dir / "checkpoints"),
                num_train_epochs=req.epochs,
                per_device_train_batch_size=req.batch_size,
                learning_rate=req.learning_rate,
                fp16=True,
                logging_steps=10,
                save_strategy="epoch",
                max_seq_length=req.max_seq_length,
                dataset_text_field="",
            ),
        )

        train_result = trainer.train()

        # Adapter speichern
        adapter_path = output_dir / "adapter"
        model.save_pretrained(str(adapter_path))
        tokenizer.save_pretrained(str(adapter_path))

        metrics = {
            "train_loss": round(train_result.training_loss, 4),
            "eval_loss": 0.0,
            "epochs_completed": req.epochs,
            "samples_trained": len(req.samples),
        }

        _update_job(
            job_id,
            status="completed",
            message="LoRA-Training abgeschlossen",
            adapter_path=str(adapter_path),
            metrics=metrics,
            finished_utc=_utc_now(),
        )
        logger.info("LoRA train job %s completed: %s", job_id, adapter_path)

    except Exception as exc:
        logger.exception("LoRA train job %s failed", job_id)
        _update_job(
            job_id,
            status="failed",
            message="LoRA-Training fehlgeschlagen",
            error=str(exc),
            finished_utc=_utc_now(),
        )
    finally:
        _restore_models_after_training()
        # Training-Guard freigeben
        try:
            from ..models import yolo_wrapper
            yolo_wrapper.end_training_guard()
        except Exception:
            pass
        with _jobs_lock:
            if _active_job_id == job_id:
                _active_job_id = None


@router.post("/training/train-lora", response_model=LoraTrainJobResponse)
async def train_lora(req: LoraTrainRequest) -> LoraTrainJobResponse:
    """Start asynchronous LoRA fine-tuning job."""
    global _active_job_id

    if len(req.samples) < 10:
        raise HTTPException(
            status_code=400,
            detail=f"Minimum 10 Samples, erhalten: {len(req.samples)}",
        )

    with _jobs_lock:
        if _active_job_id is not None:
            active = _jobs.get(_active_job_id)
            if active is not None and active.status in {"queued", "running"}:
                raise HTTPException(
                    status_code=409,
                    detail=f"Training job already running: {_active_job_id}",
                )

    # B4/B5 Fix: Guard + Lock atomar — verhindert Race und Deadlock bei Start-Fehler
    from ..models import yolo_wrapper

    job_id = str(uuid.uuid4())
    state = _LoraJobState(job_id=job_id, status="queued", message="LoRA training queued")

    with _jobs_lock:
        # Guard innerhalb des Locks — verhindert Race (B5)
        try:
            yolo_wrapper.begin_training_guard(timeout_sec=0.1)
        except Exception:
            raise HTTPException(
                status_code=409,
                detail="Training oder Inferenz laeuft bereits — LoRA kann nicht starten",
            )

        _jobs[job_id] = state
        _active_job_id = job_id

    worker = threading.Thread(
        target=_run_lora_training,
        args=(job_id, req),
        daemon=True,
        name=f"lora-train-{job_id[:8]}",
    )
    try:
        worker.start()
    except Exception:
        # Guard freigeben bei Thread-Start-Fehler (B4)
        try:
            yolo_wrapper.end_training_guard()
        except Exception:
            pass
        with _jobs_lock:
            _jobs.pop(job_id, None)
            if _active_job_id == job_id:
                _active_job_id = None
        raise

    return LoraTrainJobResponse(
        job_id=job_id, status="queued", message="LoRA training started in background"
    )


@router.get("/training/lora-jobs/{job_id}", response_model=LoraTrainJobStatusResponse)
async def get_lora_job(job_id: str) -> LoraTrainJobStatusResponse:
    """Fetch status for a running/completed LoRA training job."""
    job = _get_job(job_id)
    if job is None:
        raise HTTPException(status_code=404, detail=f"job not found: {job_id}")
    return LoraTrainJobStatusResponse(
        job_id=job.job_id,
        status=job.status,
        message=job.message,
        error=job.error,
        adapter_path=job.adapter_path,
        metrics=LoraTrainMetrics(**job.metrics) if job.metrics else None,
        started_utc=job.started_utc,
        finished_utc=job.finished_utc,
    )


@router.post("/training/deploy-lora", response_model=LoraDeployResponse)
async def deploy_lora(req: LoraDeployRequest) -> LoraDeployResponse:
    """Deploy LoRA adapter via Ollama Modelfile."""
    adapter_path = Path(req.adapter_path).resolve()
    # S1 Fix + Audit 2026-04-25 L2: Path Traversal verhindern.
    # str.startswith() ist prefix-bypassbar (z.B. allowed=sidecar, target=sidecar_evil
    # passt). Path.relative_to() loest beide Pfade auf und wirft ValueError wenn
    # adapter_path nicht echter Nachfahre von allowed_root ist — sicher.
    allowed_root = Path(__file__).resolve().parent.parent.parent  # sidecar/
    try:
        adapter_path.relative_to(allowed_root)
    except ValueError:
        raise HTTPException(
            status_code=403,
            detail=f"Adapter-Pfad ausserhalb des erlaubten Verzeichnisses: {adapter_path}",
        )
    if not adapter_path.exists():
        raise HTTPException(status_code=404, detail=f"Adapter not found: {adapter_path}")

    # Modelfile erstellen
    modelfile_content = (
        f"FROM {req.base_model}\n"
        f"ADAPTER {adapter_path}\n"
        f"PARAMETER num_ctx 32768\n"
    )

    try:
        import httpx

        with httpx.Client(base_url=req.ollama_base_url, timeout=300) as client:
            # Modelfile via Ollama API erstellen
            resp = client.post(
                "/api/create",
                json={
                    "name": req.model_name,
                    "modelfile": modelfile_content,
                },
            )
            if resp.status_code != 200:
                raise RuntimeError(f"Ollama create failed: {resp.status_code} {resp.text}")

        logger.info(
            "LoRA adapter deployed as '%s' via Ollama Modelfile", req.model_name
        )
        return LoraDeployResponse(
            status="ok",
            model_name=req.model_name,
            message=f"Adapter deployed as '{req.model_name}'",
        )

    except Exception as exc:
        logger.exception("LoRA deploy failed")
        raise HTTPException(status_code=500, detail=str(exc)) from exc
