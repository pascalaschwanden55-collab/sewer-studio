"""Wertet die vollstaendige menschliche OSD-Layout-Sichtung aus."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from collections import Counter
from pathlib import Path
from typing import Sequence


def sha256_datei(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


def lage(x: float, y: float) -> str:
    horizontal = "links" if x < 1 / 3 else "mitte" if x < 2 / 3 else "rechts"
    vertikal = "oben" if y < 1 / 3 else "mitte" if y < 2 / 3 else "unten"
    return f"{vertikal}_{horizontal}"


def anteil(anzahl: int, gesamt: int) -> dict:
    if gesamt <= 0:
        return {"anzahl": anzahl, "gesamt": gesamt, "anteil": None,
                "wilson_95": [None, None]}
    z = 1.959963984540054
    p = anzahl / gesamt
    nenner = 1 + z * z / gesamt
    mitte = (p + z * z / (2 * gesamt)) / nenner
    halb = z * math.sqrt(p * (1 - p) / gesamt + z * z / (4 * gesamt * gesamt)) / nenner
    return {"anzahl": anzahl, "gesamt": gesamt, "anteil": round(p, 4),
            "wilson_95": [round(mitte - halb, 4), round(mitte + halb, 4)]}


def anteile(zaehler: Counter[str], gesamt: int) -> dict:
    return {name: anteil(anzahl, gesamt) for name, anzahl in zaehler.most_common()}


def auswerten(queue: dict, review: dict, queue_sha256: str) -> dict:
    if review.get("queue_sha256") != queue_sha256:
        raise ValueError("Review und Queue gehoeren nicht zusammen.")
    faelle = list(queue.get("faelle") or [])
    entscheidungen = dict(review.get("entscheidungen") or {})
    ids = {str(f["fall_id"]) for f in faelle}
    if set(entscheidungen) != ids:
        raise ValueError(f"Sichtung ist unvollstaendig: {len(ids - set(entscheidungen))} offen.")

    lagen: Counter[str] = Counter()
    polaritaeten: Counter[str] = Counter()
    farben: Counter[str] = Counter()
    formate: Counter[str] = Counter()
    kombinationen: Counter[str] = Counter()
    ohne_meter = 0
    for fall_id in sorted(ids):
        eintrag = entscheidungen[fall_id]
        if not eintrag.get("meter_sichtbar"):
            ohne_meter += 1
            continue
        ort = lage(float(eintrag["x"]), float(eintrag["y"]))
        pol = str(eintrag["polaritaet"])
        farbe = str(eintrag["farbe"])
        format_name = str(eintrag["format"])
        lagen[ort] += 1
        polaritaeten[pol] += 1
        farben[farbe] += 1
        formate[format_name] += 1
        kombinationen[f"{ort}|{pol}|{farbe}|{format_name}"] += 1

    sichtbar = len(faelle) - ohne_meter
    return {
        "schema": "osd_layout_review_bericht_v1",
        "status": "vollstaendig",
        "haltungen": len(faelle),
        "meter_sichtbar": sichtbar,
        "kein_meter_sichtbar": ohne_meter,
        "lage": dict(lagen.most_common()),
        "polaritaet": dict(polaritaeten.most_common()),
        "farbe": dict(farben.most_common()),
        "format": dict(formate.most_common()),
        "kombinationen": dict(kombinationen.most_common()),
        "anteile_mit_wilson_95": {
            "nenner_sichtbare_meterstaende": sichtbar,
            "lage": anteile(lagen, sichtbar),
            "polaritaet": anteile(polaritaeten, sichtbar),
            "farbe": anteile(farben, sichtbar),
            "format": anteile(formate, sichtbar),
        },
        "einordnung": (
            "Die Zaehlung beschreibt 40 menschlich gesichtete Haltungen. Die Lage "
            "stammt aus dem Klick auf die Meteranzeige; Kopf- und Titeltext werden "
            "nicht automatisch als Meterstand gewertet."),
    }


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--queue", type=Path, required=True)
    parser.add_argument("--review", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)
    queue_path = args.queue / "queue.json"
    queue = json.loads(queue_path.read_text(encoding="utf-8-sig"))
    review = json.loads(args.review.read_text(encoding="utf-8-sig"))
    try:
        bericht = auswerten(queue, review, sha256_datei(queue_path))
    except ValueError as fehler:
        raise SystemExit(str(fehler)) from fehler
    bericht["queue_sha256"] = sha256_datei(queue_path)
    bericht["review_sha256"] = sha256_datei(args.review)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    temp = args.out.with_suffix(args.out.suffix + ".tmp")
    temp.write_text(json.dumps(bericht, indent=2, ensure_ascii=False), encoding="utf-8")
    temp.replace(args.out)
    print(f"Haltungen: {bericht['haltungen']}, Meter sichtbar: {bericht['meter_sichtbar']}")
    print(f"Bericht: {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
