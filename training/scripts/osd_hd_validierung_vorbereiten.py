"""Bereitet eine Pruefrunde des OSD-Meterlesers auf HD-Material vor.

DIAGNOSE. Kundenoriginale werden nur gelesen. Es entsteht derselbe Aufbau wie
bei der SD-Runde, damit der vorhandene Eingabeplatz unveraendert laeuft:
frames/, wahrheit.txt und leser_ergebnisse.json.

Geschichtet ausgewaehlt: gleich viele Bilder je Video, ueber die ganze Laufzeit
verteilt. Eine Zufallsauswahl waere ueberwiegend leichtes Material — derselbe
Auswahlfehler, der die Benchmark-Erweiterung v1 verzerrt hat.

Die Lesung des Programms wird ermittelt und gespeichert, aber der Eingabeplatz
zeigt sie nicht: Sie waere eine Vorgabe statt einer Pruefung.
"""

from __future__ import annotations

import argparse
import json
import random
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Sequence

sys.path.insert(0, str(Path(__file__).resolve().parent))

FFMPEG_STANDARD = Path(
    r"C:\Users\Besitzer\AppData\Local\Microsoft\WinGet\Packages"
    r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-8.0.1-full_build\bin\ffmpeg.exe"
)


def werkzeug(pfad: Path, name: str) -> Path:
    kandidat = pfad.with_name(name)
    if not kandidat.is_file():
        raise SystemExit(f"{name} nicht gefunden: {kandidat}")
    return kandidat


def videodaten(ffprobe: Path, video: Path) -> tuple[float, int, int] | None:
    ergebnis = subprocess.run(
        [str(ffprobe), "-v", "error", "-select_streams", "v:0",
         "-show_entries", "stream=width,height", "-show_entries", "format=duration",
         "-of", "default=nw=1:nk=1", str(video)],
        capture_output=True, text=True)
    zeilen = [z.strip() for z in ergebnis.stdout.splitlines() if z.strip()]
    if len(zeilen) < 3:
        return None
    try:
        # Manche Dateien melden mehrere Videostroeme: dann stehen Breite und
        # Hoehe mehrfach da und die Laufzeit ist die LETZTE Zeile, nicht die
        # dritte. Ein fester Index las sonst die Breite als Sekunden.
        return float(zeilen[-1]), int(zeilen[0]), int(zeilen[1])
    except ValueError:
        return None


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="HD-Pruefrunde des OSD-Meterlesers")
    parser.add_argument("--quelle", type=Path, default=Path(r"D:\Haltungen"))
    parser.add_argument("--ziel", type=Path, default=Path(
        r"C:\KI_BRAIN\training\diagnostics\osd_meter_reader_hd_20260808\validierung"))
    parser.add_argument("--videos", type=int, default=5, help="Anzahl Videos")
    parser.add_argument("--je-video", type=int, default=6, help="Bilder je Video")
    parser.add_argument("--min-breite", type=int, default=1280)
    parser.add_argument("--saat", type=int, default=20260808)
    parser.add_argument("--ffmpeg", type=Path, default=FFMPEG_STANDARD)
    args = parser.parse_args(argv)

    ffmpeg = args.ffmpeg if args.ffmpeg.is_file() else None
    if ffmpeg is None:
        raise SystemExit(f"ffmpeg nicht gefunden: {args.ffmpeg}")
    ffprobe = werkzeug(ffmpeg, "ffprobe.exe")

    alle = sorted(p for p in args.quelle.glob("*/*.mp4") if p.is_file())
    if not alle:
        raise SystemExit(f"Keine Videos unter {args.quelle}")
    random.Random(args.saat).shuffle(alle)

    gewaehlt: list[tuple[Path, float]] = []
    for video in alle:
        if len(gewaehlt) >= args.videos:
            break
        daten = videodaten(ffprobe, video)
        if daten is None:
            continue
        dauer, breite, _ = daten
        if breite < args.min_breite or dauer < 60:
            continue
        gewaehlt.append((video, dauer))
        print(f"  {video.parent.name:<28} {breite}px, {dauer:.0f}s")

    if not gewaehlt:
        raise SystemExit("Kein passendes HD-Video gefunden.")

    if args.ziel.exists():
        shutil.rmtree(args.ziel)
    (args.ziel / "frames").mkdir(parents=True)

    from osd_meter_leser import lese_meter, plausibilisiere_sequenz, rendere_templates

    templates = rendere_templates()
    eintraege: list[dict] = []
    nummer = 0
    for video, dauer in gewaehlt:
        haltung = video.parent.name
        # Ueber die ganze Laufzeit verteilt, Anfang und Ende ausgespart.
        schritte = [dauer * (i + 1) / (args.je_video + 1) for i in range(args.je_video)]
        roh: list[tuple[float, float | None]] = []
        vorlaeufig: list[dict] = []
        for sekunde in schritte:
            nummer += 1
            ziel = args.ziel / "frames" / f"f{nummer:04d}.jpg"
            ergebnis = subprocess.run(
                [str(ffmpeg), "-v", "error", "-ss", f"{sekunde:.2f}", "-i", str(video),
                 "-frames:v", "1", "-q:v", "2", str(ziel)],
                capture_output=True, text=True)
            if ergebnis.returncode != 0 or not ziel.is_file():
                nummer -= 1
                continue
            lesung = lese_meter(ziel, templates)
            roh.append((sekunde, lesung["meter"]))
            vorlaeufig.append({
                "nr": nummer, "datei": ziel.name, "haltung": haltung,
                "sekunde": round(sekunde), "stil": lesung["stil"],
                "roh": lesung["zeichenfolge"], "gelesen": lesung["meter"],
            })

        for eintrag, (_, wert) in zip(vorlaeufig, plausibilisiere_sequenz(roh)):
            eintrag["sequenz"] = wert
        eintraege.extend(vorlaeufig)
        print(f"    {len(vorlaeufig)} Bilder aus {haltung}")

    (args.ziel / "leser_ergebnisse.json").write_text(
        json.dumps(eintraege, indent=1, ensure_ascii=False), encoding="utf-8")
    (args.ziel / "wahrheit.txt").write_text(
        "# Meterstand je Nummer eintragen, z. B. '0042 = 12.5'. Unleserlich: '0042 = ?'\n"
        + "".join(f"{e['nr']:04d} =\n" for e in eintraege),
        encoding="utf-8")

    gelesen = sum(1 for e in eintraege if e["sequenz"] is not None)
    print(f"\n{len(eintraege)} Bilder aus {len(gewaehlt)} Videos vorbereitet.")
    print(f"Der Leser hat auf {gelesen} davon einen Wert — ob er stimmt, sagt erst die Ablesung.")
    print(f"Ziel: {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
