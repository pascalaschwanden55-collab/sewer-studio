#!/usr/bin/env python3
"""
Knowledge-Base Kontext-Filling Apply (Schritt 2c von Plan v2)

Schreibt Material/Nennweite NUR in leere Sample-Felder, basierend auf einer
zuvor von tools/StammdatenExporter erzeugten haltungs_stammdaten.json. Vor
dem UPDATE wird ein Snapshot der KB gezogen, das UPDATE selbst laeuft in
einer Transaktion und es werden zusaetzlich Audit-Spalten gesetzt
(ContextSource / ContextUpdatedAt / ContextConfidence).

Sicherheitsmodi:
    Default (DRY)   : zeigt alle geplanten UPDATEs als Diff, schreibt NICHTS.
    --apply         : schreibt nach Snapshot + Transaktion. Ohne --apply ist
                       jede Aenderung an der DB ausgeschlossen.
    --rollback <db> : ersetzt die Live-DB durch einen frueheren Snapshot
                       (Sicherheitsnetz, falls der Lauf zurueckgenommen werden soll).

Aufruf:
    python tools/kb_audit/kb_context_apply.py
        --stammdaten C:/KI_BRAIN/stammdaten/haltungs_stammdaten.json
        [--db C:/KI_BRAIN/KnowledgeBase.db]
        [--out C:/KI_BRAIN/kb_audit]
        [--snapshots C:/KI_BRAIN/snapshots]
        [--apply]
        [--max-confidence-source Xtf|Pdf|Fdb]   (Default: alle Quellen)
"""

from __future__ import annotations
import argparse
import collections
import datetime
import json
import re
import shutil
import sqlite3
import sys
from pathlib import Path

# Pattern wie in kb_context_dryrun.py — synchron halten.
HALTUNGS_SEGMENT = re.compile(r"^[0-9.]+(-[0-9.]+)+$")
NOT_FILLABLE_CASEID_PREFIXES = ("manual-", "feedback_", "feedback-")

# Quellen-Konfidenz: hoehere Quelle -> hoeheres Vertrauen
SOURCE_CONFIDENCE = {"Xtf": 0.95, "Pdf": 0.85, "Fdb": 0.65, "None": 0.0, None: 0.0}
SOURCE_RANK       = {"Xtf": 1, "Pdf": 2, "Fdb": 3}

# ── Material-Whitelist ───────────────────────────────────────────────────--
# Der PDF-Parser des Aggregators liest stellenweise die falsche Tabellenspalte
# und liefert dann Werte wie 'Kreisprofil', 'Kamerasystem', 'DN/Laenge [m]',
# '10254-07.10173' usw. Solche Werte duerfen NICHT als Material in die KB.
#
# Strategie: Substring-Whitelist (case-insensitive). Ein Wert ist plausibel,
# wenn er irgendwo eines der bekannten Material-Stamm-Woerter enthaelt:
#   "Hartpolyethylen" -> enthaelt "polyethylen" -> ok
#   "Ortsbeton"       -> enthaelt "beton"       -> ok
#   "Grauguss"        -> enthaelt "guss"        -> ok
#   "Kreisprofil"     -> enthaelt nichts        -> reject
#   "10254->07.10173" -> enthaelt nichts        -> reject
ALLOWED_MATERIAL_SUBSTRINGS = (
    "beton", "stb",
    "zement",            # faserzement, asbestzement, ortszement
    "steinzeug", "stz",
    "kunststoff", "ks",
    "polyvinyl", "pvc",
    "polyethylen", "pe-hd", "pe-h", "hdpe", "ldpe",
    "polypropylen", "pp",
    "polyester",         # GUP/Polyester-Sanierung
    "gup",               # glasfaserverstaerkter ungesaettigter Polyester
    "guss",              # gusseisen, grauguss, sphaerogu, hartguss
    "duktil",            # Duktilguss / Duktiler Guss (Gusseisen-Variante)
    "stahl",
    "gfk", "glasfaser",
    "ziegel", "klinker", "mauerwerk",
    "holz",
    "blech",
    "asbest",
    "inliner",           # Sanierungs-Inliner
    "schlauch",          # Schlauchliner
    "epoxid",
    "harz",
    "keramik",
)

