"""Echte Parallelitaet fuer das Busy-Lease-Konzept und die atomare VRAM-Eviction (Paket 2).

Alle Tests laufen ohne GPU: injizierte Fake-Modelle/Locks (threading.Thread,
threading.Event), Muster wie in test_vram_admission.py/test_inference_watchdog.py.
Die Wrapper-Rentests (YOLO/DINO/SAM/BCC) fahren die ECHTEN oeffentlichen
Wrapper-Funktionen mit Fake-Modellen und pruefen das einheitliche Muster
"Predict-Lock ZUERST, Lease DANACH".
"""

import base64
import hashlib
import io
import json
import sys
import threading
import time
import types
from types import SimpleNamespace

import numpy as np
import pytest
from PIL import Image

from sidecar.gpu_manager import (
    GpuModelManager,
    InferenceWatchdog,
    InsufficientVramError,
    ModelSlot,
    VRAM_RESERVE_GB,
    MODEL_VRAM_ESTIMATE_GB,
    WATCHDOG_EXIT_CODE,
    gpu_manager,
)

GIB = 1024**3
THREAD_TIMEOUT_SEC = 10.0


def _noise_image_b64(w: int = 64, h: int = 64) -> str:
    """Bild mit Rauschen: passiert das Quality-Gate (Helligkeit/Std/Kanten) ohne GPU."""
    rng = np.random.default_rng(42)
    arr = rng.integers(0, 256, size=(h, w, 3), dtype=np.uint8)
    img = Image.fromarray(arr, "RGB")
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


class _BlockingHook:
    """Synchronisationspunkt: der ERSTE Aufruf blockiert, bis der Test freigibt.

    So haelt Thread A nachweisbar mitten in der Inferenz (Predict-Lock + Lease),
    waehrend Thread B am selben Lock wartet.
    """

    def __init__(self) -> None:
        self.first_started = threading.Event()
        self.release_first = threading.Event()
        self.calls = 0
        self._lock = threading.Lock()

    def block(self) -> None:
        with self._lock:
            self.calls += 1
            is_first = self.calls == 1
        if is_first:
            self.first_started.set()
            assert self.release_first.wait(THREAD_TIMEOUT_SEC), "Testfreigabe kam nicht"


# ── 1/2. Wartender Request vs. laufende Inferenz (Manager-Ebene) ─────────


def test_wartender_request_verschiebt_busy_uhr_nicht_und_watchdog_sieht_haenger():
    """A inferiert (Predict-Lock + Lease), B wartet auf denselben Lock.

    B darf A's busy_since NICHT veraendern; der Waechter erkennt A's Haenger
    trotz wartendem B. Nach A's Ende bekommt B eine eigene, NEUE Lease.
    """
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.SAM, "cpu", lambda: ("sam", "predictor"))
    predict_lock = threading.Lock()
    a_in_lease = threading.Event()
    a_may_finish = threading.Event()
    b_waiting = threading.Event()
    b_done = threading.Event()
    leases: dict[str, str | None] = {}

    def worker_a():
        with predict_lock, m.busy_slot(ModelSlot.SAM) as lease:
            leases["a"] = lease
            a_in_lease.set()
            assert a_may_finish.wait(THREAD_TIMEOUT_SEC)

    def worker_b():
        assert a_in_lease.wait(THREAD_TIMEOUT_SEC)
        b_waiting.set()
        with predict_lock, m.busy_slot(ModelSlot.SAM) as lease:
            leases["b"] = lease
            b_done.set()

    ta = threading.Thread(target=worker_a, name="req-a")
    tb = threading.Thread(target=worker_b, name="req-b")
    ta.start()
    tb.start()
    try:
        assert a_in_lease.wait(THREAD_TIMEOUT_SEC)
        since_a = m.busy_snapshot()[ModelSlot.SAM]
        assert b_waiting.wait(THREAD_TIMEOUT_SEC)
        time.sleep(0.3)  # B laeuft in den Predict-Lock-Wartezustand

        assert not b_done.is_set(), "B muss am Predict-Lock warten."
        assert "b" not in leases, "B darf noch keine Lease besitzen (erst NACH dem Lock)."
        snapshot = m.busy_snapshot()
        assert list(snapshot) == [ModelSlot.SAM]
        assert snapshot[ModelSlot.SAM] == since_a, (
            "Wartender Request hat die Busy-Uhr der laufenden Inferenz veraendert."
        )

        # Waechter erkennt A's Haenger trotz wartendem B (injizierte Uhr/Exit).
        exits = []
        wd = InferenceWatchdog(
            m, limit_sec=180.0, clock=lambda: since_a + 200.0, exit_fn=exits.append)
        wd.check_once()
        assert exits == [WATCHDOG_EXIT_CODE]

        a_may_finish.set()
        assert b_done.wait(THREAD_TIMEOUT_SEC)

        # B bekam eine eigene, neue Lease (kein Ueberschreiben, kein None).
        assert leases["a"] is not None
        assert leases["b"] is not None
        assert leases["b"] != leases["a"]
    finally:
        a_may_finish.set()
        ta.join(THREAD_TIMEOUT_SEC)
        tb.join(THREAD_TIMEOUT_SEC)
    assert not ta.is_alive() and not tb.is_alive(), "Deadlock im Wrapper-Muster."
    assert m.busy_snapshot() == {}, "Nach beiden Requests muss der Slot frei sein."


