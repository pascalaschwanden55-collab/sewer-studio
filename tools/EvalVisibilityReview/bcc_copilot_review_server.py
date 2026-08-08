"""Pruefplatz fuer den Bogen-Vorabdurchlauf: bestaetigen, korrigieren, verwerfen.

Der Mensch entscheidet ueber jeden Vorschlag. Nichts wird automatisch uebernommen,
nichts in den Goldbestand geschrieben — die Entscheidungen liegen in einer eigenen
Datei und tragen ausdruecklich, dass ein Modellvorschlag sichtbar war. Genau diese
Herkunft trennt spaeter Trainingsmaterial von Messmaterial.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Sequence
from urllib.parse import parse_qs, urlparse

URTEILE = ("bestaetigt", "korrigiert", "verworfen")


def _sha256(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1 << 20), b""):
            hasher.update(block)
    return hasher.hexdigest()


class CopilotReviewStore:
    """Haelt den Durchgang und die Entscheidungen; schreibt atomar unter einer Sperre."""

    def __init__(self, lauf_root: Path, output_path: Path, reviewer: str) -> None:
        if not reviewer.strip():
            raise ValueError("Ein Pruefername ist Pflicht.")
        self.lauf_root = Path(lauf_root)
        self.output_path = Path(output_path)
        self.reviewer = reviewer.strip()
        self._lock = threading.Lock()

        quelle = self.lauf_root / "vorschlaege.json"
        if not quelle.is_file():
            raise SystemExit(f"Durchgang fehlt: {quelle}")
        self.lauf_sha256 = _sha256(quelle)
        self.lauf = json.loads(quelle.read_text(encoding="utf-8-sig"))
        self.vorschlaege = list(self.lauf.get("vorschlaege") or [])
        if not self.vorschlaege:
            raise SystemExit("Der Durchgang enthaelt keine Vorschlaege.")
        self.entscheidungen: dict[str, dict] = {}
        self._laden()

    def _laden(self) -> None:
        if not self.output_path.is_file():
            return
        vorhanden = json.loads(self.output_path.read_text(encoding="utf-8-sig"))
        if vorhanden.get("lauf_sha256") != self.lauf_sha256:
            raise SystemExit(
                "Die vorhandene Pruefung gehoert zu einem anderen Durchgang — "
                "bitte einen neuen Ausgabepfad waehlen.")
        self.entscheidungen = dict(vorhanden.get("entscheidungen") or {})

    def _speichern(self) -> None:
        inhalt = {
            "schema_version": 1,
            "reviewer": self.reviewer,
            "lauf_sha256": self.lauf_sha256,
            "haltung": self.lauf.get("haltung"),
            "video": self.lauf.get("video"),
            # Herkunft: hier war beim Entscheiden ein Modellvorschlag sichtbar.
            "vorschlag_sichtbar": True,
            "kandidat": self.lauf.get("kandidat"),
            "gewicht_sha256": self.lauf.get("gewicht_sha256"),
            "min_confidence": self.lauf.get("min_confidence"),
            "strong_confidence": self.lauf.get("strong_confidence"),
            "gesamt": len(self.vorschlaege),
            "entscheidungen": self.entscheidungen,
        }
        self.output_path.parent.mkdir(parents=True, exist_ok=True)
        temp = self.output_path.with_suffix(".json.tmp")
        temp.write_text(json.dumps(inhalt, indent=2, ensure_ascii=False), encoding="utf-8")
        temp.replace(self.output_path)

    def stand(self) -> dict:
        offen = [v for v in self.vorschlaege if str(v["nummer"]) not in self.entscheidungen]
        zaehlung = {urteil: 0 for urteil in URTEILE}
        for eintrag in self.entscheidungen.values():
            if eintrag["urteil"] in zaehlung:
                zaehlung[eintrag["urteil"]] += 1
        naechster = dict(offen[0]) if offen else None
        if naechster is not None:
            naechster["ort"] = self._ort(naechster)
        return {
            "gesamt": len(self.vorschlaege),
            "offen": len(offen),
            "naechster": naechster,
            "zaehlung": zaehlung,
            "haltung": self.lauf.get("haltung"),
        }

    @staticmethod
    def _ort(vorschlag: dict) -> str:
        """Meter, wenn gelesen — sonst ausdruecklich die Videozeit."""
        start = vorschlag.get("meter_min")
        ende = vorschlag.get("meter_max")
        if start is None:
            return f"Sekunde {vorschlag['peak_zeit']:.0f} (Meterstand nicht lesbar)"
        if abs(float(ende) - float(start)) < 0.05:
            return f"Meter {float(start):.2f}"
        return f"Meter {float(start):.2f}\u2013{float(ende):.2f}"

    def entscheiden(self, nummer: str, urteil: str, code: str, kommentar: str) -> dict:
        if urteil not in URTEILE:
            raise ValueError(f"Unbekanntes Urteil: {urteil}")
        if urteil == "korrigiert" and not code.strip():
            raise ValueError("Eine Korrektur braucht den richtigen Code.")
        with self._lock:
            vorschlag = next(
                (v for v in self.vorschlaege if str(v["nummer"]) == str(nummer)), None)
            if vorschlag is None:
                raise ValueError(f"Unbekannter Vorschlag: {nummer}")
            self.entscheidungen[str(nummer)] = {
                "urteil": urteil,
                "vorgeschlagener_code": "BCC",
                "richtiger_code": code.strip().upper() if urteil == "korrigiert" else (
                    "BCC" if urteil == "bestaetigt" else ""),
                "kommentar": kommentar.strip(),
                "meter_min": vorschlag.get("meter_min"),
                "meter_max": vorschlag.get("meter_max"),
                "peak_zeit": vorschlag.get("peak_zeit"),
                "max_conf": vorschlag.get("max_conf"),
                "stufe": vorschlag.get("stufe"),
            }
            self._speichern()
            return self.stand()

    def zuruecknehmen(self) -> dict:
        with self._lock:
            entschieden = [v for v in self.vorschlaege
                           if str(v["nummer"]) in self.entscheidungen]
            if entschieden:
                self.entscheidungen.pop(str(entschieden[-1]["nummer"]), None)
                self._speichern()
            return self.stand()

    def clip_pfad(self, nummer: str) -> Path | None:
        vorschlag = next((v for v in self.vorschlaege if str(v["nummer"]) == str(nummer)), None)
        if vorschlag is None:
            return None
        pfad = (self.lauf_root / "clips" / vorschlag["clip"]).resolve()
        wurzel = (self.lauf_root / "clips").resolve()
        if wurzel not in pfad.parents or not pfad.is_file():
            return None
        return pfad


SEITE = """<!doctype html><meta charset="utf-8"><title>Bogen-Vorschlaege pruefen</title>
<style>
 body{background:#14161a;color:#e8eaed;font-family:Segoe UI,sans-serif;margin:0;padding:18px}
 h1{font-size:19px;margin:0 0 2px} .sub{color:#9aa0a6;font-size:13px;margin-bottom:12px}
 .stark{color:#8ab4f8;font-weight:600} .schwach{color:#c9a227;font-weight:600}
 video{max-width:100%;max-height:56vh;background:#000;border-radius:6px;display:block}
 .btns{margin-top:14px;display:flex;gap:10px;flex-wrap:wrap;align-items:center}
 button{font-size:15px;padding:11px 18px;border:0;border-radius:6px;cursor:pointer;color:#fff}
 .ja{background:#2e7d32} .korr{background:#8a6d1f} .nein{background:#8e2f2f}
 .zurueck{background:#33383f;font-size:13px;padding:8px 13px}
 input{background:#20242a;border:1px solid #3a4048;color:#e8eaed;border-radius:5px;
       padding:9px 11px;font-size:15px}
 .stand{margin-top:14px;color:#9aa0a6;font-size:13px}
 .fertig{font-size:17px;color:#8ab4f8;margin-top:20px}
 kbd{background:#2a2e34;border-radius:3px;padding:1px 6px;font-size:12px}
</style>
<div id="app">Lade…</div>
<script>
let aktuell=null;
async function laden(){
  const s=await (await fetch('/stand')).json();
  const app=document.getElementById('app');
  if(!s.naechster){
    aktuell=null;
    app.innerHTML='<div class="fertig">Alle '+s.gesamt+' Vorschlaege bearbeitet.</div>'
      +'<div class="stand">bestaetigt: '+s.zaehlung.bestaetigt+' &middot; korrigiert: '
      +s.zaehlung.korrigiert+' &middot; verworfen: '+s.zaehlung.verworfen+'</div>'
      +'<div class="btns"><button class="zurueck" onclick="zurueck()">Letzte Entscheidung zuruecknehmen</button></div>';
    return;
  }
  aktuell=s.naechster;
  const nr=s.gesamt-s.offen+1;
  const stufe=aktuell.stufe==='stark'
    ?'<span class="stark">starker Vorschlag</span>':'<span class="schwach">schwacher Vorschlag</span>';
  app.innerHTML='<h1>Ist das ein Bogen?</h1>'
    +'<div class="sub">Vorschlag '+nr+' von '+s.gesamt+' &middot; Haltung '+(s.haltung||'')
    +' &middot; '+aktuell.ort+' &middot; '+stufe+' ('+Number(aktuell.max_conf).toFixed(2)+')</div>'
    +'<video src="/clip?nr='+aktuell.nummer+'" autoplay loop muted controls></video>'
    +'<div class="btns">'
    +'<button class="ja" onclick="entscheide(\\'bestaetigt\\')">Bogen bestaetigen <kbd>1</kbd></button>'
    +'<input id="code" placeholder="anderer Code, z.B. BAJC" size="18">'
    +'<button class="korr" onclick="entscheide(\\'korrigiert\\')">korrigieren <kbd>2</kbd></button>'
    +'<button class="nein" onclick="entscheide(\\'verworfen\\')">kein Bogen <kbd>3</kbd></button>'
    +'<button class="zurueck" onclick="zurueck()">zurueck</button></div>'
    +'<div class="btns"><input id="kommentar" placeholder="Bemerkung (freiwillig)" size="52"></div>'
    +'<div class="stand">bisher &mdash; bestaetigt: '+s.zaehlung.bestaetigt+' &middot; korrigiert: '
    +s.zaehlung.korrigiert+' &middot; verworfen: '+s.zaehlung.verworfen+'</div>';
}
async function entscheide(u){
  if(!aktuell)return;
  const code=(document.getElementById('code')||{}).value||'';
  const kommentar=(document.getElementById('kommentar')||{}).value||'';
  if(u==='korrigiert'&&!code.trim()){alert('Bitte den richtigen Code eintragen.');return;}
  const antwort=await fetch('/entscheiden',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({nummer:aktuell.nummer,urteil:u,code:code,kommentar:kommentar})});
  if(!antwort.ok){alert((await antwort.json()).fehler||'Fehler');return;}
  laden();
}
async function zurueck(){ await fetch('/zurueck',{method:'POST'}); laden(); }
document.addEventListener('keydown',e=>{
  if(e.target.tagName==='INPUT')return;
  if(e.key==='1')entscheide('bestaetigt');
  if(e.key==='2')entscheide('korrigiert');
  if(e.key==='3')entscheide('verworfen');
});
laden();
</script>"""


def create_server(store: CopilotReviewStore, port: int = 8778) -> ThreadingHTTPServer:
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
                self._json(store.stand())
                return
            if weg.path == "/clip":
                nummer = (parse_qs(weg.query).get("nr") or [""])[0]
                pfad = store.clip_pfad(nummer)
                if pfad is None:
                    self._json({"fehler": "Clip nicht gefunden"}, 404)
                    return
                daten = pfad.read_bytes()
                self.send_response(200)
                self.send_header("Content-Type", "video/mp4")
                self.send_header("Content-Length", str(len(daten)))
                self.end_headers()
                self.wfile.write(daten)
                return
            self._json({"fehler": "unbekannt"}, 404)

        def do_POST(self) -> None:  # noqa: N802
            weg = urlparse(self.path)
            if weg.path == "/zurueck":
                self._json(store.zuruecknehmen())
                return
            if weg.path != "/entscheiden":
                self._json({"fehler": "unbekannt"}, 404)
                return
            laenge = int(self.headers.get("Content-Length") or 0)
            anfrage = json.loads(self.rfile.read(laenge) or b"{}")
            try:
                self._json(store.entscheiden(
                    str(anfrage.get("nummer", "")),
                    anfrage.get("urteil", ""),
                    anfrage.get("code", "") or "",
                    anfrage.get("kommentar", "") or ""))
            except ValueError as fehler:
                self._json({"fehler": str(fehler)}, 400)

    return ThreadingHTTPServer(("127.0.0.1", port), Handler)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Bogen-Vorschlaege bestaetigen oder korrigieren")
    parser.add_argument("--lauf", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=None)
    parser.add_argument("--reviewer", default="Pascal")
    parser.add_argument("--port", type=int, default=8778)
    args = parser.parse_args(argv)

    ausgabe = args.output or (
        Path(r"C:\KI_BRAIN\eval_review\copilot_durchlaeufe") / f"{args.lauf.name}.json")
    store = CopilotReviewStore(args.lauf, ausgabe, args.reviewer)
    server = create_server(store, args.port)
    stand = store.stand()
    print(f"Pruefplatz: http://127.0.0.1:{server.server_address[1]}/")
    print(f"Vorschlaege: {stand['gesamt']}, offen: {stand['offen']}")
    print(f"Ergebnis: {store.output_path}")
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
