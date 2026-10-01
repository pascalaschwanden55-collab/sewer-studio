#!/usr/bin/env python3
"""Repariert gold_inbox-Pseudo-CaseIds ueber bytegleiche Kandidaten-Hashes.

Die 73 Samples mit CaseId ``gold_inbox_<hash>`` tragen keine echte Haltung.
Ihre Bildbytes sind aber in den gemessenen Kandidatenlisten
(``artifacts/klassen-messung-20260804/messung.json``) eindeutig einer Haltung
zugeordnet. Dieser Lauf schreibt CaseId und Signatur auf die belegte Haltung
um — mit Sicherung, atomarem Schreiben, Teacher-/SQLite-Gleichzug und
bytegenauer Nachpruefung. Vorflug-Schutzpruefung inklusive: Zeigt die Ziel-
haltung auf einen geschuetzten Bestand, wird das Sample nicht repariert,
sondern dekontaminiert (Beweisschwellen-Regel vom 2026-08-03).

Standard ist der schreibfreie Prueflauf; erst ``--execute`` schreibt.
"""

from __future__ import annotations

import argparse
import copy
import json
import os
import sqlite3
import subprocess
import sys
import tempfile
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path

from repair_pdf_gold_holding_ids import (
    DECONTAMINATION_REASON,
    comparison_key,
    load_protection_keys,
)

DEFAULT_MEASUREMENT = Path(
    r"C:\Sewer-Studio_KI_4.5\artifacts\klassen-messung-20260804\messung.json"
)


@dataclass(frozen=True)
class InboxRepair:
    sample_id: str
    old_case_id: str
    new_case_id: str
    code: str
    image_sha256: str
    quelle: str
    old_signature: str
    new_signature: str


def _sha256_bytes(data: bytes) -> str:
    import hashlib
    return hashlib.sha256(data).hexdigest()


def _atomic_write(path: Path, data: bytes) -> None:
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
        check=False, capture_output=True, timeout=10,
    )
    return b'"SewerStudio.exe"' in result.stdout


def load_candidate_holdings(measurement_path: Path) -> dict[str, tuple[str, str]]:
    """bild_sha256 -> (haltung, quelle); mehrdeutige Hashes fallen heraus."""
    document = json.loads(measurement_path.read_text(encoding="utf-8"))
    treffer: dict[str, set[tuple[str, str]]] = {}
    for klasse in document["klassen"].values():
        for kandidat in klasse["kandidaten"]:
            treffer.setdefault(kandidat["bild_sha256"], set()).add(
                (kandidat["haltung"], kandidat["quelle"])
            )
    result: dict[str, tuple[str, str]] = {}
    for sha, hits in treffer.items():
        haltungen = {h[0] for h in hits}
        if len(haltungen) == 1:
            result[sha] = next(iter(hits))
    return result


