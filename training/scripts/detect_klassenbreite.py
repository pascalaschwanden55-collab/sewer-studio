"""Misst, ob die schwach belegten Klassen die gut belegten verwaessern.

DIE FRAGE
Der Mehrklassen-Kandidat traegt 15 Klassen, aber nur zwei davon haben ueber 150
Trainingsboxen (BCA 163, BCC 159). Zwei Klassen haben ueberhaupt keine
(BBD_boden, SONST_schaden), eine hat fuenf (BAH). Kostet diese Breite die guten
Klassen Leistung -- dann lohnt ein enger Assistent -- oder ist sie gratis, dann
darf weiter fuer alle 15 gesammelt werden.

Die Antwort entscheidet, wofuer Pascals Handarbeit als naechstes eingesetzt wird.

WIE
Derselbe Datensatz, dieselben Bilder, dieselben Splits, dieselben
Trainingsparameter. Labelzeilen der nicht gewaehlten Klassen fallen weg, die
verbleibenden werden neu durchnummeriert.

Bilder werden NIE entfernt. Ein Bild, dessen einzige Box wegfaellt, wird zum
Negativbild (leere Labeldatei) -- fachlich richtig, denn der enge Detektor soll
dort nichts melden. Damit bleiben zwar Bildmenge und Splits gleich, zugleich
aendert sich aber der Hintergrund- und Negativdruck. Der Versuch isoliert die
Klassenbreite deshalb nicht als einzige Variable.

WAS DIESE ZAHLEN SIND UND WAS NICHT
Interne Validierung, kein Holdout. Sie ist im Projekt bekanntermassen
freundlicher als die Wirklichkeit. Vergleichbar ist ausserdem NUR der Wert JE
KLASSE: ein mAP ueber 2 Klassen und eines ueber 15 sind verschiedene Massstaebe
und duerfen nicht gegeneinander gestellt werden.

Diese Laeufe sind reine Diagnose. Sie entstehen ausserhalb der Freigabekette,
tragen kein Kandidatenmanifest und duerfen nie aktiviert werden.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import shutil
import stat
import sys
import uuid
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence


SCRIPT_DIRECTORY = Path(__file__).resolve().parent
if str(SCRIPT_DIRECTORY) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIRECTORY))

import train_detect_gold


SUPPORTED_IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}
ALLOWED_DATASET_ROOT_ENTRIES = {
    "images",
    "labels",
    "classes.txt",
    "data.yaml",
    "manifest.json",
    "_export_receipt.json",
    "klassenbreite.json",
}
STAGING_MARKER_NAME = ".detect-klassenbreite-staging"


@dataclass(frozen=True)
class LabelRow:
    class_id: int
    coordinate_texts: tuple[str, str, str, str]
    coordinates: tuple[float, float, float, float]


@dataclass(frozen=True)
class ImageInput:
    image: Path
    label: Path
    rows: tuple[LabelRow, ...]


def _lstat(path: Path, label: str) -> os.stat_result | None:
    try:
        return path.lstat()
    except FileNotFoundError:
        return None
    except OSError as error:
        raise SystemExit(f"{label} kann nicht sicher geprueft werden: {path}: {error}") from error


def _is_link_or_reparse(path: Path) -> bool:
    information = _lstat(path, "Pfad")
    if information is None:
        return False
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    attributes = getattr(information, "st_file_attributes", 0)
    return stat.S_ISLNK(information.st_mode) or bool(attributes & reparse_flag)


def _assert_no_link_components(path: Path, label: str) -> None:
    absolute = Path(os.path.abspath(path))
    for component in (*reversed(absolute.parents), absolute):
        information = _lstat(component, label)
        if information is None:
            continue
        reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
        attributes = getattr(information, "st_file_attributes", 0)
        if stat.S_ISLNK(information.st_mode) or attributes & reparse_flag:
            raise SystemExit(f"{label} enthaelt eine Verknuepfung oder Junction: {component}")


def _resolve_dataset(path: Path) -> Path:
    unresolved = Path(os.path.abspath(path.expanduser()))
    _assert_no_link_components(unresolved, "Datensatzpfad")
    try:
        resolved = unresolved.resolve(strict=True)
    except (OSError, RuntimeError) as error:
        raise SystemExit(f"Datensatz fehlt oder ist nicht sicher lesbar: {unresolved}") from error
    if not resolved.is_dir() or _is_link_or_reparse(resolved):
        raise SystemExit(f"Datensatz ist kein sicherer Ordner: {resolved}")
    return resolved


def _resolve_target(path: Path) -> Path:
    unresolved = Path(os.path.abspath(path.expanduser()))
    _assert_no_link_components(unresolved, "Zielpfad")
    try:
        return unresolved.resolve(strict=False)
    except (OSError, RuntimeError) as error:
        raise SystemExit(f"Zielpfad kann nicht sicher aufgeloest werden: {unresolved}") from error


def _path_is_occupied(path: Path) -> bool:
    return _lstat(path, "Zielpfad") is not None


def _is_within(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def _require_plain_directory(path: Path, label: str) -> Path:
    _assert_no_link_components(path, label)
    if not path.is_dir() or _is_link_or_reparse(path):
        raise SystemExit(f"{label} fehlt oder ist kein sicherer Ordner: {path}")
    return path


def _require_plain_file(path: Path, label: str) -> Path:
    _assert_no_link_components(path, label)
    if not path.is_file() or _is_link_or_reparse(path):
        raise SystemExit(f"{label} fehlt oder ist keine sichere Datei: {path}")
    return path


def _read_text(path: Path, label: str) -> str:
    _require_plain_file(path, label)
    try:
        return path.read_text(encoding="utf-8-sig")
    except (OSError, UnicodeError) as error:
        raise SystemExit(f"{label} ist nicht als UTF-8 lesbar: {path}") from error


def _read_classes_txt(classes_path: Path) -> tuple[str, ...]:
    lines = _read_text(classes_path, "classes.txt").splitlines()
    if not lines:
        raise SystemExit(f"classes.txt enthaelt keine Klassen: {classes_path}")
    classes: list[str] = []
    for line_number, line in enumerate(lines, start=1):
        name = line.strip()
        if not name:
            raise SystemExit(f"Leere Klasse in {classes_path}, Zeile {line_number}")
        if "'" in name or '"' in name:
            raise SystemExit(
                f"classes.txt muss reine Namen ohne Anfuehrungszeichen enthalten: "
                f"{classes_path}, Zeile {line_number}"
            )
        classes.append(name)
    if len(set(classes)) != len(classes):
        raise SystemExit(f"Doppelte Klassennamen in {classes_path}")
    return tuple(classes)


def lies_klassenkarte(data_yaml: Path) -> dict[int, str]:
    """Liest classes.txt und nutzt die gemeinsame strikte Gold-YAML-Pruefung."""
    data_yaml = Path(os.path.abspath(data_yaml))
    classes = _read_classes_txt(data_yaml.with_name("classes.txt"))
    _require_plain_file(data_yaml, "data.yaml")
    try:
        train_detect_gold._validate_data_yaml(data_yaml, classes)
    except (OSError, UnicodeError, ValueError) as error:
        raise SystemExit(f"data.yaml und classes.txt passen nicht zusammen: {error}") from error
    return {class_id: name for class_id, name in enumerate(classes)}


def _validate_dataset_root(dataset: Path) -> None:
    _require_plain_directory(dataset, "Datensatz")
    try:
        entries = list(dataset.iterdir())
    except OSError as error:
        raise SystemExit(f"Datensatz kann nicht aufgelistet werden: {dataset}") from error
    names = {entry.name for entry in entries}
    required = {"images", "labels", "classes.txt", "data.yaml"}
    missing = sorted(required - names)
    unexpected = sorted(names - ALLOWED_DATASET_ROOT_ENTRIES)
    if missing or unexpected:
        raise SystemExit(
            f"Unerwartete oder fehlende Eintraege im Datensatz {dataset}. "
            f"Fehlend: {missing}; unerwartet: {unexpected}"
        )
    for entry in entries:
        if _is_link_or_reparse(entry):
            raise SystemExit(f"Verknuepfung oder Junction im Datensatz: {entry}")
        if entry.name in {"images", "labels"}:
            if not entry.is_dir():
                raise SystemExit(f"Erwarteter Datensatzordner ist keine Dateiablage: {entry}")
        elif not entry.is_file():
            raise SystemExit(f"Unerwarteter Ordner im Datensatz: {entry}")


def _validate_category_root(root: Path, allowed_files: set[str] | None = None) -> None:
    _require_plain_directory(root, f"Datensatzordner {root.name}")
    allowed_files = allowed_files or set()
    expected_directories = {"train", "val"}
    try:
        entries = list(root.iterdir())
    except OSError as error:
        raise SystemExit(f"Datensatzordner kann nicht aufgelistet werden: {root}") from error
    names = {entry.name for entry in entries}
    missing = sorted(expected_directories - names)
    unexpected = sorted(names - expected_directories - allowed_files)
    if missing or unexpected:
        raise SystemExit(
            f"Unerwartete oder fehlende Eintraege unter {root}. "
            f"Fehlend: {missing}; unerwartet: {unexpected}"
        )
    for entry in entries:
        if _is_link_or_reparse(entry):
            raise SystemExit(f"Verknuepfung oder Junction im Datensatz: {entry}")
        if entry.name in expected_directories and not entry.is_dir():
            raise SystemExit(f"Erwarteter Split ist kein Ordner: {entry}")
        if entry.name in allowed_files and not entry.is_file():
            raise SystemExit(f"Unerwarteter Eintrag statt Cache-Datei: {entry}")


def _list_regular_files(directory: Path, *, image_files: bool) -> dict[str, Path]:
    _require_plain_directory(directory, f"Split-Ordner {directory}")
    by_stem: dict[str, Path] = {}
    try:
        entries = sorted(directory.iterdir(), key=lambda item: item.name.casefold())
    except OSError as error:
        raise SystemExit(f"Split-Ordner kann nicht aufgelistet werden: {directory}") from error
    for entry in entries:
        if _is_link_or_reparse(entry):
            raise SystemExit(f"Verknuepfung oder Junction ist nicht erlaubt: {entry}")
        if not entry.is_file():
            raise SystemExit(f"Unerwarteter Ordner oder Spezialeintrag im Split: {entry}")
        extension = entry.suffix.lower()
        if image_files and extension not in SUPPORTED_IMAGE_EXTENSIONS:
            raise SystemExit(f"Nicht unterstuetzte Datei im Bildordner: {entry}")
        if not image_files and extension != ".txt":
            raise SystemExit(f"Unerwartete Datei im Labelordner: {entry}")
        stem = entry.stem.casefold()
        if stem in by_stem:
            raise SystemExit(f"Doppelter Dateistamm im Split {directory.name}: {entry.stem}")
        by_stem[stem] = entry
    return by_stem


def _parse_label_file(path: Path, classes: dict[int, str]) -> tuple[LabelRow, ...]:
    rows: list[LabelRow] = []
    seen: set[tuple[int, float, float, float, float]] = set()
    for line_number, line in enumerate(_read_text(path, "Labeldatei").splitlines(), start=1):
        fields = line.split()
        if len(fields) != 5:
            raise SystemExit(f"Ungueltiges YOLO-Label: {path}, Zeile {line_number}")
        if not re.fullmatch(r"(?:0|[1-9][0-9]*)", fields[0]):
            raise SystemExit(f"Ungueltige Klassen-ID in {path}, Zeile {line_number}")
        class_id = int(fields[0])
        if class_id not in classes:
            raise SystemExit(
                f"Unbekannte Klassen-ID {class_id} in {path}, Zeile {line_number}; "
                f"erlaubt sind 0..{len(classes) - 1}"
            )
        try:
            coordinates = tuple(float(value) for value in fields[1:])
        except ValueError as error:
            raise SystemExit(f"Ungueltige Zahl in {path}, Zeile {line_number}") from error
        x_center, y_center, width, height = coordinates
        if (
            not all(math.isfinite(value) for value in coordinates)
            or not 0 <= x_center <= 1
            or not 0 <= y_center <= 1
            or not 0 < width <= 1
            or not 0 < height <= 1
            or x_center - width / 2 < -1e-6
            or y_center - height / 2 < -1e-6
            or x_center + width / 2 > 1 + 1e-6
            or y_center + height / 2 > 1 + 1e-6
        ):
            raise SystemExit(f"BBox ausserhalb des Bildes: {path}, Zeile {line_number}")
        key = (class_id, x_center, y_center, width, height)
        if key in seen:
            raise SystemExit(f"Doppeltes YOLO-Label: {path}, Zeile {line_number}")
        seen.add(key)
        rows.append(
            LabelRow(
                class_id=class_id,
                coordinate_texts=(fields[1], fields[2], fields[3], fields[4]),
                coordinates=(x_center, y_center, width, height),
            )
        )
    return tuple(rows)


def _collect_inputs(dataset: Path, classes: dict[int, str]) -> dict[str, tuple[ImageInput, ...]]:
    images_root = dataset / "images"
    labels_root = dataset / "labels"
    _validate_category_root(images_root)
    _validate_category_root(labels_root, {"train.cache", "val.cache"})
    collected: dict[str, tuple[ImageInput, ...]] = {}
    for split in ("train", "val"):
        images = _list_regular_files(images_root / split, image_files=True)
        labels = _list_regular_files(labels_root / split, image_files=False)
        if not images:
            raise SystemExit(f"Keine Bilder in {images_root / split}")
        missing_labels = sorted(images.keys() - labels.keys())
        orphan_labels = sorted(labels.keys() - images.keys())
        if missing_labels or orphan_labels:
            raise SystemExit(
                f"Bilder und Labels stimmen im Split {split!r} nicht ueberein. "
                f"Fehlende Labels: {missing_labels}; Labels ohne Bild: {orphan_labels}"
            )
        records = [
            ImageInput(
                image=images[stem],
                label=labels[stem],
                rows=_parse_label_file(labels[stem], classes),
            )
            for stem in sorted(images)
        ]
        collected[split] = tuple(records)
    return collected


def _create_staging(target: Path) -> tuple[Path, str]:
    target.parent.mkdir(parents=True, exist_ok=True)
    _assert_no_link_components(target.parent, "Zielordner")
    if not target.parent.is_dir() or _is_link_or_reparse(target.parent):
        raise SystemExit(f"Zielordner ist nicht sicher: {target.parent}")
    if _path_is_occupied(target):
        raise SystemExit(f"Ziel existiert bereits: {target}")
    token = uuid.uuid4().hex
    staging = target.parent / f".{target.name}.staging-{token}"
    try:
        staging.mkdir()
    except FileExistsError as error:
        raise SystemExit(f"Zufaelliger Staging-Ordner ist bereits belegt: {staging}") from error
    return staging, token


def _write_staging_marker(staging: Path, token: str) -> None:
    marker = staging / STAGING_MARKER_NAME
    try:
        with marker.open("x", encoding="ascii", newline="\n") as stream:
            stream.write(token + "\n")
    except OSError as error:
        try:
            staging.rmdir()
        except OSError:
            pass
        raise SystemExit(f"Besitznachweis fuer Staging konnte nicht geschrieben werden: {staging}") from error


def _verify_owned_staging(staging: Path, parent: Path, token: str) -> Path:
    if staging.parent != parent or not staging.name.endswith(token):
        raise SystemExit(f"Unsicherer Staging-Pfad bleibt zur Pruefung erhalten: {staging}")
    if not staging.is_dir() or _is_link_or_reparse(staging):
        raise SystemExit(f"Staging-Ordner wurde ersetzt oder ist unsicher: {staging}")
    marker = staging / STAGING_MARKER_NAME
    if not marker.is_file() or _is_link_or_reparse(marker):
        raise SystemExit(f"Besitznachweis des Staging-Ordners fehlt: {staging}")
    try:
        content = marker.read_text(encoding="ascii")
    except (OSError, UnicodeError) as error:
        raise SystemExit(f"Besitznachweis des Staging-Ordners ist unlesbar: {staging}") from error
    if content != token + "\n":
        raise SystemExit(f"Fremder Staging-Ordner bleibt unangetastet: {staging}")
    return marker


def _remove_owned_staging(staging: Path, parent: Path, token: str) -> None:
    if _lstat(staging, "Staging-Ordner") is None:
        return
    _verify_owned_staging(staging, parent, token)
    shutil.rmtree(staging)


def _publish_staging(staging: Path, target: Path, token: str) -> None:
    marker = _verify_owned_staging(staging, target.parent, token)
    _assert_no_link_components(target.parent, "Zielordner")
    if _path_is_occupied(target):
        raise SystemExit(f"Ziel wurde waehrend des Laufs belegt: {target}")
    try:
        marker.unlink()
        staging.rename(target)
    except BaseException:
        if _lstat(staging, "Staging-Ordner") is not None:
            try:
                with marker.open("x", encoding="ascii", newline="\n") as stream:
                    stream.write(token + "\n")
            except FileExistsError:
                pass
        raise


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--ziel", type=Path, required=True)
    parser.add_argument(
        "--klassen",
        nargs="+",
        required=True,
        help="Klassennamen aus data.yaml, z. B. BCA_anschluss BCC_bogen",
    )
    args = parser.parse_args(argv)

    dataset = _resolve_dataset(args.dataset)
    target = _resolve_target(args.ziel)
    if _path_is_occupied(target):
        raise SystemExit(f"Ziel existiert bereits: {target}")
    if _is_within(target, dataset):
        raise SystemExit(f"Ziel darf nicht im unveraenderten Quelldatensatz liegen: {target}")

    _validate_dataset_root(dataset)
    old_classes = lies_klassenkarte(dataset / "data.yaml")
    name_to_old_id = {name: class_id for class_id, name in old_classes.items()}

    unknown = [name for name in args.klassen if name not in name_to_old_id]
    if unknown:
        raise SystemExit(
            f"Unbekannte Klassen: {unknown}\nVorhanden: {sorted(name_to_old_id)}"
        )
    if len(set(args.klassen)) != len(args.klassen):
        raise SystemExit("Doppelte Klassenangabe")
    if len(args.klassen) < 2:
        raise SystemExit("Mindestens zwei Klassen -- ein Ein-Klassen-Modell ist ein anderer Versuch")

    # Neue IDs folgen der alten Reihenfolge, nicht der Eingabereihenfolge.
    retained_old_ids = sorted(name_to_old_id[name] for name in args.klassen)
    old_to_new = {old_id: new_id for new_id, old_id in enumerate(retained_old_ids)}
    new_names = [old_classes[old_id] for old_id in retained_old_ids]

    # Die vollstaendige Quelle wird geprueft, bevor irgendeine Ausgabe entsteht.
    inputs = _collect_inputs(dataset, old_classes)

    staging, token = _create_staging(target)
    published = False
    try:
        _write_staging_marker(staging, token)
        statistics: dict[str, dict[str, object]] = {}
        for split in ("train", "val"):
            (staging / "images" / split).mkdir(parents=True)
            (staging / "labels" / split).mkdir(parents=True)

            before: Counter[str] = Counter()
            after: Counter[str] = Counter()
            newly_negative = 0
            already_negative = 0

            for record in inputs[split]:
                _require_plain_file(record.image, "Quellbild")
                shutil.copy2(record.image, staging / "images" / split / record.image.name)
                output_lines: list[str] = []
                for row in record.rows:
                    class_name = old_classes[row.class_id]
                    before[class_name] += 1
                    if row.class_id in old_to_new:
                        output_lines.append(
                            " ".join((str(old_to_new[row.class_id]), *row.coordinate_texts))
                        )
                        after[class_name] += 1
                if not record.rows:
                    already_negative += 1
                elif not output_lines:
                    newly_negative += 1
                (staging / "labels" / split / record.label.name).write_text(
                    "\n".join(output_lines) + ("\n" if output_lines else ""),
                    encoding="utf-8",
                )

            statistics[split] = {
                "bilder": len(inputs[split]),
                "boxen_vorher": sum(before.values()),
                "boxen_nachher": sum(after.values()),
                "boxen_je_klasse": {name: after[name] for name in new_names},
                "bilder_vorher_negativ": already_negative,
                "bilder_neu_negativ": newly_negative,
            }

        (staging / "data.yaml").write_text(
            "path: .\ntrain: images/train\nval: images/val\n"
            f"nc: {len(new_names)}\nnames:\n"
            + "".join(
                f"  {index}: {json.dumps(name, ensure_ascii=False)}\n"
                for index, name in enumerate(new_names)
            ),
            encoding="utf-8",
        )
        (staging / "classes.txt").write_text(
            "\n".join(new_names) + "\n",
            encoding="utf-8",
        )
        (staging / "klassenbreite.json").write_text(
            json.dumps(
                {
                    "schema": "detect_klassenbreite_stufe_v1",
                    "zweck": "Reine Diagnose. Kein Kandidat, kein Manifest, nie aktivieren.",
                    "quelle": str(dataset),
                    "klassen_vorher": [old_classes[index] for index in sorted(old_classes)],
                    "klassen_nachher": new_names,
                    "id_abbildung_alt_zu_neu": {
                        old_classes[old_id]: new_id for old_id, new_id in old_to_new.items()
                    },
                    "bilder": (
                        "unveraendert vollstaendig, entfallene Boxen ergeben Negativbilder"
                    ),
                    "statistik": statistics,
                },
                indent=1,
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )

        _publish_staging(staging, target, token)
        published = True
    finally:
        if not published:
            _remove_owned_staging(staging, target.parent, token)

    print(f"{len(new_names)} Klassen: {', '.join(new_names)}")
    for split, values in statistics.items():
        print(
            f"  {split}: {values['boxen_nachher']} von {values['boxen_vorher']} Boxen, "
            f"{values['bilder']} Bilder ({values['bilder_neu_negativ']} neu negativ, "
            f"{values['bilder_vorher_negativ']} vorher schon)"
        )
    print(f"Datensatz: {target}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
