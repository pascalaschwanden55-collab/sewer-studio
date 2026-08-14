"""Prueft die produktive Sperrdatei requirements-lock.txt auf bekannte Sicherheitsluecken.

Warum eigenes Werkzeug statt `pip-audit` direkt:
  1. Die Sperrdatei enthaelt lokale CUDA-Versionen (`+cu128`) und einen Git-Pin (SAM-2).
     pip-audit kann diese nicht aufloesen und bricht ab. Sie werden herausgefiltert und
     sichtbar als ungeprueft gemeldet - nicht still weggelassen.
  2. Einzelne Luecken sind belegt blockiert (torch-Constraint, Grounding-DINO-Bruch).
     Diese stehen mit Grund und Beleg in lock_audit_exceptions.json. Alles andere ist
     ein Fehler. Eine Ausnahme, die nicht mehr auftritt, ist ebenfalls ein Fehler -
     dann kann und soll aktualisiert werden.

Aufruf:
    python security/audit_lock.py                      (aus dem sidecar-Ordner)
    python security/audit_lock.py --lock <datei> --ausnahmen <datei>

Exit-Codes:
    0 = nur bekannte, belegte Ausnahmen
    1 = neue Luecke gefunden ODER veraltete Ausnahme
    2 = technischer Fehler (pip-audit fehlt, Datei unlesbar, Format unerwartet)
"""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HIER = Path(__file__).resolve().parent
STANDARD_LOCK = HIER.parent / "requirements-lock.txt"
STANDARD_AUSNAHMEN = HIER / "lock_audit_exceptions.json"


def _ist_pruefbar(zeile: str) -> bool:
    """True, wenn die Zeile ein reiner PyPI-Pin ist (pip-audit kann sie aufloesen)."""
    text = zeile.strip()
    if not text or text.startswith("#"):
        return False
    if "@" in text:  # Git-/URL-Pin, z.B. SAM-2 @ git+https://...
        return False
    if "+" in text:  # lokale Version, z.B. torch==2.12.0.dev...+cu128
        return False
    return "==" in text


def lock_aufteilen(inhalt: str) -> tuple[list[str], list[str]]:
    """Trennt die Sperrdatei in pruefbare PyPI-Pins und ungeprueft bleibende Zeilen."""
    pruefbar: list[str] = []
    uebersprungen: list[str] = []
    for zeile in inhalt.splitlines():
        text = zeile.strip()
        if not text or text.startswith("#"):
            continue
        if _ist_pruefbar(text):
            pruefbar.append(text)
        else:
            uebersprungen.append(text)
    return pruefbar, uebersprungen


def pip_audit_aufrufen(pins: list[str]) -> list[dict]:
    """Ruft pip-audit fuer die gepinnten Pakete auf und liefert die Rohfunde."""
    befehl = _pip_audit_befehl()
    if befehl is None:
        raise RuntimeError(
            "pip-audit nicht gefunden. Installieren mit: pip install pip-audit "
            "(oder einmalig: uvx pip-audit)"
        )

    with tempfile.TemporaryDirectory() as tmp:
        datei = Path(tmp) / "lock-pypi-only.txt"
        datei.write_text("\n".join(pins) + "\n", encoding="utf-8")
        lauf = subprocess.run(
            [*befehl, "--no-deps", "-r", str(datei), "-f", "json",
             "--progress-spinner", "off"],
            capture_output=True,
            text=True,
        )

    # pip-audit gibt 1 zurueck, wenn es Funde gibt - das ist kein technischer Fehler.
    if lauf.returncode not in (0, 1) or not lauf.stdout.strip():
        raise RuntimeError(
            f"pip-audit ist fehlgeschlagen (Code {lauf.returncode}).\n"
            "Haeufigste Ursache: pip-audit laeuft mit einer aelteren Python-Version als "
            "die Sperrdatei verlangt (z.B. numpy braucht >=3.12). pip-audit dann passend "
            "installieren: uv tool install --python 3.12 pip-audit\n"
            f"{(lauf.stderr or lauf.stdout)[-2000:]}"
        )

    try:
        bericht = json.loads(lauf.stdout)
        eintraege = bericht["dependencies"]
    except (json.JSONDecodeError, KeyError, TypeError) as fehler:
        raise RuntimeError(f"pip-audit-Ausgabe nicht verstanden: {fehler}") from fehler

    funde: list[dict] = []
    for eintrag in eintraege:
        for luecke in eintrag.get("vulns") or []:
            funde.append(
                {
                    "paket": eintrag.get("name", "?"),
                    "version": eintrag.get("version", "?"),
                    "id": luecke.get("id", "?"),
                    "fix": luecke.get("fix_versions") or [],
                }
            )
    return funde


