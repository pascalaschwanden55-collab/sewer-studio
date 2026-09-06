from pathlib import Path
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from threading import Thread, Timer
import hashlib, json, shutil

root = Path(__file__).resolve().parent
desktop = Path('C:/Users/Besitzer/Desktop')
project = Path('C:/Sewer-Studio_KI_4.5/docs/reviews/2026-09-06-nova')
names = ['SewerStudio-Nova-Komplett.html','SewerStudio-Nova-Vorschau.html','SewerStudio-Nova-Optimiert.html']
fingerprints = {name:hashlib.sha256((desktop/name).read_bytes()).hexdigest() for name in names}
fingerprints['optimiert_projektkopie'] = hashlib.sha256((project/'optimiert'/names[-1]).read_bytes()).hexdigest()
fingerprints['optimierte_kopien_identisch'] = fingerprints[names[-1]] == fingerprints['optimiert_projektkopie']
previous = json.loads((project/'nachweise/originaldateien.json').read_text(encoding='utf-8'))
fingerprints['urspruengliche_originale_unveraendert'] = all(fingerprints[Path(row['path']).name] == row['sha256'] for row in previous)
(root/'dateipruefung.json').write_text(json.dumps(fingerprints,ensure_ascii=False,indent=2),encoding='utf-8')
shutil.copyfile(desktop/names[-1],root/names[-1])
class Handler(SimpleHTTPRequestHandler):
    def __init__(self,*args,**kwargs): super().__init__(*args,directory=str(root),**kwargs)
    def do_GET(self):
        if self.path == '/__finish':
            self.send_response(200); self.end_headers(); self.wfile.write(b'closed')
            Thread(target=self.server.shutdown,daemon=True).start(); return
        super().do_GET()
server = ThreadingHTTPServer(('127.0.0.1',8805),Handler)
timer = Timer(1800,server.shutdown); timer.start()
print(json.dumps(fingerprints),flush=True)
try: server.serve_forever()
finally: timer.cancel(); server.server_close()
