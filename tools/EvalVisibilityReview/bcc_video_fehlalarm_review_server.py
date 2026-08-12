"""Lokaler Pruefplatz: echter Bogen oder Fehlalarm?

DIAGNOSE. Zeigt je Meldung einen kurzen, unveraenderten Clip aus dem
Originalvideo. Konfidenz und jede Vorab-Einstufung bleiben unsichtbar, damit das
Urteil nicht gelenkt wird. Der Prueflauf veraendert weder Kundenoriginale noch
Gold, Trainingsdaten oder Modelle.
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

URTEILE = ("bogen", "kein_bogen", "unsicher")


def _sha256(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1024 * 1024), b""):
            hasher.update(block)
    return hasher.hexdigest()


class FehlalarmReviewStore:
    """Haelt Warteschlange und Urteile; schreibt atomar und unter einer Sperre."""

    def __init__(self, queue_root: Path, output_path: Path, reviewer: str) -> None:
        if not reviewer.strip():
            raise ValueError("Ein Pruefername ist Pflicht.")
        self.queue_root = Path(queue_root)
        self.output_path = Path(output_path)
        self.reviewer = reviewer.strip()
        self._lock = threading.Lock()

        manifest = self.queue_root / "queue.json"
        if not manifest.is_file():
            raise SystemExit(f"Warteschlange fehlt: {manifest}")
        self.queue_sha256 = _sha256(manifest)
        self.queue = json.loads(manifest.read_text(encoding="utf-8-sig"))
        self.faelle = list(self.queue.get("faelle") or [])
        if not self.faelle:
            raise SystemExit("Die Warteschlange enthaelt keine Faelle.")
        if int(self.queue.get("schema_version") or 1) >= 2:
            for fall in self.faelle:
                clip = self.queue_root / "clips" / str(fall.get("clip") or "")
                erwartet = str(fall.get("clip_sha256") or "")
                if not erwartet or not clip.is_file() or _sha256(clip) != erwartet:
                    raise SystemExit(
                        f"Clip fehlt oder wurde veraendert: {fall.get('fall_id', '?')}")
        self.urteile: dict[str, dict] = {}
        self._laden()

    def _laden(self) -> None:
        if not self.output_path.is_file():
            return
        vorhanden = json.loads(self.output_path.read_text(encoding="utf-8-sig"))
        if vorhanden.get("queue_sha256") != self.queue_sha256:
            raise SystemExit(
                "Vorhandene Review gehoert zu einer anderen Warteschlange — "
                "bitte einen neuen Ausgabepfad waehlen."
            )
        self.urteile = dict(vorhanden.get("urteile") or {})

    def _speichern(self) -> None:
        inhalt = {
            "schema_version": 1,
            "reviewer": self.reviewer,
            "queue_sha256": self.queue_sha256,
            "quelle_bericht_sha256": self.queue.get("quelle_bericht_sha256"),
            "gesamt": len(self.faelle),
            "urteile": self.urteile,
        }
        self.output_path.parent.mkdir(parents=True, exist_ok=True)
        temp = self.output_path.with_suffix(".json.tmp")
        temp.write_text(json.dumps(inhalt, indent=2, ensure_ascii=False), encoding="utf-8")
        temp.replace(self.output_path)

    def stand(self) -> dict:
        offen = [fall for fall in self.faelle if fall["fall_id"] not in self.urteile]
        zaehlung = {urteil: 0 for urteil in URTEILE}
        for eintrag in self.urteile.values():
            if eintrag["urteil"] in zaehlung:
                zaehlung[eintrag["urteil"]] += 1
        return {
            "gesamt": len(self.faelle),
            "offen": len(offen),
            "naechster": offen[0] if offen else None,
            "zaehlung": zaehlung,
        }

    def entscheiden(self, fall_id: str, urteil: str) -> dict:
        if urteil not in URTEILE:
            raise ValueError(f"Unbekanntes Urteil: {urteil}")
        with self._lock:
            fall = next((f for f in self.faelle if f["fall_id"] == fall_id), None)
            if fall is None:
                raise ValueError(f"Unbekannter Fall: {fall_id}")
            self.urteile[fall_id] = {
                "urteil": urteil,
                "haltung": fall["haltung"],
                "start_s": fall["start_s"],
                "ende_s": fall["ende_s"],
            }
            self._speichern()
            return self.stand()

    def zuruecknehmen(self) -> dict:
        with self._lock:
            beurteilt = [f for f in self.faelle if f["fall_id"] in self.urteile]
            if beurteilt:
                self.urteile.pop(beurteilt[-1]["fall_id"], None)
                self._speichern()
            return self.stand()

    def clip_pfad(self, fall_id: str) -> Path | None:
        fall = next((f for f in self.faelle if f["fall_id"] == fall_id), None)
        if fall is None:
            return None
        pfad = (self.queue_root / "clips" / fall["clip"]).resolve()
        wurzel = (self.queue_root / "clips").resolve()
        if wurzel not in pfad.parents:
            return None
        return pfad if pfad.is_file() else None


SEITE = """<!doctype html><meta charset="utf-8"><title>Bogen oder Fehlalarm?</title>
<style>
 body{background:#14161a;color:#e8eaed;font-family:Segoe UI,sans-serif;margin:0;padding:18px}
 h1{font-size:19px;margin:0 0 4px} .sub{color:#9aa0a6;font-size:13px;margin-bottom:14px}
 video{max-width:100%;max-height:62vh;background:#000;border-radius:6px;display:block}
 .btns{margin-top:16px;display:flex;gap:10px;flex-wrap:wrap}
 button{font-size:15px;padding:12px 20px;border:0;border-radius:6px;cursor:pointer;color:#fff}
 .ja{background:#2e7d32} .nein{background:#8e2f2f} .unklar{background:#5a5f66} .zurueck{background:#33383f;font-size:13px;padding:9px 14px}
 .stand{margin-top:16px;color:#9aa0a6;font-size:13px}
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
    app.innerHTML='<div class="fertig">Alle '+s.gesamt+' Faelle beurteilt.</div><div class="stand">Bogen: '
      +s.zaehlung.bogen+' &middot; kein Bogen: '+s.zaehlung.kein_bogen+' &middot; unsicher: '+s.zaehlung.unsicher
      +'</div><div class="btns"><button class="zurueck" onclick="zurueck()">Letztes Urteil zuruecknehmen</button></div>';
    return;
  }
  aktuell=s.naechster;
  const nr=s.gesamt-s.offen+1;
  app.innerHTML='<h1>Ist hier ein Bogen zu sehen?</h1>'
    +'<div class="sub">Fall '+nr+' von '+s.gesamt+' &middot; Haltung '+aktuell.haltung
    +' &middot; Sekunde '+aktuell.start_s+'–'+aktuell.ende_s+'</div>'
    +'<video src="/clip?id='+aktuell.fall_id+'" autoplay loop muted controls></video>'
    +'<div class="btns">'
    +'<button class="ja" onclick="urteil(\\'bogen\\')">Bogen <kbd>1</kbd></button>'
    +'<button class="nein" onclick="urteil(\\'kein_bogen\\')">kein Bogen <kbd>2</kbd></button>'
    +'<button class="unklar" onclick="urteil(\\'unsicher\\')">unsicher <kbd>3</kbd></button>'
    +'<button class="zurueck" onclick="zurueck()">zurueck</button></div>'
    +'<div class="stand">bisher &mdash; Bogen: '+s.zaehlung.bogen+' &middot; kein Bogen: '
    +s.zaehlung.kein_bogen+' &middot; unsicher: '+s.zaehlung.unsicher+'</div>';
}
async function urteil(u){
  if(!aktuell)return;
  await fetch('/urteil',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({fall_id:aktuell.fall_id,urteil:u})});
  laden();
}
async function zurueck(){ await fetch('/zurueck',{method:'POST'}); laden(); }
document.addEventListener('keydown',e=>{
  if(e.key==='1')urteil('bogen'); if(e.key==='2')urteil('kein_bogen'); if(e.key==='3')urteil('unsicher');
});
laden();
</script>"""


def create_server(store: FehlalarmReviewStore, port: int = 8776) -> ThreadingHTTPServer:
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_args) -> None:  # ruhiges Terminal
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
                fall_id = (parse_qs(weg.query).get("id") or [""])[0]
                pfad = store.clip_pfad(fall_id)
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
            if weg.path != "/urteil":
                self._json({"fehler": "unbekannt"}, 404)
                return
            laenge = int(self.headers.get("Content-Length") or 0)
            anfrage = json.loads(self.rfile.read(laenge) or b"{}")
            try:
                self._json(store.entscheiden(anfrage.get("fall_id", ""), anfrage.get("urteil", "")))
            except ValueError as fehler:
                self._json({"fehler": str(fehler)}, 400)

    return ThreadingHTTPServer(("127.0.0.1", port), Handler)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Blinde Pruefung: Bogen oder Fehlalarm?")
    parser.add_argument(
        "--queue",
        type=Path,
        default=Path(r"C:\KI_BRAIN\training\diagnostics\bcc_video_fehlalarm_queue"),
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(r"C:\KI_BRAIN\eval_review\bcc_video_fehlalarm_review.json"),
    )
    parser.add_argument("--reviewer", default="Pascal")
    parser.add_argument("--port", type=int, default=8776)
    args = parser.parse_args(argv)

    store = FehlalarmReviewStore(args.queue, args.output, args.reviewer)
    server = create_server(store, args.port)
    stand = store.stand()
    print(f"Pruefplatz: http://127.0.0.1:{server.server_address[1]}/")
    print(f"Faelle: {stand['gesamt']}, offen: {stand['offen']}")
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
