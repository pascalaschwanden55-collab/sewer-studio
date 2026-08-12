"""Wertet die Recall-Messung aus — Schwelle suchen und Schwelle pruefen getrennt.

Zwei Betriebsarten, und die Reihenfolge ist verbindlich:

  --kalibrieren   Sucht den Arbeitspunkt fuer Archivmaterial. Verwendet
                  AUSSCHLIESSLICH die Kalibrierhaelfte des Messbestands.
  --messen        Wertet die eingefrorene Messhaelfte mit einer VORGEGEBENEN
                  Schwelle aus. Die Schwelle muss von aussen kommen; das
                  Werkzeug sucht sie hier nicht.

Der Grund fuer die Trennung: Wer die Schwelle auf demselben Bestand sucht, auf
dem er danach misst, misst seine eigene Auswahl. Die Messhaelfte ist nach der
ersten Auswertung verbraucht — eine zweite Schwelle darf nicht daran geprueft
werden.

Gemessen wird nur Recall. Das PDF nennt die protokollierten Boegen, nicht alle
vorhandenen; ein Vorschlag ohne PDF-Eintrag kann ein echter Bogen sein.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Sequence

sys.path.insert(0, str(Path(__file__).resolve().parent))

from bcc_copilot_durchlauf import zusammenfassen  # noqa: E402
from bcc_pdf_recall_messung import ZEIT_TOLERANZ, zuordnen  # noqa: E402

MESSBESTAND = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_auswahl\messbestand_v1.json")
LAUF = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_recall_20260809")


def haelften_laden(messbestand: Path = MESSBESTAND) -> dict[str, tuple[str, str]]:
    bestand = json.loads(messbestand.read_text(encoding="utf-8-sig"))
    return {
        e["haltung"]: (e["haelfte"], gruppe)
        for gruppe in ("sd", "hd")
        for e in bestand[gruppe]["eintraege"]
    }


def bewerten(ergebnisse: list[dict], schwelle: float, stark: float,
             toleranz: float = ZEIT_TOLERANZ) -> dict:
    """Fasst die gespeicherten Einzelbilder bei einer Schwelle neu zusammen."""
    soll = getroffen = vorschlaege = starke = 0
    je_haltung = []
    for e in ergebnisse:
        stellen = zusammenfassen(
            [dict(t) for t in e["einzelbilder"]], schwelle, stark)
        solls = [{"code": s["code"], "wert": s["zeit_s"]} for s in e["soll"]]
        wertbar = [s for s in solls if s["wert"] is not None]
        treffer, _ = zuordnen(solls, stellen, "zeit", toleranz)

        soll += len(wertbar)
        getroffen += len(treffer)
        vorschlaege += len(stellen)
        starke += sum(1 for s in stellen if s["stufe"] == "stark")
        je_haltung.append({"haltung": e["haltung"], "gruppe": e["gruppe"],
                           "soll": len(wertbar), "getroffen": len(treffer),
                           "vorschlaege": len(stellen)})

    return {"schwelle": round(schwelle, 3), "stark_ab": round(stark, 3),
            "toleranz_s": toleranz, "haltungen": len(ergebnisse),
            "soll_boegen": soll, "getroffen": getroffen,
            "recall": round(getroffen / soll, 4) if soll else None,
            "vorschlaege": vorschlaege, "davon_stark": starke,
            "vorschlaege_je_haltung": round(vorschlaege / max(1, len(ergebnisse)), 2),
            "je_haltung": je_haltung}


def laden(haelfte: str | None, gruppe: str | None,
          messbestand: Path = MESSBESTAND,
          lauf: Path = LAUF) -> tuple[list[dict], list[dict]]:
    zuteilung = haelften_laden(messbestand)
    ergebnisse = []
    nicht_ausgewertet = []
    for pfad in sorted((lauf / "haltungen").glob("*.json")):
        e = json.loads(pfad.read_text(encoding="utf-8"))
        zugeteilt = zuteilung.get(e.get("haltung"))
        if zugeteilt is None:
            continue
        zugeteilte_haelfte, zugeteilte_gruppe = zugeteilt
        if haelfte and zugeteilte_haelfte != haelfte:
            continue
        if gruppe and zugeteilte_gruppe != gruppe:
            continue
        if e.get("zustand") != "ausgewertet":
            nicht_ausgewertet.append(e)
            continue
        ergebnisse.append(e)
    return ergebnisse, nicht_ausgewertet


def ergebnis_dateiname(schwelle: float, gruppe: str | None) -> str:
    bereich = gruppe or "gesamt"
    return f"messung_conf{int(round(schwelle * 100)):03d}_{bereich}.json"


def atomar_schreiben(ziel: Path, daten: dict) -> None:
    ziel.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(daten, indent=1, ensure_ascii=False)
    temp = ziel.with_suffix(ziel.suffix + ".tmp")
    temp.write_text(text, encoding="utf-8")
    temp.replace(ziel)


def vergleichsbeleg(ergebnis: dict, gruppe: str | None,
                     messbestand: Path = MESSBESTAND) -> dict:
    bestand_bytes = messbestand.read_bytes()
    return {
        "schema": "bcc_pdf_vergleichsbestand_v1",
        "verwendung": "bekannter Vergleichsbestand, keine unabhaengige Modellfreigabe",
        "gruppe": gruppe or "gesamt",
        "messbestand": str(messbestand),
        "messbestand_sha256": hashlib.sha256(bestand_bytes).hexdigest(),
        "schwelle": ergebnis["schwelle"],
        "stark_ab": ergebnis["stark_ab"],
        "toleranz_s": ergebnis["toleranz_s"],
        "haltungen_ausgewertet": ergebnis["haltungen"],
        "soll_boegen": ergebnis["soll_boegen"],
        "getroffen": ergebnis["getroffen"],
        "recall": ergebnis["recall"],
        "vorschlaege": ergebnis["vorschlaege"],
    }


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    modus = parser.add_mutually_exclusive_group(required=True)
    modus.add_argument("--kalibrieren", action="store_true")
    modus.add_argument("--messen", type=float, metavar="SCHWELLE",
                       help="Arbeitspunkt, der auf der Kalibrierhaelfte gefunden wurde")
    parser.add_argument("--stark-ab", type=float, default=None)
    parser.add_argument("--gruppe", choices=("sd", "hd"), default=None)
    parser.add_argument("--messbestand", type=Path, default=MESSBESTAND)
    parser.add_argument("--lauf", type=Path, default=LAUF)
    args = parser.parse_args(argv)

    haelfte = "kalibrierung" if args.kalibrieren else "messung"
    ergebnisse, offen = laden(haelfte, args.gruppe, args.messbestand, args.lauf)
    print(f"Haelfte      {haelfte}")
    print(f"Gruppe       {args.gruppe or 'SD + HD'}")
    print(f"Haltungen    {len(ergebnisse)} ausgewertet, {len(offen)} nicht auswertbar")
    if offen:
        for e in offen[:8]:
            print(f"   nicht ausgewertet: {e['haltung']:<26} {e.get('grund','')}")
    if not ergebnisse:
        print("Keine auswertbaren Haltungen.")
        return 1
    print()

    if args.kalibrieren:
        print("Schwellenkurve auf der KALIBRIERHAELFTE — das ist keine Messung.")
        print(f"{'conf':>6}{'Recall':>9}{'getroffen':>11}{'Vorschlaege':>13}{'je Haltung':>12}")
        for schritt in range(5, 61, 5):
            s = schritt / 100
            w = bewerten(ergebnisse, s, max(s, args.stark_ab or s + 0.3))
            print(f"{s:>6.2f}{(w['recall'] or 0):>8.0%}"
                  f"{w['getroffen']:>7}/{w['soll_boegen']:<4}"
                  f"{w['vorschlaege']:>13}{w['vorschlaege_je_haltung']:>12.1f}")
        print("\nDie Schwelle waehlst du hier. Danach EINMAL mit --messen pruefen.")
        return 0

    stark = args.stark_ab if args.stark_ab is not None else min(1.0, args.messen + 0.3)
    w = bewerten(ergebnisse, args.messen, stark)
    print(f"MESSUNG auf der eingefrorenen Haelfte, conf >= {args.messen:.2f}, "
          f"stark ab {stark:.2f}, Toleranz +/- {ZEIT_TOLERANZ:.0f} s\n")
    print(f"  Soll-Boegen        {w['soll_boegen']}")
    print(f"  getroffen          {w['getroffen']}")
    print(f"  Recall             {(w['recall'] or 0):.1%}")
    print(f"  Vorschlaege        {w['vorschlaege']} ({w['vorschlaege_je_haltung']:.1f} je Haltung)")
    print(f"  davon stark        {w['davon_stark']}")
    print("\n  Precision wird bewusst nicht ausgegeben: Das PDF nennt nur die")
    print("  protokollierten Boegen, nicht alle vorhandenen.")

    ziel = args.lauf / ergebnis_dateiname(args.messen, args.gruppe)
    atomar_schreiben(ziel, w)
    beleg = args.lauf / f"vergleichsbestand_conf{int(round(args.messen*100)):03d}_{args.gruppe or 'gesamt'}.json"
    atomar_schreiben(beleg, vergleichsbeleg(w, args.gruppe, args.messbestand))
    print(f"\nBericht: {ziel}")
    print(f"Vergleichsbeleg: {beleg}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
