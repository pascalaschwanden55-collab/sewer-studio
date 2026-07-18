#!/usr/bin/env python3
"""Skill-Linter fuer SewerStudio-Skills.

Prueft Skill-Dateien (SKILL.md und lose *.md mit Frontmatter) gegen bekannte
Fehlermuster aus forbidden.json und validiert das Frontmatter.

Exit-Codes:
  0  sauber
  1  Altbegriffe/Funde vorhanden
  2  Pruefung nicht moeglich (kaputtes/unbekanntes Format) — hat Vorrang vor 1

Regeln:
- Ein Treffer wird NICHT als Fund gewertet, wenn die Zeile eine Negations-/Meta-
  Markierung enthaelt (z. B. "niemals", "nicht", "veraltet", "entfernt",
  "existiert nicht") oder einen expliziten Marker "<!-- lint-ok: grund -->".
- Ordner mit "-archiv", "_archiv" oder ".system" im Pfad werden ignoriert.
- Fehlendes/kaputtes Frontmatter => Format-Fehler => Exit 2 (Vorrang vor Funden).
"""
import json
import re
import sys
from pathlib import Path

IGNORE_DIR_MARKERS = ("-archiv", "_archiv", ".system")
NEGATION_CUES = (
    "niemals", "nie ", "nicht", "kein", "veraltet", "entfernt",
    "deaktiviert", "existiert nicht", "gibt es nicht", "<!-- lint-ok",
)


def load_rules(rules_path):
    data = json.loads(Path(rules_path).read_text(encoding="utf-8"))
    return [(r["id"], re.compile(r["regex"], re.IGNORECASE), r.get("hinweis", ""))
            for r in data["regeln"]]


def is_ignored(path, root):
    rel = path.relative_to(root)
    return any(m in part.lower() for part in rel.parts for m in IGNORE_DIR_MARKERS)


def collect_skill_files(root):
    files = []
    for p in root.rglob("SKILL.md"):
        if not is_ignored(p, root):
            files.append(p)
    for p in root.glob("*.md"):
        if p.name == "SKILL.md":
            continue
        try:
            head = p.read_text(encoding="utf-8", errors="replace").lstrip()
        except OSError:
            continue
        if head.startswith("---"):
            files.append(p)
    return files


def check_frontmatter(text):
    stripped = text.lstrip()
    if not stripped.startswith("---"):
        return False, "kein Frontmatter (---) am Dateianfang"
    body = stripped[3:]
    end = body.find("\n---")
    if end == -1:
        return False, "Frontmatter nicht geschlossen (fehlendes ---)"
    fm = body[:end]
    if not re.search(r"(?m)^\s*name\s*:", fm):
        return False, "Feld 'name' fehlt"
    if not re.search(r"(?m)^\s*description\s*:", fm):
        return False, "Feld 'description' fehlt"
    return True, ""


def line_is_negated(line):
    low = line.lower()
    return any(cue in low for cue in NEGATION_CUES)


def scan_text(text, rules):
    findings = []
    for i, line in enumerate(text.splitlines(), 1):
        if line_is_negated(line):
            continue
        for rid, rx, hint in rules:
            if rx.search(line):
                findings.append((i, rid, hint, line.strip()[:100]))
    return findings


def main(argv):
    if len(argv) < 2:
        print("Verwendung: skill_lint.py <skill-root> [rules.json]", file=sys.stderr)
        return 2
    root = Path(argv[1])
    rules_path = Path(argv[2]) if len(argv) > 2 else Path(__file__).parent / "forbidden.json"
    if not root.is_dir():
        print(f"PRUEFUNG NICHT MOEGLICH: {root} ist kein Ordner", file=sys.stderr)
        return 2
    try:
        rules = load_rules(rules_path)
    except (OSError, ValueError, KeyError) as exc:
        print(f"PRUEFUNG NICHT MOEGLICH: Regeldatei unlesbar ({exc})", file=sys.stderr)
        return 2

    skill_files = collect_skill_files(root)
    if not skill_files:
        print(f"PRUEFUNG NICHT MOEGLICH: keine Skill-Dateien unter {root}", file=sys.stderr)
        return 2

    any_broken = False
    fund_count = 0
    for f in sorted(skill_files):
        text = f.read_text(encoding="utf-8", errors="replace")
        ok, reason = check_frontmatter(text)
        if not ok:
            any_broken = True
            print(f"[FORMAT] {f}: {reason}")
            continue
        for (ln, rid, hint, snippet) in scan_text(text, rules):
            fund_count += 1
            print(f"[FUND] {f}:{ln} [{rid}] {hint} -> {snippet}")

    if any_broken:
        print("ERGEBNIS: Pruefung nicht moeglich (kaputtes Format).")
        return 2
    if fund_count:
        print(f"ERGEBNIS: {fund_count} Fund(e) in {len(skill_files)} Skill-Dateien.")
        return 1
    print(f"ERGEBNIS: sauber ({len(skill_files)} Skill-Dateien).")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
