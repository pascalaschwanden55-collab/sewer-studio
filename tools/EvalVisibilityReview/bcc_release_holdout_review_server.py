from __future__ import annotations

import argparse
import hashlib
import json
import mimetypes
import os
import re
import stat
import tempfile
import threading
import unicodedata
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime, timezone
from html import escape
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Callable, Sequence
from urllib.parse import parse_qs, quote, urlparse


REVIEW_SCHEMA = "1.0"
REVIEW_PURPOSE = "bcc_release_holdout_review"
HOLDOUT_PURPOSE = "bcc_release_holdout"
HARD_NEGATIVE_REVIEW_PURPOSE = "bcc_hard_negative_review"
HARD_NEGATIVE_QUEUE_PURPOSE = "bcc_hard_negative_review_queue"
PROTO_NEGATIVE_QUEUE_PURPOSE = "proto_hard_negative_review_queue"
PROTO_NEGATIVE_PILOT = "protokoll_negative"
HOLDOUT_NAME = "SewerStudio BCC Release Holdout"
LEGACY_V1_HOLDOUT_ID = (
    "64d06094c921e90440e96823d3fc8d5ec0275c6465840201a4092f1285fe5c2e"
)
HOLDOUT_PILOT = "BCC_bogen"
HOLDOUT_ROLE = "acceptance"
VALID_DECISIONS = frozenset({"positive", "negative", "exclude"})
HARD_NEGATIVE_DECISIONS = frozenset(
    {"all_classes_clear", "mapped_object_visible", "exclude_uncertain"}
)
IMAGE_SUFFIXES = frozenset({".jpg", ".jpeg", ".png"})
MIN_IMAGE_BYTES = 1_024
MAX_REQUEST_BODY_BYTES = 16 * 1024
MAX_COMMENT_CHARACTERS = 2_000
MAX_REVIEWER_CHARACTERS = 128

_SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
_IDENTIFIER_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
_EXPECTED_ROOT_ENTRIES = frozenset(
    {"_manifest.json", "_candidates.json", "images"}
)


@dataclass(frozen=True)
class _VerifiedImage:
    candidate_id: str
    path: Path
    sha256: str
    size_bytes: int


class ReviewRevisionConflictError(ValueError):
    """Ein Browser versucht, einen inzwischen veralteten Zustand zu speichern."""


class BccReleaseHoldoutReviewStore:
    """Strikter, blinder Review-Speicher fuer einen eingefrorenen BCC-Holdout."""

    review_purpose = REVIEW_PURPOSE
    valid_decisions = VALID_DECISIONS
    identity_field = "holdout_id"
    manifest_binding_field = "manifest_sha256"
    candidate_binding_field = "candidates_sha256"

    def __init__(
        self,
        holdout_root: str | Path,
        output_path: str | Path,
        reviewer: object,
        now_utc: Callable[[], str] | None = None,
    ):
        self.holdout_root = Path(os.path.abspath(holdout_root))
        self.output_path = Path(os.path.abspath(output_path))
        self.reviewer = _required_text(
            reviewer,
            "Reviewer",
            MAX_REVIEWER_CHARACTERS,
        )
        self._now_utc = now_utc or _current_utc
        self._lock = threading.RLock()
        self._updated_at_utc = ""
        self._output_sha256: str | None = None
        self._revision = 0

        if _path_is_within(self.output_path, self.holdout_root) or _path_is_within(
            Path(os.path.realpath(self.output_path)),
            Path(os.path.realpath(self.holdout_root)),
        ):
            raise ValueError(
                "Die Review-Ausgabe muss ausserhalb des eingefrorenen "
                "Holdout-Ordners liegen."
            )
        if self.output_path.exists():
            if (
                not self.output_path.is_file()
                or _is_reparse_point(self.output_path)
            ):
                raise ValueError("Die vorhandene Review-Ausgabe ist unsicher.")

        validated_source = self._validate_source(self.holdout_root)
        (
            self.holdout_id,
            self.manifest_sha256,
            self.candidates_sha256,
            verified_images,
        ) = validated_source
        self._source_fingerprint = self._fingerprint_source(validated_source)
        self._additional_review_bindings = self._load_additional_review_bindings()
        _prepare_safe_output_parent(self.output_path)
        self._images = list(verified_images)
        self._images_by_id = {
            image.candidate_id: image for image in self._images
        }
        self._decisions: dict[str, dict[str, str]] = {}
        self._merge_existing_output()

    def prepare_output(self) -> dict[str, object]:
        with self._lock:
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
        comment: object = "",
        expected_revision: object | None = None,
    ) -> dict[str, object]:
        case_id = str(candidate_id or "")
        if case_id not in self._images_by_id:
            raise KeyError("Unbekannte Bild-ID.")
        value = str(decision or "").strip().casefold()
        if value not in self.valid_decisions:
            raise ValueError(
                "Entscheidung ist fuer diesen Pruefplatz ungueltig."
            )
        normalized_comment = _comment_text(comment)
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
            previous = self._decisions.get(case_id)
            previous_updated = self._updated_at_utc
            reviewed_at = self._timestamp()
            self._decisions[case_id] = {
                "decision": value,
                "comment": normalized_comment,
                "reviewed_at_utc": reviewed_at,
            }
            self._updated_at_utc = reviewed_at
            try:
                self._write_output_locked()
            except Exception:
                if previous is None:
                    self._decisions.pop(case_id, None)
                else:
                    self._decisions[case_id] = previous
                self._updated_at_utc = previous_updated
                raise
            self._revision += 1
            return self._state_locked(preferred_id=case_id)

    def _assert_source_unchanged(self) -> None:
        try:
            current = self._validate_source(self.holdout_root)
        except (OSError, ValueError) as error:
            raise ValueError(
                "Die gebundene Review-Quelle wurde seit dem Start veraendert."
            ) from error
        if self._fingerprint_source(current) != self._source_fingerprint:
            raise ValueError(
                "Die gebundene Review-Quelle wurde seit dem Start veraendert."
            )

    @staticmethod
    def _fingerprint_source(
        validated_source: tuple[
            str,
            str,
            str,
            tuple[_VerifiedImage, ...],
        ],
    ) -> tuple[object, ...]:
        identity, manifest_sha, candidates_sha, images = validated_source
        return (
            identity,
            manifest_sha,
            candidates_sha,
            tuple(
                (
                    image.candidate_id,
                    os.path.normcase(str(image.path)),
                    image.sha256,
                    image.size_bytes,
                )
                for image in images
            ),
        )

    def image_bytes_for(self, candidate_id: object) -> tuple[bytes, str]:
        case_id = str(candidate_id or "")
        image = self._images_by_id.get(case_id)
        if image is None:
            raise KeyError("Unbekannte Bild-ID.")
        if (
            not image.path.is_file()
            or _is_reparse_point(image.path)
            or image.path.stat().st_size != image.size_bytes
        ):
            raise ValueError("Das eingefrorene Holdout-Bild wurde veraendert.")
        body = image.path.read_bytes()
        if hashlib.sha256(body).hexdigest() != image.sha256:
            raise ValueError("Das eingefrorene Holdout-Bild wurde veraendert.")
        _validate_image_signature(body, image.path.suffix)
        return body, _image_content_type(image.path.suffix)

    def _merge_existing_output(self) -> None:
        if not self.output_path.exists():
            return
        raw = self.output_path.read_bytes()
        existing = _load_json_bytes(raw, "Review-Ausgabe")
        if not isinstance(existing, dict):
            raise ValueError("Die vorhandene Review-Ausgabe ist ungueltig.")
        if existing.get("schema_version") != REVIEW_SCHEMA:
            raise ValueError("Die vorhandene Review-Ausgabe hat ein falsches Schema.")
        if existing.get("purpose") != self.review_purpose:
            raise ValueError("Die vorhandene Datei gehoert nicht zu diesem Review.")
        if existing.get(self.identity_field) != self.holdout_id:
            raise ValueError("Die Review-Ausgabe gehoert zu einer anderen Pruefliste.")
        if existing.get(self.manifest_binding_field) != self.manifest_sha256:
            raise ValueError("Die Review-Ausgabe ist nicht an dieses Manifest gebunden.")
        if existing.get(self.candidate_binding_field) != self.candidates_sha256:
            raise ValueError(
                "Die Review-Ausgabe ist nicht an diese Prueffaelle gebunden."
            )
        if existing.get("reviewer") != self.reviewer:
            raise ValueError("Die Review-Ausgabe gehoert zu einem anderen Reviewer.")
        expected_fields = {
            "schema_version",
            "purpose",
            self.identity_field,
            self.manifest_binding_field,
            self.candidate_binding_field,
            "reviewer",
            "updated_at_utc",
            "decisions",
            *self._additional_review_bindings,
        }
        if set(existing) != expected_fields:
            raise ValueError("Die Review-Ausgabe hat fehlende oder fremde Felder.")
        for field, expected in self._additional_review_bindings.items():
            if existing.get(field) != expected:
                raise ValueError(
                    f"Die Review-Ausgabe ist nicht an {field} gebunden."
                )

        decisions = existing.get("decisions")
        if not isinstance(decisions, dict):
            raise ValueError("Die Review-Ausgabe enthaelt keine Entscheidungen.")
        unknown_ids = set(decisions) - set(self._images_by_id)
        if unknown_ids:
            raise ValueError("Die Review-Ausgabe enthaelt unbekannte Bild-IDs.")

        loaded: dict[str, dict[str, str]] = {}
        for candidate_id, raw_decision in decisions.items():
            if not isinstance(raw_decision, dict):
                raise ValueError(
                    f"Die Entscheidung fuer {candidate_id} ist ungueltig."
                )
            if set(raw_decision) != {
                "decision",
                "comment",
                "reviewed_at_utc",
            }:
                raise ValueError(
                    f"Die Entscheidung fuer {candidate_id} hat fremde Felder."
                )
            value = raw_decision.get("decision")
            if value not in self.valid_decisions:
                raise ValueError(
                    f"Die Entscheidung fuer {candidate_id} ist ungueltig."
                )
            loaded[candidate_id] = {
                "decision": str(value),
                "comment": _comment_text(raw_decision.get("comment")),
                "reviewed_at_utc": _required_text(
                    raw_decision.get("reviewed_at_utc"),
                    "Review-Zeitpunkt",
                    64,
                ),
            }
        self._decisions = loaded
        updated = existing.get("updated_at_utc")
        if updated:
            self._updated_at_utc = _required_text(
                updated,
                "Aktualisierungszeitpunkt",
                64,
            )
        self._output_sha256 = hashlib.sha256(raw).hexdigest()

    def _timestamp(self) -> str:
        return _required_text(self._now_utc(), "Review-Zeitpunkt", 64)

    def _write_output_locked(self) -> None:
        with _exclusive_review_output_lock(self.output_path):
            self._write_output_with_version_check()

    def _write_output_with_version_check(self) -> None:
        current_sha256 = None
        if self.output_path.exists():
            if (
                not self.output_path.is_file()
                or _is_reparse_point(self.output_path)
            ):
                raise ValueError("Die vorhandene Review-Ausgabe ist unsicher.")
            current_sha256 = hashlib.sha256(
                self.output_path.read_bytes()
            ).hexdigest()
        if current_sha256 != self._output_sha256:
            raise ValueError(
                "Die Review-Ausgabe wurde parallel veraendert. "
                "Bitte den Pruefplatz neu starten."
            )
        document = {
            "schema_version": REVIEW_SCHEMA,
            "purpose": self.review_purpose,
            self.identity_field: self.holdout_id,
            self.manifest_binding_field: self.manifest_sha256,
            self.candidate_binding_field: self.candidates_sha256,
            **self._additional_review_bindings,
            "reviewer": self.reviewer,
            "updated_at_utc": self._updated_at_utc,
            "decisions": {
                image.candidate_id: dict(self._decisions[image.candidate_id])
                for image in self._images
                if image.candidate_id in self._decisions
            },
        }
        _atomic_write_json(self.output_path, document)
        self._output_sha256 = hashlib.sha256(
            self.output_path.read_bytes()
        ).hexdigest()

    def _state_locked(
        self,
        preferred_id: str | None = None,
    ) -> dict[str, object]:
        items = [self._public_row(image.candidate_id) for image in self._images]
        counts = {
            value: sum(
                1
                for review in self._decisions.values()
                if review["decision"] == value
            )
            for value in self.valid_decisions
        }
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
        current = self._public_row(current_id) if current_id else None
        return {
            self.identity_field: self.holdout_id,
            "revision": self._revision,
            "total": len(self._images),
            "done": len(self._decisions),
            "open": len(self._images) - len(self._decisions),
            "counts": counts,
            "current": current,
            "items": items,
        }

    def _public_row(self, candidate_id: str) -> dict[str, object]:
        decision = self._decisions.get(candidate_id)
        return {
            "id": candidate_id,
            "decision": decision["decision"] if decision else None,
            "comment": decision["comment"] if decision else "",
            "image_url": f"/image?id={quote(candidate_id, safe='')}",
        }

    def _validate_source(
        self,
        source_root: Path,
    ) -> tuple[str, str, str, tuple[_VerifiedImage, ...]]:
        return _validate_holdout(source_root)

    def html_template(self) -> str:
        return INDEX_HTML

    def _load_additional_review_bindings(self) -> dict[str, str]:
        return {}


