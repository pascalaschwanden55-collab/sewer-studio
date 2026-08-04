from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).resolve().parents[1] / "train_bcc_pilot.py"
SPEC = importlib.util.spec_from_file_location("train_bcc_pilot", MODULE_PATH)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_data_yaml(dataset: Path, classes: list[str]) -> None:
    lines = [
        "path: .",
        "train: images/train",
        "val: images/val",
        f"nc: {len(classes)}",
        "names:",
        *(f"  {index}: {name}" for index, name in enumerate(classes)),
    ]
    (dataset / "data.yaml").write_text(
        "\n".join(lines) + "\n",
        encoding="utf-8",
    )


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
                receipt_sha256="c" * 64,
                data_yaml_sha256="d" * 64,
                classes_sha256="e" * 64,
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
            write_data_yaml(dataset, classes)
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
                "classes_sha256": sha256(dataset / "classes.txt"),
                "data_yaml_sha256": sha256(dataset / "data.yaml"),
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
            self.assertEqual(
                sha256(dataset / "_export_receipt.json"),
                validated.receipt_sha256,
            )
            self.assertEqual(sha256(dataset / "data.yaml"), validated.data_yaml_sha256)
            self.assertEqual(
                sha256(dataset / "classes.txt"),
                validated.classes_sha256,
            )

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
            write_data_yaml(dataset, classes)
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
                "classes_sha256": sha256(dataset / "classes.txt"),
                "data_yaml_sha256": sha256(dataset / "data.yaml"),
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

    def test_receipt_binds_data_yaml_and_classes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            dataset = Path(temporary) / "dataset"
            dataset.mkdir()
            manifest = dataset / "manifest.json"
            data_yaml = dataset / "data.yaml"
            classes_path = dataset / "classes.txt"
            manifest.write_text("{}\n", encoding="utf-8")
            data_yaml.write_text(
                "path: .\ntrain: images/train\nval: images/val\nnc: 1\nnames:\n"
                "  0: BCC_bogen\n",
                encoding="utf-8",
            )
            classes_path.write_text("BCC_bogen\n", encoding="utf-8")
            receipt = {
                "manifest_sha256": sha256(manifest),
                "data_yaml_sha256": sha256(data_yaml),
                "classes_sha256": sha256(classes_path),
                "images": [],
                "labels": [],
            }
            (dataset / "_export_receipt.json").write_text(
                json.dumps(receipt) + "\n",
                encoding="utf-8",
            )

            MODULE._validate_receipt(dataset, manifest, data_yaml, classes_path)

            data_yaml.write_text("path: C:/fremd\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "data.yaml"):
                MODULE._validate_receipt(dataset, manifest, data_yaml, classes_path)

            data_yaml.write_text(
                "path: .\ntrain: images/train\nval: images/val\nnc: 1\nnames:\n"
                "  0: BCC_bogen\n",
                encoding="utf-8",
            )
            classes_path.write_text("andere_klasse\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "classes.txt"):
                MODULE._validate_receipt(dataset, manifest, data_yaml, classes_path)

    def test_data_yaml_accepts_only_canonical_local_targets(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            data_yaml = Path(temporary) / "data.yaml"
            canonical = (
                "path: .\n"
                "train: images/train\n"
                "val: images/val\n"
                "nc: 15\n"
                "names:\n"
                "  14: BCC_bogen\n"
            )
            data_yaml.write_text(canonical, encoding="utf-8")

            MODULE._validate_data_yaml(data_yaml, 15)

            invalid_documents = (
                canonical.replace("path: .", "path: C:/fremd"),
                canonical.replace("train: images/train", "train: ../images/train"),
                canonical + "test: images/test\n",
                canonical.replace("val: images/val", "val: images/val\nval: images/train"),
            )
            for document in invalid_documents:
                with self.subTest(document=document):
                    data_yaml.write_text(document, encoding="utf-8")
                    with self.assertRaises(ValueError):
                        MODULE._validate_data_yaml(data_yaml, 15)

    def test_candidate_manifest_binds_all_dataset_hashes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            dataset_root = root / "dataset"
            (dataset_root / "labels" / "train").mkdir(parents=True)
            (dataset_root / "labels" / "val").mkdir(parents=True)
            classes = [f"class_{index}" for index in range(14)] + ["BCC_bogen"]
            (dataset_root / "classes.txt").write_text(
                "\n".join(classes) + "\n",
                encoding="utf-8",
            )
            dataset = MODULE.ValidatedDataset(
                root=dataset_root,
                data_yaml=dataset_root / "data.yaml",
                manifest=dataset_root / "manifest.json",
                plan_id="f" * 64,
                image_count=30,
                train_count=24,
                validation_count=6,
                instance_count=30,
                manifest_sha256="1" * 64,
                receipt_sha256="2" * 64,
                data_yaml_sha256="3" * 64,
                classes_sha256="4" * 64,
            )
            base_weights = root / "base.pt"
            base_weights.write_bytes(b"base")

            class FakeResult:
                results_dict = {"metric": 0.5}

            class FakeYolo:
                def __init__(self, weights: str) -> None:
                    self.weights = weights

                def train(self, **arguments: object) -> FakeResult:
                    run_root = Path(str(arguments["project"])) / "run"
                    (run_root / "weights").mkdir(parents=True)
                    (run_root / "weights" / "best.pt").write_bytes(b"candidate")
                    (run_root / "results.csv").write_text(
                        "epoch,metric\n0,0.5\n",
                        encoding="utf-8",
                    )
                    return FakeResult()

            fake_ultralytics = types.SimpleNamespace(YOLO=FakeYolo)
            with (
                mock.patch.dict(sys.modules, {"ultralytics": fake_ultralytics}),
                mock.patch.object(
                    MODULE,
                    "ensure_training_resources",
                    return_value=30_000,
                ),
            ):
                candidate_root = MODULE.train(
                    dataset,
                    base_weights,
                    root / "candidates",
                    epochs=1,
                    patience=0,
                    candidate_tag=None,
                )

            candidate_manifest = json.loads(
                (candidate_root / "candidate_manifest.json").read_text(
                    encoding="utf-8"
                )
            )
            self.assertEqual(
                {
                    "plan_id": dataset.plan_id,
                    "manifest_sha256": dataset.manifest_sha256,
                    "receipt_sha256": dataset.receipt_sha256,
                    "data_yaml_sha256": dataset.data_yaml_sha256,
                    "classes_sha256": dataset.classes_sha256,
                    "images": dataset.image_count,
                    "train_images": dataset.train_count,
                    "validation_images": dataset.validation_count,
                    "instances": dataset.instance_count,
                },
                candidate_manifest["dataset"],
            )


if __name__ == "__main__":
    unittest.main()