def test_altes_release_loescht_neue_lease_nicht():
    """Nach A's Ende hat B eine eigene Lease; A kann B's Lease nicht loeschen."""
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino", None))

    lease_a = m.acquire_busy(ModelSlot.DINO)
    m.release_busy(ModelSlot.DINO, lease_a)
    lease_b = m.acquire_busy(ModelSlot.DINO)
    assert lease_b is not None and lease_b != lease_a

    # Verspaetetes/dupliziertes Release des alten Besitzers (z.B. doppeltes finally).
    m.release_busy(ModelSlot.DINO, lease_a)
    assert ModelSlot.DINO in m.busy_snapshot(), (
        "Veraltetes Fremd-Release darf die neue Lease nicht loeschen."
    )

    m.release_busy(ModelSlot.DINO, lease_b)
    assert m.busy_snapshot() == {}


# ── 3. Exception waehrend der Inferenz ────────────────────────────────────


def test_exception_raeumt_nur_eigene_lease_auf_slot_danach_frei():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.YOLO, "cpu", lambda: ("yolo", None))
    predict_lock = threading.Lock()

    with pytest.raises(RuntimeError, match="Inferenzfehler"):
        with predict_lock, m.busy_slot(ModelSlot.YOLO):
            raise RuntimeError("Inferenzfehler")

    assert m.busy_snapshot() == {}, "Nach Exception muss die eigene Lease aufgeraeumt sein."

    # Slot sofort wieder nutzbar (kein verklemmter Zustand).
    with predict_lock, m.busy_slot(ModelSlot.YOLO) as lease:
        assert lease is not None
        assert ModelSlot.YOLO in m.busy_snapshot()
    assert m.busy_snapshot() == {}


def test_loader_exception_gibt_reservierung_und_lease_frei():
    """Exception beim Laden: weder Slot-Reservierung noch Lease bleiben haengen."""
    m = GpuModelManager()
    predict_lock = threading.Lock()

    def broken_loader():
        raise RuntimeError("Ladefehler")

    with pytest.raises(RuntimeError, match="Ladefehler"):
        with predict_lock, m.busy_slot(ModelSlot.DINO):
            m.ensure_loaded(ModelSlot.DINO, "cpu", broken_loader)

    assert m.busy_snapshot() == {}, "Lease muss trotz Lade-Exception freigegeben sein."
    assert ModelSlot.DINO not in m._slots, "Kein halb geladener Slot darf zurueckbleiben."

    # Erneuter Versuch laeuft normal (kein verklemmtes Lock, keine Reservierung).
    state = m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino", None))
    assert state.model == "dino"


