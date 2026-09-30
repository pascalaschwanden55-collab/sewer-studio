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
import stat
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Mapping, Sequence

from PIL import Image

from haltungsidentitaet import physischer_schluessel
import negativsatz_pruefung as negativsatz

# Vertragskonstanten, JSON-Bausteine und Splitregel der Negativsaetze liegen in
# negativsatz_pruefung.py. Die bisherigen Namen bleiben hier fuer bestehende
# Aufrufer erhalten (z. B. proto_hard_negative_review, prepare_detect_gold,
# derive_negative_set_for_gold_audit).
from negativsatz_pruefung import (  # noqa: F401
    MIN_TRAINING_NEGATIVE_BYTES,
    NEGATIVE_QUEUE_PURPOSE,
    NEGATIVE_QUEUE_ROLE,
    NEGATIVE_REVIEW_DECISIONS,
    NEGATIVE_REVIEW_PURPOSE,
    NEGATIVE_SET_PILOT,
    NEGATIVE_SET_PURPOSE,
    NEGATIVE_SET_ROLE,
    NEGATIVE_SET_SCHEMA_VERSION,
    NEGATIVE_SPLIT_SALT,
    PROTO_IMAGE_ID_PREFIX,
    PROTO_ITEM_ID_PREFIX,
    PROTO_PILOT,
    PROTO_QUEUE_PURPOSE,
    PROTO_SET_PURPOSE,
)
from negativsatz_pruefung import canonical_json_bytes as _canonical_json_bytes
from negativsatz_pruefung import negative_split_map as _negative_split_map
from negativsatz_pruefung import require_count as _require_count
from negativsatz_pruefung import require_exact_fields as _require_exact_fields
from negativsatz_pruefung import require_sha256 as _require_sha256
from negativsatz_pruefung import strict_json_bytes as _strict_json_bytes


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
MINIMUM_MASK_CONTAINMENT_NUMERATOR = 4
MINIMUM_MASK_CONTAINMENT_DENOMINATOR = 5
MINIMUM_MASK_CONTAINMENT_PERCENT = 80
TOKEN_PATTERN = re.compile(r"^\d+$")
HOLDING_KEY_PATTERN = re.compile(r"\d[\d.]*[-/]\d[\d.]*")
PDF_GOLD_PROVENANCE_PATTERN = re.compile(
    r"\APDF-Operateurreferenz: (?P<document>[^;\r\n]+); "
    r"SHA-256=(?P<sha256>[0-9A-Fa-f]{64}); "
    r"Seite=(?P<page>[1-9][0-9]*); "
    r"Foto=(?P<photo>[^;\r\n]+); "
    r"Zuordnung=(?P<match_kind>[a-z_]+)"
    r"(?:; CaseId [^;\r\n]+ -> [^;\r\n]+ \([^;\r\n]+\))*\Z",
    re.ASCII,
)
PDF_GOLD_MATCH_KINDS = ("same_block", "photo_id", "time_meter_text")
# Die Vertragskonstanten der Negativsaetze stehen in negativsatz_pruefung.py.
PROTO_ENDPOINT_PREFIX = re.compile(r"^\d{1,2}\.(.{4,})$")
REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
ACTIVE_CLASS_MAP_PATH = (
    REPOSITORY_ROOT / "training" / "class_maps" / "detect_class_map_v3.json"
)
ACTIVE_VSA_MANIFEST_PATH = (
    REPOSITORY_ROOT
    / "src"
    / "AuswertungPro.Next.UI"
    / "Data"
    / "vsa_kek_2020_catalog_manifest.json"
)

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


def _is_reparse_or_symlink(path: Path) -> bool:
    try:
        metadata = path.lstat()
    except OSError:
        return False
    attributes = getattr(metadata, "st_file_attributes", 0)
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return path.is_symlink() or bool(attributes & reparse_flag)


def _same_path(left: Path, right: Path) -> bool:
    return os.path.normcase(str(left.resolve())) == os.path.normcase(
        str(right.resolve())
    )


