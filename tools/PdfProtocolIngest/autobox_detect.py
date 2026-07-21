#!/usr/bin/env python3
"""Detect-Auto-Boxing: aus experten-gelabelten Frames -> YOLO-Detect-Dataset.

Wir kennen je Frame die Schadensklasse (aus dem Protokoll). Statt blind zu fragen
"was ist im Bild", schicken wir gezielt den Klassen-Prompt an Grounding DINO
(vorhandener Sidecar) -> viel präzisere Boxen. DINO liefert Pixel-Boxen; wir
normalisieren sie ins YOLO-Format und übernehmen die bekannte class_map-v2-ID.

Voraussetzung: der Sidecar LÄUFT (Inferenz) — hier ist das erwünscht (kein Training).
Split wird aus split_manifest.json (vom cls-Builder) übernommen -> gleicher
haltungs-sauberer Schnitt, kein Leakage zwischen cls und detect.

Beispiel (Windows):
  python autobox_detect.py --labels C:\\KI_BRAIN\\training\\pdf_ingest\\labels.jsonl ^
      --manifest C:\\KI_BRAIN\\training\\datasets\\cls_v1\\split_manifest.json ^
      --out C:\\KI_BRAIN\\training\\datasets\\detect_v1
"""
from __future__ import annotations
import argparse, base64, json, os, struct, urllib.request

SIDECAR = os.getenv("SEWER_SIDECAR_URL", "http://127.0.0.1:8100")

# class_map v2 (feste Reihenfolge = ID). Muss zur produktiven Karte passen.
CLASS_ORDER = ["BCA_anschluss", "BAB_riss", "BAC_bruch", "BAA_verformung", "BAF_oberflaeche",
               "BAH_schadanschluss", "BAI_dichtung", "BAJ_verbindung", "BBA_wurzeln",
               "BBB_anhaftung", "BBC_ablagerung", "BBD_boden", "BBF_infiltration"]
CID = {k: i for i, k in enumerate(CLASS_ORDER)}

# DINO-Prompts je Klasse (englisch — DINO reagiert darauf besser). Bewusst tunebar.
PROMPT = {
    "BCA_anschluss": "lateral pipe connection, inlet hole, junction",
    "BAB_riss": "crack in pipe wall", "BAC_bruch": "broken pipe, missing wall piece, hole",
    "BAA_verformung": "deformed pipe", "BAF_oberflaeche": "surface damage, corrosion, spalling",
    "BAH_schadanschluss": "defective intruding connection", "BAI_dichtung": "intruding sealing material",
    "BAJ_verbindung": "displaced offset pipe joint", "BBA_wurzeln": "tree roots in pipe",
    "BBB_anhaftung": "encrustation, attached deposits", "BBC_ablagerung": "sediment deposit, debris",
    "BBD_boden": "soil ingress into pipe", "BBF_infiltration": "water infiltration, leak",
}


def img_size(path):
    try:
        from PIL import Image
        with Image.open(path) as im:
            return im.size
    except Exception:
        with open(path, "rb") as fh:                       # PNG-IHDR-Fallback
            head = fh.read(26)
        if head[:8] == b"\x89PNG\r\n\x1a\n":
            return struct.unpack(">II", head[16:24])
        raise


def dino(image_b64, prompt, box_thr, text_thr, timeout=60):
    body = json.dumps({"image_base64": image_b64, "text_prompt": prompt,
                       "box_threshold": box_thr, "text_threshold": text_thr}).encode()
    req = urllib.request.Request(SIDECAR + "/detect/dino", data=body,
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read())


def sidecar_up():
    try:
        with urllib.request.urlopen(SIDECAR + "/health", timeout=3) as r:
            return 200 <= r.status < 300
    except Exception:
        return False


