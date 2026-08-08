"""Vorabdurchlauf eines Videos: Bogen-Vorschlaege zum Bestaetigen oder Korrigieren.

PROTOTYP. Die verbindlichen Regeln stehen in C#
(`BendSuggestionAggregator`, `BendSuggestionScanUseCase`); dieses Skript bildet
sie nach, damit der Weg schon vor der Programmanbindung benutzbar ist. Weichen
beide je auseinander, gilt C#.

Ablauf: Bilder in einem ffmpeg-Durchgang holen, je Bild den gepinnten Kandidaten
fragen, den Meterstand aus dem OSD lesen, Treffer zu Stellen zusammenfassen und
je Stelle einen kurzen Clip aus dem Originalvideo schneiden.

Kundenoriginale werden ausschliesslich gelesen. Es wird nichts trainiert,
aktiviert oder in den Goldbestand geschrieben.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Sequence

sys.path.insert(0, str(Path(__file__).resolve().parent))

KANDIDATEN_WURZEL = Path(r"C:\KI_BRAIN\training\models\candidates")
STANDARD_KANDIDAT = "bcc_nc15_seed46_20260808"
VORLAUF_S = 3
NACHLAUF_S = 3
# Verfahrenswerte, nicht modellabhaengig — wie in BendSuggestionOptions.
METER_LUECKE_M = 1.0
ZEIT_LUECKE_S = 3.0
MIN_METER = 0.2
SCHACHT_S = 3.0
# Aufnahmegrenze fuer das einzelne Bild. Der Arbeitspunkt gilt erst fuer die
# fertige Stelle — sonst zerlegt ein Konfidenzeinbruch (0,6 - 0,4 - 0,7) eine
# Stelle in zwei Vorschlaege. Am 2026-08-08 auf zwei Haltungen gemessen.
BODEN_CONF = 0.10
# Groesster Abstand zwischen zwei gelesenen Klammerwerten beim Luecken-
# fuellen. Darueber sind es keine Luecken mehr, sondern Wuesten.
LUECKE_MAX_S = 10.0


def sha256(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1 << 20), b""):
            hasher.update(block)
    return hasher.hexdigest()


def ffmpeg_finden(vorgabe: Path | None) -> Path:
    if vorgabe is not None:
        if not vorgabe.is_file():
            raise SystemExit(f"ffmpeg nicht gefunden: {vorgabe}")
        return vorgabe
    gefunden = shutil.which("ffmpeg")
    if gefunden:
        return Path(gefunden)
    kandidat = Path(
        r"C:\Users\Besitzer\AppData\Local\Microsoft\WinGet\Packages"
        r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
        r"\ffmpeg-8.0.1-full_build\bin\ffmpeg.exe"
    )
    if kandidat.is_file():
        return kandidat
    raise SystemExit("ffmpeg nicht gefunden — bitte mit --ffmpeg angeben.")


def arbeitspunkt_laden(kandidat: str) -> dict:
    """Ohne gemessenen Arbeitspunkt laeuft nichts — fail-closed wie in C#."""
    ordner = KANDIDATEN_WURZEL / kandidat
    punkt = ordner / "workpoint.json"
    gewicht = ordner / "best.pt"
    if not punkt.is_file():
        raise SystemExit(
            f"Fuer {kandidat} ist kein gemessener Arbeitspunkt hinterlegt: {punkt}")
    if not gewicht.is_file():
        raise SystemExit(f"Gewicht fehlt: {gewicht}")

    daten = json.loads(punkt.read_text(encoding="utf-8-sig"))
    if daten.get("candidate_id") != kandidat:
        raise SystemExit("Der Arbeitspunkt gehoert zu einem anderen Kandidaten.")
    echt = sha256(gewicht)
    if str(daten.get("weight_sha256", "")).lower() != echt:
        raise SystemExit(
            "Das Gewicht weicht von dem ab, an dem der Arbeitspunkt gemessen wurde.")
    if not str(daten.get("source") or "").strip():
        raise SystemExit("Der Arbeitspunkt traegt keinen Beleg seiner Herkunft.")

    minimum = float(daten["min_confidence"])
    stark = float(daten["strong_confidence"])
    if not 0.0 < minimum <= 1.0 or not minimum <= stark <= 1.0:
        raise SystemExit("Die Grenzen des Arbeitspunkts sind unbrauchbar.")
    return {"ordner": ordner, "gewicht": gewicht, "sha256": echt,
            "min": minimum, "stark": stark, "beleg": daten["source"]}


