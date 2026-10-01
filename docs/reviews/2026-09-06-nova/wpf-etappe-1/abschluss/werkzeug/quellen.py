import sqlite3, pathlib, hashlib, json
root = pathlib.Path(r'C:\Sewer-Studio_KI_4.5\.tmp\nova-abschluss\bedienung\quellen')
root.mkdir(parents=True, exist_ok=True)
def write(name, sql, inserts):
    path = root/name
    if path.exists():
        return
    with sqlite3.connect(path) as db:
        db.executescript(sql)
        for statement, rows in inserts:
            db.executemany(statement, rows)
write('leitungen.gpkg', '''
CREATE TABLE gpkg_contents (table_name TEXT, data_type TEXT);
INSERT INTO gpkg_contents VALUES ('leitungen','features');
CREATE TABLE leitungen (ne_bezeichnung TEXT, ha_material TEXT, ha_lichte_hoehe TEXT);
''', [('INSERT INTO leitungen VALUES (?,?,?)', [(f'{10001+i}-{10002+i}', 'Steinzeug', '400') for i in range(14)])])
write('schaechte.gpkg', '''
CREATE TABLE gpkg_contents (table_name TEXT, data_type TEXT);
INSERT INTO gpkg_contents VALUES ('schaechte','features');
CREATE TABLE schaechte (bw_bezeichnung TEXT, ns_funktion TEXT, ns_material TEXT);
''', [('INSERT INTO schaechte VALUES (?,?,?)', [(str(10001+i),'Kontroll_Einsteigschacht','Beton') for i in range(15)])])
write('kennungen.gpkg', '''
CREATE TABLE herkunft (schluessel TEXT PRIMARY KEY, wert TEXT);
INSERT INTO herkunft VALUES ('stand','2024-12');
CREATE TABLE haltungen (bezeichnung TEXT, gemeinde TEXT, haltung_id TEXT, kanal_id TEXT,
vonpunkt_id TEXT, vonpunkt_bezeichnung TEXT, nachpunkt_id TEXT, nachpunkt_bezeichnung TEXT,
rohrprofil_id TEXT, profiltyp_code INTEGER, geonis_geaendert TEXT);
CREATE TABLE schaechte (bezeichnung TEXT, gemeinde TEXT, knoten_id TEXT, bauwerk_id TEXT, geonis_geaendert TEXT);
''', [('INSERT INTO haltungen VALUES (?,?,?,?,?,?,?,?,?,?,?)', [
    (f'{10001+i}-{10002+i}', 'Musterdorf', f'chTESThaltung{i:03}', f'chTESTkanal{i:03}',
     f'chTESTpunkt{i:03}',str(10001+i),f'chTESTpunkt{i+1:03}',str(10002+i),f'chTESTprofil{i:03}',0,'2024/12/01 00:00:00+00')
    for i in range(14)])])
manifest = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in root.glob('*.gpkg')}
(root/'original-hashes.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(manifest))
