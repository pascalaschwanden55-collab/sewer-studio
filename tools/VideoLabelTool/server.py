"""
Video-Scrub-Label-Werkzeug — lokaler Server.

Zweck: Pro Protokoll-Befund (Haltung, Video-Zeit, VSA-Code) springt der Browser ins
Video, du scrubbst zur Stelle, wo der Schaden WIRKLICH sichtbar ist, greifst genau
diesen Frame und bestaetigst den Code. Ergebnis = sauberer Gold-Satz (statt Reparatur
am kaputten, am Protokoll-Meter geschnittenen Frame).

Technik:
- Der Server schneidet pro Befund mit ffmpeg ein kurzes Fenster (Zeit +/- WIN s) als
  browser-faehiges H.264-mp4 (schnell, exakt scrubbbar, geht auch fuer .mpg/.avi/.wmv).
- Der gespeicherte Frame ist das, was du im Bild siehst (Canvas-Capture, WYSIWYG) —
  keine KI-Aufwertung, nur ggf. faithful Deinterlace (TV-Quelle).
- KEINE Label-Erfindung durch die Maschine: nur DU bestaetigst den Code. "kein Befund
  im Fenster" -> LEER. Unsicher -> getrennt abgelegt, nicht ins Gold.

Start:
  python tools/VideoLabelTool/server.py            # dann http://localhost:8200 oeffnen
  python tools/VideoLabelTool/server.py --priority C:/tmp/clean_retrain/priority.json
"""
import argparse
from collections import deque
import glob
import json
import os
import re
import secrets
import shutil
import subprocess
import sys
import threading
import time
import urllib.parse
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
VIDEO_ROOT = r"D:\Haltungen"
DEFAULT_DATASET = r"C:\KI_BRAIN\yolo_vsa_cls_dataset_v3_bal"
FALLBACK_DATASET = r"C:\KI_BRAIN\yolo_vsa_cls_dataset_bal"
DATASET = os.environ.get("SEWERSTUDIO_VIDEO_LABEL_DATASET") or (
    DEFAULT_DATASET if os.path.isdir(os.path.join(DEFAULT_DATASET, "train")) else FALLBACK_DATASET
)
GOLD_ROOT = r"C:\KI_BRAIN\gold_labels"
CLIP_CACHE = r"C:\tmp\video_label_clips"
DEFAULT_CLASSES = ["ALL"]
DEFAULT_DEDUPE_WINDOW_SECONDS = 20.0
PREFERRED_CLASS_ORDER = [
    "BCA", "BCC", "BCD", "BCE",
    "BAB", "BAF", "BAI", "BAJ",
    "BBA", "BBB", "BDA", "BDD", "LEER",
]
# browser-native zuerst; der Rest wird transkodiert (Clip ist sowieso re-encoded)
VIDEO_EXT = (".mp4", ".m4v", ".mov", ".mpg", ".mpeg", ".avi", ".wmv", ".mkv", ".mp2")
NAME_RE = re.compile(r"^(.+?)_([0-9.]+)s_([A-Za-z][A-Za-z0-9]*)(?:_t[+-]\d+)?\.png$")

KLARTEXT = {
    "BAI": "Einragendes Dichtungsmaterial", "BBA": "Wurzeln / Bewuchs", "BAB": "Riss",
    "BAJ": "Verschobene Rohrverbindung (Versatz)", "BDD": "Wasserspiegel / Wasserstand",
    "BCD": "Rohranfang", "BCE": "Rohrende", "BCA": "Seitlicher Anschluss", "BCC": "Bogen",
    "BDA": "Abfluss / Wasser", "BAF": "Oberflaechenschaden / Korrosion", "BAA": "Verformung",
    "BAC": "Bruch", "BAH": "Schadhafter Anschluss", "BBB": "Anhaftende Stoffe / Inkrustation",
    "BBC": "Ablagerung", "LEER": "kein Schaden (leeres Rohr)",
}
FFMPEG = shutil.which("ffmpeg") or "ffmpeg"
SIDECAR_URL = (os.environ.get("SEWER_SIDECAR_URL") or "http://127.0.0.1:8100").rstrip("/")
MAX_POST_BYTES = 24 * 1024 * 1024
VIDEO_LABEL_TOKEN = (os.environ.get("SEWERSTUDIO_VIDEO_LABEL_TOKEN") or secrets.token_urlsafe(32)).strip()


