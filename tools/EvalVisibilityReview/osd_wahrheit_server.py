"""Eingabeplatz fuer die menschliche Ablesung der OSD-Meterstaende.

Ein Bild gross, ein Eingabefeld, Enter — fertig. Kein Wechseln zwischen
Bildbetrachter und Notepad.

Die Lesung des Programms wird bewusst NICHT angezeigt. Sie waere sonst eine
Vorgabe statt einer Pruefung; genau diese Beeinflussung hat bei den Boegen die
Treffgenauigkeit einer KI-Sichtpruefung von 33 Prozent unbemerkt gelassen.

Geschrieben wird ausschliesslich wahrheit.txt, nach jeder Eingabe und atomar.
"""

from __future__ import annotations

import argparse
import json
import re
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Sequence
from urllib.parse import parse_qs, urlparse

ZEILE = re.compile(r"^(?P<nr>\d{4})\s*=\s*(?P<wert>.*)$")


class WahrheitStore:
    """Liest und schreibt wahrheit.txt; behaelt Kopfzeilen und Reihenfolge."""

    def __init__(self, wurzel: Path) -> None:
        self.wurzel = Path(wurzel)
        self.datei = self.wurzel / "wahrheit.txt"
        self.bilder = self.wurzel / "frames"
        if not self.datei.is_file():
            raise SystemExit(f"wahrheit.txt fehlt: {self.datei}")
        if not self.bilder.is_dir():
            raise SystemExit(f"Bildordner fehlt: {self.bilder}")
        self._lock = threading.Lock()
        self._haltungen = self._haltungen_lesen()

    def _haltungen_lesen(self) -> dict[int, str]:
        quelle = self.wurzel / "leser_ergebnisse.json"
        if not quelle.is_file():
            return {}
        try:
            daten = json.loads(quelle.read_text(encoding="utf-8-sig"))
        except (OSError, ValueError):
            return {}
        # Nur Nummer, Haltung und Dateiname — die Lesung des Programms bleibt aussen vor.
        return {int(e["nr"]): str(e.get("haltung") or "") for e in daten if "nr" in e}

    def _zeilen(self) -> list[str]:
        return self.datei.read_text(encoding="utf-8").splitlines()

    def eintraege(self) -> list[dict]:
        eintraege = []
        for zeile in self._zeilen():
            treffer = ZEILE.match(zeile.strip())
            if treffer is None:
                continue
            nummer = int(treffer.group("nr"))
            eintraege.append({
                "nr": nummer,
                "wert": treffer.group("wert").strip(),
                "haltung": self._haltungen.get(nummer, ""),
                "datei": f"f{nummer:04d}.jpg",
            })
        return eintraege

    def stand(self, nummer: int | None = None) -> dict:
        eintraege = self.eintraege()
        offen = [e for e in eintraege if not e["wert"]]
        aktuell = None
        if nummer is not None:
            aktuell = next((e for e in eintraege if e["nr"] == nummer), None)
        if aktuell is None:
            aktuell = offen[0] if offen else None
        return {
            "gesamt": len(eintraege),
            "offen": len(offen),
            "aktuell": aktuell,
            "erste_nummer": eintraege[0]["nr"] if eintraege else None,
            "letzte_nummer": eintraege[-1]["nr"] if eintraege else None,
        }

    def eintragen(self, nummer: int, wert: str) -> dict:
        wert = (wert or "").strip()
        if wert and wert != "?":
            # Nur Zahlen zulassen; Komma wie Punkt.
            geprueft = wert.replace(",", ".")
            try:
                float(geprueft)
            except ValueError as fehler:
                raise ValueError(f"'{wert}' ist keine Zahl. Unleserlich bitte als ? eintragen.") from fehler
            wert = geprueft

        with self._lock:
            zeilen = self._zeilen()
            gefunden = False
            for index, zeile in enumerate(zeilen):
                treffer = ZEILE.match(zeile.strip())
                if treffer is not None and int(treffer.group("nr")) == nummer:
                    zeilen[index] = f"{nummer:04d} = {wert}"
                    gefunden = True
                    break
            if not gefunden:
                raise ValueError(f"Nummer {nummer} steht nicht in wahrheit.txt.")

            temp = self.datei.with_suffix(".txt.tmp")
            temp.write_text("\n".join(zeilen) + "\n", encoding="utf-8")
            temp.replace(self.datei)

        naechste = self.stand()["aktuell"]
        return self.stand(naechste["nr"] if naechste else None)

    def bild_pfad(self, nummer: int) -> Path | None:
        pfad = (self.bilder / f"f{nummer:04d}.jpg").resolve()
        if self.bilder.resolve() not in pfad.parents or not pfad.is_file():
            return None
        return pfad


