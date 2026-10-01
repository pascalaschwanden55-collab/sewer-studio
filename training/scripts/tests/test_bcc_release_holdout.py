from __future__ import annotations

import hashlib
import importlib.util
import json
import shutil
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).resolve().parents[1] / "bcc_release_holdout.py"
SPEC = importlib.util.spec_from_file_location("bcc_release_holdout", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


JPEG_PREFIX = b"\xff\xd8\xff\xe0"


class BccReleaseHoldoutTests(unittest.TestCase):
    def test_strenger_negativsatz_schuetzt_bild_und_beide_haltungsrichtungen(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            legacy_root = root / "training" / "negatives" / "bcc_pilot"
            legacy_root.mkdir(parents=True)
            set_root = (
                root
                / "training"
                / "negatives"
                / "sets"
                / "bcc_hn_123456789abc"
            )
            images_root = set_root / "images"
            images_root.mkdir(parents=True)
            image = self._image(images_root / "img_reviewed.jpg", b"reviewed")
            image_sha = self._sha(image)
            strict_image = {
                "path": image.relative_to(root).as_posix(),
                "sha256": image_sha,
                "source_type": "reviewed_negative_set",
                "holding_key": "100-200",
                "physical_holding_key": "100|200",
            }
            provenance = {
                "set_id": "1" * 64,
                "manifest_sha256": "2" * 64,
                "images": 1,
            }

            with mock.patch.object(
                MODULE.negative_source_tools,
                "read_training_negative_sources",
                return_value=((strict_image,), (provenance,)),
            ):
                hashes, aliases, evidence = MODULE._scan_negative_pool(root)

            self.assertEqual({"100-200"}, set(hashes.values()))
            self.assertEqual("100-200", hashes[image_sha])
            self.assertTrue({"100-200", "200-100", "100|200"} <= aliases)
            self.assertEqual(1, evidence["reviewed_set_files"])
            self.assertEqual(0, evidence["legacy_files"])
            self.assertEqual("1" * 64, evidence["reviewed_sets"][0]["set_id"])

    def test_plan_sperrt_gleiche_haltung_und_gleiche_bytes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            seen_image = self._image(root / "seen.jpg", b"seen")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(seen_image),
                holding_key="100-200",
                image_source=seen_image,
            )
            source = root / "source"
            xtf = self._xtf(
                source,
                [
                    ("same-holding.jpg", "100-200", "BCCAY", "20260420", b"a"),
                    ("same-bytes.jpg", "300-400", "BCCAY", "20260420", b"seen"),
                    ("fresh-positive.jpg", "500-600", "BCCAY", "20260420", b"p"),
                    ("fresh-negative.jpg", "700-800", "BABAA", "20260420", b"n"),
                ],
            )

            plan = MODULE.build_holdout_plan(
                knowledge_root=root,
                base_model_path=base_model,
                sources=(MODULE.SourceSpec(source, xtf),),
                queue_positive=1,
                queue_negative=1,
                minimum_positive=1,
                minimum_negative=1,
                created_utc=datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
            )

            self.assertEqual(2, len(plan.items))
            self.assertEqual(
                {"500-600", "700-800"},
                {item.holding_key for item in plan.items},
            )
            self.assertFalse((root / "eval_set" / "subsets").exists())
            self.assertEqual(1, plan.blocked_same_holding)
            self.assertEqual(1, plan.blocked_same_hash)

    def test_umgedrehte_bekannte_haltung_wird_ebenfalls_gesperrt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            seen_image = self._image(root / "seen.jpg", b"seen")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(seen_image),
                holding_key="100-200",
                image_source=seen_image,
            )
            source = root / "source"
            xtf = self._xtf(
                source,
                [
                    ("reverse.jpg", "200-100", "BCCAY", "20260420", b"r"),
                    ("positive.jpg", "300-400", "BCCAY", "20260420", b"p"),
                    ("negative.jpg", "500-600", "BABAA", "20260420", b"n"),
                ],
            )

            plan = MODULE.build_holdout_plan(
                root,
                base_model,
                (MODULE.SourceSpec(source, xtf),),
                queue_positive=1,
                queue_negative=1,
                minimum_positive=1,
                minimum_negative=1,
                created_utc=datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
            )

            self.assertNotIn("200-100", {item.holding_key for item in plan.items})
            self.assertEqual(1, plan.blocked_same_holding)

    def test_foto_altlinie_braucht_sample_id_und_passenden_bildhash(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            gold = self._image(root / "gold.jpg", b"gold")
            samples = [
                {
                    "SampleId": "sample-1",
                    "CaseId": "900-901",
                    "FramePath": str(gold),
                }
            ]
            (root / "training_samples.json").write_text(
                json.dumps(samples),
                encoding="utf-8",
            )
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(gold),
                holding_key="foto_alt",
                source_id="sample-1",
                image_source=gold,
            )

            snapshot = MODULE.scan_contamination(root, base_model)
            self.assertIn("900-901", snapshot.holding_aliases)

            samples[0]["FramePath"] = str(self._image(root / "other.jpg", b"other"))
            (root / "training_samples.json").write_text(
                json.dumps(samples),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(ValueError, "Altlinie"):
                MODULE.scan_contamination(root, base_model)

    def test_dataset_haltung_muss_zum_training_sample_passen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            image = self._image(root / "candidate-source.jpg", b"candidate")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(image),
                holding_key="100-200",
                sample_holding_key="300-400",
                image_source=image,
            )

            with self.assertRaisesRegex(ValueError, "TrainingSample"):
                MODULE.scan_contamination(root, base_model)

    def test_publish_kopiert_geprueft_und_ueberschreibt_nie(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            source = root / "source"
            xtf = self._xtf(
                source,
                [
                    ("positive.jpg", "100-200", "BCCAY", "20260420", b"p"),
                    ("negative.jpg", "300-400", "BABAA", "20260420", b"n"),
                ],
            )
            originals = {
                path: path.read_bytes()
                for path in source.rglob("*.jpg")
            }
            plan = MODULE.build_holdout_plan(
                root,
                base_model,
                (MODULE.SourceSpec(source, xtf),),
                queue_positive=1,
                queue_negative=1,
                minimum_positive=1,
                minimum_negative=1,
                created_utc=datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
            )

            target = MODULE.publish_holdout(plan)

            self.assertTrue(target.is_dir())
            manifest = json.loads((target / "_manifest.json").read_text(encoding="utf-8"))
            candidates = json.loads(
                (target / "_candidates.json").read_text(encoding="utf-8")
            )
            self.assertTrue(manifest["frozen"])
            self.assertEqual("review_incomplete", manifest["dataset_status"])
            self.assertEqual("not_evaluated", manifest["release_status"])
            self.assertEqual(2, manifest["candidates_count"])
            self.assertEqual(2, len(candidates))
            self.assertEqual(
                originals,
                {path: path.read_bytes() for path in originals},
            )
            for relative, entry in manifest["hashes"].items():
                artifact = target / Path(relative)
                self.assertEqual(entry["sha256"], self._sha(artifact))

            with self.assertRaises(FileExistsError):
                MODULE.publish_holdout(plan)

    def test_publish_sperrt_nach_der_planung_veraenderte_xtf(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            source = root / "source"
            xtf = self._xtf(
                source,
                [
                    ("positive.jpg", "100-200", "BCCAY", "20260420", b"p"),
                    ("negative.jpg", "300-400", "BABAA", "20260420", b"n"),
                ],
            )
            plan = MODULE.build_holdout_plan(
                root,
                base_model,
                (MODULE.SourceSpec(source, xtf),),
                queue_positive=1,
                queue_negative=1,
                minimum_positive=1,
                minimum_negative=1,
                created_utc=datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
            )
            xtf.write_text(
                xtf.read_text(encoding="utf-8") + "\n<!-- veraendert -->",
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "XTF-Quelle"):
                MODULE.publish_holdout(plan)
            self.assertFalse(plan.target_root.exists())

    def test_status_braucht_vollstaendige_gebundene_review(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            plan = self._release_plan(root, base_model)
            holdout = MODULE.publish_holdout(plan)
            candidates = json.loads(
                (holdout / "_candidates.json").read_text(encoding="utf-8")
            )
            review_path = root / "review.json"
            review = self._review_document(holdout, candidates)
            review["decisions"] = {
                candidates[0]["id"]: {
                    "decision": "positive",
                    "comment": "",
                    "reviewed_at_utc": "2026-07-28T12:01:00Z",
                }
            }
            review_path.write_text(json.dumps(review), encoding="utf-8")

            incomplete = MODULE.evaluate_holdout_status(
                root,
                base_model,
                holdout,
                review_path,
            )
            self.assertEqual("review_incomplete", incomplete["dataset_status"])
            self.assertEqual("not_evaluated", incomplete["release_status"])

            review["decisions"] = self._complete_decisions(candidates)
            review_path.write_text(json.dumps(review), encoding="utf-8")
            ready = MODULE.evaluate_holdout_status(
                root,
                base_model,
                holdout,
                review_path,
            )
            self.assertEqual("ready_for_binary_evaluation", ready["dataset_status"])
            self.assertEqual(20, ready["positive_images"])
            self.assertEqual(20, ready["negative_images"])
            self.assertEqual("not_evaluated", ready["release_status"])

            first_id = str(candidates[0]["id"])
            malformed = json.loads(json.dumps(review))
            del malformed["decisions"][first_id]["comment"]
            review_path.write_text(json.dumps(malformed), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Entscheidung"):
                MODULE.evaluate_holdout_status(
                    root,
                    base_model,
                    holdout,
                    review_path,
                )

            review_path.write_text(json.dumps(review), encoding="utf-8")
            review["manifest_sha256"] = "0" * 64
            review_path.write_text(json.dumps(review), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Manifest"):
                MODULE.evaluate_holdout_status(
                    root,
                    base_model,
                    holdout,
                    review_path,
                )

    def test_manifest_muss_jedes_holdout_bild_hashen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            source = root / "source"
            xtf = self._xtf(
                source,
                [
                    ("positive.jpg", "100-200", "BCCAY", "20260420", b"p"),
                    ("negative.jpg", "300-400", "BABAA", "20260420", b"n"),
                ],
            )
            plan = MODULE.build_holdout_plan(
                root,
                base_model,
                (MODULE.SourceSpec(source, xtf),),
                queue_positive=1,
                queue_negative=1,
                minimum_positive=1,
                minimum_negative=1,
                created_utc=datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
            )
            holdout = MODULE.publish_holdout(plan)
            manifest_path = holdout / "_manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            image_entry = next(
                relative
                for relative in manifest["hashes"]
                if relative.startswith("images/")
            )
            del manifest["hashes"][image_entry]
            manifest["hashes_count"] = len(manifest["hashes"])
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "Hashabdeckung"):
                MODULE._validate_holdout_files(holdout)

    def test_status_sperrt_neuen_kandidaten_nach_dem_einfrieren(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            plan = self._release_plan(root, base_model)
            holdout = MODULE.publish_holdout(plan)
            candidates = json.loads(
                (holdout / "_candidates.json").read_text(encoding="utf-8")
            )
            review_path = root / "review.json"
            review = self._review_document(holdout, candidates)
            review["decisions"] = self._complete_decisions(candidates)
            review_path.write_text(json.dumps(review), encoding="utf-8")

            fresh_training_image = self._image(root / "fresh-training.jpg", b"fresh")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(fresh_training_image),
                holding_key="900-901",
                candidate_id="bcc-new",
                plan_id="b" * 64,
                weights_payload=b"candidate-new",
                image_source=fresh_training_image,
            )

            with self.assertRaisesRegex(ValueError, "Kontaminationsbestand"):
                MODULE.evaluate_holdout_status(
                    root,
                    base_model,
                    holdout,
                    review_path,
                )

    def test_kontaminationsscan_sperrt_veraendertes_evalbild(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            eval_root = root / "eval_set" / "subsets" / "frozen"
            image = self._image(eval_root / "images" / "eval.jpg", b"eval")
            candidates_path = eval_root / "_candidates.json"
            candidates_path.write_text(
                json.dumps(
                    [
                        {
                            "id": "eval-1",
                            "frame_path": image.name,
                            "haltung_key": "500-600",
                        }
                    ]
                ),
                encoding="utf-8",
            )
            hashes = {
                "_candidates.json": {
                    "sha256": self._sha(candidates_path),
                    "size_bytes": candidates_path.stat().st_size,
                },
                "images/eval.jpg": {
                    "sha256": self._sha(image),
                    "size_bytes": image.stat().st_size,
                },
            }
            (eval_root / "_manifest.json").write_text(
                json.dumps(
                    {
                        "frozen": True,
                        "hash_algorithm": "sha256",
                        "hashes_count": len(hashes),
                        "hashes": hashes,
                    }
                ),
                encoding="utf-8",
            )
            image.write_bytes(JPEG_PREFIX + b"changed" + b"x" * 2048)

            with self.assertRaisesRegex(ValueError, "Eingefrorene Eval-Datei"):
                MODULE.scan_contamination(root, base_model)

    def test_kontaminationsscan_sperrt_umgebogene_dataset_config(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            image = self._image(root / "candidate-source.jpg", b"candidate")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(image),
                holding_key="100-200",
                image_source=image,
            )
            dataset_root = root / "training" / "datasets" / ("a" * 64)
            (dataset_root / "data.yaml").write_text(
                "path: C:/external\ntrain: images/train\nval: images/val\nnc: 15\n",
                encoding="utf-8",
            )
            receipt_path = dataset_root / "_export_receipt.json"
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            receipt["data_yaml_sha256"] = self._sha(dataset_root / "data.yaml")
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            candidate_manifest_path = (
                root
                / "training"
                / "models"
                / "candidates"
                / "bcc-test"
                / "candidate_manifest.json"
            )
            candidate_manifest = json.loads(
                candidate_manifest_path.read_text(encoding="utf-8")
            )
            candidate_manifest["dataset"]["receipt_sha256"] = self._sha(receipt_path)
            candidate_manifest["dataset"]["data_yaml_sha256"] = self._sha(
                dataset_root / "data.yaml"
            )
            candidate_manifest_path.write_text(
                json.dumps(candidate_manifest),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "kanonische Bildpfade"):
                MODULE.scan_contamination(root, base_model)

    def test_ungebundenes_dataset_schuetzt_bild_und_haltung(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            image = self._image(root / "orphan-source.jpg", b"orphan")
            image_sha = self._sha(image)
            self._candidate(
                root,
                base_model,
                image_sha256=image_sha,
                holding_key="100-200",
                image_source=image,
            )
            shutil.rmtree(
                root / "training" / "models" / "candidates" / "bcc-test"
            )

            contamination = MODULE.scan_contamination(root, base_model)

            self.assertIn(image_sha, contamination.image_hashes)
            self.assertTrue(
                MODULE._holding_aliases("100-200")
                <= contamination.holding_aliases
            )
            self.assertEqual(0, contamination.evidence[0]["candidates"])
            self.assertEqual(1, contamination.evidence[0]["datasets"])
            self.assertEqual(1, contamination.evidence[0]["unbound_datasets"])

    def test_manipuliertes_ungebundenes_dataset_sperrt_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            image = self._image(root / "orphan-source.jpg", b"orphan")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(image),
                holding_key="100-200",
                image_source=image,
            )
            shutil.rmtree(
                root / "training" / "models" / "candidates" / "bcc-test"
            )
            dataset = root / "training" / "datasets" / ("a" * 64)
            label = next((dataset / "labels").rglob("*.txt"))
            label.write_text(
                "13 0.500000 0.500000 0.500000 0.500000\n",
                encoding="utf-8",
            )
            receipt_path = dataset / "_export_receipt.json"
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            receipt["labels"][0]["sha256"] = self._sha(label)
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "Labelinhalt"):
                MODULE.scan_contamination(root, base_model)

    def test_unbekanntes_64hex_dataset_ohne_manifest_sperrt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            (root / "training" / "datasets" / ("b" * 64)).mkdir()

            with self.assertRaisesRegex(ValueError, "Pfad fehlt"):
                MODULE.scan_contamination(root, base_model)

    def test_gemeinsames_kandidatendataset_wird_nur_einmal_tief_geprueft(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            image = self._image(root / "candidate-source.jpg", b"candidate")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(image),
                holding_key="100-200",
                image_source=image,
            )
            candidate_root = root / "training" / "models" / "candidates"
            shutil.copytree(candidate_root / "bcc-test", candidate_root / "bcc-test-2")

            original = MODULE._read_receipt_artifacts
            with mock.patch.object(
                MODULE,
                "_read_receipt_artifacts",
                wraps=original,
            ) as reader:
                contamination = MODULE.scan_contamination(root, base_model)

            self.assertEqual(2, reader.call_count)
            self.assertEqual(2, contamination.evidence[0]["candidates"])
            self.assertEqual(1, contamination.evidence[0]["datasets"])
            self.assertEqual(0, contamination.evidence[0]["unbound_datasets"])

    def test_neuer_kandidat_muss_dataset_konfiguration_binden(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            image = self._image(root / "candidate-source.jpg", b"candidate")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(image),
                holding_key="100-200",
                image_source=image,
            )
            manifest_path = (
                root
                / "training"
                / "models"
                / "candidates"
                / "bcc-test"
                / "candidate_manifest.json"
            )
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            for field in (
                "receipt_sha256",
                "data_yaml_sha256",
                "classes_sha256",
            ):
                del manifest["dataset"][field]
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "bindet Receipt"):
                MODULE.scan_contamination(root, base_model)

    def test_kandidat_mit_fremdem_oder_fehlendem_pilot_stoppt_scan(self) -> None:
        for pilot in (None, "anderer_pilot"):
            with self.subTest(pilot=pilot):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    base_model = self._base_model(root)
                    image = self._image(root / "candidate-source.jpg", b"candidate")
                    self._candidate(
                        root,
                        base_model,
                        image_sha256=self._sha(image),
                        holding_key="100-200",
                        image_source=image,
                    )
                    manifest_path = (
                        root
                        / "training"
                        / "models"
                        / "candidates"
                        / "bcc-test"
                        / "candidate_manifest.json"
                    )
                    manifest = json.loads(
                        manifest_path.read_text(encoding="utf-8")
                    )
                    if pilot is None:
                        del manifest["pilot"]
                    else:
                        manifest["pilot"] = pilot
                    manifest_path.write_text(
                        json.dumps(manifest),
                        encoding="utf-8",
                    )

                    with self.assertRaisesRegex(ValueError, "fremden Pilot"):
                        MODULE.scan_contamination(root, base_model)

    def test_legacy_collapse_bildname_muss_eindeutig_aufloesbar_sein(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            report = {
                "werkzeug": "model_collapse_check",
                "fehlalarme": {"dateien": ["nicht-vorhanden.jpg"]},
            }
            (
                root
                / "training"
                / "reports"
                / "collapse_check_legacy.json"
            ).write_text(json.dumps(report), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "nicht eindeutig"):
                MODULE.scan_contamination(root, base_model)

    def test_legacy_collapse_dateien_feld_wird_strikt_validiert(self) -> None:
        invalid_values = (
            "nicht-vorhanden.jpg",
            {"name": "nicht-vorhanden.jpg"},
            [None, ""],
        )
        for invalid_value in invalid_values:
            with self.subTest(dateien=invalid_value):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    base_model = self._base_model(root)
                    self._empty_candidate_roots(root)
                    report = {
                        "werkzeug": "model_collapse_check",
                        "fehlalarme": {"dateien": invalid_value},
                    }
                    (
                        root
                        / "training"
                        / "reports"
                        / "collapse_check_legacy.json"
                    ).write_text(json.dumps(report), encoding="utf-8")

                    with self.assertRaisesRegex(
                        ValueError,
                        "dateien",
                    ):
                        MODULE.scan_contamination(root, base_model)

    def test_status_sperrt_ausgetauschtes_basismodell(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            holdout = MODULE.publish_holdout(self._release_plan(root, base_model))
            candidates = json.loads(
                (holdout / "_candidates.json").read_text(encoding="utf-8")
            )
            review_path = root / "review.json"
            review = self._review_document(holdout, candidates)
            review["decisions"] = self._complete_decisions(candidates)
            review_path.write_text(json.dumps(review), encoding="utf-8")
            base_model.write_bytes(b"anderes-basismodell")

            with self.assertRaisesRegex(ValueError, "Basismodell"):
                MODULE.evaluate_holdout_status(
                    root,
                    base_model,
                    holdout,
                    review_path,
                )

    def test_status_sperrt_holdout_ausserhalb_der_eval_subset_wurzel(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            holdout = MODULE.publish_holdout(self._release_plan(root, base_model))
            moved = holdout.rename(root / holdout.name)
            candidates = json.loads(
                (moved / "_candidates.json").read_text(encoding="utf-8")
            )
            review_path = root / "review.json"
            review = self._review_document(moved, candidates)
            review["decisions"] = self._complete_decisions(candidates)
            review_path.write_text(json.dumps(review), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "Eval-Subset"):
                MODULE.evaluate_holdout_status(
                    root,
                    base_model,
                    moved,
                    review_path,
                )

    def test_status_sperrt_nachtraeglich_abgesenkte_mindestzahlen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            holdout = MODULE.publish_holdout(self._release_plan(root, base_model))
            manifest_path = holdout / "_manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["minimum_positive_holdings"] = 0
            manifest["minimum_negative_holdings"] = 0
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            candidates = json.loads(
                (holdout / "_candidates.json").read_text(encoding="utf-8")
            )
            review_path = root / "review.json"
            review = self._review_document(holdout, candidates)
            review["decisions"] = {
                candidate["id"]: {
                    "decision": "exclude",
                    "comment": "",
                    "reviewed_at_utc": "2026-07-28T12:01:00Z",
                }
                for candidate in candidates
            }
            review_path.write_text(json.dumps(review), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "Holdout-ID|Mindest"):
                MODULE.evaluate_holdout_status(
                    root,
                    base_model,
                    holdout,
                    review_path,
                )

    def test_publish_sperrt_verknuepfung_in_der_eval_ahnenkette(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            plan = self._release_plan(root, base_model)
            real_check = MODULE._is_reparse_point

            def marks_eval_root(path: Path) -> bool:
                if Path(path) == root / "eval_set":
                    return True
                return real_check(path)

            with mock.patch.object(
                MODULE,
                "_is_reparse_point",
                side_effect=marks_eval_root,
            ):
                with self.assertRaisesRegex(ValueError, "Verknuepfung"):
                    MODULE.publish_holdout(plan)

            self.assertFalse(plan.target_root.exists())

    def test_status_sperrt_verknuepfung_in_der_eval_ahnenkette(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            holdout = MODULE.publish_holdout(self._release_plan(root, base_model))
            candidates = json.loads(
                (holdout / "_candidates.json").read_text(encoding="utf-8")
            )
            review_path = root / "review.json"
            review = self._review_document(holdout, candidates)
            review["decisions"] = self._complete_decisions(candidates)
            review_path.write_text(json.dumps(review), encoding="utf-8")
            real_check = MODULE._is_reparse_point

            def marks_eval_root(path: Path) -> bool:
                if Path(path) == root / "eval_set":
                    return True
                return real_check(path)

            with mock.patch.object(
                MODULE,
                "_is_reparse_point",
                side_effect=marks_eval_root,
            ):
                with self.assertRaisesRegex(ValueError, "Verknuepfung"):
                    MODULE.evaluate_holdout_status(
                        root,
                        base_model,
                        holdout,
                        review_path,
                    )

    def test_kontaminationsscan_sperrt_verknuepfung_am_eval_root(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            MODULE.publish_holdout(self._release_plan(root, base_model))
            real_check = MODULE._is_reparse_point

            def marks_eval_root(path: Path) -> bool:
                if Path(path) == root / "eval_set":
                    return True
                return real_check(path)

            with mock.patch.object(
                MODULE,
                "_is_reparse_point",
                side_effect=marks_eval_root,
            ):
                with self.assertRaisesRegex(ValueError, "Verknuepfung"):
                    MODULE.scan_contamination(root, base_model)

    def test_kandidat_braucht_vollstaendige_receipt_bildabdeckung(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            image = self._image(root / "candidate-source.jpg", b"candidate")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(image),
                holding_key="100-200",
                image_source=image,
            )
            receipt_path = (
                root
                / "training"
                / "datasets"
                / ("a" * 64)
                / "_export_receipt.json"
            )
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            receipt["images"] = []
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            self._rebind_candidate_receipt(root)

            with self.assertRaisesRegex(ValueError, "Receipt-Bilder"):
                MODULE.scan_contamination(root, base_model)

    def test_publish_scannt_kontamination_unmittelbar_neu(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            self._empty_candidate_roots(root)
            plan = self._release_plan(root, base_model)
            selected = plan.items[0]
            (root / "training_samples.json").write_text(
                json.dumps(
                    [
                        {
                            "SampleId": "spaet-hinzugekommen",
                            "CaseId": selected.holding_key,
                            "FramePath": str(selected.source_path),
                        }
                    ]
                ),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "Kontaminationsbestand"):
                MODULE.publish_holdout(plan)

            self.assertFalse(plan.target_root.exists())

    def test_kandidat_sperrt_ungelistete_dataset_dateien(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            image = self._image(root / "candidate-source.jpg", b"candidate")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(image),
                holding_key="100-200",
                image_source=image,
            )
            extra = (
                root
                / "training"
                / "datasets"
                / ("a" * 64)
                / "images"
                / "val"
                / "nicht-im-receipt.jpg"
            )
            self._image(extra, b"ungelistet")

            with self.assertRaisesRegex(ValueError, "Dateimenge"):
                MODULE.scan_contamination(root, base_model)

    def test_kandidat_bindet_labelinhalt_an_das_dataset_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            base_model = self._base_model(root)
            image = self._image(root / "candidate-source.jpg", b"candidate")
            self._candidate(
                root,
                base_model,
                image_sha256=self._sha(image),
                holding_key="100-200",
                image_source=image,
            )
            dataset = root / "training" / "datasets" / ("a" * 64)
            label = next((dataset / "labels").rglob("*.txt"))
            label.write_text(
                "13 0.500000 0.500000 0.500000 0.500000\n",
                encoding="utf-8",
            )
            receipt_path = dataset / "_export_receipt.json"
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            receipt["labels"][0]["sha256"] = self._sha(label)
            receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
            self._rebind_candidate_receipt(root)

            with self.assertRaisesRegex(ValueError, "Labelinhalt"):
                MODULE.scan_contamination(root, base_model)

    def _rebind_candidate_receipt(
        self,
        root: Path,
        candidate_id: str = "bcc-test",
    ) -> None:
        receipt_path = (
            root
            / "training"
            / "datasets"
            / ("a" * 64)
            / "_export_receipt.json"
        )
        manifest_path = (
            root
            / "training"
            / "models"
            / "candidates"
            / candidate_id
            / "candidate_manifest.json"
        )
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["dataset"]["receipt_sha256"] = self._sha(receipt_path)
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

    @staticmethod
    def _base_model(root: Path) -> Path:
        path = root / "base.pt"
        path.write_bytes(b"base-model")
        timestamp = datetime(2026, 4, 12, 10, 28, tzinfo=timezone.utc).timestamp()
        path.touch()
        import os

        os.utime(path, (timestamp, timestamp))
        return path

    @staticmethod
    def _empty_candidate_roots(root: Path) -> None:
        (root / "training" / "models" / "candidates").mkdir(
            parents=True, exist_ok=True
        )
        (root / "training" / "datasets").mkdir(parents=True, exist_ok=True)
        (root / "training" / "reports").mkdir(parents=True, exist_ok=True)
        (root / "training" / "negatives" / "bcc_pilot").mkdir(
            parents=True, exist_ok=True
        )
        (root / "eval_set").mkdir(parents=True, exist_ok=True)
        samples_path = root / "training_samples.json"
        if not samples_path.exists():
            samples_path.write_text("[]", encoding="utf-8")

    def _candidate(
        self,
        root: Path,
        base_model: Path,
        *,
        image_sha256: str,
        holding_key: str,
        source_id: str = "sample-direct",
        candidate_id: str = "bcc-test",
        plan_id: str = "a" * 64,
        weights_payload: bytes = b"candidate",
        image_source: Path,
        sample_holding_key: str | None = None,
    ) -> None:
        self._empty_candidate_roots(root)
        samples_path = root / "training_samples.json"
        samples = json.loads(samples_path.read_text(encoding="utf-8"))
        if not any(sample.get("SampleId") == source_id for sample in samples):
            samples.append(
                {
                    "SampleId": source_id,
                    "CaseId": sample_holding_key or holding_key,
                    "FramePath": str(image_source),
                }
            )
            samples_path.write_text(json.dumps(samples), encoding="utf-8")
        dataset = root / "training" / "datasets" / plan_id
        dataset.mkdir()
        dataset_manifest = {
            "schema_version": "2.0",
            "plan_id": plan_id,
            "images": [
                {
                    "image_sha256": image_sha256,
                    "holding_key": holding_key,
                    "target": "train",
                    "target_file_name": f"img_{image_sha256}.jpg",
                    "labels": [
                        {
                            "class_id": 14,
                            "class_name": "BCC_bogen",
                            "bounding_box": {
                                "x_center": 0.5,
                                "y_center": 0.5,
                                "width": 0.5,
                                "height": 0.5,
                                "is_valid": True,
                            },
                            "sources": [
                                {
                                    "source_type": "training_sample",
                                    "source_id": source_id,
                                    "stable_key": f"sample:{source_id}",
                                }
                            ],
                        }
                    ],
                }
            ],
        }
        manifest_bytes = (
            json.dumps(dataset_manifest, ensure_ascii=False, indent=2) + "\n"
        ).encode("utf-8")
        (dataset / "manifest.json").write_bytes(manifest_bytes)
        dataset_image = dataset / "images" / "train" / f"img_{image_sha256}.jpg"
        dataset_image.parent.mkdir(parents=True)
        dataset_image.write_bytes(image_source.read_bytes())
        dataset_label = (
            dataset / "labels" / "train" / f"img_{image_sha256}.txt"
        )
        dataset_label.parent.mkdir(parents=True)
        dataset_label.write_bytes(
            b"14 0.500000 0.500000 0.500000 0.500000\n",
        )
        classes_path = dataset / "classes.txt"
        classes_path.write_text(
            "".join(f"class_{index}\n" for index in range(15)),
            encoding="utf-8",
        )
        data_yaml_path = dataset / "data.yaml"
        data_yaml_path.write_text(
            "path: .\n"
            "train: images/train\n"
            "val: images/val\n"
            "nc: 15\n"
            "names:\n"
            + "".join(f"  {index}: class_{index}\n" for index in range(15)),
            encoding="utf-8",
        )
        (dataset / "_export_receipt.json").write_text(
            json.dumps(
                {
                    "schema_version": "2.0",
                    "plan_id": plan_id,
                    "plan_sha256": plan_id,
                    "manifest_sha256": hashlib.sha256(manifest_bytes).hexdigest(),
                    "class_count": 15,
                    "classes_sha256": self._sha(classes_path),
                    "data_yaml_sha256": self._sha(data_yaml_path),
                    "images": [
                        {
                            "path": f"images/train/img_{image_sha256}.jpg",
                            "sha256": image_sha256,
                        }
                    ],
                    "labels": [
                        {
                            "path": f"labels/train/img_{image_sha256}.txt",
                            "sha256": self._sha(dataset_label),
                        }
                    ],
                }
            ),
            encoding="utf-8",
        )
        candidate = root / "training" / "models" / "candidates" / candidate_id
        candidate.mkdir()
        weights = candidate / "best.pt"
        weights.write_bytes(weights_payload)
        (candidate / "candidate_manifest.json").write_text(
            json.dumps(
                {
                    "schema_version": "1.0",
                    "candidate_status": "not_deployed",
                    "pilot": "BCC_bogen",
                    "created_utc": "2026-07-28T10:00:00Z",
                    "dataset": {
                        "plan_id": plan_id,
                        "manifest_sha256": hashlib.sha256(manifest_bytes).hexdigest(),
                        "receipt_sha256": self._sha(
                            dataset / "_export_receipt.json"
                        ),
                        "data_yaml_sha256": self._sha(data_yaml_path),
                        "classes_sha256": self._sha(classes_path),
                        "images": 1,
                    },
                    "weights": {
                        "base_path": str(base_model),
                        "base_sha256": self._sha(base_model),
                        "candidate_path": str(weights),
                        "candidate_sha256": self._sha(weights),
                    },
                }
            ),
            encoding="utf-8",
        )

    def _xtf(
        self,
        source_root: Path,
        rows: list[tuple[str, str, str, str, bytes]],
    ) -> Path:
        photo_root = source_root / "Foto"
        photo_root.mkdir(parents=True)
        investigations: list[str] = []
        damages: list[str] = []
        files: list[str] = []
        for index, (file_name, holding, code, inspection_date, payload) in enumerate(rows):
            investigation_id = f"u{index}"
            damage_id = f"d{index}"
            investigations.append(
                f"""
                <VSA_KEK_2020_LV95.KEK.Untersuchung TID="{investigation_id}">
                  <Bezeichnung>{holding}</Bezeichnung>
                  <Zeitpunkt>{inspection_date}</Zeitpunkt>
                </VSA_KEK_2020_LV95.KEK.Untersuchung>
                """
            )
            damages.append(
                f"""
                <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="{damage_id}">
                  <UntersuchungRef REF="{investigation_id}" />
                  <KanalSchadencode>{code}</KanalSchadencode>
                </VSA_KEK_2020_LV95.KEK.Kanalschaden>
                """
            )
            files.append(
                f"""
                <VSA_KEK_2020_LV95.KEK.Datei TID="f{index}">
                  <Art>Foto</Art>
                  <Bezeichnung>{file_name}</Bezeichnung>
                  <Klasse>Kanalschaden</Klasse>
                  <Objekt>{damage_id}</Objekt>
                  <Relativpfad>Foto</Relativpfad>
                </VSA_KEK_2020_LV95.KEK.Datei>
                """
            )
            self._image(photo_root / file_name, payload)
        xtf = source_root / "source.xtf"
        xtf.write_text(
            (
                '<?xml version="1.0" encoding="utf-8"?>'
                '<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3"><DATASECTION><BASKET>'
                + "".join(investigations + damages + files)
                + "</BASKET></DATASECTION></TRANSFER>"
            ),
            encoding="utf-8",
        )
        return xtf

    @staticmethod
    def _image(path: Path, payload: bytes) -> Path:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(JPEG_PREFIX + payload + b"x" * 2048)
        return path

    @staticmethod
    def _sha(path: Path) -> str:
        return hashlib.sha256(path.read_bytes()).hexdigest()

    def _review_document(
        self,
        holdout: Path,
        candidates: list[dict[str, object]],
    ) -> dict[str, object]:
        manifest_sha = self._sha(holdout / "_manifest.json")
        candidates_sha = self._sha(holdout / "_candidates.json")
        return {
            "schema_version": "1.0",
            "purpose": "bcc_release_holdout_review",
            "holdout_id": json.loads(
                (holdout / "_manifest.json").read_text(encoding="utf-8")
            )["holdout_id"],
            "manifest_sha256": manifest_sha,
            "candidates_sha256": candidates_sha,
            "reviewer": "Besitzer",
            "updated_at_utc": "2026-07-28T12:00:00Z",
            "decisions": {},
        }

    def _release_plan(self, root: Path, base_model: Path):
        source = root / "release-source"
        xtf = self._xtf(source, self._release_rows())
        return MODULE.build_holdout_plan(
            root,
            base_model,
            (MODULE.SourceSpec(source, xtf),),
            queue_positive=20,
            queue_negative=20,
            minimum_positive=20,
            minimum_negative=20,
            created_utc=datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
        )

    @staticmethod
    def _release_rows() -> list[tuple[str, str, str, str, bytes]]:
        positives = [
            (
                f"positive-{index}.jpg",
                f"{1000 + index}-{2000 + index}",
                "BCCAY",
                "20260420",
                f"positive-{index}".encode(),
            )
            for index in range(20)
        ]
        negatives = [
            (
                f"negative-{index}.jpg",
                f"{3000 + index}-{4000 + index}",
                "BABAA",
                "20260420",
                f"negative-{index}".encode(),
            )
            for index in range(20)
        ]
        return positives + negatives

    @staticmethod
    def _complete_decisions(
        candidates: list[dict[str, object]],
    ) -> dict[str, dict[str, str]]:
        return {
            str(candidate["id"]): {
                "decision": "positive" if index < 20 else "negative",
                "comment": "",
                "reviewed_at_utc": "2026-07-28T12:01:00Z",
            }
            for index, candidate in enumerate(candidates)
        }


if __name__ == "__main__":
    unittest.main()
