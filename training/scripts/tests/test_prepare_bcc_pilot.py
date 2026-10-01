from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from unittest import mock

from PIL import Image

GOLD_AUDIT_HELPER_PATH = Path(__file__).with_name("test_gold_stock_audit.py")
GOLD_AUDIT_HELPER_SPEC = importlib.util.spec_from_file_location(
    "prepare_bcc_pilot_test_fixture",
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


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "prepare_bcc_pilot.py"
SPEC = importlib.util.spec_from_file_location("prepare_bcc_pilot", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

IMAGE_SIZE = (8, 4)
VALID_RLE = "0,10,5,17"


class PrepareBccPilotTests(unittest.TestCase):
    def test_audit_uebernimmt_nur_bcc_train_und_val(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=3)
            samples.extend(
                self._make_samples(
                    root,
                    train=1,
                    val=0,
                    test=0,
                    code="BCAAA",
                    start_index=100,
                )
            )
            audit_path = self._write_audit(root, samples)

            preparation = MODULE.build_preparation(
                root,
                "Besitzer",
                audit_path,
            )

            self.assertEqual(30, len(preparation.selected_samples))
            self.assertEqual(24, preparation.train_images)
            self.assertEqual(6, preparation.validation_images)
            self.assertEqual(3, len(preparation.excluded_test_sample_ids))
            self.assertTrue(
                all(sample.code.startswith("BCC") for sample in preparation.selected_samples)
            )
            self.assertTrue(
                all(sample.role in {"train", "val"} for sample in preparation.selected_samples)
            )

    def test_veralteter_samples_hash_stoppt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            audit_path = self._write_audit(root, samples)
            document = json.loads(audit_path.read_text(encoding="utf-8"))
            document["eingaben"]["samples_sha256"] = "0" * 64
            audit_path.write_text(json.dumps(document), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "training_samples.json"):
                MODULE.build_preparation(root, "Besitzer", audit_path)

    def test_veralteter_registry_hash_stoppt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            registry = root / "training" / "export_registry_v1.json"
            registry.parent.mkdir(parents=True)
            registry.write_text('{"alt":true}\n', encoding="utf-8")
            audit_path = self._write_audit(root, samples, registry_sha256="f" * 64)

            with self.assertRaisesRegex(ValueError, "Exportregister"):
                MODULE.build_preparation(root, "Besitzer", audit_path)

    def test_testbilder_zaehlen_nicht_zur_mindestmenge(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=5, test=10)
            audit_path = self._write_audit(root, samples)

            with self.assertRaisesRegex(ValueError, "mindestens 30"):
                MODULE.build_preparation(root, "Besitzer", audit_path)

    def test_fehlende_echte_haltung_stoppt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            samples[0]["CaseId"] = "foto_20260726_2"
            audit_path = self._write_audit(root, samples)
            audit = json.loads(audit_path.read_text(encoding="utf-8"))
            audit["samples"][0]["case_id"] = "foto_20260726_2"
            audit["samples"][0]["haltung_key"] = None
            audit["split"]["release_faehig"] = False
            audit["eingaben"]["samples_sha256"] = self._sha256(
                root / "training_samples.json"
            )
            audit_path.write_text(json.dumps(audit), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "Haltungsidentitaet"):
                MODULE.build_preparation(root, "Besitzer", audit_path)

    def test_bild_hashabweichung_stoppt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            audit_path = self._write_audit(root, samples)
            audit = json.loads(audit_path.read_text(encoding="utf-8"))
            audit["samples"][0]["image_sha256"] = "a" * 64
            audit_path.write_text(json.dumps(audit), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "Bild-Hash"):
                MODULE.build_preparation(root, "Besitzer", audit_path)

    def test_manipulierte_testrolle_stoppt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=3)
            audit_path = self._write_audit(root, samples)
            audit = json.loads(audit_path.read_text(encoding="utf-8"))
            test_entry = next(
                entry for entry in audit["samples"] if entry["rolle"] == "test"
            )
            test_entry["rolle"] = "train"
            audit_path.write_text(json.dumps(audit), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "deterministische Audit-Rolle"):
                MODULE.build_preparation(root, "Besitzer", audit_path)

    def test_geaenderter_audit_stoppt_vor_dem_schreiben(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            audit_path = self._write_audit(root, samples)
            preparation = MODULE.build_preparation(root, "Besitzer", audit_path)
            audit_path.write_bytes(audit_path.read_bytes() + b"\n")

            with self.assertRaisesRegex(ValueError, "Gold-Audit"):
                MODULE.execute_preparation(
                    preparation,
                    "Besitzer",
                    datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
                )
            self.assertFalse(preparation.registry_path.exists())

    def test_renew_archiviert_alte_registry_und_schreibt_neue(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=3)
            registry = root / "training" / "export_registry_v1.json"
            registry.parent.mkdir(parents=True)
            old_bytes = b'{"schema_version":"1.0","approval_status":"approved","alt":true}\n'
            registry.write_bytes(old_bytes)
            old_audit = root / "training" / "pilots" / "BCC" / "pilot_setup_v1.json"
            old_audit.parent.mkdir(parents=True)
            old_audit.write_text('{"alt":true}\n', encoding="utf-8")
            audit_path = self._write_audit(
                root,
                samples,
                registry_sha256=hashlib.sha256(old_bytes).hexdigest(),
            )
            preparation = MODULE.build_preparation(root, "Besitzer", audit_path)

            MODULE.execute_preparation(
                preparation,
                "Besitzer",
                datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
                renew_existing=True,
            )

            registry_document = json.loads(registry.read_text(encoding="utf-8"))
            self.assertEqual("approved", registry_document["approval_status"])
            self.assertEqual(30, len(registry_document["approved_sample_ids"]))
            self.assertNotIn("previous_registry_sha256", registry_document)
            self.assertEqual(
                {
                    "schema_version",
                    "approval_status",
                    "approved_by",
                    "approved_utc",
                    "approved_sample_ids",
                    "holding_roles",
                    "protected_sets",
                },
                set(registry_document),
            )
            history = (
                root
                / "training"
                / "pilots"
                / "BCC"
                / "registry_history"
                / f"{hashlib.sha256(old_bytes).hexdigest()}.json"
            )
            self.assertEqual(old_bytes, history.read_bytes())
            pilot_audit = json.loads(old_audit.read_text(encoding="utf-8"))
            self.assertEqual(
                hashlib.sha256(old_bytes).hexdigest(),
                pilot_audit["previous_registry_sha256"],
            )
            self.assertEqual(3, len(pilot_audit["test_sample_ids_excluded"]))

    def test_renew_erkennt_registry_aenderung_nach_der_pruefung(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            registry = root / "training" / "export_registry_v1.json"
            registry.parent.mkdir(parents=True)
            old_bytes = b'{"alt":true}\n'
            registry.write_bytes(old_bytes)
            audit_path = self._write_audit(
                root,
                samples,
                registry_sha256=hashlib.sha256(old_bytes).hexdigest(),
            )
            preparation = MODULE.build_preparation(root, "Besitzer", audit_path)
            registry.write_text('{"zwischenzeitlich":true}\n', encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "zwischenzeitlich"):
                MODULE.execute_preparation(
                    preparation,
                    "Besitzer",
                    datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
                    renew_existing=True,
                )

    def test_ohne_renew_wird_bestehende_registry_nicht_ueberschrieben(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            registry = root / "training" / "export_registry_v1.json"
            registry.parent.mkdir(parents=True)
            old_bytes = b'{"alt":true}\n'
            registry.write_bytes(old_bytes)
            audit_path = self._write_audit(
                root,
                samples,
                registry_sha256=hashlib.sha256(old_bytes).hexdigest(),
            )
            preparation = MODULE.build_preparation(root, "Besitzer", audit_path)

            with self.assertRaises(FileExistsError):
                MODULE.execute_preparation(
                    preparation,
                    "Besitzer",
                    datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
                )

            self.assertEqual(old_bytes, registry.read_bytes())

    def test_fehler_beim_finalen_wechsel_stellt_alten_stand_wieder_her(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            registry = root / "training" / "export_registry_v1.json"
            registry.parent.mkdir(parents=True)
            old_registry_bytes = b'{"alt":true}\n'
            registry.write_bytes(old_registry_bytes)
            pilot_audit = (
                root / "training" / "pilots" / "BCC" / "pilot_setup_v1.json"
            )
            pilot_audit.parent.mkdir(parents=True)
            old_audit_bytes = b'{"alter_beleg":true}\n'
            pilot_audit.write_bytes(old_audit_bytes)
            audit_path = self._write_audit(
                root,
                samples,
                registry_sha256=hashlib.sha256(old_registry_bytes).hexdigest(),
            )
            preparation = MODULE.build_preparation(root, "Besitzer", audit_path)
            real_replace = MODULE.os.replace
            failed_registry_switch = False

            def fail_once_at_registry(source: object, destination: object) -> None:
                nonlocal failed_registry_switch
                if (
                    Path(destination) == registry
                    and not failed_registry_switch
                ):
                    failed_registry_switch = True
                    raise OSError("simulierter Registry-Wechselfehler")
                real_replace(source, destination)

            with mock.patch.object(
                MODULE.os,
                "replace",
                side_effect=fail_once_at_registry,
            ):
                with self.assertRaisesRegex(OSError, "simulierter"):
                    MODULE.execute_preparation(
                        preparation,
                        "Besitzer",
                        datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
                        renew_existing=True,
                    )

            self.assertEqual(old_registry_bytes, registry.read_bytes())
            self.assertEqual(old_audit_bytes, pilot_audit.read_bytes())

    def test_negatives_dir_feeds_registry_and_audit_when_present(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            negatives = root / "training" / "negatives" / "bcc_pilot"
            negatives.mkdir(parents=True)
            first = negatives / "normal_01.png"
            self._image(first, 201)
            first.write_bytes(first.read_bytes() + b"x" * 2048)
            second = negatives / "normal_02.jpg"
            self._image(second, 202)
            second.write_bytes(second.read_bytes() + b"y" * 2048)
            audit_path = self._write_audit(root, samples, negatives=negatives)

            preparation = MODULE.build_preparation(
                root,
                "Besitzer",
                audit_path,
                negatives,
            )

            self.assertEqual(2, len(preparation.negative_images))
            MODULE.execute_preparation(
                preparation,
                "Besitzer",
                datetime(2026, 7, 28, 12, 0, tzinfo=timezone.utc),
            )
            registry = json.loads(
                preparation.registry_path.read_text(encoding="utf-8")
            )
            self.assertEqual(2, len(registry["negative_images"]))

    def test_negativsatz_provenienz_wird_in_registry_und_beleg_geschrieben(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            negative_set = create_reviewed_negative_set(root)
            audit_path = self._write_audit(
                root,
                samples,
                negative_sets=(negative_set,),
            )

            preparation = MODULE.build_preparation(
                root,
                "Besitzer",
                audit_path,
                negative_sets=(negative_set,),
            )

            self.assertEqual(2, len(preparation.negative_images))
            self.assertEqual(1, len(preparation.negative_sets))
            self.assertTrue(
                all(
                    image["source_type"] == "reviewed_negative_set"
                    for image in preparation.negative_images
                )
            )
            MODULE.execute_preparation(
                preparation,
                "Besitzer",
                datetime(2026, 7, 28, 14, 0, tzinfo=timezone.utc),
            )
            registry = json.loads(
                preparation.registry_path.read_text(encoding="utf-8")
            )
            strict_image = registry["negative_images"][0]
            self.assertIn("set_manifest_sha256", strict_image)
            self.assertIn("review_sha256", strict_image)
            self.assertIn("queue_manifest_sha256", strict_image)
            self.assertIn("class_map_sha256", strict_image)
            pilot_audit = json.loads(
                preparation.audit_path.read_text(encoding="utf-8")
            )
            self.assertEqual(
                preparation.negative_sets[0]["manifest_sha256"],
                pilot_audit["negative_sets"][0]["manifest_sha256"],
            )

    def test_legacy_und_strikte_negativsaetze_werden_nicht_gemischt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            negatives = root / "training" / "negatives" / "bcc_pilot"
            negatives.mkdir(parents=True)
            legacy = negatives / "legacy.png"
            self._image(legacy, 210)
            legacy.write_bytes(legacy.read_bytes() + b"x" * 2048)
            negative_set = create_reviewed_negative_set(root)
            audit_path = self._write_audit(
                root,
                samples,
                negatives=negatives,
                negative_sets=(negative_set,),
            )

            with self.assertRaisesRegex(ValueError, "nicht mischen"):
                MODULE.build_preparation(
                    root,
                    "Besitzer",
                    audit_path,
                    negatives,
                    negative_sets=(negative_set,),
                )

    def test_audit_provenienz_darf_nicht_ausgetauscht_werden(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            negative_set = create_reviewed_negative_set(root)
            audit_path = self._write_audit(
                root,
                samples,
                negative_sets=(negative_set,),
            )
            audit = json.loads(audit_path.read_text(encoding="utf-8"))
            audit["negativ_pool"]["sets"][0]["review_sha256"] = "a" * 64
            audit_path.write_text(json.dumps(audit), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "Provenienz|Negativsatz"):
                MODULE.build_preparation(
                    root,
                    "Besitzer",
                    audit_path,
                    negative_sets=(negative_set,),
                )

    def test_negativsatz_wird_vor_finalem_schreiben_erneut_geprueft(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            samples = self._make_samples(root, train=24, val=6, test=0)
            negative_set = create_reviewed_negative_set(root)
            audit_path = self._write_audit(
                root,
                samples,
                negative_sets=(negative_set,),
            )
            preparation = MODULE.build_preparation(
                root,
                "Besitzer",
                audit_path,
                negative_sets=(negative_set,),
            )
            review = negative_set / "receipts" / "review.json"
            review.write_bytes(review.read_bytes() + b"\n")

            with self.assertRaisesRegex(ValueError, "Review|Hash|Negativsatz"):
                MODULE.execute_preparation(
                    preparation,
                    "Besitzer",
                    datetime(2026, 7, 28, 14, 0, tzinfo=timezone.utc),
                )
            self.assertFalse(preparation.registry_path.exists())

    def test_negative_set_cli_ist_wiederholbar(self) -> None:
        with mock.patch.object(
            sys,
            "argv",
            [
                "prepare_bcc_pilot.py",
                "--gold-audit",
                "audit.json",
                "--negative-set",
                "set-a",
                "--negative-set",
                "set-b",
            ],
        ):
            args = MODULE._parse_args()

        self.assertEqual([Path("set-a"), Path("set-b")], args.negative_set)

    def _make_samples(
        self,
        root: Path,
        train: int,
        val: int,
        test: int,
        code: str = "BCCAY",
        start_index: int = 0,
    ) -> list[dict[str, object]]:
        gold = root / "gold_frames" / "BCC - Bogen"
        gold.mkdir(parents=True, exist_ok=True)
        samples: list[dict[str, object]] = []
        roles = ["train"] * train + ["val"] * val + ["test"] * test
        for offset, role in enumerate(roles):
            index = start_index + offset
            frame = gold / f"gold_{index:03d}.png"
            self._image(frame, (index % 250) + 1)
            samples.append(self._sample(index, frame, code, role))
        (root / "training_samples.json").write_text(
            json.dumps(samples),
            encoding="utf-8",
        )
        self._eval_set(root, "eval_visible_clean_eval_set")
        self._eval_set(root, "eval_unclean_or_hidden_eval_set")
        (root / "eval_set" / "images").mkdir(parents=True, exist_ok=True)
        return samples

    def _write_audit(
        self,
        root: Path,
        samples: list[dict[str, object]],
        registry_sha256: str | None = None,
        negatives: Path | None = None,
        negative_sets: tuple[Path, ...] = (),
    ) -> Path:
        samples_path = root / "training_samples.json"
        samples_path.write_text(json.dumps(samples), encoding="utf-8")
        reports = root / "training" / "reports"
        reports.mkdir(parents=True, exist_ok=True)
        entries = []
        for sample in samples:
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
                    "rolle": sample["_AuditRole"],
                    "gruppe": f"haltung:{sample['CaseId']}",
                }
            )
        negative_files: list[dict[str, object]] = []
        negative_set_provenance: list[dict[str, object]] = []
        if negative_sets:
            negative_images, provenance = (
                GOLD_AUDIT_MODULE.read_training_negative_sources(
                    root,
                    (
                        negatives
                        if negatives is not None
                        else root / "training" / "negatives" / "bcc_pilot"
                    ),
                    negative_sets,
                )
            )
            negative_files = [
                {"datei": image["path"]}
                | {key: value for key, value in image.items() if key != "path"}
                for image in negative_images
            ]
            negative_set_provenance = [dict(item) for item in provenance]
        elif negatives is not None:
            for path in sorted(negatives.iterdir()):
                if path.is_file():
                    negative_files.append(
                        {"datei": path.name, "sha256": self._sha256(path)}
                    )
        document = {
            "schema_version": "1.1",
            "bericht": "gold_stock_audit",
            "modus": "schreibfreie_pruefung",
            "zeitstempel_utc": "2026-07-28T12:00:00Z",
            "eingaben": {
                "samples_pfad": str(samples_path),
                "samples_sha256": self._sha256(samples_path),
                "registry_pfad": str(
                    root / "training" / "export_registry_v1.json"
                ),
                "registry_sha256": registry_sha256,
                "approved_by": "Besitzer",
                "approved_by_quelle": "cli",
                "eval_images_pfad": str(root / "eval_set" / "images"),
                "eval_hashes_anzahl": 0,
                "eval_haltungen_anzahl": 0,
                "negatives_pfad": str(
                    negatives
                    if negatives is not None
                    else root / "training" / "negatives" / "bcc_pilot"
                ),
                "negative_set_pfade": [
                    str(path.resolve()) for path in negative_sets
                ],
            },
            "verwerfungen": [],
            "split": {
                "test_eingefroren_nur_markiert": True,
                "release_faehig": all(
                    str(sample["CaseId"]).count("-") == 1 for sample in samples
                ),
            },
            "negativ_pool": {
                "anzahl": len(negative_files),
                "dateien": negative_files,
                "sets": negative_set_provenance,
            },
            "samples": entries,
        }
        audit_path = reports / "gold_stock_audit_test.json"
        audit_path.write_text(json.dumps(document), encoding="utf-8")
        return audit_path

    @staticmethod
    def _image(path: Path, color: int) -> None:
        Image.new("RGB", IMAGE_SIZE, (color, color, color)).save(path)

    @staticmethod
    def _sample(
        index: int,
        frame: Path,
        code: str,
        role: str,
    ) -> dict[str, object]:
        case_id = PrepareBccPilotTests._holding_for_role(index, role)
        return {
            "SampleId": f"sample-{index:03d}",
            "CaseId": case_id,
            "Code": code,
            "Beschreibung": "Bogen bei 3 Uhr",
            "FramePath": str(frame),
            "Status": 1,
            "SourceType": "ManualCoding",
            "HumanConfirmed": True,
            "Corrected": False,
            "ConfirmedByUser": "Besitzer",
            "ConfirmedAtUtc": "2026-07-28T12:00:00Z",
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
            "_AuditRole": role,
        }

    @staticmethod
    def _holding_for_role(index: int, role: str) -> str:
        for attempt in range(10000):
            key = f"{100000 + index}-{200000 + index + attempt}"
            if MODULE._expected_split_role(f"haltung:{key}") == role:
                return key
        raise AssertionError(f"Keine Testhaltung fuer Rolle {role} gefunden.")

    @staticmethod
    def _eval_set(root: Path, name: str) -> None:
        set_root = root / "eval_set" / "subsets" / name
        set_root.mkdir(parents=True, exist_ok=True)
        (set_root / "_manifest.json").write_text(
            json.dumps({"frozen": True}),
            encoding="utf-8",
        )

    @staticmethod
    def _sha256(path: Path) -> str:
        return hashlib.sha256(path.read_bytes()).hexdigest()


if __name__ == "__main__":
    unittest.main()
