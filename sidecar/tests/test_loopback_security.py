from fastapi.testclient import TestClient


def test_trusted_host_rejects_untrusted_host(monkeypatch):
    from sidecar.config import settings
    from sidecar.main import app

    monkeypatch.setattr(settings, "trusted_hosts", "127.0.0.1,localhost", raising=False)
    monkeypatch.setattr(settings, "auth_token", "", raising=False)

    client = TestClient(app, base_url="http://evil.example")
    resp = client.get("/health")

    assert resp.status_code == 403


def test_auth_token_rejects_missing_or_wrong_token(monkeypatch):
    from sidecar.config import settings
    from sidecar.main import app

    monkeypatch.setattr(settings, "trusted_hosts", "127.0.0.1,localhost", raising=False)
    monkeypatch.setattr(settings, "auth_token", "secret-token", raising=False)

    client = TestClient(app, base_url="http://localhost")

    missing = client.get("/health")
    wrong = client.get("/health", headers={"X-Sidecar-Token": "wrong-token"})
    ok = client.get("/health", headers={"X-Sidecar-Token": "secret-token"})

    assert missing.status_code == 401
    assert wrong.status_code == 401
    assert ok.status_code == 200