# Werte die exakt blockiert werden, auch wenn ein erlaubter Substring drinsteckt.
BLOCKED_EXACT_VALUES = {
    "unbekannt", "unbekanntes material",
    "k.a.", "k.a", "ka",
}


def material_is_plausible(value: str | None) -> bool:
    """Substring-Whitelist + harte Block-Liste."""
    if not value:
        return False
    v = value.strip().lower()
    if not v:
        return False
    if v in BLOCKED_EXACT_VALUES:
        return False
    return any(s in v for s in ALLOWED_MATERIAL_SUBSTRINGS)


# ── Material-Wechsel-Schutz ──────────────────────────────────────────────--
# Manche Sample-Beschreibungen erwaehnen explizit das Material an genau
# diesem Meterstand — z.B. "Rohrmaterialwechsel: Polypropylen". Wenn die
# Beschreibung ein Material erwaehnt, das vom Plan-Wert (Haltungs-Default
# aus PDF/XTF) abweicht, ueberspringen wir das Material-UPDATE fuer dieses
# Sample. Der DN-Wert bleibt unberuehrt: DN aendert sich praktisch nie
# innerhalb einer Haltung, auch wenn das Material wechselt.
#
# Mapping ist gross genug fuer die haeufigsten VSA-Begriffe; die Token sind
# Substrings, also faengt "Hartpolyethylen" auch "polyethylen" ab.
MATERIAL_FAMILIES = {
    "beton":         ("beton", "stb", "stahlbeton"),
    "zement":        ("zement", "faserzement", "asbestzement"),
    "steinzeug":     ("steinzeug", "stz"),
    "pvc":           ("pvc", "polyvinyl"),
    "pe":            ("polyethylen", "pe-hd", "hdpe", "ldpe"),
    "pp":            ("polypropylen",),
    "polyester":     ("polyester", "gup"),
    "guss":          ("guss", "gusseisen"),
    "stahl":         ("stahl",),
    "gfk":           ("gfk", "glasfaser"),
    "ziegel":        ("ziegel", "klinker", "mauerwerk"),
    "holz":          ("holz",),
    "blech":         ("blech",),
    "asbest":        ("asbest",),
    "inliner":       ("inliner", "schlauchliner"),
    "epoxid":        ("epoxid", "harz"),
    "keramik":       ("keramik",),
}


def material_family(value: str | None) -> str | None:
    if not value:
        return None
    v = value.lower()
    for family, tokens in MATERIAL_FAMILIES.items():
        if any(t in v for t in tokens):
            return family
    return None


def description_overrides_material(beschreibung: str | None,
                                   plan_material: str) -> str | None:
    """
    Liefert die Material-Familie aus der Beschreibung, WENN sie konkret
    abweicht — sonst None. None heisst: Plan darf schreiben.
    """
    if not beschreibung:
        return None
    desc_fam = material_family(beschreibung)
    if desc_fam is None:
        return None
    plan_fam = material_family(plan_material)
    if plan_fam is None:
        return None
    return desc_fam if desc_fam != plan_fam else None


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="KB Kontext-Filling Apply")
    p.add_argument("--db", default=r"C:\KI_BRAIN\KnowledgeBase.db",
                   help="Pfad zur KnowledgeBase.db")
    p.add_argument("--stammdaten", required=True,
                   help="JSON-Map vom StammdatenExporter")
    p.add_argument("--out", default=r"C:\KI_BRAIN\kb_audit",
                   help="Verzeichnis fuer Apply-Report (JSON+MD)")
    p.add_argument("--snapshots", default=r"C:\KI_BRAIN\snapshots",
                   help="Verzeichnis fuer Snapshot-Kopie der DB")
    p.add_argument("--apply", action="store_true",
                   help="Tatsaechlich schreiben. Ohne dieses Flag wird NUR ein "
                        "Plan-Report erzeugt; die DB bleibt unveraendert.")
    p.add_argument("--max-confidence-source", choices=["Xtf", "Pdf", "Fdb"],
                   default=None,
                   help="Niedrigste akzeptierte Quelle. Bsp. 'Pdf' = nur XTF+PDF, "
                        "FDB-Werte werden ignoriert. Default: alle akzeptiert.")
    return p.parse_args()


