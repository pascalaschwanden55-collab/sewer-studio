#!/usr/bin/env python3
"""
KB Material-Cleanup (Schritt 2d von Plan v2)

Setzt offensichtlich falsche Material-Werte in der Samples-Tabelle auf NULL.
Falsch heisst: Wert besteht die Plausibilitaets-Whitelist aus
kb_context_apply.py NICHT (z.B. 'Kreisprofil', 'Inspektionsrichtung',
'unbekanntes', '10254-07.10173').

Sicherheitsmuster wie kb_context_apply.py:
    DRY (Default)  : zeigt nur, was geaendert WUERDE; DB unveraendert.
    --apply        : Snapshot anlegen, in Transaktion auf NULL setzen.

Aufruf:
    python tools/kb_audit/kb_material_cleanup.py
        [--db C:/KI_BRAIN/KnowledgeBase.db]
        [--out C:/KI_BRAIN/kb_audit]
        [--snapshots C:/KI_BRAIN/snapshots]
        [--apply]
"""

from __future__ import annotations
import argparse
import collections
import datetime
import json
import sqlite3
import sys
from pathlib import Path

# Wir nutzen denselben Plausibilitaets-Check wie der Apply-Schritt.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from kb_context_apply import material_is_plausible, make_snapshot


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="KB Material-Cleanup")
    p.add_argument("--db", default=r"C:\KI_BRAIN\KnowledgeBase.db")
    p.add_argument("--out", default=r"C:\KI_BRAIN\kb_audit")
    p.add_argument("--snapshots", default=r"C:\KI_BRAIN\snapshots")
    p.add_argument("--apply", action="store_true",
                   help="Tatsaechlich NULL setzen. Ohne dieses Flag nur Plan.")
    return p.parse_args()


def main() -> int:
    args = parse_args()
    db_path = Path(args.db)
    if not db_path.exists():
        print(f"FEHLER: DB nicht gefunden: {db_path}", file=sys.stderr)
        return 2

    job_id = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    out_dir = Path(args.out); out_dir.mkdir(parents=True, exist_ok=True)

    # Plan: alle Samples mit implausiblen Material-Werten finden
    uri = f"file:{db_path.as_posix()}?mode=ro&immutable=1"
    con_ro = sqlite3.connect(uri, uri=True)
    try:
        cur = con_ro.cursor()
        cur.execute("""
            SELECT SampleId, Rohrmaterial
            FROM Samples
            WHERE Rohrmaterial IS NOT NULL AND TRIM(Rohrmaterial) != ''
        """)
        rows = cur.fetchall()
    finally:
        con_ro.close()

    bad: list[tuple[str, str]] = []
    by_value: collections.Counter = collections.Counter()
    for sid, value in rows:
        if not material_is_plausible(value):
            bad.append((sid, value))
            by_value[value] += 1

    print(f"Implausible Material-Werte gefunden: {len(bad):,} Samples "
          f"({len(by_value)} distinct)")
    for v, n in by_value.most_common():
        print(f"  {n:>5}  {v!r}")

    plan_report = {
        "JobId": job_id,
        "Mode": "apply" if args.apply else "dry",
        "GeneratedAt": datetime.datetime.now().isoformat(),
        "DbPath": str(db_path),
        "ToNullify": len(bad),
        "DistinctValues": dict(by_value.most_common()),
        "SampleIds": [sid for sid, _ in bad[:200]],
    }

    if not args.apply:
        out = out_dir / f"kb_material_cleanup_dry_{job_id}.json"
        out.write_text(json.dumps(plan_report, indent=2, ensure_ascii=False),
                       encoding="utf-8")
        print(f"\nDRY-Report: {out}")
        print("Kein --apply gesetzt - DB bleibt unveraendert.")
        return 0

    # Apply-Pfad
    print("\n[APPLY] Snapshot anlegen ...")
    snap_path = make_snapshot(db_path, Path(args.snapshots), job_id)
    print(f"  Snapshot: {snap_path}")

    con_rw = sqlite3.connect(str(db_path))
    try:
        cur = con_rw.cursor()
        cur.execute("BEGIN")
        try:
            updated = 0
            for sid, value in bad:
                # Defensive: nur loeschen wenn der Wert noch derselbe ist
                cur.execute(
                    "UPDATE Samples SET Rohrmaterial = NULL "
                    "WHERE SampleId = ? AND Rohrmaterial = ?",
                    (sid, value))
                updated += cur.rowcount
            con_rw.commit()
        except Exception:
            con_rw.rollback()
            raise
    finally:
        con_rw.close()

    plan_report["UpdatedRows"] = updated
    plan_report["SnapshotPath"] = str(snap_path)
    out = out_dir / f"kb_material_cleanup_apply_{job_id}.json"
    out.write_text(json.dumps(plan_report, indent=2, ensure_ascii=False),
                   encoding="utf-8")
    print(f"\n[APPLY] {updated:,} Material-Felder auf NULL gesetzt.")
    print(f"APPLY-Report: {out}")
    print("Rollback: snapshot ueber live-DB zurueckkopieren.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
