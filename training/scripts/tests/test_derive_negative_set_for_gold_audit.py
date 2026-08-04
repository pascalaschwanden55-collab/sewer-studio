from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import tempfile
import unittest
from dataclasses import replace
from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import patch


SCRIPT_PATH = (
    Path(__file__).resolve().parents[1]
    / "derive_negative_set_for_gold_audit.py"
)
SPEC = importlib.util.spec_from_file_location(
    "derive_negative_set_for_gold_audit",
    SCRIPT_PATH,
)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class DerivedNegativeSetTests(unittest.TestCase):
    def _fixture(
        self,
        root: Path,
    ) -> tuple[Path, bytes, dict, list[dict], dict[str, str]]:
        source = root / "training" / "negatives" / "sets" / "bcc_hn_source"
        images_root = source / "images"
        receipts_root = source / "receipts"
        images_root.mkdir(parents=True)
        receipts_root.mkdir()

        records: list[dict] = []
        hashes: dict[str, str] = {}
        decisions: dict[str, dict[str, str]] = {}
        for index, holding in enumerate(("100-200", "300-400", "500-600")):
            data = b"\x89PNG\r\n\x1a\n" + bytes([20 + index]) * 2048
            image_sha = hashlib.sha256(data).hexdigest()
            file_name = f"img_{image_sha}.png"
            (images_root / file_name).write_bytes(data)
            left, right = holding.split("-")
            physical = "|".join(sorted((left, right)))
            review_item_id = f"bcc-hn-{image_sha[:16]}"
            hashes[holding] = image_sha
            records.append(
                {
                    "id": f"bcc-neg-{image_sha}",
                    "file_name": file_name,
                    "image_sha256": image_sha,
                    "size_bytes": len(data),
                    "image_format": "png",
                    "holding_key": holding,
                    "physical_holding_key": physical,
                    "split": "train",
                    "review_item_id": review_item_id,
                    "review_decision": "all_classes_clear",
                    "source_ref": hashlib.sha256(holding.encode()).hexdigest(),
                    "inspection_date": "2026-08-02",
                }
            )
            decisions[review_item_id] = {
                "decision": "all_classes_clear",
                "comment": "Vom Besitzer geprueft.",
                "reviewed_at_utc": "2026-08-02T12:00:00Z",
            }

        queue_manifest_bytes = b"queue"
        candidates_bytes = b"candidates"
        class_map_bytes = b"class-map"
        queue_manifest_sha = hashlib.sha256(queue_manifest_bytes).hexdigest()
        candidates_sha = hashlib.sha256(candidates_bytes).hexdigest()
        class_map_sha = hashlib.sha256(class_map_bytes).hexdigest()
        review = {
            "schema_version": "1.0",
            "purpose": "bcc_hard_negative_review",
            "queue_id": "1" * 64,
            "queue_manifest_sha256": queue_manifest_sha,
            "candidates_sha256": candidates_sha,
            "class_map_sha256": class_map_sha,
            "reviewer": "Besitzer",
            "updated_at_utc": "2026-08-02T12:00:00Z",
            "decisions": decisions,
        }
        review_bytes = MODULE.negative_tools._pretty_json_bytes(review)
        (receipts_root / "review.json").write_bytes(review_bytes)
        (receipts_root / "queue_manifest.json").write_bytes(queue_manifest_bytes)
        (receipts_root / "queue_candidates.json").write_bytes(candidates_bytes)
        (receipts_root / "class_map.json").write_bytes(class_map_bytes)

        semantic = {
            "schema_version": "1.0",
            "purpose": "bcc_reviewed_negative_set",
            "pilot": "BCC_bogen",
            "role": "training_negative_set",
            "queue": {
                "queue_id": "1" * 64,
                "queue_manifest_sha256": queue_manifest_sha,
                "queue_manifest_receipt_path": "receipts/queue_manifest.json",
                "candidates_sha256": candidates_sha,
                "candidates_receipt_path": "receipts/queue_candidates.json",
            },
            "review": {
                "purpose": "bcc_hard_negative_review",
                "review_sha256": hashlib.sha256(review_bytes).hexdigest(),
                "receipt_path": "receipts/review.json",
                "reviewed_images": len(decisions),
                "decision_counts": {
                    "all_classes_clear": len(decisions),
                    "mapped_object_visible": 0,
                    "exclude_uncertain": 0,
                },
            },
            "class_map_version": 3,
            "class_map_sha256": class_map_sha,
            "class_map_receipt_path": "receipts/class_map.json",
            "vsa_manifest_hash": "5" * 64,
            "class_names": [f"class-{index}" for index in range(15)],
            "protected_sets": [],
            "protection_snapshot": {},
            "split_rule": {
                "name": "stable_rank_v1",
                "salt": "bcc-hard-negative-split-v1",
                "one_image_per_physical_holding": True,
                "validation_count": 1,
                "train_count": 2,
            },
            "images": records,
        }
        manifest = {"semantic": semantic}
        manifest_bytes = MODULE.negative_tools._pretty_json_bytes(manifest)
        (source / "_manifest.json").write_bytes(manifest_bytes)
        negative_images = [
            {
                "path": f"training/negatives/sets/bcc_hn_source/images/{item['file_name']}",
                "sha256": item["image_sha256"],
                "split": item["split"],
                "source_type": "reviewed_negative_set",
                "holding_key": item["holding_key"],
                "physical_holding_key": item["physical_holding_key"],
                "review_item_id": item["review_item_id"],
            }
            for item in records
        ]
        return source, manifest_bytes, manifest, negative_images, hashes

    @staticmethod
    def _audit(
        root: Path,
        *,
        roles: dict[str, str],
        image_hashes: frozenset[str] = frozenset(),
    ) -> MODULE.AuditBinding:
        audit_path = root / "training" / "reports" / "audit.json"
        audit_path.parent.mkdir(parents=True, exist_ok=True)
        audit_path.write_text("{}", encoding="utf-8")
        return MODULE.AuditBinding(
            path=audit_path,
            sha256="a" * 64,
            created_utc=datetime(2026, 8, 2, 13, 0, tzinfo=timezone.utc),
            samples_sha256="b" * 64,
            registry_sha256="c" * 64,
            image_hashes=image_hashes,
            physical_roles=tuple(sorted(roles.items())),
        )

    def test_testhaltung_wird_nur_im_neuen_review_ausgeschlossen(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source, manifest_bytes, manifest, negatives, _hashes = self._fixture(root)
            audit = self._audit(root, roles={"100|200": "test"})
            original_manifest = (source / "_manifest.json").read_bytes()
            original_review = (source / "receipts" / "review.json").read_bytes()

            plan = MODULE.build_plan_from_validated_inputs(
                root,
                source,
                manifest_bytes,
                manifest,
                negatives,
                audit,
                "Besitzer",
                datetime(2026, 8, 2, 14, 0, tzinfo=timezone.utc),
            )

            self.assertEqual(2, len(plan.items))
            self.assertEqual(MODULE.TEST_HOLDING_REASON, plan.excluded[0].reason)
            excluded_decision = plan.review_document["decisions"][
                plan.excluded[0].review_item_id
            ]
            self.assertEqual("exclude_uncertain", excluded_decision["decision"])
            self.assertIn(audit.sha256, excluded_decision["comment"])
            self.assertIn(plan.source_manifest_sha256, excluded_decision["comment"])
            self.assertEqual(
                "Automatischer Testsatzschutz (freigegeben: Besitzer)",
                plan.review_document["reviewer"],
            )
            self.assertEqual(
                "2026-08-02T14:00:00Z",
                plan.review_document["updated_at_utc"],
            )

            with patch.object(MODULE, "_assert_inputs_unchanged"):
                target = MODULE.publish(plan)

            self.assertTrue((target / "_manifest.json").is_file())
            self.assertEqual(2, len(list((target / "images").iterdir())))
            self.assertEqual(
                {"review.json", "queue_manifest.json", "queue_candidates.json", "class_map.json"},
                {path.name for path in (target / "receipts").iterdir()},
            )
            self.assertEqual(original_manifest, (source / "_manifest.json").read_bytes())
            self.assertEqual(original_review, (source / "receipts" / "review.json").read_bytes())

    def test_bytegleiche_gold_negative_kollision_bleibt_harter_fehler(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source, manifest_bytes, manifest, negatives, hashes = self._fixture(root)
            audit = self._audit(
                root,
                roles={"100|200": "test"},
                image_hashes=frozenset({hashes["300-400"]}),
            )

            with self.assertRaisesRegex(ValueError, "bytegleich"):
                MODULE.build_plan_from_validated_inputs(
                    root,
                    source,
                    manifest_bytes,
                    manifest,
                    negatives,
                    audit,
                    "Besitzer",
                    datetime(2026, 8, 2, 14, 0, tzinfo=timezone.utc),
                )

    def test_split_rollenkonflikte_werden_bis_zum_stabilen_satz_entfernt(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source, manifest_bytes, manifest, negatives, _hashes = self._fixture(root)
            initial_splits, _ = MODULE.negative_tools._negative_split_map(
                [str(item["physical_holding_key"]) for item in negatives]
            )
            conflicting_physical = next(
                physical
                for physical, split in initial_splits.items()
                if split == "validation"
            )
            audit = self._audit(root, roles={conflicting_physical: "train"})

            plan = MODULE.build_plan_from_validated_inputs(
                root,
                source,
                manifest_bytes,
                manifest,
                negatives,
                audit,
                "Besitzer",
                datetime(2026, 8, 2, 14, 0, tzinfo=timezone.utc),
            )

            self.assertTrue(
                any(item.reason == MODULE.ROLE_CONFLICT_REASON for item in plan.excluded)
            )
            remaining = {
                str(item.semantic["physical_holding_key"]): str(item.semantic["split"])
                for item in plan.items
            }
            self.assertNotIn(conflicting_physical, remaining)

    def test_utc_parser_akzeptiert_z_und_plus_null_offset(self) -> None:
        expected = datetime(2026, 8, 2, 13, 0, tzinfo=timezone.utc)

        self.assertEqual(expected, MODULE._parse_utc_timestamp("2026-08-02T13:00:00Z"))
        self.assertEqual(
            expected,
            MODULE._parse_utc_timestamp("2026-08-02T13:00:00+00:00"),
        )
        with self.assertRaisesRegex(ValueError, "UTC"):
            MODULE._parse_utc_timestamp("2026-08-02T15:00:00+02:00")

    def test_sicherer_eingabepfad_sperrt_ausserhalb_und_linkkomponente(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            reports = root / "training" / "reports"
            reports.mkdir(parents=True)
            outside = root / "outside.json"
            outside.write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "sicheren|innerhalb"):
                MODULE._require_plain_input_file(outside, reports, "Gold-Audit")

            nested = reports / "linked"
            nested.mkdir()
            audit = nested / "audit.json"
            audit.write_text("{}", encoding="utf-8")
            original = MODULE.detect_tools._is_reparse_or_symlink

            def fake_reparse(path: Path) -> bool:
                return Path(path) == nested or original(path)

            with patch.object(
                MODULE.detect_tools,
                "_is_reparse_or_symlink",
                side_effect=fake_reparse,
            ):
                with self.assertRaisesRegex(ValueError, "Link oder Junction"):
                    MODULE._require_plain_input_file(audit, reports, "Gold-Audit")

    def test_reviewzeit_und_satz_id_stammen_vom_echten_laufzeitpunkt(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source, manifest_bytes, manifest, negatives, _hashes = self._fixture(root)
            audit = self._audit(root, roles={"100|200": "test"})
            first_time = datetime(2026, 8, 2, 14, 0, tzinfo=timezone.utc)
            second_time = datetime(2026, 8, 2, 14, 1, tzinfo=timezone.utc)

            first = MODULE.build_plan_from_validated_inputs(
                root,
                source,
                manifest_bytes,
                manifest,
                negatives,
                audit,
                "Besitzer",
                first_time,
            )
            second = MODULE.build_plan_from_validated_inputs(
                root,
                source,
                manifest_bytes,
                manifest,
                negatives,
                audit,
                "Besitzer",
                second_time,
            )

            self.assertEqual("2026-08-02T14:00:00Z", first.review_document["updated_at_utc"])
            self.assertEqual("2026-08-02T14:01:00Z", second.review_document["updated_at_utc"])
            self.assertNotEqual(first.set_id, second.set_id)

    def test_geaenderter_input_stoppt_publish_vor_dem_schreiben(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source, manifest_bytes, manifest, negatives, _hashes = self._fixture(root)
            plan = MODULE.build_plan_from_validated_inputs(
                root,
                source,
                manifest_bytes,
                manifest,
                negatives,
                self._audit(root, roles={"100|200": "test"}),
                "Besitzer",
                datetime(2026, 8, 2, 14, 0, tzinfo=timezone.utc),
            )
            changed = replace(plan, source_manifest_sha256="f" * 64)
            with patch.object(MODULE, "build_plan", return_value=changed) as rebuild:
                with self.assertRaisesRegex(ValueError, "veraendert"):
                    MODULE._assert_inputs_unchanged(plan)
            rebuild.assert_called_once_with(
                plan.knowledge_root,
                plan.source_set_root,
                plan.gold_audit.path,
                plan.approved_by,
                created_utc=plan.created_utc,
            )
            self.assertFalse(plan.target_root.exists())

    def test_vorhandenes_ziel_wird_nie_ueberschrieben(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source, manifest_bytes, manifest, negatives, _hashes = self._fixture(root)
            plan = MODULE.build_plan_from_validated_inputs(
                root,
                source,
                manifest_bytes,
                manifest,
                negatives,
                self._audit(root, roles={"100|200": "test"}),
                "Besitzer",
                datetime(2026, 8, 2, 14, 0, tzinfo=timezone.utc),
            )
            plan.target_root.mkdir()
            marker = plan.target_root / "bestehend.txt"
            marker.write_text("bleibt", encoding="utf-8")

            with self.assertRaisesRegex(FileExistsError, "nie ueberschrieben"):
                MODULE.publish(plan)

            self.assertEqual("bleibt", marker.read_text(encoding="utf-8"))

    def test_fehler_entfernt_nur_den_eigenen_staging_ordner(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source, manifest_bytes, manifest, negatives, _hashes = self._fixture(root)
            plan = MODULE.build_plan_from_validated_inputs(
                root,
                source,
                manifest_bytes,
                manifest,
                negatives,
                self._audit(root, roles={"100|200": "test"}),
                "Besitzer",
                datetime(2026, 8, 2, 14, 0, tzinfo=timezone.utc),
            )
            sets_root = root / "training" / "negatives" / "sets"
            foreign = sets_root / ".fremdes-staging"
            foreign.mkdir()

            with (
                patch.object(MODULE, "_assert_inputs_unchanged"),
                patch.object(
                    MODULE.negative_tools.holdout_tools,
                    "_copy_verified",
                    side_effect=OSError("Testfehler"),
                ),
            ):
                with self.assertRaisesRegex(OSError, "Testfehler"):
                    MODULE.publish(plan)

            self.assertFalse(plan.target_root.exists())
            self.assertTrue(foreign.is_dir())
            self.assertEqual(
                [],
                list(sets_root.glob(".bcc-hn-audit-guard-staging-*")),
            )

    def test_veroeffentlichter_satz_besteht_den_echten_gold_reader(self) -> None:
        helper_path = Path(__file__).with_name("test_gold_stock_audit.py")
        helper_spec = importlib.util.spec_from_file_location(
            "derive_negative_gold_fixture",
            helper_path,
        )
        assert helper_spec is not None and helper_spec.loader is not None
        helper = importlib.util.module_from_spec(helper_spec)
        original_gold_module = sys.modules.get("gold_stock_audit")
        try:
            helper_spec.loader.exec_module(helper)
        finally:
            if original_gold_module is not None:
                sys.modules["gold_stock_audit"] = original_gold_module

        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = helper.create_reviewed_negative_set(
                root,
                holdings=("100-200", "300-400", "500-600"),
            )
            negative_images, _ = MODULE.gold_audit_tools._read_reviewed_negative_set(
                root,
                source,
            )
            manifest_bytes = (source / "_manifest.json").read_bytes()
            manifest = MODULE.gold_audit_tools._strict_json_bytes(
                manifest_bytes,
                "Testmanifest",
            )
            plan = MODULE.build_plan_from_validated_inputs(
                root,
                source,
                manifest_bytes,
                manifest,
                negative_images,
                self._audit(root, roles={"100|200": "test"}),
                "Besitzer",
                datetime(2026, 8, 2, 14, 0, tzinfo=timezone.utc),
            )
            with patch.object(MODULE, "_assert_inputs_unchanged"):
                target = MODULE.publish(plan)

            verified, provenance = MODULE.gold_audit_tools._read_reviewed_negative_set(
                root,
                target,
            )
            self.assertEqual(2, len(verified))
            self.assertEqual(plan.set_id, provenance["set_id"])


if __name__ == "__main__":
    unittest.main()
