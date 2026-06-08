"""Tests fuer GpuModelManager: LRU-Eviction + VRAM-Budget (Audit R8). Kein torch noetig."""

from sidecar.gpu_manager import GpuModelManager, ModelSlot, VRAM_BUDGET_GB


def test_budget_constant_positive():
    assert VRAM_BUDGET_GB > 0


def test_evict_lru_empty_returns_none():
    assert GpuModelManager().evict_lru() is None


def test_evict_lru_removes_oldest():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.YOLO, "cpu", lambda: (object(), None))
    m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: (object(), None))
    # deterministische LRU-Ordnung erzwingen
    m._slots[ModelSlot.YOLO].last_used = 1.0
    m._slots[ModelSlot.DINO].last_used = 2.0

    victim = m.evict_lru()

    assert victim == ModelSlot.YOLO
    assert set(m.get_status()["loaded_models"].keys()) == {"dino"}