def _validate_holdout(
    holdout_root: Path,
) -> tuple[str, str, str, tuple[_VerifiedImage, ...]]:
    if (
        not holdout_root.is_dir()
        or _is_reparse_point(holdout_root)
        or os.path.normcase(os.path.realpath(holdout_root))
        != os.path.normcase(str(holdout_root))
    ):
        raise ValueError("Der Holdout-Ordner fehlt oder ist unsicher.")

    entries = list(holdout_root.iterdir())
    if {entry.name for entry in entries} != _EXPECTED_ROOT_ENTRIES:
        raise ValueError("Holdout enthaelt unerwartete Dateien oder Ordner.")
    if any(_is_reparse_point(entry) for entry in entries):
        raise ValueError("Holdout enthaelt eine unsichere Verknuepfung.")

    manifest_path = holdout_root / "_manifest.json"
    candidates_path = holdout_root / "_candidates.json"
    images_root = holdout_root / "images"
    if (
        not manifest_path.is_file()
        or not candidates_path.is_file()
        or not images_root.is_dir()
    ):
        raise ValueError("Die Holdout-Struktur ist unvollstaendig.")

    image_paths = sorted(
        images_root.iterdir(),
        key=lambda item: (item.name.casefold(), item.name),
    )
    if any(
        not path.is_file()
        or _is_reparse_point(path)
        or path.suffix.casefold() not in IMAGE_SUFFIXES
        for path in image_paths
    ):
        raise ValueError("Der Holdout-Bildordner enthaelt unsichere Eintraege.")
    if len({path.name.casefold() for path in image_paths}) != len(image_paths):
        raise ValueError("Der Holdout enthaelt mehrdeutige Bildnamen.")

    manifest_bytes = manifest_path.read_bytes()
    candidates_bytes = candidates_path.read_bytes()
    manifest = _load_json_bytes(manifest_bytes, "Holdout-Manifest")
    candidates = _load_json_bytes(candidates_bytes, "Holdout-Kandidaten")
    if not isinstance(manifest, dict):
        raise ValueError("Das Holdout-Manifest muss ein JSON-Objekt sein.")
    if not isinstance(candidates, list):
        raise ValueError("Die Holdout-Kandidaten muessen ein JSON-Array sein.")
    if manifest.get("schema_version") != "1.0":
        raise ValueError("Das Holdout-Manifest hat ein falsches Schema.")
    manifest_purpose = manifest.get("purpose")
    legacy_named_holdout = (
        manifest_purpose is None
        and manifest.get("name") == HOLDOUT_NAME
        and manifest.get("holdout_id") == LEGACY_V1_HOLDOUT_ID
    )
    if manifest_purpose != HOLDOUT_PURPOSE and not legacy_named_holdout:
        raise ValueError("Das Manifest ist kein BCC-Release-Holdout.")
    if manifest.get("pilot") != HOLDOUT_PILOT:
        raise ValueError("Der Holdout gehoert nicht zum BCC-Pilot.")
    if manifest.get("role") != HOLDOUT_ROLE:
        raise ValueError("Der Holdout ist kein Acceptance-Holdout.")
    if manifest.get("frozen") is not True:
        raise ValueError("Das Holdout-Manifest ist nicht frozen=true.")
    if str(manifest.get("hash_algorithm") or "").casefold() != "sha256":
        raise ValueError("Der Holdout verwendet nicht SHA-256.")
    holdout_id = _require_sha256(manifest.get("holdout_id"), "Holdout-ID")
    if _required_integer(
        manifest.get("candidates_count"),
        "Holdout-Kandidatenzahl",
    ) != len(candidates):
        raise ValueError("Die Holdout-Kandidatenzahl stimmt nicht.")
    if _required_integer(
        manifest.get("images_count"),
        "Holdout-Bildzahl",
    ) != len(image_paths):
        raise ValueError("Die Holdout-Bildzahl stimmt nicht.")

    hashes = manifest.get("hashes")
    if not isinstance(hashes, dict):
        raise ValueError("Das Holdout-Manifest enthaelt keine Hashliste.")
    if _required_integer(
        manifest.get("hashes_count"),
        "Holdout-Hashzahl",
    ) != len(hashes):
        raise ValueError("Die Holdout-Hashzahl stimmt nicht.")
    expected_hash_paths = {"_candidates.json"} | {
        f"images/{path.name}" for path in image_paths
    }
    if set(hashes) != expected_hash_paths:
        raise ValueError("Die Holdout-Hashabdeckung ist nicht vollstaendig.")

    verified_hashes: dict[str, tuple[str, int]] = {}
    source_paths = {"_candidates.json": candidates_path}
    source_paths.update(
        {f"images/{path.name}": path for path in image_paths}
    )
    for relative in sorted(expected_hash_paths):
        raw_entry = hashes.get(relative)
        if not isinstance(raw_entry, dict):
            raise ValueError(f"Der Holdout-Hash fuer {relative} ist ungueltig.")
        if set(raw_entry) != {"sha256", "size_bytes"}:
            raise ValueError(f"Der Holdout-Hash fuer {relative} ist unvollstaendig.")
        expected_sha = _require_sha256(
            raw_entry.get("sha256"),
            f"Holdout-Hash {relative}",
        )
        expected_size = _required_integer(
            raw_entry.get("size_bytes"),
            f"Holdout-Groesse {relative}",
        )
        source_path = source_paths[relative]
        payload = source_path.read_bytes()
        if (
            len(payload) != expected_size
            or hashlib.sha256(payload).hexdigest() != expected_sha
        ):
            raise ValueError(
                f"Holdout-Datei stimmt nicht mit dem Manifest: {relative}"
            )
        verified_hashes[relative] = (expected_sha, expected_size)

    image_by_name = {path.name: path for path in image_paths}
    seen_ids: set[str] = set()
    seen_id_keys: set[str] = set()
    seen_images: set[str] = set()
    verified_images: list[_VerifiedImage] = []
    for index, candidate in enumerate(candidates):
        if not isinstance(candidate, dict):
            raise ValueError(f"Holdout-Kandidat {index} ist kein JSON-Objekt.")
        candidate_id = _required_identifier(
            candidate.get("id"),
            f"Holdout-Kandidat {index}",
        )
        candidate_key = candidate_id.casefold()
        if candidate_id in seen_ids or candidate_key in seen_id_keys:
            raise ValueError(f"Holdout-Kandidat {index} besitzt keine eindeutige ID.")

        frame = str(candidate.get("frame_path") or "").strip()
        if (
            not frame
            or Path(frame).name != frame
            or "/" in frame
            or "\\" in frame
            or frame not in image_by_name
        ):
            raise ValueError(f"Holdout-Kandidat {index} verweist auf kein sicheres Bild.")
        if frame in seen_images:
            raise ValueError(f"Holdout-Bild ist mehrfach referenziert: {frame}")
        relative = f"images/{frame}"
        image_sha, image_size = verified_hashes[relative]
        source_sha = _require_sha256(
            candidate.get("source_sha256"),
            f"Bildhash von Holdout-Kandidat {index}",
        )
        if source_sha != image_sha:
            raise ValueError(
                f"Holdout-Kandidat {index} stimmt nicht mit seinem Bildhash."
            )
        if not _numeric_holding_pattern(candidate.get("haltung_key")):
            raise ValueError(
                f"Holdout-Kandidat {index} besitzt keine belastbare Haltung."
            )
        _validate_image_signature(
            image_by_name[frame].read_bytes(),
            image_by_name[frame].suffix,
        )
        seen_ids.add(candidate_id)
        seen_id_keys.add(candidate_key)
        seen_images.add(frame)
        verified_images.append(
            _VerifiedImage(
                candidate_id=candidate_id,
                path=image_by_name[frame],
                sha256=image_sha,
                size_bytes=image_size,
            )
        )
    if seen_images != set(image_by_name):
        raise ValueError("Holdout-Bilder und Kandidaten sind nicht deckungsgleich.")

    return (
        holdout_id,
        hashlib.sha256(manifest_bytes).hexdigest(),
        hashlib.sha256(candidates_bytes).hexdigest(),
        tuple(verified_images),
    )


