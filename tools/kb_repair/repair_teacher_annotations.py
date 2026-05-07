"""
KB-Reparatur fuer teacher_annotations.json (Audit-Empfehlung).

Behebt:
1. Alte absolute Pfade C:\\Sewer-StudioKI_3.1\\... -> C:\\KI_BRAIN\\Knowledge\\...
2. VSA-Code-Normalisierung: 'BCD - ROHRANFANG' / 'BCD — ROHRANFANG' -> 'BCD'
3. Markiert Items mit fehlenden Frame-Dateien als 'fullFramePath_missing'
4. Schreibt Backup vor jeder Aenderung
5. Liefert Vorher/Nachher-Statistik

Usage: python repair_teacher_annotations.py [--dry-run]
"""
import json
import os
import re
import sys
import shutil
from datetime import datetime
from pathlib import Path

DEFAULT_PATH = Path(r"C:/KI_BRAIN/teacher_annotations.json")

OLD_PATH_PREFIXES = [
    r"C:\Sewer-StudioKI_3.1\src\AuswertungPro.Next.UI\bin\Debug\net10.0-windows\Knowledge",
    r"C:\Sewer-Studio_KI_4.0\src\AuswertungPro.Next.UI\bin\Debug\net10.0-windows\Knowledge",
    r"C:\Sewer-Studio_KI_4.1\src\AuswertungPro.Next.UI\bin\Debug\net10.0-windows\Knowledge",
    # Alte Pfad-Variante OHNE bin\Debug-Pfad
    r"C:\Sewer-StudioKI_3.1\Knowledge",
    r"C:\Sewer-Studio_KI_4.0\Knowledge",
    r"C:\Sewer-Studio_KI_4.1\Knowledge",
]
NEW_PATH_PREFIX = r"C:\KI_BRAIN\Knowledge"

# Code-Patterns: 'BCD - Rohranfang', 'BCD — Rohranfang', 'BCD ROHRANFANG' etc.
CODE_PATTERN = re.compile(r"^([A-Z]{3}[A-Z0-9]*)\s*[—–\-]\s*\w.*$|^([A-Z]{3}[A-Z0-9]*)\s+[A-Z][A-Z\s]+$")


def normalize_code(code: str) -> str:
    """Extrahiert reinen VSA-Code aus Strings wie 'BCD — ROHRANFANG'."""
    if not code:
        return code
    code = code.strip()
    m = CODE_PATTERN.match(code)
    if m:
        return (m.group(1) or m.group(2)).strip()
    # Fallback: Erste alphanumerische Token vor Whitespace/Sonderzeichen
    parts = re.split(r"[\s—–\-]+", code, maxsplit=1)
    return parts[0].strip() if parts else code


def remap_path(path: str) -> str:
    if not path:
        return path
    for old in OLD_PATH_PREFIXES:
        if path.startswith(old):
            return path.replace(old, NEW_PATH_PREFIX, 1)
    return path


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    dry_run = "--dry-run" in sys.argv
    target = DEFAULT_PATH

    if not target.exists():
        print(f"FEHLT: {target}", file=sys.stderr)
        sys.exit(1)

    raw = target.read_text(encoding="utf-8")
    data = json.loads(raw)
    items = data if isinstance(data, list) else data.get("annotations", data.get("items", []))

    print(f"=== KB-Reparatur: {target} ===")
    print(f"Mode: {'DRY-RUN' if dry_run else 'WRITE'}")
    print(f"Total Items: {len(items)}\n")

    stats = {
        "old_paths_remapped_full": 0,
        "old_paths_remapped_cropped": 0,
        "old_paths_remapped_yolo": 0,
        "missing_full_frame": 0,
        "missing_yolo": 0,
        "code_normalized": 0,
    }

    for it in items:
        # 1. Pfade remappen
        for fld in ("fullFramePath", "croppedRegionPath", "yoloAnnotationPath"):
            old = it.get(fld) or ""
            new = remap_path(old)
            if old != new:
                it[fld] = new
                key = "old_paths_remapped_" + (
                    "full" if "ull" in fld else "cropped" if "ropped" in fld else "yolo")
                stats[key] += 1

        # 2. Fehlende Frame-Dateien markieren
        ff = it.get("fullFramePath") or ""
        if ff and not os.path.exists(ff):
            it["fullFramePathMissing"] = True
            stats["missing_full_frame"] += 1
        yp = it.get("yoloAnnotationPath") or ""
        if yp and not os.path.exists(yp):
            it["yoloAnnotationPathMissing"] = True
            stats["missing_yolo"] += 1

        # 3. Code normalisieren
        code = it.get("vsaCode") or ""
        normalized = normalize_code(code)
        if normalized != code and normalized:
            it["vsaCodeOriginal"] = code  # Audit-Trail
            it["vsaCode"] = normalized
            stats["code_normalized"] += 1

    print("=== Statistik ===")
    for k, v in stats.items():
        print(f"  {k:35} {v:5}")

    if dry_run:
        print("\nDRY-RUN: keine Datei geschrieben.")
        return

    # Backup
    bak = target.with_name(f"{target.stem}.bak_{datetime.now():%Y%m%d_%H%M%S}.json")
    shutil.copy2(target, bak)
    print(f"\nBackup: {bak.name}")

    target.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Geschrieben: {target}")


if __name__ == "__main__":
    main()
