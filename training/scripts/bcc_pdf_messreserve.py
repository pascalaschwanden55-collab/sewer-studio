"""Reserviert einen neuen, noch unberuehrten SD-Messbestand fuer BCC.

Die Auswahl liest nur vorhandene Inventare. Aktuelle Mess-, Trainings- und
Eval-Haltungen werden in beiden Fahrtrichtungen ausgeschlossen. Der neue Bestand
ist weder Trainingsmaterial noch Kalibrierbestand und startet als not_evaluated.
Ein vorhandenes Ziel wird nie ueberschrieben.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Sequence

SICHTUNG = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_auswahl\sichtung.json")
MESSBESTAND = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_auswahl\messbestand_v1.json")
AUSSCHLUSS = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_auswahl\gesperrte_haltungen.json")
ZIEL = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_auswahl\messreserve_sd_v2.json")

GUELTIGE_CODES = {"BCCAA", "BCCAB", "BCCAY", "BCCBA", "BCCBB", "BCCBY", "BCCYA", "BCCYB"}


def sha256_datei(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


def varianten(haltung: str) -> set[str]:
    normal = haltung.strip().lower()
    teile = normal.split("-", 1)
    return {normal, f"{teile[1]}-{teile[0]}"} if len(teile) == 2 else {normal}


def physisch(haltung: str) -> str:
    return min(varianten(haltung))


def kandidaten_laden(sichtung: dict, messbestand: dict,
                      ausschluss: dict) -> list[dict]:
    verwendet = {
        physisch(eintrag["haltung"])
        for gruppe in ("sd", "hd")
        for eintrag in messbestand[gruppe]["eintraege"]
    }
    gesperrt = {physisch(str(haltung)) for haltung in ausschluss.get("gesperrt") or []}
    kandidaten = []
    gesehen: set[str] = set()
    for eintrag in sichtung.get("eintraege") or []:
        schluessel = physisch(str(eintrag.get("haltung") or ""))
        codes = set(eintrag.get("codes") or [])
        if str(eintrag.get("art") or "").lower() != "sd":
            continue
        if schluessel in verwendet or schluessel in gesperrt or schluessel in gesehen:
            continue
        if not codes or not codes <= GUELTIGE_CODES:
            continue
        gesehen.add(schluessel)
        kandidaten.append({
            "haltung": eintrag["haltung"],
            "physische_haltung": schluessel,
            "video": eintrag["video"],
            "boegen": int(eintrag["befunde"]),
            "codes": sorted(codes),
            "breite": int(eintrag["breite"]),
            "hoehe": int(eintrag["hoehe"]),
            "dauer_s": float(eintrag["dauer_s"]),
        })
    return kandidaten


def auswaehlen(kandidaten: list[dict], anzahl: int, saat: str) -> list[dict]:
    if anzahl <= 0:
        raise ValueError("Die Anzahl muss groesser als null sein.")
    if len(kandidaten) < anzahl:
        raise ValueError(f"Nur {len(kandidaten)} geeignete Haltungen fuer {anzahl} Plaetze.")
    sortiert = sorted(
        kandidaten,
        key=lambda e: hashlib.sha256(
            f"{saat}|{e['physische_haltung']}".encode("utf-8")).hexdigest(),
    )
    return sortiert[:anzahl]


def atomar_schreiben(ziel: Path, daten: dict) -> None:
    if ziel.exists():
        raise FileExistsError(f"Ziel existiert bereits: {ziel}")
    ziel.parent.mkdir(parents=True, exist_ok=True)
    temp = ziel.with_suffix(ziel.suffix + ".tmp")
    temp.write_text(json.dumps(daten, indent=2, ensure_ascii=False), encoding="utf-8")
    temp.replace(ziel)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sichtung", type=Path, default=SICHTUNG)
    parser.add_argument("--messbestand", type=Path, default=MESSBESTAND)
    parser.add_argument("--ausschluss", type=Path, default=AUSSCHLUSS)
    parser.add_argument("--ziel", type=Path, default=ZIEL)
    parser.add_argument("--anzahl", type=int, default=50)
    parser.add_argument("--saat", default="bcc-pdf-messreserve-sd-v2")
    args = parser.parse_args(argv)

    for pfad in (args.sichtung, args.messbestand, args.ausschluss):
        if not pfad.is_file():
            raise SystemExit(f"Eingabe fehlt: {pfad}")
    try:
        kandidaten = kandidaten_laden(
            json.loads(args.sichtung.read_text(encoding="utf-8-sig")),
            json.loads(args.messbestand.read_text(encoding="utf-8-sig")),
            json.loads(args.ausschluss.read_text(encoding="utf-8-sig")),
        )
        auswahl = auswaehlen(kandidaten, args.anzahl, args.saat)
        beleg = {
            "schema": "bcc_pdf_messreserve_v2",
            "status": "reserved_not_evaluated",
            "gruppe": "sd",
            "hinweis_hd": "Keine unberuehrte HD-Reserve vorhanden; HD ist nicht enthalten.",
            "verboten_fuer": ["training", "kalibrierung", "kandidatenauswahl"],
            "erlaubt_fuer": "einmalige unabhaengige Messung eines kuenftigen BCC-Kandidaten",
            "saat": args.saat,
            "quellen": {
                "sichtung": {"pfad": str(args.sichtung), "sha256": sha256_datei(args.sichtung)},
                "alter_messbestand": {"pfad": str(args.messbestand), "sha256": sha256_datei(args.messbestand)},
                "ausschluss": {"pfad": str(args.ausschluss), "sha256": sha256_datei(args.ausschluss)},
            },
            "freie_sd_haltungen_vor_auswahl": len(kandidaten),
            "haltungen": len(auswahl),
            "boegen": sum(eintrag["boegen"] for eintrag in auswahl),
            "eintraege": auswahl,
        }
        atomar_schreiben(args.ziel, beleg)
    except (ValueError, FileExistsError) as fehler:
        raise SystemExit(str(fehler)) from fehler

    print(f"Reserviert: {len(auswahl)} SD-Haltungen mit {beleg['boegen']} Boegen")
    print(f"Status: {beleg['status']}")
    print(f"Beleg: {args.ziel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