def _validate_hard_negative_queue(
    queue_root: Path,
) -> tuple[str, str, str, tuple[_VerifiedImage, ...]]:
    if (
        not queue_root.is_dir()
        or _is_reparse_point(queue_root)
        or os.path.normcase(os.path.realpath(queue_root))
        != os.path.normcase(str(queue_root))
    ):
        raise ValueError("Der Hard-Negative-Ordner fehlt oder ist unsicher.")

    entries = list(queue_root.iterdir())
    if {entry.name for entry in entries} != _EXPECTED_ROOT_ENTRIES:
        raise ValueError("Die Pruefliste enthaelt unerwartete Dateien oder Ordner.")
    if any(_is_reparse_point(entry) for entry in entries):
        raise ValueError("Die Pruefliste enthaelt eine unsichere Verknuepfung.")

    manifest_path = queue_root / "_manifest.json"
    candidates_path = queue_root / "_candidates.json"
    images_root = queue_root / "images"
    if (
        not manifest_path.is_file()
        or not candidates_path.is_file()
        or not images_root.is_dir()
    ):
        raise ValueError("Die Pruefliste ist unvollstaendig.")

    image_paths = sorted(
        images_root.iterdir(),
        key=lambda item: (item.name.casefold(), item.name),
    )
    if any(
        not path.is_file()
        or _is_reparse_point(path)
        or path.suffix.casefold() not in IMAGE_SUFFIXES
        for path in image_paths
    ):
        raise ValueError("Der Bildordner der Pruefliste ist unsicher.")
    if len({path.name.casefold() for path in image_paths}) != len(image_paths):
        raise ValueError("Die Pruefliste enthaelt mehrdeutige Bildnamen.")

    manifest_bytes = manifest_path.read_bytes()
    candidates_bytes = candidates_path.read_bytes()
    manifest = _load_json_bytes(manifest_bytes, "Hard-Negative-Manifest")
    candidates = _load_json_bytes(candidates_bytes, "Hard-Negative-Kandidaten")
    if not isinstance(manifest, dict) or not isinstance(candidates, list):
        raise ValueError("Die Hard-Negative-Pruefliste ist ungueltig.")
    if manifest.get("schema_version") != "1.0":
        raise ValueError("Das Hard-Negative-Manifest hat ein falsches Schema.")
    queue_purpose = manifest.get("purpose")
    if queue_purpose not in (HARD_NEGATIVE_QUEUE_PURPOSE, PROTO_NEGATIVE_QUEUE_PURPOSE):
        raise ValueError("Das Manifest ist keine Hard-Negative-Pruefliste.")
    is_proto_queue = queue_purpose == PROTO_NEGATIVE_QUEUE_PURPOSE
    expected_pilot = PROTO_NEGATIVE_PILOT if is_proto_queue else HOLDOUT_PILOT
    if manifest.get("pilot") != expected_pilot:
        raise ValueError("Die Pruefliste gehoert nicht zum deklarierten Piloten.")
    queue_prefix = "proto_hn_" if is_proto_queue else "bcc_hn_"
    if manifest.get("role") != "training_candidate_review":
        raise ValueError("Die Pruefliste hat eine ungueltige Rolle.")
    if manifest.get("frozen") is not True:
        raise ValueError("Die Pruefliste ist nicht frozen=true.")
    if str(manifest.get("hash_algorithm") or "").casefold() != "sha256":
        raise ValueError("Die Pruefliste verwendet nicht SHA-256.")
    queue_id = _require_sha256(manifest.get("queue_id"), "Prueflisten-ID")
    if queue_root.name != f"{queue_prefix}{queue_id[:12]}":
        raise ValueError("Der Ordner passt nicht zur Prueflisten-ID.")
    semantic = manifest.get("semantic")
    if not isinstance(semantic, dict):
        raise ValueError("Die Pruefliste besitzt keinen semantischen Beleg.")
    semantic_sha = hashlib.sha256(
        json.dumps(
            semantic,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()
    if semantic_sha != queue_id:
        raise ValueError("Die semantische Prueflisten-ID stimmt nicht.")
    for field in (
        "schema_version",
        "purpose",
        "pilot",
        "role",
        "class_map_version",
        "class_map_sha256",
        "vsa_manifest_hash",
        "class_names",
        "protected_sets",
        "protection_snapshot",
        "sources",
    ):
        if manifest.get(field) != semantic.get(field):
            raise ValueError(
                f"Semantischer Beleg und Manifest widersprechen sich bei {field}."
            )
    class_names = semantic.get("class_names")
    if (
        semantic.get("class_map_version") != 3
        or not isinstance(class_names, list)
        or len(class_names) != 15
        or len(set(class_names)) != 15
        or any(not isinstance(name, str) or not name for name in class_names)
        or class_names[14] != HOLDOUT_PILOT
    ):
        raise ValueError("Die Pruefliste bindet keine gueltige 15er-Klassenkarte.")
    _require_sha256(semantic.get("class_map_sha256"), "Klassenkarten-SHA")
    _require_sha256(semantic.get("vsa_manifest_hash"), "VSA-Manifest-SHA")
    selection_rule = semantic.get("selection_rule")
    if not isinstance(selection_rule, dict):
        raise ValueError("Die Hard-Negative-Auswahlregel ist ungueltig.")
    if is_proto_queue:
        if (
            selection_rule.get("one_image_per_physical_holding") is not True
            or selection_rule.get("requires_current_model_bcc_trigger") is True
            or selection_rule.get("model_involved") is not False
            or selection_rule.get("review_target")
            != "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse"
        ):
            raise ValueError("Die Protokoll-Negative-Auswahlregel ist ungueltig.")
    elif (
        selection_rule.get("one_image_per_physical_holding") is not True
        or selection_rule.get("requires_current_model_bcc_trigger") is not True
        or selection_rule.get("review_target")
        != "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse"
    ):
        raise ValueError("Die Hard-Negative-Auswahlregel ist ungueltig.")
    if _required_integer(
        manifest.get("candidates_count"),
        "Hard-Negative-Kandidatenzahl",
    ) != len(candidates):
        raise ValueError("Die Hard-Negative-Kandidatenzahl stimmt nicht.")
    if _required_integer(
        manifest.get("images_count"),
        "Hard-Negative-Bildzahl",
    ) != len(image_paths):
        raise ValueError("Die Hard-Negative-Bildzahl stimmt nicht.")

    hashes = manifest.get("hashes")
    if not isinstance(hashes, dict):
        raise ValueError("Das Hard-Negative-Manifest enthaelt keine Hashliste.")
    if _required_integer(
        manifest.get("hashes_count"),
        "Hard-Negative-Hashzahl",
    ) != len(hashes):
        raise ValueError("Die Hard-Negative-Hashzahl stimmt nicht.")
    expected_hash_paths = {"_candidates.json"} | {
        f"images/{path.name}" for path in image_paths
    }
    if set(hashes) != expected_hash_paths:
        raise ValueError("Die Hashabdeckung der Pruefliste ist unvollstaendig.")

    verified_hashes: dict[str, tuple[str, int]] = {}
    source_paths = {"_candidates.json": candidates_path}
    source_paths.update(
        {f"images/{path.name}": path for path in image_paths}
    )
    for relative in sorted(expected_hash_paths):
        raw_entry = hashes.get(relative)
        if not isinstance(raw_entry, dict) or set(raw_entry) != {
            "sha256",
            "size_bytes",
        }:
            raise ValueError(f"Der Hard-Negative-Hash fuer {relative} ist ungueltig.")
        expected_sha = _require_sha256(
            raw_entry.get("sha256"),
            f"Hard-Negative-Hash {relative}",
        )
        expected_size = _required_integer(
            raw_entry.get("size_bytes"),
            f"Hard-Negative-Groesse {relative}",
        )
        payload = source_paths[relative].read_bytes()
        if (
            len(payload) != expected_size
            or hashlib.sha256(payload).hexdigest() != expected_sha
        ):
            raise ValueError(
                f"Hard-Negative-Datei stimmt nicht mit dem Manifest: {relative}"
            )
        verified_hashes[relative] = (expected_sha, expected_size)

    image_by_name = {path.name: path for path in image_paths}
    seen_ids: set[str] = set()
    seen_id_keys: set[str] = set()
    seen_images: set[str] = set()
    verified_images: list[_VerifiedImage] = []
    for index, candidate in enumerate(candidates):
        if not isinstance(candidate, dict) or set(candidate) != {
            "id",
            "frame_path",
            "category",
            "status",
            "source_sha256",
        }:
            raise ValueError(
                f"Hard-Negative-Kandidat {index} hat fehlende oder fremde Felder."
            )
        candidate_id = _required_identifier(
            candidate.get("id"),
            f"Hard-Negative-Kandidat {index}",
        )
        candidate_key = candidate_id.casefold()
        if candidate_id in seen_ids or candidate_key in seen_id_keys:
            raise ValueError(
                f"Hard-Negative-Kandidat {index} besitzt keine eindeutige ID."
            )
        if (
            candidate.get("category") != "all_class_background_review"
            or candidate.get("status") != "pending_review"
        ):
            raise ValueError(
                f"Hard-Negative-Kandidat {index} hat einen ungueltigen Zweck."
            )
        frame = str(candidate.get("frame_path") or "").strip()
        if (
            not frame
            or Path(frame).name != frame
            or "/" in frame
            or "\\" in frame
            or frame not in image_by_name
            or frame in seen_images
        ):
            raise ValueError(
                f"Hard-Negative-Kandidat {index} verweist auf kein sicheres Bild."
            )
        relative = f"images/{frame}"
        image_sha, image_size = verified_hashes[relative]
        if _require_sha256(
            candidate.get("source_sha256"),
            f"Bildhash von Hard-Negative-Kandidat {index}",
        ) != image_sha:
            raise ValueError(
                f"Hard-Negative-Kandidat {index} stimmt nicht mit seinem Bildhash."
            )
        _validate_image_signature(
            image_by_name[frame].read_bytes(),
            image_by_name[frame].suffix,
        )
        seen_ids.add(candidate_id)
        seen_id_keys.add(candidate_key)
        seen_images.add(frame)
        verified_images.append(
            _VerifiedImage(
                candidate_id=candidate_id,
                path=image_by_name[frame],
                sha256=image_sha,
                size_bytes=image_size,
            )
        )
    if seen_images != set(image_by_name):
        raise ValueError("Hard-Negative-Bilder und Kandidaten sind nicht deckungsgleich.")

    receipt = manifest.get("selection_receipt")
    if not isinstance(receipt, dict):
        raise ValueError("Die Pruefliste besitzt keinen Auswahlbeleg.")
    receipt_items = receipt.get("items")
    if not isinstance(receipt_items, list):
        raise ValueError("Der Auswahlbeleg besitzt keine Bilder.")
    id_field = "item_id" if is_proto_queue else "id"
    receipt_ids = {
        str(item.get(id_field) or "")
        for item in receipt_items
        if isinstance(item, dict)
    }
    if len(receipt_ids) != len(receipt_items) or receipt_ids != seen_ids:
        raise ValueError("Auswahlbeleg und Pruefbilder stimmen nicht ueberein.")
    if receipt_items != semantic.get("items"):
        raise ValueError("Auswahlbeleg und semantischer Beleg widersprechen sich.")
    if is_proto_queue:
        # Protokollbasierte Auswahl: bewusst KEINE gebundenen Modelle.
        if receipt.get("models") or semantic.get("model_scope"):
            raise ValueError("Die Protokoll-Pruefliste darf keine gebundenen Modelle tragen.")
        models = []
    else:
        if receipt.get("models") != semantic.get("model_scope"):
            raise ValueError("Auswahlbeleg und semantischer Beleg widersprechen sich.")
        models = receipt.get("models")
    if not is_proto_queue and (not isinstance(models, list) or not models):
        raise ValueError("Der Auswahlbeleg besitzt keine gebundenen Modelle.")
    model_ids: set[str] = set()
    for index, model in enumerate(models):
        if not isinstance(model, dict):
            raise ValueError(f"Auswahlmodell {index} ist ungueltig.")
        model_id = _required_identifier(
            model.get("candidate_id"),
            f"Auswahlmodell {index}",
        )
        if model_id in model_ids:
            raise ValueError("Ein Auswahlmodell ist mehrfach vorhanden.")
        model_ids.add(model_id)
        _require_sha256(
            model.get("candidate_manifest_sha256"),
            f"Manifest-SHA von Auswahlmodell {index}",
        )
        _require_sha256(
            model.get("weights_sha256"),
            f"Gewichts-SHA von Auswahlmodell {index}",
        )
    verified_by_id = {
        image.candidate_id: image for image in verified_images
    }
    seen_physical_holdings: set[str] = set()
    for index, item in enumerate(receipt_items):
        if is_proto_queue:
            if not isinstance(item, dict) or set(item) != {
                "item_id",
                "image_sha256",
                "holding_key",
                "code",
                "gruppe",
                "quelle",
                "quell_datei",
                "leitungsinspektion",
                "size_bytes",
                "image_format",
                "target_file_name",
            }:
                raise ValueError(f"Auswahlbild {index} hat fehlende oder fremde Felder.")
            item_id = str(item.get("item_id") or "")
            verified = verified_by_id.get(item_id)
            if verified is None:
                raise ValueError(f"Auswahlbild {index} ist nicht gebunden.")
            physical = _proto_physical_holding_key(item.get("holding_key"))
            if physical in seen_physical_holdings:
                raise ValueError("Die Pruefliste enthaelt mehrere Bilder derselben Haltung.")
            seen_physical_holdings.add(physical)
            if (
                _require_sha256(
                    item.get("image_sha256"),
                    f"Bild-SHA von Auswahlbild {index}",
                )
                != verified.sha256
                or _required_integer(
                    item.get("size_bytes"),
                    f"Bildgroesse von Auswahlbild {index}",
                )
                != verified.size_bytes
                or str(item.get("image_format") or "").casefold()
                != verified.path.suffix.casefold().lstrip(".")
                or str(item.get("target_file_name") or "") != verified.path.name
            ):
                raise ValueError(f"Auswahlbild {index} stimmt nicht mit den Bildbytes.")
            continue
        if not isinstance(item, dict) or set(item) != {
            "id",
            "image_sha256",
            "holding_key",
            "physical_holding_key",
            "source_ref",
            "inspection_date",
            "size_bytes",
            "image_format",
            "predictions",
        }:
            raise ValueError(f"Auswahlbild {index} hat fehlende oder fremde Felder.")
        item_id = str(item.get("id") or "")
        verified = verified_by_id.get(item_id)
        if verified is None:
            raise ValueError(f"Auswahlbild {index} ist nicht gebunden.")
        holding_key = _normalized_holding_key(item.get("holding_key"))
        if holding_key is None or holding_key != str(item.get("holding_key") or ""):
            raise ValueError(f"Auswahlbild {index} besitzt keine normalisierte Haltung.")
        physical = _physical_holding_key(item.get("holding_key"))
        if item.get("physical_holding_key") != physical:
            raise ValueError(f"Auswahlbild {index} besitzt eine falsche physische Haltung.")
        if physical in seen_physical_holdings:
            raise ValueError("Die Pruefliste enthaelt mehrere Bilder derselben Haltung.")
        seen_physical_holdings.add(physical)
        if (
            _require_sha256(
                item.get("image_sha256"),
                f"Bild-SHA von Auswahlbild {index}",
            )
            != verified.sha256
            or _required_integer(
                item.get("size_bytes"),
                f"Bildgroesse von Auswahlbild {index}",
            )
            != verified.size_bytes
            or str(item.get("image_format") or "").casefold()
            != verified.path.suffix.casefold().lstrip(".")
        ):
            raise ValueError(f"Auswahlbild {index} stimmt nicht mit den Bildbytes.")
        _require_sha256(item.get("source_ref"), f"Quellen-ID von Auswahlbild {index}")
        try:
            datetime.strptime(
                str(item.get("inspection_date") or ""),
                "%Y-%m-%d",
            )
        except ValueError as error:
            raise ValueError(
                f"Auswahlbild {index} besitzt kein gueltiges Inspektionsdatum."
            ) from error
        predictions = item.get("predictions")
        if not isinstance(predictions, list) or {
            str(prediction.get("model_id") or "")
            for prediction in predictions
            if isinstance(prediction, dict)
        } != model_ids:
            raise ValueError(f"Auswahlbild {index} besitzt unvollstaendige Modellbelege.")
        if not any(
            isinstance(prediction, dict)
            and prediction.get("predicted_bcc") is True
            for prediction in predictions
        ):
            raise ValueError(f"Auswahlbild {index} ist kein Modell-Fehlalarmkandidat.")

    return (
        queue_id,
        hashlib.sha256(manifest_bytes).hexdigest(),
        hashlib.sha256(candidates_bytes).hexdigest(),
        tuple(verified_images),
    )


class BccHardNegativeReviewStore(BccReleaseHoldoutReviewStore):
    """Blinder Review-Speicher fuer moegliche klassenfreie Trainingsbilder."""

    review_purpose = HARD_NEGATIVE_REVIEW_PURPOSE
    valid_decisions = HARD_NEGATIVE_DECISIONS
    identity_field = "queue_id"
    manifest_binding_field = "queue_manifest_sha256"

    def _validate_source(
        self,
        source_root: Path,
    ) -> tuple[str, str, str, tuple[_VerifiedImage, ...]]:
        return _validate_hard_negative_queue(source_root)

    def html_template(self) -> str:
        return HARD_NEGATIVE_INDEX_HTML

    def _load_additional_review_bindings(self) -> dict[str, str]:
        manifest = _load_json_bytes(
            (self.holdout_root / "_manifest.json").read_bytes(),
            "Hard-Negative-Manifest",
        )
        if not isinstance(manifest, dict):
            raise ValueError("Das Hard-Negative-Manifest ist ungueltig.")
        return {
            "class_map_sha256": _require_sha256(
                manifest.get("class_map_sha256"),
                "Klassenkarten-SHA",
            )
        }


def _atomic_write_json(path: Path, document: object) -> None:
    safe_parent = _prepare_safe_output_parent(path)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.",
        suffix=".tmp",
        dir=safe_parent,
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(
                document,
                stream,
                ensure_ascii=False,
                indent=2,
                sort_keys=False,
            )
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        _prepare_safe_output_parent(path)
        if path.exists() and (
            not path.is_file() or _is_reparse_point(path)
        ):
            raise ValueError("Die vorhandene Review-Ausgabe ist unsicher.")
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def _load_json_bytes(payload: bytes, label: str) -> object:
    def reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
        result: dict[str, object] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"{label} enthaelt das Feld {key} doppelt.")
            result[key] = value
        return result

    try:
        return json.loads(
            payload.decode("utf-8-sig"),
            object_pairs_hook=reject_duplicate_keys,
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{label} ist kein gueltiges UTF-8-JSON.") from error


def _required_identifier(value: object, label: str) -> str:
    text = str(value or "").strip()
    if not _IDENTIFIER_PATTERN.fullmatch(text):
        raise ValueError(f"{label} besitzt keine sichere ID.")
    return text


def _require_sha256(value: object, label: str) -> str:
    text = str(value or "").strip().casefold()
    if not _SHA256_PATTERN.fullmatch(text):
        raise ValueError(f"{label} ist kein gueltiger SHA-256.")
    return text


def _required_integer(value: object, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise ValueError(f"{label} ist keine gueltige nichtnegative Ganzzahl.")
    return value


def _required_revision(value: object) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise ValueError("Revision muss eine nichtnegative Ganzzahl sein.")
    return value


def _required_text(value: object, label: str, maximum: int) -> str:
    text = unicodedata.normalize("NFC", str(value or "")).strip()
    if not text:
        raise ValueError(f"{label} ist erforderlich.")
    if len(text) > maximum:
        raise ValueError(f"{label} ist zu lang.")
    if any(ord(character) < 32 or ord(character) == 127 for character in text):
        raise ValueError(f"{label} enthaelt ungueltige Steuerzeichen.")
    return text


def _comment_text(value: object) -> str:
    text = unicodedata.normalize("NFC", str(value or "")).strip()
    if len(text) > MAX_COMMENT_CHARACTERS:
        raise ValueError("Kommentar ist zu lang.")
    if any(
        (ord(character) < 32 and character not in "\n\t")
        or ord(character) == 127
        for character in text
    ):
        raise ValueError("Kommentar enthaelt ungueltige Steuerzeichen.")
    return text


def _current_utc() -> str:
    return (
        datetime.now(timezone.utc)
        .isoformat(timespec="seconds")
        .replace("+00:00", "Z")
    )


def _path_is_within(path: Path, root: Path) -> bool:
    try:
        return os.path.commonpath(
            (os.path.normcase(str(path)), os.path.normcase(str(root)))
        ) == os.path.normcase(str(root))
    except ValueError:
        return False


@contextmanager
def _exclusive_review_output_lock(path: Path):
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
        or os.path.normcase(os.path.realpath(existing))
        != os.path.normcase(str(existing))
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


def _is_reparse_point(path: Path) -> bool:
    try:
        information = os.lstat(path)
    except OSError as error:
        raise ValueError("Ein Holdout-Pfad ist nicht sicher lesbar.") from error
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    attributes = getattr(information, "st_file_attributes", 0)
    return stat.S_ISLNK(information.st_mode) or bool(attributes & reparse_flag)


def _validate_image_signature(body: bytes, suffix: str) -> None:
    if len(body) < MIN_IMAGE_BYTES:
        raise ValueError("Ein Holdout-Bild ist zu klein oder beschaedigt.")
    normalized_suffix = suffix.casefold()
    if normalized_suffix in {".jpg", ".jpeg"}:
        valid = body.startswith(b"\xff\xd8\xff")
    elif normalized_suffix == ".png":
        valid = body.startswith(b"\x89PNG\r\n\x1a\n")
    else:
        valid = False
    if not valid:
        raise ValueError("Ein Holdout-Bild besitzt keine gueltige Bildsignatur.")


def _numeric_holding_pattern(value: object) -> bool:
    text = str(value or "").strip()
    match = re.search(r"\d[\d.]*[-/]\d[\d.]*", text)
    return match is not None


def _normalized_holding_key(value: object) -> str | None:
    text = str(value or "").strip()
    match = re.search(r"\d[\d.]*[-/]\d[\d.]*", text)
    if match is None:
        return None
    parts = re.split(r"[-/]", match.group(0), maxsplit=1)
    if len(parts) != 2:
        return None

    def strip_area_prefix(part: str) -> str:
        dot = part.rfind(".")
        return part[dot + 1 :] if 0 <= dot < len(part) - 1 else part

    left = strip_area_prefix(parts[0])
    right = strip_area_prefix(parts[1])
    return f"{left}-{right}" if left and right else None


def _physical_holding_key(value: object) -> str:
    normalized = _normalized_holding_key(value)
    if normalized is None:
        raise ValueError("Keine belastbare physische Haltung.")
    left, right = normalized.split("-", maxsplit=1)
    return "|".join(sorted((left.casefold(), right.casefold())))


_PROTO_ENDPOINT_PREFIX = re.compile(r"^\d{1,2}\.(.{4,})$")


def _proto_physical_holding_key(value: object) -> str:
    """Geschuetzte Normalisierung fuer Protokoll-Haltungen: Bereichspraefix
    (``NN.``) nur entfernen, wenn der Rest >= 4 Zeichen hat — sonst wuerden
    echte Knoten wie ``797.02`` zu ``02`` verschmolzen. Richtungsunabhaengig;
    Faellt ohne A-B-Form auf die Rohschreibweise zurueck."""
    text = str(value or "").strip()
    match = re.search(r"\d[\d.]*[-/]\d[\d.]*", text)
    if match is None:
        return text.casefold()
    left, right = re.split(r"[-/]", match.group(0), maxsplit=1)

    def guarded(part: str) -> str:
        prefix_match = _PROTO_ENDPOINT_PREFIX.match(part)
        return prefix_match.group(1) if prefix_match else part

    return "|".join(sorted((guarded(left).casefold(), guarded(right).casefold())))


def _image_content_type(suffix: str) -> str:
    if suffix.casefold() in {".jpg", ".jpeg"}:
        return "image/jpeg"
    if suffix.casefold() == ".png":
        return "image/png"
    return mimetypes.types_map.get(suffix.casefold(), "application/octet-stream")


def make_handler(store: BccReleaseHoldoutReviewStore):
    html = store.html_template().replace("__REVIEWER__", escape(store.reviewer))

    class BccReleaseHoldoutReviewHandler(BaseHTTPRequestHandler):
        server_version = "SewerStudioBccBlindReview/1.0"

        def do_GET(self) -> None:  # noqa: N802
            if not self._has_loopback_host():
                self._send_json({"error": "Ungueltiger Host."}, status=421)
                return
            parsed = urlparse(self.path)
            if parsed.path == "/":
                self._send_html(html)
                return
            if parsed.path == "/api/state":
                self._send_json(store.state())
                return
            if parsed.path == "/image":
                values = parse_qs(
                    parsed.query,
                    keep_blank_values=True,
                ).get("id", [])
                if len(values) != 1:
                    self._send_json({"error": "Unbekannte Bild-ID."}, status=404)
                    return
                try:
                    body, content_type = store.image_bytes_for(values[0])
                    self._send_bytes(body, content_type)
                except (KeyError, ValueError):
                    self._send_json({"error": "Unbekannte Bild-ID."}, status=404)
                return
            self._send_json({"error": "Nicht gefunden."}, status=404)

        def do_POST(self) -> None:  # noqa: N802
            if not self._has_loopback_host():
                self._send_json({"error": "Ungueltiger Host."}, status=421)
                return
            if urlparse(self.path).path != "/api/review":
                self._send_json({"error": "Nicht gefunden."}, status=404)
                return
            try:
                content_type = self.headers.get("Content-Type", "")
                if content_type.split(";", 1)[0].strip().casefold() != (
                    "application/json"
                ):
                    self._send_json(
                        {"error": "Content-Type muss application/json sein."},
                        status=415,
                    )
                    return
                raw_length = self.headers.get("Content-Length")
                if raw_length is None:
                    self._send_json(
                        {"error": "Content-Length fehlt."},
                        status=411,
                    )
                    return
                try:
                    length = int(raw_length)
                except ValueError:
                    self._send_json(
                        {"error": "Content-Length ist ungueltig."},
                        status=400,
                    )
                    return
                if length < 0:
                    self._send_json(
                        {"error": "Content-Length ist ungueltig."},
                        status=400,
                    )
                    return
                if length > MAX_REQUEST_BODY_BYTES:
                    self.close_connection = True
                    self._send_json(
                        {"error": "Anfrage ist zu gross."},
                        status=413,
                    )
                    return
                payload = _load_json_bytes(
                    self.rfile.read(length),
                    "Review-Anfrage",
                )
                if not isinstance(payload, dict):
                    raise ValueError("Review-Anfrage muss ein JSON-Objekt sein.")
                if set(payload) != {"id", "decision", "comment", "revision"}:
                    raise ValueError(
                        "Review-Anfrage hat fremde oder fehlende Felder."
                    )
                state = store.set_decision(
                    payload.get("id"),
                    payload.get("decision"),
                    payload.get("comment", ""),
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
                self._send_json(
                    {"error": "Review konnte nicht gespeichert werden."},
                    status=500,
                )

        def _send_html(self, text: str) -> None:
            self._send_bytes(
                text.encode("utf-8"),
                "text/html; charset=utf-8",
            )

        def _has_loopback_host(self) -> bool:
            host = self.headers.get("Host", "").strip()
            if host == "127.0.0.1":
                return True
            prefix = "127.0.0.1:"
            port_text = host[len(prefix) :] if host.startswith(prefix) else ""
            return port_text.isascii() and port_text.isdigit() and (
                0 <= int(port_text) <= 65535
            )

        def _send_json(self, data: object, status: int = 200) -> None:
            body = json.dumps(data, ensure_ascii=False).encode("utf-8")
            self._send_bytes(
                body,
                "application/json; charset=utf-8",
                status=status,
            )

        def _send_bytes(
            self,
            body: bytes,
            content_type: str,
            status: int = 200,
        ) -> None:
            self.send_response(status)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("Referrer-Policy", "no-referrer")
            self.send_header(
                "Content-Security-Policy",
                "default-src 'self'; img-src 'self'; "
                "style-src 'self' 'unsafe-inline'; "
                "script-src 'self' 'unsafe-inline'; "
                "connect-src 'self'; base-uri 'none'; frame-ancestors 'none'",
            )
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, format: str, *args: object) -> None:
            return

    return BccReleaseHoldoutReviewHandler


def create_server(
    store: BccReleaseHoldoutReviewStore,
    port: int = 0,
) -> ThreadingHTTPServer:
    if isinstance(port, bool) or not isinstance(port, int) or not 0 <= port <= 65535:
        raise ValueError("Port muss zwischen 0 und 65535 liegen.")
    return ThreadingHTTPServer(
        ("127.0.0.1", port),
        make_handler(store),
    )


INDEX_HTML = r"""<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>BCC Blind-Review</title>
  <style>
    :root { color-scheme: dark; font-family: system-ui, sans-serif; }
    body { margin: 0; background: #111827; color: #f3f4f6; }
    main { width: min(1100px, calc(100% - 32px)); margin: 18px auto; }
    header, .card { background: #1f2937; border: 1px solid #374151;
      border-radius: 12px; padding: 16px; }
    header { display: flex; gap: 16px; justify-content: space-between;
      align-items: center; margin-bottom: 16px; }
    h1 { margin: 0; font-size: 1.25rem; }
    .muted { color: #9ca3af; }
    .layout { display: grid; grid-template-columns: minmax(0, 1fr) 280px;
      gap: 16px; }
    .target-code { margin-bottom: 14px; padding: 11px; background: #111827;
      border: 1px solid #4b5563; border-radius: 8px; }
    .target-code strong { display: block; margin-top: 3px; color: #93c5fd;
      font-size: 1.1rem; }
    img { display: block; width: 100%; max-height: 68vh; object-fit: contain;
      background: #030712; border-radius: 8px; }
    textarea { width: 100%; min-height: 88px; box-sizing: border-box;
      margin-top: 12px; padding: 10px; color: inherit; background: #111827;
      border: 1px solid #4b5563; border-radius: 8px; resize: vertical; }
    button { width: 100%; margin-top: 9px; padding: 11px; border: 0;
      border-radius: 8px; cursor: pointer; font-weight: 700; }
    .positive { background: #10b981; color: #052e20; }
    .negative { background: #ef4444; color: #fff; }
    .exclude { background: #f59e0b; color: #3b2100; }
    .navigation { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
    .navigation button { background: #4b5563; color: #fff; }
    .active { outline: 3px solid #fff; }
    #status { min-height: 1.5em; margin-top: 12px; color: #fbbf24; }
    @media (max-width: 760px) { .layout { grid-template-columns: 1fr; } }
  </style>
</head>
<body>
<main>
  <header>
    <div>
      <h1>BCC Release-Holdout · Blind-Review</h1>
      <div class="muted">Reviewer: __REVIEWER__</div>
    </div>
    <div id="progress">Lade …</div>
  </header>
  <section class="layout">
    <div class="card">
      <img id="image" alt="Zu prüfendes Kanalbild">
    </div>
    <aside class="card">
      <div class="target-code">
        <div class="muted">Prüfcode</div>
        <strong id="targetCode">BCC — Bogen</strong>
      </div>
      <div class="muted">Bild-ID</div>
      <div id="caseId">–</div>
      <textarea id="comment" maxlength="2000"
        placeholder="Optionaler Kommentar"></textarea>
      <button class="positive" data-decision="positive"
        onclick='saveDecision("positive")'>1 · BCC vorhanden</button>
      <button class="negative" data-decision="negative"
        onclick='saveDecision("negative")'>2 · Kein BCC</button>
      <button class="exclude" data-decision="exclude"
        onclick='saveDecision("exclude")'>3 · Ausschliessen</button>
      <div class="navigation">
        <button onclick="move(-1)">← Vorheriges</button>
        <button onclick="move(1)">Nächstes →</button>
      </div>
      <div id="status"></div>
      <p class="muted">Tasten: 1 / 2 / 3, Pfeil links / rechts</p>
    </aside>
  </section>
</main>
<script>
  let reviewState = null;
  let currentIndex = 0;

  async function loadState(preferredId = null) {
    const response = await fetch("/api/state", {cache: "no-store"});
    if (!response.ok) throw new Error("Status konnte nicht geladen werden.");
    reviewState = await response.json();
    if (preferredId) {
      const found = reviewState.items.findIndex(item => item.id === preferredId);
      if (found >= 0) currentIndex = found;
    } else if (reviewState.current) {
      const found = reviewState.items.findIndex(
        item => item.id === reviewState.current.id
      );
      if (found >= 0) currentIndex = found;
    }
    render();
  }

  function render() {
    const item = reviewState.items[currentIndex];
    document.getElementById("progress").textContent =
      `${reviewState.done} / ${reviewState.total} geprüft`;
    if (!item) {
      document.getElementById("caseId").textContent = "Keine Bilder";
      document.getElementById("image").removeAttribute("src");
      return;
    }
    document.getElementById("caseId").textContent = item.id;
    document.getElementById("image").src = item.image_url;
    document.getElementById("comment").value = item.comment || "";
    document.querySelectorAll("[data-decision]").forEach(button => {
      button.classList.toggle("active", button.dataset.decision === item.decision);
    });
  }

  async function saveDecision(decision) {
    const item = reviewState.items[currentIndex];
    if (!item) return;
    const status = document.getElementById("status");
    status.textContent = "Speichere …";
    const response = await fetch("/api/review", {
      method: "POST",
      headers: {"Content-Type": "application/json"},
      body: JSON.stringify({
        id: item.id,
        decision: decision,
        comment: document.getElementById("comment").value,
        revision: reviewState.revision
      })
    });
    const result = await response.json();
    if (!response.ok) {
      status.textContent = result.error || "Speichern fehlgeschlagen.";
      return;
    }
    reviewState = result;
    status.textContent = "Gespeichert.";
    if (currentIndex < reviewState.items.length - 1) currentIndex += 1;
    render();
  }

  function move(offset) {
    if (!reviewState || !reviewState.items.length) return;
    currentIndex = Math.max(
      0,
      Math.min(reviewState.items.length - 1, currentIndex + offset)
    );
    document.getElementById("status").textContent = "";
    render();
  }

  document.addEventListener("keydown", event => {
    if (event.target && event.target.tagName === "TEXTAREA") return;
    switch (event.key) {
      case "1":
        saveDecision("positive");
        break;
      case "2":
        saveDecision("negative");
        break;
      case "3":
        saveDecision("exclude");
        break;
      case "ArrowLeft":
        move(-1);
        break;
      case "ArrowRight":
        move(1);
        break;
      default:
        return;
    }
    event.preventDefault();
  });

  loadState().catch(error => {
    document.getElementById("status").textContent = error.message;
  });
</script>
</body>
</html>
"""


HARD_NEGATIVE_INDEX_HTML = r"""<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>BCC Hard-Negative-Prüfung</title>
  <style>
    :root { color-scheme: dark; font-family: system-ui, sans-serif; }
    body { margin: 0; background: #111827; color: #f3f4f6; }
    main { width: min(1320px, calc(100% - 32px)); margin: 14px auto; }
    header, .card { background: #1f2937; border: 1px solid #374151;
      border-radius: 12px; padding: 14px; }
    header { display: flex; gap: 16px; justify-content: space-between;
      align-items: center; margin-bottom: 12px; }
    h1 { margin: 0; font-size: 1.2rem; }
    .muted { color: #aeb8c8; }
    #progress { font-size: 1.15rem; font-weight: 700; }
    .warning { margin: 0 0 12px; padding: 11px 13px; border: 1px solid #f59e0b;
      border-radius: 8px; color: #fde68a; background: #2a2112; font-size: 1rem; }
    .warning b { color: #fff; }
    .layout { display: grid; grid-template-columns: minmax(0, 1fr) 360px;
      gap: 14px; align-items: start; }
    img { display: block; width: 100%; max-height: 60vh; object-fit: contain;
      background: #030712; border-radius: 8px; }
    textarea { width: 100%; min-height: 52px; box-sizing: border-box;
      margin-top: 10px; padding: 8px; color: inherit; background: #111827;
      border: 1px solid #4b5563; border-radius: 8px; resize: vertical;
      font-size: 0.9rem; }
    button { width: 100%; margin-top: 9px; padding: 13px 11px; border: 0;
      border-radius: 8px; cursor: pointer; font-weight: 700; font-size: 1rem;
      text-align: left; line-height: 1.3; }
    button .sub { display: block; font-weight: 400; font-size: 0.82rem;
      opacity: 0.85; margin-top: 2px; }
    .clear { background: #10b981; color: #052e20; }
    .bcc { background: #ef4444; color: #fff; }
    .other { background: #8b5cf6; color: #fff; }
    .exclude { background: #f59e0b; color: #3b2100; }
    .navigation { display: grid; grid-template-columns: 1fr 1fr; gap: 8px;
      margin-top: 10px; }
    .navigation button { background: #4b5563; color: #fff; text-align: center;
      font-size: 0.9rem; padding: 9px; margin-top: 0; }
    .active { outline: 3px solid #fff; }
    #status { min-height: 1.4em; margin-top: 10px; color: #fbbf24; }
    .liste { margin-top: 12px; display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px; }
    .liste h3 { margin: 0 0 6px; font-size: 0.9rem; color: #93c5fd;
      text-transform: none; }
    .liste ul { margin: 0; padding-left: 16px; font-size: 0.86rem;
      line-height: 1.45; }
    .liste li b { color: #fff; }
    .hinweis { color: #34d399; font-weight: 700; }
    .egal { margin-top: 10px; padding: 9px 12px; border-radius: 8px;
      background: #172033; border: 1px solid #334155; font-size: 0.86rem;
      color: #cbd5e1; }
    .egal b { color: #fff; }
    @media (max-width: 980px) {
      .layout { grid-template-columns: 1fr; }
      .liste { grid-template-columns: 1fr; }
    }
  </style>
</head>
<body>
<main>
  <header>
    <div>
      <h1>Negativ-Prüfung · blind</h1>
      <div class="muted">Prüfer: __REVIEWER__ &nbsp;·&nbsp; Tasten 1 / 2 / 3, Pfeil links / rechts</div>
    </div>
    <div id="progress">Lade …</div>
  </header>
  <p class="warning">
    Die Frage lautet nur: <b>Ist eine der 15 trainierten Klassen im Bild zu sehen?</b>
    Alle 15 stehen unter dem Bild.<br>
    Achtung — <b>ein sauberer Anschluss und ein normaler Bogen zählen auch.</b>
    „Nichts davon sichtbar“ heisst: keine Öffnung, kein Bogen, kein Schaden.
  </p>
  <section class="layout">
    <div class="card">
      <img id="image" alt="Zu prüfendes Kanalbild">
      <div class="liste">
        <div>
          <h3>Schäden am Rohr</h3>
          <ul>
            <li><b>BAA</b> Verformung, eingedrückt</li>
            <li><b>BAB</b> Riss</li>
            <li><b>BAC</b> Bruch, Einsturz, Loch</li>
            <li><b>BAF</b> Oberfläche rau, angegriffen</li>
            <li><b>BAI</b> Dichtung ragt herein</li>
            <li><b>BAJ</b> Verbindung versetzt, klaffend</li>
          </ul>
        </div>
        <div>
          <h3>Betriebliche Störungen</h3>
          <ul>
            <li><b>BBA</b> Wurzeln</li>
            <li><b>BBB</b> Anhaftungen, Fett, Inkrustation</li>
            <li><b>BBC</b> Ablagerung, Sand, Kies</li>
            <li><b>BBD</b> Eindringender Boden</li>
            <li><b>BBF</b> Infiltration, eindringendes Wasser</li>
          </ul>
        </div>
        <div>
          <h3>Anschluss und Bogen</h3>
          <ul>
            <li><b>BCA</b> Seitlicher Anschluss
              <span class="hinweis">— auch wenn intakt!</span></li>
            <li><b>BCC</b> Bogen
              <span class="hinweis">— auch wenn normal!</span></li>
            <li><b>BAH</b> Schadhafter Anschluss</li>
            <li><b>SONST</b> sonstiger Schaden</li>
          </ul>
        </div>
      </div>
      <div class="egal">
        <b>Stört nicht — darf im Bild sein:</b> Rohranfang, Rohrende,
        Wasserspiegel, Rohrprofil- oder Materialwechsel, Schacht,
        der eingeblendete Text.
      </div>
    </div>
    <aside class="card">
      <button class="clear" data-decision="all_classes_clear"
        onclick='saveDecision("all_classes_clear")'>
        1 · Nichts davon sichtbar
        <span class="sub">Nur Rohrwand — wird Trainingsnegativ</span>
      </button>
      <button class="other" data-decision="mapped_object_visible"
        onclick='saveDecision("mapped_object_visible")'>
        2 · Etwas aus der Liste sichtbar
        <span class="sub">Auch bei intaktem Anschluss oder Bogen</span>
      </button>
      <button class="exclude" data-decision="exclude_uncertain"
        onclick='saveDecision("exclude_uncertain")'>
        3 · Unklar — ausschliessen
        <span class="sub">Unscharf, zu dunkel, nicht beurteilbar</span>
      </button>
      <div class="navigation">
        <button onclick="move(-1)">← Zurück</button>
        <button onclick="move(1)">Weiter →</button>
      </div>
      <div id="status"></div>
      <textarea id="comment" maxlength="2000"
        placeholder="Kommentar (optional)"></textarea>
      <p class="muted" style="margin-bottom:0">Bild-ID</p>
      <div id="caseId" class="muted" style="font-size:0.8rem">–</div>
    </aside>
  </section>
</main>
<script>
  let reviewState = null;
  let currentIndex = 0;

  async function loadState(preferredId = null) {
    const response = await fetch("/api/state", {cache: "no-store"});
    if (!response.ok) throw new Error("Status konnte nicht geladen werden.");
    reviewState = await response.json();
    if (preferredId) {
      const found = reviewState.items.findIndex(item => item.id === preferredId);
      if (found >= 0) currentIndex = found;
    } else if (reviewState.current) {
      const found = reviewState.items.findIndex(
        item => item.id === reviewState.current.id
      );
      if (found >= 0) currentIndex = found;
    }
    render();
  }

  function render() {
    const item = reviewState.items[currentIndex];
    document.getElementById("progress").textContent =
      `${reviewState.done} / ${reviewState.total} geprüft`;
    if (!item) {
      document.getElementById("caseId").textContent = "Keine Bilder";
      document.getElementById("image").removeAttribute("src");
      return;
    }
    document.getElementById("caseId").textContent = item.id;
    document.getElementById("image").src = item.image_url;
    document.getElementById("comment").value = item.comment || "";
    document.querySelectorAll("[data-decision]").forEach(button => {
      button.classList.toggle("active", button.dataset.decision === item.decision);
    });
  }

  async function saveDecision(decision) {
    const item = reviewState.items[currentIndex];
    if (!item) return;
    const status = document.getElementById("status");
    status.textContent = "Speichere …";
    const response = await fetch("/api/review", {
      method: "POST",
      headers: {"Content-Type": "application/json"},
      body: JSON.stringify({
        id: item.id,
        decision: decision,
        comment: document.getElementById("comment").value,
        revision: reviewState.revision
      })
    });
    const result = await response.json();
    if (!response.ok) {
      status.textContent = result.error || "Speichern fehlgeschlagen.";
      return;
    }
    reviewState = result;
    status.textContent = "Gespeichert.";
    if (currentIndex < reviewState.items.length - 1) currentIndex += 1;
    render();
  }

  function move(offset) {
    if (!reviewState || !reviewState.items.length) return;
    currentIndex = Math.max(
      0,
      Math.min(reviewState.items.length - 1, currentIndex + offset)
    );
    document.getElementById("status").textContent = "";
    render();
  }

  document.addEventListener("keydown", event => {
    if (event.target && event.target.tagName === "TEXTAREA") return;
    const decisions = {
      "1": "all_classes_clear",
      "2": "mapped_object_visible",
      "3": "exclude_uncertain"
    };
    if (decisions[event.key]) {
      saveDecision(decisions[event.key]);
    } else if (event.key === "ArrowLeft") {
      move(-1);
    } else if (event.key === "ArrowRight") {
      move(1);
    } else {
      return;
    }
    event.preventDefault();
  });

  loadState().catch(error => {
    document.getElementById("status").textContent = error.message;
  });
</script>
</body>
</html>
"""


def run_server(
    holdout_root: Path,
    output_path: Path,
    reviewer: str,
    port: int = 8773,
) -> None:
    store = BccReleaseHoldoutReviewStore(
        holdout_root,
        output_path,
        reviewer,
    )
    state = store.prepare_output()
    server = create_server(store, port)
    actual_port = server.server_address[1]
    print(f"BCC Blind-Review: http://127.0.0.1:{actual_port}/")
    print(f"Bilder: {state['total']}; offen: {state['open']}")
    print(f"Review-Ausgabe: {store.output_path}")
    print("Stoppen mit Strg+C")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Lokaler blinder BCC-Release-Holdout-Review"
    )
    parser.add_argument("--holdout", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--reviewer", required=True)
    parser.add_argument("--port", type=int, default=8773)
    parser.add_argument(
        "--prepare-only",
        action="store_true",
        help="Review-Datei vorbereiten, aber keinen HTTP-Server starten.",
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    if args.prepare_only:
        store = BccReleaseHoldoutReviewStore(
            args.holdout,
            args.output,
            args.reviewer,
        )
        state = store.prepare_output()
        print(f"Review vorbereitet: {store.output_path}")
        print(f"Bilder: {state['total']}; offen: {state['open']}")
        return 0
    run_server(
        args.holdout,
        args.output,
        args.reviewer,
        args.port,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
