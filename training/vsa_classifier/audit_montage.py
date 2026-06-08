"""
Review-Montagen pro Klasse: zeigt verdaechtige (Modell-Widerspruch) Frames aus MOEGLICHST
VIELEN verschiedenen Haltungen, je ein Frame pro Befund (keine t-Zeit-Dubletten), zum
Fachauge-Review. Read-only, aendert nichts.

  python training/vsa_classifier/audit_montage.py
"""
import argparse
import glob
import os
import re
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from ultralytics import YOLO

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nocrop_patch import letterbox_pil  # noqa: E402

DEF_WEIGHTS = r"C:\KI_BRAIN\yolo_cls_runs\vsa_cls_v5_nocrop\weights\best.pt"
DEF_SPLIT = r"C:\KI_BRAIN\yolo_vsa_cls_dataset_bal\val"
OUT = r"C:\tmp\label_audit"


def haltung_time(name):
    m = re.match(r"^(.+?)_([0-9.]+)s_", name)
    return (m.group(1), m.group(2)) if m else (name, "")


def fnt(s):
    try:
        return ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", s)
    except Exception:
        return ImageFont.load_default()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--weights", default=DEF_WEIGHTS)
    ap.add_argument("--split", default=DEF_SPLIT)
    ap.add_argument("--classes", default="BAJ,BDD,BAB,BBA")
    ap.add_argument("--per-class", type=int, default=12)
    ap.add_argument("--imgsz", type=int, default=1024)
    ap.add_argument("--conf-min", type=float, default=0.70)
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    model = YOLO(args.weights)
    made = []
    for cls in args.classes.split(","):
        d = os.path.join(args.split, cls)
        if not os.path.isdir(d):
            continue
        byfind = {}  # (haltung,time) -> (path,pred,conf) ; nur ein Frame pro Befund
        for p in glob.glob(os.path.join(d, "*.png")):
            name = os.path.basename(p)
            lb = letterbox_pil(Image.open(p), args.imgsz)
            arr = np.asarray(lb)[:, :, ::-1]
            res = model.predict(arr, imgsz=args.imgsz, verbose=False)[0]
            pred = res.names[int(res.probs.top1)]
            conf = float(res.probs.top1conf)
            if pred == cls or conf < args.conf_min:
                continue
            key = haltung_time(name)
            if key not in byfind or ("_t+0" in name and "_t+0" not in os.path.basename(byfind[key][0])):
                byfind[key] = (p, pred, conf)
        # Vielfalt: zuerst eine pro Haltung, dann auffuellen
        seen, div, rest = set(), [], []
        for (h, _t), v in sorted(byfind.items(), key=lambda x: -x[1][2]):
            (div if h not in seen else rest).append(v)
            seen.add(h)
        picks = (div + rest)[:args.per_class]
        if not picks:
            print(f"{cls}: keine Verdaechtigen", flush=True)
            continue
        cell, lab, cols = 300, 30, 4
        rows = (len(picks) + cols - 1) // cols
        canvas = Image.new("RGB", (cols * cell, rows * (cell + lab)), (15, 15, 15))
        dr = ImageDraw.Draw(canvas)
        font = fnt(13)
        for i, (p, pred, conf) in enumerate(picks):
            x, y = (i % cols) * cell, (i // cols) * (cell + lab)
            dr.rectangle([x, y, x + cell, y + lab], fill=(0, 0, 0))
            h = haltung_time(os.path.basename(p))[0]
            dr.text((x + 4, y + 2), f"{cls}->{pred} {conf:.0%} | {h[:18]}", fill=(255, 230, 0), font=font)
            canvas.paste(Image.open(p).convert("RGB").resize((cell, cell)), (x, y + lab))
        op = os.path.join(OUT, f"review_{cls}.png")
        canvas.save(op)
        n_halt = len({haltung_time(os.path.basename(v[0]))[0] for v in picks})
        print(f"MONTAGE: {op}  ({len(picks)} Frames aus {n_halt} Haltungen)", flush=True)
        made.append(op)
    print("FERTIG:", made, flush=True)


if __name__ == "__main__":
    main()
