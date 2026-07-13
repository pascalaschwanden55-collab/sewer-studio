import pytest


@pytest.fixture(autouse=True)
def sidecar_security_defaults_for_tests(monkeypatch):
    from fastapi.testclient import TestClient
    from sidecar.config import settings

    test_token = "sidecar-test-token"
    monkeypatch.setattr(
        settings,
        "trusted_hosts",
        "127.0.0.1,localhost,testserver",
        raising=False,
    )
    monkeypatch.setattr(settings, "auth_token", test_token, raising=False)

    # Normale Routentests arbeiten mit einem ausdruecklichen Test-Token. Sicherheitstests,
    # die settings.auth_token selbst auf einen anderen oder leeren Wert setzen, erhalten
    # bewusst keinen automatisch ergaenzten Header.
    original_request = TestClient.request

    def request_with_test_token(client, *args, **kwargs):
        if settings.auth_token == test_token:
            headers = dict(kwargs.get("headers") or {})
            headers.setdefault("X-Sidecar-Token", test_token)
            kwargs["headers"] = headers
        return original_request(client, *args, **kwargs)

    monkeypatch.setattr(TestClient, "request", request_with_test_token)
