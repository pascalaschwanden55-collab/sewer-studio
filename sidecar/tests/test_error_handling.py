"""Tests fuer den zentralen Exception-Handler des Sidecars.

Unbehandelte Fehler duerfen nie als roher 500-Stacktrace nach aussen gelangen;
OOM und fehlende Modelle muessen als 503 sauber signalisiert werden.
"""

from fastapi.testclient import TestClient
import pytest

from sidecar.main import app
from sidecar.models import yolo_wrapper

# Trusted-Host-Header, damit die Loopback-Middleware den Request durchlaesst.
_HEADERS = {"host": "localhost"}
_BODY = {"image_base64": "x", "confidence_threshold": 0.25}


def _client() -> TestClient:
    # raise_server_exceptions=False: Handler-Response statt Re-Raise im Test.
    return TestClient(app, raise_server_exceptions=False)


def test_unexpected_error_returns_generic_500_without_stacktrace(monkeypatch):
    def boom(*_args, **_kwargs):
        raise RuntimeError("geheimer interner stacktrace-text")

    monkeypatch.setattr(yolo_wrapper, "detect", boom)

    resp = _client().post("/detect/yolo", json=_BODY, headers=_HEADERS)

    assert resp.status_code == 500
    assert resp.json()["detail"] == "internal error"
    assert "geheimer" not in resp.text  # kein Leak des Trace-Inhalts


def test_missing_model_returns_503(monkeypatch):
    def boom(*_args, **_kwargs):
        raise FileNotFoundError("weights nicht gefunden")

    monkeypatch.setattr(yolo_wrapper, "detect", boom)

    resp = _client().post("/detect/yolo", json=_BODY, headers=_HEADERS)

    assert resp.status_code == 503
    assert resp.json()["detail"] == "model unavailable"


def test_cuda_oom_returns_503(monkeypatch):
    class OutOfMemoryError(RuntimeError):
        """Simuliert torch.cuda.OutOfMemoryError (per Typname erkannt)."""

    def boom(*_args, **_kwargs):
        raise OutOfMemoryError("CUDA out of memory")

    monkeypatch.setattr(yolo_wrapper, "detect", boom)

    resp = _client().post("/detect/yolo", json=_BODY, headers=_HEADERS)

    assert resp.status_code == 503
    assert resp.json()["detail"] == "GPU out of memory"
