"""Baut eine blinde 40er-Sichtung fuer Ort und Stil der OSD-Meteranzeige.

Je physischer Haltung wird hoechstens ein Bild verwendet. Die 30 Haltungen der
frueheren Meter-Sichtprobe werden ausgeschlossen. PDF-Wert und Lesergebnis
bleiben vollstaendig draussen, damit nur die sichtbare Einblendung beurteilt wird.
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
VORHERIGE_QA = Path(r"C:\KI_BRAIN\training\diagnostics\osd_wahrheit_protokoll_v1_qa\qa_manifest.json")
ZIEL = Path(r"C:\KI_BRAIN\training\diagnostics\osd_layout_review_v1")


def sha256_datei(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


def auswaehlen(eintraege: list[dict], ausgeschlossen: set[str],
               anzahl: int, saat: str) -> list[dict]:
    if anzahl <= 0:
        raise ValueError("Die Anzahl muss groesser als null sein.")
    je_haltung: dict[str, dict] = {}
    for eintrag in eintraege:
        haltung = str(eintrag["physische_haltung"])
        if haltung in ausgeschlossen:
            continue
        vorhanden = je_haltung.get(haltung)
        if vorhanden is None or str(eintrag["id"]) < str(vorhanden["id"]):
            je_haltung[haltung] = eintrag
    sortiert = sorted(
        je_haltung.values(),
        key=lambda e: hashlib.sha256(
            f"{saat}|{e['physische_haltung']}".encode()).hexdigest(),
    )
    if len(sortiert) < anzahl:
        raise ValueError(f"Nur {len(sortiert)} neue Haltungen fuer {anzahl} Bilder.")
    return sortiert[:anzahl]


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--quelle", type=Path, default=QUELLE)
    parser.add_argument("--vorherige-qa", type=Path, default=VORHERIGE_QA)
    parser.add_argument("--ziel", type=Path, default=ZIEL)
    parser.add_argument("--anzahl", type=int, default=40)
    parser.add_argument("--saat", default="osd-layout-review-v1")
    args = parser.parse_args(argv)

    quelle_manifest = args.quelle / "wahrheit.json"
    if not quelle_manifest.is_file() or not args.vorherige_qa.is_file():
        raise SystemExit("OSD-Bestand oder vorherige QA fehlt.")
    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits und wird nicht ueberschrieben: {args.ziel}")
    quelle = json.loads(quelle_manifest.read_text(encoding="utf-8-sig"))
    vorher = json.loads(args.vorherige_qa.read_text(encoding="utf-8-sig"))
    ausgeschlossen = {str(f["physische_haltung"]) for f in vorher.get("faelle") or []}
    try:
        auswahl = auswaehlen(
            quelle.get("eintraege") or [], ausgeschlossen, args.anzahl, args.saat)
    except ValueError as fehler:
        raise SystemExit(str(fehler)) from fehler

    staging = args.ziel.with_name(f".{args.ziel.name}.staging-{uuid.uuid4().hex}")
    bilder = staging / "bilder"
    bilder.mkdir(parents=True)
    faelle = []
    try:
        for nummer, eintrag in enumerate(auswahl, start=1):
            quelle_bild = args.quelle / eintrag["bild"]
            if not quelle_bild.is_file() or sha256_datei(quelle_bild) != eintrag["bild_sha256"]:
                raise RuntimeError(f"Quellbild fehlt oder wurde veraendert: {eintrag['id']}")
            name = f"bild_{nummer:03d}.jpg"
            ziel_bild = bilder / name
            shutil.copyfile(quelle_bild, ziel_bild)
            fall_id = hashlib.sha256(
                f"{args.saat}|{eintrag['physische_haltung']}|{eintrag['bild_sha256']}".encode()
            ).hexdigest()[:16]
            faelle.append({
                "nummer": nummer,
                "fall_id": fall_id,
                "haltung": eintrag["haltung"],
                "physische_haltung": eintrag["physische_haltung"],
                "bild": name,
                "bild_sha256": sha256_datei(ziel_bild),
            })
        manifest = {
            "schema": "osd_layout_review_queue_v1",
            "zweck": "Menschlich markierter Ort und sichtbarer Stil der Meteranzeige",
            "quelle": str(quelle_manifest),
            "quelle_sha256": sha256_datei(quelle_manifest),
            "vorherige_qa_sha256": sha256_datei(args.vorherige_qa),
            "saat": args.saat,
            "ausgeschlossene_qa_haltungen": len(ausgeschlossen),
            "faelle": faelle,
        }
        (staging / "queue.json").write_text(
            json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
        staging.replace(args.ziel)
    except Exception:
        shutil.rmtree(staging, ignore_errors=True)
        raise

    print(f"Layout-Sichtung: {len(faelle)} Bilder aus {len(faelle)} neuen Haltungen")
    print(f"Queue: {args.ziel / 'queue.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
