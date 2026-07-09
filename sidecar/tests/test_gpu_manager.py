"""Tests fuer GpuModelManager: LRU-Eviction + VRAM-Budget (Audit R8). Kein torch noetig."""

import sys
from types import SimpleNamespace

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


def test_get_status_reads_torch_total_memory(monkeypatch):
    gib = 1024**3

    fake_cuda = SimpleNamespace(
        is_available=lambda: True,
        memory_allocated=lambda _device: 2 * gib,
        get_device_properties=lambda _device: SimpleNamespace(total_memory=16 * gib),
    )
    monkeypatch.setitem(sys.modules, "torch", SimpleNamespace(cuda=fake_cuda))

    status = GpuModelManager().get_status()

    assert status["vram_allocated_gb"] == 2.0
    assert status["vram_total_gb"] == 16.0