def bilder_holen(ffmpeg: Path, video: Path, ziel: Path, fps: float) -> list[tuple[int, float, Path]]:
    if ziel.exists() and any(ziel.iterdir()):
        raise SystemExit(f"Der Zielordner muss leer sein: {ziel}")
    ziel.mkdir(parents=True, exist_ok=True)
    ergebnis = subprocess.run(
        [str(ffmpeg), "-v", "error", "-i", str(video),
         "-vf", f"fps={fps:g}", "-q:v", "3", str(ziel / "f%06d.jpg")],
        capture_output=True, text=True)
    if ergebnis.returncode != 0:
        raise SystemExit(f"ffmpeg ist fehlgeschlagen: {ergebnis.stderr.strip()}")

    bilder = []
    for pfad in sorted(ziel.glob("f*.jpg")):
        nummer = int(pfad.stem[1:])
        bilder.append((nummer, (nummer - 1) / fps, pfad))
    if not bilder:
        raise SystemExit(
            "ffmpeg hat kein Bild erzeugt. Das Video ist vermutlich defekt oder abgebrochen.")
    return bilder


def luecken_fuellen(
    lesungen: list[tuple[float, float | None]],
) -> list[tuple[float, float | None, bool]]:
    """Bildet MeterSequenceGapFiller nach.

    Drei harte Klammern: nur zwischen GELESENEN Werten (eine Schaetzung darf nie
    selbst Klammer sein), nur ueber kurze Luecken, und nie ueber einen
    Richtungswechsel — faellt der Meterstand zwischen zwei Messungen, ist die
    Kamera zurueckgefahren und ein Zwischenwert waere falsch.
    """
    geordnet = sorted(lesungen, key=lambda p: p[0])
    ergebnis: list[tuple[float, float | None, bool]] = [(z, m, False) for z, m in geordnet]

    letzte = -1
    for index, (zeit, meter) in enumerate(geordnet):
        if meter is None:
            continue
        if letzte >= 0 and index - letzte > 1:
            links_zeit, links_meter = geordnet[letzte]
            spanne = zeit - links_zeit
            if 0.0 < spanne <= LUECKE_MAX_S and meter >= links_meter:
                for zwischen in range(letzte + 1, index):
                    anteil = (geordnet[zwischen][0] - links_zeit) / spanne
                    wert = links_meter + (meter - links_meter) * anteil
                    ergebnis[zwischen] = (geordnet[zwischen][0], wert, True)
        letzte = index

    return ergebnis


