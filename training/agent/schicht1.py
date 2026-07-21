"""Schicht 1 — duenne Wrapper um die deterministischen Trainings-Skripte.

Jede Funktion ruft ein Skript unter training/scripts/ auf. Solange dieses Skript
(aus Trainingsplan Phase 0/2) noch nicht existiert, meldet der Wrapper das ehrlich
zurueck ('TODO: Skript fehlt'), statt etwas zu faken. So ist der Agent bereits jetzt
lauffaehig und wird Schritt fuer Schritt 'scharf', sobald die Skripte entstehen.

Rueckgabe immer als dict: {"ok": bool, "message": str, "data": dict|None}.
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path
from typing import Optional

from config import DETECT_IMGSZ, SCRIPTS_DIR


def _run_script(script_name: str, args: list[str]) -> dict:
    """Fuehrt training/scripts/<script_name> aus, wenn vorhanden; sonst ehrliche TODO-Meldung."""
    script = Path(SCRIPTS_DIR) / script_name
    if not script.exists():
        return {
            "ok": False,
            "message": (
                f"TODO: Skript '{script}' existiert noch nicht. "
                f"Wird in Trainingsplan Phase 0/2 gebaut. Bis dahin ist dieser Schritt ein No-Op."
            ),
            "data": None,
        }
    try:
        proc = subprocess.run(
            [sys.executable, str(script), *args],
            capture_output=True, text=True, timeout=60 * 60 * 12,  # bis 12 h fuer Trainingslaeufe
        )
        ok = proc.returncode == 0
        tail = (proc.stdout or "")[-2000:]
        err = (proc.stderr or "")[-1000:]
        return {
            "ok": ok,
            "message": f"exit={proc.returncode}\n--- stdout(tail) ---\n{tail}"
                       + (f"\n--- stderr(tail) ---\n{err}" if err else ""),
            "data": {"returncode": proc.returncode},
        }
    except subprocess.TimeoutExpired:
        return {"ok": False, "message": f"Timeout bei '{script_name}'.", "data": None}


# ── Konkrete Wrapper (Schnittstellen stabil, Implementierung folgt via Skripte) ──
def export_dataset(dataset_version: str) -> dict:
    """ExportPlanner + Export (Trainingsplan v1.2 AP 0.3). Haltungs-sauberer Split, feste class_map."""
    return _run_script("export_dataset.py", ["--version", dataset_version])


def train_detect(dataset_version: str, imgsz: int = DETECT_IMGSZ) -> dict:
    """YOLO-Detect-Training (imgsz 1280 Baseline)."""
    return _run_script("train_detect.py", ["--dataset", dataset_version, "--imgsz", str(imgsz)])


def run_eval(model_paket: str, split: str = "devval") -> dict:
    """Eval-Harness auf Dev-Val (Ereignis-Metriken). Abnahme wird per Guardrail verhindert."""
    return _run_script("run_eval.py", ["--model", model_paket, "--split", split])


def doppellauf(candidate: str, videolist: str) -> dict:
    """Offline-A/B-Vergleich alt vs. neu auf fester Videoliste + Ereignis-Diff (v1.2 Abschnitt G)."""
    return _run_script("doppellauf.py", ["--candidate", candidate, "--videos", videolist])
