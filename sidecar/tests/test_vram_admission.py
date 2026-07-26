"""Tests fuer die VRAM-Zulassungspruefung vor dem Laden (Paket 3/B).

mem_get_info wird gefaehkt (geraeteweiter freier Speicher inkl. Ollama), Loader sind
Dummies — kein echtes torch/CUDA/Modell noetig. Muster wie test_gpu_manager.py.
"""

import asyncio
import json
import sys
from types import SimpleNamespace

import pytest

from sidecar.gpu_manager import (
    GpuModelManager,
    InsufficientVramError,
    ModelSlot,
    VRAM_RESERVE_GB,
    MODEL_VRAM_ESTIMATE_GB,
)

GIB = 1024**3


def _fake_torch(monkeypatch, free_gb_sequence):
    """Installiert ein Fake-torch; mem_get_info liefert die Sequenz (letzter Wert bleibt)."""
    values = list(free_gb_sequence)
    calls = {"n": 0}

    def mem_get_info(_device):
        idx = min(calls["n"], len(values) - 1)
        calls["n"] += 1
        return int(values[idx] * GIB), 32 * GIB

    fake_cuda = SimpleNamespace(
        is_available=lambda: True,
        mem_get_info=mem_get_info,
        memory_allocated=lambda _device: 0,
        get_device_properties=lambda _device: SimpleNamespace(total_memory=32 * GIB),
        empty_cache=lambda: None,
    )
    monkeypatch.setitem(sys.modules, "torch", SimpleNamespace(cuda=fake_cuda))
    return calls


def _required(slot):
    return MODEL_VRAM_ESTIMATE_GB[slot] + VRAM_RESERVE_GB


def test_zulassung_erlaubt_bei_genug_freiem_vram(monkeypatch):
    required = _required(ModelSlot.DINO)
    _fake_torch(monkeypatch, [required + 4.0])
    m = GpuModelManager()

    state = m.ensure_loaded(ModelSlot.DINO, "cuda:0", lambda: ("dino", None))

    assert state.model == "dino"


def test_zulassung_503_pfad_ohne_ladeversuch(monkeypatch):
    required = _required(ModelSlot.DINO)
    _fake_torch(monkeypatch, [required - 5.0])
    m = GpuModelManager()
    loader_called = []

    with pytest.raises(InsufficientVramError) as excinfo:
        m.ensure_loaded(
            ModelSlot.DINO,
            "cuda:0",
            lambda: loader_called.append(1) or ("dino", None),
        )

    assert loader_called == [], "Bei Nicht-Zulassung darf KEIN Ladeversuch stattfinden."
    exc = excinfo.value
    assert exc.slot == ModelSlot.DINO
    assert exc.free_gb == pytest.approx(required - 5.0)
    assert exc.required_gb == pytest.approx(required)
    assert exc.reserved_gb == pytest.approx(VRAM_RESERVE_GB)
    assert "insufficient_vram" in str(exc)


def test_zulassung_evict_pfad_laedt_nach_freigabe(monkeypatch):
    required_dino = _required(ModelSlot.DINO)
    # 1. Aufruf: YOLO-Ladung (genug frei); 2. Aufruf: DINO-Zulassung (zu wenig);
    # 3. Aufruf: nach Eviction von YOLO wieder genug.
    _fake_torch(monkeypatch, [_required(ModelSlot.YOLO) + 5.0, required_dino - 2.0, required_dino + 1.0])
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.YOLO, "cuda:0", lambda: ("yolo", None))

    state = m.ensure_loaded(ModelSlot.DINO, "cuda:0", lambda: ("dino", None))

    assert state.model == "dino"
    loaded = m.get_status()["loaded_models"]
    assert "yolo" not in loaded, "Der freie LRU-Slot muss fuer die Zulassung evictiert werden."
    assert "dino" in loaded


def test_busy_slot_wird_bei_zulassung_nie_evictiert(monkeypatch):
    required_dino = _required(ModelSlot.DINO)
    _fake_torch(monkeypatch, [_required(ModelSlot.YOLO) + 5.0, required_dino - 2.0])
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.YOLO, "cuda:0", lambda: ("yolo", None))
    m.acquire_busy(ModelSlot.YOLO)

    with pytest.raises(InsufficientVramError):
        m.ensure_loaded(ModelSlot.DINO, "cuda:0", lambda: ("dino", None))

    assert "yolo" in m.get_status()["loaded_models"], "Busy Slot darf nie evictiert werden."


def test_evict_lru_evictiert_aeltesten_freien_slot_nicht_busy():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.YOLO, "cpu", lambda: ("yolo", None))
    m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino", None))
    m._slots[ModelSlot.YOLO].last_used = 1.0
    m._slots[ModelSlot.DINO].last_used = 2.0
    m.acquire_busy(ModelSlot.YOLO)   # aeltester, aber busy -> nicht antastbar

    victim = m.evict_lru()

    assert victim == ModelSlot.DINO
    assert "yolo" in m.get_status()["loaded_models"]


def test_evict_lru_evictiert_ausgeschlossenen_slot_nie():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.YOLO, "cpu", lambda: ("yolo", None))

    victim = m.evict_lru(exclude={ModelSlot.YOLO})

    assert victim is None
    assert "yolo" in m.get_status()["loaded_models"]


def test_evict_lru_alle_busy_gibt_none():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.YOLO, "cpu", lambda: ("yolo", None))
    m.acquire_busy(ModelSlot.YOLO)

    assert m.evict_lru() is None


def test_zulassung_cpu_geraet_wird_nicht_begrenzt(monkeypatch):
    _fake_torch(monkeypatch, [0.5])
    m = GpuModelManager()

    state = m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino", None))

    assert state.model == "dino"


def test_zulassung_ohne_cuda_laedt_wie_bisher(monkeypatch):
    fake_cuda = SimpleNamespace(is_available=lambda: False)
    monkeypatch.setitem(sys.modules, "torch", SimpleNamespace(cuda=fake_cuda))
    m = GpuModelManager()

    state = m.ensure_loaded(ModelSlot.DINO, "cuda:0", lambda: ("dino", None))

    assert state.model == "dino"


def test_insufficient_vram_handler_liefert_503_mit_maschinenlesbarem_detail():
    from sidecar.main import handle_insufficient_vram

    exc = InsufficientVramError(ModelSlot.DINO, free_gb=5.25, required_gb=16.0, reserved_gb=12.0)
    request = SimpleNamespace(method="POST", url=SimpleNamespace(path="/detect/dino"))

    response = asyncio.run(handle_insufficient_vram(request, exc))

    assert response.status_code == 503
    body = json.loads(response.body)
    assert body["code"] == "insufficient_vram"
    assert body["slot"] == "dino"
    assert body["free_gb"] == 5.25
    assert body["required_gb"] == 16.0
    assert body["reserved_gb"] == 12.0


def test_model_unloaded_handler_liefert_503_statt_500():
    from sidecar.main import handle_model_unloaded
    from sidecar.gpu_manager import ModelUnloadedError

    request = SimpleNamespace(method="POST", url=SimpleNamespace(path="/segment/sam"))
    response = asyncio.run(handle_model_unloaded(request, ModelUnloadedError("sam")))

    assert response.status_code == 503
    assert json.loads(response.body)["code"] == "model_unloaded"