def build_plan(knowledge_root: Path, measurement_path: Path,
               explicit_repairs: list[dict] | None = None) -> dict:
    samples_path = knowledge_root / "training_samples.json"
    teacher_path = knowledge_root / "teacher_annotations.json"
    database_path = knowledge_root / "KnowledgeBase.db"
    samples_bytes = samples_path.read_bytes()
    teacher_bytes = teacher_path.read_bytes()
    samples = json.loads(samples_bytes.decode("utf-8-sig"))
    teachers = json.loads(teacher_bytes.decode("utf-8-sig"))
    holdings = load_candidate_holdings(measurement_path)
    # Vorflug: nur echte Eval-Sperren (Holdouts, Eval-Sets, Testrollen).
    # Trainings-Negativsaetze sind KEINE Eval-Kontamination: Ein anderes,
    # menschlich geprueftes Bild derselben Haltung darf Schaden zeigen.
    protection = {
        key: sources
        for key, sources in load_protection_keys(knowledge_root).items()
        if any(not source.startswith("negatives:") for source in sources)
    }

    teacher_by_sample: dict[str, list[dict]] = {}
    for teacher in teachers:
        sid = str(teacher.get("sourceSampleId") or "").strip()
        if sid:
            teacher_by_sample.setdefault(sid, []).append(teacher)
    existing_signatures = {
        str(s.get("Signature") or ""): str(s.get("SampleId") or "")
        for s in samples
        if str(s.get("Signature") or "")
    }

    explicit_by_id: dict[str, dict] = {}
    if explicit_repairs is not None:
        for entry in explicit_repairs:
            explicit_by_id[str(entry["sample_id"])] = entry
    samples_by_id = {str(s.get("SampleId")): s for s in samples}

    repairs: list[InboxRepair] = []
    decontaminations: list[str] = []
    skipped: dict[str, int] = {}
    if explicit_repairs is not None:
        targets = []
        for entry in explicit_repairs:
            sample = samples_by_id.get(str(entry["sample_id"]))
            if sample is None:
                raise ValueError(f"Explizites Repair-Ziel fehlt: {entry['sample_id']}")
            targets.append(sample)
    else:
        targets = [s for s in samples
                   if str(s.get("CaseId") or "").casefold().startswith("gold_inbox_")]
    if not targets:
        raise ValueError("Keine Reparaturziele gefunden.")

    connection = sqlite3.connect(f"file:{database_path.as_posix()}?mode=ro", uri=True)
    try:
        db_rows = {
            sid: [r[0] for r in connection.execute(
                "SELECT CaseId FROM Samples WHERE SampleId = ?", (sid,))]
            for sid in (str(s["SampleId"]) for s in targets)
        }
    finally:
        connection.close()

    generated: dict[str, str] = {}
    for sample in targets:
        sample_id = str(sample["SampleId"]).strip()
        old_case = str(sample["CaseId"]).strip()
        sha = str(sample.get("image_sha256") or "")
        if not sha:
            frame = str(sample.get("FramePath") or "")
            name = Path(frame).stem
            sha = name.removeprefix("gold_")
        if explicit_repairs is not None:
            entry = explicit_by_id[sample_id]
            new_case = str(entry["new_case_id"]).strip()
            quelle = str(entry.get("beleg") or "explizit")
            if comparison_key(new_case) is None:
                raise ValueError(f"Explizite Zielhaltung ist nicht belastbar: {new_case}")
        else:
            hit = holdings.get(sha)
            if hit is None:
                skipped["kein_kandidaten_treffer"] = skipped.get("kein_kandidaten_treffer", 0) + 1
                continue
            new_case, quelle = hit
        schutz = protection.get(comparison_key(new_case))
        if schutz:
            decontaminations.append(sample_id)
            continue

        old_signature = str(sample.get("Signature") or "").strip()
        prefix = f"{old_case}|"
        if not old_signature.startswith(prefix):
            raise ValueError(f"Signatur von {sample_id} passt nicht zur CaseId.")
        new_signature = f"{new_case}|{old_signature[len(prefix):]}"
        collision = existing_signatures.get(new_signature)
        if collision is not None and collision != sample_id:
            raise ValueError(f"Neue Signatur von {sample_id} kollidiert mit {collision}.")
        prior = generated.setdefault(new_signature, sample_id)
        if prior != sample_id:
            raise ValueError(f"Neue Signatur von {sample_id} kollidiert mit {prior}.")
        linked = teacher_by_sample.get(sample_id, [])
        if len(linked) != 1:
            raise ValueError(f"Teacher-Datei besitzt {len(linked)} Verknuepfungen fuer {sample_id}.")
        teacher_case = str(linked[0].get("haltungName") or "").strip()
        if teacher_case not in ("", old_case):
            # Leer ist der Inbox-Urzustand und wird mitgezogen; jede andere
            # Abweichung bleibt eine harte Sperre.
            raise ValueError(f"Teacher-Haltung von {sample_id} weicht von der Gold-CaseId ab.")
        if db_rows.get(sample_id) != [old_case]:
            raise ValueError(f"Datenbank-Haltung von {sample_id} weicht von der Gold-CaseId ab.")
        repairs.append(InboxRepair(
            sample_id=sample_id,
            old_case_id=old_case,
            new_case_id=new_case,
            code=str(sample.get("Code") or ""),
            image_sha256=sha,
            quelle=quelle,
            old_signature=old_signature,
            new_signature=new_signature,
        ))
    return {
        "samples_path": samples_path,
        "teacher_path": teacher_path,
        "database_path": database_path,
        "samples_bytes": samples_bytes,
        "teacher_bytes": teacher_bytes,
        "samples": samples,
        "teachers": teachers,
        "repairs": repairs,
        "decontaminations": decontaminations,
        "skipped": skipped,
        "targets": len(targets),
    }


