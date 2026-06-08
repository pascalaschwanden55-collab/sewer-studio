"""
Erzeugt eine SELBSTSTAENDIGE HTML-Review-Seite zum Label-Audit: Frame fuer Frame,
mit Bild, Code + Klartext, Modell-Vorhersage, und Knoepfen
[Schaden sichtbar / Leer (Label falsch) / Unsicher] + Notiz.

Bilder sind als Base64 eingebettet -> Datei einfach im Browser oeffnen (Doppelklick).
Entscheidungen werden im Browser gespeichert (localStorage) und koennen als CSV exportiert werden.
READ-ONLY: aendert keine Trainingsdaten.

  python training/vsa_classifier/make_review_html.py
"""
import argparse
import base64
import glob
import io
import os
import re
import sys
from collections import defaultdict

import numpy as np
from PIL import Image
from ultralytics import YOLO

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nocrop_patch import letterbox_pil  # noqa: E402

DEF_WEIGHTS = r"C:\KI_BRAIN\yolo_cls_runs\vsa_cls_v5_nocrop\weights\best.pt"
DEF_SPLIT = r"C:\KI_BRAIN\yolo_vsa_cls_dataset_bal\val"
OUT_HTML = r"C:\tmp\label_audit\label_review.html"
FOCUS = ["BAJ", "BDD", "BAB", "BBA", "BAI", "LEER"]

KLARTEXT = {
    "BAJ": "Verschobene Rohrverbindung (Versatz)",
    "BDD": "Wasserspiegel / Wasserstand",
    "BAB": "Riss",
    "BBA": "Wurzeln / Bewuchs",
    "BAI": "Einragendes Dichtungsmaterial",
    "LEER": "kein Schaden (leeres Rohr)",
    "BCD": "Rohranfang (Einstiegsschacht sichtbar)",
    "BCE": "Rohrende (Zielschacht sichtbar)",
    "BCA": "Seitlicher Anschluss",
    "BCC": "Bogen",
    "BDA": "Abfluss / Wasser (BD-Gruppe)",
    "BAF": "Oberflaechenschaden / Korrosion",
    "BAA": "Verformung",
    "BAC": "Bruch",
    "BAH": "Schadhafter Anschluss",
    "BBB": "Anhaftende Stoffe / Inkrustation",
    "BBC": "Ablagerung",
}
FULLRE = re.compile(r"_[0-9.]+s_(.+?)(_t[+-]\d+)?\.png$", re.I)


def kt(c):
    return KLARTEXT.get(c, c)


def full_code(name):
    m = FULLRE.search(name)
    return m.group(1) if m else "?"


def haltung_time(name):
    m = re.match(r"^(.+?)_([0-9.]+)s_", name)
    return (m.group(1), m.group(2)) if m else (name, "")


def b64_jpeg(path, w=560):
    im = Image.open(path).convert("RGB")
    if im.width > w:
        im = im.resize((w, round(im.height * w / im.width)))
    buf = io.BytesIO()
    im.save(buf, format="JPEG", quality=80)
    return base64.b64encode(buf.getvalue()).decode("ascii")