# ── Helpers (gespiegelt aus kb_context_dryrun.py) ─────────────────────────-

def normalize_haltungs_key(raw: str) -> str:
    if not raw:
        return ""
    return raw.replace(" ", "").replace("/", "-").replace("–", "-").replace("—", "-")


def extract_haltungs_keys(case_id: str) -> list[str]:
    if not case_id:
        return []
    case_id = case_id.strip()
    if not case_id:
        return []
    keys: list[str] = []

    full_norm = normalize_haltungs_key(case_id)
    if HALTUNGS_SEGMENT.match(full_norm):
        keys.append(full_norm)

    for sep in ("/", "\\"):
        if sep in case_id:
            for seg in [s.strip() for s in case_id.split(sep) if s.strip()]:
                if HALTUNGS_SEGMENT.match(seg) and seg not in keys:
                    keys.append(seg)
            break

    if not keys and HALTUNGS_SEGMENT.match(case_id):
        keys.append(case_id)
    return keys


def load_stammdaten(path: str) -> dict[str, dict]:
    raw = json.loads(Path(path).read_text(encoding="utf-8"))
    return {normalize_haltungs_key(k): v for k, v in raw.items()}


# ── Schema-Migration (additiv, idempotent) ────────────────────────────────-

def migrate_context_columns(con: sqlite3.Connection) -> list[str]:
    """Fuegt ContextSource, ContextUpdatedAt, ContextConfidence hinzu — wenn noetig."""
    cur = con.cursor()
    cur.execute("PRAGMA table_info(Samples)")
    existing = {row[1] for row in cur.fetchall()}
    added: list[str] = []
    plan = [
        ("ContextSource",     "TEXT DEFAULT NULL"),
        ("ContextUpdatedAt",  "TEXT DEFAULT NULL"),
        ("ContextConfidence", "REAL DEFAULT NULL"),
    ]
    for col, ddl in plan:
        if col not in existing:
            cur.execute(f"ALTER TABLE Samples ADD COLUMN {col} {ddl}")
            added.append(col)
    con.commit()
    return added


# ── Snapshot ──────────────────────────────────────────────────────────────-

def make_snapshot(db_path: Path, snap_dir: Path, job_id: str) -> Path:
    """Kopiert die DB-Datei (inkl. -wal/-shm) atomar via SQLite VACUUM INTO."""
    snap_dir.mkdir(parents=True, exist_ok=True)
    snap_path = snap_dir / f"KnowledgeBase_pre_context_apply_{job_id}.db"
    # VACUUM INTO erzeugt eine konsistente Kopie OHNE die WAL/SHM-Files in den
    # Snapshot zu vererben — sicherer als shutil.copy.
    src = sqlite3.connect(str(db_path))
    try:
        src.execute(f"VACUUM INTO ?", (str(snap_path),))
    finally:
        src.close()
    return snap_path


# ── Update-Plan + Apply ───────────────────────────────────────────────────-

def lookup_for_sample(sample: dict, stammdaten: dict[str, dict],
                      min_rank: int) -> tuple[dict | None, str | None]:
    """Sucht den ersten passenden Stammdaten-Eintrag fuer ein Sample."""
    for k in extract_haltungs_keys(sample.get("CaseId") or ""):
        entry = stammdaten.get(k)
        if entry is None:
            continue
        return entry, k
    return None, None


