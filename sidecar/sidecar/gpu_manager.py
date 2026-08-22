"""GPU Model Manager – keeps multiple models resident in VRAM simultaneously."""

from __future__ import annotations

import gc
import os
import enum
import time
import threading
import logging
import uuid
from contextlib import contextmanager
from dataclasses import dataclass, field
from typing import Any, Callable, Iterable, Tuple, Optional

logger = logging.getLogger(__name__)

# VRAM-Budget (GB): YOLO/DINO/SAM bleiben bewusst gleichzeitig resident (Tempo). Auf der 32-GB-Karte
# traegt das; das Budget macht die Grenze im Code SICHTBAR (Warnung) und erlaubt LRU-Eviction bei
# Bedarf, statt sich nur auf grosszuegige Hardware zu verlassen. (Audit R8)
VRAM_BUDGET_GB = float(os.environ.get("SEWER_SIDECAR_VRAM_BUDGET_GB", "29"))
# Ollama-Reserve (GB), die beim Laden eines Vision-Modells frei bleiben muss (Paket 3/B):
# Ollama mit qwen3-vl:8b-q8 belegt auf dieser Karte typisch ~9-11 GB (Gewichte + KV-Cache)
# und waechst bei laengeren Kontexten weiter. Die Reserve verhindert, dass YOLO/DINO/SAM
# Ollama (oder ein nachgeladenes Qwen umgekehrt die Modelle) in den VRAM des anderen draengt.
VRAM_RESERVE_GB = float(os.environ.get("SEWER_SIDECAR_VRAM_RESERVE_GB", "12"))

# Inferenz-Waechter (Paket 3/A): Haengt ein Predict-Lock laenger als das Limit (z. B. fester
# CUDA-Call), gilt der Prozess als unrettbar — in-process CUDA-Recovery ist unzuverlaessig —
# und wird hart beendet. Der C#-Neustartdienst startet den Sidecar danach kontrolliert neu.
# 0 = Waechter aus.
INFERENCE_WATCHDOG_SEC = float(os.environ.get("SEWER_SIDECAR_INFERENCE_WATCHDOG_SEC", "180"))
WATCHDOG_CHECK_INTERVAL_SEC = 5.0
# Erkennbarer Exit-Code, damit Logs/Prozessueberwachung einen Waechter-Exit vom normalen
# Beenden unterscheiden koennen.
WATCHDOG_EXIT_CODE = 42


class ModelSlot(str, enum.Enum):
    NONE = "none"
    YOLO = "yolo"
    YOLO_TEST = "yolo_test"
    YOLO_OSD = "yolo_osd"
    DINO = "dino"
    SAM = "sam"
    # Logische Slots (Paket 2/A5): besitzen KEINEN SlotState/VRAM-Eintrag und werden
    # nie ueber ensure_loaded geladen. Sie existieren nur als Busy-Lease-Register-
    # Eintrag, damit der Inferenz-Waechter auch die Manager-umgangenen Pfade sieht:
    # YOLO_CPU = YOLO-CPU-Modul-Singleton, YOLO_CLS = Whole-Frame-Klassifikator
    # (CUDA-faehige Inferenz mit eigenem Predict-Lock — ein Haenger waere sonst
    # unsichtbar).
    YOLO_CPU = "yolo_cpu"
    YOLO_CLS = "yolo_cls"


# Modellschaetzungen (GB) fuer die Zulassungspruefung VOR dem Laden (Paket 3/B). Abgeleitet
# aus den Telemetrie-/health-Messwerten auf der RTX 5090 (32 GB) und bewusst aufgerundet,
# damit auch Ladespitzen abgedeckt sind: YOLO (yolo26m TensorRT/PyTorch, imgsz 1280) ~2-3 GB,
# DINO Swin-B ~3-4 GB resident, SAM 2.1 hiera_large ~5-6 GB inkl. Bild-Embedding-Puffer.
MODEL_VRAM_ESTIMATE_GB: dict[ModelSlot, float] = {
    ModelSlot.YOLO: 3.0,
    ModelSlot.YOLO_TEST: 3.0,
    ModelSlot.YOLO_OSD: 0.5,
    ModelSlot.DINO: 4.0,
    ModelSlot.SAM: 6.0,
}


