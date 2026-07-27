from __future__ import annotations

import argparse
import hashlib
import json
import math
import mimetypes
import os
import tempfile
import threading
import unicodedata
from datetime import datetime, timezone
from html import escape
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Callable
from urllib.parse import parse_qs, quote, urlparse


REVIEW_FIELDS = (
    "code_decision",
    "corrected_code",
    "corrected_title",
    "expected_severity",
    "event_id",
    "meter_start",
    "meter_end",
    "reviewed_by",
    "reviewed_at_utc",
    "comment",
)

CODE_DECISIONS = {"matches", "corrected", "no_damage"}

DEFAULT_CATALOG_PATH = (
    Path(__file__).resolve().parents[2]
    / "src"
    / "AuswertungPro.Next.UI"
    / "Data"
    / "vsa_kek_2020_catalog_manifest.json"
)


class EvalMetadataReviewStore:
    """Lesender V1-Pruefplatz mit getrenntem, atomarem Review-Ergebnis."""

    def __init__(
        self,
        eval_root: str | Path,
        output_path: str | Path,
        catalog_path: str | Path = DEFAULT_CATALOG_PATH,
        now_utc: Callable[[], str] | None = None,
    ):
        self.eval_root = Path(eval_root).resolve()
        self.output_path = Path(output_path).resolve()
        self.catalog_path = Path(catalog_path).resolve()
        self.candidates_path = self.eval_root / "_candidates.json"
        self._now_utc = now_utc or (
            lambda: datetime.now(timezone.utc).isoformat()
        )
        self._lock = threading.RLock()

        if self.output_path.is_relative_to(self.eval_root):
            raise ValueError(
                "Die Review-Ausgabe muss ausserhalb des eingefrorenen Eval-Ordners liegen."
            )
        if not self.candidates_path.is_file():
            raise FileNotFoundError(
                f"Eval-Kandidaten nicht gefunden: {self.candidates_path}"
            )
        if not self.catalog_path.is_file():
            raise FileNotFoundError(
                f"VSA-Codekatalog nicht gefunden: {self.catalog_path}"
            )

        self.source_candidates_sha256 = _sha256(self.candidates_path)
        self.source_catalog_sha256 = _sha256(self.catalog_path)
        self._code_titles = _load_code_titles(self.catalog_path)
        self.rows = self._load_damage_rows()
        self._rows_by_id = {row["id"]: row for row in self.rows}
        self._merge_existing_output()

    def _load_damage_rows(self) -> list[dict[str, object]]:
        source = json.loads(self.candidates_path.read_text(encoding="utf-8-sig"))
        if not isinstance(source, list):
            raise ValueError("_candidates.json muss ein JSON-Array sein.")

        rows: list[dict[str, object]] = []
        seen_ids: set[str] = set()
        for candidate in source:
            if not isinstance(candidate, dict):
                continue
            code = _normalize_code(
                candidate.get("korrektur") or candidate.get("code_full")
            )
            if not code.startswith(("BA", "BB")):
                continue

            case_id = str(candidate.get("id") or "").strip()
            if not case_id:
                raise ValueError("Ein Schadensfall besitzt keine ID.")
            key = case_id.casefold()
            if key in seen_ids:
                raise ValueError(f"Doppelte Schadensfall-ID: {case_id}")
            seen_ids.add(key)

            frame_path = str(candidate.get("frame_path") or "").strip()
            image_name = Path(frame_path).name if frame_path else ""
            image_path = self.eval_root / "images" / image_name
            meter = _optional_number(candidate.get("meter"))
            row: dict[str, object] = {
                "id": case_id,
                "image_name": image_name,
                "image_path": str(image_path),
                "image_exists": image_path.is_file(),
                "holding_key": str(
                    candidate.get("haltung_key")
                    or candidate.get("holding_key")
                    or ""
                ).strip(),
                "meter": meter,
                "expected_code": code,
                "expected_title": self._code_titles.get(
                    code,
                    "Klartext im aktiven VSA-Katalog nicht gefunden",
                ),
                "code_decision": None,
                "corrected_code": None,
                "corrected_title": None,
                "category": str(candidate.get("kategorie") or "").strip(),
                "expected_severity": _optional_integer(
                    candidate.get("expected_severity")
                ),
                "event_id": _optional_text(candidate.get("event_id")),
                "meter_start": _optional_number(candidate.get("meter_start")),
                "meter_end": _optional_number(candidate.get("meter_end")),
                "reviewed_by": "",
                "reviewed_at_utc": "",
                "comment": "",
            }
            rows.append(row)

        rows.sort(
            key=lambda row: (
                str(row["holding_key"]).casefold(),
                float(row["meter"]) if row["meter"] is not None else math.inf,
                str(row["expected_code"]).casefold(),
                str(row["id"]).casefold(),
            )
        )
        return rows

    def _merge_existing_output(self) -> None:
        if not self.output_path.exists():
            return

        existing = json.loads(self.output_path.read_text(encoding="utf-8-sig"))
        if not isinstance(existing, dict):
            raise ValueError("Die vorhandene Review-Ausgabe ist ungueltig.")
        if existing.get("source_candidates_sha256") != self.source_candidates_sha256:
            raise ValueError(
                "Die vorhandene Review-Ausgabe gehoert zu einem anderen Eval-Stand."
            )

        reviews = existing.get("reviews")
        if not isinstance(reviews, list):
            raise ValueError("Die vorhandene Review-Ausgabe enthaelt keine Review-Liste.")

        for old in reviews:
            if not isinstance(old, dict):
                continue
            row = self._rows_by_id.get(str(old.get("id") or ""))
            if row is None:
                continue
            for field in REVIEW_FIELDS:
                row[field] = old.get(field, row.get(field))
            if row["code_decision"] is None and _has_legacy_damage_review(row):
                row["code_decision"] = "matches"

    def prepare_output(self) -> dict[str, object]:
        with self._lock:
            self._write_output_locked()
            return self.state()

    def state(self) -> dict[str, object]:
        with self._lock:
            conflict_ids = self._event_conflict_ids()
            items = [self._public_row(row, conflict_ids) for row in self.rows]
            done = sum(
                1
                for row in self.rows
                if _is_complete(row) and str(row["id"]) not in conflict_ids
            )
            current = next(
                (
                    row
                    for row in self.rows
                    if not _is_complete(row) or str(row["id"]) in conflict_ids
                ),
                self.rows[0] if self.rows else None,
            )
            return {
                "total": len(self.rows),
                "done": done,
                "open": len(self.rows) - done,
                "conflicting_reviews": len(conflict_ids),
                "missing_images": sum(
                    1 for row in self.rows if not bool(row["image_exists"])
                ),
                "source_candidates_sha256": self.source_candidates_sha256,
                "damage_code_options": [
                    {"code": code, "title": title}
                    for code, title in sorted(self._code_titles.items())
                    if code.startswith(("BA", "BB"))
                ],
                "current": self._public_row(current, conflict_ids) if current else None,
                "items": items,
            }

    def set_review(
        self,
        case_id: str,
        severity: object,
        event_id: object,
        meter_start: object,
        meter_end: object,
        reviewed_by: object,
        comment: object,
        code_decision: object = "matches",
        corrected_code: object = None,
    ) -> dict[str, object]:
        with self._lock:
            row = self._rows_by_id.get(str(case_id))
            if row is None:
                raise KeyError(f"Schadensfall nicht gefunden: {case_id}")

            parsed_decision = _required_code_decision(code_decision)
            parsed_reviewer = _required_identifier(reviewed_by, "Pruefer")
            parsed_corrected_code: str | None = None
            parsed_corrected_title: str | None = None
            parsed_severity: int | None = None
            parsed_event_id: str | None = None
            parsed_start: float | None = None
            parsed_end: float | None = None

            if parsed_decision == "corrected":
                parsed_corrected_code = _normalize_code(corrected_code)
                if not parsed_corrected_code.startswith(("BA", "BB")):
                    raise ValueError(
                        "Die Korrektur muss ein BA- oder BB-Schadencode sein."
                    )
                parsed_corrected_title = self._code_titles.get(parsed_corrected_code)
                if parsed_corrected_title is None:
                    raise ValueError(
                        f"Schadencode nicht im aktiven VSA-Katalog: {parsed_corrected_code}"
                    )

            if parsed_decision != "no_damage":
                parsed_severity = _required_severity(severity)
                parsed_event_id = _required_identifier(event_id, "Ereignis-ID")
                parsed_start = _optional_number(meter_start)
                parsed_end = _optional_number(meter_end)
                _validate_meter_range(row, parsed_start, parsed_end)
                effective_code = (
                    parsed_corrected_code
                    if parsed_decision == "corrected"
                    else str(row["expected_code"])
                )
                self._validate_event_consistency(
                    row,
                    effective_code,
                    parsed_severity,
                    parsed_event_id,
                    parsed_start,
                    parsed_end,
                )

            row["code_decision"] = parsed_decision
            row["corrected_code"] = parsed_corrected_code
            row["corrected_title"] = parsed_corrected_title
            row["expected_severity"] = parsed_severity
            row["event_id"] = parsed_event_id
            row["meter_start"] = parsed_start
            row["meter_end"] = parsed_end
            row["reviewed_by"] = parsed_reviewer
            row["reviewed_at_utc"] = self._now_utc()
            row["comment"] = str(comment or "").strip()

            self._write_output_locked()
            return self.state()

    def _validate_event_consistency(
        self,
        current: dict[str, object],
        effective_code: str,
        severity: int,
        event_id: str,
        meter_start: float | None,
        meter_end: float | None,
    ) -> None:
        holding_key = str(current["holding_key"]).strip().casefold()
        normalized_event_id = event_id.casefold()
        for other in self.rows:
            if other is current or not _is_complete(other):
                continue
            if str(other["holding_key"]).strip().casefold() != holding_key:
                continue
            if str(other.get("event_id") or "").strip().casefold() != normalized_event_id:
                continue
            if (
                _effective_code(other) != effective_code
                or other.get("expected_severity") != severity
                or other.get("meter_start") != meter_start
                or other.get("meter_end") != meter_end
            ):
                raise ValueError(
                    "Diese Ereignis-ID ist in derselben Haltung bereits mit "
                    "anderen Angaben belegt. Bitte eine andere Ereignis-ID verwenden."
                )

    def _event_conflict_ids(self) -> set[str]:
        events: dict[tuple[str, str], tuple[tuple[object, ...], str]] = {}
        conflicts: set[str] = set()
        for row in self.rows:
            if not _is_complete(row) or row.get("code_decision") == "no_damage":
                continue
            key = (
                str(row["holding_key"]).strip().casefold(),
                str(row.get("event_id") or "").strip().casefold(),
            )
            metadata = (
                _effective_code(row),
                row.get("expected_severity"),
                row.get("meter_start"),
                row.get("meter_end"),
            )
            case_id = str(row["id"])
            previous = events.get(key)
            if previous is None:
                events[key] = (metadata, case_id)
            elif previous[0] != metadata:
                conflicts.add(previous[1])
                conflicts.add(case_id)
        return conflicts

    def image_path_for(self, case_id: str) -> Path:
        row = self._rows_by_id.get(case_id)
        if row is None:
            raise KeyError(f"Schadensfall nicht gefunden: {case_id}")
        path = Path(str(row["image_path"]))
        if not path.is_file():
            raise FileNotFoundError(f"Bild nicht gefunden: {path}")
        return path

    def _public_row(
        self,
        row: dict[str, object] | None,
        conflict_ids: set[str] | None = None,
    ) -> dict[str, object] | None:
        if row is None:
            return None
        public = dict(row)
        event_conflict = str(row["id"]) in (conflict_ids or set())
        public["effective_code"] = _effective_code(row)
        public["effective_title"] = _effective_title(row)
        public["excluded_from_damage_eval"] = row.get("code_decision") == "no_damage"
        public["event_conflict"] = event_conflict
        public["image_url"] = f"/image?id={quote(str(row['id']))}"
        public["complete"] = _is_complete(row) and not event_conflict
        return public

    def _write_output_locked(self) -> None:
        self.output_path.parent.mkdir(parents=True, exist_ok=True)
        conflict_ids = self._event_conflict_ids()
        payload = {
            "schema_version": 2,
            "purpose": "SewerStudio V1 Code-, Ereignis- und Schadensstufen-Review",
            "source_eval_root": str(self.eval_root),
            "source_candidates_sha256": self.source_candidates_sha256,
            "source_catalog_path": str(self.catalog_path),
            "source_catalog_sha256": self.source_catalog_sha256,
            "damage_frames": len(self.rows),
            "completed_reviews": sum(
                1
                for row in self.rows
                if _is_complete(row) and str(row["id"]) not in conflict_ids
            ),
            "conflicting_reviews": len(conflict_ids),
            "reviews": [dict(row) for row in self.rows],
        }
        handle, temp_name = tempfile.mkstemp(
            prefix=self.output_path.name + ".",
            suffix=".tmp",
            dir=str(self.output_path.parent),
        )
        os.close(handle)
        temp_path = Path(temp_name)
        try:
            with temp_path.open("w", encoding="utf-8", newline="\n") as stream:
                json.dump(payload, stream, ensure_ascii=False, indent=2)
                stream.write("\n")
                stream.flush()
                os.fsync(stream.fileno())
            os.replace(temp_path, self.output_path)
        finally:
            if temp_path.exists():
                temp_path.unlink()


