#!/usr/bin/env python3
"""
Knowledge-Base Audit-Dry-Run (Schritt 1 von Plan v2)

Liest die KnowledgeBase.db NUR-LESEND und schreibt einen JSON+Markdown-Report
mit allen Befunden für die spätere Kuration. Es wird NICHTS in der DB veraendert.

Aufruf:
    python tools/kb_audit/kb_audit_dryrun.py
        [--db C:/KI_BRAIN/KnowledgeBase.db]
        [--out C:/KI_BRAIN/kb_audit/]
        [--short-threshold 20]
        [--rare-threshold 10]

Ausgabe (idempotent, mit Job-ID = Timestamp):
    <out>/kb_audit_<YYYYMMDD_HHMMSS>.json
    <out>/kb_audit_<YYYYMMDD_HHMMSS>.md

Aufgaben (alle nur lesend, kein UPDATE / DELETE / INSERT):
    1. Vorher-Metriken: Total / je QualityGateLevel / je VsaCode (Top-50)
    2. Kontextabdeckung: Rohrmaterial-Quote, NennweiteMm-Quote
    3. Dubletten-Cluster: gleiche (VsaCode, Beschreibung, Material, DN)
    4. Boilerplate-Cluster: identische Beschreibungen unabhaengig von Code
    5. Kurze Beschreibungen (< short-threshold Zeichen)
    6. Seltene Codes (< rare-threshold Samples)
    7. Versionen-Verteilung (welche aktiv genutzt werden, welche leer sind)
    8. Frame-Verfügbarkeit (Samples mit FramePath aber Datei fehlt)

Fuer jeden Befund: ProsaText + concrete sample SampleIds (max 20 als Stichprobe).
"""

from __future__ import annotations
import argparse
import collections
import datetime
import hashlib
import json
import os
import sqlite3
import sys
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="KB Audit Dry-Run")
    p.add_argument("--db", default=r"C:\KI_BRAIN\KnowledgeBase.db",
                   help="Pfad zur KnowledgeBase.db")
    p.add_argument("--out", default=r"C:\KI_BRAIN\kb_audit",
                   help="Ausgabe-Verzeichnis")
    p.add_argument("--short-threshold", type=int, default=20,
                   help="Beschreibung kuerzer als N Zeichen = TextSignalWeak-Kandidat")
    p.add_argument("--rare-threshold", type=int, default=10,
                   help="VsaCode mit weniger als N Samples = selten/schuetzenswert")
    p.add_argument("--top-clusters", type=int, default=30,
                   help="Anzahl groesster Dublettencluster im Bericht")
    return p.parse_args()


def fetch_all_samples(con: sqlite3.Connection) -> list[dict]:
    """Liest die komplette Samples-Tabelle als List-of-Dicts."""
    cur = con.cursor()
    cur.execute("""
        SELECT SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
               IsStreck, FramePath, ExportedUtc, VersionId,
               COALESCE(SourceType, '') AS SourceType,
               Rohrmaterial,
               NennweiteMm,
               IsKorrigiert,
               QualityGateLevel
        FROM Samples
    """)
    cols = [d[0] for d in cur.description]
    return [dict(zip(cols, row)) for row in cur.fetchall()]


def fetch_versions(con: sqlite3.Connection) -> list[dict]:
    cur = con.cursor()
    cur.execute("""
        SELECT VersionId, CreatedAt, SampleCount, COALESCE(Notes, '') AS Notes
        FROM Versions
        ORDER BY CreatedAt
    """)
    cols = [d[0] for d in cur.description]
    return [dict(zip(cols, row)) for row in cur.fetchall()]


def fetch_embedding_count(con: sqlite3.Connection) -> int:
    cur = con.cursor()
    cur.execute("SELECT COUNT(*) FROM Embeddings")
    return int(cur.fetchone()[0])


def normalize_for_dedup(text: str | None) -> str:
    """Fuer Dublettenerkennung: trim, lower, mehrfach-whitespace zu einem."""
    if not text:
        return ""
    return " ".join(text.lower().strip().split())