def _pip_audit_befehl() -> list[str] | None:
    """Findet pip-audit: erst als Modul der laufenden Umgebung, dann im PATH."""
    probe = subprocess.run(
        [sys.executable, "-m", "pip_audit", "--version"],
        capture_output=True, text=True,
    )
    if probe.returncode == 0:
        return [sys.executable, "-m", "pip_audit"]
    pfad = shutil.which("pip-audit")
    return [pfad] if pfad else None


def bewerte(funde: list[dict], ausnahmen: list[dict]) -> tuple[list[dict], list[dict]]:
    """Vergleicht Funde mit den erlaubten Ausnahmen.

    Rueckgabe: (unerlaubte Funde, veraltete Ausnahmen). Beides leer = alles in Ordnung.
    Der Schluessel ist Paketname + Luecken-ID; die Version wird bewusst nicht
    mitverglichen, damit ein Patch-Update die Ausnahme nicht stillschweigend entwertet.
    """
    erlaubt = {(a["paket"].lower(), a["id"]) for a in ausnahmen}
    gefunden = {(f["paket"].lower(), f["id"]) for f in funde}

    unerlaubt = [f for f in funde if (f["paket"].lower(), f["id"]) not in erlaubt]
    veraltet = [a for a in ausnahmen if (a["paket"].lower(), a["id"]) not in gefunden]
    return unerlaubt, veraltet


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lock", type=Path, default=STANDARD_LOCK)
    parser.add_argument("--ausnahmen", type=Path, default=STANDARD_AUSNAHMEN)
    args = parser.parse_args(argv)

    try:
        inhalt = args.lock.read_text(encoding="utf-8")
        ausnahmen_dok = json.loads(args.ausnahmen.read_text(encoding="utf-8"))
        ausnahmen = ausnahmen_dok["ausnahmen"]
    except (OSError, json.JSONDecodeError, KeyError) as fehler:
        print(f"FEHLER: Eingabedateien nicht lesbar: {fehler}", file=sys.stderr)
        return 2

    pruefbar, uebersprungen = lock_aufteilen(inhalt)
    print(f"Sperrdatei: {args.lock}")
    print(f"  geprueft:    {len(pruefbar)} PyPI-Pins")
    print(f"  ungeprueft:  {len(uebersprungen)} (lokale CUDA-/Git-Pins)")
    for zeile in uebersprungen:
        print(f"      - {zeile}")

    try:
        funde = pip_audit_aufrufen(pruefbar)
    except RuntimeError as fehler:
        print(f"FEHLER: {fehler}", file=sys.stderr)
        return 2

    unerlaubt, veraltet = bewerte(funde, ausnahmen)

    print(f"\nFunde: {len(funde)} | bekannte Ausnahmen: {len(ausnahmen)}")
    for fund in funde:
        marke = "NEU  " if fund in unerlaubt else "bekannt"
        fix = ", ".join(fund["fix"]) or "kein Fix"
        print(f"  [{marke}] {fund['paket']} {fund['version']} {fund['id']} (Fix: {fix})")

    if veraltet:
        print("\nVERALTETE AUSNAHMEN - diese Luecken treten nicht mehr auf.")
        print("Jetzt aktualisieren und den Eintrag aus der Ausnahmedatei entfernen:")
        for eintrag in veraltet:
            print(f"  - {eintrag['paket']} {eintrag['id']}")

    if unerlaubt:
        print("\nNEUE LUECKEN OHNE AUSNAHME - Sperrdatei aktualisieren.")
        print("Wenn ein Update nicht moeglich ist, mit Grund und Beleg in")
        print(f"{args.ausnahmen.name} eintragen.")

    if unerlaubt or veraltet:
        return 1

    print("\nErgebnis: nur bekannte, belegte Ausnahmen.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