def _normalize_code(value: object) -> str:
    text = str(value or "").strip()
    return "".join(ch.upper() for ch in text if ch.isalnum())


def _load_code_titles(catalog_path: Path) -> dict[str, str]:
    manifest = json.loads(catalog_path.read_text(encoding="utf-8-sig"))
    codes = manifest.get("codes") if isinstance(manifest, dict) else None
    if not isinstance(codes, list):
        raise ValueError("Der VSA-Codekatalog enthaelt keine Code-Liste.")

    titles: dict[str, str] = {}
    for entry in codes:
        if not isinstance(entry, dict):
            continue
        code = _normalize_code(entry.get("code"))
        title = str(entry.get("title") or "").strip()
        if code and title:
            titles[code] = title
    return titles


def _optional_text(value: object) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def _optional_integer(value: object) -> int | None:
    if value is None or value == "":
        return None
    if isinstance(value, bool):
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _optional_number(value: object) -> float | None:
    if value is None or value == "":
        return None
    if isinstance(value, bool):
        raise ValueError("Meterwerte muessen Zahlen sein.")
    try:
        number = float(value)
    except (TypeError, ValueError) as exc:
        raise ValueError("Meterwerte muessen Zahlen sein.") from exc
    if not math.isfinite(number):
        raise ValueError("Meterwerte muessen endlich sein.")
    return number


