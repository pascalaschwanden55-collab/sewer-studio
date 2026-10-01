"""Fuehrt einen Bild-Einordner ueber mehrere Videos und baut eine blinde Clip-Pruefung.

WARUM DIESER WEG
Der Testteil eines Lernbestands misst das Wiedererkennen ausgewaehlter
Protokollstellen — Bilder, an denen etwas ist. Im Video laeuft das Modell ueber
JEDES Bild, auch ueber tausende ohne Befund. Beide Zahlen fielen bisher weit
auseinander: beim Bogen 60 % Precision im Video, beim Anschluss 3 von 6.

WAS BLIND BLEIBT
Der Pruefplatz zeigt nur Bild und Clip. Konfidenz, Haltung, Sekunde und die
Protokollangabe bleiben unsichtbar, damit das Urteil nicht vom Modell oder vom
Protokoll gefaerbt wird. Die Aufloesung liegt getrennt in der Queue.

WAS DIESE ZAHL IST UND WAS NICHT
Sie ist die Precision der VORSCHLAEGE, nicht die eines Ereignisses: Zwei
Vorschlaege koennen denselben Befund zeigen. Ein Recall ergibt sich daraus
nicht — dafuer braucht es den Abgleich gegen die Protokollstellen, und der ist
selbst nur eine Untergrenze, weil das Protokoll Befunde auslaesst.

Kundenoriginale werden ausschliesslich gelesen.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import random
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Sequence

SCRIPT = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT))
sys.path.insert(0, str(SCRIPT.parent / "vsa_classifier"))

from lernstufe_videolauf import FFMPEG, VORLAUF_S, NACHLAUF_S, sha256_datei, zusammenfassen
from lernstufe_mitte_ausblenden import maske_anwenden


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--auswahl", type=Path, required=True,
                        help="Vorregistrierte Videoliste (videolauf_vorauswahl_v1)")
    parser.add_argument("--gewicht", type=Path, action="append", required=True,
                        help="Ein oder mehrere Gewichte. Mehrere werden blind gemischt "
                             "und einmal beurteilt; jedes bekommt danach seine eigene Zahl.")
    parser.add_argument("--klasse", required=True)
    parser.add_argument("--schwelle", type=float, default=0.50)
    parser.add_argument("--imgsz", type=int, default=640)
    parser.add_argument("--fps", type=float, default=1.0)
    parser.add_argument("--staerkste-je-video", action="store_true",
                        help="Nur die staerkste Meldung je Video und Modell behalten. Regel aus "
                             "der Sache: Ein Video hat genau EINEN Rohranfang und genau EIN "
                             "Rohrende. Braucht kein Zeitfenster und nichts zu justieren.")
    parser.add_argument("--mitte-ausblenden", type=float, default=None,
                        help="Zentriertes Rechteck schwaerzen, Anteil der Kantenlaenge. "
                             "MUSS mit dem Wert des Lernbestands uebereinstimmen — eine "
                             "andere Vorverarbeitung als im Training verschiebt jede Zahl.")
    parser.add_argument("--ab-sekunde", type=float, default=None,
                        help="Videoanfang ausblenden. Standard 3 s (Schacht); fuer den "
                             "Rohranfang auf 0 setzen.")
    parser.add_argument("--ziel", type=Path, required=True)
    parser.add_argument("--hoechstens", type=int, default=0,
                        help="Obergrenze der Clips; 0 = alle. Ueberzaehlige werden gezogen, nicht abgeschnitten.")
    parser.add_argument("--frage", default="")
    args = parser.parse_args(argv)

    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits: {args.ziel}")

    auswahl = json.loads(args.auswahl.read_text(encoding="utf-8-sig"))
    videos = auswahl["auswahl"]

    from PIL import Image
    from ultralytics import YOLO

    from nocrop_patch import letterbox_pil

    modelle = []
    for g in args.gewicht:
        m = YOLO(str(g))
        i = next((k for k, n in m.names.items() if n == args.klasse), None)
        if i is None:
            raise SystemExit(f"Klasse {args.klasse!r} fehlt in {g}: {m.names}")
        modelle.append({"name": g.parent.parent.name, "pfad": g, "modell": m, "index": i,
                        "sha256": sha256_datei(g)})

    arbeit = args.ziel.with_name(f".{args.ziel.name}.arbeit")
    shutil.rmtree(arbeit, ignore_errors=True)
    (arbeit / "bilder").mkdir(parents=True)
    (arbeit / "clips").mkdir()
    frames = arbeit / ".frames"

    roh = []
    for i, v in enumerate(videos, start=1):
        video = Path(v["video"])
        print(f"\n[{i}/{len(videos)}] {v['haltung']} — {video.name}", flush=True)
        shutil.rmtree(frames, ignore_errors=True)
        frames.mkdir()
        lauf = subprocess.run(
            [str(FFMPEG), "-v", "error", "-y", "-i", str(video), "-vf", f"fps={args.fps:g}",
             "-q:v", "3", str(frames / "f%06d.jpg")],
            capture_output=True, text=True, timeout=2 * 60 * 60)
        if lauf.returncode != 0:
            # Ein technischer Fehler ist kein "nichts gefunden".
            raise SystemExit(f"ffmpeg fehlgeschlagen bei {video}: {lauf.stderr.strip()[:300]}")

        bilder = sorted(frames.glob("f*.jpg"))
        # Jedes Bild EINMAL vorbereiten, dann durch alle Modelle — die teure
        # Arbeit ist das Dekodieren, nicht die Vorhersage.
        werte = {m["name"]: [] for m in modelle}
        for j, bild in enumerate(bilder):
            with Image.open(bild) as bytes_bild:
                # Erst maskieren, dann letterboxen — genau wie im Lernbestand,
                # wo die Maske im gespeicherten Bild sitzt und Ultralytics
                # danach letterboxt.
                # NICHT `roh` nennen — so heisst die Sammelliste der Vorschlaege.
                bild_roh = (maske_anwenden(bytes_bild, args.mitte_ausblenden)
                            if args.mitte_ausblenden else bytes_bild)
                vorbereitet = letterbox_pil(bild_roh, args.imgsz)
            for m in modelle:
                e = m["modell"].predict(source=vorbereitet, imgsz=args.imgsz, verbose=False)[0]
                werte[m["name"]].append((j / args.fps, float(e.probs.data[m["index"]])))

        for m in modelle:
            stellen = zusammenfassen(werte[m["name"]], args.schwelle,
                                     *( (args.ab_sekunde,) if args.ab_sekunde is not None else () ))
            print(f"     {m['name']}: {len(bilder)} Bilder -> {len(stellen)} Vorschlaege", flush=True)
            for st in stellen:
                quelle = frames / f"f{int(round(st['peak_zeit'] * args.fps)) + 1:06d}.jpg"
                if not quelle.is_file():
                    quelle = frames / f"f{int(round(st['peak_zeit'] * args.fps)):06d}.jpg"
                if not quelle.is_file():
                    continue
                st |= {"haltung": v["haltung"], "video": str(video), "modell": m["name"],
                       "bild_bytes": quelle.read_bytes()}
                roh.append(st)

    shutil.rmtree(frames, ignore_errors=True)
    if not roh:
        raise SystemExit("Kein einziger Vorschlag — nichts zu pruefen.")

    if args.staerkste_je_video:
        beste: dict[tuple[str, str], dict] = {}
        for g in roh:
            schluessel = (g["haltung"], g["modell"])
            if schluessel not in beste or g["max_wert"] > beste[schluessel]["max_wert"]:
                beste[schluessel] = g
        print(f"\n{len(roh)} Meldungen -> {len(beste)} (staerkste je Video und Modell)")
        roh = list(beste.values())

    # Jedes Modell gruppiert fuer sich. Sonst haengt die Zahl der Stellen eines
    # Modells davon ab, mit wem es verglichen wird — am 2026-08-12 gemessen:
    # dasselbe v3 kam auf denselben Videos einmal auf 60, einmal auf 42 Stellen,
    # nur weil der Partner ein anderer war.
    #
    # Fuer die Anzeige werden nur ECHT ueberlappende Gruppen zu einem Clip
    # zusammengelegt; jede Modellgruppe merkt sich, zu welchem Clip sie gehoert.
    gruppen = roh
    gruppen.sort(key=lambda x: (x["haltung"], x["zeit_min"]))
    stellen: list[dict] = []
    for g in gruppen:
        letzte = stellen[-1] if stellen else None
        if letzte and letzte["haltung"] == g["haltung"] and g["zeit_min"] <= letzte["zeit_max"]:
            letzte["zeit_max"] = max(letzte["zeit_max"], g["zeit_max"])
            letzte["modelle"].setdefault(g["modell"], []).append(round(g["max_wert"], 4))
            if g["max_wert"] > letzte["max_wert"]:
                letzte |= {"max_wert": g["max_wert"], "peak_zeit": g["peak_zeit"],
                           "bild_bytes": g["bild_bytes"]}
        else:
            g = dict(g)
            g["modelle"] = {g["modell"]: [round(g["max_wert"], 4)]}
            stellen.append(g)

    print(f"\n{len(gruppen)} Modellgruppen -> {len(stellen)} Clips")
    for m in modelle:
        eigene = sum(1 for g in gruppen if g["modell"] == m["name"])
        clips = sum(1 for x in stellen if m["name"] in x["modelle"])
        print(f"   {m['name']:<24}{eigene:>4} eigene Gruppen auf {clips:>4} Clips")
    roh = stellen

    gesamt = len(roh)
    if args.hoechstens and gesamt > args.hoechstens:
        # Zufaellig ziehen, nicht die staerksten nehmen: Eine Auswahl nach
        # Konfidenz waere keine Precision mehr.
        roh = random.Random(f"{args.auswahl.name}|{args.schwelle}").sample(roh, args.hoechstens)
        print(f"\n{gesamt} Vorschlaege, davon {len(roh)} zufaellig zur Pruefung gezogen")
    roh.sort(key=lambda s: (s["haltung"], s["zeit_min"]))

    faelle, aufloesung = [], []
    for nr, s in enumerate(roh, start=1):
        bild = arbeit / "bilder" / f"{nr:03d}.jpg"
        bild.write_bytes(s["bild_bytes"])
        clip = f"clips/{nr:03d}.mp4"
        von = max(0.0, s["zeit_min"] - VORLAUF_S)
        dauer = max(1.5, (s["zeit_max"] - s["zeit_min"]) + VORLAUF_S + NACHLAUF_S)
        subprocess.run(
            [str(FFMPEG), "-v", "error", "-y", "-ss", f"{von:.2f}", "-i", s["video"],
             "-t", f"{dauer:.2f}", "-an", "-c:v", "libx264", "-preset", "veryfast",
             "-crf", "23", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
             str(arbeit / clip)], capture_output=True, timeout=5 * 60)
        # Blind: weder Konfidenz noch Haltung noch Sekunde im Fall.
        faelle.append({"nummer": nr, "bild": f"bilder/{nr:03d}.jpg",
                       "bild_sha256": hashlib.sha256(s["bild_bytes"]).hexdigest(),
                       "clip": clip})
        aufloesung.append({"nummer": nr, "haltung": s["haltung"], "zeit_s": round(s["peak_zeit"], 1),
                           "modelle": s["modelle"], "bilder": s["bilder"]})

    frage = args.frage or f"Ist ein Befund der Klasse {args.klasse!r} zu sehen?"
    queue = {
        "schema": "lernstufe_vorschlagspruefung_v1",
        "zweck": "Blinde Beurteilung der Modellvorschlaege aus ganzen Videos.",
        "grenze": ("Vorschlags-Precision, nicht Ereignis-Precision. Zwei Vorschlaege "
                   "koennen denselben Befund zeigen. Kein Recall ableitbar."),
        "videoauswahl": str(args.auswahl),
        "videoauswahl_vorregistriert": auswahl.get("warum_vorher"),
        "modelle": [{"name": m["name"], "gewicht": str(m["pfad"]), "sha256": m["sha256"]}
                    for m in modelle],
        "klasse": args.klasse,
        "schwelle": args.schwelle,
        "imgsz": args.imgsz,
        "ab_sekunde": args.ab_sekunde,
        "mitte_ausgeblendet": args.mitte_ausblenden,
        "regel": ("staerkste Meldung je Video" if args.staerkste_je_video
                  else "alle gruppierten Meldungen"),
        "videos": len(videos),
        "vorschlaege_gesamt": gesamt,
        "stichprobe": len(faelle),
        "frage": frage,
        "urteile": [
            {"wert": "sichtbar", "beschriftung": "Sichtbar", "taste": "1"},
            {"wert": "nicht_sichtbar", "beschriftung": "Nicht sichtbar", "taste": "2"},
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

    print(f"\n{gesamt} Vorschlaege aus {len(videos)} Videos, {len(faelle)} zur Pruefung")
    print(f"Queue: {args.ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
