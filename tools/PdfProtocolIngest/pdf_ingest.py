#!/usr/bin/env python3
"""PDF-Protokoll-Ingest - Inspektions-PDFs -> experten-gelabelte Frames."""
from __future__ import annotations
import argparse, glob, json, os, re, subprocess, time

FINDING = re.compile(
    r'^\s+(?P<meter>\d+[.,]\d{2})\s+(?P<code>[A-Z]{2,7})\s+(?P<desc>.+?)\s{2,}'
    r'(?P<ts>\d{2}:\d{2}:\d{2})(?:\s+(?P<foto>[\w\-.,\s]+?))?\s*$')
UHR_RANGE = re.compile(r'von\s+(\d{1,2})\s*Uhr\s+bis\s+(\d{1,2})\s*Uhr')
UHR_POINT = re.compile(r'bei\s+(\d{1,2})\s*Uhr')
VIDEXT = (".mpg", ".mp4", ".mp2", ".avi", ".mpeg", ".mov")
DMG = {"BCA": "BCA_anschluss", "BAB": "BAB_riss", "BAC": "BAC_bruch", "BAA": "BAA_verformung",
       "BAF": "BAF_oberflaeche", "BAH": "BAH_schadanschluss", "BAI": "BAI_dichtung",
       "BAJ": "BAJ_verbindung", "BBA": "BBA_wurzeln", "BBB": "BBB_anhaftung",
       "BBC": "BBC_ablagerung", "BBD": "BBD_boden", "BBF": "BBF_infiltration"}


def _sh(args, t=25):
    try:
        return subprocess.run(args, capture_output=True, text=True, timeout=t).stdout
    except Exception:
        return ""


def _template(pdf):
    m = re.search(r'Creator:\s*(.+)', _sh(["pdfinfo", pdf]))
    cr = m.group(1).strip() if m else ""
    return ("combit" if "combit" in cr else "ncreport" if "NCReport" in cr else
            "pdf24" if "PDF24" in cr else "faktura" if "norm 175" in cr else "unknown")


def _find_video(folder, root):
    vids = [f for f in glob.glob(os.path.join(folder, "*")) if f.lower().endswith(VIDEXT)]
    return os.path.relpath(max(vids, key=os.path.getsize), root) if vids else None


def _clock(desc):
    r = UHR_RANGE.search(desc)
    if r:
        return [int(r.group(1)), int(r.group(2))]
    p = UHR_POINT.search(desc)
    return [int(p.group(1))] if p else None


def _line_findings(lines):
    findings, cur = [], None
    for ln in lines:
        m = FINDING.match(ln)
        if m:
            code = m["code"]; main = code[:3]
            cur = {"meter": float(m["meter"].replace(",", ".")), "code": code, "main": main,
                   "class": DMG.get(main), "is_damage": main in DMG,
                   "desc": m["desc"].strip(), "video_ts": m["ts"], "clock": _clock(m["desc"]),
                   "foto": (m["foto"] or "").strip().rstrip(",")}
            findings.append(cur)
        elif cur is not None:
            s = ln.strip()
            if s and not s[0].isdigit() and len(s) < 70 and "Uhr" not in s and \
               not s.startswith(("Seite", "1:", "Ort", "KIT", "MPEG", "OP", "Tel.", "info",
                                 "Neuhaltenring", "Haltungsbilder")) and re.match(r'^[A-Za-z]', s):
                cur["desc"] = (cur["desc"] + " " + s).strip()
    return findings


KINS_V = re.compile(r'Video\s+(\d{2}:\d{2}:\d{2})')
KINS_M = re.compile(r'Entf\.\s+(?:gegen|in)\s+Flie\w+r\.\s+([\d,]+)\s*m')
KINS_Z = re.compile(r'Zustand\s+([A-Z][A-Z0-9.]*)')
KINS_P = re.compile(r'Position\s+(\d+)')
KINS_D = re.compile(r'Pos:\s*\d+;\s*(.+)')
KINS_F = re.compile(r'Foto\s+(\d+)')


def _kins_findings(lines):
    fnd = []; ts = None; meter = None; foto = ""
    for i, ln in enumerate(lines):
        s = ln.strip()
        mv = KINS_V.search(s); mm = KINS_M.search(s); mf = KINS_F.match(s)
        if mv:
            ts = mv.group(1)
        if mm:
            meter = float(mm.group(1).replace(",", "."))
        if mf:
            foto = mf.group(1)
        mz = KINS_Z.match(s)
        if mz:
            code = mz.group(1).replace(".", ""); main = code[:3]
            clock = None; desc = ""
            for j in range(i + 1, min(i + 8, len(lines))):
                t = lines[j].strip()
                mp = KINS_P.match(t); md = KINS_D.match(t)
                if mp and not clock:
                    clock = [int(mp.group(1))]
                if md and not desc:
                    desc = md.group(1).strip()
            fnd.append({"meter": meter, "code": code, "main": main, "class": DMG.get(main),
                        "is_damage": main in DMG, "desc": desc, "video_ts": ts,
                        "clock": clock, "foto": foto})
    return fnd


