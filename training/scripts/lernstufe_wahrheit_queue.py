"""Schneidet je Video EINEN Ausschnitt und fragt, ob der Befund darin zu sehen ist.

WOZU
Die Vorschlagspruefung misst nur, wie oft das Modell recht hat, wenn es meldet.
Sie kann nicht sagen, wie oft es schweigt, obwohl etwas da ist. Dafuer braucht
es eine vom Modell unabhaengige Wahrheit.

Fuer Klassen mit fester Lage im Video geht das billig: Ein Rohranfang liegt am
Videoanfang, ein Rohrende am Videoende. Ein einziger Ausschnitt je Video, eine
einzige Frage — und der Mensch muss nicht das ganze Video schauen.

Fuer Klassen ohne feste Lage (Anschluss, Riss, Wurzeln) taugt dieser Weg NICHT.
Dort sagt ein Ausschnitt nichts ueber den Rest des Videos.

Das Modell ist an keiner Stelle beteiligt: weder an der Auswahl noch an der
Anzeige. Kundenoriginale werden ausschliesslich gelesen.
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

SCRIPT = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT))

from bcc_lernstufe_aus_protokoll import FFMPEG


def videodauer(video: Path) -> float | None:
    lauf = subprocess.run(
        [str(FFMPEG.with_name("ffprobe.exe")), "-v", "error", "-show_entries",
         "format=duration", "-of", "csv=p=0", str(video)], capture_output=True, text=True)
    try:
        return float(lauf.stdout.strip())
    except ValueError:
        return None


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--auswahl", type=Path, required=True)
    parser.add_argument("--ziel", type=Path, required=True)
    parser.add_argument("--frage", required=True)
    parser.add_argument("--lage", choices=("anfang", "ende"), default="anfang")
    parser.add_argument("--sekunden", type=float, default=30.0)
    args = parser.parse_args(argv)

    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits: {args.ziel}")

    auswahl = json.loads(args.auswahl.read_text(encoding="utf-8-sig"))
    videos = auswahl["auswahl"]

    arbeit = args.ziel.with_name(f".{args.ziel.name}.arbeit")
    shutil.rmtree(arbeit, ignore_errors=True)
    (arbeit / "bilder").mkdir(parents=True)
    (arbeit / "clips").mkdir()

    faelle, aufloesung = [], []
    for nr, v in enumerate(sorted(videos, key=lambda x: x["haltung"]), start=1):
        video = Path(v["video"])
        if args.lage == "anfang":
            von = 0.0
        else:
            dauer = videodauer(video)
            if dauer is None:
                # Ein unlesbares Video ist kein "nichts zu sehen" — es faellt raus.
                print(f"  {v['haltung']}: Dauer nicht lesbar, uebersprungen", flush=True)
                continue
            von = max(0.0, dauer - args.sekunden)

        clip = arbeit / "clips" / f"{nr:03d}.mp4"
        lauf = subprocess.run(
            [str(FFMPEG), "-v", "error", "-y", "-ss", f"{von:.2f}", "-i", str(video),
             "-t", f"{args.sekunden:.2f}", "-an", "-c:v", "libx264", "-preset", "veryfast",
             "-crf", "23", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(clip)],
            capture_output=True, text=True)
        if lauf.returncode != 0 or not clip.is_file():
            print(f"  {v['haltung']}: Clip fehlgeschlagen, uebersprungen", flush=True)
            continue

        # Ein Standbild aus der Mitte des Ausschnitts, damit Taste 0 etwas zeigt.
        bild = arbeit / "bilder" / f"{nr:03d}.jpg"
        subprocess.run(
            [str(FFMPEG), "-v", "error", "-y", "-ss", f"{args.sekunden / 2:.2f}",
             "-i", str(clip), "-frames:v", "1", "-q:v", "3", str(bild)], capture_output=True)
        if not bild.is_file():
            subprocess.run([str(FFMPEG), "-v", "error", "-y", "-i", str(clip),
                            "-frames:v", "1", "-q:v", "3", str(bild)], capture_output=True)

        daten = bild.read_bytes()
        faelle.append({"nummer": nr, "bild": f"bilder/{nr:03d}.jpg",
                       "bild_sha256": hashlib.sha256(daten).hexdigest(),
                       "clip": f"clips/{nr:03d}.mp4"})
        aufloesung.append({"nummer": nr, "haltung": v["haltung"], "video": str(video),
                           "ausschnitt_von_s": round(von, 2), "sekunden": args.sekunden})
        print(f"  [{nr}/{len(videos)}] {v['haltung']}", flush=True)

    if not faelle:
        raise SystemExit("Kein einziger Ausschnitt erzeugt.")

    queue = {
        "schema": "lernstufe_wahrheit_v1",
        "zweck": ("Vom Modell unabhaengige Wahrheit je Video. Grundlage fuer den Recall: "
                  "Wie oft schweigt das Modell, obwohl etwas da ist?"),
        "kein_modell_beteiligt": True,
        "grenze": ("Gilt nur fuer Klassen mit fester Lage im Video. Fuer Anschluss, Riss "
                   "oder Wurzeln sagt ein Ausschnitt nichts ueber den Rest."),
        "videoauswahl": str(args.auswahl),
        "lage": args.lage,
        "sekunden": args.sekunden,
        "videos": len(faelle),
        "frage": args.frage,
        "urteile": [
            {"wert": "sichtbar", "beschriftung": "Ja, zu sehen", "taste": "1"},
            {"wert": "nicht_sichtbar", "beschriftung": "Nein, nicht zu sehen", "taste": "2"},
            {"wert": "unsicher", "beschriftung": "Unsicher", "taste": "3"},
        ],
        "faelle": faelle,
        "aufloesung": aufloesung,
    }
    text = json.dumps(queue, indent=1, ensure_ascii=False)
    (arbeit / "queue.json").write_bytes(text.encode("utf-8"))
    (arbeit / "queue.sha256").write_bytes(
        (hashlib.sha256(text.encode("utf-8")).hexdigest() + "\n").encode("utf-8"))
    arbeit.rename(args.ziel)
    print(f"\n{len(faelle)} Ausschnitte je {args.sekunden:g} s ({args.lage})")
    print(f"Queue: {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
