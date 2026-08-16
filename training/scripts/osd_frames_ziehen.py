"""Zieht Einzelbilder aus den Archivvideos in Ordner, die nach der Haltung heissen.

WOZU
Die Lehrer-Ernte (osd_ernte.py) sucht die Haltungsnummer im direkten
Elternordner des Bildes. Kein vorhandener Bildbestand ist so abgelegt:
gold_frames sortiert nach Schadenscode ("BAB - Riss"), der OSD-Wahrheitsbestand
traegt die Haltung im Dateinamen und liegt flach unter bilder/train. Gemessen am
2026-08-16: bei 300 von 300 Bildern beider Bestaende blieb die Haltung unbekannt,
und damit konnte der Gegenrichtungsschutz kein einziges Mal greifen.

Der Bildhash sperrt zwar weiterhin die 197 Goldbilder exakt - aber ein ANDERER
Frame aus derselben Goldhaltung hat andere Bytes und kaeme durch. Genau das ist
die Verunreinigung, gegen die der Schutz gebaut wurde.

Dieses Werkzeug legt Bilder so ab, wie die Ernte sie erwartet:
    <ziel>/<Haltung>/<videoname>_<lfd>.jpg
Damit greift der Schutz zum ersten Mal wirklich.

REGELN
- Das Kundenarchiv wird nur GELESEN. Keine Datei darin wird angefasst.
- Das Ziel darf nicht unter dem Kundenbestand liegen (dieselbe Regel wie in
  osd_wahrheit_aus_protokoll.py) - sonst waechst der Kundenbestand still an.
- Gesperrte Haltungen werden VOR dem Extrahieren aussortiert, nicht danach.
- Ein defektes Video beendet den Lauf nicht; es wird gezaehlt.
- Wiederaufnehmbar: Was schon gezogen ist, wird uebersprungen.
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

from PIL import Image

WURZEL = Path(__file__).resolve().parents[2]
for pfad in (WURZEL / "training" / "scripts",):
    if str(pfad) not in sys.path:
        sys.path.insert(0, str(pfad))

from osd_archiv_abdeckung_messung import gleichmaessige_indizes
from osd_schutz import Schutz, lade_schutz
from osd_wahrheit_aus_protokoll import physische_haltung

KUNDEN_WURZEL = Path(r"D:\Haltungen")
ZIEL_STANDARD = Path(r"D:\OSD_Frames")
VIDEO_ENDUNGEN = (".mpg", ".mp4", ".avi", ".mov")

# JPEG statt PNG: Die Zeichenfindung arbeitet auf Helligkeitsunterschieden, nicht
# auf einzelnen Pixelwerten. Qualitaet 92 haelt die Kanten der Ziffern sauber und
# kostet je Bild rund 70 KB statt rund 700 KB.
JPEG_QUALITAET = 92

# Nur ein Bindestrich, links und rechts Ziffern/Buchstaben/Punkte/Unterstriche -
# dieselbe Form wie in osd_ernte.haltung_aus_ordnername.
import re
_HALTUNG_MUSTER = re.compile(r"^[A-Za-z0-9._\u00e4\u00f6\u00fc\u00c4\u00d6\u00dc\u00df]+-"
                             r"[A-Za-z0-9._\u00e4\u00f6\u00fc\u00c4\u00d6\u00dc\u00df]+$")


def haltung_aus_ordnername(name: str) -> str | None:
    name = (name or "").strip()
    return name if _HALTUNG_MUSTER.match(name) else None


def pruefe_ziel(quelle: Path, ziel: Path) -> None:
    """Das Ziel darf nicht im Kundenbestand liegen. Sonst waechst der still an."""
    q = quelle.resolve()
    z = ziel.resolve() if ziel.exists() else Path(os.path.abspath(str(ziel)))
    if z == q or q in z.parents:
        raise SystemExit(
            f"ABBRUCH: Das Ziel liegt im Kundenbestand.\n"
            f"  Kundenbestand: {q}\n  Ziel:          {z}\n"
            "Bitte einen Ordner NEBEN dem Kundenbestand waehlen.")


def video_bilder_lesen(video: Path, proben: int) -> list[Image.Image]:
    """Gleichmaessig verteilte Einzelbilder. Muster aus der Archivmessung."""
    import cv2

    capture = cv2.VideoCapture(str(video))
    try:
        framezahl = int(capture.get(cv2.CAP_PROP_FRAME_COUNT))
        if not capture.isOpened() or framezahl <= 0:
            raise ValueError("Video konnte nicht geoeffnet oder gezaehlt werden")
        bilder: list[Image.Image] = []
        for index in gleichmaessige_indizes(framezahl, proben):
            capture.set(cv2.CAP_PROP_POS_FRAMES, index)
            ok, frame = capture.read()
            if not ok:
                continue
            bilder.append(Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)))
        return bilder
    finally:
        capture.release()


def videos_finden(quelle: Path) -> list[Path]:
    """Videos direkt unter <quelle>/<Haltung>/. Folgt keinen Verknuepfungen."""
    gefunden: list[Path] = []
    for ordner in sorted(p for p in quelle.iterdir() if p.is_dir() and not p.is_symlink()):
        for datei in sorted(ordner.iterdir()):
            if datei.is_file() and not datei.is_symlink() \
                    and datei.suffix.lower() in VIDEO_ENDUNGEN:
                gefunden.append(datei)
    return gefunden


def ziehe_alles(quelle: Path, ziel: Path, schutz: Schutz, proben: int,
                leser=video_bilder_lesen) -> dict[str, int]:
    """Zieht aus jedem freien Video Bilder. Liefert die Zaehlung."""
    zaehler = {"videos": 0, "gesperrt": 0, "ohne_haltung": 0,
               "schon_da": 0, "gezogen": 0, "fehlgeschlagen": 0}

    for video in videos_finden(quelle):
        zaehler["videos"] += 1
        haltung = haltung_aus_ordnername(video.parent.name)

        if haltung is None:
            # Ohne Haltung kann der Gegenrichtungsschutz nicht greifen - dann
            # wird gar nicht erst gezogen, statt ungeschuetztes Material zu schaffen.
            zaehler["ohne_haltung"] += 1
            continue

        if schutz.ist_gesperrt("", haltung):
            zaehler["gesperrt"] += 1
            continue

        ordner = ziel / haltung
        erwartet = [ordner / f"{video.stem}_{i:03d}.jpg" for i in range(proben)]
        if all(p.is_file() for p in erwartet):
            zaehler["schon_da"] += 1
            continue

        try:
            bilder = leser(video, proben)
        except Exception:
            zaehler["fehlgeschlagen"] += 1
            continue

        ordner.mkdir(parents=True, exist_ok=True)
        for lfd, bild in enumerate(bilder):
            pfad = ordner / f"{video.stem}_{lfd:03d}.jpg"
            if pfad.is_file():
                continue
            arbeit = pfad.with_suffix(".jpg.arbeit")
            try:
                bild.convert("RGB").save(arbeit, "JPEG", quality=JPEG_QUALITAET)
                os.replace(arbeit, pfad)
                zaehler["gezogen"] += 1
            finally:
                if arbeit.exists():
                    arbeit.unlink()

    return zaehler


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--quelle", type=Path, default=KUNDEN_WURZEL)
    p.add_argument("--ziel", type=Path, default=ZIEL_STANDARD)
    p.add_argument("--proben", type=int, default=10,
                   help="Bilder je Video, gleichmaessig ueber die Laufzeit verteilt.")
    args = p.parse_args(argv)

    if args.proben <= 0:
        print("ABBRUCH: --proben muss groesser als 0 sein.", file=sys.stderr)
        return 2
    if not args.quelle.is_dir():
        print(f"ABBRUCH: Kundenbestand nicht gefunden: {args.quelle}", file=sys.stderr)
        return 2

    pruefe_ziel(args.quelle, args.ziel)
    schutz = lade_schutz()
    print(f"Gesperrt: {len(schutz.haltungen)} Haltungen (Gold + Reservebestand)")

    zaehler = ziehe_alles(args.quelle, args.ziel, schutz, args.proben)

    print(f"Videos gesehen:            {zaehler['videos']}")
    print(f"  gesperrte Haltung:       {zaehler['gesperrt']}")
    print(f"  ohne erkennbare Haltung: {zaehler['ohne_haltung']}")
    print(f"  schon vorhanden:         {zaehler['schon_da']}")
    print(f"  nicht lesbar:            {zaehler['fehlgeschlagen']}")
    print(f"Bilder gezogen:            {zaehler['gezogen']}")
    print(f"Ziel: {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
