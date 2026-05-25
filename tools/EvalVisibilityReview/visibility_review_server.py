from __future__ import annotations

import argparse
import csv
import json
import mimetypes
import os
import tempfile
import threading
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, quote, urlparse


VALID_LABELS = {"ja", "nein", "unsicher"}
LABEL_ALIASES = {
    "sichtbar": "ja",
    "visible": "ja",
    "yes": "ja",
    "nicht_sichtbar": "nein",
    "nicht sichtbar": "nein",
    "not_visible": "nein",
    "no": "nein",
    "leer_ok": "nein",
}


def normalize_label(value: str | None) -> str | None:
    if value is None:
        return None
    label = value.strip().lower()
    label = LABEL_ALIASES.get(label, label)
    return label if label in VALID_LABELS else None


class VisibilityReviewStore:
    def __init__(self, manifest_path: str | Path, output_path: str | Path):
        self.manifest_path = Path(manifest_path)
        self.output_path = Path(output_path)
        self._lock = threading.Lock()
        self.rows = self._load_manifest()
        self.fieldnames = self._build_fieldnames()
        self._merge_existing_output()

    def _load_manifest(self) -> list[dict[str, str]]:
        if not self.manifest_path.exists():
            raise FileNotFoundError(f"Manifest nicht gefunden: {self.manifest_path}")
        with self.manifest_path.open("r", newline="", encoding="utf-8-sig") as handle:
            rows = [dict(row) for row in csv.DictReader(handle)]
        for row in rows:
            row.setdefault("visibility_label", "")
            row.setdefault("comment", "")
            row.setdefault("reviewed_at", "")
        return rows

    def _build_fieldnames(self) -> list[str]:
        names: list[str] = []
        for row in self.rows:
            for key in row.keys():
                if key not in names:
                    names.append(key)
        for required in ["visibility_label", "comment", "reviewed_at"]:
            if required not in names:
                names.append(required)
        return names

    def _merge_existing_output(self) -> None:
        if not self.output_path.exists():
            return
        with self.output_path.open("r", newline="", encoding="utf-8-sig") as handle:
            existing = {
                row.get("image_name", ""): dict(row)
                for row in csv.DictReader(handle)
                if row.get("image_name")
            }
        for row in self.rows:
            old = existing.get(row.get("image_name", ""))
            if not old:
                continue
            for key in ["visibility_label", "comment", "reviewed_at"]:
                row[key] = old.get(key, row.get(key, ""))

    def state(self) -> dict[str, object]:
        with self._lock:
            total = len(self.rows)
            done = sum(1 for row in self.rows if normalize_label(row.get("visibility_label")))
            counts = {label: 0 for label in sorted(VALID_LABELS)}
            for row in self.rows:
                label = normalize_label(row.get("visibility_label"))
                if label:
                    counts[label] += 1
            current = self._first_open_row() or (self.rows[0] if self.rows else None)
            return {
                "total": total,
                "done": done,
                "open": total - done,
                "counts": counts,
                "current": self._public_row(current) if current else None,
                "items": [self._public_row(row) for row in self.rows],
            }

    def _first_open_row(self) -> dict[str, str] | None:
        for row in self.rows:
            if not normalize_label(row.get("visibility_label")):
                return row
        return None

    def _public_row(self, row: dict[str, str] | None) -> dict[str, str] | None:
        if row is None:
            return None
        image_name = row.get("image_name", "")
        public = {
            "image_name": image_name,
            "expected_code": row.get("expected_code", ""),
            "router_class": row.get("router_class", ""),
            "visibility_label": row.get("visibility_label", ""),
            "comment": row.get("comment", ""),
            "reviewed_at": row.get("reviewed_at", ""),
            "image_url": f"/image?name={quote(image_name)}",
        }
        return public

    def set_label(self, image_name: str, label_value: str, comment: str = "") -> dict[str, object]:
        label = normalize_label(label_value)
        if label is None:
            raise ValueError(f"Ungueltiges Label: {label_value}")
        with self._lock:
            row = self._find_row(image_name)
            row["visibility_label"] = label
            row["comment"] = comment.strip()
            row["reviewed_at"] = datetime.now(timezone.utc).isoformat()
            self._write_output_locked()
        return self.state()

    def _find_row(self, image_name: str) -> dict[str, str]:
        for row in self.rows:
            if row.get("image_name") == image_name:
                return row
        raise KeyError(f"Bild nicht im Manifest: {image_name}")

    def image_path_for(self, image_name: str) -> Path:
        row = self._find_row(image_name)
        path = Path(row.get("image_path", ""))
        if not path.exists():
            raise FileNotFoundError(f"Bild nicht gefunden: {path}")
        return path

    def _write_output_locked(self) -> None:
        self.output_path.parent.mkdir(parents=True, exist_ok=True)
        fd, temp_name = tempfile.mkstemp(
            prefix=self.output_path.name + ".",
            suffix=".tmp",
            dir=str(self.output_path.parent),
        )
        os.close(fd)
        temp_path = Path(temp_name)
        try:
            with temp_path.open("w", newline="", encoding="utf-8-sig") as handle:
                writer = csv.DictWriter(handle, fieldnames=self.fieldnames)
                writer.writeheader()
                for row in self.rows:
                    writer.writerow({name: row.get(name, "") for name in self.fieldnames})
            temp_path.replace(self.output_path)
        finally:
            if temp_path.exists():
                temp_path.unlink()