def _required_severity(value: object) -> int:
    severity = _optional_integer(value)
    if severity is None or not 1 <= severity <= 5:
        raise ValueError("Die Schadensstufe muss zwischen 1 und 5 liegen.")
    return severity


def _required_code_decision(value: object) -> str:
    decision = str(value or "").strip().lower()
    if decision not in CODE_DECISIONS:
        raise ValueError("Bitte entscheiden, ob der Vorgabe-Code zum Bild passt.")
    return decision


def _required_identifier(value: object, label: str) -> str:
    text = unicodedata.normalize("NFC", str(value or "").strip())
    if not text:
        raise ValueError(f"{label} fehlt.")
    if any(unicodedata.category(char).startswith("C") for char in text):
        raise ValueError(f"{label} darf keine Steuerzeichen enthalten.")
    return text


def _validate_meter_range(
    row: dict[str, object],
    meter_start: float | None,
    meter_end: float | None,
) -> None:
    if (meter_start is None) != (meter_end is None):
        raise ValueError("MeterStart und MeterEnd muessen gemeinsam gesetzt sein.")
    if meter_start is None:
        return
    if meter_start < 0 or meter_end is None or meter_end < meter_start:
        raise ValueError("Der Meterbereich muss nicht negativ und aufsteigend sein.")
    frame_meter = row.get("meter")
    if frame_meter is not None and not meter_start <= float(frame_meter) <= meter_end:
        raise ValueError("Der Frame-Meterwert liegt ausserhalb des Ereignisbereichs.")