class InsufficientVramError(RuntimeError):
    """Kontrollierte Nicht-Zulassung: zu wenig freier VRAM VOR dem Laden.

    Traegt slot/free_gb/required_gb/reserved_gb fuer den maschinenlesbaren
    503-Fehler; es findet KEIN Ladeversuch statt (der bisherige OOM-/Post-Load-Pfad
    bleibt zweites Netz). reserved_gb ist die bei der Zulassung abgezogene
    Ollama-Reserve (required_gb = Schaetzung + Reserve).
    """

    def __init__(
        self,
        slot: ModelSlot,
        free_gb: float,
        required_gb: float,
        reserved_gb: float = VRAM_RESERVE_GB,
    ) -> None:
        self.slot = slot
        self.free_gb = free_gb
        self.required_gb = required_gb
        self.reserved_gb = reserved_gb
        super().__init__(
            f"insufficient_vram: slot={slot.value} free={free_gb:.1f}GB "
            f"required={required_gb:.1f}GB reserved={reserved_gb:.1f}GB")


class ModelUnloadedError(RuntimeError):
    """Slot wurde zwischen ensure_loaded und Nutzung entladen (Unload-Race).

    Kontrollierter Fehler statt AttributeError/500: der zentrale Handler liefert 503,
    der C#-Client wiederholt den Request einmal und loest damit das Nachladen aus.
    """


def find_overdue_slots(
    busy_map: dict[ModelSlot, float],
    now: float,
    limit_sec: float,
) -> list[tuple[ModelSlot, float]]:
    """Reine Entscheidungslogik des Inferenz-Waechters (CPU-testbar, injizierbare Uhr).

    Liefert (slot, busy_dauer_sec) aller Slots, die laenger als limit_sec busy sind.
    limit_sec <= 0 -> Waechter aus, immer leer.
    """
    if limit_sec <= 0:
        return []
    return [
        (slot, now - since)
        for slot, since in busy_map.items()
        if now - since > limit_sec
    ]


class InferenceWatchdog:
    """Daemon-Waechter: beendet den Prozess hart, wenn ein Slot zu lange busy ist.

    Die Entscheidung liegt in find_overdue_slots (injizierbare Uhr); Uhr und Exit-Funktion
    sind injizierbar, damit das Verhalten ohne echten Prozess-Exit testbar bleibt.
    """

    def __init__(
        self,
        manager: "GpuModelManager",
        limit_sec: float = INFERENCE_WATCHDOG_SEC,
        check_interval_sec: float = WATCHDOG_CHECK_INTERVAL_SEC,
        clock: Callable[[], float] = time.monotonic,
        exit_fn: Callable[[int], None] | None = None,
    ) -> None:
        self._manager = manager
        self._limit_sec = limit_sec
        self._check_interval_sec = check_interval_sec
        self._clock = clock
        self._exit_fn = exit_fn if exit_fn is not None else os._exit
        self._stop_event = threading.Event()
        self._thread: threading.Thread | None = None

    @property
    def enabled(self) -> bool:
        return self._limit_sec > 0

    def start(self) -> None:
        """Startet den Daemon-Thread (idempotent; No-op bei Limit 0)."""
        if not self.enabled or self._thread is not None:
            return
        self._thread = threading.Thread(
            target=self._run, name="inference-watchdog", daemon=True)
        self._thread.start()
        logger.info(
            "Inferenz-Waechter aktiv: Limit %.0fs, Pruefintervall %.0fs, Exit-Code %d.",
            self._limit_sec, self._check_interval_sec, WATCHDOG_EXIT_CODE)

    def stop(self) -> None:
        self._stop_event.set()
        thread = self._thread
        if thread is not None:
            thread.join(timeout=2.0)
            self._thread = None

    def _run(self) -> None:
        while not self._stop_event.wait(self._check_interval_sec):
            self.check_once()

    def check_once(self) -> None:
        """Ein Pruefzyklus; bei ueberaltertem Busy: ERROR-Log + harter Prozess-Exit."""
        overdue = find_overdue_slots(
            self._manager.busy_snapshot(), self._clock(), self._limit_sec)
        if not overdue:
            return
        for slot, busy_sec in overdue:
            logger.error(
                "Inferenz-Waechter: Slot %s haengt seit %.0fs im Predict-Lock "
                "(Limit %.0fs) — harter Prozess-Exit (Code %d).",
                slot.value, busy_sec, self._limit_sec, WATCHDOG_EXIT_CODE)
        self._exit_fn(WATCHDOG_EXIT_CODE)


