"""Sammelt die eigenen Fehlalarme eines Einordners als neue Gegenbeispiele.

WARUM
Ein Lernbestand aus Protokollstellen enthaelt je Haltung zwei bis drei
Gegenbeispiele. Ein echtes Video hat siebenhundert Bilder. Das Modell kennt
die Bandbreite dessen nicht, was in einem Rohr sonst vorbeikommt — und
schlaegt an. Am 2026-08-11 an 80 blind geprueften Clips gemessen: 89 %
Precision auf dem Testteil, 23 % im Video, 8 Fehlalarme je Video.

WAS HIER PASSIERT
Das Modell laeuft ueber TRAININGS-Videos. Jede Stelle, an der es anschlaegt
obwohl das Protokoll dort nichts nennt, wird als Gegenbeispiel eingesammelt.
Das sind genau die Bilder, an denen es scheitert.

DIE GRENZE DIESES WEGES
Das Protokoll ist unvollstaendig. Ein Teil dieser "Fehlalarme" sind echte,
nur nicht codierte Befunde — beim Anschluss in der Sichtpruefung 3 von 30.
Sie kommen dann als falsches Gegenbeispiel herein. Deshalb gelten dieselben
Schutzregeln wie beim Nahfeld: Abstand zu jeder Fundstelle, und Haltungen mit
duennem Protokoll bleiben draussen. Der Rest ist gemessenes Restrisiko und
gehoert vor dem Training in eine Stichprobe.

Test- und Validierungshaltungen werden NIE angefasst; sonst misst das Modell
sich selbst. Kundenoriginale werden ausschliesslich gelesen.
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
sys.path.insert(0, str(SCRIPT.parent / "vsa_classifier"))

from bcc_lernstufe_aus_protokoll import (
    DUENNES_PROTOKOLL_METER, FFMPEG, NAHFELD_ABSTAND_S, SICHERE_ZUORDNUNG,
    code_ist_lesbar, gesperrte_laden, haltungsvarianten, physische_haltung,
    sha256_datei, split_fuer,
)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scan", type=Path, required=True)
    parser.add_argument("--gewicht", type=Path, required=True)
    parser.add_argument("--klasse", required=True)
    parser.add_argument("--saat", required=True, help="Dieselbe Saat wie der Lernbestand")
    parser.add_argument("--ziel", type=Path, required=True)
    parser.add_argument("--schwelle", type=float, default=0.50)
    parser.add_argument("--imgsz", type=int, default=640)
    parser.add_argument("--fps", type=float, default=1.0)
    parser.add_argument("--videos", type=int, default=60)
    parser.add_argument("--je-video", type=int, default=12,
                        help="Hoechstzahl eingesammelter Fehlerbilder je Video")
    parser.add_argument("--ohne-duennes-protokoll", action="store_true",
                        help="Bei BCD/BCE noetig: Dort nennt JEDE Haltung genau einen Befund "
                             "am Rohranfang — die Regel wuerde sonst alle Videos sperren.")
    parser.add_argument("--nur-mitte", type=float, default=0.0,
                        help="Nur Fehlalarme einsammeln, die weiter als N Sekunden von "
                             "BEIDEN Videoenden entfernt sind. Fuer Rohranfang/Rohrende: "
                             "Am Anfang steht der echte Befund, am Ende sein Spiegelbild — "
                             "beides waere ein falsches Gegenbeispiel.")
    args = parser.parse_args(argv)

    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits: {args.ziel}")

    scan = json.loads(args.scan.read_text(encoding="utf-8-sig"))
    gesperrt, _ = gesperrte_laden()
    unlesbar = {e["haltung"] for e in scan["ergebnisse"]
                for p in (e.get("positionen") or []) if not code_ist_lesbar(p.get("code"))}

    quellen = []
    for e in scan["ergebnisse"]:
        h = e["haltung"]
        if h in unlesbar or (haltungsvarianten(h) & gesperrt):
            continue
        # NUR Trainingshaltungen. Validierung und Test bleiben unberuehrt.
        if split_fuer(h, args.saat) != "train":
            continue
        befunde = e.get("positionen") or []
        if (not args.ohne_duennes_protokoll and befunde and len(befunde) == 1
                and (befunde[0].get("meter_start") or 0.0) < DUENNES_PROTOKOLL_METER):
            continue
        if befunde:
            pfade = {p["video_path"] for p in befunde
                     if p.get("video_path") and p.get("video_match") in SICHERE_ZUORDNUNG}
            zeiten = [float(p["video_counter_seconds"]) for p in befunde
                      if p.get("video_counter_seconds") is not None]
        else:
            pfade = set(e.get("videos") or [])
            zeiten = []
            if not e.get("pdfs_ok") or e.get("pdfs_lesefehler") or e.get("pdfs_fehler"):
                continue
        if len(pfade) != 1:
            continue
        video = Path(next(iter(pfade)))
        if not video.is_file():
            continue
        quellen.append({"haltung": h, "video": str(video), "sperrzeiten": zeiten})

    quellen.sort(key=lambda x: x["haltung"])
    import random
    random.Random(f"fehler|{args.saat}").shuffle(quellen)
    quellen = quellen[:args.videos]
    print(f"{len(quellen)} Trainingsvideos werden abgesucht\n", flush=True)

    from PIL import Image
    from ultralytics import YOLO

    from nocrop_patch import letterbox_pil

    modell = YOLO(str(args.gewicht))
    index = next((i for i, n in modell.names.items() if n == args.klasse), None)
    if index is None:
        raise SystemExit(f"Klasse {args.klasse!r} fehlt im Modell: {modell.names}")

    arbeit = args.ziel.with_name(f".{args.ziel.name}.arbeit")
    shutil.rmtree(arbeit, ignore_errors=True)
    frames = arbeit / ".frames"
    for teil in ("train", "validation"):
        (arbeit / teil).mkdir(parents=True)

    eintraege, gesehen = [], set()
    for i, q in enumerate(quellen, start=1):
        shutil.rmtree(frames, ignore_errors=True)
        frames.mkdir()
        lauf = subprocess.run(
            [str(FFMPEG), "-v", "error", "-y", "-i", q["video"], "-vf", f"fps={args.fps:g}",
             "-q:v", "3", str(frames / "f%06d.jpg")], capture_output=True, text=True)
        if lauf.returncode != 0:
            # Kein stiller Ausfall: ein unlesbares Video ist kein "nichts gefunden".
            print(f"  [{i}/{len(quellen)}] {q['haltung']}: Video unlesbar, uebersprungen", flush=True)
            continue

        bilder = sorted(frames.glob("f*.jpg"))
        dauer_video = len(bilder) / args.fps
        fehler = []
        for j, bild in enumerate(bilder):
            sekunde = j / args.fps
            if any(abs(sekunde - t) < NAHFELD_ABSTAND_S for t in q["sperrzeiten"]):
                continue
            if args.nur_mitte:
                # Die Raender gehoeren nicht in die Gegenbeispiele: vorn der echte
                # Rohranfang, hinten das Rohrende, das genauso aussieht.
                if sekunde < args.nur_mitte or sekunde > dauer_video - args.nur_mitte:
                    continue
            elif sekunde < 5.0:          # sonst nur den Schacht am Anfang meiden
                continue
            with Image.open(bild) as roh:
                vorbereitet = letterbox_pil(roh, args.imgsz)
            wert = float(modell.predict(source=vorbereitet, imgsz=args.imgsz,
                                        verbose=False)[0].probs.data[index])
            if wert >= args.schwelle:
                fehler.append((wert, sekunde, bild))

        # Die staerksten Fehler zuerst — sie sind die lehrreichsten.
        fehler.sort(key=lambda x: -x[0])
        genommen = 0
        for wert, sekunde, bild in fehler:
            if genommen >= args.je_video:
                break
            daten = bild.read_bytes()
            h = hashlib.sha256(daten).hexdigest()
            if h in gesehen:
                continue
            gesehen.add(h)
            # Der Split folgt weiter der physischen Haltung. Ein Fehlerbild aus
            # einer Trainingshaltung bleibt Trainingsmaterial.
            teil = "train" if genommen % 5 else "validation"
            name = f"{''.join(c if c.isalnum() else '_' for c in q['haltung'])}_{h[:16]}.jpg"
            (arbeit / teil / name).write_bytes(daten)
            eintraege.append({"haltung": q["haltung"], "physische_haltung": physische_haltung(q["haltung"]),
                              "split": teil, "sekunde": round(sekunde, 1), "konfidenz": round(wert, 4),
                              "bild": f"{teil}/{name}", "bild_sha256": h})
            genommen += 1
        print(f"  [{i}/{len(quellen)}] {q['haltung']:<24}{len(bilder):>5} Bilder, "
              f"{len(fehler):>4} Fehlalarme, {genommen} genommen", flush=True)

    shutil.rmtree(frames, ignore_errors=True)
    if not eintraege:
        raise SystemExit("Kein einziger Fehlalarm gefunden — nichts einzusammeln.")

    manifest = {
        "schema": "lernstufe_fehlerbestand_v1",
        "zweck": "Eigene Fehlalarme des Modells als zusaetzliche Gegenbeispiele.",
        "grenze": ("Das Protokoll ist unvollstaendig. Ein Teil dieser Bilder zeigt einen "
                   "echten, nur nicht codierten Befund und waere ein falsches Gegenbeispiel. "
                   "Vor dem Training in einer Stichprobe messen."),
        "nur_trainingshaltungen": True,
        "quelle_scan": str(args.scan),
        "gewicht": str(args.gewicht),
        "gewicht_sha256": sha256_datei(args.gewicht),
        "klasse": args.klasse,
        "schwelle": args.schwelle,
        "imgsz": args.imgsz,
        "saat": args.saat,
        "abstand_zu_fundstellen_s": NAHFELD_ABSTAND_S,
        "videos_abgesucht": len(quellen),
        "bilder": len(eintraege),
        "splits": {t: sum(1 for e in eintraege if e["split"] == t) for t in ("train", "validation")},
        "eintraege": eintraege,
    }
    text = json.dumps(manifest, indent=1, ensure_ascii=False)
    (arbeit / "manifest.json").write_bytes(text.encode("utf-8"))
    (arbeit / "manifest.sha256").write_bytes(
        (hashlib.sha256(text.encode("utf-8")).hexdigest() + "\n").encode("utf-8"))
    arbeit.rename(args.ziel)

    print(f"\n{len(eintraege)} Fehlerbilder aus {len(quellen)} Videos")
    print(f"   {manifest['splits']}")
    print(f"Bestand: {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