def _stored_path(knowledge_root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(knowledge_root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve())


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
    """Einlese-Filter: nur Approved + persoenlich pruefbare Goldquelle.

    Entwuerfe (Status 4/Draft), Teacher- und Auto-Vorschlaege werden gar nicht
    erst eingelesen. Rueckgabe: (aufgenommen, ueberspring_grund).
    """
    status = item.get("Status")
    if status == STATUS_DRAFT or status == "Draft":
        return False, "entwurf"
    if not (status == STATUS_APPROVED or status == "Approved"):
        return False, "status_sonstige"
    source_type = str(item.get("SourceType") or "").strip().casefold()
    if source_type not in ("manualcoding", "pdfphoto"):
        return False, "quelle_sonstige"
    return True, ""


def check_pdf_gold_provenance(notes: Any) -> str:
    """Prueft dieselbe strenge PDF-Herkunftsspur wie die C#-Goldregel."""
    if not isinstance(notes, str):
        return "PDF-Pruefspur fehlt oder ist kein Text."

    match = PDF_GOLD_PROVENANCE_PATTERN.fullmatch(notes)
    if match is None:
        return "PDF-Pruefspur ist unvollstaendig oder hat ein unbekanntes Format."

    document = match.group("document")
    if (
        document != document.strip()
        or len(document) <= len(".pdf")
        or not document.casefold().endswith(".pdf")
        or "/" in document
        or "\\" in document
        or any(ord(character) < 32 or 127 <= ord(character) <= 159 for character in document)
    ):
        return "PDF-Referenz ist kein sicherer PDF-Dateiname."

    page = int(match.group("page"))
    if page <= 0 or page > 2_147_483_647:
        return "PDF-Seite ist keine positive Seitenzahl."

    photo = match.group("photo")
    if (
        photo != photo.strip()
        or any(ord(character) < 32 or 127 <= ord(character) <= 159 for character in photo)
    ):
        return "PDF-Foto-ID ist ungueltig."

    match_kind = match.group("match_kind")
    if match_kind not in PDF_GOLD_MATCH_KINDS:
        return "PDF-Zuordnungsart ist nicht erlaubt."
    if match_kind != "same_block" and photo == "-":
        return "PDF-Foto-ID fehlt fuer diese Zuordnungsart."

    return ""


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
    confirmed_at_raw = item.get("ConfirmedAtUtc")
    if not isinstance(confirmed_at_raw, str) or not confirmed_at_raw.strip():
        return "Zeitpunkt der persoenlichen Bestaetigung fehlt."
    try:
        confirmed_at = datetime.fromisoformat(
            confirmed_at_raw.strip().replace("Z", "+00:00")
        )
    except ValueError:
        return "Zeitpunkt der persoenlichen Bestaetigung ist kein gueltiges ISO-Datum."
    if confirmed_at.tzinfo is None or confirmed_at.utcoffset() != timedelta(0):
        return "Zeitpunkt der persoenlichen Bestaetigung ist nicht in UTC."
    match_level = str(item.get("MatchLevel") or "").strip().casefold()
    if match_level not in ("reviewapproved", "reviewcorrected"):
        return (
            "MatchLevel ist keine persoenliche Review-Freigabe "
            "(ReviewApproved/ReviewCorrected)."
        )
    source_type = str(item.get("SourceType") or "").strip().casefold()
    if source_type == "pdfphoto":
        provenance_reason = check_pdf_gold_provenance(item.get("Notes"))
        if provenance_reason:
            return provenance_reason
        if not str(item.get("SourceReferenceCode") or "").strip():
            return "PDF-Operateur-Code fehlt."
        if not str(item.get("SourceReferenceDescription") or "").strip():
            return "PDF-Operateur-Befundtext fehlt."
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
    stored_mask_area_pixels: Any = None,
) -> str:
    """Strikte SAM-RLE-Pruefung, Spiegel von SamMaskFormatValidator (C#).

    Format "start,run1,run2,...": Startwert 0/1, positive Zahl-Runs,
    Laufsumme == w*h, mindestens 1 Maskenpixel; zusaetzlich muessen die
    Maskenmasse zum gelesenen Bild passen und mindestens 80 Prozent der echten
    Maskenpixel-Mittelpunkte innerhalb der Hand-Box liegen.
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
    mask_pixels_inside_box = 0
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
                    inside_left = max(run_left, allowed_min_col)
                    inside_right = min(run_right, allowed_max_col)
                    if inside_left <= inside_right:
                        mask_pixels_inside_box += inside_right - inside_left + 1
        position += run
        current_is_mask = not current_is_mask

    expected = mask_width * mask_height
    if run_sum != expected:
        return f"Masken-RLE passt nicht zu den Bildmassen ({run_sum} statt {expected} Pixel)."
    if mask_pixels == 0:
        return "Maske enthaelt keine Pixel (Leermaske)."
    if stored_mask_area_pixels is not None:
        if (
            isinstance(stored_mask_area_pixels, bool)
            or not isinstance(stored_mask_area_pixels, int)
        ):
            return "Gespeicherte Maskenflaeche ist keine ganze Pixelzahl."
        if stored_mask_area_pixels != mask_pixels:
            return (
                "Gespeicherte Maskenflaeche passt nicht zur RLE "
                f"({stored_mask_area_pixels} statt {mask_pixels} Pixel)."
            )
    if (mask_width, mask_height) != (image_width, image_height):
        return (
            f"Masken-Bildmasse {mask_width}x{mask_height} passt nicht zum "
            f"Bild {image_width}x{image_height}."
        )
    if mask_pixels_inside_box == 0:
        return (
            "Maske gehoert nicht zur Hand-Box "
            "(kein Vordergrundpixel innerhalb der Box; mindestens 80 % erforderlich)."
        )
    required_inside_pixels = (
        mask_pixels * MINIMUM_MASK_CONTAINMENT_NUMERATOR
        + MINIMUM_MASK_CONTAINMENT_DENOMINATOR
        - 1
    ) // MINIMUM_MASK_CONTAINMENT_DENOMINATOR
    if mask_pixels_inside_box < required_inside_pixels:
        containment_percent = mask_pixels_inside_box / mask_pixels * 100.0
        return (
            "Maske liegt zu weit ausserhalb der Hand-Box "
            f"({containment_percent:.1f} % innerhalb; "
            f"mindestens {MINIMUM_MASK_CONTAINMENT_PERCENT} % erforderlich)."
        )
    return ""


def main_code(code: Any) -> str:
    """Hauptcode = erste drei Zeichen des VSA-Codes, grossgeschrieben."""
    return str(code or "").strip().upper()[:3]


def normalized_code(code: Any) -> str:
    """Exakter VSA-Code ohne Darstellungs-Punkte, grossgeschrieben."""
    return str(code or "").strip().replace(".", "").upper()


def load_active_selectable_vsa_codes() -> tuple[frozenset[str], bytes]:
    """Liest exakt auswaehlbare Codes aus dem gebundenen aktiven Manifest."""
    manifest_bytes = ACTIVE_VSA_MANIFEST_PATH.read_bytes()
    manifest = json.loads(manifest_bytes.decode("utf-8-sig"))
    entries = manifest.get("codes") if isinstance(manifest, dict) else None
    if not isinstance(entries, list):
        raise ValueError("Das aktive VSA-Manifest enthaelt keine gueltige Codeliste.")

    codes: set[str] = set()
    for entry in entries:
        if not isinstance(entry, dict):
            raise ValueError("Das aktive VSA-Manifest enthaelt einen ungueltigen Code.")
        code = normalized_code(entry.get("code"))
        if (
            code
            and entry.get("isSelectable") is True
            and entry.get("isObservedExtension") is not True
        ):
            codes.add(code)
    if not codes:
        raise ValueError("Das aktive VSA-Manifest enthaelt keine auswaehlbaren Codes.")
    return frozenset(codes), manifest_bytes


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


def _physical_holding_key(holding_key: str) -> str:
    normalized = normalize_holding_key(holding_key)
    if normalized is None or normalized != holding_key:
        raise ValueError(
            f"Negativsatz besitzt keine normalisierte Haltungsidentitaet: {holding_key}"
        )
    parts = normalized.split("-", maxsplit=1)
    if len(parts) != 2 or not all(parts):
        raise ValueError(
            f"Negativsatz besitzt keine belastbare Haltungsidentitaet: {holding_key}"
        )
    return physischer_schluessel(normalized)


def _safe_negative_set_root(knowledge_root: Path, requested: Path) -> Path:
    sets_root = Path(
        os.path.abspath(
            knowledge_root / "training" / "negatives" / "sets"
        )
    )
    requested_path = Path(os.path.abspath(requested))
    if not sets_root.is_dir():
        raise ValueError(f"Der Negativsatz-Stamm fehlt: {sets_root}")
    if (
        os.path.normcase(str(requested_path.parent))
        != os.path.normcase(str(sets_root))
        or not _same_path(requested_path.parent, sets_root)
    ):
        raise ValueError(
            "Ein expliziter Negativsatz muss direkt unter "
            f"{sets_root} liegen: {requested_path}"
        )
    for path in (
        sets_root.parent.parent,
        sets_root.parent,
        sets_root,
        requested_path,
    ):
        if _is_reparse_or_symlink(path):
            raise ValueError(
                f"Ein Negativsatz darf keinen Link oder Junction verwenden: {path}"
            )
    if not requested_path.is_dir():
        raise ValueError(f"Der explizite Negativsatz fehlt: {requested_path}")
    try:
        resolved = requested_path.resolve(strict=True)
        if resolved.parent != sets_root.resolve(strict=True):
            raise ValueError
    except (OSError, ValueError) as error:
        raise ValueError(
            f"Der Negativsatz liegt ausserhalb des Satz-Stamms: {requested_path}"
        ) from error
    expected_entries = {"_manifest.json", "images", "receipts"}
    entries = {item.name: item for item in resolved.iterdir()}
    if set(entries) != expected_entries:
        raise ValueError(
            "Der Negativsatz muss exakt _manifest.json, images und receipts enthalten."
        )
    if (
        not entries["_manifest.json"].is_file()
        or not entries["images"].is_dir()
        or not entries["receipts"].is_dir()
    ):
        raise ValueError("Die Negativsatz-Struktur ist ungueltig.")
    if any(_is_reparse_or_symlink(item) for item in entries.values()):
        raise ValueError("Die Negativsatz-Struktur darf keine Links oder Junctions enthalten.")
    return resolved


def _verify_negative_set_files(
    set_root: Path,
    manifest: Mapping[str, Any],
) -> tuple[dict[str, Path], dict[str, bytes]]:
    hashes = manifest.get("hashes")
    if not isinstance(hashes, dict):
        raise ValueError("Das Negativsatz-Manifest besitzt keine Datei-Hashes.")
    if _require_count(manifest.get("hashes_count"), "hashes_count") != len(hashes):
        raise ValueError("hashes_count im Negativsatz ist falsch.")

    actual_files: dict[str, Path] = {}
    for directory_name in ("images", "receipts"):
        directory = set_root / directory_name
        for path in directory.iterdir():
            if _is_reparse_or_symlink(path) or not path.is_file():
                raise ValueError(
                    f"Negativsatz enthaelt fremde oder unsichere Datei: {path}"
                )
            relative = path.relative_to(set_root).as_posix()
            actual_files[relative] = path.resolve(strict=True)

    expected_receipts = {
        "receipts/review.json",
        "receipts/queue_manifest.json",
        "receipts/queue_candidates.json",
        "receipts/class_map.json",
    }
    actual_receipts = {
        relative
        for relative in actual_files
        if relative.startswith("receipts/")
    }
    if actual_receipts != expected_receipts:
        raise ValueError("Der Negativsatz besitzt nicht exakt die vier gebundenen Belege.")
    if set(hashes) != set(actual_files):
        raise ValueError("Die Hashabdeckung des Negativsatzes ist unvollstaendig.")

    receipt_bytes: dict[str, bytes] = {}
    for relative, path in actual_files.items():
        entry = _require_exact_fields(
            hashes.get(relative),
            {"sha256", "size_bytes"},
            f"Hashbeleg {relative}",
        )
        expected_sha = _require_sha256(
            entry.get("sha256"),
            f"Datei-Hash {relative}",
        )
        expected_size = _require_count(
            entry.get("size_bytes"),
            f"Dateigroesse {relative}",
        )
        if path.stat().st_size != expected_size or _sha256_file(path) != expected_sha:
            raise ValueError(f"Hash oder Groesse stimmt nicht: {relative}")
        if relative.startswith("receipts/"):
            data = path.read_bytes()
            if (
                len(data) != expected_size
                or hashlib.sha256(data).hexdigest() != expected_sha
            ):
                raise ValueError(f"Beleg wurde waehrend der Pruefung veraendert: {relative}")
            receipt_bytes[relative] = data
    return actual_files, receipt_bytes


def _validate_class_map_receipt(
    receipt_bytes: bytes,
    semantic: Mapping[str, Any],
) -> tuple[int, str, str, list[str]]:
    receipt_sha = hashlib.sha256(receipt_bytes).hexdigest()
    expected_sha = _require_sha256(
        semantic.get("class_map_sha256"),
        "Klassenkarten-Hash im Negativsatz",
    )
    if receipt_sha != expected_sha:
        raise ValueError("Der Klassenkarten-Beleg passt nicht zum Negativsatz.")
    if not ACTIVE_CLASS_MAP_PATH.is_file() or _sha256_file(ACTIVE_CLASS_MAP_PATH) != expected_sha:
        raise ValueError("Der Negativsatz passt nicht zur aktiven Detect-Klassenkarte.")

    class_map = _require_exact_fields(
        _strict_json_bytes(receipt_bytes, "Klassenkarten-Beleg"),
        {"version", "vsa_manifest_hash", "classes"},
        "Klassenkarten-Beleg",
    )
    version = class_map.get("version")
    if isinstance(version, bool) or version != 3:
        raise ValueError("Der Negativsatz braucht die Detect-Klassenkarte v3.")
    classes = class_map.get("classes")
    if not isinstance(classes, dict) or len(classes) != 15:
        raise ValueError("Der Negativsatz braucht exakt 15 Detect-Klassen.")
    names_by_id: dict[int, str] = {}
    for name, raw_id in classes.items():
        if (
            not isinstance(name, str)
            or not name.strip()
            or isinstance(raw_id, bool)
            or not isinstance(raw_id, int)
            or raw_id in names_by_id
        ):
            raise ValueError("Der Klassenkarten-Beleg enthaelt ungueltige Klassen.")
        names_by_id[raw_id] = name
    if set(names_by_id) != set(range(15)) or names_by_id[14] != NEGATIVE_SET_PILOT:
        raise ValueError("Die gebundene Klassenkarte ist nicht die freigegebene BCC-Karte.")
    ordered_names = [names_by_id[index] for index in range(15)]
    vsa_hash = _require_sha256(
        class_map.get("vsa_manifest_hash"),
        "VSA-Manifest-Hash in der Klassenkarte",
    )
    if (
        not ACTIVE_VSA_MANIFEST_PATH.is_file()
        or _sha256_file(ACTIVE_VSA_MANIFEST_PATH) != vsa_hash
    ):
        raise ValueError("Die Klassenkarte passt nicht zum aktiven VSA-Manifest.")
    if (
        semantic.get("class_map_version") != version
        or semantic.get("vsa_manifest_hash") != vsa_hash
        or semantic.get("class_names") != ordered_names
    ):
        raise ValueError("Klassenkarte und Negativsatz sind nicht fest verbunden.")
    return version, expected_sha, vsa_hash, ordered_names


def _proto_physical_holding_key(value: Any) -> str:
    """Geschuetzte Normalisierung fuer Protokoll-Haltungen: Bereichspraefix
    nur bei >= 4 Restzeichen entfernen (797.02 bleibt ein echter Knoten),
    richtungsunabhaengig; ohne A-B-Form Faellt auf die Rohschreibweise."""
    text = str(value or "").strip()
    match = HOLDING_KEY_PATTERN.search(text)
    if match is None:
        return text.casefold()
    parts = re.split(r"[-/]", match.group(0), maxsplit=1)
    if len(parts) != 2:
        return text.casefold()

    def guarded(part: str) -> str:
        prefix_match = PROTO_ENDPOINT_PREFIX.match(part)
        return prefix_match.group(1) if prefix_match else part

    return "|".join(sorted((guarded(parts[0]).casefold(), guarded(parts[1]).casefold())))


def _gold_split_roles_by_physical(knowledge_root: Path) -> dict[str, str]:
    """Gold-Split-Rollen je physischer Proto-Haltung (fuer die Gold-Ausrichtung
    von Negativsatz-Splits). Haltungen ohne belastbare Identitaet fehlen."""
    samples_path = knowledge_root / "training_samples.json"
    roles: dict[str, str] = {}
    try:
        samples = json.loads(samples_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return roles
    for sample in samples:
        case_id = str(sample.get("CaseId") or "")
        normalized = normalize_holding_key(case_id)
        if not normalized:
            continue
        roles[_physical_holding_key(normalized)] = split_role(f"haltung:{normalized}")
    return roles


def _pruefe_negativbild_datei(
    files: Mapping[str, Path],
    beleg: negativsatz.Bildbeleg,
    vertrag: negativsatz.Satzvertrag,
) -> tuple[Path, int]:
    """Dateigrenze je Bild: vorhanden, Mindestgroesse, Formatsignatur, Groesse, Hash, Endung."""
    image_path = files.get(beleg.relative_path)
    if image_path is None:
        raise ValueError(vertrag.text("bild_fehlt", pfad=beleg.relative_path))
    size_bytes = _require_count(beleg.image.get("size_bytes"), vertrag.text("bildgroesse"))
    with image_path.open("rb") as stream:
        signature = stream.read(8)
    if (
        size_bytes < MIN_TRAINING_NEGATIVE_BYTES
        or not negativsatz.hat_bildsignatur(beleg.image_format, signature)
        or image_path.stat().st_size != size_bytes
        or _sha256_file(image_path) != beleg.image_sha
        or image_path.suffix.casefold() != f".{beleg.image_format}"
    ):
        raise ValueError(vertrag.text("bild_datei"))
    return image_path, size_bytes


def _proto_eval_schluessel(knowledge_root: Path) -> set[str]:
    """Geschuetzte Haltungen fuer Proto-Eval-Ausnahmen: Eval-Kandidatenlisten und
    die Testgruppen des juengsten Gold-Audits (normalisiert und physisch)."""
    eval_keys = set()
    for candidates_path in sorted((knowledge_root / "eval_set").glob("**/_candidates.json")):
        try:
            document = json.loads(candidates_path.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError):
            continue
        items = document.get("candidates") if isinstance(document, dict) else document
        for entry in items or []:
            if isinstance(entry, dict):
                for raw in (entry.get("haltung_key"), entry.get("physical_holding_key")):
                    normalized = normalize_holding_key(raw)
                    if normalized:
                        eval_keys.add(normalized)
                        eval_keys.add(_physical_holding_key(normalized))
    reports = sorted((knowledge_root / "training" / "reports").glob("gold_stock_audit_*.json"))
    if reports:
        try:
            latest = json.loads(reports[-1].read_text(encoding="utf-8"))
            for gruppe in latest.get("split", {}).get("gruppen", []):
                if gruppe.get("rolle") == "test":
                    normalized = normalize_holding_key(
                        str(gruppe.get("gruppe") or "").removeprefix("haltung:"))
                    if normalized:
                        eval_keys.add(normalized)
                        eval_keys.add(_physical_holding_key(normalized))
        except (OSError, json.JSONDecodeError):
            pass
    return eval_keys


def _gib_negativsatz_frei(
    knowledge_root: Path,
    set_root: Path,
    manifest_path: Path,
    manifest_bytes: bytes,
    manifest: Mapping[str, Any],
    files: Mapping[str, Path],
    bindung: negativsatz.Satzbindung,
    output_images: list[dict[str, Any]],
    validation_count: int,
    vertrag: negativsatz.Satzvertrag,
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    """Freigabe erst nach allen Pruefungen: Satz, Belege, Klassenkarte und VSA-Manifest
    muessen seit Pruefbeginn unveraendert sein."""
    output_images.sort(key=lambda item: str(item["sha256"]))
    provenance = negativsatz.satz_provenienz(
        bindung,
        _stored_path(knowledge_root, set_root),
        len(output_images),
        validation_count,
    )
    if manifest_path.read_bytes() != manifest_bytes:
        raise ValueError(vertrag.text("manifest_geaendert"))
    manifest_hashes = manifest["hashes"]
    for relative, path in files.items():
        hash_entry = manifest_hashes[relative]
        if (
            path.stat().st_size != hash_entry["size_bytes"]
            or _sha256_file(path) != hash_entry["sha256"]
        ):
            raise ValueError(vertrag.text("datei_geaendert", pfad=relative))
    if (
        _sha256_file(ACTIVE_CLASS_MAP_PATH) != bindung.karte.sha256
        or _sha256_file(ACTIVE_VSA_MANIFEST_PATH) != bindung.karte.vsa_hash
    ):
        raise ValueError(
            "Klassenkarte oder VSA-Manifest wurde waehrend der Pruefung geaendert."
        )
    return output_images, provenance


def _read_reviewed_negative_set(
    knowledge_root: Path,
    requested: Path,
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    """Liest einen streng veroeffentlichten BCC-Negativsatz (bcc_hn_*).

    Reihenfolge: Satzordner, Satzkopf, Dateien/Hashes, Belegbindung,
    Klassenkarte, Queue, Kandidatenliste, Review, Bildbelege mit Datei- und
    Queue-Abgleich, Vollstaendigkeit, Split und zuletzt die Freigabe nach der
    Unveraendert-Pruefung. Proto-Saetze (proto_hn_*) prueft
    _read_proto_reviewed_negative_set.
    """
    set_root = _safe_negative_set_root(knowledge_root, requested)
    manifest_path = set_root / "_manifest.json"
    manifest_bytes = manifest_path.read_bytes()
    peek = _strict_json_bytes(manifest_bytes, "Negativsatz-Manifest")
    if isinstance(peek, dict) and peek.get("purpose") == PROTO_SET_PURPOSE:
        return _read_proto_reviewed_negative_set(
            knowledge_root, set_root, manifest_path, manifest_bytes
        )
    vertrag = negativsatz.BCC_VERTRAG
    kopf = negativsatz.pruefe_satzkopf(manifest_bytes, set_root.name, vertrag)
    files, receipts = _verify_negative_set_files(set_root, kopf.manifest)
    belege = negativsatz.pruefe_belegbindung(kopf.semantic, receipts, vertrag)
    karte = negativsatz.Klassenkarte(
        *_validate_class_map_receipt(belege.class_map_bytes, kopf.semantic)
    )

    queue = negativsatz.pruefe_queue_kopf(
        belege.queue_manifest_bytes, belege.queue_binding, kopf.semantic, karte, vertrag
    )
    queue_hashes = negativsatz.pruefe_queue_hashliste(
        queue.manifest, belege.candidates_bytes, belege.candidates_sha, vertrag
    )
    model_ids, queue_items = negativsatz.pruefe_bcc_auswahl(queue.manifest, queue.semantic)
    queue_by_id = negativsatz.pruefe_bcc_queue_bilder(
        queue_items, model_ids, _physical_holding_key
    )
    candidates_by_id = negativsatz.pruefe_kandidaten(
        belege.candidates_bytes, queue.manifest, queue_by_id, queue_hashes, vertrag
    )
    accepted_ids = negativsatz.pruefe_review(
        belege.review_bytes,
        belege.review_binding,
        queue_id=queue.queue_id,
        queue_manifest_sha=belege.queue_manifest_sha,
        candidates_sha=belege.candidates_sha,
        class_map_sha=karte.sha256,
        candidates_by_id=candidates_by_id,
        vertrag=vertrag,
    )

    semantic_images = negativsatz.pruefe_bildanzahl(kopf.semantic, kopf.manifest, vertrag)
    bindung = negativsatz.Satzbindung(
        set_id=kopf.set_id,
        manifest_sha=hashlib.sha256(manifest_bytes).hexdigest(),
        queue_id=queue.queue_id,
        queue_manifest_sha=belege.queue_manifest_sha,
        candidates_sha=belege.candidates_sha,
        review_sha=belege.review_sha,
        karte=karte,
    )
    output_images: list[dict[str, Any]] = []
    bilder = negativsatz.Bildsammlung.leer()
    referenced_image_paths: set[str] = set()
    for raw_image in semantic_images:
        beleg = negativsatz.pruefe_bildbeleg(
            raw_image, accepted_ids, bilder, _physical_holding_key, vertrag
        )
        referenced_image_paths.add(beleg.relative_path)
        image_path, size_bytes = _pruefe_negativbild_datei(files, beleg, vertrag)
        queue_item, candidate = negativsatz.queue_eintrag_zum_bild(
            beleg, queue_by_id, candidates_by_id, vertrag
        )
        negativsatz.pruefe_bcc_bild_gegen_queue(beleg, queue_item, vertrag)
        negativsatz.pruefe_bild_gegen_kandidat_und_hashliste(
            beleg, size_bytes, candidate, queue_hashes, vertrag
        )
        output_images.append(
            negativsatz.negativbild_eintrag(
                beleg, _stored_path(knowledge_root, image_path), bindung
            )
        )

    negativsatz.pruefe_bildabdeckung(set(files), referenced_image_paths, vertrag)
    negativsatz.pruefe_bcc_vollzaehlig(bilder.review_ids, accepted_ids)
    validation_count = negativsatz.pruefe_bcc_split(
        kopf.semantic, bilder, len(output_images)
    )
    return _gib_negativsatz_frei(
        knowledge_root,
        set_root,
        manifest_path,
        manifest_bytes,
        kopf.manifest,
        files,
        bindung,
        output_images,
        validation_count,
        vertrag,
    )


def _read_proto_reviewed_negative_set(
    knowledge_root: Path,
    set_root: Path,
    manifest_path: Path,
    manifest_bytes: bytes,
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    """Strenge Pruefung eines protokollbasierten Negativsatzes (proto_hn_*).

    Gleiche Bindungstiefe wie der BCC-Pfad, aber mit dem ehrlichen
    Geschwister-Vertrag: keine gebundenen Auswahlmodelle, kein Modelltrigger,
    protokollbasierte Auswahl. Das BCC-Verfahren bleibt unveraendert.
    Zusaetzlich: begruendete Ausnahmen fuer klassenfreie Bilder ohne
    belastbare oder mit eval-geschuetzter Haltung und die Gold-Ausrichtung
    des Splits.
    """
    vertrag = negativsatz.PROTO_VERTRAG
    kopf = negativsatz.pruefe_satzkopf(manifest_bytes, set_root.name, vertrag)
    files, receipts = _verify_negative_set_files(set_root, kopf.manifest)
    belege = negativsatz.pruefe_belegbindung(kopf.semantic, receipts, vertrag)
    karte = negativsatz.Klassenkarte(
        *_validate_class_map_receipt(belege.class_map_bytes, kopf.semantic)
    )

    queue = negativsatz.pruefe_queue_kopf(
        belege.queue_manifest_bytes, belege.queue_binding, kopf.semantic, karte, vertrag
    )
    negativsatz.pruefe_proto_auswahlregel(queue.semantic)
    queue_hashes = negativsatz.pruefe_queue_hashliste(
        queue.manifest, belege.candidates_bytes, belege.candidates_sha, vertrag
    )
    queue_items = negativsatz.pruefe_proto_auswahl(queue.manifest, queue.semantic)
    queue_by_id = negativsatz.pruefe_proto_queue_bilder(
        queue_items, _proto_physical_holding_key
    )
    candidates_by_id = negativsatz.pruefe_kandidaten(
        belege.candidates_bytes, queue.manifest, queue_by_id, queue_hashes, vertrag
    )
    accepted_ids = negativsatz.pruefe_review(
        belege.review_bytes,
        belege.review_binding,
        queue_id=queue.queue_id,
        queue_manifest_sha=belege.queue_manifest_sha,
        candidates_sha=belege.candidates_sha,
        class_map_sha=karte.sha256,
        candidates_by_id=candidates_by_id,
        vertrag=vertrag,
    )

    semantic_images = negativsatz.pruefe_bildanzahl(kopf.semantic, kopf.manifest, vertrag)
    bindung = negativsatz.Satzbindung(
        set_id=kopf.set_id,
        manifest_sha=hashlib.sha256(manifest_bytes).hexdigest(),
        queue_id=queue.queue_id,
        queue_manifest_sha=belege.queue_manifest_sha,
        candidates_sha=belege.candidates_sha,
        review_sha=belege.review_sha,
        karte=karte,
    )
    output_images: list[dict[str, Any]] = []
    bilder = negativsatz.Bildsammlung.leer()
    referenced_image_paths: set[str] = set()
    for raw_image in semantic_images:
        beleg = negativsatz.pruefe_bildbeleg(
            raw_image, accepted_ids, bilder, _physical_holding_key, vertrag
        )
        referenced_image_paths.add(beleg.relative_path)
        image_path, size_bytes = _pruefe_negativbild_datei(files, beleg, vertrag)
        queue_item, candidate = negativsatz.queue_eintrag_zum_bild(
            beleg, queue_by_id, candidates_by_id, vertrag
        )
        negativsatz.pruefe_proto_bild_gegen_queue(
            beleg, queue_item, normalize_holding_key, vertrag
        )
        negativsatz.pruefe_bild_gegen_kandidat_und_hashliste(
            beleg, size_bytes, candidate, queue_hashes, vertrag
        )
        output_images.append(
            negativsatz.negativbild_eintrag(
                beleg, _stored_path(knowledge_root, image_path), bindung
            )
        )

    negativsatz.pruefe_bildabdeckung(set(files), referenced_image_paths, vertrag)
    if bilder.review_ids != accepted_ids:
        # Dokumentierte Ausnahmen: klassenfreie Bilder ohne belastbare
        # Haltungsidentitaet oder mit kanonisch geschuetzter Eval-Haltung
        # werden ausgeschlossen, muessen aber vollzaehlig und begruendet sein.
        excluded_ids, excluded_eval_ids = negativsatz.pruefe_proto_ausnahmelisten(
            kopf.semantic, bilder.review_ids, accepted_ids
        )
        negativsatz.pruefe_proto_ausnahmen(
            excluded_ids,
            excluded_eval_ids,
            accepted_ids,
            queue_by_id,
            _proto_eval_schluessel(knowledge_root),
            normalize_holding_key,
            _physical_holding_key,
        )
    validation_count = negativsatz.pruefe_proto_split(
        kopf.semantic,
        bilder,
        len(output_images),
        lambda: _gold_split_roles_by_physical(knowledge_root),
    )
    return _gib_negativsatz_frei(
        knowledge_root,
        set_root,
        manifest_path,
        manifest_bytes,
        kopf.manifest,
        files,
        bindung,
        output_images,
        validation_count,
        vertrag,
    )


def read_training_negative_sources(
    knowledge_root: Path,
    negatives_dir: Path,
    negative_sets: Sequence[Path] = (),
    *,
    minimum_legacy_bytes: int = 0,
) -> tuple[tuple[dict[str, Any], ...], tuple[dict[str, Any], ...]]:
    """Liest Legacy-Negative und streng veroeffentlichte Negativsaetze.

    Explizite ``negative_sets`` besitzen niemals einen stillen Fallback: Jeder
    Satz wird inklusive Queue, Review, Klassenkarte, Bildbytes, Haltung und Split
    vollstaendig neu validiert.
    """
    root = Path(os.path.abspath(knowledge_root))
    legacy_root = Path(os.path.abspath(negatives_dir))
    images: list[dict[str, Any]] = []
    provenances: list[dict[str, Any]] = []
    seen_hashes: dict[str, str] = {}
    seen_set_ids: set[str] = set()
    seen_physical_holdings: dict[str, str] = {}

    if legacy_root.is_dir():
        for path in sorted(legacy_root.iterdir(), key=lambda item: item.name.casefold()):
            if not path.is_file() or path.suffix.casefold() not in IMAGE_SUFFIXES:
                continue
            resolved = path.resolve()
            if resolved.stat().st_size < minimum_legacy_bytes:
                raise ValueError(f"Negativbild ist zu klein oder unlesbar: {resolved}")
            image_sha = _sha256_file(resolved)
            if image_sha in seen_hashes:
                raise ValueError(
                    "Der Negativ-Pool enthaelt dasselbe Bild mehrfach: "
                    f"{seen_hashes[image_sha]}, {resolved}"
                )
            seen_hashes[image_sha] = str(resolved)
            images.append(
                {
                    "path": _stored_path(root, resolved),
                    "sha256": image_sha,
                }
            )

    for requested in negative_sets:
        set_images, provenance = _read_reviewed_negative_set(root, Path(requested))
        set_id = str(provenance["set_id"])
        if set_id in seen_set_ids:
            raise ValueError(f"Negativsatz wurde mehrfach angegeben: {set_id}")
        seen_set_ids.add(set_id)
        for image in set_images:
            image_sha = str(image["sha256"])
            physical = str(image["physical_holding_key"])
            if image_sha in seen_hashes:
                raise ValueError(
                    "Negativbild ist ueber mehrere Quellen doppelt: "
                    f"{seen_hashes[image_sha]}, {image['path']}"
                )
            if physical in seen_physical_holdings:
                raise ValueError(
                    "Physische Haltung ist ueber mehrere Negativsaetze doppelt: "
                    f"{physical} ({seen_physical_holdings[physical]}, {set_id})"
                )
            seen_hashes[image_sha] = str(image["path"])
            seen_physical_holdings[physical] = set_id
            images.append(image)
        provenances.append(provenance)
    return tuple(images), tuple(provenances)


def build_audit(
    samples_path: Path,
    registry_path: Path,
    eval_images_dir: Path,
    negatives_dir: Path,
    approved_by: str,
    approved_by_quelle: str,
    jetzt: datetime,
    negative_sets: Sequence[Path] = (),
) -> dict[str, Any]:
    """Baut den Pruefbericht rein lesend; schreibt keine Dateien."""
    jetzt = jetzt.astimezone(timezone.utc)
    eval_hashes = _load_eval_hashes(eval_images_dir)
    eval_holding_keys = _load_eval_holding_keys(eval_images_dir)
    negative_set_paths = tuple(
        Path(os.path.abspath(path)) for path in negative_sets
    )
    negative_images, negative_set_provenance = read_training_negative_sources(
        samples_path.resolve().parent,
        negatives_dir,
        negative_set_paths,
        minimum_legacy_bytes=MIN_TRAINING_NEGATIVE_BYTES,
    )
    eval_physical_holdings = {
        _physical_holding_key(holding) for holding in eval_holding_keys
    }
    for negative_image in negative_images:
        image_sha = str(negative_image["sha256"])
        if image_sha in eval_hashes:
            raise ValueError(
                "Ein Negativbild gehoert bereits zum geschuetzten Eval-Bestand: "
                f"{negative_image['path']}"
            )
        physical = negative_image.get("physical_holding_key")
        if physical is not None and str(physical) in eval_physical_holdings:
            raise ValueError(
                "Eine Negativsatz-Haltung gehoert bereits zum geschuetzten "
                f"Eval-Bestand: {negative_image['holding_key']}"
            )
    negative_pool: list[dict[str, Any]] = []
    for image in negative_images:
        if image.get("source_type") == "reviewed_negative_set":
            negative_pool.append(
                {"datei": image["path"]}
                | {key: value for key, value in image.items() if key != "path"}
            )
        else:
            negative_pool.append(
                {
                    "datei": Path(str(image["path"])).name,
                    "sha256": image["sha256"],
                }
            )
    legacy_negative_count = sum(
        image.get("source_type") is None for image in negative_images
    )
    strict_negative_count = len(negative_images) - legacy_negative_count
    if legacy_negative_count and strict_negative_count:
        negative_registry_mode = "diagnose_gemischt_nicht_exportierbar"
    elif strict_negative_count:
        negative_registry_mode = "streng_reviewte_saetze"
    elif legacy_negative_count:
        negative_registry_mode = "legacy"
    else:
        negative_registry_mode = "leer"

    selectable_vsa_codes, active_manifest_bytes = load_active_selectable_vsa_codes()
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
                item.get("SamMaskAreaPixels"),
            )
        if not reason:
            stage_counts["maske_ok"] += 1
            stage = "code_ok"
            exact_code = normalized_code(item.get("Code"))
            family = main_code(exact_code)
            if exact_code not in selectable_vsa_codes:
                reason = (
                    f"Code '{exact_code}' ist nicht exakt als auswaehlbarer "
                    "VSA-Code im aktiven Katalog vorhanden."
                )
                unknown_codes.setdefault(exact_code or "(leer)", []).append(sample_id)
            elif family not in GOLD_MAIN_CODES:
                reason = (
                    f"Hauptcode '{family}' ist nicht im persoenlichen Goldkatalog."
                )
                unknown_codes.setdefault(exact_code or "(leer)", []).append(sample_id)
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
    if ACTIVE_VSA_MANIFEST_PATH.read_bytes() != active_manifest_bytes:
        raise ValueError(
            "Das aktive VSA-Manifest wurde waehrend der Pruefung veraendert."
        )

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
            "vsa_manifest_pfad": str(ACTIVE_VSA_MANIFEST_PATH),
            "vsa_manifest_sha256": hashlib.sha256(
                active_manifest_bytes
            ).hexdigest(),
            "eval_images_pfad": str(eval_images_dir),
            "eval_hashes_anzahl": len(eval_hashes),
            "eval_haltungen_anzahl": len(eval_holding_keys),
            "negatives_pfad": str(negatives_dir),
            "negative_set_pfade": [
                str(path) for path in negative_set_paths
            ],
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
            "sets": list(negative_set_provenance),
            "registry_modus": negative_registry_mode,
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
    lines.append(
        f"Negativ-Pool: {negativ['anzahl']} Bilder ({negativ['pfad']}), "
        f"{len(negativ.get('sets', []))} streng reviewte Saetze; "
        f"Registry-Modus={negativ.get('registry_modus', 'legacy')}"
    )
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
    parser.add_argument(
        "--negative-set",
        type=Path,
        action="append",
        default=[],
        help=(
            "Expliziter, veroeffentlichter Negativsatz unter "
            "<KnowledgeRoot>/training/negatives/sets; wiederholbar."
        ),
    )
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
        args.negative_set,
    )
    report_path = write_report(audit, reports_dir, jetzt)
    print(format_console(audit))
    print(f"Bericht: {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
