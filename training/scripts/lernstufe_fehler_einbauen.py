"""Legt einen eingesammelten Fehlerbestand als zusaetzliche Gegenbeispiele in einen Lernbestand.

Der neue Bestand ist eine vollstaendige Kopie des alten plus der Fehlerbilder.
Der eingefrorene Testteil bleibt BYTEGLEICH — nur so sind altes und neues
Modell auf derselben Grundlage vergleichbar.

Alle Fehlerbilder gehen in den Trainingsteil, auch wenn der Fehlerbestand sie
anders aufgeteilt hat: Sie stammen aus Trainingshaltungen, und eine
Trainingshaltung darf nie in der Validierung auftauchen. Sonst misst die
Validierung teilweise sich selbst und das fruehe Abbrechen greift zu spaet.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path
from typing import Sequence


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bestand", type=Path, required=True)
    parser.add_argument("--fehlerbestand", type=Path, required=True)
    parser.add_argument("--ziel", type=Path, required=True)
    parser.add_argument("--pruefung", type=Path, default=None,
                        help="Abgeschlossene Stichprobe; ihr Fehleranteil wird ins Manifest geschrieben")
    args = parser.parse_args(argv)

    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits: {args.ziel}")

    alt = json.loads((args.bestand / "manifest.json").read_text(encoding="utf-8-sig"))
    fehler = json.loads((args.fehlerbestand / "manifest.json").read_text(encoding="utf-8-sig"))
    negativ = alt["klasse_negativ"]

    if fehler.get("klasse") != alt.get("klasse_positiv"):
        raise SystemExit(f"Klassen passen nicht: {fehler.get('klasse')} vs {alt.get('klasse_positiv')}")

    # Haltungen des eingefrorenen Testteils — kein Fehlerbild darf von dort kommen.
    test_haltungen = {e["physische_haltung"] for e in alt["eintraege"] if e["split"] == "test"}
    val_haltungen = {e["physische_haltung"] for e in alt["eintraege"] if e["split"] == "validation"}
    bekannt = {e["bild_sha256"] for e in alt["eintraege"]}

    arbeit = args.ziel.with_name(f".{args.ziel.name}.arbeit")
    shutil.rmtree(arbeit, ignore_errors=True)
    print("Kopiere den bestehenden Bestand …", flush=True)
    shutil.copytree(args.bestand, arbeit)

    eintraege = list(alt["eintraege"])
    uebernommen = verworfen = doppelt = 0
    for e in fehler["eintraege"]:
        if e["physische_haltung"] in test_haltungen or e["physische_haltung"] in val_haltungen:
            verworfen += 1
            continue
        if e["bild_sha256"] in bekannt:
            doppelt += 1
            continue
        bekannt.add(e["bild_sha256"])
        quelle = args.fehlerbestand / e["bild"]
        daten = quelle.read_bytes()
        if hashlib.sha256(daten).hexdigest() != e["bild_sha256"]:
            raise SystemExit(f"Fehlerbild weicht von seinem Hash ab: {e['bild']}")
        name = f"fehler_{e['bild_sha256'][:16]}.jpg"
        (arbeit / "train" / negativ / name).write_bytes(daten)
        eintraege.append({"haltung": e["haltung"], "physische_haltung": e["physische_haltung"],
                          "klasse": negativ, "split": "train", "video": None,
                          "sekunde": e["sekunde"], "code": None, "meter": None,
                          "herkunft": "fehlalarm", "konfidenz": e["konfidenz"],
                          "bild": f"train/{negativ}/{name}", "bild_sha256": e["bild_sha256"]})
        uebernommen += 1

    anteil = None
    if args.pruefung and args.pruefung.is_file():
        p = json.loads(args.pruefung.read_text(encoding="utf-8-sig"))
        z = p["zusammenfassung"]
        anteil = {"geprueft": p["beurteilt"], "doch_sichtbar": z.get("sichtbar", 0),
                  "unsicher": z.get("unsicher", 0),
                  "anteil_falsch": round(z.get("sichtbar", 0) / p["beurteilt"], 4)}

    manifest = dict(alt)
    manifest |= {
        "schema": "bcc_lernstufe_protokoll_v1",
        "abgeleitet_von": {"bestand": str(args.bestand),
                           "bestand_manifest_sha256": (args.bestand / "manifest.sha256")
                           .read_text(encoding="utf-8").strip(),
                           "fehlerbestand": str(args.fehlerbestand),
                           "fehlerbestand_manifest_sha256": (args.fehlerbestand / "manifest.sha256")
                           .read_text(encoding="utf-8").strip()},
        "fehlerbilder_uebernommen": uebernommen,
        "fehlerbilder_verworfen_split": verworfen,
        "fehlerbilder_doppelt": doppelt,
        "fehlerbilder_stichprobe": anteil,
        "testteil_unveraendert": True,
        "eintraege": eintraege,
        "splits": {s: {k: sum(1 for e in eintraege if e["split"] == s and e["klasse"] == k)
                       for k in (alt["klasse_positiv"], negativ)}
                   for s in ("train", "validation", "test")},
    }
    text = json.dumps(manifest, indent=1, ensure_ascii=False)
    (arbeit / "manifest.json").write_bytes(text.encode("utf-8"))
    (arbeit / "manifest.sha256").write_bytes(
        (hashlib.sha256(text.encode("utf-8")).hexdigest() + "\n").encode("utf-8"))
    arbeit.rename(args.ziel)

    print(f"\n{uebernommen} Fehlerbilder uebernommen, {verworfen} wegen Split verworfen, "
          f"{doppelt} bereits vorhanden")
    for s, w in manifest["splits"].items():
        print(f"  {s:<12}" + "  ".join(f"{k} {v}" for k, v in w.items()))
    if anteil:
        print(f"\n  Stichprobe: {anteil['doch_sichtbar']} von {anteil['geprueft']} "
              f"Fehlerbildern zeigten doch einen Befund ({anteil['anteil_falsch']:.0%})")
    print(f"\nBestand: {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
