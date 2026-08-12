"""Baut aus Protokoll und Video einen Bogen-Lernbestand ohne menschliche Auswahl.

Der Gedanke: Das Protokoll nennt zu jedem Bogen den Meterstand UND den
Videozaehlerstand. Das Bild an diesem Zaehlerstand zeigt den Bogen. Am
2026-08-09 zweifach am Bild belegt (88218-88316: Sekunde 64 zeigt "1,54 m",
Sekunde 632 zeigt "22,20 m" — genau die Protokollwerte).

WAS DIESER BESTAND IST
Ein Bild-Einordner-Bestand: "Bogen sichtbar" gegen "kein Bogen sichtbar".
KEIN Detektorbestand — das Protokoll sagt, DASS an Meter 22,20 ein Bogen ist,
nicht WO im Bild. Ohne Box kein Detektor-Training.

WAS ER NICHT IST
Keine Messgrundlage. Ein Modell, das hierauf trainiert wurde, darf niemals
hierauf gemessen werden — sonst misst man, wie gut es das Protokoll nachahmt.

DIE WICHTIGSTE EINSCHRAENKUNG
Das Protokoll ist unvollstaendig. In der Blindpruefung vom 2026-08-09 zeigten
91 von 154 Clips einen Bogen, aber hoechstens 66 liessen sich einem
Protokolleintrag zuordnen: mindestens 25 sichtbare Boegen ohne Eintrag, also
jeder vierte. Deshalb duerfen Negative NICHT aus dem Umfeld protokollierter
Boegen stammen, sondern nur aus Haltungen, deren Protokoll ueberhaupt keinen
Bogencode enthaelt — und auch deren Fehlerquote gehoert gemessen, bevor der
Bestand verwendet wird.

Kundenoriginale unter D:\\Haltungen werden ausschliesslich gelesen.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import uuid
from pathlib import Path
from typing import Sequence

PDF_SCAN = Path(r"c:\Sewer-Studio_KI_4.5\.tmp\bcc-code-scan-20260809\bcc_positions_guarded.json")
DIAG = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_auswahl")
ZIEL = Path(r"C:\KI_BRAIN\training\lernstufen\bcc_protokoll_v1")
KUNDEN_WURZEL = Path(r"D:\Haltungen")
FFMPEG = Path(
    r"C:\Users\Besitzer\AppData\Local\Microsoft\WinGet\Packages"
    r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-8.0.1-full_build\bin\ffmpeg.exe"
)

SICHERE_ZUORDNUNG = {"exact_stem_same_folder", "single_in_pdf_folder"}
SPLITS = ("train", "validation", "test")
# Positive erst ab diesem Meterstand. Darunter zeigt das Bild den ROHRANFANG,
# nicht den Bogen: Die Kamera steht noch im Schacht oder gerade in der Oeffnung.
# Am 2026-08-10 an vier Bildern belegt — bei 0,00 m zweimal die Schachtwand mit
# der Rohroeffnung, bei 0,10 m eine Aufnahme, deren eigene Einblendung woertlich
# "Rohranfang" sagt und die zugleich einen Bogen im Protokoll traegt. Bei 0,21 m
# war es dagegen ein einwandfreier Bogen mitten im Rohr.
# Als "Bogen" trainiert wuerde das Modell lernen, dass ein Rohranfang ein Bogen
# ist — und jedes Video faengt mit einem Rohranfang an.
MIN_METER_POSITIV = 0.20

# Zeitfenster um die Protokollstelle. Ein einziges Bild zeigt nur den Moment,
# in dem der Operateur angehalten und den Befund ins Bild genommen hat — die
# privilegierte Sicht. Im Video faehrt die Kamera vorbei: Der Befund kommt von
# vorn ins Bild, ist kurz mittig, verschwindet nach hinten. Genau diese
# Ansichten fehlten. Gemessen am 2026-08-11: 88 % Recall auf Protokollbildern,
# aber nur 1 von 3 im laufenden Video.
# Die Bilder eines Fensters sind abhaengig voneinander; der Split laeuft
# deshalb weiter ueber die physische Haltung, nie ueber das Bild.
ZEITFENSTER_S = (-4.0, -2.0, 0.0, 2.0, 4.0)

NEGATIVE_JE_HALTUNG = 2
# Rand gemieden: Am Anfang steht der Schacht, am Ende die Endkontrolle.
NEGATIV_LAGE = (0.25, 0.45, 0.65, 0.85)

# Zweite Negativquelle: Bilder aus DENSELBEN Haltungen, die den gesuchten Befund
# enthalten — nur weit weg von jeder Fundstelle.
#
# Warum: Die Negativen aus befundfreien Haltungen zeigen ruhiges, gerades Rohr.
# Im echten Video kommen aber Rohrverbindungen, Ablagerungen, Schachtoeffnungen
# und Lichtreflexe vor. Solche Bilder hat das Modell nie als "nicht der Befund"
# gesehen — es kennt nur "Befund" und "langweiliges Rohr". Am 2026-08-11 an
# sechs Videovorschlaegen gemessen: 96 % Precision im Testteil, 50 % im Video.
#
# Der Abstand gilt gegen ALLE bekannten Befunde JEDER Klasse, nicht nur gegen
# den gesuchten. Sonst holt man sich einen unprotokollierten Nachbarbefund als
# Negativ herein.
NAHFELD_ABSTAND_S = 40.0
NAHFELD_JE_HALTUNG = 3

# Eine Haltung, deren Protokoll GENAU EINEN Befund nennt und den direkt am
# Rohranfang, taugt nicht als Nahfeld-Quelle. Der Operateur hat die Klasse dort
# offensichtlich nicht durchgehend codiert; weiter hinten im Rohr stehen dann
# unprotokollierte Vorkommen.
#
# Gefunden am 2026-08-11 in der Sichtpruefung der Anschluss-Nahfelder: alle 3
# nicht sauberen Bilder kamen aus solchen Haltungen, alle 27 sauberen nicht.
# Achtung — die Regel wurde AN diesen Faellen gefunden, sie ist damit noch nicht
# unabhaengig bestaetigt. Sie kostet aber nur 24 von 411 Haltungen.
DUENNES_PROTOKOLL_METER = 2.0


def sha256_datei(pfad: Path) -> str:
    h = hashlib.sha256()
    with pfad.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def haltungsvarianten(name: str) -> set[str]:
    n = (name or "").strip().lower()
    teile = n.split("-", 1)
    return {n, f"{teile[1]}-{teile[0]}"} if len(teile) == 2 else {n}


def physische_haltung(name: str) -> str:
    """Beide Fahrtrichtungen bekommen denselben Gruppenschluessel."""
    return min(haltungsvarianten(name))


def gesperrte_laden() -> tuple[set[str], dict]:
    """Alles, was fuer Training tabu ist — Trainings-, Eval- und Messbestaende.

    Beide Fahrtrichtungen. Ein Bestand, der zum Messen dient, darf nie ins
    Training geraten; sonst misst man spaeter sich selbst.
    """
    gesperrt: set[str] = set()
    herkunft: dict[str, int] = {}

    datei = DIAG / "gesperrte_haltungen.json"
    if datei.is_file():
        werte = json.loads(datei.read_text(encoding="utf-8-sig"))["gesperrt"]
        vorher = len(gesperrt)
        for w in werte:
            gesperrt |= haltungsvarianten(str(w))
        herkunft["Trainings-/Negativ-/Eval-Bestaende"] = len(gesperrt) - vorher

    for name, schluessel in (("messbestand_v1.json", ("sd", "hd")),
                             ("vorfuehrung_v1.json", ("sd", "hd"))):
        p = DIAG / name
        if not p.is_file():
            continue
        d = json.loads(p.read_text(encoding="utf-8-sig"))
        vorher = len(gesperrt)
        for k in schluessel:
            for e in (d.get(k) or {}).get("eintraege", []):
                gesperrt |= haltungsvarianten(e["haltung"])
        herkunft[name] = len(gesperrt) - vorher

    p = DIAG / "messreserve_sd_v2.json"
    if p.is_file():
        d = json.loads(p.read_text(encoding="utf-8-sig"))
        vorher = len(gesperrt)
        for k in ("eintraege", "haltungen", "auswahl"):
            v = d.get(k)
            if isinstance(v, list):
                for x in v:
                    gesperrt |= haltungsvarianten(x["haltung"] if isinstance(x, dict) else str(x))
                break
        herkunft[p.name] = len(gesperrt) - vorher

    return gesperrt, herkunft


def code_ist_lesbar(code: str | None) -> bool:
    """Ein Punkt im Rohcode heisst: die PDF-Zeile wurde nicht sauber gelesen.

    Beim Bogen fiel das als `BCC.YB` auf; dort wird die ganze betroffene Haltung
    fail-closed ausgeschlossen. Gleiche Regel hier: Ein Code, den der Leser nicht
    eindeutig aufloest, darf weder Positiv noch Sperrzone begruenden.
    """
    return bool(code) and code.replace("_", "").isalnum()


def split_fuer(name: str, saat: str) -> str:
    """Stabile Zuteilung ueber die physische Haltung — nie ueber das Bild.

    Zwei Bilder derselben Haltung sind abhaengige Beispiele. Landen sie in
    Training und Pruefung, misst die Pruefung nichts.
    """
    wert = int(hashlib.sha256(f"{saat}|{physische_haltung(name)}".encode()).hexdigest()[:8], 16) % 100
    if wert < 70:
        return "train"
    return "validation" if wert < 85 else "test"


def bild_holen(ffmpeg: Path, video: Path, sekunde: float, ziel: Path) -> bool:
    lauf = subprocess.run(
        [str(ffmpeg), "-v", "error", "-y", "-ss", f"{sekunde:.3f}", "-i", str(video),
         "-frames:v", "1", "-q:v", "2", str(ziel)],
        capture_output=True, text=True)
    return lauf.returncode == 0 and ziel.is_file() and ziel.stat().st_size > 0


def videodauer(ffmpeg: Path, video: Path) -> float | None:
    ffprobe = ffmpeg.with_name("ffprobe.exe")
    lauf = subprocess.run(
        [str(ffprobe), "-v", "error", "-show_entries", "format=duration",
         "-of", "default=nw=1:nk=1", str(video)],
        capture_output=True, text=True)
    zeilen = [z.strip() for z in lauf.stdout.splitlines() if z.strip()]
    try:
        return float(zeilen[-1])
    except (IndexError, ValueError):
        return None


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scan", type=Path, default=PDF_SCAN)
    parser.add_argument("--ziel", type=Path, default=ZIEL)
    parser.add_argument("--saat", default="bcc-lernstufe-v1")
    # Die Ordnernamen werden zu den Klassennamen des Modells. Sie muessen die
    # gesuchte Sache benennen, sonst steht spaeter "bogen" im Anschlussmodell.
    parser.add_argument("--klasse", default="bogen",
                        help="Name der positiven Klasse, z. B. anschluss")
    parser.add_argument("--min-meter", type=float, default=MIN_METER_POSITIV,
                        help="Positive erst ab diesem Meterstand. Fuer BCD/BCE auf 0 setzen — "
                             "dort IST der Rohranfang das gesuchte Merkmal.")
    parser.add_argument("--negativ-quelle", choices=("befundfrei", "nahfeld", "beide"),
                        default="beide",
                        help="Bei BCD/BCE nur 'nahfeld': Jede Haltung HAT einen Rohranfang, "
                             "eine Haltung ohne Code beweist also nichts.")
    parser.add_argument("--ohne-duennes-protokoll", action="store_true",
                        help="Die Regel 'ein Befund am Rohranfang = unzuverlaessig' abschalten. "
                             "Bei BCD/BCE ist genau das der Normalfall.")
    parser.add_argument("--nahfeld-je-haltung", type=int, default=NAHFELD_JE_HALTUNG)
    parser.add_argument("--nahfeld-von-anteil", type=float, default=0.10,
                        help="Ab welchem Anteil der Videolaenge Gegenbeispiele getastet werden. "
                             "Fuer das Rohrende auf 0 setzen — dann kommt der ROHRANFANG als "
                             "Gegenbeispiel herein, und genau den muss das Modell unterscheiden.")
    parser.add_argument("--nahfeld-bis-anteil", type=float, default=0.90)
    parser.add_argument("--negativ-haltungen", type=int, default=0,
                        help="Hoechstzahl befundfreier Quellhaltungen; 0 = alle")
    parser.add_argument("--nahfeld-scans", type=Path, nargs="*", default=[],
                        help="Weitere Klassenscans; ihre Fundstellen sperren Nahfeld-Negative")
    parser.add_argument("--grenze", type=int, default=0, help="nur die ersten N je Klasse (Probelauf)")
    args = parser.parse_args(argv)

    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits und wird nicht ueberschrieben: {args.ziel}")
    try:
        args.ziel.resolve().relative_to(KUNDEN_WURZEL.resolve())
        raise SystemExit("Das Ziel darf nicht im Kundenordner liegen.")
    except ValueError:
        pass

    scan = json.loads(args.scan.read_text(encoding="utf-8-sig"))

    # Fundzeiten JEDER bekannten Klasse je Haltung — Sperrzonen fuer Nahfeld-Negative.
    fundzeiten: dict[str, list[float]] = {}
    for quelle in [args.scan] + list(args.nahfeld_scans):
        if not Path(quelle).is_file():
            continue
        for e in json.loads(Path(quelle).read_text(encoding="utf-8-sig")).get("ergebnisse") or []:
            for pos in e.get("positionen") or []:
                sek = pos.get("video_counter_seconds")
                if sek is not None:
                    fundzeiten.setdefault(e["haltung"], []).append(float(sek))

    gesperrt, herkunft = gesperrte_laden()
    print("Gesperrt fuer Training (beide Richtungen):")
    for k, v in herkunft.items():
        print(f"   {k:<38}{v:>6} Eintraege")
    print(f"   {'gesamt':<38}{len(gesperrt):>6}\n")

    # --- Positive: je protokollierter Bogen ein Bild -------------------------
    positive = []
    verworfen = {"gesperrt": 0, "video_unsicher": 0, "ohne_zaehlerstand": 0, "rohranfang": 0,
                 "code_unlesbar": 0}

    # Fail-closed: Eine Haltung mit auch nur einem unsauber gelesenen Code faellt
    # ganz weg — als Positiv, als Negativ und als Nahfeld-Quelle.
    unlesbar = {e["haltung"] for e in scan["ergebnisse"]
                for pos in (e.get("positionen") or [])
                if not code_ist_lesbar(pos.get("code"))}
    if unlesbar:
        print(f"  {len(unlesbar)} Haltungen wegen unlesbarem Code ausgeschlossen: "
              f"{', '.join(sorted(unlesbar)[:5])}")

    for e in scan["ergebnisse"]:
        h = e["haltung"]
        if h in unlesbar:
            verworfen["code_unlesbar"] += len(e.get("positionen") or [])
            continue
        if haltungsvarianten(h) & gesperrt:
            verworfen["gesperrt"] += len(e.get("positionen") or [])
            continue
        for p in e.get("positionen") or []:
            if p.get("video_match") not in SICHERE_ZUORDNUNG:
                verworfen["video_unsicher"] += 1
                continue
            if p.get("video_counter_seconds") is None or not p.get("video_path"):
                verworfen["ohne_zaehlerstand"] += 1
                continue
            meter = p.get("meter_start")
            if meter is not None and float(meter) <= args.min_meter:
                verworfen["rohranfang"] += 1
                continue
            positive.append({"haltung": h, "code": p["code"],
                             "video": p["video_path"],
                             "sekunde": float(p["video_counter_seconds"]),
                             "meter": meter})

    # --- Negative: nur aus Haltungen OHNE jeden Bogencode --------------------
    negativ_quellen = []
    for e in scan["ergebnisse"] if args.negativ_quelle != "nahfeld" else []:
        h = e["haltung"]
        if e.get("codes"):
            continue
        if haltungsvarianten(h) & gesperrt:
            continue
        # Ein unlesbares PDF ist kein Beweis fuer "kein Bogen".
        if not e.get("pdfs") or e.get("pdfs_lesefehler") or e.get("pdfs_fehler"):
            continue
        if not e.get("pdfs_ok"):
            continue
        videos = e.get("videos") or []
        if len(videos) != 1:
            continue
        negativ_quellen.append({"haltung": h, "video": videos[0]})

    positive.sort(key=lambda x: (x["haltung"], x["sekunde"]))
    negativ_quellen.sort(key=lambda x: x["haltung"])

    # Bei sehr vielen befundfreien Haltungen (E: hat ueber 3000) wuerde das
    # Verhaeltnis kippen. Gezogen wird dann ZUFAELLIG mit der Laufsaat, nicht
    # alphabetisch — sonst haengt der Bestand am Namen der Gemeinde.
    if args.negativ_haltungen and len(negativ_quellen) > args.negativ_haltungen:
        import random
        vorher = len(negativ_quellen)
        negativ_quellen = random.Random(f"neg|{args.saat}").sample(
            negativ_quellen, args.negativ_haltungen)
        negativ_quellen.sort(key=lambda x: x["haltung"])
        print(f"  Negativquellen zufaellig verkleinert: {vorher} -> {len(negativ_quellen)}")

    if args.grenze:
        positive = positive[:args.grenze]
        negativ_quellen = negativ_quellen[:args.grenze]

    print(f"Positive Kandidaten (protokollierte Boegen): {len(positive)}")
    for k, v in verworfen.items():
        print(f"   verworfen, {k:<22}{v:>5}")
    print(f"Negativ-Quellhaltungen (kein Bogencode):     {len(negativ_quellen)}")
    print(f"   davon je {NEGATIVE_JE_HALTUNG} Bilder -> bis zu {len(negativ_quellen)*NEGATIVE_JE_HALTUNG}\n")

    ffmpeg = FFMPEG if FFMPEG.is_file() else Path(shutil.which("ffmpeg") or "")
    if not ffmpeg.is_file():
        raise SystemExit("ffmpeg nicht gefunden.")

    positiv = args.klasse
    negativ = f"kein_{positiv}"
    staging = args.ziel.with_name(f".{args.ziel.name}.staging-{uuid.uuid4().hex}")
    for split in SPLITS:
        for klasse in (positiv, negativ):
            (staging / split / klasse).mkdir(parents=True, exist_ok=True)

    eintraege = []
    gesehen: set[str] = set()
    zaehler = {positiv: 0, negativ: 0, "kein_bild": 0, "doppelt": 0}

    def aufnehmen(quelle: dict, sekunde: float, klasse: str) -> None:
        video = Path(quelle["video"])
        if not video.is_file():
            zaehler["kein_bild"] += 1
            return
        split = split_fuer(quelle["haltung"], args.saat)
        roh = f"{quelle['haltung']}|{video}|{sekunde:.3f}|{klasse}"  # Sekunde trennt das Fenster
        name = f"{''.join(c if c.isalnum() else '_' for c in quelle['haltung'])}_" \
               f"{hashlib.sha256(roh.encode()).hexdigest()[:16]}.jpg"
        ziel = staging / split / klasse / name
        if not bild_holen(ffmpeg, video, sekunde, ziel):
            zaehler["kein_bild"] += 1
            return
        h = sha256_datei(ziel)
        if h in gesehen:
            ziel.unlink(missing_ok=True)
            zaehler["doppelt"] += 1
            return
        gesehen.add(h)
        zaehler[klasse] += 1
        eintraege.append({
            "haltung": quelle["haltung"], "physische_haltung": physische_haltung(quelle["haltung"]),
            "klasse": klasse, "split": split, "video": str(video), "sekunde": round(sekunde, 3),
            "code": quelle.get("code"), "meter": quelle.get("meter"),
            "bild": f"{split}/{klasse}/{name}", "bild_sha256": h,
        })

    for i, p in enumerate(positive, start=1):
        for versatz in ZEITFENSTER_S:
            sekunde = p["sekunde"] + versatz
            if sekunde < 0.0:
                continue
            aufnehmen(p, sekunde, positiv)
        if i % 100 == 0:
            print(f"  Positive {i}/{len(positive)} …", flush=True)

    for i, q in enumerate(negativ_quellen, start=1):
        dauer = videodauer(ffmpeg, Path(q["video"]))
        if dauer is None or dauer < 30:
            zaehler["kein_bild"] += 1
            continue
        for anteil in NEGATIV_LAGE[:NEGATIVE_JE_HALTUNG]:
            aufnehmen(q, dauer * anteil, negativ)
        if i % 100 == 0:
            print(f"  Negative {i}/{len(negativ_quellen)} …", flush=True)

    # Nahfeld-Negative: dieselben Haltungen, weit weg von jeder Fundstelle.
    nahfeld_quellen = []
    zaehler["nahfeld_duennes_protokoll"] = 0
    for e in scan["ergebnisse"] if args.negativ_quelle != "befundfrei" else []:
        if not e.get("codes") or e["haltung"] in unlesbar:
            continue
        if haltungsvarianten(e["haltung"]) & gesperrt:
            continue
        # Bewusst NICHT `eintraege` nennen — so heisst die Sammelliste der
        # erzeugten Bilder, die weiter unten ins Manifest geht.
        befunde = e.get("positionen") or []
        if (not args.ohne_duennes_protokoll and len(befunde) == 1
                and (befunde[0].get("meter_start") or 0.0) < DUENNES_PROTOKOLL_METER):
            zaehler["nahfeld_duennes_protokoll"] += 1
            continue
        pfade = {p["video_path"] for p in befunde if p.get("video_path")}
        if len(pfade) != 1:
            continue
        nahfeld_quellen.append({"haltung": e["haltung"], "video": next(iter(pfade))})
    if args.grenze:
        nahfeld_quellen = nahfeld_quellen[:args.grenze]

    zaehler["nahfeld"] = 0
    for i, q in enumerate(nahfeld_quellen, start=1):
        dauer = videodauer(ffmpeg, Path(q["video"]))
        if dauer is None or dauer < 60:
            continue
        gesperrt_zeiten = fundzeiten.get(q["haltung"], [])
        genommen = 0
        # Gleichmaessig ueber das Video tasten und alles nahe einem Fund auslassen.
        for schritt in range(20):
            if genommen >= args.nahfeld_je_haltung:
                break
            spanne = args.nahfeld_bis_anteil - args.nahfeld_von_anteil
            sekunde = dauer * (args.nahfeld_von_anteil + spanne * schritt / 19)
            # Das Videoende gehoert nie zu den Negativen: dort steht das Rohrende,
            # und ein Rohrende sieht aus wie ein Rohranfang.
            if sekunde > dauer - NAHFELD_ABSTAND_S:
                continue
            if any(abs(sekunde - t) < NAHFELD_ABSTAND_S for t in gesperrt_zeiten):
                continue
            vorher = zaehler[negativ]
            aufnehmen(q, sekunde, negativ)
            if zaehler[negativ] > vorher:
                genommen += 1
                zaehler["nahfeld"] += 1
        if i % 100 == 0:
            print(f"  Nahfeld {i}/{len(nahfeld_quellen)} …", flush=True)

    manifest = {
        "schema": "bcc_lernstufe_protokoll_v1",
        "zweck": "Bild-Einordner aus Protokoll und Video, ohne menschliche Auswahl",
        "zeitfenster_s": list(ZEITFENSTER_S),
        "nahfeld_abstand_s": NAHFELD_ABSTAND_S,
        "nahfeld_je_haltung": args.nahfeld_je_haltung,
        "nahfeld_anteil": [args.nahfeld_von_anteil, args.nahfeld_bis_anteil],
        "min_meter_positiv": args.min_meter,
        "negativ_quelle": args.negativ_quelle,
        "duennes_protokoll_regel": not args.ohne_duennes_protokoll,
        "nahfeld_quellhaltungen": len(nahfeld_quellen),
        "negativ_haltungen_grenze": args.negativ_haltungen or None,
        "kein_detektorbestand": "Das Protokoll nennt die Stelle, nicht die Box. Ohne Box kein Detektor-Training.",
        "keine_messgrundlage": ("Ein hierauf trainiertes Modell darf niemals hierauf gemessen werden. "
                                "Sonst misst man, wie gut es das Protokoll nachahmt."),
        "bekannte_schwaeche": ("Das Protokoll ist unvollstaendig: In der Blindpruefung vom 2026-08-09 "
                               "zeigten 91 von 154 Clips einen Bogen, hoechstens 66 waren einem Eintrag "
                               "zuzuordnen — mindestens 25 sichtbare Boegen ohne Eintrag. Die Fehlerquote "
                               "der Negativen ist noch NICHT gemessen."),
        "status": "unvalidiert_negative_ungemessen",
        "quelle_scan": str(args.scan),
        "saat": args.saat,
        "gesperrte_haltungen": len(gesperrt),
        "zusammenfassung": zaehler,
        "klasse_positiv": positiv,
        "klasse_negativ": negativ,
        "splits": {s: {k: sum(1 for e in eintraege if e["split"] == s and e["klasse"] == k)
                       for k in (positiv, negativ)} for s in SPLITS},
        "eintraege": eintraege,
    }
    text = json.dumps(manifest, indent=1, ensure_ascii=False)
    # write_bytes statt write_text: Unter Windows macht write_text aus einem
    # Zeilenumbruch ein CR+LF, der Hash daneben wird aber ueber die reine
    # LF-Fassung gebildet. Die Bindung passte dadurch von Anfang an nie —
    # aufgefallen erst am 2026-08-12, als die Freigabedatei sie zum ersten
    # Mal wirklich geprueft hat.
    (staging / "manifest.json").write_bytes(text.encode("utf-8"))
    (staging / "manifest.sha256").write_text(
        hashlib.sha256(text.encode("utf-8")).hexdigest() + "\n", encoding="utf-8")

    args.ziel.parent.mkdir(parents=True, exist_ok=True)
    staging.rename(args.ziel)

    print(f"\n{'':2}{'':<12}{positiv:>10}{negativ:>14}")
    for s in SPLITS:
        b = manifest["splits"][s][positiv]
        n = manifest["splits"][s][negativ]
        print(f"  {s:<12}{b:>10}{n:>14}")
    print(f"  {'gesamt':<12}{zaehler[positiv]:>10}{zaehler[negativ]:>14}")
    print(f"\n  bytegleich verworfen {zaehler['doppelt']}, kein Bild {zaehler['kein_bild']}")
    print(f"\nBestand: {args.ziel}")
    print(f"Manifest-SHA-256: {(args.ziel / 'manifest.sha256').read_text().strip()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
