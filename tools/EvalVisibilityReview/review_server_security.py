"""Gemeinsame HTTP-Schutzregeln fuer lokale SewerStudio-Pruefplaetze."""

from __future__ import annotations

from http.server import BaseHTTPRequestHandler
from urllib.parse import urlsplit


MAX_JSON_BODY_BYTES = 64 * 1024
_LOOPBACK_HOSTS = {"127.0.0.1", "localhost", "::1"}


def require_loopback_host(handler: BaseHTTPRequestHandler) -> bool:
    """Erlaubt nur Host-Header, die zum lokalen Listener gehoeren."""
    raw_host = (handler.headers.get("Host") or "").strip()
    try:
        parsed = urlsplit(f"//{raw_host}")
        hostname = (parsed.hostname or "").casefold()
        request_port = parsed.port
        listener_port = int(handler.server.server_address[1])
        allowed = (
            hostname in _LOOPBACK_HOSTS
            and parsed.username is None
            and parsed.password is None
            and (request_port is None or request_port == listener_port)
        )
    except (TypeError, ValueError):
        allowed = False

    if allowed:
        return True

    handler.send_error(421, "Ungueltiger Host")
    return False


def read_json_body(
    handler: BaseHTTPRequestHandler,
    max_bytes: int = MAX_JSON_BODY_BYTES,
) -> bytes | None:
    """Prueft JSON-Inhaltstyp und Groesse, bevor der Request-Body gelesen wird."""
    content_type = (handler.headers.get("Content-Type") or "").split(";", 1)[0]
    if content_type.strip().casefold() != "application/json":
        handler.send_error(415, "Content-Type muss application/json sein")
        return None

    raw_length = handler.headers.get("Content-Length")
    if raw_length is None:
        handler.send_error(411, "Content-Length fehlt")
        return None

    try:
        length = int(raw_length)
    except (TypeError, ValueError):
        handler.send_error(400, "Content-Length ist ungueltig")
        return None

    if length < 0:
        handler.send_error(400, "Content-Length ist ungueltig")
        return None
    if length > max_bytes:
        handler.send_error(413, "Anfrage ist zu gross")
        return None

    return handler.rfile.read(length)
