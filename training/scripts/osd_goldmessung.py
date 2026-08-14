"""Misst den OSD-Meterleser gegen die eingefrorenen Goldsaetze.

WOZU
Der Leser wird geaendert. Ohne festgeschriebenen Ausgangsstand ist hinterher nicht
belegbar, ob eine Aenderung geholfen hat — und schon gar nicht, ob sie anderswo
etwas kaputt gemacht hat. Diese Messung laeuft vor und nach jeder Aenderung
identisch.

WAS GEMESSEN WIRD
Drei Kategorien, streng getrennt:

  richtig       geliefert und stimmt mit dem menschlich abgelesenen Wert ueberein
  falsch        geliefert, weicht aber ab  ← der gefaehrliche Fall
  nicht_gelesen kein Wert geliefert

Ein falscher Wert ist schlimmer als kein Wert: Er wandert unbemerkt ins Protokoll.
Deshalb wird er getrennt ausgewiesen und nie mit "nicht gelesen" vermischt.

WAS DIESE ZAHLEN SIND UND WAS NICHT
Die Goldsaetze sind klein (95 + 30 + 72 Bilder) und stammen aus wenigen Haltungen.
Sie zeigen, ob ein Stil grundsaetzlich lesbar ist — sie sagen NICHTS ueber die
Abdeckung auf dem ganzen Archiv. Dafuer gibt es osd_archiv_abdeckung_messung.py.

Rein lesend: Weder Goldsaetze noch Leser noch Kundendaten werden veraendert.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from collections import Counter
from pathlib import Path
from typing import Sequence

GOLD_WURZEL = Path(r"C:\KI_BRAIN\eval_set\osd")
BERICHT_ORDNER = Path(r"C:\KI_BRAIN\training\reports")
SAETZE = ("osd_sd_v1", "osd_hd_v1", "osd_hd2_v1")

# Zwei Nachkommastellen: Die Anzeige fuehrt hoechstens eine, der Vergleich ist
# damit exakt und nicht von Gleitkomma-Resten abhaengig.
GENAUIGKEIT = 2


def sha256(pfad: Path) -> str:
    h = hashlib.sha256()
    with pfad.open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def messe_satz(satz: Path, lese) -> dict:
    """Ein Goldsatz. Bricht ab, wenn Bildbytes vom Manifest abweichen."""
    manifest_pfad = satz / "manifest.json"
    manifest = json.loads(manifest_pfad.read_text(encoding="utf-8-sig"))
    eintraege = manifest.get("eintraege") or []

    faelle = []
    zaehler: Counter[str] = Counter()
    for eintrag in eintraege:
        bild = satz / "frames" / eintrag["datei"]
        if not bild.is_file():
            raise SystemExit(f"Goldbild fehlt: {bild}")

        # Der Satz ist eingefroren. Abweichende Bytes bedeuten, dass die Messung
        # nicht mehr mit frueheren Laeufen vergleichbar ist.
        ist_hash = sha256(bild)
        if ist_hash != eintrag["bild_sha256"]:
            raise SystemExit(
                f"Bildbytes weichen vom eingefrorenen Manifest ab: {bild}\n"
                f"  Manifest: {eintrag['bild_sha256']}\n  Datei:    {ist_hash}")

        soll = eintrag.get("meter")
        ergebnis = lese(bild)
        ist = ergebnis.get("meter")

        if ist is None:
            zustand = "nicht_gelesen"
        elif soll is None:
            zustand = "ohne_sollwert"
        elif round(float(ist), GENAUIGKEIT) == round(float(soll), GENAUIGKEIT):
            zustand = "richtig"
        else:
            zustand = "falsch"

        zaehler[zustand] += 1
        faelle.append({
            "datei": eintrag["datei"],
            "haltung": eintrag.get("haltung"),
            "soll": soll,
            "ist": ist,
            "zustand": zustand,
            "zeichenfolge": ergebnis.get("zeichenfolge"),
            "stil": ergebnis.get("stil"),
            "leseweg": ergebnis.get("leseweg"),
        })

    gesamt = len(faelle)
    return {
        "satz": satz.name,
        "manifest_sha256": sha256(manifest_pfad),
        "bilder": gesamt,
        "richtig": zaehler["richtig"],
        "falsch": zaehler["falsch"],
        "nicht_gelesen": zaehler["nicht_gelesen"],
        "ohne_sollwert": zaehler["ohne_sollwert"],
        "trefferquote": round(zaehler["richtig"] / gesamt, 4) if gesamt else 0.0,
        "faelle": faelle,
    }


def baue_leser():
    """Bindet den produktiven Leser samt seinem Datei-Hash."""
    sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "sidecar"))
    from PIL import Image
    from sidecar import osd_meter

    templates = osd_meter.get_templates()

    def lese(bild_pfad: Path) -> dict:
        with Image.open(bild_pfad) as bild:
            return osd_meter.lese_meter(bild, templates)

    return lese, sha256(Path(osd_meter.__file__))


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gold-wurzel", type=Path, default=GOLD_WURZEL)
    parser.add_argument("--bericht-ordner", type=Path, default=BERICHT_ORDNER)
    parser.add_argument("--marke", default="", help="Kurzname des Stands, z.B. 'vorher'.")
    parser.add_argument("--kein-bericht", action="store_true",
                        help="Nur anzeigen, nichts schreiben.")
    args = parser.parse_args(argv)

    lese, leser_hash = baue_leser()

    saetze = []
    for name in SAETZE:
        satz = args.gold_wurzel / name
        if not satz.is_dir():
            raise SystemExit(f"Goldsatz fehlt: {satz}")
        saetze.append(messe_satz(satz, lese))

    gesamt = {
        "bilder": sum(s["bilder"] for s in saetze),
        "richtig": sum(s["richtig"] for s in saetze),
        "falsch": sum(s["falsch"] for s in saetze),
        "nicht_gelesen": sum(s["nicht_gelesen"] for s in saetze),
    }
    gesamt["trefferquote"] = (
        round(gesamt["richtig"] / gesamt["bilder"], 4) if gesamt["bilder"] else 0.0)

    print(f"Leser: osd_meter.py  SHA-256 {leser_hash}")
    print(f"{'Satz':<14}{'Bilder':>8}{'richtig':>9}{'falsch':>8}{'nicht ges.':>12}{'Quote':>9}")
    for s in saetze:
        print(f"{s['satz']:<14}{s['bilder']:>8}{s['richtig']:>9}{s['falsch']:>8}"
              f"{s['nicht_gelesen']:>12}{s['trefferquote']:>8.1%}")
    print(f"{'GESAMT':<14}{gesamt['bilder']:>8}{gesamt['richtig']:>9}{gesamt['falsch']:>8}"
          f"{gesamt['nicht_gelesen']:>12}{gesamt['trefferquote']:>8.1%}")

    if args.kein_bericht:
        return 0

    bericht = {
        "schema": "osd_goldmessung_v1",
        "zweck": "Ausgangs- und Vergleichsstand des OSD-Meterlesers auf den eingefrorenen "
                 "Goldsaetzen. Klein und stilbezogen — keine Aussage zur Archivabdeckung.",
        "marke": args.marke,
        "leser_datei": "sidecar/sidecar/osd_meter.py",
        "leser_sha256": leser_hash,
        "gesamt": gesamt,
        "saetze": saetze,
    }
    args.bericht_ordner.mkdir(parents=True, exist_ok=True)
    name = f"osd_goldmessung_{args.marke or 'lauf'}_{leser_hash[:12]}.json"
    ziel = args.bericht_ordner / name
    if ziel.exists():
        print(f"\nBericht besteht bereits und wird nicht ueberschrieben: {ziel}")
        return 0

    text = json.dumps(bericht, indent=1, ensure_ascii=False)
    arbeit = ziel.with_suffix(".json.arbeit")
    arbeit.write_bytes(text.encode("utf-8"))
    arbeit.replace(ziel)
    print(f"\nBericht: {ziel}")
    print(f"Bericht-SHA-256: {hashlib.sha256(text.encode('utf-8')).hexdigest()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
