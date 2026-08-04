"""Bereitet ein streng freigegebenes Mehrklassen-Detect-Register vor.

Das Werkzeug exportiert und trainiert nichts. Es verbindet einen aktuellen
Goldbestands-Audit mit der aktiven Detect-Klassenkarte, der fachlich geprueften
v3-Migration und einem ausschliesslich als ``all_classes_clear`` bestaetigten
Negativsatz. Ohne ``--execute --renew-existing`` bleibt der Lauf schreibfrei.
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import stat
import sys
import uuid
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, Sequence


SCRIPT_ROOT = Path(__file__).resolve().parent
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

import gold_stock_audit as gold_audit_tools


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
ACTIVE_CLASS_MAP_PATH = (
    REPOSITORY_ROOT / "training" / "class_maps" / "detect_class_map_v3.json"
)
ACTIVE_MIGRATION_PATH = (
    REPOSITORY_ROOT
    / "training"
    / "class_maps"
    / "detect_class_migration_v3.candidate.json"
)
ACTIVE_VSA_MANIFEST_PATH = (
    REPOSITORY_ROOT
    / "src"
    / "AuswertungPro.Next.UI"
    / "Data"
    / "vsa_kek_2020_catalog_manifest.json"
)

GOLD_AUDIT_SCHEMA_VERSION = "1.1"
GOLD_AUDIT_REPORT_NAME = "gold_stock_audit"
GOLD_AUDIT_MODE = "schreibfreie_pruefung"
CLASS_MAP_VERSION = 3
MIGRATION_VERSION = 3
REGISTRY_SCHEMA_VERSION = "1.0"
RECEIPT_SCHEMA_VERSION = "1.0"
RECEIPT_PURPOSE = "detect_all_registry_preparation"
TRANSACTION_SCHEMA_VERSION = "1.0"
TRANSACTION_PURPOSE = "detect_all_registry_receipt_transaction"
TRANSACTION_FILE_NAME = ".registry_setup_v1.transaction.json"
NEGATIVE_REGISTRY_MODE = "streng_reviewte_saetze"
MIN_NEGATIVE_BYTES = 1024
GOLD_SPLIT_SALT = "split-v1"
GOLD_TRAIN_SHARE = 0.70
GOLD_VALIDATION_SHARE = 0.15
ALLOWED_AUDIT_ROLES = {"train", "val", "test"}
ALLOWED_MIGRATION_ACTIONS = {"map", "discard", "review"}
ALLOWED_MIGRATION_STATUSES = {"approved", "pending"}
REQUIRED_REGISTRY_FIELDS = {
    "schema_version",
    "approval_status",
    "approved_by",
    "approved_utc",
    "approved_sample_ids",
    "holding_roles",
    "protected_sets",
}


@dataclass(frozen=True)
class DetectSample:
    sample_id: str
    case_id: str
    holding_key: str
    physical_holding_key: str
    code: str
    target_class: str
    frame_path: Path
    image_sha256: str
    confirmed_at_utc: str
    source_type: str
    role: str
    group_key: str


@dataclass(frozen=True)
class DetectPreparation:
    registry_path: Path
    receipt_path: Path
    approved_by: str
    source_audit_path: Path
    source_audit_sha256: str
    source_samples_path: Path
    source_samples_sha256: str
    expected_existing_registry_sha256: str
    expected_existing_receipt_sha256: str | None
    class_map_path: Path
    class_map_sha256: str
    class_map_version: int
    vsa_manifest_sha256: str
    migration_path: Path
    migration_sha256: str
    migration_version: int
    personal_gold_approved_utc: str
    personal_gold_source_codes: tuple[str, ...]
    selected_samples: tuple[DetectSample, ...]
    discarded_sample_ids: tuple[str, ...]
    excluded_test_sample_ids: tuple[str, ...]
    protected_sets: tuple[dict[str, str], ...]
    negatives_dir: Path
    negative_set_roots: tuple[Path, ...]
    negative_sets: tuple[dict[str, Any], ...]
    negative_images: tuple[dict[str, Any], ...]

    @property
    def train_images(self) -> int:
        return sum(sample.role == "train" for sample in self.selected_samples)

    @property
    def validation_images(self) -> int:
        return sum(sample.role == "val" for sample in self.selected_samples)


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _strict_json_bytes(data: bytes, label: str) -> Any:
    def reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"{label} enthaelt ein doppeltes Feld: {key}")
            result[key] = value
        return result

    try:
        return json.loads(
            data.decode("utf-8-sig"),
            object_pairs_hook=reject_duplicates,
        )
    except (UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{label} ist kein sicher lesbares JSON.") from error


def _load_json_object_with_bytes(
    path: Path,
    label: str,
) -> tuple[dict[str, Any], bytes]:
    if not path.is_file():
        raise ValueError(f"{label} fehlt: {path}")
    data = path.read_bytes()
    value = _strict_json_bytes(data, label)
    if not isinstance(value, dict):
        raise ValueError(f"{label} muss ein JSON-Objekt enthalten.")
    if path.read_bytes() != data:
        raise ValueError(f"{label} wurde waehrend des Einlesens geaendert.")
    return value, data


def _load_json_array_with_bytes(
    path: Path,
    label: str,
) -> tuple[list[dict[str, Any]], bytes]:
    if not path.is_file():
        raise ValueError(f"{label} fehlt: {path}")
    data = path.read_bytes()
    value = _strict_json_bytes(data, label)
    if not isinstance(value, list) or any(not isinstance(item, dict) for item in value):
        raise ValueError(f"{label} muss ein Array aus JSON-Objekten enthalten.")
    if path.read_bytes() != data:
        raise ValueError(f"{label} wurde waehrend des Einlesens geaendert.")
    return value, data


def _require_exact_fields(
    value: Any,
    expected: set[str],
    label: str,
) -> Mapping[str, Any]:
    if not isinstance(value, dict) or set(value) != expected:
        raise ValueError(f"{label} hat fehlende oder fremde Felder.")
    return value


def _require_sha256(value: Any, label: str) -> str:
    text = str(value or "").strip().casefold()
    if len(text) != 64 or any(character not in "0123456789abcdef" for character in text):
        raise ValueError(f"{label} ist kein gueltiger SHA-256.")
    return text


def _require_nonnegative_int(value: Any, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise ValueError(f"{label} ist keine gueltige Anzahl.")
    return value


def _require_utc(value: Any, label: str) -> str:
    text = str(value or "").strip()
    if not text:
        raise ValueError(f"{label} fehlt.")
    try:
        parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError(f"{label} ist kein gueltiger ISO-Zeitpunkt.") from error
    if parsed.tzinfo is None or parsed.utcoffset() != timezone.utc.utcoffset(parsed):
        raise ValueError(f"{label} muss in UTC angegeben sein.")
    return text


def _paths_equal(left: Path, right: Path) -> bool:
    return os.path.normcase(str(left.resolve())) == os.path.normcase(
        str(right.resolve())
    )


def _is_within(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def _is_reparse_or_symlink(path: Path) -> bool:
    try:
        metadata = path.lstat()
    except OSError:
        return False
    attributes = getattr(metadata, "st_file_attributes", 0)
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return path.is_symlink() or bool(attributes & reparse_flag)


def _require_safe_storage_path(path: Path, root: Path, label: str) -> Path:
    absolute_root = Path(os.path.abspath(root))
    absolute_path = Path(os.path.abspath(path))
    if (
        not absolute_root.is_dir()
        or _is_reparse_or_symlink(absolute_root)
        or not _is_within(absolute_path, absolute_root)
    ):
        raise ValueError(
            f"{label} liegt nicht in einem sicheren Knowledge-Root: {absolute_path}"
        )
    current = absolute_root
    for part in absolute_path.relative_to(absolute_root).parts:
        current = current / part
        try:
            current.lstat()
        except FileNotFoundError:
            continue
        except OSError as error:
            raise ValueError(
                f"{label} ist nicht sicher pruefbar: {current}"
            ) from error
        if _is_reparse_or_symlink(current):
            raise ValueError(
                f"{label} darf keinen Link oder Junction verwenden: {current}"
            )
    return absolute_path


def _require_plain_path_below(path: Path, root: Path, label: str) -> Path:
    try:
        resolved_root = root.resolve(strict=True)
        resolved_path = path.resolve(strict=True)
    except OSError as error:
        raise ValueError(f"{label} ist nicht sicher aufloesbar: {path}") from error
    if not _is_within(resolved_path, resolved_root):
        raise ValueError(f"{label} liegt nicht im Goldroot: {path}")
    if _is_reparse_or_symlink(resolved_root) or _is_reparse_or_symlink(path):
        raise ValueError(f"{label} darf keinen Link oder Junction verwenden: {path}")
    current = path
    while True:
        if _is_reparse_or_symlink(current):
            raise ValueError(
                f"{label} darf keinen Link oder Junction verwenden: {current}"
            )
        try:
            if current.resolve(strict=True) == resolved_root:
                break
        except OSError as error:
            raise ValueError(f"{label} ist nicht sicher aufloesbar: {current}") from error
        parent = current.parent
        if parent == current:
            raise ValueError(f"{label} liegt nicht im Goldroot: {path}")
        current = parent
    return resolved_path


def _expected_split_role(group_key: str) -> str:
    digest = hashlib.sha256(
        f"{GOLD_SPLIT_SALT}|{group_key}".encode("utf-8")
    ).digest()
    value = int.from_bytes(digest[:8], "big") / float(1 << 64)
    if value < GOLD_TRAIN_SHARE:
        return "train"
    if value < GOLD_TRAIN_SHARE + GOLD_VALIDATION_SHARE:
        return "val"
    return "test"


def _physical_holding_key(holding_key: str) -> str:
    normalized = gold_audit_tools.normalize_holding_key(holding_key)
    if normalized is None or normalized != holding_key:
        raise ValueError(
            f"Keine normalisierte Haltungsidentitaet vorhanden: {holding_key}"
        )
    parts = normalized.split("-", maxsplit=1)
    if len(parts) != 2 or not all(parts):
        raise ValueError(f"Keine belastbare Haltungsidentitaet: {holding_key}")
    return "|".join(sorted((parts[0].casefold(), parts[1].casefold())))


def _read_active_class_map() -> tuple[
    dict[str, int],
    bytes,
    str,
    str,
]:
    path = ACTIVE_CLASS_MAP_PATH.resolve()
    document, data = _load_json_object_with_bytes(path, "Aktive Detect-Klassenkarte")
    _require_exact_fields(
        document,
        {"version", "vsa_manifest_hash", "classes"},
        "Aktive Detect-Klassenkarte",
    )
    version = document.get("version")
    if isinstance(version, bool) or version != CLASS_MAP_VERSION:
        raise ValueError("Die aktive Detect-Klassenkarte muss Version 3 sein.")
    raw_classes = document.get("classes")
    if not isinstance(raw_classes, dict) or len(raw_classes) != 15:
        raise ValueError("Die aktive Detect-Klassenkarte muss exakt 15 Klassen haben.")
    classes: dict[str, int] = {}
    ids: set[int] = set()
    for raw_name, raw_id in raw_classes.items():
        name = str(raw_name or "").strip()
        if (
            not name
            or isinstance(raw_id, bool)
            or not isinstance(raw_id, int)
            or raw_id in ids
        ):
            raise ValueError("Die aktive Detect-Klassenkarte enthaelt ungueltige Klassen.")
        classes[name] = raw_id
        ids.add(raw_id)
    if ids != set(range(len(classes))):
        raise ValueError("Die Detect-Klassen-IDs muessen lueckenlos bei 0 beginnen.")

    vsa_hash = _require_sha256(
        document.get("vsa_manifest_hash"),
        "VSA-Manifest-Hash in der Klassenkarte",
    )
    if (
        not ACTIVE_VSA_MANIFEST_PATH.is_file()
        or _sha256_file(ACTIVE_VSA_MANIFEST_PATH) != vsa_hash
    ):
        raise ValueError(
            "Die aktive Detect-Klassenkarte passt nicht zum aktiven VSA-Manifest."
        )
    if path.read_bytes() != data:
        raise ValueError("Die aktive Detect-Klassenkarte wurde parallel geaendert.")
    return classes, data, _sha256_bytes(data), vsa_hash


def _read_active_migration(
    classes: Mapping[str, int],
    vsa_manifest_sha256: str,
    approved_by: str,
    gold_audit_sha256: str,
    training_samples_sha256: str,
    audit_codes: set[str],
) -> tuple[dict[str, Mapping[str, Any]], bytes, str, str, tuple[str, ...]]:
    path = ACTIVE_MIGRATION_PATH.resolve()
    document, data = _load_json_object_with_bytes(path, "Aktive v3-Migration")
    if "personal_gold_approval" not in document:
        raise ValueError(
            "Die aktive v3-Migration besitzt keine personal_gold_approval."
        )
    _require_exact_fields(
        document,
        {
            "version",
            "target_class_map_version",
            "target_class_map",
            "generated_utc",
            "vsa_manifest_hash",
            "source_hashes",
            "sort_order",
            "resolution_order",
            "entry_counts",
            "entries",
            "personal_gold_approval",
        },
        "Aktive v3-Migration",
    )
    if (
        document.get("version") != MIGRATION_VERSION
        or document.get("target_class_map_version") != CLASS_MAP_VERSION
        or document.get("target_class_map") != ACTIVE_CLASS_MAP_PATH.name
        or document.get("vsa_manifest_hash") != vsa_manifest_sha256
    ):
        raise ValueError(
            "Die aktive v3-Migration passt nicht zur aktiven Detect-Klassenkarte."
        )
    _require_utc(document.get("generated_utc"), "Erstellzeit der v3-Migration")
    if not isinstance(document.get("source_hashes"), dict):
        raise ValueError("Die aktive v3-Migration besitzt keine Quellen-Hashes.")
    if not isinstance(document.get("sort_order"), list) or not isinstance(
        document.get("resolution_order"),
        list,
    ):
        raise ValueError("Die aktive v3-Migration besitzt keine feste Reihenfolge.")

    personal_approval = _require_exact_fields(
        document.get("personal_gold_approval"),
        {
            "schema_version",
            "gold_audit_sha256",
            "training_samples_sha256",
            "approved_by",
            "approved_utc",
            "source_codes",
        },
        "personal_gold_approval",
    )
    if personal_approval.get("schema_version") != "1.0":
        raise ValueError(
            "personal_gold_approval besitzt nicht schema_version 1.0."
        )
    if (
        _require_sha256(
            personal_approval.get("gold_audit_sha256"),
            "Gold-Audit-Hash in personal_gold_approval",
        )
        != gold_audit_sha256
    ):
        raise ValueError(
            "personal_gold_approval bindet nicht den aktuellen Gold-Audit."
        )
    if (
        _require_sha256(
            personal_approval.get("training_samples_sha256"),
            "Sample-Hash in personal_gold_approval",
        )
        != training_samples_sha256
    ):
        raise ValueError(
            "personal_gold_approval bindet nicht die aktuelle "
            "training_samples.json."
        )
    personal_approved_by = str(
        personal_approval.get("approved_by") or ""
    ).strip()
    if personal_approved_by.casefold() != approved_by.casefold():
        raise ValueError(
            "personal_gold_approval stammt nicht von der freigebenden Person."
        )
    personal_approved_utc = _require_utc(
        personal_approval.get("approved_utc"),
        "Freigabezeit in personal_gold_approval",
    )
    raw_source_codes = personal_approval.get("source_codes")
    if not isinstance(raw_source_codes, list) or not raw_source_codes:
        raise ValueError(
            "personal_gold_approval besitzt keine freigegebenen source_codes."
        )
    source_codes: list[str] = []
    seen_source_codes: set[str] = set()
    for index, raw_code in enumerate(raw_source_codes):
        if not isinstance(raw_code, str):
            raise ValueError(
                "personal_gold_approval enthaelt einen ungueltigen "
                f"source_codes-Eintrag an Position {index}."
            )
        code = gold_audit_tools.normalized_code(raw_code)
        if not code or code != raw_code:
            raise ValueError(
                "personal_gold_approval enthaelt einen leeren oder nicht "
                f"normalisierten source_code: {raw_code!r}."
            )
        if code in seen_source_codes:
            raise ValueError(
                f"personal_gold_approval enthaelt source_code doppelt: {code}."
            )
        seen_source_codes.add(code)
        source_codes.append(code)
    missing_source_codes = sorted(
        audit_codes - seen_source_codes,
        key=str.casefold,
    )
    if missing_source_codes:
        raise ValueError(
            "personal_gold_approval deckt nicht alle Audit-Codes ab. "
            "Fehlende source_codes: "
            + ", ".join(missing_source_codes)
        )

    raw_entries = document.get("entries")
    if not isinstance(raw_entries, list) or any(
        not isinstance(entry, dict) for entry in raw_entries
    ):
        raise ValueError("Die aktive v3-Migration besitzt keine gueltigen Zeilen.")
    counts = _require_exact_fields(
        document.get("entry_counts"),
        {"total", "by_source_kind", "teacher_observed_total"},
        "Anzahlen der v3-Migration",
    )
    if _require_nonnegative_int(counts.get("total"), "Migration total") != len(
        raw_entries
    ):
        raise ValueError("Die Gesamtanzahl der v3-Migration ist falsch.")
    by_source_kind = counts.get("by_source_kind")
    if not isinstance(by_source_kind, dict):
        raise ValueError("Die v3-Migration besitzt keine Quellen-Anzahlen.")
    actual_source_counts = Counter(
        str(entry.get("source_kind") or "") for entry in raw_entries
    )
    if dict(actual_source_counts) != by_source_kind:
        raise ValueError("Die Quellen-Anzahlen der v3-Migration sind falsch.")

    teacher_observed_total = 0
    seen_rows: set[tuple[str, str, str]] = set()
    teacher_rows: dict[str, Mapping[str, Any]] = {}
    base_fields = {
        "source_kind",
        "source_key",
        "observed_count",
        "proposed_action",
        "proposed_target",
        "reason",
        "approval_status",
        "approved_by",
        "approved_utc",
    }
    for index, raw_entry in enumerate(raw_entries):
        fields = set(raw_entry)
        if fields not in (base_fields, base_fields | {"source_id"}):
            raise ValueError(f"Migrationszeile {index} hat fremde oder fehlende Felder.")
        source_kind = str(raw_entry.get("source_kind") or "").strip()
        source_key = str(raw_entry.get("source_key") or "").strip()
        source_id = str(raw_entry.get("source_id") or "").strip()
        if not source_kind or not source_key:
            raise ValueError(f"Migrationszeile {index} besitzt keine Quelle.")
        row_key = (source_kind, source_key, source_id)
        if row_key in seen_rows:
            raise ValueError(f"Migrationszeile ist doppelt vorhanden: {row_key}")
        seen_rows.add(row_key)

        observed = raw_entry.get("observed_count")
        if observed is not None:
            observed = _require_nonnegative_int(
                observed,
                f"observed_count von {source_kind}:{source_key}",
            )
        action = str(raw_entry.get("proposed_action") or "").strip()
        status = str(raw_entry.get("approval_status") or "").strip()
        target = raw_entry.get("proposed_target")
        if action not in ALLOWED_MIGRATION_ACTIONS:
            raise ValueError(
                f"Migrationszeile {source_kind}:{source_key} hat Aktion '{action}'."
            )
        if status not in ALLOWED_MIGRATION_STATUSES:
            raise ValueError(
                f"Migrationszeile {source_kind}:{source_key} hat Status '{status}'."
            )
        if action == "map":
            if not isinstance(target, str) or target not in classes:
                raise ValueError(
                    f"Migrationsziel fuer {source_kind}:{source_key} ist unbekannt."
                )
        elif action == "review":
            if target is not None and (
                not isinstance(target, str) or target not in classes
            ):
                raise ValueError(
                    f"Reviewziel fuer {source_kind}:{source_key} ist unbekannt."
                )
        elif target is not None:
            raise ValueError(
                f"Migrationszeile {source_kind}:{source_key} darf kein Ziel besitzen."
            )

        if status == "approved":
            if action == "review" and source_kind == "teacher_vsa_code":
                raise ValueError(
                    f"Migrationszeile {source_kind}:{source_key} ist noch review."
                )
            if not str(raw_entry.get("approved_by") or "").strip():
                raise ValueError(
                    f"Freigabeperson fuer {source_kind}:{source_key} fehlt."
                )
            _require_utc(
                raw_entry.get("approved_utc"),
                f"Freigabezeit von {source_kind}:{source_key}",
            )
        elif raw_entry.get("approved_by") is not None or raw_entry.get(
            "approved_utc"
        ) is not None:
            raise ValueError(
                f"Pending-Migrationszeile {source_kind}:{source_key} "
                "enthaelt eine unvollstaendige Freigabe."
            )

        if source_kind == "teacher_vsa_code":
            normalized_key = gold_audit_tools.normalized_code(source_key)
            if normalized_key != source_key:
                raise ValueError(
                    f"teacher_vsa_code ist nicht normalisiert: {source_key}"
                )
            if normalized_key in teacher_rows:
                raise ValueError(
                    f"teacher_vsa_code ist mehrfach vorhanden: {normalized_key}"
                )
            teacher_rows[normalized_key] = raw_entry
            if observed is not None:
                teacher_observed_total += observed

    if (
        _require_nonnegative_int(
            counts.get("teacher_observed_total"),
            "teacher_observed_total",
        )
        != teacher_observed_total
    ):
        raise ValueError("teacher_observed_total der v3-Migration ist falsch.")
    if path.read_bytes() != data:
        raise ValueError("Die aktive v3-Migration wurde parallel geaendert.")
    return (
        teacher_rows,
        data,
        _sha256_bytes(data),
        personal_approved_utc,
        tuple(source_codes),
    )


def _discover_protected_sets(
    knowledge_root: Path,
) -> tuple[dict[str, str], ...]:
    subsets_root = knowledge_root / "eval_set" / "subsets"
    if not subsets_root.is_dir() or _is_reparse_or_symlink(subsets_root):
        raise ValueError(f"Der direkte Eval-Schutz fehlt oder ist unsicher: {subsets_root}")
    result: list[dict[str, str]] = []
    for manifest_path in sorted(
        subsets_root.glob("*/_manifest.json"),
        key=lambda path: str(path).casefold(),
    ):
        set_root = manifest_path.parent
        if _is_reparse_or_symlink(set_root) or _is_reparse_or_symlink(manifest_path):
            raise ValueError(
                f"Ein Eval-Schutz darf keinen Link oder Junction verwenden: {set_root}"
            )
        document, _ = _load_json_object_with_bytes(
            manifest_path,
            "Eval-Schutzmanifest",
        )
        if document.get("frozen") is not True:
            raise ValueError(f"Eval-Schutz ist nicht eingefroren: {manifest_path}")
        result.append(
            {
                "set_id": f"dev-val-{set_root.name.casefold().replace('_', '-')}-v1",
                "role": "development_validation",
                "root_path": str(set_root.relative_to(knowledge_root)),
                "manifest_sha256": _sha256_file(manifest_path),
            }
        )
    if not result:
        raise ValueError("Kein direktes, eingefrorenes Dev-Val-Set wurde gefunden.")
    return tuple(result)


def _protected_set_key(
    knowledge_root: Path,
    entry: Mapping[str, Any],
) -> tuple[str, str, str, str]:
    if set(entry) != {"set_id", "role", "root_path", "manifest_sha256"}:
        raise ValueError("Ein geschuetztes Set hat fremde oder fehlende Felder.")
    set_id = str(entry.get("set_id") or "").strip()
    role = str(entry.get("role") or "").strip()
    stored_root = Path(str(entry.get("root_path") or ""))
    if not set_id or role != "development_validation" or not str(stored_root):
        raise ValueError("Ein geschuetztes Set ist unvollstaendig.")
    set_root = stored_root if stored_root.is_absolute() else knowledge_root / stored_root
    manifest_path = set_root / "_manifest.json"
    manifest_sha = _require_sha256(
        entry.get("manifest_sha256"),
        f"Manifest-Hash von {set_id}",
    )
    if (
        not manifest_path.is_file()
        or _is_reparse_or_symlink(set_root)
        or _is_reparse_or_symlink(manifest_path)
        or _sha256_file(manifest_path) != manifest_sha
    ):
        raise ValueError(f"Geschuetztes Eval-Manifest stimmt nicht: {manifest_path}")
    normalized_root = os.path.normcase(str(set_root.resolve()))
    return set_id, role, normalized_root, manifest_sha


def _read_existing_registry(
    knowledge_root: Path,
    approved_by: str,
) -> tuple[dict[str, Any], bytes, str, tuple[dict[str, str], ...]]:
    registry_path = _require_safe_storage_path(
        knowledge_root / "training" / "export_registry_v1.json",
        knowledge_root,
        "Das bestehende Exportregister",
    )
    document, data = _load_json_object_with_bytes(
        registry_path,
        "Bestehendes Exportregister",
    )
    allowed_fields = REQUIRED_REGISTRY_FIELDS | {"negative_images"}
    if not REQUIRED_REGISTRY_FIELDS.issubset(document) or not set(document).issubset(
        allowed_fields
    ):
        raise ValueError(
            "Das bestehende Exportregister hat fremde oder fehlende Felder."
        )
    if (
        document.get("schema_version") != REGISTRY_SCHEMA_VERSION
        or document.get("approval_status") != "approved"
        or str(document.get("approved_by") or "").strip().casefold()
        != approved_by.casefold()
    ):
        raise ValueError("Das bestehende Exportregister ist nicht passend freigegeben.")
    _require_utc(document.get("approved_utc"), "Freigabezeit des Exportregisters")
    approved_ids = document.get("approved_sample_ids")
    holding_roles = document.get("holding_roles")
    protected_raw = document.get("protected_sets")
    if (
        not isinstance(approved_ids, list)
        or any(not isinstance(item, str) or not item.strip() for item in approved_ids)
        or len(approved_ids) != len(set(approved_ids))
        or not isinstance(holding_roles, dict)
        or any(
            not isinstance(key, str)
            or not key
            or role not in {"train", "development_validation"}
            for key, role in holding_roles.items()
        )
        or not isinstance(protected_raw, list)
        or any(not isinstance(item, dict) for item in protected_raw)
    ):
        raise ValueError("Das bestehende Exportregister ist strukturell ungueltig.")
    if "negative_images" in document and (
        not isinstance(document["negative_images"], list)
        or any(not isinstance(item, dict) for item in document["negative_images"])
    ):
        raise ValueError("Das bestehende Exportregister hat ungueltige Negativbilder.")

    discovered = _discover_protected_sets(knowledge_root)
    stored_keys = {
        _protected_set_key(knowledge_root, item) for item in protected_raw
    }
    discovered_keys = {
        _protected_set_key(knowledge_root, item) for item in discovered
    }
    if len(stored_keys) != len(protected_raw):
        raise ValueError(
            "Das bestehende Exportregister enthaelt doppelte Dev-Val-Sets."
        )
    if not stored_keys.issubset(discovered_keys):
        raise ValueError(
            "Das bestehende Exportregister bindet nicht mehr alle bisher "
            "eingefrorenen Dev-Val-Sets unveraendert."
        )
    return document, data, _sha256_bytes(data), discovered


def _read_strict_negatives(
    knowledge_root: Path,
    audit_document: Mapping[str, Any],
    class_map_sha256: str,
    vsa_manifest_sha256: str,
) -> tuple[
    Path,
    tuple[Path, ...],
    tuple[dict[str, Any], ...],
    tuple[dict[str, Any], ...],
]:
    inputs = audit_document.get("eingaben")
    if not isinstance(inputs, dict):
        raise ValueError("Der Gold-Audit enthaelt keine gueltigen Eingaben.")
    negatives_text = str(inputs.get("negatives_pfad") or "").strip()
    if not negatives_text:
        raise ValueError("Der Gold-Audit bindet keinen Negativ-Pool.")
    negatives_dir = Path(negatives_text)
    if not negatives_dir.is_absolute():
        raise ValueError("Der Negativ-Pool im Gold-Audit muss absolut sein.")
    negatives_dir = Path(os.path.abspath(negatives_dir))
    negatives_root = Path(
        os.path.abspath(knowledge_root / "training" / "negatives")
    )
    if not _is_within(negatives_dir, negatives_root):
        raise ValueError("Der Negativ-Pool liegt ausserhalb des Knowledge-Roots.")

    raw_set_paths = inputs.get("negative_set_pfade")
    if (
        not isinstance(raw_set_paths, list)
        or not raw_set_paths
        or any(not isinstance(value, str) or not value.strip() for value in raw_set_paths)
    ):
        raise ValueError(
            "Der Gold-Audit braucht mindestens einen expliziten strikten Negativsatz."
        )
    negative_set_paths = tuple(
        Path(os.path.abspath(Path(value))) for value in raw_set_paths
    )
    negative_images, negative_sets = gold_audit_tools.read_training_negative_sources(
        knowledge_root,
        negatives_dir,
        negative_set_paths,
        minimum_legacy_bytes=MIN_NEGATIVE_BYTES,
    )
    if not negative_sets or not negative_images:
        raise ValueError("Der Gold-Audit enthaelt keinen strikten Negativsatz.")
    if any(
        image.get("source_type") != "reviewed_negative_set"
        or image.get("review_decision") != "all_classes_clear"
        for image in negative_images
    ):
        raise ValueError(
            "Legacy-Negative oder nicht als all_classes_clear bestaetigte "
            "Bilder sind fuer DETECT_ALL gesperrt."
        )
    for image in negative_images:
        if (
            image.get("class_map_version") != CLASS_MAP_VERSION
            or image.get("class_map_sha256") != class_map_sha256
            or image.get("vsa_manifest_hash") != vsa_manifest_sha256
        ):
            raise ValueError(
                "Ein strikter Negativsatz passt nicht zur aktiven Klassenkarte."
            )

    pool = audit_document.get("negativ_pool")
    if not isinstance(pool, dict):
        raise ValueError("Der Gold-Audit enthaelt keinen gueltigen Negativ-Pool.")
    if pool.get("registry_modus") != NEGATIVE_REGISTRY_MODE:
        raise ValueError(
            "Der Gold-Audit verwendet keinen rein strikten Negativmodus; "
            "Legacy-Mischungen sind gesperrt."
        )
    if not _paths_equal(Path(str(pool.get("pfad") or "")), negatives_dir):
        raise ValueError("Negativ-Pfad und Gold-Audit widersprechen sich.")
    audit_entries = pool.get("dateien")
    expected_entries = [
        {"datei": image["path"]}
        | {key: value for key, value in image.items() if key != "path"}
        for image in negative_images
    ]
    if (
        not isinstance(audit_entries, list)
        or pool.get("anzahl") != len(audit_entries)
        or sorted(audit_entries, key=lambda item: str(item.get("sha256")))
        != sorted(expected_entries, key=lambda item: str(item.get("sha256")))
    ):
        raise ValueError(
            "Negativbilder oder ihre Provenienz weichen vom Gold-Audit ab."
        )
    if pool.get("sets") != list(negative_sets):
        raise ValueError("Negativsatz-Provenienz und Gold-Audit widersprechen sich.")
    return negatives_dir, negative_set_paths, negative_images, negative_sets


def _validate_audit_header(
    knowledge_root: Path,
    approved_by: str,
    audit_path: Path,
    registry_sha256: str,
    samples_sha256: str,
    vsa_manifest_sha256: str,
) -> tuple[dict[str, Any], bytes]:
    reports_root = (knowledge_root / "training" / "reports").resolve()
    resolved_audit = audit_path.resolve()
    if (
        not resolved_audit.is_file()
        or not _is_within(resolved_audit, reports_root)
        or _is_reparse_or_symlink(audit_path)
    ):
        raise ValueError(
            "Der Gold-Audit muss als normale Datei unter "
            f"{reports_root} liegen."
        )
    document, data = _load_json_object_with_bytes(
        resolved_audit,
        "Gold-Audit",
    )
    if (
        document.get("schema_version") != GOLD_AUDIT_SCHEMA_VERSION
        or document.get("bericht") != GOLD_AUDIT_REPORT_NAME
        or document.get("modus") != GOLD_AUDIT_MODE
    ):
        raise ValueError(
            "Der Gold-Audit muss ein schreibfreier Bericht mit Schema 1.1 sein."
        )
    _require_utc(document.get("zeitstempel_utc"), "Zeitstempel des Gold-Audits")
    inputs = document.get("eingaben")
    if not isinstance(inputs, dict):
        raise ValueError("Der Gold-Audit enthaelt keine gueltigen Eingaben.")
    if (
        str(inputs.get("approved_by") or "").strip().casefold()
        != approved_by.casefold()
    ):
        raise ValueError("Der Gold-Audit ist fuer eine andere Person freigegeben.")

    samples_path = (knowledge_root / "training_samples.json").resolve()
    registry_path = (
        knowledge_root / "training" / "export_registry_v1.json"
    ).resolve()
    if not _paths_equal(Path(str(inputs.get("samples_pfad") or "")), samples_path):
        raise ValueError("Der Gold-Audit gehoert nicht zu training_samples.json.")
    if not _paths_equal(Path(str(inputs.get("registry_pfad") or "")), registry_path):
        raise ValueError("Der Gold-Audit gehoert nicht zum aktiven Exportregister.")
    if (
        _require_sha256(
            inputs.get("samples_sha256"),
            "samples_sha256 im Gold-Audit",
        )
        != samples_sha256
    ):
        raise ValueError(
            "training_samples.json wurde nach dem Gold-Audit geaendert."
        )
    if (
        _require_sha256(
            inputs.get("registry_sha256"),
            "registry_sha256 im Gold-Audit",
        )
        != registry_sha256
    ):
        raise ValueError("Das Exportregister wurde nach dem Gold-Audit geaendert.")
    audit_vsa_path = Path(str(inputs.get("vsa_manifest_pfad") or ""))
    if not _paths_equal(audit_vsa_path, ACTIVE_VSA_MANIFEST_PATH):
        raise ValueError("Der Gold-Audit bindet nicht das aktive VSA-Manifest.")
    if (
        _require_sha256(
            inputs.get("vsa_manifest_sha256"),
            "VSA-Manifest-Hash im Gold-Audit",
        )
        != vsa_manifest_sha256
    ):
        raise ValueError("VSA-Manifest und Gold-Audit widersprechen sich.")

    split = document.get("split")
    if (
        not isinstance(split, dict)
        or split.get("release_faehig") is not True
        or split.get("test_eingefroren_nur_markiert") is not True
        or split.get("fehlende_haltungsidentitaet", 0) != 0
    ):
        raise ValueError("Der Gold-Audit besitzt keinen release-faehigen Split.")
    audit_samples = document.get("samples")
    stages = document.get("pruefstufen")
    if (
        not isinstance(audit_samples, list)
        or any(not isinstance(entry, dict) for entry in audit_samples)
        or not isinstance(stages, dict)
        or stages.get("final_verwendbar") != len(audit_samples)
    ):
        raise ValueError("Die verwendbare Sampleliste im Gold-Audit ist unvollstaendig.")
    split_images = split.get("bilder")
    if (
        not isinstance(split_images, dict)
        or set(split_images) != {"train", "val", "test"}
        or any(
            isinstance(value, bool) or not isinstance(value, int) or value < 0
            for value in split_images.values()
        )
        or sum(split_images.values()) != len(audit_samples)
    ):
        raise ValueError("Die Split-Anzahlen im Gold-Audit sind ungueltig.")
    if resolved_audit.read_bytes() != data:
        raise ValueError("Der Gold-Audit wurde waehrend der Pruefung geaendert.")
    return document, data


def _verify_personal_source(
    source: dict[str, Any],
    approved_by: str,
    sample_id: str,
) -> tuple[Path, str]:
    accepted, skip_reason = gold_audit_tools.is_intake_candidate(source)
    if not accepted:
        raise ValueError(
            f"Goldsample {sample_id} ist nicht Approved/ManualCoding/PdfPhoto: "
            f"{skip_reason}."
        )
    personal_reason = gold_audit_tools.check_personal(source, approved_by)
    if personal_reason:
        raise ValueError(
            f"Goldsample {sample_id} hat keine gueltige persoenliche "
            f"Bestaetigung: {personal_reason}"
        )
    bbox_reason = gold_audit_tools.check_bbox(source)
    if bbox_reason:
        raise ValueError(f"Goldsample {sample_id} hat keine gueltige BBox: {bbox_reason}")

    frame_text = str(source.get("FramePath") or "").strip()
    frame_path = Path(frame_text)
    if not frame_path.is_absolute():
        raise ValueError(f"Goldsample {sample_id} besitzt keinen absoluten Bildpfad.")
    image_reason, width, height = gold_audit_tools.check_image(frame_path)
    if image_reason:
        raise ValueError(f"Goldsample {sample_id}: {image_reason}")
    mask_reason = gold_audit_tools.check_mask(
        source.get("SamMaskRle"),
        source.get("SamMaskImageWidth"),
        source.get("SamMaskImageHeight"),
        width,
        height,
        source.get("BboxXCenter"),
        source.get("BboxYCenter"),
        source.get("BboxWidth"),
        source.get("BboxHeight"),
        source.get("SamMaskAreaPixels"),
    )
    if mask_reason:
        raise ValueError(f"Goldsample {sample_id} hat keine gueltige Maske: {mask_reason}")
    return frame_path, str(source.get("SourceType") or "").strip()


def _validate_holding_roles(
    selected_samples: Sequence[DetectSample],
    negative_images: Sequence[Mapping[str, Any]],
) -> None:
    exact_roles: dict[str, str] = {}
    physical_roles: dict[str, str] = {}
    for sample in selected_samples:
        export_role = (
            "development_validation" if sample.role == "val" else "train"
        )
        previous_exact = exact_roles.setdefault(sample.holding_key, export_role)
        if previous_exact != export_role:
            raise ValueError(
                f"Haltung {sample.holding_key} besitzt widerspruechliche Rollen."
            )
        previous_physical = physical_roles.setdefault(
            sample.physical_holding_key,
            export_role,
        )
        if previous_physical != export_role:
            raise ValueError(
                "Gegenrichtungen derselben physischen Haltung liegen in "
                "verschiedenen Rollen."
            )

    for image in negative_images:
        holding = str(image.get("holding_key") or "")
        physical = str(image.get("physical_holding_key") or "")
        if physical != _physical_holding_key(holding):
            raise ValueError("Ein Negativbild besitzt keine belastbare Haltung.")
        split = str(image.get("split") or "")
        export_role = "development_validation" if split == "validation" else split
        if export_role not in {"train", "development_validation"}:
            raise ValueError("Ein Negativbild besitzt eine unbekannte Split-Rolle.")
        previous_physical = physical_roles.setdefault(physical, export_role)
        if previous_physical != export_role:
            raise ValueError(
                "Gold- und Negativbild derselben physischen Haltung liegen in "
                "verschiedenen Rollen."
            )


def _validate_negative_audit_roles(
    audit_image_roles: Mapping[str, str],
    audit_physical_roles: Mapping[str, str],
    negative_images: Sequence[Mapping[str, Any]],
) -> None:
    for image in negative_images:
        image_sha256 = _require_sha256(
            image.get("sha256"),
            "Bild-Hash eines Negativbilds",
        )
        holding = str(image.get("holding_key") or "")
        physical = str(image.get("physical_holding_key") or "")
        if physical != _physical_holding_key(holding):
            raise ValueError("Ein Negativbild besitzt keine belastbare Haltung.")

        split = str(image.get("split") or "")
        audit_role = "val" if split == "validation" else split
        if audit_role not in {"train", "val"}:
            raise ValueError("Ein Negativbild besitzt eine unbekannte Split-Rolle.")

        image_role = audit_image_roles.get(image_sha256)
        if image_role is not None:
            raise ValueError(
                "Negativbild ist bytegleich mit einem Gold-Audit-Bild; ein "
                "identisches Goldbild darf nie als all_classes_clear gelten."
            )

        holding_role = audit_physical_roles.get(physical)
        if holding_role == "test":
            raise ValueError(
                "Negativbild stammt aus einer eingefrorenen Audit-Testhaltung."
            )
        if holding_role is not None and holding_role != audit_role:
            raise ValueError(
                "Negativbild und Gold-Audit derselben physischen Haltung liegen "
                "in verschiedenen Rollen."
            )


def build_preparation(
    knowledge_root: Path,
    approved_by: str,
    gold_audit_path: Path,
) -> DetectPreparation:
    requested_root = Path(os.path.abspath(knowledge_root))
    if (
        not requested_root.is_dir()
        or _is_reparse_or_symlink(requested_root)
    ):
        raise ValueError(
            f"Der Knowledge-Root fehlt oder ist ein Link/Junction: {requested_root}"
        )
    root = requested_root.resolve(strict=True)
    user = approved_by.strip()
    if not user:
        raise ValueError("Die freigebende Person fehlt.")
    samples_path = (root / "training_samples.json").resolve()
    raw_samples, samples_bytes = _load_json_array_with_bytes(
        samples_path,
        "training_samples.json",
    )
    samples_sha256 = _sha256_bytes(samples_bytes)
    (
        _existing_registry,
        _registry_bytes,
        registry_sha256,
        protected_sets,
    ) = _read_existing_registry(root, user)
    classes, _class_map_bytes, class_map_sha256, vsa_manifest_sha256 = (
        _read_active_class_map()
    )
    audit_document, audit_bytes = _validate_audit_header(
        root,
        user,
        gold_audit_path,
        registry_sha256,
        samples_sha256,
        vsa_manifest_sha256,
    )
    audit_sha256 = _sha256_bytes(audit_bytes)
    audit_codes: set[str] = set()
    for entry in audit_document["samples"]:
        code = gold_audit_tools.normalized_code(entry.get("code"))
        if not code:
            raise ValueError("Der Gold-Audit enthaelt einen leeren VSA-Code.")
        audit_codes.add(code)
    (
        teacher_rows,
        _migration_bytes,
        migration_sha256,
        personal_gold_approved_utc,
        personal_gold_source_codes,
    ) = _read_active_migration(
        classes,
        vsa_manifest_sha256,
        user,
        audit_sha256,
        samples_sha256,
        audit_codes,
    )
    (
        negatives_dir,
        negative_set_paths,
        negative_images,
        negative_sets,
    ) = _read_strict_negatives(
        root,
        audit_document,
        class_map_sha256,
        vsa_manifest_sha256,
    )

    source_by_id: dict[str, dict[str, Any]] = {}
    for source in raw_samples:
        sample_id = str(source.get("SampleId") or "").strip()
        if not sample_id:
            raise ValueError("training_samples.json enthaelt eine leere SampleId.")
        if sample_id in source_by_id:
            raise ValueError(f"SampleId ist mehrfach vorhanden: {sample_id}")
        source_by_id[sample_id] = source

    audit_samples = audit_document["samples"]
    gold_root = (root / "gold_frames").resolve()
    if not gold_root.is_dir() or _is_reparse_or_symlink(gold_root):
        raise ValueError(f"Der Goldroot fehlt oder ist unsicher: {gold_root}")
    selected: list[DetectSample] = []
    discarded_ids: list[str] = []
    test_ids: list[str] = []
    seen_audit_ids: set[str] = set()
    image_roles: dict[str, str] = {}
    audit_holding_roles: dict[str, str] = {}
    audit_physical_roles: dict[str, str] = {}

    for entry in audit_samples:
        sample_id = str(entry.get("sample_id") or "").strip()
        case_id = str(entry.get("case_id") or "").strip()
        holding_key = str(entry.get("haltung_key") or "").strip()
        code = gold_audit_tools.normalized_code(entry.get("code"))
        main_code = str(entry.get("hauptcode") or "").strip().upper()
        role = str(entry.get("rolle") or "").strip().casefold()
        group_key = str(entry.get("gruppe") or "").strip()
        if not sample_id or sample_id in seen_audit_ids:
            raise ValueError(
                f"Gold-Audit besitzt eine leere oder doppelte SampleId: {sample_id}"
            )
        seen_audit_ids.add(sample_id)
        if (
            not case_id
            or not holding_key
            or role not in ALLOWED_AUDIT_ROLES
            or not group_key
            or role != _expected_split_role(group_key)
        ):
            raise ValueError(
                f"Gold-Audit-Sample {sample_id} besitzt keine belastbare "
                "Haltung oder Split-Rolle."
            )
        if (
            gold_audit_tools.normalize_holding_key(case_id) != holding_key
            or gold_audit_tools.main_code(code) != main_code
        ):
            raise ValueError(
                f"Gold-Audit-Sample {sample_id} hat widerspruechliche Code-/Haltungsdaten."
            )
        physical_holding = _physical_holding_key(holding_key)
        prior_role = audit_holding_roles.setdefault(holding_key, role)
        if prior_role != role:
            raise ValueError(
                f"Haltung {holding_key} hat widerspruechliche Audit-Rollen."
            )
        prior_physical_role = audit_physical_roles.setdefault(
            physical_holding,
            role,
        )
        if prior_physical_role != role:
            raise ValueError(
                "Gegenrichtungen derselben physischen Haltung besitzen "
                "widerspruechliche Audit-Rollen."
            )

        source = source_by_id.get(sample_id)
        if source is None:
            raise ValueError(
                f"Audit-Sample fehlt in training_samples.json: {sample_id}"
            )
        source_code = gold_audit_tools.normalized_code(source.get("Code"))
        source_case = str(source.get("CaseId") or "").strip()
        if source_code != code or source_case != case_id:
            raise ValueError(
                f"Goldsample {sample_id} weicht bei Code oder CaseId vom Audit ab."
            )
        frame_path, source_type = _verify_personal_source(source, user, sample_id)
        resolved_frame = _require_plain_path_below(
            frame_path,
            gold_root,
            f"Goldbild {sample_id}",
        )
        image_sha256 = _sha256_file(resolved_frame)
        audit_image_sha256 = _require_sha256(
            entry.get("image_sha256"),
            f"Bild-Hash von {sample_id}",
        )
        if image_sha256 != audit_image_sha256:
            raise ValueError(
                f"Bild-Hash von Goldsample {sample_id} weicht vom Audit ab."
            )
        prior_image_role = image_roles.setdefault(image_sha256, role)
        if prior_image_role != role:
            raise ValueError(
                f"Dasselbe Goldbild liegt in mehreren Split-Rollen: {sample_id}"
            )

        decision = teacher_rows.get(code)
        if decision is None:
            raise ValueError(
                f"Fuer Code {code} gibt es keine teacher_vsa_code-Entscheidung "
                "in der aktiven v3-Migration."
            )
        status = str(decision.get("approval_status") or "")
        action = str(decision.get("proposed_action") or "")
        if status != "approved":
            raise ValueError(
                f"teacher_vsa_code {code} hat Status '{status}' statt approved."
            )
        if action == "review":
            raise ValueError(f"teacher_vsa_code {code} ist noch review.")
        if (
            str(decision.get("approved_by") or "").strip().casefold()
            != user.casefold()
        ):
            raise ValueError(
                f"teacher_vsa_code {code} wurde durch eine andere Person freigegeben."
            )
        _require_utc(
            decision.get("approved_utc"),
            f"Freigabezeit von teacher_vsa_code {code}",
        )
        if role == "test":
            test_ids.append(sample_id)
            continue
        if action == "discard":
            discarded_ids.append(sample_id)
            continue
        if action != "map":
            raise ValueError(
                f"teacher_vsa_code {code} besitzt die unbekannte Aktion '{action}'."
            )
        target = str(decision.get("proposed_target") or "")
        if target not in classes:
            raise ValueError(f"teacher_vsa_code {code} mappt auf unbekanntes Ziel.")
        selected.append(
            DetectSample(
                sample_id=sample_id,
                case_id=case_id,
                holding_key=holding_key,
                physical_holding_key=physical_holding,
                code=code,
                target_class=target,
                frame_path=resolved_frame,
                image_sha256=image_sha256,
                confirmed_at_utc=str(source.get("ConfirmedAtUtc") or ""),
                source_type=source_type,
                role=role,
                group_key=group_key,
            )
        )

    if len(seen_audit_ids) != len(audit_samples):
        raise ValueError("Der Gold-Audit enthaelt doppelte Samples.")
    if not selected:
        raise ValueError("Die freigegebene Migration liefert keine Detect-Goldsamples.")
    if not any(sample.role == "train" for sample in selected) or not any(
        sample.role == "val" for sample in selected
    ):
        raise ValueError(
            "Das Mehrklassen-Register braucht mindestens ein Train- und ein Val-Bild."
        )
    positive_hashes = {sample.image_sha256 for sample in selected}
    negative_hashes = {str(image.get("sha256") or "") for image in negative_images}
    collisions = sorted(positive_hashes & negative_hashes)
    if collisions:
        raise ValueError(
            "Ein Bild ist zugleich Goldsample und all_classes_clear-Negativbild: "
            + ", ".join(collisions)
        )
    _validate_negative_audit_roles(
        image_roles,
        audit_physical_roles,
        negative_images,
    )
    _validate_holding_roles(selected, negative_images)

    selected.sort(key=lambda sample: sample.sample_id.casefold())
    receipt_path = _require_safe_storage_path(
        root
        / "training"
        / "pilots"
        / "DETECT_ALL"
        / "registry_setup_v1.json",
        root,
        "Der DETECT_ALL-Beleg",
    )
    transaction_path = _require_safe_storage_path(
        receipt_path.parent / TRANSACTION_FILE_NAME,
        root,
        "Der DETECT_ALL-Transaktionsbeleg",
    )
    if transaction_path.exists():
        raise ValueError(
            "Ein unvollstaendiger DETECT_ALL-Wechsel ist vorhanden. "
            "Er muss mit --execute --renew-existing wiederaufgenommen werden."
        )
    existing_receipt_sha = (
        _sha256_file(receipt_path) if receipt_path.is_file() else None
    )
    return DetectPreparation(
        registry_path=root / "training" / "export_registry_v1.json",
        receipt_path=receipt_path,
        approved_by=user,
        source_audit_path=gold_audit_path.resolve(),
        source_audit_sha256=audit_sha256,
        source_samples_path=samples_path,
        source_samples_sha256=samples_sha256,
        expected_existing_registry_sha256=registry_sha256,
        expected_existing_receipt_sha256=existing_receipt_sha,
        class_map_path=ACTIVE_CLASS_MAP_PATH.resolve(),
        class_map_sha256=class_map_sha256,
        class_map_version=CLASS_MAP_VERSION,
        vsa_manifest_sha256=vsa_manifest_sha256,
        migration_path=ACTIVE_MIGRATION_PATH.resolve(),
        migration_sha256=migration_sha256,
        migration_version=MIGRATION_VERSION,
        personal_gold_approved_utc=personal_gold_approved_utc,
        personal_gold_source_codes=personal_gold_source_codes,
        selected_samples=tuple(selected),
        discarded_sample_ids=tuple(sorted(discarded_ids, key=str.casefold)),
        excluded_test_sample_ids=tuple(sorted(test_ids, key=str.casefold)),
        protected_sets=protected_sets,
        negatives_dir=negatives_dir,
        negative_set_roots=negative_set_paths,
        negative_sets=negative_sets,
        negative_images=negative_images,
    )


def _json_bytes(document: Mapping[str, Any]) -> bytes:
    return (json.dumps(document, ensure_ascii=False, indent=2) + "\n").encode(
        "utf-8"
    )


def _transaction_path(knowledge_root: Path) -> Path:
    return (
        knowledge_root
        / "training"
        / "pilots"
        / "DETECT_ALL"
        / TRANSACTION_FILE_NAME
    )


def _optional_file_bytes(path: Path, label: str) -> bytes | None:
    if not path.exists():
        return None
    if not path.is_file() or _is_reparse_or_symlink(path):
        raise ValueError(f"{label} ist keine sichere normale Datei: {path}")
    return path.read_bytes()


def _decode_transaction_bytes(
    value: Any,
    expected_sha256: str,
    label: str,
) -> bytes:
    if not isinstance(value, str) or not value:
        raise ValueError(f"{label} fehlt im DETECT_ALL-Transaktionsbeleg.")
    try:
        data = base64.b64decode(value.encode("ascii"), validate=True)
    except (UnicodeEncodeError, ValueError) as error:
        raise ValueError(
            f"{label} ist im DETECT_ALL-Transaktionsbeleg ungueltig."
        ) from error
    if _sha256_bytes(data) != expected_sha256:
        raise ValueError(
            f"{label} passt nicht zum Hash im DETECT_ALL-Transaktionsbeleg."
        )
    return data


def _read_transaction_journal(
    knowledge_root: Path,
    registry_path: Path,
    receipt_path: Path,
    transaction_path: Path,
) -> tuple[bytes, bytes | None, str, str]:
    document, _ = _load_json_object_with_bytes(
        transaction_path,
        "DETECT_ALL-Transaktionsbeleg",
    )
    _require_exact_fields(
        document,
        {
            "schema_version",
            "purpose",
            "registry_path",
            "receipt_path",
            "previous_registry_sha256",
            "previous_registry_base64",
            "previous_receipt_sha256",
            "previous_receipt_base64",
            "new_registry_sha256",
            "new_receipt_sha256",
        },
        "DETECT_ALL-Transaktionsbeleg",
    )
    if (
        document.get("schema_version") != TRANSACTION_SCHEMA_VERSION
        or document.get("purpose") != TRANSACTION_PURPOSE
        or not _paths_equal(
            Path(str(document.get("registry_path") or "")),
            registry_path,
        )
        or not _paths_equal(
            Path(str(document.get("receipt_path") or "")),
            receipt_path,
        )
    ):
        raise ValueError(
            "Der DETECT_ALL-Transaktionsbeleg gehoert nicht zu diesen "
            "aktiven Dateien."
        )
    previous_registry_sha256 = _require_sha256(
        document.get("previous_registry_sha256"),
        "Alter Registry-Hash im DETECT_ALL-Transaktionsbeleg",
    )
    previous_registry_bytes = _decode_transaction_bytes(
        document.get("previous_registry_base64"),
        previous_registry_sha256,
        "Alte Registry",
    )
    previous_receipt_sha256_raw = document.get("previous_receipt_sha256")
    previous_receipt_base64 = document.get("previous_receipt_base64")
    if (
        previous_receipt_sha256_raw is None
        and previous_receipt_base64 is None
    ):
        previous_receipt_bytes = None
    elif (
        previous_receipt_sha256_raw is None
        or previous_receipt_base64 is None
    ):
        raise ValueError(
            "Der alte DETECT_ALL-Beleg ist im Transaktionsbeleg unvollstaendig."
        )
    else:
        previous_receipt_sha256 = _require_sha256(
            previous_receipt_sha256_raw,
            "Alter Receipt-Hash im DETECT_ALL-Transaktionsbeleg",
        )
        previous_receipt_bytes = _decode_transaction_bytes(
            previous_receipt_base64,
            previous_receipt_sha256,
            "Alter DETECT_ALL-Beleg",
        )
    new_registry_sha256 = _require_sha256(
        document.get("new_registry_sha256"),
        "Neuer Registry-Hash im DETECT_ALL-Transaktionsbeleg",
    )
    new_receipt_sha256 = _require_sha256(
        document.get("new_receipt_sha256"),
        "Neuer Receipt-Hash im DETECT_ALL-Transaktionsbeleg",
    )
    return (
        previous_registry_bytes,
        previous_receipt_bytes,
        new_registry_sha256,
        new_receipt_sha256,
    )


def recover_incomplete_transaction(knowledge_root: Path) -> str | None:
    requested_root = Path(os.path.abspath(knowledge_root))
    if (
        not requested_root.is_dir()
        or _is_reparse_or_symlink(requested_root)
    ):
        raise ValueError(
            f"Der Knowledge-Root fehlt oder ist ein Link/Junction: {requested_root}"
        )
    root = requested_root.resolve(strict=True)
    registry_path = _require_safe_storage_path(
        root / "training" / "export_registry_v1.json",
        root,
        "Das aktive Exportregister",
    )
    receipt_path = _require_safe_storage_path(
        root
        / "training"
        / "pilots"
        / "DETECT_ALL"
        / "registry_setup_v1.json",
        root,
        "Der aktive DETECT_ALL-Beleg",
    )
    transaction_path = _require_safe_storage_path(
        _transaction_path(root),
        root,
        "Der DETECT_ALL-Transaktionsbeleg",
    )
    if not transaction_path.exists():
        return None
    if not transaction_path.is_file() or _is_reparse_or_symlink(
        transaction_path
    ):
        raise ValueError(
            "Der DETECT_ALL-Transaktionsbeleg ist keine sichere normale Datei."
        )
    (
        previous_registry_bytes,
        previous_receipt_bytes,
        new_registry_sha256,
        new_receipt_sha256,
    ) = _read_transaction_journal(
        root,
        registry_path,
        receipt_path,
        transaction_path,
    )
    previous_registry_sha256 = _sha256_bytes(previous_registry_bytes)
    previous_receipt_sha256 = (
        _sha256_bytes(previous_receipt_bytes)
        if previous_receipt_bytes is not None
        else None
    )
    current_registry_bytes = _optional_file_bytes(
        registry_path,
        "Das aktive Exportregister",
    )
    current_receipt_bytes = _optional_file_bytes(
        receipt_path,
        "Der aktive DETECT_ALL-Beleg",
    )
    current_registry_sha256 = (
        _sha256_bytes(current_registry_bytes)
        if current_registry_bytes is not None
        else None
    )
    current_receipt_sha256 = (
        _sha256_bytes(current_receipt_bytes)
        if current_receipt_bytes is not None
        else None
    )
    old_state = (
        previous_registry_sha256,
        previous_receipt_sha256,
    )
    new_state = (new_registry_sha256, new_receipt_sha256)
    current_state = (current_registry_sha256, current_receipt_sha256)
    if current_state == new_state:
        transaction_path.unlink()
        return "committed"
    if current_state == old_state:
        transaction_path.unlink()
        return "rolled_back"
    if (
        current_registry_sha256
        not in {previous_registry_sha256, new_registry_sha256}
        or current_receipt_sha256
        not in {previous_receipt_sha256, new_receipt_sha256}
    ):
        raise ValueError(
            "Der unvollstaendige DETECT_ALL-Wechsel trifft auf fremd "
            "geaenderte aktive Dateien. Automatisches Ruecksetzen ist gesperrt."
        )

    _atomic_replace_bytes(registry_path, previous_registry_bytes)
    if previous_receipt_bytes is None:
        if receipt_path.exists():
            receipt_path.unlink()
    else:
        _atomic_replace_bytes(receipt_path, previous_receipt_bytes)
    if (
        _optional_file_bytes(registry_path, "Das aktive Exportregister")
        != previous_registry_bytes
        or _optional_file_bytes(receipt_path, "Der aktive DETECT_ALL-Beleg")
        != previous_receipt_bytes
    ):
        raise RuntimeError(
            "Der unvollstaendige DETECT_ALL-Wechsel konnte nicht bytegenau "
            "zurueckgesetzt werden."
        )
    transaction_path.unlink()
    return "rolled_back"


def _stage_bytes(path: Path, data: bytes) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(
        f".{path.name}.{os.getpid()}.{uuid.uuid4().hex}.tmp"
    )
    try:
        with temporary.open("xb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        return temporary
    except Exception:
        temporary.unlink(missing_ok=True)
        raise


def _atomic_replace_bytes(path: Path, data: bytes) -> None:
    temporary = _stage_bytes(path, data)
    try:
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def _publish_new_bytes(path: Path, data: bytes) -> None:
    if path.exists():
        raise ValueError(f"Die Transaktionsdatei existiert bereits: {path}")
    temporary = _stage_bytes(path, data)
    try:
        os.link(temporary, path)
    except FileExistsError as error:
        raise ValueError(
            f"Die Transaktionsdatei wurde parallel angelegt: {path}"
        ) from error
    finally:
        temporary.unlink(missing_ok=True)


def _archive_exact_bytes(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        if not path.is_file() or path.read_bytes() != data:
            raise ValueError(
                f"Das Hash-Archiv ist bereits mit anderem Inhalt belegt: {path}"
            )
        return
    try:
        with path.open("xb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
    except FileExistsError:
        if not path.is_file() or path.read_bytes() != data:
            raise ValueError(
                f"Das Hash-Archiv wurde parallel mit anderem Inhalt angelegt: {path}"
            )


def _read_expected_bytes(
    path: Path,
    expected_sha256: str | None,
    label: str,
) -> bytes | None:
    if expected_sha256 is None:
        if path.exists():
            raise ValueError(f"{label} wurde nach der Pruefung neu angelegt.")
        return None
    if not path.is_file():
        raise ValueError(f"{label} wurde nach der Pruefung entfernt.")
    data = path.read_bytes()
    if _sha256_bytes(data) != expected_sha256:
        raise ValueError(f"{label} wurde nach der Pruefung geaendert.")
    return data


def _require_unchanged(path: Path, expected: bytes | None, label: str) -> None:
    current = path.read_bytes() if path.is_file() else None
    if current != expected:
        raise ValueError(f"{label} wurde zwischenzeitlich geaendert.")


def _rollback_path(
    path: Path,
    previous_bytes: bytes | None,
    expected_current_bytes: bytes,
) -> None:
    current = path.read_bytes() if path.is_file() else None
    if current != expected_current_bytes:
        raise RuntimeError(
            f"{path} konnte nicht sicher zurueckgesetzt werden, weil die "
            "Datei erneut geaendert wurde."
        )
    if previous_bytes is None:
        path.unlink()
    else:
        _atomic_replace_bytes(path, previous_bytes)


def _relative_or_absolute(knowledge_root: Path, path: Path | None) -> str | None:
    if path is None:
        return None
    try:
        return path.resolve().relative_to(knowledge_root).as_posix()
    except ValueError:
        return str(path.resolve())


def _revalidate_preparation(
    preparation: DetectPreparation,
    approved_by: str,
) -> None:
    user = approved_by.strip()
    if user.casefold() != preparation.approved_by.casefold():
        raise ValueError(
            f"Die Vorbereitung ist fuer '{preparation.approved_by}' statt "
            f"'{user}' freigegeben."
        )
    root = preparation.registry_path.parent.parent.resolve()
    fresh = build_preparation(
        root,
        user,
        preparation.source_audit_path,
    )
    if fresh != preparation:
        changed: list[str] = []
        for field_name in preparation.__dataclass_fields__:
            if getattr(fresh, field_name) != getattr(preparation, field_name):
                changed.append(field_name)
        labels = {
            "migration_sha256": "Migration",
            "migration_path": "Migration",
            "personal_gold_approved_utc": "Persoenliche Goldfreigabe",
            "personal_gold_source_codes": "Persoenliche Goldfreigabe",
            "class_map_sha256": "Klassenkarte",
            "class_map_path": "Klassenkarte",
            "source_audit_sha256": "Gold-Audit",
            "source_samples_sha256": "training_samples.json",
            "expected_existing_registry_sha256": "Exportregister",
            "expected_existing_receipt_sha256": "DETECT_ALL-Beleg",
            "negative_images": "Negativbilder",
            "negative_sets": "Negativsatz",
        }
        label = next(
            (labels[name] for name in changed if name in labels),
            "Vorbereitungsgrundlage",
        )
        raise ValueError(
            f"{label} wurde zwischen Pruefung und Schreiben geaendert "
            f"({', '.join(changed)})."
        )


def execute_preparation(
    preparation: DetectPreparation,
    approved_by: str,
    approved_utc: datetime,
    *,
    renew_existing: bool = False,
) -> None:
    if not renew_existing:
        raise ValueError(
            "Schreiben ist nur mit --execute --renew-existing erlaubt."
        )
    if approved_utc.tzinfo is None or approved_utc.utcoffset() is None:
        raise ValueError("Der Freigabezeitpunkt braucht eine Zeitzone.")
    approved_utc = approved_utc.astimezone(timezone.utc)
    _revalidate_preparation(preparation, approved_by)
    root = preparation.registry_path.parent.parent.resolve(strict=True)
    expected_registry_path = _require_safe_storage_path(
        root / "training" / "export_registry_v1.json",
        root,
        "Das aktive Exportregister",
    )
    expected_receipt_path = _require_safe_storage_path(
        root
        / "training"
        / "pilots"
        / "DETECT_ALL"
        / "registry_setup_v1.json",
        root,
        "Der aktive DETECT_ALL-Beleg",
    )
    transaction_path = _require_safe_storage_path(
        _transaction_path(root),
        root,
        "Der DETECT_ALL-Transaktionsbeleg",
    )
    if (
        not _paths_equal(preparation.registry_path, expected_registry_path)
        or not _paths_equal(preparation.receipt_path, expected_receipt_path)
    ):
        raise ValueError("Die Vorbereitung zeigt nicht auf die aktiven DETECT_ALL-Dateien.")
    if transaction_path.exists():
        raise ValueError(
            "Ein unvollstaendiger DETECT_ALL-Wechsel muss zuerst "
            "wiederaufgenommen werden."
        )

    previous_registry_bytes = _read_expected_bytes(
        preparation.registry_path,
        preparation.expected_existing_registry_sha256,
        "Das Exportregister",
    )
    if previous_registry_bytes is None:
        raise FileNotFoundError(
            "DETECT_ALL erneuert nur ein bestehendes Exportregister."
        )
    previous_receipt_bytes = _read_expected_bytes(
        preparation.receipt_path,
        preparation.expected_existing_receipt_sha256,
        "Der DETECT_ALL-Beleg",
    )
    previous_registry_sha256 = _sha256_bytes(previous_registry_bytes)
    previous_receipt_sha256 = (
        _sha256_bytes(previous_receipt_bytes)
        if previous_receipt_bytes is not None
        else None
    )
    pilot_root = preparation.receipt_path.parent
    registry_archive = (
        pilot_root
        / "registry_history"
        / f"{previous_registry_sha256}.json"
    )
    registry_archive = _require_safe_storage_path(
        registry_archive,
        root,
        "Das Registry-Archiv",
    )
    _archive_exact_bytes(registry_archive, previous_registry_bytes)
    receipt_archive: Path | None = None
    if previous_receipt_bytes is not None:
        receipt_archive = (
            pilot_root
            / "receipt_history"
            / f"{previous_receipt_sha256}.json"
        )
        receipt_archive = _require_safe_storage_path(
            receipt_archive,
            root,
            "Das Receipt-Archiv",
        )
        _archive_exact_bytes(receipt_archive, previous_receipt_bytes)

    holding_roles: dict[str, str] = {}
    for sample in preparation.selected_samples:
        target_role = (
            "development_validation" if sample.role == "val" else "train"
        )
        previous_role = holding_roles.setdefault(sample.holding_key, target_role)
        if previous_role != target_role:
            raise ValueError(
                f"Haltung {sample.holding_key} hat widerspruechliche Exportrollen."
            )
    holding_roles = {
        key: holding_roles[key] for key in sorted(holding_roles, key=str.casefold)
    }
    approved_time = approved_utc.isoformat().replace("+00:00", "Z")
    registry: dict[str, Any] = {
        "schema_version": REGISTRY_SCHEMA_VERSION,
        "approval_status": "approved",
        "approved_by": approved_by.strip(),
        "approved_utc": approved_time,
        "approved_sample_ids": [
            sample.sample_id for sample in preparation.selected_samples
        ],
        "holding_roles": holding_roles,
        "protected_sets": list(preparation.protected_sets),
        "negative_images": list(preparation.negative_images),
    }
    registry_bytes = _json_bytes(registry)
    new_registry_sha256 = _sha256_bytes(registry_bytes)

    class_counts = Counter(
        sample.target_class for sample in preparation.selected_samples
    )
    receipt: dict[str, Any] = {
        "schema_version": RECEIPT_SCHEMA_VERSION,
        "purpose": RECEIPT_PURPOSE,
        "approval_status": "approved",
        "approved_by": approved_by.strip(),
        "approved_utc": approved_time,
        "source_samples_path": str(preparation.source_samples_path),
        "source_samples_sha256": preparation.source_samples_sha256,
        "source_gold_audit_path": str(preparation.source_audit_path),
        "source_gold_audit_sha256": preparation.source_audit_sha256,
        "class_map_path": str(preparation.class_map_path),
        "class_map_version": preparation.class_map_version,
        "class_map_sha256": preparation.class_map_sha256,
        "vsa_manifest_sha256": preparation.vsa_manifest_sha256,
        "migration_path": str(preparation.migration_path),
        "migration_version": preparation.migration_version,
        "migration_sha256": preparation.migration_sha256,
        "personal_gold_approval": {
            "schema_version": "1.0",
            "gold_audit_sha256": preparation.source_audit_sha256,
            "training_samples_sha256": preparation.source_samples_sha256,
            "approved_by": preparation.approved_by,
            "approved_utc": preparation.personal_gold_approved_utc,
            "source_codes": list(preparation.personal_gold_source_codes),
        },
        "previous_registry_sha256": previous_registry_sha256,
        "archived_registry_path": _relative_or_absolute(
            preparation.registry_path.parent.parent,
            registry_archive,
        ),
        "new_registry_sha256": new_registry_sha256,
        "previous_receipt_sha256": previous_receipt_sha256,
        "archived_receipt_path": _relative_or_absolute(
            preparation.registry_path.parent.parent,
            receipt_archive,
        ),
        "selected_images": len(preparation.selected_samples),
        "train_images": preparation.train_images,
        "validation_images": preparation.validation_images,
        "discarded_images": len(preparation.discarded_sample_ids),
        "test_images_excluded": len(preparation.excluded_test_sample_ids),
        "negative_images": len(preparation.negative_images),
        "negative_sets": list(preparation.negative_sets),
        "class_counts": {
            name: class_counts[name] for name in sorted(class_counts, key=str.casefold)
        },
        "discarded_sample_ids": list(preparation.discarded_sample_ids),
        "test_sample_ids_excluded": list(
            preparation.excluded_test_sample_ids
        ),
        "samples": [
            {
                "sample_id": sample.sample_id,
                "case_id": sample.case_id,
                "holding_key": sample.holding_key,
                "code": sample.code,
                "target_class": sample.target_class,
                "image_sha256": sample.image_sha256,
                "target": "validation" if sample.role == "val" else "train",
                "source_type": sample.source_type,
            }
            for sample in preparation.selected_samples
        ],
    }
    receipt_bytes = _json_bytes(receipt)
    new_receipt_sha256 = _sha256_bytes(receipt_bytes)
    transaction = {
        "schema_version": TRANSACTION_SCHEMA_VERSION,
        "purpose": TRANSACTION_PURPOSE,
        "registry_path": str(preparation.registry_path),
        "receipt_path": str(preparation.receipt_path),
        "previous_registry_sha256": previous_registry_sha256,
        "previous_registry_base64": base64.b64encode(
            previous_registry_bytes
        ).decode("ascii"),
        "previous_receipt_sha256": previous_receipt_sha256,
        "previous_receipt_base64": (
            base64.b64encode(previous_receipt_bytes).decode("ascii")
            if previous_receipt_bytes is not None
            else None
        ),
        "new_registry_sha256": new_registry_sha256,
        "new_receipt_sha256": new_receipt_sha256,
    }
    transaction_bytes = _json_bytes(transaction)

    registry_stage: Path | None = None
    receipt_stage: Path | None = None
    registry_replaced = False
    receipt_replaced = False
    transaction_published = False
    try:
        registry_stage = _stage_bytes(preparation.registry_path, registry_bytes)
        receipt_stage = _stage_bytes(preparation.receipt_path, receipt_bytes)

        _revalidate_preparation(preparation, approved_by)
        _require_unchanged(
            preparation.registry_path,
            previous_registry_bytes,
            "Das Exportregister",
        )
        _require_unchanged(
            preparation.receipt_path,
            previous_receipt_bytes,
            "Der DETECT_ALL-Beleg",
        )

        _publish_new_bytes(transaction_path, transaction_bytes)
        transaction_published = True
        if transaction_path.read_bytes() != transaction_bytes:
            raise RuntimeError(
                "Der DETECT_ALL-Transaktionsbeleg ist nicht bytegenau aktiv."
            )
        os.replace(receipt_stage, preparation.receipt_path)
        receipt_replaced = True
        _require_unchanged(
            preparation.registry_path,
            previous_registry_bytes,
            "Das Exportregister",
        )
        os.replace(registry_stage, preparation.registry_path)
        registry_replaced = True

        if _sha256_file(preparation.registry_path) != new_registry_sha256:
            raise RuntimeError("Das erneuerte Exportregister hat einen falschen Hash.")
        if preparation.receipt_path.read_bytes() != receipt_bytes:
            raise RuntimeError("Der neue DETECT_ALL-Beleg ist nicht bytegenau aktiv.")
    except Exception as error:
        rollback_errors: list[str] = []
        if registry_replaced:
            try:
                _rollback_path(
                    preparation.registry_path,
                    previous_registry_bytes,
                    registry_bytes,
                )
            except Exception as rollback_error:
                rollback_errors.append(str(rollback_error))
        if receipt_replaced:
            try:
                _rollback_path(
                    preparation.receipt_path,
                    previous_receipt_bytes,
                    receipt_bytes,
                )
            except Exception as rollback_error:
                rollback_errors.append(str(rollback_error))
        if transaction_published and not rollback_errors:
            try:
                transaction_path.unlink()
            except Exception as rollback_error:
                rollback_errors.append(str(rollback_error))
        if rollback_errors:
            raise RuntimeError(
                "DETECT_ALL-Wechsel fehlgeschlagen; Ruecksetzen ebenfalls "
                f"fehlgeschlagen: {' | '.join(rollback_errors)}"
            ) from error
        raise
    else:
        transaction_path.unlink()
    finally:
        if registry_stage is not None:
            registry_stage.unlink(missing_ok=True)
        if receipt_stage is not None:
            receipt_stage.unlink(missing_ok=True)


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Streng freigegebenes Mehrklassen-Detect-Register aus Gold vorbereiten."
        )
    )
    parser.add_argument(
        "--knowledge-root",
        type=Path,
        default=Path(os.getenv("SEWERSTUDIO_KNOWLEDGE_ROOT", r"C:\KI_BRAIN")),
    )
    parser.add_argument("--approved-by", default="Besitzer")
    parser.add_argument(
        "--gold-audit",
        type=Path,
        required=True,
        help=(
            "Expliziter aktueller Goldbestands-Audit Schema 1.1 unter "
            "<KnowledgeRoot>/training/reports."
        ),
    )
    parser.add_argument(
        "--execute",
        action="store_true",
        help="Exportregister und DETECT_ALL-Beleg wirklich erneuern.",
    )
    parser.add_argument(
        "--renew-existing",
        action="store_true",
        help=(
            "Bestehendes Register bytegenau archivieren und erneuern; "
            "nur zusammen mit --execute."
        ),
    )
    args = parser.parse_args()
    if args.execute != args.renew_existing:
        parser.error(
            "Schreiben ist nur mit --execute --renew-existing gemeinsam erlaubt."
        )
    return args


def main() -> int:
    args = _parse_args()
    if args.execute:
        recovery = recover_incomplete_transaction(args.knowledge_root)
        if recovery == "committed":
            print("Ein vollstaendiger DETECT_ALL-Wechsel wurde abgeschlossen.")
            return 0
        elif recovery == "rolled_back":
            print("Ein unvollstaendiger DETECT_ALL-Wechsel wurde zurueckgesetzt.")
    preparation = build_preparation(
        args.knowledge_root,
        args.approved_by,
        args.gold_audit,
    )
    print(f"Freigegebene Goldbilder: {len(preparation.selected_samples)}")
    print(f"Train: {preparation.train_images}")
    print(f"Validation: {preparation.validation_images}")
    print(f"Explizit verworfen: {len(preparation.discarded_sample_ids)}")
    print(f"Testbilder ausgeschlossen: {len(preparation.excluded_test_sample_ids)}")
    print(f"Strikte Negativbilder: {len(preparation.negative_images)}")
    print(f"Gold-Audit: {preparation.source_audit_path}")
    print(f"Migration: {preparation.migration_path}")
    print(f"Exportregister: {preparation.registry_path}")
    if not args.execute:
        print("Nur Pruefung. Es wurde nichts geschrieben.")
        return 0

    execute_preparation(
        preparation,
        args.approved_by,
        datetime.now(timezone.utc),
        renew_existing=True,
    )
    print(f"DETECT_ALL vorbereitet: {preparation.receipt_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
