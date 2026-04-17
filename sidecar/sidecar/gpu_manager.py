"""GPU Model Manager – keeps multiple models resident in VRAM simultaneously."""

from __future__ import annotations

import gc
import enum
import time
import threading
import logging
from dataclasses import dataclass, field
from typing import Any, Callable, Tuple, Optional

logger = logging.getLogger(__name__)


class ModelSlot(str, enum.Enum):
    NONE = "none"
    YOLO = "yolo"
    DINO = "dino"
    SAM = "sam"
    VSR = "vsr"
    PARSE = "parse"
    CHANGENET = "changenet"
    # V4.2 Phase 3: DINOv2 Foundation-Encoder als Ersatz fuer die tote Grounding-DINO-Kaskade.
    DINOV2 = "dinov2"


@dataclass
class SlotState:
    """State for a single loaded model slot."""
    model: Any = None
    processor: Any = None
    device: str = ""
    load_time_sec: float = 0.0


class GpuModelManager:
    """Multi-slot persistent model manager.

    Multiple models can be loaded simultaneously and remain resident.
    No automatic eviction on slot switch — models stay until explicitly
    unloaded or the process shuts down.
    """

    def __init__(self) -> None:
        self._slots: dict[ModelSlot, SlotState] = {}
        self._locks: dict[ModelSlot, threading.Lock] = {
            ModelSlot.YOLO: threading.Lock(),
            ModelSlot.DINO: threading.Lock(),
            ModelSlot.SAM: threading.Lock(),
            ModelSlot.VSR: threading.Lock(),
            ModelSlot.DINOV2: threading.Lock(),
        }
        self._global_lock = threading.Lock()

    # ── Public API ──────────────────────────────────────────────────────

    def ensure_loaded(
        self,
        slot: ModelSlot,
        device: str,
        loader: Callable[[], Tuple[Any, Optional[Any]]],
    ) -> SlotState:
        """Load *slot* on *device* if not already loaded. Returns SlotState.

        Uses double-check locking for thread safety without blocking
        concurrent access to different slots.
        """
        # Fast path: already loaded
        state = self._slots.get(slot)
        if state is not None and state.model is not None:
            return state

        # Slow path: acquire per-slot lock and load
        lock = self._locks.get(slot) or self._get_or_create_lock(slot)
        with lock:
            # Double-check after acquiring lock
            state = self._slots.get(slot)
            if state is not None and state.model is not None:
                return state

            t0 = time.perf_counter()
            model, processor = loader()
            elapsed = time.perf_counter() - t0

            state = SlotState(
                model=model,
                processor=processor,
                device=device,
                load_time_sec=elapsed,
            )
            self._slots[slot] = state
            logger.info(
                "Loaded %s in %.1fs on %s (persistent)", slot.value, elapsed, device
            )
            return state

    def unload(self, slot: ModelSlot) -> None:
        """Explicitly unload a single slot."""
        lock = self._locks.get(slot)
        if lock is None:
            return
        with lock:
            state = self._slots.pop(slot, None)
            if state is None:
                return
            logger.info("Unloading %s from %s ...", slot.value, state.device)
            del state.model
            del state.processor
        self._try_empty_cache()
        gc.collect()

    def unload_all(self) -> None:
        """Unload all loaded models (shutdown cleanup)."""
        slots = list(self._slots.keys())
        for slot in slots:
            self.unload(slot)
        logger.info("All model slots unloaded.")

    # VRAM Watermark-Schwellen
    VRAM_WARN_PERCENT = 75.0
    VRAM_ERROR_PERCENT = 90.0

    def get_available_vram_gb(self) -> float:
        """Gibt den verfuegbaren (freien) VRAM in GB zurueck."""
        try:
            import torch
            if torch.cuda.is_available():
                allocated = torch.cuda.memory_allocated(0)
                total = torch.cuda.get_device_properties(0).total_mem
                return (total - allocated) / (1024 ** 3)
        except Exception:
            pass
        return 8.0  # Konservativer Fallback

    def get_vram_utilization_percent(self) -> float:
        """Gibt die aktuelle VRAM-Auslastung in Prozent zurueck (0-100)."""
        try:
            import torch
            if torch.cuda.is_available():
                allocated = torch.cuda.memory_allocated(0)
                total = torch.cuda.get_device_properties(0).total_mem
                if total > 0:
                    return (allocated / total) * 100.0
        except Exception:
            pass
        return 0.0

    def check_vram_health(self) -> str:
        """Prueft VRAM-Auslastung und gibt Status zurueck: ok/warning/critical.
        Loggt Warnungen bei Ueberschreitung der Schwellen.
        """
        pct = self.get_vram_utilization_percent()
        if pct >= self.VRAM_ERROR_PERCENT:
            logger.error(
                "VRAM KRITISCH: %.1f%% belegt (Schwelle: %.0f%%) — OOM-Risiko!",
                pct, self.VRAM_ERROR_PERCENT
            )
            return "critical"
        elif pct >= self.VRAM_WARN_PERCENT:
            logger.warning(
                "VRAM Warnung: %.1f%% belegt (Schwelle: %.0f%%)",
                pct, self.VRAM_WARN_PERCENT
            )
            return "warning"
        return "ok"

    def get_status(self) -> dict:
        """Return status dict for /health endpoint.

        Keeps legacy keys (current_model, vram_allocated_gb, vram_total_gb)
        for backwards compatibility and adds loaded_models detail.
        Neu: vram_utilization_percent und vram_health fuer VRAM-Monitoring.
        """
        vram_allocated = 0.0
        vram_total = 0.0
        vram_free_mb = 0
        vram_utilization = 0.0
        try:
            import torch
            if torch.cuda.is_available():
                vram_allocated = torch.cuda.memory_allocated(0) / (1024**3)
                vram_total = torch.cuda.get_device_properties(0).total_mem / (1024**3)
                free_bytes, _ = torch.cuda.mem_get_info(0)
                vram_free_mb = int(free_bytes / (1024**2))
                if vram_total > 0:
                    vram_utilization = (vram_allocated / vram_total) * 100.0
        except Exception:
            pass

        loaded = {}
        for slot, state in self._slots.items():
            if state.model is not None:
                loaded[slot.value] = {
                    "device": state.device,
                    "load_time_sec": round(state.load_time_sec, 2),
                }

        # Legacy compat: report first loaded model or "none"
        loaded_names = [s.value for s in self._slots if self._slots[s].model is not None]
        current = loaded_names[0] if loaded_names else "none"

        # Always-On Pipeline: pruefen ob YOLO+DINO+SAM geladen sind
        core_slots = {ModelSlot.YOLO, ModelSlot.DINO, ModelSlot.SAM}
        all_core_loaded = all(
            self._slots.get(s) is not None and self._slots[s].model is not None
            for s in core_slots
        )

        # VRAM Health-Check (loggt Warnungen bei Ueberschreitung)
        vram_health = self.check_vram_health()

        return {
            "current_model": current,
            "vram_allocated_gb": round(vram_allocated, 2),
            "vram_total_gb": round(vram_total, 2),
            "vram_free_mb": vram_free_mb,
            "vram_utilization_percent": round(vram_utilization, 1),
            "vram_health": vram_health,
            "all_resident": all_core_loaded,
            "prewarm_done": all_core_loaded,
            "load_times_sec": {
                s.value: round(st.load_time_sec, 2)
                for s, st in self._slots.items()
                if st.model is not None
            },
            "loaded_models": loaded,
        }

    # ── Internal ────────────────────────────────────────────────────────

    def _get_or_create_lock(self, slot: ModelSlot) -> threading.Lock:
        with self._global_lock:
            if slot not in self._locks:
                self._locks[slot] = threading.Lock()
            return self._locks[slot]

    @staticmethod
    def _try_empty_cache() -> None:
        try:
            import torch
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
        except Exception:
            pass


# Singleton
gpu_manager = GpuModelManager()