def zusammenfassen(treffer: list[dict], minimum: float, stark: float) -> list[dict]:
    """Bildet BendSuggestionAggregator nach: Meter entscheidet, Zeit ordnet zu."""
    boden = min(BODEN_CONF, minimum)
    relevant = [t for t in treffer if t["conf"] >= boden]
    # Nur ein GELESENER Meterstand ist als Ort belastbar.
    for t in relevant:
        t["ort_meter"] = None if t.get("geschaetzt") else t["meter"]
    # Schacht-Trimmung: mit Meterstand entscheidet der Meter, sonst die Anfangszeit.
    relevant = [
        t for t in relevant
        if (t["ort_meter"] >= MIN_METER if t["ort_meter"] is not None else t["zeit"] >= SCHACHT_S)
    ]
    relevant.sort(key=lambda t: t["zeit"])

    gruppen: list[dict] = []
    for t in relevant:
        ziel = None
        verortet = [g for g in gruppen if g["meter_min"] is not None]
        if t["ort_meter"] is not None and verortet:
            # Es gibt verortete Stellen: Dann entscheidet ausschliesslich der Meter.
            passend = [(g, abstand(g["meter_min"], g["meter_max"], t["ort_meter"]))
                       for g in verortet]
            passend = [(g, d) for g, d in passend if d <= METER_LUECKE_M]
            if passend:
                ziel = min(passend, key=lambda p: p[1])[0]
        else:
            # Ohne belastbaren Meter — oder solange keine Stelle einen Ort hat —
            # ordnet die Zeit zu. Eine Luecke darf eine Stelle nicht aufspalten.
            passend = [(g, abstand(g["zeit_min"], g["zeit_max"], t["zeit"])) for g in gruppen]
            passend = [(g, d) for g, d in passend if d <= ZEIT_LUECKE_S]
            if passend:
                ziel = min(passend, key=lambda p: p[1])[0]

        if ziel is None:
            gruppen.append({
                "meter_min": t["meter"] if t["conf"] >= minimum else None,
                "meter_max": t["meter"] if t["conf"] >= minimum else None,
                "meter_geschaetzt": bool(t.get("geschaetzt")) and t["conf"] >= minimum,
                "zeit_min": t["zeit"], "zeit_max": t["zeit"],
                "peak_zeit": t["zeit"], "max_conf": t["conf"], "bilder": 1,
            })
            continue

        if t["meter"] is not None and t["conf"] >= minimum:
            ziel["meter_min"] = t["meter"] if ziel["meter_min"] is None else min(ziel["meter_min"], t["meter"])
            ziel["meter_max"] = t["meter"] if ziel["meter_max"] is None else max(ziel["meter_max"], t["meter"])
            if t.get("geschaetzt"):
                ziel["meter_geschaetzt"] = True
        ziel["zeit_min"] = min(ziel["zeit_min"], t["zeit"])
        ziel["zeit_max"] = max(ziel["zeit_max"], t["zeit"])
        if t["conf"] > ziel["max_conf"]:
            ziel["max_conf"] = t["conf"]
            ziel["peak_zeit"] = t["zeit"]
        ziel["bilder"] += 1

    # Erst jetzt entscheidet der Arbeitspunkt — ueber die Stelle als ganze.
    gruppen = [g for g in gruppen if g["max_conf"] >= minimum]
    for g in gruppen:
        g["stufe"] = "stark" if g["max_conf"] >= stark else "schwach"
    gruppen.sort(key=lambda g: (g["meter_min"] if g["meter_min"] is not None else 1e9, g["peak_zeit"]))
    return gruppen


def abstand(minimum, maximum, wert) -> float:
    if minimum is None or maximum is None:
        return 1e9
    if wert < minimum:
        return minimum - wert
    return wert - maximum if wert > maximum else 0.0


