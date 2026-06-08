"""
Konfidenz-Schwellen-Auswahl fuer einen Kandidaten (z.B. v6b) gegen die v5-Baseline.

METHODIK (sauber, gegen Test-Set-Ueberanpassung):
- Schwelle T wird NUR auf dem 57er-clean gesucht.
- 63er-hidden ist NUR Kontrollblick beim AUSGEWAEHLTEN T, NICHT zur Auswahl.
- Kandidat nur, wenn die geschwellte Variante v5 schlaegt ODER gleich ist:
  Gesamt >= v5, Befund >= v5, LEER >= v5, Schluesselklassen (BAI/BAB/BBA/BDD/BAJ) nicht runter.
- active.json wird NIE angefasst — nur Kandidaten-Markierung.

Regel: predicted = top1, wenn Konfidenz >= T, sonst LEER.

  python training/vsa_classifier/threshold_select.py --weights C:\\KI_BRAIN\\yolo_cls_runs\\vsa_cls_v6b_leer38\\weights\\best.pt --name vsa_cls_v6b_thr
"""
import argparse
import json
import os
import sys
from collections import defaultdict

import numpy as np
from PIL import Image
from ultralytics import YOLO

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from eval_threshold import gt_from_filename, load_gt_map  # noqa: E402
from nocrop_patch import letterbox_pil  # noqa: E402

KEY = ["BAI", "BAB", "BBA", "BDD", "BAJ"]
DEF_EVAL = r"C:\Sewer-Studio_KI_4.4\EvalVisibilityReview_20260525\eval_visible_clean_eval_set"
DEF_HIDDEN = r"C:\Sewer-Studio_KI_4.4\EvalVisibilityReview_20260525\eval_unclean_or_hidden_eval_set"
CAND_DIR = r"C:\KI_BRAIN\model_candidates"
REPORT_DIR = os.path.join("docs", "benchmarks")
TS = [0.0, 0.3, 0.4, 0.5, 0.55, 0.6, 0.65, 0.7, 0.75, 0.8, 0.85, 0.9]


def eval_frames(model, root, imgsz):
    """Pro mappbarem Frame: (gt_class, pred_class, top1_conf). Letterbox wie im Training."""
    img_dir = os.path.join(root, "images")
    gt_map = load_gt_map(root)
    rows = []
    for name in sorted(os.listdir(img_dir)):
        if not name.lower().endswith(".png"):
            continue
        gt = gt_map.get(name) or gt_from_filename(name)
        if gt is None:
            continue
        lb = letterbox_pil(Image.open(os.path.join(img_dir, name)), imgsz)
        arr = np.asarray(lb)[:, :, ::-1]  # RGB->BGR
        res = model.predict(arr, imgsz=imgsz, verbose=False)[0]
        rows.append((gt, res.names[int(res.probs.top1)], float(res.probs.top1conf)))
    return rows


def metrics_at(rows, T):
    preds = [(g, p if c >= T else "LEER") for g, p, c in rows]
    per = defaultdict(lambda: [0, 0])
    for g, p in preds:
        per[g][1] += 1
        per[g][0] += int(g == p)
    find = [(g, p) for g, p in preds if g != "LEER"]
    leer = [(g, p) for g, p in preds if g == "LEER"]
    return {"frames": len(preds), "exact_correct": sum(g == p for g, p in preds),
            "findings_correct": sum(g == p for g, p in find), "findings_total": len(find),
            "leer_correct": sum(g == p for g, p in leer), "leer_total": len(leer),
            "per_class": {k: list(per[k]) for k in per}}


def passes(m, base):
    if m["exact_correct"] < base["exact_correct"]:
        return False
    if m["findings_correct"] < base["findings_correct"]:
        return False
    if m["leer_correct"] < base["leer_correct"]:
        return False
    for c in KEY:
        if m["per_class"].get(c, [0, 0])[0] < base["per_class"].get(c, [0, 0])[0]:
            return False
    return True


def fmt(m):
    return (f"Gesamt {m['exact_correct']}/{m['frames']} | Befund {m['findings_correct']}/{m['findings_total']} "
            f"| LEER {m['leer_correct']}/{m['leer_total']}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--weights", required=True)
    ap.add_argument("--name", default="kandidat_thr")
    ap.add_argument("--eval", default=DEF_EVAL)
    ap.add_argument("--hidden", default=DEF_HIDDEN)
    ap.add_argument("--imgsz", type=int, default=1024)
    ap.add_argument("--baseline-clean", default=os.path.join(REPORT_DIR, "_autopilot_baseline_clean.json"))
    ap.add_argument("--baseline-hidden", default=os.path.join(REPORT_DIR, "_autopilot_baseline_hidden.json"))
    args = ap.parse_args()

    base_c = json.load(open(args.baseline_clean, encoding="utf-8"))
    base_h = json.load(open(args.baseline_hidden, encoding="utf-8"))
    model = YOLO(args.weights)
    rows_c = eval_frames(model, args.eval, args.imgsz)

    print(f"\nv5-Baseline (clean): {fmt(base_c)}")
    print(f"\nSchwellen-Suche NUR auf 57er-clean (Regel: conf<T -> LEER):")
    print(f"{'T':>5} {'Gesamt':>9} {'Befund':>9} {'LEER':>8}  passt>=v5?")
    cand_T, cand_m = None, None
    for T in TS:
        m = metrics_at(rows_c, T)
        ok = passes(m, base_c)
        mark = "JA" if ok else "nein"
        print(f"{T:>5.2f} {m['exact_correct']:>4}/{m['frames']} {m['findings_correct']:>4}/{m['findings_total']} "
              f"{m['leer_correct']:>3}/{m['leer_total']}    {mark}")
        if ok and (cand_m is None or m["exact_correct"] > cand_m["exact_correct"]):
            cand_T, cand_m = T, m

    if cand_T is None:
        print("\n=== KEIN T besteht (>= v5 auf allen Kriterien). v5 bleibt Bestand. Kein Kandidat. ===")
        return

    # Hidden NUR als Kontrolle beim gewaehlten T
    rows_h = eval_frames(model, args.hidden, args.imgsz)
    hid = metrics_at(rows_h, cand_T)

    print(f"\n=== GEWAEHLTES T = {cand_T} (auf clean) ===")
    print(f"  Kandidat clean:  {fmt(cand_m)}")
    print(f"  v5 clean:        {fmt(base_c)}")
    print(f"  Schluesselklassen (clean):", {c: cand_m['per_class'].get(c, [0, 0]) for c in KEY})
    print(f"\n--- Kontrollblick 63er-hidden @T={cand_T} (NICHT zur Auswahl) ---")
    print(f"  Kandidat hidden: {fmt(hid)}")
    print(f"  v5 hidden:       {fmt(base_h)}")

    # Kandidat markieren (besteht auf clean -> Kandidat)
    os.makedirs(CAND_DIR, exist_ok=True)
    payload = {"name": args.name, "weights": args.weights, "threshold": cand_T,
               "decided_on": "57er-clean", "clean": cand_m, "hidden_control": hid,
               "baseline_clean": base_c, "baseline_hidden_control": base_h,
               "inference_rule": "pred=top1 if conf>=threshold else LEER",
               "PRODUKTION_NUR_NACH_FREIGABE": True}
    cf = os.path.join(CAND_DIR, f"{args.name}.json")
    with open(cf, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2, ensure_ascii=False)
    print(f"\n========== KANDIDAT markiert: {cf} (T={cand_T}) ==========")
    print("active.json UNBERUEHRT. Produktions-Freigabe nur durch den Menschen.")


if __name__ == "__main__":
    main()