def is_allowed_local_host(value):
    host = (value or "").strip().lower()
    if not host:
        return False
    if host.startswith("[::1]"):
        return True
    host = host.split(":", 1)[0]
    return host in ("localhost", "127.0.0.1", "::1")


def is_allowed_same_origin(value):
    if not value:
        return True
    try:
        parsed = urllib.parse.urlparse(value)
    except Exception:
        return False
    return parsed.scheme in ("http", "https") and is_allowed_local_host(parsed.netloc)


def require_post_auth(handler):
    if not is_allowed_local_host(handler.headers.get("Host", "")):
        return False, "ungueltiger Host"
    if handler.headers.get("X-Video-Label-Token", "") != VIDEO_LABEL_TOKEN:
        return False, "ungueltiges Token"
    if not is_allowed_same_origin(handler.headers.get("Origin")):
        return False, "ungueltiger Origin"
    if not is_allowed_same_origin(handler.headers.get("Referer")):
        return False, "ungueltiger Referer"
    return True, ""


def sidecar_token():
    """X-Sidecar-Token: env -> geteilte Token-Datei (%LOCALAPPDATA%/SewerStudio/.sidecar_token)."""
    t = (os.environ.get("SEWER_SIDECAR_AUTH_TOKEN") or "").strip()
    if t:
        return t
    base = os.environ.get("LOCALAPPDATA") or os.path.expanduser("~")
    try:
        return open(os.path.join(base, "SewerStudio", ".sidecar_token"), encoding="utf-8").read().strip()
    except OSError:
        return ""


def sam_segment(frame_b64, box_px, label):
    """Sidecar /segment/sam mit EINER Pixel-Box [x1,y1,x2,y2]. SAM maskiert nur — entscheidet NICHTS."""
    payload = {
        "image_base64": frame_b64.split(",")[-1],
        "bounding_boxes": [{"x1": box_px[0], "y1": box_px[1], "x2": box_px[2], "y2": box_px[3],
                            "label": label or "schaden", "confidence": 1.0}],
        "pipe_diameter_mm": None,
    }
    req = urllib.request.Request(
        SIDECAR_URL + "/segment/sam", json.dumps(payload).encode("utf-8"),
        {"Content-Type": "application/json", "X-Sidecar-Token": sidecar_token()})
    with urllib.request.urlopen(req, timeout=90) as r:
        return json.loads(r.read().decode("utf-8"))


def rle_decode(rle, h, w):
    """SAM-RLE 'start_value,run1,run2,...' (row-major) -> uint8-Maske (h,w)."""
    import numpy as np
    parts = rle.split(",")
    val = int(parts[0])
    runs = [int(x) for x in parts[1:]]
    flat = np.zeros(int(sum(runs)), dtype=np.uint8)
    i = 0
    for run in runs:
        if val:
            flat[i:i + run] = 1
        i += run
        val ^= 1
    return flat[:h * w].reshape(h, w)


def mask_overlay_b64(mask):
    """Halbtransparentes gruenes Overlay-PNG (RGBA) aus der Maske -> data-URL."""
    import base64
    import io
    import numpy as np
    from PIL import Image
    h, w = mask.shape
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[mask.astype(bool)] = (0, 235, 0, 120)
    buf = io.BytesIO()
    Image.fromarray(rgba, "RGBA").save(buf, "PNG")
    return "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode("ascii")


_state_lock = threading.Lock()
_clip_locks = {}             # out-Pfad -> Lock (verhindert doppeltes/halbes Kodieren)
_clip_locks_guard = threading.Lock()
FINDINGS = {}        # key -> dict
ORDER = []           # gefilterte/sortierte key-Liste
VIDEO_FOR = {}       # haltung -> abspath (oder None)


def kt(code):
    return KLARTEXT.get(code, KLARTEXT.get(code[:3], code))


