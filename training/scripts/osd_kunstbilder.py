"""Erzeugt kuenstliche OSD-Meteranzeigen mit exakt bekannter Wahrheit.

WOZU
Die Lehrer-Ernte lehrt nur, was der heutige Leser schon kann. Die Stile, an denen
er scheitert, kommen dort gar nicht vor. Kuenstliche Anzeigen schliessen genau
diese Luecke - und ihre Zeichenboxen sind per Konstruktion exakt.

STILE
Abgeleitet aus der menschlichen Sichtung von 40 Haltungen (2026-08-14):
  Lage       38 unten rechts, 2 unten links, 0 oben
  Polaritaet 18 hell auf dunkel, 18 dunkel auf hell, 4 andere
  Farbe      20 weiss/grau, 7 gelb, 13 andere
  Format     19 mit Praefix/fuehrenden Nullen, 15 mit Einheit, 6 ohne Einheit
Die Stichprobe ist klein: Sie belegt mehrere Hauptstile, aber keine exakten
Archivanteile. Die Verteilung hier ist deshalb bewusst breiter gezogen.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import random
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

WURZEL = Path(__file__).resolve().parents[2]
if str(WURZEL / "sidecar") not in sys.path:
    sys.path.insert(0, str(WURZEL / "sidecar"))
if str(WURZEL / "training" / "scripts") not in sys.path:
    sys.path.insert(0, str(WURZEL / "training" / "scripts"))

from sidecar import osd_meter
from osd_ernte import als_labeltext

AUSSCHNITT = (274, 92)   # entspricht der Zone unten rechts eines SD-Bildes


@dataclass(frozen=True)
class Stil:
    name: str
    vordergrund: tuple[int, int, int]
    hintergrund: tuple[int, int, int]


STILE = (
    Stil("weiss_auf_dunkel", (240, 240, 240), (16, 16, 16)),
    Stil("dunkel_auf_weiss", (20, 20, 20), (235, 235, 235)),
    Stil("gelb_auf_dunkel", (250, 220, 60), (14, 14, 20)),
    Stil("gruen_auf_dunkel", (120, 240, 140), (10, 14, 10)),
)

VORLAGEN = (
    "LZ{n}: {wert}m",
    "LZ{n}:{wert}m",
    "L{n} {wert}m",
    "{wert}m",
    "{wert}",
    "LZ{n}: {wert}",
)


@dataclass(frozen=True)
class Kunstbild:
    bild: Image.Image
    zeichen: list[tuple[int, float, float, float, float]]
    text: str
    meter: float
    stil_name: str


def _schriftart(groesse: int) -> ImageFont.FreeTypeFont:
    for name in ("consola.ttf", "cour.ttf", "arial.ttf", "DejaVuSansMono.ttf"):
        try:
            return ImageFont.truetype(name, groesse)
        except OSError:
            continue
    return ImageFont.load_default(groesse)


def _ist_hell(farbe: tuple[int, int, int]) -> bool:
    """True fuer helle Farben - dunkler Text braucht dann einen Kontrastkasten."""
    return sum(farbe) / 3 >= 128


def _video_hintergrund(groesse: tuple[int, int], saat: int) -> Image.Image:
    """Ungleichmaessiger, dunkler Ersatzhintergrund fuer die Rohrwand.

    Sichtpruefung (Schritt 5, erste Fassung): Ein einfarbiger Kasten ueber dem
    GANZEN Ausschnitt sieht neben echten OSD-Crops (Rohrwandtextur ausserhalb
    eines schmalen Anzeigekastens, siehe C:\\KI_BRAIN\\eval_set\\osd\\*) sofort
    kuenstlich aus. Kein Fotorealismus noetig - nur Rauschen und ein leichtes
    Gefaelle statt einer toten Flaeche, damit der Zeichenfinder nicht lernt,
    dass "Text" gleichbedeutend mit "einzige Flaeche im Bild" ist.

    Fix-Runde 1 (2026-08-15): `saat` ist laut Schnittstelle ein beliebiger int,
    auch negativ - `np.random.default_rng` verlangt aber einen nicht-negativen
    Startwert. Die Maskierung auf 32 Bit macht die Ableitung vorzeichenunabhaengig,
    ohne `erzeuge()` selbst eine stille Nichtnegativitaetsbedingung aufzuerlegen.
    """
    breite, hoehe = groesse
    generator = np.random.default_rng(saat & 0xFFFFFFFF)
    grundton = generator.uniform(18.0, 46.0, size=3)
    grundton += generator.uniform(-6.0, 10.0)  # leichte Warm-/Kuehlverschiebung
    rauschen = generator.normal(0.0, 9.0, size=(hoehe, breite, 1))
    gefaelle = np.linspace(-10.0, 10.0, breite, dtype=np.float64)
    gefaelle = np.tile(gefaelle, (hoehe, 1))[:, :, None]
    arr = grundton[None, None, :] + rauschen + gefaelle
    arr = np.clip(arr, 0, 255).astype("uint8")
    return Image.fromarray(arr, mode="RGB")


def erzeuge(saat: int, hintergrund: Image.Image | None = None) -> Kunstbild:
    """Ein kuenstlicher Ausschnitt. Gleiche Saat, gleiches Ergebnis.

    Fix-Runde 2 (2026-08-15): CPython nimmt bei `random.Random(int)` intern den
    Betrag des Startwerts (`random.Random(-3).getstate() ==
    random.Random(3).getstate()`) - nicht offensichtlich, aber belegt. Dadurch
    liefern `erzeuge(-n)` und `erzeuge(n)` denselben Stil, Text und dieselben
    Zeichenboxen (nur der Hintergrund unterscheidet sich, der laeuft ueber den
    separat maskierten Startwert in `_video_hintergrund`). Bewusst dokumentiertes
    Verhalten dieser reinen Funktion, siehe
    test_negativer_saat_kollidiert_mit_positivem_gleichen_betrags - deshalb
    weist main() negative `--saat` ab, statt still Dubletten zu erzeugen.
    """
    zufall = random.Random(saat)
    stil = zufall.choice(STILE)

    meter = round(zufall.uniform(0.0, 99.9), 1)
    wert = f"{meter:.1f}"
    if zufall.random() < 0.3:
        wert = wert.zfill(6)          # fuehrende Nullen, z.B. 0009.4
    text = zufall.choice(VORLAGEN).format(n=zufall.choice("123"), wert=wert)

    if hintergrund is None:
        # Kein flacher Volltonkasten mehr (siehe _video_hintergrund) - reale
        # Crops zeigen ausserhalb der Anzeige immer Rohrwandtextur.
        bild = _video_hintergrund(AUSSCHNITT, saat)
    else:
        bild = hintergrund.convert("RGB").resize(AUSSCHNITT)

    groesse = zufall.choice((16, 18, 20, 24, 28, 34))
    schrift = _schriftart(groesse)
    zeichner = ImageDraw.Draw(bild)

    laenge = zeichner.textlength(text, font=schrift)
    x = max(2.0, min(AUSSCHNITT[0] - laenge - 2.0,
                     zufall.uniform(2.0, AUSSCHNITT[0] - laenge - 2.0)))
    y = zufall.uniform(2.0, max(3.0, AUSSCHNITT[1] - groesse - 4.0))

    if _ist_hell(stil.hintergrund):
        # Dunkler Text braucht einen hellen Kontrastkasten (WinCan-typisches
        # Anzeigebanner) - echte Crops zeigen genau das, nie eine volltonig
        # gefuellte gesamte Zone. Nur so weit wie der Text plus Rand, nicht
        # der ganze Ausschnitt.
        puffer = max(3.0, groesse * 0.18)
        kasten = (
            max(0.0, x - puffer), max(0.0, y - puffer),
            min(float(AUSSCHNITT[0]), x + laenge + puffer),
            min(float(AUSSCHNITT[1]), y + groesse + puffer),
        )
        zeichner.rectangle(kasten, fill=stil.hintergrund)

    zeichen: list[tuple[int, float, float, float, float]] = []
    laufend = x
    for buchstabe in text:
        breite = zeichner.textlength(buchstabe, font=schrift)
        if buchstabe != " ":
            zeichner.text((laufend, y), buchstabe, font=schrift,
                          fill=stil.vordergrund)
            klasse = osd_meter.ZEICHEN.find(buchstabe)
            if klasse >= 0:
                # Fix-Runde 1 (Aufgabe 2): NICHT die Vorschubzelle (laufend..
                # laufend+breite, y..y+groesse) als Box nehmen - die ist fuer
                # jedes Zeichen (Ziffer wie Punkt) gleich hoch und liefert dem
                # Erntepfad (echte Connected-Component-Boxen, deutlich kleiner
                # fuer '.') zwei widerspruechliche Vorstellungen derselben
                # Klasse. textbbox() misst stattdessen die tatsaechlich
                # gezeichnete Tinte dieses einen Zeichens.
                x0, y0, x1, y1 = zeichner.textbbox(
                    (laufend, y), buchstabe, font=schrift)
                if x1 > x0 and y1 > y0:
                    zeichen.append((
                        klasse,
                        ((x0 + x1) / 2) / AUSSCHNITT[0],
                        ((y0 + y1) / 2) / AUSSCHNITT[1],
                        (x1 - x0) / AUSSCHNITT[0],
                        (y1 - y0) / AUSSCHNITT[1],
                    ))
        laufend += breite

    if zufall.random() < 0.5:
        bild = bild.filter(ImageFilter.GaussianBlur(zufall.uniform(0.2, 0.9)))

    return Kunstbild(bild, zeichen, text, meter, stil.name)


# ---------------------------------------------------------------------------
# CLI: kuenstliche Bilder erzeugen und veroeffentlichen.
#
# Der Kern oben (erzeuge) ist rein funktional und dateisystemfrei. Alles
# unterhalb ist duenne Verdrahtung: Bilder schreiben, eintraege.json bauen,
# Zusammenfassung drucken. Nicht Teil der urspruenglichen Aufgabenskizze -
# ergaenzt (Ruling zu Aufgabe 3), weil ohne einen Schreibweg keine Eintraege
# fuer Aufgabe 4 entstehen wuerden. Wiederverwendet als_labeltext() aus
# osd_ernte.py statt das YOLO-Zeilenformat ein zweites Mal zu schreiben.
# ---------------------------------------------------------------------------

BILD_ENDUNGEN = {".jpg", ".jpeg", ".png"}


def kunst_id(saat: int) -> str:
    """Kurze, stabile Dateikennung aus der Saat - reproduzierbar, kollisionsfrei."""
    return f"kunst_{saat:08d}"


def eintrag_erzeugen(kennung: str, kunst: Kunstbild, bild_sha256: str) -> dict:
    """Baut den Eintrag fuer eintraege.json (Schema osd_kunstbilder_v1).

    `haltung` ist immer None: Ein kuenstliches Bild gehoert zu keiner Haltung.
    """
    return {
        "id": kennung,
        "bild_sha256": bild_sha256,
        "haltung": None,
        "text": kunst.text,
        "meter": kunst.meter,
        "stil": kunst.stil_name,
    }


def _png_bytes(bild: Image.Image) -> bytes:
    puffer = io.BytesIO()
    bild.save(puffer, format="PNG")
    return puffer.getvalue()


def _hintergruende_laden(ordner: Path) -> list[Path]:
    return sorted(
        p for p in ordner.rglob("*")
        if p.is_file() and p.suffix.lower() in BILD_ENDUNGEN)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ziel", type=Path, required=True,
                        help="Ausgabeordner fuer Bilder, Labels und eintraege.json")
    parser.add_argument("--anzahl", type=int, default=2000,
                        help="Anzahl zu erzeugender Bilder (Default 2000)")
    parser.add_argument("--saat", type=int, default=0,
                        help="Basis-Saat; erzeugt Saaten saat..saat+anzahl-1")
    parser.add_argument("--hintergrund-ordner", type=Path, default=None,
                        help="Optionaler Ordner mit echten Ausschnitten als Hintergrund")
    args = parser.parse_args(argv)

    if args.saat < 0:
        # Fix-Runde 2 (2026-08-15): erzeuge(-n) liefert wegen CPythons
        # abs()-Behandlung von int-Startwerten denselben Text und dieselben
        # Zeichenboxen wie erzeuge(n) (siehe Kommentar dort). Ein negativer
        # Basiswert wuerde bei ausreichend grossem --anzahl still Bildpaare
        # mit identischem Etikett erzeugen. Ein Datensatzerzeuger hat fuer
        # negative Startwerte keinen Verwendungszweck - klar abweisen statt
        # still Dubletten zu produzieren.
        parser.error(
            "--saat darf nicht negativ sein: erzeuge(-n) liefert denselben "
            "Text und dieselben Zeichenboxen wie erzeuge(n) und wuerde stille "
            "Dubletten im Datensatz erzeugen.")

    if args.anzahl <= 0:
        raise SystemExit("--anzahl muss positiv sein.")

    hintergruende: list[Path] = []
    if args.hintergrund_ordner is not None:
        if not args.hintergrund_ordner.is_dir():
            raise SystemExit(f"Hintergrundordner fehlt: {args.hintergrund_ordner}")
        hintergruende = _hintergruende_laden(args.hintergrund_ordner)
        if not hintergruende:
            raise SystemExit(f"Keine Hintergrundbilder unter {args.hintergrund_ordner}")

    bilder_ordner = args.ziel / "bilder"
    labels_ordner = args.ziel / "labels"
    bilder_ordner.mkdir(parents=True, exist_ok=True)
    labels_ordner.mkdir(parents=True, exist_ok=True)

    eintraege: list[dict] = []
    stil_zaehler: dict[str, int] = {s.name: 0 for s in STILE}

    for i in range(args.anzahl):
        saat = args.saat + i
        hintergrund = None
        if hintergruende:
            # Deterministisch aus der Saat gewaehlt, kein separater Zufallszug.
            hintergrund = Image.open(
                hintergruende[saat % len(hintergruende)]).convert("RGB")

        kunst = erzeuge(saat=saat, hintergrund=hintergrund)

        kennung = kunst_id(saat)
        png = _png_bytes(kunst.bild)
        (bilder_ordner / f"{kennung}.png").write_bytes(png)
        (labels_ordner / f"{kennung}.txt").write_text(
            als_labeltext(kunst.zeichen), encoding="utf-8")

        bild_sha256 = hashlib.sha256(png).hexdigest()
        eintraege.append(eintrag_erzeugen(kennung, kunst, bild_sha256))
        stil_zaehler[kunst.stil_name] += 1

    dokument = {"schema": "osd_kunstbilder_v1", "eintraege": eintraege}
    ziel_json = args.ziel / "eintraege.json"
    # Atomar: erst in eine Temp-Datei im selben Ordner schreiben, dann
    # os.replace() - ein Absturz mittendrin hinterlaesst kein halbes JSON.
    temp = ziel_json.with_name(f".{ziel_json.name}.tmp-{uuid.uuid4().hex}")
    temp.write_text(json.dumps(dokument, indent=2, ensure_ascii=False), encoding="utf-8")
    os.replace(temp, ziel_json)

    print(f"Erzeugt: {len(eintraege)}")
    for name, anzahl in stil_zaehler.items():
        print(f"  {name}: {anzahl}")
    print(f"Bestand: {ziel_json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
