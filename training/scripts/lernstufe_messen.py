"""Misst einen Bild-Einordner gegen den eingefrorenen Testteil seines Lernbestands.

Warum eine eigene Messung statt `eval_cls.py`: Jene haengt am alten 57er-Bestand
mit eigenem Dateinamensformat und rechnet ueber elf Klassen.

WAS GEMESSEN WIRD
Recall und Precision der positiven Klasse ueber mehrere Schwellen. NICHT die
Sammelgenauigkeit: Bei einem Verhaeltnis von 1:2,6 kaeme ein Modell, das immer
"nicht sichtbar" sagt, allein dadurch auf 72 % — eine Zahl, die nichts sagt.

VORVERARBEITUNG
Dieselbe Letterbox wie im Training (`nocrop_patch.letterbox_pil`). Ultralytics
wuerde beim Vorhersagen sonst mit Resize+CenterCrop arbeiten und die seitliche
Rohrwand abschneiden — genau dort sitzen Anschluesse. Ein Modell, das anders
gemessen als trainiert wird, liefert systematisch verschobene Werte; im Projekt
schon einmal real geworden (RGB/BGR am BCC-Endpunkt, 2026-08-09).

WAS DIESE ZAHL NICHT IST
Der Testteil misst, wie gut Protokollstellen wiedererkannt werden. Er misst
nicht, wie sich das Modell in einem ganzen Video verhaelt, wo es ueber jedes
Bild laeuft. Beim Bogen lagen diese beiden Zahlen weit auseinander.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Sequence

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "vsa_classifier"))

SCHWELLEN = (0.10, 0.20, 0.30, 0.40, 0.50, 0.60, 0.70, 0.80, 0.90)


def sha256_datei(pfad: Path) -> str:
    h = hashlib.sha256()
    with pfad.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def wilson(treffer: int, gesamt: int, z: float = 1.96) -> tuple[float, float]:
    if gesamt == 0:
        return (0.0, 0.0)
    import math
    p = treffer / gesamt
    nenner = 1 + z * z / gesamt
    mitte = (p + z * z / (2 * gesamt)) / nenner
    spanne = z * math.sqrt(p * (1 - p) / gesamt + z * z / (4 * gesamt * gesamt)) / nenner
    return (max(0.0, mitte - spanne), min(1.0, mitte + spanne))


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bestand", type=Path, required=True)
    parser.add_argument("--gewicht", type=Path, required=True)
    parser.add_argument("--split", default="test")
    parser.add_argument("--imgsz", type=int, default=640)
    parser.add_argument("--out", type=Path, default=None)
    args = parser.parse_args(argv)

    manifest = json.loads((args.bestand / "manifest.json").read_text(encoding="utf-8-sig"))
    positiv = manifest.get("klasse_positiv") or "bogen"
    negativ = manifest.get("klasse_negativ") or f"kein_{positiv}"

    from PIL import Image
    from ultralytics import YOLO

    from nocrop_patch import letterbox_pil

    modell = YOLO(str(args.gewicht))
    namen = modell.names
    index = next((i for i, n in namen.items() if n == positiv), None)
    if index is None:
        raise SystemExit(f"Klasse {positiv!r} fehlt im Modell: {namen}")

    wurzel = args.bestand / args.split
    faelle = []
    for klasse, ist_positiv in ((positiv, True), (negativ, False)):
        for bild in sorted((wurzel / klasse).glob("*.jpg")):
            faelle.append((bild, ist_positiv))
    if not faelle:
        raise SystemExit(f"Keine Bilder in {wurzel}")

    print(f"Bestand   {args.bestand.name}")
    print(f"Gewicht   {args.gewicht}")
    print(f"Teil      {args.split}: {sum(1 for _, p in faelle if p)} {positiv}, "
          f"{sum(1 for _, p in faelle if not p)} {negativ}")
    print(f"Bildgroesse {args.imgsz}, Letterbox wie im Training\n")

    werte = []
    for i, (bild, ist_positiv) in enumerate(faelle, start=1):
        with Image.open(bild) as roh:
            # Gleiche Vorverarbeitung wie im Training; Ultralytics bekommt ein
            # fertig geletterboxtes Bild und darf nicht mehr zuschneiden.
            vorbereitet = letterbox_pil(roh, args.imgsz)
        ergebnis = modell.predict(source=vorbereitet, imgsz=args.imgsz, verbose=False)[0]
        werte.append((float(ergebnis.probs.data[index]), ist_positiv, bild.name))
        if i % 100 == 0:
            print(f"  {i}/{len(faelle)} …", flush=True)

    positive = sum(1 for _, p, _ in werte if p)
    zeilen = []
    print(f"\n{'Schwelle':>9}{'Recall':>9}{'Precision':>11}{'F1':>7}{'gefunden':>10}{'Fehlalarm':>11}")
    for s in SCHWELLEN:
        tp = sum(1 for w, p, _ in werte if p and w >= s)
        fp = sum(1 for w, p, _ in werte if not p and w >= s)
        recall = tp / positive if positive else 0.0
        precision = tp / (tp + fp) if (tp + fp) else 0.0
        f1 = 2 * recall * precision / (recall + precision) if (recall + precision) else 0.0
        lo, hi = wilson(tp, positive)
        zeilen.append({"schwelle": s, "recall": round(recall, 4), "precision": round(precision, 4),
                       "f1": round(f1, 4), "gefunden": tp, "verpasst": positive - tp,
                       "fehlalarme": fp, "recall_bereich_95": [round(lo, 4), round(hi, 4)]})
        print(f"{s:>9.2f}{recall:>8.0%}{precision:>11.0%}{f1:>7.2f}"
              f"{tp:>7}/{positive:<3}{fp:>11}")

    bericht = {
        "schema": "lernstufe_messung_v1",
        "hinweis": ("Recall und Precision der positiven Klasse. Die Sammelgenauigkeit ist "
                    "bewusst nicht ausgewiesen — sie taeuscht bei ungleichen Klassen."),
        "grenze": ("Gemessen wird das Wiedererkennen von Protokollstellen, nicht das "
                   "Verhalten in einem ganzen Video."),
        "bestand": str(args.bestand),
        "manifest_sha256": (args.bestand / "manifest.sha256").read_text(encoding="utf-8").strip(),
        "gewicht": str(args.gewicht),
        "gewicht_sha256": sha256_datei(args.gewicht),
        "split": args.split,
        "klasse_positiv": positiv,
        "imgsz": args.imgsz,
        "vorverarbeitung": "letterbox_pil (identisch zum Training)",
        "positive": positive,
        "negative": len(werte) - positive,
        "schwellen": zeilen,
    }
    ziel = args.out or (args.gewicht.parent.parent / f"messung_{args.split}.json")
    text = json.dumps(bericht, indent=1, ensure_ascii=False)
    ziel.write_bytes(text.encode("utf-8"))
    ziel.with_suffix(".sha256").write_bytes(
        (hashlib.sha256(text.encode("utf-8")).hexdigest() + "\n").encode("utf-8"))
    print(f"\nBericht: {ziel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