# ── 4. Einheitliches Wrapper-Muster (Rentests mit echten Wrappern) ────────


def _run_wrapper_muster(invoke, slot: ModelSlot, hook: _BlockingHook):
    """Gemeinsame Rentest-Logik fuer alle Wrapper.

    A inferiert blockiert (Lock + Lease gehalten), B wartet am selben
    Predict-Lock: B veraendert A's busy_since NICHT; danach laufen beide
    sauber durch und keine Lease bleibt uebrig.
    """
    a_done, b_done = threading.Event(), threading.Event()
    errors: list[str] = []

    def run(done):
        try:
            invoke()
        except Exception as exc:  # Fehler im Thread sichtbar machen, nicht verschlucken
            errors.append(f"{type(exc).__name__}: {exc}")
        finally:
            done.set()

    ta = threading.Thread(target=run, args=(a_done,), name="wrapper-a")
    ta.start()
    try:
        assert hook.first_started.wait(THREAD_TIMEOUT_SEC), (
            "A ist nicht in der Inferenz angekommen."
        )
        snapshot_a = gpu_manager.busy_snapshot()
        assert slot in snapshot_a, f"A muss eine Lease auf {slot.value} halten."
        since_a = snapshot_a[slot]

        tb = threading.Thread(target=run, args=(b_done,), name="wrapper-b")
        tb.start()
        try:
            time.sleep(0.3)  # B laeuft in den Wartezustand am Wrapper-Predict-Lock
            assert not b_done.is_set(), "B muss am Predict-Lock serialisiert warten."
            snapshot = gpu_manager.busy_snapshot()
            assert slot in snapshot and snapshot[slot] == since_a, (
                f"Wartender Request hat die Busy-Uhr auf {slot.value} veraendert."
            )
            assert len(snapshot) == 1, "Es darf nur A's Lease geben."

            hook.release_first.set()
            assert a_done.wait(THREAD_TIMEOUT_SEC), "A kam nicht zurueck."
            assert b_done.wait(THREAD_TIMEOUT_SEC), "B kam nicht zurueck."
        finally:
            hook.release_first.set()
            tb.join(THREAD_TIMEOUT_SEC)
        assert not tb.is_alive(), "Deadlock im B-Request."
    finally:
        hook.release_first.set()
        ta.join(THREAD_TIMEOUT_SEC)
    assert not ta.is_alive(), "Deadlock im A-Request."
    assert errors == [], f"Fehler im Wrapper-Thread: {errors}"
    assert gpu_manager.busy_snapshot() == {}, "Keine Lease darf uebrig bleiben."


def test_wrapper_muster_yolo_cpu(monkeypatch):
    """YOLO-CPU-Pfad (Modul-Singleton am Manager vorbei): logische Lease yolo_cpu."""
    from sidecar.models import yolo_wrapper

    hook = _BlockingHook()

    class FakeYolo:
        def predict(self, **_kwargs):
            hook.block()
            return []

    monkeypatch.setattr(yolo_wrapper, "_cpu_model", FakeYolo())
    monkeypatch.setattr(yolo_wrapper, "_resolve_device", lambda: "cpu")
    img = _noise_image_b64()

    def invoke():
        resp = yolo_wrapper.detect(img, 0.25)
        assert resp.frame_class == "pipe_content"

    _run_wrapper_muster(invoke, ModelSlot.YOLO_CPU, hook)


