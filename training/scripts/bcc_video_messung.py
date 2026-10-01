#!/usr/bin/env python3
"""BCC-Einzelklassen-Kandidat: erste Messung auf echten Inspektionsvideos.

Diagnose-Werkzeug, kein Produktcode. Beantwortet drei Fragen (Paket 4):
1. Wie viele der protokollierten BCC-Befunde findet das Modell im Video?
2. Wie viele Fehlalarm-Gruppen entstehen pro Haltung nach zeitlichem Merge?
3. Wie lange braucht die Inferenz pro Video?

Grundlage ist die Kandidatenliste aus ``collect_class_candidates.py``
(``artifacts/klassen-messung-20260804/messung.json``): BCC-Befunde mit
Videozaehlerstand (hh:mm:ss:ff) aus XTF/db3, gefiltert gegen Gold und
Schutzquellen. Die Haltungen dieser Liste sind damit nicht im Training des
Kandidaten enthalten — die Messung ist gegenueber dem Trainingsbestand sauber.

Auswahlregel (deterministisch): Projekte absteigend nach Kandidatenzahl, je
Projekt die ersten zwei Haltungen (alphabetisch), bis ``--max-videos`` erreicht
ist. Leitungsinspektionen (L_-Videos) bleiben ausdruecklich erlaubt, werden
aber im Bericht markiert.

Arbeitsweise je Video: 1 Frame/Sekunde via ffmpeg, Inferenz mit conf=0.10
(Arbeitspunkt aus dem Schwellenlauf, Paket 2), zeitlicher Merge positiver
Sekunden mit Luecke > 3 s als Gruppengrenze. Ein protokollierter Befund gilt
als gefunden, wenn eine Gruppe das Zeitfenster +-15 s um den Videozaehlerstand
ueberlappt. Gruppen ohne Ueberlappung zaehlen als Fehlalarm — mit der
bekannten Einschraenkung, dass nicht jeder sichtbare Bogen im Protokoll codiert
sein muss (ehrliche Obergrenze, keine exakte Wahrheit).

Schreibfrei fuer Kundenoriginale: Videos werden nur gelesen. Frames landen in
einem temporaeren Ordner und werden je Video geloescht; nur Spot-Check-Frames
(Gruppen-Maxima und Protokoll-Zeitpunkte) bleiben fuer die Sichtpruefung
bestehen.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
import time
from collections import defaultdict
from pathlib import Path

VIDEO_SUFFIXES = {".mp4", ".mpg", ".mpeg", ".avi", ".wmv", ".mov"}
MERGE_GAP_SECONDS = 3
RECALL_TOLERANCE_SECONDS = 15.0
SAMPLE_FPS = 1


def parse_videozaehler(value: str) -> float | None:
    """hh:mm:ss[:ff] -> Sekunden (Frames bleiben unberuecksichtigt, 1-fps-Raster)."""
    parts = (value or "").strip().split(":")
    if len(parts) not in (3, 4):
        return None
    try:
        hours, minutes, seconds = (int(p) for p in parts[:3])
    except ValueError:
        return None
    return float(hours * 3600 + minutes * 60 + seconds)


def projekt_root(quell_datei: str) -> Path | None:
    for ancestor in Path(quell_datei).parents:
        if (ancestor / "Film").is_dir():
            return ancestor
    return None


def finde_video(root: Path, haltung: str) -> Path | None:
    von, _, bis = haltung.partition("-")
    namen = {haltung}
    if bis:
        namen.add(f"{bis}-{von}")
    for datei in sorted((root / "Film").iterdir()):
        if datei.suffix.casefold() not in VIDEO_SUFFIXES:
            continue
        if any(name and name in datei.stem for name in namen):
            return datei
    return None


def waehle_ziele(kandidaten: list[dict], max_videos: int) -> list[dict]:
    """Deterministische Auswahl: je Projekt (nach Kandidatenzahl absteigend)
    die ersten zwei Haltungen mit auffindbarem Video."""
    je_haltung: dict[tuple[str, str], list[dict]] = defaultdict(list)
    for kandidat in kandidaten:
        je_haltung[(kandidat["quell_datei"], kandidat["haltung"])].append(kandidat)

    projekte: dict[Path, list[tuple[str, list[dict], Path]]] = defaultdict(list)
    for (quell, haltung), befunde in je_haltung.items():
        root = projekt_root(quell)
        if root is None:
            continue
        video = finde_video(root, haltung)
        if video is None:
            continue
        projekte[root].append((haltung, befunde, video))

    geordnet = sorted(projekte.items(), key=lambda item: (-len(item[1]), str(item[0])))
    ziele: list[dict] = []
    for root, haltungen in geordnet:
        # Haltungsinspektionen (H_) vor Leitungsinspektionen (L_): die
        # DN100/DN160-Schubstangen-Domain ist eine eigene Bildwelt.
        def schluessel(item: tuple[str, list[dict], Path]) -> tuple[bool, str]:
            haltung, befunde, video = item
            ist_l = video.stem.startswith("L_") or any(
                b.get("leitungsinspektion") for b in befunde)
            return (ist_l, haltung)

        for haltung, befunde, video in sorted(haltungen, key=schluessel)[:2]:
            ziele.append({
                "projekt": str(root),
                "haltung": haltung,
                "video": str(video),
                "befunde": befunde,
                "leitungsinspektion": video.stem.startswith("L_")
                or any(b.get("leitungsinspektion") for b in befunde),
            })
            if len(ziele) >= max_videos:
                return ziele
    return ziele


def video_dauer(video: Path) -> float | None:
    try:
        out = subprocess.run(
            ["ffprobe", "-v", "error", "-select_streams", "v:0",
             "-show_entries", "format=duration", "-of", "csv=p=0", str(video)],
            capture_output=True, text=True, timeout=120, check=False)
        return float(out.stdout.strip())
    except (OSError, ValueError, subprocess.TimeoutExpired):
        return None


def extrahiere_frames(video: Path, ziel: Path) -> list[Path]:
    ziel.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        ["ffmpeg", "-hide_banner", "-loglevel", "error", "-i", str(video),
         "-vf", f"fps={SAMPLE_FPS}", "-q:v", "3", str(ziel / "f%06d.jpg")],
        capture_output=True, timeout=3600, check=False)
    return sorted(ziel.glob("f*.jpg"))


def messe_video(
    ziel: dict,
    model,
    out_dir: Path,
    temp_root: Path,
    class_id: int | None = None,
) -> dict:
    from PIL import Image  # lokal, damit --help ohne venv-Deps funktioniert

    video = Path(ziel["video"])
    dauer = video_dauer(video)
    frames_dir = temp_root / "frames"
    if frames_dir.exists():
        shutil.rmtree(frames_dir)

    started = time.perf_counter()
    frames = extrahiere_frames(video, frames_dir)
    extract_s = time.perf_counter() - started

    befunde = []
    for befund in ziel["befunde"]:
        tc = parse_videozaehler(befund.get("videozaehler", ""))
        befunde.append({
            "code": befund["code"],
            "meter": befund.get("meter", ""),
            "videozaehler": befund.get("videozaehler", ""),
            "sekunde": tc,
            "pruefbar": tc is not None and dauer is not None and tc <= dauer + 1.0,
        })

    positive: dict[int, float] = {}  # sekunde -> max conf
    inferenz_started = time.perf_counter()
    for frame in frames:
        sekunde = int(frame.stem[1:]) - 1  # f000001.jpg == Sekunde 0
        with Image.open(frame) as img:
            img.load()
            # Ein Mehrklassenmodell wird auf dieselbe Klasse eingeengt, die der
            # Sidecar-Vertrag fest durchlaesst (ID 14 BCC_bogen).
            results = model.predict(
                source=img, conf=0.10, imgsz=1280, verbose=False,
                classes=None if class_id is None else [class_id])
        boxes = results[0].boxes if results else None
        if boxes is not None and len(boxes) > 0:
            positive[sekunde] = round(float(max(b.conf[0].cpu().item() for b in boxes)), 4)
    inferenz_s = time.perf_counter() - inferenz_started

    gruppen: list[dict] = []
    for sekunde in sorted(positive):
        if gruppen and sekunde - gruppen[-1]["ende"] <= MERGE_GAP_SECONDS:
            gruppen[-1]["ende"] = sekunde
            if positive[sekunde] > gruppen[-1]["max_conf"]:
                gruppen[-1]["max_conf"] = positive[sekunde]
                gruppen[-1]["peak_sekunde"] = sekunde
        else:
            gruppen.append({"start": sekunde, "ende": sekunde,
                            "max_conf": positive[sekunde], "peak_sekunde": sekunde})

    tol = RECALL_TOLERANCE_SECONDS
    for befund in befunde:
        if not befund["pruefbar"]:
            befund["gefunden"] = None
            continue
        befund["gefunden"] = any(
            g["start"] - tol <= befund["sekunde"] <= g["ende"] + tol for g in gruppen)
    treffer_sekunden = {
        int(b["sekunde"]) for b in befunde if b["pruefbar"] and b["gefunden"]}
    for gruppe in gruppen:
        gruppe["ist_treffer"] = any(
            gruppe["start"] - tol <= b["sekunde"] <= gruppe["ende"] + tol
            for b in befunde if b["pruefbar"])

    spot_dir = out_dir / "spotchecks" / ziel["haltung"].replace("/", "_")
    spot_dir.mkdir(parents=True, exist_ok=True)
    for gruppe in gruppen:
        quelle = frames_dir / f"f{gruppe['peak_sekunde'] + 1:06d}.jpg"
        if quelle.exists():
            shutil.copy2(quelle, spot_dir / (
                f"gruppe_{gruppe['start']:05d}-{gruppe['ende']:05d}"
                f"_conf{gruppe['max_conf']:.2f}"
                f"{'_TREFFER' if gruppe['ist_treffer'] else ''}.jpg"))
    for befund in befunde:
        if befund["pruefbar"]:
            quelle = frames_dir / f"f{int(befund['sekunde']) + 1:06d}.jpg"
            if quelle.exists():
                shutil.copy2(quelle, spot_dir / (
                    f"protokoll_{int(befund['sekunde']):05d}_{befund['code']}"
                    f"{'_gefunden' if befund['gefunden'] else '_verfehlt'}.jpg"))
    shutil.rmtree(frames_dir, ignore_errors=True)

    pruefbar = [b for b in befunde if b["pruefbar"]]
    return {
        "haltung": ziel["haltung"],
        "projekt": ziel["projekt"],
        "video": str(video),
        "leitungsinspektion": ziel["leitungsinspektion"],
        "dauer_s": round(dauer, 1) if dauer else None,
        "frames": len(frames),
        "extraktion_s": round(extract_s, 1),
        "inferenz_s": round(inferenz_s, 1),
        "fps_inferenz": round(len(frames) / inferenz_s, 2) if inferenz_s > 0 else None,
        "befunde": befunde,
        "befunde_pruefbar": len(pruefbar),
        "befunde_gefunden": sum(1 for b in pruefbar if b["gefunden"]),
        "gruppen": gruppen,
        "gruppen_gesamt": len(gruppen),
        "fehlalarm_gruppen": sum(1 for g in gruppen if not g["ist_treffer"]),
        "positive_sekunden": len(positive),
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--messung", type=Path, default=Path(
        r"C:\Sewer-Studio_KI_4.5\artifacts\klassen-messung-20260804\messung.json"))
    parser.add_argument("--weights", type=Path, default=Path(
        r"C:\KI_BRAIN\training\diagnostics\bcc_single_fullbg_20260807"
        r"\runs\seed44\run\weights\best.pt"))
    parser.add_argument("--out", type=Path, default=Path(
        r"C:\KI_BRAIN\training\diagnostics\bcc_video_messung_20260807"))
    parser.add_argument("--max-videos", type=int, default=8)
    parser.add_argument(
        "--class-id", type=int, default=None,
        help="Nur diese Klassen-ID werten (Mehrklassenmodell; Sidecar-Vertrag: 14).")
    args = parser.parse_args(argv)

    daten = json.loads(args.messung.read_text(encoding="utf-8"))
    kandidaten = daten["klassen"]["BCC"]["kandidaten"]
    ziele = waehle_ziele(kandidaten, args.max_videos)
    if not ziele:
        print("Keine Haltungen mit auffindbarem Video.", file=sys.stderr)
        return 1

    print(f"Ziele: {len(ziele)} Haltungen")
    for ziel in ziele:
        print(f"  {ziel['haltung']:30s} befunde={len(ziel['befunde'])} "
              f"{'L-Inspektion ' if ziel['leitungsinspektion'] else ''}{Path(ziel['video']).name}")

    from ultralytics import YOLO
    model = YOLO(str(args.weights))

    args.out.mkdir(parents=True, exist_ok=True)
    ergebnisse = []
    with tempfile.TemporaryDirectory(dir=args.out, prefix="tmp_") as temp_root:
        for ziel in ziele:
            print(f"\n=== {ziel['haltung']} ===")
            ergebnis = messe_video(
                ziel, model, args.out, Path(temp_root), args.class_id)
            ergebnisse.append(ergebnis)
            print(f"  dauer={ergebnis['dauer_s']}s frames={ergebnis['frames']} "
                  f"inferenz={ergebnis['inferenz_s']}s ({ergebnis['fps_inferenz']} fps)")
            print(f"  befunde: {ergebnis['befunde_gefunden']}/{ergebnis['befunde_pruefbar']} gefunden, "
                  f"gruppen={ergebnis['gruppen_gesamt']} davon fehlalarm={ergebnis['fehlalarm_gruppen']}")

    gesamt_pruefbar = sum(e["befunde_pruefbar"] for e in ergebnisse)
    gesamt_gefunden = sum(e["befunde_gefunden"] for e in ergebnisse)
    gesamt_fa = sum(e["fehlalarm_gruppen"] for e in ergebnisse)
    bericht = {
        "schema_version": "bcc-video-messung-v1",
        "weights": str(args.weights),
        "conf": 0.10,
        "class_id": args.class_id,
        "imgsz": 1280,
        "sample_fps": SAMPLE_FPS,
        "merge_gap_s": MERGE_GAP_SECONDS,
        "recall_tolerance_s": RECALL_TOLERANCE_SECONDS,
        "hinweis_fehlalarm": ("Gruppen ohne Protokoll-BCC im Zeitfenster gelten als "
                              "Fehlalarm. Nicht jeder sichtbare Bogen ist codiert — "
                              "die Zahl ist eine ehrliche Obergrenze, keine exakte Wahrheit."),
        "ergebnisse": ergebnisse,
        "summe": {
            "videos": len(ergebnisse),
            "befunde_pruefbar": gesamt_pruefbar,
            "befunde_gefunden": gesamt_gefunden,
            "fehlalarm_gruppen": gesamt_fa,
            "fehlalarme_je_haltung": round(gesamt_fa / len(ergebnisse), 2),
            "inferenz_s_gesamt": round(sum(e["inferenz_s"] for e in ergebnisse), 1),
        },
    }
    report_path = args.out / "report.json"
    report_path.write_text(json.dumps(bericht, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\nGESAMT: {gesamt_gefunden}/{gesamt_pruefbar} protokollierte Boegen gefunden, "
          f"{gesamt_fa} Fehlalarm-Gruppen auf {len(ergebnisse)} Haltungen "
          f"({gesamt_fa / len(ergebnisse):.1f}/Haltung)")
    print(f"Bericht: {report_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
