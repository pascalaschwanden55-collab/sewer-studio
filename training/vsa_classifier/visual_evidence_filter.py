"""
Sichtbarkeits-Filter fuer Trainings-Frames (Paket 5).

Problem: Frame-Extraktion am Protokoll-Meterstand trifft das Merkmal oft nicht —
viele BCA-Frames zeigen leeres Rohr (Label-Audit 2026-06-07, v8-Befund 2026-06-10:
BCA wird zum Staubsauger). Loesung: Grounding DINO prueft pro Frame, ob eine
Anschluss-Oeffnung ueberhaupt SICHTBAR ist. Es wird KEIN Label erfunden — das Label
kommt weiter aus dem Protokoll; hier werden nur Frames ausgemustert, die das
gelabelte Merkmal nicht zeigen.

Der Filter ENTSCHEIDET NICHT selbst: er schreibt pro Frame die DINO-Evidenz
(bestes Label, Konfidenz, Box-Geometrie) in eine CSV. Die Behalten/Raus-Regel
wird danach auf der CSV getunt (billig, ohne DINO-Neulauf) und als Exclude-Liste
fuer den ClassifierDatasetBuilder exportiert.

Aufruf (Sidecar muss laufen):
  python training/vsa_classifier/visual_evidence_filter.py --code-prefix BCA
  python training/vsa_classifier/visual_evidence_filter.py --code-prefix BCA --limit 50   # Probelauf

Resume-sicher: bereits gemessene Dateien (in der CSV) werden uebersprungen.
"""
import argparse
import base64
import csv
import json
import os
import re
import sys
import time
import urllib.request

FNAME_RE = re.compile(r"^(?P<haltung>.+?)_(?P<zeit>[0-9.]+)s_(?P<code>.+?)(_t[+-]\d+)?\.png$", re.IGNORECASE)

# Nur Anschluss-Begriffe (Teilmenge der App-Labels aus sidecar/config.py) —
# bewusst OHNE allgemeine Schadensbegriffe, wir suchen genau ein Merkmal.
PROMPT = "lateral connection . junction . inlet . branch . side opening . pipe opening"


def sidecar_token():
    tok = os.environ.get("SEWER_SIDECAR_AUTH_TOKEN") or os.environ.get("SEWER_SIDECAR_TOKEN")
    if tok:
        return tok.strip()
    base = os.environ.get("LOCALAPPDATA") or ""
    path = os.path.join(base, "SewerStudio", ".sidecar_token")
    with open(path, encoding="utf-8") as f:
        return f.read().strip()


def dino_detect(url, token, image_path, box_thr, text_thr, timeout=120):
    with open(image_path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode("ascii")
    body = json.dumps({
        "image_base64": b64,
        "text_prompt": PROMPT,
        "box_threshold": box_thr,
        "text_threshold": text_thr,
    }).encode("utf-8")
    req = urllib.request.Request(
        url + "/detect/dino", data=body,
        headers={"Content-Type": "application/json", "X-Sidecar-Token": token})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--frames", default=r"C:\KI_BRAIN\training_frames")
    ap.add_argument("--code-prefix", required=True, help="z.B. BCA — Code-Token-Prefix der zu messenden Frames")
    ap.add_argument("--sidecar", default=os.environ.get("SEWER_SIDECAR_URL", "http://127.0.0.1:8100"))
    ap.add_argument("--box-threshold", type=float, default=0.25)
    ap.add_argument("--text-threshold", type=float, default=0.20)
    ap.add_argument("--out", default=None, help="CSV-Pfad (Default: docs/benchmarks/_evidence_<prefix>.csv)")
    ap.add_argument("--limit", type=int, default=0, help="nur N Frames (Probelauf)")
    args = ap.parse_args()

    out_csv = args.out or os.path.join("docs", "benchmarks", f"_evidence_{args.code_prefix.lower()}.csv")
    os.makedirs(os.path.dirname(out_csv), exist_ok=True)
    token = sidecar_token()

    # Kandidaten einsammeln (rekursiv, wie der Dataset-Builder)
    files = []
    prefix = args.code_prefix.upper()
    for root, _dirs, names in os.walk(args.frames):
        for n in names:
            if not n.lower().endswith(".png"):
                continue
            m = FNAME_RE.match(n)
            if m and m.group("code").upper().startswith(prefix):
                files.append(os.path.join(root, n))
    files.sort()
    print(f"{len(files)} Frames mit Code-Prefix {prefix} gefunden")

    # Resume: schon gemessene Basenames ueberspringen
    done = set()
    if os.path.isfile(out_csv):
        with open(out_csv, newline="", encoding="utf-8") as f:
            done = {row["file"] for row in csv.DictReader(f)}
        print(f"{len(done)} bereits gemessen — werden uebersprungen")

    new_file = not os.path.isfile(out_csv)
    measured = 0
    t0 = time.time()
    with open(out_csv, "a", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        if new_file:
            w.writerow(["file", "boxes", "best_label", "best_conf",
                        "best_area_frac", "best_cx", "best_cy", "degraded"])
        for path in files:
            base = os.path.basename(path)
            if base in done:
                continue
            if args.limit and measured >= args.limit:
                break
            try:
                r = dino_detect(args.sidecar, token, path, args.box_threshold, args.text_threshold)
            except Exception as exc:
                print(f"FEHLER {base}: {exc}", file=sys.stderr)
                w.writerow([base, -1, "", 0.0, 0.0, 0.0, 0.0, "request_error"])
                f.flush()
                continue
            dets = r.get("detections") or []
            best = max(dets, key=lambda d: d["confidence"]) if dets else None
            # Geometrie normiert auf Bildflaeche unbekannter Groesse? DINO liefert Pixel —
            # Flaechenanteil hier ueber das Boxformat selbst (Pixel), normiert spaeter beim
            # Tunen anhand bekannter Frame-Groesse (1920x1080 bzw. 720x576).
            if best:
                area = max(0.0, (best["x2"] - best["x1"])) * max(0.0, (best["y2"] - best["y1"]))
                cx = (best["x1"] + best["x2"]) / 2.0
                cy = (best["y1"] + best["y2"]) / 2.0
                w.writerow([base, len(dets), best.get("phrase") or best.get("label"),
                            round(best["confidence"], 4), round(area, 1),
                            round(cx, 1), round(cy, 1), bool(r.get("degraded"))])
            else:
                w.writerow([base, 0, "", 0.0, 0.0, 0.0, 0.0, bool(r.get("degraded"))])
            f.flush()
            measured += 1
            if measured % 100 == 0:
                rate = measured / (time.time() - t0)
                rest = (len(files) - len(done) - measured) / max(rate, 0.01)
                print(f"  {measured} gemessen ({rate:.1f}/s, Rest ~{rest/60:.0f} min)", flush=True)

    print(f"FERTIG: {measured} neu gemessen -> {out_csv}")


if __name__ == "__main__":
    main()
