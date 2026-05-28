"""Append-only JSONL telemetry for sidecar requests."""

from __future__ import annotations

import json
import logging
import os
import threading
from datetime import datetime, timezone
from pathlib import Path

from .config import settings
from .schemas.detection import YoloResponse

logger = logging.getLogger(__name__)
_write_lock = threading.Lock()


def telemetry_path() -> Path:
    configured_dir = (settings.telemetry_dir or "").strip()
    if configured_dir:
        telemetry_dir = Path(configured_dir)
    else:
        local_app_data = os.environ.get("LOCALAPPDATA")
        if local_app_data:
            telemetry_dir = Path(local_app_data) / "SewerStudio" / "Telemetry"
        else:
            telemetry_dir = Path.home() / ".sewerstudio" / "Telemetry"

    return telemetry_dir / "sidecar.jsonl"


def write_yolo_detection(
    response: YoloResponse,
    *,
    confidence_threshold: float,
    roundtrip_ms: float,
) -> None:
    if not settings.telemetry_enabled:
        return

    try:
        event = {
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            "event": "yolo_detect",
            "model_name": response.model_name,
            "backend": _backend(response),
            "device": response.device,
            "roundtrip_ms": round(roundtrip_ms, 1),
            "inference_time_ms": response.inference_time_ms,
            "queue_wait_ms": response.queue_wait_ms,
            "gpu_utilization_percent": response.gpu_utilization_percent,
            "vram_allocated_gb": response.vram_allocated_gb,
            "vram_total_gb": response.vram_total_gb,
            "detection_count": len(response.detections or []),
            "confidence_threshold": confidence_threshold,
            "frame_class": response.frame_class,
            "is_relevant": response.is_relevant,
        }

        path = telemetry_path()
        line = json.dumps(event, ensure_ascii=False, separators=(",", ":"))

        with _write_lock:
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("a", encoding="utf-8") as handle:
                handle.write(line)
                handle.write("\n")
    except Exception as exc:
        logger.warning("Could not write sidecar telemetry: %s", exc)


def _backend(response: YoloResponse) -> str | None:
    if response.model_backend:
        return response.model_backend
    if response.device == "cpu":
        return "cpu"
    return response.device
