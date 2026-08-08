"""Rechnet die Schwellenkurve des BCC-Videowegs aus menschlichen Urteilen.

DIAGNOSE. Verbindet die blinde Bogen-/Fehlalarm-Pruefung mit der Videomessung und
gibt precision(conf) gegen recall(conf) aus. Die Urteile stammen aus einem
Prueflauf, der weder Konfidenz noch eine KI-Vorabeinstufung angezeigt hat.

Das Werkzeug schreibt nur seinen eigenen Bericht und veraendert weder Review,
Messung noch Modell.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Sequence

SCHWELLEN = (0.10, 0.15, 0.20, 0.25, 0.30, 0.35, 0.40, 0.50, 0.60, 0.70)
SCHACHT_S = 5


def _schluessel(haltung: str, start: int, ende: int) -> str:
    return f"{haltung}|{start}-{ende}"


def urteile_einlesen(review_pfad: Path) -> dict[str, str]:
    review = json.loads(review_pfad.read_text(encoding="utf-8-sig"))
    return {
        _schluessel(e["haltung"], int(e["start_s"]), int(e["ende_s"])): e["urteil"]
        for e in review["urteile"].values()
    }


def template_fuellen(template: dict, urteile: dict[str, str]) -> tuple[list[dict], list[str]]:
    gefuellt: list[dict] = []
    fehlend: list[str] = []
    for gruppe in template["gruppen"]:
        schluessel = _schluessel(gruppe["haltung"], int(gruppe["start_s"]), int(gruppe["ende_s"]))
        urteil = urteile.get(schluessel)
        if urteil is None:
            fehlend.append(schluessel)
        eintrag = dict(gruppe)
        eintrag["urteil_pascal"] = urteil
        eintrag["ist_schachtanfang"] = (
            int(gruppe["start_s"]) <= SCHACHT_S
            or "schacht" in str(gruppe.get("ki_einstufung", "")).lower()
        )
        gefuellt.append(eintrag)
    return gefuellt, fehlend


def protokoll_treffer(bericht: dict) -> list[float]:
    """Hoechste Konfidenz je protokolliertem, pruefbarem Bogen (0.0 = nie gefunden)."""
    werte: list[float] = []
    for eintrag in bericht.get("ergebnisse") or []:
        pruefbar = int(eintrag.get("befunde_pruefbar") or 0)
        treffer = [
            float(g.get("max_conf") or 0.0)
            for g in (eintrag.get("gruppen") or [])
            if g.get("ist_treffer")
        ]
        treffer.sort(reverse=True)
        werte.extend(treffer[:pruefbar])
        werte.extend([0.0] * max(0, pruefbar - len(treffer)))
    return werte


def kurve(gefuellt: list[dict], treffer_conf: list[float]) -> list[dict]:
    zeilen: list[dict] = []
    for schwelle in SCHWELLEN:
        gefunden = sum(1 for wert in treffer_conf if wert >= schwelle)
        oberhalb = [g for g in gefuellt if float(g["max_conf"]) >= schwelle]
        echte = sum(1 for g in oberhalb if g["urteil_pascal"] == "bogen")
        falsche = sum(
            1
            for g in oberhalb
            if g["urteil_pascal"] == "kein_bogen" and not g["ist_schachtanfang"]
        )
        unsicher = sum(1 for g in oberhalb if g["urteil_pascal"] == "unsicher")
        richtig = gefunden + echte
        zeilen.append(
            {
                "conf": round(schwelle, 2),
                "recall_protokoll": f"{gefunden}/{len(treffer_conf)}",
                "recall": round(gefunden / max(1, len(treffer_conf)), 4),
                "richtig": richtig,
                "davon_nicht_codiert": echte,
                "falsch": falsche,
                "unsicher": unsicher,
                "precision": round(richtig / max(1, richtig + falsche), 4),
            }
        )
    return zeilen


def main(argv: Sequence[str] | None = None) -> int:
    wurzel = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_video_messung_20260807")
    parser = argparse.ArgumentParser(description="Schwellenkurve des BCC-Videowegs")
    parser.add_argument("--template", type=Path, default=wurzel / "urteile_template.json")
    parser.add_argument("--report", type=Path, default=wurzel / "report.json")
    parser.add_argument(
        "--review",
        type=Path,
        default=Path(r"C:\KI_BRAIN\eval_review\bcc_video_fehlalarm_review.json"),
    )
    parser.add_argument("--ziel", type=Path, default=wurzel / "schwellenkurve.json")
    args = parser.parse_args(argv)

    template = json.loads(args.template.read_text(encoding="utf-8-sig"))
    bericht = json.loads(args.report.read_text(encoding="utf-8-sig"))
    urteile = urteile_einlesen(args.review)

    gefuellt, fehlend = template_fuellen(template, urteile)
    if fehlend:
        raise SystemExit(f"{len(fehlend)} Gruppen ohne Urteil, erste: {fehlend[:3]}")

    treffer_conf = protokoll_treffer(bericht)
    zeilen = kurve(gefuellt, treffer_conf)

    # Abgleich der KI-Vorabeinstufung mit der menschlichen Wahrheit.
    ki_bogen = [g for g in gefuellt if str(g.get("ki_einstufung", "")).lower() == "bogen"]
    ki_richtig = sum(1 for g in ki_bogen if g["urteil_pascal"] == "bogen")
    mensch_bogen = sum(1 for g in gefuellt if g["urteil_pascal"] == "bogen")

    print(f"{'conf':>6}{'Recall':>10}{'richtig':>9}{'(neu)':>7}{'falsch':>8}"
          f"{'unsicher':>10}{'Precision':>11}")
    for zeile in zeilen:
        print(
            f"{zeile['conf']:>6.2f}{zeile['recall_protokoll']:>10}{zeile['richtig']:>9}"
            f"{zeile['davon_nicht_codiert']:>7}{zeile['falsch']:>8}{zeile['unsicher']:>10}"
            f"{100 * zeile['precision']:>10.0f} %"
        )

    print(f"\nKI-Vorabeinstufung 'bogen': {len(ki_bogen)} Gruppen, davon menschlich bestaetigt "
          f"{ki_richtig} ({100 * ki_richtig / max(1, len(ki_bogen)):.0f} %).")
    print(f"Menschlich als Bogen bestaetigt insgesamt: {mensch_bogen} von {len(gefuellt)}.")

    ausgabe = {
        "schema_version": 1,
        "zweck": "Arbeitspunkt des BCC-Videowegs aus menschlichen Urteilen",
        "quelle_review": str(args.review),
        "quelle_report": str(args.report),
        "schachtanfaenge_ausgenommen": sum(1 for g in gefuellt if g["ist_schachtanfang"]),
        "protokollierte_boegen": len(treffer_conf),
        "kurve": zeilen,
        "ki_einstufung_abgleich": {
            "ki_bogen": len(ki_bogen),
            "davon_bestaetigt": ki_richtig,
            "mensch_bogen_gesamt": mensch_bogen,
        },
        "gruppen": gefuellt,
    }
    temp = args.ziel.with_suffix(".json.tmp")
    temp.write_text(json.dumps(ausgabe, indent=2, ensure_ascii=False), encoding="utf-8")
    temp.replace(args.ziel)
    print(f"\nBericht: {args.ziel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
