#!/usr/bin/env python3
"""Erzeugt einen eingefrorenen Mehrklassen-Pruefbestand aus frischen PDF-Fotos.

Die Auswahl verwendet keine Modellvorhersagen. Kunden-PDFs und bereits
extrahierte PDF-Fotos werden nur gelesen. Erst ``--execute`` veroeffentlicht
eine neue, unveraenderliche Kopie unter ``eval_set/subsets``.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Iterable, Sequence

from PIL import Image


SCRIPT_ROOT = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_ROOT.parents[1]
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

import bcc_release_holdout as protection


SCHEMA_VERSION = "1.0"
HOLDOUT_PURPOSE = "detect_release_holdout"
CANDIDATES_PURPOSE = "detect_release_holdout_candidates"
SELECTION_SALT = "detect-release-holdout-pdf-v1"
DEFAULT_MAX_HOLDINGS = 75
DEFAULT_IMAGES_PER_HOLDING = 3
DEFAULT_MINIMUM_HOLDINGS = 30
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")

CLASS_LABELS = (
    (0, "BCA_anschluss", "Seitlicher Anschluss"),
    (1, "BAB_riss", "Riss"),
    (2, "BAC_bruch", "Bruch"),
    (3, "BAA_verformung", "Verformung"),
    (4, "BAF_oberflaeche", "Oberflächenschaden"),
    (5, "BAH_schadanschluss", "Schadhafter Anschluss"),
    (6, "BAI_dichtung", "Einragendes Dichtungsmaterial"),
    (7, "BAJ_verbindung", "Verschobene Rohrverbindung"),
    (8, "BBA_wurzeln", "Wurzeln oder Bewuchs"),
    (9, "BBB_anhaftung", "Anhaftende Stoffe"),
    (10, "BBC_ablagerung", "Ablagerung"),
    (11, "BBD_boden", "Eindringender Boden"),
    (12, "BBF_infiltration", "Infiltration"),
    (13, "SONST_schaden", "Sonstiger Schaden"),
    (14, "BCC_bogen", "Bogen"),
)


@dataclass(frozen=True)
class CandidateBinding:
    candidate_root: Path
    candidate_id: str
    manifest_sha256: str
    weights_sha256: str
    base_model_path: Path
    base_model_sha256: str
    class_map_version: int
    class_map_sha256: str
    vsa_manifest_hash: str


@dataclass(frozen=True)
class SourcePdf:
    path: Path
    sha256: str
    name: str
    holding_key: str
    physical_holding_key: str


@dataclass(frozen=True)
class SourceImage:
    path: Path
    source_root: Path
    sha256: str
    size_bytes: int
    width: int
    height: int
    pdf: SourcePdf
    source_kind: str = "pdf_review_import"
    operator_references: tuple[dict[str, Any], ...] = ()

    @property
    def target_file_name(self) -> str:
        return f"img_{self.sha256}{self.path.suffix.casefold()}"

    @property
    def item_id(self) -> str:
        return f"detect-rh-{self.sha256[:20]}"


@dataclass(frozen=True)
class HoldoutPlan:
    knowledge_root: Path
    candidate: CandidateBinding
    class_map_path: Path
    created_utc: datetime
    contamination: protection.ContaminationSnapshot
    source_roots: tuple[Path, ...]
    pdf_import_root: Path
    items: tuple[SourceImage, ...]
    discovered_pdf_files: int
    matched_import_pdfs: int
    ambiguous_pdf_hashes: int
    blocked_same_holding: int
    blocked_same_hash: int
    holdout_id: str
    target_root: Path


def _load_json(path: Path) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"JSON ist nicht sicher lesbar: {path}") from error


def _require_object(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError(f"{label} muss ein JSON-Objekt sein.")
    return value


def _require_sha256(value: Any, label: str) -> str:
    text = str(value or "").strip().casefold()
    if not SHA256_PATTERN.fullmatch(text):
        raise ValueError(f"{label} ist kein gueltiger SHA-256.")
    return text


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _json_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def _semantic_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def _safe_root(path: Path, label: str) -> Path:
    absolute = Path(os.path.abspath(path))
    if not absolute.is_dir():
        raise ValueError(f"{label} fehlt: {absolute}")
    return protection._safe_existing_path(absolute, absolute, expect_file=False)


def _safe_file(path: Path, root: Path, label: str) -> Path:
    try:
        return protection._safe_existing_path(path, root, expect_file=True)
    except ValueError as error:
        raise ValueError(f"{label} ist unsicher: {path}") from error


def _image_info(path: Path) -> tuple[int, int]:
    protection._validate_image(path)
    try:
        with Image.open(path) as image:
            image.verify()
        with Image.open(path) as image:
            width, height = image.size
    except Exception as error:
        raise ValueError(f"Bild ist nicht sicher decodierbar: {path}") from error
    if width <= 0 or height <= 0:
        raise ValueError(f"Bild besitzt ungueltige Abmessungen: {path}")
    return int(width), int(height)


def _physical_equal(left: str, right: str) -> bool:
    return protection._physical_holding_key(left) == protection._physical_holding_key(right)


def resolve_pdf_holding(pdf_path: Path, source_root: Path) -> str | None:
    """Nimmt nur eine durch Dateiname oder naechsten Ordner belastbare Haltung."""
    relative = pdf_path.relative_to(source_root)
    file_key = protection._numeric_holding_key(pdf_path.stem)
    parent_keys: list[str] = []
    for part in reversed(relative.parts[:-1]):
        key = protection._numeric_holding_key(part)
        if key is not None:
            parent_keys.append(key)
    nearest = parent_keys[0] if parent_keys else None
    if file_key is not None and nearest is not None:
        return file_key if _physical_equal(file_key, nearest) else None
    return file_key or nearest


def validate_candidate(candidate_root: Path, class_map_path: Path) -> CandidateBinding:
    root = _safe_root(candidate_root, "Kandidatenordner")
    manifest_path = _safe_file(root / "candidate_manifest.json", root, "Kandidatenmanifest")
    weights_path = _safe_file(root / "best.pt", root, "Kandidatengewicht")
    manifest = _require_object(_load_json(manifest_path), "Kandidatenmanifest")
    if manifest.get("candidate_status") != "not_deployed":
        raise ValueError("Der Detect-Kandidat muss candidate_status=not_deployed besitzen.")
    if manifest.get("candidate_kind") != "detect_gold":
        raise ValueError("Der Kandidat ist kein detect_gold-Gesamtmodell.")
    dataset = _require_object(manifest.get("dataset"), "Kandidaten-Dataset")
    weights = _require_object(manifest.get("weights"), "Kandidaten-Gewichte")
    expected_weight = _require_sha256(weights.get("candidate_sha256"), "Kandidatengewicht-Hash")
    if _sha256_file(weights_path) != expected_weight:
        raise ValueError("Das Kandidatengewicht stimmt nicht mit seinem Manifest ueberein.")
    base_model_path = Path(str(weights.get("base_path") or ""))
    if not base_model_path.is_absolute() or not base_model_path.is_file():
        raise ValueError("Das gebundene Basismodell fehlt.")
    base_model_path = _safe_file(base_model_path, base_model_path.parent, "Basismodell")
    base_sha = _require_sha256(weights.get("base_sha256"), "Basismodell-Hash")
    if _sha256_file(base_model_path) != base_sha:
        raise ValueError("Das Basismodell stimmt nicht mit dem Kandidatenmanifest ueberein.")

    class_map = _safe_file(class_map_path, class_map_path.parent, "Klassenkarte")
    class_map_sha = _sha256_file(class_map)
    if dataset.get("class_map_version") != 3:
        raise ValueError("Der Kandidat verwendet nicht Klassenkarte v3.")
    if _require_sha256(dataset.get("class_map_sha256"), "Klassenkarten-Hash") != class_map_sha:
        raise ValueError("Die lokale Klassenkarte stimmt nicht mit dem Kandidaten ueberein.")
    class_document = _require_object(_load_json(class_map), "Klassenkarte")
    if class_document.get("version") != 3:
        raise ValueError("Die lokale Klassenkarte ist nicht Version 3.")
    classes = _require_object(class_document.get("classes"), "Klassen")
    expected_classes = {name: class_id for class_id, name, _ in CLASS_LABELS}
    if classes != expected_classes:
        raise ValueError("Die lokale Klassenkarte besitzt nicht die feste 15er-Reihenfolge.")
    vsa_hash = _require_sha256(dataset.get("vsa_manifest_hash"), "VSA-Manifest-Hash")
    if _require_sha256(class_document.get("vsa_manifest_hash"), "Klassenkarten-VSA-Hash") != vsa_hash:
        raise ValueError("VSA-Manifest-Bindung von Kandidat und Klassenkarte widerspricht sich.")
    return CandidateBinding(
        candidate_root=root,
        candidate_id=root.name,
        manifest_sha256=_sha256_file(manifest_path),
        weights_sha256=expected_weight,
        base_model_path=base_model_path,
        base_model_sha256=base_sha,
        class_map_version=3,
        class_map_sha256=class_map_sha,
        vsa_manifest_hash=vsa_hash,
    )


def _assert_candidate_bound_to_snapshot(
    knowledge_root: Path,
    candidate: CandidateBinding,
    contamination: protection.ContaminationSnapshot,
) -> None:
    candidates_root = _safe_root(
        knowledge_root / "training" / "models" / "candidates",
        "KB-Kandidatenordner",
    )
    if (
        not candidate.candidate_id
        or Path(candidate.candidate_id).name != candidate.candidate_id
        or candidate.candidate_id in {".", ".."}
    ):
        raise ValueError("Der Kandidatenordner besitzt keine sichere Kandidaten-ID.")
    expected_root = protection._safe_existing_path(
        candidates_root / candidate.candidate_id,
        candidates_root,
        expect_file=False,
    )
    if os.path.normcase(os.path.abspath(candidate.candidate_root)) != os.path.normcase(
        os.path.abspath(expected_root)
    ):
        raise ValueError(
            "Der Kandidatenordner muss direkt unter "
            "knowledge/training/models/candidates liegen."
        )

    matches: list[dict[str, Any]] = []
    for index, raw in enumerate(contamination.candidates):
        entry = _require_object(raw, f"Kontaminations-Kandidat {index}")
        if str(entry.get("candidate_id") or "") == candidate.candidate_id:
            matches.append(entry)
    if len(matches) != 1:
        raise ValueError(
            "Der ausgewaehlte Kandidat ist im Kontaminationsscan nicht "
            "genau einmal hashgebunden."
        )
    match = matches[0]
    if (
        _require_sha256(
            match.get("candidate_manifest_sha256"),
            "Kandidatenmanifest im Kontaminationsscan",
        )
        != candidate.manifest_sha256
        or _require_sha256(
            match.get("weights_sha256"),
            "Kandidatengewicht im Kontaminationsscan",
        )
        != candidate.weights_sha256
    ):
        raise ValueError(
            "Der Kontaminationsscan bindet nicht exakt das ausgewaehlte "
            "Kandidatenmanifest und Gewicht."
        )
    _require_sha256(
        match.get("dataset_plan_id"),
        "Dataset-Plan im Kontaminationsscan",
    )
    _require_sha256(
        match.get("dataset_manifest_sha256"),
        "Dataset-Manifest im Kontaminationsscan",
    )


def _iter_pdfs(root: Path) -> Iterable[Path]:
    for path in protection._find_all_files_safely(root, root):
        if path.suffix.casefold() == ".pdf":
            yield path


def discover_pdf_sources(
    source_roots: Sequence[Path],
    pdf_import_root: Path,
    *,
    require_import: bool = True,
) -> tuple[dict[str, SourcePdf], int, int]:
    import_root = _safe_root(pdf_import_root, "PDF-Pruefablage")
    import_hashes = {
        entry.name.casefold()
        for entry in import_root.iterdir()
        if entry.is_dir() and SHA256_PATTERN.fullmatch(entry.name.casefold())
    }
    if require_import and not import_hashes:
        raise ValueError("Die PDF-Pruefablage enthaelt keine inhaltsadressierten Importe.")
    by_hash: dict[str, SourcePdf] = {}
    ambiguous_hashes: set[str] = set()
    discovered = 0
    for raw_root in source_roots:
        root = _safe_root(raw_root, "PDF-Quellordner")
        for pdf in _iter_pdfs(root):
            discovered += 1
            digest = _sha256_file(pdf)
            if require_import and digest not in import_hashes:
                continue
            if digest in ambiguous_hashes:
                continue
            holding = resolve_pdf_holding(pdf, root)
            if holding is None:
                continue
            source = SourcePdf(
                path=pdf,
                sha256=digest,
                name=pdf.name,
                holding_key=holding,
                physical_holding_key=protection._physical_holding_key(holding),
            )
            previous = by_hash.get(digest)
            if previous is not None and previous.physical_holding_key != source.physical_holding_key:
                # Ein dupliziert abgelegtes PDF mit widerspruechlichem Ordnernamen
                # besitzt keine belastbare Haltung. Der Hash wird fuer diesen Lauf
                # vollstaendig ausgeschlossen, statt eine Zuordnung zu raten.
                by_hash.pop(digest, None)
                ambiguous_hashes.add(digest)
                continue
            if previous is None or str(source.path).casefold() < str(previous.path).casefold():
                by_hash[digest] = source
    return by_hash, discovered, len(ambiguous_hashes)


def discover_fresh_images(
    pdf_sources: dict[str, SourcePdf],
    pdf_import_root: Path,
    contamination: protection.ContaminationSnapshot,
) -> tuple[list[SourceImage], int, int]:
    import_root = _safe_root(pdf_import_root, "PDF-Pruefablage")
    images_by_hash: dict[str, SourceImage] = {}
    ambiguous_hashes: set[str] = set()
    blocked_holding = 0
    blocked_hash = 0
    for pdf_sha, source in sorted(pdf_sources.items()):
        if protection._holding_aliases(source.holding_key) & contamination.holding_aliases:
            blocked_holding += 1
            continue
        imported = protection._safe_existing_path(
            import_root / pdf_sha,
            import_root,
            expect_file=False,
        )
        for path in protection._find_all_files_safely(imported, import_root):
            if path.suffix.casefold() not in protection.IMAGE_SUFFIXES:
                raise ValueError(f"PDF-Importordner enthaelt eine fremde Datei: {path}")
            digest = _sha256_file(path)
            if path.stem.casefold() != digest:
                raise ValueError(f"PDF-Importbild ist nicht inhaltsadressiert: {path}")
            if digest in contamination.image_hashes:
                blocked_hash += 1
                continue
            if digest in ambiguous_hashes:
                continue
            previous = images_by_hash.get(digest)
            if previous is not None:
                if (
                    previous.pdf.physical_holding_key
                    != source.physical_holding_key
                ):
                    # Identische Bildbytes mit zwei physischen Haltungen besitzen
                    # keine belastbare Herkunft. Der Hash wird vollstaendig
                    # ausgeschlossen und darf auch bei einem dritten Fund nicht
                    # wieder aufgenommen werden.
                    images_by_hash.pop(digest, None)
                    ambiguous_hashes.add(digest)
                continue
            width, height = _image_info(path)
            images_by_hash[digest] = SourceImage(
                path=path,
                source_root=import_root,
                sha256=digest,
                size_bytes=path.stat().st_size,
                width=width,
                height=height,
                pdf=source,
            )
    return list(images_by_hash.values()), blocked_holding, blocked_hash


def discover_extraction_receipt_images(
    receipt_path: Path,
    pdf_sources: dict[str, SourcePdf],
    contamination: protection.ContaminationSnapshot,
) -> tuple[list[SourceImage], int]:
    receipt_root = _safe_root(receipt_path.parent, "PDF-Extraktionsablage")
    safe_receipt = _safe_file(receipt_path, receipt_root, "PDF-Extraktionsbeleg")
    receipt = _require_object(_load_json(safe_receipt), "PDF-Extraktionsbeleg")
    if receipt.get("schema_version") != "1.0" or receipt.get("purpose") != "detect_release_holdout_pdf_extraction":
        raise ValueError("Der Beleg stammt nicht vom Detect-Release-PDF-Extraktor.")
    if receipt.get("model_predictions_used_for_selection") is not False:
        raise ValueError("Der Extraktionsbeleg ist nicht kandidatenunabhaengig.")
    if receipt.get("training_allowed") is not False or receipt.get("gold_allowed") is not False:
        raise ValueError("Der Extraktionsbeleg sperrt Training oder Gold nicht.")
    raw_images = receipt.get("images")
    if not isinstance(raw_images, list) or not raw_images:
        raise ValueError("Der Extraktionsbeleg enthaelt keine Bilder.")
    if receipt.get("status") not in {"completed", "completed_with_errors"}:
        raise ValueError("Der Extraktionsbeleg ist nicht abgeschlossen.")
    image_count = receipt.get("image_count")
    if isinstance(image_count, bool) or not isinstance(image_count, int) or image_count != len(raw_images):
        raise ValueError("Die Bildanzahl im Extraktionsbeleg stimmt nicht.")

    seen_hashes: set[str] = set()
    images: list[SourceImage] = []
    blocked_hash = 0
    for index, raw in enumerate(raw_images):
        item = _require_object(raw, f"Extraktionsbild {index}")
        digest = _require_sha256(item.get("image_sha256"), f"Extraktionsbild {index} Hash")
        if digest in seen_hashes:
            raise ValueError("Der Extraktionsbeleg enthaelt denselben Bildhash mehrfach.")
        seen_hashes.add(digest)
        holding = protection._numeric_holding_key(item.get("holding_key"))
        if holding is None:
            raise ValueError("Extraktionsbild besitzt keine kanonische Haltung.")
        physical = protection._physical_holding_key(holding)
        if item.get("physical_holding_key") != physical:
            raise ValueError("Extraktionsbild besitzt einen falschen physischen Haltungsschluessel.")
        source_pdf_sha = _require_sha256(
            item.get("source_pdf_sha256"),
            "Extraktionsbild PDF-Hash",
        )
        source = pdf_sources.get(source_pdf_sha)
        if source is None or source.physical_holding_key != physical:
            raise ValueError("Extraktionsbild ist nicht an ein ausgewaehltes Quell-PDF gebunden.")
        if protection._holding_aliases(holding) & contamination.holding_aliases:
            raise ValueError("Extraktionsbeleg enthaelt eine bereits bekannte Haltung.")
        if digest in contamination.image_hashes:
            blocked_hash += 1
            continue
        relative_text = str(item.get("image_path") or "")
        relative = Path(relative_text)
        if (
            not relative_text
            or "\\" in relative_text
            or relative.is_absolute()
            or ".." in relative.parts
            or relative.as_posix() != relative_text
            or len(relative.parts) != 2
            or relative.parts[0] != "images"
        ):
            raise ValueError("Extraktionsbild besitzt einen unsicheren relativen Pfad.")
        if relative.stem.casefold() != digest or relative.suffix.casefold() not in protection.IMAGE_SUFFIXES:
            raise ValueError("Extraktionsbild ist nicht inhaltsadressiert benannt.")
        path = _safe_file(receipt_root / relative, receipt_root, "Extraktionsbild")
        expected_size = item.get("size_bytes")
        if isinstance(expected_size, bool) or not isinstance(expected_size, int) or expected_size <= 0:
            raise ValueError("Extraktionsbild besitzt keine gueltige Dateigroesse.")
        width = item.get("width")
        height = item.get("height")
        if any(isinstance(value, bool) or not isinstance(value, int) or value <= 0 for value in (width, height)):
            raise ValueError("Extraktionsbild besitzt keine gueltigen Abmessungen.")
        if (
            path.stat().st_size != expected_size
            or _sha256_file(path) != digest
            or _image_info(path) != (width, height)
        ):
            raise ValueError("Extraktionsbild stimmt nicht mit seinem Beleg ueberein.")
        source_kind = str(item.get("source_kind") or "")
        if source_kind not in {"operator_pdf_photo", "deterministic_video_frame"}:
            raise ValueError("Extraktionsbild besitzt eine unbekannte Quelle.")
        raw_references = item.get("operator_references")
        if not isinstance(raw_references, list):
            raise ValueError("Extraktionsbild besitzt keine gueltige Referenzliste.")
        references: list[dict[str, Any]] = []
        for reference in raw_references:
            ref = _require_object(reference, "Operateur-Referenz")
            class_id = ref.get("detect_class_id")
            class_name = str(ref.get("detect_class_name") or "")
            code = str(ref.get("vsa_code") or "").strip().upper()
            text = str(ref.get("finding_text") or "").strip()
            if (
                isinstance(class_id, bool)
                or not isinstance(class_id, int)
                or not 0 <= class_id < len(CLASS_LABELS)
                or class_name != CLASS_LABELS[class_id][1]
                or not code
                or not text
            ):
                raise ValueError("Operateur-Referenz passt nicht zur festen Klassenkarte.")
            references.append(
                {
                    "code": code,
                    "text": text,
                    "class_id": class_id,
                    "class_name": class_name,
                }
            )
        if source_kind == "operator_pdf_photo" and not references:
            raise ValueError("Ein Operateurfoto braucht mindestens eine Referenz.")
        if source_kind == "deterministic_video_frame" and references:
            raise ValueError("Ein fester Video-Frame darf keine Operateurreferenz vortaeuschen.")
        images.append(
            SourceImage(
                path=path,
                source_root=receipt_root,
                sha256=digest,
                size_bytes=expected_size,
                width=width,
                height=height,
                pdf=source,
                source_kind=source_kind,
                operator_references=tuple(references),
            )
        )
    return images, blocked_hash


def select_items(
    images: Sequence[SourceImage],
    *,
    max_holdings: int,
    images_per_holding: int,
    minimum_holdings: int,
) -> tuple[SourceImage, ...]:
    if max_holdings < minimum_holdings:
        raise ValueError("max_holdings darf nicht kleiner als minimum_holdings sein.")
    if images_per_holding < 1:
        raise ValueError("images_per_holding muss mindestens 1 sein.")
    groups: dict[str, list[SourceImage]] = {}
    for image in images:
        groups.setdefault(image.pdf.physical_holding_key, []).append(image)
    ranked_holdings = sorted(
        groups,
        key=lambda key: hashlib.sha256(
            f"{SELECTION_SALT}|holding|{key}".encode("utf-8")
        ).hexdigest(),
    )[:max_holdings]
    if len(ranked_holdings) < minimum_holdings:
        raise ValueError(
            "Zu wenig unberuehrte, bereits extrahierte PDF-Haltungen: "
            f"{len(ranked_holdings)}/{minimum_holdings}."
        )
    selected: list[SourceImage] = []
    for physical in ranked_holdings:
        ordered = sorted(
            groups[physical],
            key=lambda item: hashlib.sha256(
                f"{SELECTION_SALT}|image|{item.sha256}".encode("utf-8")
            ).hexdigest(),
        )
        selected.extend(ordered[:images_per_holding])
    return tuple(selected)


def select_extraction_items(
    images: Sequence[SourceImage],
    *,
    max_holdings: int,
    max_images: int,
    background_target: int,
    minimum_holdings: int,
    minimum_background: int,
    operator_images_per_holding: int,
) -> tuple[SourceImage, ...]:
    if max_holdings < minimum_holdings:
        raise ValueError("max_holdings darf nicht kleiner als minimum_holdings sein.")
    if max_images < 1:
        raise ValueError("max_images muss mindestens 1 sein.")
    if minimum_background < 0 or background_target < minimum_background:
        raise ValueError("Das Hintergrundziel darf nicht unter seinem Minimum liegen.")
    if minimum_background > max_images:
        raise ValueError("max_images ist kleiner als das Hintergrund-Minimum.")
    if operator_images_per_holding < 1:
        raise ValueError("operator_images_per_holding muss mindestens 1 sein.")
    groups: dict[str, list[SourceImage]] = {}
    for image in images:
        groups.setdefault(image.pdf.physical_holding_key, []).append(image)
    ranked_holdings = sorted(
        groups,
        key=lambda key: hashlib.sha256(
            f"{SELECTION_SALT}|extracted-holding|{key}".encode("utf-8")
        ).hexdigest(),
    )[:max_holdings]
    if len(ranked_holdings) < minimum_holdings:
        raise ValueError(
            "Zu wenig erfolgreich extrahierte frische Haltungen: "
            f"{len(ranked_holdings)}/{minimum_holdings}."
        )
    allowed = set(ranked_holdings)
    backgrounds = sorted(
        (
            image
            for image in images
            if image.pdf.physical_holding_key in allowed
            and image.source_kind == "deterministic_video_frame"
        ),
        key=lambda item: hashlib.sha256(
            f"{SELECTION_SALT}|background|{item.sha256}".encode("utf-8")
        ).hexdigest(),
    )
    # Hoechstens ein fester Frame pro physischer Haltung.
    unique_backgrounds: list[SourceImage] = []
    seen_background_holdings: set[str] = set()
    for image in backgrounds:
        physical = image.pdf.physical_holding_key
        if physical in seen_background_holdings:
            continue
        seen_background_holdings.add(physical)
        unique_backgrounds.append(image)
    if len(unique_backgrounds) < minimum_background:
        raise ValueError(
            "Zu wenig feste Video-Hintergrundframes: "
            f"{len(unique_backgrounds)}/{minimum_background}."
        )
    selected: list[SourceImage] = unique_backgrounds[
        : min(background_target, max_images)
    ]
    selected_hashes = {image.sha256 for image in selected}
    operator_counts: dict[str, int] = {}
    operators = [
        image
        for image in images
        if image.pdf.physical_holding_key in allowed
        and image.source_kind == "operator_pdf_photo"
    ]
    operators.sort(
        key=lambda item: hashlib.sha256(
            f"{SELECTION_SALT}|operator|{item.sha256}".encode("utf-8")
        ).hexdigest()
    )

    def can_take(image: SourceImage) -> bool:
        physical = image.pdf.physical_holding_key
        return (
            image.sha256 not in selected_hashes
            and operator_counts.get(physical, 0) < operator_images_per_holding
            and len(selected) < max_images
        )

    # Zuerst menschlich codierte seltene Klassen moeglichst gleichmaessig
    # abdecken. Das ist eine Metadaten-Stratifizierung, keine Modellselektion.
    for _ in range(20):
        progressed = False
        for class_id in range(len(CLASS_LABELS)):
            candidate = next(
                (
                    image
                    for image in operators
                    if can_take(image)
                    and any(ref["class_id"] == class_id for ref in image.operator_references)
                ),
                None,
            )
            if candidate is None:
                continue
            selected.append(candidate)
            selected_hashes.add(candidate.sha256)
            physical = candidate.pdf.physical_holding_key
            operator_counts[physical] = operator_counts.get(physical, 0) + 1
            progressed = True
        if not progressed or len(selected) >= max_images:
            break
    for image in operators:
        if not can_take(image):
            continue
        selected.append(image)
        selected_hashes.add(image.sha256)
        physical = image.pdf.physical_holding_key
        operator_counts[physical] = operator_counts.get(physical, 0) + 1
        if len(selected) >= max_images:
            break
    if len(selected) > max_images:
        raise ValueError("Die endgueltige Auswahl ueberschreitet max_images.")
    selected_holdings = {
        image.pdf.physical_holding_key
        for image in selected
    }
    if len(selected_holdings) < minimum_holdings:
        raise ValueError(
            "Zu wenig Haltungen in der endgueltigen Auswahl: "
            f"{len(selected_holdings)}/{minimum_holdings}."
        )
    return tuple(selected)


def _operator_reference_coverage(
    items: Sequence[SourceImage],
) -> dict[str, Any]:
    image_hashes_by_class: dict[int, set[str]] = {
        class_id: set()
        for class_id, _, _ in CLASS_LABELS
    }
    for item in items:
        for reference in item.operator_references:
            class_id = reference.get("class_id")
            if isinstance(class_id, int) and class_id in image_hashes_by_class:
                image_hashes_by_class[class_id].add(item.sha256)
    classes = [
        {
            "id": class_id,
            "name": name,
            "label": label,
            "reference_images": len(image_hashes_by_class[class_id]),
        }
        for class_id, name, label in CLASS_LABELS
    ]
    missing = [
        {
            "id": item["id"],
            "name": item["name"],
        }
        for item in classes
        if item["reference_images"] == 0
    ]
    return {
        "basis": "operator_metadata_before_blind_review",
        "status": (
            "operator_reference_coverage_complete"
            if not missing
            else "operator_reference_coverage_incomplete"
        ),
        "covered_classes": len(classes) - len(missing),
        "total_classes": len(classes),
        "missing_classes": missing,
        "classes": classes,
        "release_gate": False,
        "note": (
            "Dies ist nur die vorlaeufige Operateurreferenz. "
            "Freigabefaehige Klassenabdeckung entsteht erst durch die "
            "vollstaendige menschliche Review."
        ),
    }


def _contamination_fingerprint(snapshot: protection.ContaminationSnapshot) -> tuple[str, ...]:
    return (
        snapshot.base_model_sha256,
        snapshot.candidate_scope_sha256,
        snapshot.image_hashes_sha256,
        snapshot.holding_aliases_sha256,
    )


def build_plan(
    knowledge_root: Path,
    candidate_root: Path,
    class_map_path: Path,
    source_roots: Sequence[Path],
    pdf_import_root: Path,
    *,
    max_holdings: int = DEFAULT_MAX_HOLDINGS,
    images_per_holding: int = DEFAULT_IMAGES_PER_HOLDING,
    minimum_holdings: int = DEFAULT_MINIMUM_HOLDINGS,
    extraction_receipt: Path | None = None,
    max_images: int = 400,
    background_target: int = 100,
    minimum_background: int = 75,
    scanner: Callable[..., protection.ContaminationSnapshot] = protection.scan_contamination,
) -> HoldoutPlan:
    knowledge = _safe_root(knowledge_root, "KnowledgeRoot")
    candidate = validate_candidate(candidate_root, class_map_path)
    contamination = scanner(knowledge, candidate.base_model_path)
    if contamination.base_model_sha256 != candidate.base_model_sha256:
        raise ValueError("Kontaminationsscan und Kandidat verwenden verschiedene Basismodelle.")
    _assert_candidate_bound_to_snapshot(knowledge, candidate, contamination)
    safe_roots = tuple(_safe_root(path, "PDF-Quellordner") for path in source_roots)
    sources, discovered, ambiguous_pdf_hashes = discover_pdf_sources(
        safe_roots,
        pdf_import_root,
        require_import=extraction_receipt is None,
    )
    if extraction_receipt is None:
        images, blocked_holding, blocked_hash = discover_fresh_images(
            sources,
            pdf_import_root,
            contamination,
        )
        selected = select_items(
            images,
            max_holdings=max_holdings,
            images_per_holding=images_per_holding,
            minimum_holdings=minimum_holdings,
        )
    else:
        images, blocked_hash = discover_extraction_receipt_images(
            extraction_receipt,
            sources,
            contamination,
        )
        blocked_holding = 0
        selected = select_extraction_items(
            images,
            max_holdings=max_holdings,
            max_images=max_images,
            background_target=background_target,
            minimum_holdings=minimum_holdings,
            minimum_background=minimum_background,
            operator_images_per_holding=images_per_holding,
        )
    semantic = {
        "schema_version": SCHEMA_VERSION,
        "purpose": HOLDOUT_PURPOSE,
        "candidate_id": candidate.candidate_id,
        "candidate_manifest_sha256": candidate.manifest_sha256,
        "candidate_weights_sha256": candidate.weights_sha256,
        "class_map_sha256": candidate.class_map_sha256,
        "vsa_manifest_hash": candidate.vsa_manifest_hash,
        "contamination": _contamination_fingerprint(contamination),
        "items": [
            {
                "image_sha256": item.sha256,
                "haltung_key": item.pdf.holding_key,
                "physical_holding_key": item.pdf.physical_holding_key,
                "source_pdf_sha256": item.pdf.sha256,
                "source_kind": item.source_kind,
                "operator_references": list(item.operator_references),
            }
            for item in selected
        ],
    }
    holdout_id = hashlib.sha256(_semantic_bytes(semantic)).hexdigest()
    target = knowledge / "eval_set" / "subsets" / f"detect_release_holdout_{holdout_id[:12]}"
    return HoldoutPlan(
        knowledge_root=knowledge,
        candidate=candidate,
        class_map_path=_safe_file(class_map_path, class_map_path.parent, "Klassenkarte"),
        created_utc=datetime.now(timezone.utc),
        contamination=contamination,
        source_roots=safe_roots,
        pdf_import_root=_safe_root(pdf_import_root, "PDF-Pruefablage"),
        items=selected,
        discovered_pdf_files=discovered,
        matched_import_pdfs=len(sources),
        ambiguous_pdf_hashes=ambiguous_pdf_hashes,
        blocked_same_holding=blocked_holding,
        blocked_same_hash=blocked_hash,
        holdout_id=holdout_id,
        target_root=target,
    )


def _assert_plan_unchanged(
    plan: HoldoutPlan,
    scanner: Callable[..., protection.ContaminationSnapshot],
    *,
    exclude_eval_root: Path | None = None,
) -> None:
    current = scanner(
        plan.knowledge_root,
        plan.candidate.base_model_path,
        exclude_eval_root=exclude_eval_root,
    )
    if _contamination_fingerprint(current) != _contamination_fingerprint(plan.contamination):
        raise ValueError("Der Trainings- oder Eval-Schutzbestand hat sich seit der Planung geaendert.")
    _assert_candidate_bound_to_snapshot(
        plan.knowledge_root,
        plan.candidate,
        current,
    )
    checked_pdfs: set[str] = set()
    for item in plan.items:
        if item.pdf.sha256 not in checked_pdfs:
            if _sha256_file(item.pdf.path) != item.pdf.sha256:
                raise ValueError(f"Quell-PDF wurde seit der Planung veraendert: {item.pdf.name}")
            checked_pdfs.add(item.pdf.sha256)
        if (
            item.path.stat().st_size != item.size_bytes
            or _sha256_file(item.path) != item.sha256
            or _image_info(item.path) != (item.width, item.height)
        ):
            raise ValueError(f"PDF-Prueffoto wurde seit der Planung veraendert: {item.path.name}")
        if (
            item.sha256 in current.image_hashes
            or protection._holding_aliases(item.pdf.holding_key) & current.holding_aliases
        ):
            raise ValueError("Ein geplantes Pruefbild oder seine Haltung ist inzwischen kontaminiert.")


def _manifest_hashes(staging: Path) -> dict[str, dict[str, Any]]:
    paths = [staging / "_candidates.json"]
    paths.extend(sorted((staging / "images").iterdir(), key=lambda path: path.name))
    return {
        path.relative_to(staging).as_posix(): {
            "sha256": _sha256_file(path),
            "size_bytes": path.stat().st_size,
        }
        for path in paths
    }


def _verify_staging_payload(
    staging: Path,
    expected_hashes: dict[str, dict[str, Any]],
) -> None:
    safe_staging = protection._safe_existing_path(
        staging,
        staging.parent,
        expect_file=False,
    )
    files = protection._find_all_files_safely(safe_staging, staging.parent)
    actual_paths = {
        path.relative_to(safe_staging).as_posix()
        for path in files
    }
    expected_paths = set(expected_hashes) | {"_manifest.json"}
    if actual_paths != expected_paths:
        raise ValueError("Die Staging-Dateimenge stimmt nicht mit dem Manifest ueberein.")
    for relative, raw in expected_hashes.items():
        entry = _require_object(raw, f"Staging-Hash {relative}")
        expected_sha = _require_sha256(
            entry.get("sha256"),
            f"Staging-Hash {relative}",
        )
        expected_size = entry.get("size_bytes")
        if (
            isinstance(expected_size, bool)
            or not isinstance(expected_size, int)
            or expected_size < 0
        ):
            raise ValueError(f"Staging-Dateigroesse ist ungueltig: {relative}")
        path = protection._safe_existing_path(
            safe_staging / Path(relative),
            safe_staging,
            expect_file=True,
        )
        if path.stat().st_size != expected_size or _sha256_file(path) != expected_sha:
            raise ValueError(f"Staging-Datei stimmt nicht mit ihrem Hash: {relative}")

    manifest_path = protection._safe_existing_path(
        safe_staging / "_manifest.json",
        safe_staging,
        expect_file=True,
    )
    manifest = _require_object(_load_json(manifest_path), "Staging-Manifest")
    if (
        manifest.get("hash_algorithm") != "sha256"
        or manifest.get("hashes_count") != len(expected_hashes)
        or manifest.get("hashes") != expected_hashes
    ):
        raise ValueError("Das Staging-Manifest bindet seine Dateien nicht exakt.")


def publish_holdout(
    plan: HoldoutPlan,
    *,
    scanner: Callable[..., protection.ContaminationSnapshot] = protection.scan_contamination,
) -> Path:
    expected = (
        plan.knowledge_root
        / "eval_set"
        / "subsets"
        / f"detect_release_holdout_{plan.holdout_id[:12]}"
    )
    if os.path.normcase(str(plan.target_root)) != os.path.normcase(str(expected)):
        raise ValueError("Holdout-Ziel passt nicht zum geprueften Plan.")
    if plan.target_root.exists():
        raise FileExistsError(f"Vorhandener Pruefbestand wird nie ueberschrieben: {plan.target_root}")
    _assert_plan_unchanged(plan, scanner)

    eval_root = plan.knowledge_root / "eval_set"
    if not eval_root.exists():
        eval_root.mkdir()
    eval_root = protection._safe_existing_path(eval_root, plan.knowledge_root, expect_file=False)
    subsets_root = eval_root / "subsets"
    if not subsets_root.exists():
        subsets_root.mkdir()
    subsets_root = protection._safe_existing_path(subsets_root, eval_root, expect_file=False)
    staging = subsets_root / f".detect-release-staging-{uuid.uuid4().hex}"
    staging.mkdir()
    staging = protection._safe_existing_path(
        staging,
        subsets_root,
        expect_file=False,
    )
    try:
        images_root = staging / "images"
        images_root.mkdir()
        images_root = protection._safe_existing_path(
            images_root,
            staging,
            expect_file=False,
        )
        candidates: list[dict[str, Any]] = []
        provenance: list[dict[str, Any]] = []
        for item in plan.items:
            source = _safe_file(item.path, item.source_root, "PDF-Prueffoto")
            destination = images_root / item.target_file_name
            protection._copy_verified(source, destination, item.sha256)
            candidate_entry: dict[str, Any] = {
                    "id": item.item_id,
                    "image_path": f"images/{item.target_file_name}",
                    "frame_path": item.target_file_name,
                    "image_sha256": item.sha256,
                    "size_bytes": item.size_bytes,
                    "width": item.width,
                    "height": item.height,
                    "haltung_key": item.pdf.holding_key,
                    "physical_holding_key": item.pdf.physical_holding_key,
                }
            if item.operator_references:
                candidate_entry["operator_references"] = list(item.operator_references)
            candidates.append(candidate_entry)
            provenance.append(
                {
                    "id": item.item_id,
                    "source_kind": item.source_kind,
                    "source_pdf_name": item.pdf.name,
                    "source_pdf_sha256": item.pdf.sha256,
                    "image_source_kind": item.source_kind,
                }
            )
        candidates_document = {
            "schema_version": SCHEMA_VERSION,
            "purpose": CANDIDATES_PURPOSE,
            "holdout_id": plan.holdout_id,
            "candidates": candidates,
        }
        candidate_bytes = _json_bytes(candidates_document)
        protection._atomic_write(staging / "_candidates.json", candidate_bytes)
        hashes = _manifest_hashes(staging)
        created = plan.created_utc.isoformat().replace("+00:00", "Z")
        manifest = {
            "schema_version": SCHEMA_VERSION,
            "purpose": HOLDOUT_PURPOSE,
            "name": "SewerStudio Detect Release Holdout",
            "holdout_id": plan.holdout_id,
            "role": "acceptance",
            "created_utc": created,
            "frozen": True,
            "warning": "DIESES EVAL-SET DARF NIE FUER TRAINING, GOLD, FEW-SHOT ODER KANDIDATENAUSWAHL VERWENDET WERDEN",
            "training_allowed": False,
            "gold_allowed": False,
            "model_predictions_used_for_selection": False,
            "dataset_status": "review_incomplete",
            "release_status": "not_evaluated",
            "candidates_count": len(candidates),
            "images_count": len(candidates),
            "holdings_count": len({item.pdf.physical_holding_key for item in plan.items}),
            "candidates_sha256": hashlib.sha256(candidate_bytes).hexdigest(),
            "candidate_id": plan.candidate.candidate_id,
            "candidate_manifest_sha256": plan.candidate.manifest_sha256,
            "candidate_weights_sha256": plan.candidate.weights_sha256,
            "class_map_version": plan.candidate.class_map_version,
            "class_map_sha256": plan.candidate.class_map_sha256,
            "vsa_manifest_hash": plan.candidate.vsa_manifest_hash,
            "vsa_manifest_sha256": plan.candidate.vsa_manifest_hash,
            "classes": [
                {"id": class_id, "name": name, "label": label}
                for class_id, name, label in CLASS_LABELS
            ],
            "selection": {
                "source": "human_coded_pdf_photos",
                "candidate_predictions_used": False,
                "whole_holding_exclusion": True,
                "deterministic_salt": SELECTION_SALT,
                "operator_reference_coverage": _operator_reference_coverage(
                    plan.items
                ),
            },
            "contamination_proof": {
                "base_model_sha256": plan.contamination.base_model_sha256,
                "candidate_scope_sha256": plan.contamination.candidate_scope_sha256,
                "known_image_hashes": len(plan.contamination.image_hashes),
                "known_image_hashes_sha256": plan.contamination.image_hashes_sha256,
                "known_holding_aliases": len(plan.contamination.holding_aliases),
                "known_holding_aliases_sha256": plan.contamination.holding_aliases_sha256,
                "blocked_same_holding": plan.blocked_same_holding,
                "blocked_same_hash": plan.blocked_same_hash,
                "ambiguous_pdf_hashes": plan.ambiguous_pdf_hashes,
                "evidence": list(plan.contamination.evidence),
            },
            "item_provenance": provenance,
            "hash_algorithm": "sha256",
            "hashes_count": len(hashes),
            "hashes_generated_utc": created,
            "hashes": hashes,
        }
        protection._atomic_write(staging / "_manifest.json", _json_bytes(manifest))
        _assert_plan_unchanged(plan, scanner, exclude_eval_root=staging)
        _verify_staging_payload(staging, hashes)
        if plan.target_root.exists():
            raise FileExistsError(f"Vorhandener Pruefbestand wird nie ueberschrieben: {plan.target_root}")
        os.replace(staging, plan.target_root)
        return plan.target_root
    finally:
        if staging.exists():
            safe_staging = protection._safe_existing_path(
                staging,
                subsets_root,
                expect_file=False,
            )
            protection._find_all_files_safely(safe_staging, subsets_root)
            shutil.rmtree(safe_staging)


def _parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Frischen, kandidatenunabhaengigen Detect-Release-Pruefbestand vorbereiten."
    )
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument(
        "--class-map",
        type=Path,
        default=REPOSITORY_ROOT / "training" / "class_maps" / "detect_class_map_v3.json",
    )
    parser.add_argument("--source-root", type=Path, action="append", required=True)
    parser.add_argument("--pdf-import-root", type=Path)
    parser.add_argument("--max-holdings", type=int, default=DEFAULT_MAX_HOLDINGS)
    parser.add_argument("--images-per-holding", type=int, default=DEFAULT_IMAGES_PER_HOLDING)
    parser.add_argument("--minimum-holdings", type=int, default=DEFAULT_MINIMUM_HOLDINGS)
    parser.add_argument(
        "--extraction-receipt",
        type=Path,
        help="Optionaler _pdf_extraction.json-Beleg mit Operateurreferenzen und festen Video-Frames.",
    )
    parser.add_argument("--max-images", type=int, default=400)
    parser.add_argument("--background-target", type=int, default=100)
    parser.add_argument("--minimum-background", type=int, default=75)
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args(argv)
    if args.max_holdings < 1 or args.minimum_holdings < 1:
        parser.error("Haltungszahlen muessen mindestens 1 sein.")
    if args.images_per_holding < 1 or args.images_per_holding > 10:
        parser.error("--images-per-holding muss zwischen 1 und 10 liegen.")
    if args.max_images < 1:
        parser.error("--max-images muss mindestens 1 sein.")
    if args.minimum_background < 0 or args.background_target < args.minimum_background:
        parser.error("Die Zielzahl der Hintergrundframes darf nicht unter dem Minimum liegen.")
    if args.pdf_import_root is None:
        args.pdf_import_root = args.knowledge_root / "training" / "pdf_review_imports"
    return args


def _print_plan(plan: HoldoutPlan, executed: bool) -> None:
    holdings = len({item.pdf.physical_holding_key for item in plan.items})
    print(f"Kandidat: {plan.candidate.candidate_id}")
    print(f"PDF-Dateien geprueft: {plan.discovered_pdf_files}")
    print(f"Eindeutig zugeordnete Quell-PDFs: {plan.matched_import_pdfs}")
    print(f"Mehrdeutige PDF-Hashes ausgeschlossen: {plan.ambiguous_pdf_hashes}")
    print(f"Frische Haltungen ausgewaehlt: {holdings}")
    print(f"Pruefbilder: {len(plan.items)}")
    print(f"Wegen bekannter Haltung gesperrt: {plan.blocked_same_holding}")
    print(f"Wegen bekanntem Bildhash gesperrt: {plan.blocked_same_hash}")
    print(f"Ziel: {plan.target_root}")
    if executed:
        print("Status: review_incomplete; keine Modellfreigabe.")
    else:
        print("Dry-Run: Es wurde nichts geschrieben.")


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv if argv is not None else sys.argv[1:])
    try:
        plan = build_plan(
            args.knowledge_root,
            args.candidate,
            args.class_map,
            tuple(args.source_root),
            args.pdf_import_root,
            max_holdings=args.max_holdings,
            images_per_holding=args.images_per_holding,
            minimum_holdings=args.minimum_holdings,
            extraction_receipt=args.extraction_receipt,
            max_images=args.max_images,
            background_target=args.background_target,
            minimum_background=args.minimum_background,
        )
        if args.execute:
            publish_holdout(plan)
        _print_plan(plan, args.execute)
        return 0
    except (OSError, ValueError, FileExistsError) as error:
        print(f"FEHLER: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