def collect(model, split, cls, conf_min, cap, imgsz):
    d = os.path.join(split, cls)
    if not os.path.isdir(d):
        return []
    byfind = {}
    for p in glob.glob(os.path.join(d, "*.png")):
        name = os.path.basename(p)
        lb = letterbox_pil(Image.open(p), imgsz)
        arr = np.asarray(lb)[:, :, ::-1]
        res = model.predict(arr, imgsz=imgsz, verbose=False)[0]
        pred = res.names[int(res.probs.top1)]
        conf = float(res.probs.top1conf)
        if pred == cls or conf < conf_min:
            continue
        key = haltung_time(name)
        if key not in byfind or ("_t+0" in name and "_t+0" not in os.path.basename(byfind[key][0])):
            byfind[key] = (p, pred, conf)
    seen, div, rest = set(), [], []
    for (h, _t), v in sorted(byfind.items(), key=lambda x: -x[1][2]):
        (div if h not in seen else rest).append(v)
        seen.add(h)
    return (div + rest)[:cap]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--weights", default=DEF_WEIGHTS)
    ap.add_argument("--split", default=DEF_SPLIT)
    ap.add_argument("--out", default=OUT_HTML)
    ap.add_argument("--imgsz", type=int, default=1024)
    ap.add_argument("--conf-min", type=float, default=0.70)
    ap.add_argument("--per-class", type=int, default=20)
    args = ap.parse_args()
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    model = YOLO(args.weights)

    cards = []
    i = 0
    for cls in FOCUS:
        items = collect(model, args.split, cls, args.conf_min, args.per_class, args.imgsz)
        if not items:
            continue
        cards.append(f'<h2 class="grp">{cls} — {kt(cls)} <small>({len(items)} verdaechtig)</small></h2>')
        for p, pred, conf in items:
            name = os.path.basename(p)
            code = full_code(name)
            b = b64_jpeg(p)
            cards.append(f'''<div class="card" id="f{i}" data-file="{name}" data-code="{code}" data-kt="{kt(cls)}" data-pred="{pred}" data-conf="{conf*100:.0f}">
  <img loading="lazy" src="data:image/jpeg;base64,{b}">
  <div class="info">
    <div class="code"><b>{code}</b> &mdash; {kt(cls)}</div>
    <div class="model">Modell sagt: <b>{pred}</b> ({conf*100:.0f}%) &mdash; {kt(pred)}</div>
    <div class="file">{name}</div>
    <div class="btns">
      <button class="b-ok"  onclick="dec('f{i}','confirm')">&#10003; Schaden sichtbar (Label ok)</button>
      <button class="b-no"  onclick="dec('f{i}','empty')">&#10007; Leer / Label falsch</button>
      <button class="b-un"  onclick="dec('f{i}','unsure')">? Unsicher</button>
    </div>
    <input class="note" placeholder="Notiz (optional)" oninput="note('f{i}',this.value)">
  </div>
</div>''')
            i += 1

    html = """<!DOCTYPE html><html lang="de"><head><meta charset="utf-8">
<title>Label-Audit Review</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;background:#1b1b1b;color:#eee;margin:0;padding:0 0 60px}
header{position:sticky;top:0;background:#111;padding:10px 16px;border-bottom:2px solid #444;z-index:10;display:flex;gap:16px;align-items:center;flex-wrap:wrap}
header h1{font-size:18px;margin:0}
#progress{font-size:14px;color:#9cf}
button.exp{background:#2a6;color:#fff;border:0;padding:8px 14px;border-radius:6px;cursor:pointer;font-size:14px}
.intro{padding:12px 16px;color:#bbb;font-size:14px;max-width:1100px}
h2.grp{padding:8px 16px;margin:18px 0 4px;border-left:5px solid #fc0;background:#222}
h2.grp small{color:#999;font-weight:normal}
.card{display:flex;gap:14px;background:#262626;margin:10px 16px;border:2px solid #333;border-radius:8px;padding:10px;align-items:flex-start}
.card img{width:560px;max-width:48vw;border-radius:4px;background:#000}
.info{flex:1;min-width:240px}
.code{font-size:16px}.model{color:#fc8;margin:6px 0}.file{color:#888;font-size:12px;word-break:break-all;margin-bottom:8px}
.btns{display:flex;gap:8px;flex-wrap:wrap;margin:8px 0}
.btns button{border:0;padding:9px 12px;border-radius:6px;cursor:pointer;font-size:14px}
.b-ok{background:#383}.b-no{background:#933}.b-un{background:#776}
.note{width:90%;padding:6px;border-radius:5px;border:1px solid #444;background:#1b1b1b;color:#eee}
.card.confirm{border-color:#3c3;box-shadow:0 0 0 2px #3c3 inset}
.card.empty{border-color:#f55;box-shadow:0 0 0 2px #f55 inset}
.card.unsure{border-color:#fc0;box-shadow:0 0 0 2px #fc0 inset}
</style></head><body>
<header>
  <h1>Label-Audit &mdash; Frame fuer Frame bestaetigen</h1>
  <span id="progress">0/0</span>
  <button class="exp" onclick="exportCsv()">&#11015; Entscheidungen als CSV exportieren</button>
</header>
<div class="intro">Pro Frame: <b>Code + Klartext</b> = was das Protokoll-Label sagt. <b>Modell sagt</b> = was v5 (hoch-konfident) sieht.
Entscheide mit deinem Fachauge: <b>Schaden sichtbar</b> (Label korrekt) oder <b>Leer / Label falsch</b> (Frame zeigt kein Feature) oder <b>Unsicher</b>.
Deine Entscheidungen bleiben im Browser gespeichert; am Ende per Knopf als CSV exportieren. Nichts an den Trainingsdaten wird geaendert.</div>
__CARDS__
<script>
const KEY='labelReview_v1';
let dec_=JSON.parse(localStorage.getItem(KEY)||'{}');
function save(){localStorage.setItem(KEY,JSON.stringify(dec_));}
function dec(id,val){dec_[id]=Object.assign(dec_[id]||{},{decision:val});save();document.getElementById(id).className='card '+val;prog();}
function note(id,v){dec_[id]=Object.assign(dec_[id]||{},{note:v});save();}
function prog(){
  const cards=document.querySelectorAll('.card');const total=cards.length;
  let done=0,c=0,e=0,u=0;
  cards.forEach(x=>{const d=dec_[x.id];if(d&&d.decision){done++;if(d.decision=='confirm')c++;if(d.decision=='empty')e++;if(d.decision=='unsure')u++;}});
  document.getElementById('progress').textContent=`Geprueft ${done}/${total} — Schaden: ${c}, Leer/falsch: ${e}, Unsicher: ${u}`;
}
function exportCsv(){
  let rows=[['datei','code','klartext_label','modell','konfidenz','entscheidung','notiz']];
  document.querySelectorAll('.card').forEach(x=>{const d=dec_[x.id]||{};
    rows.push([x.dataset.file,x.dataset.code,x.dataset.kt,x.dataset.pred,x.dataset.conf,d.decision||'',(d.note||'').replace(/[;\\n]/g,' ')]);});
  const csv=rows.map(r=>r.map(v=>'"'+String(v).replace(/"/g,'""')+'"').join(';')).join('\\n');
  const blob=new Blob(['\\ufeff'+csv],{type:'text/csv;charset=utf-8'});
  const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download='label_review_entscheidungen.csv';a.click();
}
window.onload=function(){for(const id in dec_){const x=document.getElementById(id);if(!x)continue;if(dec_[id].decision)x.className='card '+dec_[id].decision;const n=x.querySelector('.note');if(n&&dec_[id].note)n.value=dec_[id].note;}prog();};
</script></body></html>"""
    html = html.replace("__CARDS__", "\n".join(cards))
    with open(args.out, "w", encoding="utf-8") as f:
        f.write(html)
    mb = os.path.getsize(args.out) / 1e6
    print(f"HTML: {args.out}  ({i} Frames, {mb:.1f} MB)", flush=True)


if __name__ == "__main__":
    main()
