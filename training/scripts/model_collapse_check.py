"""Schreibfreier BBox-Geometrie-/Kollaps-Test fuer YOLO-Detect-Modelle.

Das Werkzeug prueft ausschliesslich, ob ein Modell auf verschiedenen Bildern
immer dieselbe Box liefert. Gold-IoU, mAP und Aktivierungen im kuratierten
Negativ-/Hintergrund-Pool sind Zusatzwerte und keine Qualitaetsfreigabe.
Modelle und Eingangsdaten werden nicht veraendert; einzige optionale Ausgabe
ist ein JSON-Bericht unter <KnowledgeRoot>/training/reports.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import statistics
import tempfile
from dataclasses import dataclass
from datetime import datetime, timezone
from enum import Enum
from pathlib import Path
from typing import Any, Sequence


NEGATIVE_IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png"}
GOLD_HIT_IOU = 0.5
COLLAPSE_PAIR_SHARE = 0.50
COLLAPSE_STD = 0.02
COLLAPSE_DETECTION_RATE = 0.80
MIN_DETECTION_RATE = 0.20
MIN_TEST_IMAGES = 10
MIN_DETECTIONS = 5
VERDICT_HINWEIS = (
    "Das Verdikt bewertet nur BBox-Geometrie/Kollaps. "
    "Es ist keine Modell- oder Qualitaetsfreigabe."
)


@dataclass(frozen=True)
class Box:
    """Normalisierte Box (Mittenformat). conf=1.0 fuer menschliches Gold."""

    cx: float
    cy: float
    w: float
    h: float
    conf: float = 1.0


@dataclass(frozen=True)
class GoldSample:
    sample_id: str
    frame_path: Path
    box: Box


@dataclass(frozen=True)
class CollapseMetrics:
    bilder: int
    mit_vorhersage: int
    ohne_treffer_anteil: float
    detektionsrate: float
    paare_gesamt: int
    paare_identisch: int
    paar_anteil: float
    std_cx: float
    std_cy: float
    std_w: float
    std_h: float


@dataclass(frozen=True)
class GoldMetrics:
    samples: int
    trefferquote: float
    mean_iou: float
    ious: tuple[float, ...]


@dataclass(frozen=True)
class PoolActivationMetrics:
    bilder: int
    aktivierungen: int
    rate: float
    dateien: tuple[str, ...]


class VerdictStatus(str, Enum):
    PASS = "PASS"
    FAIL = "FAIL"
    INCONCLUSIVE = "INCONCLUSIVE"


@dataclass(frozen=True)
class Verdict:
    status: VerdictStatus
    gruende: tuple[str, ...]
    hinweis: str = VERDICT_HINWEIS

    @property
    def passed(self) -> bool:
        return self.status is VerdictStatus.PASS


def box_iou(a: Box, b: Box) -> float:
    """IoU zweier normalisierter Boxen im Mittenformat."""
    a_x1, a_y1 = a.cx - a.w / 2, a.cy - a.h / 2
    a_x2, a_y2 = a.cx + a.w / 2, a.cy + a.h / 2
    b_x1, b_y1 = b.cx - b.w / 2, b.cy - b.h / 2
    b_x2, b_y2 = b.cx + b.w / 2, b.cy + b.h / 2
    inter_w = min(a_x2, b_x2) - max(a_x1, b_x1)
    inter_h = min(a_y2, b_y2) - max(a_y1, b_y1)
    if inter_w <= 0 or inter_h <= 0:
        return 0.0
    inter = inter_w * inter_h
    union = a.w * a.h + b.w * b.h - inter
    return inter / union if union > 0 else 0.0


def best_box(boxes: Sequence[Box]) -> Box | None:
    """Beste Vorhersage = hoechste Konfidenz; None bei keinem Treffer."""
    if not boxes:
        return None
    return max(boxes, key=lambda box: box.conf)


def collapse_metrics(boxes: Sequence[Box | None], iou_dup: float) -> CollapseMetrics:
    """Kollaps-Kennzahlen ueber alle untersuchten Bilder.

    paar_anteil bezieht sich auf alle ungeordneten Bildpaare, bei denen beide
    Bilder eine Vorhersage haben. Die Streuung laeuft ueber die vorhandenen
    Vorhersagen (Population, bei weniger als zwei Boxen 0.0).
    """
    present = [box for box in boxes if box is not None]
    bilder = len(boxes)
    paare_gesamt = len(present) * (len(present) - 1) // 2
    paare_identisch = 0
    for first in range(len(present)):
        for second in range(first + 1, len(present)):
            if box_iou(present[first], present[second]) >= iou_dup:
                paare_identisch += 1

    def _std(values: list[float]) -> float:
        return statistics.pstdev(values) if len(values) >= 2 else 0.0

    detektionsrate = len(present) / bilder if bilder else 0.0
    return CollapseMetrics(
        bilder=bilder,
        mit_vorhersage=len(present),
        ohne_treffer_anteil=1.0 - detektionsrate,
        detektionsrate=detektionsrate,
        paare_gesamt=paare_gesamt,
        paare_identisch=paare_identisch,
        paar_anteil=paare_identisch / paare_gesamt if paare_gesamt else 0.0,
        std_cx=_std([box.cx for box in present]),
        std_cy=_std([box.cy for box in present]),
        std_w=_std([box.w for box in present]),
        std_h=_std([box.h for box in present]),
    )


def gold_metrics(
    predictions: Sequence[Box | None],
    gold_boxes: Sequence[Box],
    hit_iou: float = GOLD_HIT_IOU,
) -> GoldMetrics:
    """Qualitaet gegen Gold. Fehlende Vorhersage zaehlt als IoU 0."""
    if len(predictions) != len(gold_boxes):
        raise ValueError("Vorhersagen und Gold-Boxen passen nicht zusammen.")
    ious = tuple(
        box_iou(prediction, gold) if prediction is not None else 0.0
        for prediction, gold in zip(predictions, gold_boxes)
    )
    samples = len(gold_boxes)
    hits = sum(iou >= hit_iou for iou in ious)
    return GoldMetrics(
        samples=samples,
        trefferquote=hits / samples if samples else 0.0,
        mean_iou=sum(ious) / samples if samples else 0.0,
        ious=ious,
    )


def pool_activation_metrics(
    predictions: Sequence[tuple[str, Box | None]],
) -> PoolActivationMetrics:
    """Aktivierungen im Pool, ohne dessen fachliche Schadensfreiheit zu behaupten."""
    dateien = tuple(name for name, box in predictions if box is not None)
    bilder = len(predictions)
    return PoolActivationMetrics(
        bilder=bilder,
        aktivierungen=len(dateien),
        rate=len(dateien) / bilder if bilder else 0.0,
        dateien=dateien,
    )


def decide_verdict(
    metrics: CollapseMetrics,
    *,
    inference_error_count: int = 0,
    min_test_images: int = MIN_TEST_IMAGES,
    min_detections: int = MIN_DETECTIONS,
    min_detection_rate: float = MIN_DETECTION_RATE,
) -> Verdict:
    """Dreistufiges Verdikt fuer den reinen BBox-Geometrie-/Kollaps-Test."""
    unklar: list[str] = []
    if inference_error_count:
        unklar.append(
            f"{inference_error_count} Inferenzfehler: Der Prueflauf ist nicht vollstaendig."
        )
    if metrics.bilder < min_test_images:
        unklar.append(
            f"Nur {metrics.bilder} Pruefbilder; mindestens {min_test_images} sind erforderlich."
        )
    if metrics.mit_vorhersage < min_detections:
        unklar.append(
            f"Nur {metrics.mit_vorhersage} Erkennungen; "
            f"mindestens {min_detections} sind erforderlich."
        )
    if metrics.detektionsrate < min_detection_rate:
        unklar.append(
            f"Detektionsrate {metrics.detektionsrate:.1%} < "
            f"{min_detection_rate:.0%}: zu wenige Erkennungen fuer ein "
            "belastbares Kollaps-Verdikt."
        )
    if unklar:
        return Verdict(
            status=VerdictStatus.INCONCLUSIVE,
            gruende=tuple(unklar),
        )

    gruende: list[str] = []
    if metrics.paar_anteil >= COLLAPSE_PAIR_SHARE:
        gruende.append(
            f"Kollaps: {metrics.paar_anteil:.1%} der moeglichen Bildpaare mit "
            f"nahezu identischer Box (Schwelle {COLLAPSE_PAIR_SHARE:.0%})."
        )
    max_std = max(metrics.std_cx, metrics.std_cy, metrics.std_w, metrics.std_h)
    if max_std < COLLAPSE_STD and metrics.detektionsrate > COLLAPSE_DETECTION_RATE:
        gruende.append(
            f"Kollaps: Streuung der Boxen hoechstens {max_std:.4f} < {COLLAPSE_STD} "
            f"bei Detektionsrate {metrics.detektionsrate:.1%} "
            f"> {COLLAPSE_DETECTION_RATE:.0%}."
        )
    if gruende:
        return Verdict(status=VerdictStatus.FAIL, gruende=tuple(gruende))
    return Verdict(
        status=VerdictStatus.PASS,
        gruende=(
            f"Kein BBox-Kollaps im geprueften Bestand: "
            f"Paar-Anteil {metrics.paar_anteil:.1%}, "
            f"maximale Streuung {max_std:.4f}, "
            f"Detektionsrate {metrics.detektionsrate:.1%}.",
        ),
    )


def verdict_exit_code(verdict: Verdict) -> int:
    if verdict.status is VerdictStatus.PASS:
        return 0
    if verdict.status is VerdictStatus.FAIL:
        return 1
    return 2


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def build_provenance(
    images: Sequence[Path],
    samples: Sequence[GoldSample],
    negative_pool: Sequence[Path],
    dataset: Path | None,
) -> dict[str, Any]:
    """Bindet Eingaben per SHA-256 und weist bekannte Ueberschneidungen aus."""
    hash_cache: dict[Path, str] = {}

    def fingerprint(path: Path) -> dict[str, str]:
        resolved = path.resolve()
        sha256 = hash_cache.get(resolved)
        if sha256 is None:
            sha256 = _sha256_file(resolved)
            hash_cache[resolved] = sha256
        return {"bild": str(path), "sha256": sha256}

    image_files = [fingerprint(path) for path in images]
    gold_files = [
        {"sample_id": sample.sample_id, **fingerprint(sample.frame_path)}
        for sample in samples
    ]
    pool_files = [fingerprint(path) for path in negative_pool]

    dataset_hashes: set[str] | None = None
    dataset_document: dict[str, Any] = {
        "angegeben": dataset is not None,
        "pfad": str(dataset) if dataset is not None else None,
        "manifest_gefunden": False,
        "manifest_sha256": None,
        "bild_hashes": None,
    }
    if dataset is not None:
        manifest_path = dataset / "manifest.json"
        if manifest_path.is_file():
            manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
            if not isinstance(manifest, dict):
                raise ValueError(f"{manifest_path} muss ein JSON-Objekt enthalten.")
            entries = manifest.get("images")
            if not isinstance(entries, list):
                raise ValueError(f"{manifest_path} enthaelt keine gueltige images-Liste.")
            dataset_hashes = {
                str(entry.get("image_sha256") or "").strip().lower()
                for entry in entries
                if isinstance(entry, dict)
                and re.fullmatch(
                    r"[0-9a-fA-F]{64}",
                    str(entry.get("image_sha256") or "").strip(),
                )
            }
            dataset_document.update(
                {
                    "manifest_gefunden": True,
                    "manifest_sha256": _sha256_file(manifest_path),
                    "bild_hashes": len(dataset_hashes),
                }
            )

    image_hashes = {item["sha256"] for item in image_files}
    gold_hashes = {item["sha256"] for item in gold_files}
    pool_hashes = {item["sha256"] for item in pool_files}

    def overlap(left: set[str], right: set[str] | None) -> dict[str, Any]:
        if right is None:
            return {"bekannt": False, "anzahl": None, "sha256": []}
        hashes = sorted(left & right)
        return {"bekannt": True, "anzahl": len(hashes), "sha256": hashes}

    return {
        "pruefbestand": image_files,
        "gold_referenz": gold_files,
        "negativ_pool": pool_files,
        "datensatz_manifest": dataset_document,
        "ueberschneidungen": {
            "pruefbestand_gold_referenz": overlap(image_hashes, gold_hashes),
            "pruefbestand_negativ_pool": overlap(image_hashes, pool_hashes),
            "gold_referenz_negativ_pool": overlap(gold_hashes, pool_hashes),
            "pruefbestand_datensatz_manifest": overlap(
                image_hashes,
                dataset_hashes,
            ),
        },
    }


def _load_json_array(path: Path) -> list[dict[str, Any]]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, list):
        raise ValueError(f"{path} muss ein JSON-Array enthalten.")
    if any(not isinstance(item, dict) for item in value):
        raise ValueError(f"{path} enthaelt einen ungueltigen Eintrag.")
    return value


def load_gold_samples(
    samples_json: Path,
    limit: int | None = None,
) -> tuple[list[GoldSample], int]:
    """Nur freigegebene (Approved), menschlich bestaetigte Samples mit Box."""
    samples: list[GoldSample] = []
    fehlende_bilder = 0
    for item in _load_json_array(samples_json):
        approved = item.get("Status") == 1 or item.get("Status") == "Approved"
        if not (
            approved
            and item.get("HumanConfirmed") is True
            and item.get("HasBbox") is True
        ):
            continue
        try:
            box = Box(
                cx=float(item["BboxXCenter"]),
                cy=float(item["BboxYCenter"]),
                w=float(item["BboxWidth"]),
                h=float(item["BboxHeight"]),
            )
        except (KeyError, TypeError, ValueError):
            continue  # unvollstaendige Box ist kein Gold
        frame_path = Path(str(item.get("FramePath") or "").strip())
        if not frame_path.is_file():
            fehlende_bilder += 1
            continue
        samples.append(
            GoldSample(
                sample_id=str(item.get("SampleId") or "").strip(),
                frame_path=frame_path,
                box=box,
            )
        )
    samples.sort(key=lambda sample: sample.sample_id.casefold())
    if limit is not None:
        samples = samples[:limit]
    return samples, fehlende_bilder


def _list_images(directory: Path, limit: int | None) -> list[Path]:
    """Bilder direkt in der Wurzel (nicht rekursiv); fehlender Ordner ist leer."""
    if not directory.is_dir():
        return []
    images = sorted(
        (
            path
            for path in directory.iterdir()
            if path.is_file() and path.suffix.casefold() in NEGATIVE_IMAGE_SUFFIXES
        ),
        key=lambda path: path.name.casefold(),
    )
    if limit is not None:
        images = images[:limit]
    return images


def load_images(images_dir: Path, limit: int | None = None) -> list[Path]:
    """Vom Aufrufer angegebener Pruefbestand fuer die Kollaps-Statistik."""
    return _list_images(images_dir, limit)


def load_negatives(negatives_dir: Path, limit: int | None = None) -> list[Path]:
    """Kuratierter Negativ-/Hintergrund-Pool; keine automatische Labelpruefung."""
    return _list_images(negatives_dir, limit)


def load_model(weights: Path) -> Any:
    """Laedt das YOLO-Modell. Lazy-Import: Tests und --help brauchen kein torch."""
    from ultralytics import YOLO

    return YOLO(str(weights))


def predict_best_boxes(
    model: Any,
    images: Sequence[Path],
    conf: float,
    imgsz: int,
) -> tuple[list[Box | None], list[str]]:
    """Beste Box je Bild. Fehler pro Bild protokollieren, nicht abbrechen."""
    predictions: list[Box | None] = []
    errors: list[str] = []
    for image in images:
        try:
            results = model.predict(
                source=str(image), conf=conf, imgsz=imgsz, verbose=False
            )
            boxes: list[Box] = []
            for result in results:
                xywhn = result.boxes.xywhn
                confs = result.boxes.conf
                if xywhn is None or confs is None:
                    continue
                for row, confidence in zip(xywhn.tolist(), confs.tolist()):
                    boxes.append(
                        Box(
                            cx=float(row[0]),
                            cy=float(row[1]),
                            w=float(row[2]),
                            h=float(row[3]),
                            conf=float(confidence),
                        )
                    )
            predictions.append(best_box(boxes))
        except Exception as exc:  # ein defektes Bild darf den Lauf nicht stoppen
            errors.append(f"{image}: {exc}")
            predictions.append(None)
    return predictions, errors


def _write_runtime_yaml(path: Path, data_yaml: Path, dataset: Path) -> None:
    """Laufzeit-Yaml mit absolutem path; der Datensatz selbst bleibt unveraendert."""
    import yaml

    data = yaml.safe_load(data_yaml.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        raise ValueError(f"Ungueltige data.yaml: {data_yaml}")
    data["path"] = dataset.resolve().as_posix()
    path.write_text(
        yaml.safe_dump(data, allow_unicode=True, sort_keys=False),
        encoding="utf-8",
    )


def run_map_validation(model: Any, dataset: Path, imgsz: int) -> dict[str, Any]:
    """ultralytics val (read-only, kein Training); Laufartefakte nur im Temp-Ordner."""
    data_yaml = dataset / "data.yaml"
    if not data_yaml.is_file():
        raise ValueError(f"data.yaml fehlt im Datensatzordner: {dataset}")
    labels_root = dataset / "labels"
    cache_files = [labels_root / "train.cache", labels_root / "val.cache"]
    vorhandene_caches = {cache for cache in cache_files if cache.exists()}
    with tempfile.TemporaryDirectory() as temporary:
        runtime_yaml = Path(temporary) / "data.runtime.yaml"
        _write_runtime_yaml(runtime_yaml, data_yaml, dataset)
        try:
            results = model.val(
                data=str(runtime_yaml),
                imgsz=imgsz,
                project=temporary,
                name="val",
                verbose=False,
            )
        finally:
            # Label-Caches, die dieser Lauf im Datensatz angelegt hat, wieder entfernen
            for cache in cache_files:
                if cache not in vorhandene_caches and cache.exists():
                    cache.unlink()
    box = results.box
    names = getattr(results, "names", {}) or {}
    classes: list[dict[str, Any]] = []
    indices = getattr(box, "ap_class_index", None)
    if indices is not None:
        for position, class_index in enumerate(indices):
            classes.append(
                {
                    "klasse": str(names.get(int(class_index), int(class_index))),
                    "p": round(float(box.p[position]), 4),
                    "r": round(float(box.r[position]), 4),
                    "map50": round(float(box.ap50[position]), 4),
                    "map50_95": round(float(box.ap[position]), 4),
                }
            )
    return {
        "datensatz": str(dataset),
        "imgsz": imgsz,
        "gesamt": {
            "p": round(float(box.mp), 4),
            "r": round(float(box.mr), 4),
            "map50": round(float(box.map50), 4),
            "map50_95": round(float(box.map), 4),
        },
        "klassen": classes,
    }


def _atomic_write_json(path: Path, document: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    data = (json.dumps(document, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    try:
        with temporary.open("xb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def _box_document(box: Box | None) -> dict[str, float] | None:
    if box is None:
        return None
    return {
        "cx": round(box.cx, 6),
        "cy": round(box.cy, 6),
        "w": round(box.w, 6),
        "h": round(box.h, 6),
        "conf": round(box.conf, 4),
    }


def build_report(
    weights: Path,
    weights_sha256: str,
    args: argparse.Namespace,
    images: Sequence[Path],
    image_predictions: Sequence[Box | None],
    samples: Sequence[GoldSample],
    fehlende_bilder: int,
    negative_pool: Sequence[Path],
    gold_predictions: Sequence[Box | None],
    inferenz_fehler: Sequence[str],
    collapse: CollapseMetrics,
    gold: GoldMetrics,
    pool_activations: PoolActivationMetrics,
    map_result: dict[str, Any] | None,
    provenance: dict[str, Any],
    verdict: Verdict,
) -> dict[str, Any]:
    return {
        "schema_version": "2.0",
        "werkzeug": "model_collapse_check",
        "erstellt_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "pruefart": {
            "typ": "bbox_geometrie_kollaps",
            "aussage": (
                "Prueft, ob vorhandene Vorhersage-Boxen auf verschiedenen Bildern "
                "geometrisch kollabieren."
            ),
            "keine_qualitaetsfreigabe": True,
            "zusatzmessungen_im_verdikt": False,
        },
        "modell": {
            "pfad": str(weights),
            "name": weights.stem,
            "sha256": weights_sha256,
        },
        "schwellen": {
            "conf": args.conf,
            "iou_dup": args.iou_dup,
            "gold_treffer_iou": GOLD_HIT_IOU,
            "kollaps_paar_anteil": COLLAPSE_PAIR_SHARE,
            "kollaps_std": COLLAPSE_STD,
            "kollaps_detektionsrate": COLLAPSE_DETECTION_RATE,
            "min_pruefbilder": args.min_test_images,
            "min_erkennungen": args.min_detections,
            "min_detektionsrate": args.min_detection_rate,
        },
        "parameter": {
            "imgsz": args.imgsz,
            "limit": args.limit,
        },
        "bildzahlen": {
            "pruefbilder": len(images),
            "gold_samples": len(samples),
            "gold_bilder_fehlend": fehlende_bilder,
            "negativ_pool_bilder": len(negative_pool),
            "limit": args.limit,
        },
        "definitionen": {
            "paar_anteil": (
                "Anteil der ungeordneten Bildpaare (beide mit Vorhersage) "
                "mit Box-IoU >= iou_dup."
            ),
            "std_cx_cy_w_h": "Streuung (Population) der vorhandenen Vorhersage-Boxen.",
            "detektionsrate": "Anteil der Pruefbilder (--images-dir) mit Vorhersage-Box.",
            "gold_trefferquote": "Anteil der Gold-Samples mit IoU(Vorhersage, Gold) >= gold_treffer_iou.",
            "gold_mean_iou": "Mittleres IoU ueber alle Gold-Samples; fehlende Vorhersage zaehlt 0.",
            "negativ_pool_aktivierungsrate": (
                "Anteil der Dateien im kuratierten Negativ-/Hintergrund-Pool "
                "mit mindestens einer Vorhersage-Box. Das Werkzeug verifiziert "
                "deren fachliche Schadensfreiheit nicht."
            ),
            "verdikt": (
                "INCONCLUSIVE bei Inferenzfehlern oder zu kleinem Pruefbestand. "
                "Sonst FAIL bei nachgewiesenem BBox-Kollaps, andernfalls PASS. "
                "PASS ist keine Qualitaetsfreigabe."
            ),
        },
        "pruefbestand": {
            "quelle": str(args.images_dir),
            "bilder": collapse.bilder,
            "kollaps": {
                "mit_vorhersage": collapse.mit_vorhersage,
                "ohne_treffer_anteil": round(collapse.ohne_treffer_anteil, 6),
                "detektionsrate": round(collapse.detektionsrate, 6),
                "paare_gesamt": collapse.paare_gesamt,
                "paare_identisch": collapse.paare_identisch,
                "paar_anteil": round(collapse.paar_anteil, 6),
                "std_cx": round(collapse.std_cx, 6),
                "std_cy": round(collapse.std_cy, 6),
                "std_w": round(collapse.std_w, 6),
                "std_h": round(collapse.std_h, 6),
            },
            "einzelergebnisse": [
                {
                    "bild": str(image),
                    "vorhersage": _box_document(prediction),
                }
                for image, prediction in zip(images, image_predictions)
            ],
        },
        "zusatzmessungen_nicht_im_verdikt": {
            "gold_referenz": {
                "samples": gold.samples,
                "trefferquote": round(gold.trefferquote, 6),
                "mean_iou": round(gold.mean_iou, 6),
                "einzelergebnisse": [
                    {
                        "sample_id": sample.sample_id,
                        "bild": str(sample.frame_path),
                        "gold": _box_document(sample.box),
                        "vorhersage": _box_document(prediction),
                        "iou": round(iou, 6),
                    }
                    for sample, prediction, iou in zip(
                        samples, gold_predictions, gold.ious
                    )
                ],
            },
            "negativ_pool_aktivierungen": {
                "bilder": pool_activations.bilder,
                "aktivierungen": pool_activations.aktivierungen,
                "rate": round(pool_activations.rate, 6),
                "dateien": list(pool_activations.dateien),
            },
            "map": map_result,
        },
        "provenienz": provenance,
        "inferenz_fehler": list(inferenz_fehler),
        "verdikt": {
            "status": verdict.status.value,
            "exit_code": verdict_exit_code(verdict),
            "gruende": list(verdict.gruende),
            "hinweis": verdict.hinweis,
        },
    }


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Schreibfreier BBox-Kollaps-Test fuer YOLO-Detect-Modelle."
    )
    parser.add_argument("--weights", type=Path, required=True, help="Pflicht: Modellgewichte (.pt).")
    parser.add_argument(
        "--knowledge-root",
        type=Path,
        default=Path(os.getenv("SEWERSTUDIO_KNOWLEDGE_ROOT", r"C:\KI_BRAIN")),
    )
    parser.add_argument("--samples-json", type=Path, default=None)
    parser.add_argument("--negatives-dir", type=Path, default=None)
    parser.add_argument("--conf", type=float, default=0.25)
    parser.add_argument("--iou-dup", type=float, default=0.90)
    parser.add_argument(
        "--imgsz",
        type=int,
        default=1280,
        help="Einheitliche Inferenz-Bildgroesse (Trainings-/Pruefaufloesung des Pilots).",
    )
    parser.add_argument(
        "--images-dir",
        type=Path,
        default=None,
        help=(
            "Pruefbestand fuer Kollaps-Statistik und Detektionsrate "
            "(Default: <KnowledgeRoot>/eval_set/images)."
        ),
    )
    parser.add_argument(
        "--min-test-images",
        type=int,
        default=MIN_TEST_IMAGES,
        help=f"Mindestzahl Pruefbilder fuer PASS/FAIL (Default: {MIN_TEST_IMAGES}).",
    )
    parser.add_argument(
        "--min-detections",
        type=int,
        default=MIN_DETECTIONS,
        help=f"Mindestzahl Erkennungen fuer PASS/FAIL (Default: {MIN_DETECTIONS}).",
    )
    parser.add_argument(
        "--min-detection-rate",
        type=float,
        default=MIN_DETECTION_RATE,
        help=(
            "Mindestanteil Pruefbilder mit Erkennung fuer PASS/FAIL "
            f"(Default als Anteil: {MIN_DETECTION_RATE:.2f})."
        ),
    )
    parser.add_argument(
        "--dataset",
        type=Path,
        default=None,
        help="Optional: exportierter Datensatzordner mit data.yaml fuer mAP per ultralytics val.",
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=None,
        help="Optionale Bildzahl (je Liste, deterministisch von vorn).",
    )
    parser.add_argument(
        "--report",
        action=argparse.BooleanOptionalAction,
        default=True,
        help="JSON-Bericht unter <KnowledgeRoot>/training/reports schreiben (Default).",
    )
    args = parser.parse_args()
    if args.imgsz <= 0:
        parser.error("--imgsz muss groesser als 0 sein.")
    if not 0.0 <= args.conf <= 1.0:
        parser.error("--conf muss zwischen 0 und 1 liegen.")
    if not 0.0 <= args.iou_dup <= 1.0:
        parser.error("--iou-dup muss zwischen 0 und 1 liegen.")
    if args.min_test_images < 2:
        parser.error("--min-test-images muss mindestens 2 sein.")
    if args.min_detections < 2:
        parser.error("--min-detections muss mindestens 2 sein.")
    if not 0.0 <= args.min_detection_rate <= 1.0:
        parser.error("--min-detection-rate muss zwischen 0 und 1 liegen.")
    if args.limit is not None and args.limit <= 0:
        parser.error("--limit muss groesser als 0 sein.")
    root = args.knowledge_root
    if args.samples_json is None:
        args.samples_json = root / "training_samples.json"
    if args.negatives_dir is None:
        args.negatives_dir = root / "training" / "negatives" / "bcc_pilot"
    if args.images_dir is None:
        args.images_dir = root / "eval_set" / "images"
    return args


def main() -> int:
    args = _parse_args()
    weights = args.weights
    if not weights.is_file():
        raise ValueError(f"Modellgewichte fehlen: {weights}")

    images = load_images(args.images_dir, args.limit)
    samples, fehlende_bilder = load_gold_samples(args.samples_json, args.limit)
    if not samples:
        raise ValueError("Keine freigegebenen Gold-Samples mit Box gefunden.")
    negative_pool = load_negatives(args.negatives_dir, args.limit)
    weights_sha256 = _sha256_file(weights)
    provenance = build_provenance(
        images=images,
        samples=samples,
        negative_pool=negative_pool,
        dataset=args.dataset,
    )

    model = load_model(weights)
    image_predictions, image_errors = predict_best_boxes(
        model, images, args.conf, args.imgsz
    )
    gold_predictions, gold_errors = predict_best_boxes(
        model, [sample.frame_path for sample in samples], args.conf, args.imgsz
    )
    negative_predictions, negative_errors = predict_best_boxes(
        model, negative_pool, args.conf, args.imgsz
    )
    inferenz_fehler = image_errors + gold_errors + negative_errors

    collapse = collapse_metrics(image_predictions, args.iou_dup)
    gold = gold_metrics(gold_predictions, [sample.box for sample in samples])
    pool_activations = pool_activation_metrics(
        [(path.name, box) for path, box in zip(negative_pool, negative_predictions)]
    )
    map_result = (
        run_map_validation(model, args.dataset, args.imgsz)
        if args.dataset is not None
        else None
    )
    verdict = decide_verdict(
        collapse,
        inference_error_count=len(inferenz_fehler),
        min_test_images=args.min_test_images,
        min_detections=args.min_detections,
        min_detection_rate=args.min_detection_rate,
    )

    report = build_report(
        weights=weights,
        weights_sha256=weights_sha256,
        args=args,
        images=images,
        image_predictions=image_predictions,
        samples=samples,
        fehlende_bilder=fehlende_bilder,
        negative_pool=negative_pool,
        gold_predictions=gold_predictions,
        inferenz_fehler=inferenz_fehler,
        collapse=collapse,
        gold=gold,
        pool_activations=pool_activations,
        map_result=map_result,
        provenance=provenance,
        verdict=verdict,
    )

    report_path: Path | None = None
    if args.report:
        stamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
        safe_name = re.sub(r"[^A-Za-z0-9_.-]+", "_", weights.stem)
        report_path = (
            args.knowledge_root
            / "training"
            / "reports"
            / f"collapse_check_{safe_name}_{stamp}.json"
        )
        _atomic_write_json(report_path, report)

    print(f"Modell: {weights}")
    print(f"SHA-256: {weights_sha256}")
    print(
        f"Pruefbilder: {len(images)} ({args.images_dir}), "
        f"Gold-Samples: {len(samples)} (fehlende Bilder: {fehlende_bilder}), "
        f"Negativ-/Hintergrund-Pool: {len(negative_pool)}, imgsz={args.imgsz}"
    )
    print(
        f"Kollaps (Pruefbestand): Paar-Anteil {collapse.paar_anteil:.1%} "
        f"({collapse.paare_identisch}/{collapse.paare_gesamt} Paare, IoU >= {args.iou_dup}), "
        f"Std cx={collapse.std_cx:.4f} cy={collapse.std_cy:.4f} "
        f"w={collapse.std_w:.4f} h={collapse.std_h:.4f}, "
        f"Detektionsrate {collapse.detektionsrate:.1%}"
    )
    print(
        f"Gold-Referenz: Trefferquote {gold.trefferquote:.1%} (IoU >= {GOLD_HIT_IOU}), "
        f"mittleres IoU {gold.mean_iou:.4f}"
    )
    print(
        f"Negativ-Pool-Aktivierungen: "
        f"{pool_activations.aktivierungen}/{pool_activations.bilder} "
        f"({pool_activations.rate:.1%})"
    )
    if map_result is not None:
        gesamt = map_result["gesamt"]
        print(
            f"mAP: mAP50 {gesamt['map50']:.4f}, mAP50-95 {gesamt['map50_95']:.4f}, "
            f"P {gesamt['p']:.4f}, R {gesamt['r']:.4f} "
            f"({len(map_result['klassen'])} Klassen)"
        )
    if inferenz_fehler:
        print(f"Inferenz-Fehler: {len(inferenz_fehler)} (Details im Bericht)")
    for grund in verdict.gruende:
        print(grund)
    print(f"Verdikt: {verdict.status.value}")
    print(f"Hinweis: {verdict.hinweis}")
    if report_path is not None:
        print(f"Bericht: {report_path}")
    else:
        print("Kein Bericht geschrieben (--no-report).")
    return verdict_exit_code(verdict)


if __name__ == "__main__":
    raise SystemExit(main())
