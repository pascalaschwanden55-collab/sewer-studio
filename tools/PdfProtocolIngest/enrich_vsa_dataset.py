#!/usr/bin/env python3
"""Reichert den VSA-Klassifikator-Datensatz mit unseren PDF-Frames an (Schaden + LEER).

NICHT-DESTRUKTIV: baut/ergaenzt einen separaten Datensatz. Original, val-Split und
active.json bleiben unberuehrt.

Schaden: nur Klassen, die dein Klassifikator kennt (BAJ BAB BAF BAI BBB BBA).
LEER   : unsere Normal-Frames - aber NUR solche, die weit genug von JEDEM
         Protokolleintrag entfernt sind (auch BCD/BCE/BDA/BDD), damit LEER sauber bleibt.
Kontamination: Eval-Haltungen und Eval-Bild-Hashes werden immer ausgeschlossen.

  python enrich_vsa_dataset.py --dry-run
  python enrich_vsa_dataset.py
"""
from __future__ import annotations
import argparse, glob, hashlib, json, os, re, shutil

OVERLAP = {"BAJ", "BAB", "BAF", "BAI", "BBB", "BBA"}
EVAL_HALT = re.compile(r'^(.+?)_\d+(?:\.\d+)?s_')


def mangle(s):
    return re.sub(r'[^\w]+', "_", s or "")


def md5(path):
    h = hashlib.md5()
    with open(path, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest()


def hms(ts):
    h, m, s = (int(x) for x in ts.split(":"))
    return h * 3600 + m * 60 + s


def eval_signatures(eval_root):
    halt, hashes = set(), set()
    for p in glob.glob(os.path.join(eval_root, "**", "*.png"), recursive=True):
        m = EVAL_HALT.match(os.path.basename(p))
        if m:
            halt.add(m.group(1))
        try:
            hashes.add(md5(p))
        except Exception:
            pass
    return halt, hashes


def finding_times(catalog):
    """mangled Haltung -> sortierte Sekunden ALLER Protokolleintraege (nicht nur Schaeden)."""
    out = {}
    for r in (json.loads(l) for l in open(catalog, encoding="utf-8")):
        if r.get("skipped"):
            continue
        key = mangle(os.path.dirname(r["pdf"].replace("\\", "/")))
        ts = [hms(f["video_ts"]) for f in r["findings"] if f.get("video_ts")]
        if ts:
            out.setdefault(key, []).extend(ts)
    for k in out:
        out[k] = sorted(out[k])
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bal", default=r"C:\KI_BRAIN\yolo_vsa_cls_dataset_bal")
    ap.add_argument("--catalog", default=r"C:\KI_BRAIN\training\pdf_ingest\ingest.jsonl")
    ap.add_argument("--labels", default=r"C:\KI_BRAIN\training\pdf_ingest\labels.jsonl")
    ap.add_argument("--frames-root", default=r"C:\KI_BRAIN\training\pdf_ingest")
    ap.add_argument("--normal-src", default=r"C:\KI_BRAIN\training\datasets\cls_v1")
    ap.add_argument("--eval", default=r"C:\KI_BRAIN\eval_set")
    ap.add_argument("--out", default=r"C:\KI_BRAIN\yolo_vsa_cls_dataset_pdfplus")
    ap.add_argument("--leer-gap", type=int, default=10, help="Mindestabstand (s) zu JEDEM Eintrag")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()

    halt, hashes = eval_signatures(a.eval)
    halt_mangled = {mangle(h) for h in halt}
    ftimes = finding_times(a.catalog)
    print("Eval-Haltungen: " + str(len(halt)) + " | Eval-Hashes: " + str(len(hashes)))

    # ---- 1) Schadensframes (bekannte Klassen) ----
    plan_dmg, add_dmg, sk_h, sk_hash, sk_cls = [], {}, 0, 0, 0
    for l in (json.loads(x) for x in open(a.labels, encoding="utf-8")):
        main_code = l["code"][:3]
        if main_code not in OVERLAP:
            sk_cls += 1; continue
        hp = (l.get("haltung") or "").replace("\\", "/")
        if any(e in hp for e in halt):
            sk_h += 1; continue
        src = os.path.join(a.frames_root, l["image"].replace("\\", os.sep))
        if not os.path.exists(src):
            continue
        if md5(src) in hashes:
            sk_hash += 1; continue
        plan_dmg.append((src, main_code)); add_dmg[main_code] = add_dmg.get(main_code, 0) + 1

    # ---- 2) LEER-Frames (streng gefiltert) ----
    plan_leer, sk_near, sk_leval, sk_nokey = [], 0, 0, 0
    for split in ("train", "val"):            # gold bleibt versiegelt
        for p in glob.glob(os.path.join(a.normal_src, split, "normal", "*.png")):
            base = os.path.splitext(os.path.basename(p))[0]
            if "_" not in base:
                sk_nokey += 1; continue
            key, tstr = base.rsplit("_", 1)
            if not tstr.isdigit():
                sk_nokey += 1; continue
            if any(e and e in key for e in halt_mangled):
                sk_leval += 1; continue
            t = int(tstr)
            times = ftimes.get(key)
            if times is None:
                sk_nokey += 1; continue
            if min(abs(t - x) for x in times) < a.leer_gap:
                sk_near += 1; continue
            if md5(p) in hashes:
                sk_leval += 1; continue
            plan_leer.append(p)

    print("\nSchaden (nur bekannte Klassen):")
    for c in sorted(add_dmg):
        print("  " + c + ": +" + str(add_dmg[c]))
    print("  Summe Schaden: +" + str(len(plan_dmg)))
    print("  ausgeschlossen -> Eval-Haltung " + str(sk_h) + ", Eval-Hash " + str(sk_hash) +
          ", fremde Klasse " + str(sk_cls))
    print("\nLEER: +" + str(len(plan_leer)))
    print("  ausgeschlossen -> zu nah an einem Eintrag " + str(sk_near) +
          ", Eval " + str(sk_leval) + ", ohne Zuordnung " + str(sk_nokey))

    if a.dry_run:
        print("\n(DRY-RUN: nichts kopiert.)"); return

    if not os.path.isdir(a.out):
        print("\nKopiere Basis-Datensatz -> " + a.out + " ...", flush=True)
        shutil.copytree(a.bal, a.out)
    n = 0
    for src, cls in plan_dmg:
        d = os.path.join(a.out, "train", cls); os.makedirs(d, exist_ok=True)
        dst = os.path.join(d, "pdfplus_" + os.path.basename(src))
        if not os.path.exists(dst):
            shutil.copy2(src, dst); n += 1
    nl = 0
    d = os.path.join(a.out, "train", "LEER"); os.makedirs(d, exist_ok=True)
    for src in plan_leer:
        dst = os.path.join(d, "pdfplus_" + os.path.basename(src))
        if not os.path.exists(dst):
            shutil.copy2(src, dst); nl += 1
    print("FERTIG: +" + str(n) + " Schaden, +" + str(nl) + " LEER -> " + a.out)


if __name__ == "__main__":
    main()
