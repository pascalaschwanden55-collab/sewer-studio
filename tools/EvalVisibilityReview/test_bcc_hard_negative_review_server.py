from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from tools.EvalVisibilityReview.bcc_release_holdout_review_server import (
    HARD_NEGATIVE_INDEX_HTML,
    BccHardNegativeReviewStore,
)


class BccHardNegativeReviewStoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary = tempfile.TemporaryDirectory()
        self.root = Path(self._temporary.name)
        self.output = self.root / "reviews" / "review.json"
        self.now = lambda: "2026-07-28T19:30:00Z"
        self._write_queue()

    def tearDown(self) -> None:
        self._temporary.cleanup()

    def test_review_bindet_queue_klassenkarte_und_eindeutige_entscheidung(self) -> None:
        store = self._store()
        store.prepare_output()
        state = store.set_decision(
            "bcc-hn-a",
            "all_classes_clear",
            "Keine der gebundenen Klassen sichtbar.",
        )

        saved = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("bcc_hard_negative_review", saved["purpose"])
        self.assertEqual(self.queue_id, saved["queue_id"])
        self.assertEqual("1" * 64, saved["class_map_sha256"])
        self.assertIn("queue_manifest_sha256", saved)
        self.assertNotIn("manifest_sha256", saved)
        self.assertEqual(1, state["counts"]["all_classes_clear"])
        self.assertEqual(
            {
                "id",
                "decision",
                "comment",
                "image_url",
            },
            set(state["items"][0]),
        )

    def test_bcc_only_negative_ist_keine_gueltige_entscheidung(self) -> None:
        store = self._store()
        store.prepare_output()

        with self.assertRaisesRegex(ValueError, "ungueltig"):
            store.set_decision("bcc-hn-a", "negative")

    def test_review_mit_anderer_klassenkarte_wird_abgewiesen(self) -> None:
        store = self._store()
        store.prepare_output()
        review = json.loads(self.output.read_text(encoding="utf-8"))
        review["class_map_sha256"] = "2" * 64
        self.output.write_text(json.dumps(review), encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "class_map_sha256"):
            self._store()

    def test_manipuliertes_queue_bild_wird_abgewiesen(self) -> None:
        image = self.queue / "images" / "frame-a.jpg"
        image.write_bytes(image.read_bytes() + b"changed")

        with self.assertRaisesRegex(ValueError, "Manifest"):
            self._store()

    def test_queue_aenderung_nach_start_verhindert_entscheidung(self) -> None:
        store = self._store()
        store.prepare_output()
        output_before = self.output.read_bytes()
        image = self.queue / "images" / "frame-a.jpg"
        image.write_bytes(image.read_bytes() + b"changed-after-start")

        with self.assertRaisesRegex(ValueError, "Review-Quelle"):
            store.set_decision(
                "bcc-hn-a",
                "all_classes_clear",
                expected_revision=0,
            )

        self.assertEqual(output_before, self.output.read_bytes())
        self.assertEqual(0, store.state()["revision"])
        self.assertEqual(0, store.state()["done"])

    def test_pruefoberflaeche_erklaert_alle_klassen_und_zeigt_keine_signale(self) -> None:
        self.assertIn("15 trainierten Klassen", HARD_NEGATIVE_INDEX_HTML)
        # Die Oberflaeche muss jede der 15 Klassen einzeln benennen, sonst kann
        # der Pruefer nicht wissen, wogegen er entscheidet.
        for code in (
            "BAA", "BAB", "BAC", "BAF", "BAH", "BAI", "BAJ",
            "BBA", "BBB", "BBC", "BBD", "BBF", "BCA", "BCC", "SONST",
        ):
            with self.subTest(klasse=code):
                self.assertIn(f"<b>{code}</b>", HARD_NEGATIVE_INDEX_HTML)
        # Anschluss und Bogen zaehlen auch ohne Schaden — das ist die haeufigste
        # Fehlerquelle und muss ausdruecklich dastehen.
        self.assertIn("auch wenn intakt", HARD_NEGATIVE_INDEX_HTML)
        self.assertIn("auch wenn normal", HARD_NEGATIVE_INDEX_HTML)
        self.assertIn("all_classes_clear", HARD_NEGATIVE_INDEX_HTML)
        self.assertNotIn("max_confidence", HARD_NEGATIVE_INDEX_HTML)
        self.assertNotIn("model_id", HARD_NEGATIVE_INDEX_HTML)
        self.assertIn("revision: reviewState.revision", HARD_NEGATIVE_INDEX_HTML)

    def _store(self) -> BccHardNegativeReviewStore:
        return BccHardNegativeReviewStore(
            self.queue,
            self.output,
            "Besitzer",
            now_utc=self.now,
        )

    def _write_queue(self) -> None:
        image_payload = b"\xff\xd8\xff\xe0" + b"x" * 1_100
        image_sha = self._sha(image_payload)
        model_scope = [
            {
                "candidate_id": "model-a",
                "candidate_manifest_sha256": "3" * 64,
                "weights_sha256": "4" * 64,
            }
        ]
        semantic = {
            "schema_version": "1.0",
            "purpose": "bcc_hard_negative_review_queue",
            "pilot": "BCC_bogen",
            "role": "training_candidate_review",
            "class_map_version": 3,
            "class_map_sha256": "1" * 64,
            "vsa_manifest_hash": "2" * 64,
            "class_names": [
                *[f"class-{index}" for index in range(14)],
                "BCC_bogen",
            ],
            "protected_sets": [],
            "protection_snapshot": {},
            "model_scope": model_scope,
            "selection_rule": {
                "one_image_per_physical_holding": True,
                "requires_current_model_bcc_trigger": True,
                "review_target": (
                    "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse"
                ),
            },
            "sources": [],
            "items": [
                {
                    "id": "bcc-hn-a",
                    "image_sha256": image_sha,
                    "holding_key": "100-200",
                    "physical_holding_key": "100|200",
                    "source_ref": "5" * 64,
                    "inspection_date": "2026-04-20",
                    "size_bytes": len(image_payload),
                    "image_format": "jpg",
                    "predictions": [
                        {
                            "model_id": "model-a",
                            "predicted_bcc": True,
                        }
                    ],
                }
            ],
        }
        self.queue_id = self._sha(
            json.dumps(
                semantic,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        )
        self.queue = self.root / f"bcc_hn_{self.queue_id[:12]}"
        images = self.queue / "images"
        images.mkdir(parents=True)
        image = images / "frame-a.jpg"
        image.write_bytes(image_payload)
        candidates = [
            {
                "id": "bcc-hn-a",
                "frame_path": image.name,
                "category": "all_class_background_review",
                "status": "pending_review",
                "source_sha256": image_sha,
            }
        ]
        candidates_bytes = self._json_bytes(candidates)
        (self.queue / "_candidates.json").write_bytes(candidates_bytes)
        hashes = {
            "_candidates.json": {
                "sha256": self._sha(candidates_bytes),
                "size_bytes": len(candidates_bytes),
            },
            f"images/{image.name}": {
                "sha256": image_sha,
                "size_bytes": image.stat().st_size,
            },
        }
        manifest = {
            "schema_version": "1.0",
            "purpose": "bcc_hard_negative_review_queue",
            "queue_id": self.queue_id,
            "pilot": "BCC_bogen",
            "role": "training_candidate_review",
            "frozen": True,
            "hash_algorithm": "sha256",
            "class_map_version": 3,
            "class_map_sha256": "1" * 64,
            "vsa_manifest_hash": "2" * 64,
            "class_names": semantic["class_names"],
            "protected_sets": [],
            "protection_snapshot": {},
            "sources": [],
            "candidates_count": 1,
            "images_count": 1,
            "hashes_count": len(hashes),
            "hashes": hashes,
            "semantic": semantic,
            "selection_receipt": {
                "models": model_scope,
                "items": semantic["items"],
            },
        }
        (self.queue / "_manifest.json").write_bytes(self._json_bytes(manifest))

    @staticmethod
    def _sha(payload: bytes) -> str:
        return hashlib.sha256(payload).hexdigest()

    @staticmethod
    def _json_bytes(value: object) -> bytes:
        return (
            json.dumps(value, ensure_ascii=False, indent=2) + "\n"
        ).encode("utf-8")


if __name__ == "__main__":
    unittest.main()