def _is_complete(row: dict[str, object]) -> bool:
    decision = str(row.get("code_decision") or "").strip().lower()
    if decision not in CODE_DECISIONS:
        return False
    if not str(row.get("reviewed_by") or "").strip():
        return False
    if not str(row.get("reviewed_at_utc") or "").strip():
        return False
    if decision == "no_damage":
        return True
    if decision == "corrected" and not str(row.get("corrected_code") or "").strip():
        return False
    severity = _optional_integer(row.get("expected_severity"))
    return severity is not None and 1 <= severity <= 5 and bool(
        str(row.get("event_id") or "").strip()
    )


def _has_legacy_damage_review(row: dict[str, object]) -> bool:
    severity = _optional_integer(row.get("expected_severity"))
    return (
        severity is not None
        and 1 <= severity <= 5
        and bool(str(row.get("event_id") or "").strip())
        and bool(str(row.get("reviewed_by") or "").strip())
        and bool(str(row.get("reviewed_at_utc") or "").strip())
    )


def _effective_code(row: dict[str, object]) -> str | None:
    decision = str(row.get("code_decision") or "").strip().lower()
    if decision == "no_damage":
        return None
    if decision == "corrected":
        return _normalize_code(row.get("corrected_code")) or None
    if decision == "matches":
        return _normalize_code(row.get("expected_code")) or None
    return None