SEITE = """<!doctype html><meta charset="utf-8"><title>Meterstaende ablesen</title>
<style>
 body{background:#14161a;color:#e8eaed;font-family:Segoe UI,sans-serif;margin:0;padding:16px}
 h1{font-size:18px;margin:0 0 2px} .sub{color:#9aa0a6;font-size:13px;margin-bottom:10px}
 .rahmen{overflow:hidden;border-radius:6px;background:#000;max-height:64vh;cursor:zoom-in}
 .rahmen.gross{cursor:zoom-out}
 img{display:block;width:100%;transition:transform .12s;transform-origin:var(--ox,50%) var(--oy,50%)}
 .gross img{transform:scale(3.2)}
 .zeile{margin-top:14px;display:flex;gap:10px;align-items:center;flex-wrap:wrap}
 input{background:#20242a;border:1px solid #3a4048;color:#e8eaed;border-radius:5px;
       padding:12px 14px;font-size:22px;width:190px;text-align:center}
 button{font-size:15px;padding:11px 17px;border:0;border-radius:6px;cursor:pointer;color:#fff}
 .ok{background:#2e7d32} .frage{background:#8a6d1f} .zurueck{background:#33383f;font-size:13px;padding:8px 13px}
 .balken{margin-top:12px;height:6px;background:#20242a;border-radius:3px;overflow:hidden}
 .balken div{height:100%;background:#8ab4f8}
 .stand{margin-top:8px;color:#9aa0a6;font-size:13px}
 .fertig{font-size:17px;color:#8ab4f8;margin-top:20px}
 kbd{background:#2a2e34;border-radius:3px;padding:1px 6px;font-size:12px}
</style>
<div id="app">Lade…</div>
<script>
let aktuell=null;
async function laden(nr){
  const s=await (await fetch('/stand'+(nr?('?nr='+nr):''))).json();
  const app=document.getElementById('app');
  if(!s.aktuell){
    aktuell=null;
    app.innerHTML='<div class="fertig">Alle '+s.gesamt+' Meterstaende eingetragen.</div>'
      +'<div class="stand">wahrheit.txt ist gespeichert. Du kannst das Fenster schliessen.</div>';
    return;
  }
  aktuell=s.aktuell;
  const fertig=s.gesamt-s.offen;
  app.innerHTML='<h1>Welcher Meterstand steht im Bild?</h1>'
    +'<div class="sub">Nr. '+aktuell.nr+' von '+s.gesamt+' &middot; Haltung '+(aktuell.haltung||'?')
    +' &middot; <span style="color:#8ab4f8">Klick ins Bild vergroessert</span></div>'
    +'<div class="rahmen" id="rahmen"><img id="bild" src="/bild?nr='+aktuell.nr+'"></div>'
    +'<div class="zeile">'
    +'<input id="wert" placeholder="z.B. 12.5" value="'+(aktuell.wert||'')+'" autocomplete="off">'
    +'<button class="ok" onclick="speichern()">uebernehmen <kbd>Enter</kbd></button>'
    +'<button class="frage" onclick="unleserlich()">unleserlich <kbd>Esc</kbd></button>'
    +'<button class="zurueck" onclick="laden('+Math.max(1,aktuell.nr-1)+')">zurueck</button>'
    +'<button class="zurueck" onclick="laden('+(aktuell.nr+1)+')">weiter</button></div>'
    +'<div class="balken"><div style="width:'+(100*fertig/s.gesamt)+'%"></div></div>'
    +'<div class="stand">'+fertig+' von '+s.gesamt+' eingetragen &middot; '+s.offen+' offen</div>';
  const feld=document.getElementById('wert'); feld.focus(); feld.select();
  const rahmen=document.getElementById('rahmen');
  rahmen.onclick=e=>{
    const r=rahmen.getBoundingClientRect();
    const bild=document.getElementById('bild');
    bild.style.setProperty('--ox',(100*(e.clientX-r.left)/r.width)+'%');
    bild.style.setProperty('--oy',(100*(e.clientY-r.top)/r.height)+'%');
    rahmen.classList.toggle('gross');
  };
}
async function sende(wert){
  if(!aktuell)return;
  const antwort=await fetch('/eintragen',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({nr:aktuell.nr,wert:wert})});
  if(!antwort.ok){alert((await antwort.json()).fehler||'Fehler');return;}
  laden();
}
function speichern(){ sende(document.getElementById('wert').value); }
function unleserlich(){ sende('?'); }
document.addEventListener('keydown',e=>{
  if(e.key==='Enter'){e.preventDefault();speichern();}
  if(e.key==='Escape'){e.preventDefault();unleserlich();}
});
laden();
</script>"""