def execute_plan(plan: dict) -> Path:
    if _sewerstudio_running():
        raise ValueError("SewerStudio laeuft noch. Ausfuehrung wurde nicht gestartet.")
    samples_path: Path = plan["samples_path"]
    teacher_path: Path = plan["teacher_path"]
    if samples_path.read_bytes() != plan["samples_bytes"]:
        raise ValueError("training_samples.json wurde parallel veraendert.")
    if teacher_path.read_bytes() != plan["teacher_bytes"]:
        raise ValueError("teacher_annotations.json wurde parallel veraendert.")

    stamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    backup_dir = (samples_path.parent / "training" / "repairs"
                  / f"inbox_holding_id_repair_{stamp}")
    backup_dir.mkdir(parents=True, exist_ok=False)
    (backup_dir / "training_samples.before.json").write_bytes(plan["samples_bytes"])
    (backup_dir / "teacher_annotations.before.json").write_bytes(plan["teacher_bytes"])
    import shutil
    shutil.copy2(plan["database_path"], backup_dir / "KnowledgeBase.before.db")

    samples = copy.deepcopy(plan["samples"])
    teachers = copy.deepcopy(plan["teachers"])
    repair_by_id = {r.sample_id: r for r in plan["repairs"]}
    decon_ids = set(plan["decontaminations"])
    date_text = datetime.now(timezone.utc).date().isoformat()
    for sample in samples:
        sid = str(sample.get("SampleId") or "").strip()
        repair = repair_by_id.get(sid)
        if repair is not None:
            sample["CaseId"] = repair.new_case_id
            sample["Signature"] = repair.new_signature
            note = (f"CaseId {repair.old_case_id} -> {repair.new_case_id} "
                    f"(Kandidaten-Byte-Match {repair.quelle}, {date_text})")
            existing = str(sample.get("Notes") or "").strip()
            sample["Notes"] = note if not existing else f"{existing}; {note}"
        elif sid in decon_ids:
            sample["TrainingEligible"] = False
            sample["TrainingEligibilityReason"] = DECONTAMINATION_REASON
    for teacher in teachers:
        repair = repair_by_id.get(str(teacher.get("sourceSampleId") or "").strip())
        if repair is not None:
            teacher["haltungName"] = repair.new_case_id

    new_samples = (json.dumps(samples, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    new_teachers = (json.dumps(teachers, ensure_ascii=False, indent=2) + "\n").encode("utf-8")

    connection: sqlite3.Connection | None = None
    committed = False
    try:
        connection = sqlite3.connect(str(plan["database_path"]), timeout=0)
        connection.execute("BEGIN IMMEDIATE")
        for repair in plan["repairs"]:
            row = connection.execute(
                "SELECT CaseId FROM Samples WHERE SampleId = ?", (repair.sample_id,),
            ).fetchall()
            if row != [(repair.old_case_id,)]:
                raise ValueError(f"Datenbank wurde vor Reparatur von {repair.sample_id} veraendert.")
            cursor = connection.execute(
                "UPDATE Samples SET CaseId = ? WHERE SampleId = ? AND CaseId = ?",
                (repair.new_case_id, repair.sample_id, repair.old_case_id),
            )
            if cursor.rowcount != 1:
                raise ValueError(f"Datenbank-Update fuer {repair.sample_id} war nicht eindeutig.")
        if samples_path.read_bytes() != plan["samples_bytes"]:
            raise ValueError("training_samples.json wurde parallel veraendert.")
        if teacher_path.read_bytes() != plan["teacher_bytes"]:
            raise ValueError("teacher_annotations.json wurde parallel veraendert.")
        _atomic_write(samples_path, new_samples)
        _atomic_write(teacher_path, new_teachers)
        connection.commit()
        committed = True
    except Exception:
        if connection is not None and not committed:
            connection.rollback()
        _atomic_write(samples_path, plan["samples_bytes"])
        _atomic_write(teacher_path, plan["teacher_bytes"])
        raise
    finally:
        if connection is not None:
            connection.close()

    # Nachpruefung
    check = json.loads(samples_path.read_text(encoding="utf-8-sig"))
    by_id = {str(s.get("SampleId")): s for s in check}
    for repair in plan["repairs"]:
        s = by_id[repair.sample_id]
        if s.get("CaseId") != repair.new_case_id or s.get("Signature") != repair.new_signature:
            raise ValueError(f"Nachpruefung fehlgeschlagen: {repair.sample_id}")
    for sid in plan["decontaminations"]:
        if by_id[sid].get("TrainingEligible") is not False:
            raise ValueError(f"Dekontaminations-Nachpruefung fehlgeschlagen: {sid}")

    receipt = {
        "schema_version": "inbox-holding-id-repair-result-v1",
        "completed_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "repariert": len(plan["repairs"]),
        "dekontaminiert": len(plan["decontaminations"]),
        "uebersprungen": plan["skipped"],
        "repairs": [asdict(r) for r in plan["repairs"]],
        "decontaminations": sorted(plan["decontaminations"]),
        "output_hashes": {
            "training_samples": _sha256_bytes(samples_path.read_bytes()),
            "teacher_annotations": _sha256_bytes(teacher_path.read_bytes()),
        },
    }
    (backup_dir / "repair_result.json").write_text(
        json.dumps(receipt, ensure_ascii=False, indent=1), encoding="utf-8"
    )
    return backup_dir


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--measurement", type=Path, default=DEFAULT_MEASUREMENT)
    parser.add_argument("--repairs-file", type=Path, default=None,
                        help="Explizite Reparaturliste (JSON: sample_id, new_case_id, beleg)")
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args(argv)
    try:
        explicit = None
        if args.repairs_file is not None:
            explicit = json.loads(args.repairs_file.read_text(encoding="utf-8-sig"))
            if not isinstance(explicit, list) or not explicit:
                raise ValueError("Die explizite Reparaturliste ist leer oder ungueltig.")
        plan = build_plan(args.knowledge_root, args.measurement, explicit)
        print(f"Modus: {'AUSFUEHRUNG' if args.execute else 'PRUEFLAUF'}")
        print(f"gold_inbox-Samples: {plan['targets']} | Reparaturen: {len(plan['repairs'])} "
          f"| Dekontaminationen: {len(plan['decontaminations'])} | offen: {plan['skipped']}")
        for repair in plan["repairs"][:10]:
            print(f"  {repair.sample_id}: {repair.old_case_id} -> {repair.new_case_id} ({repair.code})")
        if not args.execute:
            print("Keine Datei wurde veraendert.")
            return 0
        backup_dir = execute_plan(plan)
        print(f"Ausgefuehrt und geprueft. Beleg: {backup_dir}")
        return 0
    except (OSError, sqlite3.Error, ValueError) as exc:
        print(f"GESPERRT: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
