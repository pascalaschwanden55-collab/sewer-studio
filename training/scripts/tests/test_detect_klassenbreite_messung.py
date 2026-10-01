from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).resolve().parents[1] / "detect_klassenbreite_messung.py"
SPEC = importlib.util.spec_from_file_location("detect_klassenbreite_messung", MODULE_PATH)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class DatasetFixture:
    def __init__(self, root: Path) -> None:
        self.dataset = root / "dataset"
        self.classes = ("klasse_a", "klasse_b", "klasse_ohne_sollbox")
        for split in ("train", "val"):
            (self.dataset / "images" / split).mkdir(parents=True)
            (self.dataset / "labels" / split).mkdir(parents=True)
            (self.dataset / "images" / split / f"{split}.jpg").write_bytes(
                f"bild-{split}".encode("ascii")
            )

        (self.dataset / "labels" / "train" / "train.txt").write_text(
            "0 0.5 0.5 0.2 0.2\n",
            encoding="utf-8",
        )
        (self.dataset / "labels" / "val" / "val.txt").write_text(
            "0 0.5 0.5 0.2 0.2\n1 0.4 0.4 0.1 0.1\n",
            encoding="utf-8",
        )
        (self.dataset / "classes.txt").write_text(
            "\n".join(self.classes) + "\n",
            encoding="utf-8",
        )
        (self.dataset / "data.yaml").write_text(
            "path: .\n"
            "train: images/train\n"
            "val: images/val\n"
            "nc: 3\n"
            "names:\n"
            "  0: klasse_a\n"
            "  1: klasse_b\n"
            "  2: klasse_ohne_sollbox\n",
            encoding="utf-8",
        )
        (self.dataset / "_export_receipt.json").write_text(
            '{"plan_id":"test"}\n',
            encoding="utf-8",
        )


class FakeBox:
    ap_class_index = [0, 1]
    p = [0.11114, 0.22225]
    r = [0.33336, 0.44447]
    ap50 = [0.55558, 0.66669]
    ap = [0.12345, 0.23456]


class FakeResult:
    box = FakeBox()


