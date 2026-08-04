#!/usr/bin/env python3
"""Entfernt eval-kontaminierte Goldsamples vorsorglich aus dem Trainingsweg.

Beweisschwellen (Entscheid 2026-08-03, hier verbindlich festgehalten):
Fuer REPARIEREN und fuer ENTKONTAMINIEREN gelten verschiedene Beweisschwellen.
Eine CaseId umzuschreiben verlangt einen harten Beleg — eine falsche Reparatur
richtet neuen Schaden an. Ein Sample vorsorglich aus dem Trainingsregister zu
nehmen kostet dagegen fast nichts, waehrend es drinzulassen im Zweifel den
Abnahmemassstab ruiniert. Deshalb gehen hier auch Faelle ohne Byte-Beweis raus.

Wirkung je Sample (atomar, mit Sicherung und Nachpruefung):
- ``training_samples.json``: ``TrainingEligible=false``,
  ``TrainingEligibilityReason='eval-holdout-contamination-precaution'``
- ``export_registry_v1.json``: Entfernung aus ``approved_sample_ids``

Das DETECT_ALL-Registersetup (registry_setup_v1.json) bleibt als
SHA-gebundene historische Momentaufnahme bestehen; es wird beim naechsten
Registeraufbau ohnehin ersetzt. Standardlauf ist schreibfrei (Prueflauf).
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

REASON = "eval-holdout-contamination-precaution"

# Kontamination belegt im Pruefbericht
# docs/quality/PRUEFBERICHT-PDF-GOLD-HALTUNGS-IDS-2026-08-03.json:
# 5 Samples aus Holdout-Haltung 60604-60603 (gruppe_4),
# 3 Samples aus Holdout-Haltung 07.148371-10300 (gruppe_1, byte-bewiesen).
DEFAULT_SAMPLE_IDS = [
    "wb_33e0e2b3d56f",
    "wb_5f7cbd92367e",
    "wb_070730d4a8eb",
    "wb_647eefeb9840",
    "wb_6ab38a8e51a4",
    "wb_4eb82c1a51f7",
    "wb_6bbc15171015",
    "wb_e343ca2a7f4e",
]


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


def build_plan(knowledge_root: Path, sample_ids: list[str]) -> dict:
    samples_path = knowledge_root / "training_samples.json"
    registry_path = knowledge_root / "training" / "export_registry_v1.json"
    samples_bytes = samples_path.read_bytes()
    registry_bytes = registry_path.read_bytes()
    samples = json.loads(samples_bytes.decode("utf-8-sig"))
    registry = json.loads(registry_bytes.decode("utf-8-sig"))

    by_id = {str(s.get("SampleId")): s for s in samples}
    plan_entries = []
    for sample_id in sample_ids:
        sample = by_id.get(sample_id)
        if sample is None:
            raise ValueError(f"Sample nicht gefunden: {sample_id}")
        registered = sample_id in registry["approved_sample_ids"]
        plan_entries.append({
            "sample_id": sample_id,
            "case_id": sample.get("CaseId"),
            "code": sample.get("Code"),
            "war_eligible": bool(sample.get("TrainingEligible")),
            "war_im_register": registered,
        })
    return {
        "samples_path": samples_path,
        "registry_path": registry_path,
        "samples_bytes": samples_bytes,
        "registry_bytes": registry_bytes,
        "samples": samples,
        "registry": registry,
        "entries": plan_entries,
    }


def print_plan(plan: dict) -> None:
    for entry in plan["entries"]:
        print(
            f"  {entry['sample_id']}  {entry['case_id']}  {entry['code']}  "
            f"eligible={entry['war_eligible']} register={entry['war_im_register']}"
        )


def execute_plan(plan: dict) -> Path:
    if _sewerstudio_running():
        raise ValueError("SewerStudio laeuft noch. Abbruch vor jeder Schreibung.")
    samples_path: Path = plan["samples_path"]
    registry_path: Path = plan["registry_path"]
    if samples_path.read_bytes() != plan["samples_bytes"]:
        raise ValueError("training_samples.json wurde parallel veraendert.")
    if registry_path.read_bytes() != plan["registry_bytes"]:
        raise ValueError("export_registry_v1.json wurde parallel veraendert.")

    stamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    backup_dir = samples_path.parent / "training" / "repairs" / f"eval_decontamination_{stamp}"
    backup_dir.mkdir(parents=True, exist_ok=False)
    (backup_dir / "training_samples.before.json").write_bytes(plan["samples_bytes"])
    (backup_dir / "export_registry_v1.before.json").write_bytes(plan["registry_bytes"])

    ids = {entry["sample_id"] for entry in plan["entries"]}
    samples = json.loads(plan["samples_bytes"].decode("utf-8-sig"))
    registry = json.loads(plan["registry_bytes"].decode("utf-8-sig"))
    changed_samples = 0
    for sample in samples:
        if str(sample.get("SampleId")) in ids:
            sample["TrainingEligible"] = False
            sample["TrainingEligibilityReason"] = REASON
            changed_samples += 1
    before_count = len(registry["approved_sample_ids"])
    registry["approved_sample_ids"] = [
        sid for sid in registry["approved_sample_ids"] if sid not in ids
    ]
    removed = before_count - len(registry["approved_sample_ids"])

    new_samples = (json.dumps(samples, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    new_registry = (json.dumps(registry, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    try:
        _atomic_write(samples_path, new_samples)
        _atomic_write(registry_path, new_registry)
    except Exception:
        _atomic_write(samples_path, plan["samples_bytes"])
        _atomic_write(registry_path, plan["registry_bytes"])
        raise

    # Nachpruefung aus den geschriebenen Dateien.
    check_samples = json.loads(samples_path.read_text(encoding="utf-8-sig"))
    check_registry = json.loads(registry_path.read_text(encoding="utf-8-sig"))
    by_id = {str(s.get("SampleId")): s for s in check_samples}
    for sample_id in ids:
        if by_id[sample_id].get("TrainingEligible") is not False:
            raise ValueError(f"Nachpruefung fehlgeschlagen (eligible): {sample_id}")
        if by_id[sample_id].get("TrainingEligibilityReason") != REASON:
            raise ValueError(f"Nachpruefung fehlgeschlagen (reason): {sample_id}")
        if sample_id in check_registry["approved_sample_ids"]:
            raise ValueError(f"Nachpruefung fehlgeschlagen (register): {sample_id}")

    receipt = {
        "schema_version": "eval-decontamination-v1",
        "completed_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "begruendung": (
            "Reparieren verlangt harten Beleg, Entkontaminieren ist Vorsorge "
            "mit geringen Kosten; deshalb auch Faelle ohne Byte-Beweis entfernt."
        ),
        "reason_token": REASON,
        "samples_geaendert": changed_samples,
        "register_entfernt": removed,
        "register_vorher": before_count,
        "register_nachher": len(registry["approved_sample_ids"]),
        "eintraege": plan["entries"],
        "hashes": {
            "training_samples_vorher": _sha256_bytes(plan["samples_bytes"]),
            "training_samples_nachher": _sha256_bytes(new_samples),
            "export_registry_vorher": _sha256_bytes(plan["registry_bytes"]),
            "export_registry_nachher": _sha256_bytes(new_registry),
        },
    }
    (backup_dir / "receipt.json").write_text(
        json.dumps(receipt, ensure_ascii=False, indent=1), encoding="utf-8"
    )
    return backup_dir


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--sample-ids", nargs="*", default=DEFAULT_SAMPLE_IDS)
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args(argv)

    try:
        plan = build_plan(args.knowledge_root, args.sample_ids)
        print(f"Modus: {'AUSFUEHRUNG' if args.execute else 'PRUEFLAUF'}")
        print(f"Betroffene Samples: {len(plan['entries'])}")
        print_plan(plan)
        if not args.execute:
            print("Keine Datei wurde veraendert.")
            return 0
        backup_dir = execute_plan(plan)
        print(f"Entfernt und geprueft. Beleg: {backup_dir}")
        return 0
    except (OSError, ValueError) as exc:
        print(f"GESPERRT: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