def make_handler(store: VisibilityReviewStore):
    class VisibilityReviewHandler(BaseHTTPRequestHandler):
        server_version = "SewerStudioVisibilityReview/1.0"

        def do_GET(self) -> None:  # noqa: N802
            parsed = urlparse(self.path)
            if parsed.path == "/":
                self._send_html(INDEX_HTML)
                return
            if parsed.path == "/api/state":
                self._send_json(store.state())
                return
            if parsed.path == "/image":
                name = parse_qs(parsed.query).get("name", [""])[0]
                self._send_image(name)
                return
            self.send_error(404, "Nicht gefunden")

        def do_POST(self) -> None:  # noqa: N802
            parsed = urlparse(self.path)
            if parsed.path != "/api/label":
                self.send_error(404, "Nicht gefunden")
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                payload = json.loads(self.rfile.read(length).decode("utf-8"))
                state = store.set_label(
                    str(payload.get("image_name", "")),
                    str(payload.get("visibility_label", "")),
                    str(payload.get("comment", "")),
                )
                self._send_json(state)
            except Exception as exc:  # pragma: no cover - defensive server path
                self._send_json({"error": str(exc)}, status=400)

        def log_message(self, format: str, *args: object) -> None:
            return

        def _send_html(self, text: str) -> None:
            body = text.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def _send_json(self, data: object, status: int = 200) -> None:
            body = json.dumps(data, ensure_ascii=False).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def _send_image(self, image_name: str) -> None:
            try:
                path = store.image_path_for(image_name)
                body = path.read_bytes()
                content_type = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
                self.send_response(200)
                self.send_header("Content-Type", content_type)
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            except Exception as exc:  # pragma: no cover - defensive server path
                self.send_error(404, str(exc))

    return VisibilityReviewHandler


