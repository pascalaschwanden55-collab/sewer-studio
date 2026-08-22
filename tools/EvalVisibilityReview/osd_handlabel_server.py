"""Lokaler Pruefplatz fuer die harte OSD-Handliste (osd_handlabel.py queue).

REWORK 2026-08-16 - warum der erste Entwurf ersetzt wurde: Er zeichnete
Boxen, die osd_meter.boxen_aus_maske() VORHER auf dem ganzen 720x576-Frame
gefunden hatte. Zwei Defekte, am fertigen 200er-Bestand nachgemessen: Erstens
waren die Boxen auf dem vollen Frame praktisch unsichtbar - die Meteranzeige
ist eine Briefmarke unten rechts. Zweitens, schlimmer: Bei 166 von 200
Faellen waren die Boxen Bruchstuecke innerhalb der Zeichenstriche, keine
Zeichen (siehe osd_handlabel.py-Moduldocstring fuer die Messung).

Der neue Ablauf zeigt deshalb NUR die Zone unten rechts, 4x vergroessert mit
NEAREST (Pixel bleiben scharf). Der Mensch zieht EINEN Kasten mit der Maus um
die Meteranzeige; der Server segmentiert live per
osd_handlabel.zeichen_in_kasten() und zeigt die gefundenen, nummerierten
Zeichenboxen. Der Mensch tippt die Zeichenfolge von links nach rechts - eine
Box pro Zeichen, Anzahl muss uebereinstimmen. Es wird NIE eine Modell- oder
Vorlagenlesung angezeigt: Genau die Beeinflussung wuerde den ganzen Zweck der
Handliste zerstoeren (siehe osd_handlabel.py-Moduldocstring). Passt der
Kasten nicht oder die Segmentierung daneben, drueckt der Mensch "boxen passen
nicht".
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Sequence
from urllib.parse import parse_qs, urlparse

try:
    from .review_server_security import read_json_body, require_loopback_host
except ImportError:  # Direkter Skriptstart aus diesem Ordner
    from review_server_security import read_json_body, require_loopback_host

from PIL import Image

WURZEL = Path(__file__).resolve().parents[2]
if str(WURZEL / "sidecar") not in sys.path:
    sys.path.insert(0, str(WURZEL / "sidecar"))
if str(WURZEL / "training" / "scripts") not in sys.path:
    sys.path.insert(0, str(WURZEL / "training" / "scripts"))

# Zeichensatz wird LIVE aus osd_meter gelesen (keine eigene Kopie - ein
# spaeteres Erweitern von ZEICHEN muesste sonst zwei Stellen pflegen).
from sidecar import osd_meter  # noqa: E402
import osd_crop  # noqa: E402
import osd_handlabel  # noqa: E402

AKTIONEN = ("uebernommen", "unleserlich", "boxen_passen_nicht")

# Vergroesserungsfaktor der angezeigten Zone. NEAREST haelt Pixel scharf -
# jede andere Interpolation wuerde die Zeichenkanten genau dort verwischen,
# wo der Mensch sie fuer den Kasten braucht.
ZONEN_SKALA = 4


def sha256_datei(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


class OsdHandlabelStore:
    """Liest queue.json, schreibt/laedt die Review atomar und revisionssicher.

    Die Revision schuetzt gegen zwei gleichzeitig offene Tabs/Prozesse: jede
    Antwort auf /stand traegt die aktuelle Revision, jede Entscheidung muss
    sie zurueckschicken. Weicht sie ab, hat ein anderer Tab zwischenzeitlich
    gespeichert - abgewiesen statt still ueberschrieben.

    Die Queue traegt KEINE Boxen mehr (siehe Moduldocstring) - Boxen entstehen
    erst hier, wenn der Mensch einen Kasten zieht (vorschau()) und werden erst
    bei einer bestaetigten Entscheidung (entscheiden()) in die Review
    geschrieben.
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

        for fall in self.faelle:
            fall_id = str(fall.get("id") or "")
            bild_pfad = Path(str(fall.get("bild_pfad") or ""))
            if not bild_pfad.is_file() or sha256_datei(bild_pfad) != fall.get("bild_sha256"):
                raise SystemExit(f"Bild fehlt oder wurde veraendert: {fall_id or '?'}")
            with Image.open(bild_pfad) as bild:
                bild.load()

        self.entscheidungen: dict[str, dict] = {}
        # fall_id -> Liste bereits gemeldeter Zeichen ausserhalb ZEICHEN, die
        # beim Uebernehmen-Versuch abgewiesen wurden. Rein informativ fuer
        # publizieren()'s Zusammenfassung ("wie viel Material geht so
        # verloren") - keine Entscheidung, aendert die Revision nicht.
        self._zeichen_ausserhalb_satz: dict[str, list[str]] = {}
        self._revision = 0
        # Die zuletzt per "zurueck" entfernte Entscheidung - nur solange
        # relevant, wie ihr Fall noch nicht neu entschieden wurde. Dient
        # ausschliesslich dazu, dem Menschen seine EIGENE vorherige Eingabe
        # wieder vorzulegen (nie eine Maschinenvermutung). Der Kasten wird
        # dabei bewusst NICHT wiederhergestellt - der Mensch zieht ihn neu.
        self._letzte_entfernte: dict | None = None
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
        self._zeichen_ausserhalb_satz = dict(daten.get("zeichen_ausserhalb_satz") or {})

    def _speichern(self) -> None:
        daten = {
            "schema": "osd_handlabel_review_v1",
            "reviewer": self.reviewer,
            "queue_sha256": self.queue_sha256,
            "gesamt": len(self.faelle),
            "entscheidungen": self.entscheidungen,
            "zeichen_ausserhalb_satz": self._zeichen_ausserhalb_satz,
        }
        self.output.parent.mkdir(parents=True, exist_ok=True)
        temp = self.output.with_suffix(self.output.suffix + ".tmp")
        temp.write_text(json.dumps(daten, indent=2, ensure_ascii=False), encoding="utf-8")
        temp.replace(self.output)

    def _fall(self, fall_id: str) -> dict | None:
        return next((f for f in self.faelle if str(f["id"]) == fall_id), None)

    def _vorherige_eingabe_fuer(self, fall_id: str) -> dict | None:
        """Die eigene fruehere Eingabe fuer GENAU diesen wiedervorgelegten
        Fall - oder None. Niemals eine Modell-/Vorlagenvermutung, und auch
        nicht der alte Kasten (der wird neu gezogen)."""
        letzte = self._letzte_entfernte
        if letzte is None or letzte["fall_id"] != fall_id:
            return None
        ergebnis = {"aktion": letzte["aktion"]}
        if letzte["aktion"] == "uebernommen":
            ergebnis["zeichenfolge"] = letzte["zeichenfolge"]
        return ergebnis

    def stand(self) -> dict:
        offen = [f for f in self.faelle if str(f["id"]) not in self.entscheidungen]
        naechster = None
        if offen:
            fall = offen[0]
            fall_id = str(fall["id"])
            naechster = {
                "id": fall_id,
                "haltung": fall.get("haltung"),
                "vorherige_eingabe": self._vorherige_eingabe_fuer(fall_id),
            }
        return {
            "gesamt": len(self.faelle),
            "offen": len(offen),
            "revision": self._revision,
            "kann_zurueck": bool(self.entscheidungen),
            "naechster": naechster,
        }

    def bild_pfad(self, fall_id: str) -> Path | None:
        fall = self._fall(fall_id)
        if fall is None:
            return None
        pfad = Path(str(fall.get("bild_pfad") or ""))
        return pfad if pfad.is_file() else None

    # -----------------------------------------------------------------
    # Zone-Anzeige + Koordinatenumrechnung. Der Client sieht und zieht
    # IMMER in skalierten Zonen-lokalen Pixeln (0..Zonenbreite*4); Boxen
    # werden intern in Vollbild-Koordinaten gefuehrt (wie
    # zeichen_in_kasten() sie liefert), weil publizieren() genau diese
    # Koordinaten fuer den YOLO-Zuschnitt braucht.
    # -----------------------------------------------------------------

    def zone_bild_bytes(self, fall_id: str) -> bytes | None:
        bild_pfad = self.bild_pfad(fall_id)
        if bild_pfad is None:
            return None
        with Image.open(bild_pfad) as roh:
            bild = roh.convert("RGB")
        zone = osd_crop.zonen_box(*bild.size)
        ausschnitt = bild.crop(zone)
        skaliert = ausschnitt.resize(
            (ausschnitt.width * ZONEN_SKALA, ausschnitt.height * ZONEN_SKALA),
            Image.NEAREST)
        puffer = io.BytesIO()
        skaliert.save(puffer, format="PNG")
        return puffer.getvalue()

    def _voller_kasten(self, bild: Image.Image,
                       kasten_skaliert: Sequence[float]) -> tuple[int, int, int, int]:
        zone_x0, zone_y0, _zx1, _zy1 = osd_crop.zonen_box(*bild.size)
        dx0, dy0, dx1, dy1 = kasten_skaliert
        return (
            zone_x0 + round(dx0 / ZONEN_SKALA), zone_y0 + round(dy0 / ZONEN_SKALA),
            zone_x0 + round(dx1 / ZONEN_SKALA), zone_y0 + round(dy1 / ZONEN_SKALA),
        )

    def _boxen_skaliert(self, bild: Image.Image,
                        boxen_voll: list[tuple[int, int, int, int]]) -> list[list[float]]:
        zone_x0, zone_y0, _zx1, _zy1 = osd_crop.zonen_box(*bild.size)
        return [
            [(x0 - zone_x0) * ZONEN_SKALA, (y0 - zone_y0) * ZONEN_SKALA,
             (x1 - zone_x0) * ZONEN_SKALA, (y1 - zone_y0) * ZONEN_SKALA]
            for (x0, y0, x1, y1) in boxen_voll
        ]

    @staticmethod
    def _kasten_pruefen(kasten_skaliert) -> None:
        if (not isinstance(kasten_skaliert, (list, tuple)) or len(kasten_skaliert) != 4
                or not all(isinstance(w, (int, float)) for w in kasten_skaliert)):
            raise ValueError(
                "Kein Kasten gezogen - bitte die Anzeige mit der Maus umrahmen.")

    def vorschau(self, fall_id: str, kasten_skaliert) -> dict:
        """Reine Vorschau: segmentiert probeweise, speichert nichts.

        Wird bei jedem Loslassen der Maus aufgerufen, damit der Mensch die
        gefundenen Zeichenboxen VOR dem Tippen sieht.
        """
        fall = self._fall(fall_id)
        if fall is None:
            raise ValueError(f"Unbekannter Fall: {fall_id}")
        self._kasten_pruefen(kasten_skaliert)
        bild_pfad = self.bild_pfad(fall_id)
        if bild_pfad is None:
            raise ValueError("Bild fehlt.")
        with Image.open(bild_pfad) as roh:
            bild = roh.convert("RGB")
        voller_kasten = self._voller_kasten(bild, kasten_skaliert)
        boxen = osd_handlabel.zeichen_in_kasten(bild, voller_kasten)
        return {"boxen": self._boxen_skaliert(bild, boxen)}

    def _zeichen_ausserhalb_satz_vermerken(self, fall_id: str, zeichen_liste: list[str]) -> None:
        """Merkt sich ALLE in diesem Versuch getippten unerlaubten Zeichen -
        nicht nur das erste aus der Fehlermeldung. Ein Versuch ist keine
        Entscheidung (der Fall bleibt offen), aendert deshalb auch nicht die
        Revision, wird aber sofort persistiert, damit publizieren() ihn auch
        nach einem Neustart mitzaehlt."""
        with self._lock:
            vorhandene = self._zeichen_ausserhalb_satz.setdefault(fall_id, [])
            neu = False
            for zeichen in zeichen_liste:
                if zeichen not in vorhandene:
                    vorhandene.append(zeichen)
                    neu = True
            if neu:
                self._speichern()

    def entscheiden(self, fall_id: str, aktion: str, zeichenfolge: str,
                    kasten_skaliert, erwartete_revision: int) -> dict:
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
            if not text:
                raise ValueError(
                    "Bitte eine Zeichenfolge eintippen oder eine andere Aktion waehlen.")
            self._kasten_pruefen(kasten_skaliert)
            bild_pfad = self.bild_pfad(fall_id)
            if bild_pfad is None:
                raise ValueError("Bild fehlt.")
            with Image.open(bild_pfad) as roh:
                bild = roh.convert("RGB")
            voller_kasten = self._voller_kasten(bild, kasten_skaliert)
            # Server-autoritativ neu segmentiert - der Client darf keine
            # eigene Boxenliste einschmuggeln, nur den gezogenen Kasten.
            boxen = osd_handlabel.zeichen_in_kasten(bild, voller_kasten)
            if len(text) != len(boxen):
                raise ValueError(
                    f"{len(text)} Zeichen fuer {len(boxen)} gefundene Boxen - "
                    "die Anzahl muss genau uebereinstimmen. Bitte den Kasten "
                    "neu ziehen, den Text korrigieren oder 'boxen passen "
                    "nicht' waehlen.")
            unbekannt = sorted(set(text) - set(osd_meter.ZEICHEN))
            if unbekannt:
                self._zeichen_ausserhalb_satz_vermerken(fall_id, unbekannt)
                raise ValueError(
                    f"Zeichen nicht erlaubt: {unbekannt[0]!r} (erlaubt: "
                    f"{osd_meter.ZEICHEN!r}). Bitte 'boxen passen nicht' waehlen.")
            eintrag = {"aktion": "uebernommen", "zeichenfolge": text,
                       "boxen": [list(box) for box in boxen]}
        else:
            eintrag = {"aktion": aktion}

        with self._lock:
            self.entscheidungen[fall_id] = eintrag
            # Die vorherige Eingabe wurde jetzt durch eine neue ersetzt -
            # nichts mehr davon zeigen, sollte dieser Fall spaeter erneut per
            # "zurueck" wieder aufgemacht werden (dann gilt seine NEUE
            # Entscheidung als "vorherige").
            if self._letzte_entfernte is not None and self._letzte_entfernte["fall_id"] == fall_id:
                self._letzte_entfernte = None
            self._revision += 1
            self._speichern()
        return self.stand()

    def zuruecknehmen(self, erwartete_revision: int) -> dict:
        """Springt zur zuletzt entschiedenen Karte zurueck und stellt sie
        erneut zur Entscheidung. Von der ersten Karte aus (nichts entschieden)
        tut der Aufruf nichts.

        Dieselbe Revisionspruefung wie entscheiden(): ein anderer Tab/Prozess
        darf den Zwei-Tabs-Schutz auch ueber diesen Weg nicht aushebeln.
        """
        if erwartete_revision != self._revision:
            raise ValueError(
                "Ein anderer Tab oder Prozess hat zwischenzeitlich gespeichert "
                "- bitte die Seite neu laden.")
        with self._lock:
            if not self.entscheidungen:
                return self.stand()
            # Dict-Einfuegereihenfolge ist seit Python 3.7 garantiert - der
            # letzte Schluessel ist die zuletzt getroffene Entscheidung.
            letzte_id = list(self.entscheidungen)[-1]
            entfernt = self.entscheidungen.pop(letzte_id)
            self._letzte_entfernte = {"fall_id": letzte_id, **entfernt}
            self._revision += 1
            self._speichern()
        return self.stand()


