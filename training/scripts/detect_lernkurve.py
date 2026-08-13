"""Misst, ob mehr handgezeichnete Goldboxen dem Detektor noch helfen.

DIE FRAGE
Der beste Mehrklassen-Kandidat erreicht auf dem Holdout 56 % Precision, aber nur
26 % Recall. Ist das Materialmangel — dann lohnt jede weitere Box — oder ist die
Kurve flach, dann hilft Handarbeit nicht mehr und es braucht etwas anderes.

Das ist die teuerste offene Frage im Projekt: Sie kostet Wochen von Pascals Zeit,
falls die Antwort falsch geraten wird.

WIE
Derselbe Datensatz, dieselbe Validierung, nur der TRAININGSTEIL wird verkleinert.
Gezogen wird ueber die physische Haltung, nie ueber das Bild — zwei Bilder
derselben Fahrt sind abhaengige Beispiele.

WAS DIESE ZAHLEN SIND UND WAS NICHT
Interne Validierung, kein Holdout. Sie ist bekanntermassen freundlicher als die
Wirklichkeit; im Projekt lagen Testteil und Video schon um 60 Punkte auseinander.
Aussagekraeftig ist hier NUR die Form der Kurve zwischen den Stufen, nicht die
absolute Hoehe.

Diese Laeufe sind reine Diagnose. Sie entstehen ausserhalb der Freigabekette,
tragen kein Kandidatenmanifest und duerfen nie aktiviert werden.
"""

from __future__ import annotations

import argparse
import json
import random
import shutil
import sys
from collections import defaultdict
from pathlib import Path
from typing import Sequence


def physische_haltung(name: str) -> str:
    n = (name or "").strip().lower()
    teile = n.split("-", 1)
    return min({n, f"{teile[1]}-{teile[0]}"}) if len(teile) == 2 else n


def haltung_aus_dateiname(name: str) -> str:
    """Der Exportname ist `img_<sha>.<endung>` — die Haltung steht im Label nicht.

    Ohne Haltungsangabe wird ueber den Bildnamen gruppiert. Das ist gröber als
    eine echte Haltungsgruppierung, verhindert aber wenigstens, dass dasselbe
    Bild in zwei Stufen verschieden behandelt wird.
    """
    return name


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--ziel", type=Path, required=True)
    parser.add_argument("--anteil", type=float, required=True)
    parser.add_argument("--saat", default="lernkurve-2026-08-13")
    args = parser.parse_args(argv)

    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits: {args.ziel}")
    if not (0.05 <= args.anteil <= 1.0):
        raise SystemExit(f"Unplausibler Anteil: {args.anteil}")

    bilder = sorted((args.dataset / "images" / "train").glob("*"))
    if not bilder:
        raise SystemExit(f"Keine Trainingsbilder in {args.dataset}")

    gruppen: dict[str, list[Path]] = defaultdict(list)
    for b in bilder:
        gruppen[haltung_aus_dateiname(b.stem)].append(b)

    schluessel = sorted(gruppen)
    random.Random(args.saat).shuffle(schluessel)
    behalten = set(schluessel[:max(1, round(len(schluessel) * args.anteil))])

    arbeit = args.ziel.with_name(f".{args.ziel.name}.arbeit")
    shutil.rmtree(arbeit, ignore_errors=True)
    for teil in ("train", "val"):
        (arbeit / "images" / teil).mkdir(parents=True)
        (arbeit / "labels" / teil).mkdir(parents=True)

    uebernommen = 0
    for k in behalten:
        for b in gruppen[k]:
            shutil.copy2(b, arbeit / "images" / "train" / b.name)
            label = args.dataset / "labels" / "train" / (b.stem + ".txt")
            if label.is_file():
                shutil.copy2(label, arbeit / "labels" / "train" / label.name)
            uebernommen += 1

    # Validierung bleibt VOLLSTAENDIG und unveraendert — sonst vergleicht die
    # Kurve verschiedene Massstaebe.
    for b in sorted((args.dataset / "images" / "val").glob("*")):
        shutil.copy2(b, arbeit / "images" / "val" / b.name)
        label = args.dataset / "labels" / "val" / (b.stem + ".txt")
        if label.is_file():
            shutil.copy2(label, arbeit / "labels" / "val" / label.name)

    for name in ("data.yaml", "classes.txt"):
        quelle = args.dataset / name
        if quelle.is_file():
            shutil.copy2(quelle, arbeit / name)

    (arbeit / "lernkurve.json").write_bytes(json.dumps({
        "schema": "detect_lernkurve_stufe_v1",
        "zweck": "Reine Diagnose. Kein Kandidat, kein Manifest, nie aktivieren.",
        "quelle": str(args.dataset),
        "anteil": args.anteil,
        "saat": args.saat,
        "gruppen_gesamt": len(schluessel),
        "gruppen_behalten": len(behalten),
        "trainingsbilder": uebernommen,
        "validierung": "unveraendert vollstaendig",
    }, indent=1, ensure_ascii=False).encode("utf-8"))
    arbeit.rename(args.ziel)

    print(f"{args.anteil:.0%}: {uebernommen} Trainingsbilder aus {len(behalten)} von "
          f"{len(schluessel)} Gruppen, Validierung unveraendert")
    print(f"Datensatz: {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