def resolve_video(haltung):
    """Bestes Video im Haltungsordner: mp4 bevorzugt, Nachbefahrung/Gegen hintan."""
    folder = os.path.join(VIDEO_ROOT, haltung)
    if not os.path.isdir(folder):
        return None
    cands = [p for p in glob.glob(os.path.join(folder, "*"))
             if os.path.splitext(p)[1].lower() in VIDEO_EXT]
    if not cands:
        return None

    def score(p):
        n = os.path.basename(p).lower()
        ext = os.path.splitext(p)[1].lower()
        return (
            0 if ext == ".mp4" else 1,                       # mp4 zuerst
            1 if ("nachbefahr" in n or n.endswith("g.mp4")) else 0,  # Erstbefahrung zuerst
            -os.path.getsize(p),                              # groesseres Video zuerst
        )
    return sorted(cands, key=score)[0]


def discover_classes(dataset):
    train = os.path.join(dataset, "train")
    if not os.path.isdir(train):
        return ["BAB"]
    available = []
    for p in glob.glob(os.path.join(train, "*")):
        if os.path.isdir(p) and glob.glob(os.path.join(p, "*.png")):
            available.append(os.path.basename(p).upper())
    preferred = [c for c in PREFERRED_CLASS_ORDER if c in available]
    rest = sorted(c for c in available if c not in preferred)
    return preferred + rest


def parse_classes(value, dataset):
    classes = []
    for part in (value or "").split(","):
        code = part.strip().upper()
        if not code:
            continue
        if code in ("ALL", "*", "MIX"):
            return discover_classes(dataset)
        if not re.fullmatch(r"[A-Z0-9]{2,8}", code):
            raise ValueError(f"ungueltige Klasse: {code}")
        classes.append(code)
    return classes or discover_classes(dataset)


def dedupe_near_duplicates(keys, findings, window_seconds=DEFAULT_DEDUPE_WINDOW_SECONDS):
    """Pro Klasse/Haltung/Code nur einen Kandidaten je Zeitfenster behalten."""
    if window_seconds <= 0:
        return list(keys)

    buckets = {}
    bucket_order = []
    for key in keys:
        f = findings[key]
        bucket = (f["klass"], f["haltung"], f["code"])
        if bucket not in buckets:
            buckets[bucket] = []
            bucket_order.append(bucket)
        buckets[bucket].append(key)

    kept = []
    for bucket in bucket_order:
        bucket_keys = buckets[bucket]
        bucket_keys.sort(key=lambda k: (
            0 if findings[k]["video_available"] else 1,
            findings[k]["zeit"],
            findings[k]["id"],
        ))
        last_time = None
        for key in bucket_keys:
            current_time = findings[key]["zeit"]
            if last_time is None or current_time - last_time >= window_seconds:
                kept.append(key)
                last_time = current_time
    return kept


def interleave_by_holding(keys, findings):
    """Innerhalb einer Klasse Haltungen abwechseln statt Serien aus derselben Haltung zu zeigen."""
    buckets = {}
    for key in keys:
        haltung = findings[key]["haltung"]
        buckets.setdefault(haltung, []).append(key)

    for haltung, haltung_keys in buckets.items():
        haltung_keys.sort(key=lambda k: (
            0 if findings[k]["video_available"] else 1,
            findings[k]["zeit"],
            findings[k]["code"],
            findings[k]["id"],
        ))
        buckets[haltung] = deque(haltung_keys)

    holding_order = sorted(
        buckets,
        key=lambda h: (
            0 if any(findings[k]["video_available"] for k in buckets[h]) else 1,
            h,
        ),
    )

    mixed = []
    while any(buckets[h] for h in holding_order):
        for haltung in holding_order:
            if buckets[haltung]:
                mixed.append(buckets[haltung].popleft())
    return mixed


def interleave_by_class(keys, findings, classes):
    buckets = {}
    for key in keys:
        cls = findings[key]["klass"]
        buckets.setdefault(cls, []).append(key)
    for cls, cls_keys in buckets.items():
        buckets[cls] = deque(interleave_by_holding(cls_keys, findings))

    class_order = [c for c in classes if c in buckets]
    class_order += sorted(c for c in buckets if c not in class_order)

    mixed = []
    while any(buckets[c] for c in class_order):
        for cls in class_order:
            if buckets[cls]:
                mixed.append(buckets[cls].popleft())
    return mixed


