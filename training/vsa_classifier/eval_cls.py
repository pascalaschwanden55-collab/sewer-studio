"""
Misst den trainierten VSA-Klassifikator auf dem eingefrorenen 57er-Clean-Eval-Set.
Der Klassifikator hat diese Frames NIE gesehen (Kontamination=0 beim Bau verifiziert).

Ground-Truth: _candidates.json (korrektur || code_full), Fallback Dateiname.
Vergleich gegen VLM-Baseline: 28% (nur LEER), Befundcodes 0%.

Beispiel:
  python training/vsa_classifier/eval_cls.py --weights C:\\KI_BRAIN\\yolo_cls_runs\\vsa_cls_v1\\weights\\best.pt
"""
import argparse
import json
import os
import re
from collections import Counter, defaultdict

from ultralytics import YOLO

TARGET = {"BCD", "BCE", "BDA", "BDD", "BAJ", "BAF", "BAB", "BAI", "BBB", "BBA", "LEER"}
FNAME_RE = re.compile(r"^(?P<haltung>.+?)_(?P<zeit>[0-9.]+)s_(?P<code>.+?)(_t[+-]\d+)?\.png$", re.IGNORECASE)


def code_to_class(code):
    if code is None:
        return None
    c = str(code).strip()
    if c.lower() in ("", "kein_schaden", "leer"):
        return "LEER"
    c = c.upper()
    if len(c) < 3:
        return None
    m = c[:3]
    return m if m in TARGET else None


def gt_from_filename(name):
    m = FNAME_RE.match(name)
    return code_to_class(m.group("code")) if m else None


def load_gt_map(eval_root):
    """basename -> gt_class aus _candidates.json (korrektur || code_full)."""
    gt = {}
    cj = os.path.join(eval_root, "_candidates.json")
    if os.path.isfile(cj):
        with open(cj, encoding="utf-8-sig") as f:
            data = json.load(f)
        for c in data:
            base = os.path.basename(c.get("frame_path") or "")
            cls = code_to_class(c.get("korrektur") or c.get("code_full") or c.get("code_main"))
            if base and cls:
                gt[base] = cls
    return gt


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--weights", required=True)
    ap.add_argument("--eval-root", default=r"C:\Sewer-Studio_KI_4.4\EvalVisibilityReview_20260525\eval_visible_clean_eval_set")
    ap.add_argument("--imgsz", type=int, default=224)
    ap.add_argument("--no-crop", action="store_true", help="Bild letterboxen statt predict croppen lassen")
    ap.add_argument("--json-out", default=None, help="Metriken als JSON-Datei schreiben (fuer Autopilot)")
    args = ap.parse_args()

    model = YOLO(args.weights)
    img_dir = os.path.join(args.eval_root, "images")
    gt_map = load_gt_map(args.eval_root)

    rows = []
    for name in sorted(os.listdir(img_dir)):
        if not name.lower().endswith(".png"):
            continue
        gt = gt_map.get(name) or gt_from_filename(name)
        if gt is None:
            continue
        src = os.path.join(img_dir, name)
        if args.no_crop:
            from nocrop_patch import letterbox_pil
            from PIL import Image as _PILImage
            import numpy as _np
            lb = letterbox_pil(_PILImage.open(src), args.imgsz)
            src = _np.asarray(lb)[:, :, ::-1]  # RGB -> BGR fuer predict (macht intern BGR->RGB)
        res = model.predict(src, imgsz=args.imgsz, verbose=False)[0]
        pred = res.names[int(res.probs.top1)]
        rows.append((name, gt, pred))

    total = len(rows)
    correct = sum(1 for _, g, p in rows if g == p)
    findings = [(g, p) for _, g, p in rows if g != "LEER"]
    leer = [(g, p) for _, g, p in rows if g == "LEER"]
    find_ok = sum(1 for g, p in findings if g == p)
    leer_ok = sum(1 for g, p in leer if g == p)

    per = defaultdict(lambda: [0, 0])
    for _, g, p in rows:
        per[g][1] += 1
        if g == p:
            per[g][0] += 1

    print(f"\n=== VSA-Klassifikator vs eingefrorenes 57er-Clean-Eval ===")
    print(f"Frames:            {total}")
    print(f"Top-1 Gesamt:      {correct}/{total} = {correct/max(1,total):.1%}")
    print(f"  Befundcodes:     {find_ok}/{len(findings)} = {find_ok/max(1,len(findings)):.1%}   (VLM war 0%)")
    print(f"  LEER:            {leer_ok}/{len(leer)} = {leer_ok/max(1,len(leer)):.1%}")
    print("Pro Klasse (richtig/total):")
    for k in sorted(per):
        c, t = per[k]
        print(f"  {k:5} {c}/{t}")
    conf = Counter((g, p) for _, g, p in rows if g != p)
    if conf:
        print("Haeufigste Verwechslungen (gt -> pred):")
        for (g, p), n in conf.most_common(12):
            print(f"  {g} -> {p}: {n}")

    if args.json_out:
        metrics = {
            "frames": total,
            "exact_correct": correct, "exact_acc": correct / max(1, total),
            "findings_correct": find_ok, "findings_total": len(findings),
            "findings_acc": find_ok / max(1, len(findings)),
            "leer_correct": leer_ok, "leer_total": len(leer),
            "leer_acc": leer_ok / max(1, len(leer)),
            "per_class": {k: list(per[k]) for k in per},
            "weights": args.weights, "eval_root": args.eval_root,
        }
        os.makedirs(os.path.dirname(os.path.abspath(args.json_out)), exist_ok=True)
        with open(args.json_out, "w", encoding="utf-8") as jf:
            json.dump(metrics, jf, indent=2, ensure_ascii=False)
        print("JSON:", args.json_out)


if __name__ == "__main__":
    main()
