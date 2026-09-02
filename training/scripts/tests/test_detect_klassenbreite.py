from __future__ import annotations

import importlib.util
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).resolve().parents[1] / "detect_klassenbreite.py"
SPEC = importlib.util.spec_from_file_location("detect_klassenbreite", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class DatasetFixture:
    def __init__(self, root: Path, *, quoted_names: bool = False) -> None:
        self.dataset = root / "dataset"
        self.classes = ("KLASSE_A", "KLASSE_B", "KLASSE_C")
        for split in ("train", "val"):
            (self.dataset / "images" / split).mkdir(parents=True)
            (self.dataset / "labels" / split).mkdir(parents=True)

        (self.dataset / "images" / "train" / "eins.jpg").write_bytes(b"bild-eins")
        (self.dataset / "labels" / "train" / "eins.txt").write_text(
            "0 0.500000 0.500000 0.200000 0.200000\n"
            "2 0.250000 0.250000 0.100000 0.100000\n",
            encoding="utf-8",
        )
        (self.dataset / "images" / "train" / "zwei.PNG").write_bytes(b"bild-zwei")
        (self.dataset / "labels" / "train" / "zwei.txt").write_text(
            "1 0.500000 0.500000 0.200000 0.200000\n",
            encoding="utf-8",
        )
        (self.dataset / "images" / "val" / "drei.webp").write_bytes(b"bild-drei")
        (self.dataset / "labels" / "val" / "drei.txt").write_bytes(b"")
        (self.dataset / "classes.txt").write_text(
            "\n".join(self.classes) + "\n",
            encoding="utf-8",
        )
        names = (
            "  0: 'KLASSE_A'\n  1: \"KLASSE_B\"\n  2: KLASSE_C\n"
            if quoted_names
            else "  0: KLASSE_A\n  1: KLASSE_B\n  2: KLASSE_C\n"
        )
        (self.dataset / "data.yaml").write_text(
            "path: .\ntrain: images/train\nval: images/val\nnc: 3\nnames:\n" + names,
            encoding="utf-8",
        )

    def arguments(self, target: Path) -> list[str]:
        return [
            "--dataset",
            str(self.dataset),
            "--ziel",
            str(target),
            "--klassen",
            "KLASSE_C",
            "KLASSE_A",
        ]


def file_snapshot(root: Path) -> dict[str, bytes]:
    return {
        path.relative_to(root).as_posix(): path.read_bytes()
        for path in sorted(root.rglob("*"))
        if path.is_file()
    }


class DetectKlassenbreiteTests(unittest.TestCase):
    def test_gueltiger_lauf_prueft_zitierte_yaml_namen_und_bewahrt_quelle(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root, quoted_names=True)
            (fixture.dataset / "labels" / "train.cache").write_bytes(b"cache")
            target = root / "diagnostics" / "klassen_2"
            source_before = file_snapshot(fixture.dataset)

            result = MODULE.main(fixture.arguments(target))

            self.assertEqual(0, result)
            self.assertEqual(source_before, file_snapshot(fixture.dataset))
            self.assertEqual(
                {0: "KLASSE_A", 1: "KLASSE_C"},
                MODULE.lies_klassenkarte(target / "data.yaml"),
            )
            self.assertEqual(
                "0 0.500000 0.500000 0.200000 0.200000\n"
                "1 0.250000 0.250000 0.100000 0.100000\n",
                (target / "labels" / "train" / "eins.txt").read_text(encoding="utf-8"),
            )
            self.assertEqual(
                "",
                (target / "labels" / "train" / "zwei.txt").read_text(encoding="utf-8"),
            )
            report = json.loads(
                (target / "klassenbreite.json").read_text(encoding="utf-8")
            )
            self.assertTrue(Path(report["quelle"]).is_absolute())
            self.assertEqual(1, report["statistik"]["train"]["bilder_neu_negativ"])
            self.assertEqual(1, report["statistik"]["val"]["bilder_vorher_negativ"])
            self.assertFalse((target / MODULE.STAGING_MARKER_NAME).exists())

    def test_klassenkarte_sperrt_doppelte_lueckenhafte_und_widerspruechliche_daten(
        self,
    ) -> None:
        prefix = "path: .\ntrain: images/train\nval: images/val\n"
        cases = (
            (
                "doppelte Klassen-ID",
                "KLASSE_A\nKLASSE_B\nKLASSE_C\n",
                prefix + "nc: 3\nnames:\n  0: KLASSE_A\n  0: KLASSE_B\n  2: KLASSE_C\n",
                "Doppelte Klassen-ID",
            ),
            (
                "lueckenhafte Klassen-ID",
                "KLASSE_A\nKLASSE_B\nKLASSE_C\n",
                prefix + "nc: 3\nnames:\n  0: KLASSE_A\n  2: KLASSE_C\n",
                "entspricht nicht",
            ),
            (
                "abweichendes nc",
                "KLASSE_A\nKLASSE_B\nKLASSE_C\n",
                prefix + "nc: 2\nnames:\n  0: KLASSE_A\n  1: KLASSE_B\n",
                "entspricht nicht",
            ),
            (
                "doppelter Name in classes.txt",
                "KLASSE_A\nKLASSE_A\n",
                prefix + "nc: 2\nnames:\n  0: KLASSE_A\n  1: KLASSE_A\n",
                "Doppelte Klassennamen",
            ),
            (
                "nicht abgeschlossenes Zitat",
                "KLASSE_A\nKLASSE_B\n",
                prefix + "nc: 2\nnames:\n  0: KLASSE_A\n  1: 'KLASSE_B\n",
                "entspricht nicht",
            ),
            (
                "fremder Datenpfad",
                "KLASSE_A\nKLASSE_B\n",
                "path: ../fremd\ntrain: images/train\nval: images/val\n"
                "nc: 2\nnames:\n  0: KLASSE_A\n  1: KLASSE_B\n",
                "darf nur",
            ),
            (
                "zusaetzlicher Schluessel",
                "KLASSE_A\nKLASSE_B\n",
                prefix
                + "nc: 2\nnames:\n  0: KLASSE_A\n  1: KLASSE_B\nextra: nein\n",
                "darf nur",
            ),
        )
        for name, classes_text, yaml_text, expected_message in cases:
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                (root / "classes.txt").write_text(classes_text, encoding="utf-8")
                (root / "data.yaml").write_text(yaml_text, encoding="utf-8")

                with self.assertRaisesRegex(SystemExit, expected_message):
                    MODULE.lies_klassenkarte(root / "data.yaml")

    def test_bildordner_sperrt_fremde_dateien_ordner_und_verknuepfungen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            foreign = fixture.dataset / "images" / "train" / "Thumbs.db"
            foreign.write_bytes(b"fremd")
            with self.assertRaisesRegex(SystemExit, "Nicht unterstuetzte Datei"):
                MODULE.main(fixture.arguments(root / "target"))

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            (fixture.dataset / "images" / "train" / "unterordner").mkdir()
            with self.assertRaisesRegex(SystemExit, "Unerwarteter Ordner"):
                MODULE.main(fixture.arguments(root / "target"))

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            linked = fixture.dataset / "images" / "train" / "eins.jpg"
            original_check = MODULE._is_link_or_reparse

            def mark_link(path: Path) -> bool:
                return Path(path) == linked or original_check(Path(path))

            with mock.patch.object(MODULE, "_is_link_or_reparse", side_effect=mark_link):
                with self.assertRaisesRegex(SystemExit, "Verknuepfung oder Junction"):
                    MODULE.main(fixture.arguments(root / "target"))

    def test_datensatzwurzel_sperrt_fremde_datei(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            (fixture.dataset / "yolo26n.pt").write_bytes(b"fremd")

            with self.assertRaisesRegex(SystemExit, "yolo26n.pt"):
                MODULE.main(fixture.arguments(root / "target"))

    def test_fehlende_oder_verwaiste_labeldatei_stoppt_vor_ausgabe(self) -> None:
        for orphan in (False, True):
            with self.subTest(orphan=orphan), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                fixture = DatasetFixture(root)
                target = root / "target"
                if orphan:
                    (fixture.dataset / "labels" / "train" / "verwaist.txt").write_bytes(b"")
                    expected = "Labels ohne Bild"
                else:
                    (fixture.dataset / "labels" / "train" / "eins.txt").unlink()
                    expected = "Fehlende Labels"

                with self.assertRaisesRegex(SystemExit, expected):
                    MODULE.main(fixture.arguments(target))

                self.assertFalse(target.exists())
                self.assertEqual([], list(root.glob(".target.staging-*")))

    def test_ungueltige_labels_und_unbekannte_id_stoppen_vor_ausgabe(self) -> None:
        cases = (
            ("9 0.5 0.5 0.2 0.2\n", "Unbekannte Klassen-ID 9"),
            ("0 0.5 0.5 2.0 0.2\n", "BBox ausserhalb"),
            ("0 0.5 0.5 0.2\n", "Ungueltiges YOLO-Label"),
            ("x 0.5 0.5 0.2 0.2\n", "Ungueltige Klassen-ID"),
        )
        for label_text, expected in cases:
            with self.subTest(label_text=label_text), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                fixture = DatasetFixture(root)
                target = root / "target"
                (fixture.dataset / "labels" / "train" / "eins.txt").write_text(
                    label_text,
                    encoding="utf-8",
                )

                with self.assertRaisesRegex(SystemExit, expected):
                    MODULE.main(fixture.arguments(target))

                self.assertFalse(target.exists())
                self.assertEqual([], list(root.glob(".target.staging-*")))

    def test_fehler_raeumt_nur_eigenen_staging_ordner(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            target = root / "target"
            foreign_work = root / ".target.arbeit"
            foreign_work.mkdir()
            (foreign_work / "fremd.txt").write_text("behalten", encoding="utf-8")

            with mock.patch.object(
                MODULE.shutil,
                "copy2",
                side_effect=OSError("kopieren fehlgeschlagen"),
            ):
                with self.assertRaisesRegex(OSError, "kopieren fehlgeschlagen"):
                    MODULE.main(fixture.arguments(target))

            self.assertEqual(
                "behalten",
                (foreign_work / "fremd.txt").read_text(encoding="utf-8"),
            )
            self.assertFalse(target.exists())
            self.assertEqual([], list(root.glob(".target.staging-*")))

    def test_fremder_staging_ordner_ohne_passenden_marker_wird_nie_geloescht(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            token = "a" * 32
            staging = root / f".target.staging-{token}"
            staging.mkdir()
            (staging / MODULE.STAGING_MARKER_NAME).write_text(
                "fremder-token\n",
                encoding="ascii",
            )
            (staging / "fremd.txt").write_text("behalten", encoding="utf-8")

            with self.assertRaisesRegex(SystemExit, "Fremder Staging-Ordner"):
                MODULE._remove_owned_staging(staging, root, token)

            self.assertEqual(
                "behalten",
                (staging / "fremd.txt").read_text(encoding="utf-8"),
            )

    def test_belegtes_ziel_beim_finalen_rename_bleibt_unveraendert(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            target = root / "target"
            real_copy = MODULE.shutil.copy2
            target_created = False

            def copy_and_occupy(source: Path, destination: Path) -> Path:
                nonlocal target_created
                result = real_copy(source, destination)
                if not target_created:
                    target.mkdir()
                    (target / "fremd.txt").write_text("behalten", encoding="utf-8")
                    target_created = True
                return Path(result)

            with mock.patch.object(MODULE.shutil, "copy2", side_effect=copy_and_occupy):
                with self.assertRaisesRegex(SystemExit, "waehrend des Laufs belegt"):
                    MODULE.main(fixture.arguments(target))

            self.assertEqual(
                "behalten",
                (target / "fremd.txt").read_text(encoding="utf-8"),
            )
            self.assertEqual([], list(root.glob(".target.staging-*")))

    def test_ziel_im_quelldatensatz_wird_ohne_aenderung_abgewiesen(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixture = DatasetFixture(root)
            source_before = file_snapshot(fixture.dataset)
            target = fixture.dataset / "ausgabe"

            with self.assertRaisesRegex(SystemExit, "Quelldatensatz"):
                MODULE.main(fixture.arguments(target))

            self.assertFalse(target.exists())
            self.assertEqual(source_before, file_snapshot(fixture.dataset))


if __name__ == "__main__":
    unittest.main()
