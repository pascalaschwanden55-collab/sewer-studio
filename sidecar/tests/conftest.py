"""Pytest fixtures + global setup fuer den Sidecar.

Audit 2026-05-17 (Nachzieh):
- Auth-Bypass: ``SEWER_SIDECAR_AUTH=disabled`` wird VOR dem Import von
  ``sidecar.main`` gesetzt. Sonst aktiviert die Middleware Token-Auth
  (Token aus %LOCALAPPDATA%/SewerStudio/.sidecar_token), und alle
  TestClient-Tests scheitern mit 401.
- Marker ``live``: Tests die einen laufenden Sidecar auf localhost:8100
  brauchen (test_batch_endpoints.py). Werden per Default uebersprungen
  ausser ``--run-live`` ist gesetzt.
- Marker ``model``: Tests die echte Modellgewichte laden (YOLO, SAM,
  DINO via TestClient + Lifespan). Werden per Default uebersprungen
  ausser ``--run-model`` ist gesetzt. Damit ist ``pytest sidecar/tests``
  auf einer frischen Linux-Station ohne Modelle reproduzierbar gruen.
- Pytest-Cache + basetemp: ``pyproject.toml`` setzt ``--basetemp`` auf
  ``.tmp/pytest/basetemp``. Diese conftest sorgt zusaetzlich dafuer dass
  der Ordner existiert und der Cache nicht in ``sidecar/.pytest_cache``
  landet (Permission-Probleme unter Windows mit aktiver venv).
"""

from __future__ import annotations

import os
from pathlib import Path

import pytest

# Auth komplett deaktivieren — VOR sidecar.main importiert wird.
os.environ.setdefault("SEWER_SIDECAR_AUTH", "disabled")

# basetemp-Ordner sicherstellen (pyproject.toml setzt nur den Pfad,
# pytest legt das Verzeichnis erst beim Bedarf an — wir sorgen vor).
_REPO_ROOT = Path(__file__).resolve().parents[2]
_BASE_TMP = _REPO_ROOT / ".tmp" / "pytest" / "basetemp"
_BASE_TMP.mkdir(parents=True, exist_ok=True)


def pytest_addoption(parser: pytest.Parser) -> None:
    parser.addoption(
        "--run-live",
        action="store_true",
        default=False,
        help="Live-Tests aktivieren (brauchen laufenden Sidecar auf localhost:8100).",
    )
    parser.addoption(
        "--run-model",
        action="store_true",
        default=False,
        help="Modell-Tests aktivieren (laden YOLO/SAM/DINO via TestClient+Lifespan, brauchen lokale Gewichte + GPU).",
    )


def pytest_configure(config: pytest.Config) -> None:
    # Marker werden zentral via pyproject.toml [tool.pytest.ini_options].markers
    # registriert, damit "pytest --markers" sie auch ohne diesen Hook listet.
    # Hier nur Cache-Redirect.
    cache_dir = _REPO_ROOT / ".tmp" / "pytest" / "sidecar-cache"
    cache_dir.mkdir(parents=True, exist_ok=True)
    # Private API, aber bewusst: keine offizielle Schnittstelle fuer Cache-Path.
    config.cache._cachedir = cache_dir  # type: ignore[attr-defined]


def pytest_collection_modifyitems(config: pytest.Config, items: list[pytest.Item]) -> None:
    run_live = config.getoption("--run-live")
    run_model = config.getoption("--run-model")

    skip_live = pytest.mark.skip(
        reason="Live-Test (braucht laufenden Sidecar). Aktivieren mit --run-live."
    )
    skip_model = pytest.mark.skip(
        reason="Model-Test (braucht echte Gewichte + GPU). Aktivieren mit --run-model."
    )

    for item in items:
        if "live" in item.keywords and not run_live:
            item.add_marker(skip_live)
        if "model" in item.keywords and not run_model:
            item.add_marker(skip_model)
