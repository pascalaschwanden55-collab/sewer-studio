"""Pytest fixtures + global setup fuer den Sidecar.

Audit 2026-05-17 (Nachzieh-v2):
- Auth-Bypass: ``SEWER_SIDECAR_AUTH=disabled`` wird VOR dem Import von
  ``sidecar.main`` gesetzt. Sonst aktiviert die Middleware Token-Auth
  (Token aus %LOCALAPPDATA%/SewerStudio/.sidecar_token), und alle
  TestClient-Tests scheitern mit 401.
- Marker ``live``: Tests die einen laufenden Sidecar auf localhost:8100
  brauchen. Skip ausser ``--run-live``.
- Marker ``model``: Tests die echte Modellgewichte laden. Skip ausser
  ``--run-model``. Damit ist ``pytest sidecar/tests`` auf einer frischen
  Maschine ohne Modelle reproduzierbar gruen.
- basetemp: NICHT mehr in pyproject.toml hartkodiert. Pytest-Default
  wird bevorzugt; wenn dessen Temp-Root nicht les-/schreibbar ist, setzt
  diese conftest einen lokalen Fallback unter ``sidecar/.pytest_tmp``.
- Cache-Redirect ist best-effort: erst Repo-``.tmp``, dann lokaler
  ``sidecar/.pytest_tmp``-Fallback. Wenn beides nicht geht, bleibt pytest
  bei seinem Default.
"""

from __future__ import annotations

import getpass
import os
import tempfile
from pathlib import Path

import pytest

# Auth komplett deaktivieren — VOR sidecar.main importiert wird.
os.environ.setdefault("SEWER_SIDECAR_AUTH", "disabled")

_SIDECAR_ROOT = Path(__file__).resolve().parents[1]
_REPO_ROOT = _SIDECAR_ROOT.parent


def _is_writable_dir(path: Path) -> bool:
    try:
        path.mkdir(parents=True, exist_ok=True)
        probe = path / ".probe"
        probe.write_text("ok", encoding="utf-8")
        probe.unlink()
        return True
    except Exception:
        return False


def _default_pytest_temproot_is_usable() -> bool:
    root = Path(tempfile.gettempdir()) / f"pytest-of-{getpass.getuser()}"
    try:
        root.mkdir(mode=0o700, exist_ok=True)
        # Pytest scans this directory before creating numbered temp dirs.
        list(root.iterdir())
        return _is_writable_dir(root)
    except Exception:
        return False


def _configure_temproot_fallback() -> None:
    if os.environ.get("PYTEST_DEBUG_TEMPROOT") or _default_pytest_temproot_is_usable():
        return

    for candidate in (
        _REPO_ROOT / ".tmp" / "pytest" / "temproot",
        _SIDECAR_ROOT / ".pytest_tmp" / "temproot",
    ):
        if _is_writable_dir(candidate):
            os.environ["PYTEST_DEBUG_TEMPROOT"] = str(candidate)
            return


_configure_temproot_fallback()


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
    """Cache-Redirect best-effort. Marker werden via pyproject.toml registriert."""
    for cache_dir in (
        _REPO_ROOT / ".tmp" / "pytest" / "sidecar-cache",
        _SIDECAR_ROOT / ".pytest_tmp" / "cache",
    ):
        if not _is_writable_dir(cache_dir):
            continue

        config.cache._cachedir = cache_dir  # type: ignore[attr-defined]
        return


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
