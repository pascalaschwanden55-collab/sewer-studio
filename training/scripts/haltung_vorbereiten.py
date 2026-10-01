"""Sagt fuer ein Video, wo Rohranfang und Rohrende liegen.

WOZU
Beide Codes stehen in jedem Protokoll und kosten beim Codieren nur Sucharbeit.
Dieses Werkzeug nimmt sie ab: Es liefert je Video eine Sekundenangabe mit
Konfidenz, die der Mensch bestaetigt oder verwirft.

WAS ES NICHT TUT
Es codiert nichts, schreibt nichts ins Protokoll und veraendert keine
Kundendatei. Es ist eine Leseh Hilfe, keine Automatik.

GEMESSENE GUETE (Stand 2026-08-12, vorregistrierte Abnahme an frischen Videos)
  Rohranfang  Precision 85,5 %  Recall 97,8 %   60 Videos
  Rohrende    Precision 88,9 %  Recall 88,4 %   46 Videos
Das heisst: Etwa jede siebte bis achte Angabe ist falsch. Immer nachsehen.

Ein Modell laeuft nur mit gueltiger Freigabedatei. Fehlt sie oder passt der
Gewicht-Hash nicht, bricht das Werkzeug ab — ein ungemessenes Modell darf hier
nicht mitreden.
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

from bcc_lernstufe_aus_protokoll import FFMPEG, sha256_datei
from lernstufe_videolauf import zusammenfassen

FREIGABEN = Path(r"C:\KI_BRAIN\training\lernstufen\freigaben")


def freigabe_laden(klasse: str) -> dict:
    pfad = FREIGABEN / f"{klasse}_v1.json"
    if not pfad.is_file():
        raise SystemExit(f"Keine Freigabe fuer {klasse}: {pfad}")
    roh = pfad.read_bytes()
    soll = pfad.with_suffix(".sha256").read_text(encoding="utf-8").strip()
    if hashlib.sha256(roh).hexdigest() != soll:
        raise SystemExit(f"Freigabe {klasse} passt nicht zu ihrem Hash")
    d = json.loads(roh.decode("utf-8-sig"))
    if d.get("status") != "freigegeben":
        raise SystemExit(f"Freigabe {klasse} traegt den Status {d.get('status')!r}")
    gewicht = Path(d["gewicht"])
    if not gewicht.is_file():
        raise SystemExit(f"Gewicht fehlt: {gewicht}")
    if sha256_datei(gewicht) != d["gewicht_sha256"]:
        raise SystemExit(f"Gewicht von {klasse} weicht von der Freigabe ab")
    return d


def sekunde_als_text(s: float) -> str:
    return f"{int(s) // 60}:{int(s) % 60:02d}"


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--video", type=Path, required=True)
    parser.add_argument("--klassen", nargs="*", default=["rohranfang", "rohrende"])
    parser.add_argument("--imgsz", type=int, default=640)
    parser.add_argument("--fps", type=float, default=1.0)
    parser.add_argument("--bericht", type=Path, default=None)
    args = parser.parse_args(argv)

    if not args.video.is_file():
        raise SystemExit(f"Video fehlt: {args.video}")

    freigaben = {k: freigabe_laden(k) for k in args.klassen}

    from PIL import Image
    from ultralytics import YOLO

    from nocrop_patch import letterbox_pil

    modelle = {}
    for k, f in freigaben.items():
        m = YOLO(f["gewicht"])
        i = next((idx for idx, n in m.names.items() if n == k), None)
        if i is None:
            raise SystemExit(f"Klasse {k!r} fehlt im Gewicht: {m.names}")
        modelle[k] = (m, i)

    arbeit = Path(f".{args.video.stem}.vorbereitung")
    shutil.rmtree(arbeit, ignore_errors=True)
    arbeit.mkdir()
    try:
        lauf = subprocess.run(
            [str(FFMPEG), "-v", "error", "-y", "-i", str(args.video), "-vf", f"fps={args.fps:g}",
             "-q:v", "3", str(arbeit / "f%06d.jpg")],
            capture_output=True, text=True, timeout=2 * 60 * 60)
        if lauf.returncode != 0:
            raise SystemExit(f"Video nicht lesbar: {lauf.stderr.strip()[:200]}")
        bilder = sorted(arbeit.glob("f*.jpg"))
        if not bilder:
            raise SystemExit("Keine Bilder aus dem Video gewonnen.")

        werte = {k: [] for k in modelle}
        for j, bild in enumerate(bilder):
            with Image.open(bild) as roh:
                vorbereitet = letterbox_pil(roh, args.imgsz)
            for k, (m, i) in modelle.items():
                e = m.predict(source=vorbereitet, imgsz=args.imgsz, verbose=False)[0]
                werte[k].append((j / args.fps, float(e.probs.data[i])))
    finally:
        shutil.rmtree(arbeit, ignore_errors=True)

    dauer = len(bilder) / args.fps
    print(f"\n{args.video.name}   {sekunde_als_text(dauer)} lang, {len(bilder)} Bilder geprueft\n")
    ergebnisse = []
    for k in args.klassen:
        # Regel aus der Abnahme: die staerkste Meldung im ganzen Video, genau eine.
        stellen = zusammenfassen(werte[k], 0.50, 0.0)
        if not stellen:
            print(f"  {k:<12}keine Stelle gefunden — bitte selbst nachsehen")
            ergebnisse.append({"klasse": k, "sekunde": None, "konfidenz": None})
            continue
        beste = max(stellen, key=lambda s: s["max_wert"])
        print(f"  {k:<12}Sekunde {beste['peak_zeit']:>6.0f}  ({sekunde_als_text(beste['peak_zeit'])})"
              f"   Konfidenz {beste['max_wert']:.2f}")
        ergebnisse.append({"klasse": k, "sekunde": round(beste["peak_zeit"], 1),
                           "konfidenz": round(beste["max_wert"], 4),
                           "bilder": beste["bilder"]})

    print("\n  Etwa jede siebte Angabe ist falsch — immer nachsehen, bevor du sie uebernimmst.")

    if args.bericht:
        doc = {"schema": "haltung_vorbereitung_v1",
               "video": str(args.video), "dauer_s": round(dauer, 1),
               "regel": "staerkste Meldung je Video und Klasse, Schwelle 0,50",
               "guete": {k: {"precision": f["abnahme"]["precision"],
                             "recall": f["abnahme"]["recall"]} for k, f in freigaben.items()},
               "hinweis": "Vorschlag zum Bestaetigen, keine Codierung.",
               "ergebnisse": ergebnisse}
        args.bericht.write_bytes(json.dumps(doc, indent=1, ensure_ascii=False).encode("utf-8"))
        print(f"  Bericht: {args.bericht}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
