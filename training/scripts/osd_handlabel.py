"""Handliste fuer die harten OSD-Faelle, die der Lehrer nicht lesen kann.

WOZU
Stufe 1 des Zeichentrainings ist nachweisbar gescheitert: Der trainierte
Kandidat liest 120 von 197 Goldbildern richtig (1 falsch), der bestehende
Vorlagenleser 138 richtig (0 falsch). Ursache: Die Trainingsdaten kamen als
Lehrer-Ernte (osd_ernte.py) - das Modell lernte nur, was der Lehrer selbst
schon kann (932 Ausschnitte aus nur 229 von 1361 Haltungen). Die anderen 1132
Haltungen tragen genau die Anzeigestile, an denen der Lehrer scheitert.

Deshalb beschriftet jetzt ein Mensch die harten Faelle von Hand. Der ganze
Zweck ist, Stile einzufangen, an denen der Lehrer scheitert - jede Beeinflussung
durch eine Modell- oder Vorlagen-Vermutung zerstoert genau diesen Wert.

DIE MESSUNG, DIE DEN ENTWURF BESTIMMT
Auf 400 Stichprobenbildern fand die Zeichenfindung des Lehrers (glyphenmaske +
boxen_aus_maske) in 64 % der Faelle Boxen, an denen aber Benennung/
Vollstaendigkeit der Lesung scheitert. 16 % sind bereits vollstaendig lesbar
(gehoeren der Lehrer-Ernte), 16 % liefern gar keine Boxen und 4 % weniger als
vier Boxen. Auf zwei Dritteln der harten Bilder existieren die Zeichenboxen also
schon - nur die Benennung fehlt. Der Mensch tippt deshalb nur eine Zeichenkette;
er zeichnet nie eine Box. Das macht daraus eine Stundenarbeit statt ein
Wochenendprojekt.

ZWEI MODI
"queue" baut die eingefrorene Arbeitsliste: durchsucht die Quelle (Standard
D:\\OSD_Frames, Ablage <Haltung>/<name>.jpg), behaelt nur Bilder mit
mindestens 4 gefundenen Boxen, an denen die vollstaendige Lesung scheitert,
hoechstens ein Bild je physischer Haltung, prueft die Sperrliste
(osd_schutz.lade_schutz()) und waehlt deterministisch aus.
"publizieren" liest eine abgeschlossene Review und baut daraus Trainingsdaten
im Schema osd_ernte_v1 - exakt in der Form, die osd_datensatz.py bereits
kennt (Wiederverwendung von osd_ernte.eintrag_erzeugen/als_labeltext statt
einer zweiten Implementierung).

Kundenoriginale und D:\\OSD_Frames sind fuer dieses Werkzeug nur lesend.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import random
import shutil
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence

from PIL import Image

WURZEL = Path(__file__).resolve().parents[2]
if str(WURZEL / "sidecar") not in sys.path:
    sys.path.insert(0, str(WURZEL / "sidecar"))
if str(WURZEL / "training" / "scripts") not in sys.path:
    sys.path.insert(0, str(WURZEL / "training" / "scripts"))

from sidecar import osd_meter
import osd_ernte
from osd_crop import schneide_zone
from osd_schutz import GOLD_WURZEL, RESERVEBESTAND_STANDARD, lade_schutz
from osd_wahrheit_aus_protokoll import physische_haltung

QUELLE_STANDARD = Path(r"D:\OSD_Frames")
ZIEL_STANDARD = Path(r"C:\KI_BRAIN\training\diagnostics\osd_handlabel_v1")

SCHEMA_QUEUE = "osd_handlabel_queue_v1"
SCHEMA_ERNTE = "osd_ernte_v1"
MIN_BOXEN = 4
AKTIONEN = ("uebernommen", "unleserlich", "boxen_passen_nicht")


def _sha256_datei(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1 << 20), b""):
            hasher.update(block)
    return hasher.hexdigest()


def _png_bytes(bild: Image.Image) -> bytes:
    """PNG-Bytes im Speicher - hashbar, bevor irgendetwas geschrieben wird."""
    puffer = io.BytesIO()
    bild.save(puffer, format="PNG")
    return puffer.getvalue()


# ---------------------------------------------------------------------------
# Modus "queue": reine Bild->Boxen-Logik (dateisystemfrei, testbar wie
# osd_ernte.ernte_bild). Der Schutz-Check liegt bewusst eine Ebene hoeher
# (dort sind Bildhash und Haltung schon bekannt).
# ---------------------------------------------------------------------------

@dataclass(frozen=True)
class HarterFall:
    """Ein Bild, dessen Boxen brauchbar sind, dessen Lesung aber scheitert."""

    boxen: list[tuple[int, int, int, int]]
    stil: str


def lesung_scheitert(zeichenfolge: str, stil: str) -> bool:
    """True, wenn die vollstaendige Vorlagenlesung fehlschlaegt.

    Dieselben drei Bedingungen wie in osd_ernte.ernte_bild(), nur umgekehrt
    gelesen: Dort ist ein Treffer ein Ausschlussgrund (das Material ist schon
    geerntet); hier ist er der Aufnahmegrund fuer die Handliste.
    """
    if "?" in zeichenfolge:
        return True
    if not osd_meter._zeichenfolge_ist_vollstaendig(zeichenfolge):
        return True
    return osd_meter.parse_meter(zeichenfolge, stil) is None


def pruefe_bild(bild: Image.Image, templates) -> HarterFall | None:
    """Liefert Boxen+Stil eines harten Falls - oder None.

    None bedeutet eines von zwei Dingen: zu wenige Boxen (unbrauchbar) ODER
    eine bereits vollstaendige Lesung (gehoert der Lehrer-Ernte, nicht der
    Handliste).
    """
    maske, stil = osd_meter.glyphenmaske(bild)
    boxen = osd_meter.boxen_aus_maske(maske, stil)
    if len(boxen) < MIN_BOXEN:
        return None

    zeichenfolge = ""
    for (x0, y0, x1, y1) in boxen:
        glyph = maske[y0:y1, x0:x1].astype("float32")
        zeichen, _ = osd_meter.klassifiziere(glyph, templates)
        zeichenfolge += zeichen or "?"

    if not lesung_scheitert(zeichenfolge, stil):
        return None

    return HarterFall(list(boxen), stil)


def waehle_faelle(kandidaten: list[dict], anzahl: int, saat: int) -> list[dict]:
    """Waehlt hoechstens ANZAHL Faelle, ein Bild je physischer Haltung.

    Deterministisch bei gleicher Eingabe: Zuerst wird je physischer Haltung
    der Kandidat mit dem kleinsten Bildpfad behalten (Sortierschluessel, nicht
    von der Reihenfolge der Eingabeliste abhaengig), danach werden die
    Haltungsschluessel sortiert und mit fester Saat gemischt - dieselbe
    Technik wie osd_datensatz.teile_auf().
    """
    if anzahl <= 0:
        raise ValueError("Die Anzahl muss groesser als null sein.")

    je_haltung: dict[str, dict] = {}
    for kandidat in kandidaten:
        phys = physische_haltung(str(kandidat["haltung"]))
        vorhanden = je_haltung.get(phys)
        if vorhanden is None or str(kandidat["bild_pfad"]) < str(vorhanden["bild_pfad"]):
            je_haltung[phys] = kandidat

    schluessel = sorted(je_haltung)
    zufall = random.Random(saat)
    zufall.shuffle(schluessel)
    return [je_haltung[schluessel_wert] for schluessel_wert in schluessel[:anzahl]]


def fall_erzeugen(bild_sha256: str, haltung: str, bild_pfad: str,
                  boxen: list[tuple[int, int, int, int]], stil: str) -> dict:
    """Baut einen Eintrag fuer queue.json (Schema osd_handlabel_queue_v1)."""
    return {
        "id": osd_ernte.bild_id(bild_sha256),
        "bild_sha256": bild_sha256,
        "haltung": haltung,
        "bild_pfad": bild_pfad,
        "boxen": [list(box) for box in boxen],
        "stil": stil,
    }


# ---------------------------------------------------------------------------
# Modus "queue": CLI. Durchsucht die Quelle, ruft pruefe_bild() je Bild auf,
# waehlt deterministisch aus und schreibt queue.json atomar.
# ---------------------------------------------------------------------------

def _haltungsordner_finden(quelle: Path) -> list[Path]:
    """Direkte Unterordner von quelle, sortiert. Folgt keinen Verknuepfungen."""
    return sorted(
        pfad for pfad in quelle.iterdir()
        if pfad.is_dir() and not pfad.is_symlink())


def _bilder_unter_haltung(haltung_ordner: Path) -> list[Path]:
    """Bilddateien direkt unter dem Haltungsordner, sortiert."""
    return sorted(
        pfad for pfad in haltung_ordner.iterdir()
        if pfad.is_file() and not pfad.is_symlink()
        and pfad.suffix.lower() in osd_ernte.BILD_ENDUNGEN)


def _main_queue(args: argparse.Namespace) -> int:
    if not args.quelle.is_dir():
        raise SystemExit(f"Quellordner fehlt: {args.quelle}")
    if args.anzahl <= 0:
        raise SystemExit("--anzahl muss groesser als null sein.")
    if args.ziel.exists():
        raise SystemExit(
            f"Ziel existiert bereits und wird nicht ueberschrieben: {args.ziel}")

    schutz = lade_schutz(args.gold_wurzel, reservebestand=args.reservebestand)
    templates = osd_meter.get_templates()

    zaehler = {"gesehen": 0, "unlesbar": 0, "geschuetzt": 0, "mit_boxen": 0,
               "ohne_haltung": 0}
    kandidaten: list[dict] = []
    geschuetzte_treffer: list[str] = []

    for haltung_ordner in _haltungsordner_finden(args.quelle):
        haltung = osd_ernte.haltung_aus_ordnername(haltung_ordner.name)
        if haltung is None:
            zaehler["ohne_haltung"] += 1
            continue

        for bild_pfad in _bilder_unter_haltung(haltung_ordner):
            zaehler["gesehen"] += 1
            try:
                bild = Image.open(bild_pfad)
                bild.load()
                bild_sha256 = _sha256_datei(bild_pfad)
            except Exception:
                zaehler["unlesbar"] += 1
                continue

            if schutz.ist_gesperrt(bild_sha256, haltung):
                # Die Extraktion (osd_frames_ziehen.py) haette gesperrte
                # Haltungen bereits ausschliessen muessen. Ein Treffer hier
                # ist ein Zeichen, dass etwas vorgelagert falsch gelaufen
                # ist - sichtbar machen, aber den Lauf nicht abbrechen.
                zaehler["geschuetzt"] += 1
                geschuetzte_treffer.append(str(bild_pfad))
                continue

            try:
                ergebnis = pruefe_bild(bild, templates)
            except Exception:
                zaehler["unlesbar"] += 1
                continue
            if ergebnis is None:
                continue

            zaehler["mit_boxen"] += 1
            kandidaten.append(fall_erzeugen(
                bild_sha256, haltung, str(bild_pfad.resolve()),
                ergebnis.boxen, ergebnis.stil))

    if not kandidaten:
        raise SystemExit("Keine brauchbaren Bilder gefunden (harte Faelle mit "
                         f">= {MIN_BOXEN} Boxen und scheiternder Lesung).")

    auswahl = waehle_faelle(kandidaten, args.anzahl, args.saat)

    dokument = {
        "schema": SCHEMA_QUEUE,
        "erzeugt_utc": datetime.now(timezone.utc).isoformat(),
        "quelle": str(args.quelle),
        "saat": args.saat,
        "faelle": auswahl,
    }

    staging = args.ziel.with_name(f".{args.ziel.name}.staging-{uuid.uuid4().hex}")
    staging.mkdir(parents=True)
    try:
        (staging / "queue.json").write_text(
            json.dumps(dokument, indent=2, ensure_ascii=False), encoding="utf-8")
        staging.replace(args.ziel)
    except Exception:
        shutil.rmtree(staging, ignore_errors=True)
        raise

    haltungen_verfuegbar = len(
        {physische_haltung(str(k["haltung"])) for k in kandidaten})

    print(f"Bilder gesehen: {zaehler['gesehen']}")
    print(f"Mit brauchbaren Boxen (>= {MIN_BOXEN} Boxen, Lesung scheitert): "
          f"{zaehler['mit_boxen']}")
    print(f"Geschuetzt uebersprungen: {zaehler['geschuetzt']}")
    if geschuetzte_treffer:
        print("ACHTUNG: geschuetzte Bilder unter der Quelle gefunden - die "
              "Extraktion haette diese bereits ausschliessen muessen:")
        for treffer in geschuetzte_treffer[:20]:
            print(f"  {treffer}")
    if zaehler["ohne_haltung"]:
        print(f"Ordner ohne erkennbare Haltung uebersprungen: {zaehler['ohne_haltung']}")
    if zaehler["unlesbar"]:
        print(f"Unlesbare Bilder uebersprungen: {zaehler['unlesbar']}")
    print(f"Haltungen verfuegbar: {haltungen_verfuegbar}")
    print(f"Queue-Groesse: {len(auswahl)}")
    print(f"Queue: {args.ziel / 'queue.json'}")
    return 0


# ---------------------------------------------------------------------------
# Modus "publizieren": liest Queue + abgeschlossene Review, baut daraus einen
# Bestand im Schema osd_ernte_v1 - Wiederverwendung von
# osd_ernte.eintrag_erzeugen()/als_labeltext(), keine zweite Implementierung.
# ---------------------------------------------------------------------------

def _main_publizieren(args: argparse.Namespace) -> int:
    queue_pfad = args.queue / "queue.json"
    if not queue_pfad.is_file():
        raise SystemExit(f"Queue fehlt: {queue_pfad}")
    queue_sha256 = _sha256_datei(queue_pfad)
    queue = json.loads(queue_pfad.read_text(encoding="utf-8-sig"))
    faelle = list(queue.get("faelle") or [])
    if not faelle:
        raise SystemExit("Queue enthaelt keine Faelle.")

    if not args.review.is_file():
        raise SystemExit(f"Review fehlt: {args.review}")
    review = json.loads(args.review.read_text(encoding="utf-8-sig"))
    if review.get("queue_sha256") != queue_sha256:
        raise SystemExit(
            "Review passt nicht zur Queue (queue_sha256 weicht ab) - "
            "vermutlich eine andere oder neuere Queue-Datei.")

    entscheidungen = dict(review.get("entscheidungen") or {})
    ids = {str(fall["id"]) for fall in faelle}
    offen = ids - set(entscheidungen)
    if offen:
        raise SystemExit(
            f"Review ist unvollstaendig: {len(offen)} von {len(ids)} Faellen "
            "ohne Entscheidung.")

    bilder_ordner = args.ziel / "bilder"
    labels_ordner = args.ziel / "labels"
    bilder_ordner.mkdir(parents=True, exist_ok=True)
    labels_ordner.mkdir(parents=True, exist_ok=True)

    zaehler = {"uebernommen": 0, "unleserlich": 0, "boxen_passen_nicht": 0,
               "ohne_meterwert": 0}
    eintraege: list[dict] = []

    for fall in faelle:
        fall_id = str(fall["id"])
        entscheidung = entscheidungen[fall_id]
        aktion = entscheidung.get("aktion")
        if aktion not in AKTIONEN:
            raise SystemExit(f"Fall {fall_id}: unbekannte Aktion {aktion!r}.")
        if aktion != "uebernommen":
            zaehler[aktion] += 1
            continue

        # Zweite Pruefung nach der Server-Validierung (Fail-closed, dieselbe
        # Haltung wie osd_datensatz.pruefe_keine_gesperrten): eine von Hand
        # veraenderte Review-Datei darf nie unbemerkt falsche Zeichen in den
        # Trainingsdatensatz schleusen.
        zeichenfolge = str(entscheidung.get("zeichenfolge") or "").replace(" ", "")
        boxen = [tuple(int(wert) for wert in box) for box in fall["boxen"]]
        if len(zeichenfolge) != len(boxen):
            raise SystemExit(
                f"Fall {fall_id}: Zeichenzahl ({len(zeichenfolge)}) passt "
                f"nicht zur Boxenzahl ({len(boxen)}).")
        unbekannt = sorted(set(zeichenfolge) - set(osd_meter.ZEICHEN))
        if unbekannt:
            raise SystemExit(
                f"Fall {fall_id}: unbekannte Zeichen {unbekannt!r} (erlaubt: "
                f"{osd_meter.ZEICHEN!r}).")

        bild_pfad = Path(str(fall["bild_pfad"]))
        if not bild_pfad.is_file():
            raise SystemExit(f"Fall {fall_id}: Bild fehlt: {bild_pfad}")
        bild_sha256 = _sha256_datei(bild_pfad)
        if bild_sha256 != fall.get("bild_sha256"):
            raise SystemExit(
                f"Fall {fall_id}: Bild wurde seit der Queue-Erzeugung veraendert.")

        bild = Image.open(bild_pfad)
        bild.load()
        ausschnitt, (versatz_x, versatz_y) = schneide_zone(bild)
        a_breite, a_hoehe = ausschnitt.size
        if a_breite <= 0 or a_hoehe <= 0:
            raise SystemExit(f"Fall {fall_id}: leerer Zuschnitt.")

        zeichen_labels: list[tuple[int, float, float, float, float]] = []
        for zeichen, (x0, y0, x1, y1) in zip(zeichenfolge, boxen):
            klasse = osd_meter.ZEICHEN.find(zeichen)
            rx0, rx1 = x0 - versatz_x, x1 - versatz_x
            ry0, ry1 = y0 - versatz_y, y1 - versatz_y
            if rx0 < 0 or ry0 < 0 or rx1 > a_breite or ry1 > a_hoehe:
                raise SystemExit(
                    f"Fall {fall_id}: Box liegt ausserhalb des Zuschnitts.")
            zeichen_labels.append((
                klasse,
                ((rx0 + rx1) / 2) / a_breite,
                ((ry0 + ry1) / 2) / a_hoehe,
                (rx1 - rx0) / a_breite,
                (ry1 - ry0) / a_hoehe,
            ))

        stil = str(fall.get("stil") or "dunkel")
        # None bleibt None: ein nicht parsebarer Meterwert macht die
        # Zeichenwahrheit nicht wertlos, siehe Modul-Docstring.
        meter = osd_meter.parse_meter(zeichenfolge, stil)
        if meter is None:
            zaehler["ohne_meterwert"] += 1

        png_bytes = _png_bytes(ausschnitt)
        ausschnitt_sha256 = hashlib.sha256(png_bytes).hexdigest()

        (bilder_ordner / f"{fall_id}.png").write_bytes(png_bytes)
        (labels_ordner / f"{fall_id}.txt").write_text(
            osd_ernte.als_labeltext(zeichen_labels), encoding="utf-8")
        eintraege.append(osd_ernte.eintrag_erzeugen(
            bild_sha256, fall.get("haltung"), zeichenfolge, meter,
            ausschnitt_sha256))
        zaehler["uebernommen"] += 1

    dokument = {"schema": SCHEMA_ERNTE, "eintraege": eintraege}
    args.ziel.mkdir(parents=True, exist_ok=True)
    ziel_json = args.ziel / "eintraege.json"
    temp = ziel_json.with_name(f".{ziel_json.name}.tmp-{uuid.uuid4().hex}")
    temp.write_text(json.dumps(dokument, indent=2, ensure_ascii=False), encoding="utf-8")
    os.replace(temp, ziel_json)

    print(f"Faelle gesamt: {len(faelle)}")
    print(f"Uebernommen: {zaehler['uebernommen']}")
    print(f"  davon ohne gueltigen Meterwert (Zeichenwahrheit bleibt trotzdem "
          f"brauchbar): {zaehler['ohne_meterwert']}")
    print(f"Unleserlich: {zaehler['unleserlich']}")
    print(f"Boxen passen nicht: {zaehler['boxen_passen_nicht']}")
    print(f"Bestand: {ziel_json}")
    return 0


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="modus", required=True)

    p_queue = sub.add_parser(
        "queue", help="Baut die eingefrorene Arbeitsliste der harten Faelle.")
    p_queue.add_argument("--quelle", type=Path, default=QUELLE_STANDARD)
    p_queue.add_argument("--ziel", type=Path, default=ZIEL_STANDARD)
    p_queue.add_argument("--anzahl", type=int, default=200)
    p_queue.add_argument("--saat", type=int, default=0)
    p_queue.add_argument("--gold-wurzel", type=Path, default=GOLD_WURZEL)
    p_queue.add_argument("--reservebestand", type=Path, default=RESERVEBESTAND_STANDARD)

    p_pub = sub.add_parser(
        "publizieren",
        help="Baut aus einer abgeschlossenen Review einen osd_ernte_v1-Bestand.")
    p_pub.add_argument("--queue", type=Path, required=True,
                       help="Ordner mit queue.json (aus Modus 'queue')")
    p_pub.add_argument("--review", type=Path, required=True,
                       help="Abgeschlossene Review-JSON-Datei")
    p_pub.add_argument("--ziel", type=Path, required=True,
                       help="Ausgabeordner fuer bilder/labels/eintraege.json")

    args = parser.parse_args(argv)
    if args.modus == "queue":
        return _main_queue(args)
    return _main_publizieren(args)


if __name__ == "__main__":
    raise SystemExit(main())