def build_report(samples: list[dict], versions: list[dict],
                 embedding_count: int, args: argparse.Namespace) -> dict:
    """Baut den vollstaendigen Audit-Report (reines Lesen + Aggregation)."""
    total = len(samples)
    job_id = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")

    # ── 1. Vorher-Metriken ────────────────────────────────────────────
    quality_dist: dict[str, int] = collections.Counter()
    code_dist: collections.Counter = collections.Counter()
    source_dist: collections.Counter = collections.Counter()
    korrigiert_count = 0
    has_material = 0
    has_dn = 0
    has_caseid = 0
    has_frame_path = 0

    for s in samples:
        ql = (s.get("QualityGateLevel") or "Unklassifiziert").strip() or "Unklassifiziert"
        quality_dist[ql] += 1
        code_dist[(s.get("VsaCode") or "").strip()] += 1
        source_dist[s.get("SourceType") or ""] += 1
        if int(s.get("IsKorrigiert") or 0) == 1:
            korrigiert_count += 1
        if (s.get("Rohrmaterial") or "").strip():
            has_material += 1
        if s.get("NennweiteMm") is not None:
            has_dn += 1
        if (s.get("CaseId") or "").strip():
            has_caseid += 1
        if (s.get("FramePath") or "").strip():
            has_frame_path += 1

    # ── 2. Kontextabdeckung (% mit Material/DN/CaseId) ────────────────
    pct = lambda n: round((n / total) * 100, 2) if total else 0
    coverage = {
        "TotalSamples": total,
        "WithRohrmaterial": has_material,
        "WithRohrmaterialPercent": pct(has_material),
        "WithNennweiteMm": has_dn,
        "WithNennweiteMmPercent": pct(has_dn),
        "WithCaseId": has_caseid,
        "WithCaseIdPercent": pct(has_caseid),
        "WithFramePath": has_frame_path,
        "WithFramePathPercent": pct(has_frame_path),
        "IsKorrigiert": korrigiert_count,
        "IsKorrigiertPercent": pct(korrigiert_count),
        "WithEmbedding": embedding_count,
        "EmbeddingCoveragePercent": pct(embedding_count),
    }

    # ── 3. Dubletten-Cluster (VsaCode + Beschreibung + Material + DN) ─
    dedup_buckets: dict[tuple, list[str]] = collections.defaultdict(list)
    for s in samples:
        key = (
            (s.get("VsaCode") or "").strip(),
            normalize_for_dedup(s.get("Beschreibung")),
            (s.get("Rohrmaterial") or "").strip(),
            s.get("NennweiteMm"),
        )
        dedup_buckets[key].append(s["SampleId"])

    dedup_clusters = []
    for key, ids in dedup_buckets.items():
        if len(ids) < 2:
            continue
        dedup_clusters.append({
            "VsaCode": key[0],
            "Beschreibung": key[1][:120],
            "Rohrmaterial": key[2],
            "NennweiteMm": key[3],
            "Count": len(ids),
            "SampleIds_Sample": ids[:20],
        })
    dedup_clusters.sort(key=lambda c: -c["Count"])

    affected = sum(c["Count"] for c in dedup_clusters)
    dedup_summary = {
        "ClusterCount": len(dedup_clusters),
        "AffectedSamples": affected,
        "RedundantSamples": affected - len(dedup_clusters),  # ueberzaehlig wenn 1 Repraesentant pro Cluster
        "TopClusters": dedup_clusters[:args.top_clusters],
    }

    # ── 4. Boilerplate (gleiche Beschreibung quer ueber alle Codes) ───
    text_buckets: dict[str, list[tuple[str, str]]] = collections.defaultdict(list)
    for s in samples:
        norm = normalize_for_dedup(s.get("Beschreibung"))
        if not norm:
            continue
        text_buckets[norm].append((s["SampleId"], (s.get("VsaCode") or "").strip()))

    boilerplate_clusters = []
    for norm, entries in text_buckets.items():
        if len(entries) < 5:
            continue
        codes = collections.Counter(e[1] for e in entries)
        boilerplate_clusters.append({
            "Beschreibung": norm[:120],
            "Count": len(entries),
            "DistinctCodes": len(codes),
            "TopCode": codes.most_common(1)[0],
            "SampleIds_Sample": [e[0] for e in entries[:20]],
        })
    boilerplate_clusters.sort(key=lambda c: -c["Count"])

    boilerplate_summary = {
        "ClusterCount": len(boilerplate_clusters),
        "TopClusters": boilerplate_clusters[:args.top_clusters],
    }

    # ── 5. Kurze Beschreibungen ───────────────────────────────────────
    short_samples = [
        {"SampleId": s["SampleId"], "VsaCode": s.get("VsaCode"), "Beschreibung": s.get("Beschreibung")}
        for s in samples
        if len((s.get("Beschreibung") or "").strip()) < args.short_threshold
    ]

    # ── 6. Seltene Codes ──────────────────────────────────────────────
    rare_codes = [
        {"VsaCode": code, "Count": n}
        for code, n in code_dist.items()
        if n < args.rare_threshold
    ]
    rare_codes.sort(key=lambda c: c["Count"])

    # ── 7. Versionen ──────────────────────────────────────────────────
    used_versions = collections.Counter(s.get("VersionId") for s in samples)
    versions_report = []
    for v in versions:
        live = used_versions.get(v["VersionId"], 0)
        versions_report.append({
            "VersionId": v["VersionId"],
            "CreatedAt": v["CreatedAt"],
            "DeclaredSampleCount": v.get("SampleCount", 0),
            "ActualSampleCount": live,
            "Notes": v.get("Notes", ""),
            "IsEmpty": live == 0,
        })

    # ── 8. Frame-Verfügbarkeit (Stichprobe checken) ───────────────────
    # Nur Stichprobe pruefen, sonst zu langsam bei 21k Samples.
    frame_missing = 0
    frame_checked = 0
    frame_missing_examples: list[str] = []
    for s in samples[::max(1, total // 1000)]:  # ~1000 Stichproben
        fp = (s.get("FramePath") or "").strip()
        if not fp:
            continue
        frame_checked += 1
        if not Path(fp).exists():
            frame_missing += 1
            if len(frame_missing_examples) < 20:
                frame_missing_examples.append(fp)
    miss_pct = (frame_missing / frame_checked) if frame_checked else 0
    frame_summary = {
        "Sampled": frame_checked,
        "Missing": frame_missing,
        "MissingPercent": round(miss_pct * 100, 2) if frame_checked else 0,
        # Hochrechnung NUR auf Samples mit FramePath (sonst irrefuehrend - Samples
        # ohne FramePath haben gar keine Datei zu fehlen)
        "ExtrapolatedToWithFramePath": int(round(miss_pct * has_frame_path)) if frame_checked else 0,
        "ExtrapolatedTotal_AllSamples": int(round(miss_pct * total)) if frame_checked else 0,
        "MissingExamples": frame_missing_examples,
    }

    # ── Top-Codes ───────────────────────────────────────────────────────
    top_codes = [{"VsaCode": c, "Count": n} for c, n in code_dist.most_common(50)]

    # ── Empfehlungen ablegen ────────────────────────────────────────────
    recommendations = []
    if coverage["WithRohrmaterialPercent"] < 30:
        recommendations.append(
            f"Schritt 2 (Kontext): Rohrmaterial nur bei {coverage['WithRohrmaterialPercent']}% gefuellt - "
            f"groesster Hebel. Nur leere Felder per CaseId nachziehen, ContextSource setzen.")
    if dedup_summary["ClusterCount"] > 100:
        recommendations.append(
            f"Schritt 4 (Boilerplate-Abwertung): {dedup_summary['ClusterCount']} Dublettencluster - "
            f"pro (Code,Material,DN) 1 visuell repraesentativen Frame behalten, Rest RetrievalWeight=0.1.")
    if len(rare_codes) > 0:
        recommendations.append(
            f"Schutz: {len(rare_codes)} seltene Codes (<{args.rare_threshold} Samples) - "
            f"NICHT automatisch wegen Seltenheit oder Dublettenlogik abwerten. "
            f"Bei klaren Defekten (Frame fehlt, Text leer, Code falsch) trotzdem in Review.")
    if frame_summary["MissingPercent"] > 5:
        recommendations.append(
            f"Frames: ~{frame_summary['MissingPercent']}% der Stichproben haben FramePath ohne Datei - "
            f"vor jeder Re-Indexierung pruefen.")

    # ── Report-Struktur ─────────────────────────────────────────────────
    return {
        "JobId": job_id,
        "GeneratedAt": datetime.datetime.now().isoformat(),
        "DbPath": args.db,
        "Args": vars(args),
        "Coverage": coverage,
        "QualityGateDistribution": dict(quality_dist),
        "SourceTypeDistribution": dict(source_dist),
        "TopCodes": top_codes,
        "RareCodes": {"Threshold": args.rare_threshold, "Count": len(rare_codes), "List": rare_codes},
        "ShortDescriptions": {"Threshold": args.short_threshold, "Count": len(short_samples),
                              "Sample": short_samples[:50]},
        "Duplicates": dedup_summary,
        "Boilerplate": boilerplate_summary,
        "Versions": versions_report,
        "Frames": frame_summary,
        "Recommendations": recommendations,
    }


def write_markdown(report: dict, path: Path) -> None:
    """Liest Report-Dict und schreibt eine kuratierte Markdown-Zusammenfassung."""
    cov = report["Coverage"]
    lines = [
        f"# KB Audit — {report['JobId']}",
        "",
        f"**Generated:** {report['GeneratedAt']}",
        f"**DB:** `{report['DbPath']}`",
        "",
        "## Vorher-Metriken (read-only)",
        "",
        f"- **Total Samples:** {cov['TotalSamples']:,}",
        f"- **Mit Embedding:** {cov['WithEmbedding']:,} ({cov['EmbeddingCoveragePercent']}%)",
        "",
        "### Kontextabdeckung",
        f"- Rohrmaterial: {cov['WithRohrmaterial']:,} / {cov['TotalSamples']:,} = **{cov['WithRohrmaterialPercent']}%**",
        f"- NennweiteMm:  {cov['WithNennweiteMm']:,} / {cov['TotalSamples']:,} = **{cov['WithNennweiteMmPercent']}%**",
        f"- CaseId:       {cov['WithCaseId']:,} / {cov['TotalSamples']:,} = **{cov['WithCaseIdPercent']}%**",
        f"- FramePath:    {cov['WithFramePath']:,} / {cov['TotalSamples']:,} = **{cov['WithFramePathPercent']}%**",
        f"- IsKorrigiert: {cov['IsKorrigiert']:,} ({cov['IsKorrigiertPercent']}%)",
        "",
        "### QualityGateLevel-Verteilung",
    ]
    for level, n in sorted(report["QualityGateDistribution"].items(), key=lambda x: -x[1]):
        lines.append(f"- {level}: {n:,}")

    lines += [
        "",
        "### SourceType",
    ]
    for src, n in sorted(report["SourceTypeDistribution"].items(), key=lambda x: -x[1]):
        lines.append(f"- `{src or '(leer)'}`: {n:,}")

    lines += [
        "",
        "## Top-50 Codes",
        "",
        "| VsaCode | Samples |",
        "|---|---:|",
    ]
    for c in report["TopCodes"]:
        lines.append(f"| `{c['VsaCode'] or '(leer)'}` | {c['Count']:,} |")

    rare = report["RareCodes"]
    lines += [
        "",
        f"## Seltene Codes (<{rare['Threshold']} Samples) — schuetzenswert",
        "",
        f"**{rare['Count']} Codes** mit weniger als {rare['Threshold']} Samples. NICHT abwerten, NICHT auf NeedsReview setzen.",
        "",
        "| VsaCode | Samples |",
        "|---|---:|",
    ]
    for r in rare["List"][:50]:
        lines.append(f"| `{r['VsaCode'] or '(leer)'}` | {r['Count']} |")
    if rare["Count"] > 50:
        lines.append(f"| ... | (+{rare['Count'] - 50} weitere im JSON) |")

    short = report["ShortDescriptions"]
    lines += [
        "",
        f"## Kurze Beschreibungen (<{short['Threshold']} Zeichen)",
        "",
        f"**{short['Count']} Samples** mit kurzer Beschreibung. Empfehlung: TextSignalWeak=1 markieren, NICHT ausschliessen — Bild-Retrieval bleibt voll dabei.",
    ]

    dup = report["Duplicates"]
    lines += [
        "",
        "## Dubletten-Cluster (Code + Beschreibung + Material + DN)",
        "",
        f"- **{dup['ClusterCount']} Cluster** mit insgesamt **{dup['AffectedSamples']:,} betroffenen Samples**",
        f"- Wenn pro Cluster 1 Repraesentant behalten: **{dup['RedundantSamples']:,} Samples ueberzaehlig**",
        f"- Top {len(dup['TopClusters'])} Cluster:",
        "",
        "| # | VsaCode | Material | DN | Count | Beschreibung (Anfang) |",
        "|---:|---|---|---:|---:|---|",
    ]
    for i, c in enumerate(dup["TopClusters"], 1):
        desc = c["Beschreibung"].replace("|", "\\|")[:80]
        lines.append(f"| {i} | `{c['VsaCode']}` | {c['Rohrmaterial'] or '-'} | "
                     f"{c['NennweiteMm'] or '-'} | {c['Count']:,} | {desc} |")

    bp = report["Boilerplate"]
    lines += [
        "",
        "## Boilerplate (gleiche Beschreibung quer ueber Codes)",
        "",
        f"- **{bp['ClusterCount']} Boilerplate-Cluster** mit ≥5 identischen Texten",
        "",
        "| # | Count | DistinctCodes | TopCode | Text (Anfang) |",
        "|---:|---:|---:|---|---|",
    ]
    for i, c in enumerate(bp["TopClusters"], 1):
        desc = c["Beschreibung"].replace("|", "\\|")[:80]
        top_code, top_count = c["TopCode"]
        lines.append(f"| {i} | {c['Count']:,} | {c['DistinctCodes']} | "
                     f"`{top_code}`×{top_count:,} | {desc} |")

    lines += [
        "",
        "## Versionen",
        "",
        "Versionen mit `Declared > 0` aber `Actual = 0` sind keine leeren Klick-Runs - "
        "das sind sehr wahrscheinlich **fehlgeschlagene oder abgebrochene Imports** "
        "(Versions-Eintrag wurde geschrieben, die zugehoerigen Samples landeten unter einer "
        "anderen VersionId oder wurden nie persistiert). Vor Archivieren: Ursache pruefen "
        "(Crash-Log? Re-Import? Bewusster Rollback?).",
        "",
        "| VersionId | CreatedAt | Declared | Tatsaechlich | Status | Notes |",
        "|---|---|---:|---:|---|---|",
    ]
    for v in report["Versions"]:
        status = ""
        if v["IsEmpty"] and v["DeclaredSampleCount"] > 0:
            status = "**Import abgebrochen?**"
        elif v["IsEmpty"]:
            status = "leer"
        notes = (v["Notes"] or "").replace("|", "\\|")[:60]
        lines.append(f"| `{v['VersionId'][:16]}...` | {v['CreatedAt']} | "
                     f"{v['DeclaredSampleCount']:,} | {v['ActualSampleCount']:,} | {status} | {notes} |")

    fr = report["Frames"]
    lines += [
        "",
        "## Frame-Verfuegbarkeit (Stichprobe)",
        "",
        f"- {fr['Sampled']} Samples mit FramePath geprueft",
        f"- {fr['Missing']} Frames fehlen ({fr['MissingPercent']}%)",
        f"- Hochrechnung auf Samples mit FramePath: ~{fr['ExtrapolatedToWithFramePath']:,} fehlend",
        f"- Hochrechnung auf alle Samples (inkl. ohne FramePath): ~{fr['ExtrapolatedTotal_AllSamples']:,}",
    ]

    lines += [
        "",
        "## Empfehlungen (Plan v2 — naechste Schritte)",
        "",
    ]
    for i, r in enumerate(report["Recommendations"], 1):
        lines.append(f"{i}. {r}")
    if not report["Recommendations"]:
        lines.append("- Keine kritischen Befunde.")

    lines += [
        "",
        "---",
        "",
        "**Hinweis:** DB read-only, Report writing only. Keine UPDATE/DELETE/INSERT in der KB. "
        "Es wurden ausschliesslich JSON+Markdown-Reports geschrieben.",
        "",
    ]

    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    args = parse_args()
    db_path = Path(args.db)
    if not db_path.exists():
        print(f"FEHLER: DB nicht gefunden: {db_path}", file=sys.stderr)
        return 2

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    # Read-only Connection (uri=True ermoeglicht ?mode=ro Flag).
    # immutable=1 zusaetzlich: SQLite umgeht WAL/SHM und setzt kein FileLock —
    # damit klappt der Audit auch waehrend die App die KB offen haelt. Annahme:
    # die DB-Datei aendert sich waehrend des Lesens nicht (fuer einen Audit ok).
    uri = f"file:{db_path.as_posix()}?mode=ro&immutable=1"
    con = sqlite3.connect(uri, uri=True)

    try:
        print(f"Lese Samples aus {db_path}...")
        samples = fetch_all_samples(con)
        versions = fetch_versions(con)
        embedding_count = fetch_embedding_count(con)
        print(f"  -> {len(samples):,} Samples, {len(versions)} Versionen, "
              f"{embedding_count:,} Embeddings.")

        report = build_report(samples, versions, embedding_count, args)
    finally:
        con.close()

    job_id = report["JobId"]
    json_path = out_dir / f"kb_audit_{job_id}.json"
    md_path = out_dir / f"kb_audit_{job_id}.md"

    json_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    write_markdown(report, md_path)

    print(f"\nReport geschrieben:")
    print(f"  JSON:     {json_path}")
    print(f"  Markdown: {md_path}")
    print(f"\nKein UPDATE/DELETE/INSERT ausgefuehrt - DB unveraendert.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
