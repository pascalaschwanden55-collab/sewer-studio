#!/usr/bin/env python3
"""
Knowledge-Base Kontext-Filling Dry-Run (Schritt 2 von Plan v2)

Liest die KnowledgeBase.db NUR-LESEND und erstellt einen Report, welche Samples
mit Material/Nennweite nachgezogen werden KOENNTEN, wenn die ueblichen
Stammdaten-Quellen (FDB/PDF/XTF aus dem Original-Export) verfuegbar waeren.

Es wird NICHTS in der DB veraendert. Es werden auch keine Stammdaten-Quellen
aktiv eingelesen — das macht spaeter ein separater C#-Schritt
(StammdatenAggregator.Build) und schreibt eine haltungs_stammdaten.json. Dieses
Skript kann optional eine bereits exportierte JSON-Map konsumieren und die
tatsaechliche Trefferquote pro CaseId zeigen, bleibt aber auch ohne sie
nutzbar (Klassifikation der Fillbarkeit).

Aufruf:
    python tools/kb_audit/kb_context_dryrun.py
        [--db C:/KI_BRAIN/KnowledgeBase.db]
        [--out C:/KI_BRAIN/kb_audit/]
        [--stammdaten C:/KI_BRAIN/stammdaten/haltungs_stammdaten.json]

Optionales Stammdaten-JSON-Format (Map<HaltungsKey, dict>):
    {
        "1.01-59007":      {"Material": "Beton",     "DN_mm": 300},
        "06.691078-691070": {"Material": "Steinzeug", "DN_mm": 250},
        ...
    }
HaltungsKey wird mit derselben Normalisierung wie StammdatenAggregator
erzeugt (Trim, Whitespace entfernen, "/" -> "-", "–"/"—" -> "-").

Ausgabe:
    <out>/kb_context_dryrun_<YYYYMMDD_HHMMSS>.json
    <out>/kb_context_dryrun_<YYYYMMDD_HHMMSS>.md
"""

from __future__ import annotations
import argparse
import collections
import datetime
import json
import re
import sqlite3
import sys
from pathlib import Path

# ── Klassifikations-Konstanten ─────────────────────────────────────────────
# Klassen der "Fillability" — gespiegelt im Report
FILL_ALREADY_COMPLETE   = "AlreadyComplete"        # Material + DN vorhanden
FILL_PARTIAL_HAS_MAT    = "PartialOnlyMaterial"    # Material da, DN fehlt
FILL_PARTIAL_HAS_DN     = "PartialOnlyDn"          # DN da, Material fehlt
FILL_HALTUNG_CLEAN      = "EmptyButHaltungIdClean" # Beides leer, CaseId = sauberer Haltungs-Key
FILL_HALTUNG_PATH       = "EmptyButHaltungInPath"  # Beides leer, CaseId = Pfad mit Haltungs-Segment
FILL_NOT_FILLABLE       = "EmptyAndNotFillable"    # Beides leer, kein verwertbarer Haltungs-Key
                                                   #   (manual-*, feedback_*, leer)

# Pattern fuer "Haltungs-ID-aussehende" Segmente.
# Echte VSA/IBAK-Knotennummern bestehen aus Ziffern und (optional) Punkten als
# Zonen-Trennzeichen. Mindestens ein Bindestrich trennt zwei Knoten. Auch
# verkettete Pfad-Formen wie "06.691078-691070-06.24373-14.24374" sind erlaubt.
# Buchstaben werden bewusst NICHT akzeptiert, sonst rutscht Pfad-Schrott wie
# "Zone6.16-PDF" oder "Zone6.02-DatenDataver" als vermeintliche Haltungs-ID
# durch und blaeht die Kandidaten-Liste auf.
HALTUNGS_SEGMENT = re.compile(r"^[0-9.]+(-[0-9.]+)+$")

