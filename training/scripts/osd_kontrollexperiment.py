#!/usr/bin/env python3
"""OSD-Kontrollexperiment, Teil B: Liest das BCC-Modell den eingebrannten Text?

DIAGNOSE — kein Produktcode, kein Eingriff in fail-closed Werkzeuge. Der
Bestand `detect_benchmark_v1` bleibt unberuehrt; Varianten entstehen in einem
eigenen Diagnoseordner. Die fail-closed Release-Evaluatoren binden Bildbytes
per SHA-256 und wuerden modifizierte Pixel ablehnen — genau darum traegt
dieses Skript den Abgleich selbst und dokumentiert seine Vereinfachungen.

Frage: Nutzt das Modell den OSD-Text (Ecken) als Merkmal statt des Schadens?
Drei Varianten, dasselbe Modell, deterministische Inferenz:

- V0 Original (Referenz)
- V1 maskiert: erkannte Schriftzeichen unscharf (adaptiv je Bild, nur
  Glyphenpixel — kein Balken, der Rohrwand entfernt)
- V2 vertauscht: die eigenen OSD-Streifen werden in 8-px-Bloecken wuerfelt
  wieder eingesetzt (gleiche Pixel, gleiche Stellen, Inhalt unleserlich —
  kein Fremdmaterial, kein Stilbruch)

Die Texterkennung arbeitet ohne feste Zonen: helle Klein-Komponenten im
oberen und unteren Bildrahmen (Hoehe 6–34 px). Damit sind alle OSD-Layouts
der Projekte abgedeckt; feste Eckzonen verfehlen fremde Layouts (Sichtprobe
am Rauchtest).

Entscheidungsregel (vor dem Lauf festgelegt, siehe
docs/briefings/osd-kontrollexperiment-2026-08-08.md):
V1 ≈ V0 und V2 ≈ V0 -> OSD irrelevant. V2 faellt deutlich (>2 TP) -> das
Modell liest -> Konsequenz: randomisierte OSD-Augmentation, nicht Loeschung.

Gemessen wird nur die Pilotklasse (ID 14, BCC_bogen): TP gegen die
menschlich gesetzten BCC-Boxen (IoU >= 0,5, gierig je Sollbox), Feuer auf
echten Negativbildern und auf Fremdschaden-Bildern (positive ohne BCC-Box).
`exclude`-Bilder werden nie gewertet.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

from PIL import Image, ImageFilter

OSD_ZONEN = {
    "oben_links": (0.00, 0.00, 0.50, 0.13),
    "oben_rechts": (0.70, 0.00, 1.00, 0.07),
    "unten_links": (0.00, 0.88, 0.20, 1.00),
    "unten_rechts": (0.72, 0.86, 1.00, 1.00),
}
BCC_CLASS_ID = 14
IOU_SCHWELLE = 0.5
CONFS = (0.10, 0.25, 0.50)

# Schrifterkennung: hell, klein, nur im oberen/unteren Bildrahmen.
RAHMEN_OBEN = 0.18
RAHMEN_UNTEN = 0.85
GLYPHE_MIN_H, GLYPHE_MAX_H = 6, 34
GLYPHE_MAX_W_ANTEIL = 0.09
HELLIGKEIT = 150


def text_maske(bild: Image.Image) -> "object":
    """Binärmaske mutmasslicher OSD-Glyphen (adaptiv, layout-unabhaengig)."""
    import cv2
    import numpy as np

    arr = np.asarray(bild.convert("RGB"))
    hell = arr.max(axis=2)
    kandidaten = (hell > HELLIGKEIT).astype("uint8")
    h, w = hell.shape
    # Nur der obere und untere Rahmen wird ueberhaupt betrachtet.
    rahmen = np.zeros_like(kandidaten)
    rahmen[: round(h * RAHMEN_OBEN), :] = 1
    rahmen[round(h * RAHMEN_UNTEN):, :] = 1
    kandidaten *= rahmen
    anzahl, labels, stats, _ = cv2.connectedComponentsWithStats(kandidaten, 8)
    maske = np.zeros_like(kandidaten)
    for i in range(1, anzahl):
        x, y, bw, bh, flaeche = stats[i]
        if (GLYPHE_MIN_H <= bh <= GLYPHE_MAX_H and bw <= w * GLYPHE_MAX_W_ANTEIL
                and 6 <= flaeche <= bw * bh * 0.9):
            maske[labels == i] = 1
    kernel = np.ones((5, 5), "uint8")
    return cv2.dilate(maske, kernel, iterations=2)


def mache_maskiert(bild: Image.Image) -> Image.Image:
    import numpy as np

    maske = text_maske(bild).astype(bool)
    unscharf = np.asarray(
        bild.convert("RGB").filter(ImageFilter.GaussianBlur(radius=13)))
    arr = np.asarray(bild.convert("RGB")).copy()
    arr[maske] = unscharf[maske]
    return Image.fromarray(arr)


def mache_vertauscht(bild: Image.Image, seed: int = 7) -> Image.Image:
    """Eigene OSD-Streifen in 32-px-Bloecken wuerfeln — gleiche Pixel, gleiche
    Stellen, Inhalt unleserlich. Nur Glyphenpixel werden zurueckgeschrieben."""
    import numpy as np

    arr = np.asarray(bild.convert("RGB")).copy()
    maske = text_maske(bild).astype(bool)
    h, w = maske.shape
    zufall = np.random.default_rng(seed)
    for y0, y1 in ((0, round(h * RAHMEN_OBEN)), (round(h * RAHMEN_UNTEN), h)):
        streifen = arr[y0:y1].copy()
        gruppe = [(x, min(x + 32, w)) for x in range(0, w, 32)]
        gruppe = [b for b in gruppe if b[1] - b[0] == 32]
        if len(gruppe) >= 2:
            quell = [streifen[:, a:b].copy() for a, b in gruppe]
            for (a, b), q in zip(
                    (gruppe[i] for i in zufall.permutation(len(gruppe))), quell):
                streifen[:, a:b] = q
        streifen_maske = maske[y0:y1]
        aus = arr[y0:y1]
        aus[streifen_maske] = streifen[streifen_maske]
    return Image.fromarray(arr)


def mache_kontrolle(bild: Image.Image, seed: int = 7) -> Image.Image:
    """V3-Kontrolle: dieselbe Verwuerfelung ausserhalb der OSD-Zone, auf den
    seitlichen Randstreifen mittlerer Hoehe (kein Text, ueblicherweise glatte
    Rohrwand). Beantwortet: kippt das Modell schon durch beliebige
    Pixelaenderung, ohne einen Buchstaben gelesen zu haben? Faellt V3 genauso
    wie V2, ist der Effekt allgemeine Empfindlichkeit — nicht Lesen."""
    import numpy as np

    arr = np.asarray(bild.convert("RGB")).copy()
    h, w = arr.shape[:2]
    zufall = np.random.default_rng(seed + 1)
    y0, y1 = round(h * 0.25), round(h * 0.65)
    for x0, x1 in ((0, round(w * 0.10)), (round(w * 0.90), w)):
        teil = arr[y0:y1, x0:x1].copy()
        bloecke = [(y, min(y + 32, y1 - y0)) for y in range(0, y1 - y0, 32)]
        volle = [b for b in bloecke if b[1] - b[0] == 32]
        if len(volle) >= 2:
            quell = [teil[a:b].copy() for a, b in volle]
            for (a, b), q in zip(
                    (volle[i] for i in zufall.permutation(len(volle))), quell):
                teil[a:b] = q
        arr[y0:y1, x0:x1] = teil
    return Image.fromarray(arr)


def iou(a: tuple[float, float, float, float], b: tuple[float, float, float, float]) -> float:
    ix = min(a[2], b[2]) - max(a[0], b[0])
    iy = min(a[3], b[3]) - max(a[1], b[1])
    if ix <= 0 or iy <= 0:
        return 0.0
    inter = ix * iy
    flaeche_a = (a[2] - a[0]) * (a[3] - a[1])
    flaeche_b = (b[2] - b[0]) * (b[3] - b[1])
    return inter / (flaeche_a + flaeche_b - inter)


def yolo_zu_xyxy(box: dict, w: int, h: int) -> tuple[float, float, float, float]:
    bw = box["width"] * w
    bh = box["height"] * h
    cx = box["x_center"] * w
    cy = box["y_center"] * h
    return (cx - bw / 2, cy - bh / 2, cx + bw / 2, cy + bh / 2)


def lade_faelle(benchmark: Path) -> list[dict]:
    kandidaten = json.loads(
        (benchmark / "_candidates.json").read_text(encoding="utf-8-sig"))["candidates"]
    review = json.loads(
        Path(r"C:\KI_BRAIN\eval_review\detect_benchmark_v1_review.json").read_text(
            encoding="utf-8-sig"))["decisions"]

    faelle = []
    for k in kandidaten:
        entscheidung = review.get(k["id"])
        if entscheidung is None:
            continue
        urteil = entscheidung.get("decision")
        if urteil == "exclude":
            continue
        bcc_boxen = [
            a["box"] for a in entscheidung.get("annotations") or []
            if a.get("class_id") == BCC_CLASS_ID
        ]
        herkunft = next(
            (r.get("text") for r in k.get("operator_references") or []
             if r.get("code") == "HERKUNFT"), "unbekannt")
        if urteil == "negative":
            rolle = "negativ"
        elif bcc_boxen:
            rolle = "bcc"
        else:
            rolle = "fremdschaden"
        faelle.append({
            "id": k["id"],
            "bild": str(benchmark / k["image_path"]),
            "rolle": rolle,
            "bcc_boxen": bcc_boxen,
            "herkunft": herkunft,
        })
    return faelle


def werte_aus(dets: list[tuple[float, tuple[float, float, float, float]]],
              fall: dict, w: int, h: int) -> dict:
    """dets: [(conf, xyxy)] der Pilotklasse. Gieriger Abgleich je Sollbox."""
    soll = [yolo_zu_xyxy(b, w, h) for b in fall["bcc_boxen"]]
    gefunden = 0
    for s in soll:
        if any(iou(s, d) >= IOU_SCHWELLE for _, d in dets):
            gefunden += 1
    return {"soll": len(soll), "tp": gefunden, "feuer": len(dets)}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--benchmark", type=Path, default=Path(
        r"C:\KI_BRAIN\eval_set\subsets\detect_benchmark_v1"))
    parser.add_argument("--weights", type=Path, default=Path(
        r"C:\KI_BRAIN\training\diagnostics\bcc_nc15_20260807\runs\seed44\run\weights\best.pt"))
    parser.add_argument("--out", type=Path, default=Path(
        r"C:\KI_BRAIN\training\diagnostics\osd_kontrollexperiment_20260808"))
    parser.add_argument("--device", default="cpu")
    parser.add_argument("--limit", type=int, default=0, help="Nur erste N Faelle (Rauchtest)")
    parser.add_argument("--v3", action="store_true",
                        help="Nur die Kontrollvariante bauen und werten; Ergebnis "
                             "wird in einen bestehenden Bericht eingefuegt. Faellt "
                             "v3 genauso wie v2, ist der Effekt allgemeine "
                             "Pixelempfindlichkeit, nicht Lesen.")
    args = parser.parse_args(argv)

    faelle = lade_faelle(args.benchmark)
    if args.limit:
        faelle = faelle[: args.limit]
    rollen = {}
    for f in faelle:
        rollen[f["rolle"]] = rollen.get(f["rolle"], 0) + 1
    print(f"Faelle: {len(faelle)} ({rollen})")

    # Varianten bauen
    var_dir = {"v1": args.out / "varianten" / "v1_maskiert",
               "v2": args.out / "varianten" / "v2_vertauscht",
               "v3": args.out / "varianten" / "v3_kontrolle"}
    bauer = {"v1": mache_maskiert, "v2": mache_vertauscht, "v3": mache_kontrolle}
    zu_bauen = ("v3",) if args.v3 else ("v1", "v2")
    for name in zu_bauen:
        var_dir[name].mkdir(parents=True, exist_ok=True)
    gebaut = 0
    for fall in faelle:
        with Image.open(fall["bild"]) as img:
            img.load()
            bild = img.convert("RGB")
        for name in zu_bauen:
            ziel = var_dir[name] / Path(fall["bild"]).name
            if not ziel.exists():
                bauer[name](bild).save(ziel)
                gebaut += 1
    print(f"Varianten gebaut: {gebaut} (Rest vorhanden)")

    from ultralytics import YOLO
    model = YOLO(str(args.weights))
    namen = model.names
    assert namen.get(BCC_CLASS_ID) == "BCC_bogen", namen

    zu_werten = (("v3", lambda f: str(var_dir["v3"] / Path(f["bild"]).name)),) if args.v3 else (
        ("v0", lambda f: f["bild"]),
        ("v1", lambda f: str(var_dir["v1"] / Path(f["bild"]).name)),
        ("v2", lambda f: str(var_dir["v2"] / Path(f["bild"]).name)),
    )
    ergebnisse: dict[str, dict] = {}
    for variante, pfad_fn in zu_werten:
        started = time.perf_counter()
        # conf -> aggregat
        agg = {c: {"soll": 0, "tp": 0, "fa_negativ": 0, "fa_fremd": 0}
               for c in CONFS}
        for fall in faelle:
            with Image.open(pfad_fn(fall)) as img:
                img.load()
                w, h = img.size
                res = model.predict(source=img, conf=min(CONFS), imgsz=1280,
                                    device=args.device, verbose=False,
                                    classes=[BCC_CLASS_ID])
            dets: list[tuple[float, tuple[float, float, float, float]]] = []
            boxes = res[0].boxes if res else None
            if boxes is not None:
                for b in boxes:
                    dets.append((float(b.conf[0].cpu().item()),
                                 tuple(b.xyxy[0].cpu().tolist())))
            for c in CONFS:
                ueber = [d for d in dets if d[0] >= c]
                wert = werte_aus(ueber, fall, w, h)
                a = agg[c]
                a["soll"] += wert["soll"]
                a["tp"] += wert["tp"]
                if fall["rolle"] == "negativ" and wert["feuer"]:
                    a["fa_negativ"] += 1
                if fall["rolle"] == "fremdschaden" and wert["feuer"]:
                    a["fa_fremd"] += 1
        dauer = time.perf_counter() - started
        ergebnisse[variante] = {str(c): agg[c] for c in CONFS}
        print(f"{variante}: {dauer:.0f}s — " + " | ".join(
            f"conf {c}: tp {agg[c]['tp']}/{agg[c]['soll']}, "
            f"neg {agg[c]['fa_negativ']}, fremd {agg[c]['fa_fremd']}"
            for c in CONFS))

    ziel = args.out / "ergebnis.json"
    if args.v3 and ziel.exists():
        bericht = json.loads(ziel.read_text(encoding="utf-8"))
        bericht["ergebnisse"].update(ergebnisse)
    else:
        bericht = {
            "schema_version": "osd-kontrollexperiment-v1",
            "weights": str(args.weights),
            "device": args.device,
            "varianten": {"v0": "original",
                          "v1": "osd-glyphen unscharf (adaptiv)",
                          "v2": "eigene osd-bloecke verwuerfelt (inhalt unleserlich)",
                          "v3": "kontrolle: verwuerfelte seitenrand-streifen (kein text)"},
            "entscheidungsregel": ("v1~v0 und v2~v0 -> osd irrelevant; "
                                   "v2 faellt >2 tp -> v3-kontrolle; faellt v3 "
                                   "genauso, ist es allgemeine pixelempfindlichkeit; "
                                   "nur v2 allein faellt -> modell liest"),
            "warnung": ("Die v0-Zahlen gelten NUR innerhalb dieses Versuchs. Der "
                        "Abgleich hier ist gierig je Sollbox (IoU>=0,5); der "
                        "offizielle Auswerter ordnet nach maximaler Trefferzahl und "
                        "maximalem Gesamt-IoU zu. v0 niemals neben Werte aus "
                        "messung_benchmark_v1.json stellen — das waere ein Vergleich "
                        "zweier Messverfahren, nicht zweier Modelle."),
            "rollen": rollen,
            "ergebnisse": ergebnisse,
        }
    args.out.mkdir(parents=True, exist_ok=True)
    ziel.write_text(json.dumps(bericht, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\nBericht: {ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