@dataclass
class SlotState:
    """State for a single loaded model slot."""
    model: Any = None
    processor: Any = None
    device: str = ""
    load_time_sec: float = 0.0
    last_used: float = 0.0   # time.monotonic() der letzten Nutzung (fuer LRU-Eviction)
    # Inhaltskennung des geladenen Modells (z. B. Gewichts-SHA-256). Teilen sich
    # mehrere Wrapper einen Slot, entscheidet allein dieser Wert ueber
    # Wiederverwenden oder Neuladen (Audit 2026-08-14, S-H1). None = Slot mit nur
    # einem festen Modell (YOLO, DINO, SAM).
    content_id: str | None = None


@dataclass(frozen=True)
class _BusyLease:
    """Eine vergebene Busy-Lease (Besitznachweis ueber eindeutige lease_id)."""
    lease_id: str
    since: float   # time.monotonic() des Lease-Erwerbs (Watchdog-Referenz)


class GpuModelManager:
    """Multi-slot persistent model manager.

    YOLO/DINO/SAM koennen gleichzeitig resident bleiben (bewusst, fuer Tempo). KEINE
    automatische Eviction beim Slot-Wechsel. Ein konfigurierbares VRAM-Budget
    (VRAM_BUDGET_GB) macht die Grenze sichtbar (Warnung beim Ueberschreiten); ueber
    evict_lru() ist LRU-Eviction moeglich (z.B. nach OOM). (Audit R8)

    Busy-Verfolgung (Paket 2): pro Slot hoechstens EINE Busy-Lease mit eindeutiger
    Besitzer-ID (_busy-Register). Nur der Besitzer kann seinen Eintrag setzen und
    loesen; fremde Releases werden still ignoriert. Logische Slots (YOLO_CPU,
    YOLO_CLS) existieren nur im Lease-Register (kein VRAM, kein ensure_loaded).

    SPERRENREIHENFOLGE (Deadlock-Freiheit):
      1. Wrapper-Predict-Locks (Modulebene, z.B. _yolo_predict_lock) — immer aussen;
         Manager-Code fordert sie nie an. Teilen sich zwei Wrapper einen Slot, teilen
         sie sich auch das Lock (YOLO_TEST: yolo_test_slot.PREDICT_LOCK); die
         Slot-Identitaet haengt zusaetzlich an SlotState.content_id (Audit S-H1).
      2. Per-Slot-Lock (self._locks[slot]) — serialisiert Laden/Entladen eines Slots.
      3. _global_lock — schuetzt _slots-Register und _busy-Leases (nur kurze
         kritische Abschnitte).
    Erlaubt ist damit ausschliesslich die Ordnung Wrapper-Lock -> Slot-Lock ->
    _global_lock. Wer _global_lock haelt, wartet NIEMALS auf ein Slot- oder
    Wrapper-Lock: evict_lru waehlt, prueft (Lease!) und reserviert (pop) unter
    _global_lock und raeumt das Modell erst NACH der Freigabe auf. Dadurch kann
    kein Zyklus entstehen.
    """

    def __init__(self) -> None:
        self._slots: dict[ModelSlot, SlotState] = {}
        self._locks: dict[ModelSlot, threading.Lock] = {
            ModelSlot.YOLO: threading.Lock(),
            ModelSlot.YOLO_TEST: threading.Lock(),
            ModelSlot.YOLO_OSD: threading.Lock(),
            ModelSlot.DINO: threading.Lock(),
            ModelSlot.SAM: threading.Lock(),
        }
        self._busy: dict[ModelSlot, _BusyLease] = {}
        # Der globale Lock schuetzt nur kurze Register-/Lease-Entscheidungen.
        # Modell- und CUDA-Bereinigung laufen immer ausserhalb, damit der Watchdog
        # busy_snapshot() auch bei einem haengenden empty_cache erreichen kann.
        self._global_lock = threading.Lock()
        # Gerade LADENDE Slots mit ihrer geschaetzten GB-Reservierung (Paket 2/B4):
        # zwei Modelle duerfen nicht gleichzeitig denselben freien VRAM zugelassen bekommen.
        self._inflight_loads: dict[ModelSlot, float] = {}
        self._watchdog: InferenceWatchdog | None = None

    # ── Public API ──────────────────────────────────────────────────────

    def ensure_loaded(
        self,
        slot: ModelSlot,
        device: str,
        loader: Callable[[], Tuple[Any, Optional[Any]]],
        content_id: str | None = None,
    ) -> SlotState:
        """Load *slot* on *device* if not already loaded. Returns SlotState.

        Uses double-check locking for thread safety without blocking
        concurrent access to different slots. Vor dem Laden prueft die
        VRAM-Zulassung den geraeteweit freien Speicher (Paket 3/B).

        *content_id* benennt den INHALT des Slots (z. B. den Gewichts-SHA-256 eines
        Testkandidaten). Teilen sich mehrere Wrapper einen Slot, ist diese Kennung
        die einzige Wahrheit darueber, welches Modell gerade drinsteckt: Bei
        Abweichung entlaedt und laedt der Manager selbst. Frueher merkte sich jeder
        Wrapper seinen eigenen Hash in einer Modulvariablen und sah den Wechsel des
        anderen nicht — dann inferierte das FREMDE Modell (Audit 2026-08-14, S-H1).
        Slots mit nur einem festen Modell rufen ohne Kennung auf und verhalten sich
        unveraendert.
        """
        # Fast path: already loaded (nur bei exakt passendem Inhalt)
        state = self._slots.get(slot)
        if state is not None and state.model is not None and state.content_id == content_id:
            state.last_used = time.monotonic()
            return state

        # Slow path: acquire per-slot lock and load
        lock = self._locks.get(slot) or self._get_or_create_lock(slot)
        with lock:
            # Double-check after acquiring lock (Unload-Race: ein zwischenzeitlich
            # entladener Slot hat model=None und wird hier frisch geladen).
            state = self._slots.get(slot)
            if state is not None and state.model is not None:
                if state.content_id == content_id:
                    state.last_used = time.monotonic()
                    return state
                # Fremder Inhalt im gemeinsamen Slot: kontrolliert entladen, bevor
                # das eigene Gewicht geladen wird. Eine laufende Inferenz hat immer
                # Vorrang — dann lieber ein kontrollierter 503 als ein Modellwechsel
                # unter den Fuessen des anderen Aufrufers.
                if not self._unload_locked(slot):
                    raise ModelUnloadedError(slot.value)
                # Bereinigung VOR der VRAM-Zulassung: der eben freigegebene Speicher
                # soll dem neuen Modell zugutekommen. Laeuft ausserhalb _global_lock,
                # damit der Watchdog erreichbar bleibt.
                self._try_empty_cache()
                gc.collect()

            # Zulassung VOR dem Ladeversuch (geraeteweit freier VRAM, inkl. Ollama).
            # Paket 2/B4: Zulassung und In-flight-Reservierung atomar unter _global_lock —
            # ein parallel ladendes Modell sieht diese Reservierung sofort. Notwendige
            # Eviction-Bereinigung laeuft ausserhalb des Locks fuer den Watchdog.
            self._admit_vram_or_raise(slot, device)
            try:
                t0 = time.perf_counter()
                model, processor = loader()
                elapsed = time.perf_counter() - t0
            finally:
                with self._global_lock:
                    self._inflight_loads.pop(slot, None)

            state = SlotState(
                model=model,
                processor=processor,
                device=device,
                load_time_sec=elapsed,
                last_used=time.monotonic(),
                content_id=content_id,
            )
            with self._global_lock:
                self._slots[slot] = state
            logger.info(
                "Loaded %s in %.1fs on %s (persistent)", slot.value, elapsed, device
            )
            self._warn_if_over_budget()
            return state

    def unload(self, slot: ModelSlot) -> bool:
        """Explicitly unload a single slot. Liefert True, wenn entladen wurde.

        Lease-Schutz (Paket 2): UNMITTELBAR vor dem Entladen wird unter
        _global_lock erneut geprueft, ob der Slot gerade leased/busy ist — eine
        laufende Inferenz darf nie entladen werden (verlorenes Rennen: kein
        Entladen, kein Fehler, Aufrufer kann spaeter erneut versuchen).
        """
        lock = self._locks.get(slot)
        if lock is None:
            return False
        with lock:
            if not self._unload_locked(slot):
                return False
        self._try_empty_cache()
        gc.collect()
        return True

    def discard_foreign_content(self, slot: ModelSlot, content_id: str | None) -> bool:
        """Entlaedt den Slot, wenn ein ANDERER Inhalt darin liegt.

        Fuer geteilte Slots (YOLO_TEST): Der Aufrufer ruft das VOR seiner eigenen
        Busy-Lease auf, denn die eigene Lease wuerde das Entladen selbst sperren
        (Lease-Schutz). Danach laedt `ensure_loaded` unter der Lease das eigene
        Gewicht. Liefert True, wenn wirklich entladen wurde.
        """
        state = self._slots.get(slot)
        if state is None or state.model is None or state.content_id == content_id:
            return False
        return self.unload(slot)

    def _unload_locked(self, slot: ModelSlot) -> bool:
        """Entlaedt einen Slot; der Slot-Lock MUSS bereits gehalten werden.

        Liefert False, wenn nichts geladen war ODER eine Lease laeuft. Die
        CUDA-/GC-Bereinigung macht der Aufrufer nach dem Freigeben des Locks,
        damit der Watchdog auch bei haengendem empty_cache erreichbar bleibt.
        """
        with self._global_lock:
            if slot in self._busy:
                logger.warning(
                    "unload(%s) verweigert: Slot ist leased (laufende Inferenz).",
                    slot.value)
                return False
            state = self._slots.pop(slot, None)
        if state is None:
            return False
        logger.info("Unloading %s from %s ...", slot.value, state.device)
        # Referenzen auf None setzen (nicht del): haelt ein anderer Thread die SlotState ueber
        # den lockfreien Fast-Path noch, faellt der Zugriff auf einen definierten None-Wert statt
        # auf ein geloeschtes Attribut. Das Modell wird per Refcount freigegeben, sobald die
        # letzte Referenz faellt.
        state.model = None
        state.processor = None
        return True

    def unload_all(self) -> None:
        """Unload all loaded models (shutdown cleanup)."""
        slots = list(self._slots.keys())
        for slot in slots:
            self.unload(slot)
        logger.info("All model slots unloaded.")

    def get_status(self) -> dict:
        """Return status dict for /health endpoint.

        Keeps legacy keys (current_model, vram_allocated_gb, vram_total_gb)
        for backwards compatibility and adds loaded_models detail sowie
        Busy-/Waechter-Info pro Slot (Paket 3/A).
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
        busy_map = self.busy_snapshot()
        now = time.monotonic()
        loaded = {}
        # Busy-Info kommt aus dem Lease-Register — inkl. logischer Slots
        # (yolo_cpu/yolo_cls), die keinen SlotState haben (additiv in busy_slots).
        busy_slots = {
            slot.value: round(now - since, 1)
            for slot, since in busy_map.items()
        }
        for slot, state in snapshot:
            if state.model is not None:
                loaded[slot.value] = {
                    "device": state.device,
                    "load_time_sec": round(state.load_time_sec, 2),
                    "busy": slot in busy_map,
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
            "busy_slots": busy_slots,
            "watchdog": {
                "enabled": INFERENCE_WATCHDOG_SEC > 0,
                "limit_sec": INFERENCE_WATCHDOG_SEC,
            },
        }

    def empty_cache(self) -> None:
        """Gibt den CUDA-Cache frei (z.B. zur Erholung nach einem OOM-Fehler)."""
        self._try_empty_cache()
        gc.collect()

    # ── Busy-Leases + Inferenz-Waechter (Paket 3/A, Lease-Konzept Paket 2) ──

    def acquire_busy(self, slot: ModelSlot) -> str | None:
        """Lease-Erwerb: markiert den Slot als busy und liefert die Besitzer-ID.

        Rueckgabe None = keine Lease (no-op-kompatibel):
          - ModelSlot.NONE / unbekannter Slot: nichts zu ueberwachen.
          - Slot ist bereits leased: die AELTERE Lease hat Vorrang. Ein zweiter
            Erwerber darf weder die Busy-Uhr noch den Zustand verschieben — bei
            korrekter Predict-Lock-Serialisierung (Lease erst NACH dem Lock) kann
            dieser Fall nicht auftreten; er ist der Manipulationsschutz.
        Logische Slots (YOLO_CPU/YOLO_CLS) bekommen einen Register-Eintrag ohne
        SlotState/VRAM — so sieht der Waechter auch die Manager-umgangenen Pfade.
        """
        if slot == ModelSlot.NONE:
            return None
        now = time.monotonic()
        lease_id = uuid.uuid4().hex
        with self._global_lock:
            if slot in self._busy:
                logger.warning(
                    "acquire_busy(%s) abgelehnt: Slot bereits leased "
                    "(Predict-Lock-Serialisierung verletzt?).", slot.value)
                return None
            self._busy[slot] = _BusyLease(lease_id=lease_id, since=now)
            # last_used mitziehen, falls ein SlotState existiert (Evict-Schutz
            # zusaetzlich zur Lease; logische Slots haben keinen).
            state = self._slots.get(slot)
            if state is not None:
                state.last_used = now
        return lease_id

    def release_busy(self, slot: ModelSlot, lease_id: str | None) -> None:
        """Lease-Freigabe: loescht NUR bei Besitz-Uebereinstimmung.

        Fremdes/veraltetes Release (lease_id passt nicht zur aktiven Lease) wird
        still ignoriert — kein Fehler, kein Zustandswechsel. release(slot, None)
        ist ein sicherer No-op (passt zu acquire_busy -> None).
        """
        if lease_id is None:
            return
        with self._global_lock:
            lease = self._busy.get(slot)
            if lease is not None and lease.lease_id == lease_id:
                del self._busy[slot]

    def busy_snapshot(self) -> dict[ModelSlot, float]:
        """Konsistente Kopie der belegten Slots (slot -> busy_since) fuer den Waechter."""
        with self._global_lock:
            return {slot: lease.since for slot, lease in self._busy.items()}

    def start_watchdog(self) -> None:
        """Startet den Inferenz-Waechter (idempotent; No-op bei Limit 0)."""
        with self._global_lock:
            if self._watchdog is None:
                self._watchdog = InferenceWatchdog(self)
            watchdog = self._watchdog
        watchdog.start()

    def stop_watchdog(self) -> None:
        """Stoppt den Inferenz-Waechter (Shutdown)."""
        with self._global_lock:
            watchdog = self._watchdog
            self._watchdog = None
        if watchdog is not None:
            watchdog.stop()

    @contextmanager
    def busy_slot(self, slot: ModelSlot):
        """Kontextmanager: Lease beim Eintritt ziehen, garantiert beim Verlassen
        freigeben (nur die eigene). Liefert die lease_id (None = keine Lease).

        Aufrufmuster IMMER `with <predict_lock>, gpu_manager.busy_slot(slot):`
        — Lock ZUERST, Lease DANACH. Wartende Requests besitzen noch keine Lease
        und koennen die Busy-Uhr des laufenden Requests weder verschieben noch
        loeschen (Paket 2).
        """
        lease_id = self.acquire_busy(slot)
        try:
            yield lease_id
        finally:
            self.release_busy(slot, lease_id)

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
            logger.debug("CUDA-Speicherbelegung konnte nicht gelesen werden", exc_info=True)
        return 0.0

    def _warn_if_over_budget(self) -> None:
        alloc = self._allocated_gb()
        if alloc > VRAM_BUDGET_GB:
            loaded = [s.value for s, st in self._slots_snapshot() if st.model is not None]
            logger.warning(
                "VRAM ueber Budget: %.1f GB > %.1f GB (geladen: %s). evict_lru()/unload() erwaegen.",
                alloc, VRAM_BUDGET_GB, ", ".join(loaded),
            )

    def evict_lru(self, exclude: Iterable[ModelSlot] | None = None) -> Optional[ModelSlot]:
        """Entlaedt den aeltesten FREIEN Slot (LRU). Gibt den Slot zurueck oder None.

        Leased/busy Slots (laufender Request) und explizit ausgeschlossene Slots
        werden NIE evictiert. ATOMAR (Paket 2): Auswahl, letzte Lease-Pruefung und
        Reservierung (pop aus dem Register) laufen unter EINEM _global_lock —
        danach ist der Slot fuer alle anderen unsichtbar (kein TOCTOU mehr, kein
        zweiter Evicter kann ihn waehlen). Erst danach werden die Modellreferenzen
        ausserhalb des Locks genullt. Bei verlorenem Rennen (Slot zwischenzeitlich
        leased oder weg) wird der naechste Kandidat versucht; kein sicherer
        Kandidat -> None (der Zulassungs-Loop endet dann in InsufficientVramError,
        kein 500er, kein Deadlock — _global_lock wartet nie auf Slot-Locks).
        """
        excluded = set(exclude) if exclude else set()
        with self._global_lock:
            reserved = self._take_lru_victim_locked(excluded)
        if reserved is None:
            return None
        victim, state = reserved
        self._cleanup_evicted_state(victim, state)
        return victim

    def _admit_vram_or_raise(self, slot: ModelSlot, device: str) -> None:
        """Zulassungspruefung VOR dem Laden (Paket 3/B, erstes Netz).

        Nutzt torch.cuda.mem_get_info: der GERAETEWEIT freie Speicher, also inklusive
        aller anderen Prozesse auf der Karte (v. a. Ollama/Qwen). Zugelassen wird nur,
        wenn effektiv frei >= Modellschaetzung + Ollama-Reserve; effektiv frei heisst:
        abzueglich gerade laufender Lade-Reservierungen anderer Slots (Paket 2/B4 —
        zwei gleichzeitig ladende Modelle duerfen nicht denselben freien VRAM sehen).
        Bei Nicht-Zulassung zuerst LRU-Eviction freier Slots (niemals busy, niemals den
        Zielslot), dann erneut pruefen; reicht es immer noch nicht: InsufficientVramError
        -> 503 OHNE Ladeversuch. Die bisherige Post-Load-Warnung bleibt als zweites Netz
        bestehen. CPU-Geraete werden nicht begrenzt; ohne CUDA/torch wird wie bisher geladen.
        reserved_gb im Fehler = Ollama-Reserve + laufende Lade-Reservierungen.
        """
        if not str(device).startswith("cuda"):
            return

        estimate = MODEL_VRAM_ESTIMATE_GB.get(slot, 0.0)
        required = estimate + VRAM_RESERVE_GB
        warned = False
        while True:
            free = self._device_free_vram_gb()
            with self._global_lock:
                inflight = sum(self._inflight_loads.values())
                effective = None if free is None else free - inflight
                if effective is None or effective >= required:
                    self._inflight_loads[slot] = estimate
                    return
                reserved_gb = VRAM_RESERVE_GB + inflight
                eviction = self._take_lru_victim_locked({slot})

            if not warned:
                logger.warning(
                    "VRAM-Zulassung %s: %.1f GB effektiv frei < %.1f GB benoetigt "
                    "(Schaetzung %.1f GB + Reserve %.1f GB, davon %.1f GB laufende Ladevorgaenge) "
                    "— versuche LRU-Eviction freier Slots.",
                    slot.value, effective, required, estimate, VRAM_RESERVE_GB, inflight)
                warned = True

            if eviction is None:
                raise InsufficientVramError(slot, free, required, reserved_gb)

            victim, state = eviction
            self._cleanup_evicted_state(victim, state)

    def _take_lru_victim_locked(
        self,
        excluded: set[ModelSlot],
    ) -> Optional[Tuple[ModelSlot, SlotState]]:
        """Reserviert unter _global_lock genau einen freien LRU-Slot zum Entladen."""
        candidates = [
            (state.last_used, candidate)
            for candidate, state in self._slots.items()
            if state.model is not None
            and candidate not in self._busy
            and candidate not in excluded
        ]
        if not candidates:
            return None
        _, victim = min(candidates, key=lambda item: item[0])
        return victim, self._slots.pop(victim)

    def _cleanup_evicted_state(self, victim: ModelSlot, state: SlotState) -> None:
        """Gibt Modellreferenzen und CUDA-Cache ohne gehaltenen globalen Lock frei."""
        logger.info("evict_lru: entlade %s (LRU)", victim.value)
        state.model = None
        state.processor = None
        self._try_empty_cache()
        gc.collect()

    @staticmethod
    def _device_free_vram_gb() -> float | None:
        """Geraeteweit freier VRAM (GB) inkl. Fremdprozesse (z.B. Ollama); None ohne CUDA."""
        try:
            import torch
            if torch.cuda.is_available():
                free, _total = torch.cuda.mem_get_info(0)
                return free / (1024**3)
        except Exception:
            logger.debug("Freier CUDA-Speicher konnte nicht gelesen werden", exc_info=True)
        return None

    @staticmethod
    def _try_empty_cache() -> None:
        try:
            import torch
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
        except Exception:
            logger.debug("CUDA-Zwischenspeicher konnte nicht geleert werden", exc_info=True)


# Singleton
gpu_manager = GpuModelManager()
