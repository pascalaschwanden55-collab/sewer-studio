"""Lokaler Pruefplatz: Ist auf diesem Bild ein Bogen sichtbar?

Misst die Fehlerquote der automatisch erzeugten Negativbilder des Lernbestands
`bcc_protokoll_v1`. Diese Bilder stammen aus Haltungen, deren Protokoll keinen
Bogencode enthaelt — ob dort wirklich keiner ist, war bisher ungeprueft.

Warum das noetig ist: In der Blindpruefung vom 2026-08-09 zeigten 91 von 154
Clips einen Bogen, aber hoechstens 66 liessen sich einem Protokolleintrag
zuordnen. Mindestens 25 sichtbare Boegen ohne Eintrag, also jeder vierte. Das
Protokoll ist damit als Wahrheit unvollstaendig; wie oft das eine GANZE Haltung
betrifft, entscheidet diese Stichprobe.

BLIND: Gezeigt wird ausschliesslich das Bild. Haltung, Meterstand, Videopfad und
Sekunde bleiben verdeckt — sie stehen zwar in der Queue, werden aber nie an den
Browser gegeben. Ein Hinweis auf die Herkunft wuerde das Urteil lenken.

Der Pruefplatz veraendert weder den Lernbestand noch Kundenoriginale.
"""

from __future__ import annotations

import argparse
import hashlib
import http.server
import json
import os
import socketserver
import sys
import tempfile
import threading
import urllib.parse
import webbrowser
from pathlib import Path
from typing import Sequence

try:
    from .review_server_security import read_json_body, require_loopback_host
except ImportError:  # Direkter Skriptstart aus diesem Ordner
    from review_server_security import read_json_body, require_loopback_host

# Rueckfall fuer Bestaende ohne eigene Urteilsliste (die BCC-Negativpruefung
# vom 2026-08-10 ist damit gespeichert und muss weiter lesbar bleiben).
URTEILE = ("bogen_sichtbar", "kein_bogen", "unsicher")
STANDARD_URTEILE = [
    {"wert": "bogen_sichtbar", "beschriftung": "Bogen sichtbar", "taste": "1"},
    {"wert": "kein_bogen", "beschriftung": "kein Bogen", "taste": "2"},
    {"wert": "unsicher", "beschriftung": "unsicher", "taste": "3"},
]

SEITE = """<!doctype html>
<meta charset="utf-8">
<title>__FRAGE__</title>
<style>
 :root { color-scheme: dark; }
 body { margin:0; background:#15181c; color:#e8eaed;
        font-family:Segoe UI,system-ui,sans-serif; }
 header { padding:10px 18px; background:#1e2228; display:flex;
          align-items:baseline; gap:22px; border-bottom:1px solid #2c3138; }
 h1 { font-size:17px; margin:0; font-weight:600; }
 .fort { color:#9aa3ad; font-size:14px; }
 .bild { display:flex; justify-content:center; align-items:center;
         height:calc(100vh - 168px); background:#0d0f12; }
 .bild img, .bild video { max-width:98%; max-height:100%; object-fit:contain; }
 .bild video { background:#000; }
 .umschalter { position:absolute; top:78px; right:22px; font-size:13px; color:#9aa3ad; }
 footer { display:flex; gap:14px; padding:14px 18px; justify-content:center;
          background:#1e2228; border-top:1px solid #2c3138; }
 button { font-size:15px; padding:12px 26px; border-radius:7px; cursor:pointer;
          border:1px solid #3a4048; background:#262b32; color:#e8eaed; }
 button:hover { background:#2f353d; }
 .ja { border-color:#7a4a4a; } .nein { border-color:#3f6b46; }
 kbd { background:#111418; border:1px solid #394049; border-radius:4px;
       padding:1px 6px; font-size:12px; color:#9aa3ad; margin-left:8px; }
 .fertig { text-align:center; padding:70px 20px; font-size:19px; line-height:1.6; }
</style>
<header>
  <h1>__FRAGE__</h1>
  <span class="fort" id="fort"></span>
</header>
<div class="bild">
  <img id="bild" alt="">
  <video id="clip" autoplay loop muted controls style="display:none"></video>
</div>
<div class="umschalter" id="umschalter"></div>
<footer id="knoepfe">__KNOEPFE__</footer>
<script>
let fall = null;
async function laden() {
  const a = await (await fetch('/naechster')).json();
  if (a.fertig) {
    document.querySelector('.bild').innerHTML =
      '<div class="fertig">Alle ' + a.gesamt + ' Bilder beurteilt.<br>' +
      a.zusammenfassung + '<br><br>Fenster kann geschlossen werden.</div>';
    document.getElementById('knoepfe').style.display = 'none';
    document.getElementById('fort').textContent = '';
    fall = null;
    return;
  }
  fall = a.nummer;
  const bild = document.getElementById('bild');
  const clip = document.getElementById('clip');
  bild.src = '/bild/' + a.nummer + '?v=' + a.revision;
  if (a.clip) {
    // Bewegung zeigt mehr als ein Standbild. Der Clip laeuft, das Bild bleibt
    // per Taste 0 erreichbar — die Spitze des Modells ist manchmal schaerfer.
    clip.src = '/clip/' + a.nummer + '?v=' + a.revision;
    clip.style.display = 'block'; bild.style.display = 'none';
    document.getElementById('umschalter').textContent = 'Taste 0: Standbild / Clip';
  } else {
    clip.removeAttribute('src'); clip.style.display = 'none';
    bild.style.display = 'block';
    document.getElementById('umschalter').textContent = '';
  }
  document.getElementById('fort').textContent = a.erledigt + ' von ' + a.gesamt;
}
function umschalten() {
  const bild = document.getElementById('bild'), clip = document.getElementById('clip');
  if (!clip.getAttribute('src')) return;
  const zeigtClip = clip.style.display !== 'none';
  clip.style.display = zeigtClip ? 'none' : 'block';
  bild.style.display = zeigtClip ? 'block' : 'none';
}
async function urteil(wert) {
  if (fall === null) return;
  await fetch('/urteil', {method:'POST', headers:{'Content-Type':'application/json'},
    body: JSON.stringify({nummer: fall, urteil: wert})});
  laden();
}
const TASTEN = __TASTEN__;
document.addEventListener('keydown', e => {
  if (e.key === '0') { umschalten(); return; }
  if (TASTEN[e.key]) urteil(TASTEN[e.key]);
});
laden();
</script>
"""


