r"""Trainiert einen getrennten YOLO-Detect-Kandidaten fuer den BCC-Bogen-Pilot.

Das bestehende SewerStudio-Modell wird weder ersetzt noch veraendert. Das Skript
akzeptiert nur einen vom gemeinsamen Exportplaner erzeugten Datensatz unter
``C:\KI_BRAIN\training\datasets``. Ohne freien GPU-Speicher und bei laufendem
Sidecar verweigert es den Trainingsstart.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import shutil
import socket
import subprocess
import urllib.error
import urllib.request
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PILOT_CLASS_ID = 14
PILOT_CLASS_NAME = "BCC_bogen"
MINIMUM_IMAGES = 30
MINIMUM_FREE_VRAM_MB = 28_000
SIDECAR_HEALTH_URL = "http://127.0.0.1:8100/health"


@dataclass(frozen=True)
class ValidatedDataset:
    root: Path
    data_yaml: Path
    manifest: Path
    plan_id: str
    image_count: int
    train_count: int
    validation_count: int
    instance_count: int
    manifest_sha256: str


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _is_within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except (OSError, ValueError):
        return False


def _load_json_object(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"{path} muss ein JSON-Objekt enthalten.")
    return value


def discover_dataset(dataset_root: Path) -> Path:
    root = dataset_root.resolve()
    if not root.is_dir():
        raise ValueError(f"Datensatzwurzel fehlt: {root}")
    candidates = [
        directory
        for directory in root.iterdir()
        if directory.is_dir()
        and not directory.name.startswith(".")
        and (directory / "manifest.json").is_file()
        and (directory / "classes.txt").is_file()
        and PILOT_CLASS_NAME
        in (directory / "classes.txt").read_text(encoding="utf-8-sig").splitlines()
    ]
    if not candidates:
        raise ValueError("Kein exportierter BCC-Pilotdatensatz wurde gefunden.")
    return max(candidates, key=lambda directory: directory.stat().st_mtime_ns)


def _validate_receipt(dataset: Path, manifest: Path) -> dict[str, Any]:
    receipt_path = dataset / "_export_receipt.json"
    receipt = _load_json_object(receipt_path)
    if receipt.get("manifest_sha256") != _sha256_file(manifest):
        raise ValueError("Der Datensatzbeleg passt nicht mehr zu manifest.json.")

    for category in ("images", "labels"):
        entries = receipt.get(category)
        if not isinstance(entries, list):
            raise ValueError(f"Der Datensatzbeleg enthaelt keine gueltige Liste '{category}'.")
        for entry in entries:
            if not isinstance(entry, dict):
                raise ValueError(f"Ungueltiger {category}-Eintrag im Datensatzbeleg.")
            relative = str(entry.get("path") or "")
            expected_hash = str(entry.get("sha256") or "").lower()
            target = (dataset / Path(relative)).resolve()
            if not relative or not _is_within(target, dataset) or not target.is_file():
                raise ValueError(f"Unsichere oder fehlende Datensatzdatei: {relative}")
            if _sha256_file(target) != expected_hash:
                raise ValueError(f"Datensatzdatei wurde nach dem Export veraendert: {relative}")
    return receipt


def _validate_label_file(path: Path) -> int:
    instances = 0
    for line_number, line in enumerate(
        path.read_text(encoding="utf-8-sig").splitlines(),
        start=1,
    ):
        fields = line.split()
        if len(fields) != 5:
            raise ValueError(f"Ungueltiges YOLO-Label: {path}, Zeile {line_number}")
        try:
            class_id = int(fields[0])
            values = [float(value) for value in fields[1:]]
        except ValueError as error:
            raise ValueError(
                f"Ungueltige Zahl im YOLO-Label: {path}, Zeile {line_number}"
            ) from error
        if class_id != PILOT_CLASS_ID:
            raise ValueError(
                f"Der BCC-Pilot enthaelt eine fremde Klasse {class_id}: {path}"
            )
        x_center, y_center, width, height = values
        if (
            not 0 <= x_center <= 1
            or not 0 <= y_center <= 1
            or not 0 < width <= 1
            or not 0 < height <= 1
            or x_center - width / 2 < -1e-6
            or y_center - height / 2 < -1e-6
            or x_center + width / 2 > 1 + 1e-6
            or y_center + height / 2 > 1 + 1e-6
        ):
            raise ValueError(f"BBox ausserhalb des Bildes: {path}, Zeile {line_number}")
        instances += 1
    # Leere Labeldatei (0 Bytes) ist seit dem Negativ-Pool-Anschluss GUELTIG:
    # sie kennzeichnet ein kuratiertes Negativ-/Hintergrundbild (Trainingsplan D.3).
    # Ein Positivbild OHNE jede Labeldatei stoppt weiterhin ueber den
    # Bilder/Labels-Abgleich in validate_dataset.
    return instances


def validate_dataset(dataset: Path, dataset_root: Path) -> ValidatedDataset:
    root = dataset.resolve()
    allowed_root = dataset_root.resolve()
    if root.parent != allowed_root or not root.is_dir():
        raise ValueError(
            f"Der Datensatz muss ein direkter Unterordner von {allowed_root} sein."
        )

    manifest_path = root / "manifest.json"
    data_yaml = root / "data.yaml"
    classes_path = root / "classes.txt"
    for required in (manifest_path, data_yaml, classes_path, root / "_export_receipt.json"):
        if not required.is_file():
            raise ValueError(f"Pflichtdatei fehlt: {required}")

    classes = classes_path.read_text(encoding="utf-8-sig").splitlines()
    if len(classes) != 15 or classes[PILOT_CLASS_ID] != PILOT_CLASS_NAME:
        raise ValueError("Die feste Detect-Klassenkarte v2 mit BCC-ID 14 fehlt.")

    manifest = _load_json_object(manifest_path)
    manifest_classes = manifest.get("classes")
    if manifest_classes != classes:
        raise ValueError("classes.txt und manifest.json verwenden verschiedene Klassen.")
    plan_id = str(manifest.get("plan_id") or "")
    if not plan_id or plan_id != root.name:
        raise ValueError("Datensatzordner und unveraenderlicher Plan stimmen nicht ueberein.")

    receipt = _validate_receipt(root, manifest_path)
    if str(receipt.get("plan_id") or "") != plan_id:
        raise ValueError("Der Exportbeleg gehoert zu einem anderen Plan.")

    counts: dict[str, int] = {}
    instance_count = 0
    for split in ("train", "val"):
        image_dir = root / "images" / split
        label_dir = root / "labels" / split
        if not image_dir.is_dir() or not label_dir.is_dir():
            raise ValueError(f"Split fehlt: {split}")
        image_stems = {path.stem for path in image_dir.iterdir() if path.is_file()}
        label_paths = [path for path in label_dir.iterdir() if path.is_file()]
        label_stems = {path.stem for path in label_paths}
        if image_stems != label_stems:
            raise ValueError(f"Bilder und Labels stimmen im Split '{split}' nicht ueberein.")
        counts[split] = len(image_stems)
        instance_count += sum(_validate_label_file(path) for path in label_paths)

    image_count = counts["train"] + counts["val"]
    if image_count < MINIMUM_IMAGES or counts["train"] < 1 or counts["val"] < 1:
        raise ValueError(
            f"Der BCC-Pilot ist zu klein oder unvollstaendig: "
            f"{counts['train']} Train, {counts['val']} Pruefung."
        )
    if receipt.get("total_samples") != image_count:
        raise ValueError("Die Bildzahl stimmt nicht mit dem Exportbeleg ueberein.")
    manifest_instances = manifest.get("instances_per_class")
    if (
        not isinstance(manifest_instances, dict)
        or manifest_instances.get(PILOT_CLASS_NAME) != instance_count
    ):
        raise ValueError("Die BCC-Instanzzahl stimmt nicht mit manifest.json ueberein.")

    return ValidatedDataset(
        root=root,
        data_yaml=data_yaml,
        manifest=manifest_path,
        plan_id=plan_id,
        image_count=image_count,
        train_count=counts["train"],
        validation_count=counts["val"],
        instance_count=instance_count,
        manifest_sha256=_sha256_file(manifest_path),
    )


def sidecar_running(timeout: float = 1.5) -> bool:
    try:
        with urllib.request.urlopen(SIDECAR_HEALTH_URL, timeout=timeout) as response:
            return 200 <= response.status < 300
    except urllib.error.HTTPError:
        return True
    except Exception:
        try:
            with socket.create_connection(("127.0.0.1", 8100), timeout=timeout):
                return True
        except OSError:
            return False


def gpu_free_vram_mb() -> int | None:
    executable = shutil.which("nvidia-smi")
    if not executable:
        return None
    try:
        output = subprocess.run(
            [
                executable,
                "--query-gpu=memory.free",
                "--format=csv,noheader,nounits",
            ],
            capture_output=True,
            text=True,
            timeout=5,
            check=True,
        ).stdout.strip().splitlines()
        return int(output[0].strip()) if output else None
    except (OSError, ValueError, subprocess.SubprocessError):
        return None


def ensure_training_resources() -> int:
    if sidecar_running():
        raise RuntimeError(
            "SewerStudio-Sidecar laeuft. Bitte SewerStudio schliessen; "
            "der BCC-Pilot stoppt es niemals automatisch."
        )
    free_vram = gpu_free_vram_mb()
    if free_vram is None:
        raise RuntimeError("Freier GPU-Speicher konnte nicht sicher gemessen werden.")
    if free_vram < MINIMUM_FREE_VRAM_MB:
        raise RuntimeError(
            f"Zu wenig freier GPU-Speicher: {free_vram} MB statt "
            f"mindestens {MINIMUM_FREE_VRAM_MB} MB."
        )
    return free_vram


def _write_runtime_yaml(path: Path, dataset: ValidatedDataset) -> None:
    classes = (dataset.root / "classes.txt").read_text(
        encoding="utf-8-sig"
    ).splitlines()
    lines = [
        f"path: {dataset.root.as_posix()}",
        "train: images/train",
        "val: images/val",
        f"nc: {len(classes)}",
        "names:",
        *(f"  {index}: {name}" for index, name in enumerate(classes)),
    ]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def _remove_ultralytics_label_caches(dataset: ValidatedDataset) -> None:
    labels_root = (dataset.root / "labels").resolve()
    for name in ("train.cache", "val.cache"):
        cache_path = (labels_root / name).resolve()
        if cache_path.parent != labels_root:
            raise RuntimeError(f"Unsicherer Cachepfad: {cache_path}")
        if cache_path.is_symlink():
            raise RuntimeError(f"Cachepfad ist eine Verknuepfung: {cache_path}")
        if cache_path.is_file():
            cache_path.unlink()


def _completed_epochs(results_csv: Path) -> int:
    if not results_csv.is_file():
        return 0
    with results_csv.open("r", encoding="utf-8-sig", newline="") as stream:
        return sum(1 for _ in csv.DictReader(stream))


def _json_safe(value: Any) -> Any:
    if value is None or isinstance(value, (str, int, float, bool)):
        return value
    if isinstance(value, dict):
        return {str(key): _json_safe(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [_json_safe(item) for item in value]
    if hasattr(value, "item"):
        return _json_safe(value.item())
    return str(value)


def train(
    dataset: ValidatedDataset,
    base_weights: Path,
    candidates_root: Path,
    epochs: int,
    patience: int,
    candidate_tag: str | None,
) -> Path:
    weights = base_weights.resolve()
    if not weights.is_file():
        raise ValueError(f"Basisgewicht fehlt: {weights}")
    if epochs < 1:
        raise ValueError("epochs muss mindestens 1 sein.")
    if patience < 0:
        raise ValueError("patience darf nicht negativ sein.")
    normalized_tag = (candidate_tag or "").strip().lower()
    if normalized_tag and not re.fullmatch(r"[a-z0-9][a-z0-9_-]{0,31}", normalized_tag):
        raise ValueError("candidate-tag darf nur a-z, 0-9, _ und - enthalten.")

    free_vram = ensure_training_resources()
    candidate_name = f"bcc_bogen_{dataset.plan_id[:12]}"
    if normalized_tag:
        candidate_name += f"_{normalized_tag}"
    candidate_root = candidates_root.resolve() / candidate_name
    if candidate_root.exists():
        raise FileExistsError(
            f"Der Kandidatenordner existiert bereits und wird nicht ueberschrieben: "
            f"{candidate_root}"
        )
    candidate_root.mkdir(parents=True)
    runtime_yaml = candidate_root / "data.runtime.yaml"
    _write_runtime_yaml(runtime_yaml, dataset)
    _remove_ultralytics_label_caches(dataset)

    from ultralytics import YOLO

    model = YOLO(str(weights))
    try:
        # Augmentierung gemaess Trainingsplan (Trainingsplan_Detail_KI-Pipeline.md,
        # Phase 2a): flipud=0.0, fliplr=0.0 — die Uhrlage des Befundes ist fachlich
        # verbindlich und darf nie gespiegelt werden. "Licht-Augmentierung" ist als
        # leichte Helligkeits-/Farbrausch-Augmentierung umgesetzt (keine Geometrie).
        result = model.train(
            data=str(runtime_yaml),
            epochs=epochs,
            imgsz=1280,
            batch=3,
            workers=0,
            patience=patience,
            device=0,
            seed=42,
            deterministic=True,
            cache=False,
            close_mosaic=5,
            flipud=0.0,
            fliplr=0.0,
            hsv_h=0.01,
            hsv_s=0.3,
            hsv_v=0.3,
            project=str(candidate_root),
            name="run",
            exist_ok=False,
            plots=True,
            verbose=True,
        )
    finally:
        _remove_ultralytics_label_caches(dataset)

    trained_weights = candidate_root / "run" / "weights" / "best.pt"
    if not trained_weights.is_file():
        raise RuntimeError(f"Training endete ohne best.pt: {trained_weights}")
    candidate_weights = candidate_root / "best.pt"
    shutil.copy2(trained_weights, candidate_weights)
    candidate_manifest = {
        "schema_version": "1.0",
        "candidate_status": "not_deployed",
        "pilot": PILOT_CLASS_NAME,
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "dataset": {
            "plan_id": dataset.plan_id,
            "manifest_sha256": dataset.manifest_sha256,
            "images": dataset.image_count,
            "train_images": dataset.train_count,
            "validation_images": dataset.validation_count,
            "instances": dataset.instance_count,
        },
        "training": {
            "epochs_requested": epochs,
            "epochs_completed": _completed_epochs(candidate_root / "run" / "results.csv"),
            "patience": patience,
            "image_size": 1280,
            "batch": 3,
            "seed": 42,
            "deterministic": True,
            "free_vram_mb_at_start": free_vram,
            "results": _json_safe(getattr(result, "results_dict", None)),
        },
        "weights": {
            "base_path": str(weights),
            "base_sha256": _sha256_file(weights),
            "candidate_path": str(candidate_weights),
            "candidate_sha256": _sha256_file(candidate_weights),
        },
    }
    (candidate_root / "candidate_manifest.json").write_text(
        json.dumps(candidate_manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return candidate_root


def main() -> int:
    repository_root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--dataset", type=Path)
    parser.add_argument(
        "--base-weights",
        type=Path,
        default=repository_root / "sidecar" / "models" / "yolo26m" / "yolo26m.pt",
    )
    parser.add_argument("--epochs", type=int, default=40)
    parser.add_argument(
        "--patience",
        type=int,
        default=10,
        help=(
            "Early-Stopping-Geduld in Epochen (Default 10, empfohlener Wert). "
            "0 bleibt als explizite Option erlaubt und fuehrt alle Epochen aus."
        ),
    )
    parser.add_argument(
        "--candidate-tag",
        help="Optionaler Zusatz fuer einen getrennten Wiederholungskandidaten.",
    )
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="Prueft Datensatz und GPU-Zustand, startet aber kein Training.",
    )
    arguments = parser.parse_args()

    dataset_root = arguments.knowledge_root / "training" / "datasets"
    dataset_path = arguments.dataset or discover_dataset(dataset_root)
    validated = validate_dataset(dataset_path, dataset_root)
    print(
        f"BCC-Pilotdatensatz geprueft: {validated.image_count} Bilder "
        f"({validated.train_count} Train, {validated.validation_count} Pruefung), "
        f"{validated.instance_count} BCC-BBoxen."
    )

    if arguments.check_only:
        free_vram = gpu_free_vram_mb()
        is_sidecar_running = sidecar_running()
        blocked = (
            is_sidecar_running
            or free_vram is None
            or free_vram < MINIMUM_FREE_VRAM_MB
        )
        print(
            f"Trainingsstatus: {'GESPERRT' if blocked else 'BEREIT'} | "
            f"Sidecar: {'laeuft' if is_sidecar_running else 'aus'} | "
            f"freier VRAM: {free_vram if free_vram is not None else 'unbekannt'} MB"
        )
        return 0

    candidate = train(
        validated,
        arguments.base_weights,
        arguments.knowledge_root / "training" / "models" / "candidates",
        arguments.epochs,
        arguments.patience,
        arguments.candidate_tag,
    )
    print(f"BCC-Kandidat fertig (nicht aktiviert): {candidate}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
