"""Einmaliger Import des Altbestands ``gold_labels`` als persoenliche Goldsamples.

Der Altbestand (VideoLabelTool, Stand <= 2026-07-24) enthaelt handgezeichnete
Hand-Boxen und SAM-RLE-Masken des Besitzers. Dieses Werkzeug wandelt sie in
TrainingSamples (SourceType ``ManualCoding``, MatchLevel ``ReviewApproved``) um
und haengt sie an ``training_samples.json`` an. Bilder werden im aktuellen
``gold_frames`` per Dateiname gesucht und per Inhalt gegen Duplikate geprueft.

Fail-closed wie der Gold-Audit: Bild lesbar, Box im Bild, RLE-Summe == w*h,
>=80 % der Maskenpixel in der Box, kein Eval-Treffer, kein Duplikat.
Ohne ``--apply`` bleibt der Lauf schreibfrei (Vorschau).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image

SCRIPT_ROOT = Path(__file__).resolve().parent
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

import gold_stock_audit as audit_tools

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
VSA_MANIFEST_PATH = (
    REPOSITORY_ROOT
    / "src"
    / "AuswertungPro.Next.UI"
    / "Data"
    / "vsa_kek_2020_catalog_manifest.json"
)

IMAGE_SUFFIXES = {".png", ".jpg", ".jpeg"}
STATUS_APPROVED = 1
KB_INDEX_NONE = 0
MIN_CONTAINMENT = 0.80


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _utc_stamp(moment: datetime) -> str:
    return (
        moment.strftime("%Y-%m-%dT%H:%M:%S.")
        + f"{moment.microsecond:06d}0Z"
    )


def _load_catalog_titles(path: Path) -> dict[str, str]:
    document = json.loads(path.read_text(encoding="utf-8"))
    return {
        entry["code"]: entry.get("title") or entry["code"]
        for entry in document["codes"]
    }


def _index_current_frames(gold_root: Path) -> dict[str, Path]:
    index: dict[str, Path] = {}
    for dirpath, _dirnames, filenames in os.walk(gold_root):
        for name in filenames:
            if Path(name).suffix.casefold() in IMAGE_SUFFIXES:
                index.setdefault(name, Path(dirpath) / name)
    return index


def _parse_rle(text: str, width: int, height: int):
    """Prueft die RLE und liefert (maskenpixel, pixel_in_box_anteil_ok) oder None."""
    parts = text.split(",")
    if len(parts) < 2:
        return None
    try:
        values = [int(part) for part in parts]
    except ValueError:
        return None
    if values[0] not in (0, 1) or any(run <= 0 for run in values[1:]):
        return None
    if sum(values[1:]) != width * height:
        return None
    mask_pixels = sum(
        run for index, run in enumerate(values[1:]) if (index % 2 == 0) == (values[0] == 1)
    )
    if mask_pixels <= 0:
        return None
    return values, mask_pixels


def _containment_ok(values, width, height, box) -> bool:
    """Spiegelt die 80-%-Regel aus gold_stock_audit.check_mask."""
    xc, yc, w, h = box
    left, right = xc - w / 2.0, xc + w / 2.0
    top, bottom = yc - h / 2.0, yc + h / 2.0
    min_col = max(0, math.ceil(left * width - 0.5 - 1e-12))
    max_col = min(width - 1, math.floor(right * width - 0.5 + 1e-12))
    min_row = max(0, math.ceil(top * height - 0.5 - 1e-12))
    max_row = min(height - 1, math.floor(bottom * height - 0.5 + 1e-12))

    position = 0
    is_mask = values[0] == 1
    mask_pixels = 0
    inside = 0
    for run in values[1:]:
        if is_mask:
            mask_pixels += run
            run_start, run_end = position, position + run - 1
            start_row, start_col = divmod(run_start, width)
            end_row, end_col = divmod(run_end, width)
            for row in range(max(start_row, min_row), min(end_row, max_row) + 1):
                run_left = start_col if row == start_row else 0
                run_right = end_col if row == end_row else width - 1
                inside_left = max(run_left, min_col)
                inside_right = min(run_right, max_col)
                if inside_left <= inside_right:
                    inside += inside_right - inside_left + 1
        position += run
        is_mask = not is_mask
    return inside >= math.ceil(mask_pixels * MIN_CONTAINMENT)


def build_samples(alt_root: Path, knowledge_root: Path, stamp: str, limit: int | None):
    gold_root = knowledge_root / "gold_frames"
    titles = _load_catalog_titles(VSA_MANIFEST_PATH)
    frames = _index_current_frames(gold_root)
    eval_hashes = audit_tools._load_eval_hashes(knowledge_root / "eval_set" / "images")
    # Eval-Haltungsschutz physisch (beide Richtungen)
    eval_keys = audit_tools._load_eval_holding_keys(knowledge_root / "eval_set" / "images")
    eval_phys = {audit_tools._physical_holding_key(key) for key in eval_keys}

    existing_path = knowledge_root / "training_samples.json"
    existing = json.loads(existing_path.read_text(encoding="utf-8"))
    existing_ids = {str(s.get("SampleId")) for s in existing}
    existing_hashes: set[str] = set()
    for s in existing:
        fp = str(s.get("FramePath") or "")
        if fp and Path(fp).is_file():
            existing_hashes.add(_sha256_file(Path(fp)))

    created: list[dict] = []
    skipped: dict[str, int] = {}
    seen_run: set[str] = set()

    def skip(reason: str) -> None:
        skipped[reason] = skipped.get(reason, 0) + 1

    label_files = sorted(alt_root.glob("*/*.json"))
    for label_path in label_files:
        if limit is not None and len(created) >= limit:
            break
        folder = label_path.parent.name
        if folder == "LEER":
            continue
        try:
            label = json.loads(label_path.read_text(encoding="utf-8"))
        except Exception:
            skip("json_unlesbar")
            continue
        frame_name = str(label.get("frame") or "")
        code = str(label.get("code") or "").strip().upper()
        holding = str(label.get("haltung") or "").strip()
        box = label.get("box_norm")
        rle = str(label.get("mask_rle") or "").strip()
        if not frame_name or not code or not holding or not isinstance(box, list) or len(box) != 4 or not rle:
            skip("label_unvollstaendig")
            continue
        if audit_tools.main_code(code) not in audit_tools.GOLD_MAIN_CODES:
            skip("code_nicht_im_goldkatalog")
            continue
        image_path = frames.get(frame_name)
        if image_path is None:
            skip("bild_nicht_in_gold_frames")
            continue
        holding_key = audit_tools.normalize_holding_key(holding)
        if not holding_key:
            skip("haltung_ungueltig")
            continue
        if audit_tools._physical_holding_key(holding_key) in eval_phys:
            skip("eval_geschuetzt")
            continue
        image_sha = _sha256_file(image_path)
        if image_sha in eval_hashes:
            skip("eval_bild")
            continue
        if image_sha in existing_hashes or image_sha in seen_run:
            skip("duplikat")
            continue
        try:
            with Image.open(image_path) as image:
                if image.format not in ("JPEG", "PNG"):
                    skip("bildformat")
                    continue
                width, height = image.size
                image.load()
        except Exception:
            skip("bild_unlesbar")
            continue
        try:
            box_values = [float(value) for value in box]
        except (TypeError, ValueError):
            skip("box_unlesbar")
            continue
        xc, yc, w, h = box_values
        if not (0 < w <= 1 and 0 < h <= 1 and 0 <= xc <= 1 and 0 <= yc <= 1):
            skip("box_werte")
            continue
        if xc - w / 2 < -1e-9 or yc - h / 2 < -1e-9 or xc + w / 2 > 1 + 1e-9 or yc + h / 2 > 1 + 1e-9:
            skip("box_ausserhalb")
            continue
        parsed = _parse_rle(rle, width, height)
        if parsed is None:
            skip("maske_format")
            continue
        values, mask_pixels = parsed
        if not _containment_ok(values, width, height, (xc, yc, w, h)):
            skip("maske_ausserhalb_box")
            continue
        sample_id = "wb_" + hashlib.sha256(str(image_path).encode("utf-8")).hexdigest()[:12]
        if sample_id in existing_ids:
            skip("sample_id_kollision")
            continue
        seen_run.add(image_sha)
        created.append({
            "SampleId": sample_id,
            "CaseId": holding_key,
            "Code": code,
            "Beschreibung": titles.get(code, code),
            "MeterStart": 0,
            "MeterEnd": 0,
            "IsStreckenschaden": False,
            "TimeSeconds": float(label.get("protocol_time") or 0.0),
            "DetectedMeter": None,
            "MeterSource": "",
            "FramePath": str(image_path),
            "EvidenceFramePath": None,
            "Status": STATUS_APPROVED,
            "ExportedUtc": stamp,
            "Notes": "Altbestand gold_labels: handgezeichnete Box+SAM-Maske (VideoLabelTool), Import 2026-08-01",
            "TruthMeterCenter": None,
            "OdsDeltaMeters": None,
            "HasOsdMismatch": False,
            "Signature": f"{holding_key}|{code}|0.0|0.0",
            "FrameIndex": 0,
            "MatchLevel": "ReviewApproved",
            "KiCode": None,
            "KbCheck": None,
            "SourceType": "ManualCoding",
            "SourceReferenceCode": None,
            "SourceReferenceDescription": None,
            "CodeMeta": None,
            "TechniqueGrade": None,
            "AdditionalFramePaths": None,
            "KbIndexState": KB_INDEX_NONE,
            "InspectionDate": None,
            "TrainingEligible": True,
            "TrainingEligibilityReason": None,
            "HumanConfirmed": True,
            "Corrected": False,
            "ConfirmedByUser": "Besitzer",
            "ConfirmedAtUtc": stamp,
            "QualityGateLevel": "Green",
            "CentralDecision": None,
            "SnapshotError": None,
            "BboxXCenter": xc,
            "BboxYCenter": yc,
            "BboxWidth": w,
            "BboxHeight": h,
            "HasBbox": True,
            "SamMaskRle": rle,
            "SamMaskImageWidth": width,
            "SamMaskImageHeight": height,
            "SamMaskAreaPixels": mask_pixels,
            "SamMaskConfidence": None,
            "SamMaskLabel": None,
            "HasSamMask": True,
        })
    return existing, created, skipped


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--alt-root", type=Path,
                        default=Path(r"C:\KI_BRAIN_ALT_20260724_090413\gold_labels"))
    parser.add_argument("--limit", type=int, default=None,
                        help="Nur die ersten N verwendbaren Labels (Testlauf).")
    parser.add_argument("--apply", action="store_true",
                        help="Schreibt training_samples.json wirklich (mit Backup).")
    args = parser.parse_args()

    stamp = _utc_stamp(datetime.now(timezone.utc))
    existing, created, skipped = build_samples(
        args.alt_root, args.knowledge_root, stamp, args.limit)

    print(f"Bestehende Samples: {len(existing)}")
    print(f"Neu verwendbar: {len(created)}")
    if skipped:
        print("Uebersprungen: " + ", ".join(f"{k}={v}" for k, v in sorted(skipped.items())))
    per_code: dict[str, int] = {}
    for sample in created:
        main = audit_tools.main_code(sample["Code"])
        per_code[main] = per_code.get(main, 0) + 1
    print("Neu nach Hauptcode: " + json.dumps(dict(sorted(per_code.items()))))

    if not args.apply:
        print("Nur Vorschau. Es wurde nichts geschrieben (--apply fehlt).")
        return 0

    # Laufende App = zweiter Schreiber auf derselben Datei. Ihr naechster Save wuerde
    # unsere Ergaenzung stillschweigend ueberschreiben (Audit 2026-08-14, M11).
    if _sewerstudio_laeuft():
        print(
            "ABBRUCH: SewerStudio.exe laeuft. Die App schreibt dieselbe "
            "training_samples.json. Bitte zuerst beenden.",
            file=sys.stderr)
        return 2

    samples_path = args.knowledge_root / "training_samples.json"
    backup = samples_path.with_suffix(
        ".json.bak_vor_goldlabels_" + datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S"))
    backup.write_bytes(samples_path.read_bytes())
    merged = existing + created
    _atomar_schreiben(
        samples_path,
        (json.dumps(merged, ensure_ascii=True, indent=2) + "\n").encode("utf-8"))
    print(f"Geschrieben: {samples_path} ({len(merged)} Samples)")
    print(f"Backup: {backup}")
    return 0


def _sewerstudio_laeuft() -> bool:
    """Wie in repair_gold_holding_ids.py: nur unter Windows pruefbar."""
    if os.name != "nt":
        return False
    result = subprocess.run(
        ["tasklist", "/FI", "IMAGENAME eq SewerStudio.exe", "/FO", "CSV", "/NH"],
        check=False,
        capture_output=True,
        text=True,
        timeout=10,
    )
    return '"SewerStudio.exe"' in result.stdout


def _atomar_schreiben(pfad: Path, daten: bytes) -> None:
    """Erst vollstaendig danebenschreiben, dann umbenennen.

    Ein direktes write_text laesst die Golddatei bei einem Absturz oder vollen
    Datentraeger halb geschrieben zurueck (Audit 2026-08-14, M11).
    """
    pfad.parent.mkdir(parents=True, exist_ok=True)
    temporaer = pfad.with_name(f".{pfad.name}.{os.getpid()}.tmp")
    try:
        with temporaer.open("xb") as strom:
            strom.write(daten)
            strom.flush()
            os.fsync(strom.fileno())
        os.replace(temporaer, pfad)
    finally:
        temporaer.unlink(missing_ok=True)


if __name__ == "__main__":
    raise SystemExit(main())