def sha256_bytes(daten: bytes) -> str:
    return hashlib.sha256(daten).hexdigest()


class NegativReviewStore:
    """Haelt Queue und Urteile; prueft jede Bindung vor der Ausgabe."""

    def __init__(self, queue_root: Path, output_path: Path, reviewer: str) -> None:
        self.queue_root = queue_root
        self.output_path = output_path
        self.reviewer = reviewer.strip()
        if not self.reviewer:
            raise ValueError("Ein Reviewer-Name ist Pflicht.")
        self._sperre = threading.Lock()
        self._laden()

    def _laden(self) -> None:
        queue_datei = self.queue_root / "queue.json"
        sha_datei = self.queue_root / "queue.sha256"
        if not queue_datei.is_file() or not sha_datei.is_file():
            raise ValueError(f"Queue unvollstaendig: {self.queue_root}")

        rohbytes = queue_datei.read_bytes()
        # Die Queue ist an ihren eigenen Hash gebunden. Eine nachtraeglich
        # veraenderte Stichprobe waere keine Stichprobe mehr.
        ist = sha256_bytes(rohbytes)
        soll = sha_datei.read_text(encoding="utf-8").strip()
        if ist != soll:
            raise ValueError(
                f"Die Queue passt nicht zu ihrem Hash.\n  erwartet {soll}\n  gefunden {ist}")

        self.queue = json.loads(rohbytes.decode("utf-8-sig"))
        self.queue_sha256 = ist
        self.faelle = {int(f["nummer"]): f for f in self.queue["faelle"]}
        if not self.faelle:
            raise ValueError("Die Queue enthaelt keine Faelle.")

        # Frage und Urteile kommen aus der Queue; ein Bestand ohne eigene
        # Liste faellt auf die Bogen-Fassung zurueck, damit die abgeschlossene
        # Pruefung vom 2026-08-10 lesbar bleibt.
        self.frage = self.queue.get("frage") or "Ist das gesuchte Merkmal sichtbar?"
        self.urteilsliste = self.queue.get("urteile") or STANDARD_URTEILE
        self.erlaubt = tuple(u["wert"] for u in self.urteilsliste)

        self.urteile: dict[int, str] = {}
        if self.output_path.is_file():
            vorher = json.loads(self.output_path.read_text(encoding="utf-8-sig"))
            if vorher.get("queue_sha256") != self.queue_sha256:
                raise ValueError(
                    "Die vorhandene Review gehoert zu einer anderen Queue. "
                    "Bitte einen anderen Ausgabepfad waehlen.")
            for e in vorher.get("urteile", []):
                nummer = int(e["nummer"])
                if nummer in self.faelle and e.get("urteil") in self.erlaubt:
                    self.urteile[nummer] = e["urteil"]

    def seite(self) -> str:
        """Fuellt die Platzhalter der Seite aus der Queue."""
        knoepfe = "".join(
            f'<button class="{"ja" if i == 0 else ("nein" if i == 1 else "")}" '
            f"onclick=\"urteil('{u['wert']}')\">{u['beschriftung']}"
            f"<kbd>{u['taste']}</kbd></button>"
            for i, u in enumerate(self.urteilsliste))
        tasten = json.dumps({u["taste"]: u["wert"] for u in self.urteilsliste})
        return (SEITE.replace("__FRAGE__", self.frage)
                     .replace("__KNOEPFE__", knoepfe)
                     .replace("__TASTEN__", tasten))

    def naechster(self) -> dict:
        with self._sperre:
            offen = [n for n in sorted(self.faelle) if n not in self.urteile]
            gesamt = len(self.faelle)
            if not offen:
                z = {u["wert"]: sum(1 for v in self.urteile.values() if v == u["wert"])
                     for u in self.urteilsliste}
                text = " · ".join(
                    f'{u["beschriftung"]}: {z[u["wert"]]}' for u in self.urteilsliste)
                return {"fertig": True, "gesamt": gesamt, "zusammenfassung": text}
            return {"fertig": False, "nummer": offen[0], "gesamt": gesamt,
                    "clip": bool(self.faelle[offen[0]].get("clip")),
                    "erledigt": len(self.urteile), "revision": len(self.urteile)}

    def bild(self, nummer: int) -> bytes:
        fall = self.faelle.get(nummer)
        if fall is None:
            raise KeyError(nummer)
        pfad = self.queue_root / fall["bild"]
        daten = pfad.read_bytes()
        # Vor jeder Anzeige gegen den gebundenen Hash pruefen: Beurteilt werden
        # muss genau das Bild, das im Lernbestand liegt.
        if sha256_bytes(daten) != fall["bild_sha256"]:
            raise ValueError(f"Bild {nummer} weicht von seinem Hash ab: {pfad}")
        return daten

    def clip(self, nummer: int) -> bytes:
        """Bewegtbild zum Fall. Ohne Clip-Eintrag gibt es nichts auszuliefern."""
        fall = self.faelle.get(nummer)
        if fall is None or not fall.get("clip"):
            raise KeyError(nummer)
        wurzel = self.queue_root.resolve()
        pfad = (self.queue_root / fall["clip"]).resolve()
        # Der Pfad kommt aus einer Datei; er darf die Warteschlange nicht verlassen.
        if not pfad.is_relative_to(wurzel) or pfad.suffix.lower() != ".mp4":
            raise ValueError(f"Clip liegt ausserhalb der Warteschlange: {pfad}")
        return pfad.read_bytes()

    def urteilen(self, nummer: int, urteil: str) -> None:
        if urteil not in self.erlaubt:
            raise ValueError(f"Unzulaessiges Urteil: {urteil!r}")
        with self._sperre:
            if nummer not in self.faelle:
                raise KeyError(nummer)
            self.urteile[nummer] = urteil
            self._schreiben()

    def _schreiben(self) -> None:
        z = {u["wert"]: sum(1 for v in self.urteile.values() if v == u["wert"])
             for u in self.urteilsliste}
        # Erster Wert = "Merkmal sichtbar" (also ein falsches Negativ),
        # zweiter = "nicht sichtbar". Der dritte ist "unsicher" und zaehlt nicht.
        treffer = z[self.urteilsliste[0]["wert"]]
        gewertet = treffer + z[self.urteilsliste[1]["wert"]]
        dokument = {
            "schema": "bcc_negativ_review_v1",
            "frage": self.frage,
            "reviewer": self.reviewer,
            "queue": str(self.queue_root),
            "queue_sha256": self.queue_sha256,
            "lernbestand": self.queue.get("lernbestand"),
            "lernbestand_manifest_sha256": self.queue.get("lernbestand_manifest_sha256"),
            "stichprobe": len(self.faelle),
            "beurteilt": len(self.urteile),
            "vollstaendig": len(self.urteile) == len(self.faelle),
            "zusammenfassung": z,
            "fehlerquote_der_negativen": (
                round(treffer / gewertet, 4) if gewertet else None),
            "hinweis": ("Die Fehlerquote zaehlt unsichere Faelle nicht mit. Sie sagt, wie oft "
                        "ein automatisch erzeugtes Negativbild in Wahrheit einen Bogen zeigt."),
            "urteile": [{"nummer": n, "urteil": self.urteile[n],
                         "bild_sha256": self.faelle[n]["bild_sha256"]}
                        for n in sorted(self.urteile)],
        }
        text = json.dumps(dokument, indent=1, ensure_ascii=False)
        self.output_path.parent.mkdir(parents=True, exist_ok=True)
        fd, temp = tempfile.mkstemp(prefix=f".{self.output_path.name}.", suffix=".tmp",
                                    dir=str(self.output_path.parent))
        try:
            with os.fdopen(fd, "w", encoding="utf-8") as f:
                f.write(text)
            Path(temp).replace(self.output_path)
        except BaseException:
            Path(temp).unlink(missing_ok=True)
            raise


