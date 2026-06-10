"""
Bewertet Trainings-Frames mit einem vorhandenen Klassifikator (Standard: v5/Bestand)
und schreibt pro Frame Top-1-Klasse, Top-1-Konfidenz und LEER-Konfidenz in eine CSV.

Zweck (Paket 5): "Sieht-leer-aus"-Erkennung fuer verschmutzte Befund-Klassen.
Methode vom Label-Audit 2026-06-07 validiert (User-Review: 68% der Hochkonfidenz-
LEER-Flags waren wirklich falsch gelabelt). Es wird NICHTS geloescht/gelabelt —
nur gemessen; die Ausschluss-Regel wird danach auf der CSV getunt.

Preprocessing identisch zu eval_cls.py --no-crop (Letterbox + RGB->BGR).

Aufruf:
  python training/vsa_classifier/score_frames.py --code-prefix BCA
"""
import argparse
import csv
import os
import re
import time

from ultralytics import YOLO

FNAME_RE = re.compile(r"^(?P<haltung>.+?)_(?P<zeit>[0-9.]+)s_(?P<code>.+?)(_t[+-]\d+)?\.png$", re.IGNORECASE)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--frames", default=r"C:\KI_BRAIN\training_frames")
    ap.add_argument("--code-prefix", required=True)
    ap.add_argument("--weights", default=r"C:\KI_BRAIN\yolo_cls_runs\vsa_cls_v5_nocrop\weights\best.pt")
    ap.add_argument("--imgsz", type=int, default=1024)
    ap.add_argument("--out", default=None)
    ap.add_argument("--limit", type=int, default=0)
    args = ap.parse_args()

    out_csv = args.out or os.path.join("docs", "benchmarks", f"_score_{args.code_prefix.lower()}_v5.csv")
    os.makedirs(os.path.dirname(out_csv), exist_ok=True)

    prefix = args.code_prefix.upper()
    files = []
    for root, _dirs, names in os.walk(args.frames):
        for n in names:
            if not n.lower().endswith(".png"):
                continue
            m = FNAME_RE.match(n)
            if m and m.group("code").upper().startswith(prefix):
                files.append(os.path.join(root, n))
    files.sort()
    print(f"{len(files)} Frames mit Code-Prefix {prefix}", flush=True)

    done = set()
    if os.path.isfile(out_csv):
        with open(out_csv, newline="", encoding="utf-8") as f:
            done = {row["file"] for row in csv.DictReader(f)}
        print(f"{len(done)} bereits bewertet — uebersprungen", flush=True)

    model = YOLO(args.weights)

    from nocrop_patch import letterbox_pil
    from PIL import Image
    import numpy as np

    new_file = not os.path.isfile(out_csv)
    t0 = time.time()
    measured = 0
    with open(out_csv, "a", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        if new_file:
            w.writerow(["file", "top1", "top1_conf", "leer_conf"])
        for path in files:
            base = os.path.basename(path)
            if base in done:
                continue
            if args.limit and measured >= args.limit:
                break
            lb = letterbox_pil(Image.open(path), args.imgsz)
            arr = np.asarray(lb)[:, :, ::-1]  # RGB -> BGR wie eval_cls.py
            res = model.predict(arr, imgsz=args.imgsz, verbose=False)[0]
            names = res.names
            probs = res.probs
            top1 = names[int(probs.top1)]
            leer_idx = next((i for i, n in names.items() if n == "LEER"), None)
            leer_conf = float(probs.data[leer_idx]) if leer_idx is not None else 0.0
            w.writerow([base, top1, round(float(probs.top1conf), 4), round(leer_conf, 4)])
            measured += 1
            if measured % 200 == 0:
                f.flush()
                rate = measured / (time.time() - t0)
                rest = (len(files) - len(done) - measured) / max(rate, 0.01)
                print(f"  {measured} bewertet ({rate:.1f}/s, Rest ~{rest/60:.0f} min)", flush=True)

    print(f"FERTIG: {measured} neu bewertet -> {out_csv}", flush=True)


if __name__ == "__main__":
    main()
