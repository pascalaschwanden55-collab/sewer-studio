#!/usr/bin/env python3
"""Auto-Boxing-Pilot: prueft, ob DINO auf unseren Frames die richtige Stelle einrahmt.

Nimmt je Schadensklasse einige Frames, schickt sie mit dem Klassen-Prompt an den
Sidecar (Grounding DINO), zeichnet die gefundenen Boxen SICHTBAR ein und legt sie
zur Sichtpruefung ab. Kein Training, nur Diagnose.

Voraussetzung: der Sidecar LAEUFT (DINO wird gebraucht).
  python autobox_pilot.py
"""
from __future__ import annotations
import argparse, base64, collections, json, os, urllib.request

PROMPT = {
    "BCA": "lateral pipe connection, inlet hole, junction",
    "BAB": "crack in pipe wall", "BAC": "broken pipe, missing wall piece, hole",
    "BAA": "deformed pipe", "BAF": "surface damage, corrosion, spalling",
    "BAH": "defective intruding connection", "BAI": "intruding sealing material",
    "BAJ": "displaced offset pipe joint", "BBA": "tree roots in pipe",
    "BBB": "encrustation, attached deposits", "BBC": "sediment deposit, debris",
    "BBD": "soil ingress into pipe", "BBF": "water infiltration, leak",
}


def resolve_token(token_file=""):
    """Sidecar-Auth-Token holen. Reihenfolge wie im Sidecar: env
    SEWER_SIDECAR_AUTH_TOKEN -> Token-Datei (%LOCALAPPDATA%\\SewerStudio\\.sidecar_token).
    JEDE Anfrage an den Sidecar (auch /health) braucht diesen Token, sonst 401."""
    t = os.getenv("SEWER_SIDECAR_AUTH_TOKEN", "").strip()
    if t:
        return t
    path = token_file.strip() if token_file else os.path.join(
        os.getenv("LOCALAPPDATA", os.path.expanduser("~")), "SewerStudio", ".sidecar_token")
    try:
        return open(path, encoding="utf-8").read().strip()
    except Exception:
        return ""


def dino(url, image_b64, prompt, box_thr, text_thr, token, timeout=60):
    body = json.dumps({"image_base64": image_b64, "text_prompt": prompt,
                       "box_threshold": box_thr, "text_threshold": text_thr}).encode()
    req = urllib.request.Request(url + "/detect/dino", data=body,
                                 headers={"Content-Type": "application/json",
                                          "X-Sidecar-Token": token})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--labels", default=r"C:\KI_BRAIN\training\pdf_ingest\labels.jsonl")
    ap.add_argument("--frames-root", default=r"C:\KI_BRAIN\training\pdf_ingest")
    ap.add_argument("--out", default=r"C:\KI_BRAIN\training\autobox_pilot")
    ap.add_argument("--sidecar", default="http://127.0.0.1:8100")
    ap.add_argument("--per-class", type=int, default=5)
    ap.add_argument("--box-thr", type=float, default=0.30)
    ap.add_argument("--text-thr", type=float, default=0.25)
    ap.add_argument("--token-file", default="")
    a = ap.parse_args()

    token = resolve_token(a.token_file)
    if not token:
        print("WARNUNG: kein Sidecar-Token gefunden "
              "(%LOCALAPPDATA%\\SewerStudio\\.sidecar_token). Der Sidecar lehnt sonst mit 401 ab.")

    from PIL import Image, ImageDraw
    vizdir = os.path.join(a.out, "viz"); os.makedirs(vizdir, exist_ok=True)

    # je Klasse bis zu per-class Frames auswaehlen
    by_cls = collections.defaultdict(list)
    for l in (json.loads(x) for x in open(a.labels, encoding="utf-8")):
        mc = l["code"][:3]
        if mc in PROMPT and len(by_cls[mc]) < a.per_class:
            src = os.path.join(a.frames_root, l["image"].replace("\\", os.sep))
            if os.path.exists(src):
                by_cls[mc].append((src, l["code"], l.get("desc", "")))

    stats = {}
    total = boxed = 0
    print("Sidecar: " + a.sidecar + "  (muss laufen)")
    for mc in sorted(by_cls):
        hit = 0
        for src, code, desc in by_cls[mc]:
            total += 1
            try:
                b64 = base64.b64encode(open(src, "rb").read()).decode()
                resp = dino(a.sidecar, b64, PROMPT[mc], a.box_thr, a.text_thr, token)
            except Exception as e:
                print("  FEHLER DINO (" + str(e)[:60] + ") - Sidecar laeuft? Token da?"); continue
            dets = sorted(resp.get("detections", []), key=lambda d: -d["confidence"])
            im = Image.open(src).convert("RGB"); dr = ImageDraw.Draw(im)
            for d in dets[:3]:
                dr.rectangle([d["x1"], d["y1"], d["x2"], d["y2"]], outline=(255, 0, 0), width=3)
                dr.text((d["x1"] + 2, max(0, d["y1"] - 12)),
                        code + " " + str(round(d["confidence"], 2)), fill=(255, 255, 0))
            if dets:
                hit += 1; boxed += 1
            out = os.path.join(vizdir, mc + "__" + str(len(dets)) + "box__" + os.path.basename(src)[:40] + ".jpg")
            im.save(out, "JPEG", quality=88)
        n = len(by_cls[mc]); stats[mc] = (hit, n)
        print("  " + mc + ": " + str(hit) + "/" + str(n) + " mit Box")
    json.dump(stats, open(os.path.join(a.out, "pilot_summary.json"), "w"), indent=1)
    print("\nGESAMT mit Box: " + str(boxed) + "/" + str(total) + "  -> Bilder in " + vizdir)


if __name__ == "__main__":
    main()
