import hashlib
import http.client
import json
import os
import tempfile
import threading
import unittest
from pathlib import Path
from unittest import mock

from tools.EvalVisibilityReview.bcc_release_holdout_review_server import (
    INDEX_HTML,
    MAX_REQUEST_BODY_BYTES,
    BccReleaseHoldoutReviewStore,
    create_server,
)


class BccReleaseHoldoutReviewStoreTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.root = Path(self._temp.name)
        self.holdout = self.root / "bcc_release_holdout"
        self.output = self.root / "reviews" / "bcc_review.json"
        self.now = lambda: "2026-07-28T15:30:00Z"

    def tearDown(self):
        self._temp.cleanup()

    def test_gueltiger_holdout_wird_vollstaendig_gebunden_und_atomar_gespeichert(self):
        self._write_holdout()
        manifest_before = (self.holdout / "_manifest.json").read_bytes()
        candidates_before = (self.holdout / "_candidates.json").read_bytes()
        image_before = (self.holdout / "images" / "frame-a.jpg").read_bytes()

        store = self._store()
        store.prepare_output()
        initial_output = self.output.read_bytes()
        real_replace = os.replace
        with mock.patch(
            "tools.EvalVisibilityReview.bcc_release_holdout_review_server.os.replace",
            side_effect=real_replace,
        ) as replace:
            state = store.set_decision(
                "bcc-rh-a",
                "positive",
                "Bild zeigt einen Bogen.",
            )

        saved = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual("1.0", saved["schema_version"])
        self.assertEqual("bcc_release_holdout_review", saved["purpose"])
        self.assertEqual("a" * 64, saved["holdout_id"])
        self.assertEqual(
            hashlib.sha256(manifest_before).hexdigest(),
            saved["manifest_sha256"],
        )
        self.assertEqual(
            hashlib.sha256(candidates_before).hexdigest(),
            saved["candidates_sha256"],
        )
        self.assertEqual("Blind Reviewer", saved["reviewer"])
        self.assertNotIn("revision", saved)
        self.assertEqual(
            {
                "decision": "positive",
                "comment": "Bild zeigt einen Bogen.",
                "reviewed_at_utc": "2026-07-28T15:30:00Z",
            },
            saved["decisions"]["bcc-rh-a"],
        )
        self.assertEqual(1, state["done"])
        self.assertEqual(1, state["revision"])
        self.assertEqual({"positive": 1, "negative": 0, "exclude": 0}, state["counts"])
        self.assertEqual(manifest_before, (self.holdout / "_manifest.json").read_bytes())
        self.assertEqual(
            candidates_before,
            (self.holdout / "_candidates.json").read_bytes(),
        )
        self.assertEqual(
            image_before,
            (self.holdout / "images" / "frame-a.jpg").read_bytes(),
        )
        self.assertNotEqual(initial_output, self.output.read_bytes())
        replace.assert_called_once()
        temporary, destination = map(Path, replace.call_args.args)
        self.assertEqual(self.output, destination)
        self.assertEqual(self.output.parent, temporary.parent)
        self.assertTrue(temporary.name.startswith(f".{self.output.name}."))

    def test_fehlgeschlagenes_publish_belaesst_datei_und_speicherzustand(self):
        self._write_holdout()
        store = self._store()
        store.prepare_output()
        before = self.output.read_bytes()

        with mock.patch(
            "tools.EvalVisibilityReview.bcc_release_holdout_review_server.os.replace",
            side_effect=OSError("simulierter Fehler"),
        ):
            with self.assertRaisesRegex(OSError, "simulierter Fehler"):
                store.set_decision("bcc-rh-a", "negative", "Kein Bogen.")

        self.assertEqual(before, self.output.read_bytes())
        row = next(item for item in store.state()["items"] if item["id"] == "bcc-rh-a")
        self.assertIsNone(row["decision"])
        self.assertEqual([], list(self.output.parent.glob(f".{self.output.name}.*.tmp")))

    def test_ausgabe_muss_ausserhalb_liegen_und_reviewer_ist_pflicht(self):
        self._write_holdout()

        with self.assertRaisesRegex(ValueError, "ausserhalb"):
            BccReleaseHoldoutReviewStore(
                self.holdout,
                self.holdout / "review.json",
                "Blind Reviewer",
            )
        with self.assertRaisesRegex(ValueError, "Reviewer"):
            BccReleaseHoldoutReviewStore(
                self.holdout,
                self.output,
                "   ",
            )

    def test_ausgabe_sperrt_verknuepfung_in_der_ahnenkette(self):
        self._write_holdout()
        original = (
            "tools.EvalVisibilityReview.bcc_release_holdout_review_server."
            "_is_reparse_point"
        )

        def marks_output_parent(path: Path) -> bool:
            return Path(path) == self.output.parent

        with mock.patch(original, side_effect=marks_output_parent):
            with self.assertRaisesRegex(ValueError, "Ausgabeordner"):
                BccReleaseHoldoutReviewStore(
                    self.holdout,
                    self.output,
                    "Blind Reviewer",
                )

    def test_holdout_pruefung_sperrt_jede_relevante_manipulation(self):
        mutations = {
            "nicht_frozen": self._make_not_frozen,
            "kandidaten_geaendert": self._tamper_candidates,
            "bild_geaendert": self._tamper_image,
            "unerwartete_datei": self._add_untracked_file,
            "fehlende_hashabdeckung": self._remove_image_hash,
            "falscher_quellhash": self._set_wrong_source_hash_and_rehash,
            "ungueltige_haltung": self._set_invalid_holding_and_rehash,
        }

        for name, mutate in mutations.items():
            with self.subTest(name=name):
                holdout = self.root / name
                output = self.root / "review-results" / f"{name}.json"
                self._write_holdout(holdout)
                mutate(holdout)
                with self.assertRaises(ValueError):
                    BccReleaseHoldoutReviewStore(
                        holdout,
                        output,
                        "Blind Reviewer",
                    )

    def test_unsichere_bildreferenz_wird_auch_mit_passendem_dateihash_abgelehnt(self):
        self._write_holdout()
        candidates = self._read_json(self.holdout / "_candidates.json")
        candidates[0]["frame_path"] = "../_manifest.json"
        self._rewrite_candidates_and_hash(self.holdout, candidates)

        with self.assertRaisesRegex(ValueError, "Bild"):
            self._store()

    def test_vorhandene_ausgabe_muss_an_alle_vier_quellen_gebunden_sein(self):
        mutations = {
            "holdout_id": lambda review: review.__setitem__("holdout_id", "b" * 64),
            "manifest_sha256": lambda review: review.__setitem__(
                "manifest_sha256", "0" * 64
            ),
            "candidates_sha256": lambda review: review.__setitem__(
                "candidates_sha256", "1" * 64
            ),
            "reviewer": lambda review: review.__setitem__("reviewer", "Andere Person"),
        }

        for name, mutate in mutations.items():
            with self.subTest(name=name):
                holdout = self.root / f"binding-{name}"
                output = self.root / "bindings" / f"{name}.json"
                self._write_holdout(holdout)
                store = BccReleaseHoldoutReviewStore(
                    holdout,
                    output,
                    "Blind Reviewer",
                    now_utc=self.now,
                )
                store.prepare_output()
                review = self._read_json(output)
                mutate(review)
                output.write_text(
                    json.dumps(review, ensure_ascii=False),
                    encoding="utf-8",
                )

                with self.assertRaisesRegex(ValueError, "gehoert|gebunden|Reviewer"):
                    BccReleaseHoldoutReviewStore(
                        holdout,
                        output,
                        "Blind Reviewer",
                        now_utc=self.now,
                    )

    def test_nur_erlaubte_entscheidungen_und_bekannte_ids_werden_angenommen(self):
        self._write_holdout()
        store = self._store()

        for decision in ("positive", "negative", "exclude"):
            with self.subTest(decision=decision):
                state = store.set_decision("bcc-rh-a", decision, "")
                self.assertEqual(decision, state["current"]["decision"])

        with self.assertRaisesRegex(ValueError, "Entscheidung"):
            store.set_decision("bcc-rh-a", "unsicher", "")
        with self.assertRaisesRegex(KeyError, "Bild-ID"):
            store.set_decision("../_manifest.json", "positive", "")

    def test_ui_und_api_status_bleiben_blind_und_bilder_werden_nur_ueber_id_aufgeloest(self):
        self._write_holdout(
            candidates=[
                {
                    "id": "bcc-rh-a",
                    "frame_path": "frame-a.jpg",
                    "haltung_key": "88442-88443",
                    "kategorie": "bcc_blind_review",
                    "source_sha256": "",
                    "model_prediction": "TOPSECRET_MODEL_PREDICTION",
                    "xtf_code": "TOPSECRET_XTF_CODE",
                    "hidden_hint": "TOPSECRET_HIDDEN_HINT",
                    "source": r"C:\TOPSECRET_SOURCE\source.xtf",
                }
            ]
        )
        store = self._store()

        state = store.state()
        serialized = json.dumps(state, ensure_ascii=False)
        self.assertEqual(
            {"id", "decision", "comment", "image_url"},
            set(state["items"][0]),
        )
        self.assertNotIn("TOPSECRET_MODEL_PREDICTION", serialized)
        self.assertNotIn("TOPSECRET_XTF_CODE", serialized)
        self.assertNotIn("TOPSECRET_HIDDEN_HINT", serialized)
        self.assertNotIn("TOPSECRET_SOURCE", serialized)
        self.assertNotIn("haltung_key", serialized)
        self.assertNotIn("source_sha256", serialized)
        body, content_type = store.image_bytes_for("bcc-rh-a")
        self.assertEqual(b"\xff\xd8\xff" + b"x" * 2048, body)
        self.assertEqual("image/jpeg", content_type)
        with self.assertRaisesRegex(KeyError, "Bild-ID"):
            store.image_bytes_for("../_manifest.json")

    def test_http_ist_nur_loopback_body_begrenzt_und_verrraet_keine_quellen(self):
        self._write_holdout()
        store = self._store()
        server = create_server(store, port=0)
        self.assertEqual("127.0.0.1", server.server_address[0])
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(thread.join, 2)
        self.addCleanup(server.server_close)
        self.addCleanup(server.shutdown)
        port = server.server_address[1]

        connection = http.client.HTTPConnection("127.0.0.1", port, timeout=5)
        self.addCleanup(connection.close)
        oversized = b"x" * (MAX_REQUEST_BODY_BYTES + 1)
        connection.request(
            "POST",
            "/api/review",
            body=oversized,
            headers={"Content-Type": "application/json"},
        )
        response = connection.getresponse()
        response.read()
        self.assertEqual(413, response.status)

        connection.request("GET", "/image?id=../_manifest.json")
        response = connection.getresponse()
        unsafe_body = response.read()
        self.assertEqual(404, response.status)
        self.assertNotIn(b"holdout_id", unsafe_body)

        connection.request("GET", "/api/state")
        response = connection.getresponse()
        state_body = response.read()
        self.assertEqual(200, response.status)
        self.assertNotIn(b"haltung_key", state_body)
        self.assertNotIn(b"source_sha256", state_body)

        connection.request("GET", "/")
        response = connection.getresponse()
        page_body = response.read()
        self.assertEqual(200, response.status)
        self.assertIn("BCC — Bogen".encode("utf-8"), page_body)

        connection.request(
            "GET",
            "/api/state",
            headers={"Host": "fremde-domain.example"},
        )
        response = connection.getresponse()
        response.read()
        self.assertEqual(421, response.status)

    def test_http_revision_verhindert_stilles_ueberschreiben_und_post_ist_exakt(self):
        self._write_holdout()
        store = self._store()
        store.prepare_output()
        server = create_server(store, port=0)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(thread.join, 2)
        self.addCleanup(server.server_close)
        self.addCleanup(server.shutdown)
        connection = http.client.HTTPConnection(
            "127.0.0.1",
            server.server_address[1],
            timeout=5,
        )
        self.addCleanup(connection.close)

        connection.request("GET", "/api/state")
        response = connection.getresponse()
        initial = json.loads(response.read())
        self.assertEqual(200, response.status)
        self.assertEqual(0, initial["revision"])

        first_payload = {
            "id": "bcc-rh-a",
            "decision": "positive",
            "comment": "Erste Entscheidung",
            "revision": initial["revision"],
        }
        connection.request(
            "POST",
            "/api/review",
            body=json.dumps(first_payload),
            headers={"Content-Type": "application/json"},
        )
        response = connection.getresponse()
        accepted = json.loads(response.read())
        self.assertEqual(200, response.status)
        self.assertEqual(1, accepted["revision"])
        saved_after_first = self.output.read_bytes()

        stale_payload = {
            **first_payload,
            "decision": "negative",
            "comment": "Veralteter Browser-Tab",
        }
        connection.request(
            "POST",
            "/api/review",
            body=json.dumps(stale_payload),
            headers={"Content-Type": "application/json"},
        )
        response = connection.getresponse()
        conflict = json.loads(response.read())
        self.assertEqual(409, response.status)
        self.assertIn("veraltet", conflict["error"])
        self.assertEqual(saved_after_first, self.output.read_bytes())
        self.assertEqual(1, store.state()["revision"])
        self.assertEqual("positive", store.state()["current"]["decision"])

        invalid_payload = {
            **first_payload,
            "revision": 1,
            "unexpected": True,
        }
        connection.request(
            "POST",
            "/api/review",
            body=json.dumps(invalid_payload),
            headers={"Content-Type": "application/json"},
        )
        response = connection.getresponse()
        response.read()
        self.assertEqual(400, response.status)
        self.assertEqual(saved_after_first, self.output.read_bytes())

        negative_revision_payload = {
            **first_payload,
            "revision": -1,
        }
        connection.request(
            "POST",
            "/api/review",
            body=json.dumps(negative_revision_payload),
            headers={"Content-Type": "application/json"},
        )
        response = connection.getresponse()
        response.read()
        self.assertEqual(400, response.status)
        self.assertEqual(saved_after_first, self.output.read_bytes())

    def test_quellenaenderung_nach_start_verhindert_entscheidung(self):
        self._write_holdout()
        store = self._store()
        store.prepare_output()
        output_before = self.output.read_bytes()
        image = self.holdout / "images" / "frame-a.jpg"
        image.write_bytes(image.read_bytes() + b"changed-after-start")

        with self.assertRaisesRegex(ValueError, "Review-Quelle"):
            store.set_decision(
                "bcc-rh-a",
                "positive",
                "Darf nicht gespeichert werden.",
                expected_revision=0,
            )

        self.assertEqual(output_before, self.output.read_bytes())
        self.assertEqual(0, store.state()["revision"])
        self.assertEqual(0, store.state()["done"])

    def test_tastatursteuerung_deckt_alle_entscheidungen_und_navigation_ab(self):
        for fragment in (
            'case "1"',
            'case "2"',
            'case "3"',
            'case "ArrowLeft"',
            'case "ArrowRight"',
            'saveDecision("positive")',
            'saveDecision("negative")',
            'saveDecision("exclude")',
        ):
            with self.subTest(fragment=fragment):
                self.assertIn(fragment, INDEX_HTML)
        self.assertIn("revision: reviewState.revision", INDEX_HTML)

    def test_ui_zeigt_festen_bcc_code_mit_klartext(self):
        self.assertIn("Prüfcode", INDEX_HTML)
        self.assertIn('id="targetCode"', INDEX_HTML)
        self.assertIn("BCC — Bogen", INDEX_HTML)
        self.assertNotIn("hidden_hint", INDEX_HTML)
        self.assertNotIn("xtf_code", INDEX_HTML)
        self.assertNotIn("model_prediction", INDEX_HTML)

    def test_erster_eingefrorener_holdout_ohne_purpose_bleibt_pruefbar(self):
        self._write_holdout()
        manifest_path = self.holdout / "_manifest.json"
        manifest = self._read_json(manifest_path)
        del manifest["purpose"]
        manifest["name"] = "SewerStudio BCC Release Holdout"
        manifest["holdout_id"] = (
            "64d06094c921e90440e96823d3fc8d5ec0275c6465840201a4092f1285fe5c2e"
        )
        manifest_path.write_bytes(self._json_bytes(manifest))

        store = self._store()

        self.assertEqual(manifest["holdout_id"], store.holdout_id)
        self.assertEqual(1, store.state()["total"])

    def test_beliebiger_purpose_loser_holdout_wird_nicht_als_v1_akzeptiert(self):
        self._write_holdout()
        manifest_path = self.holdout / "_manifest.json"
        manifest = self._read_json(manifest_path)
        del manifest["purpose"]
        manifest["name"] = "SewerStudio BCC Release Holdout"
        manifest_path.write_bytes(self._json_bytes(manifest))

        with self.assertRaisesRegex(ValueError, "BCC-Release-Holdout"):
            self._store()

    def test_paralleler_pruefplatz_ueberschreibt_keine_entscheidungen(self):
        self._write_holdout()
        first = self._store()
        second = self._store()
        first.prepare_output()

        with self.assertRaisesRegex(ValueError, "parallel"):
            second.prepare_output()

    def test_gleichzeitige_schreiber_werden_prozessweit_serialisiert(self):
        self._write_holdout()
        seed = self._store()
        seed.prepare_output()
        first = self._store()
        second = self._store()
        barrier = threading.Barrier(3)
        successes: list[str] = []
        errors: list[Exception] = []
        result_lock = threading.Lock()

        def write(store, decision: str) -> None:
            barrier.wait()
            try:
                store.set_decision("bcc-rh-a", decision)
                with result_lock:
                    successes.append(decision)
            except Exception as error:
                with result_lock:
                    errors.append(error)

        threads = [
            threading.Thread(target=write, args=(first, "positive")),
            threading.Thread(target=write, args=(second, "negative")),
        ]
        for thread in threads:
            thread.start()
        barrier.wait()
        for thread in threads:
            thread.join(timeout=5)

        self.assertTrue(all(not thread.is_alive() for thread in threads))
        self.assertEqual(1, len(successes))
        self.assertEqual(1, len(errors))
        self.assertIn("parallel", str(errors[0]))
        saved = self._read_json(self.output)
        self.assertEqual(
            successes[0],
            saved["decisions"]["bcc-rh-a"]["decision"],
        )

    def _store(self) -> BccReleaseHoldoutReviewStore:
        return BccReleaseHoldoutReviewStore(
            self.holdout,
            self.output,
            "Blind Reviewer",
            now_utc=self.now,
        )

    def _write_holdout(
        self,
        holdout: Path | None = None,
        candidates: list[dict[str, object]] | None = None,
    ) -> None:
        holdout = holdout or self.holdout
        images = holdout / "images"
        images.mkdir(parents=True)
        image_payload = b"\xff\xd8\xff" + b"x" * 2048
        image_path = images / "frame-a.jpg"
        image_path.write_bytes(image_payload)
        rows = candidates or [
            {
                "id": "bcc-rh-a",
                "frame_path": "frame-a.jpg",
                "haltung_key": "88442-88443",
                "kategorie": "bcc_blind_review",
                "source_sha256": "",
            }
        ]
        rows[0]["source_sha256"] = hashlib.sha256(image_payload).hexdigest()
        candidates_path = holdout / "_candidates.json"
        candidates_path.write_bytes(self._json_bytes(rows))
        hashes = {
            "_candidates.json": self._hash_entry(candidates_path),
            "images/frame-a.jpg": self._hash_entry(image_path),
        }
        manifest = {
            "schema_version": "1.0",
            "purpose": "bcc_release_holdout",
            "pilot": "BCC_bogen",
            "role": "acceptance",
            "holdout_id": "a" * 64,
            "frozen": True,
            "candidates_count": len(rows),
            "images_count": 1,
            "hash_algorithm": "sha256",
            "hashes_count": len(hashes),
            "hashes": hashes,
        }
        (holdout / "_manifest.json").write_bytes(self._json_bytes(manifest))

    @staticmethod
    def _json_bytes(value: object) -> bytes:
        return (
            json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
        ).encode("utf-8")

    @staticmethod
    def _read_json(path: Path):
        return json.loads(path.read_text(encoding="utf-8"))

    @staticmethod
    def _hash_entry(path: Path) -> dict[str, object]:
        payload = path.read_bytes()
        return {
            "sha256": hashlib.sha256(payload).hexdigest(),
            "size_bytes": len(payload),
        }

    def _rewrite_candidates_and_hash(
        self,
        holdout: Path,
        candidates: list[dict[str, object]],
    ) -> None:
        candidates_path = holdout / "_candidates.json"
        candidates_path.write_bytes(self._json_bytes(candidates))
        manifest_path = holdout / "_manifest.json"
        manifest = self._read_json(manifest_path)
        manifest["hashes"]["_candidates.json"] = self._hash_entry(candidates_path)
        manifest_path.write_bytes(self._json_bytes(manifest))

    def _make_not_frozen(self, holdout: Path) -> None:
        manifest_path = holdout / "_manifest.json"
        manifest = self._read_json(manifest_path)
        manifest["frozen"] = False
        manifest_path.write_bytes(self._json_bytes(manifest))

    @staticmethod
    def _tamper_candidates(holdout: Path) -> None:
        with (holdout / "_candidates.json").open("ab") as stream:
            stream.write(b" ")

    @staticmethod
    def _tamper_image(holdout: Path) -> None:
        with (holdout / "images" / "frame-a.jpg").open("ab") as stream:
            stream.write(b"tampered")

    @staticmethod
    def _add_untracked_file(holdout: Path) -> None:
        (holdout / "unexpected.txt").write_text("nicht gebunden", encoding="utf-8")

    def _remove_image_hash(self, holdout: Path) -> None:
        manifest_path = holdout / "_manifest.json"
        manifest = self._read_json(manifest_path)
        del manifest["hashes"]["images/frame-a.jpg"]
        manifest["hashes_count"] = len(manifest["hashes"])
        manifest_path.write_bytes(self._json_bytes(manifest))

    def _set_wrong_source_hash_and_rehash(self, holdout: Path) -> None:
        candidates = self._read_json(holdout / "_candidates.json")
        candidates[0]["source_sha256"] = "f" * 64
        self._rewrite_candidates_and_hash(holdout, candidates)

    def _set_invalid_holding_and_rehash(self, holdout: Path) -> None:
        candidates = self._read_json(holdout / "_candidates.json")
        candidates[0]["haltung_key"] = "unbekannt"
        self._rewrite_candidates_and_hash(holdout, candidates)


if __name__ == "__main__":
    unittest.main()
