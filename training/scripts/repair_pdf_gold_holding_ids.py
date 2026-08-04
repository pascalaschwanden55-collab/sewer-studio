#!/usr/bin/env python3
"""Prueflauf: falsche Haltungs-IDs in PDF-basierten Goldsamples klassifizieren.

Geschwister-Skript zu ``repair_gold_holding_ids.py`` (das nur ``foto_*``-CaseIds
repariert). Dieser Lauf ist strikt schreibfrei: Er klassifiziert jedes
persoenlich bestaetigte Goldsample mit ``PDF-Operateurreferenz`` in genau eine
Gruppe und schreibt einen Pruefbericht:

- ``bereits_korrekt``            CaseId entspricht Ordner- und Dateinamen-Wahrheit
- ``gruppe_1_mit_bildbeleg``     reparierbar, Goldbild bytegleich im PDF (SHA-256)
- ``gruppe_2_ohne_bildbeleg``    reparierbar nur ueber Ordner + Dateiname
- ``gruppe_3_quarantaene``       Ordner widerspricht Dateiname oder PDF-Hash
- ``gruppe_4_kein_beleg``        farbnormalisiert (PNG): kein Byte-Beweis moeglich
- ``quelle_fehlt``               Quell-PDF unter dem Haltungsstamm nicht auffindbar

Kundenoriginale und der Wissensbestand werden nicht veraendert.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
from collections import Counter
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
    token = HOLDING_TOKEN.search(value.replace("/", "-"))
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
) -> tuple[Path | None, str | None, str | None]:
    """Findet das Quell-PDF samt Top-Haltungsordner; bei Mehrdeutigkeit entscheidet der Notiz-Hash."""
    candidates = index.get(pdf_name.casefold(), [])
    if not candidates:
        return None, None, "nicht_gefunden"
    if len(candidates) == 1 and not expected_sha256:
        return candidates[0][0], candidates[0][1], None
    for path, top_name in candidates:
        if _sha256_file(path).casefold() == (expected_sha256 or "").casefold():
            return path, top_name, None
    return None, None, "hash_abweichung"


def load_probe_hashes(probe_exe: Path, pdf_path: Path, cache: dict[str, dict]) -> dict:
    """Ruft PdfImageAnalyzer --json ab und cached das Ergebnis pro PDF."""
    key = str(pdf_path)
    if key in cache:
        return cache[key]
    result = subprocess.run(
        [str(probe_exe), "--json", str(pdf_path)],
        capture_output=True, text=True, timeout=300, check=False,
    )
    entry: dict = {"raw_sha256": set(), "error": None}
    if result.returncode != 0:
        entry["error"] = f"Probe Exit {result.returncode}: {result.stderr.strip()[:200]}"
    else:
        try:
            records = json.loads(result.stdout)
            for record in records:
                if record.get("error"):
                    entry["error"] = record["error"]
                elif record.get("rawSha256"):
                    entry["raw_sha256"].add(record["rawSha256"].casefold())
        except json.JSONDecodeError as exc:
            entry["error"] = f"Probe-JSON unlesbar: {exc}"
    cache[key] = entry
    return entry


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--holdings-root", type=Path, default=Path(r"D:\Haltungen"))
    parser.add_argument("--approved-by", default="Besitzer")
    parser.add_argument("--probe-exe", type=Path, default=Path(
        r"C:\Sewer-Studio_KI_4.5\tools\PdfImageAnalyzer\bin\Release\net10.0\PdfImageAnalyzer.exe"))
    parser.add_argument("--out", type=Path, default=Path(
        r"C:\Sewer-Studio_KI_4.5\artifacts\holding-repair-20260803\pruefbericht.json"))
    parser.add_argument("--limit", type=int, default=0, help="Nur die ersten N Goldsamples (Test)")
    args = parser.parse_args(argv)

    samples_path = args.knowledge_root / "training_samples.json"
    samples = json.loads(samples_path.read_text(encoding="utf-8-sig"))
    print(f"Goldsamples geladen: {len(samples)} ({samples_path})")
    print("Baue PDF-Index auf (nur Aufzaehlung) ...")
    index = build_pdf_index(args.holdings_root)
    print(f"PDF-Index: {sum(len(v) for v in index.values())} Dateien in {len(index)} Namen")

    probe_cache: dict[str, dict] = {}
    classified: list[dict] = []
    skipped = Counter()
    sha_repaired_count = 0

    for sample in samples:
        notes = str(sample.get("Notes") or "")
        if "Bild-SHA" in notes:
            sha_repaired_count += 1
        match = NOTES_PATTERN.search(notes)
        if match is None:
            continue
        if not _is_approved_personal_gold(sample, args.approved_by):
            skipped["nicht_approved_gold"] += 1
            continue

        sample_id = str(sample.get("SampleId") or "")
        case_id = str(sample.get("CaseId") or "")
        pdf_name = match.group("pdf")
        note_sha = match.group("sha")
        frame_path = Path(str(sample.get("FramePath") or ""))
        record = {
            "sample_id": sample_id, "case_id": case_id,
            "code": sample.get("Code"), "pdf": pdf_name,
            "frame": str(frame_path), "gruppe": None, "kandidat": None,
            "bildbeleg": None, "detail": None,
        }

        source_pdf, holding_folder, resolve_error = resolve_source_pdf(pdf_name, note_sha, index)
        holding_file = derive_holding_from_name(pdf_name)

        if resolve_error == "nicht_gefunden":
            record["gruppe"] = "quelle_fehlt"
        elif resolve_error == "hash_abweichung":
            record["gruppe"] = "gruppe_3_quarantaene"
            record["detail"] = "PDF-Datei gefunden, aber SHA-256 weicht von der Notiz ab"
        else:
            key_file = comparison_key(holding_file)
            key_folder = comparison_key(holding_folder)
            if key_folder is None or key_file is None:
                record["gruppe"] = "gruppe_3_quarantaene"
                record["detail"] = f"Haltung nicht ableitbar (Datei: {holding_file}, Ordner: {holding_folder})"
            elif key_file != key_folder:
                record["gruppe"] = "gruppe_3_quarantaene"
                record["detail"] = f"Ordner '{holding_folder}' widerspricht Dateiname '{holding_file}'"
            else:
                record["kandidat"] = holding_folder
                if comparison_key(case_id) == key_folder:
                    record["gruppe"] = "bereits_korrekt"
                else:
                    suffix = frame_path.suffix.casefold()
                    if suffix not in IMAGE_SUFFIXES or not frame_path.exists():
                        record["gruppe"] = "gruppe_2_ohne_bildbeleg"
                        record["detail"] = "Goldbild fehlt oder unbekanntes Format"
                    elif suffix == ".png":
                        record["gruppe"] = "gruppe_4_kein_beleg"
                        record["detail"] = "farbnormalisiertes PNG, kein Byte-Beweis moeglich"
                    else:
                        probe = load_probe_hashes(args.probe_exe, source_pdf, probe_cache)
                        if probe["error"]:
                            record["gruppe"] = "gruppe_2_ohne_bildbeleg"
                            record["detail"] = f"Probe nicht lesbar: {probe['error']}"
                        else:
                            gold_sha = _sha256_file(frame_path).casefold()
                            if gold_sha in probe["raw_sha256"]:
                                record["gruppe"] = "gruppe_1_mit_bildbeleg"
                                record["bildbeleg"] = gold_sha
                            else:
                                record["gruppe"] = "gruppe_2_ohne_bildbeleg"
                                record["detail"] = "kein bytegleiches Bild im Quell-PDF"
        classified.append(record)
        if args.limit and len(classified) >= args.limit:
            break

    counts = Counter(r["gruppe"] for r in classified)
    print("\n=== Ergebnis (schreibfreier Prueflauf) ===")
    print(f"bewertete Goldsamples: {len(classified)} (uebersprungen: {dict(skipped)})")
    for gruppe in ("bereits_korrekt", "gruppe_1_mit_bildbeleg", "gruppe_2_ohne_bildbeleg",
                   "gruppe_3_quarantaene", "gruppe_4_kein_beleg", "quelle_fehlt"):
        print(f"  {gruppe:28s}: {counts.get(gruppe, 0)}")
    print(f"(Referenz: frueher per Bild-SHA reparierte Samples im Bestand: {sha_repaired_count})")

    args.out.parent.mkdir(parents=True, exist_ok=True)
    report = {
        "schema_version": "pdf-gold-holding-id-probe-v1",
        "modus": "schreibfreier_prueflauf",
        "created_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "eingaben": {
            "training_samples": str(samples_path),
            "haltungs_stamm": str(args.holdings_root),
            "probe": str(args.probe_exe),
        },
        "gruppen": {k: counts.get(k, 0) for k in (
            "bereits_korrekt", "gruppe_1_mit_bildbeleg", "gruppe_2_ohne_bildbeleg",
            "gruppe_3_quarantaene", "gruppe_4_kein_beleg", "quelle_fehlt")},
        "uebersprungen": dict(skipped),
        "frueher_bild_sha_repariert": sha_repaired_count,
        "records": classified,
    }
    args.out.write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\nPruefbericht: {args.out}")
    print("Keine Datei wurde veraendert.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
