"""Tests fuer den Inferenz-Waechter (Paket 3/A) und das Busy-Lease-Konzept (Paket 2).

Die Entscheidungslogik wird mit injizierter Uhr und injizierter Exit-Funktion geprueft —
kein echter Prozess-Exit, keine GPU, keine echten Modelle. Echte Parallelitaet
(Lock/Lease-Rennen zwischen Threads) liegt in test_busy_lease_concurrency.py.
"""

from sidecar.gpu_manager import (
    GpuModelManager,
    InferenceWatchdog,
    ModelSlot,
    WATCHDOG_EXIT_CODE,
    find_overdue_slots,
)


def test_find_overdue_feuert_bei_ueberaltertem_busy():
    busy = {ModelSlot.SAM: 100.0}
    assert find_overdue_slots(busy, now=100.0 + 181.0, limit_sec=180.0) == [
        (ModelSlot.SAM, 181.0)
    ]


def test_find_overdue_nicht_bei_frischem_busy():
    busy = {ModelSlot.SAM: 100.0}
    assert find_overdue_slots(busy, now=100.0 + 10.0, limit_sec=180.0) == []


def test_find_overdue_limit_null_ist_aus():
    busy = {ModelSlot.SAM: 0.0}
    assert find_overdue_slots(busy, now=99999.0, limit_sec=0.0) == []


def test_check_once_exit_bei_ueberaltertem_busy():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.SAM, "cpu", lambda: ("sam", "predictor"))
    m.acquire_busy(ModelSlot.SAM)
    since = m.busy_snapshot()[ModelSlot.SAM]

    exits = []
    wd = InferenceWatchdog(m, limit_sec=180.0, clock=lambda: since + 200.0, exit_fn=exits.append)

    wd.check_once()

    assert exits == [WATCHDOG_EXIT_CODE]


def test_check_once_kein_exit_bei_frischem_busy_und_leerlauf():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.SAM, "cpu", lambda: ("sam", "predictor"))
    lease = m.acquire_busy(ModelSlot.SAM)
    since = m.busy_snapshot()[ModelSlot.SAM]

    exits = []
    wd = InferenceWatchdog(m, limit_sec=180.0, clock=lambda: since + 5.0, exit_fn=exits.append)

    wd.check_once()
    assert exits == []

    # Frischer Leerlauf (nichts busy) -> ebenfalls kein Exit.
    m.release_busy(ModelSlot.SAM, lease)
    wd.check_once()
    assert exits == []


def test_watchdog_limit_null_ist_deaktiviert_und_startet_keinen_thread():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.SAM, "cpu", lambda: ("sam", "predictor"))
    m.acquire_busy(ModelSlot.SAM)

    exits = []
    wd = InferenceWatchdog(m, limit_sec=0.0, clock=lambda: 1e9, exit_fn=exits.append)

    assert not wd.enabled
    wd.check_once()
    assert exits == []
    wd.start()
    assert wd._thread is None


def test_acquire_busy_setzt_last_used_und_release_raeumt_auf():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino", None))
    m._slots[ModelSlot.DINO].last_used = 1.0

    lease = m.acquire_busy(ModelSlot.DINO)

    assert lease is not None
    state = m._slots[ModelSlot.DINO]
    assert state.last_used > 1.0, "acquire_busy muss last_used aktualisieren (Evict-Schutz)."
    assert m.busy_snapshot()[ModelSlot.DINO] > 0

    m.release_busy(ModelSlot.DINO, lease)
    assert m.busy_snapshot() == {}


def test_acquire_busy_gibt_none_bei_slot_none_und_release_none_ist_noop():
    m = GpuModelManager()
    assert m.acquire_busy(ModelSlot.NONE) is None
    # release mit None-Lease muss ein sicherer No-op sein (CPU-Bypass-Kompatibilitaet).
    m.release_busy(ModelSlot.NONE, None)
    assert m.busy_snapshot() == {}


def test_zweite_lease_auf_belegtem_slot_wird_abgelehnt_und_verschiebt_uhr_nicht():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino", None))
    lease_a = m.acquire_busy(ModelSlot.DINO)
    since_a = m.busy_snapshot()[ModelSlot.DINO]

    lease_b = m.acquire_busy(ModelSlot.DINO)

    assert lease_b is None, "Die aeltere Lease hat Vorrang — kein Ueberschreiben der Busy-Uhr."
    assert m.busy_snapshot()[ModelSlot.DINO] == since_a

    m.release_busy(ModelSlot.DINO, lease_a)
    assert m.busy_snapshot() == {}


def test_fremdes_release_wird_still_ignoriert():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino", None))
    lease = m.acquire_busy(ModelSlot.DINO)

    m.release_busy(ModelSlot.DINO, "fremde-lease-id")
    assert ModelSlot.DINO in m.busy_snapshot(), "Fremdes Release darf die Lease nicht loeschen."

    m.release_busy(ModelSlot.DINO, lease)
    assert m.busy_snapshot() == {}


def test_busy_slot_contextmanager_liefert_lease_und_raeumt_auch_bei_fehler_auf():
    m = GpuModelManager()
    m.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino", None))

    with m.busy_slot(ModelSlot.DINO) as lease:
        assert lease is not None
        assert ModelSlot.DINO in m.busy_snapshot()
    assert m.busy_snapshot() == {}

    try:
        with m.busy_slot(ModelSlot.DINO):
            raise ValueError("Testfehler")
    except ValueError:
        pass
    assert m.busy_snapshot() == {}
