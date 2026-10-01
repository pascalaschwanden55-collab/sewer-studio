#!/usr/bin/env python3
"""Repariert fehlende Haltungs-IDs in persoenlich bestaetigten Goldsamples.

Die Zuordnung erfolgt ausschliesslich ueber bytegleichen SHA-256-Abgleich mit
einem Quellenordner, dessen Dateinamen die Haltung enthalten. Ohne ``--execute``
bleibt der Lauf schreibfrei. Im Ausfuehrungsmodus werden Gold-JSON,
Teacher-JSON und SQLite gemeinsam gesichert, geprueft und aktualisiert.
Kundenbilder werden nie veraendert.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sqlite3
import subprocess
import sys
import tempfile
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from typing import Any, Iterable

import gold_stock_audit


IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png"}
SOURCE_NAME_PATTERN = re.compile(
    r"^(?P<holding>.+?)_p\d+_img\d+(?:_|$)",
    re.IGNORECASE,
)
REPARSE_POINT_ATTRIBUTE = 0x400


@dataclass(frozen=True)
class HoldingRepair:
    sample_id: str
    old_case_id: str
    new_case_id: str
    code: str
    image_sha256: str
    source_file: str
    old_signature: str
    new_signature: str


@dataclass(frozen=True)
class RepairPlan:
    knowledge_root: Path
    source_images: Path
    samples_path: Path
    teacher_path: Path
    database_path: Path
    samples_bytes: bytes
    teacher_bytes: bytes
    samples: list[dict[str, Any]]
    teachers: list[dict[str, Any]]
    repairs: tuple[HoldingRepair, ...]


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _is_link_or_reparse(path: Path) -> bool:
    try:
        stat = path.lstat()
    except OSError:
        return True
    return path.is_symlink() or bool(
        getattr(stat, "st_file_attributes", 0) & REPARSE_POINT_ATTRIBUTE
    )


def _require_plain_directory(path: Path, label: str) -> Path:
    resolved = path.resolve(strict=True)
    if not resolved.is_dir() or _is_link_or_reparse(path):
        raise ValueError(f"{label} ist kein sicherer normaler Ordner: {path}")
    return resolved


def _require_plain_file(path: Path, label: str) -> Path:
    resolved = path.resolve(strict=True)
    if not resolved.is_file() or _is_link_or_reparse(path):
        raise ValueError(f"{label} ist keine sichere normale Datei: {path}")
    return resolved


def _require_below(path: Path, root: Path, label: str) -> Path:
    resolved = _require_plain_file(path, label)
    try:
        resolved.relative_to(root)
    except ValueError as exc:
        raise ValueError(f"{label} liegt ausserhalb des Goldordners: {path}") from exc
    return resolved


def _load_json_array(path: Path, label: str) -> tuple[list[dict[str, Any]], bytes]:
    data = _require_plain_file(path, label).read_bytes()
    try:
        document = json.loads(data.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError(f"{label} ist kein gueltiges UTF-8-JSON: {path}") from exc
    if not isinstance(document, list) or any(not isinstance(item, dict) for item in document):
        raise ValueError(f"{label} muss ein JSON-Array aus Objekten sein: {path}")
    return document, data


def _is_approved_personal_gold(sample: dict[str, Any], approved_by: str) -> bool:
    status = sample.get("Status")
    return (
        status in (1, "1", "Approved", "approved")
        and sample.get("HumanConfirmed") is True
        and str(sample.get("ConfirmedByUser") or "").strip().casefold()
        == approved_by.casefold()
        and sample.get("HasBbox") is True
        and sample.get("HasSamMask") is True
    )


def _holding_from_source_name(path: Path) -> str:
    match = SOURCE_NAME_PATTERN.match(path.stem)
    if match is None:
        raise ValueError(f"Quellname besitzt keine eindeutige Haltung: {path.name}")
    raw = match.group("holding")
    normalized = gold_stock_audit.normalize_holding_key(raw)
    if normalized is None or gold_stock_audit.normalize_holding_key(normalized) != normalized:
        raise ValueError(f"Quellname besitzt keine belastbare Haltung: {path.name}")
    return normalized


def _find_source_matches(
    source_dir: Path,
    targets: dict[tuple[int, str], str],
) -> dict[str, Path]:
    wanted_lengths = {length for length, _ in targets}
    matches: dict[str, list[Path]] = {sample_id: [] for sample_id in targets.values()}
    for path in sorted(source_dir.iterdir(), key=lambda item: item.name.casefold()):
        if _is_link_or_reparse(path) or not path.is_file():
            continue
        if path.suffix.casefold() not in IMAGE_SUFFIXES:
            continue
        size = path.stat().st_size
        if size not in wanted_lengths:
            continue
        image_hash = _sha256_file(path)
        sample_id = targets.get((size, image_hash))
        if sample_id is not None:
            matches[sample_id].append(path.resolve())

    result: dict[str, Path] = {}
    for sample_id, paths in matches.items():
        if len(paths) != 1:
            raise ValueError(
                f"Goldsample {sample_id} besitzt {len(paths)} statt genau einer "
                "bytegleichen Quelldatei."
            )
        result[sample_id] = paths[0]
    return result


def _read_database_rows(database_path: Path, sample_ids: Iterable[str]) -> dict[str, str]:
    uri = f"file:{database_path.as_posix()}?mode=ro"
    connection = sqlite3.connect(uri, uri=True)
    try:
        rows: dict[str, str] = {}
        for sample_id in sample_ids:
            values = list(
                connection.execute(
                    "SELECT CaseId FROM Samples WHERE SampleId = ?",
                    (sample_id,),
                )
            )
            if len(values) != 1:
                raise ValueError(
                    f"Wissensdatenbank besitzt {len(values)} Zeilen fuer {sample_id}."
                )
            rows[sample_id] = str(values[0][0] or "").strip()
        return rows
    finally:
        connection.close()


def build_plan(
    knowledge_root: Path,
    source_images: Path,
    approved_by: str,
) -> RepairPlan:
    root = _require_plain_directory(knowledge_root, "Wissensordner")
    source_dir = _require_plain_directory(source_images, "Quellenordner")
    samples_path = root / "training_samples.json"
    teacher_path = root / "teacher_annotations.json"
    database_path = _require_plain_file(root / "KnowledgeBase.db", "Wissensdatenbank")
    samples, samples_bytes = _load_json_array(samples_path, "Goldsamples")
    teachers, teacher_bytes = _load_json_array(teacher_path, "Teacher-Annotationen")
    gold_root = _require_plain_directory(root / "gold_frames", "Goldbildordner")

    candidates: list[tuple[dict[str, Any], Path, str]] = []
    seen_sample_ids: set[str] = set()
    targets: dict[tuple[int, str], str] = {}
    for sample in samples:
        if not _is_approved_personal_gold(sample, approved_by):
            continue
        old_case = str(sample.get("CaseId") or "").strip()
        if not old_case.casefold().startswith("foto_"):
            continue
        sample_id = str(sample.get("SampleId") or "").strip()
        if not sample_id or sample_id in seen_sample_ids:
            raise ValueError(f"Goldsample-ID fehlt oder ist doppelt: {sample_id!r}")
        seen_sample_ids.add(sample_id)
        frame_path = _require_below(
            Path(str(sample.get("FramePath") or "")),
            gold_root,
            f"Goldbild {sample_id}",
        )
        image_hash = _sha256_file(frame_path)
        target_key = (frame_path.stat().st_size, image_hash)
        prior = targets.setdefault(target_key, sample_id)
        if prior != sample_id:
            raise ValueError(
                f"Die Samples {prior} und {sample_id} verwenden dasselbe Goldbild."
            )
        candidates.append((sample, frame_path, image_hash))

    if not candidates:
        raise ValueError("Keine persoenlichen Goldsamples mit fehlender Haltungs-ID gefunden.")

    source_matches = _find_source_matches(source_dir, targets)
    teacher_by_sample: dict[str, list[dict[str, Any]]] = {}
    for teacher in teachers:
        source_sample_id = str(teacher.get("sourceSampleId") or "").strip()
        if source_sample_id:
            teacher_by_sample.setdefault(source_sample_id, []).append(teacher)
    database_rows = _read_database_rows(database_path, seen_sample_ids)
    existing_signatures = {
        str(sample.get("Signature") or ""): str(sample.get("SampleId") or "")
        for sample in samples
        if str(sample.get("Signature") or "")
    }

    repairs: list[HoldingRepair] = []
    generated_signatures: dict[str, str] = {}
    for sample, _frame_path, image_hash in candidates:
        sample_id = str(sample["SampleId"]).strip()
        old_case = str(sample["CaseId"]).strip()
        source_path = source_matches[sample_id]
        new_case = _holding_from_source_name(source_path)
        if new_case == old_case:
            raise ValueError(f"Goldsample {sample_id} besitzt bereits die Zielhaltung.")
        old_signature = str(sample.get("Signature") or "").strip()
        prefix = f"{old_case}|"
        if not old_signature.startswith(prefix):
            raise ValueError(f"Signatur von Goldsample {sample_id} passt nicht zur CaseId.")
        new_signature = f"{new_case}|{old_signature[len(prefix):]}"
        collision = existing_signatures.get(new_signature)
        if collision is not None and collision != sample_id:
            raise ValueError(
                f"Neue Signatur von {sample_id} kollidiert mit Goldsample {collision}."
            )
        prior_generated = generated_signatures.setdefault(new_signature, sample_id)
        if prior_generated != sample_id:
            raise ValueError(
                f"Neue Signatur von {sample_id} kollidiert mit {prior_generated}."
            )
        linked_teachers = teacher_by_sample.get(sample_id, [])
        if len(linked_teachers) != 1:
            raise ValueError(
                f"Teacher-Datei besitzt {len(linked_teachers)} Verknuepfungen fuer {sample_id}."
            )
        teacher_case = str(linked_teachers[0].get("haltungName") or "").strip()
        if teacher_case != old_case:
            raise ValueError(f"Teacher-Haltung von {sample_id} weicht von der Gold-CaseId ab.")
        if database_rows[sample_id] != old_case:
            raise ValueError(f"Datenbank-Haltung von {sample_id} weicht von der Gold-CaseId ab.")
        repairs.append(
            HoldingRepair(
                sample_id=sample_id,
                old_case_id=old_case,
                new_case_id=new_case,
                code=str(sample.get("Code") or "").strip(),
                image_sha256=image_hash,
                source_file=str(source_path),
                old_signature=old_signature,
                new_signature=new_signature,
            )
        )

    return RepairPlan(
        knowledge_root=root,
        source_images=source_dir,
        samples_path=samples_path.resolve(),
        teacher_path=teacher_path.resolve(),
        database_path=database_path.resolve(),
        samples_bytes=samples_bytes,
        teacher_bytes=teacher_bytes,
        samples=samples,
        teachers=teachers,
        repairs=tuple(sorted(repairs, key=lambda item: item.sample_id.casefold())),
    )


def _json_bytes(document: Any) -> bytes:
    return (json.dumps(document, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def _atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=".tmp", dir=path.parent
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_path, path)
    finally:
        temporary_path.unlink(missing_ok=True)


def _sewerstudio_running() -> bool:
    if os.name != "nt":
        return False
    result = subprocess.run(
        ["tasklist", "/FI", "IMAGENAME eq SewerStudio.exe", "/FO", "CSV", "/NH"],
        check=False,
        capture_output=True,
        text=True,
        timeout=10,
    )
    return '"SewerStudio.exe"' in result.stdout


def _backup_database(source: Path, target: Path) -> None:
    source_connection = sqlite3.connect(str(source), timeout=0)
    target_connection = sqlite3.connect(str(target))
    try:
        source_connection.backup(target_connection)
    finally:
        target_connection.close()
        source_connection.close()


def _restore_database(source_backup: Path, target: Path) -> None:
    source_connection = sqlite3.connect(str(source_backup))
    target_connection = sqlite3.connect(str(target), timeout=0)
    try:
        source_connection.backup(target_connection)
    finally:
        target_connection.close()
        source_connection.close()


def _apply_json_changes(
    plan: RepairPlan,
    now: datetime,
) -> tuple[bytes, bytes]:
    samples = copy.deepcopy(plan.samples)
    teachers = copy.deepcopy(plan.teachers)
    repair_by_id = {item.sample_id: item for item in plan.repairs}
    date_text = now.astimezone(timezone.utc).date().isoformat()
    source_label = str(plan.source_images)
    for sample in samples:
        repair = repair_by_id.get(str(sample.get("SampleId") or "").strip())
        if repair is None:
            continue
        sample["CaseId"] = repair.new_case_id
        sample["Signature"] = repair.new_signature
        note = (
            f"CaseId {repair.old_case_id} -> {repair.new_case_id} "
            f"(Bild-SHA-Match {source_label}, {date_text})"
        )
        existing = str(sample.get("Notes") or "").strip()
        sample["Notes"] = note if not existing else f"{existing}; {note}"
    for teacher in teachers:
        repair = repair_by_id.get(str(teacher.get("sourceSampleId") or "").strip())
        if repair is not None:
            teacher["haltungName"] = repair.new_case_id
    return _json_bytes(samples), _json_bytes(teachers)


def execute_plan(plan: RepairPlan, now: datetime) -> Path:
    if _sewerstudio_running():
        raise ValueError("SewerStudio laeuft noch. Reparatur wurde nicht gestartet.")
    if plan.samples_path.read_bytes() != plan.samples_bytes:
        raise ValueError("training_samples.json wurde parallel veraendert.")
    if plan.teacher_path.read_bytes() != plan.teacher_bytes:
        raise ValueError("teacher_annotations.json wurde parallel veraendert.")

    repairs_root = plan.knowledge_root / "training" / "repairs"
    repairs_root.mkdir(parents=True, exist_ok=True)
    if _is_link_or_reparse(repairs_root):
        raise ValueError(f"Reparaturordner ist unsicher: {repairs_root}")
    stamp = now.astimezone(timezone.utc).strftime("%Y%m%d_%H%M%S_%f")
    backup_dir = repairs_root / f"holding_id_repair_{stamp}"
    backup_dir.mkdir(exist_ok=False)

    samples_backup = backup_dir / "training_samples.before.json"
    teacher_backup = backup_dir / "teacher_annotations.before.json"
    database_backup = backup_dir / "KnowledgeBase.before.db"
    samples_backup.write_bytes(plan.samples_bytes)
    teacher_backup.write_bytes(plan.teacher_bytes)
    _backup_database(plan.database_path, database_backup)
    plan_document = {
        "schema_version": "gold-holding-id-repair-v1",
        "created_utc": now.astimezone(timezone.utc).isoformat().replace("+00:00", "Z"),
        "knowledge_root": str(plan.knowledge_root),
        "source_images": str(plan.source_images),
        "source_hashes": {
            "training_samples": _sha256_bytes(plan.samples_bytes),
            "teacher_annotations": _sha256_bytes(plan.teacher_bytes),
            "knowledge_base_backup": _sha256_file(database_backup),
        },
        "repairs": [asdict(item) for item in plan.repairs],
    }
    _atomic_write(backup_dir / "repair_plan.json", _json_bytes(plan_document))

    new_samples_bytes, new_teacher_bytes = _apply_json_changes(plan, now)
    connection: sqlite3.Connection | None = None
    committed = False
    try:
        connection = sqlite3.connect(str(plan.database_path), timeout=0)
        connection.execute("BEGIN IMMEDIATE")
        for repair in plan.repairs:
            row = connection.execute(
                "SELECT CaseId FROM Samples WHERE SampleId = ?",
                (repair.sample_id,),
            ).fetchall()
            if row != [(repair.old_case_id,)]:
                raise ValueError(
                    f"Datenbank wurde vor Reparatur von {repair.sample_id} veraendert."
                )
            cursor = connection.execute(
                "UPDATE Samples SET CaseId = ? WHERE SampleId = ? AND CaseId = ?",
                (repair.new_case_id, repair.sample_id, repair.old_case_id),
            )
            if cursor.rowcount != 1:
                raise ValueError(f"Datenbank-Update fuer {repair.sample_id} war nicht eindeutig.")
        if plan.samples_path.read_bytes() != plan.samples_bytes:
            raise ValueError("training_samples.json wurde parallel veraendert.")
        if plan.teacher_path.read_bytes() != plan.teacher_bytes:
            raise ValueError("teacher_annotations.json wurde parallel veraendert.")
        _atomic_write(plan.samples_path, new_samples_bytes)
        _atomic_write(plan.teacher_path, new_teacher_bytes)
        connection.commit()
        committed = True
    except Exception:
        if connection is not None and not committed:
            connection.rollback()
        _atomic_write(plan.samples_path, plan.samples_bytes)
        _atomic_write(plan.teacher_path, plan.teacher_bytes)
        if committed:
            _restore_database(database_backup, plan.database_path)
        raise
    finally:
        if connection is not None:
            connection.close()

    try:
        verification = build_plan_after_repair(plan, new_samples_bytes, new_teacher_bytes)
        result = {
            "schema_version": "gold-holding-id-repair-result-v1",
            "completed_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
            "repair_count": len(plan.repairs),
            "verified": verification,
            "output_hashes": {
                "training_samples": _sha256_file(plan.samples_path),
                "teacher_annotations": _sha256_file(plan.teacher_path),
                "knowledge_base": _sha256_file(plan.database_path),
            },
        }
        _atomic_write(backup_dir / "repair_result.json", _json_bytes(result))
    except Exception:
        _atomic_write(plan.samples_path, plan.samples_bytes)
        _atomic_write(plan.teacher_path, plan.teacher_bytes)
        _restore_database(database_backup, plan.database_path)
        raise
    return backup_dir


def build_plan_after_repair(
    plan: RepairPlan,
    expected_samples_bytes: bytes,
    expected_teacher_bytes: bytes,
) -> bool:
    if plan.samples_path.read_bytes() != expected_samples_bytes:
        raise ValueError("Gold-JSON stimmt nach Reparatur nicht bytegenau.")
    if plan.teacher_path.read_bytes() != expected_teacher_bytes:
        raise ValueError("Teacher-JSON stimmt nach Reparatur nicht bytegenau.")
    samples = json.loads(expected_samples_bytes.decode("utf-8"))
    teachers = json.loads(expected_teacher_bytes.decode("utf-8"))
    samples_by_id = {str(item.get("SampleId") or ""): item for item in samples}
    teachers_by_id = {
        str(item.get("sourceSampleId") or ""): item
        for item in teachers
        if item.get("sourceSampleId")
    }
    database_rows = _read_database_rows(
        plan.database_path, (repair.sample_id for repair in plan.repairs)
    )
    for repair in plan.repairs:
        sample = samples_by_id.get(repair.sample_id)
        teacher = teachers_by_id.get(repair.sample_id)
        if (
            sample is None
            or sample.get("CaseId") != repair.new_case_id
            or sample.get("Signature") != repair.new_signature
            or teacher is None
            or teacher.get("haltungName") != repair.new_case_id
            or database_rows.get(repair.sample_id) != repair.new_case_id
        ):
            raise ValueError(f"Nachpruefung von {repair.sample_id} ist fehlgeschlagen.")
    return True


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Fehlende Gold-Haltungs-IDs per bytegleichem Quellenbild reparieren."
    )
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--source-images", type=Path, required=True)
    parser.add_argument("--approved-by", default="Besitzer")
    parser.add_argument("--execute", action="store_true")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _build_parser().parse_args(argv)
    try:
        plan = build_plan(args.knowledge_root, args.source_images, args.approved_by.strip())
        print(f"Modus: {'AUSFUEHRUNG' if args.execute else 'PRUEFLAUF'}")
        print(f"Eindeutige Reparaturen: {len(plan.repairs)}")
        for repair in plan.repairs:
            print(
                f"  {repair.sample_id}: {repair.old_case_id} -> "
                f"{repair.new_case_id} ({Path(repair.source_file).name})"
            )
        if not args.execute:
            print("Keine Datei wurde veraendert.")
            return 0
        backup_dir = execute_plan(plan, datetime.now(timezone.utc))
        print(f"Reparatur abgeschlossen und geprueft. Beleg: {backup_dir}")
        return 0
    except (OSError, sqlite3.Error, ValueError) as exc:
        print(f"GESPERRT: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
