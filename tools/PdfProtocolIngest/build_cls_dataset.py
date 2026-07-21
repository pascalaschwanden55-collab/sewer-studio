#!/usr/bin/env python3
"""Baut aus dem PDF-Ingest ein cls-Dataset  "schaden" vs "normal" (das Gate).

plan  : rechnet nur den haltungs-sauberen Split (train/val/gold) aus - kein Bild noetig.
build : kopiert Schaden-Frames in train/val, zieht "normal"-Frames aus schadenfreien
        Abschnitten per ffmpeg. Absturzsicher, fortsetzbar, mit Fortschritt.

Eine Haltung liegt immer ganz in EINEM Split. gold ist versiegelt (nicht ins Training).
"""
from __future__ import annotations
import argparse, json, os, random, re, shutil, subprocess


def load(jl):
    return [json.loads(l) for l in open(jl, encoding="utf-8")] if os.path.exists(jl) else []


def hkey_pdf(pdf):                      # Ordner der Inspektion, separator-neutral
    return os.path.dirname(pdf.replace("\\", "/"))


def hkey_lab(h):
    return (h or "").replace("\\", "/")


def split_haltungen(keys, seed, val, gold):
    ks = sorted(keys); random.Random(seed).shuffle(ks)
    n = len(ks); ng = int(n * gold); nv = int(n * val)
    out = {}
    for k in ks[:ng]:
        out[k] = "gold"
    for k in ks[ng:ng + nv]:
        out[k] = "val"
    for k in ks[ng + nv:]:
        out[k] = "train"
    return out


def hms(ts):
    h, m, s = (int(x) for x in ts.split(":"))
    return h * 3600 + m * 60 + s


def plan(cat, seed, val, gold):
    per = {}
    for r in cat:
        if r.get("skipped"):
            continue
        k = hkey_pdf(r["pdf"]); d = sum(1 for f in r["findings"] if f.get("is_damage"))
        cur = per.get(k, 0); per[k] = cur + d
    assign = split_haltungen(per.keys(), seed, val, gold)
    agg = {s: [0, 0] for s in ("train", "val", "gold")}
    for k, d in per.items():
        s = assign[k]; agg[s][0] += 1; agg[s][1] += d
    print("Haltungen: " + str(len(per)) + "  (seed=" + str(seed) + ")")
    for s in ("train", "val", "gold"):
        print("  " + s + ": " + str(agg[s][0]) + " Haltungen, " + str(agg[s][1]) + " Schaden-Frames")
    return assign


def safe_normal_ts(findings, gap=8, maxn=3):
    dmg = sorted(hms(f["video_ts"]) for f in findings if f.get("is_damage") and f.get("video_ts"))
    if not dmg:
        return []
    end = max(dmg)
    out = []
    for t in range(5, max(6, end), 7):
        if all(abs(t - d) >= gap for d in dmg):
            out.append(t)
        if len(out) >= maxn:
            break
    return out


def build(cat, labels, labels_dir, root, out, seed, val, gold, normal_per):
    assign = plan(cat, seed, val, gold)
    for s in ("train", "val", "gold"):
        for c in ("schaden", "normal"):
            os.makedirs(os.path.join(out, s, c), exist_ok=True)
    json.dump({"seed": seed, "val": val, "gold": gold, "haltungen": assign},
              open(os.path.join(out, "split_manifest.json"), "w", encoding="utf-8"), indent=1)
    # 1) Schaden-Frames einsortieren
    ns = 0
    for lab in labels:
        try:
            s = assign.get(hkey_lab(lab.get("haltung")), "train")
            src = os.path.join(labels_dir, lab["image"].replace("\\", os.sep))
            dst = os.path.join(out, s, "schaden", os.path.basename(src))
            if os.path.exists(dst):
                continue
            if os.path.exists(src):
                shutil.copy2(src, dst); ns += 1
        except Exception as e:
            print("  SKIP schaden (" + str(e)[:40] + ")", flush=True)
    print("Schaden einsortiert: " + str(ns), flush=True)
    # 2) Normal-Frames ziehen
    nn = 0
    for idx, r in enumerate(cat):
        try:
            if r.get("skipped") or not r.get("video"):
                continue
            k = hkey_pdf(r["pdf"]); s = assign.get(k, "train")
            for t in safe_normal_ts(r["findings"], maxn=normal_per):
                png = os.path.join(out, s, "normal", re.sub(r'[^\w]+', "_", k + "_" + str(t)) + ".png")
                if os.path.exists(png):
                    continue
                subprocess.run(["ffmpeg", "-y", "-ss", str(t), "-i", os.path.join(root, r["video"].replace(chr(92), os.sep)),
                                "-frames:v", "1", "-q:v", "2", png], capture_output=True, timeout=120)
                if os.path.exists(png):
                    nn += 1
        except Exception as e:
            print("  SKIP normal (" + str(e)[:40] + ")", flush=True)
        if idx % 100 == 0:
            print("  ..." + str(idx) + "/" + str(len(cat)) + "  (" + str(nn) + " normal)", flush=True)
    print("FERTIG: " + str(ns) + " schaden + " + str(nn) + " normal -> " + out)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["plan", "build"])
    ap.add_argument("--catalog", required=True)
    ap.add_argument("--labels", default="")
    ap.add_argument("--root", default=r"D:\Haltungen")
    ap.add_argument("--out", default=r"C:\KI_BRAIN\training\datasets\cls_v1")
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--val", type=float, default=0.15)
    ap.add_argument("--gold", type=float, default=0.15)
    ap.add_argument("--normal-per", type=int, default=3)
    a = ap.parse_args()
    cat = load(a.catalog)
    if a.mode == "plan":
        plan(cat, a.seed, a.val, a.gold)
    else:
        ldir = os.path.dirname(os.path.abspath(a.labels))
        build(cat, load(a.labels), ldir, a.root, a.out, a.seed, a.val, a.gold, a.normal_per)
