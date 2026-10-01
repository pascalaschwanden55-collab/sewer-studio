"""Erzeugt den begrenzten Exportvertrag aus den mitgelieferten offiziellen ILI-Dateien.

Kein allgemeiner INTERLIS-Compiler. Unbekannte Typen brechen die Generierung ab.
Die fertige Lieferung wird zusätzlich mit ilivalidator geprüft.
"""
import hashlib
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
MODEL = ROOT / 'src/AuswertungPro.Next.Infrastructure/Import/Xtf/Models/Dss'
OUT = ROOT / 'src/AuswertungPro.Next.Application/Xtf/Dss/ExportSchema.json'
names = ['Kanal', 'Normschacht', 'Spezialbauwerk', 'Versickerungsanlage', 'Einleitstelle',
         'Haltung', 'Haltungspunkt', 'Abwasserknoten', 'Rohrprofil', 'Deckel', 'Einstiegshilfe',
         'Unterhalt', 'Organisation', 'Hydr_Geometrie', 'FoerderAggregat', 'Absperr_Drosselorgan',
         'Leapingwehr', 'Streichwehr', 'Trockenwetterfallrohr', 'ARABauwerk',
         'Abwasserbauwerk_Text', 'Haltung_Text', 'Messstelle']
classes, domains = {}, {}
sources = {}
for file in [MODEL/'Base_d-20181005.ili', MODEL/'Base_2020_1.ili', MODEL/'DSS_2020_1_LV95.ili']:
    sources[file.name] = hashlib.sha256(file.read_bytes()).hexdigest()
    text = re.sub(r'!![^\n]*', '', file.read_text(encoding='utf-8-sig'))
    # Base enthält LV03 und LV95; für den Export gilt ausschliesslich LV95.
    if file.name.startswith('Base_d'): text = text[text.index('MODEL Base_LV95'):]
    for m in re.finditer(r'CLASS\s+(\w+)([^=]*)=(.*?)END\s+\1\s*;', text, re.S):
        parent = re.search(r'EXTENDS\s+([\w.]+)', m[2])
        attrs = {a[1]: a[2].strip() for a in re.finditer(r'^\s*(\w+)\s*(?:\(EXTENDED\))?\s*:\s*(.*?);', m[3], re.M|re.S)}
        classes[m[1]] = (parent[1].split('.')[-1] if parent else None, attrs)
    for part in re.split(r'\bDOMAIN\b', text)[1:]:
        part = re.split(r'\b(?:CLASS|UNIT|TOPIC|ASSOCIATION|STRUCTURE|FUNCTION)\b', part)[0]
        for m in re.finditer(r'^\s*(\w+)(?:\s+EXTENDS\s+([\w.]+))?\s*=\s*(.*?);', part, re.M|re.S):
            spec = m[3].strip()
            if m[2] and spec.startswith('('):
                # ALL OF Statuswerte enthält auch die Wurzelknoten der Erweiterung.
                domains[m[1]] = (spec, domains[m[2].split('.')[-1]])
            else: domains[m[1]] = spec

def enums(spec, all_nodes=False):
    tokens = re.findall(r'[A-Za-z_][\w]*|[(),]', spec)
    def group(i, prefix=''):
        result=[]
        while i < len(tokens):
            t=tokens[i]; i+=1
            if t==')': return result,i
            if t in ('(', ','): continue
            if i<len(tokens) and tokens[i]=='(':
                if all_nodes: result.append(prefix+t)
                sub,i=group(i+1,prefix+t+'.'); result+=sub
            else: result.append(prefix+t)
        return result,i
    return group(1)[0]

def resolve(spec, trail=()):
    required = spec.startswith('MANDATORY ')
    spec = re.sub(r'^MANDATORY\s+', '', spec).strip()
    result = {'Required': required}
    if spec.startswith('('): result.update(Kind='Enum', Values=enums(spec))
    # INTERLIS-2.3-Referenzhandbuch, Anhang A (eingebaute, geordnete Typen):
    # https://www.interlis.ch/modelle/internes-datenmodell
    elif spec in ('HALIGNMENT', 'INTERLIS.HALIGNMENT'):
        result.update(Kind='Enum', Values=['Left', 'Center', 'Right'])
    elif spec in ('VALIGNMENT', 'INTERLIS.VALIGNMENT'):
        result.update(Kind='Enum', Values=['Top', 'Cap', 'Half', 'Base', 'Bottom'])
    elif spec.startswith('ALL OF '):
        v=domains[spec[7:].split('.')[-1]]
        result.update(Kind='Enum',Values=list(dict.fromkeys(enums(v[1], True)+enums(v[0],True))))
    elif re.match(r'M?TEXT(?:\*\d+)?$',spec):
        result.update(Kind='Text', MaxLength=int(spec.split('*')[1]) if '*' in spec else 0)
    elif spec in ('INTERLIS_1_DATE','INTERLIS.INTERLIS_1_DATE'): result['Kind']='Date'
    elif re.match(r'-?\d',spec):
        m=re.match(r'(-?\d+(?:\.\d+)?)\s*\.\.\s*(-?\d+(?:\.\d+)?)',spec)
        if not m: raise ValueError(spec)
        result.update(Kind='Number', Minimum=m[1], Maximum=m[2], Decimals=len(m[1].split('.')[1]) if '.' in m[1] else 0)
    elif re.match(r'(COORD|POLYLINE|SURFACE|AREA)\b',spec): result['Kind']='Structure'
    else:
        key=spec.split('.')[-1]
        if key in trail or key not in domains: raise ValueError(f'Nicht unterstützter Typ {spec}; {trail}')
        result=resolve(domains[key],trail+(key,)) | {'Required':required}
    return result

def attributes(cls):
    parent, own = classes[cls]
    return (attributes(parent) if parent in classes else {}) | own

out={ 'Sources': sources, 'Classes': {n:{k:resolve(v) for k,v in attributes(n).items()} for n in names} }
OUT.parent.mkdir(parents=True,exist_ok=True)
OUT.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'{len(names)} Klassen, {sum(len(v) for v in out["Classes"].values())} Felddefinitionen')
