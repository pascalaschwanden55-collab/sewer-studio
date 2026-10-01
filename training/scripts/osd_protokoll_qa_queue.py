"""Baut eine kleine blinde Sichtprobe fuer den PDF-beschrifteten OSD-Bestand.

Je physischer Haltung wird hoechstens ein Bild verwendet. Die PDF-Sollwerte
bleiben im Pruefplatz unsichtbar; der Pruefer liest den sichtbaren Meterstand
selbst ab. Kundenoriginale werden nicht benoetigt oder veraendert.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import uuid
from pathlib import Path
from typing import Sequence

QUELLE = Path(r"C:\KI_BRAIN\training\diagnostics\osd_wahrheit_protokoll_v1")
ZIEL = Path(r"C:\KI_BRAIN\training\diagnostics\osd_wahrheit_protokoll_v1_qa")


def sha256_datei(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


def auswaehlen(eintraege: list[dict], anzahl: int, saat: str) -> list[dict]:
    if anzahl <= 0:
        raise ValueError("Die Anzahl muss groesser als null sein.")
    je_haltung: dict[str, dict] = {}
    for eintrag in eintraege:
        haltung = str(eintrag["physische_haltung"])
        vorhanden = je_haltung.get(haltung)
        if vorhanden is None or str(eintrag["id"]) < str(vorhanden["id"]):
            je_haltung[haltung] = eintrag
    sortiert = sorted(
        je_haltung.values(),
        key=lambda e: hashlib.sha256(
            f"{saat}|{e['physische_haltung']}".encode()).hexdigest(),
    )
    if len(sortiert) < anzahl:
        raise ValueError(f"Nur {len(sortiert)} eindeutige Haltungen fuer {anzahl} Bilder.")
    return sortiert[:anzahl]


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--quelle", type=Path, default=QUELLE)
    parser.add_argument("--ziel", type=Path, default=ZIEL)
    parser.add_argument("--anzahl", type=int, default=30)
    parser.add_argument("--saat", default="osd-protokoll-qa-v1")
    args = parser.parse_args(argv)

    manifest = args.quelle / "wahrheit.json"
    if not manifest.is_file():
        raise SystemExit(f"OSD-Bestand fehlt: {manifest}")
    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits und wird nicht ueberschrieben: {args.ziel}")
    daten = json.loads(manifest.read_text(encoding="utf-8-sig"))
    try:
        auswahl = auswaehlen(daten.get("eintraege") or [], args.anzahl, args.saat)
    except ValueError as fehler:
        raise SystemExit(str(fehler)) from fehler

    staging = args.ziel.with_name(f".{args.ziel.name}.staging-{uuid.uuid4().hex}")
    frames = staging / "frames"
    frames.mkdir(parents=True)
    qa_faelle = []
    try:
        for nummer, eintrag in enumerate(auswahl, start=1):
            quelle = args.quelle / eintrag["bild"]
            if not quelle.is_file() or sha256_datei(quelle) != eintrag["bild_sha256"]:
                raise RuntimeError(f"Quellbild fehlt oder wurde veraendert: {eintrag['id']}")
            name = f"f{nummer:04d}.jpg"
            ziel = frames / name
            shutil.copyfile(quelle, ziel)
            qa_faelle.append({
                "nr": nummer,
                "haltung": eintrag["haltung"],
                "physische_haltung": eintrag["physische_haltung"],
                "split": eintrag["split"],
                "datei": name,
                "bild_sha256": sha256_datei(ziel),
                "soll_meter": eintrag["soll_meter"],
            })
        (staging / "wahrheit.txt").write_text(
            "# Meterstand blind ablesen; ? bedeutet unleserlich\n"
            + "".join(f"{fall['nr']:04d} = \n" for fall in qa_faelle),
            encoding="utf-8",
        )
        (staging / "leser_ergebnisse.json").write_text(
            json.dumps([{"nr": f["nr"], "haltung": f["haltung"]} for f in qa_faelle],
                       indent=2, ensure_ascii=False),
            encoding="utf-8",
        )
        qa_manifest = {
            "schema": "osd_protokoll_qa_v1",
            "status": "review_offen",
            "quelle": str(manifest),
            "quelle_sha256": sha256_datei(manifest),
            "saat": args.saat,
            "faelle": qa_faelle,
        }
        (staging / "qa_manifest.json").write_text(
            json.dumps(qa_manifest, indent=2, ensure_ascii=False), encoding="utf-8")
        staging.replace(args.ziel)
    except Exception:
        shutil.rmtree(staging, ignore_errors=True)
        raise

    print(f"Sichtprobe: {len(qa_faelle)} Bilder aus {len(qa_faelle)} Haltungen")
    print(f"Pruefordner: {args.ziel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
