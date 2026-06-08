"""Magnifizierte Ausschnitte (zentral) zum Pruefen, ob feine Risse/Texturen die Filter ueberleben."""
import os
import re
from PIL import Image, ImageDraw, ImageFont

EVAL_IMG = r"C:\Sewer-Studio_KI_4.4\EvalVisibilityReview_20260525\eval_visible_clean_eval_set\images"
OUT = r"C:\tmp\enhance_preview"


def code_of(n):
    m = re.match(r"^.+?_[0-9.]+s_(.+?)(_t[+-]\d+)?\.png$", n, re.I)
    return m.group(1) if m else None


allpng = sorted(os.listdir(EVAL_IMG))


def find(code):
    for n in allpng:
        if code_of(n) == code:
            return os.path.join(EVAL_IMG, n)
    return None


def fnt(sz):
    try:
        return ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", sz)
    except Exception:
        return ImageFont.load_default()


def zoom(im, frac=0.42, mag=2.6):
    w, h = im.size
    cw, ch = int(w * frac), int(h * frac)
    x0, y0 = (w - cw) // 2, (h - ch) // 2
    return im.crop((x0, y0, x0 + cw, y0 + ch)).resize((int(cw * mag), int(ch * mag)), Image.LANCZOS)


font = fnt(22)
targets = [("BAIZ", 2, "Dichtung"), ("BABBA", 5, "Laengsriss")]
for code, i, desc in targets:
    variants = [
        ("Original", find(code)),
        ("bilateral_cuda", os.path.join(OUT, f"f{i}_bil.png")),
        ("hqdn3d", os.path.join(OUT, f"f{i}_hq.png")),
        ("nlmeans", os.path.join(OUT, f"f{i}_nlm.png")),
    ]
    zs = [(name, zoom(Image.open(p).convert("RGB"))) for name, p in variants if p and os.path.exists(p)]
    cw, ch = zs[0][1].size
    lab = 30
    canvas = Image.new("RGB", (cw * len(zs), ch + lab), (15, 15, 15))
    d = ImageDraw.Draw(canvas)
    for c, (name, im) in enumerate(zs):
        x = c * cw
        d.rectangle([x, 0, x + cw, lab], fill=(0, 0, 0))
        d.text((x + 6, 5), f"{code} ({desc}) | {name}", fill=(255, 255, 0), font=font)
        canvas.paste(im, (x, lab))
    op = os.path.join(OUT, f"zoom_{code}.png")
    canvas.save(op)
    print("GESPEICHERT:", op)
