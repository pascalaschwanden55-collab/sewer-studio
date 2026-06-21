"""GPU-freier Test fuer evict_lru (Audit Fix #5).

Der OOM-Handler in main.py ruft bei CUDA-Out-of-Memory gpu_manager.evict_lru() auf,
um den am laengsten ungenutzten Slot zu entladen (statt nur den Cache zu leeren).
Hier wird nur die LRU-Mechanik geprueft — ohne echtes Modell/GPU (loader gibt ein
Dummy-Tupel zurueck, torch wird in get_status lazy/abgesichert importiert).
"""

from sidecar.gpu_manager import GpuModelManager, ModelSlot


def test_evict_lru_unloads_least_recently_used():
    mgr = GpuModelManager()
    mgr.ensure_loaded(ModelSlot.YOLO, "cpu", lambda: ("yolo_model", None))
    mgr.ensure_loaded(ModelSlot.DINO, "cpu", lambda: ("dino_model", None))

    # YOLO wurde zuerst geladen -> aeltestes last_used -> wird zuerst evicted.
    victim = mgr.evict_lru()

    assert victim == ModelSlot.YOLO
    status = mgr.get_status()
    assert "yolo" not in status["loaded_models"]
    assert "dino" in status["loaded_models"]


def test_evict_lru_on_empty_manager_returns_none():
    mgr = GpuModelManager()
    assert mgr.evict_lru() is None
