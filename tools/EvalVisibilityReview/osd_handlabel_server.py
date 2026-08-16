"""Lokaler Pruefplatz fuer die harte OSD-Handliste (osd_handlabel.py queue).

Zeigt ein Bild mit den bereits gefundenen Zeichenboxen. Der Mensch tippt die
Zeichenfolge von links nach rechts - eine Box pro Zeichen. Es wird NIE eine
Modell- oder Vorlagenlesung angezeigt: Genau die Beeinflussung wuerde den
ganzen Zweck der Handliste zerstoeren (siehe osd_handlabel.py-Modul-Docstring).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Sequence
from urllib.parse import parse_qs, urlparse

from PIL import Image

WURZEL = Path(__file__).resolve().parents[2]
if str(WURZEL / "sidecar") not in sys.path:
    sys.path.insert(0, str(WURZEL / "sidecar"))

# Zeichensatz wird LIVE aus osd_meter gelesen (keine eigene Kopie - ein
# spaeteres Erweitern von ZEICHEN muesste sonst zwei Stellen pflegen).
from sidecar import osd_meter  # noqa: E402

AKTIONEN = ("uebernommen", "unleserlich", "boxen_passen_nicht")


def sha256_datei(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


def _content_type(pfad: Path) -> str:
    return "image/png" if pfad.suffix.lower() == ".png" else "image/jpeg"


class OsdHandlabelStore:
    """Liest queue.json, schreibt/laedt die Review atomar und revisionssicher.

    Die Revision schuetzt gegen zwei gleichzeitig offene Tabs/Prozesse: jede
    Antwort auf /stand traegt die aktuelle Revision, jede Entscheidung muss
    sie zurueckschicken. Weicht sie ab, hat ein anderer Tab zwischenzeitlich
    gespeichert - abgewiesen statt still ueberschrieben.
    """

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
            raise SystemExit("Queue enthaelt keine Faelle.")

        self._bildgroessen: dict[str, tuple[int, int]] = {}
        for fall in self.faelle:
            fall_id = str(fall.get("id") or "")
            bild_pfad = Path(str(fall.get("bild_pfad") or ""))
            if not bild_pfad.is_file() or sha256_datei(bild_pfad) != fall.get("bild_sha256"):
                raise SystemExit(f"Bild fehlt oder wurde veraendert: {fall_id or '?'}")
            with Image.open(bild_pfad) as bild:
                self._bildgroessen[fall_id] = bild.size

        self.entscheidungen: dict[str, dict] = {}
        self._revision = 0
        self._laden()

    def _laden(self) -> None:
        if not self.output.is_file():
            return
        daten = json.loads(self.output.read_text(encoding="utf-8-sig"))
        if daten.get("queue_sha256") != self.queue_sha256:
            raise SystemExit("Vorhandene Review gehoert zu einer anderen Queue.")
        entscheidungen = dict(daten.get("entscheidungen") or {})
        erlaubte_ids = {str(f["id"]) for f in self.faelle}
        if set(entscheidungen) - erlaubte_ids:
            raise SystemExit("Vorhandene Review enthaelt unbekannte Faelle.")
        self.entscheidungen = entscheidungen
        self._revision = len(entscheidungen)

    def _speichern(self) -> None:
        daten = {
            "schema": "osd_handlabel_review_v1",
            "reviewer": self.reviewer,
            "queue_sha256": self.queue_sha256,
            "gesamt": len(self.faelle),
            "entscheidungen": self.entscheidungen,
        }
        self.output.parent.mkdir(parents=True, exist_ok=True)
        temp = self.output.with_suffix(self.output.suffix + ".tmp")
        temp.write_text(json.dumps(daten, indent=2, ensure_ascii=False), encoding="utf-8")
        temp.replace(self.output)

    def _fall(self, fall_id: str) -> dict | None:
        return next((f for f in self.faelle if str(f["id"]) == fall_id), None)

    def stand(self) -> dict:
        offen = [f for f in self.faelle if str(f["id"]) not in self.entscheidungen]
        naechster = None
        if offen:
            fall = offen[0]
            fall_id = str(fall["id"])
            breite, hoehe = self._bildgroessen[fall_id]
            boxen_anteil = [
                [box[0] / breite, box[1] / hoehe,
                 (box[2] - box[0]) / breite, (box[3] - box[1]) / hoehe]
                for box in fall["boxen"]
            ]
            naechster = {
                "id": fall_id,
                "haltung": fall.get("haltung"),
                "anzahl_boxen": len(fall["boxen"]),
                "boxen_anteil": boxen_anteil,
            }
        return {
            "gesamt": len(self.faelle),
            "offen": len(offen),
            "revision": self._revision,
            "naechster": naechster,
        }

    def entscheiden(self, fall_id: str, aktion: str, zeichenfolge: str,
                    erwartete_revision: int) -> dict:
        if erwartete_revision != self._revision:
            raise ValueError(
                "Ein anderer Tab oder Prozess hat zwischenzeitlich gespeichert "
                "- bitte die Seite neu laden.")
        fall = self._fall(fall_id)
        if fall is None:
            raise ValueError(f"Unbekannter Fall: {fall_id}")
        if fall_id in self.entscheidungen:
            raise ValueError("Dieser Fall wurde bereits entschieden.")
        if aktion not in AKTIONEN:
            raise ValueError(f"Unbekannte Aktion: {aktion!r}")

        if aktion == "uebernommen":
            text = (zeichenfolge or "").replace(" ", "")
            boxen = fall["boxen"]
            if len(text) != len(boxen):
                raise ValueError(
                    f"{len(text)} Zeichen fuer {len(boxen)} Boxen - die Anzahl "
                    "muss genau uebereinstimmen. Bitte korrigieren oder "
                    "'boxen passen nicht' waehlen.")
            unbekannt = sorted(set(text) - set(osd_meter.ZEICHEN))
            if unbekannt:
                raise ValueError(
                    f"Zeichen nicht erlaubt: {unbekannt[0]!r} (erlaubt: "
                    f"{osd_meter.ZEICHEN!r}).")
            eintrag = {"aktion": "uebernommen", "zeichenfolge": text}
        else:
            eintrag = {"aktion": aktion}

        with self._lock:
            self.entscheidungen[fall_id] = eintrag
            self._revision += 1
            self._speichern()
        return self.stand()

    def bild_pfad(self, fall_id: str) -> Path | None:
        fall = self._fall(fall_id)
        if fall is None:
            return None
        pfad = Path(str(fall.get("bild_pfad") or ""))
        return pfad if pfad.is_file() else None


SEITE = """<!doctype html><meta charset="utf-8"><title>OSD-Handliste</title>
<style>
body{background:#14161a;color:#e8eaed;font-family:Segoe UI,sans-serif;margin:0;padding:16px}
h1{font-size:19px;margin:0 0 4px}.sub,.stand{color:#9aa0a6;font-size:13px}
.rahmen{position:relative;display:inline-block;margin-top:12px;background:#000;line-height:0}
img{display:block;max-width:96vw;max-height:62vh}
.box{position:absolute;border:2px solid #ff3b30;pointer-events:none;box-sizing:border-box}
.zeile{display:flex;gap:10px;flex-wrap:wrap;margin-top:14px;align-items:center}
input{background:#20242a;border:1px solid #3a4048;color:#e8eaed;border-radius:5px;
      padding:12px 14px;font-size:20px;width:240px}
button{border:0;border-radius:6px;padding:11px 16px;font-size:14px;cursor:pointer;color:#fff}
.ok{background:#2e7d32}.frage{background:#8a6d1f}.nein{background:#8e2f2f}
.fehler{color:#ff6b6b;margin-top:8px;font-size:13px}
</style><div id="app">Lade...</div><script>
let aktuell=null, revision=0;
async function laden(){
  const s=await (await fetch('/stand')).json();
  revision=s.revision;
  const a=document.getElementById('app');
  if(!s.naechster){a.innerHTML='<h1>Alle '+s.gesamt+' Faelle entschieden.</h1>';return;}
  aktuell=s.naechster;
  const nr=s.gesamt-s.offen+1;
  let boxen='';
  for(const b of aktuell.boxen_anteil){
    boxen+='<div class="box" style="left:'+(b[0]*100)+'%;top:'+(b[1]*100)+'%;width:'+(b[2]*100)+'%;height:'+(b[3]*100)+'%"></div>';
  }
  a.innerHTML='<h1>Welche Zeichen stehen in den roten Boxen?</h1>'
    +'<div class="sub">Fall '+nr+' von '+s.gesamt+' &middot; Haltung '+(aktuell.haltung||'?')
    +' &middot; '+aktuell.anzahl_boxen+' Boxen, von links nach rechts eintippen</div>'
    +'<div class="rahmen"><img id="bild" src="/bild?id='+aktuell.id+'">'+boxen+'</div>'
    +'<div class="zeile"><input id="wert" placeholder="von links nach rechts" autocomplete="off">'
    +'<button class="ok" onclick="senden(\\'uebernommen\\')">uebernehmen</button>'
    +'<button class="frage" onclick="senden(\\'unleserlich\\')">unleserlich</button>'
    +'<button class="nein" onclick="senden(\\'boxen_passen_nicht\\')">boxen passen nicht</button></div>'
    +'<div id="meldung"></div>'
    +'<div class="stand">'+(s.gesamt-s.offen)+' erledigt &middot; '+s.offen+' offen</div>';
  const feld=document.getElementById('wert');
  feld.focus();
  feld.addEventListener('keydown',e=>{if(e.key==='Enter'){e.preventDefault();senden('uebernommen');}});
}
async function senden(aktion){
  if(!aktuell)return;
  const meldung=document.getElementById('meldung');
  meldung.innerHTML='';
  let zeichenfolge='';
  if(aktion==='uebernommen'){
    zeichenfolge=document.getElementById('wert').value.replace(/ /g,'');
    if(zeichenfolge.length!==aktuell.anzahl_boxen){
      meldung.innerHTML='<div class="fehler">'+zeichenfolge.length+' Zeichen fuer '
        +aktuell.anzahl_boxen+' Boxen - passt nicht. Bitte korrigieren oder '
        +'"boxen passen nicht" waehlen.</div>';
      return;
    }
  }
  const antwort=await fetch('/entscheiden',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({id:aktuell.id,aktion:aktion,zeichenfolge:zeichenfolge,revision:revision})});
  if(!antwort.ok){
    meldung.innerHTML='<div class="fehler">'+(await antwort.json()).fehler+'</div>';
    return;
  }
  laden();
}
laden();
</script>"""


def create_server(store: OsdHandlabelStore, port: int) -> ThreadingHTTPServer:
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
                self.send_header("Content-Type", _content_type(bild))
                self.send_header("Cache-Control", "no-store")
                self.send_header("Content-Length", str(len(daten)))
                self.end_headers()
                self.wfile.write(daten)
                return
            self._json({"fehler": "unbekannt"}, 404)

        def do_POST(self) -> None:  # noqa: N802
            if urlparse(self.path).path != "/entscheiden":
                self._json({"fehler": "unbekannt"}, 404)
                return
            laenge = int(self.headers.get("Content-Length") or 0)
            anfrage = json.loads(self.rfile.read(laenge) or b"{}")
            try:
                self._json(store.entscheiden(
                    str(anfrage.get("id") or ""),
                    str(anfrage.get("aktion") or ""),
                    str(anfrage.get("zeichenfolge") or ""),
                    int(anfrage.get("revision") or 0)))
            except ValueError as fehler:
                self._json({"fehler": str(fehler)}, 400)

    return ThreadingHTTPServer(("127.0.0.1", port), Handler)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--queue", type=Path,
                        default=Path(r"C:\KI_BRAIN\training\diagnostics\osd_handlabel_v1"))
    parser.add_argument("--output", type=Path,
                        default=Path(r"C:\KI_BRAIN\eval_review\osd_handlabel_review_v1.json"))
    parser.add_argument("--reviewer", default="Pascal")
    parser.add_argument("--port", type=int, default=18913)
    args = parser.parse_args(argv)
    store = OsdHandlabelStore(args.queue, args.output, args.reviewer)
    server = create_server(store, args.port)
    stand = store.stand()
    print(f"Pruefplatz: http://127.0.0.1:{server.server_address[1]}/")
    print(f"Bilder: {stand['gesamt']}, offen: {stand['offen']}")
    print(f"Review-Ausgabe: {store.output}")
    print("Stoppen mit Strg+C")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
