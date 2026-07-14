import json
import sys
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


QGIS_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(QGIS_ROOT))

from sewerstudio_bridge.bridge_http import (  # noqa: E402
    ACCEPT_HEADER,
    fetch_bridge_bytes,
    fetch_bridge_json,
)


class _BridgeHandler(BaseHTTPRequestHandler):
    last_accept = None

    def do_GET(self):
        type(self).last_accept = self.headers.get("Accept")
        if self.path == "/qgis/status.json":
            body = json.dumps({"ok": True, "app": "SewerStudio"}).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        if self.path == "/qgis/current.geojson":
            body = b'{"type":"FeatureCollection","features":[]}'
            self.send_response(200)
            self.send_header("Content-Type", "application/geo+json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        if self.path == "/invalid-json":
            body = b"kein json"
            self.send_response(200)
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        if self.path == "/failure":
            self.send_response(503)
            self.end_headers()
            return

        self.send_response(404)
        self.end_headers()

    def log_message(self, format, *args):
        pass


class BridgeHttpTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), _BridgeHandler)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()
        cls.base_url = f"http://127.0.0.1:{cls.server.server_port}"

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join(timeout=5)

    def test_status_json_uses_real_http_and_expected_accept_header(self):
        result = fetch_bridge_json(self.base_url, "/qgis/status.json")

        self.assertIsNone(result.warning)
        self.assertEqual({"ok": True, "app": "SewerStudio"}, result.value)
        self.assertEqual(ACCEPT_HEADER, _BridgeHandler.last_accept)

    def test_geojson_bytes_are_returned_unchanged(self):
        result = fetch_bridge_bytes(self.base_url, "/qgis/current.geojson")

        self.assertIsNone(result.warning)
        self.assertEqual(b'{"type":"FeatureCollection","features":[]}', result.value)

    def test_404_is_silent_but_server_error_is_visible(self):
        missing = fetch_bridge_bytes(self.base_url, "/missing")
        failure = fetch_bridge_bytes(self.base_url, "/failure")

        self.assertIsNone(missing.value)
        self.assertIsNone(missing.warning)
        self.assertIsNone(failure.value)
        self.assertIn("503", failure.warning)

    def test_invalid_json_is_rejected_without_exception(self):
        result = fetch_bridge_json(self.base_url, "/invalid-json")

        self.assertIsNone(result.value)
        self.assertIn("Ungueltiges JSON", result.warning)

    def test_remote_or_malformed_urls_are_rejected_before_request(self):
        for url in (
            "http://192.168.1.20:8765",
            "https://127.0.0.1:8765",
            "http://user:secret@127.0.0.1:8765",
            "http://127.0.0.1:99999",
            "not-a-url",
        ):
            with self.subTest(url=url):
                result = fetch_bridge_bytes(url, "/qgis/status.json")
                self.assertIsNone(result.value)
                self.assertIn("lokale HTTP-Adresse", result.warning)


if __name__ == "__main__":
    unittest.main()
