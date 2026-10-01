"""Zieht einen vierten, stilgemischten Goldsatz aus dem Archiv und friert ihn ein.

WOZU
Die drei bestehenden Goldsaetze messen fast nur den beschrifteten Stil
("LZ1: 3.2m"). Gemessen am 2026-08-17: 64 der 97 handbeschrifteten
Archivanzeigen bestehen NUR aus Zahlen ohne Beschriftung - beim Lehrer waren es
3 von 932. Ein Modell, das die uebrigen zwei Drittel des Bestands lernt, sieht
auf dieser Messlatte deshalb schlechter aus, obwohl es auf dem Archiv besser
liest: Kandidat v2 verdoppelte die Lesequote auf ungesehenen Haltungen (20 von
88 statt 10) und fiel auf Gold von 120 auf 104 richtig.

Der eigentliche Befund war damit nicht "zu wenige Daten", sondern eine
Messlatte, die den Bestand nicht vertritt. Dieses Werkzeug baut die fehlende
vierte Messlatte.

WIE DIE AUSWAHL ENTSTEHT
Gleichmaessige Ziehung ueber die freien physischen Haltungen - NICHT nach
Lesbarkeit. Kein Werkzeug dieser Kette darf bei der Ziehung eine Lesung
ansehen; genau so entstand die Verzerrung, die hier gemessen werden soll. Die
Lehrer-Ernte konnte nur ihre eigenen Stile weitergeben, und die Handliste hat
ueber ihre Segmentierung wiederum eine eigene Auswahl getroffen.

DREIFACHE SPERRE
Gold, Reservebestand UND Trainingsmaterial (Lehrer-Ernte und Handliste). Nur
so ist der Satz fuer BEIDE Leser fair: Der Vorlagenleser hat ohnehin kein
Training, das Modell darf sein eigenes Material nicht wiedersehen.

Die Trainingssperre steht bewusst NICHT in osd_schutz.lade_schutz(). Diesen
Schutz laden auch die Trainingsskripte (osd_ernte, osd_frames_ziehen,
osd_datensatz) - dort ist Trainingsmaterial per Definition erlaubt, und eine
gemeinsame Sperre wuerde jede Wiederholung einer Ernte blockieren. Die Sperre
gehoert allein zur Ziehung einer Messlatte.

WEITERE REGELN
- Eine physische Haltung liefert genau ein Bild; Gegenrichtungen gelten als
  dieselbe Haltung.
- Auch Bilder OHNE sichtbare Anzeige bleiben drin ('?' beim Ablesen). Sie
  messen den teuersten Fehler: eine erfundene Zahl, wo keine steht. Die drei
  alten Saetze enthalten ausschliesslich lesbare Anzeigen und koennen das
  deshalb gar nicht messen.
- Kundenoriginale werden nur GELESEN.
- Station und Goldsatz werden atomar veroeffentlicht und nie ueberschrieben.

ABLAUF
    queue       -> Ableseplatz bauen (Bilder ziehen, wahrheit.txt anlegen)
    <ablesen>   -> tools/EvalVisibilityReview/osd_wahrheit_server.py
    einfrieren  -> manifest.json in Goldform, atomar veroeffentlicht
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import random
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable, Sequence

WURZEL = Path(__file__).resolve().parents[2]
for pfad in (WURZEL / "training" / "scripts",):
    if str(pfad) not in sys.path:
        sys.path.insert(0, str(pfad))

from osd_ernte import haltung_aus_ordnername
from osd_schutz import Schutz, lade_schutz
from osd_wahrheit_aus_protokoll import physische_haltung

QUELLE_STANDARD = Path(r"D:\OSD_Frames")
GOLD_WURZEL = Path(r"C:\KI_BRAIN\eval_set\osd")
STATION_STANDARD = Path(r"C:\KI_BRAIN\eval_set\osd\_station_osd_mix_v1")
BILD_ENDUNGEN = (".jpg", ".jpeg", ".png")

# Belege der bisherigen Trainingslaeufe. Fehlt einer, bricht die Ziehung ab -
# ein uebersprungener Beleg bedeutet Goldbilder, die das Modell schon kennt.
TRAININGSQUELLEN_STANDARD = (
    Path(r"C:\KI_BRAIN\training\osd_zeichen\ernte_v1\eintraege.json"),
    Path(r"C:\KI_BRAIN\training\osd_zeichen\hand_v1\eintraege.json"),
)

ZWECK = ("Vierte Messlatte fuer den OSD-Meterleser: gleichmaessig ueber freie "
         "Archivhaltungen gezogen, nicht nach Lesbarkeit ausgewaehlt")
REGEL = ("Enthaelt ALLE gezogenen Bilder, auch solche ohne sichtbare Anzeige "
         "(meter=null, menschlich_lesbar=false) - eine Lesung darauf ist ein "
         "erfundener Wert und zaehlt als falsch. Erweiterungen nur monoton als "
         "naechste Version; bestehende Eintraege bleiben unveraendert.")


def sha256(pfad: Path) -> str:
    h = hashlib.sha256()
    with pfad.open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def jetzt() -> str:
    return datetime.now(timezone.utc).isoformat()


# ---------------------------------------------------------------------------
# Sperren
# ---------------------------------------------------------------------------

def trainings_haltungen(quellen: Sequence[Path]) -> set[str]:
    """Physische Haltungen, die in ein Training eingegangen sind.

    Fail-closed: Eine fehlende oder leere Quelle bricht ab. Sie stillschweigend
    zu uebergehen hiesse, Bilder in die Messlatte zu lassen, die das Modell
    schon gesehen hat - der Fehler, den man hinterher nicht mehr erkennt.
    """
    haltungen: set[str] = set()
    for quelle in quellen:
        if not quelle.is_file():
            raise SystemExit(
                f"ABBRUCH: Trainingsbeleg fehlt: {quelle}\n"
                "Ohne ihn kann nicht bewiesen werden, dass die Messlatte "
                "unbekanntes Material enthaelt.")
        daten = json.loads(quelle.read_text(encoding="utf-8-sig"))
        eintraege = daten if isinstance(daten, list) else (
            daten.get("eintraege") or daten.get("faelle") or [])
        if not eintraege:
            raise SystemExit(f"ABBRUCH: Trainingsbeleg ohne Eintraege: {quelle}")
        for eintrag in eintraege:
            haltung = eintrag.get("haltung")
            if haltung:
                haltungen.add(physische_haltung(str(haltung)))
    return haltungen


def freie_haltungen(quelle: Path, schutz: Schutz,
                    trainings: Iterable[str]) -> dict[str, list[Path]]:
    """Physische Haltung -> Bildpfade, die keine der drei Sperren trifft.

    Gegenrichtungen laufen zusammen. Bildhashes werden zusaetzlich einzeln
    geprueft: Die Goldbilder wurden aus demselben Archiv geschnitten, ein
    bytegleiches Bild kann also unter einem anderen Ordnernamen liegen.
    """
    gesperrt = {physische_haltung(h) for h in trainings}
    frei: dict[str, list[Path]] = {}

    for ordner in sorted(p for p in quelle.iterdir()
                         if p.is_dir() and not p.is_symlink()):
        roh = haltung_aus_ordnername(ordner.name)
        if roh is None:
            continue
        haltung = physische_haltung(roh)
        if haltung in gesperrt or schutz.ist_gesperrt("", haltung):
            continue

        for datei in sorted(ordner.iterdir()):
            if not datei.is_file() or datei.is_symlink():
                continue
            if datei.suffix.lower() not in BILD_ENDUNGEN:
                continue
            if schutz.ist_gesperrt(sha256(datei), haltung):
                continue
            frei.setdefault(haltung, []).append(datei)

    return frei


# ---------------------------------------------------------------------------
# Ziehung
# ---------------------------------------------------------------------------

def ziehe(frei: dict[str, list[Path]], anzahl: int, saat: int) -> list[dict]:
    """Zieht anzahl Haltungen und aus jeder genau ein Bild. Wiederholbar.

    Das Bild wird ebenfalls gezogen und nicht das erste genommen: Anfangsbilder
    zeigen fast immer 0.00, und genau diese Verzerrung hat die erste Handliste
    ruiniert (141 von 200 Anfangsbilder).
    """
    if anzahl <= 0:
        raise SystemExit("ABBRUCH: --anzahl muss groesser als 0 sein.")
    kandidaten = sorted(h for h, bilder in frei.items() if bilder)
    if len(kandidaten) < anzahl:
        raise SystemExit(
            f"ABBRUCH: Nur {len(kandidaten)} freie Haltungen, {anzahl} verlangt.")

    wuerfel = random.Random(saat)
    faelle: list[dict] = []
    for haltung in wuerfel.sample(kandidaten, anzahl):
        bild = wuerfel.choice(sorted(frei[haltung]))
        faelle.append({
            "haltung": haltung,
            "bild_pfad": str(bild),
            "bild_sha256": sha256(bild),
        })
    return faelle


# ---------------------------------------------------------------------------
# Ableseplatz
# ---------------------------------------------------------------------------

_KOPF = ("# Meterstand je Nummer eintragen, z. B. '0042 = 12.5'. "
         "Keine Anzeige sichtbar: '0042 = ?'")


def baue_station(faelle: Sequence[dict], ziel: Path, quelle: Path,
                 saat: int) -> Path:
    """Legt frames/, wahrheit.txt, leser_ergebnisse.json und queue.json an.

    leser_ergebnisse.json traegt bewusst nur Nummer, Haltung und Dateiname.
    Eine sichtbare Maschinenlesung waere eine Vorgabe statt einer Pruefung -
    dieselbe Regel wie im bestehenden Ableseplatz.
    """
    if ziel.exists():
        raise SystemExit(f"ABBRUCH: Station existiert schon: {ziel}")

    arbeit = ziel.with_name(ziel.name + ".arbeit")
    if arbeit.exists():
        shutil.rmtree(arbeit)
    (arbeit / "frames").mkdir(parents=True)

    try:
        zeilen = [_KOPF]
        leser: list[dict] = []
        eintraege: list[dict] = []

        for nr, fall in enumerate(faelle, start=1):
            datei = f"f{nr:04d}.jpg"
            shutil.copyfile(fall["bild_pfad"], arbeit / "frames" / datei)
            zeilen.append(f"{nr:04d} = ")
            leser.append({"nr": nr, "haltung": fall["haltung"], "datei": datei})
            eintraege.append({
                "nr": nr,
                "datei": datei,
                "haltung": fall["haltung"],
                "bild_sha256": fall["bild_sha256"],
                "bild_pfad": fall["bild_pfad"],
            })

        (arbeit / "wahrheit.txt").write_text("\n".join(zeilen) + "\n",
                                             encoding="utf-8")
        (arbeit / "leser_ergebnisse.json").write_text(
            json.dumps(leser, ensure_ascii=False, indent=2), encoding="utf-8")
        (arbeit / "queue.json").write_text(json.dumps({
            "schema": "osd_goldsatz_queue_v1",
            "erzeugt_utc": jetzt(),
            "quelle": str(quelle),
            "saat": saat,
            "bilder": len(eintraege),
            "eintraege": eintraege,
        }, ensure_ascii=False, indent=2), encoding="utf-8")

        os.replace(arbeit, ziel)
    finally:
        if arbeit.exists():
            shutil.rmtree(arbeit, ignore_errors=True)

    return ziel


# ---------------------------------------------------------------------------
# Ablesung einlesen
# ---------------------------------------------------------------------------

def lese_wahrheit(datei: Path) -> dict[int, float | None]:
    """wahrheit.txt -> Nummer auf Meterwert. '?' bedeutet None, nicht 0.

    Eine offene Zeile bricht ab: Ein unabgelesenes Bild als "keine Anzeige" zu
    verbuchen waere ein erfundener Sollwert.
    """
    if not datei.is_file():
        raise SystemExit(f"ABBRUCH: wahrheit.txt fehlt: {datei}")

    werte: dict[int, float | None] = {}
    for zeile in datei.read_text(encoding="utf-8").splitlines():
        zeile = zeile.strip()
        if not zeile or zeile.startswith("#") or "=" not in zeile:
            continue
        links, rechts = zeile.split("=", 1)
        try:
            nr = int(links.strip())
        except ValueError:
            raise SystemExit(f"ABBRUCH: Zeile ohne Nummer: {zeile!r}") from None

        rohwert = rechts.strip()
        if not rohwert:
            raise SystemExit(f"ABBRUCH: Zeile {nr:04d} ist noch offen.")
        if rohwert == "?":
            werte[nr] = None
            continue
        try:
            werte[nr] = float(rohwert.replace(",", "."))
        except ValueError:
            raise SystemExit(
                f"ABBRUCH: Zeile {nr:04d}: {rohwert!r} ist keine Zahl. "
                "Keine Anzeige sichtbar bitte als ? eintragen.") from None

    return werte


# ---------------------------------------------------------------------------
# Einfrieren
# ---------------------------------------------------------------------------

def friere_ein(station: Path, gold_wurzel: Path, name: str, version: int,
               material: str = "Archiv gemischt (Ziehung ueber freie Haltungen)",
               schutz: Schutz | None = None,
               trainings: Iterable[str] | None = None) -> Path:
    """Prueft Ablesung und Bildbytes und veroeffentlicht den Satz atomar.

    Das Einfrieren ist die letzte Gelegenheit, einen Fehler zu bemerken:
    danach ist der Satz eine Messlatte, an der Modelle beurteilt werden.
    """
    queue_pfad = station / "queue.json"
    if not queue_pfad.is_file():
        raise SystemExit(f"ABBRUCH: queue.json fehlt: {queue_pfad}")

    queue = json.loads(queue_pfad.read_text(encoding="utf-8-sig"))
    eintraege = queue.get("eintraege") or []
    if not eintraege:
        raise SystemExit(f"ABBRUCH: queue.json ohne Eintraege: {queue_pfad}")

    werte = lese_wahrheit(station / "wahrheit.txt")
    fehlend = [e["nr"] for e in eintraege if e["nr"] not in werte]
    if fehlend:
        raise SystemExit(
            f"ABBRUCH: {len(fehlend)} von {len(eintraege)} Bildern sind nicht "
            f"abgelesen (erste offene Nummer: {fehlend[0]:04d}).")

    gesperrte_trainings = {physische_haltung(h) for h in (trainings or ())}
    fertig: list[dict] = []
    for eintrag in eintraege:
        bild = station / "frames" / eintrag["datei"]
        if not bild.is_file():
            raise SystemExit(f"ABBRUCH: Bild fehlt: {bild}")

        ist = sha256(bild)
        if ist != eintrag["bild_sha256"]:
            raise SystemExit(
                f"ABBRUCH: Bildbytes weichen von der Ziehung ab: {bild}\n"
                f"  Ziehung: {eintrag['bild_sha256']}\n  Datei:   {ist}")

        haltung = physische_haltung(str(eintrag["haltung"]))
        if haltung in gesperrte_trainings:
            raise SystemExit(
                f"ABBRUCH: Haltung {haltung} ist Trainingsmaterial: {bild}")
        if schutz is not None and schutz.ist_gesperrt(ist, haltung):
            raise SystemExit(
                f"ABBRUCH: Haltung {haltung} ist gesperrt "
                f"({schutz.sperrquelle(ist, haltung)}): {bild}")

        meter = werte[eintrag["nr"]]
        fertig.append({
            "nr": eintrag["nr"],
            "datei": eintrag["datei"],
            "haltung": haltung,
            "bild_sha256": ist,
            "menschlich_lesbar": meter is not None,
            "meter": meter,
        })

    ziel = gold_wurzel / f"{name}_v{version}"
    if ziel.exists():
        raise SystemExit(
            f"ABBRUCH: Goldsatz existiert schon und wird nie ueberschrieben: {ziel}")

    manifest = {
        "schema_version": 1,
        "name": name,
        "version": version,
        "material": material,
        "eingefroren_utc": jetzt(),
        "zweck": ZWECK,
        "regel": REGEL,
        "auswahl": {
            "verfahren": "gleichmaessige Ziehung ueber freie physische Haltungen",
            "quelle": queue.get("quelle"),
            "saat": queue.get("saat"),
            "gezogen_utc": queue.get("erzeugt_utc"),
            "keine_auswahl_nach_lesbarkeit": True,
        },
        "bilder": len(fertig),
        "menschlich_lesbar": sum(1 for e in fertig if e["menschlich_lesbar"]),
        "eintraege": fertig,
    }

    arbeit = ziel.with_name(ziel.name + ".arbeit")
    if arbeit.exists():
        shutil.rmtree(arbeit)
    try:
        (arbeit / "frames").mkdir(parents=True)
        for eintrag in fertig:
            shutil.copyfile(station / "frames" / eintrag["datei"],
                            arbeit / "frames" / eintrag["datei"])
        (arbeit / "manifest.json").write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        os.replace(arbeit, ziel)
    finally:
        if arbeit.exists():
            shutil.rmtree(arbeit, ignore_errors=True)

    return ziel


# ---------------------------------------------------------------------------
# Befehlszeile
# ---------------------------------------------------------------------------

def main(argv: Sequence[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    unter = p.add_subparsers(dest="modus", required=True)

    q = unter.add_parser("queue", help="Ableseplatz bauen")
    q.add_argument("--quelle", type=Path, default=QUELLE_STANDARD)
    q.add_argument("--ziel", type=Path, default=STATION_STANDARD)
    q.add_argument("--anzahl", type=int, default=120)
    q.add_argument("--saat", type=int, default=0)
    q.add_argument("--trainingsquelle", type=Path, action="append")

    f = unter.add_parser("einfrieren", help="Abgelesenen Satz einfrieren")
    f.add_argument("--station", type=Path, default=STATION_STANDARD)
    f.add_argument("--gold-wurzel", type=Path, default=GOLD_WURZEL)
    f.add_argument("--name", default="osd_mix")
    f.add_argument("--version", type=int, default=1)
    f.add_argument("--trainingsquelle", type=Path, action="append")

    args = p.parse_args(argv)
    quellen = tuple(args.trainingsquelle or TRAININGSQUELLEN_STANDARD)

    if args.modus == "queue":
        if args.saat < 0:
            print("ABBRUCH: --saat darf nicht negativ sein.", file=sys.stderr)
            return 2
        if not args.quelle.is_dir():
            print(f"ABBRUCH: Quelle fehlt: {args.quelle}", file=sys.stderr)
            return 2

        schutz = lade_schutz()
        trainings = trainings_haltungen(quellen)
        print(f"Gesperrt: {len(schutz.haltungen)} Haltungen (Gold + Reserve), "
              f"{len(trainings)} aus Training")

        frei = freie_haltungen(args.quelle, schutz, trainings)
        print(f"Frei: {len(frei)} physische Haltungen, "
              f"{sum(len(v) for v in frei.values())} Bilder")

        faelle = ziehe(frei, args.anzahl, args.saat)
        ziel = baue_station(faelle, args.ziel, args.quelle, args.saat)
        print(f"Gezogen: {len(faelle)} Bilder aus {len(faelle)} Haltungen")
        print(f"Ableseplatz: {ziel}")
        print("Starten mit:")
        print(f"  sidecar\\.venv\\Scripts\\python.exe "
              f"tools\\EvalVisibilityReview\\osd_wahrheit_server.py --wurzel \"{ziel}\"")
        return 0

    schutz = lade_schutz()
    trainings = trainings_haltungen(quellen)
    satz = friere_ein(args.station, args.gold_wurzel, args.name, args.version,
                      schutz=schutz, trainings=trainings)
    manifest = json.loads((satz / "manifest.json").read_text(encoding="utf-8"))
    print(f"Eingefroren: {satz}")
    print(f"  Bilder:            {manifest['bilder']}")
    print(f"  mit Anzeige:       {manifest['menschlich_lesbar']}")
    print(f"  ohne Anzeige:      {manifest['bilder'] - manifest['menschlich_lesbar']}")
    print(f"  manifest_sha256:   {sha256(satz / 'manifest.json')}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