def plan_updates(samples: list[dict], stammdaten: dict[str, dict],
                 min_rank: int) -> tuple[list[dict], collections.Counter, collections.Counter]:
    """
    Liefert (Plan, RejectedMaterialsCounter, MaterialConflictCounter).
    JEDE Aktion betrifft NUR ein leeres Feld — vorhandene Werte werden niemals
    ueberschrieben. Material-Werte ohne plausibles Material-Token werden
    abgelehnt. Material-Wechsel (Beschreibung sagt anderes Material als der
    Haltungs-Default) werden uebersprungen — DN-Update bleibt aber erlaubt.
    """
    plan: list[dict] = []
    rejected_materials: collections.Counter = collections.Counter()
    material_conflicts: collections.Counter = collections.Counter()
    for s in samples:
        case_id = (s.get("CaseId") or "").strip()
        if not case_id:
            continue
        if any(case_id.lower().startswith(p) for p in NOT_FILLABLE_CASEID_PREFIXES):
            continue

        has_material = bool((s.get("Rohrmaterial") or "").strip())
        has_dn = s.get("NennweiteMm") is not None and (s.get("NennweiteMm") or 0) > 0
        if has_material and has_dn:
            continue

        entry, used_key = lookup_for_sample(s, stammdaten, min_rank)
        if entry is None:
            continue

        new_material: str | None = None
        new_dn: int | None = None
        material_source: str | None = None
        dn_source: str | None = None

        if not has_material:
            v = (entry.get("Material") or "").strip()
            src = entry.get("MaterialSource") or "None"
            if v and SOURCE_RANK.get(src, 99) <= min_rank:
                if not material_is_plausible(v):
                    rejected_materials[v] += 1
                else:
                    desc_fam = description_overrides_material(
                        s.get("Beschreibung"), v)
                    if desc_fam is not None:
                        # Beschreibung sagt explizit ein anderes Material —
                        # Material-UPDATE ueberspringen, DN-Update unten bleibt.
                        material_conflicts[
                            f"{material_family(v)}->{desc_fam}"] += 1
                    else:
                        new_material = v
                        material_source = src

        if not has_dn:
            v = entry.get("DN_mm")
            src = entry.get("DnSource") or "None"
            if isinstance(v, int) and v > 0 and SOURCE_RANK.get(src, 99) <= min_rank:
                new_dn = v
                dn_source = src

        if new_material is None and new_dn is None:
            continue

        # Konfidenz aus der schwaecheren der beiden gewaehlten Quellen
        used_sources = [s for s in (material_source, dn_source) if s]
        confidence = min(SOURCE_CONFIDENCE.get(src, 0.0) for src in used_sources)
        # Tag der genutzten Quellen, getrennt durch '+' fuer ContextSource-Spalte
        context_source = "+".join(sorted(set(used_sources)))

        plan.append({
            "SampleId": s["SampleId"],
            "CaseId": case_id,
            "MatchedKey": used_key,
            "OldMaterial": s.get("Rohrmaterial"),
            "NewMaterial": new_material,
            "OldDn": s.get("NennweiteMm"),
            "NewDn": new_dn,
            "ContextSource": context_source,
            "ContextConfidence": round(confidence, 3),
        })
    return plan, rejected_materials, material_conflicts


