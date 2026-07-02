import unittest
from pathlib import Path
from tempfile import TemporaryDirectory


class AuthTokenTests(unittest.TestCase):
    def test_generated_token_must_be_persisted(self):
        from sidecar.auth_token import resolve_or_create_token

        with TemporaryDirectory() as tmp:
            blocking_parent = Path(tmp) / "not-a-directory"
            blocking_parent.write_text("blockiert", encoding="utf-8")
            token_file = blocking_parent / ".sidecar_token"

            with self.assertRaisesRegex(RuntimeError, "Sidecar-Token konnte nicht geschrieben werden"):
                resolve_or_create_token("", token_file)


if __name__ == "__main__":
    unittest.main()
