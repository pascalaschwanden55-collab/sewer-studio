"""Auth-Token-Aufloesung fuer den Sidecar.

Bewusst ohne FastAPI-/Settings-Abhaengigkeit, damit die Sicherheitslogik separat testbar bleibt.
"""

from __future__ import annotations

import logging
import secrets
from pathlib import Path


def resolve_or_create_token(
    configured_token: str | None,
    token_file_path: Path,
    logger: logging.Logger | None = None,
) -> str:
    """Token aus Konfiguration, Datei oder neuem persistentem Wert aufloesen."""
    token = (configured_token or "").strip()
    if token:
        return token

    try:
        if token_file_path.exists():
            existing = token_file_path.read_text(encoding="utf-8").strip()
            if existing:
                return existing
    except OSError as exc:
        if logger is not None:
            logger.warning("Sidecar-Token-Datei nicht lesbar (%s): %s", token_file_path, exc)

    token = secrets.token_urlsafe(32)
    try:
        token_file_path.parent.mkdir(parents=True, exist_ok=True)
        token_file_path.write_text(token, encoding="utf-8")
        if logger is not None:
            logger.info("Neues Sidecar-Token erzeugt: %s", token_file_path)
    except OSError as exc:
        message = f"Sidecar-Token konnte nicht geschrieben werden ({token_file_path}): {exc}"
        if logger is not None:
            logger.error(message)
        raise RuntimeError(message) from exc

    return token
