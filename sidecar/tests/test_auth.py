"""Tests fuer die Auth-Token-Erzwingung (Audit-Befund #3).

GPU-frei: prueft die Middleware-Erzwingung (401 ohne/falschem Token, 200 mit korrektem)
und die Token-Aufloesung (env -> Datei -> neu erzeugen).
"""

import pytest
from fastapi.testclient import TestClient


@pytest.fixture
def app():
    from sidecar.main import app as a
    return a


def test_health_401_without_or_wrong_token(app, monkeypatch):
    from sidecar.config import settings
    monkeypatch.setattr(settings, "auth_token", "secret-123", raising=False)
    client = TestClient(app)

    assert client.get("/health").status_code == 401                                  # ohne Header
    assert client.get("/health", headers={"X-Sidecar-Token": "wrong"}).status_code == 401  # falsch
    assert client.get("/health", headers={"X-Sidecar-Token": "secret-123"}).status_code == 200  # korrekt


def test_empty_server_token_fails_closed(app, monkeypatch):
    from sidecar.config import settings
    monkeypatch.setattr(settings, "auth_token", "", raising=False)
    client = TestClient(app)

    response = client.get("/health")

    assert response.status_code == 503
    assert response.json() == {
        "detail": "Sidecar authentication is not initialized.",
        "code": "auth_unavailable",
    }


def test_resolve_creates_token_when_missing(monkeypatch, tmp_path):
    from sidecar.config import settings
    import sidecar.main as main
    tf = tmp_path / "SewerStudio" / ".sidecar_token"
    monkeypatch.setattr(settings, "auth_token", "", raising=False)
    monkeypatch.setattr(settings, "auth_token_file", str(tf), raising=False)

    tok = main._resolve_or_create_token()
    assert tok and len(tok) >= 20
    assert tf.read_text(encoding="utf-8").strip() == tok          # in Datei geschrieben


def test_resolve_reads_existing_token(monkeypatch, tmp_path):
    from sidecar.config import settings
    import sidecar.main as main
    tf = tmp_path / ".sidecar_token"
    tf.write_text("vorhandenes-token\n", encoding="utf-8")
    monkeypatch.setattr(settings, "auth_token", "", raising=False)
    monkeypatch.setattr(settings, "auth_token_file", str(tf), raising=False)

    assert main._resolve_or_create_token() == "vorhandenes-token"  # wiederverwendet, nicht ueberschrieben


def test_resolve_env_token_wins(monkeypatch, tmp_path):
    from sidecar.config import settings
    import sidecar.main as main
    monkeypatch.setattr(settings, "auth_token", "env-token", raising=False)
    monkeypatch.setattr(settings, "auth_token_file", str(tmp_path / "ignored"), raising=False)

    assert main._resolve_or_create_token() == "env-token"


def test_resolve_fails_when_generated_token_cannot_be_persisted(monkeypatch, tmp_path):
    from sidecar.config import settings
    import sidecar.main as main

    blocking_parent = tmp_path / "not-a-directory"
    blocking_parent.write_text("blockiert", encoding="utf-8")
    monkeypatch.setattr(settings, "auth_token", "", raising=False)
    monkeypatch.setattr(settings, "auth_token_file", str(blocking_parent / ".sidecar_token"), raising=False)

    with pytest.raises(RuntimeError, match="Sidecar-Token konnte nicht geschrieben werden"):
        main._resolve_or_create_token()
