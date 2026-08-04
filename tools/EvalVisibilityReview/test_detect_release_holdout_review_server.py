import base64
import hashlib
import http.client
import json
import os
import tempfile
import threading
import unittest
from pathlib import Path
from unittest import mock

from tools.EvalVisibilityReview.detect_release_holdout_review_server import (
    INDEX_HTML,
    MAX_REQUEST_BODY_BYTES,
    DetectReleaseHoldoutReviewStore,
    ReviewRevisionConflictError,
    create_server,
)


CLASS_NAMES = (
    "BCA_anschluss",
    "BAB_riss",
    "BAC_bruch",
    "BAA_verformung",
    "BAF_oberflaeche",
    "BAH_schadanschluss",
    "BAI_dichtung",
    "BAJ_verbindung",
    "BBA_wurzeln",
    "BBB_anhaftung",
    "BBC_ablagerung",
    "BBD_boden",
    "BBF_infiltration",
    "SONST_schaden",
    "BCC_bogen",
)

# Echtes, kleines 1x1-PNG. Die Tests verwenden nie Kundenbilder.
PNG_BYTES = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
)


class DetectReleaseHoldoutReviewStoreTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.root = Path(self._temp.name)
        self.holdout = self.root / "detect_release_holdout"
        self.output = self.root / "eval_review" / "detect_review.json"
        self.now = lambda: "2026-08-03T12:30:00Z"

    def tearDown(self):
        self._temp.cleanup()

    def test_gueltiger_app_kompatibler_holdout_bindet_alle_hashes_und_mehrfachboxen(self):
        self._write_holdout(with_references=True)
        manifest_before = (self.holdout / "_manifest.json").read_bytes()
        candidates_before = (self.holdout / "_candidates.json").read_bytes()
        image_before = (self.holdout / "images" / "frame-a.png").read_bytes()
        store = self._store()
        state = store.prepare_output()

        annotations = [
            self._annotation("ann-riss", 1, 0.25, 0.25, 0.3, 0.3),
            self._annotation("ann-bogen", 14, 0.75, 0.75, 0.3, 0.3),
        ]
        state = store.set_decision(
            "drh-a",
            "positive",
            annotations,
            "Alle sichtbaren Objekte markiert.",
            expected_revision=state["revision"],
        )

        saved = self._read_json(self.output)
        manifest = self._read_json(self.holdout / "_manifest.json")
        self.assertEqual("1.0", saved["schema_version"])
        self.assertEqual("detect_release_holdout_review", saved["purpose"])
        self.assertEqual("h" * 64, saved["holdout_id"])
        self.assertEqual(hashlib.sha256(manifest_before).hexdigest(), saved["manifest_sha256"])
        for field in (
            "candidates_sha256",
            "candidate_id",
            "candidate_manifest_sha256",
            "candidate_weights_sha256",
            "class_map_version",
            "class_map_sha256",
            "vsa_manifest_hash",
            "vsa_manifest_sha256",
        ):
            with self.subTest(field=field):
                self.assertEqual(manifest[field], saved[field])
        self.assertEqual("Besitzer", saved["reviewer"])
        self.assertEqual(annotations, saved["decisions"]["drh-a"]["annotations"])
        self.assertEqual(1, state["done"])
        self.assertEqual(1, state["revision"])
        self.assertEqual(manifest_before, (self.holdout / "_manifest.json").read_bytes())
        self.assertEqual(candidates_before, (self.holdout / "_candidates.json").read_bytes())
        self.assertEqual(image_before, (self.holdout / "images" / "frame-a.png").read_bytes())

        public = store.state()["items"][0]
        self.assertEqual("BABBC", public["operator_references"][0]["code"])
        self.assertEqual("BAB_riss", public["operator_references"][0]["class_name"])
        self.assertNotIn("haltung_key", public)
        self.assertNotIn("image_path", public)
        self.assertNotIn("operator_references", saved["decisions"]["drh-a"])

    def test_positive_braucht_box_negative_und_exclude_verbieten_boxen(self):
        self._write_holdout()
        store = self._store()
        annotation = self._annotation("ann-a", 0, 0.5, 0.5, 0.4, 0.4)

        with self.assertRaisesRegex(ValueError, "mindestens eine Box"):
            store.set_decision("drh-a", "positive", [])
        with self.assertRaisesRegex(ValueError, "duerfen keine Boxen"):
            store.set_decision("drh-a", "negative", [annotation])
        with self.assertRaisesRegex(ValueError, "duerfen keine Boxen"):
            store.set_decision("drh-a", "exclude", [annotation])

        state = store.set_decision("drh-a", "negative", [], expected_revision=0)
        self.assertEqual("negative", state["current"]["decision"])
        self.assertEqual([], state["current"]["annotations"])

    def test_unbekannte_klasse_und_ungueltige_box_werden_abgelehnt(self):
        self._write_holdout()
        store = self._store()
        cases = {
            "unbekannte_id": self._annotation("ann-a", 99, 0.5, 0.5, 0.4, 0.4),
            "falscher_name": {
                **self._annotation("ann-a", 1, 0.5, 0.5, 0.4, 0.4),
                "class_name": "BCA_anschluss",
            },
            "ausserhalb": self._annotation("ann-a", 1, 0.1, 0.5, 0.4, 0.4),
            "nullbreite": self._annotation("ann-a", 1, 0.5, 0.5, 0.0, 0.4),
            "nicht_endlich": self._annotation("ann-a", 1, float("nan"), 0.5, 0.4, 0.4),
        }
        for name, annotation in cases.items():
            with self.subTest(name=name), self.assertRaises(ValueError):
                store.set_decision("drh-a", "positive", [annotation])

    def test_annotations_ids_muessen_eindeutig_sein(self):
        self._write_holdout()
        store = self._store()
        annotations = [
            self._annotation("gleich", 0, 0.25, 0.25, 0.2, 0.2),
            self._annotation("gleich", 1, 0.75, 0.75, 0.2, 0.2),
        ]

        with self.assertRaisesRegex(ValueError, "eindeutig"):
            store.set_decision("drh-a", "positive", annotations)

    def test_revision_verhindert_stilles_ueberschreiben(self):
        self._write_holdout()
        store = self._store()
        store.prepare_output()
        annotation = self._annotation("ann-a", 0, 0.5, 0.5, 0.4, 0.4)
        store.set_decision("drh-a", "positive", [annotation], expected_revision=0)
        saved = self.output.read_bytes()

        with self.assertRaisesRegex(ReviewRevisionConflictError, "veraltet"):
            store.set_decision("drh-a", "negative", [], expected_revision=0)

        self.assertEqual(saved, self.output.read_bytes())
        self.assertEqual(1, store.state()["revision"])
        self.assertEqual("positive", store.state()["current"]["decision"])

    def test_bildmutation_sperrt_bildzugriff_und_speicherung(self):
        self._write_holdout()
        store = self._store()
        store.prepare_output()
        before = self.output.read_bytes()
        image = self.holdout / "images" / "frame-a.png"
        image.write_bytes(image.read_bytes() + b"veraendert")

        with self.assertRaisesRegex(ValueError, "veraendert"):
            store.image_bytes_for("drh-a")
        with self.assertRaisesRegex(ValueError, "Review-Quelle"):
            store.set_decision("drh-a", "negative", [], expected_revision=0)

        self.assertEqual(before, self.output.read_bytes())
        self.assertEqual(0, store.state()["done"])

    def test_kandidaten_oder_manifest_hashmutation_sperrt_start(self):
        self._write_holdout()
        candidates = self._read_json(self.holdout / "_candidates.json")
        candidates["candidates"][0]["haltung_key"] = "1-2"
        (self.holdout / "_candidates.json").write_bytes(self._json_bytes(candidates))
        with self.assertRaisesRegex(ValueError, "candidates_sha256"):
            self._store()

        second = self.root / "wrong-class"
        self._write_holdout(second)
        manifest = self._read_json(second / "_manifest.json")
        manifest["classes"][0]["name"] = "BAB_riss"
        (second / "_manifest.json").write_bytes(self._json_bytes(manifest))
        with self.assertRaisesRegex(ValueError, "Klassenname|Klassenreihenfolge"):
            DetectReleaseHoldoutReviewStore(
                second,
                self.root / "wrong-class-review.json",
                "Besitzer",
            )

    def test_app_vertrag_verlangt_frame_path_kanonische_haltung_und_ein_bild(self):
        mutations = {
            "frame_path_fehlt": lambda row: row.pop("frame_path"),
            "frame_path_mit_ordner": lambda row: row.__setitem__(
                "frame_path", "images/frame-a.png"
            ),
            "haltung_nicht_kanonisch": lambda row: row.__setitem__(
                "haltung_key", "06.88442-06.88443"
            ),
            "physical_falsch": lambda row: row.__setitem__(
                "physical_holding_key", "88443|88442"
            ),
        }
        for name, mutate in mutations.items():
            with self.subTest(name=name):
                holdout = self.root / name
                self._write_holdout(holdout)
                document = self._read_json(holdout / "_candidates.json")
                mutate(document["candidates"][0])
                self._rewrite_candidates_binding(holdout, document)
                with self.assertRaises(ValueError):
                    DetectReleaseHoldoutReviewStore(
                        holdout,
                        self.root / f"{name}.json",
                        "Besitzer",
                    )

        duplicate = self.root / "duplicate-candidate"
        self._write_holdout(duplicate)
        document = self._read_json(duplicate / "_candidates.json")
        second = dict(document["candidates"][0])
        second["id"] = "drh-b"
        second["haltung_key"] = "90001-90002"
        second["physical_holding_key"] = "90001|90002"
        document["candidates"].append(second)
        self._rewrite_candidates_binding(duplicate, document)
        manifest_path = duplicate / "_manifest.json"
        manifest = self._read_json(manifest_path)
        manifest["candidates_count"] = 2
        manifest_path.write_bytes(self._json_bytes(manifest))
        with self.assertRaisesRegex(ValueError, "Doppelter Bildpfad|mehrfach"):
            DetectReleaseHoldoutReviewStore(
                duplicate,
                self.root / "duplicate-review.json",
                "Besitzer",
            )

    def test_nur_aktive_class_map_v3_und_passender_vsa_hash_werden_angenommen(self):
        mutations = {
            "class_map_version": lambda manifest: manifest.__setitem__(
                "class_map_version", 2
            ),
            "vsa_manifest_hash": lambda manifest: manifest.__setitem__(
                "vsa_manifest_hash", "5" * 64
            ),
        }
        for name, mutate in mutations.items():
            with self.subTest(name=name):
                holdout = self.root / name
                self._write_holdout(holdout)
                manifest_path = holdout / "_manifest.json"
                manifest = self._read_json(manifest_path)
                mutate(manifest)
                manifest_path.write_bytes(self._json_bytes(manifest))
                with self.assertRaises(ValueError):
                    DetectReleaseHoldoutReviewStore(
                        holdout,
                        self.root / f"{name}-review.json",
                        "Besitzer",
                    )

    def test_ausgabe_muss_ausserhalb_des_holdouts_liegen(self):
        self._write_holdout()

        with self.assertRaisesRegex(ValueError, "ausserhalb"):
            DetectReleaseHoldoutReviewStore(
                self.holdout,
                self.holdout / "review.json",
                "Besitzer",
            )

    def test_vorhandene_review_muss_an_jede_quelle_und_reviewer_gebunden_bleiben(self):
        self._write_holdout()
        store = self._store()
        store.prepare_output()
        review = self._read_json(self.output)
        review["candidate_weights_sha256"] = "0" * 64
        self.output.write_bytes(self._json_bytes(review))

        with self.assertRaisesRegex(ValueError, "candidate_weights_sha256"):
            self._store()

    def test_atomare_ausgabe_und_prozessweiter_versionsschutz(self):
        self._write_holdout()
        first = self._store()
        second = self._store()
        real_replace = os.replace
        with mock.patch(
            "tools.EvalVisibilityReview.detect_release_holdout_review_server.os.replace",
            side_effect=real_replace,
        ) as replace:
            first.prepare_output()
        replace.assert_called_once()
        temporary, destination = map(Path, replace.call_args.args)
        self.assertEqual(self.output, destination)
        self.assertEqual(self.output.parent, temporary.parent)

        with self.assertRaisesRegex(ValueError, "parallel"):
            second.prepare_output()

    def test_http_sperrt_fremden_host_origin_und_zu_grosse_anfrage(self):
        self._write_holdout()
        store = self._store()
        store.prepare_output()
        server = create_server(store, port=0)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(thread.join, 2)
        self.addCleanup(server.server_close)
        self.addCleanup(server.shutdown)
        port = server.server_address[1]
        connection = http.client.HTTPConnection("127.0.0.1", port, timeout=5)
        self.addCleanup(connection.close)

        connection.request("GET", "/api/state", headers={"Host": "fremd.example"})
        response = connection.getresponse()
        response.read()
        self.assertEqual(421, response.status)

        payload = {
            "id": "drh-a",
            "decision": "negative",
            "annotations": [],
            "comment": "",
            "revision": 0,
        }
        connection.request(
            "POST",
            "/api/review",
            body=json.dumps(payload),
            headers={"Content-Type": "application/json", "Origin": "https://fremd.example"},
        )
        response = connection.getresponse()
        response.read()
        self.assertEqual(403, response.status)

        connection.request(
            "POST",
            "/api/review",
            body=b"x" * (MAX_REQUEST_BODY_BYTES + 1),
            headers={"Content-Type": "application/json"},
        )
        response = connection.getresponse()
        response.read()
        self.assertEqual(413, response.status)

    def test_ui_erklaert_referenz_mehrfachboxen_und_tastatursteuerung(self):
        for fragment in (
            "Operateur-Referenz aus PDF",
            "keine KI-Angabe",
            "Alle sichtbaren Objekte der 15 Klassen",
            "Mehrere Boxen sind erlaubt",
            "keine der 15 Klassen sichtbar",
            "ArrowLeft",
            "ArrowRight",
            "removeAnnotation",
            "saveDecision('positive')",
            "saveDecision('negative')",
            "saveDecision('exclude')",
        ):
            with self.subTest(fragment=fragment):
                self.assertIn(fragment, INDEX_HTML)
        self.assertNotIn("model_prediction", INDEX_HTML)
        self.assertNotIn("/training", INDEX_HTML.casefold())
        self.assertNotIn("als gold speichern", INDEX_HTML.casefold())

    def _store(self) -> DetectReleaseHoldoutReviewStore:
        return DetectReleaseHoldoutReviewStore(
            self.holdout,
            self.output,
            "Besitzer",
            now_utc=self.now,
        )

    def _write_holdout(
        self,
        holdout: Path | None = None,
        *,
        with_references: bool = False,
    ) -> None:
        holdout = holdout or self.holdout
        images = holdout / "images"
        images.mkdir(parents=True)
        image_path = images / "frame-a.png"
        image_path.write_bytes(PNG_BYTES)
        image_sha = hashlib.sha256(PNG_BYTES).hexdigest()
        row: dict[str, object] = {
            "id": "drh-a",
            "image_path": "images/frame-a.png",
            "frame_path": "frame-a.png",
            "image_sha256": image_sha,
            "size_bytes": len(PNG_BYTES),
            "width": 1,
            "height": 1,
            "haltung_key": "88442-88443",
            "physical_holding_key": "88442|88443",
        }
        if with_references:
            row["operator_references"] = [
                {
                    "code": "BABBC",
                    "description": "Riss längs im Rohr sichtbar.",
                },
                {
                    "code": "BCCBY",
                    "text": "Bogen nach rechts.",
                    "class_id": 14,
                    "class_name": "BCC_bogen",
                },
            ]
        candidates = {
            "schema_version": "1.0",
            "purpose": "detect_release_holdout_candidates",
            "holdout_id": "h" * 64,
            "candidates": [row],
        }
        candidates_path = holdout / "_candidates.json"
        candidates_path.write_bytes(self._json_bytes(candidates))
        candidates_sha = self._sha_file(candidates_path)
        hashes = {
            "_candidates.json": {
                "sha256": candidates_sha,
                "size_bytes": candidates_path.stat().st_size,
            },
            "images/frame-a.png": {
                "sha256": image_sha,
                "size_bytes": len(PNG_BYTES),
            },
        }
        classes = [
            {"id": class_id, "name": name, "label": f"Klasse {class_id}"}
            for class_id, name in enumerate(CLASS_NAMES)
        ]
        manifest = {
            "schema_version": "1.0",
            "purpose": "detect_release_holdout",
            "holdout_id": "h" * 64,
            "frozen": True,
            "hash_algorithm": "sha256",
            "hashes_count": len(hashes),
            "candidates_count": 1,
            "candidates_sha256": candidates_sha,
            "candidate_id": "detect_gold_candidate",
            "candidate_manifest_sha256": "1" * 64,
            "candidate_weights_sha256": "2" * 64,
            "class_map_version": 3,
            "class_map_sha256": "3" * 64,
            "vsa_manifest_hash": "4" * 64,
            "vsa_manifest_sha256": "4" * 64,
            "classes": classes,
            "hashes": hashes,
            # Dokumentierte Schutzmetadaten duerfen den Leser nicht blockieren.
            "role": "acceptance",
            "training_allowed": False,
            "model_predictions_visible": False,
        }
        (holdout / "_manifest.json").write_bytes(self._json_bytes(manifest))

    def _rewrite_candidates_binding(self, holdout: Path, document: object) -> None:
        candidates_path = holdout / "_candidates.json"
        candidates_path.write_bytes(self._json_bytes(document))
        sha256 = self._sha_file(candidates_path)
        manifest_path = holdout / "_manifest.json"
        manifest = self._read_json(manifest_path)
        manifest["candidates_sha256"] = sha256
        manifest["hashes"]["_candidates.json"] = {
            "sha256": sha256,
            "size_bytes": candidates_path.stat().st_size,
        }
        manifest_path.write_bytes(self._json_bytes(manifest))

    @staticmethod
    def _annotation(
        annotation_id: str,
        class_id: int,
        x_center: float,
        y_center: float,
        width: float,
        height: float,
    ) -> dict[str, object]:
        class_name = CLASS_NAMES[class_id] if 0 <= class_id < len(CLASS_NAMES) else "unbekannt"
        return {
            "id": annotation_id,
            "class_id": class_id,
            "class_name": class_name,
            "box": {
                "x_center": x_center,
                "y_center": y_center,
                "width": width,
                "height": height,
            },
        }

    @staticmethod
    def _json_bytes(value: object) -> bytes:
        return (
            json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
        ).encode("utf-8")

    @staticmethod
    def _read_json(path: Path):
        return json.loads(path.read_text(encoding="utf-8"))

    @staticmethod
    def _sha_file(path: Path) -> str:
        return hashlib.sha256(path.read_bytes()).hexdigest()


if __name__ == "__main__":
    unittest.main()