def test_wrapper_muster_dino(monkeypatch):
    from sidecar.models import dino_wrapper

    hook = _BlockingHook()

    def fake_predict(**_kwargs):
        hook.block()
        return [], [], []

    package = types.ModuleType("groundingdino")
    util = types.ModuleType("groundingdino.util")
    inference = types.ModuleType("groundingdino.util.inference")
    inference.predict = fake_predict
    package.util = util
    util.inference = inference
    monkeypatch.setitem(sys.modules, "groundingdino", package)
    monkeypatch.setitem(sys.modules, "groundingdino.util", util)
    monkeypatch.setitem(sys.modules, "groundingdino.util.inference", inference)
    monkeypatch.setattr(dino_wrapper, "_load_dino_on", lambda device: (object(), None))
    monkeypatch.setattr(dino_wrapper, "_resolve_device", lambda: "cpu")
    img = _noise_image_b64()

    def invoke():
        resp = dino_wrapper.detect(img, None, 0.30, 0.25)
        assert not resp.degraded, resp.error

    try:
        _run_wrapper_muster(invoke, ModelSlot.DINO, hook)
    finally:
        gpu_manager.unload(ModelSlot.DINO)


def test_wrapper_muster_sam(monkeypatch):
    from sidecar.models import sam_wrapper
    from sidecar.schemas.detection import BoundingBox

    hook = _BlockingHook()

    class FakePredictor:
        def set_image(self, _arr):
            pass

        def predict(self, **_kwargs):
            hook.block()
            return np.ones((1, 4, 4), dtype=bool), np.array([0.99]), None

    monkeypatch.setattr(
        sam_wrapper, "_load_sam_on", lambda device: (object(), FakePredictor()))
    monkeypatch.setattr(sam_wrapper, "_resolve_device", lambda: "cpu")
    img = _noise_image_b64()

    def invoke():
        resp = sam_wrapper.segment(
            img, [BoundingBox(x1=1.0, y1=1.0, x2=10.0, y2=10.0, label="BCC_bogen")])
        assert resp.masks, "Maske erwartet (Fake-Score ueber sam_min_score)."

    try:
        _run_wrapper_muster(invoke, ModelSlot.SAM, hook)
    finally:
        gpu_manager.unload(ModelSlot.SAM)


def _write_bcc_candidate(root, candidate_id: str, *, map50: float) -> str:
    """Minimaler gueltiger BCC-Kandidat (Muster wie test_bcc_test_candidate.py)."""
    candidate = root / candidate_id
    candidate.mkdir()
    weights = candidate / "best.pt"
    weights.write_bytes(f"weights-{candidate_id}".encode())
    actual_sha = hashlib.sha256(weights.read_bytes()).hexdigest()
    manifest = {
        "schema_version": "1.0",
        "candidate_status": "not_deployed",
        "pilot": "BCC_bogen",
        "created_utc": "2026-07-24T12:00:00+00:00",
        "dataset": {"images": 48},
        "training": {
            "epochs_completed": 40,
            "results": {"metrics/mAP50(B)": map50},
        },
        "weights": {"candidate_sha256": actual_sha},
    }
    (candidate / "candidate_manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    return actual_sha


def test_wrapper_muster_bcc(tmp_path, monkeypatch):
    from sidecar.config import settings
    from sidecar.models import bcc_test_wrapper

    hook = _BlockingHook()

    class FakeYolo:
        def predict(self, **_kwargs):
            hook.block()
            return []

    _write_bcc_candidate(tmp_path, "bcc_full40", map50=0.76)
    monkeypatch.setattr(
        settings, "training_model_candidates_root", str(tmp_path), raising=False)
    monkeypatch.setattr(
        bcc_test_wrapper, "_load_candidate", lambda candidate, device: (FakeYolo(), None))
    monkeypatch.setattr(bcc_test_wrapper, "_resolve_device", lambda: "cpu")
    monkeypatch.setattr(bcc_test_wrapper, "_loaded_candidate_sha256", None)
    img = _noise_image_b64()

    def invoke():
        resp = bcc_test_wrapper.detect(img, 0.25)
        assert resp.available

    try:
        _run_wrapper_muster(invoke, ModelSlot.YOLO_TEST, hook)
    finally:
        gpu_manager.unload(ModelSlot.YOLO_TEST)


# ── 5. Eviction vs. laufende Inferenz ─────────────────────────────────────


def test_evict_respektiert_lease_mitten_in_der_inferenz():
    """Eviction waehlt den aeltesten Slot; beginnt dort gerade eine Inferenz
    (Lease aktiv), bleibt der Slot geladen — kein Entladen mitten im Request."""
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.YOLO, "cpu", lambda: ("yolo", None))
    m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino", None))
    m._slots[ModelSlot.YOLO].last_used = 1.0  # aeltester -> sonst erstes Opfer
    m._slots[ModelSlot.DINO].last_used = 2.0

    predict_lock = threading.Lock()
    inferenz_laeuft = threading.Event()
    inferenz_ende = threading.Event()
    fertig = threading.Event()

    def inferenz():  # Wrapper-Muster: Lock -> Lease -> Inferenz
        with predict_lock, m.busy_slot(ModelSlot.YOLO):
            inferenz_laeuft.set()
            assert inferenz_ende.wait(THREAD_TIMEOUT_SEC)
        fertig.set()

    t = threading.Thread(target=inferenz, name="inferenz")
    t.start()
    try:
        assert inferenz_laeuft.wait(THREAD_TIMEOUT_SEC)

        # Eviction mitten in der Inferenz: der leased (aelteste) Slot bleibt
        # verschont, der freie wird genommen.
        assert m.evict_lru() == ModelSlot.DINO
        assert m._slots[ModelSlot.YOLO].model == "yolo", (
            "Leased Slot wurde mitten in der Inferenz entladen."
        )

        # Kein anderer Kandidat mehr: Eviction verweigert, Slot bleibt geladen.
        assert m.evict_lru() is None
        assert m._slots[ModelSlot.YOLO].model == "yolo"
    finally:
        inferenz_ende.set()
        assert fertig.wait(THREAD_TIMEOUT_SEC)
        t.join(THREAD_TIMEOUT_SEC)
    assert not t.is_alive()

    # Nach Lease-Ende ist der Slot wieder antastbar.
    assert m.evict_lru() == ModelSlot.YOLO


