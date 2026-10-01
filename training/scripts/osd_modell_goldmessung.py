"""Misst den trainierten Zeichenleser gegen die drei eingefrorenen Goldsaetze.

Benutzt bewusst messe_satz() aus osd_goldmessung.py: dieselbe Hashpruefung der
Bildbytes, dieselbe Einteilung in richtig / falsch / nicht_gelesen. Nur der Leser
ist ein anderer. Damit sind alter und neuer Stand direkt vergleichbar.

Der Lauf verweigert, solange die Schwelle im Kandidatenmanifest nicht eingefroren
ist - sonst waere die Versuchung gross, sie nach dem Ergebnis nachzuziehen.

RULING zu Aufgabe 8: Der urspruengliche Brief definierte eine eigene
baue_modell_leser(). Das wird hier NICHT getan. osd_modell_leser.baue_modell_leser
(Aufgabe 7) ist der einzige Inferenzpfad - Zuschnitt, Normierung, Stilermittlung,
Zeichenzusammenbau und Deutung. Ein zweiter, hier neu geschriebener Leser wuerde
riskieren, dass Schwellenkalibrierung und Goldmessung leise auseinanderlaufen
(anderes Cropping, andere Normierung, andere parse_meter-Aufrufe), ohne dass das
je auffaellt. Dieses Skript ruft ihn nur auf.

FAIL-CLOSED: Wie in osd_modell_leser dokumentiert, wird um die Inferenz kein
try/except gelegt. Ein technischer Fehler (defektes Bild, CUDA-Fehler, ein
fehlendes Modul, ...) muss den Lauf sichtbar sprengen statt als "nicht_gelesen"
in die Zahlen einzugehen - das wuerde die Messung leise beschoenigen.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

import osd_goldmessung

GOLD_WURZEL = Path(r"C:\KI_BRAIN\eval_set\osd")
BERICHT_ORDNER = Path(r"C:\KI_BRAIN\training\reports")

# Freigabemarke (Spec Abschnitt 6): null falsch UND mindestens 170 von 197
# Goldbildern richtig gelesen. Beide Bedingungen muessen GEMEINSAM gelten -
# viele richtige Werte gleichen keinen einzigen falschen aus (ein falscher
# Wert ist teurer als zehn fehlende).
FREIGABE_MINDEST_RICHTIG = 170


def freigabe_erreicht(gesamt: dict) -> bool:
    """Reine Logik: True nur bei 0 falsch UND >= FREIGABE_MINDEST_RICHTIG richtig."""
    return gesamt["falsch"] == 0 and gesamt["richtig"] >= FREIGABE_MINDEST_RICHTIG


def ist_freigabelauf(saetze: tuple[str, ...]) -> bool:
    """Nur die drei Standardsaetze duerfen ueber die Freigabe entscheiden.

    Die Marke "170 richtig" ist an deren 197 Bilder gebunden. Ein Lauf ueber
    einen anderen Satz - etwa die vierte, stilgemischte Messlatte - misst
    dieselbe Groesse an einem anderen Bestand; die Marke daraufhin anzuwenden
    waere ein stilles Verschieben des Massstabs in beide Richtungen.
    """
    return tuple(saetze) == tuple(osd_goldmessung.SAETZE)


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--kandidat", type=Path, required=True)
    p.add_argument("--gold-wurzel", type=Path, default=GOLD_WURZEL)
    p.add_argument("--bericht-ordner", type=Path, default=BERICHT_ORDNER)
    p.add_argument("--satz", action="append",
                   help="Goldsatz statt der drei Standardsaetze. Mehrfach "
                        "moeglich. Ein solcher Lauf ist eine Zusatzmessung und "
                        "kann die Freigabemarke nicht erreichen.")
    args = p.parse_args(argv)
    saetze_namen = tuple(args.satz or osd_goldmessung.SAETZE)

    manifest_pfad = args.kandidat / "manifest.json"
    if not manifest_pfad.is_file():
        print(f"ABBRUCH: Kandidatenmanifest fehlt: {manifest_pfad}", file=sys.stderr)
        return 2
    manifest = json.loads(manifest_pfad.read_text(encoding="utf-8-sig"))

    schwelle = manifest.get("schwelle")
    if schwelle is None:
        print("ABBRUCH: Die Schwelle ist nicht eingefroren. Erst "
              "osd_schwelle_kalibrieren.py laufen lassen.", file=sys.stderr)
        return 2

    gewicht = args.kandidat / manifest["gewicht_datei"]
    ist_hash = osd_goldmessung.sha256(gewicht)
    if ist_hash != manifest["gewicht_sha256"]:
        print(f"ABBRUCH: Gewichtshash weicht ab.\n  Manifest: "
              f"{manifest['gewicht_sha256']}\n  Datei:    {ist_hash}", file=sys.stderr)
        return 2

    # Lazy: reine Logiktests dieses Skripts (beide Verweigerungen,
    # freigabe_erreicht) brauchen kein Ultralytics/Torch. Der geteilte Leser
    # kommt UNVERAENDERT aus Aufgabe 7 - hier wird er nur aufgerufen, nie
    # neu geschrieben (siehe Ruling im Modul-Docstring).
    from osd_modell_leser import baue_modell_leser, code_hashes

    lese = baue_modell_leser(args.kandidat, float(schwelle))

    # Kein try/except um diese Zeile oder um messe_satz(): ein technischer
    # Fehler bei der Inferenz muss den Lauf abbrechen, nicht leise als
    # "nicht_gelesen" verschwinden. messe_satz() ruft lese() seinerseits
    # ebenfalls ohne eigenes try/except auf - beide Seiten bleiben fail-closed.
    saetze = [osd_goldmessung.messe_satz(args.gold_wurzel / name, lese)
              for name in saetze_namen]

    gesamt = {
        "bilder": sum(s["bilder"] for s in saetze),
        "richtig": sum(s["richtig"] for s in saetze),
        "falsch": sum(s["falsch"] for s in saetze),
        "nicht_gelesen": sum(s["nicht_gelesen"] for s in saetze),
        # Fix-Runde 1 (Aufgabe 6): messe_satz() liefert fuenf Zustaende, nicht
        # vier - ohne dieses Feld summiert sich die Tabelle nicht immer zu
        # "bilder", ohne dass das irgendwo sichtbar wird.
        "ohne_sollwert": sum(s["ohne_sollwert"] for s in saetze),
        # Teilmenge von 'falsch': erfundene Zahlen auf Bildern ohne Anzeige.
        "erfunden": sum(s.get("erfunden", 0) for s in saetze),
    }

    print(f"Kandidat: {manifest['kandidat_id']}  Schwelle {schwelle}")
    print(f"{'Satz':<14}{'Bilder':>8}{'richtig':>9}{'falsch':>8}{'nicht ges.':>12}{'ohne Soll':>11}")
    for s in saetze:
        print(f"{s['satz']:<14}{s['bilder']:>8}{s['richtig']:>9}"
              f"{s['falsch']:>8}{s['nicht_gelesen']:>12}{s['ohne_sollwert']:>11}")
    print(f"{'GESAMT':<14}{gesamt['bilder']:>8}{gesamt['richtig']:>9}"
          f"{gesamt['falsch']:>8}{gesamt['nicht_gelesen']:>12}{gesamt['ohne_sollwert']:>11}")
    if gesamt["erfunden"]:
        print(f"  davon erfunden (Lesung auf Bild ohne Anzeige): {gesamt['erfunden']}")
    print()

    freigabelauf = ist_freigabelauf(saetze_namen)
    if not freigabelauf:
        erreicht = None
        print("Zusatzmessung auf " + ", ".join(saetze_namen) + ".")
        print("Die Freigabemarke gilt nur fuer die drei Standardsaetze und wird "
              "hier NICHT beurteilt.")
    else:
        print(f"Freigabemarke: null falsch UND mindestens {FREIGABE_MINDEST_RICHTIG} richtig.")
        erreicht = freigabe_erreicht(gesamt)
        if erreicht:
            print("ERREICHT.")
        else:
            print("NICHT erreicht - der Kandidat bleibt diagnostic_not_deployed.")

    bericht = {
        "schema": "osd_modell_goldmessung_v1",
        "kandidat_id": manifest["kandidat_id"],
        "gewicht_sha256": manifest["gewicht_sha256"],
        # Aufgabe 4: der Sibling osd_goldmessung.py bindet leser_sha256 -
        # hier war bisher nur das Gewicht gebunden, nicht der Code, der mit
        # demselben Gewicht die Lesung bestimmt (ZIEL_HOEHE, _IOU_SCHWELLE,
        # TOR_MINDESTZEICHEN, _YOLO_CONF, Zuschnitt).
        "code_sha256": code_hashes(),
        "schwelle": schwelle,
        "gesamt": gesamt,
        # null (nicht false) bei einer Zusatzmessung: Die Marke ist dort nicht
        # anwendbar, und "false" waere als bestandene Pruefung mit schlechtem
        # Ergebnis lesbar statt als ungeprueft.
        "freigabe_erreicht": erreicht,
        "freigabelauf": freigabelauf,
        "gemessene_saetze": list(saetze_namen),
        "saetze": saetze,
    }
    args.bericht_ordner.mkdir(parents=True, exist_ok=True)
    # Der Dateiname trug nur die Kandidaten-ID. Seit es --satz gibt, treffen
    # zwei verschiedene Messungen desselben Kandidaten sonst auf denselben
    # Namen - und weil ein bestehender Bericht nie ueberschrieben wird, ginge
    # die zweite Messung stillschweigend verloren (real passiert, 2026-08-17).
    # Der Freigabelauf behaelt seinen Namen, damit alte Berichte auffindbar
    # bleiben; jede Zusatzmessung bekommt ihren Bestand in den Namen.
    kennung = manifest["kandidat_id"]
    if not freigabelauf:
        kennung += "_" + "_".join(saetze_namen)
    ziel = args.bericht_ordner / f"osd_modell_goldmessung_{kennung}.json"
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