def clip_schneiden(ffmpeg: Path, video: Path, von: float, bis: float, ziel: Path) -> bool:
    dauer = max(1.0, bis - von)
    ergebnis = subprocess.run(
        [str(ffmpeg), "-v", "error", "-y", "-ss", f"{max(0.0, von):.2f}",
         "-i", str(video), "-t", f"{dauer:.2f}", "-an",
         "-c:v", "libx264", "-preset", "veryfast", "-crf", "23",
         "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(ziel)],
        capture_output=True, text=True)
    return ergebnis.returncode == 0 and ziel.is_file() and ziel.stat().st_size > 0


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Bogen-Vorabdurchlauf ueber ein Video")
    parser.add_argument("--video", type=Path, required=True)
    parser.add_argument("--haltung", default="")
    parser.add_argument("--kandidat", default=STANDARD_KANDIDAT)
    parser.add_argument("--fps", type=float, default=1.0)
    parser.add_argument("--ffmpeg", type=Path, default=None)
    parser.add_argument("--ziel", type=Path,
                        default=Path(r"C:\KI_BRAIN\training\diagnostics\bcc_copilot_durchlaeufe"))
    args = parser.parse_args(argv)

    if not args.video.is_file():
        raise SystemExit(f"Video nicht gefunden: {args.video}")

    punkt = arbeitspunkt_laden(args.kandidat)
    ffmpeg = ffmpeg_finden(args.ffmpeg)
    haltung = args.haltung or args.video.stem
    lauf = args.ziel / f"{haltung}_{punkt['sha256'][:12]}"
    if lauf.exists():
        shutil.rmtree(lauf)
    (lauf / "clips").mkdir(parents=True)

    print(f"Kandidat     {args.kandidat}")
    print(f"Arbeitspunkt conf >= {punkt['min']:.2f}, stark ab {punkt['stark']:.2f}")
    print(f"Beleg        {punkt['beleg']}")

    import time
    from ultralytics import YOLO
    from osd_meter_leser import lese_meter, plausibilisiere_sequenz, rendere_templates

    gestartet = time.perf_counter()
    bilder = bilder_holen(ffmpeg, args.video, lauf / "frames", args.fps)
    print(f"{len(bilder)} Bilder extrahiert")

    modell = YOLO(str(punkt["gewicht"]))
    templates = rendere_templates()
    roh_meter: list[tuple[float, float | None]] = []
    conf_je_zeit: dict[float, float] = {}
    for _nummer, zeit, pfad in bilder:
        ergebnis = modell.predict(source=str(pfad), conf=min(BODEN_CONF, punkt["min"]), imgsz=1280,
                                  classes=[14], device=0, verbose=False)[0]
        roh_meter.append((zeit, lese_meter(pfad, templates)["meter"]))
        if ergebnis.boxes is not None and len(ergebnis.boxes) > 0:
            conf_je_zeit[zeit] = float(max(b.conf[0].item() for b in ergebnis.boxes))

    # Erst die Sequenz plausibilisieren (unmoegliche Werte raus), dann kurze
    # Luecken fuellen. Gefuellte Werte ordnen zu, setzen aber keinen Ort.
    geprueft = plausibilisiere_sequenz(roh_meter)
    gelesen = sum(1 for _, m in geprueft if m is not None)
    gefuellt_meter = luecken_fuellen(geprueft)

    treffer = [
        {"zeit": zeit, "meter": meter, "geschaetzt": geschaetzt, "conf": conf_je_zeit[zeit]}
        for zeit, meter, geschaetzt in gefuellt_meter
        if zeit in conf_je_zeit
    ]

    stellen = zusammenfassen(treffer, punkt["min"], punkt["stark"])
    dauer = time.perf_counter() - gestartet
    abdeckung = gelesen / max(1, len(bilder))
    ergaenzt = sum(1 for _, m, g in gefuellt_meter if g and m is not None)
    print(f"Meterstand gelesen auf {gelesen}/{len(bilder)} Bildern ({abdeckung:.0%})"
          + (f", {ergaenzt} kurze Luecken gefuellt" if ergaenzt else ""))
    print(f"{len(stellen)} Vorschlaege in {dauer:.0f} s")

    for index, stelle in enumerate(stellen, start=1):
        name = f"vorschlag_{index:03d}.mp4"
        clip_schneiden(ffmpeg, args.video,
                       stelle["zeit_min"] - VORLAUF_S, stelle["zeit_max"] + NACHLAUF_S,
                       lauf / "clips" / name)
        stelle["nummer"] = index
        stelle["clip"] = name

    shutil.rmtree(lauf / "frames", ignore_errors=True)
    inhalt = {
        "schema_version": 1,
        "zweck": "Vorabdurchlauf: Bogen-Vorschlaege zum Bestaetigen oder Korrigieren",
        "haltung": haltung,
        "video": str(args.video),
        "kandidat": args.kandidat,
        "gewicht_sha256": punkt["sha256"],
        "min_confidence": punkt["min"],
        "strong_confidence": punkt["stark"],
        "arbeitspunkt_beleg": punkt["beleg"],
        "bilder": len(bilder),
        "meter_abdeckung": round(abdeckung, 4),
        "laufzeit_s": round(dauer, 1),
        "vorschlaege": stellen,
    }
    ziel = lauf / "vorschlaege.json"
    temp = ziel.with_suffix(".json.tmp")
    temp.write_text(json.dumps(inhalt, indent=2, ensure_ascii=False), encoding="utf-8")
    temp.replace(ziel)
    print(f"\nDurchgang: {lauf}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
