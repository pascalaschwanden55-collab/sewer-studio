"""Nur Standardbibliothek, echte Managerlogik mit kuenstlicher VRAM-Messung; keine GPU/Modelle."""
import importlib.util
import json
from pathlib import Path
import sys
import threading

path = Path(__file__).resolve().parents[3] / "sidecar/sidecar/gpu_manager.py"
spec = importlib.util.spec_from_file_location("audit_gpu_manager", path)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
module.VRAM_RESERVE_GB = 12.0
manager = module.GpuModelManager()
manager._warn_if_over_budget = lambda: None

memory = {"free": 20.0}
a_loading = threading.Event()
b_measured = threading.Event()
a_loaded = threading.Event()
b_done = threading.Event()
errors = []
loaded = []

def free_memory():
    snapshot = memory["free"]
    if threading.current_thread().name == "B":
        b_measured.set()
        if not a_loaded.wait(3):
            raise RuntimeError("A did not finish")
    return snapshot

manager._device_free_vram_gb = free_memory

def a_loader():
    a_loading.set()
    if not b_measured.wait(3):
        raise RuntimeError("B did not measure")
    memory["free"] -= 4.0
    return object(), None

def b_loader():
    memory["free"] -= 6.0
    return object(), None

def a_run():
    try:
        with manager.busy_slot(module.ModelSlot.DINO):
            manager.ensure_loaded(module.ModelSlot.DINO, "cuda:0", a_loader)
            loaded.append("DINO 4 GB")
            a_loaded.set()
            b_done.wait(3)
    except Exception as exc:
        errors.append(type(exc).__name__ + ": " + str(exc))

def b_run():
    try:
        if not a_loading.wait(3):
            raise RuntimeError("A not loading")
        with manager.busy_slot(module.ModelSlot.SAM):
            manager.ensure_loaded(module.ModelSlot.SAM, "cuda:0", b_loader)
            loaded.append("SAM 6 GB")
    except Exception as exc:
        errors.append(type(exc).__name__ + ": " + str(exc))
    finally:
        b_done.set()

threads = [threading.Thread(target=a_run, name="A"), threading.Thread(target=b_run, name="B")]
for thread in threads:
    thread.start()
for thread in threads:
    thread.join(5)
print(json.dumps({"test": "parallel_stale_free_measurement", "initial_free_gb": 20, "required_reserve_gb": 12,
                  "loaded": loaded, "fake_free_after_loads_gb": memory["free"],
                  "errors": errors, "safetyHolds": memory["free"] >= module.VRAM_RESERVE_GB}))

accurate = module.GpuModelManager()
accurate._device_free_vram_gb = lambda: 16.0
try:
    accurate._admit_vram_or_raise(module.ModelSlot.SAM, "cuda:0")
    guarded = False
except module.InsufficientVramError:
    guarded = True
print(json.dumps({"test": "fresh_free_measurement", "free_gb": 16, "required_gb": 18,
                  "safetyHolds": guarded}))
