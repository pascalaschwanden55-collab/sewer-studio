import unittest

import server


def item(klass, haltung, zeit, code=None, video=True):
    code = code or klass
    key = f"{haltung}|{zeit:.1f}|{code}"
    return key, {
        "id": key,
        "klass": klass,
        "haltung": haltung,
        "zeit": zeit,
        "code": code,
        "video_available": video,
    }


class ServerSelectionTests(unittest.TestCase):
    def test_dedupe_near_duplicates_keeps_one_per_clip_window(self):
        pairs = [
            item("BCA", "H1", 10.0),
            item("BCA", "H1", 12.0),
            item("BCA", "H1", 14.0),
            item("BCA", "H1", 35.0),
            item("BCA", "H2", 12.0),
        ]
        findings = dict(pairs)
        keys = [k for k, _ in pairs]

        kept = server.dedupe_near_duplicates(keys, findings, window_seconds=20.0)

        self.assertEqual(
            ["H1|10.0|BCA", "H1|35.0|BCA", "H2|12.0|BCA"],
            kept,
        )

    def test_interleave_by_class_rotates_holdings_inside_each_class(self):
        pairs = [
            item("BCA", "H1", 10.0),
            item("BCA", "H1", 35.0),
            item("BCA", "H2", 11.0),
            item("BCC", "H3", 20.0),
            item("BCC", "H3", 45.0),
            item("BCC", "H4", 21.0),
        ]
        findings = dict(pairs)
        keys = [k for k, _ in pairs]

        mixed = server.interleave_by_class(keys, findings, ["BCA", "BCC"])
        bca_holdings = [findings[k]["haltung"] for k in mixed if findings[k]["klass"] == "BCA"]
        bcc_holdings = [findings[k]["haltung"] for k in mixed if findings[k]["klass"] == "BCC"]

        self.assertEqual(["H1", "H2", "H1"], bca_holdings)
        self.assertEqual(["H3", "H4", "H3"], bcc_holdings)


class ServerSecurityTests(unittest.TestCase):
    @staticmethod
    def _call(method, host):
        handler = object.__new__(server.Handler)
        handler.command = method
        handler.path = "/session.json"
        handler.headers = {"Host": host}
        responses = []
        handler._send = lambda code, body, ctype="application/json", extra=None: responses.append(
            (code, body)
        )

        getattr(handler, f"do_{method}")()
        return responses

    def test_get_und_head_verweigern_fremden_host_vor_der_tokenausgabe(self):
        for method in ("GET", "HEAD"):
            with self.subTest(method=method):
                responses = self._call(method, "angreifer.example")

                self.assertEqual(403, responses[0][0])
                self.assertNotIn("video_label_token", responses[0][1])

    def test_session_json_bleibt_ueber_localhost_erreichbar(self):
        responses = self._call("GET", "localhost:8200")

        self.assertEqual(200, responses[0][0])
        self.assertEqual(server.VIDEO_LABEL_TOKEN, responses[0][1]["video_label_token"])


if __name__ == "__main__":
    unittest.main()
