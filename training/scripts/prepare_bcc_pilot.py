"""Bereitet den getrennten BCC-Bogen-Pilot aus persoenlichem Hand-Gold vor.

Das Skript schreibt keinen YOLO-Datensatz. Es erzeugt ausschliesslich das
menschlich gebundene Exportregister, das danach vom gemeinsamen C#-Exportplaner
gelesen wird. Ohne --execute bleibt der Lauf schreibfrei.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence


SCRIPT_ROOT = Path(__file__).resolve().parent
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

import gold_stock_audit as gold_audit_tools


PILOT_MAIN_CODE = "BCC"
PILOT_NAME = "BCC_bogen"
MINIMUM_IMAGES = 30
ALLOWED_MATCH_LEVELS = {"ReviewApproved", "ReviewCorrected"}
MIN_NEGATIVE_BYTES = 1024
GOLD_AUDIT_SCHEMA_VERSION = "1.1"
GOLD_AUDIT_REPORT_NAME = "gold_stock_audit"
GOLD_AUDIT_MODE = "schreibfreie_pruefung"
GOLD_SPLIT_SALT = "split-v1"
GOLD_TRAIN_SHARE = 0.70
GOLD_VALIDATION_SHARE = 0.15


@dataclass(frozen=True)
class PilotSample:
    sample_id: str
    case_id: str
    holding_key: str
    code: str
    frame_path: Path
    image_sha256: str
    confirmed_at_utc: str
    role: str


@dataclass(frozen=True)
class PilotPreparation:
    registry_path: Path
    audit_path: Path
    approved_by: str
    source_audit_path: Path
    source_audit_sha256: str
    source_samples_sha256: str
    expected_existing_registry_sha256: str | None
    selected_samples: tuple[PilotSample, ...]
    duplicate_sample_ids: tuple[str, ...]
    train_cases: tuple[str, ...]
    validation_cases: tuple[str, ...]
    protected_sets: tuple[dict[str, str], ...]
    excluded_test_sample_ids: tuple[str, ...]
    excluded_eval_sample_ids: tuple[str, ...]
    negatives_dir: Path
    negative_set_roots: tuple[Path, ...]
    negative_sets: tuple[dict[str, Any], ...] = ()
    negative_images: tuple[dict[str, Any], ...] = ()

    @property
    def train_images(self) -> int:
        return sum(sample.role == "train" for sample in self.selected_samples)

    @property
    def validation_images(self) -> int:
        return sum(sample.role == "val" for sample in self.selected_samples)


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _load_json_array(path: Path) -> list[dict[str, Any]]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, list):
        raise ValueError(f"{path} muss ein JSON-Array enthalten.")
    if any(not isinstance(item, dict) for item in value):
        raise ValueError(f"{path} enthaelt einen ungueltigen Eintrag.")
    return value


def _is_personal_complete_bcc(item: dict[str, Any], approved_by: str) -> bool:
    status = item.get("Status")
    approved_status = status == 1 or status == "Approved"
    return (
        approved_status
        and item.get("HumanConfirmed") is True
        and item.get("Corrected") is not None
        and str(item.get("ConfirmedByUser") or "").strip().casefold()
        == approved_by.strip().casefold()
        and str(item.get("SourceType") or "").strip().casefold() == "manualcoding"
        and str(item.get("MatchLevel") or "").strip() in ALLOWED_MATCH_LEVELS
        and item.get("HasBbox") is True
        and item.get("HasSamMask") is True
        and str(item.get("Code") or "").strip().upper().startswith(PILOT_MAIN_CODE)
    )


def _is_within(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def _load_json_object(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"{path} muss ein JSON-Objekt enthalten.")
    return value


def _require_sha256(value: Any, label: str) -> str:
    text = str(value or "").strip().casefold()
    if len(text) != 64 or any(character not in "0123456789abcdef" for character in text):
        raise ValueError(f"{label} ist kein gueltiger SHA-256.")
    return text


def _paths_equal(left: Path, right: Path) -> bool:
    return os.path.normcase(str(left.resolve())) == os.path.normcase(str(right.resolve()))


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


def _read_audit_samples(
    knowledge_root: Path,
    approved_by: str,
    audit_path: Path,
    negatives_dir: Path,
    negative_sets: Sequence[Path],
) -> tuple[
    list[PilotSample],
    tuple[str, ...],
    tuple[str, ...],
    str,
    str | None,
    tuple[dict[str, Any], ...],
    tuple[dict[str, Any], ...],
]:
    root = knowledge_root.resolve()
    reports_root = (root / "training" / "reports").resolve()
    resolved_audit = audit_path.resolve()
    if not resolved_audit.is_file() or not _is_within(resolved_audit, reports_root):
        raise ValueError(
            "Der Gold-Audit muss als vorhandener Bericht unter "
            f"{reports_root} liegen."
        )

    document = _load_json_object(resolved_audit)
    if document.get("schema_version") != GOLD_AUDIT_SCHEMA_VERSION:
        raise ValueError(
            f"Der Gold-Audit braucht Schema {GOLD_AUDIT_SCHEMA_VERSION}."
        )
    if document.get("bericht") != GOLD_AUDIT_REPORT_NAME:
        raise ValueError("Die angegebene Datei ist kein Goldbestands-Audit.")
    if document.get("modus") != GOLD_AUDIT_MODE:
        raise ValueError("Der Gold-Audit ist kein schreibfreier Pruefbericht.")

    inputs = document.get("eingaben")
    if not isinstance(inputs, dict):
        raise ValueError("Der Gold-Audit enthaelt keine gueltigen Eingaben.")
    audit_user = str(inputs.get("approved_by") or "").strip()
    if audit_user.casefold() != approved_by.strip().casefold():
        raise ValueError(
            f"Der Gold-Audit ist fuer '{audit_user}' statt '{approved_by.strip()}' freigegeben."
        )

    samples_path = (root / "training_samples.json").resolve()
    audit_samples_path = Path(str(inputs.get("samples_pfad") or ""))
    if not _paths_equal(audit_samples_path, samples_path):
        raise ValueError(
            f"Der Gold-Audit gehoert nicht zur aktiven training_samples.json: {samples_path}"
        )
    samples_sha256 = _require_sha256(
        inputs.get("samples_sha256"),
        "samples_sha256 im Gold-Audit",
    )
    current_samples_sha256 = _sha256_file(samples_path)
    if samples_sha256 != current_samples_sha256:
        raise ValueError(
            "training_samples.json wurde nach dem Gold-Audit geaendert. "
            "Bitte zuerst einen neuen Gold-Audit erzeugen."
        )

    registry_path = (root / "training" / "export_registry_v1.json").resolve()
    audit_registry_path = Path(str(inputs.get("registry_pfad") or ""))
    if not _paths_equal(audit_registry_path, registry_path):
        raise ValueError(
            f"Der Gold-Audit gehoert nicht zum aktiven Exportregister: {registry_path}"
        )
    expected_registry_sha256: str | None
    if registry_path.is_file():
        expected_registry_sha256 = _require_sha256(
            inputs.get("registry_sha256"),
            "registry_sha256 im Gold-Audit",
        )
        if expected_registry_sha256 != _sha256_file(registry_path):
            raise ValueError(
                "Das Exportregister wurde nach dem Gold-Audit geaendert. "
                "Bitte zuerst einen neuen Gold-Audit erzeugen."
            )
    else:
        expected_registry_sha256 = None
        if inputs.get("registry_sha256") not in (None, ""):
            raise ValueError(
                "Der Gold-Audit erwartet ein Exportregister, das nicht mehr vorhanden ist."
            )

    split = document.get("split")
    if not isinstance(split, dict) or split.get("release_faehig") is not True:
        raise ValueError(
            "Der Gold-Audit ist wegen fehlender Haltungsidentitaet nicht release-faehig."
        )
    if split.get("test_eingefroren_nur_markiert") is not True:
        raise ValueError(
            "Der Gold-Audit bestaetigt den eingefrorenen Test-Split nicht."
        )

    raw_samples = _load_json_array(samples_path)
    source_by_id: dict[str, dict[str, Any]] = {}
    for item in raw_samples:
        sample_id = str(item.get("SampleId") or "").strip()
        if not sample_id:
            continue
        if sample_id in source_by_id:
            raise ValueError(f"SampleId ist mehrfach vorhanden: {sample_id}")
        source_by_id[sample_id] = item

    audit_samples = document.get("samples")
    if not isinstance(audit_samples, list) or any(
        not isinstance(item, dict) for item in audit_samples
    ):
        raise ValueError("Der Gold-Audit enthaelt keine gueltige Sampleliste.")

    gold_root = (root / "gold_frames" / "BCC - Bogen").resolve()
    selected: list[PilotSample] = []
    test_ids: list[str] = []
    seen_image_hashes: dict[str, str] = {}
    holding_roles: dict[str, str] = {}
    for entry in audit_samples:
        if str(entry.get("hauptcode") or "").strip().upper() != PILOT_MAIN_CODE:
            continue

        sample_id = str(entry.get("sample_id") or "").strip()
        case_id = str(entry.get("case_id") or "").strip()
        holding_key = str(entry.get("haltung_key") or "").strip()
        code = str(entry.get("code") or "").strip().upper()
        role = str(entry.get("rolle") or "").strip().casefold()
        group_key = str(entry.get("gruppe") or "").strip()
        if not sample_id or not case_id or not holding_key:
            raise ValueError(
                f"BCC-Sample {sample_id or '(ohne ID)'} besitzt keine belastbare Haltungsidentitaet."
            )
        if role not in {"train", "val", "test"}:
            raise ValueError(f"BCC-Sample {sample_id} hat eine unbekannte Audit-Rolle: {role}")
        if not group_key or role != _expected_split_role(group_key):
            raise ValueError(
                f"BCC-Sample {sample_id} hat keine belastbare, deterministische Audit-Rolle."
            )

        source = source_by_id.get(sample_id)
        if source is None:
            raise ValueError(f"BCC-Sample aus dem Audit fehlt in training_samples.json: {sample_id}")
        if not _is_personal_complete_bcc(source, approved_by):
            raise ValueError(f"BCC-Sample ist nicht mehr vollstaendig freigegeben: {sample_id}")
        if str(source.get("CaseId") or "").strip() != case_id:
            raise ValueError(f"CaseId von BCC-Sample {sample_id} weicht vom Gold-Audit ab.")
        if str(source.get("Code") or "").strip().upper() != code:
            raise ValueError(f"Code von BCC-Sample {sample_id} weicht vom Gold-Audit ab.")

        frame_text = str(source.get("FramePath") or "").strip()
        frame_path = Path(frame_text)
        if not frame_path.is_file():
            raise ValueError(f"Goldbild fehlt: {frame_path}")
        resolved_frame = frame_path.resolve()
        if not _is_within(resolved_frame, gold_root):
            raise ValueError(f"BCC-Pilotbild liegt nicht im BCC-Goldordner: {frame_path}")
        image_sha256 = _sha256_file(resolved_frame)
        audit_image_sha256 = _require_sha256(
            entry.get("image_sha256"),
            f"Bild-Hash von {sample_id}",
        )
        if image_sha256 != audit_image_sha256:
            raise ValueError(f"Bild-Hash von BCC-Sample {sample_id} weicht vom Gold-Audit ab.")

        previous_sample_id = seen_image_hashes.get(image_sha256)
        if previous_sample_id is not None:
            raise ValueError(
                "Der Gold-Audit enthaelt dasselbe BCC-Bild mehrfach: "
                f"{previous_sample_id}, {sample_id}."
            )
        seen_image_hashes[image_sha256] = sample_id
        previous_role = holding_roles.get(holding_key)
        if previous_role is not None and previous_role != role:
            raise ValueError(
                f"Haltung {holding_key} hat widerspruechliche Rollen: "
                f"{previous_role}, {role}."
            )
        holding_roles[holding_key] = role

        if role == "test":
            test_ids.append(sample_id)
            continue

        selected.append(
            PilotSample(
                sample_id=sample_id,
                case_id=case_id,
                holding_key=holding_key,
                code=code,
                frame_path=resolved_frame,
                image_sha256=image_sha256,
                confirmed_at_utc=str(source.get("ConfirmedAtUtc") or ""),
                role=role,
            )
        )

    selected.sort(key=lambda sample: sample.sample_id.casefold())
    if len(selected) < MINIMUM_IMAGES:
        raise ValueError(
            f"Der BCC-Pilot braucht mindestens {MINIMUM_IMAGES} verschiedene "
            f"Train-/Pruefbilder; gefunden wurden {len(selected)}."
        )
    if not any(sample.role == "train" for sample in selected) or not any(
        sample.role == "val" for sample in selected
    ):
        raise ValueError("Der BCC-Pilot braucht mindestens eine Train- und eine Val-Haltung.")

    rejection_entries = document.get("verwerfungen")
    eval_ids: list[str] = []
    if isinstance(rejection_entries, list):
        for rejection in rejection_entries:
            if not isinstance(rejection, dict) or rejection.get("stufe") != "eval_sauber":
                continue
            rejected_id = str(rejection.get("sample_id") or "").strip()
            source = source_by_id.get(rejected_id)
            if source is not None and str(source.get("Code") or "").strip().upper().startswith(
                PILOT_MAIN_CODE
            ):
                eval_ids.append(rejected_id)

    negative_images, negative_set_provenance = (
        gold_audit_tools.read_training_negative_sources(
            root,
            negatives_dir,
            negative_sets,
            minimum_legacy_bytes=MIN_NEGATIVE_BYTES,
        )
    )
    if negative_set_provenance and any(
        image.get("source_type") is None for image in negative_images
    ):
        raise ValueError(
            "Ein neues Exportregister darf alte Legacy-Negative und streng "
            "reviewte Negativsaetze nicht mischen. Fuer den strikten Lauf bitte "
            "einen leeren oder nicht vorhandenen --negatives-dir verwenden."
        )
    audit_negatives_path = Path(str(inputs.get("negatives_pfad") or ""))
    if not _paths_equal(audit_negatives_path, negatives_dir):
        raise ValueError(
            "Der Gold-Audit gehoert nicht zum gewaehlten Negativ-Pool: "
            f"{negatives_dir}"
        )
    raw_audit_set_paths = inputs.get("negative_set_pfade", [])
    if (
        not isinstance(raw_audit_set_paths, list)
        or any(not isinstance(path, str) or not path.strip() for path in raw_audit_set_paths)
        or len(raw_audit_set_paths) != len(negative_sets)
    ):
        raise ValueError("Der Gold-Audit enthaelt keine gueltigen Negativsatz-Pfade.")
    for audit_set_path, current_set_path in zip(
        raw_audit_set_paths,
        negative_sets,
        strict=True,
    ):
        if not _paths_equal(Path(audit_set_path), Path(current_set_path)):
            raise ValueError(
                "Der Gold-Audit gehoert nicht zu den gewaehlten Negativsaetzen."
            )
    audit_negative_pool = document.get("negativ_pool")
    if not isinstance(audit_negative_pool, dict):
        raise ValueError("Der Gold-Audit enthaelt keinen gueltigen Negativ-Pool.")
    audit_negative_entries = audit_negative_pool.get("dateien")
    if (
        not isinstance(audit_negative_entries, list)
        or any(not isinstance(entry, dict) for entry in audit_negative_entries)
        or audit_negative_pool.get("anzahl") != len(audit_negative_entries)
    ):
        raise ValueError("Der Negativ-Pool im Gold-Audit ist ungueltig.")
    if len(audit_negative_entries) != len(negative_images):
        raise ValueError(
            "Der Negativ-Pool wurde nach dem Gold-Audit geaendert. "
            "Bitte zuerst einen neuen Gold-Audit erzeugen."
        )
    audit_legacy_entries = [
        entry
        for entry in audit_negative_entries
        if entry.get("source_type") is None
    ]
    audit_strict_entries = [
        entry
        for entry in audit_negative_entries
        if entry.get("source_type") == "reviewed_negative_set"
    ]
    if len(audit_legacy_entries) + len(audit_strict_entries) != len(
        audit_negative_entries
    ):
        raise ValueError("Der Gold-Audit enthaelt eine unbekannte Negativquelle.")
    audit_negative_hashes = sorted(
        _require_sha256(entry.get("sha256"), "Negativbild-Hash im Gold-Audit")
        for entry in audit_legacy_entries
    )
    if len(audit_negative_hashes) != len(set(audit_negative_hashes)):
        raise ValueError("Der Gold-Audit enthaelt doppelte Negativbilder.")
    current_legacy = [
        entry
        for entry in negative_images
        if entry.get("source_type") is None
    ]
    current_strict = [
        entry
        for entry in negative_images
        if entry.get("source_type") == "reviewed_negative_set"
    ]
    current_negative_hashes = sorted(entry["sha256"] for entry in current_legacy)
    if audit_negative_hashes != current_negative_hashes:
        raise ValueError(
            "Der Negativ-Pool wurde nach dem Gold-Audit geaendert. "
            "Bitte zuerst einen neuen Gold-Audit erzeugen."
        )
    expected_strict_entries = [
        {"datei": image["path"]}
        | {key: value for key, value in image.items() if key != "path"}
        for image in current_strict
    ]
    if sorted(
        audit_strict_entries,
        key=lambda entry: str(entry.get("sha256")),
    ) != sorted(
        expected_strict_entries,
        key=lambda entry: str(entry.get("sha256")),
    ):
        raise ValueError(
            "Die Negativsatz-Bildprovenienz weicht vom Gold-Audit ab."
        )
    audit_set_provenance = audit_negative_pool.get("sets", [])
    if (
        not isinstance(audit_set_provenance, list)
        or audit_set_provenance != list(negative_set_provenance)
    ):
        raise ValueError(
            "Die Negativsatz-Provenienz weicht vom Gold-Audit ab."
        )

    return (
        selected,
        tuple(sorted(test_ids, key=str.casefold)),
        tuple(sorted(set(eval_ids), key=str.casefold)),
        current_samples_sha256,
        expected_registry_sha256,
        negative_images,
        negative_set_provenance,
    )


def _discover_protected_sets(knowledge_root: Path) -> tuple[dict[str, str], ...]:
    subsets_root = knowledge_root / "eval_set" / "subsets"
    result: list[dict[str, str]] = []
    for manifest_path in sorted(subsets_root.glob("*/_manifest.json")):
        set_root = manifest_path.parent
        document = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        if not isinstance(document, dict) or document.get("frozen") is not True:
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


def _read_negatives(
    knowledge_root: Path,
    negatives_dir: Path,
) -> tuple[dict[str, Any], ...]:
    """Liest menschlich kuratierte Negativ-/Hintergrundbilder (schadensfrei).

    Nur jpg/jpeg/png direkt in der Wurzel (nicht rekursiv), lesbar und mit
    Mindestgroesse. Fehlender oder leerer Ordner ist KEIN Fehler — dann bleibt
    das Register ohne 'negative_images' (bisheriges Verhalten).
    """
    images, _ = gold_audit_tools.read_training_negative_sources(
        knowledge_root,
        negatives_dir,
        minimum_legacy_bytes=MIN_NEGATIVE_BYTES,
    )
    return images


def build_preparation(
    knowledge_root: Path,
    approved_by: str,
    gold_audit_path: Path,
    negatives_dir: Path | None = None,
    negative_sets: Sequence[Path] = (),
) -> PilotPreparation:
    root = knowledge_root.resolve()
    if not approved_by.strip():
        raise ValueError("Die freigebende Person fehlt.")
    negatives_path = (
        negatives_dir.resolve()
        if negatives_dir is not None
        else root / "training" / "negatives" / "bcc_pilot"
    )
    negative_set_paths = tuple(
        Path(os.path.abspath(path)) for path in negative_sets
    )
    (
        samples,
        excluded_test_sample_ids,
        excluded_eval_sample_ids,
        source_samples_sha256,
        expected_existing_registry_sha256,
        negative_images,
        negative_set_provenance,
    ) = _read_audit_samples(
        root,
        approved_by,
        gold_audit_path,
        negatives_path,
        negative_set_paths,
    )
    train_cases = tuple(
        sorted(
            {sample.holding_key for sample in samples if sample.role == "train"},
            key=str.casefold,
        )
    )
    validation_cases = tuple(
        sorted(
            {sample.holding_key for sample in samples if sample.role == "val"},
            key=str.casefold,
        )
    )
    protected_sets = _discover_protected_sets(root)
    pilot_root = root / "training" / "pilots" / PILOT_MAIN_CODE
    resolved_audit = gold_audit_path.resolve()
    return PilotPreparation(
        registry_path=root / "training" / "export_registry_v1.json",
        audit_path=pilot_root / "pilot_setup_v1.json",
        approved_by=approved_by.strip(),
        source_audit_path=resolved_audit,
        source_audit_sha256=_sha256_file(resolved_audit),
        source_samples_sha256=source_samples_sha256,
        expected_existing_registry_sha256=expected_existing_registry_sha256,
        selected_samples=tuple(samples),
        duplicate_sample_ids=(),
        train_cases=train_cases,
        validation_cases=validation_cases,
        protected_sets=protected_sets,
        excluded_test_sample_ids=excluded_test_sample_ids,
        excluded_eval_sample_ids=excluded_eval_sample_ids,
        negatives_dir=negatives_path,
        negative_set_roots=negative_set_paths,
        negative_sets=negative_set_provenance,
        negative_images=negative_images,
    )


def _json_bytes(document: dict[str, Any]) -> bytes:
    return (json.dumps(document, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def _stage_bytes(path: Path, data: bytes) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{os.getpid()}.{uuid.uuid4().hex}.tmp")
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
                f"Das Hash-Archiv wurde gleichzeitig mit anderem Inhalt angelegt: {path}"
            )


def _read_expected_registry_bytes(preparation: PilotPreparation) -> bytes | None:
    expected_hash = preparation.expected_existing_registry_sha256
    path = preparation.registry_path
    if expected_hash is None:
        if path.exists():
            raise ValueError(
                "Das Exportregister wurde nach der Pruefung zwischenzeitlich angelegt."
            )
        return None

    if not path.is_file():
        raise ValueError(
            "Das Exportregister wurde nach der Pruefung zwischenzeitlich entfernt."
        )
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != expected_hash:
        raise ValueError(
            "Das Exportregister wurde nach der Pruefung zwischenzeitlich geaendert."
        )
    return data


def _require_unchanged(path: Path, expected: bytes | None, label: str) -> None:
    current = path.read_bytes() if path.is_file() else None
    if current != expected:
        raise ValueError(f"{label} wurde zwischenzeitlich geaendert.")


def _revalidate_preparation_inputs(
    preparation: PilotPreparation,
    approved_by: str,
) -> None:
    if approved_by.strip().casefold() != preparation.approved_by.casefold():
        raise ValueError(
            f"Die Vorbereitung ist fuer '{preparation.approved_by}' statt "
            f"'{approved_by.strip()}' freigegeben."
        )

    knowledge_root = preparation.registry_path.parent.parent.resolve()
    samples_path = knowledge_root / "training_samples.json"
    if (
        not samples_path.is_file()
        or _sha256_file(samples_path) != preparation.source_samples_sha256
    ):
        raise ValueError(
            "training_samples.json wurde zwischen Pruefung und Schreiben geaendert."
        )
    if (
        not preparation.source_audit_path.is_file()
        or _sha256_file(preparation.source_audit_path)
        != preparation.source_audit_sha256
    ):
        raise ValueError(
            "Der Gold-Audit wurde zwischen Pruefung und Schreiben geaendert."
        )

    selected_ids = {sample.sample_id for sample in preparation.selected_samples}
    protected_ids = set(preparation.excluded_test_sample_ids) | set(
        preparation.excluded_eval_sample_ids
    )
    leaked_ids = sorted(selected_ids & protected_ids, key=str.casefold)
    if leaked_ids:
        raise ValueError(
            "Geschuetzte Test-/Eval-Samples duerfen nicht im BCC-Register stehen: "
            + ", ".join(leaked_ids)
        )

    for sample in preparation.selected_samples:
        if (
            not sample.frame_path.is_file()
            or _sha256_file(sample.frame_path) != sample.image_sha256
        ):
            raise ValueError(
                "BCC-Goldbild wurde zwischen Pruefung und Schreiben geaendert: "
                f"{sample.sample_id}"
            )

    current_negative_images, current_negative_sets = (
        gold_audit_tools.read_training_negative_sources(
            knowledge_root,
            preparation.negatives_dir,
            preparation.negative_set_roots,
            minimum_legacy_bytes=MIN_NEGATIVE_BYTES,
        )
    )
    if (
        current_negative_images != preparation.negative_images
        or current_negative_sets != preparation.negative_sets
    ):
        raise ValueError(
            "Negativbilder oder ihre Provenienz wurden zwischen Pruefung und "
            "Schreiben geaendert."
        )

    for protected_set in preparation.protected_sets:
        stored_root = Path(protected_set["root_path"])
        set_root = (
            stored_root
            if stored_root.is_absolute()
            else knowledge_root / stored_root
        )
        manifest_path = set_root / "_manifest.json"
        if (
            not manifest_path.is_file()
            or _sha256_file(manifest_path) != protected_set["manifest_sha256"]
        ):
            raise ValueError(
                "Ein geschuetztes Eval-Manifest wurde zwischen Pruefung und "
                f"Schreiben geaendert: {manifest_path}"
            )


def _rollback_path(
    path: Path,
    previous_bytes: bytes | None,
    expected_current_bytes: bytes,
) -> None:
    current = path.read_bytes() if path.is_file() else None
    if current != expected_current_bytes:
        raise RuntimeError(
            f"{path} konnte nicht sicher zurueckgesetzt werden, weil sich die Datei "
            "erneut geaendert hat."
        )
    if previous_bytes is None:
        path.unlink()
    else:
        _atomic_replace_bytes(path, previous_bytes)


def execute_preparation(
    preparation: PilotPreparation,
    approved_by: str,
    approved_utc: datetime,
    renew_existing: bool = False,
) -> None:
    if approved_utc.tzinfo is None or approved_utc.utcoffset() is None:
        raise ValueError("Der Freigabezeitpunkt braucht eine Zeitzone.")
    approved_utc = approved_utc.astimezone(timezone.utc)
    _revalidate_preparation_inputs(preparation, approved_by)
    previous_registry_bytes = _read_expected_registry_bytes(preparation)
    if previous_registry_bytes is not None and not renew_existing:
        raise FileExistsError(
            f"Das Exportregister existiert bereits und wurde nicht ueberschrieben: "
            f"{preparation.registry_path}"
        )
    if previous_registry_bytes is None and renew_existing:
        raise FileNotFoundError(
            "Eine Erneuerung wurde verlangt, aber es gibt kein bestehendes "
            f"Exportregister: {preparation.registry_path}"
        )

    knowledge_root = preparation.registry_path.parent.parent.resolve()
    pilot_root = preparation.audit_path.parent
    previous_registry_sha256 = (
        hashlib.sha256(previous_registry_bytes).hexdigest()
        if previous_registry_bytes is not None
        else None
    )
    previous_audit_bytes = (
        preparation.audit_path.read_bytes()
        if preparation.audit_path.is_file()
        else None
    )
    previous_audit_sha256 = (
        hashlib.sha256(previous_audit_bytes).hexdigest()
        if previous_audit_bytes is not None
        else None
    )

    archived_registry_path: Path | None = None
    if previous_registry_bytes is not None:
        archived_registry_path = (
            pilot_root
            / "registry_history"
            / f"{previous_registry_sha256}.json"
        )
        _archive_exact_bytes(archived_registry_path, previous_registry_bytes)

    archived_audit_path: Path | None = None
    if previous_audit_bytes is not None:
        archived_audit_path = (
            pilot_root
            / "audit_history"
            / f"{previous_audit_sha256}.json"
        )
        _archive_exact_bytes(archived_audit_path, previous_audit_bytes)

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
        holding_key: holding_roles[holding_key]
        for holding_key in sorted(holding_roles, key=str.casefold)
    }
    registry = {
        "schema_version": "1.0",
        "approval_status": "approved",
        "approved_by": approved_by.strip(),
        "approved_utc": approved_utc.isoformat().replace("+00:00", "Z"),
        "approved_sample_ids": sorted(
            (sample.sample_id for sample in preparation.selected_samples),
            key=str.casefold,
        ),
        "holding_roles": holding_roles,
        "protected_sets": list(preparation.protected_sets),
    }
    # Additiv: das Feld wird nur geschrieben, wenn kuratierte Negative vorliegen —
    # Alt-Registrys und der strikte C#-Leser bleiben kompatibel.
    if preparation.negative_images:
        registry["negative_images"] = list(preparation.negative_images)
    registry_bytes = _json_bytes(registry)
    new_registry_sha256 = hashlib.sha256(registry_bytes).hexdigest()

    def relative_or_absolute(path: Path | None) -> str | None:
        if path is None:
            return None
        try:
            return path.resolve().relative_to(knowledge_root).as_posix()
        except ValueError:
            return str(path.resolve())

    audit = {
        "schema_version": "1.1",
        "pilot": PILOT_NAME,
        "created_utc": approved_utc.isoformat().replace("+00:00", "Z"),
        "approved_by": approved_by.strip(),
        "source": str(knowledge_root / "training_samples.json"),
        "source_samples_sha256": preparation.source_samples_sha256,
        "source_gold_audit_path": str(preparation.source_audit_path),
        "source_gold_audit_sha256": preparation.source_audit_sha256,
        "previous_registry_sha256": previous_registry_sha256,
        "archived_registry_path": relative_or_absolute(archived_registry_path),
        "new_registry_sha256": new_registry_sha256,
        "previous_pilot_audit_sha256": previous_audit_sha256,
        "archived_pilot_audit_path": relative_or_absolute(archived_audit_path),
        "selected_images": len(preparation.selected_samples),
        "train_images": preparation.train_images,
        "validation_images": preparation.validation_images,
        "negative_images": len(preparation.negative_images),
        "negative_sets": list(preparation.negative_sets),
        "duplicate_sample_ids_excluded": list(preparation.duplicate_sample_ids),
        "test_sample_ids_excluded": list(preparation.excluded_test_sample_ids),
        "eval_sample_ids_excluded": list(preparation.excluded_eval_sample_ids),
        "samples": [
            {
                "sample_id": sample.sample_id,
                "case_id": sample.case_id,
                "holding_key": sample.holding_key,
                "code": sample.code,
                "image_sha256": sample.image_sha256,
                "target": "validation" if sample.role == "val" else "train",
            }
            for sample in preparation.selected_samples
        ],
    }
    audit_bytes = _json_bytes(audit)
    registry_stage: Path | None = None
    audit_stage: Path | None = None
    registry_replaced = False
    audit_replaced = False
    try:
        registry_stage = _stage_bytes(preparation.registry_path, registry_bytes)
        audit_stage = _stage_bytes(preparation.audit_path, audit_bytes)

        # Direkt vor dem Wechsel nochmals pruefen, damit keine spaete Aenderung
        # unbemerkt ueberschrieben wird.
        _revalidate_preparation_inputs(preparation, approved_by)
        _require_unchanged(
            preparation.registry_path,
            previous_registry_bytes,
            "Das Exportregister",
        )
        _require_unchanged(
            preparation.audit_path,
            previous_audit_bytes,
            "Der BCC-Pilotbeleg",
        )

        # Der Beleg wird zuerst aktiviert, das produktiv gelesene Register zuletzt.
        os.replace(audit_stage, preparation.audit_path)
        audit_replaced = True
        _require_unchanged(
            preparation.registry_path,
            previous_registry_bytes,
            "Das Exportregister",
        )
        os.replace(registry_stage, preparation.registry_path)
        registry_replaced = True

        if _sha256_file(preparation.registry_path) != new_registry_sha256:
            raise RuntimeError("Das erneuerte Exportregister hat einen falschen Hash.")
        if preparation.audit_path.read_bytes() != audit_bytes:
            raise RuntimeError("Der neue BCC-Pilotbeleg ist nicht bytegenau aktiv.")
    except Exception as exc:
        rollback_errors: list[str] = []
        if registry_replaced:
            try:
                _rollback_path(
                    preparation.registry_path,
                    previous_registry_bytes,
                    registry_bytes,
                )
            except Exception as rollback_exc:
                rollback_errors.append(str(rollback_exc))
        if audit_replaced:
            try:
                _rollback_path(
                    preparation.audit_path,
                    previous_audit_bytes,
                    audit_bytes,
                )
            except Exception as rollback_exc:
                rollback_errors.append(str(rollback_exc))
        if rollback_errors:
            raise RuntimeError(
                "Registry-Wechsel fehlgeschlagen; Ruecksetzen ebenfalls "
                f"fehlgeschlagen: {' | '.join(rollback_errors)}"
            ) from exc
        raise
    finally:
        if registry_stage is not None:
            registry_stage.unlink(missing_ok=True)
        if audit_stage is not None:
            audit_stage.unlink(missing_ok=True)


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="BCC-Bogen-Pilot aus persoenlichem Gold vorbereiten."
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
            "Expliziter, aktueller Goldbestands-Audit unter "
            "<KnowledgeRoot>/training/reports."
        ),
    )
    parser.add_argument(
        "--negatives-dir",
        type=Path,
        default=None,
        help=(
            "Legacy-Ordner mit menschlich kuratierten Negativ-/Hintergrundbildern "
            "(Default: <KnowledgeRoot>/training/negatives/bcc_pilot). "
            "Darf nicht mit --negative-set gemischt werden; fuer einen strikten "
            "Satzlauf einen leeren oder nicht vorhandenen Ordner angeben."
        ),
    )
    parser.add_argument(
        "--negative-set",
        type=Path,
        action="append",
        default=[],
        help=(
            "Expliziter, veroeffentlichter Negativsatz unter "
            "<KnowledgeRoot>/training/negatives/sets; wiederholbar und ohne "
            "stillen Fallback."
        ),
    )
    parser.add_argument(
        "--execute",
        action="store_true",
        help="Exportregister und Auditdatei wirklich schreiben.",
    )
    parser.add_argument(
        "--renew-existing",
        action="store_true",
        help=(
            "Bestehendes Register nach Hash-Pruefung bytegenau archivieren und "
            "kontrolliert erneuern (nur zusammen mit --execute)."
        ),
    )
    args = parser.parse_args()
    if args.renew_existing and not args.execute:
        parser.error("--renew-existing ist nur zusammen mit --execute erlaubt.")
    return args


def main() -> int:
    args = _parse_args()
    preparation = build_preparation(
        args.knowledge_root,
        args.approved_by,
        args.gold_audit,
        args.negatives_dir,
        args.negative_set,
    )
    print(f"BCC-Goldbilder: {len(preparation.selected_samples)}")
    print(f"Train: {preparation.train_images}")
    print(f"Pruefung: {preparation.validation_images}")
    print(f"Negativbilder: {len(preparation.negative_images)}")
    print(f"Testbilder strikt ausgeschlossen: {len(preparation.excluded_test_sample_ids)}")
    print(f"Eval-Treffer strikt ausgeschlossen: {len(preparation.excluded_eval_sample_ids)}")
    print(f"Doppelte Bildinhalte ausgelassen: {len(preparation.duplicate_sample_ids)}")
    print(f"Geschuetzte Dev-Val-Sets: {len(preparation.protected_sets)}")
    print(f"Gold-Audit: {preparation.source_audit_path}")
    print(f"Exportregister: {preparation.registry_path}")
    if not args.execute:
        print("Nur Pruefung. Es wurde nichts geschrieben.")
        return 0

    execute_preparation(
        preparation,
        args.approved_by,
        datetime.now(timezone.utc),
        renew_existing=args.renew_existing,
    )
    print(f"BCC-Pilot vorbereitet: {preparation.audit_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
