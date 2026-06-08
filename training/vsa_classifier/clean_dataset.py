"""
Baut aus dem Original-Datensatz einen BEREINIGTEN Datensatz, indem ein
OUT-OF-SAMPLE-Modell (Spiegel-Modell, auf dem val-Split trainiert) den
train-Split flaggt: hoch-konfidenter Widerspruch zum Datei-Label (pred != label,
conf >= Schwelle) = vermutlicher Label-/Frame-Extraktions-Fehler -> ENTFERNEN.

LEITPLANKEN:
- KEIN Label erfunden: Frames werden nur ENTFERNT, nie umgeschrieben.
- Klassen-Untergrenze: pro Klasse bleiben mind. --min-keep bzw. --min-keep-frac
  erhalten (es werden zuerst die hoechst-konfidenten Widersprueche entfernt),
  damit keine Klasse kollabiert.
- val-Split bleibt UNVERAENDERT (stabiler Massstab fuers Early-Stopping).
- --dry-run zeigt nur die Per-Klasse-Zahlen, schreibt nichts.

Materialisierung: Hardlinks (gleiches Volume, kein doppelter Speicher), der
bereinigte Datensatz ist dadurch unabhaengig vom Original (Cache/Loeschen
beruehrt das Original nicht).

  python training/vsa_classifier/clean_dataset.py --weights <mirror best.pt> --dry-run
  python training/vsa_classifier/clean_dataset.py --weights <mirror best.pt> --out C:/KI_BRAIN/yolo_vsa_cls_dataset_clean80
"""
import argparse
import glob
import json
import math
import os
import sys

import numpy as np
from PIL import Image
from ultralytics import YOLO

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nocrop_patch import letterbox_pil  # noqa: E402

DEF_SRC = r"C:\KI_BRAIN\yolo_vsa_cls_dataset_bal"


def flag_split(model, train_dir, imgsz, conf_min):
    """Pro Klasse: (alle Bilder, Liste geflaggter (path,pred,conf) absteigend nach conf)."""
    classes = sorted(d for d in os.listdir(train_dir) if os.path.isdir(os.path.join(train_dir, d)))
    result = {}
    for cls in classes:
        paths = sorted(glob.glob(os.path.join(train_dir, cls, "*.png")))
        flagged = []
        for p in paths:
            lb = letterbox_pil(Image.open(p), imgsz)
            arr = np.asarray(lb)[:, :, ::-1]
            res = model.predict(arr, imgsz=imgsz, verbose=False)[0]
            pred = res.names[int(res.probs.top1)]
            conf = float(res.probs.top1conf)
            if pred != cls and conf >= conf_min:
                flagged.append((p, pred, conf))
        flagged.sort(key=lambda x: -x[2])
        result[cls] = (paths, flagged)
        print(f"  {cls:5s}  total={len(paths):5d}  geflaggt(>= {conf_min:.2f})={len(flagged):4d}", flush=True)
    return result


