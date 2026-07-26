"""Bereitet den getrennten BCC-Bogen-Pilot aus persoenlichem Hand-Gold vor.

Das Skript schreibt keinen YOLO-Datensatz. Es erzeugt ausschliesslich das
menschlich gebundene Exportregister, das danach vom gemeinsamen C#-Exportplaner
gelesen wird. Ohne --execute bleibt der Lauf schreibfrei.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


PILOT_MAIN_CODE = "BCC"
PILOT_NAME = "BCC_bogen"
MINIMUM_IMAGES = 30
VALIDATION_RATIO = 0.20
ALLOWED_MATCH_LEVELS = {"ReviewApproved", "ReviewCorrected"}
NEGATIVE_IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png"}
MIN_NEGATIVE_BYTES = 1024


@dataclass(frozen=True)
class PilotSample:
    sample_id: str
    case_id: str
    code: str
    frame_path: Path
    image_sha256: str
    confirmed_at_utc: str


@dataclass(frozen=True)
class PilotPreparation:
    registry_path: Path
    audit_path: Path
    selected_samples: tuple[PilotSample, ...]
    duplicate_sample_ids: tuple[str, ...]
    train_cases: tuple[str, ...]
    validation_cases: tuple[str, ...]
    protected_sets: tuple[dict[str, str], ...]
    negative_images: tuple[dict[str, str], ...] = ()

    @property
    def train_images(self) -> int:
        return sum(sample.case_id in self.train_cases for sample in self.selected_samples)

    @property
    def validation_images(self) -> int:
        return sum(sample.case_id in self.validation_cases for sample in self.selected_samples)


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


def _is_personal_complete_bcc(item: dict[str, Any], approved_by: str) -> bool:
    status = item.get("Status")
    approved_status = status == 1 or status == "Approved"
    return (
        approved_status
        and item.get("HumanConfirmed") is True
        and item.get("Corrected") is not None
        and str(item.get("ConfirmedByUser") or "").strip().casefold()
        == approved_by.strip().casefold()
        and str(item.get("SourceType") or "").strip().casefold() == "manualcoding"
        and str(item.get("MatchLevel") or "").strip() in ALLOWED_MATCH_LEVELS
        and item.get("HasBbox") is True
        and item.get("HasSamMask") is True
        and str(item.get("Code") or "").strip().upper().startswith(PILOT_MAIN_CODE)
    )


def _is_within(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def _read_samples(knowledge_root: Path, approved_by: str) -> tuple[list[PilotSample], list[str]]:
    source_path = knowledge_root / "training_samples.json"
    gold_root = (knowledge_root / "gold_frames" / "BCC - Bogen").resolve()
    candidates: list[PilotSample] = []
    for item in _load_json_array(source_path):
        if not _is_personal_complete_bcc(item, approved_by):
            continue

        sample_id = str(item.get("SampleId") or "").strip()
        case_id = str(item.get("CaseId") or "").strip()
        code = str(item.get("Code") or "").strip().upper()
        frame_text = str(item.get("FramePath") or "").strip()
        if not sample_id or not case_id or not frame_text:
            raise ValueError("Ein vollstaendiges BCC-Goldsample hat keine ID, Foto-ID oder Bilddatei.")

        frame_path = Path(frame_text)
        if not frame_path.is_file():
            raise ValueError(f"Goldbild fehlt: {frame_path}")
        resolved = frame_path.resolve()
        if not _is_within(resolved, gold_root):
            raise ValueError(f"BCC-Pilotbild liegt nicht im BCC-Goldordner: {frame_path}")

        candidates.append(
            PilotSample(
                sample_id=sample_id,
                case_id=case_id,
                code=code,
                frame_path=resolved,
                image_sha256=_sha256_file(resolved),
                confirmed_at_utc=str(item.get("ConfirmedAtUtc") or ""),
            )
        )

    by_hash: dict[str, PilotSample] = {}
    duplicates: list[str] = []
    for candidate in sorted(
        candidates,
        key=lambda sample: (sample.confirmed_at_utc, sample.sample_id.casefold()),
    ):
        previous = by_hash.get(candidate.image_sha256)
        if previous is not None:
            duplicates.append(previous.sample_id)
        by_hash[candidate.image_sha256] = candidate

    selected = sorted(by_hash.values(), key=lambda sample: sample.sample_id.casefold())
    if len(selected) < MINIMUM_IMAGES:
        raise ValueError(
            f"Der BCC-Pilot braucht mindestens {MINIMUM_IMAGES} verschiedene Goldbilder; "
            f"gefunden wurden {len(selected)}."
        )
    return selected, sorted(set(duplicates), key=str.casefold)


def _split_cases(samples: Iterable[PilotSample]) -> tuple[tuple[str, ...], tuple[str, ...]]:
    grouped: dict[str, int] = {}
    for sample in samples:
        grouped[sample.case_id] = grouped.get(sample.case_id, 0) + 1

    ranked = sorted(
        grouped,
        key=lambda case_id: hashlib.sha256(
            f"bcc-pilot-v1|{case_id}".encode("utf-8")
        ).hexdigest(),
    )
    target_validation_images = max(
        1,
        round(sum(grouped.values()) * VALIDATION_RATIO),
    )
    validation: list[str] = []
    validation_images = 0
    for case_id in ranked:
        if validation_images >= target_validation_images:
            break
        validation.append(case_id)
        validation_images += grouped[case_id]

    validation_set = set(validation)
    train = [case_id for case_id in grouped if case_id not in validation_set]
    if len(train) < 1 or validation_images < 1:
        raise ValueError("Der BCC-Pilot konnte nicht sicher in Train und Pruefung getrennt werden.")
    return (
        tuple(sorted(train, key=str.casefold)),
        tuple(sorted(validation, key=str.casefold)),
    )


def _discover_protected_sets(knowledge_root: Path) -> tuple[dict[str, str], ...]:
    subsets_root = knowledge_root / "eval_set" / "subsets"
    result: list[dict[str, str]] = []
    for manifest_path in sorted(subsets_root.glob("*/_manifest.json")):
        set_root = manifest_path.parent
        document = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        if not isinstance(document, dict) or document.get("frozen") is not True:
            raise ValueError(f"Eval-Schutz ist nicht eingefroren: {manifest_path}")
        result.append(
            {
                "set_id": f"dev-val-{set_root.name.casefold().replace('_', '-')}-v1",
                "role": "development_validation",
                "root_path": str(set_root.relative_to(knowledge_root)),
                "manifest_sha256": _sha256_file(manifest_path),
            }
        )
    if not result:
        raise ValueError("Kein direktes, eingefrorenes Dev-Val-Set wurde gefunden.")
    return tuple(result)


def _read_negatives(knowledge_root: Path, negatives_dir: Path) -> tuple[dict[str, str], ...]:
    """Liest menschlich kuratierte Negativ-/Hintergrundbilder (schadensfrei).

    Nur jpg/jpeg/png direkt in der Wurzel (nicht rekursiv), lesbar und mit
    Mindestgroesse. Fehlender oder leerer Ordner ist KEIN Fehler — dann bleibt
    das Register ohne 'negative_images' (bisheriges Verhalten).
    """
    if not negatives_dir.is_dir():
        return ()
    candidates = sorted(
        (
            path
            for path in negatives_dir.iterdir()
            if path.is_file() and path.suffix.casefold() in NEGATIVE_IMAGE_SUFFIXES
        ),
        key=lambda path: path.name.casefold(),
    )
    if not candidates:
        return ()

    root = knowledge_root.resolve()
    result: list[dict[str, str]] = []
    for path in candidates:
        resolved = path.resolve()
        if resolved.stat().st_size < MIN_NEGATIVE_BYTES:
            raise ValueError(f"Negativbild ist zu klein oder unlesbar: {resolved}")
        try:
            relative = resolved.relative_to(root)
            stored_path = relative.as_posix()
        except ValueError:
            stored_path = str(resolved)
        result.append({"path": stored_path, "sha256": _sha256_file(resolved)})
    return tuple(result)


def build_preparation(
    knowledge_root: Path,
    approved_by: str,
    negatives_dir: Path | None = None,
) -> PilotPreparation:
    root = knowledge_root.resolve()
    if not approved_by.strip():
        raise ValueError("Die freigebende Person fehlt.")
    samples, duplicate_ids = _read_samples(root, approved_by)
    train_cases, validation_cases = _split_cases(samples)
    protected_sets = _discover_protected_sets(root)
    pilot_root = root / "training" / "pilots" / PILOT_MAIN_CODE
    negatives_path = (
        negatives_dir
        if negatives_dir is not None
        else root / "training" / "negatives" / "bcc_pilot"
    )
    negative_images = _read_negatives(root, negatives_path)
    return PilotPreparation(
        registry_path=root / "training" / "export_registry_v1.json",
        audit_path=pilot_root / "pilot_setup_v1.json",
        selected_samples=tuple(samples),
        duplicate_sample_ids=tuple(duplicate_ids),
        train_cases=train_cases,
        validation_cases=validation_cases,
        protected_sets=protected_sets,
        negative_images=negative_images,
    )


def _atomic_write_json(path: Path, document: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    data = (json.dumps(document, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    try:
        with temporary.open("xb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def execute_preparation(
    preparation: PilotPreparation,
    approved_by: str,
    approved_utc: datetime,
) -> None:
    approved_utc = approved_utc.astimezone(timezone.utc)
    if preparation.registry_path.exists():
        raise FileExistsError(
            f"Das Exportregister existiert bereits und wurde nicht ueberschrieben: "
            f"{preparation.registry_path}"
        )

    validation = set(preparation.validation_cases)
    holding_roles = {
        case_id: (
            "development_validation" if case_id in validation else "train"
        )
        for case_id in sorted(
            set(preparation.train_cases) | validation,
            key=str.casefold,
        )
    }
    registry = {
        "schema_version": "1.0",
        "approval_status": "approved",
        "approved_by": approved_by.strip(),
        "approved_utc": approved_utc.isoformat().replace("+00:00", "Z"),
        "approved_sample_ids": [
            sample.sample_id for sample in preparation.selected_samples
        ],
        "holding_roles": holding_roles,
        "protected_sets": list(preparation.protected_sets),
    }
    # Additiv: das Feld wird nur geschrieben, wenn kuratierte Negative vorliegen —
    # Alt-Registrys und der strikte C#-Leser bleiben kompatibel.
    if preparation.negative_images:
        registry["negative_images"] = list(preparation.negative_images)
    audit = {
        "schema_version": "1.0",
        "pilot": PILOT_NAME,
        "created_utc": approved_utc.isoformat().replace("+00:00", "Z"),
        "approved_by": approved_by.strip(),
        "source": str(preparation.registry_path.parents[1] / "training_samples.json"),
        "selected_images": len(preparation.selected_samples),
        "train_images": preparation.train_images,
        "validation_images": preparation.validation_images,
        "negative_images": len(preparation.negative_images),
        "duplicate_sample_ids_excluded": list(preparation.duplicate_sample_ids),
        "samples": [
            {
                "sample_id": sample.sample_id,
                "case_id": sample.case_id,
                "code": sample.code,
                "image_sha256": sample.image_sha256,
                "target": (
                    "validation" if sample.case_id in validation else "train"
                ),
            }
            for sample in preparation.selected_samples
        ],
    }
    _atomic_write_json(preparation.registry_path, registry)
    try:
        _atomic_write_json(preparation.audit_path, audit)
    except Exception:
        preparation.registry_path.unlink(missing_ok=True)
        raise


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="BCC-Bogen-Pilot aus persoenlichem Gold vorbereiten."
    )
    parser.add_argument(
        "--knowledge-root",
        type=Path,
        default=Path(os.getenv("SEWERSTUDIO_KNOWLEDGE_ROOT", r"C:\KI_BRAIN")),
    )
    parser.add_argument("--approved-by", default="Besitzer")
    parser.add_argument(
        "--negatives-dir",
        type=Path,
        default=None,
        help=(
            "Ordner mit menschlich kuratierten Negativ-/Hintergrundbildern "
            "(Default: <KnowledgeRoot>/training/negatives/bcc_pilot). "
            "Fehlt der Ordner oder ist er leer, bleibt das Register ohne negative_images."
        ),
    )
    parser.add_argument(
        "--execute",
        action="store_true",
        help="Exportregister und Auditdatei wirklich schreiben.",
    )
    return parser.parse_args()


def main() -> int:
    args = _parse_args()
    preparation = build_preparation(args.knowledge_root, args.approved_by, args.negatives_dir)
    print(f"BCC-Goldbilder: {len(preparation.selected_samples)}")
    print(f"Train: {preparation.train_images}")
    print(f"Pruefung: {preparation.validation_images}")
    print(f"Negativbilder: {len(preparation.negative_images)}")
    print(f"Doppelte Bildinhalte ausgelassen: {len(preparation.duplicate_sample_ids)}")
    print(f"Geschuetzte Dev-Val-Sets: {len(preparation.protected_sets)}")
    print(f"Exportregister: {preparation.registry_path}")
    if not args.execute:
        print("Nur Pruefung. Es wurde nichts geschrieben.")
        return 0

    execute_preparation(
        preparation,
        args.approved_by,
        datetime.now(timezone.utc),
    )
    print(f"BCC-Pilot vorbereitet: {preparation.audit_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
