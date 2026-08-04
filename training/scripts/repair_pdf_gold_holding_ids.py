#!/usr/bin/env python3
"""Repariert falsche Haltungs-IDs in PDF-basierten Goldsamples (fail-closed).

Geschwister-Skript zu ``repair_gold_holding_ids.py`` (das nur ``foto_*``-CaseIds
repariert). Standardlauf ist ein schreibfreier Prueflauf; erst ``--execute``
schreibt — atomar, mit Sicherung, gemeinsamer Aktualisierung von Gold-JSON,
Teacher-JSON, SQLite und Exportregister sowie bytegenauer Nachpruefung.

Gruppen des Prueflaufs:
- ``bereits_korrekt``            CaseId entspricht Ordner- und Dateinamen-Wahrheit
- ``gruppe_1_mit_bildbeleg``     reparierbar, Goldbild bytegleich im PDF (SHA-256)
- ``gruppe_4_normalisierer``     reparierbar, Beweis ueber die CMYK-Normalisierung
- ``gruppe_2_ohne_bildbeleg``    reparierbar nur ueber Ordner + Dateiname
- ``gruppe_3_quarantaene``       Ordner widerspricht Dateiname oder PDF-Hash
- ``gruppe_4_kein_beleg``        farbnormalisiert, ohne Normalisierer-Treffer
- ``quelle_fehlt``               Quell-PDF unter dem Haltungsstamm nicht auffindbar
- ``preflight_dekontamination``  Zielhaltung ist geschuetzt (Holdout/Eval/Negativ/
                                 Gold-Audit-Testrolle) — wird NICHT repariert,
                                 sondern aus dem Trainingsweg genommen

Schutzquellen (Vorflug): Holdouts und Eval-Sets unter ``eval_set`` (inkl.
``subsets``), Negativsaetze unter ``training/negatives``, Test-Rollen des
neuesten Gold-Audits sowie ``protected_sets`` des Exportregisters. Die
Diagnose-Warteschlangen unter ``eval_review/detect_gold_failure_review``
gehoeren bewusst NICHT dazu (abgeleitet, wuerde legitime Haltungen sperren).

Kundenoriginale und der Wissensbestand werden im Prueflauf nicht veraendert.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
import re
import sqlite3
import subprocess
import sys
import tempfile
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path

from repair_gold_holding_ids import (
    _is_approved_personal_gold,
    _is_link_or_reparse,
    _sha256_file,
)

NOTES_PATTERN = re.compile(
    r"PDF-Operateurreferenz: (?P<pdf>[^;]+?\.pdf);\s*SHA-256=(?P<sha>[0-9a-fA-F]{64})"
    r"(?:;\s*Seite=(?P<page>\d+))?"
)
DATE_PREFIX = re.compile(r"^\d{6,8}[_-]?")
HOLDING_TOKEN = re.compile(r"\d[\d.]*-\d[\d.]*")
ENDPOINT_PREFIX = re.compile(r"^(\d{1,2})\.(\d.*)$")
IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png"}
DECONTAMINATION_REASON = "eval-holdout-contamination-precaution"

GROUP_ORDER = (
    "bereits_korrekt",
    "gruppe_1_mit_bildbeleg",
    "gruppe_4_normalisierer",
    "gruppe_2_ohne_bildbeleg",
    "gruppe_3_quarantaene",
    "gruppe_4_kein_beleg",
    "quelle_fehlt",
    "preflight_dekontamination",
)


# ---------------------------------------------------------------------------
# Reine Normalisierung / Ableitung (kein I/O)
# ---------------------------------------------------------------------------

def _strip_endpoint_prefix(endpoint: str) -> str:
    """Bereichspraefix (``NN.``) nur entfernen, wenn der Rest >= 4 Zeichen hat."""
    match = ENDPOINT_PREFIX.match(endpoint)
    if match and len(match.group(2)) >= 4:
        return match.group(2)
    return endpoint


def comparison_key(value: str | None) -> str | None:
    """Richtungsunabhaengiger Vergleichsschluessel einer Haltungs-ID."""
    if not value:
        return None
    token = HOLDING_TOKEN.search(str(value).replace("/", "-"))
    if token is None:
        return None
    endpoints = [_strip_endpoint_prefix(part.strip('.'))
                 for part in token.group(0).split("-", 1)]
    if len(endpoints) != 2 or not all(endpoints):
        return None
    return "-".join(sorted(endpoints)).casefold()


def derive_holding_from_name(pdf_name: str) -> str | None:
    """Haltung aus dem PDF-Dateinamen (Datumspraefix abgezogen, geschuetzte Regeln)."""
    stem = DATE_PREFIX.sub("", Path(pdf_name).stem)
    token = HOLDING_TOKEN.search(stem)
    if token is None:
        return None
    raw = token.group(0)
    endpoints = raw.split("-", 1)
    # Uhrlagen (z. B. 12-1) und zu kurze Fragmente sind keine Haltungen.
    if all(len(part.strip('.')) < 3 for part in endpoints):
        return None
    return raw


# ---------------------------------------------------------------------------
# Schutzquellen (Vorflug)
# ---------------------------------------------------------------------------

def _register_holding_keys(candidate: dict) -> set[str]:
    keys = set()
    for raw in (candidate.get("haltung_key"), candidate.get("physical_holding_key")):
        key = comparison_key(raw)
        if key:
            keys.add(key)
    return keys


def load_protection_keys(knowledge_root: Path) -> dict[str, set[str]]:
    """Geschuetzte Haltungsschluessel -> Quellen. Nur Holdouts, Eval-Sets,
    Negativsaetze und Gold-Audit-Testrollen — keine Diagnose-Warteschlangen."""
    protection: dict[str, set[str]] = {}

    def add(key: str | None, source: str) -> None:
        if key:
            protection.setdefault(key, set()).add(source)

    eval_set = knowledge_root / "eval_set"
    for candidates_path in sorted(eval_set.glob("**/_candidates.json")):
        try:
            document = json.loads(candidates_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        items = document.get("candidates") if isinstance(document, dict) else document
        if not isinstance(items, list):
            continue
        source = f"eval_set:{candidates_path.parent.name}"
        for item in items:
            if isinstance(item, dict):
                for key in _register_holding_keys(item):
                    add(key, source)

    for manifest in sorted((knowledge_root / "training" / "negatives").glob("**/manifest.json")):
        try:
            document = json.loads(manifest.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        for item in document.get("images") or document.get("items") or []:
            if isinstance(item, dict):
                add(comparison_key(item.get("holding_key")), f"negatives:{manifest.parent.name}")

    reports = sorted((knowledge_root / "training" / "reports").glob("gold_stock_audit_*.json"))
    if reports:
        try:
            audit = json.loads(reports[-1].read_text(encoding="utf-8"))
            for gruppe in audit.get("split", {}).get("gruppen", []):
                if gruppe.get("rolle") == "test":
                    name = str(gruppe.get("gruppe") or "")
                    add(comparison_key(name.removeprefix("haltung:")), f"gold_audit_test:{reports[-1].name}")
        except (OSError, json.JSONDecodeError):
            pass

    registry_path = knowledge_root / "training" / "export_registry_v1.json"
    if registry_path.exists():
        try:
            registry = json.loads(registry_path.read_text(encoding="utf-8"))
            for protected in registry.get("protected_sets") or []:
                root = protected.get("root_path")
                if not root:
                    continue
                candidates = knowledge_root / root / "_candidates.json"
                if not candidates.exists():
                    continue
                document = json.loads(candidates.read_text(encoding="utf-8"))
                items = document.get("candidates") if isinstance(document, dict) else document
                for item in items or []:
                    if isinstance(item, dict):
                        for key in _register_holding_keys(item):
                            add(key, f"registry_protected:{protected.get('set_id')}")
        except (OSError, json.JSONDecodeError):
            pass

    return protection


# ---------------------------------------------------------------------------
# Klassifikation (rein, mit injiziertem Beweiser)
# ---------------------------------------------------------------------------

@dataclass(frozen=True)
class SourceResolution:
    pdf_path: Path | None
    holding_folder: str | None
    error: str | None  # None | "nicht_gefunden" | "hash_abweichung"


def classify_sample(
    sample_id: str,
    case_id: str,
    pdf_name: str,
    frame_path: Path,
    resolution: SourceResolution,
    protection_keys: dict[str, set[str]],
    image_prover,
) -> dict:
    """Ordnet ein Goldsample genau einer Gruppe zu. ``image_prover`` erhaelt
    (frame_path, source_pdf, modus) mit modus in {"roh", "normalisiert"} und
    liefert True bei byte-bzw. wandlungsgleichem Beweis."""
    record = {
        "sample_id": sample_id,
        "case_id": case_id,
        "pdf": pdf_name,
        "frame": str(frame_path),
        "gruppe": None,
        "kandidat": None,
        "bildbeleg": None,
        "detail": None,
        "schutzquellen": None,
    }
    if resolution.error == "nicht_gefunden":
        record["gruppe"] = "quelle_fehlt"
        return record
    if resolution.error == "hash_abweichung":
        record["gruppe"] = "gruppe_3_quarantaene"
        record["detail"] = "PDF-Datei gefunden, aber SHA-256 weicht von der Notiz ab"
        return record

    holding_file = derive_holding_from_name(pdf_name)
    key_file = comparison_key(holding_file)
    key_folder = comparison_key(resolution.holding_folder)
    if key_folder is None or key_file is None:
        record["gruppe"] = "gruppe_3_quarantaene"
        record["detail"] = f"Haltung nicht ableitbar (Datei: {holding_file}, Ordner: {resolution.holding_folder})"
        return record
    if key_file != key_folder:
        record["gruppe"] = "gruppe_3_quarantaene"
        record["detail"] = f"Ordner '{resolution.holding_folder}' widerspricht Dateiname '{holding_file}'"
        return record

    record["kandidat"] = resolution.holding_folder
    if comparison_key(case_id) == key_folder:
        record["gruppe"] = "bereits_korrekt"
        return record

    # Vorflug-Schutzpruefung VOR jeder Reparatur-Entscheidung.
    schutz = protection_keys.get(key_folder)
    if schutz:
        record["gruppe"] = "preflight_dekontamination"
        record["schutzquellen"] = sorted(schutz)
        record["detail"] = "Zielhaltung geschuetzt — Dekontamination statt Reparatur"
        return record

    suffix = frame_path.suffix.casefold()
    if suffix not in IMAGE_SUFFIXES or not frame_path.exists():
        record["gruppe"] = "gruppe_2_ohne_bildbeleg"
        record["detail"] = "Goldbild fehlt oder unbekanntes Format"
        return record
    if image_prover(frame_path, resolution.pdf_path, "roh"):
        record["gruppe"] = "gruppe_1_mit_bildbeleg"
        return record
    if suffix == ".png":
        if image_prover(frame_path, resolution.pdf_path, "normalisiert"):
            record["gruppe"] = "gruppe_4_normalisierer"
            return record
        record["gruppe"] = "gruppe_4_kein_beleg"
        record["detail"] = "farbnormalisiertes PNG ohne Normalisierer-Treffer"
        return record
    record["gruppe"] = "gruppe_2_ohne_bildbeleg"
    record["detail"] = "kein bytegleiches Bild im Quell-PDF"
    return record


# ---------------------------------------------------------------------------
# I/O: PDF-Index, Quellenaufloesung, Probe
# ---------------------------------------------------------------------------

def build_pdf_index(holdings_root: Path) -> dict[str, list[tuple[Path, str]]]:
    """Dateiname -> (Pfad, Top-Haltungsordner) unter dem Stamm (rekursiv, nur Aufzaehlung)."""
    index: dict[str, list[tuple[Path, str]]] = {}
    for top in sorted(holdings_root.iterdir()):
        if _is_link_or_reparse(top) or not top.is_dir():
            continue
        for dirpath, _dirnames, filenames in os.walk(top):
            if _is_link_or_reparse(Path(dirpath)):
                continue
            for name in filenames:
                if name.casefold().endswith(".pdf"):
                    path = Path(dirpath) / name
                    index.setdefault(name.casefold(), []).append((path, top.name))
    return index


def resolve_source_pdf(
    pdf_name: str,
    expected_sha256: str | None,
    index: dict[str, list[tuple[Path, str]]],
) -> SourceResolution:
    """Findet das Quell-PDF samt Top-Haltungsordner; bei Mehrdeutigkeit entscheidet der Notiz-Hash."""
    candidates = index.get(pdf_name.casefold(), [])
    if not candidates:
        return SourceResolution(None, None, "nicht_gefunden")
    if len(candidates) == 1 and not expected_sha256:
        return SourceResolution(candidates[0][0], candidates[0][1], None)
    for path, top_name in candidates:
        if _sha256_file(path).casefold() == (expected_sha256 or "").casefold():
            return SourceResolution(path, top_name, None)
    return SourceResolution(None, None, "hash_abweichung")


def load_probe_hashes(probe_exe: Path, pdf_path: Path, cache: dict[str, dict]) -> dict:
    """Ruft PdfImageAnalyzer --normalize-json ab und cached das Ergebnis pro PDF."""
    key = str(pdf_path)
    if key in cache:
        return cache[key]
    result = subprocess.run(
        [str(probe_exe), "--normalize-json", str(pdf_path)],
        capture_output=True, text=True, timeout=600, check=False,
    )
    entry: dict = {"raw": set(), "normalized": set(), "error": None}
    if result.returncode != 0:
        entry["error"] = f"Probe Exit {result.returncode}: {result.stderr.strip()[:200]}"
    else:
        try:
            records = json.loads(result.stdout)
            for probe_record in records:
                if probe_record.get("error"):
                    entry["error"] = probe_record["error"]
                else:
                    if probe_record.get("rawSha256"):
                        entry["raw"].add(probe_record["rawSha256"].casefold())
                    if probe_record.get("normalizedSha256"):
                        entry["normalized"].add(probe_record["normalizedSha256"].casefold())
        except json.JSONDecodeError as exc:
            entry["error"] = f"Probe-JSON unlesbar: {exc}"
    cache[key] = entry
    return entry


def make_probe_image_prover(probe_exe: Path) -> tuple:
    """Beweiser auf Basis der PdfImageAnalyzer-Probe (roh + normalisiert)."""
    cache: dict[str, dict] = {}

    def prove(frame_path: Path, source_pdf: Path | None, modus: str) -> bool:
        if source_pdf is None or not frame_path.exists():
            return False
        probe = load_probe_hashes(probe_exe, source_pdf, cache)
        if probe["error"]:
            return False
        gold_sha = _sha256_file(frame_path).casefold()
        return gold_sha in probe["raw" if modus == "roh" else "normalized"]

    return prove, cache


# ---------------------------------------------------------------------------
# Klassifikationslauf (I/O-Orchestrierung)
# ---------------------------------------------------------------------------

def run_classification(
    knowledge_root: Path,
    holdings_root: Path,
    approved_by: str,
    image_prover,
    limit: int = 0,
) -> tuple[list[dict], dict]:
    samples = json.loads(
        (knowledge_root / "training_samples.json").read_text(encoding="utf-8-sig")
    )
    index = build_pdf_index(holdings_root)
    protection = load_protection_keys(knowledge_root)
    classified: list[dict] = []
    skipped = {"nicht_approved_gold": 0}
    for sample in samples:
        match = NOTES_PATTERN.search(str(sample.get("Notes") or ""))
        if match is None:
            continue
        if not _is_approved_personal_gold(sample, approved_by):
            skipped["nicht_approved_gold"] += 1
            continue
        resolution = resolve_source_pdf(match.group("pdf"), match.group("sha"), index)
        record = classify_sample(
            sample_id=str(sample.get("SampleId") or ""),
            case_id=str(sample.get("CaseId") or ""),
            pdf_name=match.group("pdf"),
            frame_path=Path(str(sample.get("FramePath") or "")),
            resolution=resolution,
            protection_keys=protection,
            image_prover=image_prover,
        )
        classified.append(record)
        if limit and len(classified) >= limit:
            break
    stats = {
        "bewertet": len(classified),
        "uebersprungen": skipped,
        "schutzschluessel": len(protection),
    }
    return classified, stats


# ---------------------------------------------------------------------------
# Ausfuehrungsplan (rein)
# ---------------------------------------------------------------------------

REPAIR_GROUPS = ("gruppe_1_mit_bildbeleg", "gruppe_4_normalisierer")


@dataclass(frozen=True)
class PdfRepair:
    sample_id: str
    old_case_id: str
    new_case_id: str
    code: str
    bildbeleg: str
    old_signature: str
    new_signature: str


@dataclass(frozen=True)
class Decontamination:
    sample_id: str
    case_id: str
    code: str
    schutzquellen: tuple[str, ...]


@dataclass(frozen=True)
class ExecutePlan:
    knowledge_root: Path
    samples_path: Path
    teacher_path: Path
    database_path: Path
    registry_path: Path
    samples_bytes: bytes
    teacher_bytes: bytes
    registry_bytes: bytes
    samples: list[dict]
    teachers: list[dict]
    repairs: tuple[PdfRepair, ...]
    decontaminations: tuple[Decontamination, ...]
    skipped_groups: dict


def _read_database_rows(database_path: Path, sample_ids) -> dict[str, str]:
    uri = f"file:{database_path.as_posix()}?mode=ro"
    connection = sqlite3.connect(uri, uri=True)
    try:
        rows: dict[str, str] = {}
        for sample_id in sample_ids:
            values = list(connection.execute(
                "SELECT CaseId FROM Samples WHERE SampleId = ?", (sample_id,)))
            if len(values) != 1:
                raise ValueError(f"Wissensdatenbank besitzt {len(values)} Zeilen fuer {sample_id}.")
            rows[sample_id] = str(values[0][0] or "").strip()
        return rows
    finally:
        connection.close()


def build_execute_plan(knowledge_root: Path, classified: list[dict]) -> ExecutePlan:
    """Baut den Ausfuehrungsplan aus einer frischen Klassifikation. Fail-closed:
    jede verletzte Invariante (Signatur, Teacher, DB) sperrt den ganzen Lauf."""
    samples_path = knowledge_root / "training_samples.json"
    teacher_path = knowledge_root / "teacher_annotations.json"
    registry_path = knowledge_root / "training" / "export_registry_v1.json"
    database_path = knowledge_root / "KnowledgeBase.db"
    samples_bytes = samples_path.read_bytes()
    teacher_bytes = teacher_path.read_bytes()
    registry_bytes = registry_path.read_bytes()
    samples = json.loads(samples_bytes.decode("utf-8-sig"))
    teachers = json.loads(teacher_bytes.decode("utf-8-sig"))
    registry = json.loads(registry_bytes.decode("utf-8-sig"))

    samples_by_id = {str(s.get("SampleId")): s for s in samples}
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

    repairs: list[PdfRepair] = []
    decontaminations: list[Decontamination] = []
    skipped: dict[str, int] = {}
    targets = [r for r in classified
               if r["gruppe"] in REPAIR_GROUPS or r["gruppe"] == "preflight_dekontamination"]
    database_rows = _read_database_rows(database_path, (r["sample_id"] for r in targets)) if targets else {}
    generated_signatures: dict[str, str] = {}

    for record in targets:
        sample_id = record["sample_id"]
        if record["gruppe"] == "preflight_dekontamination":
            sample = samples_by_id.get(sample_id)
            if (sample is not None
                    and sample.get("TrainingEligible") is False
                    and sample.get("TrainingEligibilityReason") == DECONTAMINATION_REASON
                    and sample_id not in registry["approved_sample_ids"]):
                skipped["bereits_dekontaminiert"] = skipped.get("bereits_dekontaminiert", 0) + 1
                continue
            decontaminations.append(Decontamination(
                sample_id=sample_id,
                case_id=record["case_id"],
                code=str((sample or {}).get("Code") or ""),
                schutzquellen=tuple(record.get("schutzquellen") or ()),
            ))
            continue

        sample = samples_by_id.get(sample_id)
        if sample is None:
            raise ValueError(f"Sample nicht im Bestand: {sample_id}")
        old_case = str(sample.get("CaseId") or "").strip()
        new_case = str(record["kandidat"] or "").strip()
        if not new_case or new_case == old_case:
            raise ValueError(f"Ungueltige Reparatur fuer {sample_id}: {old_case} -> {new_case}")
        old_signature = str(sample.get("Signature") or "").strip()
        prefix = f"{old_case}|"
        if not old_signature.startswith(prefix):
            raise ValueError(f"Signatur von {sample_id} passt nicht zur CaseId.")
        new_signature = f"{new_case}|{old_signature[len(prefix):]}"
        collision = existing_signatures.get(new_signature)
        if collision is not None and collision != sample_id:
            raise ValueError(f"Neue Signatur von {sample_id} kollidiert mit {collision}.")
        prior = generated_signatures.setdefault(new_signature, sample_id)
        if prior != sample_id:
            raise ValueError(f"Neue Signatur von {sample_id} kollidiert mit {prior}.")
        linked = teacher_by_sample.get(sample_id, [])
        if len(linked) != 1:
            raise ValueError(f"Teacher-Datei besitzt {len(linked)} Verknuepfungen fuer {sample_id}.")
        if str(linked[0].get("haltungName") or "").strip() != old_case:
            raise ValueError(f"Teacher-Haltung von {sample_id} weicht von der Gold-CaseId ab.")
        if database_rows.get(sample_id) != old_case:
            raise ValueError(f"Datenbank-Haltung von {sample_id} weicht von der Gold-CaseId ab.")
        repairs.append(PdfRepair(
            sample_id=sample_id,
            old_case_id=old_case,
            new_case_id=new_case,
            code=str(sample.get("Code") or ""),
            bildbeleg=record["gruppe"],
            old_signature=old_signature,
            new_signature=new_signature,
        ))

    for record in classified:
        if record["gruppe"] not in REPAIR_GROUPS and record["gruppe"] != "preflight_dekontamination":
            skipped[record["gruppe"]] = skipped.get(record["gruppe"], 0) + 1

    return ExecutePlan(
        knowledge_root=knowledge_root,
        samples_path=samples_path,
        teacher_path=teacher_path,
        database_path=database_path,
        registry_path=registry_path,
        samples_bytes=samples_bytes,
        teacher_bytes=teacher_bytes,
        registry_bytes=registry_bytes,
        samples=samples,
        teachers=teachers,
        repairs=tuple(sorted(repairs, key=lambda r: r.sample_id.casefold())),
        decontaminations=tuple(sorted(decontaminations, key=lambda d: d.sample_id.casefold())),
        skipped_groups=skipped,
    )


# ---------------------------------------------------------------------------
# Ausfuehrung (atomar, mit Sicherung und Nachpruefung)
# ---------------------------------------------------------------------------

def _json_bytes(document) -> bytes:
    return (json.dumps(document, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


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


def _backup_database(source: Path, target: Path) -> None:
    source_connection = sqlite3.connect(str(source), timeout=0)
    target_connection = sqlite3.connect(str(target))
    try:
        source_connection.backup(target_connection)
    finally:
        target_connection.close()
        source_connection.close()


def _restore_database(source_backup: Path, target: Path) -> None:
    source_connection = sqlite3.connect(str(source_backup), timeout=0)
    target_connection = sqlite3.connect(str(target), timeout=0)
    try:
        source_connection.backup(target_connection)
    finally:
        target_connection.close()
        source_connection.close()


def _apply_changes(plan: ExecutePlan, now: datetime) -> tuple[bytes, bytes, bytes]:
    samples = copy.deepcopy(plan.samples)
    teachers = copy.deepcopy(plan.teachers)
    registry = json.loads(plan.registry_bytes.decode("utf-8-sig"))
    repair_by_id = {r.sample_id: r for r in plan.repairs}
    decon_ids = {d.sample_id for d in plan.decontaminations}
    date_text = now.astimezone(timezone.utc).date().isoformat()

    for sample in samples:
        sample_id = str(sample.get("SampleId") or "").strip()
        repair = repair_by_id.get(sample_id)
        if repair is not None:
            sample["CaseId"] = repair.new_case_id
            sample["Signature"] = repair.new_signature
            note = (f"CaseId {repair.old_case_id} -> {repair.new_case_id} "
                    f"(PDF-Bildbeleg {repair.bildbeleg}, {date_text})")
            existing = str(sample.get("Notes") or "").strip()
            sample["Notes"] = note if not existing else f"{existing}; {note}"
        elif sample_id in decon_ids:
            sample["TrainingEligible"] = False
            sample["TrainingEligibilityReason"] = DECONTAMINATION_REASON
            note = f"Aus Trainingsweg genommen (Vorflug-Schutzpruefung, {date_text})"
            existing = str(sample.get("Notes") or "").strip()
            sample["Notes"] = note if not existing else f"{existing}; {note}"

    for teacher in teachers:
        repair = repair_by_id.get(str(teacher.get("sourceSampleId") or "").strip())
        if repair is not None:
            teacher["haltungName"] = repair.new_case_id

    if decon_ids:
        registry["approved_sample_ids"] = [
            sid for sid in registry["approved_sample_ids"] if sid not in decon_ids
        ]

    return _json_bytes(samples), _json_bytes(teachers), _json_bytes(registry)


def execute_plan(plan: ExecutePlan, now: datetime) -> Path:
    if _sewerstudio_running():
        raise ValueError("SewerStudio laeuft noch. Ausfuehrung wurde nicht gestartet.")
    if plan.samples_path.read_bytes() != plan.samples_bytes:
        raise ValueError("training_samples.json wurde parallel veraendert.")
    if plan.teacher_path.read_bytes() != plan.teacher_bytes:
        raise ValueError("teacher_annotations.json wurde parallel veraendert.")
    if plan.registry_path.read_bytes() != plan.registry_bytes:
        raise ValueError("export_registry_v1.json wurde parallel veraendert.")

    stamp = now.astimezone(timezone.utc).strftime("%Y%m%d_%H%M%S")
    backup_dir = (plan.knowledge_root / "training" / "repairs"
                  / f"pdf_gold_holding_id_repair_{stamp}")
    backup_dir.mkdir(parents=True, exist_ok=False)
    (backup_dir / "training_samples.before.json").write_bytes(plan.samples_bytes)
    (backup_dir / "teacher_annotations.before.json").write_bytes(plan.teacher_bytes)
    (backup_dir / "export_registry_v1.before.json").write_bytes(plan.registry_bytes)
    database_backup = backup_dir / "KnowledgeBase.before.db"
    _backup_database(plan.database_path, database_backup)

    new_samples, new_teachers, new_registry = _apply_changes(plan, now)
    connection: sqlite3.Connection | None = None
    committed = False
    try:
        connection = sqlite3.connect(str(plan.database_path), timeout=0)
        connection.execute("BEGIN IMMEDIATE")
        for repair in plan.repairs:
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
        if plan.samples_path.read_bytes() != plan.samples_bytes:
            raise ValueError("training_samples.json wurde parallel veraendert.")
        if plan.teacher_path.read_bytes() != plan.teacher_bytes:
            raise ValueError("teacher_annotations.json wurde parallel veraendert.")
        _atomic_write(plan.samples_path, new_samples)
        _atomic_write(plan.teacher_path, new_teachers)
        _atomic_write(plan.registry_path, new_registry)
        connection.commit()
        committed = True
    except Exception:
        if connection is not None and not committed:
            connection.rollback()
        _atomic_write(plan.samples_path, plan.samples_bytes)
        _atomic_write(plan.teacher_path, plan.teacher_bytes)
        _atomic_write(plan.registry_path, plan.registry_bytes)
        if committed:
            _restore_database(database_backup, plan.database_path)
        raise
    finally:
        if connection is not None:
            connection.close()

    try:
        verify_after_repair(plan, new_samples, new_teachers, new_registry)
    except Exception:
        _atomic_write(plan.samples_path, plan.samples_bytes)
        _atomic_write(plan.teacher_path, plan.teacher_bytes)
        _atomic_write(plan.registry_path, plan.registry_bytes)
        _restore_database(database_backup, plan.database_path)
        raise

    receipt = {
        "schema_version": "pdf-gold-holding-id-repair-result-v1",
        "completed_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "repariert": len(plan.repairs),
        "dekontaminiert": len(plan.decontaminations),
        "uebersprungene_gruppen": plan.skipped_groups,
        "repairs": [asdict(r) for r in plan.repairs],
        "decontaminations": [asdict(d) for d in plan.decontaminations],
        "output_hashes": {
            "training_samples": _sha256_file(plan.samples_path),
            "teacher_annotations": _sha256_file(plan.teacher_path),
            "export_registry": _sha256_file(plan.registry_path),
            "knowledge_base": _sha256_file(plan.database_path),
        },
    }
    _atomic_write(backup_dir / "repair_result.json", _json_bytes(receipt))
    return backup_dir


def verify_after_repair(
    plan: ExecutePlan,
    expected_samples: bytes,
    expected_teachers: bytes,
    expected_registry: bytes,
) -> None:
    if plan.samples_path.read_bytes() != expected_samples:
        raise ValueError("Gold-JSON stimmt nach Reparatur nicht bytegenau.")
    if plan.teacher_path.read_bytes() != expected_teachers:
        raise ValueError("Teacher-JSON stimmt nach Reparatur nicht bytegenau.")
    if plan.registry_path.read_bytes() != expected_registry:
        raise ValueError("Register stimmt nach Reparatur nicht bytegenau.")
    samples = json.loads(expected_samples.decode("utf-8"))
    teachers = json.loads(expected_teachers.decode("utf-8"))
    registry = json.loads(expected_registry.decode("utf-8"))
    samples_by_id = {str(s.get("SampleId")): s for s in samples}
    teachers_by_id = {str(t.get("sourceSampleId")): t for t in teachers if t.get("sourceSampleId")}
    decon_ids = {d.sample_id for d in plan.decontaminations}
    database_rows = _read_database_rows(plan.database_path, (r.sample_id for r in plan.repairs)) \
        if plan.repairs else {}
    for repair in plan.repairs:
        sample = samples_by_id.get(repair.sample_id)
        teacher = teachers_by_id.get(repair.sample_id)
        if (sample is None
                or sample.get("CaseId") != repair.new_case_id
                or sample.get("Signature") != repair.new_signature
                or teacher is None
                or teacher.get("haltungName") != repair.new_case_id
                or database_rows.get(repair.sample_id) != repair.new_case_id):
            raise ValueError(f"Nachpruefung von {repair.sample_id} ist fehlgeschlagen.")
    for sample_id in decon_ids:
        sample = samples_by_id.get(sample_id)
        if (sample is None
                or sample.get("TrainingEligible") is not False
                or sample.get("TrainingEligibilityReason") != DECONTAMINATION_REASON
                or sample_id in registry["approved_sample_ids"]):
            raise ValueError(f"Dekontaminations-Nachpruefung von {sample_id} ist fehlgeschlagen.")


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--holdings-root", type=Path, default=Path(r"D:\Haltungen"))
    parser.add_argument("--approved-by", default="Besitzer")
    parser.add_argument("--probe-exe", type=Path, default=Path(
        r"C:\Sewer-Studio_KI_4.5\tools\PdfImageAnalyzer\bin\Release\net10.0-windows\PdfImageAnalyzer.exe"))
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--report-out", type=Path, default=None,
                        help="Optionaler Pfad fuer den Pruefbericht (JSON)")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _build_parser().parse_args(argv)
    try:
        prover, _cache = make_probe_image_prover(args.probe_exe)
        print("Klassifikation laeuft (PDF-Index, Schutzquellen, Bildbelege) ...")
        classified, stats = run_classification(
            args.knowledge_root, args.holdings_root, args.approved_by, prover, args.limit)

        counts: dict[str, int] = {}
        for record in classified:
            counts[record["gruppe"]] = counts.get(record["gruppe"], 0) + 1
        print(f"bewertete Goldsamples: {stats['bewertet']} "
              f"(uebersprungen: {stats['uebersprungen']}, Schutzschluessel: {stats['schutzschluessel']})")
        for gruppe in GROUP_ORDER:
            print(f"  {gruppe:28s}: {counts.get(gruppe, 0)}")

        if args.report_out is not None:
            args.report_out.parent.mkdir(parents=True, exist_ok=True)
            args.report_out.write_text(json.dumps({
                "schema_version": "pdf-gold-holding-id-probe-v2",
                "modus": "ausfuehrung" if args.execute else "schreibfreier_prueflauf",
                "created_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "gruppen": {g: counts.get(g, 0) for g in GROUP_ORDER},
                "records": classified,
            }, ensure_ascii=False, indent=1), encoding="utf-8")
            print(f"Pruefbericht: {args.report_out}")

        if not args.execute:
            print("Keine Datei wurde veraendert.")
            return 0

        plan = build_execute_plan(args.knowledge_root, classified)
        print(f"Ausfuehrung: {len(plan.repairs)} Reparaturen, "
              f"{len(plan.decontaminations)} Dekontaminationen")
        backup_dir = execute_plan(plan, datetime.now(timezone.utc))
        print(f"Ausgefuehrt und geprueft. Beleg: {backup_dir}")
        return 0
    except (OSError, sqlite3.Error, ValueError) as exc:
        print(f"GESPERRT: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
