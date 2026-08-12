"""Schreibt die Freigabedatei eines Lernstufen-Modells — oder verweigert sie.

WOZU
Ein Modell darf nur ins Programm, wenn belegt ist, WAS gemessen wurde, MIT WELCHER
Regel und WIE GUT. Diese Datei bindet Gewicht, Lernbestand, Messregel und
Ergebnis in einem hashgepruefte Dokument zusammen. Ohne sie lehnt der Sidecar
das Modell ab.

Der Anlass ist real: Das aktive Detect-Altmodell lief lange produktiv, bis 2026-07-25
auffiel, dass seine Boxen kollabiert waren. Seither gilt: kein Modell ohne
ausdrueckliche, gebundene Freigabe.

WAS GEPRUEFT WIRD, BEVOR ETWAS GESCHRIEBEN WIRD
- Gewicht vorhanden, lesbar, kein Link
- Lernbestand vorhanden, Manifest passt zu seinem Hash
- Abnahme vollstaendig beurteilt und an genau diese Videoauswahl gebunden
- Precision UND Recall erreichen die in der Abnahme vorher festgelegte Grenze

Fehlt eines davon, entsteht KEINE Datei. Ein Modell ohne Freigabe ist gesperrt,
nicht "vorlaeufig erlaubt".
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence


def sha256_datei(pfad: Path) -> str:
    h = hashlib.sha256()
    with pfad.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def ist_link(pfad: Path) -> bool:
    try:
        return pfad.is_symlink() or bool(
            os.stat(pfad, follow_symlinks=False).st_file_attributes & 0x400)  # REPARSE_POINT
    except (OSError, AttributeError):
        return pfad.is_symlink()


def wilson(treffer: int, gesamt: int, z: float = 1.96) -> tuple[float, float]:
    if not gesamt:
        return (0.0, 0.0)
    import math
    p = treffer / gesamt
    nenner = 1 + z * z / gesamt
    mitte = (p + z * z / (2 * gesamt)) / nenner
    spanne = z * math.sqrt(p * (1 - p) / gesamt + z * z / (4 * gesamt * gesamt)) / nenner
    return (max(0.0, mitte - spanne), min(1.0, mitte + spanne))


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gewicht", type=Path, required=True)
    parser.add_argument("--bestand", type=Path, required=True)
    parser.add_argument("--klasse", required=True)
    parser.add_argument("--auswahl", type=Path, required=True,
                        help="Vorregistrierte Videoauswahl mit regel_vollstaendig_vorher")
    parser.add_argument("--vorschlaege", type=Path, required=True, help="Queue der Modellvorschlaege")
    parser.add_argument("--vorschlaege-review", type=Path, required=True)
    parser.add_argument("--wahrheit", type=Path, required=True, help="Queue der Wahrheitsclips")
    parser.add_argument("--wahrheit-review", type=Path, required=True)
    parser.add_argument("--ziel", type=Path, required=True)
    args = parser.parse_args(argv)

    fehler: list[str] = []

    if not args.gewicht.is_file():
        fehler.append(f"Gewicht fehlt: {args.gewicht}")
    elif ist_link(args.gewicht):
        fehler.append(f"Gewicht ist eine Verknuepfung: {args.gewicht}")

    manifest_pfad = args.bestand / "manifest.json"
    if not manifest_pfad.is_file():
        fehler.append(f"Lernbestand ohne Manifest: {args.bestand}")
    else:
        roh = manifest_pfad.read_bytes()
        soll = (args.bestand / "manifest.sha256").read_text(encoding="utf-8").strip()
        if hashlib.sha256(roh).hexdigest() != soll:
            fehler.append("Lernbestand-Manifest passt nicht zu seinem Hash")

    auswahl = json.loads(args.auswahl.read_text(encoding="utf-8-sig"))
    regel = auswahl.get("regel_vollstaendig_vorher")
    if not regel:
        fehler.append("Die Videoauswahl enthaelt keine vorher festgelegte Regel")

    qv = json.loads((args.vorschlaege / "queue.json").read_text(encoding="utf-8-sig"))
    rv = json.loads(args.vorschlaege_review.read_text(encoding="utf-8-sig"))
    qw = json.loads((args.wahrheit / "queue.json").read_text(encoding="utf-8-sig"))
    rw = json.loads(args.wahrheit_review.read_text(encoding="utf-8-sig"))

    for name, review, queue in (("Vorschlaege", rv, qv), ("Wahrheit", rw, qw)):
        if not review.get("vollstaendig"):
            fehler.append(f"{name}-Review ist unvollstaendig")
        if review.get("queue_sha256") != (queue_sha := (
                (args.vorschlaege if name == "Vorschlaege" else args.wahrheit)
                / "queue.sha256").read_text(encoding="utf-8").strip()):
            fehler.append(f"{name}-Review gehoert zu einer anderen Warteschlange")
        del queue_sha

    auf = {a["nummer"]: a for a in qv["aufloesung"]}
    u = {x["nummer"]: x["urteil"] for x in rv["urteile"]}
    uw = {x["nummer"]: x["urteil"] for x in rw["urteile"]}
    wahr = {a["haltung"]: uw[a["nummer"]] for a in qw["aufloesung"] if a["nummer"] in uw}

    ja = sum(1 for v in u.values() if v == "sichtbar")
    nein = sum(1 for v in u.values() if v == "nicht_sichtbar")
    unsicher = len(u) - ja - nein
    bestaetigt = {auf[n]["haltung"] for n, v in u.items() if v == "sichtbar"}
    mit_befund = {h for h, v in wahr.items() if v == "sichtbar"}
    gefunden = len(bestaetigt & mit_befund)

    if not (ja + nein) or not mit_befund:
        fehler.append("Zu wenige verwertbare Urteile fuer eine Freigabe")
        precision = recall = 0.0
    else:
        precision = ja / (ja + nein)
        recall = gefunden / len(mit_befund)

    grenze_text = (regel or {}).get("bestanden_ab", "")
    grenze = 0.75 if ">= 75" in grenze_text else None
    if grenze is None:
        fehler.append(f"Bestehensgrenze nicht erkannt: {grenze_text!r}")
    elif precision < grenze or recall < grenze:
        fehler.append(f"Grenze nicht erreicht: Precision {precision:.1%}, Recall {recall:.1%}, "
                      f"verlangt >= {grenze:.0%}")

    if fehler:
        print("KEINE FREIGABE:")
        for f in fehler:
            print(f"   {f}")
        return 1

    plo, phi = wilson(ja, ja + nein)
    rlo, rhi = wilson(gefunden, len(mit_befund))
    freigabe = {
        "schema": "lernstufe_freigabe_v1",
        "status": "freigegeben",
        "klasse": args.klasse,
        "erstellt_utc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "gewicht": str(args.gewicht),
        "gewicht_sha256": sha256_datei(args.gewicht),
        "lernbestand": str(args.bestand),
        "lernbestand_manifest_sha256": (args.bestand / "manifest.sha256")
        .read_text(encoding="utf-8").strip(),
        "regel": regel,
        "abnahme": {
            "videoauswahl": str(args.auswahl),
            "videos": len(qw["aufloesung"]),
            "vorschlaege_queue_sha256": (args.vorschlaege / "queue.sha256")
            .read_text(encoding="utf-8").strip(),
            "wahrheit_queue_sha256": (args.wahrheit / "queue.sha256")
            .read_text(encoding="utf-8").strip(),
            "vorschlaege": ja + nein + unsicher,
            "bestaetigt": ja, "verworfen": nein, "unsicher": unsicher,
            "videos_mit_befund": len(mit_befund), "davon_gefunden": gefunden,
            "precision": round(precision, 4), "precision_95": [round(plo, 4), round(phi, 4)],
            "recall": round(recall, 4), "recall_95": [round(rlo, 4), round(rhi, 4)],
        },
        "grenzen": {
            "keine_ereignis_precision": ("Gemessen wird der VORSCHLAG, nicht das Ereignis. "
                                         "Zwei Vorschlaege koennen dieselbe Stelle zeigen."),
            "recall_nur_im_wahrheitsausschnitt": ("Die Wahrheit deckt nur den Ausschnitt ab, "
                                                  "aus dem sie geschnitten wurde."),
            "menschliche_streuung": ("Bei 106 doppelt beurteilten Videos widersprach sich der "
                                     "Mensch in 3 Faellen. Unterschiede unter 3 Punkten sind "
                                     "mit diesem Verfahren nicht belastbar."),
        },
    }
    text = json.dumps(freigabe, indent=1, ensure_ascii=False)
    args.ziel.parent.mkdir(parents=True, exist_ok=True)
    args.ziel.write_bytes(text.encode("utf-8"))
    args.ziel.with_suffix(".sha256").write_bytes(
        (hashlib.sha256(text.encode("utf-8")).hexdigest() + "\n").encode("utf-8"))
    print(f"FREIGEGEBEN  {args.klasse}")
    print(f"   Precision {precision:.1%} ({plo:.0%}-{phi:.0%})   "
          f"Recall {recall:.1%} ({rlo:.0%}-{rhi:.0%})")
    print(f"   {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
