"""Baut eine blinde Sichtpruefung fuer die Negativbilder eines Lernbestands.

WARUM
Ein Negativbild traegt die Behauptung "hier ist der Befund NICHT zu sehen".
Diese Behauptung stammt aus dem Protokoll, nicht aus dem Bild. Zwei Wege
koennen sie brechen:

1. Befundfreie Haltung — das Protokoll nennt keinen Code. Vielleicht war
   wirklich nichts da, vielleicht wurde es nur nicht codiert.
2. Nahfeld — dieselbe Haltung, aber weit weg von jeder Fundstelle. Hier ist
   das Risiko groesser: Die Haltung hat den Befund nachweislich, und ein
   zweites, nicht protokolliertes Vorkommen ist gut moeglich.

Ohne diese Pruefung lernt das Modell im schlechtesten Fall, den Befund als
"nicht der Befund" einzuordnen. Der Fehler waere unsichtbar: Er senkt still
den Recall und niemand sieht warum.

WAS DIESES WERKZEUG NICHT TUT
Es misst nicht das Modell. Es prueft nur, ob die Negativbehauptung der
Trainingsdaten haelt. Kein Modell ist an der Auswahl beteiligt.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import random
import shutil
import sys
from pathlib import Path
from typing import Sequence


def sha256_bytes(daten: bytes) -> str:
    return hashlib.sha256(daten).hexdigest()


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bestand", type=Path, required=True)
    parser.add_argument("--scan", type=Path, required=True,
                        help="Derselbe Protokollscan, aus dem der Bestand gebaut wurde")
    parser.add_argument("--ziel", type=Path, required=True)
    parser.add_argument("--anzahl", type=int, default=30)
    parser.add_argument("--quelle", choices=("nahfeld", "befundfrei", "beide"), default="nahfeld")
    parser.add_argument("--frage", default="")
    parser.add_argument("--saat", default="")
    args = parser.parse_args(argv)

    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits: {args.ziel}")

    manifest = json.loads((args.bestand / "manifest.json").read_text(encoding="utf-8-sig"))
    # Zwei Bestandsarten teilen sich diesen Pruefweg: der normale Lernbestand
    # und der eingesammelte Fehlerbestand. Beide behaupten dasselbe ueber ihre
    # Bilder — "der Befund ist hier nicht zu sehen" — und beide koennen irren.
    fehlerbestand = manifest.get("schema") == "lernstufe_fehlerbestand_v1"
    positiv = manifest["klasse"] if fehlerbestand else manifest["klasse_positiv"]
    negativ = None if fehlerbestand else manifest["klasse_negativ"]

    # Haltungen MIT Befund im Protokoll — daraus stammen die Nahfeld-Negative.
    scan = json.loads(args.scan.read_text(encoding="utf-8-sig"))
    mit_befund = {e["haltung"] for e in scan["ergebnisse"] if e.get("codes")}

    kandidaten = []
    for e in manifest["eintraege"]:
        if not fehlerbestand and e["klasse"] != negativ:
            continue
        ist_nahfeld = e["haltung"] in mit_befund
        if args.quelle == "nahfeld" and not ist_nahfeld:
            continue
        if args.quelle == "befundfrei" and ist_nahfeld:
            continue
        e = dict(e)
        e["nahfeld"] = ist_nahfeld
        kandidaten.append(e)

    if not kandidaten:
        raise SystemExit(f"Keine Negativbilder der Quelle {args.quelle!r} im Bestand")

    # Hoechstens ein Bild je physischer Haltung: zwei Bilder derselben Fahrt
    # sind kein zweiter unabhaengiger Beleg.
    #
    # Bei `beide` wird GESCHICHTET gezogen. Die Nahfeld-Bilder sind die kleinere
    # Gruppe; eine freie Ziehung erwischt sie kaum. Beim ersten Versuch am
    # 2026-08-11 lag genau 1 Nahfeld-Bild in 30 — die neue Quelle waere
    # ungeprueft geblieben.
    kandidaten.sort(key=lambda k: k["bild"])
    r = random.Random(args.saat or f"{args.bestand.name}|{args.quelle}")
    r.shuffle(kandidaten)

    if fehlerbestand:
        # Beim Fehlerbestand trennt "Nahfeld" nichts Sinnvolles — jedes Bild ist
        # ein Fehlalarm des Modells. Ungeschichtet ziehen.
        gruppen = [(kandidaten, args.anzahl)]
    elif args.quelle == "beide":
        haelfte = args.anzahl // 2
        gruppen = [([k for k in kandidaten if k["nahfeld"]], args.anzahl - haelfte),
                   ([k for k in kandidaten if not k["nahfeld"]], haelfte)]
    else:
        gruppen = [(kandidaten, args.anzahl)]

    gesehen: set[str] = set()
    auswahl = []
    for gruppe, soll in gruppen:
        genommen = 0
        for k in gruppe:
            if genommen >= soll:
                break
            if k["physische_haltung"] in gesehen:
                continue
            gesehen.add(k["physische_haltung"])
            auswahl.append(k)
            genommen += 1
        if genommen < soll:
            print(f"  Hinweis: nur {genommen} von {soll} Bildern verfuegbar "
                  f"({'Nahfeld' if gruppe and gruppe[0]['nahfeld'] else 'befundfrei'})")

    arbeit = args.ziel.with_name(f".{args.ziel.name}.arbeit")
    shutil.rmtree(arbeit, ignore_errors=True)
    (arbeit / "images").mkdir(parents=True)

    faelle = []
    for nr, k in enumerate(sorted(auswahl, key=lambda x: x["bild"]), start=1):
        quelle = args.bestand / k["bild"]
        daten = quelle.read_bytes()
        if sha256_bytes(daten) != k["bild_sha256"]:
            raise SystemExit(f"Bild weicht vom Manifest ab: {k['bild']}")
        name = f"bild_{nr:03d}.jpg"
        (arbeit / "images" / name).write_bytes(daten)
        # Blind: keine Haltung, keine Sekunde, kein Code im Fall.
        faelle.append({"nummer": nr, "bild": f"images/{name}", "bild_sha256": k["bild_sha256"]})

    frage = args.frage or f"Ist ein Befund der Klasse {positiv!r} im Bild sichtbar?"
    queue = {
        "schema": "lernstufe_negativ_pruefung_v1",
        "zweck": ("Blinde Pruefung der Negativbehauptung eines Lernbestands. "
                  "Misst NICHT das Modell."),
        "kein_modell_beteiligt": True,
        "bestand": str(args.bestand),
        "bestand_manifest_sha256": (args.bestand / "manifest.sha256").read_text(encoding="utf-8").strip(),
        "bestandsart": "fehlerbestand" if fehlerbestand else "lernbestand",
        "quelle": "fehlalarme" if fehlerbestand else args.quelle,
        "grundgesamtheit": len(kandidaten),
        "je_physischer_haltung": 1,
        "saat": args.saat or f"{args.bestand.name}|{args.quelle}",
        "frage": frage,
        "urteile": [
            # Schluessel "beschriftung" — so liest der Pruefplatz die Knoepfe.
            {"wert": "nicht_sichtbar", "beschriftung": "Nichts davon zu sehen", "taste": "1"},
            {"wert": "sichtbar", "beschriftung": "DOCH sichtbar", "taste": "2"},
            {"wert": "unsicher", "beschriftung": "Unsicher / Bild unbrauchbar", "taste": "3"},
        ],
        "faelle": faelle,
        # Zuordnung Fall -> Herkunft, damit die Auswertung spaeter nachvollziehbar
        # bleibt. Der Pruefplatz liest diesen Block nicht.
        "aufloesung": [
            {"nummer": nr, "haltung": k["haltung"], "sekunde": k["sekunde"],
             "nahfeld": k["nahfeld"], "bild": k["bild"]}
            for nr, k in enumerate(sorted(auswahl, key=lambda x: x["bild"]), start=1)
        ],
    }
    text = json.dumps(queue, indent=1, ensure_ascii=False)
    (arbeit / "queue.json").write_bytes(text.encode("utf-8"))
    (arbeit / "queue.sha256").write_bytes((sha256_bytes(text.encode("utf-8")) + "\n").encode("utf-8"))
    arbeit.rename(args.ziel)

    print(f"Grundgesamtheit  {len(kandidaten)} Negativbilder der Quelle {args.quelle!r}")
    print(f"Stichprobe       {len(faelle)} Bilder aus {len(faelle)} physischen Haltungen")
    print(f"Frage            {frage}")
    print(f"\nQueue: {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
