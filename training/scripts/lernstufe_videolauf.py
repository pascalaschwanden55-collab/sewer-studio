"""Laesst einen Bild-Einordner ueber ein ganzes Video laufen und schneidet Clips.

Der Testteil eines Lernbestands misst das Wiedererkennen von Protokollstellen —
ausgewaehlte Bilder, an denen etwas ist. Im Video laeuft das Modell ueber JEDES
Bild, auch ueber tausende, an denen nichts ist. Beim Bogen fielen diese beiden
Zahlen weit auseinander: intern gut, im Video 60 % Precision.

Dieses Werkzeug erzeugt deshalb dieselbe Grundlage wie beim Bogen: Vorschlaege
mit Zeitbereich und je einen kurzen Clip, damit ein Mensch sie beurteilen kann.

Kundenoriginale werden ausschliesslich gelesen.
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

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "vsa_classifier"))

FFMPEG = Path(
    r"C:\Users\Besitzer\AppData\Local\Microsoft\WinGet\Packages"
    r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-8.0.1-full_build\bin\ffmpeg.exe"
)
# Zusammenfassen wie beim Bogen: Eine Stelle wird ueber mehrere Bilder gesehen.
ZEIT_LUECKE_S = 3.0
VORLAUF_S, NACHLAUF_S = 2.0, 2.0
SCHACHT_S = 3.0


def sha256_datei(pfad: Path) -> str:
    h = hashlib.sha256()
    with pfad.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def zusammenfassen(treffer: list[tuple[float, float]], schwelle: float,
                   ab_sekunde: float = SCHACHT_S) -> list[dict]:
    """Bilder derselben Stelle zu einem Vorschlag buendeln.

    Der Arbeitspunkt gilt fuer die STELLE, nicht fuer das einzelne Bild:
    Gesammelt wird ab einer niedrigen Aufnahmegrenze, gemessen wird die fertige
    Stelle. Ein Konfidenzeinbruch zerlegt sie sonst in zwei Vorschlaege — beim
    Bogen am 2026-08-08 gemessen.
    """
    boden = min(0.10, schwelle)
    # `ab_sekunde` blendet den Schacht am Videoanfang aus. Fuer Klassen, die
    # GENAU dort sitzen — Rohranfang —, muss der Aufrufer 0 uebergeben, sonst
    # wirft die Zusammenfassung den einzigen echten Treffer weg.
    relevant = [(t, w) for t, w in treffer if w >= boden and t >= ab_sekunde]
    relevant.sort()
    gruppen: list[dict] = []
    for zeit, wert in relevant:
        if gruppen and zeit - gruppen[-1]["zeit_max"] <= ZEIT_LUECKE_S:
            g = gruppen[-1]
            g["zeit_max"] = zeit
            g["bilder"] += 1
            if wert > g["max_wert"]:
                g["max_wert"], g["peak_zeit"] = wert, zeit
        else:
            gruppen.append({"zeit_min": zeit, "zeit_max": zeit, "peak_zeit": zeit,
                            "max_wert": wert, "bilder": 1})
    return [g for g in gruppen if g["max_wert"] >= schwelle]


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--video", type=Path, required=True)
    parser.add_argument("--haltung", default="")
    parser.add_argument("--gewicht", type=Path, required=True)
    parser.add_argument("--klasse", required=True, help="Name der positiven Klasse im Modell")
    parser.add_argument("--schwelle", type=float, default=0.50)
    parser.add_argument("--imgsz", type=int, default=640)
    parser.add_argument("--fps", type=float, default=1.0)
    parser.add_argument("--ziel", type=Path, required=True)
    args = parser.parse_args(argv)

    if not args.video.is_file():
        raise SystemExit(f"Video fehlt: {args.video}")
    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits: {args.ziel}")

    from PIL import Image
    from ultralytics import YOLO

    from nocrop_patch import letterbox_pil

    modell = YOLO(str(args.gewicht))
    index = next((i for i, n in modell.names.items() if n == args.klasse), None)
    if index is None:
        raise SystemExit(f"Klasse {args.klasse!r} fehlt im Modell: {modell.names}")

    haltung = args.haltung or args.video.stem
    arbeit = args.ziel.with_name(f".{args.ziel.name}.arbeit")
    shutil.rmtree(arbeit, ignore_errors=True)
    (arbeit / "frames").mkdir(parents=True)
    (arbeit / "clips").mkdir()

    lauf = subprocess.run(
        [str(FFMPEG), "-v", "error", "-i", str(args.video), "-vf", f"fps={args.fps:g}",
         "-q:v", "3", str(arbeit / "frames" / "f%06d.jpg")],
        capture_output=True, text=True, timeout=2 * 60 * 60)
    if lauf.returncode != 0:
        raise SystemExit(f"ffmpeg fehlgeschlagen: {lauf.stderr.strip()[:300]}")

    bilder = sorted((arbeit / "frames").glob("f*.jpg"))
    print(f"{len(bilder)} Bilder aus {args.video.name}")

    treffer: list[tuple[float, float]] = []
    for i, bild in enumerate(bilder):
        zeit = i / args.fps
        with Image.open(bild) as roh:
            vorbereitet = letterbox_pil(roh, args.imgsz)
        ergebnis = modell.predict(source=vorbereitet, imgsz=args.imgsz, verbose=False)[0]
        treffer.append((zeit, float(ergebnis.probs.data[index])))
        if (i + 1) % 200 == 0:
            print(f"  {i + 1}/{len(bilder)} …", flush=True)

    stellen = zusammenfassen(treffer, args.schwelle)
    print(f"\n{len(stellen)} Vorschlaege bei Schwelle {args.schwelle:.2f}")

    for nr, s in enumerate(stellen, start=1):
        name = f"vorschlag_{nr:03d}.mp4"
        von = max(0.0, s["zeit_min"] - VORLAUF_S)
        dauer = max(1.5, (s["zeit_max"] - s["zeit_min"]) + VORLAUF_S + NACHLAUF_S)
        subprocess.run(
            [str(FFMPEG), "-v", "error", "-y", "-ss", f"{von:.2f}", "-i", str(args.video),
             "-t", f"{dauer:.2f}", "-an", "-c:v", "libx264", "-preset", "veryfast",
             "-crf", "23", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
             str(arbeit / "clips" / name)],
            capture_output=True, timeout=5 * 60)
        s["nummer"] = nr
        s["clip"] = name

    shutil.rmtree(arbeit / "frames", ignore_errors=True)
    bericht = {
        "schema": "lernstufe_videolauf_v1",
        "zweck": "Verhalten eines Bild-Einordners ueber ein ganzes Video, Grundlage der Sichtpruefung",
        "keine_messung": ("Ohne menschliche Beurteilung der Clips ist die Zahl der Vorschlaege "
                          "kein Precision-Wert."),
        "haltung": haltung,
        "video": str(args.video),
        "gewicht": str(args.gewicht),
        "gewicht_sha256": sha256_datei(args.gewicht),
        "klasse": args.klasse,
        "schwelle": args.schwelle,
        "imgsz": args.imgsz,
        "bilder": len(bilder),
        "bilder_ueber_schwelle": sum(1 for _, w in treffer if w >= args.schwelle),
        "vorschlaege": stellen,
    }
    text = json.dumps(bericht, indent=1, ensure_ascii=False)
    (arbeit / "vorschlaege.json").write_bytes(text.encode("utf-8"))
    arbeit.rename(args.ziel)
    print(f"Durchgang: {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
