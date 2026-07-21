"""Erkennung von CUDA-Out-of-Memory- und CUDA-/Treiberfehlern ohne harten torch-Import.

Gemeinsames Modul, damit sowohl der zentrale Exception-Handler (main.py) als auch die
Modell-Wrapper (sam_wrapper, dino_wrapper) dieselben Kriterien nutzen koennen — ohne einen
Importzyklus (main -> routes -> models -> main) zu erzeugen.
"""

from __future__ import annotations


def looks_like_oom(exc: BaseException) -> bool:
    """Erkennt CUDA-Out-of-Memory ohne harten torch-Import."""
    if "OutOfMemory" in type(exc).__name__:
        return True
    return "out of memory" in str(exc).lower()


def looks_like_cuda_failure(exc: BaseException) -> bool:
    """Erkennt typische CUDA-/Treiberfehler ohne harten torch-Import."""
    type_name = type(exc).__name__.lower()
    message = str(exc).lower()
    if "cuda" in type_name:
        return True

    markers = (
        "cuda error",
        "cuda runtime",
        "cuda driver",
        "cuda initialization",
        "cuda-capable device",
        "cuda gpus",
        "cublas",
        "cudnn",
        "nvrtc",
        "device-side assert",
        "invalid device ordinal",
        "unspecified launch failure",
        "illegal memory access",
    )
    return any(marker in message for marker in markers)