SEITE = """<!doctype html><meta charset="utf-8"><title>OSD-Handliste</title>
<style>
body{background:#14161a;color:#e8eaed;font-family:Segoe UI,sans-serif;margin:0;padding:16px}
h1{font-size:19px;margin:0 0 4px}.sub,.stand{color:#9aa0a6;font-size:13px}
.rahmen{position:relative;display:inline-block;margin-top:12px;background:#000;line-height:0;
        cursor:crosshair;touch-action:none;max-width:96vw;overflow:auto}
#bild{display:block;max-width:96vw;image-rendering:pixelated;image-rendering:crisp-edges;
      -webkit-user-drag:none;user-select:none}
.dragbox{position:absolute;border:2px dashed #facc15;pointer-events:none;box-sizing:border-box}
.zbox{position:absolute;border:2px solid #ff3b30;pointer-events:none;box-sizing:border-box}
.zbox .nr{position:absolute;top:-15px;left:-2px;font-size:10px;line-height:1;color:#ff3b30;
          background:#14161a;padding:0 2px}
.zeile{display:flex;gap:10px;flex-wrap:wrap;margin-top:14px;align-items:center}
input{background:#20242a;border:1px solid #3a4048;color:#e8eaed;border-radius:5px;
      padding:12px 14px;font-size:20px;width:240px}
button{border:0;border-radius:6px;padding:11px 16px;font-size:14px;cursor:pointer;color:#fff}
button:disabled{opacity:.35;cursor:not-allowed}
.ok{background:#2e7d32}.frage{background:#8a6d1f}.nein{background:#8e2f2f}.zurueck{background:#454b54}
.fehler{color:#ff6b6b;margin-top:8px;font-size:13px}
.hinweis{color:#8ab4f8;margin-top:10px;font-size:13px}
</style><div id="app">Lade...</div><script>
let aktuell=null, revision=0, kannZurueck=false, kastenAktuell=null;

async function laden(){
  const s=await (await fetch('/stand')).json();
  revision=s.revision;
  kannZurueck=s.kann_zurueck;
  const a=document.getElementById('app');
  if(!s.naechster){
    a.innerHTML='<h1>Alle '+s.gesamt+' Faelle entschieden.</h1>'
      +'<div class="zeile"><button class="zurueck"'+(kannZurueck?'':' disabled')
      +' onclick="zurueck()">zurueck</button></div>';
    return;
  }
  aktuell=s.naechster;
  kastenAktuell=null;
  const nr=s.gesamt-s.offen+1;
  let vorwert='', hinweisAlt='';
  const vorherige=aktuell.vorherige_eingabe;
  if(vorherige){
    if(vorherige.aktion==='uebernommen'){
      vorwert=vorherige.zeichenfolge||'';
    } else {
      hinweisAlt='<div class="hinweis">zuletzt: '
        +(vorherige.aktion==='unleserlich'?'unleserlich':'boxen passen nicht')+'</div>';
    }
  }
  a.innerHTML='<h1>Kasten um die Meteranzeige ziehen, dann Zeichen eintippen</h1>'
    +'<div class="sub">Fall '+nr+' von '+s.gesamt+' &middot; Haltung '+(aktuell.haltung||'?')+'</div>'
    +'<div class="rahmen" id="rahmen"><img id="bild" draggable="false" src="/bild?id='
    +aktuell.id+'&r='+Date.now()+'"></div>'
    +hinweisAlt
    +'<div class="hinweis" id="boxhinweis">Kasten mit der Maus um die Anzeige ziehen.</div>'
    +'<div class="zeile"><input id="wert" placeholder="von links nach rechts" autocomplete="off" value="'+vorwert+'">'
    +'<button class="ok" onclick="senden(\\'uebernommen\\')">uebernehmen</button>'
    +'<button class="frage" onclick="senden(\\'unleserlich\\')">unleserlich</button>'
    +'<button class="nein" onclick="senden(\\'boxen_passen_nicht\\')">boxen passen nicht</button>'
    +'<button class="zurueck"'+(kannZurueck?'':' disabled')+' onclick="zurueck()">zurueck</button></div>'
    +'<div id="meldung"></div>'
    +'<div class="stand">'+(s.gesamt-s.offen)+' erledigt &middot; '+s.offen+' offen</div>';
  bindeRahmen();
  const feld=document.getElementById('wert');
  feld.focus();
  feld.select();
  feld.addEventListener('keydown',e=>{if(e.key==='Enter'){e.preventDefault();senden('uebernommen');}});
}

function bindeRahmen(){
  const rahmen=document.getElementById('rahmen');
  let start=null;
  rahmen.addEventListener('pointerdown',e=>{
    start=punkt(e);
    try{rahmen.setPointerCapture(e.pointerId);}catch(err){}
    zeichneDrag(rahmen,start,start);
  });
  rahmen.addEventListener('pointermove',e=>{
    if(!start)return;
    zeichneDrag(rahmen,start,punkt(e));
  });
  rahmen.addEventListener('pointerup',async e=>{
    if(!start)return;
    const ende=punkt(e);
    const x0=Math.min(start.x,ende.x), y0=Math.min(start.y,ende.y);
    const x1=Math.max(start.x,ende.x), y1=Math.max(start.y,ende.y);
    start=null;
    entferneKlasse(rahmen,'dragbox');
    if(x1-x0<3||y1-y0<3){
      document.getElementById('boxhinweis').textContent='Kasten zu klein - bitte neu ziehen.';
      return;
    }
    kastenAktuell=[x0,y0,x1,y1];
    await vorschauZeigen(rahmen);
  });
}

function punkt(e){
  const bild=document.getElementById('bild');
  const rect=bild.getBoundingClientRect();
  const x=Math.max(0,Math.min(bild.naturalWidth,(e.clientX-rect.left)*bild.naturalWidth/rect.width));
  const y=Math.max(0,Math.min(bild.naturalHeight,(e.clientY-rect.top)*bild.naturalHeight/rect.height));
  return {x:x,y:y};
}

function positioniere(div,x0,y0,x1,y1){
  const bild=document.getElementById('bild');
  const fx=100/bild.naturalWidth, fy=100/bild.naturalHeight;
  div.style.left=(x0*fx)+'%';
  div.style.top=(y0*fy)+'%';
  div.style.width=((x1-x0)*fx)+'%';
  div.style.height=((y1-y0)*fy)+'%';
}

function zeichneDrag(rahmen,a,b){
  entferneKlasse(rahmen,'dragbox');
  const div=document.createElement('div');
  div.className='dragbox';
  positioniere(div,Math.min(a.x,b.x),Math.min(a.y,b.y),Math.max(a.x,b.x),Math.max(a.y,b.y));
  rahmen.appendChild(div);
}

function entferneKlasse(rahmen,klasse){
  for(const el of rahmen.querySelectorAll('.'+klasse))el.remove();
}

async function vorschauZeigen(rahmen){
  entferneKlasse(rahmen,'zbox');
  document.getElementById('boxhinweis').textContent='...';
  const antwort=await fetch('/vorschau',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({id:aktuell.id,kasten:kastenAktuell})});
  if(!antwort.ok){
    document.getElementById('boxhinweis').textContent='Konnte nicht segmentieren - Kasten neu ziehen.';
    return;
  }
  const daten=await antwort.json();
  let i=1;
  for(const b of daten.boxen){
    const div=document.createElement('div');
    div.className='zbox';
    positioniere(div,b[0],b[1],b[2],b[3]);
    const nr=document.createElement('span');
    nr.className='nr';
    nr.textContent=i++;
    div.appendChild(nr);
    rahmen.appendChild(div);
  }
  document.getElementById('boxhinweis').textContent=daten.boxen.length+' Zeichenboxen gefunden.';
}

async function senden(aktion){
  if(!aktuell)return;
  const meldung=document.getElementById('meldung');
  meldung.innerHTML='';
  let zeichenfolge='';
  if(aktion==='uebernommen'){
    zeichenfolge=document.getElementById('wert').value.replace(/ /g,'');
    if(!zeichenfolge){
      meldung.innerHTML='<div class="fehler">Bitte eine Zeichenfolge eintippen.</div>';
      return;
    }
    if(!kastenAktuell){
      meldung.innerHTML='<div class="fehler">Bitte zuerst einen Kasten um die Anzeige ziehen.</div>';
      return;
    }
  }
  const antwort=await fetch('/entscheiden',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({id:aktuell.id,aktion:aktion,zeichenfolge:zeichenfolge,
      kasten:kastenAktuell,revision:revision})});
  if(!antwort.ok){
    meldung.innerHTML='<div class="fehler">'+(await antwort.json()).fehler+'</div>';
    return;
  }
  laden();
}
async function zurueck(){
  if(!kannZurueck)return;
  const antwort=await fetch('/zurueck',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({revision:revision})});
  if(!antwort.ok){
    const meldung=document.getElementById('meldung');
    if(meldung)meldung.innerHTML='<div class="fehler">'+(await antwort.json()).fehler+'</div>';
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
                daten = store.zone_bild_bytes(fall_id)
                if daten is None:
                    self._json({"fehler": "Bild fehlt"}, 404)
                    return
                self.send_response(200)
                self.send_header("Content-Type", "image/png")
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
            if weg not in ("/entscheiden", "/zurueck", "/vorschau"):
                self._json({"fehler": "unbekannt"}, 404)
                return
            anfrage = json.loads(body or b"{}")
            try:
                if weg == "/zurueck":
                    self._json(store.zuruecknehmen(int(anfrage.get("revision") or 0)))
                elif weg == "/vorschau":
                    self._json(store.vorschau(
                        str(anfrage.get("id") or ""), anfrage.get("kasten")))
                else:
                    self._json(store.entscheiden(
                        str(anfrage.get("id") or ""),
                        str(anfrage.get("aktion") or ""),
                        str(anfrage.get("zeichenfolge") or ""),
                        anfrage.get("kasten"),
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