class DetectKlassenbreiteMessungTests(unittest.TestCase):
    def test_messmodus_nutzt_absolute_pfade_fp32_und_laesst_dataset_sauber(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            knowledge_root = root / "knowledge"
            weight = root / "weight.pt"
            weight.write_bytes(b"messgewicht")
            calls: list[tuple[str, object]] = []
            constructor_cwds: list[Path] = []
            runtime_yaml_text: list[str] = []

            class FakeYolo:
                def __init__(self, weights: str) -> None:
                    calls.append(("init", weights))
                    constructor_cwds.append(Path.cwd())
                    (Path.cwd() / "yolo26n.pt").write_bytes(b"download")
                    self.names = dict(enumerate(fixture.classes))

                def val(self, **arguments: object) -> FakeResult:
                    calls.append(("val", arguments))
                    runtime_yaml = Path(str(arguments["data"]))
                    runtime_yaml_text.append(runtime_yaml.read_text(encoding="utf-8"))
                    (Path(str(arguments["project"])) / str(arguments["name"])).mkdir(
                        parents=True
                    )
                    return FakeResult()

            fake_ultralytics = types.SimpleNamespace(YOLO=FakeYolo)
            vorher = Path.cwd()
            os.chdir(root)
            try:
                with (
                    mock.patch.dict(sys.modules, {"ultralytics": fake_ultralytics}),
                    mock.patch.object(
                        MODULE,
                        "sewerstudio_laeuft",
                        side_effect=AssertionError("Messmodus darf Prozess nicht pruefen"),
                    ),
                    mock.patch.object(
                        MODULE.train_detect_gold,
                        "ensure_training_resources",
                        side_effect=AssertionError("Messmodus darf VRAM nicht pruefen"),
                    ),
                ):
                    result = MODULE.main(
                        [
                            "--dataset",
                            "dataset",
                            "--name",
                            "referenz_15_fp32_b4",
                            "--gewicht",
                            "weight.pt",
                            "--knowledge-root",
                            "knowledge",
                        ]
                    )
                self.assertEqual(root, Path.cwd())
            finally:
                os.chdir(vorher)

            self.assertEqual(0, result)
            self.assertEqual([("init", str(weight.resolve()))], calls[:1])
            val_call = next(value for name, value in calls if name == "val")
            assert isinstance(val_call, dict)
            self.assertTrue(Path(str(val_call["data"])).is_absolute())
            self.assertTrue(Path(str(val_call["project"])).is_absolute())
            self.assertEqual(4, val_call["batch"])
            self.assertIs(False, val_call["half"])
            self.assertIs(False, val_call["exist_ok"])
            self.assertEqual("referenz_15_fp32_b4_val", val_call["name"])
            self.assertIn(f'path: "{fixture.dataset.resolve().as_posix()}"', runtime_yaml_text[0])
            self.assertTrue(all(cwd != fixture.dataset for cwd in constructor_cwds))
            self.assertFalse((fixture.dataset / "yolo26n.pt").exists())

            report_path = (
                knowledge_root
                / "training"
                / "diagnostics"
                / "referenz_15_fp32_b4_klassenwerte.json"
            )
            report = json.loads(report_path.read_text(encoding="utf-8"))
            self.assertEqual("detect_klassenbreite_messung_v2", report["schema"])
            self.assertEqual(str(fixture.dataset.resolve()), report["datensatz"])
            self.assertEqual(sha256(weight), report["herkunft"]["gewicht_sha256"])
            self.assertEqual(
                sha256(fixture.dataset / "_export_receipt.json"),
                report["datensatz_belege"]["export_receipt"]["sha256"],
            )
            self.assertEqual(0.5556, report["klassen"]["klasse_a"]["ap50"])
            self.assertEqual(0.6667, report["klassen"]["klasse_b"]["ap50"])
            self.assertEqual(
                {
                    "soll_boxen_val": 0,
                    "precision": None,
                    "recall": None,
                    "ap50": None,
                    "ap50_95": None,
                    "grund": "0 Soll-Boxen in val",
                },
                report["klassen"]["klasse_ohne_sollbox"],
            )

    def test_training_prueft_prozess_und_vram_und_misst_best_pt_neu(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            knowledge_root = root / "knowledge"
            base = root / "base.pt"
            base.write_bytes(b"basis")
            train_calls: list[dict[str, object]] = []
            val_calls: list[dict[str, object]] = []
            loaded_weights: list[Path] = []
            cache_state_before_train: list[bool] = []

            class FakeYolo:
                def __init__(self, weights: str) -> None:
                    loaded_weights.append(Path(weights))
                    self.names = {0: "fremde_basis_klasse"}

                def train(self, **arguments: object) -> FakeResult:
                    train_calls.append(arguments)
                    cache_state_before_train.append(
                        (fixture.dataset / "labels" / "train.cache").exists()
                    )
                    (fixture.dataset / "labels" / "train.cache").write_bytes(b"cache")
                    (fixture.dataset / "labels" / "val.cache").write_bytes(b"cache")
                    best = (
                        Path(str(arguments["project"]))
                        / str(arguments["name"])
                        / "weights"
                        / "best.pt"
                    )
                    best.parent.mkdir(parents=True)
                    best.write_bytes(b"best")
                    return FakeResult()

                def val(self, **arguments: object) -> FakeResult:
                    val_calls.append(arguments)
                    (Path(str(arguments["project"])) / str(arguments["name"])).mkdir(
                        parents=True
                    )
                    return FakeResult()

            (fixture.dataset / "labels" / "train.cache").write_bytes(b"alt")
            (fixture.dataset / "labels" / "val.cache").write_bytes(b"alt")
            process_check = mock.Mock(return_value=False)
            resource_check = mock.Mock(return_value=31_234)
            with (
                mock.patch.dict(
                    sys.modules,
                    {"ultralytics": types.SimpleNamespace(YOLO=FakeYolo)},
                ),
                mock.patch.object(MODULE, "sewerstudio_laeuft", process_check),
                mock.patch.object(
                    MODULE.train_detect_gold,
                    "ensure_training_resources",
                    resource_check,
                ),
            ):
                result = MODULE.main(
                    [
                        "--dataset",
                        str(fixture.dataset),
                        "--name",
                        "klassen_5_fp32_b4",
                        "--basisgewicht",
                        str(base),
                        "--knowledge-root",
                        str(knowledge_root),
                    ]
                )

            self.assertEqual(0, result)
            process_check.assert_called_once_with()
            resource_check.assert_called_once_with()
            self.assertEqual([False], cache_state_before_train)
            self.assertEqual(1, len(train_calls))
            self.assertEqual(1, len(val_calls))
            self.assertEqual(base.resolve(), loaded_weights[0])
            best = (
                knowledge_root
                / "training"
                / "cls_runs"
                / "klassen_5_fp32_b4"
                / "weights"
                / "best.pt"
            )
            self.assertEqual(best.resolve(), loaded_weights[1])
            self.assertIs(False, train_calls[0]["exist_ok"])
            self.assertEqual(0.0, train_calls[0]["flipud"])
            self.assertEqual(0.5, train_calls[0]["fliplr"])
            self.assertEqual(0.015, train_calls[0]["hsv_h"])
            self.assertEqual(0.7, train_calls[0]["hsv_s"])
            self.assertEqual(0.4, train_calls[0]["hsv_v"])
            self.assertEqual(1.0, train_calls[0]["mosaic"])
            self.assertEqual(4, val_calls[0]["batch"])
            self.assertIs(False, val_calls[0]["half"])
            self.assertIs(False, val_calls[0]["exist_ok"])
            self.assertFalse((fixture.dataset / "labels" / "train.cache").exists())
            self.assertFalse((fixture.dataset / "labels" / "val.cache").exists())

            report_path = (
                knowledge_root
                / "training"
                / "diagnostics"
                / "klassen_5_fp32_b4_klassenwerte.json"
            )
            report = json.loads(report_path.read_text(encoding="utf-8"))
            self.assertEqual("trainiert", report["herkunft"]["art"])
            self.assertEqual(31_234, report["herkunft"]["freier_vram_mb_vor_start"])
            self.assertEqual(sha256(base), report["herkunft"]["basisgewicht_sha256"])
            self.assertEqual(sha256(best), report["herkunft"]["gewicht_sha256"])
            self.assertEqual(4, report["messung"]["batch"])
            self.assertIs(False, report["messung"]["half"])

    def test_sewerstudio_sperrt_nur_den_trainingszweig_vor_jeder_ausgabe(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            base = root / "base.pt"
            base.write_bytes(b"basis")
            knowledge_root = root / "knowledge"
            resource_check = mock.Mock()

            with (
                mock.patch.object(MODULE, "sewerstudio_laeuft", return_value=True),
                mock.patch.object(
                    MODULE.train_detect_gold,
                    "ensure_training_resources",
                    resource_check,
                ),
            ):
                with self.assertRaisesRegex(RuntimeError, "SewerStudio.exe laeuft"):
                    MODULE.main(
                        [
                            "--dataset",
                            str(fixture.dataset),
                            "--name",
                            "klassen_2_fp32_b4",
                            "--basisgewicht",
                            str(base),
                            "--knowledge-root",
                            str(knowledge_root),
                        ]
                    )

            resource_check.assert_not_called()
            self.assertFalse(knowledge_root.exists())

    def test_falsche_gewichtsklassen_stoppen_vor_der_messung(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            weight = root / "weight.pt"
            weight.write_bytes(b"gewicht")
            val_call = mock.Mock()

            class FakeYolo:
                names = {0: "klasse_b", 1: "klasse_a", 2: "klasse_ohne_sollbox"}

                def __init__(self, _weights: str) -> None:
                    pass

                val = val_call

            with mock.patch.dict(
                sys.modules,
                {"ultralytics": types.SimpleNamespace(YOLO=FakeYolo)},
            ):
                with self.assertRaisesRegex(RuntimeError, "passt nicht zum Datensatz"):
                    MODULE.main(
                        [
                            "--dataset",
                            str(fixture.dataset),
                            "--name",
                            "referenz_15_fp32_b4",
                            "--gewicht",
                            str(weight),
                            "--knowledge-root",
                            str(root / "knowledge"),
                        ]
                    )

            val_call.assert_not_called()

    def test_trainingsfehler_stellt_cwd_wieder_her_und_entfernt_caches(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            base = root / "base.pt"
            base.write_bytes(b"basis")
            vorher = Path.cwd()

            class FakeYolo:
                names = {}

                def __init__(self, _weights: str) -> None:
                    pass

                def train(self, **_arguments: object) -> FakeResult:
                    (fixture.dataset / "labels" / "train.cache").write_bytes(b"cache")
                    (fixture.dataset / "labels" / "val.cache").write_bytes(b"cache")
                    raise RuntimeError("simulierter Trainingsfehler")

            with (
                mock.patch.dict(
                    sys.modules,
                    {"ultralytics": types.SimpleNamespace(YOLO=FakeYolo)},
                ),
                mock.patch.object(MODULE, "sewerstudio_laeuft", return_value=False),
                mock.patch.object(
                    MODULE.train_detect_gold,
                    "ensure_training_resources",
                    return_value=30_000,
                ),
            ):
                with self.assertRaisesRegex(RuntimeError, "simulierter Trainingsfehler"):
                    MODULE.main(
                        [
                            "--dataset",
                            str(fixture.dataset),
                            "--name",
                            "klassen_5_fehler",
                            "--basisgewicht",
                            str(base),
                            "--knowledge-root",
                            str(root / "knowledge"),
                        ]
                    )

            self.assertEqual(vorher, Path.cwd())
            self.assertFalse((fixture.dataset / "labels" / "train.cache").exists())
            self.assertFalse((fixture.dataset / "labels" / "val.cache").exists())

    def test_unsicherer_cachepfad_wird_nicht_geloescht(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            cache = fixture.dataset / "labels" / "train.cache"
            cache.write_bytes(b"behalten")
            echte_pruefung = MODULE.dataset_guard._is_link_or_reparse

            def markiere_cache(pfad: Path) -> bool:
                return Path(pfad) == cache or echte_pruefung(Path(pfad))

            with mock.patch.object(
                MODULE.dataset_guard,
                "_is_link_or_reparse",
                side_effect=markiere_cache,
            ):
                with self.assertRaisesRegex(RuntimeError, "Verknuepfung"):
                    MODULE._entferne_ultralytics_caches(fixture.dataset)

            self.assertEqual(b"behalten", cache.read_bytes())

    def test_ungueltige_namen_und_bestehende_laufordner_werden_abgewiesen(self) -> None:
        for invalid in ("../lauf", "Lauf", "name mit leerzeichen", "a" * 81):
            with self.subTest(invalid=invalid):
                with self.assertRaisesRegex(SystemExit, "Ungueltiger Laufname"):
                    MODULE._pruefe_laufname(invalid)

        for valid in (
            "referenz_15_fp32_b4",
            "klassen_5_fp32_b4",
            "klassen_2_fp32_b4",
        ):
            self.assertEqual(valid, MODULE._pruefe_laufname(valid))

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            runs = root / "runs"
            report = root / "report.json"
            (runs / "frei_val").mkdir(parents=True)
            with self.assertRaisesRegex(SystemExit, "Laufordner existiert bereits"):
                MODULE._pruefe_ausgabeziele("frei", runs, report)

            (runs / "frei_val").rmdir()
            (runs / "frei").mkdir()
            with self.assertRaisesRegex(SystemExit, "Laufordner existiert bereits"):
                MODULE._pruefe_ausgabeziele("frei", runs, report)


if __name__ == "__main__":
    unittest.main()