def _effective_title(row: dict[str, object]) -> str | None:
    decision = str(row.get("code_decision") or "").strip().lower()
    if decision == "corrected":
        return _optional_text(row.get("corrected_title"))
    if decision == "matches":
        return _optional_text(row.get("expected_title"))
    return None


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def make_handler(store: EvalMetadataReviewStore, reviewer: str):
    html = INDEX_HTML.replace("__REVIEWER__", escape(reviewer))

    class EvalMetadataReviewHandler(BaseHTTPRequestHandler):
        server_version = "SewerStudioEvalMetadataReview/1.0"

        def do_GET(self) -> None:  # noqa: N802
            parsed = urlparse(self.path)
            if parsed.path == "/":
                self._send_html(html)
                return
            if parsed.path == "/api/state":
                self._send_json(store.state())
                return
            if parsed.path == "/image":
                self._send_image(parse_qs(parsed.query).get("id", [""])[0])
                return
            self.send_error(404, "Nicht gefunden")

        def do_POST(self) -> None:  # noqa: N802
            if urlparse(self.path).path != "/api/review":
                self.send_error(404, "Nicht gefunden")
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                payload = json.loads(self.rfile.read(length).decode("utf-8"))
                state = store.set_review(
                    str(payload.get("id", "")),
                    payload.get("expected_severity"),
                    payload.get("event_id"),
                    payload.get("meter_start"),
                    payload.get("meter_end"),
                    payload.get("reviewed_by"),
                    payload.get("comment"),
                    payload.get("code_decision"),
                    payload.get("corrected_code"),
                )
                self._send_json(state)
            except Exception as exc:  # pragma: no cover - defensiver Serverpfad
                self._send_json({"error": str(exc)}, status=400)

        def _send_image(self, case_id: str) -> None:
            try:
                path = store.image_path_for(case_id)
                body = path.read_bytes()
                content_type = (
                    mimetypes.guess_type(path.name)[0] or "application/octet-stream"
                )
                self.send_response(200)
                self.send_header("Content-Type", content_type)
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            except Exception as exc:  # pragma: no cover - defensiver Serverpfad
                self.send_error(404, str(exc))

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

        def log_message(self, format: str, *args: object) -> None:
            return

    return EvalMetadataReviewHandler