def test_unload_verweigert_bei_aktiver_lease():
    """unload prueft unmittelbar vor dem Entladen die Lease (Rennen verloren)."""
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.SAM, "cpu", lambda: ("sam", None))
    lease = m.acquire_busy(ModelSlot.SAM)

    assert m.unload(ModelSlot.SAM) is False, "Entladen trotz laufender Inferenz!"
    assert m._slots[ModelSlot.SAM].model == "sam"

    m.release_busy(ModelSlot.SAM, lease)
    assert m.unload(ModelSlot.SAM) is True
    assert ModelSlot.SAM not in m._slots


# ── 6. Paralleles ensure_loaded + predict + evict ─────────────────────────


def test_paralleles_ensure_predict_evict_kein_zielslot_evict_kein_deadlock():
    """Dauerfeuer aus ensure_loaded/predict (leased) und paralleler Eviction.

    Invarianten: unter eigener Lease ist das Modell nie weg/None (kein
    Zielslot-Evict, kein None-Rennen), kein Deadlock (Timeout-Join), kein
    doppeltes Entladen bei zwei Evictern (pop-Reservierung).
    """
    m = GpuModelManager()
    predict_lock = threading.Lock()
    stop = threading.Event()
    fehler: list[str] = []

    def yolo_requester():
        while not stop.is_set():
            # Wrapper-Muster: Lock -> Lease -> ensure -> "predict"
            with predict_lock, m.busy_slot(ModelSlot.YOLO):
                state = m.ensure_loaded(ModelSlot.YOLO, "cpu", lambda: ("yolo", None))
                if state.model != "yolo":
                    fehler.append("YOLO-Modell unter eigener Lease verloren/None")
            time.sleep(0.001)

    def dino_loader():  # paralleles Laden/Entladen eines anderen Slots
        while not stop.is_set():
            m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino", None))
            time.sleep(0.001)

    def evictor():
        while not stop.is_set():
            # Bewusst OHNE exclude: der Lease-Schutz allein muss den leased
            # Slot schuetzen; freie Slots duerfen jederzeit genommen werden.
            m.evict_lru()
            time.sleep(0.001)

    threads = [
        threading.Thread(target=yolo_requester, name="yolo-req"),
        threading.Thread(target=dino_loader, name="dino-load"),
        threading.Thread(target=evictor, name="evict-1"),
        threading.Thread(target=evictor, name="evict-2"),
    ]
    for t in threads:
        t.start()
    time.sleep(1.5)
    stop.set()
    for t in threads:
        t.join(THREAD_TIMEOUT_SEC)
    for t in threads:
        assert not t.is_alive(), f"Deadlock in Thread {t.name}"
    assert fehler == [], fehler


