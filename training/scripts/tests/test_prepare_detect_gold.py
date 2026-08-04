from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import sys
import tempfile
import unittest
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterator
from unittest import mock

from PIL import Image

GOLD_AUDIT_HELPER_PATH = Path(__file__).with_name("test_gold_stock_audit.py")
GOLD_AUDIT_HELPER_SPEC = importlib.util.spec_from_file_location(
    "prepare_detect_gold_test_fixture",
    GOLD_AUDIT_HELPER_PATH,
)
assert (
    GOLD_AUDIT_HELPER_SPEC is not None
    and GOLD_AUDIT_HELPER_SPEC.loader is not None
)
GOLD_AUDIT_HELPER = importlib.util.module_from_spec(GOLD_AUDIT_HELPER_SPEC)
ORIGINAL_GOLD_AUDIT_MODULE = sys.modules.get("gold_stock_audit")
try:
    GOLD_AUDIT_HELPER_SPEC.loader.exec_module(GOLD_AUDIT_HELPER)
finally:
    if ORIGINAL_GOLD_AUDIT_MODULE is None:
        sys.modules.pop("gold_stock_audit", None)
    else:
        sys.modules["gold_stock_audit"] = ORIGINAL_GOLD_AUDIT_MODULE

GOLD_AUDIT_MODULE = GOLD_AUDIT_HELPER.MODULE
create_reviewed_negative_set = GOLD_AUDIT_HELPER.create_reviewed_negative_set


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "prepare_detect_gold.py"
SPEC = importlib.util.spec_from_file_location("prepare_detect_gold", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

IMAGE_SIZE = (8, 4)
VALID_RLE = "0,10,5,17"


class PrepareDetectGoldTests(unittest.TestCase):
    def test_dry_run_waehlt_alle_map_train_val_und_schreibt_nichts(self) -> None:
        with self._scenario() as scenario:
            preparation = MODULE.build_preparation(
                scenario["root"],
                "Besitzer",
                scenario["audit_path"],
            )

            self.assertEqual(
                ["sample-bab-train", "sample-bcc-val"],
                [sample.sample_id for sample in preparation.selected_samples],
            )
            self.assertEqual(
                ["sample-aec-discard"],
                list(preparation.discarded_sample_ids),
            )
            self.assertEqual(
                ["sample-bcc-test"],
                list(preparation.excluded_test_sample_ids),
            )
            self.assertEqual(
                {"BAB_riss", "BCC_bogen"},
                {sample.target_class for sample in preparation.selected_samples},
            )
            self.assertEqual(2, len(preparation.negative_images))
            self.assertTrue(
                all(
                    image["review_decision"] == "all_classes_clear"
                    for image in preparation.negative_images
                )
            )
            self.assertFalse(preparation.receipt_path.exists())

    def test_dry_run_ergaenzt_neues_eingefrorenes_eval_set(self) -> None:
        with self._scenario() as scenario:
            self._write_eval_set(
                scenario["root"],
                "detect_release_holdout_neu",
            )

            preparation = MODULE.build_preparation(
                scenario["root"],
                "Besitzer",
                scenario["audit_path"],
            )

            self.assertEqual(3, len(preparation.protected_sets))
            self.assertIn(
                "dev-val-detect-release-holdout-neu-v1",
                {entry["set_id"] for entry in preparation.protected_sets},
            )

    def test_dry_run_lehnt_geaendertes_bisheriges_eval_set_ab(self) -> None:
        with self._scenario() as scenario:
            manifest = (
                scenario["root"]
                / "eval_set"
                / "subsets"
                / "eval_visible_clean_eval_set"
                / "_manifest.json"
            )
            manifest.write_text(
                '{"frozen":true,"geaendert":true}\n',
                encoding="utf-8",
            )

            with self.assertRaisesRegex(
                ValueError,
                "Geschuetztes Eval-Manifest stimmt nicht",
            ):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

    def test_persoenlicher_goldfreigabebeleg_ist_verpflichtend(self) -> None:
        with self._scenario() as scenario:
            migration = self._read_json(scenario["migration_path"])
            del migration["personal_gold_approval"]
            self._write_json(scenario["migration_path"], migration)

            with self.assertRaisesRegex(ValueError, "personal_gold_approval"):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

    def test_persoenlicher_goldfreigabebeleg_bindet_audit_und_samples(self) -> None:
        for field, expected_error in (
            ("gold_audit_sha256", "aktuellen Gold-Audit"),
            ("training_samples_sha256", "aktuelle.*training_samples"),
        ):
            with self.subTest(field=field), self._scenario() as scenario:
                migration = self._read_json(scenario["migration_path"])
                migration["personal_gold_approval"][field] = "a" * 64
                self._write_json(scenario["migration_path"], migration)

                with self.assertRaisesRegex(ValueError, expected_error):
                    MODULE.build_preparation(
                        scenario["root"],
                        "Besitzer",
                        scenario["audit_path"],
                    )

    def test_persoenlicher_goldfreigabebeleg_deckt_alle_audit_codes_ab(
        self,
    ) -> None:
        with self._scenario() as scenario:
            migration = self._read_json(scenario["migration_path"])
            migration["personal_gold_approval"]["source_codes"].remove("BABBC")
            self._write_json(scenario["migration_path"], migration)

            with self.assertRaisesRegex(
                ValueError,
                "source_codes.*BABBC",
            ):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

    def test_pending_review_oder_unbekannter_code_stoppt_fail_closed(self) -> None:
        with self._scenario() as scenario:
            migration = self._read_json(scenario["migration_path"])
            bab = next(
                entry
                for entry in migration["entries"]
                if entry["source_key"] == "BABBC"
            )
            bab["approval_status"] = "pending"
            bab["approved_by"] = None
            bab["approved_utc"] = None
            self._write_json(scenario["migration_path"], migration)

            with self.assertRaisesRegex(ValueError, "pending"):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

            migration["entries"] = [
                entry
                for entry in migration["entries"]
                if entry["source_key"] != "BABBC"
            ]
            migration["entry_counts"]["total"] = len(migration["entries"])
            migration["entry_counts"]["by_source_kind"]["teacher_vsa_code"] = len(
                migration["entries"]
            )
            migration["entry_counts"]["teacher_observed_total"] = len(
                migration["entries"]
            )
            self._write_json(scenario["migration_path"], migration)

            with self.assertRaisesRegex(ValueError, "keine.*teacher_vsa_code"):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

    def test_bild_ausserhalb_goldroot_oder_falscher_hash_stoppt(self) -> None:
        with self._scenario() as scenario:
            samples = self._read_json(scenario["samples_path"])
            outside = scenario["root"] / "fremd.png"
            self._image(outside, 211)
            samples[0]["FramePath"] = str(outside)
            self._write_json(scenario["samples_path"], samples)
            self._refresh_audit_samples_hash(scenario)
            audit = self._read_json(scenario["audit_path"])
            audit["samples"][0]["image_sha256"] = self._sha256(outside)
            self._write_json(scenario["audit_path"], audit)
            self._refresh_personal_gold_approval(scenario)

            with self.assertRaisesRegex(ValueError, "Goldroot"):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

        with self._scenario() as scenario:
            audit = self._read_json(scenario["audit_path"])
            audit["samples"][0]["image_sha256"] = "a" * 64
            self._write_json(scenario["audit_path"], audit)
            self._refresh_personal_gold_approval(scenario)

            with self.assertRaisesRegex(ValueError, "Bild-Hash"):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

    def test_pdfphoto_braucht_die_strenge_persoenliche_pruefspur(self) -> None:
        with self._scenario() as scenario:
            samples = self._read_json(scenario["samples_path"])
            pdf = next(
                sample
                for sample in samples
                if sample["SampleId"] == "sample-bcc-val"
            )
            pdf["Notes"] = "unvollstaendig"
            self._write_json(scenario["samples_path"], samples)
            self._refresh_audit_samples_hash(scenario)

            with self.assertRaisesRegex(ValueError, "PDF-Pruefspur"):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

    def test_audit_bindet_die_aktuelle_registry_bytegenau(self) -> None:
        with self._scenario() as scenario:
            scenario["registry_path"].write_bytes(
                scenario["registry_path"].read_bytes() + b"\n"
            )

            with self.assertRaisesRegex(ValueError, "Exportregister.*geaendert"):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

    def test_legacy_negative_duerfen_nicht_mit_striktem_satz_gemischt_werden(
        self,
    ) -> None:
        with self._scenario(include_legacy_negative=True) as scenario:
            with self.assertRaisesRegex(ValueError, "Legacy"):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

    def test_negativbild_aus_gold_testhaltung_stoppt_fail_closed(self) -> None:
        test_holding = self._holding_for_role(3, "test")
        with self._scenario(
            negative_holdings=(test_holding, "910003-910004"),
        ) as scenario:
            with self.assertRaisesRegex(
                ValueError,
                "Negativbild.*Audit-Testhaltung",
            ):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

    def test_negative_audit_rollenschutz_prueft_bild_und_gegenrichtung(self) -> None:
        image_sha = "a" * 64
        other_sha = "b" * 64
        physical = MODULE._physical_holding_key("100-200")
        negative = {
            "sha256": image_sha,
            "holding_key": "100-200",
            "physical_holding_key": physical,
            "split": "train",
        }

        with self.subTest("identisches Bild bleibt auch bei gleicher Rolle verboten"):
            with self.assertRaisesRegex(ValueError, "identisches Goldbild"):
                MODULE._validate_negative_audit_roles(
                    {image_sha: "train"},
                    {},
                    [negative],
                )

        with self.subTest("gleiche physische Haltung in gleicher Rolle ist erlaubt"):
            MODULE._validate_negative_audit_roles(
                {other_sha: "train"},
                {physical: "train"},
                [negative],
            )

        reverse_negative = dict(negative)
        reverse_negative["holding_key"] = "200-100"
        with self.subTest("Gegenrichtung einer Testhaltung bleibt verboten"):
            with self.assertRaisesRegex(ValueError, "Audit-Testhaltung"):
                MODULE._validate_negative_audit_roles(
                    {},
                    {physical: "test"},
                    [reverse_negative],
                )

        with self.subTest("verschiedene Train-Validation-Rollen bleiben verboten"):
            with self.assertRaisesRegex(ValueError, "verschiedenen Rollen"):
                MODULE._validate_negative_audit_roles(
                    {},
                    {physical: "val"},
                    [negative],
                )

    def test_execute_erneuert_registry_und_archiviert_registry_und_beleg(self) -> None:
        with self._scenario() as scenario:
            receipt_path = (
                scenario["root"]
                / "training"
                / "pilots"
                / "DETECT_ALL"
                / "registry_setup_v1.json"
            )
            receipt_path.parent.mkdir(parents=True)
            old_receipt = b'{"alter_beleg":true}\n'
            receipt_path.write_bytes(old_receipt)
            old_registry = scenario["registry_path"].read_bytes()
            preparation = MODULE.build_preparation(
                scenario["root"],
                "Besitzer",
                scenario["audit_path"],
            )

            MODULE.execute_preparation(
                preparation,
                "Besitzer",
                datetime(2026, 7, 30, 18, 0, tzinfo=timezone.utc),
                renew_existing=True,
            )

            old_registry_sha = hashlib.sha256(old_registry).hexdigest()
            old_receipt_sha = hashlib.sha256(old_receipt).hexdigest()
            self.assertEqual(
                old_registry,
                (
                    receipt_path.parent
                    / "registry_history"
                    / f"{old_registry_sha}.json"
                ).read_bytes(),
            )
            self.assertEqual(
                old_receipt,
                (
                    receipt_path.parent
                    / "receipt_history"
                    / f"{old_receipt_sha}.json"
                ).read_bytes(),
            )

            registry = self._read_json(scenario["registry_path"])
            self.assertEqual("approved", registry["approval_status"])
            self.assertEqual(
                ["sample-bab-train", "sample-bcc-val"],
                registry["approved_sample_ids"],
            )
            self.assertEqual(2, len(registry["negative_images"]))
            self.assertEqual(
                {"train", "development_validation"},
                set(registry["holding_roles"].values()),
            )

            receipt = self._read_json(receipt_path)
            self.assertEqual("detect_all_registry_preparation", receipt["purpose"])
            self.assertEqual(old_registry_sha, receipt["previous_registry_sha256"])
            self.assertEqual(2, receipt["selected_images"])
            self.assertEqual(1, receipt["discarded_images"])
            self.assertEqual(1, receipt["test_images_excluded"])
            self.assertEqual(
                self._sha256(scenario["migration_path"]),
                receipt["migration_sha256"],
            )
            self.assertEqual(
                self._sha256(MODULE.ACTIVE_CLASS_MAP_PATH),
                receipt["class_map_sha256"],
            )
            self.assertFalse(
                (receipt_path.parent / MODULE.TRANSACTION_FILE_NAME).exists()
            )

    def test_execute_verlangt_renew_existing(self) -> None:
        with self._scenario() as scenario:
            old_registry = scenario["registry_path"].read_bytes()
            preparation = MODULE.build_preparation(
                scenario["root"],
                "Besitzer",
                scenario["audit_path"],
            )

            with self.assertRaisesRegex(ValueError, "renew-existing"):
                MODULE.execute_preparation(
                    preparation,
                    "Besitzer",
                    datetime.now(timezone.utc),
                    renew_existing=False,
                )

            self.assertEqual(old_registry, scenario["registry_path"].read_bytes())
            self.assertFalse(preparation.receipt_path.exists())

    def test_execute_prueft_migration_erneut_vor_dem_schreiben(self) -> None:
        with self._scenario() as scenario:
            old_registry = scenario["registry_path"].read_bytes()
            preparation = MODULE.build_preparation(
                scenario["root"],
                "Besitzer",
                scenario["audit_path"],
            )
            scenario["migration_path"].write_bytes(
                scenario["migration_path"].read_bytes() + b"\n"
            )

            with self.assertRaisesRegex(ValueError, "Migration"):
                MODULE.execute_preparation(
                    preparation,
                    "Besitzer",
                    datetime.now(timezone.utc),
                    renew_existing=True,
                )

            self.assertEqual(old_registry, scenario["registry_path"].read_bytes())
            self.assertFalse(preparation.receipt_path.exists())

    def test_fehler_beim_registrywechsel_setzt_den_aktiven_beleg_zurueck(self) -> None:
        with self._scenario() as scenario:
            receipt_path = (
                scenario["root"]
                / "training"
                / "pilots"
                / "DETECT_ALL"
                / "registry_setup_v1.json"
            )
            receipt_path.parent.mkdir(parents=True)
            old_receipt = b'{"alter_beleg":true}\n'
            receipt_path.write_bytes(old_receipt)
            old_registry = scenario["registry_path"].read_bytes()
            preparation = MODULE.build_preparation(
                scenario["root"],
                "Besitzer",
                scenario["audit_path"],
            )
            real_replace = os.replace

            def fail_registry_replace(source: object, destination: object) -> None:
                if Path(destination) == preparation.registry_path:
                    raise OSError("simulierter Registryfehler")
                real_replace(source, destination)

            with mock.patch.object(
                MODULE.os,
                "replace",
                side_effect=fail_registry_replace,
            ):
                with self.assertRaisesRegex(OSError, "simulierter Registryfehler"):
                    MODULE.execute_preparation(
                        preparation,
                        "Besitzer",
                        datetime.now(timezone.utc),
                        renew_existing=True,
                    )

            self.assertEqual(old_registry, scenario["registry_path"].read_bytes())
            self.assertEqual(old_receipt, receipt_path.read_bytes())

    def test_absturz_zwischen_beiden_dateitauschen_wird_sicher_erkannt(
        self,
    ) -> None:
        with self._scenario() as scenario:
            preparation = MODULE.build_preparation(
                scenario["root"],
                "Besitzer",
                scenario["audit_path"],
            )
            old_registry = scenario["registry_path"].read_bytes()
            real_replace = os.replace

            def crash_before_registry(source: object, destination: object) -> None:
                if Path(destination) == preparation.registry_path:
                    raise KeyboardInterrupt("simulierter Prozessabsturz")
                real_replace(source, destination)

            with mock.patch.object(
                MODULE.os,
                "replace",
                side_effect=crash_before_registry,
            ):
                with self.assertRaisesRegex(
                    KeyboardInterrupt,
                    "simulierter Prozessabsturz",
                ):
                    MODULE.execute_preparation(
                        preparation,
                        "Besitzer",
                        datetime.now(timezone.utc),
                        renew_existing=True,
                    )

            transaction_path = (
                preparation.receipt_path.parent / MODULE.TRANSACTION_FILE_NAME
            )
            self.assertTrue(transaction_path.is_file())
            self.assertEqual(old_registry, scenario["registry_path"].read_bytes())
            self.assertTrue(preparation.receipt_path.is_file())
            with self.assertRaisesRegex(ValueError, "unvollstaendiger"):
                MODULE.build_preparation(
                    scenario["root"],
                    "Besitzer",
                    scenario["audit_path"],
                )

            self.assertEqual(
                "rolled_back",
                MODULE.recover_incomplete_transaction(scenario["root"]),
            )
            self.assertEqual(old_registry, scenario["registry_path"].read_bytes())
            self.assertFalse(preparation.receipt_path.exists())
            self.assertFalse(transaction_path.exists())

    def test_main_beendet_nach_wiedererkanntem_vollstaendigem_commit(self) -> None:
        args = argparse.Namespace(
            execute=True,
            renew_existing=True,
            knowledge_root=Path(r"C:\nur_test"),
            approved_by="Besitzer",
            gold_audit=Path("audit.json"),
        )
        with (
            mock.patch.object(MODULE, "_parse_args", return_value=args),
            mock.patch.object(
                MODULE,
                "recover_incomplete_transaction",
                return_value="committed",
            ),
            mock.patch.object(MODULE, "build_preparation") as build,
        ):
            self.assertEqual(0, MODULE.main())
        build.assert_not_called()

    def test_aktive_dateien_und_archive_sperren_links_und_junctions(self) -> None:
        for target_kind in ("registry", "receipt", "archive"):
            with self.subTest(target_kind=target_kind), self._scenario() as scenario:
                receipt_path = (
                    scenario["root"]
                    / "training"
                    / "pilots"
                    / "DETECT_ALL"
                    / "registry_setup_v1.json"
                )
                if target_kind == "receipt":
                    receipt_path.parent.mkdir(parents=True, exist_ok=True)
                    receipt_path.write_bytes(b'{"alt":true}\n')
                    target = receipt_path
                elif target_kind == "archive":
                    preparation = MODULE.build_preparation(
                        scenario["root"],
                        "Besitzer",
                        scenario["audit_path"],
                    )
                    target = receipt_path.parent / "registry_history"
                    target.mkdir(parents=True)
                else:
                    target = scenario["registry_path"]
                original_check = MODULE._is_reparse_or_symlink

                def fake_reparse(path: Path) -> bool:
                    if Path(path) == target:
                        return True
                    return original_check(path)

                with mock.patch.object(
                    MODULE,
                    "_is_reparse_or_symlink",
                    side_effect=fake_reparse,
                ):
                    if target_kind == "archive":
                        with self.assertRaisesRegex(ValueError, "Link|Junction"):
                            MODULE.execute_preparation(
                                preparation,
                                "Besitzer",
                                datetime.now(timezone.utc),
                                renew_existing=True,
                            )
                    else:
                        with self.assertRaisesRegex(ValueError, "Link|Junction"):
                            MODULE.build_preparation(
                                scenario["root"],
                                "Besitzer",
                                scenario["audit_path"],
                            )

    @contextmanager
    def _scenario(
        self,
        *,
        include_legacy_negative: bool = False,
        negative_holdings: tuple[str, ...] = (
            "910001-910002",
            "910003-910004",
        ),
    ) -> Iterator[dict[str, Path]]:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self._write_eval_set(root, "eval_visible_clean_eval_set")
            self._write_eval_set(root, "eval_unclean_or_hidden_eval_set")
            (root / "eval_set" / "images").mkdir(parents=True)

            negative_set = create_reviewed_negative_set(
                root,
                holdings=negative_holdings,
            )
            negatives_dir = root / "training" / "negatives" / "strict_only_empty"
            negatives_dir.mkdir(parents=True)
            if include_legacy_negative:
                legacy = negatives_dir / "legacy.png"
                self._image(legacy, 177)
                legacy.write_bytes(legacy.read_bytes() + b"x" * 2048)

            samples = self._make_samples(root)
            samples_path = root / "training_samples.json"
            self._write_json(samples_path, samples)
            registry_path = root / "training" / "export_registry_v1.json"
            registry_path.parent.mkdir(parents=True, exist_ok=True)

            negative_images, negative_sets = (
                GOLD_AUDIT_MODULE.read_training_negative_sources(
                    root,
                    negatives_dir,
                    (negative_set,),
                    minimum_legacy_bytes=1024,
                )
            )
            registry = {
                "schema_version": "1.0",
                "approval_status": "approved",
                "approved_by": "Besitzer",
                "approved_utc": "2026-07-28T20:42:28Z",
                "approved_sample_ids": [],
                "holding_roles": {},
                "protected_sets": self._protected_sets(root),
                "negative_images": list(negative_images),
            }
            self._write_json(registry_path, registry)

            audit_path = self._write_audit(
                root,
                samples,
                registry_path,
                negatives_dir,
                (negative_set,),
                negative_images,
                negative_sets,
            )
            migration_path = root / "detect_class_migration_v3.candidate.json"
            self._write_migration(
                migration_path,
                audit_path,
                samples_path,
            )

            with mock.patch.object(
                MODULE,
                "ACTIVE_MIGRATION_PATH",
                migration_path,
            ):
                yield {
                    "root": root,
                    "samples_path": samples_path,
                    "registry_path": registry_path,
                    "migration_path": migration_path,
                    "audit_path": audit_path,
                    "negative_set": negative_set,
                    "negatives_dir": negatives_dir,
                }

    def _make_samples(self, root: Path) -> list[dict[str, object]]:
        definitions = (
            ("sample-bab-train", "BABBC", "train", "ManualCoding"),
            ("sample-bcc-val", "BCCAY", "val", "PdfPhoto"),
            ("sample-aec-discard", "AECXC", "train", "ManualCoding"),
            ("sample-bcc-test", "BCCAY", "test", "ManualCoding"),
        )
        result: list[dict[str, object]] = []
        for index, (sample_id, code, role, source_type) in enumerate(definitions):
            holding = self._holding_for_role(index, role)
            frame = root / "gold_frames" / code[:3] / f"{sample_id}.png"
            frame.parent.mkdir(parents=True, exist_ok=True)
            self._image(frame, 30 + index)
            sample: dict[str, object] = {
                "SampleId": sample_id,
                "CaseId": holding,
                "Code": code,
                "Beschreibung": f"Gold {code}",
                "FramePath": str(frame),
                "Status": 1,
                "SourceType": source_type,
                "HumanConfirmed": True,
                "Corrected": False,
                "ConfirmedByUser": "Besitzer",
                "ConfirmedAtUtc": "2026-07-30T12:00:00Z",
                "MatchLevel": "ReviewApproved",
                "HasBbox": True,
                "BboxXCenter": 0.5,
                "BboxYCenter": 0.5,
                "BboxWidth": 0.5,
                "BboxHeight": 0.5,
                "HasSamMask": True,
                "SamMaskRle": VALID_RLE,
                "SamMaskImageWidth": IMAGE_SIZE[0],
                "SamMaskImageHeight": IMAGE_SIZE[1],
                "SamMaskAreaPixels": 5,
                "_AuditRole": role,
            }
            if source_type == "PdfPhoto":
                sample.update(
                    {
                        "Notes": (
                            "PDF-Operateurreferenz: haltung.pdf; SHA-256="
                            + "a" * 64
                            + "; Seite=1; Foto=12; Zuordnung=photo_id"
                        ),
                        "SourceReferenceCode": code,
                        "SourceReferenceDescription": f"Operateur {code}",
                    }
                )
            result.append(sample)
        return result

    def _write_audit(
        self,
        root: Path,
        samples: list[dict[str, object]],
        registry_path: Path,
        negatives_dir: Path,
        negative_set_paths: tuple[Path, ...],
        negative_images: tuple[dict[str, object], ...],
        negative_sets: tuple[dict[str, object], ...],
    ) -> Path:
        samples_path = root / "training_samples.json"
        entries: list[dict[str, object]] = []
        split_counts = {"train": 0, "val": 0, "test": 0}
        for sample in samples:
            role = str(sample["_AuditRole"])
            split_counts[role] += 1
            frame = Path(str(sample["FramePath"]))
            entries.append(
                {
                    "sample_id": sample["SampleId"],
                    "case_id": sample["CaseId"],
                    "haltung_key": sample["CaseId"],
                    "code": sample["Code"],
                    "hauptcode": str(sample["Code"])[:3],
                    "image_sha256": self._sha256(frame),
                    "kb_text_offen": False,
                    "rolle": role,
                    "gruppe": f"haltung:{sample['CaseId']}",
                }
            )
        audit = {
            "schema_version": "1.1",
            "bericht": "gold_stock_audit",
            "modus": "schreibfreie_pruefung",
            "zeitstempel_utc": "2026-07-30T12:05:00Z",
            "eingaben": {
                "samples_pfad": str(samples_path),
                "samples_sha256": self._sha256(samples_path),
                "registry_pfad": str(registry_path),
                "registry_sha256": self._sha256(registry_path),
                "approved_by": "Besitzer",
                "approved_by_quelle": "registry",
                "vsa_manifest_pfad": str(MODULE.ACTIVE_VSA_MANIFEST_PATH),
                "vsa_manifest_sha256": self._sha256(
                    MODULE.ACTIVE_VSA_MANIFEST_PATH
                ),
                "eval_images_pfad": str(root / "eval_set" / "images"),
                "eval_hashes_anzahl": 0,
                "eval_haltungen_anzahl": 0,
                "negatives_pfad": str(negatives_dir),
                "negative_set_pfade": [
                    str(path.resolve()) for path in negative_set_paths
                ],
            },
            "einlesen": {
                "datei_gesamt": len(samples),
                "uebersprungen_entwurf": 0,
                "uebersprungen_status_sonstige": 0,
                "uebersprungen_quelle_sonstige": 0,
                "eingelesen": len(samples),
            },
            "pruefstufen": {
                "eingelesen": len(samples),
                "persoenlich": len(samples),
                "bild_ok": len(samples),
                "box_ok": len(samples),
                "maske_ok": len(samples),
                "code_ok": len(samples),
                "eval_sauber": len(samples),
                "final_verwendbar": len(samples),
            },
            "verwerfungen": [],
            "unbekannte_codes": [],
            "duplikat_gruppen": [],
            "duplikat_gruppen_anzahl": 0,
            "eval_treffer_ausgeschlossen": 0,
            "eval_haltungen": [],
            "kb_text_offen": 0,
            "split": {
                "regel": "fixture",
                "test_eingefroren_nur_markiert": True,
                "release_faehig": True,
                "fehlende_haltungsidentitaet": 0,
                "gruppen": [],
                "bilder": split_counts,
            },
            "piloten_schwelle": 30,
            "piloten": [],
            "piloten_nicht_auswertbar": [],
            "hauptcode_verteilung": {},
            "negativ_pool": {
                "pfad": str(negatives_dir),
                "anzahl": len(negative_images),
                "dateien": [
                    {"datei": image["path"]}
                    | {key: value for key, value in image.items() if key != "path"}
                    for image in negative_images
                ],
                "sets": list(negative_sets),
                "registry_modus": (
                    "diagnose_gemischt_nicht_exportierbar"
                    if any(image.get("source_type") is None for image in negative_images)
                    else "streng_reviewte_saetze"
                ),
            },
            "samples": entries,
        }
        reports = root / "training" / "reports"
        reports.mkdir(parents=True, exist_ok=True)
        path = reports / "gold_stock_audit_fixture.json"
        self._write_json(path, audit)
        return path

    def _write_migration(
        self,
        path: Path,
        audit_path: Path,
        samples_path: Path,
    ) -> None:
        entries = [
            self._migration_entry("BABBC", "map", "BAB_riss"),
            self._migration_entry("BCCAY", "map", "BCC_bogen"),
            self._migration_entry("AECXC", "discard", None),
        ]
        class_map = self._read_json(MODULE.ACTIVE_CLASS_MAP_PATH)
        document = {
            "version": 3,
            "target_class_map_version": 3,
            "target_class_map": "detect_class_map_v3.json",
            "generated_utc": "2026-07-30T11:00:00Z",
            "vsa_manifest_hash": class_map["vsa_manifest_hash"],
            "source_hashes": {},
            "sort_order": ["source_kind", "source_key", "source_id"],
            "resolution_order": [
                "annotation_override",
                "teacher_vsa_code",
                "legacy_class_map",
                "productive_yolo_name",
            ],
            "entry_counts": {
                "total": len(entries),
                "by_source_kind": {"teacher_vsa_code": len(entries)},
                "teacher_observed_total": len(entries),
            },
            "entries": entries,
            "personal_gold_approval": {
                "schema_version": "1.0",
                "gold_audit_sha256": self._sha256(audit_path),
                "training_samples_sha256": self._sha256(samples_path),
                "approved_by": "Besitzer",
                "approved_utc": "2026-07-30T11:45:00Z",
                "source_codes": sorted(
                    {
                        entry["code"]
                        for entry in self._read_json(audit_path)["samples"]
                    }
                ),
            },
        }
        self._write_json(path, document)

    @staticmethod
    def _migration_entry(
        code: str,
        action: str,
        target: str | None,
    ) -> dict[str, object]:
        return {
            "source_kind": "teacher_vsa_code",
            "source_key": code,
            "observed_count": 1,
            "proposed_action": action,
            "proposed_target": target,
            "reason": "Testfreigabe",
            "approval_status": "approved",
            "approved_by": "Besitzer",
            "approved_utc": "2026-07-30T11:30:00Z",
        }

    def _refresh_audit_samples_hash(self, scenario: dict[str, Path]) -> None:
        audit = self._read_json(scenario["audit_path"])
        audit["eingaben"]["samples_sha256"] = self._sha256(
            scenario["samples_path"]
        )
        self._write_json(scenario["audit_path"], audit)
        self._refresh_personal_gold_approval(scenario)

    def _refresh_personal_gold_approval(
        self,
        scenario: dict[str, Path],
    ) -> None:
        migration = self._read_json(scenario["migration_path"])
        approval = migration["personal_gold_approval"]
        audit = self._read_json(scenario["audit_path"])
        approval["gold_audit_sha256"] = self._sha256(scenario["audit_path"])
        approval["training_samples_sha256"] = self._sha256(
            scenario["samples_path"]
        )
        approval["source_codes"] = sorted(
            {entry["code"] for entry in audit["samples"]}
        )
        self._write_json(scenario["migration_path"], migration)

    @staticmethod
    def _protected_sets(root: Path) -> list[dict[str, str]]:
        result: list[dict[str, str]] = []
        for manifest in sorted(
            (root / "eval_set" / "subsets").glob("*/_manifest.json")
        ):
            set_root = manifest.parent
            result.append(
                {
                    "set_id": (
                        f"dev-val-{set_root.name.casefold().replace('_', '-')}-v1"
                    ),
                    "role": "development_validation",
                    "root_path": str(set_root.relative_to(root)),
                    "manifest_sha256": hashlib.sha256(
                        manifest.read_bytes()
                    ).hexdigest(),
                }
            )
        return result

    @staticmethod
    def _write_eval_set(root: Path, name: str) -> None:
        path = root / "eval_set" / "subsets" / name / "_manifest.json"
        path.parent.mkdir(parents=True)
        path.write_text('{"frozen":true}\n', encoding="utf-8")

    @staticmethod
    def _holding_for_role(index: int, role: str) -> str:
        for attempt in range(10000):
            key = f"{300000 + index}-{400000 + index + attempt}"
            if MODULE._expected_split_role(f"haltung:{key}") == role:
                return key
        raise AssertionError(f"Keine Testhaltung fuer Rolle {role} gefunden.")

    @staticmethod
    def _image(path: Path, color: int) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        Image.new("RGB", IMAGE_SIZE, (color, color, color)).save(path)

    @staticmethod
    def _write_json(path: Path, value: object) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps(value, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    @staticmethod
    def _read_json(path: Path) -> object:
        return json.loads(path.read_text(encoding="utf-8-sig"))

    @staticmethod
    def _sha256(path: Path) -> str:
        return hashlib.sha256(path.read_bytes()).hexdigest()


if __name__ == "__main__":
    unittest.main()
