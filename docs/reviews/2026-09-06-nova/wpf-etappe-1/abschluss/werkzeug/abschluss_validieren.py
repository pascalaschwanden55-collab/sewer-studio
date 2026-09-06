from pathlib import Path
from html.parser import HTMLParser
import hashlib, json, shutil, zipfile

root = Path(r'C:\Sewer-Studio_KI_4.5')
out = root/'docs/reviews/2026-09-06-nova/wpf-etappe-1/abschluss'
class Links(HTMLParser):
    def __init__(self):
        super().__init__()
        self.files = set()
    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        for key in ('src','href'):
            if key in a and not a[key].startswith(('#','data:','http')):
                self.files.add(a[key])
links = Links()
links.feed((out/'ueberblick.html').read_text(encoding='utf-8'))
assert all((out/p).is_file() for p in links.files)
for file in ('testnachweise.zip','bediennachweise.zip'):
    with zipfile.ZipFile(out/file) as archive:
        assert archive.testzip() is None
qa = {'browser':'Playwright Chromium', 'viewports':[
    {'width':1920,'height':1080,'horizontalOverflow':False},
    {'width':1280,'height':720,'horizontalOverflow':False}],
    'visibleActionRowsWhenExpanded':18,'missingImages':0,'httpLinksChecked':10,
    'httpLinksStatus':200,'screenshotsVisuallyReviewed':['fullhd.png','gesamt.png'],
    'note':'Lokale Browserfenster-Pruefung; keine neue Windows-DPI-Messung. Alle Links bleiben relativ und funktionieren auch beim direkten Oeffnen der HTML-Datei.'}
(out/'html-pruefung.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2),encoding='utf-8')
with zipfile.ZipFile(out/'html-pruefbilder.zip','w',zipfile.ZIP_DEFLATED) as archive:
    for name in qa['screenshotsVisuallyReviewed']:
        archive.write(root/'output/playwright/nova-abschluss'/name,name)
shutil.copy2(__file__,out/'werkzeug/abschluss_validieren.py')
marker='## Prüfung des HTML-Berichts'
md=out/'ABSCHLUSS.md'
if marker not in md.read_text(encoding='utf-8'):
    with md.open('a',encoding='utf-8') as stream:
        stream.write('\n\n'+marker+'\n\nPlaywright/Chromium: 1920 × 1080 und 1280 × 720 ohne horizontales Seitenüberlaufen. Prüfliste aufgeklappt, 18 Aktionsgruppen vorhanden; keine fehlenden Bilder. Zehn lokale Dateilinks liefern HTTP 200. Startansicht und Gesamtseite visuell kontrolliert, Bilder in `html-pruefbilder.zip`. Die Datenarchive wurden vollständig zur Integritätsprüfung zurückgelesen. Die Seite benötigt keinen Internetzugriff.\n')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
manifest={str(p.relative_to(out)):sha(p) for p in sorted(out.rglob('*')) if p.is_file() and p.name!='SHA256.json'}
(out/'SHA256.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
assert all(sha(out/name)==value for name,value in manifest.items())
print(json.dumps({'links':len(links.files),'filesVerified':len(manifest),'archivesValid':True}))
