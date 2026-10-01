"""Vergleicht die blinde OSD-Sichtprobe mit den PDF-Sollwerten."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Sequence

ZEILE = re.compile(r"^(?P<nr>\d{4})\s*=\s*(?P<wert>.*)$")


def sha256_datei(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


def lesungen_laden(pfad: Path) -> dict[int, str]:
    ergebnis = {}
    for zeile in pfad.read_text(encoding="utf-8-sig").splitlines():
        treffer = ZEILE.match(zeile.strip())
        if treffer:
            ergebnis[int(treffer.group("nr"))] = treffer.group("wert").strip()
    return ergebnis


def auswerten(manifest: dict, lesungen: dict[int, str]) -> dict:
    offen = [f["nr"] for f in manifest["faelle"] if not lesungen.get(f["nr"], "")]
    if offen:
        raise ValueError(f"Sichtprobe ist unvollstaendig: {len(offen)} Bilder offen.")
    gleich = abweichend = unleserlich = 0
    auf_zehntel_passend = grob_abweichend = 0
    details = []
    for fall in manifest["faelle"]:
        roh = lesungen[fall["nr"]]
        if roh == "?":
            urteil = "unleserlich"
            unleserlich += 1
            gelesen = None
        else:
            try:
                gelesen = float(roh.replace(",", "."))
            except ValueError as fehler:
                raise ValueError(f"Ungueltige Lesung bei Bild {fall['nr']}: {roh}") from fehler
            if abs(gelesen - float(fall["soll_meter"])) <= 0.011:
                urteil = "gleich"
                gleich += 1
            else:
                urteil = "abweichend"
                abweichend += 1
            if abs(gelesen - float(fall["soll_meter"])) <= 0.101:
                auf_zehntel_passend += 1
            else:
                grob_abweichend += 1
        details.append({"nr": fall["nr"], "haltung": fall["haltung"],
                        "soll_meter": fall["soll_meter"], "gelesen_meter": gelesen,
                        "urteil": urteil})
    lesbar = gleich + abweichend
    return {
        "schema": "osd_protokoll_qa_bericht_v1",
        "status": "vollstaendig",
        "bilder": len(details),
        "gleich": gleich,
        "abweichend": abweichend,
        "unleserlich": unleserlich,
        "trefferquote_lesbar": round(gleich / lesbar, 4) if lesbar else None,
        "auf_zehntel_passend": auf_zehntel_passend,
        "grob_abweichend": grob_abweichend,
        "quote_auf_zehntel": round(auf_zehntel_passend / lesbar, 4) if lesbar else None,
        "einordnung": (
            "Die Sichtprobe misst die Zuordnung zwischen PDF-Zeit und Videobild, "
            "nicht die Genauigkeit des Lesers. Kleine Abweichungen koennen durch "
            "Kamerabewegung zwischen Protokollmoment und extrahiertem Bild entstehen. "
            "PDF-Werte bleiben schwache Labels; nur menschliche Ablesungen sind Gold."),
        "details": details,
    }


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--wurzel", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)
    manifest_path = args.wurzel / "qa_manifest.json"
    wahrheit = args.wurzel / "wahrheit.txt"
    if not manifest_path.is_file() or not wahrheit.is_file():
        raise SystemExit("QA-Manifest oder wahrheit.txt fehlt.")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    try:
        bericht = auswerten(manifest, lesungen_laden(wahrheit))
    except ValueError as fehler:
        raise SystemExit(str(fehler)) from fehler
    bericht["qa_manifest_sha256"] = sha256_datei(manifest_path)
    bericht["wahrheit_sha256"] = sha256_datei(wahrheit)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    temp = args.out.with_suffix(args.out.suffix + ".tmp")
    temp.write_text(json.dumps(bericht, indent=2, ensure_ascii=False), encoding="utf-8")
    temp.replace(args.out)
    print(f"Gleich: {bericht['gleich']}, abweichend: {bericht['abweichend']}, "
          f"unleserlich: {bericht['unleserlich']}")
    print(f"Bericht: {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