INDEX_HTML = r"""<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<title>SewerStudio KI-Pruefsatz</title>
<link rel="icon" href="data:,">
<style>
html, body { margin: 0; height: 100%; background: #090b0d; color: #f1f5f9; font-family: Segoe UI, Arial, sans-serif; }
body { display: grid; grid-template-rows: auto 1fr auto; }
header, footer { background: #171b21; padding: 12px 18px; }
header { border-bottom: 1px solid #2e3440; }
footer { border-top: 1px solid #2e3440; display: flex; gap: 10px; justify-content: center; }
h1 { font-size: 19px; margin: 0 0 5px; }
.status, .muted { color: #aeb7c2; font-size: 13px; }
main { display: grid; grid-template-columns: minmax(0, 1fr) 400px; min-height: 0; }
.stage { display: flex; align-items: center; justify-content: center; padding: 10px; min-height: 0; }
img { max-width: 100%; max-height: calc(100vh - 160px); object-fit: contain; background: #000; border: 1px solid #2e3440; }
.side { background: #11151a; border-left: 1px solid #2e3440; padding: 16px; overflow: auto; }
.facts { display: grid; grid-template-columns: 110px 1fr; gap: 7px 10px; margin-bottom: 16px; }
.facts strong { color: #8bd5ff; }
label { display: block; margin: 11px 0 4px; }
input, textarea, select { width: 100%; box-sizing: border-box; background: #0b0f14; color: #f1f5f9; border: 1px solid #3b4655; border-radius: 6px; padding: 9px; font-size: 15px; }
.pair { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.explanation { margin-top: 8px; padding: 9px 10px; border-left: 3px solid #38bdf8; background: #0c1720; color: #cbd5e1; font-size: 13px; line-height: 1.35; }
.hidden { display: none; }
textarea { min-height: 70px; resize: vertical; }
button { border: 1px solid #3b4655; background: #242b35; color: #f1f5f9; border-radius: 6px; padding: 10px 16px; cursor: pointer; }
button:hover { background: #303947; }
button:disabled { opacity: .55; cursor: wait; }
.save { background: #166534; border-color: #22c55e; font-size: 16px; }
.warning { color: #fbbf24; margin-top: 10px; min-height: 18px; }
@media (max-width: 900px) { main { grid-template-columns: 1fr; } .side { border-left: 0; border-top: 1px solid #2e3440; } }
</style>
</head>
<body>
<header>
  <h1>KI-Pruefsatz: Code, Ereignis und Schadensstufe</h1>
  <div class="status" id="status">Lade...</div>
</header>
<main>
  <section class="stage"><img id="photo" alt="Schadensbild"></section>
  <aside class="side">
    <div class="facts">
      <span>Code</span><strong id="code">-</strong>
      <span>Klartext</span><strong id="codeTitle">-</strong>
      <span>Haltung</span><strong id="holding">-</strong>
      <span>Meter</span><strong id="meter">-</strong>
      <span>Bild</span><span class="muted" id="imageName">-</span>
    </div>
    <label for="codeDecision">Passt der Vorgabe-Code zum Bild?</label>
    <select id="codeDecision" onchange="updateCodeDecisionUi()">
      <option value="">Bitte waehlen</option>
      <option value="matches">Ja, Code passt</option>
      <option value="corrected">Nein, anderer Schadencode</option>
      <option value="no_damage">Nein, kein passender Schaden sichtbar</option>
    </select>
    <div id="correctionFields" class="hidden">
      <label for="correctedCode">Anderer Schadencode</label>
      <input id="correctedCode" list="damageCodes" placeholder="z.B. BAIZ"
             oninput="updateCorrectedTitle()">
      <datalist id="damageCodes"></datalist>
      <div class="muted" id="correctedTitle">Klartext erscheint nach der Code-Eingabe.</div>
    </div>
    <div id="damageFields" class="hidden">
      <label for="severity">Schadensstufe fuer den KI-Test (1 bis 5)</label>
      <select id="severity">
        <option value="">Bitte waehlen</option>
        <option value="1">1 - gering</option>
        <option value="2">2 - eher gering</option>
        <option value="3">3 - mittel</option>
        <option value="4">4 - schwer / wichtiger KI-Prueffall</option>
        <option value="5">5 - kritisch / wichtiger KI-Prueffall</option>
      </select>
      <div class="explanation">
        <strong>Wirkung:</strong> Bewerte die fachliche Bedeutung des Schadens,
        nicht die Bildqualitaet. Die Stufe veraendert weder den Code noch die Zustandsklasse.
        Stufe 4 und 5 werden zusaetzlich als wichtige Schaeden
        ausgewertet. Fuer eine belastbare Freigabe braucht der Pruefsatz mindestens
        20 unterschiedliche Ereignisse mit Stufe 4 oder 5.
      </div>
      <label for="eventId">Ereignis-ID</label>
      <input id="eventId" placeholder="z.B. 81030-80945-BAIZ-01">
      <div class="muted">Mehrere Bilder desselben Schadens erhalten dieselbe ID.</div>
      <div class="pair">
        <div><label for="meterStart">MeterStart optional</label><input id="meterStart" type="number" step="0.001" min="0"></div>
        <div><label for="meterEnd">MeterEnd optional</label><input id="meterEnd" type="number" step="0.001" min="0"></div>
      </div>
    </div>
    <label for="reviewer">Geprueft von</label>
    <input id="reviewer" value="__REVIEWER__">
    <label for="comment">Bemerkung optional</label>
    <textarea id="comment"></textarea>
    <div class="warning" id="warning"></div>
  </aside>
</main>
<footer>
  <button onclick="previousItem()">Zurueck</button>
  <button onclick="copyPrevious()">Wie vorheriger Schaden</button>
  <button class="save" onclick="saveCurrent()">Speichern und weiter</button>
  <button onclick="nextItem()">Ueberspringen (nicht speichern)</button>
</footer>
<script>
let items = [];
let codeOptions = [];
let index = 0;
let busy = false;

async function loadState() {
  const response = await fetch('/api/state');
  const state = await response.json();
  items = state.items || [];
  codeOptions = state.damage_code_options || [];
  fillDamageCodeList();
  const openIndex = items.findIndex(item => !item.complete);
  index = openIndex >= 0 ? openIndex : 0;
  render(state);
}

function render(state) {
  if (!items.length) {
    document.getElementById('status').textContent = 'Keine Schadensbilder gefunden.';
    return;
  }
  const item = items[index];
  document.getElementById('photo').src = item.image_url;
  document.getElementById('code').textContent = item.expected_code || '-';
  document.getElementById('codeTitle').textContent = item.expected_title || '-';
  document.getElementById('holding').textContent = item.holding_key || '-';
  document.getElementById('meter').textContent = item.meter ?? '-';
  document.getElementById('imageName').textContent = item.image_name || '-';
  document.getElementById('codeDecision').value = item.code_decision || '';
  document.getElementById('correctedCode').value = item.corrected_code || '';
  document.getElementById('severity').value = item.expected_severity || '';
  document.getElementById('eventId').value = item.event_id || '';
  document.getElementById('meterStart').value = item.meter_start ?? '';
  document.getElementById('meterEnd').value = item.meter_end ?? '';
  document.getElementById('reviewer').value = item.reviewed_by || '__REVIEWER__';
  document.getElementById('comment').value = item.comment || '';
  updateCodeDecisionUi();
  updateCorrectedTitle();
  const warnings = [];
  if (!item.image_exists) warnings.push('Bilddatei fehlt.');
  if (item.event_conflict) {
    warnings.push('Diese Ereignis-ID ist mit anderen Codes oder Stufen belegt. Bitte korrigieren.');
  }
  document.getElementById('warning').textContent = warnings.join(' ');
  const done = items.filter(row => row.complete).length;
  const conflicts = items.filter(row => row.event_conflict).length;
  document.getElementById('status').textContent =
    `Schadensbild ${index + 1} / ${items.length} | geprueft: ${done} | offen: ${items.length - done}` +
    (conflicts ? ` | Konflikte: ${conflicts}` : '');
}

function valueOrNull(id) {
  const value = document.getElementById(id).value.trim();
  return value === '' ? null : Number(value);
}

async function saveCurrent() {
  if (busy) return;
  busy = true;
  setButtonsDisabled(true);
  document.getElementById('warning').textContent = '';
  const item = items[index];
  try {
    const response = await fetch('/api/review', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({
        id: item.id,
        expected_severity: valueOrNull('severity'),
        event_id: document.getElementById('eventId').value,
        meter_start: valueOrNull('meterStart'),
        meter_end: valueOrNull('meterEnd'),
        reviewed_by: document.getElementById('reviewer').value,
        comment: document.getElementById('comment').value,
        code_decision: document.getElementById('codeDecision').value,
        corrected_code: document.getElementById('correctedCode').value
      })
    });
    const state = await response.json();
    if (state.error) {
      document.getElementById('warning').textContent = state.error;
      return;
    }
    items = state.items || items;
    let nextOpen = items.findIndex((row, rowIndex) => rowIndex > index && !row.complete);
    if (nextOpen < 0) nextOpen = items.findIndex(row => !row.complete);
    if (nextOpen >= 0) index = nextOpen;
    else if (index < items.length - 1) index++;
    render(state);
  } catch (error) {
    document.getElementById('warning').textContent = 'Speichern fehlgeschlagen. Bitte erneut versuchen.';
  } finally {
    busy = false;
    setButtonsDisabled(false);
  }
}

function copyPrevious() {
  if (index === 0) return;
  const previous = items[index - 1];
  const current = items[index];
  if (previous.code_decision === 'no_damage') {
    document.getElementById('codeDecision').value = 'no_damage';
    document.getElementById('correctedCode').value = '';
  } else if (previous.effective_code === current.expected_code) {
    document.getElementById('codeDecision').value = 'matches';
    document.getElementById('correctedCode').value = '';
  } else if (previous.effective_code) {
    document.getElementById('codeDecision').value = 'corrected';
    document.getElementById('correctedCode').value = previous.effective_code;
  }
  document.getElementById('severity').value = previous.expected_severity || '';
  document.getElementById('eventId').value = previous.event_id || '';
  document.getElementById('meterStart').value = previous.meter_start ?? '';
  document.getElementById('meterEnd').value = previous.meter_end ?? '';
  updateCodeDecisionUi();
  updateCorrectedTitle();
}

function fillDamageCodeList() {
  const list = document.getElementById('damageCodes');
  list.replaceChildren();
  codeOptions.forEach(item => {
    const option = document.createElement('option');
    option.value = item.code;
    option.label = item.title;
    list.appendChild(option);
  });
}

function normalizedCode(value) {
  return String(value || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
}

function updateCorrectedTitle() {
  const code = normalizedCode(document.getElementById('correctedCode').value);
  const match = codeOptions.find(item => item.code === code);
  document.getElementById('correctedTitle').textContent =
    match ? match.title : (code ? 'Code nicht im aktiven VSA-Katalog gefunden.' : 'Klartext erscheint nach der Code-Eingabe.');
}

function updateCodeDecisionUi() {
  const decision = document.getElementById('codeDecision').value;
  document.getElementById('correctionFields').classList.toggle('hidden', decision !== 'corrected');
  document.getElementById('damageFields').classList.toggle('hidden', decision === 'no_damage' || decision === '');
}

function nextItem() {
  if (index < items.length - 1) index++;
  render({});
}

function previousItem() {
  if (index > 0) index--;
  render({});
}

function setButtonsDisabled(disabled) {
  document.querySelectorAll('button').forEach(button => button.disabled = disabled);
}

document.addEventListener('keydown', event => {
  if (busy) return;
  if (event.target && ['INPUT', 'TEXTAREA', 'SELECT'].includes(event.target.tagName)) {
    if (event.ctrlKey && event.key === 'Enter') saveCurrent();
    return;
  }
  if (['1', '2', '3', '4', '5'].includes(event.key)) {
    document.getElementById('severity').value = event.key;
  }
  if (event.key === 'ArrowRight') nextItem();
  if (event.key === 'ArrowLeft') previousItem();
  if (event.ctrlKey && event.key === 'Enter') saveCurrent();
});

loadState().catch(() => {
  document.getElementById('status').textContent = 'Pruefplatz nicht erreichbar.';
});
</script>
</body>
</html>
"""


