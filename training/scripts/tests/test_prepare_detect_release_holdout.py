from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from unittest import mock

from PIL import Image


SCRIPT_ROOT = Path(__file__).resolve().parents[1]
REPOSITORY_ROOT = SCRIPT_ROOT.parents[1]
for path in (SCRIPT_ROOT, REPOSITORY_ROOT):
    if str(path) not in sys.path:
        sys.path.insert(0, str(path))

import bcc_release_holdout as protection
import prepare_detect_release_holdout as target
from tools.EvalVisibilityReview.detect_release_holdout_review_server import (
    DetectReleaseHoldoutReviewStore,
)


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def snapshot(
    *,
    aliases: set[str] | None = None,
    candidates: tuple[dict[str, object], ...] = (),
) -> protection.ContaminationSnapshot:
    holding_aliases = frozenset(aliases or set())
    return protection.ContaminationSnapshot(
        image_hashes=frozenset(),
        holding_aliases=holding_aliases,
        candidates=candidates,
        evidence=(),
        base_model_sha256="1" * 64,
        candidate_scope_sha256="2" * 64,
        image_hashes_sha256="3" * 64,
        holding_aliases_sha256="4" * 64,
    )


class DetectReleaseHoldoutBuilderTests(unittest.TestCase):
    @staticmethod
    def _selection_image(
        root: Path,
        index: int,
        holding: str,
        source_kind: str,
        references: tuple[dict[str, object], ...] = (),
    ) -> target.SourceImage:
        left, right = holding.split("-", maxsplit=1)
        pdf = target.SourcePdf(
            root / f"{index}.pdf",
            f"{index + 1:064x}",
            f"{index}.pdf",
            holding,
            "|".join(sorted((left, right))),
        )
        return target.SourceImage(
            root / f"{index}.jpg",
            root,
            f"{index + 100:064x}",
            2048,
            64,
            48,
            pdf,
            source_kind,
            references,
        )

    def _write_extraction_receipt(
        self,
        root: Path,
        *,
        image_count: int = 1,
        content_addressed_name: bool = True,
    ) -> tuple[Path, dict[str, target.SourcePdf]]:
        images = root / "images"
        images.mkdir()
        temporary_image = images / "frame.jpg"
        Image.effect_noise((64, 48), 24).convert("RGB").save(
            temporary_image,
            format="JPEG",
            quality=90,
        )
        digest = sha(temporary_image)
        image_path = images / ((digest if content_addressed_name else "wrong") + ".jpg")
        temporary_image.rename(image_path)
        pdf = root / "20250101_111-222.pdf"
        pdf.write_bytes(b"pdf")
        pdf_sha = sha(pdf)
        receipt = {
            "schema_version": "1.0",
            "purpose": "detect_release_holdout_pdf_extraction",
            "model_predictions_used_for_selection": False,
            "training_allowed": False,
            "gold_allowed": False,
            "status": "completed",
            "image_count": image_count,
            "images": [
                {
                    "image_path": f"images/{image_path.name}",
                    "image_sha256": digest,
                    "size_bytes": image_path.stat().st_size,
                    "width": 64,
                    "height": 48,
                    "holding_key": "111-222",
                    "physical_holding_key": "111|222",
                    "source_kind": "operator_pdf_photo",
                    "source_pdf_sha256": pdf_sha,
                    "operator_references": [
                        {
                            "vsa_code": "BCC",
                            "detect_class_id": 2,
                            "detect_class_name": "BCC_bogen",
                            "finding_text": "Bogen",
                        }
                    ],
                }
            ],
        }
        receipt_path = root / "_pdf_extraction.json"
        receipt_path.write_text(json.dumps(receipt), encoding="utf-8")
        source = target.SourcePdf(pdf, pdf_sha, pdf.name, "111-222", "111|222")
        return receipt_path, {pdf_sha: source}

    def test_haltung_ist_kanonisch_und_widerspruch_wird_nicht_geraten(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            good = root / "06.24379-06.24377" / "20220522_06.24379-06.24377.pdf"
            good.parent.mkdir()
            good.write_bytes(b"pdf")
            bad = root / "111-222" / "20220522_333-444.pdf"
            bad.parent.mkdir()
            bad.write_bytes(b"pdf")
            self.assertEqual("24379-24377", target.resolve_pdf_holding(good, root))
            self.assertIsNone(target.resolve_pdf_holding(bad, root))

    def test_umgekehrte_bekannte_haltung_sperrt_den_ganzen_pdf_import(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            import_root = root / "imports"
            pdf_sha = "a" * 64
            image_root = import_root / pdf_sha
            image_root.mkdir(parents=True)
            image_path = image_root / ("0" * 64 + ".jpg")
            Image.new("RGB", (32, 24), "gray").save(image_path, format="JPEG")
            digest = sha(image_path)
            renamed = image_path.with_name(digest + ".jpg")
            image_path.rename(renamed)
            pdf = root / "20250101_111-222.pdf"
            pdf.write_bytes(b"pdf")
            source = target.SourcePdf(pdf, pdf_sha, pdf.name, "111-222", "111|222")
            known = snapshot(aliases=protection._holding_aliases("222-111"))
            images, blocked_holding, blocked_hash = target.discover_fresh_images(
                {pdf_sha: source}, import_root, known
            )
            self.assertEqual([], images)
            self.assertEqual(1, blocked_holding)
            self.assertEqual(0, blocked_hash)

    def test_gleicher_bildhash_aus_zwei_haltungen_wird_ganz_ausgeschlossen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            import_root = root / "imports"
            temporary_image = root / "frame.jpg"
            Image.effect_noise((128, 96), 24).convert("RGB").save(
                temporary_image,
                format="JPEG",
                quality=90,
            )
            image_bytes = temporary_image.read_bytes()
            digest = sha(temporary_image)
            sources: dict[str, target.SourcePdf] = {}
            for index, holding in enumerate(("111-222", "333-444"), start=1):
                pdf_sha = f"{index:064x}"
                image_root = import_root / pdf_sha
                image_root.mkdir(parents=True)
                (image_root / f"{digest}.jpg").write_bytes(image_bytes)
                left, right = holding.split("-", maxsplit=1)
                pdf = root / f"2025010{index}_{holding}.pdf"
                pdf.write_bytes(f"pdf-{index}".encode("ascii"))
                sources[pdf_sha] = target.SourcePdf(
                    pdf,
                    pdf_sha,
                    pdf.name,
                    holding,
                    "|".join(sorted((left, right))),
                )

            images, _, _ = target.discover_fresh_images(
                sources,
                import_root,
                snapshot(),
            )

            self.assertEqual([], images)

    def test_kandidat_muss_im_kb_ordner_und_im_snapshot_hashgebunden_sein(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            knowledge = Path(temporary) / "brain"
            candidates_root = knowledge / "training" / "models" / "candidates"
            candidate_root = candidates_root / "detect_gold_test"
            candidate_root.mkdir(parents=True)
            binding = target.CandidateBinding(
                candidate_root,
                candidate_root.name,
                "5" * 64,
                "6" * 64,
                candidate_root / "base.pt",
                "1" * 64,
                3,
                "7" * 64,
                "8" * 64,
            )
            bound = snapshot(
                candidates=(
                    {
                        "candidate_id": binding.candidate_id,
                        "candidate_manifest_sha256": binding.manifest_sha256,
                        "weights_sha256": binding.weights_sha256,
                        "dataset_plan_id": "9" * 64,
                        "dataset_manifest_sha256": "a" * 64,
                    },
                )
            )

            target._assert_candidate_bound_to_snapshot(knowledge, binding, bound)

            outside = target.CandidateBinding(
                Path(temporary) / "outside" / binding.candidate_id,
                binding.candidate_id,
                binding.manifest_sha256,
                binding.weights_sha256,
                binding.base_model_path,
                binding.base_model_sha256,
                binding.class_map_version,
                binding.class_map_sha256,
                binding.vsa_manifest_hash,
            )
            with self.assertRaisesRegex(ValueError, "Kandidatenordner"):
                target._assert_candidate_bound_to_snapshot(knowledge, outside, bound)

            wrong_hash = snapshot(
                candidates=(
                    {
                        "candidate_id": binding.candidate_id,
                        "candidate_manifest_sha256": "b" * 64,
                        "weights_sha256": binding.weights_sha256,
                        "dataset_plan_id": "9" * 64,
                        "dataset_manifest_sha256": "a" * 64,
                    },
                )
            )
            with self.assertRaisesRegex(ValueError, "Kontaminationsscan"):
                target._assert_candidate_bound_to_snapshot(
                    knowledge,
                    binding,
                    wrong_hash,
                )

    def test_extraktionsauswahl_respektiert_max_images_strikt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            images = tuple(
                self._selection_image(
                    root,
                    index,
                    f"{index + 1}-{index + 101}",
                    "deterministic_video_frame",
                )
                for index in range(3)
            )

            selected = target.select_extraction_items(
                images,
                max_holdings=3,
                max_images=1,
                background_target=3,
                minimum_holdings=1,
                minimum_background=1,
                operator_images_per_holding=1,
            )

            self.assertEqual(1, len(selected))

    def test_mindesthaltungen_gelten_fuer_die_endgueltige_auswahl(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            images = [
                self._selection_image(
                    root,
                    index,
                    f"{index + 1}-{index + 101}",
                    "deterministic_video_frame",
                )
                for index in range(3)
            ]
            reference = (
                {"class_id": 14, "class_name": "BCC_bogen", "code": "BCC", "text": "Bogen"},
            )
            images.extend(
                self._selection_image(
                    root,
                    10 + index,
                    "1-101",
                    "operator_pdf_photo",
                    reference,
                )
                for index in range(3)
            )

            with self.assertRaisesRegex(ValueError, "endgueltigen Auswahl"):
                target.select_extraction_items(
                    images,
                    max_holdings=3,
                    max_images=4,
                    background_target=1,
                    minimum_holdings=3,
                    minimum_background=1,
                    operator_images_per_holding=3,
                )

    def test_finale_staging_pruefung_erkennt_nachtraegliche_aenderung(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            staging = Path(temporary) / "staging"
            images = staging / "images"
            images.mkdir(parents=True)
            (staging / "_candidates.json").write_text("{}", encoding="utf-8")
            image = images / "image.jpg"
            image.write_bytes(b"original")
            expected = target._manifest_hashes(staging)
            (staging / "_manifest.json").write_text("{}", encoding="utf-8")
            image.write_bytes(b"veraendert")

            with self.assertRaisesRegex(ValueError, "Staging-Datei"):
                target._verify_staging_payload(staging, expected)

    def test_finale_staging_pruefung_sperrt_reparse_point(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            staging = Path(temporary) / "staging"
            images = staging / "images"
            images.mkdir(parents=True)
            (staging / "_candidates.json").write_text("{}", encoding="utf-8")
            (staging / "_manifest.json").write_text("{}", encoding="utf-8")
            expected = target._manifest_hashes(staging)
            original = protection._is_reparse_point

            def mark_images(path: Path) -> bool:
                return Path(path) == images or original(path)

            with mock.patch.object(
                protection,
                "_is_reparse_point",
                side_effect=mark_images,
            ):
                with self.assertRaisesRegex(ValueError, "Verknuepfung"):
                    target._verify_staging_payload(staging, expected)

    def test_extraktionsbeleg_prueft_bildanzahl(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            receipt, sources = self._write_extraction_receipt(root, image_count=2)

            with self.assertRaisesRegex(ValueError, "Bildanzahl"):
                target.discover_extraction_receipt_images(receipt, sources, snapshot())

    def test_extraktionsbeleg_verlangt_inhaltsadressierten_dateinamen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            receipt, sources = self._write_extraction_receipt(
                root,
                content_addressed_name=False,
            )

            with self.assertRaisesRegex(ValueError, "inhaltsadressiert"):
                target.discover_extraction_receipt_images(receipt, sources, snapshot())

    def test_extraktionsbeleg_verlangt_direkte_pdf_hashbindung(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            receipt, sources = self._write_extraction_receipt(root)
            document = json.loads(receipt.read_text(encoding="utf-8"))
            document["images"][0]["source_pdf_sha256"] = None
            receipt.write_text(json.dumps(document), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "PDF-Hash"):
                target.discover_extraction_receipt_images(receipt, sources, snapshot())

    def test_extraktionslauf_findet_auch_pdf_ohne_importiertes_foto(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source_root = root / "source"
            import_root = root / "imports"
            source_root.mkdir()
            import_root.mkdir()
            pdf = source_root / "20250101_111-222.pdf"
            pdf.write_bytes(b"pdf")

            sources, discovered, ambiguous = target.discover_pdf_sources(
                (source_root,),
                import_root,
                require_import=False,
            )

            self.assertEqual(1, discovered)
            self.assertEqual(0, ambiguous)
            self.assertIn(sha(pdf), sources)

    def test_publikation_ist_app_kompatibel_hashgebunden_und_nicht_ueberschreibbar(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            knowledge = root / "brain"
            source_root = root / "source"
            import_root = root / "imports"
            candidate_root = root / "candidate"
            for directory in (knowledge, source_root, import_root, candidate_root):
                directory.mkdir()
            base_model = candidate_root / "base.pt"
            base_model.write_bytes(b"base")
            pdf = source_root / "20250101_111-222.pdf"
            pdf.write_bytes(b"pdf")
            image_path = import_root / "frame.jpg"
            Image.effect_noise((256, 192), 32).convert("RGB").save(
                image_path,
                format="JPEG",
                quality=90,
            )
            image_sha = sha(image_path)
            source_pdf = target.SourcePdf(
                pdf, sha(pdf), pdf.name, "111-222", "111|222"
            )
            item = target.SourceImage(
                path=image_path,
                source_root=import_root,
                sha256=image_sha,
                size_bytes=image_path.stat().st_size,
                width=256,
                height=192,
                pdf=source_pdf,
            )
            class_map = root / "class_map.json"
            class_map.write_text("{}", encoding="utf-8")
            binding = target.CandidateBinding(
                candidate_root,
                "detect_gold_test",
                "5" * 64,
                "6" * 64,
                base_model,
                "1" * 64,
                3,
                "7" * 64,
                "8" * 64,
            )
            candidate_root = (
                knowledge
                / "training"
                / "models"
                / "candidates"
                / binding.candidate_id
            )
            candidate_root.mkdir(parents=True)
            binding = target.CandidateBinding(
                candidate_root,
                binding.candidate_id,
                binding.manifest_sha256,
                binding.weights_sha256,
                binding.base_model_path,
                binding.base_model_sha256,
                binding.class_map_version,
                binding.class_map_sha256,
                binding.vsa_manifest_hash,
            )
            contamination = snapshot(
                candidates=(
                    {
                        "candidate_id": binding.candidate_id,
                        "candidate_manifest_sha256": binding.manifest_sha256,
                        "weights_sha256": binding.weights_sha256,
                        "dataset_plan_id": "a" * 64,
                        "dataset_manifest_sha256": "b" * 64,
                    },
                )
            )
            holdout_id = "9" * 64
            target_root = knowledge / "eval_set" / "subsets" / (
                "detect_release_holdout_" + holdout_id[:12]
            )
            plan = target.HoldoutPlan(
                knowledge,
                binding,
                class_map,
                datetime.now(timezone.utc),
                contamination,
                (source_root,),
                import_root,
                (item,),
                1,
                1,
                0,
                0,
                0,
                holdout_id,
                target_root,
            )

            def scanner(*_args, **_kwargs):
                return contamination

            published = target.publish_holdout(plan, scanner=scanner)
            manifest = json.loads((published / "_manifest.json").read_text(encoding="utf-8"))
            candidates = json.loads((published / "_candidates.json").read_text(encoding="utf-8"))
            self.assertTrue(manifest["frozen"])
            self.assertFalse(manifest["training_allowed"])
            self.assertEqual(15, len(manifest["classes"]))
            self.assertEqual(1, manifest["candidates_count"])
            self.assertEqual(
                "operator_reference_coverage_incomplete",
                manifest["selection"]["operator_reference_coverage"]["status"],
            )
            self.assertEqual(
                15,
                len(manifest["selection"]["operator_reference_coverage"]["missing_classes"]),
            )
            self.assertEqual(
                candidates["candidates"][0]["frame_path"],
                Path(candidates["candidates"][0]["image_path"]).name,
            )
            store = DetectReleaseHoldoutReviewStore(
                published,
                root / "review.json",
                "Tester",
            )
            self.assertEqual(1, store.state()["total"])
            with self.assertRaises(FileExistsError):
                target.publish_holdout(plan, scanner=scanner)


if __name__ == "__main__":
    unittest.main()