# Pattern fuer Wegwerf-CaseIds, die niemals Haltungs-Stammdaten haben werden:
NOT_FILLABLE_CASEID_PREFIXES = ("manual-", "feedback_", "feedback-")


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="KB Kontext-Filling Dry-Run")
    p.add_argument("--db", default=r"C:\KI_BRAIN\KnowledgeBase.db",
                   help="Pfad zur KnowledgeBase.db (read-only)")
    p.add_argument("--out", default=r"C:\KI_BRAIN\kb_audit",
                   help="Ausgabe-Verzeichnis")
    p.add_argument("--stammdaten", default=None,
                   help="Optional: JSON-Map mit Haltungs-Stammdaten zur Trefferquoten-Analyse")
    p.add_argument("--top-clusters", type=int, default=30,
                   help="Anzahl groesster Cluster pro Tabelle im Report")
    return p.parse_args()


# ── CaseId-Klassifikation ────────────────────────────────────────────────--

def normalize_haltungs_key(raw: str) -> str:
    """Spiegelt StammdatenAggregator.NormalizeKey (C#)."""
    if not raw:
        return ""
    return raw.replace(" ", "").replace("/", "-").replace("–", "-").replace("—", "-")


def extract_haltungs_keys(case_id: str) -> list[str]:
    """
    Liefert Haltungs-Kandidaten aus einer CaseId zurueck, sortiert nach Spezifitaet
    (laengster zuerst). Beispiele:
      "1.01-59007"                      -> ["1.01-59007"]
      "06.691078-691070/06.24373-14.24374"
                                        -> ["06.691078-691070-06.24373-14.24374",   (volle Pfad-Normalisierung)
                                            "06.24373-14.24374",                    (innerer Schritt)
                                            "06.691078-691070"]
      "diverse_Strassen_.../Misc/Docu" -> []   (keine Haltungs-Form gefunden)
    """
    if not case_id:
        return []
    case_id = case_id.strip()
    if not case_id:
        return []

    keys: list[str] = []

    # 1) Volle Normalisierung (gleiche Logik wie C#-Aggregator)
    full_norm = normalize_haltungs_key(case_id)
    if HALTUNGS_SEGMENT.match(full_norm):
        keys.append(full_norm)

    # 2) Einzelne Segmente, die wie eine Haltungs-ID aussehen
    for sep in ("/", "\\"):
        if sep in case_id:
            segments = [s.strip() for s in case_id.split(sep) if s.strip()]
            for seg in segments:
                if HALTUNGS_SEGMENT.match(seg) and seg not in keys:
                    keys.append(seg)
            break  # nur ein Trennzeichen-Modus pro CaseId

    # Wenn nichts gefunden und CaseId selbst wie Haltungs-ID aussieht
    if not keys and HALTUNGS_SEGMENT.match(case_id):
        keys.append(case_id)

    return keys


def classify_sample(sample: dict) -> tuple[str, list[str]]:
    """
    Liefert (FillabilityKlasse, [HaltungsKandidaten]).
    Die Kandidaten sind potentielle Lookup-Keys fuer das Stammdaten-JSON.
    """
    has_material = bool((sample.get("Rohrmaterial") or "").strip())
    has_dn = sample.get("NennweiteMm") is not None and (
        sample.get("NennweiteMm") or 0) > 0

    if has_material and has_dn:
        return FILL_ALREADY_COMPLETE, []
    if has_material and not has_dn:
        return FILL_PARTIAL_HAS_MAT, extract_haltungs_keys(sample.get("CaseId") or "")
    if has_dn and not has_material:
        return FILL_PARTIAL_HAS_DN, extract_haltungs_keys(sample.get("CaseId") or "")

    # Beide leer
    case_id = (sample.get("CaseId") or "").strip()
    if not case_id:
        return FILL_NOT_FILLABLE, []
    if any(case_id.lower().startswith(p) for p in NOT_FILLABLE_CASEID_PREFIXES):
        return FILL_NOT_FILLABLE, []

    candidates = extract_haltungs_keys(case_id)
    if not candidates:
        return FILL_NOT_FILLABLE, []

    # Hat die CaseId Pfad-Trennzeichen?
    if "/" in case_id or "\\" in case_id:
        return FILL_HALTUNG_PATH, candidates
    return FILL_HALTUNG_CLEAN, candidates


