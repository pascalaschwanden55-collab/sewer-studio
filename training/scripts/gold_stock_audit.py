"""Schreibfreier Aufbau-Pruefbericht fuer den persoenlichen Gold-Trainingsbestand.

Das Skript exportiert nichts, trainiert nichts und aktiviert nichts. Es liest
ausschliesslich training_samples.json (keine Entwuerfe, keine Teacher- oder
Auto-Vorschlaege), prueft jede Stufe des freigegebenen Regelwerks und schreibt
genau einen JSON-Bericht unter <KnowledgeRoot>/training/reports plus die
Konsolen-Zusammenfassung. KI-Brain und Repo werden sonst nicht veraendert.

Pruefstufen (Trichter):
  eingelesen -> persoenlich -> bild_ok -> box_ok -> maske_ok -> code_ok
  -> eval_sauber -> final_verwendbar

Split: Echte Haltungen werden vor der Gruppierung normalisiert. Gleiche Bildbytes
verbinden ausserdem alle betroffenen Haltungen zu genau einer Split-Gruppe. Bei
Pseudo-/fehlenden IDs bleibt wenigstens jedes identische Bild ueber seinen SHA-256
zusammen. Der Bericht markiert einen solchen Split als nicht release-faehig.
Rolle je Gruppe ueber SHA-256("split-v1|<gruppe>") mit Ziel 70/15/15.
test bleibt eingefroren und wird nur markiert.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image


SCHEMA_VERSION = "1.1"

# Persoenlicher Goldkatalog (19 Hauptcodes, freigegeben im Mandat).
GOLD_MAIN_CODES = (
    "BAB", "BAA", "BAC", "BAF", "BAH", "BAI", "BAJ",
    "BCA", "BCC", "BCD", "BCE", "BDA", "BDD",
    "BBA", "BBB", "BBC", "BBD",
    "AED", "BBF",
)

IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png"}
PLACEHOLDER_TEXTS = ("ausmass ergaenzen", "ausmass ergänzen")
SPLIT_SALT = "split-v1"
TRAIN_SHARE = 0.70
VAL_SHARE = 0.15  # test = Rest (~0.15)
PILOT_MIN_SAMPLES = 30
TOKEN_PATTERN = re.compile(r"^\d+$")
HOLDING_KEY_PATTERN = re.compile(r"\d[\d.]*[-/]\d[\d.]*")

STATUS_APPROVED = 1  # TrainingSampleStatus.Approved
STATUS_DRAFT = 4     # TrainingSampleStatus.Draft

STUFEN = (
    "eingelesen",
    "persoenlich",
    "bild_ok",
    "box_ok",
    "maske_ok",
    "code_ok",
    "eval_sauber",
    "final_verwendbar",
)


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _load_json_array(path: Path) -> list[dict[str, Any]]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, list):
        raise ValueError(f"{path} muss ein JSON-Array enthalten.")
    if any(not isinstance(item, dict) for item in value):
        raise ValueError(f"{path} enthaelt einen ungueltigen Eintrag.")
    return value


def resolve_approved_by(cli_value: str | None, registry_path: Path) -> tuple[str, str]:
    """Zulaessiger Benutzername: CLI hat Vorrang, sonst approved_by aus dem Register."""
    if cli_value and cli_value.strip():
        return cli_value.strip(), "cli"
    if registry_path.is_file():
        document = json.loads(registry_path.read_text(encoding="utf-8-sig"))
        value = str(document.get("approved_by") or "").strip()
        if value:
            return value, "registry"
    raise ValueError(
        "Kein zulaessiger Benutzername: --approved-by fehlt und das Register "
        f"{registry_path} enthaelt kein Feld approved_by."
    )


def is_intake_candidate(item: dict[str, Any]) -> tuple[bool, str]:
    """Einlese-Filter: nur Approved + SourceType ManualCoding.

    Entwuerfe (Status 4/Draft), Teacher- und Auto-Vorschlaege werden gar nicht
    erst eingelesen. Rueckgabe: (aufgenommen, ueberspring_grund).
    """
    status = item.get("Status")
    if status == STATUS_DRAFT or status == "Draft":
        return False, "entwurf"
    if not (status == STATUS_APPROVED or status == "Approved"):
        return False, "status_sonstige"
    source_type = str(item.get("SourceType") or "").strip().casefold()
    if source_type != "manualcoding":
        return False, "quelle_sonstige"
    return True, ""


def check_personal(item: dict[str, Any], approved_by: str) -> str:
    """Persoenliche Bestaetigung; liefert den Ablehnungsgrund oder ''."""
    if item.get("HumanConfirmed") is not True:
        return "Keine persoenliche Bestaetigung (HumanConfirmed != true)."
    confirmed_by = str(item.get("ConfirmedByUser") or "").strip()
    if not confirmed_by:
        return "ConfirmedByUser ist nicht gesetzt."
    if confirmed_by.casefold() != approved_by.strip().casefold():
        return f"Bestaetigt durch '{confirmed_by}' statt '{approved_by.strip()}'."
    if not isinstance(item.get("Corrected"), bool):
        return "Persoenliche Entscheidung fehlt (Corrected ist nicht gesetzt)."
    if not str(item.get("ConfirmedAtUtc") or "").strip():
        return "Zeitpunkt der persoenlichen Bestaetigung fehlt."
    match_level = str(item.get("MatchLevel") or "").strip().casefold()
    if match_level not in ("reviewapproved", "reviewcorrected"):
        return (
            "MatchLevel ist keine persoenliche Review-Freigabe "
            "(ReviewApproved/ReviewCorrected)."
        )
    return ""


def check_image(frame_path: Path) -> tuple[str, int, int]:
    """Bild vorhanden, JPEG/PNG und lesbar. Rueckgabe: (grund, breite, hoehe)."""
    if not frame_path.is_file():
        return f"Bilddatei fehlt: {frame_path}", 0, 0
    if frame_path.suffix.casefold() not in IMAGE_SUFFIXES:
        return f"Bild ist kein JPEG/PNG: {frame_path.name}", 0, 0
    try:
        with Image.open(frame_path) as image:
            if image.format not in ("JPEG", "PNG"):
                return f"Bildformat ist kein JPEG/PNG ({image.format}).", 0, 0
            width, height = image.size
            image.load()
    except Exception as exc:
        return f"Bild nicht lesbar ({exc.__class__.__name__}).", 0, 0
    if width <= 0 or height <= 0:
        return "Bildmasse sind ungueltig.", 0, 0
    return "", width, height


def check_bbox(item: dict[str, Any]) -> str:
    """Normalisierte Box: positive Groesse und vollstaendig innerhalb des Bildes."""
    if item.get("HasBbox") is not True:
        return "Keine BBox vorhanden (HasBbox != true)."
    try:
        cx = float(item.get("BboxXCenter"))
        cy = float(item.get("BboxYCenter"))
        width = float(item.get("BboxWidth"))
        height = float(item.get("BboxHeight"))
    except (TypeError, ValueError):
        return "BBox-Werte sind nicht lesbar."
    if not (0.0 < width <= 1.0) or not (0.0 < height <= 1.0):
        return f"BBox-Groesse ausserhalb (0,1]: w={width}, h={height}."
    if not (0.0 <= cx <= 1.0) or not (0.0 <= cy <= 1.0):
        return f"BBox-Zentrum ausserhalb des Bildes: x={cx}, y={cy}."
    left = cx - width / 2.0
    right = cx + width / 2.0
    top = cy - height / 2.0
    bottom = cy + height / 2.0
    epsilon = 1e-9
    if left < -epsilon or top < -epsilon or right > 1.0 + epsilon or bottom > 1.0 + epsilon:
        return (
            "BBox ragt ueber den Bildrand: "
            f"links={left}, oben={top}, rechts={right}, unten={bottom}."
        )
    return ""


def check_mask(
    rle: Any,
    mask_width: Any,
    mask_height: Any,
    image_width: int,
    image_height: int,
    bbox_x_center: Any,
    bbox_y_center: Any,
    bbox_width: Any,
    bbox_height: Any,
) -> str:
    """Strikte SAM-RLE-Pruefung, Spiegel von SamMaskFormatValidator (C#).

    Format "start,run1,run2,...": Startwert 0/1, positive Zahl-Runs,
    Laufsumme == w*h, mindestens 1 Maskenpixel; zusaetzlich muessen die
    Maskenmasse zum gelesenen Bild passen und ein echter Maskenpixel-Mittelpunkt
    innerhalb der Hand-Box liegen.
    """
    text = str(rle or "").strip()
    if not text:
        return "Keine Masken-RLE vorhanden."
    if not isinstance(mask_width, int) or not isinstance(mask_height, int) \
            or mask_width <= 0 or mask_height <= 0:
        return "Masken-Bildmasse fehlen oder sind ungueltig."

    parts = text.split(",")
    if len(parts) < 2:
        return "Masken-RLE nicht lesbar (keine Runs)."
    if any(not TOKEN_PATTERN.match(part) for part in parts):
        return "Masken-RLE nicht lesbar (defektes Token)."

    start_value = int(parts[0])
    if start_value not in (0, 1):
        return "Masken-RLE nicht lesbar (Startwert muss 0 oder 1 sein)."
    if any(int(part) <= 0 for part in parts[1:]):
        return "Masken-RLE nicht lesbar (Runs muessen positiv sein)."

    box_left = float(bbox_x_center) - float(bbox_width) / 2.0
    box_right = float(bbox_x_center) + float(bbox_width) / 2.0
    box_top = float(bbox_y_center) - float(bbox_height) / 2.0
    box_bottom = float(bbox_y_center) + float(bbox_height) / 2.0
    epsilon = 1e-12
    allowed_min_col = max(0, math.ceil(box_left * mask_width - 0.5 - epsilon))
    allowed_max_col = min(
        mask_width - 1,
        math.floor(box_right * mask_width - 0.5 + epsilon),
    )
    allowed_min_row = max(0, math.ceil(box_top * mask_height - 0.5 - epsilon))
    allowed_max_row = min(
        mask_height - 1,
        math.floor(box_bottom * mask_height - 0.5 + epsilon),
    )

    run_sum = 0
    mask_pixels = 0
    intersects_box = False
    position = 0
    current_is_mask = start_value == 1
    for part in parts[1:]:
        run = int(part)
        run_sum += run
        if current_is_mask:
            mask_pixels += run
            if run > 0:
                run_start = position
                run_end = position + run - 1
                start_row, start_col = divmod(run_start, mask_width)
                end_row, end_col = divmod(run_end, mask_width)
                first_row = max(start_row, allowed_min_row)
                last_row = min(end_row, allowed_max_row)
                for row in range(first_row, last_row + 1):
                    run_left = start_col if row == start_row else 0
                    run_right = end_col if row == end_row else mask_width - 1
                    if run_left <= allowed_max_col and run_right >= allowed_min_col:
                        intersects_box = True
                        break
        position += run
        current_is_mask = not current_is_mask

    expected = mask_width * mask_height
    if run_sum != expected:
        return f"Masken-RLE passt nicht zu den Bildmassen ({run_sum} statt {expected} Pixel)."
    if mask_pixels == 0:
        return "Maske enthaelt keine Pixel (Leermaske)."
    if (mask_width, mask_height) != (image_width, image_height):
        return (
            f"Masken-Bildmasse {mask_width}x{mask_height} passt nicht zum "
            f"Bild {image_width}x{image_height}."
        )
    if not intersects_box:
        return "Maske gehoert nicht zur Hand-Box (kein Maskenpixel darin)."
    return ""


def main_code(code: Any) -> str:
    """Hauptcode = erste drei Zeichen des VSA-Codes, grossgeschrieben."""
    return str(code or "").strip().upper()[:3]


def has_placeholder_text(description: Any) -> bool:
    text = str(description or "").casefold()
    return any(marker in text for marker in PLACEHOLDER_TEXTS)


def normalize_holding_key(case_id: Any) -> str | None:
    """Echte Haltung analog EvalContaminationGuard auf ein Schachtpaar bringen.

    Freitext, leere Werte sowie foto_*/gold_inbox_*-Pseudo-IDs ergeben bewusst
    ``None``. Ein unsicherer Wert darf niemals als release-faehige Haltung gelten.
    """
    text = str(case_id or "").strip()
    if not text:
        return None
    match = HOLDING_KEY_PATTERN.search(text)
    if not match:
        return None
    parts = re.split(r"[-/]", match.group(0), maxsplit=1)
    if len(parts) != 2:
        return None

    def strip_area_prefix(value: str) -> str:
        return value.rsplit(".", maxsplit=1)[-1] if "." in value else value

    left = strip_area_prefix(parts[0])
    right = strip_area_prefix(parts[1])
    return f"{left}-{right}" if left and right else None


def group_key(case_id: str, image_sha256: str) -> str:
    """Einzelsample-Gruppe; Komponentenbildung erfolgt spaeter."""
    holding_key = normalize_holding_key(case_id)
    return f"haltung:{holding_key}" if holding_key else f"bild:{image_sha256}"


def split_role(key: str) -> str:
    """Deterministische Rolle je Gruppe ueber SHA-256('split-v1|<gruppe>')."""
    digest = hashlib.sha256(f"{SPLIT_SALT}|{key}".encode("utf-8")).digest()
    value = int.from_bytes(digest[:8], "big") / float(1 << 64)
    if value < TRAIN_SHARE:
        return "train"
    if value < TRAIN_SHARE + VAL_SHARE:
        return "val"
    return "test"


def _load_eval_hashes(eval_images_dir: Path) -> dict[str, str]:
    """SHA-256 aller Bilder unterhalb von eval_set/images (rekursiv)."""
    hashes: dict[str, str] = {}
    if not eval_images_dir.is_dir():
        return hashes
    for path in sorted(eval_images_dir.rglob("*")):
        if path.is_file() and path.suffix.casefold() in IMAGE_SUFFIXES:
            hashes[_sha256_file(path)] = path.name
    return hashes


def _load_eval_holding_keys(eval_images_dir: Path) -> set[str]:
    """Reservierte Eval-Haltungen aus Kandidatenlisten und Dateinamen laden."""
    result: set[str] = set()
    eval_root = eval_images_dir.parent if eval_images_dir.name.casefold() == "images" \
        else eval_images_dir
    if not eval_root.is_dir():
        return result

    for candidates_path in sorted(eval_root.rglob("_candidates.json")):
        try:
            document = json.loads(candidates_path.read_text(encoding="utf-8-sig"))
            entries = document if isinstance(document, list) else document.get("candidates", [])
            if not isinstance(entries, list):
                continue
            for entry in entries:
                if not isinstance(entry, dict):
                    continue
                key = normalize_holding_key(entry.get("haltung_key"))
                if key:
                    result.add(key)
        except (OSError, UnicodeError, json.JSONDecodeError):
            # Eine defekte Untermenge darf die uebrigen Schutzlisten nicht aushebeln.
            continue

    for path in sorted(eval_root.rglob("*")):
        if path.is_file() and path.suffix.casefold() in IMAGE_SUFFIXES:
            key = normalize_holding_key(path.name)
            if key:
                result.add(key)
    return result


def _build_split_groups(
    samples: list[dict[str, Any]],
) -> dict[str, list[dict[str, Any]]]:
    """Haltungs- und Bildgleichheit als verbundene, leckagefreie Gruppen."""
    parent: dict[str, str] = {}

    def find(node: str) -> str:
        parent.setdefault(node, node)
        while parent[node] != node:
            parent[node] = parent[parent[node]]
            node = parent[node]
        return node

    def union(left: str, right: str) -> None:
        left_root = find(left)
        right_root = find(right)
        if left_root == right_root:
            return
        first, second = sorted((left_root, right_root), key=str.casefold)
        parent[second] = first

    for sample in samples:
        image_node = f"bild:{sample['image_sha256']}"
        find(image_node)
        holding_key = sample.get("haltung_key")
        if holding_key:
            union(image_node, f"haltung:{holding_key}")

    components: dict[str, list[dict[str, Any]]] = {}
    for sample in samples:
        root = find(f"bild:{sample['image_sha256']}")
        components.setdefault(root, []).append(sample)

    groups: dict[str, list[dict[str, Any]]] = {}
    for members in components.values():
        holdings = sorted(
            {str(item["haltung_key"]) for item in members if item.get("haltung_key")},
            key=str.casefold,
        )
        if len(holdings) == 1:
            key = f"haltung:{holdings[0]}"
        elif len(holdings) > 1:
            fingerprint = hashlib.sha256(
                "|".join(holdings).encode("utf-8")
            ).hexdigest()
            key = f"haltungsverbund:{fingerprint}"
        else:
            key = f"bild:{min(item['image_sha256'] for item in members)}"
        groups[key] = members
    return groups


def _read_negative_pool(negatives_dir: Path) -> list[dict[str, str]]:
    """Negativ-Pool-Stand: Anzahl und Hashes kuratierter Hintergrundbilder."""
    pool: list[dict[str, str]] = []
    if not negatives_dir.is_dir():
        return pool
    for path in sorted(negatives_dir.iterdir(), key=lambda p: p.name.casefold()):
        if path.is_file() and path.suffix.casefold() in IMAGE_SUFFIXES:
            pool.append({"datei": path.name, "sha256": _sha256_file(path)})
    return pool


def build_audit(
    samples_path: Path,
    registry_path: Path,
    eval_images_dir: Path,
    negatives_dir: Path,
    approved_by: str,
    approved_by_quelle: str,
    jetzt: datetime,
) -> dict[str, Any]:
    """Baut den Pruefbericht rein lesend; schreibt keine Dateien."""
    jetzt = jetzt.astimezone(timezone.utc)
    eval_hashes = _load_eval_hashes(eval_images_dir)
    eval_holding_keys = _load_eval_holding_keys(eval_images_dir)
    negative_pool = _read_negative_pool(negatives_dir)

    raw = _load_json_array(samples_path)
    skipped = {"entwurf": 0, "status_sonstige": 0, "quelle_sonstige": 0}
    candidates: list[dict[str, Any]] = []
    for item in raw:
        taken, skip_reason = is_intake_candidate(item)
        if taken:
            candidates.append(item)
        else:
            skipped[skip_reason] += 1

    stage_counts = {stage: 0 for stage in STUFEN}
    stage_counts["eingelesen"] = len(candidates)
    rejections: list[dict[str, str]] = []
    unknown_codes: dict[str, list[str]] = {}
    final_samples: list[dict[str, Any]] = []

    for item in candidates:
        sample_id = str(item.get("SampleId") or "").strip()

        reason = check_personal(item, approved_by)
        stage = "persoenlich"
        image_sha = ""
        width = height = 0
        if not reason:
            stage_counts["persoenlich"] += 1
            stage = "bild_ok"
            frame_path = Path(str(item.get("FramePath") or "").strip())
            reason, width, height = check_image(frame_path)
        if not reason:
            stage_counts["bild_ok"] += 1
            image_sha = _sha256_file(Path(str(item.get("FramePath") or "").strip()))
            stage = "box_ok"
            reason = check_bbox(item)
        if not reason:
            stage_counts["box_ok"] += 1
            stage = "maske_ok"
            reason = check_mask(
                item.get("SamMaskRle"),
                item.get("SamMaskImageWidth"),
                item.get("SamMaskImageHeight"),
                width,
                height,
                item.get("BboxXCenter"),
                item.get("BboxYCenter"),
                item.get("BboxWidth"),
                item.get("BboxHeight"),
            )
        if not reason:
            stage_counts["maske_ok"] += 1
            stage = "code_ok"
            code = main_code(item.get("Code"))
            if code not in GOLD_MAIN_CODES:
                reason = f"Hauptcode '{code}' ist nicht im persoenlichen Goldkatalog."
                unknown_codes.setdefault(code or "(leer)", []).append(sample_id)
        if not reason:
            stage_counts["code_ok"] += 1
            stage = "eval_sauber"
            if image_sha in eval_hashes:
                reason = f"Bild gehoert zum Eval-Set ({eval_hashes[image_sha]})."
            else:
                holding_key = normalize_holding_key(item.get("CaseId"))
                if holding_key and holding_key in eval_holding_keys:
                    reason = f"Haltung {holding_key} ist fuer das Eval-Set reserviert."

        if reason:
            rejections.append(
                {"sample_id": sample_id, "stufe": stage, "grund": reason}
            )
            continue

        stage_counts["eval_sauber"] += 1
        stage_counts["final_verwendbar"] += 1
        final_samples.append(
            {
                "sample_id": sample_id,
                "case_id": str(item.get("CaseId") or "").strip(),
                "haltung_key": normalize_holding_key(item.get("CaseId")),
                "code": str(item.get("Code") or "").strip().upper(),
                "hauptcode": main_code(item.get("Code")),
                "image_sha256": image_sha,
                "kb_text_offen": has_placeholder_text(item.get("Beschreibung")),
            }
        )

    # Doppelte Bilder ueber SHA-256 (Gruppen ausweisen, nichts entfernen).
    by_hash: dict[str, list[str]] = {}
    for sample in final_samples:
        by_hash.setdefault(sample["image_sha256"], []).append(sample["sample_id"])
    duplicate_groups = [
        {
            "sha256": sha,
            "anzahl": len(ids),
            "sample_ids": sorted(ids, key=str.casefold),
        }
        for sha, ids in sorted(by_hash.items())
        if len(ids) > 1
    ]

    # Split: gleiche Haltung und gleiche Bildbytes bilden transitive Komponenten.
    # Damit kann weder eine abweichende Schreibweise noch eine Dateikopie leaken.
    groups = _build_split_groups(final_samples)
    missing_holding_count = sum(
        1 for sample in final_samples if not sample["haltung_key"]
    )
    group_entries = []
    split_counts = {"train": 0, "val": 0, "test": 0}
    for key in sorted(groups, key=str.casefold):
        role = split_role(key)
        members = sorted(groups[key], key=lambda s: s["sample_id"].casefold())
        for member in members:
            member["rolle"] = role
            member["gruppe"] = key
        split_counts[role] += len(members)
        group_entries.append(
            {"gruppe": key, "rolle": role, "samples": len(members)}
        )

    # Piloten brauchen neben der Mindestmenge auch Daten auf beiden Seiten:
    # Training UND mindestens eine unabhaengige Val-/Test-Gruppe.
    per_code: dict[str, dict[str, int]] = {}
    for sample in final_samples:
        entry = per_code.setdefault(
            sample["hauptcode"],
            {"gesamt": 0, "train": 0, "val": 0, "test": 0},
        )
        entry["gesamt"] += 1
        entry[sample["rolle"]] += 1
    pilot_candidates = [
        {"code": code, **counts}
        for code, counts in sorted(
            per_code.items(),
            key=lambda pair: (-pair[1]["gesamt"], pair[0]),
        )
        if counts["gesamt"] >= PILOT_MIN_SAMPLES
    ]
    pilots = [
        candidate
        for candidate in pilot_candidates
        if candidate["train"] > 0
        and candidate["val"] + candidate["test"] > 0
    ]
    pilot_not_evaluable = [
        {
            **candidate,
            "grund": "Klasse liegt nicht in Training und Val/Test vor.",
        }
        for candidate in pilot_candidates
        if candidate not in pilots
    ]

    kb_text_offen = sum(1 for sample in final_samples if sample["kb_text_offen"])
    eval_hits = sum(1 for r in rejections if r["stufe"] == "eval_sauber")

    return {
        "schema_version": SCHEMA_VERSION,
        "bericht": "gold_stock_audit",
        "modus": "schreibfreie_pruefung",
        "zeitstempel_utc": jetzt.isoformat().replace("+00:00", "Z"),
        "eingaben": {
            "samples_pfad": str(samples_path),
            "samples_sha256": _sha256_file(samples_path),
            "registry_pfad": str(registry_path),
            "registry_sha256": (
                _sha256_file(registry_path) if registry_path.is_file() else None
            ),
            "approved_by": approved_by,
            "approved_by_quelle": approved_by_quelle,
            "eval_images_pfad": str(eval_images_dir),
            "eval_hashes_anzahl": len(eval_hashes),
            "eval_haltungen_anzahl": len(eval_holding_keys),
            "negatives_pfad": str(negatives_dir),
        },
        "einlesen": {
            "datei_gesamt": len(raw),
            "uebersprungen_entwurf": skipped["entwurf"],
            "uebersprungen_status_sonstige": skipped["status_sonstige"],
            "uebersprungen_quelle_sonstige": skipped["quelle_sonstige"],
            "eingelesen": len(candidates),
        },
        "pruefstufen": stage_counts,
        "verwerfungen": sorted(rejections, key=lambda r: r["sample_id"].casefold()),
        "unbekannte_codes": [
            {"code": code, "anzahl": len(ids), "sample_ids": sorted(ids, key=str.casefold)}
            for code, ids in sorted(unknown_codes.items())
        ],
        "duplikat_gruppen": duplicate_groups,
        "duplikat_gruppen_anzahl": len(duplicate_groups),
        "eval_treffer_ausgeschlossen": eval_hits,
        "eval_haltungen": sorted(eval_holding_keys, key=str.casefold),
        "kb_text_offen": kb_text_offen,
        "split": {
            "regel": (
                f"sha256('{SPLIT_SALT}|<gruppe>'), Ziel 70/15/15; "
                "Gruppe = normalisierte Haltung; identische Bilder verbinden "
                "Haltungen, bei fehlender Haltung ersatzweise Bild-SHA"
            ),
            "test_eingefroren_nur_markiert": True,
            "release_faehig": missing_holding_count == 0,
            "fehlende_haltungsidentitaet": missing_holding_count,
            "gruppen": group_entries,
            "bilder": split_counts,
        },
        "piloten_schwelle": PILOT_MIN_SAMPLES,
        "piloten": pilots,
        "piloten_nicht_auswertbar": pilot_not_evaluable,
        "hauptcode_verteilung": {
            code: counts["gesamt"]
            for code, counts in sorted(
                per_code.items(), key=lambda pair: (-pair[1]["gesamt"], pair[0])
            )
        },
        "negativ_pool": {
            "pfad": str(negatives_dir),
            "anzahl": len(negative_pool),
            "dateien": negative_pool,
        },
        "samples": final_samples,
    }


def write_report(audit: dict[str, Any], reports_dir: Path, jetzt: datetime) -> Path:
    """Schreibt genau den einen Bericht; sonst nichts."""
    reports_dir.mkdir(parents=True, exist_ok=True)
    stamp = jetzt.astimezone(timezone.utc).strftime("%Y%m%d_%H%M%S_%f")[:-3]
    path = reports_dir / f"gold_stock_audit_{stamp}.json"
    data = (json.dumps(audit, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    temporary = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    try:
        with temporary.open("xb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()
    return path


def format_console(audit: dict[str, Any]) -> str:
    lines = []
    lines.append("Gold-Trainingsbestand - schreibfreie Pruefung")
    lines.append(f"Zeitpunkt (UTC): {audit['zeitstempel_utc']}")
    lines.append(
        f"Eingaben: {audit['eingaben']['samples_pfad']} "
        f"(SHA-256 {audit['eingaben']['samples_sha256'][:12]}...), "
        f"approved_by={audit['eingaben']['approved_by']} "
        f"({audit['eingaben']['approved_by_quelle']})"
    )
    einlesen = audit["einlesen"]
    lines.append(
        f"Einlesen: {einlesen['datei_gesamt']} in Datei, "
        f"{einlesen['uebersprungen_entwurf']} Entwuerfe, "
        f"{einlesen['uebersprungen_status_sonstige']} sonstige Stati, "
        f"{einlesen['uebersprungen_quelle_sonstige']} sonstige Quellen uebersprungen"
    )
    stufen = audit["pruefstufen"]
    for stufe in STUFEN:
        lines.append(f"  {stufe:<18} {stufen[stufe]}")
    lines.append(f"Verwerfungen: {len(audit['verwerfungen'])}")
    for verwerfung in audit["verwerfungen"]:
        lines.append(
            f"  {verwerfung['sample_id']} [{verwerfung['stufe']}] {verwerfung['grund']}"
        )
    lines.append(f"Duplikatgruppen: {audit['duplikat_gruppen_anzahl']}")
    for gruppe in audit["duplikat_gruppen"]:
        lines.append(
            f"  {gruppe['sha256'][:12]}... x{gruppe['anzahl']}: "
            + ", ".join(gruppe["sample_ids"])
        )
    lines.append(f"Eval-Treffer ausgeschlossen: {audit['eval_treffer_ausgeschlossen']}")
    lines.append(
        f"kb_text_offen (Platzhalter 'Ausmass ergaenzen', KB/Qwen gesperrt): "
        f"{audit['kb_text_offen']}"
    )
    split = audit["split"]
    lines.append(
        f"Split (70/15/15, test eingefroren/nur markiert): "
        f"train={split['bilder']['train']}, val={split['bilder']['val']}, "
        f"test={split['bilder']['test']} aus {len(split['gruppen'])} Gruppen"
    )
    if not split["release_faehig"]:
        lines.append(
            "  NICHT release-faehig: "
            f"{split['fehlende_haltungsidentitaet']} Samples besitzen keine "
            "belastbare Haltungsidentitaet."
        )
    if len(split["gruppen"]) <= 20:
        for gruppe in split["gruppen"]:
            lines.append(
                f"  {gruppe['gruppe']:<24} {gruppe['rolle']:<6} {gruppe['samples']}"
            )
    else:
        lines.append("  Einzelne Split-Gruppen stehen im JSON-Bericht.")
    lines.append(
        f"Auswertbare Piloten (>={audit['piloten_schwelle']} Samples in Train und Val/Test): "
        + (", ".join(f"{p['code']}={p['gesamt']}" for p in audit["piloten"]) or "keine")
    )
    for pilot in audit["piloten_nicht_auswertbar"]:
        lines.append(
            f"  NOCH NICHT AUSWERTBAR: {pilot['code']}={pilot['gesamt']} "
            f"(train={pilot['train']}, val={pilot['val']}, test={pilot['test']})"
        )
    negativ = audit["negativ_pool"]
    lines.append(f"Negativ-Pool: {negativ['anzahl']} Bilder ({negativ['pfad']})")
    return "\n".join(lines)


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Schreibfreier Pruefbericht zum Gold-Trainingsbestand."
    )
    parser.add_argument(
        "--knowledge-root",
        type=Path,
        default=Path(os.getenv("SEWERSTUDIO_KNOWLEDGE_ROOT", r"C:\KI_BRAIN")),
    )
    parser.add_argument("--samples", type=Path, default=None,
                        help="Pfad zu training_samples.json (Default: <KnowledgeRoot>/training_samples.json).")
    parser.add_argument("--registry", type=Path, default=None,
                        help="Pfad zu export_registry_v1.json (Default: <KnowledgeRoot>/training/export_registry_v1.json).")
    parser.add_argument("--eval-images", type=Path, default=None,
                        help="Eval-Bilder (Default: <KnowledgeRoot>/eval_set/images).")
    parser.add_argument("--negatives-dir", type=Path, default=None,
                        help="Negativ-Pool (Default: <KnowledgeRoot>/training/negatives/bcc_pilot).")
    parser.add_argument("--reports-dir", type=Path, default=None,
                        help="Berichtsordner (Default: <KnowledgeRoot>/training/reports).")
    parser.add_argument(
        "--approved-by",
        default=None,
        help="Zulaessiger Benutzername (Default: approved_by aus dem Exportregister).",
    )
    return parser.parse_args()


def main() -> int:
    args = _parse_args()
    root = args.knowledge_root
    samples_path = args.samples or root / "training_samples.json"
    registry_path = args.registry or root / "training" / "export_registry_v1.json"
    eval_images = args.eval_images or root / "eval_set" / "images"
    negatives_dir = args.negatives_dir or root / "training" / "negatives" / "bcc_pilot"
    reports_dir = args.reports_dir or root / "training" / "reports"

    approved_by, quelle = resolve_approved_by(args.approved_by, registry_path)
    jetzt = datetime.now(timezone.utc)
    audit = build_audit(
        samples_path,
        registry_path,
        eval_images,
        negatives_dir,
        approved_by,
        quelle,
        jetzt,
    )
    report_path = write_report(audit, reports_dir, jetzt)
    print(format_console(audit))
    print(f"Bericht: {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