def build_findings(priority_path, limit, classes, dedupe_window=DEFAULT_DEDUPE_WINDOW_SECONDS):
    findings = {}
    for cls in classes:
        for p in glob.glob(os.path.join(DATASET, "train", cls, "*.png")):
            m = NAME_RE.match(os.path.basename(p))
            if not m:
                continue
            hal, zeit, code = m.group(1), m.group(2), m.group(3)
            key = f"{hal}|{zeit}|{code}"
            if key not in findings:
                findings[key] = {"id": key, "haltung": hal, "zeit": float(zeit), "zeit_str": zeit,
                                 "code": code, "klass": cls, "klartext": kt(code)}
    # Video je Haltung aufloesen (gecacht)
    for f in findings.values():
        hal = f["haltung"]
        if hal not in VIDEO_FOR:
            VIDEO_FOR[hal] = resolve_video(hal)
        v = VIDEO_FOR[hal]
        f["video_available"] = bool(v)
        f["video_name"] = os.path.basename(v) if v else None

    # Reihenfolge: optionale Prioritaetsliste (z.B. vom Clean-Report entfernte Befunde) zuerst
    prio = []
    if priority_path and os.path.isfile(priority_path):
        with open(priority_path, encoding="utf-8") as fh:
            prio = [k for k in json.load(fh) if k in findings]
    prio_set = set(prio)
    # Rest: Klassen mischen, damit nicht hunderte Risse am Stueck kommen.
    rest = [k for k in findings if k not in prio_set]
    rest = dedupe_near_duplicates(rest, findings, dedupe_window)
    order = prio + interleave_by_class(rest, findings, classes)
    # Befunde ohne Video ans Ende (kann man eh nicht reviewen)
    order.sort(key=lambda k: 0 if findings[k]["video_available"] else 1)
    if limit:
        order = order[:limit]
    return findings, order


def ensure_clip(key, win):
    """ffmpeg-Fenster um den Befund als kleines mp4; gecacht. Gibt (clip_path, clip_start) zurueck."""
    f = FINDINGS[key]
    video = VIDEO_FOR.get(f["haltung"])
    if not video or not os.path.isfile(video):
        return None, None
    start = max(0.0, f["zeit"] - win)
    dur = win * 2
    os.makedirs(CLIP_CACHE, exist_ok=True)
    safe = re.sub(r"[^0-9A-Za-z._-]", "_", f"{f['haltung']}_{start:.1f}_{dur:.0f}")
    out = os.path.join(CLIP_CACHE, safe + ".mp4")
    if os.path.isfile(out) and os.path.getsize(out) > 1024:
        return out, start
    # Per-Clip-Lock: kein paralleles/halbes Kodieren; atomar via tmp -> os.replace
    with _clip_locks_guard:
        lock = _clip_locks.setdefault(out, threading.Lock())
    with lock:
        if os.path.isfile(out) and os.path.getsize(out) > 1024:
            return out, start
        tmp = f"{out}.part{os.getpid()}_{threading.get_ident()}.mp4"   # .mp4-Endung: ffmpeg erkennt Muxer
        cmd = [FFMPEG, "-hide_banner", "-loglevel", "error", "-ss", f"{start:.3f}", "-i", video,
               "-t", f"{dur:.3f}", "-vf", "yadif=deint=interlaced", "-c:v", "libx264",
               "-preset", "veryfast", "-crf", "16", "-pix_fmt", "yuv420p",
               "-movflags", "+faststart", "-an", "-f", "mp4", "-y", tmp]
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0 or not os.path.isfile(tmp) or os.path.getsize(tmp) <= 1024:
            sys.stderr.write(f"[clip] ffmpeg-Fehler {key}: {r.stderr[:300]}\n")
            try:
                os.remove(tmp)
            except OSError:
                pass
            return None, None
        os.replace(tmp, out)   # atomar
    return out, start


