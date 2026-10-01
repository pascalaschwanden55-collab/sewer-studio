"""Tests fuer In-flight-VRAM-Reservierungen (Paket 2/B4): zwei parallel ladende
Modelle duerfen nicht denselben freien VRAM zugelassen bekommen.

mem_get_info ist konstant gefaehkt (geraeteweiter freier Speicher), Loader sind
Dummies mit echter Thread-Parallelitaet — Muster wie test_vram_admission.py.
"""

import sys
import threading
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


def _fake_torch_const(monkeypatch, free_gb, total_gb=32):
    fake_cuda = SimpleNamespace(
        is_available=lambda: True,
        mem_get_info=lambda _device: (int(free_gb * GIB), int(total_gb * GIB)),
        memory_allocated=lambda _device: 0,
        get_device_properties=lambda _device: SimpleNamespace(total_memory=int(total_gb * GIB)),
        empty_cache=lambda: None,
    )
    monkeypatch.setitem(sys.modules, "torch", SimpleNamespace(cuda=fake_cuda))


def _estimates(monkeypatch, mapping):
    estimates = dict(MODEL_VRAM_ESTIMATE_GB)
    estimates.update(mapping)
    monkeypatch.setattr("sidecar.gpu_manager.MODEL_VRAM_ESTIMATE_GB", estimates)


def test_gleichzeitige_gpu_ladungen_sehen_nicht_denselben_freien_vram(monkeypatch):
    # DINO (16 GB) und SAM (18 GB) laden GLEICHZEITIG bei 32 GB frei: einzeln kaeme
    # jeder durch (16+12<=32, 18+12<=32) — zusammen duerfen sie NICHT zugelassen werden.
    # Genau einer laedt, der andere muss kontrolliert insufficient_vram liefern
    # (kein Rennen, kein Deadlock, kein zweiter Gewinner).
    _estimates(monkeypatch, {ModelSlot.DINO: 16.0, ModelSlot.SAM: 18.0})
    _fake_torch_const(monkeypatch, free_gb=32.0)
    m = GpuModelManager()

    gate = threading.Event()           # beide Loader warten auf das gemeinsame Startsignal
    load_started = threading.Event()   # der zuerst zugelassene Loader meldet sich
    results: dict[ModelSlot, object] = {}
    errors: dict[ModelSlot, BaseException] = {}

    def make_loader(slot):
        def _load():
            load_started.set()
            gate.wait(timeout=10.0)
            return (slot.value, None)
        return _load

    def run(slot):
        try:
            results[slot] = m.ensure_loaded(slot, "cuda:0", make_loader(slot))
        except BaseException as exc:  # Test sammelt bewusst jeden Ausgang
            errors[slot] = exc

    threads = [
        threading.Thread(target=run, args=(ModelSlot.DINO,)),
        threading.Thread(target=run, args=(ModelSlot.SAM,)),
    ]
    for t in threads:
        t.start()
    assert load_started.wait(timeout=5.0), "Kein Ladevorgang gestartet."
    gate.set()
    for t in threads:
        t.join(timeout=15.0)
    assert all(not t.is_alive() for t in threads), "Deadlock: Threads laufen noch."

    assert len(results) == 1, (
        f"Erwartet genau 1 erfolgreiche Ladung, nicht {sorted(results)}; Fehler: {errors}")
    assert len(errors) == 1, f"Erwartet genau 1 kontrollierte Ablehnung: {errors}"

    exc = next(iter(errors.values()))
    assert isinstance(exc, InsufficientVramError), f"Falscher Fehlertyp: {exc!r}"

    winner_estimate = 16.0 if ModelSlot.DINO in results else 18.0
    loser_estimate = 18.0 if ModelSlot.DINO in results else 16.0
    # reserved_gb = Ollama-Reserve + laufende Reservierung des Gewinners.
    assert exc.reserved_gb == pytest.approx(VRAM_RESERVE_GB + winner_estimate)
    # Der Verlierer sah die Reservierung des Gewinners: effektiv frei < benoetigt.
    assert 32.0 - winner_estimate < loser_estimate + VRAM_RESERVE_GB


def test_laufende_ladung_zaehlt_auch_fuer_29gb_budget(monkeypatch):
    # 48 GB gesamt, 25 GB frei: 23 GB bereits belegt. YOLO (+3) passt,
    # DINO (+4) allein auch; zusammen waeren es 30 GB.
    _fake_torch_const(monkeypatch, free_gb=25.0, total_gb=48)
    m = GpuModelManager()

    m._admit_vram_or_raise(ModelSlot.YOLO, "cuda:0")
    with pytest.raises(InsufficientVramError) as excinfo:
        m._admit_vram_or_raise(ModelSlot.DINO, "cuda:0")

    assert excinfo.value.reason == "budget"
    assert excinfo.value.used_gb == pytest.approx(30.0)
    assert m._inflight_loads == {ModelSlot.YOLO: MODEL_VRAM_ESTIMATE_GB[ModelSlot.YOLO]}


