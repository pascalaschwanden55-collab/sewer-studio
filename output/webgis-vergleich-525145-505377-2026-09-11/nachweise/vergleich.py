from pathlib import Path
import json,xml.etree.ElementTree as E,hashlib,time
out=Path('.tmp/webgis-vergleich-525145');p=Path('D:/Projekte/Sanierungsabnahme_Zone_5.01_GKS_Bürglen/Projektdateien/projekt.json')
j=json.loads(p.read_text(encoding='utf-8-sig'));h=next(x for x in j['Data'] if x['Fields'].get('Haltungsname')=='525145-505377')
(out/'sewer-gespeichert.json').write_text(json.dumps({'Projekt':j['Name'],'ProjektVersion':j['Version'],'ModifiedAtUtc':j['ModifiedAtUtc'],'ObjektaktenAnzahl':len(j.get('Objektakten',[])),'Haltung':{k:h.get(k) for k in ['Id','Fields','FieldMeta','Geonis','XtfHerkunft','ImportBezeichnung']},'Akten':[a for a in j.get('Objektakten',[]) if a.get('Id')==h['Id'] or h['Id'] in a.get('Bezuege',[])]},ensure_ascii=False,indent=2),encoding='utf-8')
quelle=Path('D:/QGIS_V4.2/GeoShop/2026-09/34UR_Abwasser_DSS_2020_1.xtf')
def local(tag):return tag.split('}')[-1]
needed={h['Geonis'][k] for k in ['Haltung','Kanal','VonPunkt','NachPunkt','Rohrprofil']};objs={};seednames={'525145','505377','10.527526'}
for n in range(4):
 before=len(objs);depth=0
 for ev,e in E.iterparse(quelle,events=('start','end')):
  if ev=='start':depth+=1;continue
  if depth==4:
   tid=e.get('TID');vals={local(c.tag):(c.text or '').strip() for c in e if len(c)==0 and not c.attrib}
   if tid and (tid in needed or vals.get('Bezeichnung') in seednames):
    refs={local(c.tag):c.attrib['REF'] for c in e if 'REF' in c.attrib}
    objs[tid]={'Klasse':local(e.tag),'TID':tid,'Werte':vals,'Referenzen':refs,'XML':E.tostring(e,encoding='unicode')}
    needed.update(refs.values())
   e.clear()
  depth-=1
 print('Durchlauf',n+1,'Objekte',len(objs),flush=True)
 if len(objs)==before:break
result={'Quelle':str(quelle),'Bytes':quelle.stat().st_size,'SHA256':hashlib.file_digest(quelle.open('rb'),'sha256').hexdigest(),'Objekte':list(objs.values()),'NichtAufgeloesteReferenzen':sorted(needed-set(objs))}
(out/'xtf-verbund.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
for x in objs.values():print(json.dumps({k:v for k,v in x.items() if k!='XML'},ensure_ascii=False))
print('Nicht aufgeloest:',result['NichtAufgeloesteReferenzen'])