def plan_removal(result, min_keep, min_keep_frac):
    """Pro Klasse entscheiden, wie viele der geflaggten (hoechst-konf zuerst) wirklich raus."""
    plan = {}
    for cls, (paths, flagged) in result.items():
        total = len(paths)
        floor = max(min_keep, math.ceil(min_keep_frac * total))
        removable = max(0, total - floor)
        remove = flagged[:min(len(flagged), removable)]
        capped = len(flagged) - len(remove)
        plan[cls] = {"total": total, "flagged": len(flagged), "remove": remove,
                     "kept": total - len(remove), "floor": floor, "capped": capped}
    return plan


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--weights", required=True, help="Spiegel-Modell (auf val-Split trainiert)")
    ap.add_argument("--src", default=DEF_SRC, help="Original-Datensatz (train/ + val/)")
    ap.add_argument("--out", default=None, help="Ziel fuer bereinigten Datensatz (Pflicht ohne --dry-run)")
    ap.add_argument("--imgsz", type=int, default=1024)
    ap.add_argument("--conf-min", type=float, default=0.80)
    ap.add_argument("--min-keep", type=int, default=30, help="Klassen-Untergrenze absolut")
    ap.add_argument("--min-keep-frac", type=float, default=0.5, help="Klassen-Untergrenze relativ")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    train_dir = os.path.join(args.src, "train")
    val_dir = os.path.join(args.src, "val")
    print(f"=== FLAGGING train-Split mit Spiegel-Modell (conf>= {args.conf_min:.2f}, imgsz {args.imgsz}) ===", flush=True)
    model = YOLO(args.weights)
    result = flag_split(model, train_dir, args.imgsz, args.conf_min)
    plan = plan_removal(result, args.min_keep, args.min_keep_frac)

    tot_total = sum(v["total"] for v in plan.values())
    tot_remove = sum(len(v["remove"]) for v in plan.values())
    tot_capped = sum(v["capped"] for v in plan.values())
    print("\n=== ENTFERNUNGS-PLAN (train) ===", flush=True)
    print(f"{'Klasse':6s} {'total':>6s} {'geflaggt':>8s} {'entfernt':>8s} {'behalten':>8s} {'floor':>6s} {'gekappt':>7s}", flush=True)
    for cls in sorted(plan):
        v = plan[cls]
        print(f"{cls:6s} {v['total']:6d} {v['flagged']:8d} {len(v['remove']):8d} {v['kept']:8d} {v['floor']:6d} {v['capped']:7d}", flush=True)
    print(f"{'GESAMT':6s} {tot_total:6d} {'':8s} {tot_remove:8d} {tot_total-tot_remove:8d} {'':6s} {tot_capped:7d}", flush=True)
    print(f"\nEntfernungs-Quote: {tot_remove}/{tot_total} = {tot_remove/max(1,tot_total):.1%}"
          f"  (durch floor gekappt: {tot_capped})", flush=True)

    if args.dry_run:
        print("\nDRY-RUN: nichts geschrieben.", flush=True)
        return
    if not args.out:
        raise SystemExit("--out fehlt (ohne --dry-run noetig)")

    # Bereinigten Datensatz via Hardlinks materialisieren
    remove_set = {p for v in plan.values() for (p, _pr, _cf) in v["remove"]}
    print(f"\n=== MATERIALISIERE bereinigten Datensatz: {args.out} ===", flush=True)
    if os.path.exists(args.out):
        raise SystemExit(f"ABBRUCH: {args.out} existiert bereits — bitte vorher entfernen.")

    linked_train = linked_val = 0
    # train: nur behaltene Frames
    for cls in sorted(plan):
        dst_dir = os.path.join(args.out, "train", cls)
        os.makedirs(dst_dir, exist_ok=True)
        for p in glob.glob(os.path.join(train_dir, cls, "*.png")):
            if p in remove_set:
                continue
            os.link(p, os.path.join(dst_dir, os.path.basename(p)))
            linked_train += 1
    # val: unveraendert uebernehmen
    for cls in sorted(d for d in os.listdir(val_dir) if os.path.isdir(os.path.join(val_dir, d))):
        dst_dir = os.path.join(args.out, "val", cls)
        os.makedirs(dst_dir, exist_ok=True)
        for p in glob.glob(os.path.join(val_dir, cls, "*.png")):
            os.link(p, os.path.join(dst_dir, os.path.basename(p)))
            linked_val += 1

    report = {
        "src": args.src, "out": args.out, "weights": args.weights,
        "conf_min": args.conf_min, "imgsz": args.imgsz,
        "min_keep": args.min_keep, "min_keep_frac": args.min_keep_frac,
        "total_train": tot_total, "removed_train": tot_remove, "kept_train": tot_total - tot_remove,
        "capped_by_floor": tot_capped, "linked_train": linked_train, "linked_val": linked_val,
        "per_class": {c: {"total": v["total"], "flagged": v["flagged"],
                          "removed": len(v["remove"]), "kept": v["kept"],
                          "floor": v["floor"], "capped": v["capped"],
                          "examples": [[os.path.basename(p), pr, round(cf, 3)] for p, pr, cf in v["remove"][:8]]}
                      for c, v in plan.items()},
    }
    rep_path = os.path.join(args.out, "_clean_report.json")
    with open(rep_path, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2, ensure_ascii=False)
    print(f"FERTIG. train-Hardlinks={linked_train}  val-Hardlinks={linked_val}", flush=True)
    print("REPORT:", rep_path, flush=True)


if __name__ == "__main__":
    main()
