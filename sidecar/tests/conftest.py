import pytest


@pytest.fixture(autouse=True)
def sidecar_security_defaults_for_tests(monkeypatch):
    from sidecar.config import settings

    monkeypatch.setattr(
        settings,
        "trusted_hosts",
        "127.0.0.1,localhost,testserver",
        raising=False,
    )
    monkeypatch.setattr(settings, "auth_token", "", raising=False)