INDEX_HTML = r"""<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<title>SewerStudio Sichtbarkeits-Review</title>
<style>
html, body { margin: 0; height: 100%; background: #090b0d; color: #f1f5f9; font-family: Segoe UI, Arial, sans-serif; }
body { display: grid; grid-template-rows: auto 1fr auto; }
header, footer { background: #171b21; border-color: #2e3440; padding: 12px 18px; }
header { border-bottom: 1px solid #2e3440; }
footer { border-top: 1px solid #2e3440; display: flex; gap: 10px; align-items: center; justify-content: center; flex-wrap: wrap; }
h1 { font-size: 18px; margin: 0 0 6px 0; }
.status { color: #aeb7c2; font-size: 14px; }
main { display: grid; grid-template-columns: 1fr 360px; min-height: 0; }
.stage { display: flex; align-items: center; justify-content: center; min-width: 0; min-height: 0; padding: 10px; }
img { max-width: 100%; max-height: calc(100vh - 180px); object-fit: contain; background: #000; border: 1px solid #2e3440; }
.side { border-left: 1px solid #2e3440; background: #11151a; padding: 16px; display: grid; align-content: start; gap: 12px; }
.code { font-size: 24px; font-weight: 700; }
.muted { color: #9aa4b2; font-size: 13px; word-break: break-all; }
button { border: 1px solid #3b4655; background: #242b35; color: #f1f5f9; border-radius: 6px; padding: 12px 18px; font-size: 16px; cursor: pointer; }
button:hover { background: #303947; }
button:disabled { opacity: .55; cursor: wait; }
.yes { background: #166534; border-color: #22c55e; }
.no { background: #7f1d1d; border-color: #ef4444; }
.maybe { background: #7c5a12; border-color: #f59e0b; }
.nav { font-size: 14px; padding: 8px 12px; }
.small { font-size: 13px; padding: 8px 10px; }
textarea { width: 100%; box-sizing: border-box; min-height: 80px; background: #0b0f14; color: #f1f5f9; border: 1px solid #3b4655; border-radius: 6px; padding: 8px; resize: vertical; }
.pill { display: inline-block; padding: 4px 8px; border-radius: 999px; border: 1px solid #3b4655; background: #202733; }
@media (max-width: 900px) { main { grid-template-columns: 1fr; } .side { border-left: 0; border-top: 1px solid #2e3440; } }
</style>
</head>
<body>
<header>
  <h1>SewerStudio Sichtbarkeits-Review</h1>
  <div class="status" id="status">Lade...</div>
</header>
<main>
  <section class="stage">
    <img id="photo" alt="Review-Bild">
  </section>
  <aside class="side">
    <div class="code" id="code">-</div>
    <div><span class="pill" id="klass">-</span></div>
    <div class="muted" id="name">-</div>
    <div class="muted" id="currentLabel">Noch nicht bewertet</div>
    <label>
      Kommentar optional
      <textarea id="comment"></textarea>
    </label>
    <div class="muted">
      Frage: Ist der Schaden im Einzelbild wirklich sichtbar?<br>
      1 = Ja, 2 = Nein, 3 = Unsicher
    </div>
  </aside>
</main>
<footer>
  <button class="nav" onclick="previousImage()">Zurueck</button>
  <button class="yes" onclick="labelCurrent('ja')">1 Ja sichtbar</button>
  <button class="no" onclick="labelCurrent('nein')">2 Nein</button>
  <button class="maybe" onclick="labelCurrent('unsicher')">3 Unsicher</button>
  <button class="nav" onclick="nextImage()">Weiter</button>
  <button class="nav small" onclick="labelCurrent('unsicher')">Haengt? Unsicher + weiter</button>
</footer>
<script>
let items = [];
let index = 0;
let busy = false;
let imageTimer = null;

async function loadState() {
  try {
    const res = await fetch('/api/state');
    const state = await res.json();
    items = state.items || [];
    const firstOpen = items.findIndex(x => !x.visibility_label);
    index = firstOpen >= 0 ? firstOpen : 0;
    render(state);
  } catch (err) {
    document.getElementById('status').textContent = 'Server nicht erreichbar. Seite neu laden.';
  }
}

function render(state) {
  if (!items.length) {
    document.getElementById('status').textContent = 'Keine Bilder gefunden.';
    return;
  }
  const item = items[index];
  const photo = document.getElementById('photo');
  photo.onload = () => {
    if (imageTimer) clearTimeout(imageTimer);
    const done = items.filter(x => x.visibility_label).length;
    document.getElementById('status').textContent =
      `Bild ${index + 1} / ${items.length} | bewertet: ${done} | offen: ${items.length - done}`;
  };
  photo.onerror = () => {
    if (imageTimer) clearTimeout(imageTimer);
    document.getElementById('status').textContent =
      `Bild kann nicht geladen werden: ${item.image_name}. Druecke 3 fuer Unsicher.`;
  };
  if (imageTimer) clearTimeout(imageTimer);
  imageTimer = setTimeout(() => {
    document.getElementById('status').textContent =
      `Bild laedt langsam: ${item.image_name}. Wenn es haengt, druecke 3.`;
  }, 5000);
  photo.src = item.image_url;
  document.getElementById('code').textContent = item.expected_code || '-';
  document.getElementById('klass').textContent = item.router_class || '-';
  document.getElementById('name').textContent = item.image_name || '-';
  document.getElementById('comment').value = item.comment || '';
  document.getElementById('currentLabel').textContent = item.visibility_label
    ? 'Bewertung: ' + item.visibility_label
    : 'Noch nicht bewertet';
  const done = items.filter(x => x.visibility_label).length;
  document.getElementById('status').textContent =
    `Lade Bild ${index + 1} / ${items.length} | bewertet: ${done} | offen: ${items.length - done}`;
}

async function labelCurrent(label) {
  if (busy) return;
  busy = true;
  setButtonsDisabled(true);
  const item = items[index];
  const comment = document.getElementById('comment').value || '';
  document.getElementById('status').textContent = 'Speichere Bewertung...';
  try {
    const res = await fetch('/api/label', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({image_name: item.image_name, visibility_label: label, comment})
    });
    const state = await res.json();
    if (state.error) {
      alert(state.error);
      return;
    }
    items = state.items || items;
    const nextOpen = items.findIndex((x, i) => i > index && !x.visibility_label);
    if (nextOpen >= 0) index = nextOpen;
    else if (index < items.length - 1) index++;
    render(state);
  } catch (err) {
    document.getElementById('status').textContent = 'Speichern fehlgeschlagen. Bitte F5 druecken.';
  } finally {
    busy = false;
    setButtonsDisabled(false);
  }
}

function nextImage() {
  if (index < items.length - 1) index++;
  render({});
}

function previousImage() {
  if (index > 0) index--;
  render({});
}

document.addEventListener('keydown', e => {
  if (e.target && e.target.tagName === 'TEXTAREA') return;
  if (busy) return;
  if (e.key === '1' || e.key.toLowerCase() === 'j') labelCurrent('ja');
  if (e.key === '2' || e.key.toLowerCase() === 'n') labelCurrent('nein');
  if (e.key === '3' || e.key.toLowerCase() === 'u') labelCurrent('unsicher');
  if (e.key === 'ArrowRight') nextImage();
  if (e.key === 'ArrowLeft') previousImage();
});

function setButtonsDisabled(disabled) {
  document.querySelectorAll('button').forEach(btn => btn.disabled = disabled);
}

loadState();
</script>
</body>
</html>
"""


def run_server(manifest: Path, output: Path, port: int) -> None:
    store = VisibilityReviewStore(manifest, output)
    server = ThreadingHTTPServer(("127.0.0.1", port), make_handler(store))
    url = f"http://127.0.0.1:{port}/"
    print(f"Sichtbarkeits-Review laeuft: {url}")
    print(f"Eingabe: {manifest}")
    print(f"Ausgabe: {output}")
    print("Stoppen mit Strg+C")
    server.serve_forever()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="SewerStudio Eval-Sichtbarkeits-Review")
    default_manifest = Path.cwd() / "EvalVisibilityReview_20260525" / "review_manifest.csv"
    parser.add_argument("--manifest", default=str(default_manifest), help="review_manifest.csv")
    parser.add_argument("--output", default="", help="Ziel-CSV, default: visibility_labels.csv neben manifest")
    parser.add_argument("--port", type=int, default=8771)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    manifest = Path(args.manifest)
    output = Path(args.output) if args.output else manifest.with_name("visibility_labels.csv")
    run_server(manifest, output, args.port)


if __name__ == "__main__":
    main()