def create_handler(store: NegativReviewStore):
    class Handler(http.server.BaseHTTPRequestHandler):
        def log_message(self, *_args) -> None:  # noqa: D401 - stiller Server
            pass

        def _senden(self, code: int, typ: str, koerper: bytes) -> None:
            self.send_response(code)
            self.send_header("Content-Type", typ)
            self.send_header("Content-Length", str(len(koerper)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(koerper)

        def do_GET(self) -> None:
            if not require_loopback_host(self):
                return
            pfad = urllib.parse.urlparse(self.path).path
            try:
                if pfad == "/":
                    self._senden(200, "text/html; charset=utf-8", store.seite().encode("utf-8"))
                elif pfad == "/naechster":
                    self._senden(200, "application/json; charset=utf-8",
                                 json.dumps(store.naechster()).encode("utf-8"))
                elif pfad.startswith("/bild/"):
                    nummer = int(pfad.rsplit("/", 1)[1])
                    self._senden(200, "image/jpeg", store.bild(nummer))
                elif pfad.startswith("/clip/"):
                    nummer = int(pfad.rsplit("/", 1)[1])
                    self._senden(200, "video/mp4", store.clip(nummer))
                else:
                    self._senden(404, "text/plain; charset=utf-8", b"unbekannt")
            except Exception as fehler:  # sichtbar melden statt still 500
                self._senden(500, "text/plain; charset=utf-8", str(fehler).encode("utf-8"))

        def do_POST(self) -> None:
            if not require_loopback_host(self):
                return
            body = read_json_body(self)
            if body is None:
                return
            if urllib.parse.urlparse(self.path).path != "/urteil":
                self._senden(404, "text/plain; charset=utf-8", b"unbekannt")
                return
            try:
                daten = json.loads(body.decode("utf-8"))
                store.urteilen(int(daten["nummer"]), str(daten["urteil"]))
                self._senden(200, "application/json; charset=utf-8", b'{"ok":true}')
            except Exception as fehler:
                self._senden(400, "text/plain; charset=utf-8", str(fehler).encode("utf-8"))

    return Handler


def run_server(queue_root: Path, output_path: Path, reviewer: str, port: int = 8776,
               browser_oeffnen: bool = True) -> None:
    store = NegativReviewStore(queue_root, output_path, reviewer)
    handler = create_handler(store)

    class Server(socketserver.ThreadingTCPServer):
        """Mehrfaedig — sonst bleibt der Pruefplatz nach dem ersten Bild stehen.

        Chrome und Edge oeffnen vorsorglich Verbindungen, ohne sofort eine
        Anfrage zu senden. Ein einfaediger Server nimmt so eine an, wartet auf
        eine Anfrage, die nie kommt, und blockiert damit alles Weitere. Am
        2026-08-10 genau so aufgetreten: Ein Bild liess sich beurteilen, danach
        ging nichts mehr.

        KEIN allow_reuse_address: Unter Windows erlaubt das, sich auf einen
        bereits belegten Port zu setzen; die Anfragen landen dann beim fremden
        Prozess. Auch das ist am 2026-08-10 passiert.
        """

        daemon_threads = True

    try:
        server = Server(("127.0.0.1", port), handler)
    except OSError:
        server = Server(("127.0.0.1", 0), handler)
        print(f"  Hinweis: Port {port} ist belegt, es wird ein freier verwendet.")

    with server:
        adresse = f"http://127.0.0.1:{server.server_address[1]}/"
        zustand = store.naechster()
        offen = 0 if zustand.get("fertig") else zustand["gesamt"] - zustand["erledigt"]
        print()
        print(f"  Pruefplatz laeuft:  {adresse}")
        print(f"  Bilder: {len(store.faelle)}, offen: {offen}")
        print(f"  Ausgabe: {output_path}")
        print()
        print("  Falls sich kein Browser oeffnet, die Adresse oben von Hand eingeben.")
        print("  Beenden mit Strg+C oder Fenster schliessen.")
        print()
        if browser_oeffnen:
            # Der Server oeffnet den Browser selbst, sobald er lauscht. Das
            # Startskript kann das nicht zuverlaessig: Es weiss nicht, wann der
            # Server bereit ist, und die Anfuehrungszeichen-Verschachtelung in
            # cmd zerlegte den Aufruf still.
            threading.Timer(0.5, lambda: webbrowser.open(adresse)).start()
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Blinde Pruefung der automatischen Negativbilder")
    parser.add_argument("--queue", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--reviewer", required=True)
    parser.add_argument("--port", type=int, default=8776)
    parser.add_argument("--kein-browser", action="store_true")
    args = parser.parse_args(argv)
    run_server(args.queue, args.output, args.reviewer, args.port,
               browser_oeffnen=not args.kein_browser)
    return 0


if __name__ == "__main__":
    sys.exit(main())