def create_server(store: WahrheitStore, port: int = 8790) -> ThreadingHTTPServer:
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_args) -> None:
            pass

        def _json(self, nutzlast: dict, status: int = 200) -> None:
            roh = json.dumps(nutzlast, ensure_ascii=False).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(roh)))
            self.end_headers()
            self.wfile.write(roh)

        def do_GET(self) -> None:  # noqa: N802
            weg = urlparse(self.path)
            if weg.path == "/":
                roh = SEITE.encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(roh)))
                self.end_headers()
                self.wfile.write(roh)
                return
            if weg.path == "/stand":
                roh = (parse_qs(weg.query).get("nr") or [""])[0]
                self._json(store.stand(int(roh) if roh.isdigit() else None))
                return
            if weg.path == "/bild":
                roh = (parse_qs(weg.query).get("nr") or [""])[0]
                pfad = store.bild_pfad(int(roh)) if roh.isdigit() else None
                if pfad is None:
                    self._json({"fehler": "Bild nicht gefunden"}, 404)
                    return
                daten = pfad.read_bytes()
                self.send_response(200)
                self.send_header("Content-Type", "image/jpeg")
                self.send_header("Content-Length", str(len(daten)))
                self.end_headers()
                self.wfile.write(daten)
                return
            self._json({"fehler": "unbekannt"}, 404)

        def do_POST(self) -> None:  # noqa: N802
            if urlparse(self.path).path != "/eintragen":
                self._json({"fehler": "unbekannt"}, 404)
                return
            laenge = int(self.headers.get("Content-Length") or 0)
            anfrage = json.loads(self.rfile.read(laenge) or b"{}")
            try:
                self._json(store.eintragen(int(anfrage.get("nr", 0)), anfrage.get("wert", "")))
            except (ValueError, TypeError) as fehler:
                self._json({"fehler": str(fehler)}, 400)

    return ThreadingHTTPServer(("127.0.0.1", port), Handler)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Meterstaende ablesen und eintragen")
    parser.add_argument(
        "--wurzel",
        type=Path,
        default=Path(r"C:\KI_BRAIN\training\diagnostics\osd_meter_reader_20260808\validierung"),
    )
    parser.add_argument("--port", type=int, default=8790)
    args = parser.parse_args(argv)

    store = WahrheitStore(args.wurzel)
    server = create_server(store, args.port)
    stand = store.stand()
    print(f"Eingabeplatz: http://127.0.0.1:{server.server_address[1]}/")
    print(f"Bilder: {stand['gesamt']}, offen: {stand['offen']}")
    print(f"Ziel: {store.datei}")
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
