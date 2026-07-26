from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "train_bcc_pilot.py"
SPEC = importlib.util.spec_from_file_location("train_bcc_pilot", MODULE_PATH)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class TrainBccPilotTests(unittest.TestCase):
    def test_removes_only_generated_label_caches(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "dataset"
            (root / "labels" / "train").mkdir(parents=True)
            (root / "labels" / "val").mkdir(parents=True)
            (root / "labels" / "train.cache").write_bytes(b"cache")
            (root / "labels" / "val.cache").write_bytes(b"cache")
            keep = root / "labels" / "train" / "image.txt"
            keep.write_text("14 0.5 0.5 0.2 0.2\n")
            dataset = MODULE.ValidatedDataset(
                root=root,
                data_yaml=root / "data.yaml",
                manifest=root / "manifest.json",
                plan_id="a" * 64,
                image_count=30,
                train_count=24,
                validation_count=6,
                instance_count=30,
                manifest_sha256="b" * 64,
            )

            MODULE._remove_ultralytics_label_caches(dataset)

            self.assertFalse((root / "labels" / "train.cache").exists())
            self.assertFalse((root / "labels" / "val.cache").exists())
            self.assertTrue(keep.is_file())

    def test_validates_approved_export_and_rejects_foreign_label(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            dataset_root = Path(temporary) / "training" / "datasets"
            plan_id = "a" * 64
            dataset = dataset_root / plan_id
            classes = [f"class_{index}" for index in range(14)] + ["BCC_bogen"]
            receipt_files: dict[str, list[dict[str, str]]] = {
                "images": [],
                "labels": [],
            }
            images: list[dict[str, object]] = []
            for index in range(30):
                split = "train" if index < 24 else "val"
                image = dataset / "images" / split / f"image_{index}.jpg"
                label = dataset / "labels" / split / f"image_{index}.txt"
                image.parent.mkdir(parents=True, exist_ok=True)
                label.parent.mkdir(parents=True, exist_ok=True)
                image.write_bytes(b"\xff\xd8\xff" + bytes([index]))
                label.write_text("14 0.500000 0.500000 0.400000 0.400000\n")
                receipt_files["images"].append(
                    {
                        "path": image.relative_to(dataset).as_posix(),
                        "sha256": sha256(image),
                    }
                )
                receipt_files["labels"].append(
                    {
                        "path": label.relative_to(dataset).as_posix(),
                        "sha256": sha256(label),
                    }
                )
                images.append({"image_sha256": sha256(image)})

            (dataset / "classes.txt").write_text("\n".join(classes) + "\n")
            (dataset / "data.yaml").write_text("path: .\n")
            manifest = {
                "plan_id": plan_id,
                "classes": classes,
                "instances_per_class": {"BCC_bogen": 30},
                "images": images,
            }
            (dataset / "manifest.json").write_text(
                json.dumps(manifest) + "\n",
                encoding="utf-8",
            )
            receipt = {
                "plan_id": plan_id,
                "manifest_sha256": sha256(dataset / "manifest.json"),
                "total_samples": 30,
                **receipt_files,
            }
            (dataset / "_export_receipt.json").write_text(
                json.dumps(receipt) + "\n",
                encoding="utf-8",
            )

            validated = MODULE.validate_dataset(dataset, dataset_root)

            self.assertEqual(30, validated.image_count)
            self.assertEqual(24, validated.train_count)
            self.assertEqual(6, validated.validation_count)
            self.assertEqual(30, validated.instance_count)

            foreign_label = dataset / "labels" / "train" / "image_0.txt"
            foreign_label.write_text("0 0.500000 0.500000 0.400000 0.400000\n")
            receipt_files["labels"][0]["sha256"] = sha256(foreign_label)
            (dataset / "_export_receipt.json").write_text(
                json.dumps(receipt) + "\n",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(ValueError, "fremde Klasse"):
                MODULE.validate_dataset(dataset, dataset_root)

    def test_empty_label_file_is_a_valid_negative(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            label = Path(temporary) / "neg.txt"
            label.write_bytes(b"")
            self.assertEqual(0, MODULE._validate_label_file(label))
            label.write_text("14 0.5 0.5 0.2 0.2\n")
            self.assertEqual(1, MODULE._validate_label_file(label))

    def test_dataset_with_empty_label_file_passes_but_missing_label_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            dataset_root = Path(temporary) / "training" / "datasets"
            plan_id = "c" * 64
            dataset = dataset_root / plan_id
            classes = [f"class_{index}" for index in range(14)] + ["BCC_bogen"]
            receipt_files: dict[str, list[dict[str, str]]] = {"images": [], "labels": []}
            images: list[dict[str, object]] = []
            for index in range(30):
                split = "train" if index < 24 else "val"
                image = dataset / "images" / split / f"image_{index}.jpg"
                label = dataset / "labels" / split / f"image_{index}.txt"
                image.parent.mkdir(parents=True, exist_ok=True)
                label.parent.mkdir(parents=True, exist_ok=True)
                image.write_bytes(b"\xff\xd8\xff" + bytes([index]))
                # Bild 7 ist ein kuratiertes Negativ: bewusst LEERE Labeldatei.
                if index == 7:
                    label.write_bytes(b"")
                else:
                    label.write_text("14 0.500000 0.500000 0.400000 0.400000\n")
                receipt_files["images"].append(
                    {"path": image.relative_to(dataset).as_posix(), "sha256": sha256(image)}
                )
                receipt_files["labels"].append(
                    {"path": label.relative_to(dataset).as_posix(), "sha256": sha256(label)}
                )
                images.append({"image_sha256": sha256(image)})

            (dataset / "classes.txt").write_text("\n".join(classes) + "\n")
            (dataset / "data.yaml").write_text("path: .\n")
            manifest = {
                "plan_id": plan_id,
                "classes": classes,
                "instances_per_class": {"BCC_bogen": 29},
                "images": images,
            }
            (dataset / "manifest.json").write_text(json.dumps(manifest) + "\n", encoding="utf-8")
            receipt = {
                "plan_id": plan_id,
                "manifest_sha256": sha256(dataset / "manifest.json"),
                "total_samples": 30,
                **receipt_files,
            }
            (dataset / "_export_receipt.json").write_text(
                json.dumps(receipt) + "\n", encoding="utf-8"
            )

            validated = MODULE.validate_dataset(dataset, dataset_root)

            self.assertEqual(30, validated.image_count)
            self.assertEqual(29, validated.instance_count)

            # Ein Positivbild OHNE jede Labeldatei bleibt ein harter Fehler:
            # Datei entfernen und den Beleg konsistent nachziehen.
            missing = dataset / "labels" / "train" / "image_3.txt"
            receipt["labels"] = [
                entry
                for entry in receipt["labels"]
                if not entry["path"].endswith("image_3.txt")
            ]
            missing.unlink()
            (dataset / "_export_receipt.json").write_text(
                json.dumps(receipt) + "\n", encoding="utf-8"
            )
            with self.assertRaisesRegex(ValueError, "nicht ueberein"):
                MODULE.validate_dataset(dataset, dataset_root)


if __name__ == "__main__":
    unittest.main()
