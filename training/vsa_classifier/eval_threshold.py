"""
Gratis-Experiment: Konfidenz-Schwelle gegen das LEER-Problem.
Laeuft den Klassifikator einmal ueber das 57er-Clean-Eval, sammelt pro Frame die
Top-1-Konfidenz und testet die Regel: "wenn Konfidenz < T -> sag LEER".
Sweept T, ohne neu zu trainieren.

  python training/vsa_classifier/eval_threshold.py --weights <best.pt> --imgsz 1024
"""
import argparse
import json
import os
import re

from ultralytics import YOLO

from repo_paths import CLEAN_EVAL_ROOT

# Paket 5: gleiche Zielmenge wie eval_cls.py, damit threshold_select.py die
# neuen v8-Klassen nicht aus der Ground Truth herausfiltert.
TARGET = {"BCD", "BCE", "BDA", "BDD", "BAJ", "BAF", "BAB", "BAI", "BBB", "BBA", "LEER",
          "BCA", "BCC", "BBC", "BAA"}
FNAME_RE = re.compile(r"^(?P<haltung>.+?)_(?P<zeit>[0-9.]+)s_(?P<code>.+?)(_t[+-]\d+)?\.png$", re.IGNORECASE)


def code_to_class(code):
    if code is None:
        return None
    c = str(code).strip()
    if c.lower() in ("", "kein_schaden", "leer"):
        return "LEER"
    c = c.upper()
    return c[:3] if (len(c) >= 3 and c[:3] in TARGET) else None


def gt_from_filename(name):
    m = FNAME_RE.match(name)
    return code_to_class(m.group("code")) if m else None


def load_gt_map(eval_root):
    gt = {}
    cj = os.path.join(eval_root, "_candidates.json")
    if os.path.isfile(cj):
        with open(cj, encoding="utf-8-sig") as f:
            for c in json.load(f):
                base = os.path.basename(c.get("frame_path") or "")
                cls = code_to_class(c.get("korrektur") or c.get("code_full") or c.get("code_main"))
                if base and cls:
                    gt[base] = cls
    return gt


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--weights", required=True)
    ap.add_argument("--eval-root", default=CLEAN_EVAL_ROOT)
    ap.add_argument("--imgsz", type=int, default=1024)
    args = ap.parse_args()

    model = YOLO(args.weights)
    img_dir = os.path.join(args.eval_root, "images")
    gt_map = load_gt_map(args.eval_root)

    rows = []  # (gt, pred_class, top_conf)
    for name in sorted(os.listdir(img_dir)):
        if not name.lower().endswith(".png"):
            continue
        gt = gt_map.get(name) or gt_from_filename(name)
        if gt is None:
            continue
        res = model.predict(os.path.join(img_dir, name), imgsz=args.imgsz, verbose=False)[0]
        rows.append((gt, res.names[int(res.probs.top1)], float(res.probs.top1conf)))

    print("Regel: wenn Top-1-Konfidenz < T -> sag LEER\n")
    print(f"{'T':>4}  {'Gesamt':>10}  {'Befundcodes':>14}  {'LEER':>10}")
    for T in [0.0, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9]:
        preds = [(g, (p if c >= T else "LEER")) for g, p, c in rows]
        tot = len(preds)
        cor = sum(g == p for g, p in preds)
        find = [(g, p) for g, p in preds if g != "LEER"]
        leer = [(g, p) for g, p in preds if g == "LEER"]
        fok = sum(g == p for g, p in find)
        lok = sum(g == p for g, p in leer)
        print(f"{T:>4.1f}  {cor:>3}/{tot} = {cor/tot:>3.0%}  {fok:>3}/{len(find)} = {fok/len(find):>3.0%}  {lok:>3}/{len(leer)} = {lok/len(leer):>3.0%}")


if __name__ == "__main__":
    main()
