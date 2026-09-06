import csv, hashlib, json
from collections import defaultdict, Counter
from pathlib import Path
import xml.etree.ElementTree as ET

root=Path('C:/Sewer-Studio_KI_4.5')
audit=root/'.tmp/programmaudit-2026-09-06'
inv=audit/'inventory'
lines=defaultdict(dict)
raw_hit=raw_total=0
reports=list((audit/'coverage').rglob('*.cobertura.xml'))
for report in reports:
    tree=ET.parse(report).getroot()
    raw_hit+=int(tree.get('lines-covered',0)); raw_total+=int(tree.get('lines-valid',0))
    for cls in tree.findall('./packages/package/classes/class'):
        path=Path(cls.get('filename',''))
        try: rel=path.relative_to(root).as_posix()
        except ValueError: continue
        if not rel.startswith(('src/','tools/')) or any(x in path.parts for x in ['bin','obj','.tmp']): continue
        for ln in cls.findall('./lines/line'):
            n=int(ln.get('number')); hit=int(ln.get('hits'))>0
            lines[rel][n]=lines[rel].get(n,False) or hit
stats={}
for prefix in ['src/AuswertungPro.Next.Domain/','src/AuswertungPro.Next.Application/','src/AuswertungPro.Next.Infrastructure/','src/AuswertungPro.Next.UI/','src/','tools/']:
    selected=[v for k,d in lines.items() if k.startswith(prefix) for v in d.values()]
    stats[prefix]={'covered':sum(selected),'measured':len(selected),'percent':round(100*sum(selected)/len(selected),2) if selected else None,'files':sum(k.startswith(prefix) for k in lines)}
rows=json.loads((inv/'csharp-functions.json').read_text('utf-8-sig'))
out=[]
for row in rows:
    if not row['file'].startswith('src/'): continue
    measure=lines.get(row['file'],{})
    values=[hit for ln,hit in measure.items() if row['line']<=ln<row['line']+row['lines']]
    status='ohne messbare Zeile' if not values else 'nicht ausgeführt' if not any(values) else 'teilweise ausgeführt' if not all(values) else 'alle messbaren Zeilen ausgeführt'
    out.append({**row,'coverage_status':status,'measured_lines':len(values),'covered_lines':sum(values)})
with (inv/'functions-coverage.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=out[0].keys());writer.writeheader();writer.writerows(out)
changed=[]
for manifest in ['csharp-files.json','python-files.json']:
    for file in json.loads((inv/manifest).read_text('utf-8-sig')):
        path=root/file['file']
        if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest()!=file['sha256']:
            changed.append(file['file'])
summary={'reports':len(reports),'raw_ci_percent':round(raw_hit/raw_total*100,2),'raw_ci_covered':raw_hit,'raw_ci_total':raw_total,'deduplicated_production':stats,'src_declarations':len(out),'declaration_line_overlap_status':dict(Counter(r['coverage_status'] for r in out)),'changed_since_inventory':changed,'caveat':'Zeilenabdeckung ist kein fachlicher Vollständigkeitsnachweis. Verschachtelte Deklarationen können dieselben Zeilen enthalten. Nicht geladene Module fehlen im Messnenner. Wiederholungsläufe nicht instrumentiert.'}
(inv/'coverage-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),'utf-8')
print(json.dumps(summary,ensure_ascii=False,indent=2))