# ── DB-Zugriff ───────────────────────────────────────────────────────────--

def fetch_all_samples(con: sqlite3.Connection) -> list[dict]:
    cur = con.cursor()
    cur.execute("""
        SELECT SampleId, CaseId, VsaCode,
               COALESCE(SourceType, '') AS SourceType,
               Rohrmaterial,
               NennweiteMm
        FROM Samples
    """)
    cols = [d[0] for d in cur.description]
    return [dict(zip(cols, row)) for row in cur.fetchall()]


# ── Stammdaten-Map (optional) ────────────────────────────────────────────--

def load_stammdaten_map(path: str | None) -> dict[str, dict]:
    """
    Liest die optionale Haltungs-Stammdaten-JSON. Keys werden mit derselben
    Normalisierung wie StammdatenAggregator.NormalizeKey verglichen.
    """
    if not path:
        return {}
    p = Path(path)
    if not p.exists():
        print(f"WARN: Stammdaten-Datei nicht gefunden: {p}", file=sys.stderr)
        return {}
    raw = json.loads(p.read_text(encoding="utf-8"))
    return {normalize_haltungs_key(k): v for k, v in raw.items()}


# ── Report-Aufbau ────────────────────────────────────────────────────────--

def build_report(samples: list[dict], stammdaten: dict[str, dict],
                 args: argparse.Namespace) -> dict:
    job_id = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    total = len(samples)

    # Per-Klasse Zaehler + Stichproben
    class_counts: collections.Counter = collections.Counter()
    class_per_source: dict[str, collections.Counter] = collections.defaultdict(
        collections.Counter)
    class_samples: dict[str, list[str]] = collections.defaultdict(list)

    # Kandidaten-Map: HaltungsKey -> Anzahl Samples, die ihn brauchen
    candidate_demand: collections.Counter = collections.Counter()
    # Pro Sample der GEWAEHLTE Lookup-Key (erster Treffer im JSON, sonst erster Kandidat)
    sample_chosen_key: dict[str, str] = {}

    # Wenn Stammdaten-Map vorhanden: Trefferzaehler
    join_hits = 0
    join_misses = 0
    join_only_material = 0
    join_only_dn = 0
    join_both = 0
    miss_examples: list[str] = []

    for s in samples:
        klass, candidates = classify_sample(s)
        class_counts[klass] += 1
        class_per_source[s.get("SourceType") or ""][klass] += 1

        if len(class_samples[klass]) < 30:
            class_samples[klass].append(s["SampleId"])

        if klass in (FILL_ALREADY_COMPLETE, FILL_NOT_FILLABLE):
            continue

        for c in candidates:
            candidate_demand[c] += 1

        if not stammdaten:
            continue

        chosen = None
        for c in candidates:
            if c in stammdaten:
                chosen = c
                break
        if chosen is None:
            join_misses += 1
            if len(miss_examples) < 30 and candidates:
                miss_examples.append(f"{s['SampleId']}  candidates={candidates}")
            continue

        sample_chosen_key[s["SampleId"]] = chosen
        join_hits += 1
        entry = stammdaten[chosen]
        has_mat = bool((entry.get("Material") or "").strip())
        has_dn = entry.get("DN_mm") is not None and (entry.get("DN_mm") or 0) > 0
        if has_mat and has_dn:
            join_both += 1
        elif has_mat:
            join_only_material += 1
        elif has_dn:
            join_only_dn += 1
        # Wenn beides leer im JSON: zaehlt als Hit ohne Nutzen — separat
        # nicht ausgewiesen, weil das eher ein Stammdaten-Quelle-Problem ist.

    # Pro Klasse Prozent
    pct = lambda n: round(n / total * 100, 2) if total else 0
    fillability_summary = {
        klass: {"Count": n, "Percent": pct(n),
                "SampleIds_Sample": class_samples[klass][:20]}
        for klass, n in class_counts.most_common()
    }

    # Kandidaten — Top der gefragten Haltungs-Keys (zeigt, welche Stammdaten
    # zuerst beschafft werden sollten, weil ihr Effekt am groessten ist)
    top_candidates = [
        {"HaltungsKey": k, "DependentSamples": n,
         "InStammdaten": (k in stammdaten) if stammdaten else None}
        for k, n in candidate_demand.most_common(args.top_clusters)
    ]

    # Pro SourceType die Verteilung
    per_source = {}
    for src, counts in class_per_source.items():
        src_total = sum(counts.values())
        per_source[src or "(leer)"] = {
            "Total": src_total,
            "Classes": {k: counts.get(k, 0) for k in (
                FILL_ALREADY_COMPLETE, FILL_PARTIAL_HAS_MAT, FILL_PARTIAL_HAS_DN,
                FILL_HALTUNG_CLEAN, FILL_HALTUNG_PATH, FILL_NOT_FILLABLE)},
        }

    # Was waere die theoretische Coverage NACH dem Filling?
    fillable_now = (
        class_counts.get(FILL_PARTIAL_HAS_MAT, 0)
        + class_counts.get(FILL_PARTIAL_HAS_DN, 0)
        + class_counts.get(FILL_HALTUNG_CLEAN, 0)
        + class_counts.get(FILL_HALTUNG_PATH, 0)
    )
    forecast = {
        "AlreadyComplete": class_counts.get(FILL_ALREADY_COMPLETE, 0),
        "TheoreticallyFillable": fillable_now,
        "NotFillable": class_counts.get(FILL_NOT_FILLABLE, 0),
        "MaxCoveragePercent": pct(class_counts.get(FILL_ALREADY_COMPLETE, 0) + fillable_now),
    }

    # Join-Realitaet wenn Stammdaten vorhanden
    if stammdaten:
        join_total = join_hits + join_misses
        join_summary = {
            "StammdatenEntries": len(stammdaten),
            "CandidateSamples": join_total,
            "Hits": join_hits,
            "Misses": join_misses,
            "HitPercent": round(join_hits / join_total * 100, 2) if join_total else 0,
            "BothMatAndDn": join_both,
            "OnlyMaterial": join_only_material,
            "OnlyDn": join_only_dn,
            "MissExamples": miss_examples,
        }
    else:
        join_summary = None

    # Empfehlungen (text only — KEIN UPDATE/DELETE/INSERT generiert)
    recs: list[str] = []
    if not stammdaten:
        recs.append(
            "Kein --stammdaten <json> angegeben. Schritt 2a (dieser Lauf) zeigt "
            "nur die Klassifikation der Fillbarkeit. Vor dem Apply-Schritt: "
            "C#-Tool laufen lassen, das pro Projekt-Export den StammdatenAggregator "
            "aufruft und eine haltungs_stammdaten.json schreibt. Dann diesen "
            "Dry-Run mit --stammdaten erneut starten und die Trefferquote pruefen.")
    else:
        if join_summary["HitPercent"] < 80:
            recs.append(
                f"Stammdaten-Trefferquote nur {join_summary['HitPercent']}% — vor dem "
                "Apply-Schritt erst die Stammdaten-Quelle vervollstaendigen oder die "
                "CaseId-Normalisierung ueberpruefen, sonst bleibt zu viel Kontext leer.")
        elif join_summary["HitPercent"] >= 95:
            recs.append(
                f"Stammdaten-Trefferquote {join_summary['HitPercent']}% — Apply-Schritt "
                "kann erfolgen. Pflicht: Snapshot anlegen, in einer Transaktion "
                "ausfuehren, NUR leere Felder fuellen (Rohrmaterial IS NULL OR ''  "
                "bzw. NennweiteMm IS NULL OR <=0), Vorher/Nachher-Audit erzeugen.")

    not_fillable = class_counts.get(FILL_NOT_FILLABLE, 0)
    if not_fillable > 0:
        recs.append(
            f"{not_fillable:,} Samples sind ueber Stammdaten NICHT fuellbar "
            "(manual-/feedback-/leerer CaseId). Diese sind kein Fehler — sie "
            "sind nicht aus einem Projekt-Import entstanden. NICHT abwerten.")

    return {
        "JobId": job_id,
        "GeneratedAt": datetime.datetime.now().isoformat(),
        "DbPath": args.db,
        "StammdatenPath": args.stammdaten,
        "TotalSamples": total,
        "Fillability": fillability_summary,
        "ForecastAfterFill": forecast,
        "PerSourceType": per_source,
        "TopHaltungsCandidates": top_candidates,
        "JoinAgainstStammdaten": join_summary,
        "Recommendations": recs,
    }


