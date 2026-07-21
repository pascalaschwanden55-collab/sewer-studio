"""GPU Model Manager – keeps multiple models resident in VRAM simultaneously."""

from __future__ import annotations

import gc
import os
import enum
import time
import threading
import logging
from dataclasses import dataclass, field
from typing import Any, Callable, Tuple, Optional

logger = logging.getLogger(__name__)

# VRAM-Budget (GB): YOLO/DINO/SAM bleiben bewusst gleichzeitig resident (Tempo). Auf der 32-GB-Karte
# traegt das; das Budget macht die Grenze im Code SICHTBAR (Warnung) und erlaubt LRU-Eviction bei
# Bedarf, statt sich nur auf grosszuegige Hardware zu verlassen. (Audit R8)
VRAM_BUDGET_GB = float(os.environ.get("SEWER_SIDECAR_VRAM_BUDGET_GB", "29"))


class ModelSlot(str, enum.Enum):
    NONE = "none"
    YOLO = "yolo"
    DINO = "dino"
    SAM = "sam"


@dataclass
class SlotState:
    """State for a single loaded model slot."""
    model: Any = None
    processor: Any = None
    device: str = ""
    load_time_sec: float = 0.0
    last_used: float = 0.0   # time.monotonic() der letzten Nutzung (fuer LRU-Eviction)


class GpuModelManager:
    """Multi-slot persistent model manager.

    YOLO/DINO/SAM koennen gleichzeitig resident bleiben (bewusst, fuer Tempo). KEINE
    automatische Eviction beim Slot-Wechsel. Ein konfigurierbares VRAM-Budget
    (VRAM_BUDGET_GB) macht die Grenze sichtbar (Warnung beim Ueberschreiten); ueber
    evict_lru() ist LRU-Eviction moeglich (z.B. nach OOM). (Audit R8)
    """

    def __init__(self) -> None:
        self._slots: dict[ModelSlot, SlotState] = {}
        self._locks: dict[ModelSlot, threading.Lock] = {
            ModelSlot.YOLO: threading.Lock(),
            ModelSlot.DINO: threading.Lock(),
            ModelSlot.SAM: threading.Lock(),
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
            state.last_used = time.monotonic()
            return state

        # Slow path: acquire per-slot lock and load
        lock = self._locks.get(slot) or self._get_or_create_lock(slot)
        with lock:
            # Double-check after acquiring lock
            state = self._slots.get(slot)
            if state is not None and state.model is not None:
                state.last_used = time.monotonic()
                return state

            t0 = time.perf_counter()
            model, processor = loader()
            elapsed = time.perf_counter() - t0

            state = SlotState(
                model=model,
                processor=processor,
                device=device,
                load_time_sec=elapsed,
                last_used=time.monotonic(),
            )
            with self._global_lock:
                self._slots[slot] = state
            logger.info(
                "Loaded %s in %.1fs on %s (persistent)", slot.value, elapsed, device
            )
            self._warn_if_over_budget()
            return state

    def unload(self, slot: ModelSlot) -> None:
        """Explicitly unload a single slot."""
        lock = self._locks.get(slot)
        if lock is None:
            return
        with lock:
            with self._global_lock:
                state = self._slots.pop(slot, None)
            if state is None:
                return
            logger.info("Unloading %s from %s ...", slot.value, state.device)
            # Referenzen auf None setzen (nicht del): haelt ein anderer Thread die SlotState ueber
            # den lockfreien Fast-Path noch, faellt der Zugriff auf einen definierten None-Wert statt
            # auf ein geloeschtes Attribut. Das Modell wird per Refcount freigegeben, sobald die
            # letzte Referenz faellt.
            state.model = None
            state.processor = None
        self._try_empty_cache()
        gc.collect()

    def unload_all(self) -> None:
        """Unload all loaded models (shutdown cleanup)."""
        slots = list(self._slots.keys())
        for slot in slots:
            self.unload(slot)
        logger.info("All model slots unloaded.")

    def get_status(self) -> dict:
        """Return status dict for /health endpoint.

        Keeps legacy keys (current_model, vram_allocated_gb, vram_total_gb)
        for backwards compatibility and adds loaded_models detail.
        """
        vram_allocated = 0.0
        vram_total = 0.0
        try:
            import torch
            if torch.cuda.is_available():
                vram_allocated = torch.cuda.memory_allocated(0) / (1024**3)
                vram_total = torch.cuda.get_device_properties(0).total_memory / (1024**3)
        except Exception:
            logger.debug("CUDA VRAM status unavailable", exc_info=True)

        snapshot = self._slots_snapshot()
        loaded = {}
        for slot, state in snapshot:
            if state.model is not None:
                loaded[slot.value] = {
                    "device": state.device,
                    "load_time_sec": round(state.load_time_sec, 2),
                }

        # Legacy compat: report first loaded model or "none"
        loaded_names = [s.value for s, st in snapshot if st.model is not None]
        current = loaded_names[0] if loaded_names else "none"

        return {
            "current_model": current,
            "vram_allocated_gb": round(vram_allocated, 2),
            "vram_total_gb": round(vram_total, 2),
            "vram_budget_gb": VRAM_BUDGET_GB,
            "load_times_sec": {
                s.value: round(st.load_time_sec, 2)
                for s, st in snapshot
                if st.model is not None
            },
            "loaded_models": loaded,
        }

    def empty_cache(self) -> None:
        """Gibt den CUDA-Cache frei (z.B. zur Erholung nach einem OOM-Fehler)."""
        self._try_empty_cache()
        gc.collect()

    # ── Internal ────────────────────────────────────────────────────────

    def _get_or_create_lock(self, slot: ModelSlot) -> threading.Lock:
        with self._global_lock:
            if slot not in self._locks:
                self._locks[slot] = threading.Lock()
            return self._locks[slot]

    def _slots_snapshot(self) -> list[tuple[ModelSlot, SlotState]]:
        """Konsistente Kopie fuer lesende Iterationen — verhindert 'dictionary changed size
        during iteration', wenn ein Threadpool-Thread parallel ein Modell laedt/entlaedt."""
        with self._global_lock:
            return list(self._slots.items())

    @staticmethod
    def _allocated_gb() -> float:
        try:
            import torch
            if torch.cuda.is_available():
                return torch.cuda.memory_allocated(0) / (1024**3)
        except Exception:
            pass
        return 0.0

    def _warn_if_over_budget(self) -> None:
        alloc = self._allocated_gb()
        if alloc > VRAM_BUDGET_GB:
            loaded = [s.value for s, st in self._slots_snapshot() if st.model is not None]
            logger.warning(
                "VRAM ueber Budget: %.1f GB > %.1f GB (geladen: %s). evict_lru()/unload() erwaegen.",
                alloc, VRAM_BUDGET_GB, ", ".join(loaded),
            )

    def evict_lru(self) -> Optional[ModelSlot]:
        """Entlaedt den am laengsten ungenutzten Slot (LRU). Gibt den Slot zurueck oder None."""
        candidates = [(st.last_used, s) for s, st in self._slots_snapshot() if st.model is not None]
        if not candidates:
            return None
        _, victim = min(candidates, key=lambda x: x[0])
        logger.info("evict_lru: entlade %s (LRU)", victim.value)
        self.unload(victim)
        return victim

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
