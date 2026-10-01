"""Friert einen OSD-Pruefbestand samt menschlicher Ablesung ein.

Der Bestand bindet Bildbytes und Wahrheit ueber SHA-256 an eine Version. Erst
damit sind zwei Groessen ueber die Zeit vergleichbar:

  Abdeckung    = gelesen von ALLEN Bildern
  Richtigkeit  = richtig von gelesen

Deshalb gehoeren ALLE geprueften Bilder hinein, nicht nur die, die ein bestimmter
Leser lesen konnte. Ein Bestand aus den erfolgreichen Bildern waere durch das
Verhalten des heutigen Lesers ausgewaehlt — Abdeckung liesse sich daran nie
messen, nur Richtigkeit auf einer geschoenten Teilmenge.

Erweiterungen sind monoton: v2 enthaelt v1 unveraendert. Ein Lauf aendert genau
eine Sache — Modell ODER Bestand, nie beides.

Das Werkzeug ist schreibfrei fuer die Quelle und ueberschreibt nie eine
vorhandene Version.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence

ZEILE = re.compile(r"^(?P<nr>\d{4})\s*=\s*(?P<wert>.*)$")


def sha256(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1 << 20), b""):
            hasher.update(block)
    return hasher.hexdigest()


def lies_wahrheit(pfad: Path) -> dict[int, str]:
    werte: dict[int, str] = {}
    for zeile in pfad.read_text(encoding="utf-8").splitlines():
        treffer = ZEILE.match(zeile.strip())
        if treffer is not None:
            werte[int(treffer.group("nr"))] = treffer.group("wert").strip()
    return werte


def baue_eintraege(
    wahrheit: dict[int, str],
    haltungen: dict[int, str],
    bilder: Path,
) -> list[dict]:
    """Ein Eintrag je Bild — auch fuer unleserliche, sonst fehlt der Nenner."""
    eintraege: list[dict] = []
    for nummer in sorted(wahrheit):
        roh = wahrheit[nummer]
        if not roh:
            raise SystemExit(f"Nr. {nummer} hat keine Ablesung — der Bestand waere unvollstaendig.")
        bild = bilder / f"f{nummer:04d}.jpg"
        if not bild.is_file():
            raise SystemExit(f"Bild fehlt: {bild}")

        lesbar = roh != "?"
        wert: float | None = None
        if lesbar:
            try:
                wert = round(float(roh.replace(",", ".")), 2)
            except ValueError as fehler:
                raise SystemExit(f"Nr. {nummer}: '{roh}' ist keine Zahl.") from fehler

        eintraege.append({
            "nr": nummer,
            "datei": bild.name,
            "haltung": haltungen.get(nummer, ""),
            "bild_sha256": sha256(bild),
            "menschlich_lesbar": lesbar,
            "meter": wert,
        })
    return eintraege


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="OSD-Pruefbestand einfrieren")
    parser.add_argument("--quelle", type=Path, required=True, help="Ordner mit frames/ und wahrheit.txt")
    parser.add_argument("--name", required=True, help="Name des Bestands, z. B. osd_hd")
    parser.add_argument("--version", type=int, default=1)
    parser.add_argument("--material", required=True, help="z. B. HD 1920x1080 oder SD 720x576")
    parser.add_argument("--ziel", type=Path, default=Path(r"C:\KI_BRAIN\eval_set\osd"))
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args(argv)

    bilder = args.quelle / "frames"
    wahrheit_datei = args.quelle / "wahrheit.txt"
    for pfad in (bilder, wahrheit_datei):
        if not pfad.exists():
            raise SystemExit(f"Fehlt: {pfad}")

    haltungen: dict[int, str] = {}
    ergebnisse = args.quelle / "leser_ergebnisse.json"
    if ergebnisse.is_file():
        haltungen = {
            int(e["nr"]): str(e.get("haltung") or "")
            for e in json.loads(ergebnisse.read_text(encoding="utf-8-sig"))
            if "nr" in e
        }

    eintraege = baue_eintraege(lies_wahrheit(wahrheit_datei), haltungen, bilder)
    lesbar = sum(1 for e in eintraege if e["menschlich_lesbar"])
    ziel = args.ziel / f"{args.name}_v{args.version}"

    print(f"Bestand      {args.name} v{args.version}")
    print(f"Material     {args.material}")
    print(f"Bilder       {len(eintraege)}, davon menschlich lesbar {lesbar}")
    print(f"Haltungen    {len({e['haltung'] for e in eintraege if e['haltung']})}")
    print(f"Ziel         {ziel}")

    if ziel.exists():
        raise SystemExit("Diese Version besteht bereits und wird nie ueberschrieben.")
    if not args.execute:
        print("\nPrueflauf — nichts geschrieben. Mit --execute einfrieren.")
        return 0

    staging = args.ziel / f".{args.name}_v{args.version}.staging"
    if staging.exists():
        shutil.rmtree(staging)
    (staging / "frames").mkdir(parents=True)
    try:
        for eintrag in eintraege:
            quelle = bilder / eintrag["datei"]
            ziel_bild = staging / "frames" / eintrag["datei"]
            shutil.copy2(quelle, ziel_bild)
            if sha256(ziel_bild) != eintrag["bild_sha256"]:
                raise SystemExit(f"Kopie weicht ab: {eintrag['datei']}")

        manifest = {
            "schema_version": 1,
            "name": args.name,
            "version": args.version,
            "material": args.material,
            "eingefroren_utc": datetime.now(timezone.utc).isoformat(),
            "zweck": "Messgrundlage fuer den OSD-Meterleser: Abdeckung UND Richtigkeit",
            "regel": (
                "Enthaelt ALLE geprueften Bilder, auch unleserliche und nicht gelesene. "
                "Erweiterungen nur monoton als naechste Version; ein Lauf aendert genau "
                "eine Sache — Leser ODER Bestand."
            ),
            "bilder": len(eintraege),
            "menschlich_lesbar": lesbar,
            "eintraege": eintraege,
        }
        text = json.dumps(manifest, indent=2, ensure_ascii=False)
        (staging / "manifest.json").write_text(text, encoding="utf-8")
        staging.rename(ziel)
    except Exception:
        shutil.rmtree(staging, ignore_errors=True)
        raise

    manifest_sha = sha256(ziel / "manifest.json")
    print(f"\nEingefroren: {ziel}")
    print(f"Manifest-SHA-256: {manifest_sha}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
