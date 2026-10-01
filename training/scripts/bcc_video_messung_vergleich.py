"""Vergleicht zwei BCC-Videomessungen und uebertraegt die menschlichen Urteile.

DIAGNOSE. Die Urteile von Pascal haengen an den Gruppen der ersten Messung. Eine
zweite Messung bildet andere Zeitfenster; deshalb wird jede neue Gruppe ueber
zeitliche Ueberlappung auf ein vorhandenes Urteil abgebildet. Gruppen ohne
Ueberlappung bleiben ausdruecklich `unbeurteilt` und werden nie als Fehlalarm
gezaehlt — sie sind ungemessen, nicht falsch.

Das Werkzeug schreibt nur seinen eigenen Bericht.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Sequence

TOLERANZ_S = 15


def gruppen_je_haltung(bericht: dict) -> dict[str, list[dict]]:
    return {
        str(e["haltung"]): list(e.get("gruppen") or [])
        for e in (bericht.get("ergebnisse") or [])
    }


def treffer_je_haltung(bericht: dict) -> dict[str, tuple[int, int]]:
    werte: dict[str, tuple[int, int]] = {}
    for eintrag in bericht.get("ergebnisse") or []:
        werte[str(eintrag["haltung"])] = (
            int(eintrag.get("befunde_gefunden") or 0),
            int(eintrag.get("befunde_pruefbar") or 0),
        )
    return werte


def urteil_uebertragen(haltung: str, gruppe: dict, urteile: dict) -> str:
    """Urteil der ersten Messung uebernehmen, wenn sich die Zeitfenster beruehren."""
    if gruppe.get("ist_treffer"):
        return "protokoll_bogen"
    start = int(gruppe["start"])
    ende = int(gruppe["ende"])
    beste = None
    for eintrag in urteile.values():
        if eintrag["haltung"] != haltung:
            continue
        if start - TOLERANZ_S <= int(eintrag["ende_s"]) and int(eintrag["start_s"]) <= ende + TOLERANZ_S:
            # Ein bestaetigter Bogen schlaegt ein Fehlalarm-Urteil bei Mehrfachtreffern.
            if eintrag["urteil"] == "bogen":
                return "bogen"
            beste = beste or eintrag["urteil"]
    return beste or "unbeurteilt"


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Vergleich zweier BCC-Videomessungen")
    parser.add_argument(
        "--alt",
        type=Path,
        default=Path(r"C:\KI_BRAIN\training\diagnostics\bcc_video_messung_20260807\report.json"),
    )
    parser.add_argument(
        "--neu",
        type=Path,
        default=Path(r"C:\KI_BRAIN\training\diagnostics\bcc_video_messung_nc15_20260808\report.json"),
    )
    parser.add_argument(
        "--review",
        type=Path,
        default=Path(r"C:\KI_BRAIN\eval_review\bcc_video_fehlalarm_review.json"),
    )
    parser.add_argument("--ziel", type=Path, default=None)
    args = parser.parse_args(argv)

    alt = json.loads(args.alt.read_text(encoding="utf-8-sig"))
    neu = json.loads(args.neu.read_text(encoding="utf-8-sig"))
    urteile = json.loads(args.review.read_text(encoding="utf-8-sig"))["urteile"]

    alt_gruppen = gruppen_je_haltung(alt)
    neu_gruppen = gruppen_je_haltung(neu)
    alt_treffer = treffer_je_haltung(alt)
    neu_treffer = treffer_je_haltung(neu)

    print(f"{'Haltung':<26}{'Protokoll alt':>15}{'neu':>7}{'Gruppen alt':>13}{'neu':>7}")
    summe_alt = summe_neu = summe_pruefbar = 0
    for haltung in sorted(alt_gruppen):
        a_gef, a_pruef = alt_treffer.get(haltung, (0, 0))
        n_gef, n_pruef = neu_treffer.get(haltung, (0, 0))
        summe_alt += a_gef
        summe_neu += n_gef
        summe_pruefbar += a_pruef
        print(
            f"{haltung:<26}{f'{a_gef}/{a_pruef}':>15}{f'{n_gef}/{n_pruef}':>7}"
            f"{len(alt_gruppen[haltung]):>13}{len(neu_gruppen.get(haltung, [])):>7}"
        )
    print(f"\n{'SUMME':<26}{f'{summe_alt}/{summe_pruefbar}':>15}"
          f"{f'{summe_neu}/{summe_pruefbar}':>7}")

    zaehlung = {"protokoll_bogen": 0, "bogen": 0, "kein_bogen": 0, "unsicher": 0, "unbeurteilt": 0}
    unbeurteilt: list[dict] = []
    for haltung, gruppen in neu_gruppen.items():
        for gruppe in gruppen:
            urteil = urteil_uebertragen(haltung, gruppe, urteile)
            zaehlung[urteil] = zaehlung.get(urteil, 0) + 1
            if urteil == "unbeurteilt":
                unbeurteilt.append(
                    {
                        "haltung": haltung,
                        "start_s": int(gruppe["start"]),
                        "ende_s": int(gruppe["ende"]),
                        "max_conf": float(gruppe.get("max_conf") or 0),
                    }
                )

    print("\n=== Gruppen der neuen Messung, gegen die vorhandenen Urteile ===")
    for schluessel, wert in zaehlung.items():
        print(f"  {schluessel:<18}{wert:>4}")
    richtig = zaehlung["protokoll_bogen"] + zaehlung["bogen"]
    falsch = zaehlung["kein_bogen"]
    if richtig + falsch:
        print(f"\n  Treffgenauigkeit auf beurteiltem Bestand: "
              f"{100 * richtig / (richtig + falsch):.0f} % ({richtig} richtig, {falsch} falsch)")
    if unbeurteilt:
        print(f"\n  ACHTUNG: {len(unbeurteilt)} Gruppen haben kein Urteil und sind ungemessen.")
        for eintrag in sorted(unbeurteilt, key=lambda e: -e["max_conf"])[:8]:
            print(f"    {eintrag['haltung']:<26}{eintrag['start_s']:>5}-{eintrag['ende_s']:<5}"
                  f"conf {eintrag['max_conf']:.2f}")

    if args.ziel is not None:
        ausgabe = {
            "schema_version": 1,
            "zweck": "Vergleich zweier BCC-Videomessungen mit uebertragenen Urteilen",
            "alt": str(args.alt),
            "neu": str(args.neu),
            "protokoll_alt": f"{summe_alt}/{summe_pruefbar}",
            "protokoll_neu": f"{summe_neu}/{summe_pruefbar}",
            "zaehlung": zaehlung,
            "unbeurteilt": unbeurteilt,
        }
        temp = args.ziel.with_suffix(".json.tmp")
        temp.write_text(json.dumps(ausgabe, indent=2, ensure_ascii=False), encoding="utf-8")
        temp.replace(args.ziel)
        print(f"\nBericht: {args.ziel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
