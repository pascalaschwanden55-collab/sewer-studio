"""
Sichtbarer Mini-Beweis: Erzeugt fuer repraesentative Eval-Frames (je Code) die
Filter-Varianten nebeneinander, damit man mit dem Auge prueft, ob die Aufbereitung
Schaeden sichtbarer macht ODER Kanten/Risse verfaelscht.

Varianten: Original | bilateral_cuda (Fallback CPU) | hqdn3d | nlmeans  (alle "treu", erfinden nichts)
Deinterlace (bwdif) und Multi-Frame brauchen das Quell-VIDEO -> separater Test.

  python training/vsa_classifier/enhance_preview.py
"""
import os
import re
import subprocess
from PIL import Image, ImageDraw, ImageFont

EVAL_IMG = r"C:\Sewer-Studio_KI_4.4\EvalVisibilityReview_20260525\eval_visible_clean_eval_set\images"
OUT_DIR = r"C:\tmp\enhance_preview"
os.makedirs(OUT_DIR, exist_ok=True)

WANT = ["BCD", "BDDC", "BAIZ", "BAJB", "BDA", "BABBA"]  # + 2x kein_schaden (LEER)


def code_of(name):
    m = re.match(r"^.+?_[0-9.]+s_(.+?)(_t[+-]\d+)?\.png$", name, re.I)
    return m.group(1) if m else None


def pick_frames():
    allpng = sorted(os.listdir(EVAL_IMG))
    out = []
    for w in WANT:
        for n in allpng:
            if code_of(n) == w:
                out.append((w, os.path.join(EVAL_IMG, n)))
                break
    leer = [n for n in allpng if code_of(n) == "kein_schaden"][:2]
    for n in leer:
        out.append(("LEER", os.path.join(EVAL_IMG, n)))
    return out


def ff(vf, src, dst, cuda=False):
    pre = ["-init_hw_device", "cuda=cu", "-filter_hw_device", "cu"] if cuda else []
    cmd = ["ffmpeg", "-y", "-loglevel", "error", *pre, "-i", src, "-vf", vf, dst]
    r = subprocess.run(cmd, capture_output=True, text=True)
    ok = r.returncode == 0 and os.path.exists(dst) and os.path.getsize(dst) > 0
    return ok, r.stderr.strip()


def make_variant(label, src, idx):
    """Erzeugt eine Variante, gibt (Pfad, Anzeigename) zurueck."""
    base = os.path.join(OUT_DIR, f"f{idx}")
    if label == "Original":
        return src, "Original"
    if label == "bilateral":
        dst = base + "_bil.png"
        ok, _ = ff("format=yuv420p,hwupload_cuda,bilateral_cuda=sigmaS=2.0:sigmaR=20.0:window_size=5,hwdownload,format=yuv420p", src, dst, cuda=True)
        if ok:
            return dst, "bilateral_cuda"
        ok, err = ff("bilateral=sigmaS=10:sigmaR=0.1", src, dst, cuda=False)
        return (dst, "bilateral (CPU)") if ok else (None, f"bilateral FEHLER: {err[:60]}")
    if label == "hqdn3d":
        dst = base + "_hq.png"
        ok, err = ff("hqdn3d=4:3:6:4", src, dst)
        return (dst, "hqdn3d") if ok else (None, f"hqdn3d FEHLER: {err[:60]}")
    if label == "nlmeans":
        dst = base + "_nlm.png"
        ok, err = ff("nlmeans=s=2.0", src, dst)
        return (dst, "nlmeans") if ok else (None, f"nlmeans FEHLER: {err[:60]}")
    return None, "?"


def font(sz):
    try:
        return ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", sz)
    except Exception:
        return ImageFont.load_default()


def main():
    frames = pick_frames()
    cols = ["Original", "bilateral", "hqdn3d", "nlmeans"]
    cell_w, lab_h = 460, 30
    f = font(20)

    # erst alle Varianten erzeugen
    grid = []  # je Frame: (code, [(img_path, name), ...])
    for i, (codе, src) in enumerate(frames):
        variants = [make_variant(c, src, i) for c in cols]
        grid.append((codе, variants))
        print(f"[{i+1}/{len(frames)}] {codе:12} " + " | ".join(n for _, n in variants))

    # Zellhoehe aus erstem Bild
    with Image.open(frames[0][1]) as im0:
        cell_h = int(cell_w * im0.height / im0.width)

    W = cell_w * len(cols)
    H = (cell_h + lab_h) * len(grid)
    canvas = Image.new("RGB", (W, H), (20, 20, 20))
    draw = ImageDraw.Draw(canvas)

    for r, (codе, variants) in enumerate(grid):
        y = r * (cell_h + lab_h)
        for c, (path, name) in enumerate(variants):
            x = c * cell_w
            draw.rectangle([x, y, x + cell_w, y + lab_h], fill=(0, 0, 0))
            draw.text((x + 6, y + 5), f"{codе}  |  {name}", fill=(255, 255, 0), font=f)
            if path and os.path.exists(path):
                with Image.open(path) as im:
                    im = im.convert("RGB").resize((cell_w, cell_h))
                    canvas.paste(im, (x, y + lab_h))
            else:
                draw.text((x + 6, y + lab_h + 10), name, fill=(255, 80, 80), font=f)

    # in 2 Bilder splitten (Lesbarkeit)
    half = (len(grid) + 1) // 2
    outs = []
    for part in range(2):
        rows = range(part * half, min((part + 1) * half, len(grid)))
        if not rows:
            continue
        y0 = part * half * (cell_h + lab_h)
        y1 = min((part + 1) * half, len(grid)) * (cell_h + lab_h)
        crop = canvas.crop((0, y0, W, y1))
        op = os.path.join(OUT_DIR, f"enhance_compare_{part+1}.png")
        crop.save(op)
        outs.append(op)
        print("GESPEICHERT:", op)
    return outs


if __name__ == "__main__":
    main()