# ── Markdown-Renderer ────────────────────────────────────────────────────--

def write_markdown(report: dict, path: Path) -> None:
    lines = [
        f"# KB Kontext-Filling Dry-Run — {report['JobId']}",
        "",
        f"**Generated:** {report['GeneratedAt']}",
        f"**DB:** `{report['DbPath']}` (read-only)",
        f"**Stammdaten-JSON:** `{report['StammdatenPath'] or '(keine)'}`",
        f"**Total Samples:** {report['TotalSamples']:,}",
        "",
        "## Fillability-Klassen",
        "",
        "Wie viele Samples koennen mit Stammdaten gefuellt werden — und welche nicht.",
        "",
        "| Klasse | Count | Percent |",
        "|---|---:|---:|",
    ]
    for klass, info in report["Fillability"].items():
        lines.append(f"| `{klass}` | {info['Count']:,} | {info['Percent']}% |")

    fc = report["ForecastAfterFill"]
    lines += [
        "",
        "## Prognose nach Filling (theoretisches Maximum)",
        "",
        f"- AlreadyComplete:        **{fc['AlreadyComplete']:,}**",
        f"- TheoreticallyFillable:  **{fc['TheoreticallyFillable']:,}**",
        f"- NotFillable (manual/feedback/leer): **{fc['NotFillable']:,}**",
        f"- Erreichbare Coverage (Material ODER DN): **{fc['MaxCoveragePercent']}%**",
        "",
        "Hinweis: Die echte Trefferquote haengt davon ab, ob die Stammdaten-Quelle "
        "die zugehoerigen Haltungen tatsaechlich kennt. Siehe Abschnitt "
        "*Join-Trefferquote* (nur mit `--stammdaten`).",
        "",
        "## Verteilung pro SourceType",
        "",
        "| SourceType | Total | Complete | Halt-Clean | Halt-Path | Partial-Mat | Partial-DN | NotFillable |",
        "|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for src, info in sorted(report["PerSourceType"].items(), key=lambda x: -x[1]["Total"]):
        c = info["Classes"]
        lines.append(
            f"| `{src}` | {info['Total']:,} | "
            f"{c.get(FILL_ALREADY_COMPLETE, 0):,} | "
            f"{c.get(FILL_HALTUNG_CLEAN, 0):,} | "
            f"{c.get(FILL_HALTUNG_PATH, 0):,} | "
            f"{c.get(FILL_PARTIAL_HAS_MAT, 0):,} | "
            f"{c.get(FILL_PARTIAL_HAS_DN, 0):,} | "
            f"{c.get(FILL_NOT_FILLABLE, 0):,} |")

    lines += [
        "",
        "## Top-nachgefragte Haltungs-Keys",
        "",
        "Welche Haltungen die meisten leeren Samples bedienen wuerden — also wo "
        "die Beschaffung von Stammdaten den groessten Effekt hat. *Hinweis:* "
        "Ein Sample mit Pfad-CaseId (z.B. zwei verschachtelte Haltungen) wird "
        "fuer JEDEN seiner Kandidaten-Keys gezaehlt, weil im Apply-Schritt der "
        "erste passende Treffer gewinnt — die Summe ueber die Spalte ist deshalb "
        "groesser als die Anzahl fillbarer Samples.",
        "",
    ]
    if report["JoinAgainstStammdaten"] is not None:
        lines += [
            "| HaltungsKey | DependentSamples | InStammdaten? |",
            "|---|---:|---|",
        ]
        for c in report["TopHaltungsCandidates"]:
            mark = ":white_check_mark:" if c["InStammdaten"] else ":x:"
            lines.append(f"| `{c['HaltungsKey']}` | {c['DependentSamples']:,} | {mark} |")
    else:
        lines += [
            "| HaltungsKey | DependentSamples |",
            "|---|---:|",
        ]
        for c in report["TopHaltungsCandidates"]:
            lines.append(f"| `{c['HaltungsKey']}` | {c['DependentSamples']:,} |")

    j = report["JoinAgainstStammdaten"]
    if j is not None:
        lines += [
            "",
            "## Join-Trefferquote gegen Stammdaten",
            "",
            f"- StammdatenEntries:   {j['StammdatenEntries']:,}",
            f"- CandidateSamples:    {j['CandidateSamples']:,}",
            f"- Hits:                **{j['Hits']:,} ({j['HitPercent']}%)**",
            f"- Misses:              {j['Misses']:,}",
            f"- BothMatAndDn:        {j['BothMatAndDn']:,}",
            f"- OnlyMaterial:        {j['OnlyMaterial']:,}",
            f"- OnlyDn:              {j['OnlyDn']:,}",
            "",
            "### Beispiele Misses (max 30)",
            "",
            "```",
        ]
        for m in j["MissExamples"]:
            lines.append(m)
        lines.append("```")
    else:
        lines += [
            "",
            "## Join-Trefferquote gegen Stammdaten",
            "",
            "_Kein Stammdaten-JSON uebergeben (--stammdaten <pfad>). Diesen Schritt "
            "wiederholen, sobald ein C#-Export der Aggregation vorliegt._",
        ]

    lines += [
        "",
        "## Empfehlungen",
        "",
    ]
    for i, r in enumerate(report["Recommendations"], 1):
        lines.append(f"{i}. {r}")

    lines += [
        "",
        "---",
        "",
        "**Hinweis:** DB read-only, Report writing only. Keine UPDATE/DELETE/INSERT in "
        "der KB. Auch keine Schreibzugriffe auf Stammdaten-Quellen. Dieses Skript "
        "klassifiziert nur und erstellt JSON+Markdown-Reports.",
        "",
    ]

    path.write_text("\n".join(lines), encoding="utf-8")


# ── Main ─────────────────────────────────────────────────────────────────--

def main() -> int:
    args = parse_args()
    db_path = Path(args.db)
    if not db_path.exists():
        print(f"FEHLER: DB nicht gefunden: {db_path}", file=sys.stderr)
        return 2

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    # immutable=1 zusaetzlich zu mode=ro: SQLite oeffnet die Datei OHNE WAL/SHM
    # zu beruehren und ohne ein FileLock zu setzen. Wichtig wenn die Live-KB
    # gerade von der App gehalten wird — sonst kommt SQLITE_BUSY trotz mode=ro.
    # Annahme dabei: die DB-Datei aendert sich waehrend des Lesens nicht. Fuer
    # ein Audit ist das in Ordnung.
    uri = f"file:{db_path.as_posix()}?mode=ro&immutable=1"
    con = sqlite3.connect(uri, uri=True)
    try:
        print(f"Lese Samples aus {db_path}...")
        samples = fetch_all_samples(con)
        print(f"  -> {len(samples):,} Samples")
    finally:
        con.close()

    stammdaten = load_stammdaten_map(args.stammdaten)
    if args.stammdaten:
        print(f"Stammdaten-Map: {len(stammdaten):,} Eintraege geladen.")

    report = build_report(samples, stammdaten, args)

    job_id = report["JobId"]
    json_path = out_dir / f"kb_context_dryrun_{job_id}.json"
    md_path = out_dir / f"kb_context_dryrun_{job_id}.md"

    json_path.write_text(json.dumps(report, indent=2, ensure_ascii=False),
                         encoding="utf-8")
    write_markdown(report, md_path)

    print(f"\nReport geschrieben:")
    print(f"  JSON:     {json_path}")
    print(f"  Markdown: {md_path}")
    print(f"\nKein UPDATE/DELETE/INSERT ausgefuehrt - DB unveraendert.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
