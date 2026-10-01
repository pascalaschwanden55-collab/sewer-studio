import re,json,pathlib
root=pathlib.Path(__file__).parent
runtime=json.loads((root/'runtime.json').read_text(encoding='utf-8-sig'))
def parse(path):
 s=re.sub(r'!![^\n]*','',path.read_text(encoding='utf-8-sig'))
 classes={}
 for m in re.finditer(r'CLASS\s+(\w+)([^=]*)=(.*?)END\s+\1\s*;',s,re.S):
  attrs={a.group(1):a.group(2).strip() for a in re.finditer(r'^\s*(\w+)\s*(?:\(EXTENDED\))?\s*:\s*(.*?);',m.group(3),re.M|re.S)}
  parent=re.search(r'EXTENDS\s+([\w.]+)',m.group(2))
  classes[m.group(1)]={'parent':parent.group(1).split('.')[-1] if parent else None,'attrs':attrs}
 return classes
def leaves(spec):
 if not spec.lstrip().startswith('('):return None
 tokens=re.findall(r'[A-Za-z_][\w]*|[(),]',spec)
 def group(i,prefix=''):
  found=[]
  while i<len(tokens):
   t=tokens[i];i+=1
   if t==')':return found,i
   if t in ('(', ','):continue
   if i<len(tokens) and tokens[i]=='(':
    sub,i=group(i+1,prefix+t+'.');found+=sub
   else:found.append(prefix+t)
  return found,i
 return group(1)[0]
targets={'Material':'Haltung','Lichte_Hoehe':'Haltung','Laenge':'Haltung','Lagebestimmung':'Haltung','Profiltyp':'Rohrprofil'}
results={}
for filename in ['SIA405_2020.ili','SIA405_2020_1.ili','SIA405_2021.ili']:
 classes=parse(root/filename)
 def attr(cls,key):
  while cls in classes:
   c=classes[cls]
   if key in c['attrs']:return c['attrs'][key]
   cls=c['parent']
  return None
 checks=[]
 for f in runtime['Fields']:
  target=f['ExportTarget']
  if not target or not f['Values']:continue
  cls=targets.get(target,'Kanal')
  spec=attr(cls,target)
  if target=='Status':
   base=re.sub(r'!![^\n]*','',(root/('Base_2020_1.ili' if '_1.' in filename else 'Base_2020.ili')).read_text(encoding='utf-8-sig'))
   spec=re.search(r'\bStatus\s*=\s*(\(.*?\))\s*;',base,re.S).group(1)
  enums=leaves(spec or '')
  exports=[v['Export'] for v in f['Values'] if v['Export']]
  checks.append({'Field':f['Field'],'Target':cls+'.'+target,'Count':len(enums or []),
    'Missing':sorted(set(enums or [])-set(exports)),
    'Invalid':sorted(set(exports)-set(enums)) if enums else [],
    'Unmapped':[v['Display'] for v in f['Values'] if v['Display'] and not v['Export']],
    'ModelSpec':spec})
 for f in runtime['ShaftEnums']:
  enums=leaves(attr('Normschacht',f['Field']) or '')
  exports=[v['Export'] for v in f['Values'] if v['Export']]
  checks.append({'Field':'Schacht.'+f['Field'],'Target':'Normschacht.'+f['Field'],'Count':len(enums or []),
    'Missing':sorted(set(enums or [])-set(exports)),'Invalid':sorted(set(exports)-set(enums or [])),
    'Unmapped':[v['Display'] for v in f['Values'] if v['Display'] and not v['Export']]})
 for key,values in runtime['OtherEnums'].items():
  cls,target=key.split('.')
  enums=leaves(attr(cls,target) or '') or []
  actual=[v for v in values if v]
  checks.append({'Field':key,'Target':key,'Count':len(enums),'Missing':sorted(set(enums)-set(actual)),
     'Invalid':sorted(set(actual)-set(enums)),'Unmapped':[]})
 results[filename]={'Checks':checks,'Classes':classes}
(root/'model-comparison.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
for filename,result in results.items():
 print(filename)
 for c in result['Checks']: print(c['Field'],c['Count'],'FEHLT',c['Missing'],'UNGUELTIG',c['Invalid'],'OHNE ZIEL',c['Unmapped'])
print('Schacht-Formular:',[(x['Field'],x['OptionField']) for x in runtime['ShaftFields']])