def apply_plan(con: sqlite3.Connection, plan: list[dict], now_iso: str) -> int:
    """Fuehrt das Plan in EINER Transaktion aus. Liefert Anzahl Updated rows."""
    cur = con.cursor()
    updated = 0
    cur.execute("BEGIN")
    try:
        for p in plan:
            sets = []
            params: list = []
            # Defensive Schreib-Bedingung: nur fuellen wenn aktuell wirklich leer.
            # Verhindert Race / Wiederholten-Lauf-Doppelschreib.
            where_extra = []

            if p["NewMaterial"] is not None:
                sets.append("Rohrmaterial = ?")
                params.append(p["NewMaterial"])
                where_extra.append("(Rohrmaterial IS NULL OR TRIM(Rohrmaterial) = '')")
            if p["NewDn"] is not None:
                sets.append("NennweiteMm = ?")
                params.append(p["NewDn"])
                where_extra.append("(NennweiteMm IS NULL OR NennweiteMm <= 0)")

            sets.append("ContextSource = COALESCE(ContextSource, ?)")
            params.append(p["ContextSource"])
            sets.append("ContextUpdatedAt = ?")
            params.append(now_iso)
            sets.append("ContextConfidence = COALESCE(ContextConfidence, ?)")
            params.append(p["ContextConfidence"])

            params.append(p["SampleId"])
            sql = (
                f"UPDATE Samples SET {', '.join(sets)} "
                f"WHERE SampleId = ? AND ({' OR '.join(where_extra)})"
            )
            cur.execute(sql, params)
            updated += cur.rowcount
        con.commit()
    except Exception:
        con.rollback()
        raise
    return updated


# ── Report ────────────────────────────────────────────────────────────────-

