"""Trainiert oder misst eine Klassenbreiten-Stufe einheitlich je Klasse.

Die Trainingslaeufe bleiben reine Diagnose. Sie erzeugen weder einen Kandidaten
noch ein Manifest und duerfen nie aktiviert werden. Jede Abschlussmessung laeuft
mit FP32 und Stapel 4, damit Referenz und reduzierte Klassenstufen vergleichbar
gemessen werden.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
from contextlib import contextmanager
from pathlib import Path
from typing import Any, Iterator, Sequence


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_DIRECTORY = Path(__file__).resolve().parent
if str(SCRIPT_DIRECTORY) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIRECTORY))

import train_detect_gold
import detect_klassenbreite as dataset_guard


DEFAULT_BASISGEWICHT = (
    REPOSITORY_ROOT / "sidecar" / "models" / "yolo26m" / "yolo26m.pt"
)
DEFAULT_KNOWLEDGE_ROOT = Path(
    os.getenv("SEWERSTUDIO_KNOWLEDGE_ROOT", r"C:\KI_BRAIN")
)
LAUFNAME_PATTERN = re.compile(r"[a-z0-9][a-z0-9_-]{0,79}")

# Exakt die Werte des historischen Referenzlaufs lernkurve_100. Die starke
# Standard-Farbvariation und fliplr=0.5 bleiben fuer diesen Vergleich erhalten.
TRAINING_PARAMETER: dict[str, Any] = {
    "epochs": 40,
    "patience": 100,
    "batch": 4,
    "imgsz": 1280,
    "device": "0",
    "workers": 0,
    "seed": 42,
    "deterministic": True,
    "pretrained": True,
    "verbose": False,
    "exist_ok": False,
    "plots": True,
    "flipud": 0.0,
    "fliplr": 0.5,
    "hsv_h": 0.015,
    "hsv_s": 0.7,
    "hsv_v": 0.4,
    "mosaic": 1.0,
}

MESS_PARAMETER: dict[str, Any] = {
    "imgsz": 1280,
    "batch": 4,
    "half": False,
    "device": "0",
    "workers": 0,
    "exist_ok": False,
    "plots": False,
}


def _sha256_datei(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _pruefe_laufname(name: str) -> str:
    if not LAUFNAME_PATTERN.fullmatch(name):
        raise SystemExit(
            "Ungueltiger Laufname. Erlaubt sind 1 bis 80 Zeichen: "
            "a-z, 0-9, _ und -."
        )
    return name


def _lies_datasetklassen(dataset: Path) -> tuple[str, ...]:
    # Historische Diagnoseordner koennen fremde Wurzeldateien enthalten. Fuer die
    # reine Messung werden nur die tatsaechlich gelesenen Pfade streng geprueft.
    for pfad, label in (
        (dataset / "images" / "train", "Trainings-Bildordner"),
        (dataset / "images" / "val", "Validation-Bildordner"),
        (dataset / "labels" / "train", "Trainings-Labelordner"),
        (dataset / "labels" / "val", "Validation-Labelordner"),
    ):
        dataset_guard._require_plain_directory(pfad, label)
    klassenkarte = dataset_guard.lies_klassenkarte(dataset / "data.yaml")
    return tuple(klassenkarte[index] for index in range(len(klassenkarte)))


def _zaehle_sollboxen_val(dataset: Path, klassenanzahl: int) -> tuple[int, ...]:
    labelordner = dataset / "labels" / "val"
    dataset_guard._require_plain_directory(labelordner, "Validation-Labelordner")

    zaehler = [0] * klassenanzahl
    for label in sorted(labelordner.iterdir(), key=lambda path: path.name.lower()):
        if (
            dataset_guard._is_link_or_reparse(label)
            or not label.is_file()
            or label.suffix.lower() != ".txt"
        ):
            raise SystemExit(f"Unerwarteter Eintrag im Validation-Labelordner: {label}")
        try:
            zeilen = label.read_text(encoding="utf-8-sig").splitlines()
        except (OSError, UnicodeError) as error:
            raise SystemExit(f"Labeldatei ist nicht lesbar: {label}") from error
        for zeilennummer, zeile in enumerate(zeilen, start=1):
            roh = zeile.strip()
            if not roh:
                continue
            felder = roh.split()
            if len(felder) != 5:
                raise SystemExit(
                    f"Ungueltige YOLO-Zeile in {label}, Zeile {zeilennummer}."
                )
            try:
                klassen_id = int(felder[0])
            except ValueError as error:
                raise SystemExit(
                    f"Ungueltige Klassen-ID in {label}, Zeile {zeilennummer}."
                ) from error
            if klassen_id < 0 or klassen_id >= klassenanzahl:
                raise SystemExit(
                    f"Klassen-ID {klassen_id} in {label}, Zeile {zeilennummer}, "
                    f"liegt ausserhalb 0..{klassenanzahl - 1}."
                )
            zaehler[klassen_id] += 1
    return tuple(zaehler)


def _modellklassen(modell: Any) -> tuple[str, ...]:
    namen = modell.names
    if isinstance(namen, dict):
        try:
            nach_id = {int(key): str(value) for key, value in namen.items()}
        except (TypeError, ValueError) as error:
            raise RuntimeError("Das Gewicht besitzt keine gueltige Klassenkarte.") from error
        if set(nach_id) != set(range(len(nach_id))):
            raise RuntimeError("Die Klassen-IDs des Gewichts sind nicht lueckenlos.")
        return tuple(nach_id[index] for index in range(len(nach_id)))
    if isinstance(namen, (list, tuple)):
        return tuple(str(name) for name in namen)
    raise RuntimeError("Das Gewicht besitzt keine lesbare Klassenkarte.")


def _pruefe_messgewicht_klassen(modell: Any, datasetklassen: tuple[str, ...]) -> None:
    gewichtsklassen = _modellklassen(modell)
    if gewichtsklassen != datasetklassen:
        raise RuntimeError(
            "Die Klassenkarte des Messgewichts passt nicht zum Datensatz.\n"
            f"Gewicht: {list(gewichtsklassen)}\n"
            f"Datensatz: {list(datasetklassen)}"
        )


def werte_je_klasse(
    ergebnis: Any,
    datasetklassen: Sequence[str],
    sollboxen_val: Sequence[int],
) -> dict[str, dict[str, float | int | str | None]]:
    """Gibt jede Datasetklasse aus und kennzeichnet Klassen ohne Soll-Box."""
    if len(datasetklassen) != len(sollboxen_val):
        raise ValueError("Klassen und Soll-Box-Zaehler haben verschiedene Laengen.")

    box = ergebnis.box
    klassen_ids = [int(value) for value in box.ap_class_index]
    laengen = {
        len(klassen_ids),
        len(box.p),
        len(box.r),
        len(box.ap50),
        len(box.ap),
    }
    if len(laengen) != 1 or len(set(klassen_ids)) != len(klassen_ids):
        raise RuntimeError("Ultralytics lieferte widerspruechliche Klassenwerte.")

    nach_id: dict[int, dict[str, float]] = {}
    for platz, klassen_id in enumerate(klassen_ids):
        if klassen_id < 0 or klassen_id >= len(datasetklassen):
            raise RuntimeError(f"Ultralytics lieferte unbekannte Klassen-ID {klassen_id}.")
        nach_id[klassen_id] = {
            "precision": round(float(box.p[platz]), 4),
            "recall": round(float(box.r[platz]), 4),
            "ap50": round(float(box.ap50[platz]), 4),
            "ap50_95": round(float(box.ap[platz]), 4),
        }

    werte: dict[str, dict[str, float | int | str | None]] = {}
    for klassen_id, name in enumerate(datasetklassen):
        sollboxen = int(sollboxen_val[klassen_id])
        if sollboxen == 0:
            werte[name] = {
                "soll_boxen_val": 0,
                "precision": None,
                "recall": None,
                "ap50": None,
                "ap50_95": None,
                "grund": "0 Soll-Boxen in val",
            }
            continue
        if klassen_id not in nach_id:
            raise RuntimeError(
                f"Ultralytics lieferte fuer '{name}' trotz {sollboxen} Soll-Boxen "
                "keine Klassenwerte."
            )
        werte[name] = {"soll_boxen_val": sollboxen, **nach_id[klassen_id]}
    return werte


def _schreibe_runtime_yaml(
    ziel: Path,
    dataset: Path,
    datasetklassen: Sequence[str],
) -> None:
    dataset_yaml = json.dumps(dataset.as_posix(), ensure_ascii=False)
    zeilen = [
        f"path: {dataset_yaml}",
        "train: images/train",
        "val: images/val",
        f"nc: {len(datasetklassen)}",
        "names:",
        *(
            f"  {index}: {json.dumps(name, ensure_ascii=False)}"
            for index, name in enumerate(datasetklassen)
        ),
    ]
    ziel.write_text("\n".join(zeilen) + "\n", encoding="utf-8")


@contextmanager
def _im_arbeitsordner(arbeitsordner: Path) -> Iterator[None]:
    vorher = Path.cwd()
    os.chdir(arbeitsordner)
    try:
        yield
    finally:
        os.chdir(vorher)


def _entferne_ultralytics_caches(dataset: Path) -> None:
    labels_root_unaufgeloest = dataset / "labels"
    try:
        dataset_guard._require_plain_directory(labels_root_unaufgeloest, "Labelordner")
    except SystemExit as error:
        raise RuntimeError(str(error)) from error
    labels_root = labels_root_unaufgeloest.resolve()
    for name in ("train.cache", "val.cache"):
        cache = labels_root_unaufgeloest / name
        if dataset_guard._is_link_or_reparse(cache):
            raise RuntimeError(f"Cachepfad ist eine Verknuepfung: {cache}")
        if dataset_guard._lstat(cache, "Cachepfad") is not None and not cache.is_file():
            raise RuntimeError(f"Cachepfad ist keine regulaere Datei: {cache}")
        if cache.resolve().parent != labels_root:
            raise RuntimeError(f"Unsicherer Cachepfad: {cache}")
        if cache.is_file():
            cache.unlink()


def sewerstudio_laeuft() -> bool:
    """Prueft unter Windows den echten Prozess, ohne ihn zu beenden."""
    if os.name != "nt":
        return False
    try:
        ergebnis = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq SewerStudio.exe", "/FO", "CSV", "/NH"],
            check=False,
            capture_output=True,
            text=True,
            timeout=10,
        )
    except (OSError, subprocess.SubprocessError) as error:
        raise RuntimeError(
            "SewerStudio-Prozessstatus konnte nicht sicher geprueft werden."
        ) from error
    if ergebnis.returncode != 0:
        raise RuntimeError("SewerStudio-Prozessstatus konnte nicht sicher geprueft werden.")
    return '"sewerstudio.exe"' in ergebnis.stdout.lower()


def _pruefe_trainingsressourcen() -> int:
    if sewerstudio_laeuft():
        raise RuntimeError(
            "SewerStudio.exe laeuft. Bitte SewerStudio vor dem Training schliessen; "
            "das Skript beendet es niemals automatisch."
        )
    return train_detect_gold.ensure_training_resources()


def _pruefe_ausgabeziele(
    name: str,
    laufordner: Path,
    bericht: Path,
) -> tuple[Path, Path]:
    train_lauf = laufordner / name
    mess_lauf = laufordner / f"{name}_val"
    for pfad in (train_lauf, mess_lauf):
        if dataset_guard._path_is_occupied(pfad):
            raise SystemExit(f"Laufordner existiert bereits: {pfad}")
    if dataset_guard._path_is_occupied(bericht):
        raise SystemExit(f"Messung existiert bereits: {bericht}")
    return train_lauf, mess_lauf


def _belege(dataset: Path) -> dict[str, dict[str, str]]:
    for filename in ("data.yaml", "classes.txt"):
        dataset_guard._require_plain_file(dataset / filename, filename)
    belege: dict[str, dict[str, str]] = {
        "data_yaml": {
            "pfad": str(dataset / "data.yaml"),
            "sha256": _sha256_datei(dataset / "data.yaml"),
        },
        "klassen": {
            "pfad": str(dataset / "classes.txt"),
            "sha256": _sha256_datei(dataset / "classes.txt"),
        },
    }
    exportbeleg = dataset / "_export_receipt.json"
    if dataset_guard._lstat(exportbeleg, "Exportbeleg") is not None:
        dataset_guard._require_plain_file(exportbeleg, "Exportbeleg")
        belege["export_receipt"] = {
            "pfad": str(exportbeleg),
            "sha256": _sha256_datei(exportbeleg),
        }
    klassenbreitenbeleg = dataset / "klassenbreite.json"
    if dataset_guard._lstat(klassenbreitenbeleg, "Klassenbreitenbeleg") is not None:
        dataset_guard._require_plain_file(klassenbreitenbeleg, "Klassenbreitenbeleg")
        belege["klassenbreite"] = {
            "pfad": str(klassenbreitenbeleg),
            "sha256": _sha256_datei(klassenbreitenbeleg),
        }
    return belege


def _schreibe_bericht_exklusiv(ziel: Path, bericht: dict[str, Any]) -> None:
    daten = json.dumps(bericht, indent=1, ensure_ascii=False) + "\n"
    try:
        with ziel.open("x", encoding="utf-8", newline="\n") as stream:
            stream.write(daten)
    except FileExistsError as error:
        raise SystemExit(f"Messung existiert bereits: {ziel}") from error


def _loese_sichere_datei_auf(path: Path, label: str) -> Path:
    aufgeloest = dataset_guard._resolve_target(path)
    dataset_guard._require_plain_file(aufgeloest, label)
    return aufgeloest


def _pruefe_ausgabewurzeln(*wurzeln: Path) -> None:
    for wurzel in wurzeln:
        dataset_guard._assert_no_link_components(wurzel, "Ausgabeordner")
        if dataset_guard._path_is_occupied(wurzel) and (
            not wurzel.is_dir() or dataset_guard._is_link_or_reparse(wurzel)
        ):
            raise SystemExit(f"Unsicherer Ausgabeordner: {wurzel}")


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--name", required=True)
    parser.add_argument(
        "--gewicht",
        type=Path,
        help="Nur ein vorhandenes Gewicht messen; es wird nicht neu trainiert.",
    )
    parser.add_argument(
        "--basisgewicht",
        type=Path,
        default=DEFAULT_BASISGEWICHT,
        help="Basisgewicht fuer einen neuen Diagnoselauf.",
    )
    parser.add_argument(
        "--knowledge-root",
        type=Path,
        default=DEFAULT_KNOWLEDGE_ROOT,
        help="SewerStudio-Wissensordner; alternativ SEWERSTUDIO_KNOWLEDGE_ROOT.",
    )
    args = parser.parse_args(argv)

    name = _pruefe_laufname(args.name)
    dataset = dataset_guard._resolve_dataset(args.dataset)
    knowledge_root = dataset_guard._resolve_target(args.knowledge_root)
    if dataset_guard._path_is_occupied(knowledge_root) and (
        not knowledge_root.is_dir()
        or dataset_guard._is_link_or_reparse(knowledge_root)
    ):
        raise SystemExit(f"Unsicherer KnowledgeRoot: {knowledge_root}")
    messgewicht = (
        _loese_sichere_datei_auf(args.gewicht, "Messgewicht")
        if args.gewicht is not None
        else None
    )
    basisgewicht = (
        _loese_sichere_datei_auf(args.basisgewicht, "Basisgewicht")
        if messgewicht is None
        else None
    )
    laufordner = knowledge_root / "training" / "cls_runs"
    berichte = knowledge_root / "training" / "diagnostics"
    ziel = berichte / f"{name}_klassenwerte.json"

    datasetklassen = _lies_datasetklassen(dataset)
    sollboxen_val = _zaehle_sollboxen_val(dataset, len(datasetklassen))
    datasetbelege = _belege(dataset)
    _pruefe_ausgabewurzeln(laufordner, berichte)
    _pruefe_ausgabeziele(name, laufordner, ziel)

    if messgewicht is not None:
        freier_vram = None
    else:
        assert basisgewicht is not None
        freier_vram = _pruefe_trainingsressourcen()

    laufordner.mkdir(parents=True, exist_ok=True)
    berichte.mkdir(parents=True, exist_ok=True)
    for ausgabeordner in (laufordner, berichte):
        dataset_guard._require_plain_directory(ausgabeordner, "Ausgabeordner")
    _pruefe_ausgabeziele(name, laufordner, ziel)

    basis_hash = _sha256_datei(basisgewicht) if messgewicht is None else None
    _entferne_ultralytics_caches(dataset)
    try:
        with tempfile.TemporaryDirectory(prefix="sewerstudio-klassenbreite-") as temporaer:
            arbeitsordner = Path(temporaer).resolve()
            runtime_yaml = arbeitsordner / "data.runtime.yaml"
            _schreibe_runtime_yaml(runtime_yaml, dataset, datasetklassen)

            with _im_arbeitsordner(arbeitsordner):
                from ultralytics import YOLO

                if messgewicht is not None:
                    modell = YOLO(str(messgewicht))
                    _pruefe_messgewicht_klassen(modell, datasetklassen)
                    herkunft: dict[str, Any] = {
                        "art": "nur gemessen",
                        "gewicht": str(messgewicht),
                        "gewicht_sha256": _sha256_datei(messgewicht),
                    }
                else:
                    modell = YOLO(str(basisgewicht))
                    modell.train(
                        data=str(runtime_yaml),
                        project=str(laufordner),
                        name=name,
                        **TRAINING_PARAMETER,
                    )
                    if _sha256_datei(basisgewicht) != basis_hash:
                        raise RuntimeError(
                            "Das Basisgewicht wurde waehrend des Trainings veraendert."
                        )
                    auswertungsgewicht = laufordner / name / "weights" / "best.pt"
                    if auswertungsgewicht.is_symlink() or not auswertungsgewicht.is_file():
                        raise RuntimeError(
                            f"Training endete ohne sicheres best.pt: {auswertungsgewicht}"
                        )
                    herkunft = {
                        "art": "trainiert",
                        "basisgewicht": str(basisgewicht),
                        "basisgewicht_sha256": basis_hash,
                        "gewicht": str(auswertungsgewicht),
                        "gewicht_sha256": _sha256_datei(auswertungsgewicht),
                        "freier_vram_mb_vor_start": freier_vram,
                        "parameter": dict(TRAINING_PARAMETER),
                    }
                    modell = YOLO(str(auswertungsgewicht))

                ergebnis = modell.val(
                    data=str(runtime_yaml),
                    project=str(laufordner),
                    name=f"{name}_val",
                    **MESS_PARAMETER,
                )
    finally:
        _entferne_ultralytics_caches(dataset)

    bericht = {
        "schema": "detect_klassenbreite_messung_v2",
        "zweck": "Reine Diagnose. Kein Kandidat, kein Manifest, nie aktivieren.",
        "name": name,
        "datensatz": str(dataset),
        "datensatz_belege": datasetbelege,
        "herkunft": herkunft,
        "messung": dict(MESS_PARAMETER),
        "hinweis": (
            "Nur die Werte JE KLASSE sind zwischen Stufen vergleichbar. "
            "Ein mAP ueber 2 Klassen und eines ueber 15 sind verschiedene Massstaebe."
        ),
        "klassen": werte_je_klasse(ergebnis, datasetklassen, sollboxen_val),
    }
    _schreibe_bericht_exklusiv(ziel, bericht)

    print(f"\n{name}:")
    for klasse, werte in bericht["klassen"].items():
        if werte["ap50"] is None:
            print(f"  {klasse:22s} nicht messbar ({werte['grund']})")
            continue
        print(
            f"  {klasse:22s} AP50 {werte['ap50']:.3f}  "
            f"AP50-95 {werte['ap50_95']:.3f}  "
            f"R {werte['recall']:.3f}  P {werte['precision']:.3f}"
        )
    print(f"Bericht: {ziel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
