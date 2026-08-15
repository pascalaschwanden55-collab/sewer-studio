"""Erzeugt echte Meterfolgen, wie der Bogen-Scan sie im Programm sieht.

WOZU
Die Archivmessung zaehlt EINZELNE Bilder an 20 weit auseinander liegenden Stellen.
So arbeitet das Programm nicht: Der Bogen-Scan zieht ein Bild je Sekunde, und
`MeterSequenceGapFiller` fuellt danach kurze Luecken zwischen zwei gelesenen
Werten. Die fuer den Benutzer sichtbare Quote ist deshalb hoeher als die reine
Lesequote — nur weiss niemand, um wie viel.

Dieses Skript liefert die Rohfolgen. Das Fuellen macht bewusst NICHT dieses
Skript, sondern der echte C#-Code ueber die erzeugte JSON-Datei: Eine
Nachbildung wuerde frueher oder spaeter vom Produktivverhalten abweichen, und
genau darum geht es hier.

Videos werden nur gelesen. Die Auswahl ist deterministisch und im Bericht
festgehalten.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Sequence

from PIL import Image

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "sidecar"))
from sidecar import osd_meter  # noqa: E402

# Abtastrate des Bogen-Scans (VideoFrameSequenceRequest.FramesPerSecond).
BILDER_JE_SEKUNDE = 1.0


def ffmpeg_pfad() -> str:
    pfad = shutil.which("ffmpeg")
    if not pfad:
        raise SystemExit("ffmpeg wurde nicht gefunden.")
    return pfad


def bildfolge_lesen(ffmpeg: str, video: Path, ziel: Path) -> list[tuple[float, float | None]]:
    """Ein Bild je Sekunde extrahieren und den Meterstand lesen."""
    lauf = subprocess.run(
        [ffmpeg, "-hide_banner", "-loglevel", "error", "-i", str(video),
         "-vf", f"fps={BILDER_JE_SEKUNDE}", "-q:v", "2", str(ziel / "f%06d.jpg")],
        capture_output=True, text=True)
    if lauf.returncode != 0:
        raise RuntimeError(lauf.stderr.strip()[:300] or "ffmpeg fehlgeschlagen")

    templates = osd_meter.get_templates()
    folge: list[tuple[float, float | None]] = []
    for nr, bild_pfad in enumerate(sorted(ziel.glob("f*.jpg"))):
        with Image.open(bild_pfad) as bild:
            wert = osd_meter.lese_meter(bild, templates)["meter"]
        folge.append((nr / BILDER_JE_SEKUNDE, wert))
    return folge


def auswahl(eintraege: list[dict], je_gruppe: dict[str, int]) -> list[dict]:
    """Gleichmaessig ueber die sortierte Liste verteilt, nicht die ersten N.

    Die ersten Eintraege stammen sonst aus demselben Projekt und zeigen denselben
    Anzeigestil — die Stichprobe waere dann nicht repraesentativ.
    """
    gewaehlt = []
    for gruppe, anzahl in je_gruppe.items():
        kandidaten = sorted(
            (e for e in eintraege if e.get("gruppe") == gruppe and e.get("video")),
            key=lambda e: str(e.get("haltung")))
        if not kandidaten or anzahl <= 0:
            continue
        schritt = max(1, len(kandidaten) // anzahl)
        gewaehlt.extend(kandidaten[::schritt][:anzahl])
    return gewaehlt


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--quelle", type=Path, required=True,
                        help="Bericht der Archivmessung; liefert Videoliste und Gruppen.")
    parser.add_argument("--ziel", type=Path, required=True)
    parser.add_argument("--sd", type=int, default=8)
    parser.add_argument("--hd", type=int, default=4)
    args = parser.parse_args(argv)

    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits und wird nicht ueberschrieben: {args.ziel}")

    daten = json.loads(args.quelle.read_text(encoding="utf-8-sig"))
    gewaehlt = auswahl(list(daten.get("ergebnisse") or []), {"sd": args.sd, "hd": args.hd})
    if not gewaehlt:
        raise SystemExit("Keine Videos ausgewaehlt.")

    ffmpeg = ffmpeg_pfad()
    folgen = []
    for nr, eintrag in enumerate(gewaehlt, start=1):
        video = Path(eintrag["video"])
        print(f"[{nr}/{len(gewaehlt)}] {eintrag['haltung']} ({eintrag['gruppe']}) …",
              flush=True)
        if not video.is_file():
            folgen.append({**_kopf(eintrag), "zustand": "video_fehlt"})
            continue
        with tempfile.TemporaryDirectory() as temp:
            try:
                folge = bildfolge_lesen(ffmpeg, video, Path(temp))
            except Exception as ex:  # defektes Video stoppt die uebrigen nicht
                folgen.append({**_kopf(eintrag), "zustand": "fehler", "grund": str(ex)})
                continue
        gelesen = sum(1 for _t, w in folge if w is not None)
        folgen.append({
            **_kopf(eintrag),
            "zustand": "geprueft",
            "bilder": len(folge),
            "gelesen": gelesen,
            "lesequote": round(gelesen / len(folge), 4) if folge else 0.0,
            "folge": [{"t": t, "meter": w} for t, w in folge],
        })
        print(f"    {gelesen}/{len(folge)} gelesen", flush=True)

    bericht = {
        "schema": "osd_sequenz_abdeckung_v1",
        "zweck": "Rohe Meterfolgen bei 1 Bild/Sekunde. Das Fuellen kurzer Luecken "
                 "macht der produktive C#-Code, nicht dieses Skript.",
        "bilder_je_sekunde": BILDER_JE_SEKUNDE,
        "quelle": str(args.quelle),
        "leser_sha256": _sha256(Path(osd_meter.__file__)),
        "videos": folgen,
    }
    args.ziel.parent.mkdir(parents=True, exist_ok=True)
    arbeit = args.ziel.with_suffix(".arbeit")
    arbeit.write_bytes(json.dumps(bericht, indent=1, ensure_ascii=False).encode("utf-8"))
    arbeit.replace(args.ziel)

    geprueft = [f for f in folgen if f.get("zustand") == "geprueft"]
    bilder = sum(f["bilder"] for f in geprueft)
    gelesen = sum(f["gelesen"] for f in geprueft)
    print(f"\n{len(geprueft)} Videos, {bilder} Bilder, {gelesen} gelesen "
          f"({gelesen / bilder:.1%})" if bilder else "\nnichts gemessen")
    print(f"Folgen: {args.ziel}")
    return 0


def _kopf(eintrag: dict) -> dict:
    return {"haltung": eintrag.get("haltung"), "gruppe": eintrag.get("gruppe"),
            "video": eintrag.get("video")}


def _sha256(pfad: Path) -> str:
    import hashlib
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


if __name__ == "__main__":
    sys.exit(main())
