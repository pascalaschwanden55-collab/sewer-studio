"""Misst den Recall des Bogen-Copiloten gegen die protokollierten PDF-Boegen.

Gemessen wird NUR Recall. Das PDF nennt die Boegen, die der Operateur
aufgenommen hat — es ist keine vollstaendige Wahrheit. Ein Vorschlag ohne
PDF-Eintrag kann ein echter, nicht protokollierter Bogen sein. Eine
Precision-Zahl aus diesem Bestand waere eine Falschaussage.

Verglichen wird ueber den Videozaehlerstand aus dem Protokoll (+/- 15 s),
getrennt nach SD und HD.

Der urspruenglich geplante Metervergleich ist NICHT moeglich. Der OSD-Leser
liest auf dem Archivbestand D:\\Haltungen nur 11 % (SD) beziehungsweise 5 % (HD)
der Bilder; keine einzige Haltung erreicht 70 %. Die frueher belegten 76-95 %
stammen aus D:\\Videoprojekte, also aus wenigen neueren Exportprojekten mit
einem anderen OSD-Stil. Beleg: meter_abdeckung.json im Auswahlordner.

Die Zeitachse ist dagegen geprueft: Auf Haltung 88218-88316 zeigt das Video bei
Sekunde 64 sichtbar "1,54 m" und bei Sekunde 632 "22,20 m" — genau die Werte,
die das Protokoll fuer diese Zaehlerstaende nennt. Der Nullpunkt des
Protokollzaehlers stimmt also mit der Videodatei ueberein.

Was NICHT als Fehler zaehlt: ein technischer Fehler, ein Bogen ohne
vergleichbare Position und eine Haltung mit unsicherer Videozuordnung. Solche
Faelle werden getrennt ausgewiesen. "Nichts gefunden" und "nichts gesehen"
sind verschiedene Aussagen.

Kundenoriginale unter D:\\Haltungen werden ausschliesslich gelesen. Es wird
nichts trainiert, aktiviert oder in den Goldbestand geschrieben.
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
import time
from pathlib import Path
from typing import Sequence

sys.path.insert(0, str(Path(__file__).resolve().parent))

from bcc_copilot_durchlauf import (  # noqa: E402
    BODEN_CONF,
    arbeitspunkt_laden,
    bilder_holen,
    ffmpeg_finden,
    luecken_fuellen,
    zusammenfassen,
)

MESSBESTAND = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_auswahl\messbestand_v1.json")
PDF_SCAN = Path(r"c:\Sewer-Studio_KI_4.5\.tmp\bcc-code-scan-20260809\bcc_positions_guarded.json")
ZIEL = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_recall_20260809")

METER_TOLERANZ = 1.0   # SD
ZEIT_TOLERANZ = 15.0   # HD

# Eine unsichere Videozuordnung macht jeden Vergleich wertlos: Der Vorschlag
# kaeme dann aus einem anderen Video als der protokollierte Bogen.
UNSICHERE_ZUORDNUNG = {"ambiguous_pdf_folder"}


def spanne_abstand(minimum, maximum, wert) -> float:
    """Abstand eines Werts zu einem Bereich; 0 wenn er darin liegt."""
    if minimum is None or maximum is None:
        return float("inf")
    if wert < minimum:
        return minimum - wert
    return wert - maximum if wert > maximum else 0.0


def zuordnen(solls: list[dict], vorschlaege: list[dict], schluessel: str,
             toleranz: float) -> tuple[list[dict], set[int]]:
    """Ordnet Soll-Boegen und Vorschlaege einander zu, jeder hoechstens einmal.

    Gierig ueber den kleinsten Abstand — nicht in Listenreihenfolge, sonst
    entscheidet die Sortierung mit, wer als Treffer gilt.
    """
    paare = []
    for si, soll in enumerate(solls):
        if soll["wert"] is None:
            continue
        for vi, vorschlag in enumerate(vorschlaege):
            if schluessel == "meter":
                # Nur ein gelesener, nicht geschaetzter Meterstand verortet.
                if vorschlag["meter_min"] is None or vorschlag["meter_geschaetzt"]:
                    continue
                d = spanne_abstand(vorschlag["meter_min"], vorschlag["meter_max"], soll["wert"])
            else:
                d = spanne_abstand(vorschlag["zeit_min"], vorschlag["zeit_max"], soll["wert"])
            if d <= toleranz:
                paare.append((d, si, vi))

    paare.sort()
    soll_belegt: set[int] = set()
    vorschlag_belegt: set[int] = set()
    treffer = []
    for d, si, vi in paare:
        if si in soll_belegt or vi in vorschlag_belegt:
            continue
        soll_belegt.add(si)
        vorschlag_belegt.add(vi)
        treffer.append({"soll_index": si, "vorschlag_index": vi, "abstand": round(d, 3),
                        "code": solls[si]["code"], "stufe": vorschlaege[vi]["stufe"]})
    return treffer, soll_belegt


def haltung_messen(eintrag: dict, positionen: list[dict], gruppe: str,
                   modell, templates, ffmpeg: Path, punkt: dict,
                   arbeitsordner: Path, fps: float) -> dict:
    """Ein Video durchlaufen und gegen seine PDF-Boegen vergleichen."""
    from osd_meter_leser import lese_meter, plausibilisiere_sequenz

    haltung = eintrag["haltung"]
    # Der Videopfad kommt aus dem PDF-Scan: Er ist an das Protokoll gebunden,
    # aus dem die Soll-Boegen stammen. Das erste Video im Ordner waere oft ein
    # anderer, aelterer Befahrungsstand.
    videopfade = {p["video_path"] for p in positionen if p.get("video_path")}
    if len(videopfade) != 1:
        return {"haltung": haltung, "gruppe": gruppe, "zustand": "nicht ausgewertet",
                "grund": f"{len(videopfade)} verschiedene Videopfade im Protokoll"}
    video = Path(next(iter(videopfade)))
    if not video.is_file():
        return {"haltung": haltung, "gruppe": gruppe, "zustand": "nicht ausgewertet",
                "grund": f"Video fehlt: {video}"}

    frames = arbeitsordner / "frames"
    shutil.rmtree(frames, ignore_errors=True)
    gestartet = time.perf_counter()
    try:
        bilder = bilder_holen(ffmpeg, video, frames, fps)
    except SystemExit as fehler:
        shutil.rmtree(frames, ignore_errors=True)
        return {"haltung": haltung, "gruppe": gruppe, "zustand": "nicht ausgewertet",
                "grund": f"Bildextraktion: {fehler}", "video": str(video)}

    roh_meter: list[tuple[float, float | None]] = []
    conf_je_zeit: dict[float, float] = {}
    technische_fehler = 0
    for _nummer, zeit, pfad in bilder:
        try:
            ergebnis = modell.predict(source=str(pfad), conf=min(BODEN_CONF, punkt["min"]),
                                      imgsz=1280, classes=[14], device=0, verbose=False)[0]
        except Exception:
            # Ein technischer Fehler ist kein "kein Bogen".
            technische_fehler += 1
            continue
        roh_meter.append((zeit, lese_meter(pfad, templates)["meter"]))
        if ergebnis.boxes is not None and len(ergebnis.boxes) > 0:
            conf_je_zeit[zeit] = float(max(b.conf[0].item() for b in ergebnis.boxes))

    shutil.rmtree(frames, ignore_errors=True)
    if technische_fehler:
        return {"haltung": haltung, "gruppe": gruppe, "zustand": "nicht ausgewertet",
                "grund": f"{technische_fehler} technische Inferenzfehler",
                "video": str(video), "bilder": len(bilder)}

    geprueft = plausibilisiere_sequenz(roh_meter)
    gelesen = sum(1 for _, m in geprueft if m is not None)
    gefuellt = luecken_fuellen(geprueft)
    treffer_bilder = [
        {"zeit": z, "meter": m, "geschaetzt": g, "conf": conf_je_zeit[z]}
        for z, m, g in gefuellt if z in conf_je_zeit
    ]
    vorschlaege = zusammenfassen(treffer_bilder, punkt["min"], punkt["stark"])

    # --- Soll-Boegen aufbereiten --------------------------------------------
    # Beide Gruppen ueber die Zeit; der Metervergleich scheitert am Leser
    # (siehe Modulkopf). Der Sollmeter bleibt als Ablesehilfe erhalten.
    solls = [{"code": p["code"], "wert": p.get("video_counter_seconds"),
              "einheit": "s", "meter_soll": p.get("meter_start")}
             for p in positionen]
    schluessel, toleranz = "zeit", ZEIT_TOLERANZ

    wertbar = [s for s in solls if s["wert"] is not None]
    treffer, belegt = zuordnen(solls, vorschlaege, schluessel, toleranz)

    ergebnis = {
        "haltung": haltung, "gruppe": gruppe, "zustand": "ausgewertet",
        "video": str(video), "bilder": len(bilder),
        "meter_abdeckung": round(gelesen / max(1, len(bilder)), 4),
        "laufzeit_s": round(time.perf_counter() - gestartet, 1),
        "soll_boegen": len(solls),
        "soll_wertbar": len(wertbar),
        "soll_ohne_vergleichswert": len(solls) - len(wertbar),
        "vorschlaege": len(vorschlaege),
        "vorschlaege_stark": sum(1 for v in vorschlaege if v["stufe"] == "stark"),
        "getroffen": len(treffer),
        "verpasst": len(wertbar) - len(treffer),
        "zuordnungen": treffer,
        "verpasste_codes": [s["code"] for i, s in enumerate(solls)
                            if i not in belegt and s["wert"] is not None],
        "vorschlaege_roh": vorschlaege,
        # Die Einzelbildfolge wird mitgeschrieben: Die Inferenz ist der teure
        # Teil, die Zusammenfassung ist billig. So laesst sich spaeter jede
        # Schwelle auswerten, ohne 86 Videos erneut zu rechnen — und ohne die
        # Versuchung, waehrend des Laufens an der Schwelle zu drehen.
        "einzelbilder": [{"zeit": round(t["zeit"], 3),
                          "meter": None if t["meter"] is None else round(t["meter"], 3),
                          "geschaetzt": t["geschaetzt"],
                          "conf": round(t["conf"], 4)} for t in treffer_bilder],
        "soll": [{"code": s["code"], "zeit_s": s["wert"], "meter": s.get("meter_soll")}
                 for s in solls],
    }

    # Nebenbefund: Wie viele Vorschlaege haetten ueberhaupt einen belastbaren
    # Meterstand? Zeigt, wie weit der Leser den Copiloten im Alltag traegt.
    ergebnis["vorschlaege_ohne_belastbaren_meter"] = sum(
        1 for v in vorschlaege if v["meter_min"] is None or v["meter_geschaetzt"])

    # Empfindlichkeit der Toleranz: Eine Recall-Zahl, die bei +/- 10 s
    # zusammenbricht und bei +/- 20 s davonlaeuft, ist keine belastbare Zahl.
    for weite in (10.0, 20.0, 30.0):
        weitere, _ = zuordnen(solls, vorschlaege, "zeit", weite)
        ergebnis[f"getroffen_bei_{int(weite)}s"] = len(weitere)
    return ergebnis


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--kandidat", default="bcc_nc15_seed46_20260808")
    parser.add_argument("--fps", type=float, default=1.0)
    parser.add_argument("--ffmpeg", type=Path, default=None)
    parser.add_argument("--ziel", type=Path, default=ZIEL)
    parser.add_argument("--grenze", type=int, default=0,
                        help="nur die ersten N Haltungen (Probelauf)")
    args = parser.parse_args(argv)

    punkt = arbeitspunkt_laden(args.kandidat)
    ffmpeg = ffmpeg_finden(args.ffmpeg)
    bestand = json.loads(MESSBESTAND.read_text(encoding="utf-8-sig"))
    scan = json.loads(PDF_SCAN.read_text(encoding="utf-8-sig"))
    positionen = {e["haltung"]: e.get("positionen", []) for e in scan["ergebnisse"]}

    args.ziel.mkdir(parents=True, exist_ok=True)
    einzeln = args.ziel / "haltungen"
    einzeln.mkdir(exist_ok=True)
    arbeit = args.ziel / "_arbeit"
    arbeit.mkdir(exist_ok=True)

    auftraege = []
    for gruppe in ("sd", "hd"):
        for e in bestand[gruppe]["eintraege"]:
            auftraege.append((gruppe, e))
    if args.grenze:
        auftraege = auftraege[:args.grenze]

    print(f"Kandidat     {args.kandidat}")
    print(f"Arbeitspunkt conf >= {punkt['min']:.2f}, stark ab {punkt['stark']:.2f}")
    print(f"Messbestand  {MESSBESTAND}")
    print(f"{len(auftraege)} Haltungen\n")

    from ultralytics import YOLO
    from osd_meter_leser import rendere_templates

    modell = YOLO(str(punkt["gewicht"]))
    templates = rendere_templates()

    for index, (gruppe, eintrag) in enumerate(auftraege, start=1):
        ziel = einzeln / f"{gruppe}_{eintrag['haltung'].replace('.', '_')}.json"
        if ziel.is_file():
            print(f"[{index}/{len(auftraege)}] {eintrag['haltung']:<26} bereits vorhanden")
            continue

        pos = positionen.get(eintrag["haltung"], [])
        unsicher = {p.get("video_match") for p in pos} & UNSICHERE_ZUORDNUNG
        if unsicher:
            ergebnis = {"haltung": eintrag["haltung"], "gruppe": gruppe,
                        "zustand": "nicht ausgewertet",
                        "grund": "unsichere Videozuordnung im PDF-Scan",
                        "soll_boegen": len(pos)}
        else:
            ergebnis = haltung_messen(eintrag, pos, gruppe, modell, templates,
                                      ffmpeg, punkt, arbeit, args.fps)

        temp = ziel.with_suffix(".json.tmp")
        temp.write_text(json.dumps(ergebnis, indent=1, ensure_ascii=False), encoding="utf-8")
        temp.replace(ziel)

        if ergebnis["zustand"] == "ausgewertet":
            print(f"[{index}/{len(auftraege)}] {eintrag['haltung']:<26} "
                  f"{ergebnis['getroffen']}/{ergebnis['soll_wertbar']} Boegen, "
                  f"{ergebnis['vorschlaege']} Vorschlaege, "
                  f"Meter {ergebnis['meter_abdeckung']:.0%}, "
                  f"{ergebnis['laufzeit_s']:.0f} s", flush=True)
        else:
            print(f"[{index}/{len(auftraege)}] {eintrag['haltung']:<26} "
                  f"NICHT AUSGEWERTET — {ergebnis['grund']}", flush=True)

    shutil.rmtree(arbeit, ignore_errors=True)
    print("\nEinzelergebnisse:", einzeln)
    print("Auswertung mit bcc_pdf_recall_bericht.py")
    return 0


if __name__ == "__main__":
    sys.exit(main())
