"""Erntet exakt beschriftete Zeichenausschnitte mit dem heutigen Leser.

WOZU
Der Vorlagenweg von osd_meter.py liefert dort, wo er eine VOLLSTAENDIGE Lesung
schafft, nachweislich fehlerfreie Werte - auf dem gesamten Goldbestand null
falsch. Genau diese Lesungen sind gratis verfuegbare Zeichenwahrheit auf echten
Bildern, inklusive Zeichenboxen aus boxen_aus_maske().

WAS NICHT GEERNTET WIRD
Bruchstueck-Lesungen (Ziffern erkannt, aber weder Beschriftung noch Einheit).
Dort steht die Stellenzahl nicht fest und der Dezimalpunkt wird geraten; auf 897
beschrifteten Archivbildern waren 58 von 61 solcher Werte grob falsch. Als
Trainingsetikett waere das Gift.

GRENZE
Diese Quelle lehrt nur, was der Lehrer schon kann. Sie allein hebt die Abdeckung
nicht - dafuer sind die kuenstlichen Bilder und die Handfaelle da.

Rein lesend: Kundenbilder werden nie veraendert.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence

from PIL import Image

WURZEL = Path(__file__).resolve().parents[2]
if str(WURZEL / "sidecar") not in sys.path:
    sys.path.insert(0, str(WURZEL / "sidecar"))
if str(WURZEL / "training" / "scripts") not in sys.path:
    sys.path.insert(0, str(WURZEL / "training" / "scripts"))

from sidecar import osd_meter
from osd_crop import schneide_zone
from osd_schutz import GOLD_WURZEL, RESERVEBESTAND_STANDARD, Schutz, lade_schutz


@dataclass(frozen=True)
class Ernteergebnis:
    ausschnitt: Image.Image
    zeichen: list[tuple[int, float, float, float, float]]
    zeichenfolge: str
    meter: float


def zonen_ausschnitt(bild: Image.Image) -> tuple[Image.Image, tuple[int, int]]:
    """Schneidet die Zone unten rechts heraus.

    Fix-Runde 1 (Aufgabe 3): frueher eigenes int()-Runden hier, waehrend
    osd_modell_leser.py mit round() schnitt - auf zwei von drei
    Gold-Aufloesungen verschob das den Ausschnitt zwischen Training und
    Messung um eine Bildzeile. Beide Seiten nutzen jetzt denselben
    osd_crop.schneide_zone() (siehe dort fuer die Begruendung von round()).
    """
    return schneide_zone(bild)


def als_labeltext(zeichen: list[tuple[int, float, float, float, float]]) -> str:
    """YOLO-Labelzeilen mit sechs Nachkommastellen."""
    return "".join(
        f"{klasse} {x:.6f} {y:.6f} {b:.6f} {h:.6f}\n"
        for klasse, x, y, b, h in zeichen)


def ernte_bild(bild: Image.Image, templates, schutz: Schutz,
               bild_sha256: str, haltung: str | None) -> Ernteergebnis | None:
    """Liefert Ausschnitt plus Zeichenboxen - oder None, wenn nichts taugt."""
    if schutz.ist_gesperrt(bild_sha256, haltung):
        return None

    maske, stil = osd_meter.glyphenmaske(bild)
    boxen = osd_meter.boxen_aus_maske(maske, stil)
    if not boxen:
        return None

    zeichenfolge = ""
    for (x0, y0, x1, y1) in boxen:
        glyph = maske[y0:y1, x0:x1].astype("float32")
        zeichen, _ = osd_meter.klassifiziere(glyph, templates)
        zeichenfolge += zeichen or "?"

    # Nur der vollstaendige Vorlagenweg. Kein Tesseract-Rueckfall, kein Raten.
    if "?" in zeichenfolge:
        return None
    if not osd_meter._zeichenfolge_ist_vollstaendig(zeichenfolge):
        return None
    meter = osd_meter.parse_meter(zeichenfolge, stil)
    if meter is None:
        return None

    ausschnitt, (versatz_x, versatz_y) = zonen_ausschnitt(bild)
    a_breite, a_hoehe = ausschnitt.size
    if a_breite <= 0 or a_hoehe <= 0:
        return None

    zeichen_labels: list[tuple[int, float, float, float, float]] = []
    for zeichen, (x0, y0, x1, y1) in zip(zeichenfolge, boxen):
        klasse = osd_meter.ZEICHEN.find(zeichen)
        if klasse < 0:
            return None
        # Boxen liegen in Vollbildkoordinaten; auf den Ausschnitt umrechnen.
        rx0, rx1 = x0 - versatz_x, x1 - versatz_x
        ry0, ry1 = y0 - versatz_y, y1 - versatz_y
        if rx0 < 0 or ry0 < 0 or rx1 > a_breite or ry1 > a_hoehe:
            return None
        zeichen_labels.append((
            klasse,
            ((rx0 + rx1) / 2) / a_breite,
            ((ry0 + ry1) / 2) / a_hoehe,
            (rx1 - rx0) / a_breite,
            (ry1 - ry0) / a_hoehe,
        ))

    return Ernteergebnis(ausschnitt, zeichen_labels, zeichenfolge, meter)


# ---------------------------------------------------------------------------
# CLI: Ernte auf einem ganzen Bilderordner ausfuehren und veroeffentlichen.
#
# Der Kern oben (ernte_bild) ist rein funktional und dateisystemfrei. Alles
# unterhalb ist duenne Verdrahtung: Ordner durchsuchen, Ergebnis wegschreiben,
# Zusammenfassung drucken. Nicht Teil der urspruenglichen Aufgabenskizze -
# ergaenzt, weil ohne einen Schreibweg keine Eintraege fuer Aufgabe 4
# entstehen wuerden.
# ---------------------------------------------------------------------------

BILD_ENDUNGEN = {".jpg", ".jpeg", ".png"}

# Sieht der Ordnername wie eine Haltung aus? Genau ein Bindestrich, beide
# Seiten nicht leer, aus Ziffern/Buchstaben (inkl. Umlaute/ss)/Punkten/
# Unterstrichen. Kein Normalisieren hier - das macht Schutz.ist_gesperrt()
# bereits selbst ueber physische_haltung().
#
# Fix-Runde 1 (2026-08-15): Das urspruengliche Muster ohne Punkt liess das
# GESAMTE echte Archiv unter D:\Haltungen durchfallen - dort tragen die
# Ordner Punkte ("06.24341-35625", "06.691078-691070"), nur die Goldmanifeste
# haben punktlose Namen ("36051-33461"). Ohne erkannte Haltung griff der
# Gegenrichtungsschutz nirgends mehr, nur noch der Bildhash-Schutz - eine
# Goldhaltung in der Gegenrichtung waere ungehindert ins Training gelangt.
# Weil beide Seiten hier keinen Bindestrich enthalten duerfen, bleibt das
# Muster automatisch zum ERSTEN-Bindestrich-Split von haltungsvarianten()
# (osd_wahrheit_aus_protokoll.py, split("-", 1)) konsistent: Ein Treffer hat
# hier ohnehin nie mehr als einen Bindestrich.
#
# Beim Abgleich gegen das echte Archiv (1476 Ordner) fiel zusaetzlich ein
# echter Umlautname auf ("61542-Schaechen_Bach", hier mit "ae" wiedergegeben -
# die echte Datei traegt den echten Umlaut). "Buchstaben" schliesst im
# Deutschen Umlaute und "ss" ein, deshalb ergaenzt. NICHT ergaenzt: Leerzeichen
# um den Bindestrich ("36510 - 36906", 1 von 1476 Ordnern) - dieses Format
# wuerde mit den `<Hauptcode - Klartext>`-Ordnern von gold_frames kollidieren
# (siehe CLAUDE.md, z. B. "BAB - Riss"), die ausdruecklich KEINE Haltungen
# sind. Bleibt bewusst ohne Haltungserkennung; der Bildhash-Schutz greift dort
# weiterhin.
_HALTUNG_MUSTER = re.compile(r"^[A-Za-z0-9._äöüÄÖÜß]+-[A-Za-z0-9._äöüÄÖÜß]+$")


def haltung_aus_ordnername(name: str) -> str | None:
    """Rohen Ordnernamen als Haltung uebernehmen, wenn er danach aussieht."""
    name = (name or "").strip()
    return name if _HALTUNG_MUSTER.match(name) else None


def bild_id(bild_sha256: str) -> str:
    """Kurze, stabile Dateikennung: die ersten 16 Hexzeichen des Bildhashes."""
    return bild_sha256[:16]


def eintrag_erzeugen(bild_sha256: str, haltung: str | None,
                      zeichenfolge: str, meter: float) -> dict:
    """Baut den Eintrag fuer eintraege.json (Schema osd_ernte_v1)."""
    return {
        "id": bild_id(bild_sha256),
        "bild_sha256": bild_sha256,
        "haltung": haltung,
        "zeichenfolge": zeichenfolge,
        "meter": meter,
    }


def _sha256_datei(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1 << 20), b""):
            hasher.update(block)
    return hasher.hexdigest()


def _ist_link_oder_junction(pfad: Path) -> bool:
    """True bei Symlink oder Windows-Reparse-Point (z.B. Junction).

    Ohne diese Pruefung koennte ein verlinkter Unterordner unter --bilder auf
    einen ganz anderen Ort zeigen (z.B. hinaus aus dem beabsichtigten Archiv)
    und dessen Inhalt unbemerkt in die Ernte holen - dieselbe Frage ("was darf
    in die Ernte") wie beim Schutz gegen Gold-/Reservebilder, nur auf
    Dateisystemebene. lstat() statt stat(): der Link selbst wird geprueft,
    nicht sein Ziel. Ein Lesefehler zaehlt als Link (fail-closed).
    """
    try:
        metadaten = pfad.lstat()
    except OSError:
        return True
    attribute = getattr(metadaten, "st_file_attributes", 0)
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return pfad.is_symlink() or bool(attribute & reparse_flag)


def _bilder_finden(wurzel: Path) -> list[Path]:
    """Durchsucht wurzel rekursiv, folgt aber keinem Link/Junction.

    os.walk() statt rglob(): rglob() steigt in JEDEN Unterordner ab, auch
    verlinkte - dirnames[:] laesst sich dagegen vor dem Abstieg filtern.
    """
    if _ist_link_oder_junction(wurzel):
        return []

    treffer: list[Path] = []
    for aktuell, unterordner, dateien in os.walk(wurzel):
        aktueller_pfad = Path(aktuell)
        unterordner[:] = [
            name for name in unterordner
            if not _ist_link_oder_junction(aktueller_pfad / name)
        ]
        for name in dateien:
            pfad = aktueller_pfad / name
            if pfad.suffix.lower() in BILD_ENDUNGEN and not _ist_link_oder_junction(pfad):
                treffer.append(pfad)
    return sorted(treffer)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bilder", type=Path, required=True,
                        help="Ordner mit Quellbildern (rekursiv durchsucht)")
    parser.add_argument("--ziel", type=Path, required=True,
                        help="Ausgabeordner fuer geerntete Bilder, Labels und eintraege.json")
    parser.add_argument("--gold-wurzel", type=Path, default=GOLD_WURZEL,
                        help="Wurzel der eingefrorenen Goldsaetze (Sperrliste)")
    parser.add_argument("--reservebestand", type=Path, default=RESERVEBESTAND_STANDARD,
                        help="wahrheit.json des Reservebestands (Testteil gesperrt)")
    parser.add_argument("--limit", type=int, default=None,
                        help="Nur die ersten N Bilder verarbeiten (Probelauf)")
    args = parser.parse_args(argv)

    if not args.bilder.is_dir():
        raise SystemExit(f"Bilderordner fehlt: {args.bilder}")
    if args.limit is not None and args.limit <= 0:
        raise SystemExit("--limit muss positiv sein.")

    schutz = lade_schutz(args.gold_wurzel, reservebestand=args.reservebestand)
    templates = osd_meter.get_templates()

    bilder_ordner = args.ziel / "bilder"
    labels_ordner = args.ziel / "labels"
    bilder_ordner.mkdir(parents=True, exist_ok=True)
    labels_ordner.mkdir(parents=True, exist_ok=True)

    quellen = _bilder_finden(args.bilder)
    if args.limit is not None:
        quellen = quellen[:args.limit]
    if not quellen:
        raise SystemExit(f"Keine Bilder unter {args.bilder}")

    zaehler = {"gesehen": 0, "geerntet": 0, "geschuetzt": 0, "unlesbar": 0,
               "ohne_haltung": 0}
    eintraege: list[dict] = []

    for pfad in quellen:
        zaehler["gesehen"] += 1
        try:
            # Fix-Runde 1 (2026-08-15): Der gesamte Bildkoerper - Oeffnen,
            # Hashen, ernte_bild() bis zum Schreiben - liegt bewusst in EINEM
            # try/except. Ein einzelnes ungewoehnliches, aber dekodierbares
            # Bild darf einen Lauf ueber tausende Bilder nicht mit einer
            # unbehandelten Ausnahme abbrechen (dann wuerde eintraege.json
            # nie geschrieben und der ganze Lauf waere wertlos).
            bild = Image.open(pfad)
            bild.load()

            bild_sha256 = _sha256_datei(pfad)
            haltung = haltung_aus_ordnername(pfad.parent.name)
            if haltung is None:
                # Sichtbar machen statt still: Ohne erkannte Haltung greift
                # fuer dieses Bild nur der Bildhash-Schutz, nicht der
                # Gegenrichtungsschutz ueber physische_haltung().
                zaehler["ohne_haltung"] += 1

            # Vorab pruefen (ernte_bild prueft intern erneut): nur so kann
            # die Zusammenfassung "geschuetzt" von "unlesbar/unvollstaendig"
            # trennen - ernte_bild() gibt fuer beide Faelle gleichermassen
            # None zurueck.
            if schutz.ist_gesperrt(bild_sha256, haltung):
                zaehler["geschuetzt"] += 1
                continue

            ergebnis = ernte_bild(bild, templates, schutz, bild_sha256, haltung)
            if ergebnis is None:
                zaehler["unlesbar"] += 1
                continue

            kennung = bild_id(bild_sha256)
            ergebnis.ausschnitt.save(bilder_ordner / f"{kennung}.png")
            (labels_ordner / f"{kennung}.txt").write_text(
                als_labeltext(ergebnis.zeichen), encoding="utf-8")
            eintraege.append(eintrag_erzeugen(
                bild_sha256, haltung, ergebnis.zeichenfolge, ergebnis.meter))
            zaehler["geerntet"] += 1
        except Exception:
            zaehler["unlesbar"] += 1
            continue

    dokument = {"schema": "osd_ernte_v1", "eintraege": eintraege}
    ziel_json = args.ziel / "eintraege.json"
    # Atomar: erst in eine Temp-Datei im selben Ordner schreiben, dann
    # os.replace() - ein Absturz mittendrin hinterlaesst kein halbes JSON.
    temp = ziel_json.with_name(f".{ziel_json.name}.tmp-{uuid.uuid4().hex}")
    temp.write_text(json.dumps(dokument, indent=2, ensure_ascii=False), encoding="utf-8")
    os.replace(temp, ziel_json)

    print(f"Bilder gesehen: {zaehler['gesehen']}")
    print(f"Geerntet: {zaehler['geerntet']}")
    print(f"Uebersprungen (geschuetzt): {zaehler['geschuetzt']}")
    print(f"Uebersprungen (unlesbar/unvollstaendig): {zaehler['unlesbar']}")
    print(f"Ohne erkennbare Haltung (Gegenrichtungsschutz konnte nicht "
          f"greifen): {zaehler['ohne_haltung']}")
    print(f"Bestand: {ziel_json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