def write_report(out_dir: Path, job_id: str, mode: str, db_path: Path,
                 stammdaten_path: str, snapshot_path: Path | None,
                 plan: list[dict], updated_count: int,
                 added_columns: list[str], min_source: str | None,
                 rejected_materials: collections.Counter,
                 material_conflicts: collections.Counter) -> tuple[Path, Path]:
    out_dir.mkdir(parents=True, exist_ok=True)

    # Aggregate
    fills_material = sum(1 for p in plan if p["NewMaterial"] is not None)
    fills_dn       = sum(1 for p in plan if p["NewDn"] is not None)
    fills_both     = sum(1 for p in plan if p["NewMaterial"] is not None and p["NewDn"] is not None)
    by_source      = collections.Counter(p["ContextSource"] for p in plan)
    avg_conf = round(sum(p["ContextConfidence"] for p in plan) / len(plan), 3) if plan else 0

    rep = {
        "JobId": job_id,
        "Mode": mode,
        "GeneratedAt": datetime.datetime.now().isoformat(),
        "DbPath": str(db_path),
        "StammdatenPath": stammdaten_path,
        "SnapshotPath": str(snapshot_path) if snapshot_path else None,
        "AddedColumns": added_columns,
        "MinSource": min_source,
        "PlannedUpdates": len(plan),
        "AppliedUpdates": updated_count,
        "FillsMaterial": fills_material,
        "FillsDn": fills_dn,
        "FillsBoth": fills_both,
        "BySource": dict(by_source.most_common()),
        "AvgConfidence": avg_conf,
        "PlanSample": plan[:50],
        "RejectedMaterialsTotal": sum(rejected_materials.values()),
        "RejectedMaterialsDistinct": len(rejected_materials),
        "RejectedMaterialsTop": rejected_materials.most_common(40),
        "MaterialConflictsTotal": sum(material_conflicts.values()),
        "MaterialConflictsTop": material_conflicts.most_common(20),
    }
    json_path = out_dir / f"kb_context_apply_{mode}_{job_id}.json"
    md_path   = out_dir / f"kb_context_apply_{mode}_{job_id}.md"
    json_path.write_text(json.dumps(rep, indent=2, ensure_ascii=False), encoding="utf-8")

    md = [
        f"# KB Kontext Apply — {mode.upper()} — {job_id}",
        "",
        f"**Mode:** `{mode}`",
        f"**Generated:** {rep['GeneratedAt']}",
        f"**DB:** `{db_path}`",
        f"**Stammdaten:** `{stammdaten_path}`",
        f"**Snapshot:** `{snapshot_path or '(keiner — DRY-Lauf)'}`",
        f"**Schema-Migration:** {', '.join(added_columns) if added_columns else '(nichts hinzugefuegt — bereits vorhanden)'}",
        f"**MinSource:** `{min_source or '(alle akzeptiert)'}`",
        "",
        "## Plan",
        f"- Geplante UPDATEs:  **{rep['PlannedUpdates']:,}**",
        f"- Davon Material:    {fills_material:,}",
        f"- Davon DN_mm:       {fills_dn:,}",
        f"- Davon beides:      {fills_both:,}",
        f"- Avg-Konfidenz:     {avg_conf}",
        "",
        "### Verteilung nach Quelle (kombinierte ContextSource)",
        "",
        "| Quelle | Updates |",
        "|---|---:|",
    ]
    for src, n in by_source.most_common():
        md.append(f"| `{src}` | {n:,} |")

    md += [
        "",
        "## Material-Whitelist Rejections",
        "",
        f"- Total verworfene Material-Werte: **{rep['RejectedMaterialsTotal']:,}** "
        f"(distinct: {rep['RejectedMaterialsDistinct']:,})",
        "",
        "Diese Werte kamen aus dem PDF-Parser, sind aber kein gueltiges Material "
        "(z.B. Tabellen-Spaltenkopf, Profilform, Inspektionsdatum). Sie wurden "
        "VOR dem Schreiben blockiert.",
        "",
        "| Wert | Anzahl |",
        "|---|---:|",
    ]
    for v, n in rep["RejectedMaterialsTop"]:
        md.append(f"| `{v[:60]}` | {n:,} |")

    md += [
        "",
        "## Material-Wechsel-Schutz",
        "",
        f"- Uebersprungene Material-UPDATEs (Beschreibung sagt anderes Material "
        f"als Haltungs-Default): **{rep['MaterialConflictsTotal']:,}**",
        "",
        "Diese Samples bekommen KEIN Material-UPDATE, weil ihre Beschreibung "
        "explizit ein anderes Material erwaehnt. Ein DN-UPDATE bleibt davon "
        "unberuehrt (DN aendert sich kaum innerhalb einer Haltung).",
        "",
        "| Konflikt (Plan -> Beschreibung) | Anzahl |",
        "|---|---:|",
    ]
    for v, n in rep["MaterialConflictsTop"]:
        md.append(f"| `{v}` | {n:,} |")

    md += [
        "",
        f"## Apply-Ergebnis",
        f"- Tatsaechlich geschriebene Zeilen: **{updated_count:,}**",
        "",
        "Hinweis: Differenz zwischen *PlannedUpdates* und *AppliedUpdates* "
        "kann auftreten, wenn ein Feld zwischen Plan und Apply von einem anderen "
        "Prozess gefuellt wurde (Defensive Where-Klausel verhindert Ueberschreiben).",
        "",
        "## Stichprobe Plan (max 30)",
        "",
        "| SampleId (gekuerzt) | CaseId | Material alt -> neu | DN alt -> neu | Quelle | Konfidenz |",
        "|---|---|---|---|---|---:|",
    ]
    for p in plan[:30]:
        md.append(
            f"| `{p['SampleId'][:16]}…` | `{p['CaseId']}` | "
            f"{(p['OldMaterial'] or '-')[:18]} -> {(p['NewMaterial'] or '-')[:18]} | "
            f"{p['OldDn'] or '-'} -> {p['NewDn'] or '-'} | "
            f"`{p['ContextSource']}` | {p['ContextConfidence']} |")

    md += [
        "",
        "---",
        "",
        ("**DRY-Lauf:** keine DB-Aenderung. Erneut mit `--apply` starten." if mode == "dry"
         else "**APPLY-Lauf:** Snapshot wurde erzeugt, UPDATE lief in Transaktion. "
              "Rueckgaengig machen: Snapshot ueber die Live-DB zuruecklegen "
              "(Live-DB-Datei ersetzen, KB-Verbraucher vorher schliessen)."),
        "",
    ]
    md_path.write_text("\n".join(md), encoding="utf-8")
    return json_path, md_path


# ── Main ──────────────────────────────────────────────────────────────────-

