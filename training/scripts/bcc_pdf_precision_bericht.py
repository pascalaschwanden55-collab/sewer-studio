"""Berechnet Precision erst nach vollstaendiger blinder BCC-Clip-Pruefung."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
from typing import Sequence


def sha256_datei(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


def wilson95(treffer: int, gesamt: int) -> tuple[float, float] | None:
    if gesamt <= 0:
        return None
    z = 1.959963984540054
    p = treffer / gesamt
    nenner = 1 + z * z / gesamt
    mitte = (p + z * z / (2 * gesamt)) / nenner
    halb = z * math.sqrt((p * (1 - p) + z * z / (4 * gesamt)) / gesamt) / nenner
    return round(max(0.0, mitte - halb), 4), round(min(1.0, mitte + halb), 4)


def precision_berechnen(queue: dict, review: dict) -> dict:
    faelle = queue.get("faelle") or []
    urteile = review.get("urteile") or {}
    erlaubt = {fall["fall_id"] for fall in faelle}
    fremd = sorted(set(urteile) - erlaubt)
    if fremd:
        raise ValueError("Review enthaelt Faelle, die nicht zur Warteschlange gehoeren.")
    offen = sorted(erlaubt - set(urteile))
    if offen:
        raise ValueError(f"Review ist unvollstaendig: {len(offen)} Faelle offen.")

    bogen = sum(1 for eintrag in urteile.values() if eintrag.get("urteil") == "bogen")
    kein_bogen = sum(
        1 for eintrag in urteile.values() if eintrag.get("urteil") == "kein_bogen")
    unsicher = sum(1 for eintrag in urteile.values() if eintrag.get("urteil") == "unsicher")
    if bogen + kein_bogen + unsicher != len(faelle):
        raise ValueError("Review enthaelt unbekannte Urteile.")
    entschieden = bogen + kein_bogen
    gesamt = len(faelle)
    return {
        "schema": "bcc_pdf_precision_v1",
        "status": "vollstaendig",
        "voller_bestand": bool(queue.get("voller_bestand")),
        "population_vorschlaege": int(queue.get("population_vorschlaege") or gesamt),
        "geprueft": gesamt,
        "bogen": bogen,
        "kein_bogen": kein_bogen,
        "unsicher": unsicher,
        "precision_ohne_unsichere": round(bogen / entschieden, 4) if entschieden else None,
        "wilson95_ohne_unsichere": wilson95(bogen, entschieden),
        "precision_untere_grenze": round(bogen / gesamt, 4) if gesamt else None,
        "precision_obere_grenze": round((bogen + unsicher) / gesamt, 4) if gesamt else None,
        "hinweis": "Unsichere Faelle werden als Unter- und Obergrenze ausgewiesen.",
    }


def atomar_schreiben(ziel: Path, daten: dict) -> None:
    ziel.parent.mkdir(parents=True, exist_ok=True)
    temp = ziel.with_suffix(ziel.suffix + ".tmp")
    temp.write_text(json.dumps(daten, indent=2, ensure_ascii=False), encoding="utf-8")
    temp.replace(ziel)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--queue", type=Path, required=True)
    parser.add_argument("--review", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)

    queue_path = args.queue / "queue.json" if args.queue.is_dir() else args.queue
    if not queue_path.is_file() or not args.review.is_file():
        raise SystemExit("Warteschlange oder Review fehlt.")
    queue_sha = sha256_datei(queue_path)
    queue = json.loads(queue_path.read_text(encoding="utf-8-sig"))
    review = json.loads(args.review.read_text(encoding="utf-8-sig"))
    if review.get("queue_sha256") != queue_sha:
        raise SystemExit("Review und Warteschlange passen nicht zusammen.")
    try:
        bericht = precision_berechnen(queue, review)
    except ValueError as fehler:
        raise SystemExit(str(fehler)) from fehler
    bericht["queue_sha256"] = queue_sha
    bericht["review_sha256"] = sha256_datei(args.review)
    atomar_schreiben(args.out, bericht)
    print(f"Precision: {bericht['precision_ohne_unsichere']:.1%}")
    print(f"Bericht: {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
