"""Bereitet die menschliche Pruefung der BCC-Videomeldungen ohne Protokollbezug vor.

DIAGNOSE. Kundenoriginale werden ausschliesslich gelesen. Aus jeder Meldung, die
im Protokoll keinen Bogen findet, entsteht ein kurzer, unveraenderter Clip aus dem
Originalvideo. Ein Bogen ist im Bewegungsablauf zu beurteilen, nicht auf einem
Einzelbild.

Die Warteschlange ist bewusst blind: Sie enthaelt weder Konfidenz noch eine
Vorab-Einstufung. Eine vorsortierte Auswahl wuerde das Urteil verzerren — genau
dieser Fehler ist bei der Benchmark-Erweiterung v1 schon einmal passiert.

Das Werkzeug trainiert nichts, aktiviert nichts und erzeugt keine Trainingsdaten.
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

VORLAUF_S = 3
NACHLAUF_S = 3


def _sha256(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1024 * 1024), b""):
            hasher.update(block)
    return hasher.hexdigest()


def _fall_id(haltung: str, start: int, ende: int) -> str:
    roh = f"{haltung}|{start}|{ende}".encode("utf-8")
    return hashlib.sha256(roh).hexdigest()[:16]


def _ffmpeg_suchen(vorgabe: Path | None) -> Path:
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


def faelle_aus_bericht(bericht: dict) -> list[dict]:
    """Alle Meldungen ohne Protokollbezug, stabil sortiert."""
    faelle: list[dict] = []
    for eintrag in bericht.get("ergebnisse") or []:
        haltung = str(eintrag["haltung"])
        video = Path(str(eintrag["video"]))
        dauer = float(eintrag.get("dauer_s") or 0.0)
        for gruppe in eintrag.get("gruppen") or []:
            if gruppe.get("ist_treffer"):
                continue
            start = int(gruppe["start"])
            ende = int(gruppe["ende"])
            faelle.append(
                {
                    "fall_id": _fall_id(haltung, start, ende),
                    "haltung": haltung,
                    "video": str(video),
                    "video_dauer_s": dauer,
                    "start_s": start,
                    "ende_s": ende,
                    "peak_s": int(gruppe.get("peak_sekunde", start)),
                }
            )
    faelle.sort(key=lambda fall: (fall["haltung"], fall["start_s"], fall["ende_s"]))
    return faelle


def reihenfolge_mischen(faelle: list[dict], saat: str) -> list[dict]:
    """Deterministisch mischen, damit Haltungen nicht gebuendelt beurteilt werden."""
    return sorted(
        faelle,
        key=lambda fall: hashlib.sha256(f"{saat}|{fall['fall_id']}".encode()).hexdigest(),
    )


def clip_schneiden(ffmpeg: Path, fall: dict, ziel: Path) -> bool:
    quelle = Path(fall["video"])
    if not quelle.is_file():
        return False
    von = max(0.0, fall["start_s"] - VORLAUF_S)
    bis = fall["ende_s"] + NACHLAUF_S
    if fall["video_dauer_s"] > 0:
        bis = min(bis, fall["video_dauer_s"])
    dauer = max(1.0, bis - von)
    ergebnis = subprocess.run(
        [
            str(ffmpeg), "-v", "error", "-y",
            "-ss", f"{von:.2f}", "-i", str(quelle), "-t", f"{dauer:.2f}",
            "-an", "-c:v", "libx264", "-preset", "veryfast", "-crf", "23",
            "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            str(ziel),
        ],
        capture_output=True,
        text=True,
        timeout=5 * 60,
    )
    return ergebnis.returncode == 0 and ziel.is_file() and ziel.stat().st_size > 0


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Blinde Pruefliste der BCC-Videomeldungen ohne Protokollbezug"
    )
    parser.add_argument(
        "--report",
        type=Path,
        default=Path(
            r"C:\KI_BRAIN\training\diagnostics\bcc_video_messung_20260807\report.json"
        ),
    )
    parser.add_argument(
        "--ziel",
        type=Path,
        default=Path(r"C:\KI_BRAIN\training\diagnostics\bcc_video_fehlalarm_queue"),
    )
    parser.add_argument("--ffmpeg", type=Path, default=None)
    parser.add_argument("--saat", default="bcc-fehlalarm-v1")
    args = parser.parse_args(argv)

    if not args.report.is_file():
        raise SystemExit(f"Bericht fehlt: {args.report}")

    bericht = json.loads(args.report.read_text(encoding="utf-8-sig"))
    bericht_sha = _sha256(args.report)
    faelle = reihenfolge_mischen(faelle_aus_bericht(bericht), args.saat)
    if not faelle:
        raise SystemExit("Keine Meldungen ohne Protokollbezug im Bericht.")

    ffmpeg = _ffmpeg_suchen(args.ffmpeg)
    clips = args.ziel / "clips"
    clips.mkdir(parents=True, exist_ok=True)

    fertig: list[dict] = []
    for nummer, fall in enumerate(faelle, start=1):
        name = f"fall_{nummer:03d}_{fall['fall_id']}.mp4"
        ziel = clips / name
        if ziel.is_file() and ziel.stat().st_size > 0:
            geschnitten = True
        else:
            geschnitten = clip_schneiden(ffmpeg, fall, ziel)
        if not geschnitten:
            print(f"  Clip fehlgeschlagen, uebersprungen: {fall['haltung']} @ {fall['start_s']}s")
            continue
        fertig.append(
            {
                "nummer": nummer,
                "fall_id": fall["fall_id"],
                "haltung": fall["haltung"],
                "start_s": fall["start_s"],
                "ende_s": fall["ende_s"],
                "clip": name,
                # Konfidenz und Vorab-Einstufung bleiben bewusst draussen.
            }
        )
        print(f"  [{nummer:>3}/{len(faelle)}] {fall['haltung']} {fall['start_s']}-{fall['ende_s']}s")

    warteschlange = {
        "schema_version": 1,
        "zweck": "Blinde menschliche Pruefung: echter Bogen oder Fehlalarm?",
        "quelle_bericht": str(args.report),
        "quelle_bericht_sha256": bericht_sha,
        "saat": args.saat,
        "faelle": fertig,
    }
    manifest = args.ziel / "queue.json"
    temp = manifest.with_suffix(".json.tmp")
    temp.write_text(json.dumps(warteschlange, indent=2, ensure_ascii=False), encoding="utf-8")
    temp.replace(manifest)

    print(f"\n{len(fertig)} Faelle vorbereitet.")
    print(f"Warteschlange: {manifest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