def run(labels, labels_dir, manifest, out, box_thr, text_thr, multi, limit):
    if not sidecar_up():
        raise SystemExit(f"Sidecar unter {SIDECAR} nicht erreichbar. Bitte starten (DINO wird gebraucht).")
    split_of = {}
    if manifest and os.path.exists(manifest):
        split_of = json.load(open(manifest, encoding="utf-8")).get("haltungen", {})
    for s in ("train", "val", "gold"):
        os.makedirs(os.path.join(out, "images", s), exist_ok=True)
        os.makedirs(os.path.join(out, "labels", s), exist_ok=True)
    review = open(os.path.join(out, "no_box_review.jsonl"), "w", encoding="utf-8")
    stats = {"boxed": 0, "no_box": 0, "degraded": 0, "per_class": {}}
    rows = [json.loads(l) for l in open(labels, encoding="utf-8")]
    if limit:
        rows = rows[:limit]
    for i, lab in enumerate(rows):
        cls = lab["class"]
        if cls not in CID:
            continue
        src = os.path.join(labels_dir, lab["image"])
        if not os.path.exists(src):
            continue
        split = split_of.get(lab["haltung"], "train")
        b64 = base64.b64encode(open(src, "rb").read()).decode()
        try:
            resp = dino(b64, PROMPT[cls], box_thr, text_thr)
        except Exception as e:
            stats["degraded"] += 1; review.write(json.dumps({"image": lab["image"], "class": cls, "err": str(e)[:100]}) + "\n"); continue
        if resp.get("degraded"):
            stats["degraded"] += 1; review.write(json.dumps({"image": lab["image"], "class": cls, "err": "degraded"}) + "\n"); continue
        dets = sorted(resp.get("detections", []), key=lambda d: -d["confidence"])
        if not dets:
            stats["no_box"] += 1
            review.write(json.dumps({"image": lab["image"], "class": cls, "reason": "no_box"}) + "\n")
            continue
        if not multi:
            dets = dets[:1]                                # bekannte Einzelklasse -> beste Box
        W, H = img_size(src)
        lines = []
        for d in dets:
            xc = ((d["x1"] + d["x2"]) / 2) / W; yc = ((d["y1"] + d["y2"]) / 2) / H
            w = abs(d["x2"] - d["x1"]) / W; h = abs(d["y2"] - d["y1"]) / H
            lines.append(f"{CID[cls]} {xc:.6f} {yc:.6f} {w:.6f} {h:.6f}")
        base = os.path.splitext(os.path.basename(src))[0]
        import shutil
        shutil.copy2(src, os.path.join(out, "images", split, base + ".png"))
        open(os.path.join(out, "labels", split, base + ".txt"), "w").write("\n".join(lines) + "\n")
        stats["boxed"] += 1
        stats["per_class"][cls] = stats["per_class"].get(cls, 0) + 1
        if i % 200 == 0:
            print(f"...{i}/{len(rows)} boxed={stats['boxed']} no_box={stats['no_box']}", flush=True)
    review.close()
    # dataset.yaml (nur train/val fürs Training; gold bleibt versiegelt)
    with open(os.path.join(out, "dataset.yaml"), "w", encoding="utf-8") as fh:
        fh.write(f"path: {out}\ntrain: images/train\nval: images/val\nnames:\n")
        for i, k in enumerate(CLASS_ORDER):
            fh.write(f"  {i}: {k}\n")
    json.dump(stats, open(os.path.join(out, "autobox_summary.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print(f"\nfertig: geboxt={stats['boxed']}  ohne Box={stats['no_box']}  degraded={stats['degraded']}")
    print("pro Klasse:", stats["per_class"])
    print(f"Review-Liste (ohne Box): {os.path.join(out, 'no_box_review.jsonl')}")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--labels", required=True)
    ap.add_argument("--manifest", default="", help="split_manifest.json vom cls-Builder (gleicher Split)")
    ap.add_argument("--out", default=r"C:\KI_BRAIN\training\datasets\detect_v1")
    ap.add_argument("--box-thr", type=float, default=0.30)
    ap.add_argument("--text-thr", type=float, default=0.25)
    ap.add_argument("--multi", action="store_true", help="alle Boxen behalten statt nur der besten")
    ap.add_argument("--limit", type=int, default=0, help="nur erste N Frames (Pilot)")
    a = ap.parse_args()
    labels_dir = os.path.dirname(os.path.abspath(a.labels))
    run(a.labels, labels_dir, a.manifest, a.out, a.box_thr, a.text_thr, a.multi, a.limit)
