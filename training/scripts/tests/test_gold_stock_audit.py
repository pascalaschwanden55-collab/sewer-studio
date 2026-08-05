from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence

from PIL import Image


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "gold_stock_audit.py"
SPEC = importlib.util.spec_from_file_location("gold_stock_audit", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

IMAGE_SIZE = (8, 4)
PIXELS = IMAGE_SIZE[0] * IMAGE_SIZE[1]
# 10 Hintergrund, 5 Maske, 17 Hintergrund = 32 Pixel.
VALID_RLE = "0,10,5,17"


def _canonical_json_bytes(value: object) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def _write_fixture_json(path: Path, value: object) -> None:
    path.write_text(
        json.dumps(value, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def create_reviewed_negative_set(
    root: Path,
    holdings: Sequence[str] = ("100-200", "300-400"),
    *,
    color_offset: int = 0,
    invalid_split: bool = False,
    holding_override: str | None = None,
) -> Path:
    """Erzeugt einen kleinen, voll gebundenen Publisher-Satz fuer Tests."""
    staging = (
        root
        / "training"
        / "negatives"
        / "sets"
        / f".fixture-{color_offset}"
    )
    images_root = staging / "images"
    receipts_root = staging / "receipts"
    images_root.mkdir(parents=True)
    receipts_root.mkdir()

    class_map_path = (
        SCRIPT_PATH.parents[2]
        / "training"
        / "class_maps"
        / "detect_class_map_v3.json"
    )
    class_map_bytes = class_map_path.read_bytes()
    class_map_sha = hashlib.sha256(class_map_bytes).hexdigest()
    class_map = json.loads(class_map_bytes.decode("utf-8-sig"))
    ordered_names = [
        name
        for name, _ in sorted(
            class_map["classes"].items(),
            key=lambda pair: pair[1],
        )
    ]
    (receipts_root / "class_map.json").write_bytes(class_map_bytes)
    model_scope = [
        {
            "candidate_id": f"fixture-model-{color_offset}",
            "candidate_manifest_sha256": hashlib.sha256(
                f"candidate-{color_offset}".encode("utf-8")
            ).hexdigest(),
            "weights_sha256": hashlib.sha256(
                f"weights-{color_offset}".encode("utf-8")
            ).hexdigest(),
            "dataset_plan_id": hashlib.sha256(
                f"plan-{color_offset}".encode("utf-8")
            ).hexdigest(),
            "dataset_manifest_sha256": hashlib.sha256(
                f"dataset-{color_offset}".encode("utf-8")
            ).hexdigest(),
        }
    ]

    queue_items: list[dict[str, object]] = []
    candidates: list[dict[str, object]] = []
    image_records: list[dict[str, object]] = []
    queue_hashes: dict[str, dict[str, object]] = {}
    for index, holding in enumerate(holdings):
        left, right = holding.split("-", maxsplit=1)
        physical = "|".join(sorted((left.casefold(), right.casefold())))
        image_path = images_root / f"fixture_{color_offset}_{index}.png"
        Image.new(
            "RGB",
            IMAGE_SIZE,
            (
                (color_offset + index + 20) % 255,
                (color_offset + index + 40) % 255,
                (color_offset + index + 60) % 255,
            ),
        ).save(image_path)
        image_path.write_bytes(image_path.read_bytes() + b"x" * 2048)
        image_sha = hashlib.sha256(image_path.read_bytes()).hexdigest()
        target_name = f"img_{image_sha}.png"
        image_path.rename(images_root / target_name)
        image_path = images_root / target_name
        item_id = f"bcc-hn-{image_sha[:16]}"
        source_ref = hashlib.sha256(
            f"source-{color_offset}-{index}".encode("utf-8")
        ).hexdigest()
        queue_item = {
            "id": item_id,
            "image_sha256": image_sha,
            "holding_key": holding,
            "physical_holding_key": physical,
            "source_ref": source_ref,
            "inspection_date": "2026-07-28",
            "size_bytes": image_path.stat().st_size,
            "image_format": "png",
            "predictions": [
                {
                    "model_id": model_scope[0]["candidate_id"],
                    "predicted_bcc": True,
                    "bcc_detection_count": 1,
                    "max_bcc_confidence": 0.75,
                }
            ],
        }
        queue_items.append(queue_item)
        candidates.append(
            {
                "id": item_id,
                "frame_path": target_name,
                "category": "all_class_background_review",
                "status": "pending_review",
                "source_sha256": image_sha,
            }
        )
        queue_hashes[f"images/{target_name}"] = {
            "sha256": image_sha,
            "size_bytes": image_path.stat().st_size,
        }
        image_records.append(
            {
                "id": f"bcc-neg-{image_sha}",
                "file_name": target_name,
                "image_sha256": image_sha,
                "size_bytes": image_path.stat().st_size,
                "image_format": "png",
                "holding_key": holding,
                "physical_holding_key": physical,
                "split": "",
                "review_item_id": item_id,
                "review_decision": "all_classes_clear",
                "source_ref": source_ref,
                "inspection_date": "2026-07-28",
            }
        )

    candidates_path = receipts_root / "queue_candidates.json"
    _write_fixture_json(candidates_path, candidates)
    candidates_sha = hashlib.sha256(candidates_path.read_bytes()).hexdigest()
    queue_hashes["_candidates.json"] = {
        "sha256": candidates_sha,
        "size_bytes": candidates_path.stat().st_size,
    }
    queue_semantic = {
        "schema_version": "1.0",
        "purpose": "bcc_hard_negative_review_queue",
        "pilot": "BCC_bogen",
        "role": "training_candidate_review",
        "class_map_version": class_map["version"],
        "class_map_sha256": class_map_sha,
        "vsa_manifest_hash": class_map["vsa_manifest_hash"],
        "class_names": ordered_names,
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
        "items": queue_items,
    }
    queue_id = hashlib.sha256(_canonical_json_bytes(queue_semantic)).hexdigest()
    queue_manifest = {
        "schema_version": "1.0",
        "purpose": "bcc_hard_negative_review_queue",
        "queue_id": queue_id,
        "pilot": "BCC_bogen",
        "role": "training_candidate_review",
        "created_utc": "2026-07-28T12:00:00Z",
        "frozen": True,
        "dataset_status": "review_incomplete",
        "warning": "fixture",
        "review_target": (
            "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse"
        ),
        "class_map_version": class_map["version"],
        "class_map_sha256": class_map_sha,
        "vsa_manifest_hash": class_map["vsa_manifest_hash"],
        "class_names": ordered_names,
        "protected_sets": [],
        "protection_snapshot": {},
        "selection_rule": {
            "one_image_per_physical_holding": True,
            "requires_current_model_bcc_trigger": True,
            "reviewer_sees_model_signals": False,
        },
        "sources": [],
        "candidates_count": len(candidates),
        "images_count": len(candidates),
        "holdings_count": len(candidates),
        "hash_algorithm": "sha256",
        "hashes_count": len(queue_hashes),
        "hashes": queue_hashes,
        "semantic": queue_semantic,
        "selection_receipt": {"models": model_scope, "items": queue_items},
    }
    queue_manifest_path = receipts_root / "queue_manifest.json"
    _write_fixture_json(queue_manifest_path, queue_manifest)
    queue_manifest_sha = hashlib.sha256(
        queue_manifest_path.read_bytes()
    ).hexdigest()

    decisions = {
        str(item["id"]): {
            "decision": "all_classes_clear",
            "comment": "",
            "reviewed_at_utc": "2026-07-28T12:30:00Z",
        }
        for item in queue_items
    }
    review = {
        "schema_version": "1.0",
        "purpose": "bcc_hard_negative_review",
        "queue_id": queue_id,
        "queue_manifest_sha256": queue_manifest_sha,
        "candidates_sha256": candidates_sha,
        "class_map_sha256": class_map_sha,
        "reviewer": "Besitzer",
        "updated_at_utc": "2026-07-28T12:30:00Z",
        "decisions": decisions,
    }
    review_path = receipts_root / "review.json"
    _write_fixture_json(review_path, review)
    review_sha = hashlib.sha256(review_path.read_bytes()).hexdigest()

    ranked = sorted(
        (str(item["physical_holding_key"]) for item in image_records),
        key=lambda physical: (
            hashlib.sha256(
                f"bcc-hard-negative-split-v1|{physical}".encode("utf-8")
            ).hexdigest(),
            physical,
        ),
    )
    validation_count = 0 if len(ranked) < 2 else max(1, (len(ranked) + 2) // 5)
    validation = set(ranked[:validation_count])
    for image in image_records:
        physical = str(image["physical_holding_key"])
        image["split"] = "validation" if physical in validation else "train"
    if invalid_split:
        image_records[0]["split"] = (
            "train"
            if image_records[0]["split"] == "validation"
            else "validation"
        )
    if holding_override is not None:
        image_records[0]["holding_key"] = holding_override

    counts = {
        "all_classes_clear": len(decisions),
        "mapped_object_visible": 0,
        "exclude_uncertain": 0,
    }
    semantic = {
        "schema_version": "1.0",
        "purpose": "bcc_reviewed_negative_set",
        "pilot": "BCC_bogen",
        "role": "training_negative_set",
        "queue": {
            "queue_id": queue_id,
            "queue_manifest_sha256": queue_manifest_sha,
            "queue_manifest_receipt_path": "receipts/queue_manifest.json",
            "candidates_sha256": candidates_sha,
            "candidates_receipt_path": "receipts/queue_candidates.json",
        },
        "review": {
            "purpose": "bcc_hard_negative_review",
            "review_sha256": review_sha,
            "receipt_path": "receipts/review.json",
            "reviewed_images": len(decisions),
            "decision_counts": counts,
        },
        "class_map_version": class_map["version"],
        "class_map_sha256": class_map_sha,
        "class_map_receipt_path": "receipts/class_map.json",
        "vsa_manifest_hash": class_map["vsa_manifest_hash"],
        "class_names": ordered_names,
        "protected_sets": [],
        "protection_snapshot": {},
        "split_rule": {
            "name": "stable_rank_v1",
            "salt": "bcc-hard-negative-split-v1",
            "one_image_per_physical_holding": True,
            "validation_count": validation_count,
            "train_count": len(image_records) - validation_count,
        },
        "images": image_records,
    }
    set_id = hashlib.sha256(_canonical_json_bytes(semantic)).hexdigest()
    hashes: dict[str, dict[str, object]] = {}
    for path in sorted(
        [*images_root.iterdir(), *receipts_root.iterdir()],
        key=lambda item: item.relative_to(staging).as_posix(),
    ):
        hashes[path.relative_to(staging).as_posix()] = {
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "size_bytes": path.stat().st_size,
        }
    manifest = {
        "schema_version": "1.0",
        "purpose": "bcc_reviewed_negative_set",
        "set_id": set_id,
        "pilot": "BCC_bogen",
        "role": "training_negative_set",
        "created_utc": "2026-07-28T13:00:00Z",
        "frozen": True,
        "dataset_status": "ready_for_training",
        "hash_algorithm": "sha256",
        "images_count": len(image_records),
        "holdings_count": len(image_records),
        "hashes_count": len(hashes),
        "hashes": hashes,
        "semantic": semantic,
    }
    _write_fixture_json(staging / "_manifest.json", manifest)
    target = staging.with_name(f"bcc_hn_{set_id[:12]}")
    staging.rename(target)
    return target


class GoldStockAuditTests(unittest.TestCase):
    def _make_root(self, root: Path) -> tuple[Path, Path, Path, Path]:
        frames = root / "frames"
        frames.mkdir(parents=True)
        eval_images = root / "eval_set" / "images"
        eval_images.mkdir(parents=True)
        negatives = root / "negatives"
        negatives.mkdir(parents=True)
        registry = root / "export_registry_v1.json"
        registry.write_text(
            json.dumps({"approved_by": "Besitzer"}), encoding="utf-8"
        )
        return frames, eval_images, negatives, registry

    def _image(self, frames: Path, name: str, color: int = 7) -> Path:
        path = frames / name
        Image.new("RGB", IMAGE_SIZE, (color, color, color)).save(path)
        return path

    def _sample(
        self,
        sample_id: str,
        frame: Path,
        case_id: str = "case-1",
        code: str = "BCCBY",
        description: str = "Bogen bei 3 Uhr",
    ) -> dict:
        return {
            "SampleId": sample_id,
            "CaseId": case_id,
            "Code": code,
            "Beschreibung": description,
            "FramePath": str(frame),
            "Status": 1,
            "SourceType": "ManualCoding",
            "HumanConfirmed": True,
            "Corrected": False,
            "ConfirmedByUser": "Besitzer",
            "ConfirmedAtUtc": "2026-07-25T12:00:00Z",
            "MatchLevel": "ReviewApproved",
            "HasBbox": True,
            "BboxXCenter": 0.5,
            "BboxYCenter": 0.5,
            "BboxWidth": 0.5,
            "BboxHeight": 0.5,
            "SamMaskRle": VALID_RLE,
            "SamMaskImageWidth": IMAGE_SIZE[0],
            "SamMaskImageHeight": IMAGE_SIZE[1],
        }

    @staticmethod
    def _pdf_notes(
        match_kind: str = "time_meter_text",
        photo_id: str = "231123_115548_266.jpg",
    ) -> str:
        return (
            "PDF-Operateurreferenz: 20231123_06.887943-90327.pdf; "
            "SHA-256="
            "8a7cfb71d1289694b8a650fe2c49357840fe1935ac120b8fb83d24f899c99c6f; "
            f"Seite=3; Foto={photo_id}; Zuordnung={match_kind}"
        )

    def _audit(
        self,
        root: Path,
        samples: list[dict],
        eval_images: Path,
        negatives: Path,
        registry: Path,
        approved_by: str = "Besitzer",
        negative_sets: Sequence[Path] = (),
    ) -> dict:
        samples_path = root / "training_samples.json"
        samples_path.write_text(json.dumps(samples), encoding="utf-8")
        return MODULE.build_audit(
            samples_path,
            registry,
            eval_images,
            negatives,
            approved_by,
            "registry",
            datetime(2026, 7, 25, 12, 0, tzinfo=timezone.utc),
            negative_sets,
        )

    def test_jede_pruefstufe_verwirft_korrekt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            good_frame = self._image(frames, "good.png")
            eval_frame = self._image(frames, "eval.png", color=42)
            # Gleiche Bytes ins Eval-Set legen: Hash-Kollision muss verwerfen.
            (eval_images / "eval.png").write_bytes(eval_frame.read_bytes())
            broken = frames / "broken.jpg"
            broken.write_bytes(b"das-ist-kein-bild")

            samples = [
                self._sample("ok", good_frame),
                self._sample("entwurf", good_frame) | {"Status": 4},
                self._sample("zurueckgewiesen", good_frame) | {"Status": 2},
                self._sample("teacher", good_frame) | {"SourceType": "TeacherModel"},
                self._sample("nicht-bestaetigt", good_frame) | {"HumanConfirmed": False},
                self._sample("fremder-user", good_frame) | {"ConfirmedByUser": "Fremd"},
                self._sample("entscheidung-fehlt", good_frame) | {"Corrected": None},
                self._sample("zeitpunkt-fehlt", good_frame) | {"ConfirmedAtUtc": None},
                self._sample("matchlevel-falsch", good_frame) | {
                    "MatchLevel": "AutoMatched",
                },
                self._sample("bild-fehlt", frames / "fehlt.png"),
                self._sample("bild-kaputt", broken),
                self._sample("box-null", good_frame) | {"BboxWidth": 0.0},
                self._sample("box-zu-gross", good_frame) | {"BboxHeight": 1.2},
                self._sample("box-zentrum-draussen", good_frame) | {"BboxXCenter": 1.4},
                self._sample("box-ragt-raus", good_frame) | {
                    "BboxXCenter": 0.9,
                    "BboxWidth": 0.4,
                },
                self._sample("rle-fehlt", good_frame) | {"SamMaskRle": None},
                self._sample("rle-startwert", good_frame) | {"SamMaskRle": "2,10,5,17"},
                self._sample("rle-token", good_frame) | {"SamMaskRle": "0,10,x,17"},
                self._sample("rle-summe", good_frame) | {"SamMaskRle": "0,10,5,10"},
                self._sample("rle-leer", good_frame) | {"SamMaskRle": f"0,{PIXELS}"},
                self._sample("rle-maske-dims", good_frame) | {
                    # Format gueltig (16 Pixel), aber 4x4 passt nicht zum 8x4-Bild.
                    "SamMaskRle": "0,6,4,6",
                    "SamMaskImageWidth": 4,
                    "SamMaskImageHeight": 4,
                },
                self._sample("maske-ausserhalb-box", good_frame) | {
                    "SamMaskRle": "1,5,27",
                    "BboxXCenter": 0.75,
                    "BboxYCenter": 0.75,
                    "BboxWidth": 0.25,
                    "BboxHeight": 0.25,
                },
                self._sample("masken-huelle-taeuscht", good_frame) | {
                    # Pixel nur ganz links oben + rechts unten: Die Huelle schneidet
                    # die mittige Box, aber kein echter Maskenpixel liegt darin.
                    "SamMaskRle": "1,1,30,1",
                    "BboxXCenter": 0.5,
                    "BboxYCenter": 0.5,
                    "BboxWidth": 0.25,
                    "BboxHeight": 0.25,
                },
                self._sample("rle-ungerade-gueltig", good_frame) | {
                    # Der echte Encoder darf mit einem Vordergrund-Run enden.
                    "SamMaskRle": "0,27,5",
                    "BboxXCenter": 0.75,
                    "BboxYCenter": 0.75,
                    "BboxWidth": 0.5,
                    "BboxHeight": 0.5,
                },
                # Hauptcode BAB ist bekannt; der erfundene Untercode darf trotzdem
                # nie ueber einen Hauptcode-Rueckfall als Gold gelten.
                self._sample("code-unbekannt", good_frame, code="BABZZ"),
                self._sample("eval-treffer", eval_frame),
            ]
            audit = self._audit(root, samples, eval_images, negatives, registry)

            einlesen = audit["einlesen"]
            self.assertEqual(26, einlesen["datei_gesamt"])
            self.assertEqual(1, einlesen["uebersprungen_entwurf"])
            self.assertEqual(1, einlesen["uebersprungen_status_sonstige"])
            self.assertEqual(1, einlesen["uebersprungen_quelle_sonstige"])
            self.assertEqual(23, einlesen["eingelesen"])

            stufen = audit["pruefstufen"]
            self.assertEqual(23, stufen["eingelesen"])
            self.assertEqual(18, stufen["persoenlich"])
            self.assertEqual(16, stufen["bild_ok"])
            self.assertEqual(12, stufen["box_ok"])
            self.assertEqual(4, stufen["maske_ok"])
            self.assertEqual(3, stufen["code_ok"])
            self.assertEqual(2, stufen["eval_sauber"])
            self.assertEqual(2, stufen["final_verwendbar"])

            gruende = {v["sample_id"]: v["stufe"] for v in audit["verwerfungen"]}
            self.assertEqual(
                {
                    "nicht-bestaetigt": "persoenlich",
                    "fremder-user": "persoenlich",
                    "entscheidung-fehlt": "persoenlich",
                    "zeitpunkt-fehlt": "persoenlich",
                    "matchlevel-falsch": "persoenlich",
                    "bild-fehlt": "bild_ok",
                    "bild-kaputt": "bild_ok",
                    "box-null": "box_ok",
                    "box-zu-gross": "box_ok",
                    "box-zentrum-draussen": "box_ok",
                    "box-ragt-raus": "box_ok",
                    "rle-fehlt": "maske_ok",
                    "rle-startwert": "maske_ok",
                    "rle-token": "maske_ok",
                    "rle-summe": "maske_ok",
                    "rle-leer": "maske_ok",
                    "rle-maske-dims": "maske_ok",
                    "maske-ausserhalb-box": "maske_ok",
                    "masken-huelle-taeuscht": "maske_ok",
                    "code-unbekannt": "code_ok",
                    "eval-treffer": "eval_sauber",
                },
                gruende,
            )
            self.assertEqual(1, audit["eval_treffer_ausgeschlossen"])
            unbekannte = audit["unbekannte_codes"]
            self.assertEqual(1, len(unbekannte))
            self.assertEqual("BABZZ", unbekannte[0]["code"])
            self.assertEqual(["code-unbekannt"], unbekannte[0]["sample_ids"])
            final_ids = {s["sample_id"] for s in audit["samples"]}
            self.assertEqual({"ok", "rle-ungerade-gueltig"}, final_ids)

    def test_bestaetigte_pdf_fotos_mit_strenger_pruefspur_werden_gold(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            samples = []
            for index, (match_kind, photo_id) in enumerate(
                (
                    ("same_block", "-"),
                    ("photo_id", "264"),
                    ("time_meter_text", "231123_115548_266.jpg"),
                )
            ):
                frame = self._image(frames, f"pdf-{index}.png", color=index + 30)
                samples.append(
                    self._sample(
                        f"pdf-{index}",
                        frame,
                        case_id=f"100-{200 + index}",
                    )
                    | {
                        "SourceType": "PdfPhoto",
                        "Notes": self._pdf_notes(match_kind, photo_id),
                        "SourceReferenceCode": "BCCBY",
                        "SourceReferenceDescription": "Bogen nach rechts",
                    }
                )

            audit = self._audit(root, samples, eval_images, negatives, registry)

            self.assertEqual(3, audit["pruefstufen"]["final_verwendbar"])
            self.assertEqual(
                {"pdf-0", "pdf-1", "pdf-2"},
                {value["sample_id"] for value in audit["samples"]},
            )

    def test_pdf_fotos_ohne_strenge_pruefspur_bleiben_gesperrt(self) -> None:
        invalid_notes = (
            None,
            "",
            (
                "PDF-Operateurreferenz: ; "
                "SHA-256="
                "8a7cfb71d1289694b8a650fe2c49357840fe1935ac120b8fb83d24f899c99c6f; "
                "Seite=3; Foto=42; Zuordnung=photo_id"
            ),
            self._pdf_notes().replace("c99c6f;", "c99c6;"),
            self._pdf_notes().replace("c99c6f;", "c99c6g;"),
            self._pdf_notes().replace(
                "20231123_06.887943-90327.pdf",
                ".pdf",
            ),
            self._pdf_notes().replace("Seite=3", "Seite=0"),
            self._pdf_notes("photo_id", "-"),
            self._pdf_notes("unsicher", "42"),
            self._pdf_notes().replace(
                "20231123_06.887943-90327.pdf",
                r"..\20231123_06.887943-90327.pdf",
            ),
            self._pdf_notes() + "; Zusatz=ungeprueft",
        )

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "pdf.png")
            samples = [
                self._sample(f"pdf-invalid-{index}", frame)
                | {
                    "SourceType": "PdfPhoto",
                    "Notes": notes,
                    "SourceReferenceCode": "BCCBY",
                    "SourceReferenceDescription": "Bogen nach rechts",
                }
                for index, notes in enumerate(invalid_notes)
            ]

            audit = self._audit(root, samples, eval_images, negatives, registry)

            self.assertEqual(len(invalid_notes), audit["einlesen"]["eingelesen"])
            self.assertEqual(0, audit["pruefstufen"]["persoenlich"])
            self.assertEqual(0, audit["pruefstufen"]["final_verwendbar"])
            self.assertTrue(
                all(
                    value["stufe"] == "persoenlich"
                    and "PDF-" in value["grund"]
                    for value in audit["verwerfungen"]
                )
            )

    def test_pdf_pruefspur_mit_reparatur_suffix_bleibt_gueltig(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "pdf-repair.png")
            gueltig = self._pdf_notes() + (
                "; CaseId 9109-10 -> 9109-10.8433 "
                "(PDF-Bildbeleg gruppe_1_mit_bildbeleg, 2026-08-04)"
            )
            doppelt = gueltig + (
                "; CaseId alt-1 -> neu-1 (Kandidaten-Byte-Match xtf, 2026-08-05)"
            )
            ungueltig = self._pdf_notes() + "; CaseId ohne-beleg"
            samples = [
                self._sample("pdf-rep-ok", frame)
                | {"SourceType": "PdfPhoto", "Notes": gueltig,
                   "SourceReferenceCode": "BCCBY",
                   "SourceReferenceDescription": "Bogen nach rechts"},
                self._sample("pdf-rep-ok2", frame)
                | {"SourceType": "PdfPhoto", "Notes": doppelt,
                   "SourceReferenceCode": "BCCBY",
                   "SourceReferenceDescription": "Bogen nach rechts"},
                self._sample("pdf-rep-bad", frame)
                | {"SourceType": "PdfPhoto", "Notes": ungueltig,
                   "SourceReferenceCode": "BCCBY",
                   "SourceReferenceDescription": "Bogen nach rechts"},
            ]

            audit = self._audit(root, samples, eval_images, negatives, registry)

            verwendbar = {value["sample_id"] for value in audit["samples"]}
            self.assertIn("pdf-rep-ok", verwendbar)
            self.assertIn("pdf-rep-ok2", verwendbar)
            self.assertNotIn("pdf-rep-bad", verwendbar)

    def test_pdf_foto_ohne_Operateur_Code_oder_Text_bleibt_gesperrt(self) -> None:
        base = {
            "HumanConfirmed": True,
            "Corrected": False,
            "ConfirmedByUser": "Besitzer",
            "ConfirmedAtUtc": "2026-07-25T12:00:00Z",
            "MatchLevel": "ReviewApproved",
            "SourceType": "PdfPhoto",
            "Notes": self._pdf_notes(),
            "SourceReferenceCode": "BCCBY",
            "SourceReferenceDescription": "Bogen nach rechts",
        }

        self.assertIn(
            "Code fehlt",
            MODULE.check_personal(base | {"SourceReferenceCode": None}, "Besitzer"),
        )
        self.assertIn(
            "Befundtext fehlt",
            MODULE.check_personal(
                base | {"SourceReferenceDescription": None},
                "Besitzer",
            ),
        )

    def test_bestaetigungszeitpunkt_muss_gueltiges_utc_iso_datum_sein(self) -> None:
        base = {
            "HumanConfirmed": True,
            "Corrected": False,
            "ConfirmedByUser": "Besitzer",
            "ConfirmedAtUtc": "2026-07-25T12:00:00Z",
            "MatchLevel": "ReviewApproved",
            "SourceType": "ManualCoding",
        }

        self.assertIn(
            "kein gueltiges ISO-Datum",
            MODULE.check_personal(base | {"ConfirmedAtUtc": "irgendwann"}, "Besitzer"),
        )
        self.assertIn(
            "nicht in UTC",
            MODULE.check_personal(
                base | {"ConfirmedAtUtc": "2026-07-25T12:00:00+02:00"},
                "Besitzer",
            ),
        )

    def test_gespeicherte_maskenflaeche_muss_zur_rle_passen(self) -> None:
        reason = MODULE.check_mask(
            VALID_RLE,
            IMAGE_SIZE[0],
            IMAGE_SIZE[1],
            IMAGE_SIZE[0],
            IMAGE_SIZE[1],
            0.5,
            0.5,
            0.5,
            0.5,
            999,
        )

        self.assertIn("passt nicht zur RLE", reason)

    def test_maske_braucht_mindestens_80_prozent_in_der_hand_box(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "mask.png")
            exact_eighty = self._sample("exact-80", frame)
            below_eighty = self._sample("below-80", frame) | {
                "BboxXCenter": 0.4375,
                "BboxWidth": 0.375,
            }

            audit = self._audit(
                root,
                [exact_eighty, below_eighty],
                eval_images,
                negatives,
                registry,
            )

            self.assertEqual(
                {"exact-80"},
                {value["sample_id"] for value in audit["samples"]},
            )
            rejection = next(
                value
                for value in audit["verwerfungen"]
                if value["sample_id"] == "below-80"
            )
            self.assertEqual("maske_ok", rejection["stufe"])
            self.assertIn("60.0 % innerhalb", rejection["grund"])
            self.assertIn("mindestens 80 %", rejection["grund"])

    def test_platzhalter_ist_verwendbar_aber_kb_text_offen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            samples = [
                self._sample("ohne", frame, description="Riss laengs bei 12 Uhr"),
                self._sample(
                    "platzhalter-ae",
                    frame,
                    description="Riss laengs bei 12 Uhr — Ausmass ergaenzen",
                ),
                self._sample(
                    "platzhalter-umlaut",
                    frame,
                    description="Riss quer — Ausmass ergänzen",
                ),
            ]
            audit = self._audit(root, samples, eval_images, negatives, registry)

            self.assertEqual(3, audit["pruefstufen"]["final_verwendbar"])
            self.assertEqual(2, audit["kb_text_offen"])
            flags = {s["sample_id"]: s["kb_text_offen"] for s in audit["samples"]}
            self.assertEqual(
                {"ohne": False, "platzhalter-ae": True, "platzhalter-umlaut": True},
                flags,
            )

    def test_duplikate_werden_als_gruppe_ausgewiesen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            kopie = frames / "kopie.png"
            kopie.write_bytes(frame.read_bytes())
            anderes = self._image(frames, "b.png", color=99)
            samples = [
                self._sample("s1", frame),
                self._sample("s2", kopie),
                self._sample("s3", anderes),
            ]
            audit = self._audit(root, samples, eval_images, negatives, registry)

            self.assertEqual(3, audit["pruefstufen"]["final_verwendbar"])
            self.assertEqual(1, audit["duplikat_gruppen_anzahl"])
            gruppe = audit["duplikat_gruppen"][0]
            self.assertEqual(2, gruppe["anzahl"])
            self.assertEqual(["s1", "s2"], gruppe["sample_ids"])
            self.assertEqual(
                hashlib.sha256(frame.read_bytes()).hexdigest(), gruppe["sha256"]
            )

    def test_split_ist_deterministisch_und_gruppentreu(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            samples = []
            for index in range(6):
                frame = self._image(frames, f"f{index}.png", color=index + 1)
                samples.append(
                    self._sample(f"s{index}", frame, case_id=f"foto_20260720_{index}")
                )
            copy = frames / "f0-copy.png"
            copy.write_bytes((frames / "f0.png").read_bytes())
            samples.append(
                self._sample("s0-copy", copy, case_id="foto_20260721_99")
            )
            halt_frame = self._image(frames, "h.png", color=200)
            halt_frame_2 = self._image(frames, "h2.png", color=201)
            samples.append(self._sample("h1", halt_frame, case_id="21683-21749"))
            samples.append(self._sample("h2", halt_frame_2, case_id="21683-21749"))

            audit_a = self._audit(root, samples, eval_images, negatives, registry)
            audit_b = self._audit(root, samples, eval_images, negatives, registry)

            rollen_a = {s["sample_id"]: s["rolle"] for s in audit_a["samples"]}
            rollen_b = {s["sample_id"]: s["rolle"] for s in audit_b["samples"]}
            self.assertEqual(rollen_a, rollen_b)

            # Pseudo-IDs werden nicht nach Aufnahmetag zusammengeworfen. Nur identische
            # Bilder teilen ihren SHA-Schluessel; eine echte Haltung bleibt komplett zusammen.
            gruppen = {g["gruppe"]: g["rolle"] for g in audit_a["split"]["gruppen"]}
            self.assertEqual(7, len(gruppen))
            f0_sha = hashlib.sha256((frames / "f0.png").read_bytes()).hexdigest()
            erwartet_foto = MODULE.split_role(f"bild:{f0_sha}")
            erwartet_haltung = MODULE.split_role("haltung:21683-21749")
            self.assertEqual(erwartet_foto, gruppen[f"bild:{f0_sha}"])
            self.assertEqual(
                erwartet_haltung, gruppen["haltung:21683-21749"]
            )
            self.assertEqual(erwartet_foto, rollen_a["s0"])
            self.assertEqual(erwartet_foto, rollen_a["s0-copy"])
            self.assertEqual(erwartet_haltung, rollen_a["h1"])
            self.assertEqual(erwartet_haltung, rollen_a["h2"])
            for index in range(6):
                image_sha = hashlib.sha256(
                    (frames / f"f{index}.png").read_bytes()
                ).hexdigest()
                self.assertEqual(
                    MODULE.split_role(f"bild:{image_sha}"),
                    rollen_a[f"s{index}"],
                )

            bilder = audit_a["split"]["bilder"]
            self.assertEqual(9, bilder["train"] + bilder["val"] + bilder["test"])
            self.assertTrue(audit_a["split"]["test_eingefroren_nur_markiert"])
            self.assertFalse(audit_a["split"]["release_faehig"])
            self.assertEqual(7, audit_a["split"]["fehlende_haltungsidentitaet"])

    def test_split_normalisiert_haltung_und_verbindet_bildduplikate(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            shared = self._image(frames, "shared.png", color=10)
            shared_copy = frames / "shared-copy.png"
            shared_copy.write_bytes(shared.read_bytes())
            a_other = self._image(frames, "a-other.png", color=11)
            b_other = self._image(frames, "b-other.png", color=12)
            samples = [
                self._sample("a1", shared, case_id="06.24379-06.24377"),
                self._sample("a2", a_other, case_id="24379-24377/2026"),
                self._sample("b1", shared_copy, case_id="111-222"),
                self._sample("b2", b_other, case_id="111-222"),
            ]

            audit = self._audit(root, samples, eval_images, negatives, registry)

            # Das Bildduplikat verbindet beide Haltungen transitiv. Kein Mitglied
            # darf dadurch in einer anderen Split-Rolle landen.
            self.assertEqual(1, len(audit["split"]["gruppen"]))
            self.assertEqual(1, len({sample["rolle"] for sample in audit["samples"]}))
            keys = {sample["sample_id"]: sample["haltung_key"] for sample in audit["samples"]}
            self.assertEqual("24379-24377", keys["a1"])
            self.assertEqual("24379-24377", keys["a2"])
            self.assertTrue(audit["split"]["release_faehig"])

    def test_leere_und_gold_inbox_ids_sind_keine_haltungen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            first = self._image(frames, "first.png", color=20)
            second = self._image(frames, "second.png", color=21)
            samples = [
                self._sample("leer", first, case_id=""),
                self._sample("inbox", second, case_id="gold_inbox_abc123"),
            ]

            audit = self._audit(root, samples, eval_images, negatives, registry)

            self.assertFalse(audit["split"]["release_faehig"])
            self.assertEqual(2, audit["split"]["fehlende_haltungsidentitaet"])
            self.assertTrue(all(sample["haltung_key"] is None for sample in audit["samples"]))

    def test_eval_haltung_wird_auch_bei_anderen_bildbytes_gesperrt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            (eval_images.parent / "_candidates.json").write_text(
                json.dumps([{"haltung_key": "06.24379-06.24377"}]),
                encoding="utf-8",
            )
            frame = self._image(frames, "anderer-frame.png", color=31)
            sample = self._sample(
                "eval-haltung",
                frame,
                case_id="24379-24377/2026_Saniert",
            )

            audit = self._audit(root, [sample], eval_images, negatives, registry)

            self.assertEqual(0, audit["pruefstufen"]["final_verwendbar"])
            self.assertEqual(1, audit["eval_treffer_ausgeschlossen"])
            self.assertEqual("eval_sauber", audit["verwerfungen"][0]["stufe"])
            self.assertIn("24379-24377", audit["verwerfungen"][0]["grund"])

    def test_split_rolle_folgt_dem_hash(self) -> None:
        def erwartete_rolle(key: str) -> str:
            digest = hashlib.sha256(f"split-v1|{key}".encode("utf-8")).digest()
            value = int.from_bytes(digest[:8], "big") / float(1 << 64)
            if value < 0.70:
                return "train"
            if value < 0.85:
                return "val"
            return "test"

        for key in ("foto_20260720", "foto_20260724", "21683-21749", "x", "y"):
            self.assertEqual(erwartete_rolle(key), MODULE.split_role(key))

    def test_pilotenschwelle_bei_30(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            samples = []
            for index in range(29):
                frame = self._image(frames, f"f{index}.png", color=index + 1)
                samples.append(self._sample(f"s{index:02d}", frame, case_id=f"case-{index}"))

            audit29 = self._audit(root, samples, eval_images, negatives, registry)
            self.assertEqual([], audit29["piloten"])

            frame = self._image(frames, "f29.png", color=250)
            samples.append(self._sample("s29", frame, case_id="case-29"))
            audit30 = self._audit(root, samples, eval_images, negatives, registry)
            self.assertEqual(1, len(audit30["piloten"]))
            pilot = audit30["piloten"][0]
            self.assertEqual("BCC", pilot["code"])
            self.assertEqual(30, pilot["gesamt"])
            self.assertEqual(30, pilot["train"] + pilot["val"] + pilot["test"])

    def test_pilot_ohne_unabhaengige_val_test_gruppe_ist_nicht_auswertbar(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            samples = []
            for index in range(30):
                frame = self._image(frames, f"same-holding-{index}.png", color=index + 1)
                samples.append(
                    self._sample(
                        f"same-{index:02d}",
                        frame,
                        case_id="287425-81162",
                    )
                )

            audit = self._audit(root, samples, eval_images, negatives, registry)

            self.assertEqual([], audit["piloten"])
            self.assertEqual(1, len(audit["piloten_nicht_auswertbar"]))
            blocked = audit["piloten_nicht_auswertbar"][0]
            self.assertEqual("BCC", blocked["code"])
            self.assertEqual(30, blocked["gesamt"])
            self.assertEqual(1, sum(value > 0 for value in (
                blocked["train"], blocked["val"], blocked["test"]
            )))

    def test_bericht_wird_geschrieben_und_enthaelt_kernfelder(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            negative = negatives / "neg1.png"
            negative.write_bytes(
                frame.read_bytes() + b"x" * 2048
            )
            samples = [self._sample("ok", frame)]
            audit = self._audit(root, samples, eval_images, negatives, registry)

            reports = root / "reports"
            pfad = MODULE.write_report(
                audit, reports, datetime(2026, 7, 25, 12, 0, tzinfo=timezone.utc)
            )
            self.assertTrue(pfad.is_file())
            self.assertTrue(pfad.name.startswith("gold_stock_audit_"))
            dokument = json.loads(pfad.read_text(encoding="utf-8"))
            self.assertEqual("schreibfreie_pruefung", dokument["modus"])
            self.assertIn("samples_sha256", dokument["eingaben"])
            self.assertIn("registry_sha256", dokument["eingaben"])
            self.assertIn("zeitstempel_utc", dokument)
            self.assertEqual(1, dokument["negativ_pool"]["anzahl"])
            self.assertEqual(
                hashlib.sha256(negative.read_bytes()).hexdigest(),
                dokument["negativ_pool"]["dateien"][0]["sha256"],
            )

    def test_veroeffentlichte_negativsaetze_behalten_provenienz(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            legacy = self._image(negatives, "legacy.png", color=90)
            legacy.write_bytes(legacy.read_bytes() + b"x" * 2048)
            first_set = create_reviewed_negative_set(
                root,
                ("100-200", "300-400"),
                color_offset=10,
            )
            second_set = create_reviewed_negative_set(
                root,
                ("500-600", "700-800"),
                color_offset=80,
            )

            audit = self._audit(
                root,
                [self._sample("ok", frame)],
                eval_images,
                negatives,
                registry,
                negative_sets=(first_set, second_set),
            )

            self.assertEqual(5, audit["negativ_pool"]["anzahl"])
            self.assertEqual(2, len(audit["negativ_pool"]["sets"]))
            self.assertEqual(
                "diagnose_gemischt_nicht_exportierbar",
                audit["negativ_pool"]["registry_modus"],
            )
            self.assertEqual(
                [str(first_set.resolve()), str(second_set.resolve())],
                audit["eingaben"]["negative_set_pfade"],
            )
            strict = [
                item
                for item in audit["negativ_pool"]["dateien"]
                if item.get("source_type") == "reviewed_negative_set"
            ]
            self.assertEqual(4, len(strict))
            self.assertTrue(all(item["review_decision"] == "all_classes_clear" for item in strict))
            self.assertEqual(
                hashlib.sha256(legacy.read_bytes()).hexdigest(),
                next(
                    item["sha256"]
                    for item in audit["negativ_pool"]["dateien"]
                    if item.get("source_type") is None
                ),
            )
            for provenance in audit["negativ_pool"]["sets"]:
                self.assertIn("manifest_sha256", provenance)
                self.assertIn("review_sha256", provenance)
                self.assertIn("queue_manifest_sha256", provenance)
                self.assertIn("class_map_sha256", provenance)

    def test_expliziter_negativsatz_hat_keinen_stillen_fallback(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")

            with self.assertRaisesRegex(ValueError, "Negativsatz"):
                self._audit(
                    root,
                    [self._sample("ok", frame)],
                    eval_images,
                    negatives,
                    registry,
                    negative_sets=(
                        root / "training" / "negatives" / "sets" / "fehlt",
                    ),
                )

    def test_derselbe_negativsatz_darf_nicht_mehrfach_einfliessen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            negative_set = create_reviewed_negative_set(root)

            with self.assertRaisesRegex(ValueError, "mehrfach"):
                self._audit(
                    root,
                    [self._sample("ok", frame)],
                    eval_images,
                    negatives,
                    registry,
                    negative_sets=(negative_set, negative_set),
                )

    def test_negativbild_darf_nicht_im_eval_bestand_liegen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            negative = self._image(negatives, "legacy.png", color=91)
            negative.write_bytes(negative.read_bytes() + b"x" * 2048)
            (eval_images / "same.png").write_bytes(negative.read_bytes())

            with self.assertRaisesRegex(ValueError, "Eval-Bestand"):
                self._audit(
                    root,
                    [self._sample("ok", frame)],
                    eval_images,
                    negatives,
                    registry,
                )

    def test_manipulierter_review_beleg_wird_abgelehnt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            negative_set = create_reviewed_negative_set(root)
            review = negative_set / "receipts" / "review.json"
            review.write_bytes(review.read_bytes() + b"\n")

            with self.assertRaisesRegex(ValueError, "Review|Hash"):
                self._audit(
                    root,
                    [self._sample("ok", frame)],
                    eval_images,
                    negatives,
                    registry,
                    negative_sets=(negative_set,),
                )

    def test_manipulierte_bildbytes_und_klassenkarte_werden_abgelehnt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            negative_set = create_reviewed_negative_set(root)
            image = next((negative_set / "images").iterdir())
            image.write_bytes(image.read_bytes() + b"x")

            with self.assertRaisesRegex(ValueError, "Hash|Groesse"):
                self._audit(
                    root,
                    [self._sample("ok", frame)],
                    eval_images,
                    negatives,
                    registry,
                    negative_sets=(negative_set,),
                )

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            negative_set = create_reviewed_negative_set(root)
            class_map = negative_set / "receipts" / "class_map.json"
            class_map.write_bytes(class_map.read_bytes() + b"\n")

            with self.assertRaisesRegex(ValueError, "Hash|Klassenkarte"):
                self._audit(
                    root,
                    [self._sample("ok", frame)],
                    eval_images,
                    negatives,
                    registry,
                    negative_sets=(negative_set,),
                )

    def test_manifest_und_ordnername_sind_an_set_id_gebunden(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            negative_set = create_reviewed_negative_set(root)
            manifest_path = negative_set / "_manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["set_id"] = "f" * 64
            _write_fixture_json(manifest_path, manifest)

            with self.assertRaisesRegex(ValueError, "Negativsatz-ID"):
                self._audit(
                    root,
                    [self._sample("ok", frame)],
                    eval_images,
                    negatives,
                    registry,
                    negative_sets=(negative_set,),
                )

    def test_nicht_referenziertes_zusatzbild_wird_abgelehnt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            negative_set = create_reviewed_negative_set(root)
            extra = negative_set / "images" / "extra.png"
            Image.new("RGB", IMAGE_SIZE, (1, 2, 3)).save(extra)
            extra.write_bytes(extra.read_bytes() + b"x" * 2048)
            manifest_path = negative_set / "_manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["hashes"]["images/extra.png"] = {
                "sha256": hashlib.sha256(extra.read_bytes()).hexdigest(),
                "size_bytes": extra.stat().st_size,
            }
            manifest["hashes_count"] = len(manifest["hashes"])
            _write_fixture_json(manifest_path, manifest)

            with self.assertRaisesRegex(ValueError, "deckungsgleich"):
                self._audit(
                    root,
                    [self._sample("ok", frame)],
                    eval_images,
                    negatives,
                    registry,
                    negative_sets=(negative_set,),
                )

    def test_manipulierter_split_und_haltung_werden_abgelehnt(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            bad_split = create_reviewed_negative_set(
                root,
                invalid_split=True,
            )

            with self.assertRaisesRegex(ValueError, "Split"):
                self._audit(
                    root,
                    [self._sample("ok", frame)],
                    eval_images,
                    negatives,
                    registry,
                    negative_sets=(bad_split,),
                )

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frames, eval_images, negatives, registry = self._make_root(root)
            frame = self._image(frames, "a.png")
            bad_holding = create_reviewed_negative_set(
                root,
                holding_override="900-901",
            )

            with self.assertRaisesRegex(ValueError, "Haltung|Queue"):
                self._audit(
                    root,
                    [self._sample("ok", frame)],
                    eval_images,
                    negatives,
                    registry,
                    negative_sets=(bad_holding,),
                )

    def test_resolve_approved_by_cli_hat_vorrang(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "export_registry_v1.json"
            registry.write_text(
                json.dumps({"approved_by": "Besitzer"}), encoding="utf-8"
            )
            self.assertEqual(
                ("Jemand", "cli"), MODULE.resolve_approved_by("Jemand", registry)
            )
            self.assertEqual(
                ("Besitzer", "registry"), MODULE.resolve_approved_by(None, registry)
            )
            with self.assertRaises(ValueError):
                MODULE.resolve_approved_by(None, Path(temporary) / "fehlt.json")


if __name__ == "__main__":
    unittest.main()
