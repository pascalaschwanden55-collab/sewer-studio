#!/usr/bin/env python3
"""Lokaler Mehrklassen-Pruefplatz fuer einen eingefrorenen Detect-Holdout.

Das Werkzeug liest den Holdout ausschliesslich, zeigt keine Modellvorhersagen und
schreibt nur eine getrennte, hashgebundene Review-Datei ausserhalb des Holdouts.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import stat
import struct
import tempfile
import threading
import unicodedata
import webbrowser
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path, PurePosixPath
from typing import Callable, Iterator
from urllib.parse import parse_qs, quote, urlparse


SCHEMA_VERSION = "1.0"
HOLDOUT_PURPOSE = "detect_release_holdout"
CANDIDATES_PURPOSE = "detect_release_holdout_candidates"
REVIEW_PURPOSE = "detect_release_holdout_review"
VALID_DECISIONS = frozenset({"positive", "negative", "exclude"})
IMAGE_SUFFIXES = frozenset({".jpg", ".jpeg", ".png"})
MAX_REQUEST_BODY_BYTES = 512 * 1024
MAX_JSON_FILE_BYTES = 32 * 1024 * 1024
MAX_COMMENT_CHARACTERS = 2_000
MAX_REVIEWER_CHARACTERS = 128
MAX_TEXT_CHARACTERS = 200

_SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
_IDENTIFIER_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
_CANONICAL_HOLDING_PATTERN = re.compile(r"^[0-9]+-[0-9]+$")
_VSA_CODE_PATTERN = re.compile(r"^[A-Z0-9]{2,16}$")
_EXPECTED_CLASS_NAMES = (
    "BCA_anschluss",
    "BAB_riss",
    "BAC_bruch",
    "BAA_verformung",
    "BAF_oberflaeche",
    "BAH_schadanschluss",
    "BAI_dichtung",
    "BAJ_verbindung",
    "BBA_wurzeln",
    "BBB_anhaftung",
    "BBC_ablagerung",
    "BBD_boden",
    "BBF_infiltration",
    "SONST_schaden",
    "BCC_bogen",
)
_MANIFEST_REQUIRED_FIELDS = frozenset(
    {
        "schema_version",
        "purpose",
        "holdout_id",
        "frozen",
        "hash_algorithm",
        "hashes_count",
        "candidates_count",
        "candidates_sha256",
        "candidate_id",
        "candidate_manifest_sha256",
        "candidate_weights_sha256",
        "class_map_version",
        "class_map_sha256",
        "vsa_manifest_hash",
        "vsa_manifest_sha256",
        "classes",
        "hashes",
    }
)
_CANDIDATES_FIELDS = frozenset(
    {"schema_version", "purpose", "holdout_id", "candidates"}
)
_CANDIDATE_REQUIRED_FIELDS = frozenset(
    {
        "id",
        "image_path",
        "frame_path",
        "image_sha256",
        "size_bytes",
        "width",
        "height",
        "haltung_key",
        "physical_holding_key",
    }
)
_CANDIDATE_OPTIONAL_FIELDS = frozenset(
    {"operator_reference", "operator_references"}
)
_REVIEW_BINDING_FIELDS = (
    "manifest_sha256",
    "candidates_sha256",
    "candidate_id",
    "candidate_manifest_sha256",
    "candidate_weights_sha256",
    "class_map_version",
    "class_map_sha256",
    "vsa_manifest_hash",
    "vsa_manifest_sha256",
)
_REVIEW_FIELDS = frozenset(
    {
        "schema_version",
        "purpose",
        "holdout_id",
        *_REVIEW_BINDING_FIELDS,
        "reviewer",
        "updated_at_utc",
        "decisions",
    }
)


@dataclass(frozen=True)
class DetectClass:
    class_id: int
    name: str
    label: str

    def public(self) -> dict[str, object]:
        return {"id": self.class_id, "name": self.name, "label": self.label}


@dataclass(frozen=True)
class OperatorReference:
    code: str
    text: str
    class_id: int | None
    class_name: str | None

    def public(self) -> dict[str, object]:
        return {
            "code": self.code,
            "text": self.text,
            "class_id": self.class_id,
            "class_name": self.class_name,
        }


@dataclass(frozen=True)
class VerifiedImage:
    candidate_id: str
    relative_path: str
    path: Path
    sha256: str
    size_bytes: int
    width: int
    height: int
    operator_references: tuple[OperatorReference, ...]


@dataclass(frozen=True)
class HoldoutSnapshot:
    holdout_id: str
    manifest_sha256: str
    candidates_sha256: str
    candidate_id: str
    candidate_manifest_sha256: str
    candidate_weights_sha256: str
    class_map_version: int
    class_map_sha256: str
    vsa_manifest_hash: str
    vsa_manifest_sha256: str
    classes: tuple[DetectClass, ...]
    images: tuple[VerifiedImage, ...]

    def bindings(self) -> dict[str, object]:
        return {
            "manifest_sha256": self.manifest_sha256,
            "candidates_sha256": self.candidates_sha256,
            "candidate_id": self.candidate_id,
            "candidate_manifest_sha256": self.candidate_manifest_sha256,
            "candidate_weights_sha256": self.candidate_weights_sha256,
            "class_map_version": self.class_map_version,
            "class_map_sha256": self.class_map_sha256,
            "vsa_manifest_hash": self.vsa_manifest_hash,
            "vsa_manifest_sha256": self.vsa_manifest_sha256,
        }


class ReviewRevisionConflictError(ValueError):
    """Ein Browser-Tab arbeitet mit einem veralteten Review-Zustand."""


class DetectReleaseHoldoutReviewStore:
    """Validiert Holdout und Review und besitzt den einzigen Schreibweg."""

    def __init__(
        self,
        holdout_root: str | Path,
        output_path: str | Path,
        reviewer: object,
        now_utc: Callable[[], str] | None = None,
    ) -> None:
        self.holdout_root = Path(os.path.abspath(holdout_root))
        self.output_path = Path(os.path.abspath(output_path))
        self.reviewer = _required_text(
            reviewer,
            "Reviewer",
            MAX_REVIEWER_CHARACTERS,
        )
        self._now_utc = now_utc or _current_utc
        self._lock = threading.RLock()
        self._revision = 0
        self._updated_at_utc = ""
        self._output_sha256: str | None = None

        _validate_output_location(self.output_path, self.holdout_root)
        self._snapshot = _validate_holdout(self.holdout_root)
        self._images = list(self._snapshot.images)
        self._images_by_id = {image.candidate_id: image for image in self._images}
        self._classes = list(self._snapshot.classes)
        self._classes_by_id = {item.class_id: item for item in self._classes}
        _prepare_safe_output_parent(self.output_path)
        self._decisions: dict[str, dict[str, object]] = {}
        self._merge_existing_output()

    @property
    def holdout_id(self) -> str:
        return self._snapshot.holdout_id

    def prepare_output(self) -> dict[str, object]:
        with self._lock:
            self._assert_source_unchanged()
            previous_updated = self._updated_at_utc
            self._updated_at_utc = self._timestamp()
            try:
                self._write_output_locked()
            except Exception:
                self._updated_at_utc = previous_updated
                raise
            return self._state_locked()

    def state(self) -> dict[str, object]:
        with self._lock:
            return self._state_locked()

    def set_decision(
        self,
        candidate_id: object,
        decision: object,
        annotations: object,
        comment: object = "",
        expected_revision: object | None = None,
    ) -> dict[str, object]:
        image_id = str(candidate_id or "")
        if image_id not in self._images_by_id:
            raise KeyError("Unbekannte Bild-ID.")
        value = str(decision or "").strip().casefold()
        if value not in VALID_DECISIONS:
            raise ValueError("Entscheidung ist fuer diesen Pruefplatz ungueltig.")
        normalized_comment = _comment_text(comment)
        normalized_annotations = _validate_annotations(
            value,
            annotations,
            self._classes_by_id,
        )
        parsed_revision = (
            None
            if expected_revision is None
            else _required_revision(expected_revision)
        )

        with self._lock:
            if parsed_revision is not None and parsed_revision != self._revision:
                raise ReviewRevisionConflictError(
                    "Der Review-Zustand ist veraltet. Bitte neu laden."
                )
            self._assert_source_unchanged()
            previous = self._decisions.get(image_id)
            previous_updated = self._updated_at_utc
            reviewed_at = self._timestamp()
            self._decisions[image_id] = {
                "decision": value,
                "comment": normalized_comment,
                "reviewed_at_utc": reviewed_at,
                "annotations": normalized_annotations,
            }
            self._updated_at_utc = reviewed_at
            try:
                self._write_output_locked()
            except Exception:
                if previous is None:
                    self._decisions.pop(image_id, None)
                else:
                    self._decisions[image_id] = previous
                self._updated_at_utc = previous_updated
                raise
            self._revision += 1
            return self._state_locked(preferred_id=image_id)

    def image_bytes_for(self, candidate_id: object) -> tuple[bytes, str]:
        image_id = str(candidate_id or "")
        image = self._images_by_id.get(image_id)
        if image is None:
            raise KeyError("Unbekannte Bild-ID.")
        with self._lock:
            self._assert_documents_unchanged()
            payload = _read_verified_image(image)
        return payload, _image_content_type(image.path.suffix)

    def _timestamp(self) -> str:
        value = self._now_utc()
        _require_utc_timestamp(value, "Review-Zeitpunkt")
        return value

    def _assert_documents_unchanged(self) -> None:
        manifest = _read_limited(self.holdout_root / "_manifest.json")
        candidates = _read_limited(self.holdout_root / "_candidates.json")
        if _sha256(manifest) != self._snapshot.manifest_sha256:
            raise ValueError("Die gebundene Review-Quelle wurde seit dem Start veraendert.")
        if _sha256(candidates) != self._snapshot.candidates_sha256:
            raise ValueError("Die gebundene Review-Quelle wurde seit dem Start veraendert.")

    def _assert_source_unchanged(self) -> None:
        try:
            current = _validate_holdout(self.holdout_root)
        except (OSError, ValueError) as error:
            raise ValueError(
                "Die gebundene Review-Quelle wurde seit dem Start veraendert."
            ) from error
        if current != self._snapshot:
            raise ValueError(
                "Die gebundene Review-Quelle wurde seit dem Start veraendert."
            )

    def _merge_existing_output(self) -> None:
        if not self.output_path.exists():
            return
        if not self.output_path.is_file() or _is_reparse_point(self.output_path):
            raise ValueError("Die vorhandene Review-Ausgabe ist unsicher.")
        raw = _read_limited(self.output_path)
        existing = _load_json_bytes(raw, "Review-Ausgabe")
        if not isinstance(existing, dict) or set(existing) != _REVIEW_FIELDS:
            raise ValueError("Die vorhandene Review-Ausgabe hat ein falsches Schema.")
        if existing.get("schema_version") != SCHEMA_VERSION:
            raise ValueError("Die vorhandene Review-Ausgabe hat ein falsches Schema.")
        if existing.get("purpose") != REVIEW_PURPOSE:
            raise ValueError("Die vorhandene Datei gehoert nicht zu diesem Review.")
        if existing.get("holdout_id") != self.holdout_id:
            raise ValueError("Die Review-Ausgabe gehoert zu einem anderen Holdout.")
        for field, expected in self._snapshot.bindings().items():
            if existing.get(field) != expected:
                raise ValueError(f"Die Review-Ausgabe ist nicht an {field} gebunden.")
        if existing.get("reviewer") != self.reviewer:
            raise ValueError("Die Review-Ausgabe gehoert zu einem anderen Reviewer.")
        self._updated_at_utc = _require_utc_timestamp(
            existing.get("updated_at_utc"),
            "Review-Aktualisierungszeitpunkt",
        )
        raw_decisions = existing.get("decisions")
        if not isinstance(raw_decisions, dict):
            raise ValueError("Review-Entscheidungen muessen ein Objekt sein.")
        if not set(raw_decisions).issubset(self._images_by_id):
            raise ValueError("Review enthaelt unbekannte Bild-IDs.")
        for image_id, raw_decision in raw_decisions.items():
            self._decisions[image_id] = _validate_saved_decision(
                raw_decision,
                self._classes_by_id,
            )
        self._output_sha256 = _sha256(raw)

    def _write_output_locked(self) -> None:
        document = {
            "schema_version": SCHEMA_VERSION,
            "purpose": REVIEW_PURPOSE,
            "holdout_id": self.holdout_id,
            **self._snapshot.bindings(),
            "reviewer": self.reviewer,
            "updated_at_utc": self._updated_at_utc,
            "decisions": {
                image.candidate_id: self._decisions[image.candidate_id]
                for image in self._images
                if image.candidate_id in self._decisions
            },
        }
        with _exclusive_review_output_lock(self.output_path):
            current_sha256: str | None = None
            if self.output_path.exists():
                if not self.output_path.is_file() or _is_reparse_point(self.output_path):
                    raise ValueError("Die vorhandene Review-Ausgabe ist unsicher.")
                current_sha256 = _sha256(_read_limited(self.output_path))
            if current_sha256 != self._output_sha256:
                raise ValueError(
                    "Die Review-Ausgabe wurde parallel veraendert. "
                    "Bitte den Pruefplatz neu starten."
                )
            _atomic_write_json(self.output_path, document)
            self._output_sha256 = _sha256(_read_limited(self.output_path))

    def _state_locked(self, preferred_id: str | None = None) -> dict[str, object]:
        items = [self._public_row(image.candidate_id) for image in self._images]
        current_id = preferred_id
        if current_id is None:
            current_id = next(
                (
                    image.candidate_id
                    for image in self._images
                    if image.candidate_id not in self._decisions
                ),
                self._images[0].candidate_id if self._images else None,
            )
        counts = {
            value: sum(
                1
                for saved in self._decisions.values()
                if saved["decision"] == value
            )
            for value in VALID_DECISIONS
        }
        return {
            "holdout_id": self.holdout_id,
            "revision": self._revision,
            "total": len(self._images),
            "done": len(self._decisions),
            "open": len(self._images) - len(self._decisions),
            "counts": counts,
            "classes": [item.public() for item in self._classes],
            "current": self._public_row(current_id) if current_id else None,
            "items": items,
        }

    def _public_row(self, candidate_id: str) -> dict[str, object]:
        image = self._images_by_id[candidate_id]
        saved = self._decisions.get(candidate_id)
        return {
            "id": candidate_id,
            "image_url": f"/image?id={quote(candidate_id, safe='')}",
            "width": image.width,
            "height": image.height,
            "operator_references": [
                reference.public() for reference in image.operator_references
            ],
            "decision": saved["decision"] if saved else None,
            "comment": saved["comment"] if saved else "",
            "annotations": saved["annotations"] if saved else [],
        }


def _validate_holdout(holdout_root: Path) -> HoldoutSnapshot:
    root = Path(os.path.abspath(holdout_root))
    if not root.is_dir() or _is_reparse_point(root):
        raise ValueError("Der Detect-Release-Holdout ist kein sicherer Ordner.")
    if os.path.normcase(os.path.realpath(root)) != os.path.normcase(str(root)):
        raise ValueError("Der Detect-Release-Holdout ist verknuepft oder unsicher.")

    manifest_path = root / "_manifest.json"
    candidates_path = root / "_candidates.json"
    images_root = root / "images"
    for path, label in (
        (manifest_path, "Manifest"),
        (candidates_path, "Kandidatenliste"),
    ):
        if not path.is_file() or _is_reparse_point(path):
            raise ValueError(f"{label} fehlt oder ist unsicher.")
    if not images_root.is_dir() or _is_reparse_point(images_root):
        raise ValueError("Der Bilderordner fehlt oder ist unsicher.")

    manifest_bytes = _read_limited(manifest_path)
    candidates_bytes = _read_limited(candidates_path)
    manifest = _load_json_bytes(manifest_bytes, "Holdout-Manifest")
    candidates_document = _load_json_bytes(candidates_bytes, "Kandidatenliste")
    if not isinstance(manifest, dict) or not _MANIFEST_REQUIRED_FIELDS.issubset(manifest):
        raise ValueError("Holdout-Manifest hat fehlende Pflichtfelder.")
    if not isinstance(candidates_document, dict) or set(candidates_document) != _CANDIDATES_FIELDS:
        raise ValueError("Kandidatenliste hat fehlende oder fremde Felder.")
    if manifest.get("schema_version") != SCHEMA_VERSION:
        raise ValueError(f"Holdout-Manifest braucht Schema {SCHEMA_VERSION}.")
    if manifest.get("purpose") != HOLDOUT_PURPOSE:
        raise ValueError("Manifest ist kein Detect-Release-Holdout.")
    if manifest.get("frozen") is not True:
        raise ValueError("Detect-Release-Holdout muss mit frozen=true eingefroren sein.")
    if manifest.get("hash_algorithm") != "sha256":
        raise ValueError("Holdout-Manifest braucht hash_algorithm=sha256.")
    if candidates_document.get("schema_version") != SCHEMA_VERSION:
        raise ValueError(f"Kandidatenliste braucht Schema {SCHEMA_VERSION}.")
    if candidates_document.get("purpose") != CANDIDATES_PURPOSE:
        raise ValueError("Datei ist keine Detect-Release-Kandidatenliste.")

    holdout_id = _required_identifier(manifest.get("holdout_id"), "Holdout-ID")
    if candidates_document.get("holdout_id") != holdout_id:
        raise ValueError("Manifest und Kandidatenliste haben verschiedene Holdout-IDs.")
    candidates_sha256 = _require_sha256(
        manifest.get("candidates_sha256"),
        "Kandidaten-SHA",
    )
    actual_candidates_sha256 = _sha256(candidates_bytes)
    if candidates_sha256 != actual_candidates_sha256:
        raise ValueError("Kandidatenliste stimmt nicht mit candidates_sha256 ueberein.")

    candidate_id = _required_identifier(manifest.get("candidate_id"), "Kandidaten-ID")
    candidate_manifest_sha256 = _require_sha256(
        manifest.get("candidate_manifest_sha256"),
        "Kandidatenmanifest-SHA",
    )
    candidate_weights_sha256 = _require_sha256(
        manifest.get("candidate_weights_sha256"),
        "Kandidatengewicht-SHA",
    )
    class_map_version = _required_positive_integer(
        manifest.get("class_map_version"),
        "Klassenkarten-Version",
    )
    if class_map_version != 3:
        raise ValueError("Detect-Release-Holdout braucht class_map_version=3.")
    class_map_sha256 = _require_sha256(
        manifest.get("class_map_sha256"),
        "Klassenkarten-SHA",
    )
    vsa_manifest_sha256 = _require_sha256(
        manifest.get("vsa_manifest_sha256"),
        "VSA-Manifest-SHA",
    )
    vsa_manifest_hash = _require_sha256(
        manifest.get("vsa_manifest_hash"),
        "VSA-Manifest-Hash",
    )
    if vsa_manifest_hash != vsa_manifest_sha256:
        raise ValueError("vsa_manifest_hash und vsa_manifest_sha256 widersprechen sich.")
    classes = _validate_classes(manifest.get("classes"))

    raw_candidates = candidates_document.get("candidates")
    if not isinstance(raw_candidates, list) or not raw_candidates:
        raise ValueError("Kandidatenliste braucht mindestens einen Kandidaten.")
    candidates_count = _required_positive_integer(
        manifest.get("candidates_count"),
        "Kandidatenanzahl",
    )
    if candidates_count != len(raw_candidates):
        raise ValueError("candidates_count stimmt nicht mit der Kandidatenliste ueberein.")
    hashes = _validate_manifest_hashes(manifest.get("hashes"))
    hashes_count = _required_positive_integer(
        manifest.get("hashes_count"),
        "Hashanzahl",
    )
    if hashes_count != len(hashes):
        raise ValueError("hashes_count stimmt nicht mit dem hashes-Objekt ueberein.")
    candidate_hash_entry = hashes.get("_candidates.json")
    if candidate_hash_entry is None or candidate_hash_entry[0] != candidates_sha256:
        raise ValueError("Manifest bindet _candidates.json nicht korrekt.")
    if candidate_hash_entry[1] is not None and candidate_hash_entry[1] != len(candidates_bytes):
        raise ValueError("Manifest nennt eine falsche Groesse fuer _candidates.json.")

    seen_ids: set[str] = set()
    seen_paths: set[str] = set()
    seen_hashes: set[str] = set()
    images: list[VerifiedImage] = []
    for index, raw in enumerate(raw_candidates):
        if (
            not isinstance(raw, dict)
            or not _CANDIDATE_REQUIRED_FIELDS.issubset(raw)
            or not set(raw).issubset(
                _CANDIDATE_REQUIRED_FIELDS | _CANDIDATE_OPTIONAL_FIELDS
            )
        ):
            raise ValueError(f"Kandidat {index + 1} hat fehlende oder fremde Felder.")
        image_id = _required_identifier(raw.get("id"), f"Kandidat {index + 1} ID")
        folded_id = image_id.casefold()
        if folded_id in seen_ids:
            raise ValueError(f"Doppelte Kandidaten-ID: {image_id}")
        seen_ids.add(folded_id)

        relative_path = _required_image_path(raw.get("image_path"))
        frame_path = _required_frame_path(raw.get("frame_path"))
        if frame_path != PurePosixPath(relative_path).name:
            raise ValueError(
                f"Kandidat {image_id}: frame_path stimmt nicht mit image_path ueberein."
            )
        folded_path = relative_path.casefold()
        if folded_path in seen_paths:
            raise ValueError(f"Doppelter Bildpfad: {relative_path}")
        seen_paths.add(folded_path)
        image_sha256 = _require_sha256(raw.get("image_sha256"), "Bild-SHA")
        if image_sha256 in seen_hashes:
            raise ValueError("Dasselbe Bild ist mehrfach im Holdout enthalten.")
        seen_hashes.add(image_sha256)
        size_bytes = _required_positive_integer(raw.get("size_bytes"), "Bildgroesse")
        width = _required_positive_integer(raw.get("width"), "Bildbreite")
        height = _required_positive_integer(raw.get("height"), "Bildhoehe")
        holding_key = _required_canonical_holding(raw.get("haltung_key"))
        expected_physical = _physical_holding_key(holding_key)
        if raw.get("physical_holding_key") != expected_physical:
            raise ValueError(
                f"Kandidat {image_id} besitzt keinen kanonischen physischen Haltungsschluessel."
            )
        operator_references = _validate_operator_references(
            raw.get("operator_reference"),
            raw.get("operator_references"),
            {item.class_id: item for item in classes},
        )

        image_path = root.joinpath(*PurePosixPath(relative_path).parts)
        if not _path_is_within(image_path, images_root):
            raise ValueError(f"Kandidat {image_id} verweist ausserhalb des Bilderordners.")
        manifest_hash = hashes.get(relative_path)
        if manifest_hash is None or manifest_hash[0] != image_sha256:
            raise ValueError(f"Manifest bindet das Bild {relative_path} nicht korrekt.")
        if manifest_hash[1] is not None and manifest_hash[1] != size_bytes:
            raise ValueError(f"Manifest nennt eine falsche Groesse fuer {relative_path}.")
        verified = VerifiedImage(
            candidate_id=image_id,
            relative_path=relative_path,
            path=image_path,
            sha256=image_sha256,
            size_bytes=size_bytes,
            width=width,
            height=height,
            operator_references=operator_references,
        )
        _read_verified_image(verified)
        images.append(verified)

    expected_hash_paths = {"_candidates.json", *(image.relative_path for image in images)}
    if set(hashes) != expected_hash_paths:
        raise ValueError("Manifest-Hashes decken den Holdout nicht exakt ab.")
    expected_files = {"_manifest.json", "_candidates.json", *(image.relative_path for image in images)}
    expected_dirs = {"images"}
    for image in images:
        parent = PurePosixPath(image.relative_path).parent
        while str(parent) not in {".", ""}:
            expected_dirs.add(parent.as_posix())
            parent = parent.parent
    actual_files, actual_dirs = _collect_safe_tree(root)
    if actual_files != expected_files or actual_dirs != expected_dirs:
        raise ValueError("Holdout enthaelt fehlende oder nicht gebundene Dateien/Ordner.")

    return HoldoutSnapshot(
        holdout_id=holdout_id,
        manifest_sha256=_sha256(manifest_bytes),
        candidates_sha256=candidates_sha256,
        candidate_id=candidate_id,
        candidate_manifest_sha256=candidate_manifest_sha256,
        candidate_weights_sha256=candidate_weights_sha256,
        class_map_version=class_map_version,
        class_map_sha256=class_map_sha256,
        vsa_manifest_hash=vsa_manifest_hash,
        vsa_manifest_sha256=vsa_manifest_sha256,
        classes=classes,
        images=tuple(images),
    )


def _validate_classes(raw_classes: object) -> tuple[DetectClass, ...]:
    if not isinstance(raw_classes, list) or len(raw_classes) != 15:
        raise ValueError("Der Holdout braucht exakt 15 Klassen.")
    classes: list[DetectClass] = []
    seen_names: set[str] = set()
    for expected_id, raw in enumerate(raw_classes):
        if not isinstance(raw, dict) or set(raw) != {"id", "name", "label"}:
            raise ValueError("Jede Klasse braucht exakt id, name und label.")
        class_id = raw.get("id")
        if isinstance(class_id, bool) or class_id != expected_id:
            raise ValueError("Klassen-IDs muessen lueckenlos von 0 bis 14 sortiert sein.")
        name = _required_identifier(raw.get("name"), f"Klassenname {expected_id}")
        if name != _EXPECTED_CLASS_NAMES[expected_id]:
            raise ValueError(
                "Klassenname oder Klassenreihenfolge stimmt nicht mit class_map v3 ueberein."
            )
        label = _required_text(raw.get("label"), f"Klassenlabel {expected_id}", MAX_TEXT_CHARACTERS)
        if name.casefold() in seen_names:
            raise ValueError("Klassennamen muessen eindeutig sein.")
        seen_names.add(name.casefold())
        classes.append(DetectClass(expected_id, name, label))
    return tuple(classes)


def _validate_operator_references(
    singular: object,
    plural: object,
    classes_by_id: dict[int, DetectClass],
) -> tuple[OperatorReference, ...]:
    raw_references: list[object] = []
    if singular is not None:
        raw_references.append(singular)
    if plural is not None:
        if not isinstance(plural, list):
            raise ValueError("operator_references muss eine Liste sein.")
        raw_references.extend(plural)

    result: list[OperatorReference] = []
    seen: set[tuple[str, str, int | None]] = set()
    allowed_fields = {
        "code",
        "text",
        "description",
        "finding_text",
        "finding",
        "label",
        "klartext",
        "befund",
        "class_id",
        "class_name",
    }
    text_fields = (
        "text",
        "description",
        "finding_text",
        "finding",
        "klartext",
        "befund",
        "label",
    )
    prefix_map = {
        item.name.split("_", maxsplit=1)[0]: item
        for item in classes_by_id.values()
        if item.name != "SONST_schaden"
    }
    for raw in raw_references:
        if not isinstance(raw, dict) or not set(raw).issubset(allowed_fields):
            raise ValueError("Operateur-Referenz besitzt ein ungueltiges Schema.")
        raw_code = raw.get("code")
        if not isinstance(raw_code, str):
            raise ValueError("Operateur-Referenz braucht einen VSA-Code.")
        code = raw_code.strip().upper()
        if not _VSA_CODE_PATTERN.fullmatch(code):
            raise ValueError("Operateur-Referenz besitzt einen ungueltigen VSA-Code.")
        text = next(
            (
                unicodedata.normalize("NFC", str(raw.get(field))).strip()
                for field in text_fields
                if isinstance(raw.get(field), str) and str(raw.get(field)).strip()
            ),
            "",
        )
        if not text or len(text) > 2_000:
            raise ValueError("Operateur-Referenz braucht einen gueltigen Klartext/Befund.")
        if any(
            (ord(character) < 32 and character not in "\n\t")
            or ord(character) == 127
            for character in text
        ):
            raise ValueError("Operateur-Referenz enthaelt ungueltige Steuerzeichen.")

        raw_class_id = raw.get("class_id")
        raw_class_name = raw.get("class_name")
        if (raw_class_id is None) != (raw_class_name is None):
            raise ValueError(
                "Operateur-Referenz braucht class_id und class_name gemeinsam."
            )
        mapped: DetectClass | None
        if raw_class_id is not None:
            if isinstance(raw_class_id, bool) or not isinstance(raw_class_id, int):
                raise ValueError("Operateur-Referenz besitzt eine ungueltige Klassen-ID.")
            mapped = classes_by_id.get(raw_class_id)
            if mapped is None or raw_class_name != mapped.name:
                raise ValueError("Operateur-Referenz verwendet eine unbekannte Klasse.")
        else:
            mapped = prefix_map.get(code[:3])
        key = (code, text, mapped.class_id if mapped else None)
        if key in seen:
            continue
        seen.add(key)
        result.append(
            OperatorReference(
                code=code,
                text=text,
                class_id=mapped.class_id if mapped else None,
                class_name=mapped.name if mapped else None,
            )
        )
    return tuple(result)


def _validate_manifest_hashes(raw_hashes: object) -> dict[str, tuple[str, int | None]]:
    if not isinstance(raw_hashes, dict) or not raw_hashes:
        raise ValueError("Manifest braucht ein nichtleeres hashes-Objekt.")
    result: dict[str, tuple[str, int | None]] = {}
    for raw_path, raw_entry in raw_hashes.items():
        if not isinstance(raw_path, str) or not raw_path:
            raise ValueError("Manifest enthaelt einen ungueltigen Hashpfad.")
        if raw_path != "_candidates.json":
            _required_image_path(raw_path)
        if not isinstance(raw_entry, dict) or not set(raw_entry).issubset(
            {"sha256", "size_bytes"}
        ) or "sha256" not in raw_entry:
            raise ValueError(f"Hash {raw_path} braucht ein Objekt mit sha256.")
        sha256 = _require_sha256(raw_entry.get("sha256"), f"Hash {raw_path}")
        size = raw_entry.get("size_bytes")
        if size is not None:
            size = _required_positive_integer(size, f"Groesse {raw_path}")
        result[raw_path] = (sha256, size)
    return result


def _validate_annotations(
    decision: str,
    raw_annotations: object,
    classes_by_id: dict[int, DetectClass],
) -> list[dict[str, object]]:
    if not isinstance(raw_annotations, list):
        raise ValueError("Annotationen muessen eine Liste sein.")
    if decision == "positive" and not raw_annotations:
        raise ValueError("Eine positive Entscheidung braucht mindestens eine Box.")
    if decision in {"negative", "exclude"} and raw_annotations:
        raise ValueError("Negative und ausgeschlossene Bilder duerfen keine Boxen besitzen.")

    result: list[dict[str, object]] = []
    seen_ids: set[str] = set()
    for index, raw in enumerate(raw_annotations):
        if not isinstance(raw, dict) or set(raw) != {
            "id",
            "class_id",
            "class_name",
            "box",
        }:
            raise ValueError(f"Annotation {index + 1} hat fehlende oder fremde Felder.")
        annotation_id = _required_identifier(raw.get("id"), "Annotations-ID")
        if annotation_id.casefold() in seen_ids:
            raise ValueError("Annotations-IDs muessen je Bild eindeutig sein.")
        seen_ids.add(annotation_id.casefold())
        class_id = raw.get("class_id")
        if isinstance(class_id, bool) or not isinstance(class_id, int):
            raise ValueError("Klassen-ID muss eine Ganzzahl sein.")
        known_class = classes_by_id.get(class_id)
        if known_class is None or raw.get("class_name") != known_class.name:
            raise ValueError("Annotation verwendet eine unbekannte Klasse.")
        box = _validate_normalized_box(raw.get("box"))
        result.append(
            {
                "id": annotation_id,
                "class_id": class_id,
                "class_name": known_class.name,
                "box": box,
            }
        )
    return result


def _validate_normalized_box(raw_box: object) -> dict[str, float]:
    fields = ("x_center", "y_center", "width", "height")
    if not isinstance(raw_box, dict) or set(raw_box) != set(fields):
        raise ValueError("Box braucht exakt x_center, y_center, width und height.")
    values: dict[str, float] = {}
    for field in fields:
        raw_value = raw_box.get(field)
        if isinstance(raw_value, bool) or not isinstance(raw_value, (int, float)):
            raise ValueError(f"Boxwert {field} muss eine Zahl sein.")
        value = float(raw_value)
        if not math.isfinite(value):
            raise ValueError(f"Boxwert {field} muss endlich sein.")
        values[field] = value
    if not 0.0 <= values["x_center"] <= 1.0 or not 0.0 <= values["y_center"] <= 1.0:
        raise ValueError("Boxmittelpunkt muss im normalisierten Bild liegen.")
    if not 0.0 < values["width"] <= 1.0 or not 0.0 < values["height"] <= 1.0:
        raise ValueError("Boxbreite und Boxhoehe muessen in (0, 1] liegen.")
    epsilon = 1e-9
    if (
        values["x_center"] - values["width"] / 2.0 < -epsilon
        or values["x_center"] + values["width"] / 2.0 > 1.0 + epsilon
        or values["y_center"] - values["height"] / 2.0 < -epsilon
        or values["y_center"] + values["height"] / 2.0 > 1.0 + epsilon
    ):
        raise ValueError("Box muss vollstaendig innerhalb des Bildes liegen.")
    return values


def _validate_saved_decision(
    raw: object,
    classes_by_id: dict[int, DetectClass],
) -> dict[str, object]:
    if not isinstance(raw, dict) or set(raw) != {
        "decision",
        "comment",
        "reviewed_at_utc",
        "annotations",
    }:
        raise ValueError("Gespeicherte Entscheidung hat ein falsches Schema.")
    decision = str(raw.get("decision") or "").strip().casefold()
    if decision not in VALID_DECISIONS:
        raise ValueError("Gespeicherte Entscheidung ist ungueltig.")
    return {
        "decision": decision,
        "comment": _comment_text(raw.get("comment")),
        "reviewed_at_utc": _require_utc_timestamp(
            raw.get("reviewed_at_utc"),
            "Review-Zeitpunkt",
        ),
        "annotations": _validate_annotations(
            decision,
            raw.get("annotations"),
            classes_by_id,
        ),
    }


def _read_verified_image(image: VerifiedImage) -> bytes:
    path = image.path
    if not path.is_file() or _is_reparse_point(path):
        raise ValueError("Ein Holdout-Bild fehlt oder ist unsicher.")
    payload = path.read_bytes()
    if len(payload) != image.size_bytes or _sha256(payload) != image.sha256:
        raise ValueError("Ein Holdout-Bild wurde veraendert.")
    width, height = _image_dimensions(payload, path.suffix)
    if width != image.width or height != image.height:
        raise ValueError("Die Bildmasse stimmen nicht mit dem Holdout ueberein.")
    return payload


def _image_dimensions(payload: bytes, suffix: str) -> tuple[int, int]:
    normalized = suffix.casefold()
    if normalized == ".png":
        if (
            len(payload) < 24
            or payload[:8] != b"\x89PNG\r\n\x1a\n"
            or payload[12:16] != b"IHDR"
        ):
            raise ValueError("PNG-Bildsignatur ist ungueltig.")
        width, height = struct.unpack(">II", payload[16:24])
        if width <= 0 or height <= 0:
            raise ValueError("PNG-Bildmasse sind ungueltig.")
        return width, height
    if normalized not in {".jpg", ".jpeg"} or not payload.startswith(b"\xff\xd8"):
        raise ValueError("Bildsignatur ist ungueltig.")
    index = 2
    sof_markers = {
        0xC0,
        0xC1,
        0xC2,
        0xC3,
        0xC5,
        0xC6,
        0xC7,
        0xC9,
        0xCA,
        0xCB,
        0xCD,
        0xCE,
        0xCF,
    }
    while index < len(payload):
        while index < len(payload) and payload[index] != 0xFF:
            index += 1
        while index < len(payload) and payload[index] == 0xFF:
            index += 1
        if index >= len(payload):
            break
        marker = payload[index]
        index += 1
        if marker in {0xD8, 0xD9} or 0xD0 <= marker <= 0xD7:
            continue
        if index + 2 > len(payload):
            break
        segment_length = int.from_bytes(payload[index : index + 2], "big")
        if segment_length < 2 or index + segment_length > len(payload):
            break
        if marker in sof_markers:
            if segment_length < 7:
                break
            height = int.from_bytes(payload[index + 3 : index + 5], "big")
            width = int.from_bytes(payload[index + 5 : index + 7], "big")
            if width > 0 and height > 0:
                return width, height
            break
        index += segment_length
    raise ValueError("JPEG-Bildmasse konnten nicht sicher gelesen werden.")


def _collect_safe_tree(root: Path) -> tuple[set[str], set[str]]:
    files: set[str] = set()
    directories: set[str] = set()
    pending = [root]
    while pending:
        current = pending.pop()
        with os.scandir(current) as entries:
            for entry in entries:
                path = Path(entry.path)
                if _is_reparse_point(path):
                    raise ValueError("Holdout enthaelt eine Verknuepfung oder Junction.")
                relative = path.relative_to(root).as_posix()
                if entry.is_dir(follow_symlinks=False):
                    directories.add(relative)
                    pending.append(path)
                elif entry.is_file(follow_symlinks=False):
                    files.add(relative)
                else:
                    raise ValueError("Holdout enthaelt einen unbekannten Dateityp.")
    return files, directories


def _required_image_path(value: object) -> str:
    if not isinstance(value, str) or not value or "\\" in value:
        raise ValueError("Bildpfad muss ein relativer Pfad unter images/ sein.")
    path = PurePosixPath(value)
    if (
        path.is_absolute()
        or len(path.parts) != 2
        or path.parts[0] != "images"
        or any(part in {"", ".", ".."} for part in path.parts)
        or path.suffix.casefold() not in IMAGE_SUFFIXES
        or path.as_posix() != value
    ):
        raise ValueError("Bildpfad muss ein kanonischer Pfad unter images/ sein.")
    return value


def _required_frame_path(value: object) -> str:
    if not isinstance(value, str) or not value or "\\" in value or "/" in value:
        raise ValueError("frame_path muss genau ein Bilddateiname sein.")
    path = PurePosixPath(value)
    if (
        path.name != value
        or path.suffix.casefold() not in IMAGE_SUFFIXES
        or path.parts != (value,)
    ):
        raise ValueError("frame_path muss genau ein gueltiger Bilddateiname sein.")
    return value


def _required_canonical_holding(value: object) -> str:
    if not isinstance(value, str) or not _CANONICAL_HOLDING_PATTERN.fullmatch(value):
        raise ValueError("haltung_key muss ein kanonischer numerischer Schachtpaar-Schluessel sein.")
    return value


def _physical_holding_key(holding_key: str) -> str:
    left, right = holding_key.split("-", maxsplit=1)
    return "|".join(sorted((left, right)))


def _required_identifier(value: object, label: str) -> str:
    if not isinstance(value, str):
        raise ValueError(f"{label} ist erforderlich.")
    text = value.strip()
    if not _IDENTIFIER_PATTERN.fullmatch(text):
        raise ValueError(f"{label} ist ungueltig.")
    return text


def _required_text(value: object, label: str, maximum: int) -> str:
    if not isinstance(value, str):
        raise ValueError(f"{label} ist erforderlich.")
    text = unicodedata.normalize("NFC", value).strip()
    if not text:
        raise ValueError(f"{label} ist erforderlich.")
    if len(text) > maximum:
        raise ValueError(f"{label} ist zu lang.")
    if any(ord(character) < 32 or ord(character) == 127 for character in text):
        raise ValueError(f"{label} enthaelt ungueltige Steuerzeichen.")
    return text


def _comment_text(value: object) -> str:
    if not isinstance(value, str):
        raise ValueError("Kommentar muss Text sein.")
    text = unicodedata.normalize("NFC", value).strip()
    if len(text) > MAX_COMMENT_CHARACTERS:
        raise ValueError("Kommentar ist zu lang.")
    if any(
        (ord(character) < 32 and character not in "\n\t")
        or ord(character) == 127
        for character in text
    ):
        raise ValueError("Kommentar enthaelt ungueltige Steuerzeichen.")
    return text


def _require_sha256(value: object, label: str) -> str:
    if not isinstance(value, str) or not _SHA256_PATTERN.fullmatch(value):
        raise ValueError(f"{label} ist keine gueltige SHA-256-Pruefsumme.")
    return value


def _required_positive_integer(value: object, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ValueError(f"{label} muss eine positive Ganzzahl sein.")
    return value


def _required_revision(value: object) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise ValueError("Revision muss eine nichtnegative Ganzzahl sein.")
    return value


def _require_utc_timestamp(value: object, label: str) -> str:
    if not isinstance(value, str) or not value or not value.endswith("Z"):
        raise ValueError(f"{label} muss ein UTC-Zeitpunkt sein.")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise ValueError(f"{label} muss ein UTC-Zeitpunkt sein.") from error
    if parsed.utcoffset() != timezone.utc.utcoffset(parsed):
        raise ValueError(f"{label} muss ein UTC-Zeitpunkt sein.")
    return value


def _current_utc() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def _read_limited(path: Path) -> bytes:
    if not path.is_file() or _is_reparse_point(path):
        raise ValueError(f"Datei fehlt oder ist unsicher: {path.name}")
    size = path.stat().st_size
    if size <= 0 or size > MAX_JSON_FILE_BYTES:
        raise ValueError(f"Datei ist leer oder zu gross: {path.name}")
    payload = path.read_bytes()
    if len(payload) != size:
        raise ValueError(f"Datei wurde waehrend des Lesens veraendert: {path.name}")
    return payload


def _load_json_bytes(payload: bytes, label: str) -> object:
    def reject_duplicates(pairs: list[tuple[str, object]]) -> dict[str, object]:
        result: dict[str, object] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"{label} enthaelt den doppelten Schluessel {key}.")
            result[key] = value
        return result

    def reject_constant(value: str) -> object:
        raise ValueError(f"{label} enthaelt den ungueltigen Zahlenwert {value}.")

    try:
        return json.loads(
            payload.decode("utf-8"),
            object_pairs_hook=reject_duplicates,
            parse_constant=reject_constant,
        )
    except UnicodeDecodeError as error:
        raise ValueError(f"{label} ist kein gueltiges UTF-8-JSON.") from error
    except json.JSONDecodeError as error:
        raise ValueError(f"{label} ist kein gueltiges JSON.") from error


def _sha256(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def _image_content_type(suffix: str) -> str:
    return "image/png" if suffix.casefold() == ".png" else "image/jpeg"


def _path_is_within(path: Path, root: Path) -> bool:
    try:
        return os.path.commonpath(
            (os.path.normcase(str(path)), os.path.normcase(str(root)))
        ) == os.path.normcase(str(root))
    except ValueError:
        return False


def _is_reparse_point(path: Path) -> bool:
    try:
        information = os.lstat(path)
    except OSError as error:
        raise ValueError("Ein Pfad ist nicht sicher lesbar.") from error
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    attributes = getattr(information, "st_file_attributes", 0)
    return stat.S_ISLNK(information.st_mode) or bool(attributes & reparse_flag)


def _validate_output_location(output_path: Path, holdout_root: Path) -> None:
    if output_path.suffix.casefold() != ".json":
        raise ValueError("Die Review-Ausgabe muss eine JSON-Datei sein.")
    if _path_is_within(output_path, holdout_root) or _path_is_within(
        Path(os.path.realpath(output_path)),
        Path(os.path.realpath(holdout_root)),
    ):
        raise ValueError("Die Review-Ausgabe muss ausserhalb des Holdout-Ordners liegen.")
    if output_path.exists() and (
        not output_path.is_file() or _is_reparse_point(output_path)
    ):
        raise ValueError("Die vorhandene Review-Ausgabe ist unsicher.")


def _prepare_safe_output_parent(path: Path) -> Path:
    parent = Path(os.path.abspath(path.parent))
    existing = parent
    while not existing.exists():
        if existing.parent == existing:
            raise ValueError("Die Review-Ausgabewurzel fehlt.")
        existing = existing.parent
    if (
        not existing.is_dir()
        or _is_reparse_point(existing)
        or os.path.normcase(os.path.realpath(existing)) != os.path.normcase(str(existing))
    ):
        raise ValueError("Die Review-Ausgabewurzel ist unsicher.")
    parent.mkdir(parents=True, exist_ok=True)
    current = existing
    for part in parent.relative_to(existing).parts:
        current = current / part
        if not current.is_dir() or _is_reparse_point(current):
            raise ValueError("Der Review-Ausgabeordner ist unsicher.")
    if os.path.normcase(os.path.realpath(parent)) != os.path.normcase(str(parent)):
        raise ValueError("Der Review-Ausgabeordner ist unsicher.")
    return parent


@contextmanager
def _exclusive_review_output_lock(path: Path) -> Iterator[None]:
    parent = _prepare_safe_output_parent(path)
    lock_path = parent / f".{path.name}.lock"
    if lock_path.exists() and (
        not lock_path.is_file() or _is_reparse_point(lock_path)
    ):
        raise ValueError("Die Review-Sperrdatei ist unsicher.")
    descriptor = os.open(lock_path, os.O_CREAT | os.O_RDWR, 0o600)
    with os.fdopen(descriptor, "r+b", closefd=True) as stream:
        stream.seek(0, os.SEEK_END)
        if stream.tell() == 0:
            stream.write(b"\0")
            stream.flush()
            os.fsync(stream.fileno())
        stream.seek(0)
        if os.name == "nt":
            import msvcrt

            msvcrt.locking(stream.fileno(), msvcrt.LK_LOCK, 1)
        else:
            import fcntl

            fcntl.flock(stream.fileno(), fcntl.LOCK_EX)
        try:
            yield
        finally:
            stream.seek(0)
            if os.name == "nt":
                import msvcrt

                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                import fcntl

                fcntl.flock(stream.fileno(), fcntl.LOCK_UN)


def _atomic_write_json(path: Path, document: object) -> None:
    parent = _prepare_safe_output_parent(path)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.",
        suffix=".tmp",
        dir=parent,
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(document, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        _prepare_safe_output_parent(path)
        if path.exists() and (not path.is_file() or _is_reparse_point(path)):
            raise ValueError("Die vorhandene Review-Ausgabe ist unsicher.")
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def create_server(
    store: DetectReleaseHoldoutReviewStore,
    port: int = 8774,
) -> ThreadingHTTPServer:
    html = INDEX_HTML

    class DetectReleaseReviewHandler(BaseHTTPRequestHandler):
        server_version = "SewerStudioDetectReleaseReview/1.0"

        def do_GET(self) -> None:  # noqa: N802
            if not self._has_loopback_host():
                self._send_json({"error": "Ungueltiger Host."}, status=421)
                return
            parsed = urlparse(self.path)
            if parsed.path == "/" and not parsed.query:
                self._send_bytes(html.encode("utf-8"), "text/html; charset=utf-8")
                return
            if parsed.path == "/api/state" and not parsed.query:
                self._send_json(store.state())
                return
            if parsed.path == "/image":
                values = parse_qs(parsed.query, keep_blank_values=True).get("id", [])
                if len(values) != 1:
                    self._send_json({"error": "Unbekannte Bild-ID."}, status=404)
                    return
                try:
                    body, content_type = store.image_bytes_for(values[0])
                    self._send_bytes(body, content_type)
                except KeyError:
                    self._send_json({"error": "Unbekannte Bild-ID."}, status=404)
                except (ValueError, OSError):
                    self._send_json({"error": "Bild ist nicht sicher verfuegbar."}, status=409)
                return
            self._send_json({"error": "Nicht gefunden."}, status=404)

        def do_POST(self) -> None:  # noqa: N802
            if not self._has_loopback_host():
                self._send_json({"error": "Ungueltiger Host."}, status=421)
                return
            if not self._has_allowed_origin():
                self._send_json({"error": "Ungueltiger Origin."}, status=403)
                return
            parsed = urlparse(self.path)
            if parsed.path != "/api/review" or parsed.query:
                self._send_json({"error": "Nicht gefunden."}, status=404)
                return
            try:
                content_type = self.headers.get("Content-Type", "")
                if content_type.split(";", 1)[0].strip().casefold() != "application/json":
                    self._send_json(
                        {"error": "Content-Type muss application/json sein."},
                        status=415,
                    )
                    return
                raw_length = self.headers.get("Content-Length")
                if raw_length is None:
                    self._send_json({"error": "Content-Length fehlt."}, status=411)
                    return
                try:
                    length = int(raw_length)
                except ValueError:
                    self._send_json({"error": "Content-Length ist ungueltig."}, status=400)
                    return
                if length < 0:
                    self._send_json({"error": "Content-Length ist ungueltig."}, status=400)
                    return
                if length > MAX_REQUEST_BODY_BYTES:
                    # Einen begrenzten Anfang einlesen. Dadurch erreicht die 413-Antwort
                    # den Browser auch unter Windows, ohne eine riesige Anfrage zu puffern.
                    self.rfile.read(min(length, MAX_REQUEST_BODY_BYTES + 1))
                    self.close_connection = True
                    self._send_json({"error": "Anfrage ist zu gross."}, status=413)
                    return
                payload = _load_json_bytes(self.rfile.read(length), "Review-Anfrage")
                if not isinstance(payload, dict) or set(payload) != {
                    "id",
                    "decision",
                    "annotations",
                    "comment",
                    "revision",
                }:
                    raise ValueError("Review-Anfrage hat fremde oder fehlende Felder.")
                state = store.set_decision(
                    payload.get("id"),
                    payload.get("decision"),
                    payload.get("annotations"),
                    payload.get("comment"),
                    payload.get("revision"),
                )
                self._send_json(state)
            except KeyError:
                self._send_json({"error": "Unbekannte Bild-ID."}, status=404)
            except ReviewRevisionConflictError as error:
                self._send_json({"error": str(error)}, status=409)
            except ValueError as error:
                self._send_json({"error": str(error)}, status=400)
            except OSError:
                self._send_json({"error": "Review konnte nicht gespeichert werden."}, status=500)

        def do_OPTIONS(self) -> None:  # noqa: N802
            self._send_json({"error": "Nicht erlaubt."}, status=405)

        def _has_loopback_host(self) -> bool:
            port_number = int(self.server.server_address[1])
            host = self.headers.get("Host", "").strip().casefold()
            return host in {
                f"127.0.0.1:{port_number}",
                f"localhost:{port_number}",
            }

        def _has_allowed_origin(self) -> bool:
            origin = self.headers.get("Origin")
            if origin is None:
                return True
            port_number = int(self.server.server_address[1])
            return origin.strip().casefold() in {
                f"http://127.0.0.1:{port_number}",
                f"http://localhost:{port_number}",
            }

        def _send_json(self, data: object, status: int = 200) -> None:
            self._send_bytes(
                json.dumps(data, ensure_ascii=False).encode("utf-8"),
                "application/json; charset=utf-8",
                status,
            )

        def _send_bytes(self, body: bytes, content_type: str, status: int = 200) -> None:
            self.send_response(status)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("X-Frame-Options", "DENY")
            self.send_header("Referrer-Policy", "no-referrer")
            self.send_header(
                "Content-Security-Policy",
                "default-src 'self'; img-src 'self' data:; "
                "style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; "
                "frame-ancestors 'none'; base-uri 'none'; form-action 'none'",
            )
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, _format: str, *_args: object) -> None:
            return

    server = ThreadingHTTPServer(("127.0.0.1", port), DetectReleaseReviewHandler)
    server.daemon_threads = True
    return server


INDEX_HTML = r"""<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Detect Release-Holdout · Box-Review</title>
<style>
:root{color-scheme:dark;--bg:#0d1523;--panel:#1d2939;--line:#475569;--text:#f8fafc;--muted:#b8c2d1;--blue:#3b82f6;--green:#10b981;--red:#ef4444;--amber:#f59e0b}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font-family:Segoe UI,Arial,sans-serif;overflow:hidden}
header{height:76px;margin:8px;padding:14px 18px;background:var(--panel);border:1px solid #334155;border-radius:12px;display:flex;align-items:center;justify-content:space-between}
h1{font-size:21px;margin:0 0 5px}.muted{color:var(--muted);font-size:13px}.layout{height:calc(100vh - 92px);display:grid;grid-template-columns:minmax(0,1fr) 390px;gap:10px;padding:0 8px 8px}
.viewer,.side{background:var(--panel);border:1px solid #334155;border-radius:12px;min-height:0}.viewer{display:flex;align-items:center;justify-content:center;overflow:auto;padding:10px}.canvas-wrap{position:relative;line-height:0}canvas{display:block;background:#000;max-width:100%;max-height:calc(100vh - 120px);width:auto;height:auto;cursor:crosshair}
.side{padding:14px;overflow:auto}label{display:block;font-size:13px;color:var(--muted);margin:9px 0 5px}select,textarea{width:100%;background:#0f172a;color:var(--text);border:1px solid var(--line);border-radius:7px;padding:9px;font:inherit}textarea{min-height:70px;resize:vertical}
.buttons{display:grid;gap:8px;margin-top:12px}button{border:0;border-radius:7px;padding:11px;font-weight:700;color:white;cursor:pointer}button:disabled{opacity:.45;cursor:not-allowed}.positive{background:var(--green)}.negative{background:var(--red)}.exclude{background:var(--amber);color:#231600}.secondary{background:#526174}.nav{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:9px}
.box-list{display:grid;gap:7px;margin-top:8px}.box-row{border:1px solid var(--line);border-left:5px solid var(--blue);border-radius:7px;padding:8px;display:grid;grid-template-columns:1fr auto;gap:8px;align-items:center;cursor:pointer}.box-row.selected{outline:2px solid #facc15}.box-row button{background:#b91c1c;padding:7px 9px}.status{min-height:40px;margin-top:11px;color:var(--muted);white-space:pre-wrap}.error{color:#fca5a5}.ok{color:#86efac}.kbd{margin-top:14px;font-size:12px;color:var(--muted)}
.reference{border:1px solid #64748b;border-radius:7px;padding:8px;margin-top:7px;background:#111c2e}.reference b{display:block;color:#fcd34d}.reference p{margin:5px 0;white-space:pre-wrap}.reference button{background:#2563eb;padding:7px 9px}.guidance{margin:9px 0;padding:9px;border:1px solid #475569;border-radius:7px;color:#dbeafe;font-size:13px;line-height:1.4}
@media(max-width:900px){body{overflow:auto}.layout{height:auto;grid-template-columns:1fr}.viewer{min-height:55vh}.side{min-height:420px}header{height:auto}.layout canvas{max-height:70vh}}
</style>
</head>
<body>
<header><div><h1>Detect Release-Holdout · menschliche Boxprüfung</h1><div class="muted">Keine Modellvorhersage · keine Trainingsspeicherung</div></div><div id="progress">–</div></header>
<main class="layout">
  <section class="viewer"><div class="canvas-wrap"><canvas id="canvas"></canvas></div></section>
  <aside class="side">
    <div class="muted">Bild-ID</div><div id="imageId">–</div>
    <div class="guidance"><b>Positiv:</b> Alle sichtbaren Objekte der 15 Klassen einzeichnen. Mehrere Boxen sind erlaubt.<br><b>Negativ:</b> Nur wählen, wenn keine der 15 Klassen sichtbar ist.<br><b>Ausschließen:</b> Für unbrauchbare oder unklare Bilder.</div>
    <label>Operateur-Referenz aus PDF</label><div id="operatorReferences"></div>
    <label for="classSelect">Klasse für neue oder gewählte Box</label><select id="classSelect"></select>
    <div class="nav"><button class="secondary" id="undoBox">Letzte Box löschen</button><button class="secondary" id="clearBoxes">Alle Boxen löschen</button></div>
    <label>Boxen im Bild</label><div id="boxList" class="box-list"></div>
    <label for="comment">Optionaler Kommentar</label><textarea id="comment" maxlength="2000"></textarea>
    <div class="buttons">
      <button class="positive" id="positive">Positiv bestätigen</button>
      <button class="negative" id="negative">Negativ – keine der 15 Klassen</button>
      <button class="exclude" id="exclude">Ausschließen</button>
    </div>
    <div class="nav"><button class="secondary" id="previous">← Vorheriges</button><button class="secondary" id="next">Nächstes →</button></div>
    <div id="status" class="status"></div>
    <div class="kbd">Maus: Box ziehen · Entf: gewählte Box löschen · Pfeil links/rechts: Bild wechseln · 1/2/3: speichern</div>
  </aside>
</main>
<script>
'use strict';
const $=id=>document.getElementById(id),canvas=$('canvas'),ctx=canvas.getContext('2d');
let reviewState=null,currentIndex=0,working=[],selectedId=null,dragStart=null,dragNow=null,imageToken=0;
const sourceImage=new Image();
function setStatus(text,kind=''){const node=$('status');node.textContent=text;node.className='status '+kind}
function current(){return reviewState?.items?.[currentIndex]||null}
function classById(id){return reviewState.classes.find(item=>item.id===Number(id))}
function copyAnnotations(value){return JSON.parse(JSON.stringify(value||[]))}
function annotationId(){return 'ann-'+(crypto.randomUUID?crypto.randomUUID():Date.now()+'-'+Math.random().toString(16).slice(2))}
async function loadState(preferredId=null){
  const response=await fetch('/api/state',{cache:'no-store'});if(!response.ok)throw new Error('Prüfzustand konnte nicht geladen werden.');
  reviewState=await response.json();
  $('classSelect').replaceChildren(...reviewState.classes.map(item=>{const option=document.createElement('option');option.value=item.id;option.textContent=`${item.id} · ${item.name} — ${item.label}`;return option}));
  if(preferredId){const found=reviewState.items.findIndex(item=>item.id===preferredId);if(found>=0)currentIndex=found}else{const open=reviewState.items.findIndex(item=>!item.decision);currentIndex=open>=0?open:0}
  await showCurrent();
}
async function showCurrent(){
  const item=current();if(!item){setStatus('Keine Bilder vorhanden.','error');return}
  selectedId=null;working=copyAnnotations(item.annotations);$('comment').value=item.comment||'';$('imageId').textContent=item.id;renderOperatorReferences(item.operator_references||[]);
  $('progress').textContent=`${reviewState.done} / ${reviewState.total} geprüft · ${working.length} Box(en)`;
  renderBoxList();setStatus('Bild wird geprüft…');const token=++imageToken;
  await new Promise((resolve,reject)=>{sourceImage.onload=resolve;sourceImage.onerror=()=>reject(new Error('Bild konnte nicht sicher geladen werden.'));sourceImage.src=item.image_url+'&v='+Date.now()});
  if(token!==imageToken)return;canvas.width=sourceImage.naturalWidth;canvas.height=sourceImage.naturalHeight;draw();setStatus(item.decision?`Gespeichert als: ${item.decision}`:'Box ziehen und Entscheidung speichern.',item.decision?'ok':'');
}
function renderOperatorReferences(references){
  const root=$('operatorReferences');root.replaceChildren();
  if(!references.length){const empty=document.createElement('div');empty.className='muted';empty.textContent='Keine Operateur-Referenz vorhanden.';root.append(empty);return}
  references.forEach(reference=>{const card=document.createElement('div');card.className='reference';const title=document.createElement('b');title.textContent=`${reference.code} · keine KI-Angabe`;const text=document.createElement('p');text.textContent=reference.text;card.append(title,text);if(reference.class_id!==null){const button=document.createElement('button');button.type='button';button.textContent=`Klasse ${reference.class_name} übernehmen`;button.addEventListener('click',()=>{$('classSelect').value=reference.class_id;$('classSelect').dispatchEvent(new Event('change'));setStatus('Operateur-Klasse gewählt. Box bitte selbst prüfen oder zeichnen.','ok')});card.append(button)}root.append(card)});
}
function draw(){
  if(!sourceImage.complete||!canvas.width)return;ctx.clearRect(0,0,canvas.width,canvas.height);ctx.drawImage(sourceImage,0,0,canvas.width,canvas.height);
  const line=Math.max(2,canvas.width/500);ctx.font=`${Math.max(14,canvas.width/55)}px Segoe UI`;
  for(const ann of working){const b=ann.box,x=(b.x_center-b.width/2)*canvas.width,y=(b.y_center-b.height/2)*canvas.height,w=b.width*canvas.width,h=b.height*canvas.height;ctx.strokeStyle=ann.id===selectedId?'#facc15':'#00f58a';ctx.lineWidth=line;ctx.strokeRect(x,y,w,h);const text=ann.class_name;const tw=ctx.measureText(text).width;ctx.fillStyle='rgba(0,0,0,.75)';ctx.fillRect(x,Math.max(0,y-24),tw+10,24);ctx.fillStyle='#fff';ctx.fillText(text,x+5,Math.max(17,y-6))}
  if(dragStart&&dragNow){const x=Math.min(dragStart.x,dragNow.x),y=Math.min(dragStart.y,dragNow.y),w=Math.abs(dragNow.x-dragStart.x),h=Math.abs(dragNow.y-dragStart.y);ctx.strokeStyle='#ff3b30';ctx.lineWidth=line;ctx.strokeRect(x,y,w,h)}
}
function point(event){const rect=canvas.getBoundingClientRect();return{x:Math.max(0,Math.min(canvas.width,(event.clientX-rect.left)*canvas.width/rect.width)),y:Math.max(0,Math.min(canvas.height,(event.clientY-rect.top)*canvas.height/rect.height))}}
canvas.addEventListener('pointerdown',event=>{if(!current())return;canvas.setPointerCapture(event.pointerId);dragStart=point(event);dragNow=dragStart;draw()});
canvas.addEventListener('pointermove',event=>{if(!dragStart)return;dragNow=point(event);draw()});
canvas.addEventListener('pointerup',event=>{if(!dragStart)return;const end=point(event),x1=Math.min(dragStart.x,end.x),x2=Math.max(dragStart.x,end.x),y1=Math.min(dragStart.y,end.y),y2=Math.max(dragStart.y,end.y);dragStart=dragNow=null;if(x2-x1>=4&&y2-y1>=4){const cls=classById($('classSelect').value);const ann={id:annotationId(),class_id:cls.id,class_name:cls.name,box:{x_center:(x1+x2)/(2*canvas.width),y_center:(y1+y2)/(2*canvas.height),width:(x2-x1)/canvas.width,height:(y2-y1)/canvas.height}};working.push(ann);selectedId=ann.id}renderBoxList();draw()});
$('classSelect').addEventListener('change',()=>{const ann=working.find(item=>item.id===selectedId);if(!ann)return;const cls=classById($('classSelect').value);ann.class_id=cls.id;ann.class_name=cls.name;renderBoxList();draw()});
function renderBoxList(){
  const list=$('boxList');list.replaceChildren();
  working.forEach((ann,index)=>{const row=document.createElement('div');row.className='box-row'+(ann.id===selectedId?' selected':'');const text=document.createElement('div');const cls=classById(ann.class_id);text.textContent=`${index+1}. ${ann.class_name} — ${cls?.label||''}`;const remove=document.createElement('button');remove.type='button';remove.textContent='Löschen';remove.addEventListener('click',event=>{event.stopPropagation();removeAnnotation(ann.id)});row.addEventListener('click',()=>{selectedId=ann.id;$('classSelect').value=ann.class_id;renderBoxList();draw()});row.append(text,remove);list.append(row)});
  $('progress').textContent=reviewState?`${reviewState.done} / ${reviewState.total} geprüft · ${working.length} Box(en)`:'–';
}
function removeAnnotation(id){working=working.filter(item=>item.id!==id);if(selectedId===id)selectedId=null;renderBoxList();draw()}
$('undoBox').addEventListener('click',()=>{if(selectedId)removeAnnotation(selectedId);else if(working.length)removeAnnotation(working[working.length-1].id)});
$('clearBoxes').addEventListener('click',()=>{if(working.length&&confirm('Alle Boxen dieses Bildes löschen?')){working=[];selectedId=null;renderBoxList();draw()}});
async function saveDecision(decision){
  const item=current();if(!item)return;let annotations=copyAnnotations(working);
  if(decision==='positive'&&!annotations.length){setStatus('Positiv braucht mindestens eine Box.','error');return}
  if(decision!=='positive'&&annotations.length){if(!confirm('Diese Entscheidung entfernt alle gezeichneten Boxen. Fortfahren?'))return;annotations=[]}
  setStatus('Speichere…');
  const response=await fetch('/api/review',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({id:item.id,decision,annotations,comment:$('comment').value,revision:reviewState.revision})});
  const data=await response.json();if(!response.ok){if(response.status===409)await loadState(item.id);throw new Error(data.error||'Speichern fehlgeschlagen.')}
  reviewState=data;const savedIndex=reviewState.items.findIndex(row=>row.id===item.id);currentIndex=savedIndex>=0?savedIndex:currentIndex;working=copyAnnotations(reviewState.items[currentIndex].annotations);renderBoxList();setStatus('Entscheidung sicher gespeichert.','ok');
}
async function move(delta){if(!reviewState?.items?.length)return;currentIndex=(currentIndex+delta+reviewState.items.length)%reviewState.items.length;await showCurrent()}
$('positive').addEventListener('click',()=>saveDecision('positive').catch(error=>setStatus(error.message,'error')));
$('negative').addEventListener('click',()=>saveDecision('negative').catch(error=>setStatus(error.message,'error')));
$('exclude').addEventListener('click',()=>saveDecision('exclude').catch(error=>setStatus(error.message,'error')));
$('previous').addEventListener('click',()=>move(-1).catch(error=>setStatus(error.message,'error')));$('next').addEventListener('click',()=>move(1).catch(error=>setStatus(error.message,'error')));
document.addEventListener('keydown',event=>{if(['INPUT','TEXTAREA','SELECT'].includes(event.target.tagName))return;if(event.key==='ArrowLeft'){event.preventDefault();move(-1).catch(error=>setStatus(error.message,'error'))}else if(event.key==='ArrowRight'){event.preventDefault();move(1).catch(error=>setStatus(error.message,'error'))}else if(event.key==='Delete'&&selectedId){removeAnnotation(selectedId)}else if(event.key==='1'){saveDecision('positive').catch(error=>setStatus(error.message,'error'))}else if(event.key==='2'){saveDecision('negative').catch(error=>setStatus(error.message,'error'))}else if(event.key==='3'){saveDecision('exclude').catch(error=>setStatus(error.message,'error'))}});
loadState().catch(error=>setStatus(error.message,'error'));
</script>
</body>
</html>
"""


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Lokaler Mehrklassen-Box-Review fuer einen Detect-Release-Holdout."
    )
    parser.add_argument("--holdout", required=True, help="Eingefrorener Holdout-Ordner")
    parser.add_argument("--output", required=True, help="Review-JSON ausserhalb des Holdouts")
    parser.add_argument("--reviewer", required=True, help="Name des menschlichen Pruefers")
    parser.add_argument("--port", type=int, default=8774, help="Lokaler Port, Standard 8774")
    parser.add_argument("--prepare-only", action="store_true", help="Nur pruefen und Review-Datei vorbereiten")
    parser.add_argument("--open-browser", action="store_true", help="Pruefplatz im Standardbrowser oeffnen")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _build_parser()
    args = parser.parse_args(argv)
    if not 1 <= args.port <= 65535:
        parser.error("--port muss zwischen 1 und 65535 liegen.")
    if args.prepare_only and args.open_browser:
        parser.error("--open-browser ist zusammen mit --prepare-only nicht sinnvoll.")
    store = DetectReleaseHoldoutReviewStore(
        args.holdout,
        args.output,
        args.reviewer,
    )
    state = store.prepare_output()
    print(f"Detect Release-Holdout: {store.holdout_id}")
    print(f"Bilder: {state['total']}; offen: {state['open']}")
    print(f"Review-Ausgabe: {store.output_path}")
    if args.prepare_only:
        print("Pruefung und Vorbereitung abgeschlossen.")
        return 0
    server = create_server(store, args.port)
    url = f"http://127.0.0.1:{server.server_address[1]}/"
    print(f"Mehrklassen-Pruefplatz: {url}")
    print("Stoppen mit Strg+C")
    if args.open_browser:
        webbrowser.open(url, new=1)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nPruefplatz beendet.")
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