def parse_pdf(path, root):
    kind = _template(path)
    rec = {"pdf": os.path.relpath(path, root), "template": kind}
    if kind == "faktura":
        rec["skipped"] = True; rec["findings"] = []; return rec
    lines = _sh(["pdftotext", "-layout", "-f", "1", "-l", "8", path, "-"]).splitlines()
    if kind == "unknown":
        findings = _kins_findings(lines)
        if findings:
            rec["template"] = "kins"
    else:
        findings = _line_findings(lines)
    rec["skipped"] = False
    rec["video"] = _find_video(os.path.dirname(path), root)
    rec["findings"] = findings
    return rec


def do_parse(root, out, budget):
    os.makedirs(out, exist_ok=True)
    jl = os.path.join(out, "ingest.jsonl")
    done = set()
    if os.path.exists(jl):
        for line in open(jl, encoding="utf-8"):
            try:
                done.add(json.loads(line)["pdf"])
            except Exception:
                pass
    pdfs = sorted(glob.glob(os.path.join(root, "**", "*.pdf"), recursive=True))
    t0 = time.time(); n = 0
    with open(jl, "a", encoding="utf-8") as f:
        for p in pdfs:
            rel = os.path.relpath(p, root)
            if rel in done:
                continue
            if budget and time.time() - t0 > budget:
                print("PAUSE - erneut starten"); break
            try:
                rec = parse_pdf(p, root)
            except Exception as e:
                rec = {"pdf": rel, "template": "error", "skipped": True,
                       "error": str(e)[:120], "findings": []}
            f.write(json.dumps(rec, ensure_ascii=False) + "\n"); f.flush(); n += 1
    print("parse: " + str(n) + " neu, " + str(len(done) + n) + "/" + str(len(pdfs)) + " -> " + jl)


def do_extract(root, out, window):
    jl = os.path.join(out, "ingest.jsonl")
    labels = os.path.join(out, "labels.jsonl")
    seen = set()
    if os.path.exists(labels):
        for line in open(labels, encoding="utf-8"):
            try:
                seen.add(json.loads(line)["id"])
            except Exception:
                pass
    recs = [json.loads(l) for l in open(jl, encoding="utf-8")]
    n = 0
    lf = open(labels, "a", encoding="utf-8")
    for idx, r in enumerate(recs):
        try:
            if r.get("skipped") or not r.get("video"):
                continue
            video = os.path.join(root, r["video"])
            for i, fnd in enumerate(r["findings"]):
                if not fnd.get("is_damage"):
                    continue
                fid = r["pdf"] + "#" + str(i)
                if fid in seen:
                    continue
                cls = fnd["class"]
                cls_dir = os.path.join(out, "frames", cls)
                os.makedirs(cls_dir, exist_ok=True)
                stem = re.sub(r'[^\w]+', "_", os.path.dirname(r["pdf"]) + "_" +
                              format(fnd["meter"], ".2f") + "_" + fnd["code"])
                png = os.path.join(cls_dir, stem + ".png")
                subprocess.run(["ffmpeg", "-y", "-ss", fnd["video_ts"], "-i", video,
                                "-frames:v", "1", "-q:v", "2", png], capture_output=True, timeout=120)
                if os.path.exists(png):
                    rec = {"id": fid, "image": os.path.relpath(png, out), "class": cls,
                           "code": fnd["code"], "meter": fnd["meter"], "clock": fnd["clock"],
                           "desc": fnd["desc"], "haltung": os.path.dirname(r["pdf"]),
                           "video_ts": fnd["video_ts"]}
                    lf.write(json.dumps(rec, ensure_ascii=False) + "\n"); lf.flush(); n += 1
        except Exception as e:
            print("  SKIP " + str(r.get("pdf", "?")) + " (" + str(e)[:50] + ")", flush=True)
        if idx % 50 == 0:
            print("  ..." + str(idx) + "/" + str(len(recs)) + "  (" + str(n) + " Frames)", flush=True)
    lf.close()
    print("extract: " + str(n) + " Frames -> " + os.path.join(out, "frames"))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["parse", "extract"])
    ap.add_argument("--root", default=r"D:\Haltungen")
    ap.add_argument("--out", default=r"C:\KI_BRAIN\training\pdf_ingest")
    ap.add_argument("--budget", type=float, default=0)
    ap.add_argument("--window", type=float, default=0.0)
    a = ap.parse_args()
    if a.mode == "parse":
        do_parse(a.root, a.out, a.budget)
    else:
        do_extract(a.root, a.out, a.window)
