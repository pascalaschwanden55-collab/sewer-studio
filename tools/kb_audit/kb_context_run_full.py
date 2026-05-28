#!/usr/bin/env python3
"""
End-to-End Helper: Vorher-Audit -> Apply -> Nachher-Audit (Schritt 2c).

Klebt drei bestehende Skripte zusammen:
  1) kb_audit_dryrun.py     auf Live-DB        (vorher)
  2) kb_context_apply.py    --apply            (Snapshot + Transaktion)
  3) kb_audit_dryrun.py     auf derselben DB   (nachher)

Liefert am Ende eine kurze Diff-Zeile (Coverage vorher/nachher) und die Pfade
aller erzeugten Reports. Ohne --apply laufen Schritt 2 und 3 nicht — nur das
Vorher-Audit wird erzeugt.

Aufruf:
    python tools/kb_audit/kb_context_run_full.py
        --stammdaten C:/KI_BRAIN/stammdaten/haltungs_stammdaten.json
        [--db C:/KI_BRAIN/KnowledgeBase.db]
        [--apply]
"""

from __future__ import annotations
import argparse
import datetime
import glob
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--db", default=r"C:\KI_BRAIN\KnowledgeBase.db")
    p.add_argument("--stammdaten", required=True)
    p.add_argument("--out", default=r"C:\KI_BRAIN\kb_audit")
    p.add_argument("--snapshots", default=r"C:\KI_BRAIN\snapshots")
    p.add_argument("--apply", action="store_true")
    return p.parse_args()


def run(cmd: list[str]) -> None:
    print(f"\n>>> {' '.join(cmd)}")
    res = subprocess.run(cmd, check=False)
    if res.returncode != 0:
        print(f"FEHLER: Subkommando endete mit Exit-Code {res.returncode}", file=sys.stderr)
        sys.exit(res.returncode)


def latest_audit_json(out_dir: Path, prefix: str) -> Path | None:
    files = sorted(out_dir.glob(f"{prefix}*.json"))
    return files[-1] if files else None


def load_coverage(json_path: Path) -> dict:
    return json.loads(json_path.read_text(encoding="utf-8")).get("Coverage", {})


def main() -> int:
    args = parse_args()
    out_dir = Path(args.out)

    # --- Vorher-Audit ---
    print("=== Vorher-Audit ===")
    run([sys.executable, str(HERE / "kb_audit_dryrun.py"),
         "--db", args.db, "--out", args.out])
    pre_json = latest_audit_json(out_dir, "kb_audit_")
    pre_cov = load_coverage(pre_json) if pre_json else {}

    if not args.apply:
        print("\nKein --apply gesetzt — Stoppe nach Vorher-Audit.")
        return 0

    # --- Apply ---
    print("\n=== Kontext-Apply ===")
    run([sys.executable, str(HERE / "kb_context_apply.py"),
         "--db", args.db, "--stammdaten", args.stammdaten,
         "--out", args.out, "--snapshots", args.snapshots, "--apply"])

    # --- Nachher-Audit ---
    print("\n=== Nachher-Audit ===")
    run([sys.executable, str(HERE / "kb_audit_dryrun.py"),
         "--db", args.db, "--out", args.out])
    post_json = latest_audit_json(out_dir, "kb_audit_")
    post_cov = load_coverage(post_json) if post_json else {}

    # --- Diff ---
    print("\n=== Coverage Diff ===")
    keys = ["WithRohrmaterial", "WithRohrmaterialPercent",
            "WithNennweiteMm",  "WithNennweiteMmPercent"]
    width = max(len(k) for k in keys)
    for k in keys:
        before = pre_cov.get(k, "?")
        after  = post_cov.get(k, "?")
        print(f"  {k.ljust(width)}  {before}  ->  {after}")
    print(f"\nVorher-Audit:  {pre_json}")
    print(f"Nachher-Audit: {post_json}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
