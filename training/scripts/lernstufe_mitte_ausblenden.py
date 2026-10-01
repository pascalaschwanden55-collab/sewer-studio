"""Erzeugt aus einem Lernbestand eine Kopie mit ausgeblendeter Bildmitte.

WOZU
Aus der Aufnahmevorgabe (GIBZ, VSA-KEK): Codiert wird im NAHBEREICH, etwa 30 bis
40 cm vor der Linse. Die Kamera sieht in Axialsicht aber fuenf bis zehn Meter
voraus. Alles nahe am Fluchtpunkt — also grob in der Bildmitte — ist zu weit weg,
um codiert zu werden.

Ein Bild-Einordner kennt diesen Unterschied nicht. Er bewertet das ganze Bild und
schlaegt auf einen Riss an, der zehn Meter voraus sichtbar ist. Der Mensch sagt
dann zu Recht "hier ist keiner". Ein Teil der gemessenen Fehlalarme koennte also
gar kein Fehler des Modells sein, sondern ein richtiger Fund am falschen Ort.

Dieser Versuch prueft das: Dieselben Bilder, dieselbe Aufteilung, nur die Mitte
geschwaerzt. Wird das Modell dadurch im Video besser, war die Tiefe das Problem.

WICHTIG
Die Maske muss beim Anwenden GENAU GLEICH sein wie im Training. Beim Letterbox
gegen Zuschnitt ist genau dieser Fehler im Projekt schon einmal real geworden.

DIE WILLKUER DIESER FASSUNG
Der Anteil 0,40 ist gesetzt, nicht gemessen: Die Mitte ist nicht immer der
Fluchtpunkt, und wie weit "zu weit" ist, haengt vom Rohrdurchmesser ab. Erst
wenn dieser grobe Versuch etwas bewegt, lohnt eine echte Fluchtpunkt-Erkennung
(`sidecar/sidecar/models/bend_geometry.py` kann das, im HEAD abgeschaltet).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path
from typing import Sequence

MITTE_ANTEIL = 0.40


def maske_anwenden(bild, anteil: float):
    """Schwaerzt ein zentriertes Rechteck mit `anteil` der Kantenlaenge."""
    from PIL import ImageDraw

    breite, hoehe = bild.size
    bw, bh = breite * anteil, hoehe * anteil
    links, oben = (breite - bw) / 2, (hoehe - bh) / 2
    kopie = bild.convert("RGB")
    ImageDraw.Draw(kopie).rectangle(
        [links, oben, links + bw, oben + bh], fill=(0, 0, 0))
    return kopie


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bestand", type=Path, required=True)
    parser.add_argument("--ziel", type=Path, required=True)
    parser.add_argument("--anteil", type=float, default=MITTE_ANTEIL)
    args = parser.parse_args(argv)

    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits: {args.ziel}")
    if not (0.05 <= args.anteil <= 0.9):
        raise SystemExit(f"Unplausibler Anteil: {args.anteil}")

    from PIL import Image

    manifest = json.loads((args.bestand / "manifest.json").read_text(encoding="utf-8-sig"))
    arbeit = args.ziel.with_name(f".{args.ziel.name}.arbeit")
    shutil.rmtree(arbeit, ignore_errors=True)

    eintraege = []
    fehlend = 0
    for i, e in enumerate(manifest["eintraege"], start=1):
        quelle = args.bestand / e["bild"]
        if not quelle.is_file():
            # Kommt im Altbestand vor: Manifestzeile ohne Datei. Wird
            # uebersprungen statt den ganzen Lauf abzubrechen.
            fehlend += 1
            continue
        ziel = arbeit / e["bild"]
        ziel.parent.mkdir(parents=True, exist_ok=True)
        with Image.open(quelle) as roh:
            maske_anwenden(roh, args.anteil).save(ziel, quality=95)
        neu = dict(e)
        neu["bild_sha256"] = hashlib.sha256(ziel.read_bytes()).hexdigest()
        eintraege.append(neu)
        if i % 500 == 0:
            print(f"  {i}/{len(manifest['eintraege'])} …", flush=True)

    neu_manifest = dict(manifest)
    neu_manifest |= {
        "abgeleitet_von": str(args.bestand),
        "abgeleitet_von_manifest_sha256": (args.bestand / "manifest.sha256")
        .read_text(encoding="utf-8").strip(),
        "mitte_ausgeblendet": args.anteil,
        "warum": ("Codiert wird im Nahbereich; die Bildmitte zeigt in Axialsicht das "
                  "Ferne um den Fluchtpunkt. Die Maske muss beim Anwenden identisch sein."),
        "eintraege": eintraege,
    }
    text = json.dumps(neu_manifest, indent=1, ensure_ascii=False)
    (arbeit / "manifest.json").write_bytes(text.encode("utf-8"))
    (arbeit / "manifest.sha256").write_bytes(
        (hashlib.sha256(text.encode("utf-8")).hexdigest() + "\n").encode("utf-8"))
    arbeit.rename(args.ziel)

    print(f"\n{len(eintraege)} Bilder mit {args.anteil:.0%} ausgeblendeter Mitte")
    print(f"Bestand: {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
