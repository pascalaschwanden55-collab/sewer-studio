"""Fuehrt Ernte und Kunstbilder zu einem YOLO-Datensatz zusammen.

Die Aufteilung geht nach PHYSISCHER Haltung, nie nach Bild: Zwei Bilder aus
derselben Haltung in train und val zugleich waeren eine verdeckte Selbstmessung.
Kuenstliche Bilder haben keine Haltung und bilden eigene Gruppen.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import random
import shutil
import sys
import uuid
from pathlib import Path
from typing import Sequence

WURZEL = Path(__file__).resolve().parents[2]
for pfad in (WURZEL / "sidecar", WURZEL / "training" / "scripts"):
    if str(pfad) not in sys.path:
        sys.path.insert(0, str(pfad))

from sidecar import osd_meter
from osd_schutz import GOLD_WURZEL, Schutz, lade_schutz
from osd_wahrheit_aus_protokoll import physische_haltung

ZIEL_WURZEL = Path(r"C:\KI_BRAIN\training\osd_zeichen")


def baue_gruppen(eintraege: list[dict]) -> dict[str, list[str]]:
    """Bildet die Split-Gruppen. Bytegleiche Bilder kommen nur einmal vor.

    Taucht DASSELBE Bild unter zwei Haltungen auf, werden diese Haltungen zu
    einer gemeinsamen Gruppe verbunden - sonst stuende dieselbe Aufnahme in
    train und val zugleich. Dasselbe Verfahren benutzt gold_stock_audit.py.
    Kuenstliche Bilder haben keine Haltung und bilden je eine eigene Gruppe.
    """
    # 1. Byte-Duplikate wegwerfen, ersten Eintrag behalten.
    gesehen: set[str] = set()
    eindeutig: list[dict] = []
    for eintrag in eintraege:
        hash_wert = str(eintrag.get("bild_sha256") or "").lower()
        if hash_wert and hash_wert in gesehen:
            continue
        if hash_wert:
            gesehen.add(hash_wert)
        eindeutig.append(eintrag)

    # 2. Haltungen verbinden, die sich ein Bild teilen (Union-Find).
    eltern: dict[str, str] = {}

    def finde(knoten: str) -> str:
        eltern.setdefault(knoten, knoten)
        while eltern[knoten] != knoten:
            eltern[knoten] = eltern[eltern[knoten]]
            knoten = eltern[knoten]
        return knoten

    def verbinde(a: str, b: str) -> None:
        wurzel_a, wurzel_b = finde(a), finde(b)
        if wurzel_a != wurzel_b:
            eltern[max(wurzel_a, wurzel_b)] = min(wurzel_a, wurzel_b)

    hash_zu_haltung: dict[str, str] = {}
    for eintrag in eintraege:
        haltung = eintrag.get("haltung")
        if not haltung:
            continue
        schluessel = physische_haltung(str(haltung))
        finde(schluessel)
        hash_wert = str(eintrag.get("bild_sha256") or "").lower()
        if not hash_wert:
            continue
        if hash_wert in hash_zu_haltung:
            verbinde(hash_zu_haltung[hash_wert], schluessel)
        else:
            hash_zu_haltung[hash_wert] = schluessel

    # 3. Eintraege den Gruppen zuordnen.
    gruppen: dict[str, list[str]] = {}
    for lauf, eintrag in enumerate(eindeutig):
        haltung = eintrag.get("haltung")
        if haltung:
            schluessel = finde(physische_haltung(str(haltung)))
        else:
            # Ohne Haltung: eigene Gruppe, damit kuenstliche Bilder keine
            # echten Haltungen an sich binden.
            schluessel = f"kunst_{lauf:06d}"
        gruppen.setdefault(schluessel, []).append(str(eintrag["id"]))
    return gruppen


def teile_auf(gruppen: dict[str, list[str]], val_anteil: float,
              saat: int) -> dict[str, str]:
    """Ordnet jede Gruppe genau einem Teil zu. Gleiche Saat, gleiche Aufteilung."""
    schluessel = sorted(gruppen)
    zufall = random.Random(saat)
    zufall.shuffle(schluessel)

    anzahl_val = int(len(schluessel) * val_anteil)
    # Mindestens eine Gruppe bleibt im Training, sonst ist der Lauf sinnlos.
    anzahl_val = min(anzahl_val, max(0, len(schluessel) - 1))

    zuordnung = {name: "train" for name in schluessel}
    for name in schluessel[:anzahl_val]:
        zuordnung[name] = "val"
    return zuordnung


def schreibe_data_yaml(ziel: Path) -> Path:
    """Die Klassenliste ist die Zeichenkette ZEICHEN, Position = Klassen-ID."""
    namen = "\n".join(f"  {i}: {zeichen!r}"
                      for i, zeichen in enumerate(osd_meter.ZEICHEN))
    text = (
        "# Erzeugt von osd_datensatz.py - nicht von Hand aendern.\n"
        "path: .\n"
        "train: images/train\n"
        "val: images/val\n"
        f"nc: {len(osd_meter.ZEICHEN)}\n"
        "names:\n"
        f"{namen}\n"
    )
    ziel.mkdir(parents=True, exist_ok=True)
    pfad = ziel / "data.yaml"
    pfad.write_text(text, encoding="utf-8")
    return pfad


# ---------------------------------------------------------------------------
# CLI: eine oder mehrere Quellen (osd_ernte/osd_kunstbilder) zu einem
# schreibfertigen YOLO-Datensatz zusammenfuehren.
#
# Der Kern oben (baue_gruppen, teile_auf, schreibe_data_yaml) ist rein
# funktional und dateisystemfrei. Alles unterhalb ist duenne Verdrahtung:
# Quellen einlesen, ein zweites Mal gegen die Sperrliste pruefen, Bild-/
# Labeldateien kopieren, Beleg schreiben. Ergaenzt (Ruling zu Aufgabe 4) -
# der Brief beschreibt nur die drei reinen Bausteine, aber ohne einen
# Schreibweg entsteht kein Datensatz auf der Platte.
# ---------------------------------------------------------------------------

SCHEMA_ERNTE = "osd_ernte_v1"
SCHEMA_KUNSTBILDER = "osd_kunstbilder_v1"
BEKANNTE_SCHEMAS = (SCHEMA_ERNTE, SCHEMA_KUNSTBILDER)


def eintraege_aus_dokument(dokument: dict, quelle_label: str) -> list[dict]:
    """Prueft das Schema und liefert die Eintragsliste.

    Ein unbekanntes Schema bricht laut ab statt eine unbekannte Datenform
    stillschweigend wie osd_ernte_v1 zu behandeln.
    """
    schema = dokument.get("schema")
    if schema not in BEKANNTE_SCHEMAS:
        raise SystemExit(
            f"Unbekanntes Schema in {quelle_label}: {schema!r} "
            f"(erwartet eines von: {', '.join(BEKANNTE_SCHEMAS)})")
    return list(dokument.get("eintraege") or [])


def pruefe_keine_gesperrten(eintraege: list[dict], schutz: Schutz,
                            quelle_label: str) -> None:
    """Zweite Schutzpruefung nach der Ernte - ein Treffer ist ein harter Fehler.

    osd_ernte.py und osd_kunstbilder.py filtern die Sperrliste bereits selbst
    heraus. Taucht hier trotzdem ein gesperrter Eintrag auf, ist etwas
    vorgelagert falsch gelaufen; der Lauf bricht komplett ab statt den
    Eintrag still zu ueberspringen - sonst koennte unbemerkt ein
    kontaminierter Datensatz entstehen ("ein falscher Wert ist teurer als
    zehn fehlende").
    """
    for eintrag in eintraege:
        bild_sha256 = str(eintrag.get("bild_sha256") or "")
        haltung = eintrag.get("haltung")
        if schutz.ist_gesperrt(bild_sha256, haltung):
            raise SystemExit(
                f"Geschuetzter Eintrag in {quelle_label}: id={eintrag.get('id')!r} "
                "- die Ernte haette diesen Eintrag bereits ausschliessen muessen. "
                "Abbruch statt stillem Uebersprung.")


def pruefe_eindeutige_ids(eintraege: list[dict]) -> None:
    """Dieselbe id darf nie zu unterschiedlichen Bildinhalten gehoeren.

    Sonst wuerde main() beim Kopieren unter derselben Zielkennung das
    falsche Bild ablegen (z.B. wenn zwei Kunstbilder-Laeufe mit gleicher
    Saat, aber unterschiedlichem --hintergrund-ordner, in verschiedene
    Quellordner geschrieben wurden).
    """
    id_zu_hash: dict[str, str] = {}
    for eintrag in eintraege:
        id_ = str(eintrag["id"])
        hash_wert = str(eintrag.get("bild_sha256") or "").lower()
        vorher = id_zu_hash.get(id_)
        if vorher is not None and vorher != hash_wert:
            raise SystemExit(
                f"Widerspruechliche id={id_!r}: unterschiedliche Bildinhalte "
                "unter derselben Kennung in den angegebenen Quellen.")
        id_zu_hash[id_] = hash_wert


def baue_beleg(quellen: list[dict], val_anteil: float, saat: int,
              id_zu_split: dict[str, str]) -> dict:
    """Baut den Inhalt von datensatz.json - rein aus bereits bekannten Werten."""
    splits = {"train": 0, "val": 0}
    for teil in id_zu_split.values():
        splits[teil] = splits.get(teil, 0) + 1
    return {
        "schema": "osd_datensatz_v1",
        "quellen": quellen,
        "val_anteil": val_anteil,
        "saat": saat,
        "splits": splits,
        "bilder_gesamt": len(id_zu_split),
        "labels_gesamt": len(id_zu_split),
    }


def _ziel_ist_frei(ziel: Path) -> bool:
    """True, wenn ziel fehlt oder ein leerer Ordner ist - beides sicher nutzbar."""
    if not ziel.exists():
        return True
    return ziel.is_dir() and not any(ziel.iterdir())


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--quelle", type=Path, action="append", required=True,
                        help="Ordner mit eintraege.json aus osd_ernte/osd_kunstbilder "
                             "(mehrfach angebbar)")
    parser.add_argument("--ziel", type=Path, default=ZIEL_WURZEL,
                        help="Zielordner fuer den YOLO-Datensatz")
    parser.add_argument("--val-anteil", type=float, default=0.2,
                        help="Anteil der Gruppen im Validierungsteil (Default 0.2)")
    parser.add_argument("--saat", type=int, default=0,
                        help="Saat fuer die Aufteilung (Default 0)")
    parser.add_argument("--gold-wurzel", type=Path, default=GOLD_WURZEL,
                        help="Wurzel der eingefrorenen Goldsaetze (Sperrliste)")
    args = parser.parse_args(argv)

    if not 0.0 <= args.val_anteil < 1.0:
        raise SystemExit("--val-anteil muss zwischen 0 und 1 liegen (0 <= x < 1).")

    for quelle in args.quelle:
        if not quelle.is_dir():
            raise SystemExit(f"Quellordner fehlt: {quelle}")

    if args.ziel.exists() and not args.ziel.is_dir():
        raise SystemExit(f"Ziel ist kein Ordner: {args.ziel}")
    if not _ziel_ist_frei(args.ziel):
        raise SystemExit(
            f"Ziel existiert bereits und ist nicht leer: {args.ziel} - ein "
            "bestehender Datensatz wird nie repariert oder zusammengefuehrt.")

    schutz = lade_schutz(args.gold_wurzel)

    alle_eintraege: list[dict] = []
    id_zu_quelle: dict[str, Path] = {}
    quellen_belege: list[dict] = []

    for quelle in args.quelle:
        eintraege_pfad = quelle / "eintraege.json"
        if not eintraege_pfad.is_file():
            raise SystemExit(f"eintraege.json fehlt: {eintraege_pfad}")

        rohtext = eintraege_pfad.read_text(encoding="utf-8")
        dokument = json.loads(rohtext)
        eintraege = eintraege_aus_dokument(dokument, str(eintraege_pfad))
        pruefe_keine_gesperrten(eintraege, schutz, str(eintraege_pfad))

        for eintrag in eintraege:
            id_zu_quelle.setdefault(str(eintrag["id"]), quelle)
        alle_eintraege.extend(eintraege)
        quellen_belege.append({
            "pfad": str(quelle),
            "schema": dokument.get("schema"),
            "eintraege_json_sha256": hashlib.sha256(rohtext.encode("utf-8")).hexdigest(),
            "anzahl_eintraege": len(eintraege),
        })

    if not alle_eintraege:
        raise SystemExit("Keine Eintraege in den angegebenen Quellen gefunden.")

    pruefe_eindeutige_ids(alle_eintraege)

    gruppen = baue_gruppen(alle_eintraege)
    zuordnung = teile_auf(gruppen, args.val_anteil, args.saat)

    id_zu_split: dict[str, str] = {}
    for schluessel, ids in gruppen.items():
        teil = zuordnung[schluessel]
        for id_ in ids:
            id_zu_split[id_] = teil

    staging = args.ziel.with_name(f".{args.ziel.name}.staging-{uuid.uuid4().hex}")
    try:
        for teil in ("train", "val"):
            (staging / "images" / teil).mkdir(parents=True, exist_ok=True)
            (staging / "labels" / teil).mkdir(parents=True, exist_ok=True)

        for id_, teil in id_zu_split.items():
            quelle = id_zu_quelle[id_]
            bild_treffer = sorted((quelle / "bilder").glob(f"{id_}.*"))
            if len(bild_treffer) != 1:
                raise SystemExit(
                    f"Bilddatei fuer id={id_!r} in {quelle / 'bilder'} nicht "
                    f"eindeutig gefunden ({len(bild_treffer)} Treffer).")
            bild_quelle = bild_treffer[0]
            label_quelle = quelle / "labels" / f"{id_}.txt"
            if not label_quelle.is_file():
                raise SystemExit(f"Labeldatei fehlt: {label_quelle}")

            shutil.copy2(bild_quelle,
                        staging / "images" / teil / f"{id_}{bild_quelle.suffix}")
            shutil.copy2(label_quelle, staging / "labels" / teil / f"{id_}.txt")

        schreibe_data_yaml(staging)

        beleg = baue_beleg(quellen_belege, args.val_anteil, args.saat, id_zu_split)
        beleg_text = json.dumps(beleg, indent=2, ensure_ascii=False)
        beleg_pfad = staging / "datensatz.json"
        # Atomar: erst in eine Temp-Datei im selben Ordner schreiben, dann
        # os.replace() - ein Absturz mittendrin hinterlaesst kein halbes JSON.
        temp = beleg_pfad.with_name(f".{beleg_pfad.name}.tmp-{uuid.uuid4().hex}")
        temp.write_text(beleg_text, encoding="utf-8")
        os.replace(temp, beleg_pfad)

        if not _ziel_ist_frei(args.ziel):
            # Erneute Pruefung direkt vor der Veroeffentlichung: ein
            # nebenlaeufiger Schreiber darf das Ziel nicht unbemerkt gefuellt
            # haben, waehrend dieser Lauf Quellen gelesen und kopiert hat.
            raise SystemExit(
                f"Ziel wurde waehrend des Laufs belegt: {args.ziel}")
        if args.ziel.is_dir():
            args.ziel.rmdir()  # bereits als leer geprueft - gefahrlos
        staging.replace(args.ziel)
    except Exception:
        shutil.rmtree(staging, ignore_errors=True)
        raise

    zaehler = {"train": 0, "val": 0}
    for teil in id_zu_split.values():
        zaehler[teil] += 1

    print(f"Quellen: {len(args.quelle)}")
    for beleg_quelle in quellen_belege:
        print(f"  {beleg_quelle['pfad']}: {beleg_quelle['anzahl_eintraege']} "
              f"Eintraege ({beleg_quelle['schema']})")
    print(f"Gruppen gesamt: {len(gruppen)}")
    print(f"Train: {zaehler['train']} Bilder")
    print(f"Val:   {zaehler['val']} Bilder")
    print(f"Bestand: {args.ziel / 'data.yaml'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
