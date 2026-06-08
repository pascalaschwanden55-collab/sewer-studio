"""
Label-Audit (read-only): findet WAHRSCHEINLICHE Fehl-Labels in den Trainingsdaten.

Prinzip (Cleanlab-artig): das vertrauenswuerdige Modell (v5) laeuft auf einem
OUT-OF-SAMPLE-Split (val, Haltungs-getrennt -> v5 hat diese Bilder NICHT trainiert) und
sammelt HOCH-KONFIDENTE Widersprueche zum Datei-Label. Solche Faelle sind verdaechtig:
entweder ist das LABEL falsch (korrigieren) oder das Modell irrt sich (lassen) — das
entscheidet der MENSCH.

LEITPLANKE: aendert/loescht NICHTS. Erzeugt nur eine Review-Liste + Beispielbilder.
Review-Paket-Disziplin (active-learning-curator): gruppiert nach Klasse, begrenzt, klare Begruendung.

  python training/vsa_classifier/label_audit.py
"""
import argparse
import glob
import os
import sys
from collections import defaultdict

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from ultralytics import YOLO

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nocrop_patch import letterbox_pil  # noqa: E402

DEF_WEIGHTS = r"C:\KI_BRAIN\yolo_cls_runs\vsa_cls_v5_nocrop\weights\best.pt"
DEF_SPLIT = r"C:\KI_BRAIN\yolo_vsa_cls_dataset_bal\val"
FOCUS = ["LEER", "BAI", "BAB", "BBA", "BDD", "BAJ"]
OUT = r"C:\tmp\label_audit"


def fnt(sz):
    try:
        return ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", sz)
    except Exception:
        return ImageFont.load_default()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--weights", default=DEF_WEIGHTS)
    ap.add_argument("--split", default=DEF_SPLIT)
    ap.add_argument("--imgsz", type=int, default=1024)
    ap.add_argument("--conf-min", type=float, default=0.70)
    ap.add_argument("--top-n", type=int, default=8)
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)

    model = YOLO(args.weights)
    classes = sorted(d for d in os.listdir(args.split) if os.path.isdir(os.path.join(args.split, d)))
    dis = defaultdict(list)   # label-class -> [(path, pred, conf)]
    counts = defaultdict(int)
    for cls in classes:
        for p in glob.glob(os.path.join(args.split, cls, "*.png")):
            counts[cls] += 1
            lb = letterbox_pil(Image.open(p), args.imgsz)
            arr = np.asarray(lb)[:, :, ::-1]
            res = model.predict(arr, imgsz=args.imgsz, verbose=False)[0]
            pred = res.names[int(res.probs.top1)]
            conf = float(res.probs.top1conf)
            if pred != cls and conf >= args.conf_min:
                dis[cls].append((p, pred, conf))

    # Report
    rep = os.path.join(OUT, "label_audit_report.md")
    all_focus = []
    with open(rep, "w", encoding="utf-8") as f:
        f.write("# Label-Audit (read-only) — verdaechtige Trainings-Labels\n\n")
        f.write(f"Modell v5 auf out-of-sample Val-Split. Hoch-konfidenter Widerspruch (>= {args.conf_min:.0%}) "
                "= Label vermutlich falsch ODER Modell irrt. **Mensch entscheidet, nichts wird geaendert.**\n\n")
        f.write("## Verdaechtigkeits-Rate pro Klasse (Fokus)\n\n| Klasse | val-Bilder | hoch-konf. Widerspruch | Rate |\n|---|---:|---:|---:|\n")
        for cls in FOCUS:
            n, d = counts.get(cls, 0), len(dis.get(cls, []))
            f.write(f"| {cls} | {n} | {d} | {d / max(1, n):.0%} |\n")
        f.write("\n## Review-Pakete (Top-Verdaechtige pro Fokus-Klasse)\n")
        for cls in FOCUS:
            items = sorted(dis.get(cls, []), key=lambda x: -x[2])[:args.top_n]
            if not items:
                continue
            f.write(f"\n### Label={cls} — {len(dis[cls])} verdaechtig (Top {len(items)})\n")
            for p, pred, conf in items:
                f.write(f"- `{os.path.basename(p)}` — Label **{cls}**, Modell sagt **{pred}** ({conf:.0%})\n")
                all_focus.append((p, cls, pred, conf))
    print("REPORT:", rep, flush=True)

    # Montage der Top-9 verdaechtigsten (ueber Fokus-Klassen)
    top = sorted(all_focus, key=lambda x: -x[3])[:9]
    if top:
        cell, lab = 340, 34
        cols, rows = 3, (len(top) + 2) // 3
        canvas = Image.new("RGB", (cols * cell, rows * (cell + lab)), (15, 15, 15))
        d = ImageDraw.Draw(canvas)
        font = fnt(16)
        for i, (p, cls, pred, conf) in enumerate(top):
            x, y = (i % cols) * cell, (i // cols) * (cell + lab)
            d.rectangle([x, y, x + cell, y + lab], fill=(0, 0, 0))
            d.text((x + 5, y + 2), f"Label {cls} -> Modell {pred} ({conf:.0%})", fill=(255, 230, 0), font=font)
            im = Image.open(p).convert("RGB").resize((cell, cell))
            canvas.paste(im, (x, y + lab))
        mp = os.path.join(OUT, "label_audit_top9.png")
        canvas.save(mp)
        print("MONTAGE:", mp, flush=True)

    print(f"\nZusammenfassung: { {c: len(dis.get(c, [])) for c in FOCUS} }", flush=True)


if __name__ == "__main__":
    main()
