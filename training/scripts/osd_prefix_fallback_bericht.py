"""Misst den engen OSD-Vierziffern-Rueckfall gegen Gold und Archivsichtung."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Sequence

from PIL import Image

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "sidecar"))
from sidecar import osd_meter  # noqa: E402


def sha256(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


def bild_laden(pfad: Path) -> Image.Image:
    with Image.open(pfad) as original:
        return original.convert("RGB")


def statistik(zeilen: list[tuple[float | None, float | None]]) -> dict:
    geliefert = [(wert, soll) for wert, soll in zeilen if wert is not None]
    richtig = sum(
        soll is not None and abs(float(wert) - float(soll)) <= 0.011
        for wert, soll in geliefert)
    return {"bilder": len(zeilen), "geliefert": len(geliefert),
            "richtig_1cm": richtig, "falsch_oder_unpruefbar": len(geliefert) - richtig}


def gelieferter_gold_fall(eintrag: dict, ergebnis: dict) -> dict | None:
    wert = ergebnis.get("meter")
    if wert is None:
        return None
    soll = eintrag.get("meter") if eintrag.get("menschlich_lesbar") else None
    richtig = soll is not None and abs(float(wert) - float(soll)) <= 0.011
    return {
        "datei": eintrag["datei"],
        "haltung": eintrag.get("haltung"),
        "soll_meter": soll,
        "gelesen_meter": wert,
        "richtig_1cm": richtig,
        "leseweg": ergebnis.get("leseweg"),
        "zeichenfolge": ergebnis.get("zeichenfolge"),
        "tesseract_text": ergebnis.get("tesseract_text"),
    }


def ist_zielstil(urteil: dict) -> bool:
    return (
        float(urteil["x"]) >= 2 / 3 and float(urteil["y"]) >= 2 / 3
        and urteil["polaritaet"] == "hell_auf_dunkel"
        and urteil["farbe"] == "weiss_grau"
        and urteil["format"] == "praefix_oder_nullen")


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--eval-root", type=Path, default=Path(r"C:\KI_BRAIN\eval_set\osd"))
    parser.add_argument("--queue", type=Path, default=Path(
        r"C:\KI_BRAIN\training\diagnostics\osd_layout_review_v1"))
    parser.add_argument("--review", type=Path, default=Path(
        r"C:\KI_BRAIN\eval_review\osd_layout_review_v1.json"))
    parser.add_argument("--weak-source", type=Path, default=Path(
        r"C:\KI_BRAIN\training\diagnostics\osd_wahrheit_protokoll_v1\wahrheit.json"))
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)

    templates = osd_meter.get_templates()
    gold = {}
    for name in ("osd_sd_v1", "osd_hd_v1", "osd_hd2_v1"):
        root = args.eval_root / name
        manifest_path = root / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        zeilen = []
        gelieferte_faelle = []
        for eintrag in manifest["eintraege"]:
            bild = root / "frames" / eintrag["datei"]
            if sha256(bild) != eintrag["bild_sha256"]:
                raise SystemExit(f"Goldbild wurde veraendert: {name}/{eintrag['datei']}")
            ergebnis = osd_meter.lese_meter(bild_laden(bild), templates)
            soll = eintrag["meter"] if eintrag["menschlich_lesbar"] else None
            zeilen.append((ergebnis["meter"], soll))
            fall = gelieferter_gold_fall(eintrag, ergebnis)
            if fall is not None:
                gelieferte_faelle.append(fall)
        gold[name] = {
            **statistik(zeilen),
            "manifest_sha256": sha256(manifest_path),
            "gelieferte_faelle": gelieferte_faelle,
        }

    queue_path = args.queue / "queue.json"
    queue = json.loads(queue_path.read_text(encoding="utf-8-sig"))
    review = json.loads(args.review.read_text(encoding="utf-8-sig"))
    if review.get("queue_sha256") != sha256(queue_path):
        raise SystemExit("Layout-Review gehoert nicht zur Queue.")
    if len(review.get("entscheidungen") or {}) != len(queue.get("faelle") or []):
        raise SystemExit("Layout-Review ist unvollstaendig.")
    source = json.loads(args.weak_source.read_text(encoding="utf-8-sig"))
    soll_je_hash = {e["bild_sha256"]: float(e["soll_meter"]) for e in source["eintraege"]}
    alle, ziel, fallback = [], [], []
    for fall in queue["faelle"]:
        bild = args.queue / "bilder" / fall["bild"]
        if sha256(bild) != fall["bild_sha256"]:
            raise SystemExit(f"Queue-Bild wurde veraendert: {fall['bild']}")
        soll = soll_je_hash.get(fall["bild_sha256"])
        if soll is None:
            raise SystemExit(f"Schwaches Label fehlt: {fall['fall_id']}")
        ergebnis = osd_meter.lese_meter(bild_laden(bild), templates)
        paar = (ergebnis["meter"], soll)
        alle.append(paar)
        if ist_zielstil(review["entscheidungen"][fall["fall_id"]]):
            ziel.append(paar)
        if ergebnis["leseweg"] == "tesseract_vierziffern":
            fallback.append(paar)

    bericht = {
        "schema": "osd_prefix_fallback_bericht_v1",
        "status": "diagnostic_not_deployed",
        "reader_sha256": sha256(REPO / "sidecar" / "sidecar" / "osd_meter.py"),
        "gold": gold,
        "archiv_40_schwache_labels": statistik(alle),
        "zielstil_12_schwache_labels": statistik(ziel),
        "neuer_rueckfallweg_schwache_labels": statistik(fallback),
        "bindungen": {
            "queue_sha256": sha256(queue_path),
            "review_sha256": sha256(args.review),
            "weak_source_sha256": sha256(args.weak_source),
        },
        "einordnung": (
            "Gold misst Richtigkeit und Abdeckung gegen menschliche Ablesungen. "
            "Die 40er- und 12er-Werte vergleichen nur gegen schwache PDF-Zeit-Labels; "
            "sie sind Diagnose und keine Freigabe."),
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    temp = args.out.with_suffix(args.out.suffix + ".tmp")
    temp.write_text(json.dumps(bericht, indent=2, ensure_ascii=False), encoding="utf-8")
    temp.replace(args.out)
    print(json.dumps(bericht, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