def main() -> int:
    args = parse_args()
    db_path = Path(args.db)
    if not db_path.exists():
        print(f"FEHLER: DB nicht gefunden: {db_path}", file=sys.stderr)
        return 2
    if not Path(args.stammdaten).exists():
        print(f"FEHLER: Stammdaten-JSON nicht gefunden: {args.stammdaten}", file=sys.stderr)
        return 2

    job_id = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    now_iso = datetime.datetime.now().isoformat()
    min_rank = SOURCE_RANK.get(args.max_confidence_source, 99) if args.max_confidence_source else 99

    # Stammdaten laden
    stammdaten = load_stammdaten(args.stammdaten)
    print(f"Stammdaten-Map: {len(stammdaten):,} Eintraege.")

    # Plan immer im RO-Modus erzeugen.
    # immutable=1: kein FileLock, kein WAL/SHM — der Plan-Lauf kollidiert nicht
    # mit einer eventuell laufenden App, die die KB geoeffnet hat. Annahme:
    # zwischen Plan und Apply aendert niemand sonst die DB. Apply-Pfad oeffnet
    # spaeter eine normale Read-Write-Verbindung.
    uri = f"file:{db_path.as_posix()}?mode=ro&immutable=1"
    con_ro = sqlite3.connect(uri, uri=True)
    try:
        cur = con_ro.cursor()
        cur.execute("""
            SELECT SampleId, CaseId, Rohrmaterial, NennweiteMm, Beschreibung
            FROM Samples
        """)
        cols = [d[0] for d in cur.description]
        samples = [dict(zip(cols, r)) for r in cur.fetchall()]
    finally:
        con_ro.close()
    print(f"Samples: {len(samples):,}")

    plan, rejected_materials, material_conflicts = plan_updates(
        samples, stammdaten, min_rank)
    print(f"Geplante UPDATEs: {len(plan):,}")
    print(f"Material-Rejects (Whitelist):       {sum(rejected_materials.values()):,} "
          f"(distinct: {len(rejected_materials):,})")
    print(f"Material-Wechsel (Beschreibungs-Konflikt, uebersprungen): "
          f"{sum(material_conflicts.values()):,}")

    out_dir = Path(args.out)
    snap_dir = Path(args.snapshots)

    if not args.apply:
        json_p, md_p = write_report(
            out_dir, job_id, "dry", db_path, args.stammdaten,
            snapshot_path=None, plan=plan, updated_count=0,
            added_columns=[], min_source=args.max_confidence_source,
            rejected_materials=rejected_materials,
            material_conflicts=material_conflicts)
        print(f"\nDRY-Report: {md_p}")
        print("Kein --apply gesetzt — DB bleibt unveraendert.")
        return 0

    # ── APPLY-Pfad ───────────────────────────────────────────────────────--
    print("\n[APPLY] Snapshot anlegen …")
    snap_path = make_snapshot(db_path, snap_dir, job_id)
    print(f"  Snapshot: {snap_path}")

    con_rw = sqlite3.connect(str(db_path))
    try:
        added = migrate_context_columns(con_rw)
        if added:
            print(f"[APPLY] Schema-Migration: {added}")
        else:
            print("[APPLY] Schema bereits aktuell.")

        print("[APPLY] UPDATE in Transaktion …")
        updated = apply_plan(con_rw, plan, now_iso)
        print(f"  -> {updated:,} Zeilen geschrieben.")
    finally:
        con_rw.close()

    json_p, md_p = write_report(
        out_dir, job_id, "apply", db_path, args.stammdaten,
        snapshot_path=snap_path, plan=plan, updated_count=updated,
        added_columns=added, min_source=args.max_confidence_source,
        rejected_materials=rejected_materials,
        material_conflicts=material_conflicts)
    print(f"\nAPPLY-Report: {md_p}")
    print(f"Rollback: snap_path -> live-DB ueberschreiben (vorher KB-Consumers schliessen).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