def ledger_keys():
    p = os.path.join(GOLD_ROOT, "gold_ledger.jsonl")
    done = {}
    if os.path.isfile(p):
        with open(p, encoding="utf-8") as fh:
            for line in fh:
                try:
                    r = json.loads(line)
                    done[r["key"]] = r.get("decision", "")
                except Exception:
                    pass
    return done


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def _send(self, code, body, ctype="application/json", extra=None):
        if isinstance(body, (dict, list)):
            body = json.dumps(body, ensure_ascii=False).encode("utf-8")
        elif isinstance(body, str):
            body = body.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        for k, v in (extra or {}).items():
            self.send_header(k, v)
        self.end_headers()
        if self.command != "HEAD":
            self.wfile.write(body)

    def do_GET(self):
        u = urllib.parse.urlparse(self.path)
        q = urllib.parse.parse_qs(u.query)
        if u.path in ("/", "/index.html"):
            with open(os.path.join(HERE, "app.html"), "rb") as fh:
                self._send(200, fh.read(), "text/html; charset=utf-8")
        elif u.path == "/session.json":
            self._send(200, {"video_label_token": VIDEO_LABEL_TOKEN}, extra={"Cache-Control": "no-store"})
        elif u.path == "/findings.json":
            done = ledger_keys()
            items = [{**FINDINGS[k], "decision": done.get(k, "")} for k in ORDER]
            self._send(200, {"count": len(items), "items": items, "klartext": KLARTEXT})
        elif u.path == "/clip":
            self.serve_clip(q)
        elif u.path == "/trainframe":
            self.serve_trainframe(q)
        else:
            self._send(404, {"error": "not found"})

    def serve_trainframe(self, q):
        """Der originale, am Protokoll-Meter geschnittene Trainings-Frame (Kontext)."""
        key = (q.get("key") or [""])[0]
        if key not in FINDINGS:
            return self._send(404, {"error": "unknown key"})
        f = FINDINGS[key]
        d = os.path.join(DATASET, "train", f["klass"])
        esc = glob.escape(f"{f['haltung']}_{f['zeit_str']}s_{f['code']}")
        cand = None
        for off in ("_t+0", "_t-2", "_t+2", ""):
            hits = glob.glob(os.path.join(d, esc + glob.escape(off) + ".png"))
            if hits:
                cand = hits[0]
                break
        if not cand:
            hits = glob.glob(os.path.join(d, esc + "*.png"))
            cand = hits[0] if hits else None
        if not cand or not os.path.isfile(cand):
            return self._send(404, {"error": "kein Trainingsframe"})
        with open(cand, "rb") as fh:
            self._send(200, fh.read(), "image/png", {"Cache-Control": "max-age=300"})

    def serve_clip(self, q):
        key = (q.get("key") or [""])[0]
        try:
            win = float((q.get("win") or ["10"])[0])
        except ValueError:
            win = 10.0
        win = max(1.0, min(60.0, win))
        if key not in FINDINGS:
            return self._send(404, {"error": "unknown key"})
        clip, start = ensure_clip(key, win)
        if not clip:
            return self._send(404, {"error": "kein Video / Clip fehlgeschlagen"})
        # Range-faehiges Ausliefern (das <video>-Element verlangt Range)
        size = os.path.getsize(clip)
        rng = self.headers.get("Range")
        hdr = {"Accept-Ranges": "bytes", "X-Clip-Start": f"{start:.3f}",
               "Cache-Control": "no-store"}
        if rng:
            m = re.match(r"bytes=(\d*)-(\d*)", rng)
            s = int(m.group(1)) if m and m.group(1) else 0
            e = int(m.group(2)) if m and m.group(2) else size - 1
            e = min(e, size - 1)
            s = min(s, e)
            length = e - s + 1
            self.send_response(206)
            self.send_header("Content-Type", "video/mp4")
            self.send_header("Content-Range", f"bytes {s}-{e}/{size}")
            self.send_header("Content-Length", str(length))
            for k, v in hdr.items():
                self.send_header(k, v)
            self.end_headers()
            with open(clip, "rb") as fh:
                fh.seek(s)
                remaining = length
                while remaining > 0:
                    chunk = fh.read(min(262144, remaining))
                    if not chunk:
                        break
                    self.wfile.write(chunk)
                    remaining -= len(chunk)
        else:
            self.send_response(200)
            self.send_header("Content-Type", "video/mp4")
            self.send_header("Content-Length", str(size))
            for k, v in hdr.items():
                self.send_header(k, v)
            self.end_headers()
            with open(clip, "rb") as fh:
                shutil.copyfileobj(fh, self.wfile)

    def do_HEAD(self):
        self.do_GET()

    def do_POST(self):
        u = urllib.parse.urlparse(self.path)
        if u.path not in ("/save", "/segment"):
            return self._send(404, {"error": "not found"})

        ok, reason = require_post_auth(self)
        if not ok:
            return self._send(403, {"error": reason})

        try:
            n = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            return self._send(400, {"error": "ungueltige Content-Length"})
        if n > MAX_POST_BYTES:
            return self._send(413, {"error": "Payload zu gross"})
        try:
            data = json.loads(self.rfile.read(n).decode("utf-8"))
        except Exception as ex:
            return self._send(400, {"error": f"bad json: {ex}"})
        if (data.get("key") or "") not in FINDINGS:
            return self._send(400, {"error": "unknown key"})
        if u.path == "/segment":
            return self.handle_segment(data)
        return self.handle_save(data)

    def handle_segment(self, data):
        """Box (Pixel) -> Sidecar SAM -> Maske als Overlay-PNG + RLE. SAM entscheidet NICHTS, der Mensch boxt."""
        key = data["key"]
        frame = data.get("frame_png_b64") or ""
        box = data.get("box_px")
        if not frame or not box or len(box) != 4:
            return self._send(400, {"error": "frame_png_b64 oder box_px fehlt"})
        label = data.get("code") or FINDINGS[key]["code"]
        try:
            resp = sam_segment(frame, [float(v) for v in box], label)
        except Exception as ex:
            return self._send(502, {"error": f"Sidecar/SAM nicht erreichbar: {ex}"})
        masks = resp.get("masks") or []
        if not masks:
            return self._send(200, {"ok": False, "degraded": resp.get("degraded", False),
                                    "error": "SAM lieferte keine Maske (Box ausserhalb Bild / Null-Flaeche?)"})
        m = masks[0]
        iw, ih = int(resp.get("image_width", 0)), int(resp.get("image_height", 0))
        try:
            overlay = mask_overlay_b64(rle_decode(m["mask_rle"], ih, iw))
        except Exception as ex:
            return self._send(500, {"error": f"Maske dekodieren fehlgeschlagen: {ex}"})
        return self._send(200, {"ok": True, "mask_png_b64": overlay, "mask_rle": m["mask_rle"],
                                "image_width": iw, "image_height": ih,
                                "area_pixels": m.get("mask_area_pixels"), "score": m.get("confidence"),
                                "centroid": [m.get("centroid_x"), m.get("centroid_y")],
                                "degraded": resp.get("degraded", False)})

    def handle_save(self, data):
        key = data["key"]
        f = FINDINGS[key]
        decision = data.get("decision", "")          # confirm | empty | unsure | skip
        chosen_code = (data.get("chosen_code") or "").strip().upper() or "LEER"
        chosen_time = float(data.get("chosen_time", f["zeit"]))
        note = data.get("note", "")
        image_file = ""
        # Bild nur bei confirm/empty mit Capture speichern; unsure/skip ohne Bild ok
        b64 = data.get("image_png_b64", "")
        if b64 and decision in ("confirm", "empty"):
            import base64
            # Leitplanke: Unterordner aus Code STRIKT saniert -> kein Pfad-Traversal, niemals ausserhalb GOLD_ROOT
            raw = chosen_code if decision == "confirm" else "LEER"
            sub = re.sub(r"[^A-Z0-9]", "", raw)[:8] or "UNDEF"
            outdir = os.path.join(GOLD_ROOT, sub)
            if os.path.commonpath([os.path.abspath(outdir), os.path.abspath(GOLD_ROOT)]) != os.path.abspath(GOLD_ROOT):
                return self._send(400, {"error": "ungueltiger Code"})
            os.makedirs(outdir, exist_ok=True)
            fname = re.sub(r"[^0-9A-Za-z._-]", "_", f"{f['haltung']}_{chosen_time:.1f}s_{sub}_gold") + ".png"
            outp = os.path.join(outdir, fname)
            try:
                with open(outp, "wb") as fh:
                    fh.write(base64.b64decode(b64.split(",")[-1]))
                image_file = outp
            except Exception as ex:
                return self._send(500, {"error": f"save failed: {ex}"})
        # Box + Maske als Gold-Annotation (nur bei confirm mit gezeichneter Box) — alles in EINER json
        ann_file = ""
        if decision == "confirm" and image_file and (data.get("box_norm") or data.get("mask_rle")):
            ann = {"frame": os.path.basename(image_file), "haltung": f["haltung"],
                   "protocol_time": f["zeit"], "chosen_time": round(chosen_time, 2), "code": chosen_code,
                   "box_norm": data.get("box_norm"), "box_px": data.get("box_px"),
                   "mask_rle": data.get("mask_rle"), "image_w": data.get("image_w"),
                   "image_h": data.get("image_h"), "mask_area_pixels": data.get("area_pixels"),
                   "source_video": f.get("video_name"), "annotated_by": "mensch",
                   "ts": time.strftime("%Y-%m-%d %H:%M:%S")}
            ann_file = os.path.splitext(image_file)[0] + ".json"
            try:
                with open(ann_file, "w", encoding="utf-8") as jf:
                    json.dump(ann, jf, ensure_ascii=False, indent=2)
            except OSError as ex:
                return self._send(500, {"error": f"annotation save failed: {ex}"})
        rec = {"ts": time.strftime("%Y-%m-%d %H:%M:%S"), "key": key, "haltung": f["haltung"],
               "protocol_code": f["code"], "chosen_code": chosen_code, "protocol_time": f["zeit"],
               "chosen_time": round(chosen_time, 2), "decision": decision, "note": note,
               "image_file": image_file, "annotation_file": ann_file,
               "has_box": bool(data.get("box_norm")), "has_mask": bool(data.get("mask_rle")),
               "video": f.get("video_name")}
        os.makedirs(GOLD_ROOT, exist_ok=True)
        with _state_lock:
            with open(os.path.join(GOLD_ROOT, "gold_ledger.jsonl"), "a", encoding="utf-8") as fh:
                fh.write(json.dumps(rec, ensure_ascii=False) + "\n")
        self._send(200, {"ok": True, "saved": image_file or "(kein Bild)",
                         "annotation": ann_file, "decision": decision})


