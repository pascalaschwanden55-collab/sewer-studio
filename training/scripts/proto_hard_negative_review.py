#!/usr/bin/env python3
"""Protokollbasierte Negativ-Sammlung (Geschwister von bcc_hard_negative_review).

Sammelt Kandidaten fuer klassenfreie Trainingsnegative aus Protokollquellen
(XTF beider Modellvarianten + WinCan-.db3): Befunde, deren Code NICHT zu den
14 Detect-Klassen gehoert. Kein Modell ist an der Auswahl beteiligt
(``selection_rule.model_involved = false``) — die Auswahl folgt einer Quote
nach Codegruppen, damit insbesondere Rohranfaenge/-enden (BCD/BCE) als
Verwechslungsquelle von BCA_anschluss direkt antrainiert werden.

Vertrag gespiegelt von bcc_hard_negative_review: Queue-Ordner
``proto_hn_<id>`` mit ``images/``, ``_candidates.json`` (blind: kein Code,
keine Herkunft) und ``_manifest.json`` (semantisch gebundene Hashliste,
Klassenkarte v3, Schutz-Schnappschuss). Die Blindpruefung laeuft ueber den
unveraenderten ``BccHardNegativeReviewStore``.

``--publish-set`` veroeffentlicht als Geschwister von ``publish_negative_set``
nur ``all_classes_clear`` als Trainingsnegative (Split ca. 80/20, eine Haltung
nie in beiden). ``mapped_object_visible`` und ``exclude_uncertain`` werden nie
veroeffentlicht.

Standard ist der schreibfreie Plan; erst die ausdruecklichen Publish-Flags
schreiben. Kundenoriginale auf ``D:\\`` werden nur gelesen.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import tempfile
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

SCRIPT_ROOT = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_ROOT.parents[1]
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

import bcc_release_holdout as holdout_tools

from collect_class_candidates import (
    ALL_CLASSES,
    build_image_index,
    collect_sources,
    decodable_size,
    holding_label,
    is_line_inspection,
    resolve_image,
)
from repair_gold_holding_ids import _is_link_or_reparse, _sha256_file
from repair_pdf_gold_holding_ids import comparison_key, load_protection_keys
from gold_stock_audit import (
    NEGATIVE_SPLIT_SALT,
    _gold_split_roles_by_physical,
    _negative_split_map,
    _proto_physical_holding_key,
)

SCHEMA_VERSION = "1.0"
QUEUE_PURPOSE = "proto_hard_negative_review_queue"
SET_PURPOSE = "proto_reviewed_negative_set"
REVIEW_PURPOSE = "bcc_hard_negative_review"
PILOT_NAME = "protokoll_negative"
QUEUE_ROLE = "training_candidate_review"
SET_ROLE = "training_negative_set"
SELECTION_SALT = "proto-hard-negative-review-v1"
REVIEW_TARGET = "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse"
DETECT_CLASSES = frozenset(ALL_CLASSES)

DEFAULT_CLASS_MAP = REPOSITORY_ROOT / "training" / "class_maps" / "detect_class_map_v3.json"
DEFAULT_VSA_MANIFEST = (
    REPOSITORY_ROOT / "src" / "AuswertungPro.Next.UI" / "Data" / "vsa_kek_2020_catalog_manifest.json"
)

# Quote je Codegruppe (Ziel 500 Kandidaten).
QUOTA_GROUPS: tuple[tuple[str, frozenset[str], int], ...] = (
    ("rohranfang_ende", frozenset({"BCD", "BCE"}), 250),
    ("wasser_betrieb", frozenset({"BDA", "BDD", "BDB", "BDC"}), 150),
    ("bauteil_sonstige", frozenset(), 100),  # leere Menge = alle uebrigen Nicht-Detect-Codes
)


def _canonical_json_bytes(document) -> bytes:
    return json.dumps(document, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def _pretty_json_bytes(document) -> bytes:
    return (json.dumps(document, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def load_class_map(path: Path, vsa_manifest_path: Path) -> dict:
    document = json.loads(path.read_bytes().decode("utf-8-sig"))
    classes = document.get("classes")
    if not isinstance(classes, dict) or len(classes) != 15:
        raise ValueError("Die 15er-Klassenkarte v3 ist ungueltig.")
    ordered = [name for name, _id in sorted(classes.items(), key=lambda item: item[1])]
    vsa_hash = _sha256_file(vsa_manifest_path)
    if str(document.get("vsa_manifest_hash") or "") != vsa_hash:
        raise ValueError("Die Klassenkarte passt nicht zum VSA-Manifest.")
    return {
        "version": int(document.get("version")),
        "sha256": _sha256_file(path),
        "vsa_manifest_hash": vsa_hash,
        "ordered_names": ordered,
    }


def _quota_group(code3: str) -> str:
    for name, codes, _quota in QUOTA_GROUPS:
        if codes and code3 in codes:
            return name
    return QUOTA_GROUPS[-1][0]


IMAGE_HASH_SUFFIXES = {".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"}


def load_protected_image_hashes(knowledge_root: Path) -> tuple[set[str], dict[str, int]]:
    """Byte-Hashes aller Bilder in Eval-, Negativ- und Goldbestaenden.

    Schliesst Luecken des Haltungsschluessels: Ein Bild, dessen Bytes in einem
    geschuetzten Bestand liegen, wird gesperrt — auch wenn seine Haltung
    unbekannt oder falsch geschrieben ist. Die aus Gold abgeleiteten
    Diagnose-Warteschlangen (detect_gold_failure_review) gehoeren bewusst
    NICHT dazu; eval_review wird nur nach Holdout-Artefakten durchsucht.
    """
    roots = (
        ("eval_set", knowledge_root / "eval_set"),
        ("eval_review", knowledge_root / "eval_review"),
        ("training_negatives", knowledge_root / "training" / "negatives"),
        ("gold_frames", knowledge_root / "gold_frames"),
    )
    hashes: set[str] = set()
    counts: dict[str, int] = {}
    for label, root in roots:
        if not root.is_dir() or _is_link_or_reparse(root):
            counts[label] = 0
            continue
        count = 0
        for dirpath, dirnames, filenames in os.walk(root):
            dirnames[:] = [d for d in dirnames if not _is_link_or_reparse(Path(dirpath) / d)]
            for name in filenames:
                if Path(name).suffix.casefold() not in IMAGE_HASH_SUFFIXES:
                    continue
                path = Path(dirpath) / name
                if _is_link_or_reparse(path):
                    continue
                hashes.add(_sha256_file(path))
                count += 1
        counts[label] = count
    return hashes, counts


def snapshot_protected_sets(knowledge_root: Path) -> list[dict]:
    """Explizite, nicht-leere Liste der geschuetzten Bestaende fuer das Manifest."""
    sets: list[dict] = []
    eval_set = knowledge_root / "eval_set"
    if eval_set.is_dir():
        sets.append({"art": "eval_set", "pfad": "eval_set"})
        for subset in sorted((eval_set / "subsets").glob("*")):
            if subset.is_dir():
                sets.append({"art": "eval_set_subset", "pfad": f"eval_set/subsets/{subset.name}"})
    for extra, art in (
        ("eval_review", "eval_review_holdout"),
        ("training/negatives", "training_negatives"),
        ("gold_frames", "gold_frames"),
    ):
        if (knowledge_root / extra).is_dir():
            sets.append({"art": art, "pfad": extra})
    reports = sorted((knowledge_root / "training" / "reports").glob("gold_stock_audit_*.json")) \
        if (knowledge_root / "training" / "reports").is_dir() else []
    if reports:
        sets.append({"art": "gold_audit_testrollen", "pfad": f"training/reports/{reports[-1].name}"})
    return sets


def select_candidates(
    befunde: list[dict],
    image_index: dict[str, list[Path]],
    gold_keys: set,
    protection: dict,
    protected_hashes: set | None = None,
    quotas: dict[str, int] | None = None,
) -> tuple[list[dict], dict]:
    """Waehlt Nicht-Detect-Kandidaten nach Quote, ein Bild je physischer Haltung
    ueber die ganze Warteschlange. Deterministisch (stabiler Hash-Rang).
    Zusaetzlich Byte-Schutz: Bilder, deren Bytes in Eval-/Negativ-/Gold-
    Bestaenden liegen, werden unabhaengig von der Haltung gesperrt."""
    protected_hashes = protected_hashes or set()
    quota_by_group = quotas or {name: quota for name, _codes, quota in QUOTA_GROUPS}
    stats: dict[str, int] = {}
    pool: list[dict] = []
    seen_bytes: set[str] = set()

    for eintrag in befunde:
        code3 = (eintrag.get("code") or "")[:3].upper()
        if not code3 or code3 in DETECT_CLASSES:
            stats["detect_code_uebersprungen"] = stats.get("detect_code_uebersprungen", 0) + 1
            continue
        bild = resolve_image(eintrag["datei_name"], image_index)
        if bild is None:
            stats["bild_fehlt"] = stats.get("bild_fehlt", 0) + 1
            continue
        if decodable_size(bild) is None:
            stats["nicht_dekodierbar"] = stats.get("nicht_dekodierbar", 0) + 1
            continue
        byte_hash = _sha256_file(bild)
        if byte_hash in protected_hashes:
            stats["byte_geschuetzt"] = stats.get("byte_geschuetzt", 0) + 1
            continue
        if byte_hash in seen_bytes:
            stats["byte_dublette"] = stats.get("byte_dublette", 0) + 1
            continue
        seen_bytes.add(byte_hash)
        haltung = holding_label(eintrag)
        schluessel = comparison_key(haltung)
        if schluessel in gold_keys:
            stats["in_gold"] = stats.get("in_gold", 0) + 1
            continue
        if schluessel in protection:
            stats["geschuetzt"] = stats.get("geschuetzt", 0) + 1
            continue
        pool.append({
            "eintrag": eintrag,
            "bild": bild,
            "bild_sha256": byte_hash,
            "haltung": haltung,
            "schluessel": schluessel or haltung.casefold(),
            "gruppe": _quota_group(code3),
            "code": eintrag["code"],
        })

    # Stabiler, inhaltgebundener Rang ueber den gesamten Pool.
    pool.sort(key=lambda item: hashlib.sha256(
        f"{SELECTION_SALT}|{item['bild_sha256']}".encode()).hexdigest())

    taken_holdings: set[str] = set()
    group_counts: dict[str, int] = {name: 0 for name in quota_by_group}
    selected: list[dict] = []
    for item in pool:
        if item["schluessel"] in taken_holdings:
            stats["haltung_bereits_belegt"] = stats.get("haltung_bereits_belegt", 0) + 1
            continue
        gruppe = item["gruppe"]
        if group_counts.get(gruppe, 0) >= quota_by_group.get(gruppe, 0):
            stats["quote_voll"] = stats.get("quote_voll", 0) + 1
            continue
        taken_holdings.add(item["schluessel"])
        group_counts[gruppe] = group_counts.get(gruppe, 0) + 1
        selected.append(item)

    stats["ausgewaehlt"] = len(selected)
    stats.update({f"gruppe_{name}": count for name, count in group_counts.items()})
    return selected, stats


# ---------------------------------------------------------------------------
# Queue-Plan und Veroeffentlichung
# ---------------------------------------------------------------------------

@dataclass(frozen=True)
class QueueItem:
    item_id: str
    source_path: Path
    image_sha256: str
    holding_key: str
    schluessel: str
    code: str
    gruppe: str
    quelle: str
    quell_datei: str
    leitungsinspektion: bool
    size_bytes: int
    image_format: str

    @property
    def target_file_name(self) -> str:
        return f"img_{self.image_sha256}{self.source_path.suffix.casefold()}"


def _semantic_item(item: QueueItem) -> dict:
    return {
        "item_id": item.item_id,
        "image_sha256": item.image_sha256,
        "holding_key": item.holding_key,
        "code": item.code,
        "gruppe": item.gruppe,
        "quelle": item.quelle,
        "quell_datei": item.quell_datei,
        "leitungsinspektion": item.leitungsinspektion,
        "size_bytes": item.size_bytes,
        "image_format": item.image_format,
        "target_file_name": item.target_file_name,
    }


def build_queue_plan(
    knowledge_root: Path,
    selected: list[dict],
    class_map: dict,
    created_utc: datetime | None = None,
) -> dict:
    items = []
    for item in selected:
        bild: Path = item["bild"]
        eintrag = item["eintrag"]
        items.append(QueueItem(
            item_id=f"proto-hn-{item['bild_sha256'][:20]}",
            source_path=bild,
            image_sha256=item["bild_sha256"],
            holding_key=item["haltung"],
            schluessel=item["schluessel"],
            code=item["code"],
            gruppe=item["gruppe"],
            quelle=eintrag["quelle"],
            quell_datei=eintrag["quell_datei"],
            leitungsinspektion=is_line_inspection(item["haltung"], eintrag),
            size_bytes=bild.stat().st_size,
            image_format=bild.suffix.casefold().lstrip("."),
        ))
    if len({item.schluessel for item in items}) != len(items):
        raise ValueError("Die Pruefliste enthaelt mehr als ein Bild je physischer Haltung.")
    if not items:
        raise ValueError("Keine Kandidaten fuer die Negativ-Pruefliste gefunden.")

    protection = load_protection_keys(knowledge_root)
    quellen: dict[str, int] = {}
    for sources in protection.values():
        for source in sources:
            art = source.split(":", 1)[0]
            quellen[art] = quellen.get(art, 0) + 1
    protected_sets = snapshot_protected_sets(knowledge_root)
    if not protected_sets:
        raise ValueError(
            "protected_sets waere leer — fail-closed: ein ungeschuetzter Lauf "
            "darf nicht wie ein geschuetzter aussehen."
        )
    protection_snapshot = {
        "schluessel_gesamt": len(protection),
        "quellen_anteile": quellen,
        "ohne_diagnose_warteschlangen": True,
        "byte_schutz": True,
    }
    sources = sorted({item.quelle for item in items})
    semantic = {
        "schema_version": SCHEMA_VERSION,
        "purpose": QUEUE_PURPOSE,
        "pilot": PILOT_NAME,
        "role": QUEUE_ROLE,
        "class_map_version": class_map["version"],
        "class_map_sha256": class_map["sha256"],
        "vsa_manifest_hash": class_map["vsa_manifest_hash"],
        "class_names": list(class_map["ordered_names"]),
        "protected_sets": protected_sets,
        "protection_snapshot": protection_snapshot,
        "sources": sources,
        "selection_rule": {
            "one_image_per_physical_holding": True,
            "model_involved": False,
            "selection_basis": "protokoll_operateurcodes_nicht_detect",
            "quota": {name: quota for name, _codes, quota in QUOTA_GROUPS},
            "reviewer_sees_model_signals": False,
            "review_target": REVIEW_TARGET,
        },
        "items": [_semantic_item(item) for item in items],
    }
    queue_id = hashlib.sha256(_canonical_json_bytes(semantic)).hexdigest()
    return {
        "knowledge_root": knowledge_root,
        "class_map": class_map,
        "created_utc": created_utc or datetime.now(timezone.utc),
        "items": items,
        "semantic": semantic,
        "queue_id": queue_id,
        "target_root": (knowledge_root / "training" / "hard_negative_review"
                        / "queues" / f"proto_hn_{queue_id[:12]}"),
        "protection_snapshot": protection_snapshot,
        "sources": sources,
    }


def publish_queue(plan: dict) -> Path:
    target_root: Path = plan["target_root"]
    expected = (plan["knowledge_root"] / "training" / "hard_negative_review"
                / "queues" / f"proto_hn_{plan['queue_id'][:12]}")
    if os.path.normcase(str(target_root)) != os.path.normcase(str(expected)):
        raise ValueError("Das Ziel passt nicht zur geprueften Prueflisten-ID.")
    if target_root.exists() or target_root.is_symlink():
        raise FileExistsError(f"Vorhandene Pruefliste wird nie ueberschrieben: {target_root}")

    queues_root = target_root.parent
    queues_root.mkdir(parents=True, exist_ok=True)
    if _is_link_or_reparse(queues_root):
        raise ValueError(f"Unsicherer Warteschlangenordner: {queues_root}")
    staging = queues_root / f".proto-hn-staging-{uuid.uuid4().hex}"
    staging.mkdir()
    try:
        images_root = staging / "images"
        images_root.mkdir()
        for item in plan["items"]:
            source = item.source_path
            holdout_tools._validate_image(source)
            if _sha256_file(source) != item.image_sha256:
                raise ValueError("Ein Quellbild wurde nach der Planung veraendert.")
            holdout_tools._copy_verified(source, images_root / item.target_file_name, item.image_sha256)

        # Blind: kein Code, keine Haltung, keine Quelle in der Pruefansicht.
        candidates = [
            {
                "id": item.item_id,
                "frame_path": item.target_file_name,
                "category": "all_class_background_review",
                "status": "pending_review",
                "source_sha256": item.image_sha256,
            }
            for item in plan["items"]
        ]
        (staging / "_candidates.json").write_bytes(_pretty_json_bytes(candidates))
        hashes = holdout_tools._manifest_hash_entries(staging)
        semantic = plan["semantic"]
        manifest = {
            "schema_version": SCHEMA_VERSION,
            "purpose": QUEUE_PURPOSE,
            "queue_id": plan["queue_id"],
            "pilot": PILOT_NAME,
            "role": QUEUE_ROLE,
            "created_utc": plan["created_utc"].isoformat().replace("+00:00", "Z"),
            "frozen": True,
            "dataset_status": "review_incomplete",
            "warning": "NUR all_classes_clear DARF SPAETER ALS TRAININGSNEGATIV VEROEFFENTLICHT WERDEN",
            "review_target": REVIEW_TARGET,
            "class_map_version": plan["class_map"]["version"],
            "class_map_sha256": plan["class_map"]["sha256"],
            "vsa_manifest_hash": plan["class_map"]["vsa_manifest_hash"],
            "class_names": list(plan["class_map"]["ordered_names"]),
            "protected_sets": plan["semantic"]["protected_sets"],
            "protection_snapshot": plan["protection_snapshot"],
            "selection_rule": semantic["selection_rule"],
            "sources": plan["sources"],
            "candidates_count": len(candidates),
            "images_count": len(candidates),
            "holdings_count": len(plan["items"]),
            "hash_algorithm": "sha256",
            "hashes_count": len(hashes),
            "hashes": hashes,
            "semantic": semantic,
            "selection_receipt": {"items": semantic["items"]},
        }
        for field in ("schema_version", "purpose", "pilot", "role", "class_map_version",
                      "class_map_sha256", "vsa_manifest_hash", "class_names",
                      "protected_sets", "protection_snapshot", "sources"):
            if manifest[field] != semantic[field]:
                raise ValueError(f"Semantik und Manifest widersprechen sich bei {field}.")
        (staging / "_manifest.json").write_bytes(_pretty_json_bytes(manifest))
        if target_root.exists() or target_root.is_symlink():
            raise FileExistsError(f"Prueflistenziel existiert bereits: {target_root}")
        os.rename(staging, target_root)
    finally:
        if staging.exists():
            shutil.rmtree(staging)
    return target_root


# ---------------------------------------------------------------------------
# Negativsatz-Veroeffentlichung (nur all_classes_clear)
# ---------------------------------------------------------------------------

def build_set_plan(
    knowledge_root: Path,
    queue_root: Path,
    review_path: Path,
    class_map_path: Path,
) -> dict:
    queue_manifest = json.loads((queue_root / "_manifest.json").read_bytes().decode("utf-8-sig"))
    candidates = json.loads((queue_root / "_candidates.json").read_bytes().decode("utf-8-sig"))
    review_document = json.loads(review_path.read_bytes().decode("utf-8-sig"))
    # Die Review muss an genau diese Warteschlange gebunden sein.
    if str(review_document.get("queue_id") or "") != str(queue_manifest.get("queue_id") or ""):
        raise ValueError("Die Review gehoert zu einer anderen Warteschlange.")
    if str(review_document.get("queue_manifest_sha256") or "") != _sha256_file(queue_root / "_manifest.json"):
        raise ValueError("Die Review passt nicht zum Warteschlangen-Manifest.")
    if str(review_document.get("candidates_sha256") or "") != _sha256_file(queue_root / "_candidates.json"):
        raise ValueError("Die Review passt nicht zur Kandidatenliste.")
    if str(review_document.get("class_map_sha256") or "") != str(queue_manifest.get("class_map_sha256") or ""):
        raise ValueError("Die Review passt nicht zur gebundenen Klassenkarte.")
    decisions_raw = review_document.get("decisions")
    if not isinstance(decisions_raw, dict):
        raise ValueError("Die Review-Datei enthaelt keine Entscheidungen.")
    decisions: dict[str, str] = {}
    for item_id, entry in decisions_raw.items():
        decision = (entry or {}).get("decision") if isinstance(entry, dict) else entry
        decisions[str(item_id)] = str(decision or "")
    pending = [c["id"] for c in candidates if c["id"] not in decisions]
    if pending:
        raise ValueError(f"Review unvollstaendig: {len(pending)} Bilder ohne Entscheidung.")
    ungueltig = sorted({d for d in decisions.values()
                        if d not in ("all_classes_clear", "mapped_object_visible", "exclude_uncertain")})
    if ungueltig:
        raise ValueError(f"Unbekannte Review-Entscheidungen: {ungueltig}")
    decision_counts = {
        name: sum(1 for d in decisions.values() if d == name)
        for name in ("all_classes_clear", "mapped_object_visible", "exclude_uncertain")
    }

    receipt_items = {item["item_id"]: item for item in queue_manifest["semantic"]["items"]}
    clear_items = []
    for candidate in candidates:
        item_id = candidate["id"]
        decision = decisions[item_id]
        if decision != "all_classes_clear":
            # mapped_object_visible und exclude_uncertain werden NIE veroeffentlicht.
            continue
        receipt = receipt_items[item_id]
        clear_items.append({
            "item_id": item_id,
            "image_sha256": receipt["image_sha256"],
            "holding_key": receipt["holding_key"],
            "target_file_name": receipt["target_file_name"],
            "size_bytes": receipt["size_bytes"],
            "image_format": receipt["image_format"],
            "quelle": receipt["quelle"],
        })
    if not clear_items:
        raise ValueError("Kein all_classes_clear im Review — kein Negativsatz zu bauen.")

    for item in clear_items:
        item["physical"] = _proto_physical_holding_key(item["holding_key"])
    split_map, validation_count = _negative_split_map([i["physical"] for i in clear_items])
    for item in clear_items:
        item["split"] = split_map[item["physical"]]

    # Gold-Ausrichtung: Negativ-Splits folgen bei gemeinsamen Haltungen dem
    # Gold-Split (gold train -> train, gold val/test -> validation), damit die
    # interne Validierung kein bereits bekanntes Rohr als Fehlalarm-Massstab nutzt.
    gold_roles = _gold_split_roles_by_physical(knowledge_root)
    gold_alignments: list[dict] = []
    for item in clear_items:
        gold_role = gold_roles.get(item["physical"])
        if gold_role is None:
            continue
        expected = "train" if gold_role == "train" else "validation"
        if item["split"] != expected:
            gold_alignments.append({
                "physical_holding_key": item["physical"],
                "gold_role": gold_role,
                "forced_split": expected,
            })
            item["split"] = expected
    if gold_alignments:
        validation_count = sum(1 for i in clear_items if i["split"] == "validation")

    queue_manifest_sha = _sha256_file(queue_root / "_manifest.json")
    candidates_sha = _sha256_file(queue_root / "_candidates.json")
    review_sha = _sha256_file(review_path)
    class_map_sha = str(queue_manifest["class_map_sha256"])
    if _sha256_file(class_map_path) != class_map_sha:
        raise ValueError("Die uebergebene Klassenkarte passt nicht zur Warteschlange.")
    semantic_images = [{
        "id": f"proto-neg-{item['image_sha256']}",
        "file_name": item["target_file_name"],
        "image_sha256": item["image_sha256"],
        "size_bytes": item["size_bytes"],
        "image_format": item["image_format"],
        "holding_key": item["holding_key"],
        "physical_holding_key": item["physical"],
        "split": item["split"],
        "review_item_id": item["item_id"],
        "review_decision": "all_classes_clear",
        "quelle": item["quelle"],
    } for item in clear_items]
    train_count = len(clear_items) - validation_count
    split_rule: dict = {
        "name": "stable_rank_v1_gold_aligned" if gold_alignments else "stable_rank_v1",
        "salt": NEGATIVE_SPLIT_SALT,
        "one_image_per_physical_holding": True,
        "validation_count": validation_count,
        "train_count": train_count,
    }
    if gold_alignments:
        split_rule["gold_alignments"] = sorted(
            gold_alignments, key=lambda a: a["physical_holding_key"]
        )
    semantic = {
        "schema_version": SCHEMA_VERSION,
        "purpose": SET_PURPOSE,
        "pilot": PILOT_NAME,
        "role": SET_ROLE,
        "queue": {
            "queue_id": queue_manifest["queue_id"],
            "queue_manifest_sha256": queue_manifest_sha,
            "queue_manifest_receipt_path": "receipts/queue_manifest.json",
            "candidates_sha256": candidates_sha,
            "candidates_receipt_path": "receipts/queue_candidates.json",
        },
        "review": {
            "purpose": REVIEW_PURPOSE,
            "review_sha256": review_sha,
            "receipt_path": "receipts/review.json",
            "reviewed_images": len(decisions),
            "decision_counts": decision_counts,
        },
        "class_map_version": queue_manifest["class_map_version"],
        "class_map_sha256": class_map_sha,
        "class_map_receipt_path": "receipts/class_map.json",
        "vsa_manifest_hash": queue_manifest["vsa_manifest_hash"],
        "class_names": list(queue_manifest["class_names"]),
        "protected_sets": list(queue_manifest["protected_sets"]),
        "protection_snapshot": dict(queue_manifest["protection_snapshot"]),
        "split_rule": split_rule,
        "images": semantic_images,
    }
    set_id = hashlib.sha256(_canonical_json_bytes(semantic)).hexdigest()
    return {
        "knowledge_root": knowledge_root,
        "queue_root": queue_root,
        "review_path": review_path,
        "class_map_path": class_map_path,
        "created_utc": datetime.now(timezone.utc),
        "items": clear_items,
        "semantic": semantic,
        "set_id": set_id,
        "target_root": (knowledge_root / "training" / "negatives" / "sets" / f"proto_hn_{set_id[:12]}"),
        "queue_manifest_sha256": queue_manifest_sha,
        "candidates_sha256": candidates_sha,
        "review_sha256": review_sha,
        "class_map_sha256": class_map_sha,
    }


def publish_set(plan: dict) -> Path:
    target_root: Path = plan["target_root"]
    if target_root.exists() or target_root.is_symlink():
        raise FileExistsError(f"Vorhandener Negativsatz wird nie ueberschrieben: {target_root}")
    sets_root = target_root.parent
    sets_root.mkdir(parents=True, exist_ok=True)
    if _is_link_or_reparse(sets_root):
        raise ValueError(f"Unsicherer Negativsatz-Ordner: {sets_root}")
    staging = sets_root / f".proto-hn-set-staging-{uuid.uuid4().hex}"
    staging.mkdir()
    try:
        images_root = staging / "images"
        images_root.mkdir()
        for item in plan["items"]:
            source = plan["queue_root"] / "images" / item["target_file_name"]
            holdout_tools._validate_image(source)
            if _sha256_file(source) != item["image_sha256"]:
                raise ValueError("Ein Review-Bild wurde vor der Veroeffentlichung veraendert.")
            holdout_tools._copy_verified(source, images_root / item["target_file_name"], item["image_sha256"])

        receipts_root = staging / "receipts"
        receipts_root.mkdir()
        for source_path, name, expected in (
            (plan["review_path"], "review.json", plan["review_sha256"]),
            (plan["queue_root"] / "_manifest.json", "queue_manifest.json", plan["queue_manifest_sha256"]),
            (plan["queue_root"] / "_candidates.json", "queue_candidates.json", plan["candidates_sha256"]),
            (plan["class_map_path"], "class_map.json", plan["class_map_sha256"]),
        ):
            if _sha256_file(source_path) != expected:
                raise ValueError(f"Beleg wurde veraendert: {source_path}")
            holdout_tools._copy_verified(source_path, receipts_root / name, expected)

        hashes: dict[str, dict] = {}
        for path in sorted(list(images_root.iterdir()) + list(receipts_root.iterdir())):
            relative = path.relative_to(staging).as_posix()
            hashes[relative] = {"sha256": _sha256_file(path), "size_bytes": path.stat().st_size}
        manifest = {
            "schema_version": SCHEMA_VERSION,
            "purpose": SET_PURPOSE,
            "set_id": plan["set_id"],
            "pilot": PILOT_NAME,
            "role": SET_ROLE,
            "created_utc": plan["created_utc"].isoformat().replace("+00:00", "Z"),
            "frozen": True,
            "dataset_status": "ready_for_training",
            "hash_algorithm": "sha256",
            "images_count": len(plan["items"]),
            "holdings_count": len(plan["items"]),
            "hashes_count": len(hashes),
            "hashes": hashes,
            "semantic": plan["semantic"],
        }
        (staging / "_manifest.json").write_bytes(_pretty_json_bytes(manifest))
        if target_root.exists() or target_root.is_symlink():
            raise FileExistsError(f"Negativsatzziel existiert bereits: {target_root}")
        os.rename(staging, target_root)
    finally:
        if staging.exists():
            shutil.rmtree(staging)
    return target_root


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--projects-root", type=Path, default=Path(r"D:\Videoprojekte"))
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--class-map", type=Path, default=DEFAULT_CLASS_MAP)
    parser.add_argument("--vsa-manifest", type=Path, default=DEFAULT_VSA_MANIFEST)
    parser.add_argument("--publish-queue", action="store_true",
                        help="Schreibt die Pruefliste nach C:\\KI_BRAIN (Default: nur Plan)")
    parser.add_argument("--publish-set", type=Path, default=None, metavar="QUEUE_ROOT",
                        help="Veroeffentlicht einen Negativsatz aus einer abgeschlossenen Review")
    parser.add_argument("--review", type=Path, default=None, help="Review-Datei fuer --publish-set")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _build_parser().parse_args(argv)
    try:
        if args.publish_set is not None:
            if args.review is None:
                raise ValueError("--publish-set verlangt --review <pfad>")
            plan = build_set_plan(args.knowledge_root, args.publish_set, args.review, args.class_map)
            target = publish_set(plan)
            print(f"Negativsatz veroeffentlicht: {target}")
            print(f"Bilder: {len(plan['items'])} "
                  f"(train {plan['semantic']['split_rule']['train_count']}, "
                  f"val {plan['semantic']['split_rule']['validation_count']})")
            return 0

        print("Scan laeuft (XTF + db3) ...")
        befunde, _tx, _td, n_xtf, n_db3 = collect_sources(args.projects_root)
        print(f"Quellen: {n_xtf} XTF, {n_db3} db3 | Befunde mit Foto: {len(befunde)}")
        print("Baue Bild-Index auf ...")
        image_index = build_image_index(args.projects_root)
        samples = json.loads(
            (args.knowledge_root / "training_samples.json").read_text(encoding="utf-8-sig"))
        gold_keys = {comparison_key(s.get("CaseId")) for s in samples}
        gold_keys.discard(None)
        protection = load_protection_keys(args.knowledge_root)
        pro_quelle: dict[str, int] = {}
        for sources in protection.values():
            for source in sources:
                art = source.split(":", 1)[0]
                pro_quelle[art] = pro_quelle.get(art, 0) + 1
        print(f"Haltungs-Schutzschluessel: {len(protection)} "
              f"(je Quelle: {pro_quelle})")
        print("Berechne Byte-Schutz-Hashes (eval/negativ/gold) ...")
        protected_hashes, hash_counts = load_protected_image_hashes(args.knowledge_root)
        print(f"Byte-Schutz: {len(protected_hashes)} Bilder gesperrt (je Bestand: {hash_counts})")

        selected, stats = select_candidates(
            befunde, image_index, gold_keys, protection, protected_hashes)
        print("=== Auswahl ===")
        for key in sorted(stats):
            print(f"  {key}: {stats[key]}")

        class_map = load_class_map(args.class_map, args.vsa_manifest)
        plan = build_queue_plan(args.knowledge_root, selected, class_map)
        print(f"Queue-ID: {plan['queue_id'][:24]}… | Bilder: {len(plan['items'])} "
              f"| Haltungen: {len({i.schluessel for i in plan['items']})}")
        if not args.publish_queue:
            print("Schreibfrei — keine Pruefliste geschrieben. Zum Veroeffentlichen: --publish-queue")
            return 0
        target = publish_queue(plan)
        print(f"Pruefliste veroeffentlicht: {target}")
        return 0
    except (OSError, ValueError, FileExistsError) as exc:
        print(f"GESPERRT: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
