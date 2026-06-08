import csv
import tempfile
import unittest
from pathlib import Path

from tools.EvalVisibilityReview.visibility_review_server import VisibilityReviewStore, normalize_label


class VisibilityReviewStoreTests(unittest.TestCase):
    def test_normalize_label_accepts_three_decisions(self):
        self.assertEqual(normalize_label("ja"), "ja")
        self.assertEqual(normalize_label("nein"), "nein")
        self.assertEqual(normalize_label("unsicher"), "unsicher")
        self.assertEqual(normalize_label("sichtbar"), "ja")
        self.assertEqual(normalize_label("nicht_sichtbar"), "nein")
        self.assertIsNone(normalize_label("anderes"))

    def test_store_loads_manifest_and_writes_labels(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            image = root / "a.png"
            image.write_bytes(b"png")
            manifest = root / "review_manifest.csv"
            output = root / "visibility_labels.csv"
            with manifest.open("w", newline="", encoding="utf-8") as f:
                writer = csv.DictWriter(
                    f,
                    fieldnames=[
                        "image_name",
                        "image_path",
                        "label_path",
                        "expected_code",
                        "router_class",
                        "suggested_visibility",
                        "visibility_label",
                        "comment",
                    ],
                )
                writer.writeheader()
                writer.writerow(
                    {
                        "image_name": "a.png",
                        "image_path": str(image),
                        "label_path": "",
                        "expected_code": "BAIZ",
                        "router_class": "dichtung",
                        "suggested_visibility": "",
                        "visibility_label": "",
                        "comment": "",
                    }
                )

            store = VisibilityReviewStore(manifest, output)
            state = store.state()

            self.assertEqual(state["total"], 1)
            self.assertEqual(state["done"], 0)
            self.assertEqual(state["current"]["image_name"], "a.png")

            store.set_label("a.png", "ja", "sichtbar genug")

            with output.open("r", newline="", encoding="utf-8-sig") as f:
                rows = list(csv.DictReader(f))

            self.assertEqual(rows[0]["visibility_label"], "ja")
            self.assertEqual(rows[0]["comment"], "sichtbar genug")
            self.assertTrue(rows[0]["reviewed_at"])

    def test_store_resumes_existing_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            image = root / "a.png"
            image.write_bytes(b"png")
            manifest = root / "review_manifest.csv"
            output = root / "visibility_labels.csv"
            fieldnames = [
                "image_name",
                "image_path",
                "label_path",
                "expected_code",
                "router_class",
                "suggested_visibility",
                "visibility_label",
                "comment",
            ]
            with manifest.open("w", newline="", encoding="utf-8") as f:
                writer = csv.DictWriter(f, fieldnames=fieldnames)
                writer.writeheader()
                writer.writerow(
                    {
                        "image_name": "a.png",
                        "image_path": str(image),
                        "label_path": "",
                        "expected_code": "LEER",
                        "router_class": "leer",
                        "suggested_visibility": "",
                        "visibility_label": "",
                        "comment": "",
                    }
                )
            with output.open("w", newline="", encoding="utf-8") as f:
                writer = csv.DictWriter(f, fieldnames=fieldnames + ["reviewed_at"])
                writer.writeheader()
                writer.writerow(
                    {
                        "image_name": "a.png",
                        "image_path": str(image),
                        "label_path": "",
                        "expected_code": "LEER",
                        "router_class": "leer",
                        "suggested_visibility": "",
                        "visibility_label": "nein",
                        "comment": "leer passt",
                        "reviewed_at": "2026-05-25T10:00:00+00:00",
                    }
                )

            store = VisibilityReviewStore(manifest, output)
            state = store.state()

            self.assertEqual(state["done"], 1)
            self.assertEqual(state["current"]["visibility_label"], "nein")


if __name__ == "__main__":
    unittest.main()
