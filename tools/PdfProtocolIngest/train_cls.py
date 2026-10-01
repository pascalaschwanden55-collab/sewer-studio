#!/usr/bin/env python3
"""cls-Gate-Training (Schaden vs. Normal) + ehrliche Auswertung.

train : Ultralytics-cls auf dem gebauten Dataset (imgsz 1024).
eval  : trainiertes Modell ueber einen Split messen - Schwerpunkt Schaden-Recall.

VRAM-Schutz: train verweigert, solange der Sidecar laeuft (--force ueberschreibt).
Hinweis: workers=0 -> stabiles Dataloading unter Windows (vermeidet den
shared-memory/pin_memory-Absturz, der das Training vorzeitig beendet hat).
"""
from __future__ import annotations
import argparse, os, socket, sys, urllib.error, urllib.request

SIDECAR = os.getenv("SEWER_SIDECAR_HEALTH_URL", "http://127.0.0.1:8100/health")


def sidecar_up(t=1.5):
    try:
        with urllib.request.urlopen(SIDECAR, timeout=t) as r:
            return 200 <= r.status < 300
    except urllib.error.HTTPError:
        # Auch 401/403 beweist, dass der Sidecar laeuft und VRAM belegen kann.
        return True
    except Exception:
        try:
            with socket.create_connection(("127.0.0.1", 8100), timeout=t):
                return True
        except OSError:
            return False


def train(data, out, model, epochs, imgsz, batch, force):
    if sidecar_up() and not force:
        sys.exit("VERWEIGERT: Sidecar laeuft (VRAM-Budget). Sidecar stoppen oder --force.")
    from ultralytics import YOLO
    m = YOLO(model)
    m.train(data=data, epochs=epochs, imgsz=imgsz, batch=batch, workers=0,
            project=out, name="cls_v1", patience=10, exist_ok=True)
    print("Fertig. Bestes Modell:", os.path.join(out, "cls_v1", "weights", "best.pt"))


def eval_split(weights, split_dir, imgsz, thresholds):
    from ultralytics import YOLO
    m = YOLO(weights)
    inv = {v: k for k, v in m.names.items()}
    if "schaden" not in inv:
        sys.exit("Modell kennt keine Klasse 'schaden'. Klassen: " + str(m.names))
    si = inv["schaden"]
    res = []
    for cls in ("schaden", "normal"):
        d = os.path.join(split_dir, cls)
        if not os.path.isdir(d):
            continue
        for f in os.listdir(d):
            r = m.predict(os.path.join(d, f), imgsz=imgsz, verbose=False)[0]
            res.append((cls, float(r.probs.data[si])))
    npos = sum(1 for t, _ in res if t == "schaden")
    nneg = len(res) - npos
    print("Split: " + split_dir)
    print("Bilder: " + str(len(res)) + "  (schaden=" + str(npos) + ", normal=" + str(nneg) + ")\n")
    print(f"{'thr':>5} {'schaden-recall':>15} {'normal-recall':>14} {'verpasst(FN)':>13}")
    for thr in thresholds:
        TP = FN = TN = FP = 0
        for truth, p in res:
            pred_schaden = p >= thr
            if truth == "schaden":
                TP += pred_schaden; FN += not pred_schaden
            else:
                FP += pred_schaden; TN += not pred_schaden
        sr = TP / (TP + FN) if TP + FN else 0.0
        nr = TN / (TN + FP) if TN + FP else 0.0
        print(f"{thr:5.2f} {sr:15.3f} {nr:14.3f} {FN:13d}")
    print("\nGate-Ziel: Schaden-Recall moeglichst nahe 1.0 (kein Schaden verpasst).")
    print("Normal-Recall = Anteil korrekt uebersprungener Normal-Frames (Effizienz).")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="mode", required=True)
    t = sub.add_parser("train")
    t.add_argument("--data", required=True)
    t.add_argument("--out", default=r"C:\KI_BRAIN\training\runs")
    t.add_argument("--model", default="yolo11m-cls.pt")
    t.add_argument("--epochs", type=int, default=30)
    t.add_argument("--imgsz", type=int, default=1024)
    t.add_argument("--batch", type=int, default=-1)
    t.add_argument("--force", action="store_true")
    e = sub.add_parser("eval")
    e.add_argument("--weights", required=True)
    e.add_argument("--split", required=True)
    e.add_argument("--imgsz", type=int, default=1024)
    e.add_argument("--thresholds", default="0.3,0.5,0.7,0.9")
    a = ap.parse_args()
    if a.mode == "train":
        train(a.data, a.out, a.model, a.epochs, a.imgsz, a.batch, a.force)
    else:
        eval_split(a.weights, a.split, a.imgsz, [float(x) for x in a.thresholds.split(",")])
