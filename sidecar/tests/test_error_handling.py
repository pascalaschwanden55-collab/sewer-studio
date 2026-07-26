"""Tests fuer den zentralen Exception-Handler des Sidecars.

Unbehandelte Fehler duerfen nie als roher 500-Stacktrace nach aussen gelangen;
OOM und fehlende Modelle muessen als 503 sauber signalisiert werden.
"""

from fastapi.testclient import TestClient
import pytest

from sidecar.main import app
from sidecar.models import detector_qualification, yolo_wrapper

# Trusted-Host-Header, damit die Loopback-Middleware den Request durchlaesst.
_HEADERS = {"host": "localhost"}
_BODY = {"image_base64": "x", "confidence_threshold": 0.25}


def _client() -> TestClient:
    # raise_server_exceptions=False: Handler-Response statt Re-Raise im Test.
    return TestClient(app, raise_server_exceptions=False)


@pytest.fixture(autouse=True)
def qualified_standard_detector(monkeypatch):
    """These tests exercise inference errors behind the qualification gate."""

    monkeypatch.setattr(
        detector_qualification,
        "evaluate_active_detector",
        lambda: {
            "qualified": True,
            "status": "qualified",
            "reason": None,
            "artifact": {
                "file_name": "detector.pt",
                "sha256": "a" * 64,
                "backend": "pytorch",
                "loaded": False,
            },
            "marked_utc": "2026-07-25T00:00:00Z",
        },
    )


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


def test_cuda_runtime_error_returns_clear_503_without_internal_detail(monkeypatch):
    def boom(*_args, **_kwargs):
        raise RuntimeError("CUDA error: invalid device ordinal; geheimer Treiberpfad")

    monkeypatch.setattr(yolo_wrapper, "detect", boom)

    resp = _client().post("/detect/yolo", json=_BODY, headers=_HEADERS)

    assert resp.status_code == 503
    assert resp.json() == {
        "detail": "GPU/CUDA temporarily unavailable",
        "code": "cuda_unavailable",
    }
    assert "Treiberpfad" not in resp.text
