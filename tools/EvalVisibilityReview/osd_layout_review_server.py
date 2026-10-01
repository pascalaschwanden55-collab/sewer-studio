"""Lokaler Pruefplatz fuer Ort und sichtbaren Stil der OSD-Meteranzeige."""

from __future__ import annotations

import argparse
import hashlib
import json
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Sequence
from urllib.parse import parse_qs, urlparse

try:
    from .review_server_security import read_json_body, require_loopback_host
except ImportError:  # Direkter Skriptstart aus diesem Ordner
    from review_server_security import read_json_body, require_loopback_host

POLARITAETEN = ("hell_auf_dunkel", "dunkel_auf_hell", "andere")
FARBEN = ("weiss_grau", "gelb", "andere")
FORMATE = ("zahl_mit_einheit", "zahl_ohne_einheit", "praefix_oder_nullen", "andere")


def sha256_datei(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


class OsdLayoutReviewStore:
    def __init__(self, queue_root: Path, output: Path, reviewer: str) -> None:
        if not reviewer.strip():
            raise ValueError("Ein Pruefername ist Pflicht.")
        self.queue_root = Path(queue_root)
        self.output = Path(output)
        self.reviewer = reviewer.strip()
        self._lock = threading.Lock()
        queue_path = self.queue_root / "queue.json"
        if not queue_path.is_file():
            raise SystemExit(f"Queue fehlt: {queue_path}")
        self.queue_sha256 = sha256_datei(queue_path)
        self.queue = json.loads(queue_path.read_text(encoding="utf-8-sig"))
        self.faelle = list(self.queue.get("faelle") or [])
        if not self.faelle:
            raise SystemExit("Queue enthaelt keine Bilder.")
        for fall in self.faelle:
            bild = self.queue_root / "bilder" / str(fall.get("bild") or "")
            if not bild.is_file() or sha256_datei(bild) != fall.get("bild_sha256"):
                raise SystemExit(f"Bild fehlt oder wurde veraendert: {fall.get('fall_id', '?')}")
        self.entscheidungen: dict[str, dict] = {}
        self._laden()

    def _laden(self) -> None:
        if not self.output.is_file():
            return
        daten = json.loads(self.output.read_text(encoding="utf-8-sig"))
        if daten.get("queue_sha256") != self.queue_sha256:
            raise SystemExit("Vorhandene Review gehoert zu einer anderen Queue.")
        entscheidungen = dict(daten.get("entscheidungen") or {})
        erlaubte_ids = {str(f["fall_id"]) for f in self.faelle}
        if set(entscheidungen) - erlaubte_ids:
            raise SystemExit("Vorhandene Review enthaelt unbekannte Faelle.")
        self.entscheidungen = entscheidungen

    def _speichern(self) -> None:
        daten = {
            "schema": "osd_layout_review_v1",
            "reviewer": self.reviewer,
            "queue_sha256": self.queue_sha256,
            "gesamt": len(self.faelle),
            "entscheidungen": self.entscheidungen,
        }
        self.output.parent.mkdir(parents=True, exist_ok=True)
        temp = self.output.with_suffix(self.output.suffix + ".tmp")
        temp.write_text(json.dumps(daten, indent=2, ensure_ascii=False), encoding="utf-8")
        temp.replace(self.output)

    def stand(self) -> dict:
        offen = [f for f in self.faelle if f["fall_id"] not in self.entscheidungen]
        ohne_meter = sum(1 for e in self.entscheidungen.values() if not e["meter_sichtbar"])
        return {"gesamt": len(self.faelle), "offen": len(offen),
                "naechster": offen[0] if offen else None, "ohne_meter": ohne_meter}

    def entscheiden(self, fall_id: str, meter_sichtbar: bool, x: object = None,
                    y: object = None, polaritaet: str = "", farbe: str = "",
                    format_name: str = "") -> dict:
        fall = next((f for f in self.faelle if f["fall_id"] == fall_id), None)
        if fall is None:
            raise ValueError(f"Unbekannter Fall: {fall_id}")
        if meter_sichtbar:
            try:
                x_wert, y_wert = float(x), float(y)
            except (TypeError, ValueError) as fehler:
                raise ValueError("Bitte direkt auf den Meterstand im Bild klicken.") from fehler
            if not (0 <= x_wert <= 1 and 0 <= y_wert <= 1):
                raise ValueError("Der Klickpunkt liegt ausserhalb des Bildes.")
            if polaritaet not in POLARITAETEN or farbe not in FARBEN or format_name not in FORMATE:
                raise ValueError("Bitte Polaritaet, Farbe und Schreibweise auswaehlen.")
        else:
            x_wert = y_wert = None
            polaritaet = farbe = format_name = "nicht_anwendbar"
        with self._lock:
            self.entscheidungen[fall_id] = {
                "haltung": fall["haltung"],
                "meter_sichtbar": bool(meter_sichtbar),
                "x": None if x_wert is None else round(x_wert, 5),
                "y": None if y_wert is None else round(y_wert, 5),
                "polaritaet": polaritaet,
                "farbe": farbe,
                "format": format_name,
            }
            self._speichern()
        return self.stand()

    def zuruecknehmen(self) -> dict:
        with self._lock:
            erledigt = [f for f in self.faelle if f["fall_id"] in self.entscheidungen]
            if erledigt:
                self.entscheidungen.pop(erledigt[-1]["fall_id"], None)
                self._speichern()
        return self.stand()

    def bild_pfad(self, fall_id: str) -> Path | None:
        fall = next((f for f in self.faelle if f["fall_id"] == fall_id), None)
        if fall is None:
            return None
        wurzel = (self.queue_root / "bilder").resolve()
        bild = (wurzel / fall["bild"]).resolve()
        return bild if wurzel in bild.parents and bild.is_file() else None


SEITE = """<!doctype html><meta charset="utf-8"><title>OSD-Anordnung sichten</title>
<style>
body{background:#14161a;color:#e8eaed;font-family:Segoe UI,sans-serif;margin:0;padding:16px}
h1{font-size:19px;margin:0 0 4px}.sub,.stand{color:#9aa0a6;font-size:13px}
.rahmen{position:relative;display:inline-block;margin-top:12px;background:#000;line-height:0}
img{display:block;max-width:96vw;max-height:68vh}.marker{position:absolute;width:18px;height:18px;
border:3px solid #ff3b30;border-radius:50%;transform:translate(-50%,-50%);pointer-events:none}
.felder,.buttons{display:flex;gap:10px;flex-wrap:wrap;margin-top:12px;align-items:center}
select,button{background:#252a31;color:#fff;border:1px solid #444b55;border-radius:6px;padding:10px;font-size:14px}
button{border:0;cursor:pointer}.ok{background:#2e7d32}.nein{background:#8e2f2f}.zurueck{background:#454b54}
</style><div id="app">Lade...</div><script>
let aktuell=null,x=null,y=null;
async function laden(){const s=await (await fetch('/stand')).json(),a=document.getElementById('app');
if(!s.naechster){a.innerHTML='<h1>Alle '+s.gesamt+' Bilder eingeordnet.</h1><div class="stand">Ohne sichtbaren Meterstand: '+s.ohne_meter+'</div>';return;}
aktuell=s.naechster;x=null;y=null;const nr=s.gesamt-s.offen+1;
a.innerHTML='<h1>Wo steht der Meterstand?</h1><div class="sub">Bild '+nr+' von '+s.gesamt+' · Haltung '+aktuell.haltung+' · direkt auf die Meteranzeige klicken</div>'+
'<div class="rahmen" id="rahmen"><img id="bild" src="/bild?id='+aktuell.fall_id+'"><div id="marker" class="marker" hidden></div></div>'+
'<div class="felder"><select id="pol"><option value="">Polaritaet...</option><option value="hell_auf_dunkel">hell auf dunkel</option><option value="dunkel_auf_hell">dunkel auf hell</option><option value="andere">andere</option></select>'+
'<select id="farbe"><option value="">Farbe...</option><option value="weiss_grau">weiss/grau</option><option value="gelb">gelb</option><option value="andere">andere</option></select>'+
'<select id="format"><option value="">Schreibweise...</option><option value="zahl_mit_einheit">Zahl mit m</option><option value="zahl_ohne_einheit">Zahl ohne m</option><option value="praefix_oder_nullen">Praefix/fuehrende Nullen</option><option value="andere">andere</option></select></div>'+
'<div class="buttons"><button class="ok" onclick="speichern(true)">uebernehmen</button><button class="nein" onclick="speichern(false)">kein Meter sichtbar</button><button class="zurueck" onclick="zurueck()">zurueck</button></div>'+
'<div class="stand">'+(s.gesamt-s.offen)+' erledigt · '+s.offen+' offen</div>';
document.getElementById('rahmen').onclick=e=>{const r=document.getElementById('bild').getBoundingClientRect();x=(e.clientX-r.left)/r.width;y=(e.clientY-r.top)/r.height;const m=document.getElementById('marker');m.style.left=(x*100)+'%';m.style.top=(y*100)+'%';m.hidden=false;};}
async function speichern(sichtbar){const body={fall_id:aktuell.fall_id,meter_sichtbar:sichtbar,x:x,y:y,polaritaet:document.getElementById('pol').value,farbe:document.getElementById('farbe').value,format:document.getElementById('format').value};const r=await fetch('/entscheiden',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});if(!r.ok){alert((await r.json()).fehler);return;}laden();}
async function zurueck(){await fetch('/zurueck',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'});laden();}laden();</script>"""


def create_server(store: OsdLayoutReviewStore, port: int) -> ThreadingHTTPServer:
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_args) -> None:
            pass

        def _json(self, daten: dict, status: int = 200) -> None:
            roh = json.dumps(daten, ensure_ascii=False).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(roh)))
            self.end_headers()
            self.wfile.write(roh)

        def do_GET(self) -> None:  # noqa: N802
            if not require_loopback_host(self):
                return
            weg = urlparse(self.path)
            if weg.path == "/":
                roh = SEITE.encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Cache-Control", "no-store")
                self.send_header("Content-Length", str(len(roh)))
                self.end_headers()
                self.wfile.write(roh)
                return
            if weg.path == "/stand":
                self._json(store.stand())
                return
            if weg.path == "/bild":
                fall_id = (parse_qs(weg.query).get("id") or [""])[0]
                bild = store.bild_pfad(fall_id)
                if bild is None:
                    self._json({"fehler": "Bild fehlt"}, 404)
                    return
                daten = bild.read_bytes()
                self.send_response(200)
                self.send_header("Content-Type", "image/jpeg")
                self.send_header("Cache-Control", "no-store")
                self.send_header("Content-Length", str(len(daten)))
                self.end_headers()
                self.wfile.write(daten)
                return
            self._json({"fehler": "unbekannt"}, 404)

        def do_POST(self) -> None:  # noqa: N802
            if not require_loopback_host(self):
                return
            body = read_json_body(self)
            if body is None:
                return
            weg = urlparse(self.path).path
            if weg == "/zurueck":
                self._json(store.zuruecknehmen())
                return
            if weg != "/entscheiden":
                self._json({"fehler": "unbekannt"}, 404)
                return
            anfrage = json.loads(body or b"{}")
            try:
                self._json(store.entscheiden(
                    str(anfrage.get("fall_id") or ""), bool(anfrage.get("meter_sichtbar")),
                    anfrage.get("x"), anfrage.get("y"), str(anfrage.get("polaritaet") or ""),
                    str(anfrage.get("farbe") or ""), str(anfrage.get("format") or "")))
            except ValueError as fehler:
                self._json({"fehler": str(fehler)}, 400)

    return ThreadingHTTPServer(("127.0.0.1", port), Handler)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--queue", type=Path, default=Path(r"C:\KI_BRAIN\training\diagnostics\osd_layout_review_v1"))
    parser.add_argument("--output", type=Path, default=Path(r"C:\KI_BRAIN\eval_review\osd_layout_review_v1.json"))
    parser.add_argument("--reviewer", default="Pascal")
    parser.add_argument("--port", type=int, default=18912)
    args = parser.parse_args(argv)
    store = OsdLayoutReviewStore(args.queue, args.output, args.reviewer)
    server = create_server(store, args.port)
    print(f"Pruefplatz: http://127.0.0.1:{server.server_address[1]}/")
    print(f"Bilder: {store.stand()['gesamt']}, offen: {store.stand()['offen']}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
