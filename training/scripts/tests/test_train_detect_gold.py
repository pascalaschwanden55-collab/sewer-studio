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


MODULE_PATH = Path(__file__).resolve().parents[1] / "train_detect_gold.py"
SPEC = importlib.util.spec_from_file_location("train_detect_gold", MODULE_PATH)
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
    (dataset / "data.yaml").write_text("\n".join(lines) + "\n", encoding="utf-8")


class DatasetFixture:
    def __init__(
        self,
        temporary_root: Path,
        *,
        image_count: int = 30,
        train_count: int = 24,
        negative_indexes: set[int] | None = None,
    ) -> None:
        self.dataset_root = temporary_root / "training" / "datasets"
        self.plan_id = "a" * 64
        self.dataset = self.dataset_root / self.plan_id
        self.classes = list(MODULE.ACTIVE_CLASSES)
        self.negative_indexes = negative_indexes or set()
        self.manifest_images: list[dict[str, object]] = []
        instance_counts = {name: 0 for name in self.classes}
        train_holdings: list[str] = []
        validation_holdings: list[str] = []

        for split in ("train", "val"):
            (self.dataset / "images" / split).mkdir(parents=True, exist_ok=True)
            (self.dataset / "labels" / split).mkdir(parents=True, exist_ok=True)

        for index in range(image_count):
            split = "train" if index < train_count else "val"
            target = "train" if split == "train" else "validation"
            holding = f"{1000 + index}-{2000 + index}"
            if split == "train":
                train_holdings.append(holding)
            else:
                validation_holdings.append(holding)
            image_bytes = f"synthetic-image-{index}".encode("ascii")
            image_hash = hashlib.sha256(image_bytes).hexdigest()
            file_name = f"img_{image_hash}.jpg"
            image = self.dataset / "images" / split / file_name
            label = self.dataset / "labels" / split / f"img_{image_hash}.txt"
            image.write_bytes(image_bytes)

            labels: list[dict[str, object]] = []
            is_negative = index in self.negative_indexes
            if is_negative:
                label.write_bytes(b"")
            else:
                class_id = index % len(self.classes)
                label.write_text(
                    f"{class_id} 0.500000 0.500000 0.400000 0.400000\n",
                    encoding="utf-8",
                )
                instance_counts[self.classes[class_id]] += 1
                labels.append(
                    {
                        "class_id": class_id,
                        "class_name": self.classes[class_id],
                        "bounding_box": {
                            "x_center": 0.5,
                            "y_center": 0.5,
                            "width": 0.4,
                            "height": 0.4,
                            "is_valid": True,
                        },
                        "sources": [
                            {
                                "source_type": "training_sample",
                                "source_id": f"sample-{index}",
                                "stable_key": f"sample:sample-{index}",
                            }
                        ],
                    }
                )
            planned: dict[str, object] = {
                "image_sha256": image_hash,
                "holding_key": holding,
                "target": target,
                "target_file_name": file_name,
                "labels": labels,
            }
            if is_negative:
                planned["is_negative"] = True
            self.manifest_images.append(planned)

        self.manifest: dict[str, object] = {
            "schema_version": MODULE.EXPECTED_PLAN_SCHEMA_VERSION,
            "plan_id": self.plan_id,
            "generated_utc": "2026-07-30T12:00:00+00:00",
            "inventory_run_id": "inventory-run",
            "source_snapshot_hashes": {
                "training_samples.json": "b" * 64,
            },
            "class_map_version": MODULE.ACTIVE_CLASS_MAP_VERSION,
            "vsa_manifest_hash": MODULE.load_active_class_map().vsa_manifest_hash,
            "registry_hash": "c" * 64,
            "protected_sets": [
                {
                    "set_id": "eval-v1",
                    "role": "development_validation",
                    "manifest_sha256": "d" * 64,
                }
            ],
            "classes": self.classes,
            "train_holding_keys": train_holdings,
            "validation_holding_keys": validation_holdings,
            "instances_per_class": {
                name: count for name, count in instance_counts.items() if count > 0
            },
            "images": self.manifest_images,
            "exclusions": [],
        }
        self.write_control_files()

    def write_control_files(self) -> None:
        (self.dataset / "classes.txt").write_text(
            "\n".join(self.classes) + "\n",
            encoding="utf-8",
        )
        write_data_yaml(self.dataset, self.classes)
        (self.dataset / "manifest.json").write_text(
            json.dumps(self.manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        image_entries = []
        label_entries = []
        for split in ("train", "val"):
            for image in sorted((self.dataset / "images" / split).iterdir()):
                image_entries.append(
                    {
                        "path": image.relative_to(self.dataset).as_posix(),
                        "sha256": sha256(image),
                    }
                )
            for label in sorted((self.dataset / "labels" / split).iterdir()):
                label_entries.append(
                    {
                        "path": label.relative_to(self.dataset).as_posix(),
                        "sha256": sha256(label),
                    }
                )
        receipt = {
            "class_count": len(self.classes),
            "class_map_version": MODULE.ACTIVE_CLASS_MAP_VERSION,
            "classes_sha256": sha256(self.dataset / "classes.txt"),
            "data_yaml_sha256": sha256(self.dataset / "data.yaml"),
            "images": image_entries,
            "labels": label_entries,
            "manifest_sha256": sha256(self.dataset / "manifest.json"),
            "plan_id": self.plan_id,
            "plan_sha256": self.plan_id,
            "registry_hash": self.manifest["registry_hash"],
            "schema_version": MODULE.EXPECTED_PLAN_SCHEMA_VERSION,
            "total_samples": len(self.manifest_images),
            "train_count": sum(
                item["target"] == "train" for item in self.manifest_images
            ),
            "val_count": sum(
                item["target"] == "validation" for item in self.manifest_images
            ),
            "vsa_manifest_hash": self.manifest["vsa_manifest_hash"],
        }
        (self.dataset / "_export_receipt.json").write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )


class TrainDetectGoldTests(unittest.TestCase):
    def test_validates_all_15_classes_and_reviewed_negative(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = DatasetFixture(Path(temporary), negative_indexes={29})

            validated = MODULE.validate_dataset(
                fixture.dataset,
                fixture.dataset_root,
            )

            self.assertEqual(30, validated.image_count)
            self.assertEqual(24, validated.train_count)
            self.assertEqual(6, validated.validation_count)
            self.assertEqual(29, validated.instance_count)
            self.assertEqual(set(MODULE.ACTIVE_CLASSES), set(validated.instances_per_class))
            self.assertEqual(
                fixture.manifest["instances_per_class"],
                {
                    name: count
                    for name, count in validated.instances_per_class.items()
                    if count > 0
                },
            )

    def test_rejects_internally_rehashed_but_wrong_class_map(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = DatasetFixture(Path(temporary))
            fixture.classes[0], fixture.classes[1] = (
                fixture.classes[1],
                fixture.classes[0],
            )
            fixture.manifest["classes"] = fixture.classes
            fixture.write_control_files()

            with self.assertRaisesRegex(ValueError, "aktiven 15er-Klassenkarte"):
                MODULE.validate_dataset(fixture.dataset, fixture.dataset_root)

    def test_rejects_label_id_outside_zero_to_fourteen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = DatasetFixture(Path(temporary))
            first = fixture.manifest_images[0]
            file_name = str(first["target_file_name"])
            label = fixture.dataset / "labels" / "train" / f"{Path(file_name).stem}.txt"
            label.write_text(
                "15 0.500000 0.500000 0.400000 0.400000\n",
                encoding="utf-8",
            )
            fixture.write_control_files()

            with self.assertRaisesRegex(ValueError, "ausserhalb 0..14"):
                MODULE.validate_dataset(fixture.dataset, fixture.dataset_root)

    def test_rejects_manifest_instance_count_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = DatasetFixture(Path(temporary))
            counts = fixture.manifest["instances_per_class"]
            assert isinstance(counts, dict)
            counts[MODULE.ACTIVE_CLASSES[0]] += 1
            fixture.write_control_files()

            with self.assertRaisesRegex(ValueError, "Instanzzahlen"):
                MODULE.validate_dataset(fixture.dataset, fixture.dataset_root)

    def test_empty_label_requires_reviewed_negative_flag(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = DatasetFixture(Path(temporary), negative_indexes={29})
            fixture.manifest_images[29].pop("is_negative")
            fixture.write_control_files()

            with self.assertRaisesRegex(ValueError, "nicht als geprueftes Negativ"):
                MODULE.validate_dataset(fixture.dataset, fixture.dataset_root)

    def test_receipt_must_bind_every_file_and_all_control_hashes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = DatasetFixture(Path(temporary))
            receipt_path = fixture.dataset / "_export_receipt.json"
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            receipt["labels"].pop()
            receipt_path.write_text(json.dumps(receipt) + "\n", encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "nicht vollstaendig"):
                MODULE.validate_dataset(fixture.dataset, fixture.dataset_root)

        with tempfile.TemporaryDirectory() as temporary:
            fixture = DatasetFixture(Path(temporary))
            (fixture.dataset / "data.yaml").write_text(
                "path: C:/fremd\n"
                "train: images/train\n"
                "val: images/val\n"
                "nc: 15\n"
                "names:\n",
                encoding="utf-8",
            )
            receipt_path = fixture.dataset / "_export_receipt.json"
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            receipt["data_yaml_sha256"] = sha256(fixture.dataset / "data.yaml")
            receipt_path.write_text(json.dumps(receipt) + "\n", encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "data.yaml"):
                MODULE.validate_dataset(fixture.dataset, fixture.dataset_root)

        with tempfile.TemporaryDirectory() as temporary:
            fixture = DatasetFixture(Path(temporary))
            image = next((fixture.dataset / "images" / "train").iterdir())
            image.write_bytes(b"tampered")

            with self.assertRaisesRegex(ValueError, "veraendert"):
                MODULE.validate_dataset(fixture.dataset, fixture.dataset_root)

    def test_requires_minimum_total_and_both_splits(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = DatasetFixture(
                Path(temporary),
                image_count=29,
                train_count=23,
            )
            with self.assertRaisesRegex(ValueError, "zu klein oder unvollstaendig"):
                MODULE.validate_dataset(fixture.dataset, fixture.dataset_root)

        with tempfile.TemporaryDirectory() as temporary:
            fixture = DatasetFixture(
                Path(temporary),
                image_count=30,
                train_count=30,
            )
            with self.assertRaisesRegex(ValueError, "zu klein oder unvollstaendig"):
                MODULE.validate_dataset(fixture.dataset, fixture.dataset_root)

    def test_training_writes_isolated_not_deployed_candidate_with_safe_parameters(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root, negative_indexes={29})
            dataset = MODULE.validate_dataset(fixture.dataset, fixture.dataset_root)
            base_weights = root / "base.pt"
            base_weights.write_bytes(b"unchanged-base")
            train_calls: list[dict[str, object]] = []

            class FakeResult:
                results_dict = {"metrics/mAP50(B)": 0.5}

            class FakeYolo:
                def __init__(self, weights: str) -> None:
                    self.weights = weights

                def train(self, **arguments: object) -> FakeResult:
                    train_calls.append(arguments)
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
                    epochs=2,
                    patience=1,
                    candidate_tag="mixed",
                )

            self.assertEqual(
                f"detect_gold_{fixture.plan_id[:12]}_mixed",
                candidate_root.name,
            )
            self.assertEqual(b"unchanged-base", base_weights.read_bytes())
            self.assertEqual(1, len(train_calls))
            call = train_calls[0]
            self.assertEqual(1280, call["imgsz"])
            self.assertEqual(3, call["batch"])
            self.assertEqual(0.0, call["flipud"])
            self.assertEqual(0.0, call["fliplr"])
            self.assertEqual(0.01, call["hsv_h"])
            self.assertEqual(0.3, call["hsv_s"])
            self.assertEqual(0.3, call["hsv_v"])
            self.assertEqual(1, call["patience"])
            self.assertFalse(call["exist_ok"])

            candidate_manifest = json.loads(
                (candidate_root / "candidate_manifest.json").read_text(
                    encoding="utf-8"
                )
            )
            self.assertEqual("not_deployed", candidate_manifest["candidate_status"])
            self.assertEqual("detect_gold", candidate_manifest["candidate_kind"])
            self.assertEqual(dataset.plan_id, candidate_manifest["dataset"]["plan_id"])
            self.assertEqual(
                dataset.receipt_sha256,
                candidate_manifest["dataset"]["receipt_sha256"],
            )
            self.assertEqual(
                dataset.data_yaml_sha256,
                candidate_manifest["dataset"]["data_yaml_sha256"],
            )
            self.assertEqual(
                dataset.classes_sha256,
                candidate_manifest["dataset"]["classes_sha256"],
            )
            self.assertEqual(
                dataset.instances_per_class,
                candidate_manifest["dataset"]["instances_per_class"],
            )

    def test_resource_gate_never_stops_sidecar_and_requires_28000_mb(self) -> None:
        with mock.patch.object(MODULE, "sidecar_running", return_value=True):
            with self.assertRaisesRegex(RuntimeError, "Sidecar laeuft"):
                MODULE.ensure_training_resources()

        with (
            mock.patch.object(MODULE, "sidecar_running", return_value=False),
            mock.patch.object(MODULE, "gpu_free_vram_mb", return_value=27_999),
        ):
            with self.assertRaisesRegex(RuntimeError, "28000"):
                MODULE.ensure_training_resources()


if __name__ == "__main__":
    unittest.main()