def main():
    global FINDINGS, ORDER, DATASET, VIDEO_ROOT
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int, default=8200)
    ap.add_argument("--priority", default=None, help="JSON-Liste von Befund-Keys (zuerst zeigen)")
    ap.add_argument("--limit", type=int, default=0, help="max. Anzahl Befunde (0=alle)")
    ap.add_argument("--classes", default=",".join(DEFAULT_CLASSES),
                    help="Kommagetrennte Trainingsklassen, z.B. BCA,BCC oder BCA,BCC,LEER")
    ap.add_argument("--dedupe-window", type=float, default=DEFAULT_DEDUPE_WINDOW_SECONDS,
                    help="Sekundenfenster fuer nahe Duplikate pro Haltung/Code (0=aus)")
    ap.add_argument("--dataset", default=DATASET,
                    help="ImageFolder-Datensatz mit train/<Klasse>/*.png")
    ap.add_argument("--video-root", default=VIDEO_ROOT,
                    help="Root-Ordner der Haltungs-Videos")
    args = ap.parse_args()
    DATASET = args.dataset
    VIDEO_ROOT = args.video_root
    try:
        classes = parse_classes(args.classes, DATASET)
    except ValueError as ex:
        raise SystemExit(str(ex))
    print(f"Baue Befund-Liste ({','.join(classes)}, Video-Aufloesung)...", flush=True)
    t0 = time.time()
    FINDINGS, ORDER = build_findings(args.priority, args.limit or 0, classes, args.dedupe_window)
    n_vid = sum(1 for k in ORDER if FINDINGS[k]["video_available"])
    print(f"Befunde: {len(ORDER)}  (mit Video: {n_vid})  in {time.time()-t0:.1f}s", flush=True)
    print(f"ffmpeg: {FFMPEG}", flush=True)
    print(f"Gold-Satz -> {GOLD_ROOT}", flush=True)
    srv = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    print(f"\n  >>> Im Browser oeffnen:  http://localhost:{args.port}/\n", flush=True)
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\nGestoppt.")


if __name__ == "__main__":
    main()