# ── 7. Kein freier Kandidat -> insufficient_vram mit vollem Payload ────────


def _fake_torch(monkeypatch, free_gb_sequence):
    """Wie test_vram_admission: mem_get_info liefert die Sequenz (letzter bleibt)."""
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


def test_kein_freier_kandidat_insufficient_vram_mit_free_required_reserved(monkeypatch):
    """Einziger Evict-Kandidat ist leased -> keine Reservierung moeglich ->
    InsufficientVramError (kein 500, kein None-Rennen) mit free/required/reserved."""
    required_dino = _required(ModelSlot.DINO)
    # YOLO-Ladung ok; DINO-Zulassung scheitert auch NACH Eviction-Versuchen.
    _fake_torch(monkeypatch, [_required(ModelSlot.YOLO) + 5.0, required_dino - 2.0])
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.YOLO, "cuda:0", lambda: ("yolo", None))
    m.acquire_busy(ModelSlot.YOLO)  # einziger Kandidat leased -> nicht antastbar

    with pytest.raises(InsufficientVramError) as excinfo:
        m.ensure_loaded(ModelSlot.DINO, "cuda:0", lambda: ("dino", None))

    exc = excinfo.value
    assert exc.free_gb == pytest.approx(required_dino - 2.0)
    assert exc.required_gb == pytest.approx(required_dino)
    assert exc.reserved_gb == pytest.approx(VRAM_RESERVE_GB)
    assert "yolo" in m.get_status()["loaded_models"], (
        "Der leased Slot darf fuer die Zulassung nie geopfert werden."
    )


# ── 9. CPU-Pfad: Waechter sieht logische Leases (ohne GPU) ────────────────


def test_watchdog_sieht_logische_cpu_leases_ohne_gpu():
    """YOLO_CPU/YOLO_CLS erzeugen Lease-Eintraege ohne SlotState/VRAM; ein
    Haenger auf einem logischen Slot loest den Waechter aus (A5-Entscheidung:
    CPU-Inferenzen werden bewusst ueberwacht)."""
    m = GpuModelManager()
    lease_cpu = m.acquire_busy(ModelSlot.YOLO_CPU)
    lease_cls = m.acquire_busy(ModelSlot.YOLO_CLS)
    assert lease_cpu is not None and lease_cls is not None
    assert ModelSlot.YOLO_CPU not in m._slots and ModelSlot.YOLO_CLS not in m._slots

    snapshot = m.busy_snapshot()
    assert set(snapshot) == {ModelSlot.YOLO_CPU, ModelSlot.YOLO_CLS}

    exits = []
    wd = InferenceWatchdog(
        m,
        limit_sec=180.0,
        clock=lambda: snapshot[ModelSlot.YOLO_CPU] + 200.0,
        exit_fn=exits.append,
    )
    wd.check_once()
    assert exits == [WATCHDOG_EXIT_CODE], "Haenger auf logischem CPU-Slot nicht erkannt."

    # get_status meldet die logischen Slots in busy_slots (health bleibt wahrheitsgemaess).
    status = m.get_status()
    assert set(status["busy_slots"]) == {"yolo_cpu", "yolo_cls"}
    assert status["loaded_models"] == {}, "Logische Slots erzeugen keinen geladenen Eintrag."

    m.release_busy(ModelSlot.YOLO_CPU, lease_cpu)
    m.release_busy(ModelSlot.YOLO_CLS, lease_cls)
    assert m.busy_snapshot() == {}
