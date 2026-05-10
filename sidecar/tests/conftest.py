"""Gemeinsame Test-Fixtures fuer den Sidecar-Testbaum.

Trennt Live-Tests (gegen einen laufenden Sidecar via httpx) von In-Process-
Tests (FastAPI TestClient direkt am `app`). Live-Tests muessen den
`X-Sidecar-Token`-Header mitsenden, weil der Sidecar in Phase 4.1
fail-closed Auth einfuehrt.

Marker:
- `live` : Test trifft einen laufenden Sidecar via HTTP. Wird im Default-
           pytest-Lauf nicht ausgefuehrt; via `pytest -m live` aktivierbar.

Fixtures:
- `sidecar_token` : Liefert den Token aus `SEWER_SIDECAR_TOKEN` bzw.
                    `SIDECAR_TOKEN`. Skipt den Test, wenn keiner gesetzt
                    ist (Live-Pfad braucht echten Token, sonst 401).
- `auth_headers`  : Dict mit `X-Sidecar-Token` fuer httpx.Client(headers=...)
                    bzw. einzelne Requests.
"""

from __future__ import annotations

import os

import pytest


def pytest_configure(config: pytest.Config) -> None:
    """Marker registrieren, damit Standard-Lauf keine 'unknown marker'-Warnung wirft."""
    config.addinivalue_line(
        "markers",
        "live: Test trifft einen laufenden Sidecar via HTTP (braucht "
        "SEWER_SIDECAR_TOKEN env var). Default-Lauf skippt diese Tests.",
    )


def pytest_collection_modifyitems(
    config: pytest.Config, items: list[pytest.Item]
) -> None:
    """Default-Lauf skippt Live-Tests, sofern der User nicht explizit `-m live` waehlt.

    Begruendung: Der CI-Lauf hat keinen Sidecar-Prozess und wuerde sonst
    bei jedem Live-Test 401 sehen oder Connection-Refused. Mit dieser
    Hook werden Live-Tests automatisch geskippt — bis ein Entwickler
    `pytest -m live` ausfuehrt und vorher den Sidecar gestartet hat.
    """
    marker_expr = config.getoption("-m") or ""
    if "live" in marker_expr:
        return  # User hat live explizit gewaehlt — nicht skippen.

    skip_live = pytest.mark.skip(
        reason="Live-Sidecar-Test (braucht laufenden Sidecar + "
        "SEWER_SIDECAR_TOKEN). Aktivieren via `pytest -m live`."
    )
    for item in items:
        if "live" in item.keywords:
            item.add_marker(skip_live)


@pytest.fixture(scope="session")
def sidecar_token() -> str:
    """Liefert den Sidecar-Auth-Token aus der Umgebung.

    Skipt den Test, wenn weder `SEWER_SIDECAR_TOKEN` noch `SIDECAR_TOKEN`
    gesetzt ist — Live-Tests koennen nicht sinnvoll ohne Token laufen,
    weil der Sidecar fail-closed mit 401 antwortet.
    """
    token = (
        os.environ.get("SEWER_SIDECAR_TOKEN", "").strip()
        or os.environ.get("SIDECAR_TOKEN", "").strip()
    )
    if not token:
        pytest.skip(
            "Kein SEWER_SIDECAR_TOKEN gesetzt — Live-Test wird uebersprungen."
        )
    return token


@pytest.fixture(scope="session")
def auth_headers(sidecar_token: str) -> dict[str, str]:
    """Header-Dict zum Direkt-Mitgeben an httpx-Calls.

    Verwendung:
        resp = client.post("/detect/yolo", json={...}, headers=auth_headers)

    Oder beim Client-Setup als Default-Header:
        with httpx.Client(base_url=SIDECAR_URL, headers=auth_headers) as c:
            ...
    """
    return {"X-Sidecar-Token": sidecar_token}