def test_reservierung_wird_nach_dem_laden_freigegeben(monkeypatch):
    # Nach dem Ende des Ladevorgangs steht die Reservierung nicht mehr im Weg:
    # ein zweites Modell darf anschliessend laden (kein verklemmter Zustand).
    _estimates(monkeypatch, {ModelSlot.DINO: 10.0, ModelSlot.SAM: 10.0})
    _fake_torch_const(monkeypatch, free_gb=33.0, total_gb=48)
    m = GpuModelManager()

    m.ensure_loaded(ModelSlot.DINO, "cuda:0", lambda: ("dino", None))
    state = m.ensure_loaded(ModelSlot.SAM, "cuda:0", lambda: ("sam", None))

    assert state.model == "sam"
    with m._global_lock:
        assert m._inflight_loads == {}, "Reservierung wurde nicht freigegeben."


def test_watchdog_snapshot_bleibt_waehrend_eviction_bereinigung_erreichbar(monkeypatch):
    _estimates(monkeypatch, {ModelSlot.DINO: 4.0})
    _fake_torch_const(monkeypatch, free_gb=15.0)
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.YOLO, "cpu", lambda: ("yolo", None))

    cleanup_started = threading.Event()
    release_cleanup = threading.Event()
    snapshot_done = threading.Event()
    load_errors: list[BaseException] = []

    def blocking_cleanup():
        cleanup_started.set()
        release_cleanup.wait(timeout=10.0)

    def load_dino():
        try:
            m.ensure_loaded(ModelSlot.DINO, "cuda:0", lambda: ("dino", None))
        except BaseException as exc:
            load_errors.append(exc)

    monkeypatch.setattr(m, "_try_empty_cache", blocking_cleanup)
    load_thread = threading.Thread(target=load_dino)
    load_thread.start()
    assert cleanup_started.wait(timeout=5.0), "Eviction-Bereinigung wurde nicht erreicht."

    snapshot_thread = threading.Thread(
        target=lambda: (m.busy_snapshot(), snapshot_done.set()))
    snapshot_thread.start()
    snapshot_was_responsive = snapshot_done.wait(timeout=0.5)

    release_cleanup.set()
    load_thread.join(timeout=10.0)
    snapshot_thread.join(timeout=10.0)

    assert snapshot_was_responsive, (
        "Der Watchdog wartet auf _global_lock, solange die GPU-Bereinigung blockiert.")
    assert not load_thread.is_alive()
    assert not snapshot_thread.is_alive()
    assert len(load_errors) == 1
    assert isinstance(load_errors[0], InsufficientVramError)


def test_messung_vor_dem_ende_einer_fremden_ladung_wird_wiederholt(monkeypatch):
    # Audit A11 (23.09.2026): B misst 20 GB frei, WAEHREND A noch laedt. A belegt danach 4 GB
    # und gibt seine Reservierung frei. B verrechnete die alte Messung mit dem neuen
    # Reservierungsstand (0) und wurde zugelassen — nach beiden Ladungen blieben 10 GB,
    # weniger als die 12 GB Reserve. Richtig: B misst nach A neu (16 GB) und laedt nicht.
    _estimates(monkeypatch, {ModelSlot.DINO: 4.0, ModelSlot.SAM: 6.0})
    monkeypatch.setattr("sidecar.gpu_manager.VRAM_RESERVE_GB", 12.0)
    m = GpuModelManager()
    m._warn_if_over_budget = lambda: None

    speicher = {"frei": 20.0}
    a_laedt = threading.Event()
    b_hat_gemessen = threading.Event()
    a_fertig = threading.Event()
    geladen: list[str] = []
    fehler: dict[str, BaseException] = {}

    def messung():
        stand = speicher["frei"]
        if threading.current_thread().name == "B" and not b_hat_gemessen.is_set():
            b_hat_gemessen.set()
            assert a_fertig.wait(5.0), "A wurde nicht fertig."
        return stand

    m._device_vram_gb = lambda: (messung(), 32.0)

    def a_laden():
        a_laedt.set()
        assert b_hat_gemessen.wait(5.0), "B hat nicht gemessen."
        speicher["frei"] -= 4.0
        return object(), None

    def b_laden():
        speicher["frei"] -= 6.0
        return object(), None

    def a():
        try:
            m.ensure_loaded(ModelSlot.DINO, "cuda:0", a_laden)
            geladen.append("DINO")
        except BaseException as exc:
            fehler["A"] = exc
        finally:
            a_fertig.set()

    def b():
        try:
            assert a_laedt.wait(5.0), "A laedt nicht."
            m.ensure_loaded(ModelSlot.SAM, "cuda:0", b_laden)
            geladen.append("SAM")
        except BaseException as exc:
            fehler["B"] = exc

    threads = [threading.Thread(target=a, name="A"), threading.Thread(target=b, name="B")]
    for t in threads:
        t.start()
    for t in threads:
        t.join(timeout=15.0)
    assert all(not t.is_alive() for t in threads), "Deadlock: Threads laufen noch."

    assert geladen == ["DINO"]
    assert isinstance(fehler.get("B"), InsufficientVramError)
    assert speicher["frei"] >= 12.0


def test_ohne_fremde_ladung_genuegt_eine_messung(monkeypatch):
    # Gegenprobe: Aendert sich zwischen Messung und Sperre nichts, wird nicht erneut gemessen.
    _estimates(monkeypatch, {ModelSlot.SAM: 6.0})
    monkeypatch.setattr("sidecar.gpu_manager.VRAM_RESERVE_GB", 12.0)
    m = GpuModelManager()
    messungen = []
    m._device_vram_gb = lambda: (messungen.append(1) or 20.0, 32.0)

    m._admit_vram_or_raise(ModelSlot.SAM, "cuda:0")

    assert len(messungen) == 1
