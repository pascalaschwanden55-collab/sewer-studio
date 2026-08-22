"""Guardrails des Training-Ops-Agenten.

Wichtig: Diese Pruefungen leben im CODE, nicht im Prompt. Der Agent kann sie nicht
"wegreden" — ein Tool verweigert die Ausfuehrung, wenn ein Guard ausloest.

Bewusst nur Standardbibliothek -> ohne LLM-SDK importierbar und damit einfach testbar.
"""
from __future__ import annotations

import shutil
import socket
import subprocess
import urllib.error
import urllib.request
from typing import Optional

from config import (
    MIN_FREE_VRAM_MB,
    SEALED_SPLIT_TOKENS,
    SIDECAR_HEALTH_URL,
)


class GuardrailViolation(Exception):
    """Wird geworfen, wenn eine Aktion gegen eine Sicherheitsregel verstoesst."""


# ── VRAM / Sidecar ───────────────────────────────────────────────────────────
def sidecar_running(timeout: float = 1.5) -> bool:
    """True, wenn der Sidecar erreichbar ist (== Prozess laeuft == haelt evtl. VRAM).

    Auch ein HTTP-Fehler oder ein offener Sidecar-Port gilt als erreichbar.
    """
    try:
        with urllib.request.urlopen(SIDECAR_HEALTH_URL, timeout=timeout) as resp:
            return 200 <= resp.status < 300
    except urllib.error.HTTPError:
        return True
    except Exception:
        try:
            with socket.create_connection(("127.0.0.1", 8100), timeout=timeout):
                return True
        except OSError:
            return False


def gpu_free_vram_mb() -> Optional[int]:
    """Freier VRAM in MB laut nvidia-smi; None, wenn nvidia-smi fehlt/fehlschlaegt."""
    exe = shutil.which("nvidia-smi")
    if not exe:
        return None
    try:
        out = subprocess.run(
            [exe, "--query-gpu=memory.free", "--format=csv,noheader,nounits"],
            capture_output=True, text=True, timeout=5, check=True,
        ).stdout.strip().splitlines()
        # erste GPU (RTX 5090)
        return int(out[0].strip()) if out else None
    except Exception:
        return None


def ensure_gpu_free_for_training(min_free_mb: int = MIN_FREE_VRAM_MB) -> None:
    """Erlaubt Training nur, wenn der Sidecar aus ist UND genug VRAM frei ist.

    Schuetzt das 29-GB-Laufzeitbudget: Training und produktive Inferenz nie gleichzeitig.
    """
    if sidecar_running():
        raise GuardrailViolation(
            "Sidecar laeuft (Health erreichbar). Training wuerde mit der Inferenz um VRAM "
            "konkurrieren. Bitte Sidecar stoppen und erneut versuchen."
        )
    free = gpu_free_vram_mb()
    if free is not None and free < min_free_mb:
        raise GuardrailViolation(
            f"Zu wenig freier VRAM: {free} MB < {min_free_mb} MB. Andere GPU-Last beenden."
        )
    # free is None -> nvidia-smi nicht verfuegbar: wir blockieren nicht, warnen aber im Tool.


# ── Versiegelte Splits ───────────────────────────────────────────────────────
def is_sealed_split(name: str) -> bool:
    """True, wenn der Split/Datensatzname auf das versiegelte Abnahme-/Gold-Set zeigt."""
    low = (name or "").lower()
    return any(tok in low for tok in SEALED_SPLIT_TOKENS)


def assert_eval_split_allowed(split: str) -> None:
    """Verbietet jede Auswertung auf dem versiegelten Abnahme-Set (nur Dev-Val erlaubt)."""
    if is_sealed_split(split):
        raise GuardrailViolation(
            f"Split '{split}' ist versiegelt (Abnahme/Gold). Der Agent darf ausschliesslich "
            "auf Dev-Val messen. Die Abnahme laeuft manuell je Release-Kandidat."
        )
