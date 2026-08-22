from __future__ import annotations

import io
from email.message import Message
from pathlib import Path

from tools.EvalVisibilityReview.review_server_security import (
    MAX_JSON_BODY_BYTES,
    read_json_body,
    require_loopback_host,
)


class _FakeServer:
    server_address = ("127.0.0.1", 8123)


class _FakeHandler:
    def __init__(self, *, host: str = "127.0.0.1:8123", body: bytes = b"{}") -> None:
        self.headers = Message()
        self.headers["Host"] = host
        self.headers["Content-Type"] = "application/json"
        self.headers["Content-Length"] = str(len(body))
        self.rfile = io.BytesIO(body)
        self.server = _FakeServer()
        self.error: tuple[int, str] | None = None

    def send_error(self, status: int, message: str) -> None:
        self.error = (status, message)


def test_loopback_host_und_kleines_json_werden_akzeptiert() -> None:
    handler = _FakeHandler(body=b'{"ok":true}')

    assert require_loopback_host(handler)
    assert read_json_body(handler) == b'{"ok":true}'
    assert handler.error is None


def test_fremder_host_wird_vor_der_ausgabe_abgewiesen() -> None:
    handler = _FakeHandler(host="angreifer.example:8123")

    assert not require_loopback_host(handler)
    assert handler.error is not None
    assert handler.error[0] == 421


def test_text_plain_und_zu_grosse_json_anfragen_werden_abgewiesen() -> None:
    text_handler = _FakeHandler()
    text_handler.headers.replace_header("Content-Type", "text/plain")
    assert read_json_body(text_handler) is None
    assert text_handler.error is not None
    assert text_handler.error[0] == 415

    large_handler = _FakeHandler(body=b"x" * (MAX_JSON_BODY_BYTES + 1))
    assert read_json_body(large_handler) is None
    assert large_handler.error is not None
    assert large_handler.error[0] == 413


def test_alle_aelteren_pruefplaetze_verwenden_die_gemeinsamen_schutzregeln() -> None:
    root = Path(__file__).resolve().parent
    files = (
        "osd_layout_review_server.py",
        "bcc_video_fehlalarm_review_server.py",
        "bcc_copilot_review_server.py",
        "osd_handlabel_server.py",
        "osd_wahrheit_server.py",
        "visibility_review_server.py",
        "eval_metadata_review_server.py",
        "bcc_negativ_review_server.py",
    )

    for name in files:
        source = (root / name).read_text(encoding="utf-8")
        assert source.count("require_loopback_host(self)") >= 2, name
        assert "read_json_body(self)" in source, name
