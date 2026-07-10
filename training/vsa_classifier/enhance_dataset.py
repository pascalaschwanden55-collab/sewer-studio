"""
Bereitet den balancierten Klassifikator-Datensatz UND das 57er-Clean-Eval IDENTISCH
mit einem moderaten, kantenerhaltenden Bilateral-Filter auf (OpenCV/CPU; dasselbe Filter
wie ffmpeg bilateral_cuda, nur batch-tauglich).

Eine Variable: nur Bilateral. Sonst alles wie v3 (balanciert, 1024 spaeter im Training).

  python training/vsa_classifier/enhance_dataset.py
"""
import os
import shutil
import sys
import time

import cv2
import numpy as np

from repo_paths import CLEAN_EVAL_ROOT, EVAL_REVIEW_ROOT

SRC_DS = r"C:\KI_BRAIN\yolo_vsa_cls_dataset_bal"
DST_DS = r"C:\KI_BRAIN\yolo_vsa_cls_dataset_bal_bilateral"
SRC_EVAL = CLEAN_EVAL_ROOT
DST_EVAL = str(EVAL_REVIEW_ROOT / "eval_visible_clean_eval_set_bilateral")

# Moderat, kantenerhaltend (kleine Nachbarschaft d=5 -> haelt Riss-Kanten).
D, SIGMA_COLOR, SIGMA_SPACE = 5, 50, 50


def enhance(src, dst):
    # Unicode-sicher: cv2.imread/imwrite scheitern auf Windows an Umlaut-Pfaden (z.B. "Gewaesser").
    img = cv2.imdecode(np.fromfile(src, dtype=np.uint8), cv2.IMREAD_COLOR)
    if img is None:
        return False
    out = cv2.bilateralFilter(img, D, SIGMA_COLOR, SIGMA_SPACE)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    ok, buf = cv2.imencode(".png", out)
    if not ok:
        return False
    buf.tofile(dst)
    return True


def do_dir(src_root, dst_root):
    n, failed, t = 0, 0, time.time()
    for dirpath, _, files in os.walk(src_root):
        for fn in files:
            if not fn.lower().endswith(".png"):
                continue
            rel = os.path.relpath(os.path.join(dirpath, fn), src_root)
            if not enhance(os.path.join(dirpath, fn), os.path.join(dst_root, rel)):
                failed += 1
                print(f"  FEHLER (uebersprungen): {rel}", flush=True)
            n += 1
            if n % 2000 == 0:
                print(f"  {n} Bilder ... {time.time()-t:.0f}s", flush=True)
    print(f"  fertig: {n} Bilder, {failed} Fehler, in {time.time()-t:.0f}s", flush=True)
    return n, failed


def main():
    for d in (DST_DS, DST_EVAL):
        if os.path.exists(d) and os.listdir(d):
            print(f"ABBRUCH: Zielordner existiert und ist nicht leer: {d}")
            sys.exit(1)

    print(f"Bilateral d={D} sigmaColor={SIGMA_COLOR} sigmaSpace={SIGMA_SPACE}", flush=True)
    print("=== Trainings-Datensatz (train) ===", flush=True)
    do_dir(os.path.join(SRC_DS, "train"), os.path.join(DST_DS, "train"))
    print("=== Trainings-Datensatz (val) ===", flush=True)
    do_dir(os.path.join(SRC_DS, "val"), os.path.join(DST_DS, "val"))
    print("=== 57er-Eval (images) ===", flush=True)
    do_dir(os.path.join(SRC_EVAL, "images"), os.path.join(DST_EVAL, "images"))
    shutil.copy2(os.path.join(SRC_EVAL, "_candidates.json"), os.path.join(DST_EVAL, "_candidates.json"))
    print(f"FERTIG.\n  Datensatz: {DST_DS}\n  Eval:      {DST_EVAL}", flush=True)


if __name__ == "__main__":
    main()