def run_server(
    eval_root: Path,
    output: Path,
    catalog: Path,
    port: int,
    reviewer: str,
) -> None:
    store = EvalMetadataReviewStore(eval_root, output, catalog)
    store.prepare_output()
    server = ThreadingHTTPServer(
        ("127.0.0.1", port),
        make_handler(store, reviewer),
    )
    print(f"KI-Pruefsatz laeuft: http://127.0.0.1:{port}/")
    print(f"Eval-Set (nur lesen): {eval_root}")
    print(f"VSA-Codekatalog:       {catalog}")
    print(f"Review-Ausgabe:       {output}")
    print("Stoppen mit Strg+C")
    server.serve_forever()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="SewerStudio Review fuer Ereignis-ID und Schadensstufe"
    )
    parser.add_argument("--eval-root", default=r"C:\KI_BRAIN\eval_set")
    parser.add_argument(
        "--output",
        default=r"C:\KI_BRAIN\eval_review\v1_event_metadata_review.json",
    )
    parser.add_argument("--catalog", default=str(DEFAULT_CATALOG_PATH))
    parser.add_argument("--reviewer", default="Pascal")
    parser.add_argument("--port", type=int, default=8772)
    parser.add_argument(
        "--prepare-only",
        action="store_true",
        help="Erzeugt nur die getrennte Review-Vorlage und startet keinen Server.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    eval_root = Path(args.eval_root)
    output = Path(args.output)
    catalog = Path(args.catalog)
    if args.prepare_only:
        store = EvalMetadataReviewStore(eval_root, output, catalog)
        state = store.prepare_output()
        print(f"Review-Vorlage vorbereitet: {output}")
        print(f"Schadensbilder: {state['total']}")
        print(f"Bereits geprueft: {state['done']}")
        return
    run_server(eval_root, output, catalog, args.port, args.reviewer)


if __name__ == "__main__":
    main()
