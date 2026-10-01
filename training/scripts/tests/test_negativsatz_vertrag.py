"""Vertragstests der strengen Negativsatz-Pruefung (AP10).

Haelt fuer beide Varianten (BCC-Satz ``bcc_hn_*`` und Protokoll-Satz
``proto_hn_*``) fest, welche manipulierten Saetze ``_read_reviewed_negative_set``
heute mit welcher Meldung ablehnt. Der Baukasten ``baue_satz`` erzeugt einen
voll gebundenen synthetischen Satz und rechnet nach jedem Eingriff alle
abhaengigen Hashes (Kandidaten, Queue-ID, Queue-Manifest, Review, Satz-ID,
Dateiliste, Ordnername) neu aus. So erreicht jeder Eingriff genau die Regel,
die er pruefen soll, und wird nicht schon von einer frueheren Hashpruefung
abgefangen. Keine echten Daten: alles liegt in temporaeren Ordnern.
"""
from __future__ import annotations

import copy
import hashlib
import json
import re
import shutil
import sys
import tempfile
from pathlib import Path
from typing import Any, Callable
from unittest import mock

import pytest
from PIL import Image

SCRIPTS_DIR = Path(__file__).resolve().parents[1]
REPO_ROOT = SCRIPTS_DIR.parents[1]
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))

import gold_stock_audit as AUDIT  # noqa: E402

KLASSENKARTE = REPO_ROOT / "training" / "class_maps" / "detect_class_map_v3.json"
SPLIT_SALZ = "bcc-hard-negative-split-v1"
BILDGROESSE = (8, 4)
HALTUNGEN = ("100-200", "300-400", "500-600")

Haken = Callable[..., Any] | None


def _kanonisch(wert: Any) -> bytes:
    return json.dumps(
        wert, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")


def _sha(daten: bytes) -> str:
    return hashlib.sha256(daten).hexdigest()


def _schreibe_json(pfad: Path, wert: Any) -> None:
    pfad.write_text(json.dumps(wert, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


class Ersatz:
    """Rueckgabe eines Eingriffs, die den ganzen Wert ersetzt."""

    def __init__(self, wert: Any) -> None:
        self.wert = wert


def _anwenden(haken: Haken, wert: Any, *extra: Any) -> Any:
    """Ruft einen Eingriff auf; nur eine ``Ersatz``-Rueckgabe ersetzt den Wert."""
    if haken is None:
        return wert
    ergebnis = haken(wert, *extra)
    return ergebnis.wert if isinstance(ergebnis, Ersatz) else wert


def _physisch(haltung: str) -> str:
    links, rechts = haltung.split("-", maxsplit=1)
    return "|".join(sorted((links.casefold(), rechts.casefold())))


def _erwartete_splits(physische: list[str]) -> dict[str, str]:
    """Unabhaengige Nachrechnung der stabilen Rangregel (nicht aus dem Modul)."""
    rang = sorted(
        physische,
        key=lambda wert: (_sha(f"{SPLIT_SALZ}|{wert}".encode("utf-8")), wert),
    )
    anzahl = 0 if len(rang) < 2 else max(1, (len(rang) + 2) // 5)
    validierung = set(rang[:anzahl])
    return {wert: "validation" if wert in validierung else "train" for wert in rang}


def _bild(pfad: Path, farbe: int) -> bytes:
    Image.new("RGB", BILDGROESSE, (farbe % 255, (farbe + 40) % 255, (farbe + 80) % 255)).save(pfad)
    daten = pfad.read_bytes() + b"x" * 2048
    pfad.write_bytes(daten)
    return daten


def baue_satz(
    wurzel: Path,
    variante: str,
    haltungen: tuple[str, ...] = HALTUNGEN,
    *,
    entscheidungen: dict[int, str] | None = None,
    ohne_bild: tuple[int, ...] = (),
    klassenkarte_bytes: bytes | None = None,
    bilddaten: Haken = None,
    queue_items: Haken = None,
    queue_semantic: Haken = None,
    queue_manifest: Haken = None,
    kandidaten: Haken = None,
    review: Haken = None,
    bilder: Haken = None,
    semantic: Haken = None,
    dateien: Haken = None,
    manifest: Haken = None,
    manifest_bytes: Haken = None,
    ordnername: Haken = None,
) -> Path:
    """Baut einen voll gebundenen Negativsatz; Haken greifen je Stufe ein.

    ``variante`` ist ``bcc`` oder ``proto``. Nach jedem Haken werden alle
    davon abhaengigen Bindungen neu berechnet.
    """
    proto = variante == "proto"
    entscheidungen = entscheidungen or {}
    saetze = wurzel / "training" / "negatives" / "sets"
    saetze.mkdir(parents=True, exist_ok=True)
    staging = saetze / f".fixture-{variante}-{len(list(saetze.iterdir()))}"
    bilder_ordner = staging / "images"
    belege = staging / "receipts"
    bilder_ordner.mkdir(parents=True)
    belege.mkdir()
    queue_bilder = wurzel / "queue_bilder"
    queue_bilder.mkdir(exist_ok=True)

    karte_bytes = klassenkarte_bytes if klassenkarte_bytes is not None else KLASSENKARTE.read_bytes()
    karte = json.loads(karte_bytes.decode("utf-8-sig"))
    karte_sha = _sha(karte_bytes)
    namen = [name for name, _ in sorted(karte["classes"].items(), key=lambda paar: str(paar[1]).zfill(4))]
    (belege / "class_map.json").write_bytes(karte_bytes)

    modelle = [
        {
            "candidate_id": "fixture-modell",
            "candidate_manifest_sha256": _sha(b"kandidat"),
            "weights_sha256": _sha(b"gewichte"),
            "dataset_plan_id": _sha(b"plan"),
            "dataset_manifest_sha256": _sha(b"datensatz"),
        }
    ]

    eintraege: list[dict[str, Any]] = []
    for index, haltung in enumerate(haltungen):
        datei = queue_bilder / f"{variante}_{index}.png"
        daten = _bild(datei, 20 + 7 * index + (100 if proto else 0))
        if bilddaten is not None:
            daten = bilddaten(index, daten)
        sha = _sha(daten)
        if proto:
            item = {
                "item_id": f"proto-hn-{sha[:20]}",
                "image_sha256": sha,
                "holding_key": haltung,
                "code": "BCDYA",
                "gruppe": "rohranfang_ende",
                "quelle": "xtf",
                "quell_datei": "quelle.xtf",
                "leitungsinspektion": False,
                "size_bytes": len(daten),
                "image_format": "png",
                "target_file_name": f"img_{sha}.png",
            }
        else:
            item = {
                "id": f"bcc-hn-{sha[:16]}",
                "image_sha256": sha,
                "holding_key": haltung,
                "physical_holding_key": _physisch(haltung),
                "source_ref": _sha(f"quelle-{index}".encode("utf-8")),
                "inspection_date": "2026-07-28",
                "size_bytes": len(daten),
                "image_format": "png",
                "predictions": [
                    {
                        "model_id": "fixture-modell",
                        "predicted_bcc": True,
                        "bcc_detection_count": 1,
                        "max_bcc_confidence": 0.75,
                    }
                ],
            }
        eintraege.append({"index": index, "item": item, "daten": daten, "sha": sha, "haltung": haltung})

    items = _anwenden(queue_items, [eintrag["item"] for eintrag in eintraege])
    id_feld = "item_id" if proto else "id"

    kandidaten_liste = [
        {
            "id": eintrag["item"][id_feld],
            "frame_path": f"img_{eintrag['sha']}.png",
            "category": "all_class_background_review",
            "status": "pending_review",
            "source_sha256": eintrag["sha"],
        }
        for eintrag in eintraege
    ]
    kandidaten_liste = _anwenden(kandidaten, kandidaten_liste)
    kandidaten_pfad = belege / "queue_candidates.json"
    _schreibe_json(kandidaten_pfad, kandidaten_liste)
    kandidaten_bytes = kandidaten_pfad.read_bytes()
    kandidaten_sha = _sha(kandidaten_bytes)

    queue_hashes: dict[str, Any] = {
        f"images/img_{eintrag['sha']}.png": {
            "sha256": eintrag["sha"],
            "size_bytes": len(eintrag["daten"]),
        }
        for eintrag in eintraege
    }
    queue_hashes["_candidates.json"] = {"sha256": kandidaten_sha, "size_bytes": len(kandidaten_bytes)}

    if proto:
        auswahlregel = {
            "one_image_per_physical_holding": True,
            "model_involved": False,
            "selection_basis": "protokoll_operateurcodes_nicht_detect",
            "reviewer_sees_model_signals": False,
        }
    else:
        auswahlregel = {
            "one_image_per_physical_holding": True,
            "requires_current_model_bcc_trigger": True,
        }
    geschuetzt = [{"name": "fixture_eval", "manifest_sha256": _sha(b"eval")}]
    q_semantik: dict[str, Any] = {
        "schema_version": "1.0",
        "purpose": "proto_hard_negative_review_queue" if proto else "bcc_hard_negative_review_queue",
        "pilot": "protokoll_negative" if proto else "BCC_bogen",
        "role": "training_candidate_review",
        "class_map_version": karte["version"],
        "class_map_sha256": karte_sha,
        "vsa_manifest_hash": karte["vsa_manifest_hash"],
        "class_names": namen,
        "protected_sets": geschuetzt,
        "protection_snapshot": {"byte_schutz": True},
        "selection_rule": auswahlregel,
        "sources": ["xtf"] if proto else [],
        "items": items,
    }
    if not proto:
        q_semantik["model_scope"] = modelle
    q_semantik = _anwenden(queue_semantic, q_semantik)
    queue_id = _sha(_kanonisch(q_semantik))
    q_manifest: dict[str, Any] = {
        "schema_version": "1.0",
        "purpose": q_semantik.get("purpose"),
        "queue_id": queue_id,
        "pilot": q_semantik.get("pilot"),
        "role": "training_candidate_review",
        "created_utc": "2026-07-28T12:00:00Z",
        "frozen": True,
        "dataset_status": "review_incomplete",
        "warning": "fixture",
        "review_target": "Keine sichtbare Instanz irgendeiner gebundenen Detect-Klasse",
        "class_map_version": karte["version"],
        "class_map_sha256": karte_sha,
        "vsa_manifest_hash": karte["vsa_manifest_hash"],
        "class_names": namen,
        "protected_sets": geschuetzt,
        "protection_snapshot": {"byte_schutz": True},
        "selection_rule": auswahlregel,
        "sources": q_semantik.get("sources"),
        "candidates_count": len(eintraege),
        "images_count": len(eintraege),
        "holdings_count": len(eintraege),
        "hash_algorithm": "sha256",
        "hashes_count": len(queue_hashes),
        "hashes": queue_hashes,
        "semantic": q_semantik,
        "selection_receipt": (
            {"items": copy.deepcopy(q_semantik.get("items"))}
            if proto
            else {
                "models": copy.deepcopy(q_semantik.get("model_scope")),
                "items": copy.deepcopy(q_semantik.get("items")),
            }
        ),
    }
    q_manifest = _anwenden(queue_manifest, q_manifest)
    queue_manifest_pfad = belege / "queue_manifest.json"
    _schreibe_json(queue_manifest_pfad, q_manifest)
    queue_manifest_sha = _sha(queue_manifest_pfad.read_bytes())

    entscheid_je_id = {
        eintrag["item"][id_feld]: entscheidungen.get(eintrag["index"], "all_classes_clear")
        for eintrag in eintraege
    }
    r_dokument: dict[str, Any] = {
        "schema_version": "1.0",
        "purpose": "bcc_hard_negative_review",
        "queue_id": queue_id,
        "queue_manifest_sha256": queue_manifest_sha,
        "candidates_sha256": kandidaten_sha,
        "class_map_sha256": karte_sha,
        "reviewer": "Besitzer",
        "updated_at_utc": "2026-07-28T12:30:00Z",
        "decisions": {
            item_id: {"decision": wert, "comment": "", "reviewed_at_utc": "2026-07-28T12:30:00Z"}
            for item_id, wert in entscheid_je_id.items()
        },
    }
    r_dokument = _anwenden(review, r_dokument)
    review_pfad = belege / "review.json"
    _schreibe_json(review_pfad, r_dokument)
    review_sha = _sha(review_pfad.read_bytes())

    satz_eintraege = [
        eintrag
        for eintrag in eintraege
        if entscheid_je_id[eintrag["item"][id_feld]] == "all_classes_clear"
        and eintrag["index"] not in ohne_bild
    ]
    bild_belege: list[dict[str, Any]] = []
    for eintrag in satz_eintraege:
        sha = eintrag["sha"]
        haltung = str(AUDIT.normalize_holding_key(eintrag["haltung"]) or eintrag["haltung"]) if proto else eintrag["haltung"]
        beleg: dict[str, Any] = {
            "id": f"proto-neg-{sha}" if proto else f"bcc-neg-{sha}",
            "file_name": f"img_{sha}.png",
            "image_sha256": sha,
            "size_bytes": len(eintrag["daten"]),
            "image_format": "png",
            "holding_key": haltung,
            "physical_holding_key": _physisch(haltung) if "-" in haltung else haltung,
            "split": "",
            "review_item_id": eintrag["item"][id_feld],
            "review_decision": "all_classes_clear",
        }
        if proto:
            beleg["quelle"] = "xtf"
        else:
            beleg["source_ref"] = eintrag["item"]["source_ref"]
            beleg["inspection_date"] = "2026-07-28"
        bild_belege.append(beleg)
        (bilder_ordner / f"img_{sha}.png").write_bytes(eintrag["daten"])
    splits = _erwartete_splits([str(beleg["physical_holding_key"]) for beleg in bild_belege])
    for beleg in bild_belege:
        beleg["split"] = splits[str(beleg["physical_holding_key"])]
    validierung = sum(1 for wert in splits.values() if wert == "validation")
    bild_belege = _anwenden(bilder, bild_belege)

    anzahlen = {"all_classes_clear": 0, "mapped_object_visible": 0, "exclude_uncertain": 0}
    for wert in entscheid_je_id.values():
        anzahlen[wert] = anzahlen.get(wert, 0) + 1
    s_semantik: dict[str, Any] = {
        "schema_version": "1.0",
        "purpose": "proto_reviewed_negative_set" if proto else "bcc_reviewed_negative_set",
        "pilot": "protokoll_negative" if proto else "BCC_bogen",
        "role": "training_negative_set",
        "queue": {
            "queue_id": queue_id,
            "queue_manifest_sha256": queue_manifest_sha,
            "queue_manifest_receipt_path": "receipts/queue_manifest.json",
            "candidates_sha256": kandidaten_sha,
            "candidates_receipt_path": "receipts/queue_candidates.json",
        },
        "review": {
            "purpose": "bcc_hard_negative_review",
            "review_sha256": review_sha,
            "receipt_path": "receipts/review.json",
            "reviewed_images": len(entscheid_je_id),
            "decision_counts": anzahlen,
        },
        "class_map_version": karte["version"],
        "class_map_sha256": karte_sha,
        "class_map_receipt_path": "receipts/class_map.json",
        "vsa_manifest_hash": karte["vsa_manifest_hash"],
        "class_names": namen,
        "protected_sets": geschuetzt,
        "protection_snapshot": {"byte_schutz": True},
        "split_rule": {
            "name": "stable_rank_v1",
            "salt": SPLIT_SALZ,
            "one_image_per_physical_holding": True,
            "validation_count": validierung,
            "train_count": len(bild_belege) - validierung,
        },
        "images": bild_belege,
    }
    if proto:
        s_semantik["excluded_not_normalizable"] = []
        s_semantik["excluded_eval_protected"] = []
    s_semantik = _anwenden(semantic, s_semantik, wurzel)
    satz_id = _sha(_kanonisch(s_semantik))

    _anwenden(dateien, staging)
    hashes = {}
    for pfad in sorted([*bilder_ordner.iterdir(), *belege.iterdir()], key=lambda p: p.name):
        if pfad.is_file():
            hashes[pfad.relative_to(staging).as_posix()] = {
                "sha256": _sha(pfad.read_bytes()),
                "size_bytes": pfad.stat().st_size,
            }
    m_dokument: dict[str, Any] = {
        "schema_version": "1.0",
        "purpose": s_semantik.get("purpose") if isinstance(s_semantik, dict) else None,
        "set_id": satz_id,
        "pilot": "protokoll_negative" if proto else "BCC_bogen",
        "role": "training_negative_set",
        "created_utc": "2026-07-28T13:00:00Z",
        "frozen": True,
        "dataset_status": "ready_for_training",
        "hash_algorithm": "sha256",
        "images_count": len(bild_belege) if isinstance(bild_belege, list) else 0,
        "holdings_count": len(bild_belege) if isinstance(bild_belege, list) else 0,
        "hashes_count": len(hashes),
        "hashes": hashes,
        "semantic": s_semantik,
    }
    m_dokument = _anwenden(manifest, m_dokument)
    m_bytes = (json.dumps(m_dokument, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    if manifest_bytes is not None:
        m_bytes = manifest_bytes(m_bytes)
    (staging / "_manifest.json").write_bytes(m_bytes)
    praefix = "proto_hn" if proto else "bcc_hn"
    ziel_name = f"{praefix}_{satz_id[:12]}"
    if ordnername is not None:
        ziel_name = ordnername(ziel_name)
    ziel = staging.with_name(ziel_name)
    staging.rename(ziel)
    return ziel


def pruefe(wurzel: Path, satz: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    return AUDIT._read_reviewed_negative_set(wurzel, satz)


def genau(text: str) -> str:
    return "^" + re.escape(text) + "$"


@pytest.fixture()
def wurzel():
    with tempfile.TemporaryDirectory() as temporaer:
        yield Path(temporaer)


# ---------------------------------------------------------------------------
# Gueltige Saetze: Ergebnis und Provenienz exakt festhalten
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("variante", ["bcc", "proto"])
def test_gueltiger_satz_wird_mit_vollstaendiger_provenienz_angenommen(wurzel: Path, variante: str) -> None:
    satz = baue_satz(wurzel, variante)
    bilder, provenienz = pruefe(wurzel, satz)

    manifest_bytes = (satz / "_manifest.json").read_bytes()
    manifest = json.loads(manifest_bytes)
    semantik = manifest["semantic"]
    queue = semantik["queue"]
    erwartete_bilder = sorted(
        (
            {
                "path": f"training/negatives/sets/{satz.name}/images/{beleg['file_name']}",
                "sha256": beleg["image_sha256"],
                "split": beleg["split"],
                "source_type": "reviewed_negative_set",
                "holding_key": beleg["holding_key"],
                "physical_holding_key": beleg["physical_holding_key"],
                "set_id": manifest["set_id"],
                "set_manifest_sha256": _sha(manifest_bytes),
                "queue_id": queue["queue_id"],
                "queue_manifest_sha256": queue["queue_manifest_sha256"],
                "candidates_sha256": queue["candidates_sha256"],
                "review_sha256": semantik["review"]["review_sha256"],
                "class_map_version": 3,
                "class_map_sha256": semantik["class_map_sha256"],
                "vsa_manifest_hash": semantik["vsa_manifest_hash"],
                "review_item_id": beleg["review_item_id"],
                "review_decision": "all_classes_clear",
            }
            for beleg in semantik["images"]
        ),
        key=lambda eintrag: eintrag["sha256"],
    )
    assert bilder == erwartete_bilder
    validierung = semantik["split_rule"]["validation_count"]
    assert provenienz == {
        "set_id": manifest["set_id"],
        "root_path": f"training/negatives/sets/{satz.name}",
        "manifest_sha256": _sha(manifest_bytes),
        "queue_id": queue["queue_id"],
        "queue_manifest_sha256": queue["queue_manifest_sha256"],
        "candidates_sha256": queue["candidates_sha256"],
        "review_sha256": semantik["review"]["review_sha256"],
        "class_map_version": 3,
        "class_map_sha256": semantik["class_map_sha256"],
        "vsa_manifest_hash": semantik["vsa_manifest_hash"],
        "images": 3,
        "train_images": 3 - validierung,
        "validation_images": validierung,
    }
    assert validierung == 1


@pytest.mark.parametrize("variante", ["bcc", "proto"])
def test_nicht_klassenfreie_entscheidungen_bleiben_draussen(wurzel: Path, variante: str) -> None:
    satz = baue_satz(
        wurzel,
        variante,
        entscheidungen={0: "mapped_object_visible", 1: "exclude_uncertain"},
    )
    bilder, provenienz = pruefe(wurzel, satz)
    assert len(bilder) == 1
    assert provenienz["images"] == 1
    assert provenienz["validation_images"] == 0


def test_proto_nicht_normierbare_haltung_darf_begruendet_fehlen(wurzel: Path) -> None:
    items: list[dict] = []
    satz = baue_satz(
        wurzel,
        "proto",
        ("unbekannt", "300-400", "500-600"),
        ohne_bild=(0,),
        queue_items=lambda liste: items.extend(liste),
        semantic=lambda semantik, _w: semantik.__setitem__(
            "excluded_not_normalizable", [items[0]["item_id"]]
        ),
    )
    bilder, provenienz = pruefe(wurzel, satz)
    assert len(bilder) == 2
    assert provenienz["images"] == 2


def _eval_kandidaten(wurzel: Path, haltung: str) -> None:
    ordner = wurzel / "eval_set" / "subsets" / "fixture"
    ordner.mkdir(parents=True, exist_ok=True)
    _schreibe_json(ordner / "_candidates.json", [{"haltung_key": haltung}])


def _test_bericht(wurzel: Path, haltung: str) -> None:
    ordner = wurzel / "training" / "reports"
    ordner.mkdir(parents=True, exist_ok=True)
    _schreibe_json(
        ordner / "gold_stock_audit_20260101_000000_000.json",
        {"split": {"gruppen": [{"rolle": "test", "gruppe": f"haltung:{haltung}"}]}},
    )


@pytest.mark.parametrize("schutzquelle", ["eval_kandidaten", "gold_testbericht"])
def test_proto_eval_geschuetzte_haltung_darf_begruendet_fehlen(wurzel: Path, schutzquelle: str) -> None:
    if schutzquelle == "eval_kandidaten":
        _eval_kandidaten(wurzel, "600-500")
    else:
        _test_bericht(wurzel, "06.500-600")
    items: list[dict] = []
    satz = baue_satz(
        wurzel,
        "proto",
        ohne_bild=(2,),
        queue_items=lambda liste: items.extend(liste),
        semantic=lambda semantik, _w: semantik.__setitem__(
            "excluded_eval_protected", [items[2]["item_id"]]
        ),
    )
    bilder, _ = pruefe(wurzel, satz)
    assert len(bilder) == 2


def _gold_ausrichtung(wurzel: Path, physisch: str, rolle: str | None = None, ziel: str | None = None):
    """Semantik-Haken fuer die Proto-Splitregel stable_rank_v1_gold_aligned."""

    def haken(semantik: dict, _wurzel: Path) -> None:
        gold_rolle = rolle or AUDIT.split_role("haltung:" + physisch.replace("|", "-"))
        erzwungen = ziel or ("train" if gold_rolle == "train" else "validation")
        for beleg in semantik["images"]:
            if beleg["physical_holding_key"] == physisch:
                beleg["split"] = erzwungen
        validierung = sum(1 for beleg in semantik["images"] if beleg["split"] == "validation")
        semantik["split_rule"] = {
            "name": "stable_rank_v1_gold_aligned",
            "salt": SPLIT_SALZ,
            "one_image_per_physical_holding": True,
            "validation_count": validierung,
            "train_count": len(semantik["images"]) - validierung,
            "gold_alignments": [
                {"physical_holding_key": physisch, "gold_role": gold_rolle, "forced_split": erzwungen}
            ],
        }

    return haken


def _gold_samples(wurzel: Path, *haltungen: str) -> None:
    _schreibe_json(wurzel / "training_samples.json", [{"CaseId": haltung} for haltung in haltungen])


def test_proto_gold_ausgerichteter_split_wird_angenommen(wurzel: Path) -> None:
    _gold_samples(wurzel, "500-600")
    satz = baue_satz(wurzel, "proto", semantic=_gold_ausrichtung(wurzel, "500|600"))
    bilder, provenienz = pruefe(wurzel, satz)
    gold_rolle = AUDIT.split_role("haltung:500-600")
    erwartet = "train" if gold_rolle == "train" else "validation"
    assert next(bild for bild in bilder if bild["physical_holding_key"] == "500|600")["split"] == erwartet
    assert provenienz["validation_images"] == sum(1 for bild in bilder if bild["split"] == "validation")


# ---------------------------------------------------------------------------
# Ablehnungsfaelle: Eingriff -> exakte heutige Meldung je Variante
# ---------------------------------------------------------------------------


def _setze(pfad: str, wert: Any) -> Callable[..., None]:
    """Haken, der ein (punktgetrenntes) Feld eines Dokuments setzt."""

    def haken(dokument: Any, *_: Any) -> None:
        ziel = dokument
        teile = pfad.split(".")
        for teil in teile[:-1]:
            ziel = ziel[int(teil)] if isinstance(ziel, list) else ziel[teil]
        letzter = teile[-1]
        if isinstance(ziel, list):
            ziel[int(letzter)] = wert
        else:
            ziel[letzter] = wert

    return haken


def _entferne(pfad: str) -> Callable[..., None]:
    def haken(dokument: Any, *_: Any) -> None:
        ziel = dokument
        teile = pfad.split(".")
        for teil in teile[:-1]:
            ziel = ziel[int(teil)] if isinstance(ziel, list) else ziel[teil]
        del ziel[teile[-1]]

    return haken


def _dazu(pfad: str, feld: str = "fremd", wert: Any = 1) -> Callable[..., None]:
    def haken(dokument: Any, *_: Any) -> None:
        ziel = dokument
        for teil in [teil for teil in pfad.split(".") if teil]:
            ziel = ziel[int(teil)] if isinstance(ziel, list) else ziel[teil]
        ziel[feld] = wert

    return haken


def _items(haken: Callable[[list], Any]) -> Callable[[dict], None]:
    """Wendet einen Listen-Eingriff auf die Queue-Items der Semantik an."""

    def aussen(semantik: dict) -> None:
        haken(semantik["items"])

    return aussen


def _gegenrichtung(liste: list) -> None:
    liste[1]["holding_key"] = "200-100"
    if "physical_holding_key" in liste[1]:
        liste[1]["physical_holding_key"] = "100|200"


def _doppelte_bildzeile(bilder: list) -> Ersatz:
    return Ersatz([bilder[0], *bilder])


def _entferne_erstes_bild(staging: Path) -> None:
    ersten = sorted((staging / "images").iterdir())[0]
    ersten.unlink()


def _tausche_bildinhalt(staging: Path) -> None:
    bild = sorted((staging / "images").iterdir())[0]
    daten = bild.read_bytes()
    bild.write_bytes(daten[:-1] + (b"y" if daten[-1:] != b"y" else b"z"))


def _satz_zweck_zurueck(manifest: dict) -> None:
    proto = manifest["pilot"] == "protokoll_negative"
    manifest["purpose"] = "proto_reviewed_negative_set" if proto else "bcc_reviewed_negative_set"


def _queue_zweck_zurueck(queue_manifest: dict) -> None:
    proto = queue_manifest["pilot"] == "protokoll_negative"
    queue_manifest["purpose"] = (
        "proto_hard_negative_review_queue" if proto else "bcc_hard_negative_review_queue"
    )


def _queue_pilot_zurueck(queue_manifest: dict) -> None:
    proto = queue_manifest["purpose"].startswith("proto")
    queue_manifest["pilot"] = "protokoll_negative" if proto else "BCC_bogen"


def _zusatzbeleg(staging: Path) -> None:
    (staging / "receipts" / "extra.json").write_text("{}", encoding="utf-8")


def _unterordner_im_bildordner(staging: Path) -> None:
    (staging / "images" / "ordner").mkdir()


def _nicht_klassenfrei(dokument: dict) -> None:
    erste = next(iter(dokument["decisions"]))
    dokument["decisions"][erste]["decision"] = "negative"


# Jeder Fall: (Kennung, Baukasten-Argumente, {"bcc": Meldung, "proto": Meldung}).
# Meldung ist ein regulaerer Ausdruck; ``None`` heisst "Regel gilt fuer diese
# Variante nicht".
FAELLE: list[tuple[str, dict[str, Any], dict[str, str | None]]] = [
    # --- JSON-Struktur des Satz-Manifests ------------------------------------
    (
        "manifest_kein_json",
        {"manifest_bytes": lambda _b: b"{kaputt"},
        {"bcc": genau("Negativsatz-Manifest ist kein sicher lesbares JSON."),
         "proto": genau("Negativsatz-Manifest ist kein sicher lesbares JSON.")},
    ),
    (
        "manifest_doppeltes_feld",
        {"manifest_bytes": lambda b: b.replace(b'"frozen": true,', b'"frozen": true, "frozen": true,', 1)},
        {"bcc": genau("Negativsatz-Manifest enthaelt ein doppeltes Feld: frozen"),
         "proto": genau("Negativsatz-Manifest enthaelt ein doppeltes Feld: frozen")},
    ),
    (
        "manifest_unbekanntes_feld",
        {"manifest": _dazu("")},
        {"bcc": genau("Negativsatz-Manifest hat fehlende oder fremde Felder."),
         "proto": genau("Negativsatz-Manifest hat fehlende oder fremde Felder.")},
    ),
    (
        "manifest_fehlendes_feld",
        {"manifest": _entferne("created_utc")},
        {"bcc": genau("Negativsatz-Manifest hat fehlende oder fremde Felder."),
         "proto": genau("Negativsatz-Manifest hat fehlende oder fremde Felder.")},
    ),
    # --- Rollen, Versionen, Einfrieren -----------------------------------------
    *[
        (
            f"manifest_kopf_{feld}",
            {"manifest": _setze(feld, wert)},
            {"bcc": genau("Der Negativsatz ist nicht streng eingefroren und trainingsbereit."),
             "proto": genau("Der Proto-Negativsatz ist nicht streng eingefroren und trainingsbereit.")},
        )
        for feld, wert in (
            ("schema_version", "2.0"),
            ("pilot", "fremd"),
            ("role", "eval_set"),
            ("frozen", False),
            ("dataset_status", "review_incomplete"),
            ("hash_algorithm", "md5"),
            ("created_utc", "2026-07-28T13:00:00"),
            ("created_utc", 20260728),
        )
    ],
    (
        # Unbekannter Zweck faellt nie in den Proto-Pfad, sondern in den strengen BCC-Pfad.
        "manifest_unbekannter_zweck",
        {"manifest": _setze("purpose", "fremd")},
        {"bcc": genau("Der Negativsatz ist nicht streng eingefroren und trainingsbereit."),
         "proto": genau("Der Negativsatz ist nicht streng eingefroren und trainingsbereit.")},
    ),
    (
        "semantik_unbekanntes_feld",
        {"semantic": _dazu("")},
        {"bcc": genau("Semantischer Negativsatz-Beleg hat fehlende oder fremde Felder."),
         "proto": genau("Semantischer Proto-Negativsatz-Beleg hat fehlende oder fremde Felder.")},
    ),
    (
        "semantik_falsche_rolle",
        {"semantic": _setze("role", "eval_set")},
        {"bcc": genau("Manifest und semantischer Negativsatz-Beleg widersprechen sich."),
         "proto": genau("Manifest und semantischer Proto-Beleg widersprechen sich.")},
    ),
    (
        "semantik_falscher_zweck",
        {"semantic": _setze("purpose", "fremd"), "manifest": _satz_zweck_zurueck},
        {"bcc": genau("Manifest und semantischer Negativsatz-Beleg widersprechen sich."),
         "proto": genau("Manifest und semantischer Proto-Beleg widersprechen sich.")},
    ),
    (
        "semantik_falsche_version",
        {"semantic": _setze("schema_version", "1.1")},
        {"bcc": genau("Manifest und semantischer Negativsatz-Beleg widersprechen sich."),
         "proto": genau("Manifest und semantischer Proto-Beleg widersprechen sich.")},
    ),
    (
        "satz_id_kein_sha",
        {"manifest": _setze("set_id", "keine-id")},
        {"bcc": genau("Negativsatz-ID ist kein gueltiger SHA-256."),
         "proto": genau("Proto-Negativsatz-ID ist kein gueltiger SHA-256.")},
    ),
    (
        "satz_id_passt_nicht",
        {"manifest": _setze("set_id", "f" * 64)},
        {"bcc": genau("Die Negativsatz-ID passt nicht zum semantischen Beleg."),
         "proto": genau("Die Proto-Negativsatz-ID passt nicht zum semantischen Beleg.")},
    ),
    (
        "ordnername_passt_nicht",
        {"ordnername": lambda name: name[:-1] + ("0" if name[-1] != "0" else "1")},
        {"bcc": genau("Der Negativsatz-Ordner passt nicht zur Negativsatz-ID."),
         "proto": genau("Der Proto-Negativsatz-Ordner passt nicht zur Satz-ID.")},
    ),
    (
        "falsches_praefix",
        {"ordnername": lambda name: ("proto_hn_" if name.startswith("bcc") else "bcc_hn_") + name.split("_")[-1]},
        {"bcc": genau("Der Negativsatz-Ordner passt nicht zur Negativsatz-ID."),
         "proto": genau("Der Proto-Negativsatz-Ordner passt nicht zur Satz-ID.")},
    ),
    # --- Dateien und Hashes -------------------------------------------------------
    (
        "dateihashes_fehlen",
        {"manifest": _setze("hashes", [])},
        {"bcc": genau("Das Negativsatz-Manifest besitzt keine Datei-Hashes."),
         "proto": genau("Das Negativsatz-Manifest besitzt keine Datei-Hashes.")},
    ),
    (
        "hashes_count_falsch",
        {"manifest": lambda m: m.__setitem__("hashes_count", m["hashes_count"] + 1)},
        {"bcc": genau("hashes_count im Negativsatz ist falsch."),
         "proto": genau("hashes_count im Negativsatz ist falsch.")},
    ),
    (
        "unterordner_im_bildordner",
        {"dateien": _unterordner_im_bildordner},
        {"bcc": r"^Negativsatz enthaelt fremde oder unsichere Datei: .*ordner$",
         "proto": r"^Negativsatz enthaelt fremde oder unsichere Datei: .*ordner$"},
    ),
    (
        "zusaetzlicher_beleg",
        {"dateien": _zusatzbeleg},
        {"bcc": genau("Der Negativsatz besitzt nicht exakt die vier gebundenen Belege."),
         "proto": genau("Der Negativsatz besitzt nicht exakt die vier gebundenen Belege.")},
    ),
    (
        "hashabdeckung_unvollstaendig",
        {"manifest": lambda m: (m["hashes"].pop(next(iter(m["hashes"]))), m.__setitem__("hashes_count", len(m["hashes"])))},
        {"bcc": genau("Die Hashabdeckung des Negativsatzes ist unvollstaendig."),
         "proto": genau("Die Hashabdeckung des Negativsatzes ist unvollstaendig.")},
    ),
    (
        "hashbeleg_fremdes_feld",
        {"manifest": lambda m: m["hashes"]["receipts/review.json"].__setitem__("fremd", 1)},
        {"bcc": genau("Hashbeleg receipts/review.json hat fehlende oder fremde Felder."),
         "proto": genau("Hashbeleg receipts/review.json hat fehlende oder fremde Felder.")},
    ),
    (
        "hashbeleg_kein_sha",
        {"manifest": lambda m: m["hashes"]["receipts/review.json"].__setitem__("sha256", "xyz")},
        {"bcc": genau("Datei-Hash receipts/review.json ist kein gueltiger SHA-256."),
         "proto": genau("Datei-Hash receipts/review.json ist kein gueltiger SHA-256.")},
    ),
    (
        "manipulierte_pruefsumme",
        {"manifest": lambda m: m["hashes"]["receipts/review.json"].__setitem__("sha256", "0" * 64)},
        {"bcc": genau("Hash oder Groesse stimmt nicht: receipts/review.json"),
         "proto": genau("Hash oder Groesse stimmt nicht: receipts/review.json")},
    ),
    (
        "manipulierte_groesse",
        {"manifest": lambda m: m["hashes"]["receipts/review.json"].__setitem__("size_bytes", 1)},
        {"bcc": genau("Hash oder Groesse stimmt nicht: receipts/review.json"),
         "proto": genau("Hash oder Groesse stimmt nicht: receipts/review.json")},
    ),
    (
        "negative_groesse",
        {"manifest": lambda m: m["hashes"]["receipts/review.json"].__setitem__("size_bytes", -1)},
        {"bcc": genau("Dateigroesse receipts/review.json ist keine gueltige Anzahl."),
         "proto": genau("Dateigroesse receipts/review.json ist keine gueltige Anzahl.")},
    ),
    # --- Beleg-Bindungen im semantischen Satz ------------------------------------
    (
        "queue_bindung_fremdes_feld",
        {"semantic": _dazu("queue")},
        {"bcc": genau("Queue-Bindung hat fehlende oder fremde Felder."),
         "proto": genau("Proto-Queue-Bindung hat fehlende oder fremde Felder.")},
    ),
    (
        "review_bindung_fremdes_feld",
        {"semantic": _dazu("review")},
        {"bcc": genau("Review-Bindung hat fehlende oder fremde Felder."),
         "proto": genau("Proto-Review-Bindung hat fehlende oder fremde Felder.")},
    ),
    *[
        (
            f"fester_belegpfad_{name}",
            {"semantic": _setze(pfad, "receipts/anders.json")},
            {"bcc": genau("Der Negativsatz verweist nicht auf die festen Belegpfade."),
             "proto": genau("Der Proto-Negativsatz verweist nicht auf die festen Belegpfade.")},
        )
        for name, pfad in (
            ("queue", "queue.queue_manifest_receipt_path"),
            ("kandidaten", "queue.candidates_receipt_path"),
            ("review", "review.receipt_path"),
            ("klassenkarte", "class_map_receipt_path"),
        )
    ],
    (
        "queue_manifest_hash_kein_sha",
        {"semantic": _setze("queue.queue_manifest_sha256", "kurz")},
        {"bcc": genau("Queue-Manifest-Hash ist kein gueltiger SHA-256."),
         "proto": genau("Queue-Manifest-Hash ist kein gueltiger SHA-256.")},
    ),
    (
        "queue_manifest_beleg_manipuliert",
        {"semantic": _setze("queue.queue_manifest_sha256", "a" * 64)},
        {"bcc": genau("Der Queue-Manifest-Beleg passt nicht zum Negativsatz."),
         "proto": genau("Der Queue-Manifest-Beleg passt nicht zum Proto-Satz.")},
    ),
    (
        "kandidaten_beleg_manipuliert",
        {"semantic": _setze("queue.candidates_sha256", "a" * 64)},
        {"bcc": genau("Der Kandidaten-Beleg passt nicht zum Negativsatz."),
         "proto": genau("Der Kandidaten-Beleg passt nicht zum Proto-Satz.")},
    ),
    (
        "review_beleg_manipuliert",
        {"semantic": _setze("review.review_sha256", "a" * 64)},
        {"bcc": genau("Der Review-Beleg passt nicht zum Negativsatz."),
         "proto": genau("Der Review-Beleg passt nicht zum Proto-Satz.")},
    ),
    # --- Klassenkarte -----------------------------------------------------------------
    (
        "klassenkarte_hash_passt_nicht",
        {"semantic": _setze("class_map_sha256", "a" * 64)},
        {"bcc": genau("Der Klassenkarten-Beleg passt nicht zum Negativsatz."),
         "proto": genau("Der Klassenkarten-Beleg passt nicht zum Negativsatz.")},
    ),
    (
        "fremde_klassenkarte",
        {"klassenkarte_bytes": KLASSENKARTE.read_bytes() + b"\n"},
        {"bcc": genau("Der Negativsatz passt nicht zur aktiven Detect-Klassenkarte."),
         "proto": genau("Der Negativsatz passt nicht zur aktiven Detect-Klassenkarte.")},
    ),
    (
        "klassennamen_nicht_gebunden",
        {"semantic": lambda s, _w: s.__setitem__("class_names", list(reversed(s["class_names"])))},
        {"bcc": genau("Klassenkarte und Negativsatz sind nicht fest verbunden."),
         "proto": genau("Klassenkarte und Negativsatz sind nicht fest verbunden.")},
    ),
    (
        "klassenkarten_version_nicht_gebunden",
        {"semantic": _setze("class_map_version", 2)},
        {"bcc": genau("Klassenkarte und Negativsatz sind nicht fest verbunden."),
         "proto": genau("Klassenkarte und Negativsatz sind nicht fest verbunden.")},
    ),
    # --- Queue-Manifest -----------------------------------------------------------------
    (
        "queue_manifest_fremdes_feld",
        {"queue_manifest": _dazu("")},
        {"bcc": genau("Queue-Manifest-Beleg hat fehlende oder fremde Felder."),
         "proto": genau("Queue-Manifest-Beleg hat fehlende oder fremde Felder.")},
    ),
    (
        "queue_semantik_fremdes_feld",
        {"queue_semantic": _dazu("")},
        {"bcc": genau("Semantischer Queue-Beleg hat fehlende oder fremde Felder."),
         "proto": genau("Semantischer Proto-Queue-Beleg hat fehlende oder fremde Felder.")},
    ),
    (
        "proto_queue_mit_modellbindung",
        {"queue_semantic": _dazu("", "model_scope", [])},
        {"bcc": None,
         "proto": genau("Semantischer Proto-Queue-Beleg hat fehlende oder fremde Felder.")},
    ),
    (
        "queue_id_kein_sha",
        {"semantic": _setze("queue.queue_id", "kurz")},
        {"bcc": genau("Queue-ID ist kein gueltiger SHA-256."),
         "proto": genau("Proto-Queue-ID ist kein gueltiger SHA-256.")},
    ),
    *[
        (
            f"queue_kopf_{feld}",
            {"queue_manifest": _setze(feld, wert)},
            {"bcc": genau("Der Queue-Beleg ist nicht fest an die Queue-ID gebunden."),
             "proto": genau("Der Proto-Queue-Beleg ist nicht fest an die Queue-ID gebunden.")},
        )
        for feld, wert in (
            ("schema_version", "2.0"),
            ("purpose", "fremd"),
            ("queue_id", "b" * 64),
            ("pilot", "fremd"),
            ("role", "training_negative_set"),
            ("frozen", False),
            ("hash_algorithm", "md5"),
        )
    ],
    (
        "queue_semantik_falsche_rolle",
        {"queue_semantic": _setze("role", "training_negative_set")},
        {"bcc": genau("Der Queue-Beleg ist nicht fest an die Queue-ID gebunden."),
         "proto": genau("Der Proto-Queue-Beleg ist nicht fest an die Queue-ID gebunden.")},
    ),
    (
        # Nur die Queue-Semantik weicht ab; das Queue-Manifest behaelt seinen Zweck.
        "queue_semantik_falscher_zweck",
        {"queue_semantic": _setze("purpose", "fremd"), "queue_manifest": _queue_zweck_zurueck},
        {"bcc": genau("Der Queue-Beleg ist nicht fest an die Queue-ID gebunden."),
         "proto": genau("Der Proto-Queue-Beleg ist nicht fest an die Queue-ID gebunden.")},
    ),
    (
        "queue_semantik_falscher_pilot",
        {"queue_semantic": _setze("pilot", "fremd"), "queue_manifest": _queue_pilot_zurueck},
        {"bcc": genau("Der Queue-Beleg ist nicht fest an die Queue-ID gebunden."),
         "proto": genau("Der Proto-Queue-Beleg ist nicht fest an die Queue-ID gebunden.")},
    ),
    (
        "queue_id_passt_nicht_zur_semantik",
        {"queue_manifest": lambda q: q["semantic"].__setitem__("sources", ["nachtraeglich"])},
        {"bcc": genau("Der Queue-Beleg ist nicht fest an die Queue-ID gebunden."),
         "proto": genau("Der Proto-Queue-Beleg ist nicht fest an die Queue-ID gebunden.")},
    ),
    *[
        (
            f"queue_widerspricht_satz_{feld}",
            {"queue_manifest": _setze(feld, wert)},
            {"bcc": genau(f"Queue und Negativsatz widersprechen sich bei {feld}."),
             "proto": genau(f"Proto-Queue und Negativsatz widersprechen sich bei {feld}.")},
        )
        for feld, wert in (
            ("class_map_version", 2),
            ("class_map_sha256", "c" * 64),
            ("vsa_manifest_hash", "c" * 64),
            ("class_names", ["x"]),
            ("protected_sets", []),
            ("protection_snapshot", {}),
        )
    ],
    (
        "queue_semantik_widerspricht_schutzmenge",
        {"queue_semantic": _setze("protected_sets", [])},
        {"bcc": genau("Queue und Negativsatz widersprechen sich bei protected_sets."),
         "proto": genau("Proto-Queue und Negativsatz widersprechen sich bei protected_sets.")},
    ),
    *[
        (
            f"proto_auswahlregel_{name}",
            {"queue_semantic": haken},
            {"bcc": None,
             "proto": genau("Die Proto-Auswahlregel muss modellfrei und haltungseinheitlich sein.")},
        )
        for name, haken in (
            ("kein_objekt", _setze("selection_rule", [])),
            ("mehrere_bilder_je_haltung", _setze("selection_rule.one_image_per_physical_holding", False)),
            ("modell_beteiligt", _setze("selection_rule.model_involved", True)),
            ("modell_fehlt", _entferne("selection_rule.model_involved")),
            ("bcc_trigger", _dazu("selection_rule", "requires_current_model_bcc_trigger", True)),
        )
    ],
    (
        "queue_hashliste_fehlt",
        {"queue_manifest": _setze("hashes", [])},
        {"bcc": genau("Der Queue-Beleg besitzt keine gueltige Hashliste."),
         "proto": genau("Der Proto-Queue-Beleg besitzt keine gueltige Hashliste.")},
    ),
    (
        "queue_hashes_count_falsch",
        {"queue_manifest": _setze("hashes_count", 99)},
        {"bcc": genau("Der Queue-Beleg besitzt keine gueltige Hashliste."),
         "proto": genau("Der Proto-Queue-Beleg besitzt keine gueltige Hashliste.")},
    ),
    (
        "queue_kandidatenhash_fehlt",
        {"queue_manifest": lambda q: (q["hashes"].pop("_candidates.json"), q.__setitem__("hashes_count", len(q["hashes"])))},
        {"bcc": genau("Queue-Kandidaten-Hash hat fehlende oder fremde Felder."),
         "proto": genau("Queue-Kandidaten-Hash hat fehlende oder fremde Felder.")},
    ),
    (
        "queue_kandidatenhash_falsch",
        {"queue_manifest": lambda q: q["hashes"]["_candidates.json"].__setitem__("sha256", "d" * 64)},
        {"bcc": genau("Die Queue bindet den Kandidaten-Beleg nicht bytegenau."),
         "proto": genau("Die Proto-Queue bindet den Kandidaten-Beleg nicht bytegenau.")},
    ),
    (
        "queue_kandidatengroesse_falsch",
        {"queue_manifest": lambda q: q["hashes"]["_candidates.json"].__setitem__("size_bytes", 3)},
        {"bcc": genau("Die Queue bindet den Kandidaten-Beleg nicht bytegenau."),
         "proto": genau("Die Proto-Queue bindet den Kandidaten-Beleg nicht bytegenau.")},
    ),
    # --- Auswahlbeleg und Modelle ------------------------------------------------------
    (
        "auswahlbeleg_fremdes_feld",
        {"queue_manifest": _dazu("selection_receipt")},
        {"bcc": genau("Queue-Auswahlbeleg hat fehlende oder fremde Felder."),
         "proto": genau("Proto-Queue-Auswahlbeleg hat fehlende oder fremde Felder.")},
    ),
    (
        "proto_auswahlbeleg_mit_modellen",
        {"queue_manifest": _dazu("selection_receipt", "models", [])},
        {"bcc": None,
         "proto": genau("Proto-Queue-Auswahlbeleg hat fehlende oder fremde Felder.")},
    ),
    (
        "keine_auswahlmodelle",
        {"queue_semantic": _setze("model_scope", [])},
        {"bcc": genau("Der Queue-Beleg besitzt keine gebundenen Auswahlmodelle."),
         "proto": None},
    ),
    (
        "auswahlmodell_fremdes_feld",
        {"queue_semantic": _dazu("model_scope.0")},
        {"bcc": genau("Queue-Auswahlmodell hat fehlende oder fremde Felder."), "proto": None},
    ),
    (
        "auswahlmodell_doppelt",
        {"queue_semantic": lambda q: q["model_scope"].append(dict(q["model_scope"][0]))},
        {"bcc": genau("Der Queue-Beleg besitzt doppelte oder leere Modell-IDs."), "proto": None},
    ),
    (
        "auswahlmodell_gewicht_kein_sha",
        {"queue_semantic": _setze("model_scope.0.weights_sha256", "kurz")},
        {"bcc": genau("weights_sha256 von fixture-modell ist kein gueltiger SHA-256."), "proto": None},
    ),
    (
        "auswahlbeleg_modelle_widersprechen",
        {"queue_manifest": _setze("selection_receipt.models", [])},
        {"bcc": genau("Semantischer Queue-Beleg und Auswahlbeleg widersprechen sich."), "proto": None},
    ),
    (
        "auswahlbeleg_items_widersprechen",
        {"queue_manifest": lambda q: q["selection_receipt"]["items"].pop()},
        {"bcc": genau("Semantischer Queue-Beleg und Auswahlbeleg widersprechen sich."),
         "proto": genau("Semantischer Proto-Beleg und Auswahlbeleg widersprechen sich.")},
    ),
    # --- Queue-Bildbelege ----------------------------------------------------------------
    (
        "queue_item_fremdes_feld",
        {"queue_items": _dazu("0")},
        {"bcc": genau("Queue-Bildbeleg hat fehlende oder fremde Felder."),
         "proto": genau("Proto-Queue-Bildbeleg hat fehlende oder fremde Felder.")},
    ),
    (
        "queue_item_doppelte_id",
        {"queue_items": lambda liste: liste.append(dict(liste[0]))},
        {"bcc": genau("Der Queue-Beleg enthaelt doppelte oder leere Bild-IDs."),
         "proto": r"^Proto-Queue-Bildbeleg proto-hn-[0-9a-f]{20} ist ungueltig\.$"},
    ),
    (
        "queue_item_bildhash_kein_sha",
        {"queue_items": _setze("0.image_sha256", "kurz")},
        {"bcc": r"^Queue-Bildhash bcc-hn-[0-9a-f]{16} ist kein gueltiger SHA-256\.$",
         "proto": r"^Proto-Queue-Bildhash proto-hn-[0-9a-f]{20} ist kein gueltiger SHA-256\.$"},
    ),
    (
        "queue_item_id_passt_nicht_zum_hash",
        {"queue_items": lambda liste: liste[0].__setitem__("id" if "id" in liste[0] else "item_id", ("bcc-hn-" if "id" in liste[0] else "proto-hn-") + "0" * 20)},
        {"bcc": r"^Queue-Bildbeleg bcc-hn-0+ ist ungueltig\.$",
         "proto": r"^Proto-Queue-Bildbeleg proto-hn-0+ ist ungueltig\.$"},
    ),
    (
        "queue_item_fremde_haltung",
        {"queue_items": _setze("0.physical_holding_key", "900|901")},
        {"bcc": r"^Queue-Bildbeleg bcc-hn-[0-9a-f]{16} ist ungueltig\.$", "proto": None},
    ),
    (
        "queue_item_gegenrichtung",
        {"queue_items": _gegenrichtung},
        {"bcc": r"^Queue-Bildbeleg bcc-hn-[0-9a-f]{16} ist ungueltig\.$",
         "proto": r"^Proto-Queue-Bildbeleg proto-hn-[0-9a-f]{20} ist ungueltig\.$"},
    ),
    (
        "queue_item_haltung_nicht_normiert",
        {"queue_items": _setze("0.holding_key", "06.100-200")},
        {"bcc": genau("Negativsatz besitzt keine normalisierte Haltungsidentitaet: 06.100-200"),
         "proto": None},
    ),
    (
        "queue_item_zu_klein",
        {"queue_items": _setze("0.size_bytes", 1023)},
        {"bcc": r"^Queue-Bildbeleg bcc-hn-[0-9a-f]{16} ist ungueltig\.$",
         "proto": r"^Proto-Queue-Bildbeleg proto-hn-[0-9a-f]{20} ist ungueltig\.$"},
    ),
    (
        "queue_item_groesse_keine_zahl",
        {"queue_items": _setze("0.size_bytes", "1024")},
        {"bcc": r"^Queue-Bildgroesse bcc-hn-[0-9a-f]{16} ist keine gueltige Anzahl\.$",
         "proto": r"^Proto-Queue-Bildgroesse proto-hn-[0-9a-f]{20} ist keine gueltige Anzahl\.$"},
    ),
    (
        "queue_item_format",
        {"queue_items": _setze("0.image_format", "gif")},
        {"bcc": r"^Queue-Bildbeleg bcc-hn-[0-9a-f]{16} ist ungueltig\.$",
         "proto": r"^Proto-Queue-Bildbeleg proto-hn-[0-9a-f]{20} ist ungueltig\.$"},
    ),
    (
        # Format und Zieldatei passen zueinander, das Format ist aber nicht erlaubt.
        "proto_queue_item_format_mit_zieldatei",
        {"queue_items": lambda liste: liste[0].update(
            {"image_format": "gif", "target_file_name": liste[0]["target_file_name"].replace(".png", ".gif")})},
        {"bcc": None, "proto": r"^Proto-Queue-Bildbeleg proto-hn-[0-9a-f]{20} ist ungueltig\.$"},
    ),
    (
        "proto_queue_item_zieldatei",
        {"queue_items": _setze("0.target_file_name", "anders.png")},
        {"bcc": None, "proto": r"^Proto-Queue-Bildbeleg proto-hn-[0-9a-f]{20} ist ungueltig\.$"},
    ),
    (
        "queue_item_quellbeleg_gross",
        {"queue_items": lambda liste: liste[0].__setitem__("source_ref", liste[0]["source_ref"].upper())},
        {"bcc": r"^Queue-Bildbeleg bcc-hn-[0-9a-f]{16} ist ungueltig\.$", "proto": None},
    ),
    (
        "queue_item_inspektionsdatum",
        {"queue_items": _setze("0.inspection_date", "28.07.2026")},
        {"bcc": r"^Queue-Bild bcc-hn-[0-9a-f]{16} besitzt kein gueltiges Inspektionsdatum\.$",
         "proto": None},
    ),
    (
        "queue_vorhersage_fehlt",
        {"queue_items": _setze("0.predictions", [])},
        {"bcc": r"^Queue-Bild bcc-hn-[0-9a-f]{16} besitzt keine vollstaendige Modellvorhersage\.$",
         "proto": None},
    ),
    (
        "queue_vorhersage_fremdes_feld",
        {"queue_items": _dazu("0.predictions.0")},
        {"bcc": r"^Queue-Vorhersage bcc-hn-[0-9a-f]{16} hat fehlende oder fremde Felder\.$",
         "proto": None},
    ),
    *[
        (
            f"queue_vorhersage_ungueltig_{name}",
            {"queue_items": _setze(f"0.predictions.0.{feld}", wert)},
            {"bcc": r"^Queue-Vorhersage bcc-hn-[0-9a-f]{16} ist ungueltig\.$", "proto": None},
        )
        for name, feld, wert in (
            ("fremdes_modell", "model_id", "anderes-modell"),
            ("kein_bool", "predicted_bcc", 1),
            ("anzahl_bool", "bcc_detection_count", True),
            ("anzahl_negativ", "bcc_detection_count", -1),
            ("konfidenz_zu_gross", "max_bcc_confidence", 1.5),
            ("konfidenz_text", "max_bcc_confidence", "0.5"),
            ("trigger_ohne_box", "bcc_detection_count", 0),
        )
    ],
    (
        "queue_vorhersage_anzahl_kommazahl",
        {"queue_items": _setze("0.predictions.0.bcc_detection_count", 1.5)},
        {"bcc": r"^Queue-Vorhersage bcc-hn-[0-9a-f]{16} ist ungueltig\.$", "proto": None},
    ),
    (
        "queue_ohne_bcc_trigger",
        {"queue_items": _setze("0.predictions.0.predicted_bcc", False)},
        {"bcc": r"^Queue-Bild bcc-hn-[0-9a-f]{16} ist nicht an einen BCC-Modelltrigger gebunden\.$",
         "proto": None},
    ),
    # --- Kandidatenliste -------------------------------------------------------------
    (
        "kandidaten_kein_array",
        {"kandidaten": lambda liste: Ersatz({"liste": liste})},
        {"bcc": genau("Der Queue-Kandidaten-Beleg ist kein JSON-Array."),
         "proto": genau("Der Queue-Kandidaten-Beleg ist kein JSON-Array.")},
    ),
    (
        "kandidat_fremdes_feld",
        {"kandidaten": _dazu("0")},
        {"bcc": genau("Queue-Kandidat hat fehlende oder fremde Felder."),
         "proto": genau("Queue-Kandidat hat fehlende oder fremde Felder.")},
    ),
    (
        "kandidat_doppelt",
        {"kandidaten": lambda liste: liste.append(dict(liste[0]))},
        {"bcc": genau("Der Kandidaten-Beleg enthaelt doppelte oder leere IDs."),
         "proto": genau("Der Kandidaten-Beleg enthaelt doppelte oder leere IDs.")},
    ),
    (
        "kandidat_falscher_status",
        {"kandidaten": _setze("0.status", "reviewed")},
        {"bcc": genau("Ein Queue-Kandidat besitzt einen ungueltigen Reviewstatus."),
         "proto": genau("Ein Queue-Kandidat besitzt einen ungueltigen Reviewstatus.")},
    ),
    (
        "kandidat_falsche_kategorie",
        {"kandidaten": _setze("0.category", "bcc_only")},
        {"bcc": genau("Ein Queue-Kandidat besitzt einen ungueltigen Reviewstatus."),
         "proto": genau("Ein Queue-Kandidat besitzt einen ungueltigen Reviewstatus.")},
    ),
    (
        "kandidat_fehlt",
        {"kandidaten": lambda liste: liste.pop()},
        {"bcc": genau("Queue-Manifest, Auswahlbeleg und Kandidatenliste sind unvollstaendig."),
         "proto": genau("Proto-Queue-Manifest, Auswahlbeleg und Kandidatenliste sind unvollstaendig.")},
    ),
    *[
        (
            f"queue_anzahl_{feld}",
            {"queue_manifest": _setze(feld, 2)},
            {"bcc": genau("Queue-Manifest, Auswahlbeleg und Kandidatenliste sind unvollstaendig."),
             "proto": genau("Proto-Queue-Manifest, Auswahlbeleg und Kandidatenliste sind unvollstaendig.")},
        )
        for feld in ("candidates_count", "images_count", "holdings_count")
    ],
    (
        "kandidat_falscher_bildpfad",
        {"kandidaten": _setze("0.frame_path", "anders.png")},
        {"bcc": r"^Queue-Kandidat bcc-hn-[0-9a-f]{16} passt nicht zum Auswahlbeleg\.$",
         "proto": r"^Queue-Kandidat proto-hn-[0-9a-f]{20} passt nicht zum Auswahlbeleg\.$"},
    ),
    (
        "kandidat_falscher_quellhash",
        {"kandidaten": _setze("0.source_sha256", "e" * 64)},
        {"bcc": r"^Queue-Kandidat bcc-hn-[0-9a-f]{16} passt nicht zum Auswahlbeleg\.$",
         "proto": r"^Queue-Kandidat proto-hn-[0-9a-f]{20} passt nicht zum Auswahlbeleg\.$"},
    ),
    (
        "queue_bildhash_fehlt",
        {"queue_manifest": lambda q: (q["hashes"].pop(sorted(k for k in q["hashes"] if k.startswith("images/"))[0]), q.__setitem__("hashes_count", len(q["hashes"])))},
        {"bcc": r"^Queue-Bildhash images/img_[0-9a-f]{64}\.png hat fehlende oder fremde Felder\.$",
         "proto": r"^Queue-Bildhash images/img_[0-9a-f]{64}\.png hat fehlende oder fremde Felder\.$"},
    ),
    (
        "queue_bildhash_falsch",
        {"queue_manifest": lambda q: next(v for k, v in sorted(q["hashes"].items()) if k.startswith("images/")).__setitem__("size_bytes", 5)},
        {"bcc": r"^Queue-Hashliste bindet bcc-hn-[0-9a-f]{16} nicht bytegenau\.$",
         "proto": r"^Queue-Hashliste bindet proto-hn-[0-9a-f]{20} nicht bytegenau\.$"},
    ),
    (
        "queue_hashliste_mit_zusatz",
        {"queue_manifest": lambda q: (q["hashes"].__setitem__("images/extra.png", {"sha256": "f" * 64, "size_bytes": 2000}), q.__setitem__("hashes_count", len(q["hashes"])))},
        {"bcc": genau("Queue-Kandidaten und Queue-Hashliste sind nicht deckungsgleich."),
         "proto": genau("Queue-Kandidaten und Queue-Hashliste sind nicht deckungsgleich.")},
    ),
    # --- Review ----------------------------------------------------------------------
    (
        "review_fremdes_feld",
        {"review": _dazu("")},
        {"bcc": genau("Review-Beleg hat fehlende oder fremde Felder."),
         "proto": genau("Review-Beleg hat fehlende oder fremde Felder.")},
    ),
    *[
        (
            f"review_bindung_{name}",
            haken,
            {"bcc": genau("Review, Queue und Klassenkarte sind nicht fest verbunden."),
             "proto": genau("Review, Proto-Queue und Klassenkarte sind nicht fest verbunden.")},
        )
        for name, haken in (
            ("schema", {"review": _setze("schema_version", "2.0")}),
            ("zweck", {"review": _setze("purpose", "bcc_release_holdout_review")}),
            ("bindungszweck", {"semantic": _setze("review.purpose", "fremd")}),
            ("queue_id", {"review": _setze("queue_id", "1" * 64)}),
            ("queue_manifest", {"review": _setze("queue_manifest_sha256", "1" * 64)}),
            ("kandidaten", {"review": _setze("candidates_sha256", "1" * 64)}),
            ("klassenkarte", {"review": _setze("class_map_sha256", "1" * 64)}),
            ("ohne_pruefer", {"review": _setze("reviewer", "  ")}),
        )
    ],
    (
        "review_unvollstaendig",
        {"review": lambda r: r["decisions"].pop(next(iter(r["decisions"])))},
        {"bcc": genau("Das Review ist nicht vollstaendig oder enthaelt fremde Bild-IDs."),
         "proto": genau("Das Review ist nicht vollstaendig oder enthaelt fremde Bild-IDs.")},
    ),
    (
        "review_fremde_bild_id",
        {"review": lambda r: r["decisions"].__setitem__("fremd", dict(next(iter(r["decisions"].values()))))},
        {"bcc": genau("Das Review ist nicht vollstaendig oder enthaelt fremde Bild-IDs."),
         "proto": genau("Das Review ist nicht vollstaendig oder enthaelt fremde Bild-IDs.")},
    ),
    (
        "review_entscheidung_fremdes_feld",
        {"review": lambda r: next(iter(r["decisions"].values())).__setitem__("fremd", 1)},
        {"bcc": r"^Review-Entscheidung bcc-hn-[0-9a-f]{16} hat fehlende oder fremde Felder\.$",
         "proto": r"^Review-Entscheidung proto-hn-[0-9a-f]{20} hat fehlende oder fremde Felder\.$"},
    ),
    (
        "review_altes_holdout_urteil",
        {"review": _nicht_klassenfrei},
        {"bcc": r"^Review-Entscheidung bcc-hn-[0-9a-f]{16} ist nicht erlaubt\.$",
         "proto": r"^Review-Entscheidung proto-hn-[0-9a-f]{20} ist nicht erlaubt\.$"},
    ),
    (
        "review_kommentar_kein_text",
        {"review": lambda r: next(iter(r["decisions"].values())).__setitem__("comment", 5)},
        {"bcc": r"^Review-Entscheidung bcc-hn-[0-9a-f]{16} ist nicht erlaubt\.$",
         "proto": r"^Review-Entscheidung proto-hn-[0-9a-f]{20} ist nicht erlaubt\.$"},
    ),
    (
        "review_zeit_ohne_z",
        {"review": lambda r: next(iter(r["decisions"].values())).__setitem__("reviewed_at_utc", "2026-07-28T12:30:00")},
        {"bcc": r"^Review-Entscheidung bcc-hn-[0-9a-f]{16} ist nicht erlaubt\.$",
         "proto": r"^Review-Entscheidung proto-hn-[0-9a-f]{20} ist nicht erlaubt\.$"},
    ),
    (
        "review_anzahlen_kein_objekt",
        {"semantic": _setze("review.decision_counts", [])},
        {"bcc": genau("Der Negativsatz besitzt ungueltige Review-Anzahlen."),
         "proto": genau("Der Proto-Satz besitzt ungueltige Review-Anzahlen.")},
    ),
    (
        "review_anzahlen_fremde_entscheidung",
        {"semantic": _dazu("review.decision_counts", "negative", 0)},
        {"bcc": genau("Der Negativsatz besitzt ungueltige Review-Anzahlen."),
         "proto": genau("Der Proto-Satz besitzt ungueltige Review-Anzahlen.")},
    ),
    (
        "review_anzahl_keine_zahl",
        {"semantic": _setze("review.decision_counts.all_classes_clear", "3")},
        {"bcc": genau("Review-Anzahl all_classes_clear ist keine gueltige Anzahl."),
         "proto": genau("Review-Anzahl all_classes_clear ist keine gueltige Anzahl.")},
    ),
    (
        "review_anzahl_falsch",
        {"semantic": _setze("review.decision_counts.exclude_uncertain", 1)},
        {"bcc": genau("Review-Anzahlen und Negativsatz widersprechen sich."),
         "proto": genau("Review-Anzahlen und Proto-Negativsatz widersprechen sich.")},
    ),
    (
        "reviewte_bilder_falsch",
        {"semantic": _setze("review.reviewed_images", 4)},
        {"bcc": genau("Review-Anzahlen und Negativsatz widersprechen sich."),
         "proto": genau("Review-Anzahlen und Proto-Negativsatz widersprechen sich.")},
    ),
    # --- Bildbelege des Satzes ---------------------------------------------------------
    (
        "keine_bilder",
        {"semantic": _setze("images", [])},
        {"bcc": genau("Der Negativsatz enthaelt keine freigegebenen Bilder."),
         "proto": genau("Der Proto-Negativsatz enthaelt keine freigegebenen Bilder.")},
    ),
    (
        "bildanzahl_falsch",
        {"manifest": _setze("images_count", 2)},
        {"bcc": genau("Die Bild-/Haltungsanzahl im Negativsatz ist falsch."),
         "proto": genau("Die Bild-/Haltungsanzahl im Proto-Negativsatz ist falsch.")},
    ),
    (
        "haltungsanzahl_falsch",
        {"manifest": _setze("holdings_count", 4)},
        {"bcc": genau("Die Bild-/Haltungsanzahl im Negativsatz ist falsch."),
         "proto": genau("Die Bild-/Haltungsanzahl im Proto-Negativsatz ist falsch.")},
    ),
    (
        "bild_fremdes_feld",
        {"bilder": _dazu("0")},
        {"bcc": genau("Negativsatz-Bild hat fehlende oder fremde Felder."),
         "proto": genau("Proto-Negativsatz-Bild hat fehlende oder fremde Felder.")},
    ),
    (
        "bild_hash_kein_sha",
        {"bilder": _setze("0.image_sha256", "kurz")},
        {"bcc": genau("Negativsatz-Bildhash ist kein gueltiger SHA-256."),
         "proto": genau("Proto-Negativbildhash ist kein gueltiger SHA-256.")},
    ),
    (
        "bild_fremde_physische_haltung",
        {"bilder": _setze("0.physical_holding_key", "900|901")},
        {"bcc": genau("Haltung und physische Haltung im Negativsatz widersprechen sich."),
         "proto": genau("Haltung und physische Haltung im Proto-Satz widersprechen sich.")},
    ),
    (
        "bild_haltung_nicht_normiert",
        {"bilder": _setze("0.holding_key", "06.100-200")},
        {"bcc": genau("Negativsatz besitzt keine normalisierte Haltungsidentitaet: 06.100-200"),
         "proto": genau("Negativsatz besitzt keine normalisierte Haltungsidentitaet: 06.100-200")},
    ),
    (
        "bild_haltung_ohne_identitaet",
        {"bilder": _setze("0.holding_key", "unbekannt")},
        {"bcc": genau("Negativsatz besitzt keine normalisierte Haltungsidentitaet: unbekannt"),
         "proto": genau("Negativsatz besitzt keine normalisierte Haltungsidentitaet: unbekannt")},
    ),
    *[
        (
            f"bild_nicht_klassenfrei_gebunden_{name}",
            {"bilder": haken},
            {"bcc": genau("Negativsatz-Bild ist nicht als klassenfreies Trainingsbild gebunden."),
             "proto": genau("Proto-Negativbild ist nicht als klassenfreies Trainingsbild gebunden.")},
        )
        for name, haken in (
            ("falsche_id", _setze("0.id", "neg-0")),
            ("falscher_dateiname", _setze("0.file_name", "bild.png")),
            ("falsches_format", _setze("0.image_format", "gif")),
            ("falscher_split_test", _setze("0.split", "test")),
            ("falscher_split_val", _setze("0.split", "val")),
            ("entscheidung", _setze("0.review_decision", "mapped_object_visible")),
            ("fremdes_review_item", _setze("0.review_item_id", "fremd")),
        )
    ],
    (
        "bild_doppelt",
        {"bilder": _doppelte_bildzeile},
        {"bcc": genau("Negativsatz enthaelt doppelte Bilder oder Haltungen."),
         "proto": genau("Proto-Negativsatz enthaelt doppelte Bilder oder Haltungen.")},
    ),
    (
        "proto_bild_gleiche_haltung_mit_praefix",
        {"haltungen": ("100-200", "06.100-200", "500-600")},
        {"bcc": None, "proto": genau("Proto-Negativsatz enthaelt doppelte Bilder oder Haltungen.")},
    ),
    (
        "fehlende_bilddatei",
        {"dateien": _entferne_erstes_bild},
        {"bcc": r"^Gebundenes Negativbild fehlt: images/img_[0-9a-f]{64}\.png$",
         "proto": r"^Gebundenes Proto-Negativbild fehlt: images/img_[0-9a-f]{64}\.png$"},
    ),
    (
        "bildgroesse_keine_zahl",
        {"bilder": _setze("0.size_bytes", None)},
        {"bcc": genau("Negativbild-Groesse ist keine gueltige Anzahl."),
         "proto": genau("Proto-Negativbild-Groesse ist keine gueltige Anzahl.")},
    ),
    (
        "bildgroesse_falsch",
        {"bilder": lambda liste: liste[0].__setitem__("size_bytes", liste[0]["size_bytes"] - 1)},
        {"bcc": genau("Negativbild passt nicht zum semantischen Bildbeleg."),
         "proto": genau("Proto-Negativbild passt nicht zum semantischen Bildbeleg.")},
    ),
    (
        # Dateiname und Satz-Hashliste stimmen, aber der Inhalt ist nicht das gebundene Bild.
        "bildinhalt_passt_nicht_zum_bildhash",
        {"dateien": _tausche_bildinhalt},
        {"bcc": genau("Negativbild passt nicht zum semantischen Bildbeleg."),
         "proto": genau("Proto-Negativbild passt nicht zum semantischen Bildbeleg.")},
    ),
    (
        "bildsignatur_passt_nicht_zum_format",
        {"bilddaten": lambda index, daten: b"GIF89a" + daten[6:] if index == 0 else daten},
        {"bcc": genau("Negativbild passt nicht zum semantischen Bildbeleg."),
         "proto": genau("Proto-Negativbild passt nicht zum semantischen Bildbeleg.")},
    ),
    (
        "bild_widerspricht_queue_quelle",
        {"bilder": _setze("0.source_ref", "9" * 64)},
        {"bcc": genau("Negativbild und Queue widersprechen sich bei source_ref."), "proto": None},
    ),
    (
        "bild_widerspricht_queue_datum",
        {"bilder": _setze("0.inspection_date", "2026-07-29")},
        {"bcc": genau("Negativbild und Queue widersprechen sich bei inspection_date."), "proto": None},
    ),
    (
        "bild_widerspricht_queue_haltung",
        {"bilder": lambda liste: liste[0].update({"holding_key": "900-901", "physical_holding_key": "900|901"})},
        {"bcc": genau("Negativbild und Queue widersprechen sich bei holding_key."),
         "proto": genau("Proto-Negativbild und Queue widersprechen sich bei holding_key.")},
    ),
    (
        "bild_gegenrichtung_der_queue",
        {"bilder": lambda liste: liste[0].update({"holding_key": "200-100"})},
        {"bcc": genau("Negativbild und Queue widersprechen sich bei holding_key."),
         "proto": genau("Proto-Negativbild und Queue widersprechen sich bei holding_key.")},
    ),
    (
        "proto_bild_widerspricht_queue_quelle",
        {"bilder": _setze("0.quelle", "pdf")},
        {"bcc": None, "proto": genau("Proto-Negativbild und Queue widersprechen sich bei quelle.")},
    ),
    # --- Vollstaendigkeit und Split -----------------------------------------------------
    (
        "zusaetzliche_bilddatei",
        {"dateien": lambda staging: shutil.copyfile(
            sorted((staging / "images").iterdir())[0], staging / "images" / "extra.png")},
        {"bcc": genau("Bilder, Hashliste und semantischer Negativsatz-Beleg sind nicht deckungsgleich."),
         "proto": genau("Bilder, Hashliste und semantischer Proto-Beleg sind nicht deckungsgleich.")},
    ),
    (
        "klassenfreies_bild_fehlt_im_satz",
        {"ohne_bild": (0,)},
        {"bcc": genau("Der Negativsatz muss exakt alle klassenfreien Review-Entscheidungen enthalten."),
         "proto": genau("Der Proto-Satz muss exakt alle klassenfreien Review-Entscheidungen enthalten.")},
    ),
    (
        "manipulierter_split",
        {"bilder": lambda liste: [
            eintrag.__setitem__("split", "train" if eintrag["split"] == "validation" else "validation")
            for eintrag in liste]},
        {"bcc": genau("Der Negativsatz besitzt einen manipulierten Split."),
         "proto": genau("Der Proto-Negativsatz besitzt einen manipulierten Split.")},
    ),
    (
        "splitregel_fremdes_feld",
        {"semantic": _dazu("split_rule")},
        {"bcc": genau("Negativsatz-Splitregel hat fehlende oder fremde Felder."),
         "proto": genau("Proto-Splitregel hat fehlende oder fremde Felder.")},
    ),
    (
        "splitregel_kein_objekt",
        {"semantic": _setze("split_rule", "stable_rank_v1")},
        {"bcc": genau("Negativsatz-Splitregel hat fehlende oder fremde Felder."),
         "proto": genau("Die Proto-Splitregel fehlt.")},
    ),
    (
        "splitregel_unbekannt",
        {"semantic": _setze("split_rule.name", "zufall")},
        {"bcc": genau("Die Negativsatz-Splitregel ist ungueltig."),
         "proto": genau("Unbekannte Proto-Splitregel.")},
    ),
    *[
        (
            f"splitregel_ungueltig_{name}",
            {"semantic": haken},
            {"bcc": genau("Die Negativsatz-Splitregel ist ungueltig."),
             "proto": genau("Die Proto-Splitregel ist ungueltig.")},
        )
        for name, haken in (
            ("salz", _setze("split_rule.salt", "split-v1")),
            ("mehrere_bilder", _setze("split_rule.one_image_per_physical_holding", False)),
            ("validierungsanzahl", _setze("split_rule.validation_count", 2)),
            ("trainingsanzahl", _setze("split_rule.train_count", 3)),
        )
    ],
    (
        "splitregel_anzahl_keine_zahl",
        {"semantic": _setze("split_rule.validation_count", True)},
        {"bcc": genau("validation_count der Negativsatz-Splitregel ist keine gueltige Anzahl."),
         "proto": genau("validation_count ist keine gueltige Anzahl.")},
    ),
]


def _faelle_fuer(variante: str):
    for kennung, argumente, meldungen in FAELLE:
        meldung = meldungen.get(variante)
        if meldung is not None:
            yield pytest.param(argumente, meldung, id=f"{variante}-{kennung}")


def _baue(wurzel: Path, variante: str, argumente: dict[str, Any]) -> Path:
    argumente = dict(argumente)
    haltungen = argumente.pop("haltungen", HALTUNGEN)
    return baue_satz(wurzel, variante, haltungen, **argumente)


@pytest.mark.parametrize(("argumente", "meldung"), list(_faelle_fuer("bcc")))
def test_bcc_satz_lehnt_manipulation_mit_heutiger_meldung_ab(
    wurzel: Path, argumente: dict[str, Any], meldung: str
) -> None:
    satz = _baue(wurzel, "bcc", argumente)
    with pytest.raises(ValueError, match=meldung):
        pruefe(wurzel, satz)


@pytest.mark.parametrize(("argumente", "meldung"), list(_faelle_fuer("proto")))
def test_proto_satz_lehnt_manipulation_mit_heutiger_meldung_ab(
    wurzel: Path, argumente: dict[str, Any], meldung: str
) -> None:
    satz = _baue(wurzel, "proto", argumente)
    with pytest.raises(ValueError, match=meldung):
        pruefe(wurzel, satz)


# ---------------------------------------------------------------------------
# Proto-Ausnahmen: nur vollzaehlig begruendete Luecken sind erlaubt
# ---------------------------------------------------------------------------


def _proto_mit_luecke(wurzel: Path, haltungen: tuple[str, ...], ausschluss: Callable[[dict, list], None]) -> Path:
    items: list[dict] = []
    return baue_satz(
        wurzel,
        "proto",
        haltungen,
        ohne_bild=(0,),
        queue_items=lambda liste: items.extend(liste),
        semantic=lambda semantik, _w: ausschluss(semantik, items),
    )


PROTO_VOLLZAEHLIG = genau("Der Proto-Satz muss exakt alle klassenfreien Review-Entscheidungen enthalten.")


@pytest.mark.parametrize(
    ("haltungen", "ausschluss", "meldung"),
    [
        pytest.param(
            ("unbekannt", "300-400", "500-600"),
            lambda s, items: s.__setitem__("excluded_not_normalizable", None),
            PROTO_VOLLZAEHLIG,
            id="ausschlussliste_kein_array",
        ),
        pytest.param(
            ("unbekannt", "300-400", "500-600"),
            lambda s, items: s.__setitem__("excluded_eval_protected", {}),
            PROTO_VOLLZAEHLIG,
            id="evalliste_kein_array",
        ),
        pytest.param(
            ("unbekannt", "300-400", "500-600"),
            lambda s, items: None,
            PROTO_VOLLZAEHLIG,
            id="luecke_ohne_begruendung",
        ),
        pytest.param(
            ("unbekannt", "300-400", "500-600"),
            lambda s, items: s.__setitem__(
                "excluded_not_normalizable", [items[0]["item_id"], "proto-hn-fremd"]
            ),
            PROTO_VOLLZAEHLIG,
            id="fremde_ausnahme",
        ),
        pytest.param(
            ("100-200", "300-400", "500-600"),
            lambda s, items: s.__setitem__("excluded_not_normalizable", [items[0]["item_id"]]),
            genau("Ein ausgeschlossenes Proto-Bild besitzt eine belastbare Haltung oder fehlt im Review."),
            id="normierbare_haltung_als_nicht_normierbar",
        ),
        pytest.param(
            ("100-200", "300-400", "500-600"),
            lambda s, items: s.__setitem__("excluded_eval_protected", [items[0]["item_id"]]),
            genau("Ein eval-ausgeschlossenes Proto-Bild ist nicht im geschuetzten Bestand."),
            id="eval_ausnahme_ohne_schutz",
        ),
        pytest.param(
            ("unbekannt", "300-400", "500-600"),
            lambda s, items: s.__setitem__("excluded_eval_protected", [items[0]["item_id"]]),
            genau("Ein eval-ausgeschlossenes Proto-Bild ist nicht im geschuetzten Bestand."),
            id="eval_ausnahme_ohne_haltung",
        ),
    ],
)
def test_proto_ausnahmen_muessen_begruendet_sein(
    wurzel: Path, haltungen: tuple[str, ...], ausschluss: Callable, meldung: str
) -> None:
    satz = _proto_mit_luecke(wurzel, haltungen, ausschluss)
    with pytest.raises(ValueError, match=meldung):
        pruefe(wurzel, satz)


def test_proto_eval_ausnahme_nur_mit_passendem_schutzbestand(wurzel: Path) -> None:
    """Eine Eval-Kandidatenliste einer anderen Haltung begruendet nichts."""
    _eval_kandidaten(wurzel, "700-800")
    _test_bericht(wurzel, "900-901")
    satz = _proto_mit_luecke(
        wurzel,
        HALTUNGEN,
        lambda s, items: s.__setitem__("excluded_eval_protected", [items[0]["item_id"]]),
    )
    with pytest.raises(
        ValueError,
        match=genau("Ein eval-ausgeschlossenes Proto-Bild ist nicht im geschuetzten Bestand."),
    ):
        pruefe(wurzel, satz)


# ---------------------------------------------------------------------------
# Entscheid 30.09.2026 (1): Satzbild und Ausnahmeliste ueberschneiden sich nie
# ---------------------------------------------------------------------------


def _ueberschneidung(auch_luecke: bool, feld: str = "excluded_eval_protected") -> Callable[[Path], Path]:
    """Baut den Satz aus scratchpad/ueberlappung.py (AP10-Befund 1) synthetisch nach.

    Das Bild der eval-geschuetzten Haltung ``500-600`` steht im Satz und zugleich
    auf der Ausnahmeliste. Mit ``auch_luecke`` fehlt zusaetzlich das nicht
    normierbare Bild, sodass die Ausnahmepruefung ueberhaupt anlaeuft.
    """

    def baue(wurzel: Path) -> Path:
        _eval_kandidaten(wurzel, "500-600")
        items: list[dict] = []

        def ausnahmen(semantik: dict, _w: Path) -> None:
            if auch_luecke:
                semantik["excluded_not_normalizable"] = [items[0]["item_id"]]
            semantik[feld] = [items[2]["item_id"]]

        return baue_satz(
            wurzel,
            "proto",
            ("unbekannt", "300-400", "500-600") if auch_luecke else HALTUNGEN,
            ohne_bild=(0,) if auch_luecke else (),
            queue_items=lambda liste: items.extend(liste),
            semantic=ausnahmen,
        )

    return baue


def _ueberschneidung_meldung(grund: str) -> str:
    return (
        r"^Proto-Bild proto-hn-[0-9a-f]+ \(Haltung 500-600\) steht im Satz und ist zugleich als "
        + re.escape(grund)
        + r" ausgeschlossen\.$"
    )


@pytest.mark.parametrize(
    ("baue", "meldung"),
    [
        pytest.param(
            _ueberschneidung(auch_luecke=True),
            _ueberschneidung_meldung("eval-geschuetzt"),
            id="befund_ueberlappung_py",
        ),
        pytest.param(
            _ueberschneidung(auch_luecke=False),
            _ueberschneidung_meldung("eval-geschuetzt"),
            id="ohne_luecke_eval",
        ),
        pytest.param(
            _ueberschneidung(auch_luecke=False, feld="excluded_not_normalizable"),
            _ueberschneidung_meldung("ohne belastbare Haltung"),
            id="ohne_luecke_nicht_normierbar",
        ),
    ],
)
def test_proto_bild_im_satz_und_auf_ausnahmeliste_wird_abgelehnt(
    wurzel: Path, baue: Callable[[Path], Path], meldung: str
) -> None:
    satz = baue(wurzel)
    with pytest.raises(ValueError, match=meldung):
        pruefe(wurzel, satz)


# ---------------------------------------------------------------------------
# Proto-Gold-Ausrichtung
# ---------------------------------------------------------------------------


def _andere_rolle(physisch: str) -> str:
    rolle = AUDIT.split_role("haltung:" + physisch.replace("|", "-"))
    return "val" if rolle == "train" else "train"


def _mit_regel(basis: Callable, eingriff: Callable[[dict], Any]) -> Callable:
    def haken(semantik: dict, wurzel: Path) -> None:
        basis(semantik, wurzel)
        eingriff(semantik["split_rule"])

    return haken


def _falsches_ziel() -> str:
    return "validation" if AUDIT.split_role("haltung:500-600") == "train" else "train"


@pytest.mark.parametrize(
    ("haken", "meldung"),
    [
        pytest.param(
            lambda w: _mit_regel(
                _gold_ausrichtung(w, "500|600"), lambda r: r.__setitem__("gold_alignments", None)
            ),
            genau("Die Gold-Ausrichtung der Proto-Splitregel fehlt."),
            id="ausrichtung_kein_array",
        ),
        pytest.param(
            lambda w: _mit_regel(
                _gold_ausrichtung(w, "500|600"),
                lambda r: r["gold_alignments"][0].__setitem__("fremd", 1),
            ),
            genau("Gold-Ausrichtung hat fehlende oder fremde Felder."),
            id="ausrichtung_fremdes_feld",
        ),
        pytest.param(
            lambda w: _mit_regel(_gold_ausrichtung(w, "500|600"), lambda r: r.pop("gold_alignments")),
            genau("Proto-Splitregel (gold-aligned) hat fehlende oder fremde Felder."),
            id="ausrichtung_fehlt_ganz",
        ),
        pytest.param(
            lambda w: _mit_regel(
                _gold_ausrichtung(w, "500|600"),
                lambda r: r["gold_alignments"][0].__setitem__("physical_holding_key", "900|901"),
            ),
            genau("Gold-Ausrichtung verweist auf eine fremde Haltung."),
            id="fremde_haltung",
        ),
        pytest.param(
            lambda w: _gold_ausrichtung(w, "500|600", rolle=_andere_rolle("500|600")),
            genau("Gold-Ausrichtung widerspricht dem aktuellen Gold-Split."),
            id="falsche_gold_rolle",
        ),
        pytest.param(
            lambda w: _gold_ausrichtung(w, "300|400"),
            genau("Gold-Ausrichtung widerspricht dem aktuellen Gold-Split."),
            id="haltung_ohne_gold",
        ),
        pytest.param(
            lambda w: _gold_ausrichtung(w, "500|600", ziel=_falsches_ziel()),
            genau("Gold-Ausrichtung verwendet eine falsche Zielrolle."),
            id="falsche_zielrolle",
        ),
        pytest.param(
            lambda w: _mit_regel(
                _gold_ausrichtung(w, "500|600"),
                lambda r: r.__setitem__("validation_count", r["validation_count"] + 1),
            ),
            genau("Die Proto-Splitregel ist ungueltig."),
            id="ausgerichtete_anzahl_falsch",
        ),
    ],
)
def test_proto_gold_ausrichtung_lehnt_manipulation_ab(wurzel: Path, haken: Callable, meldung: str) -> None:
    _gold_samples(wurzel, "500-600")
    satz = baue_satz(wurzel, "proto", semantic=haken(wurzel))
    with pytest.raises(ValueError, match=meldung):
        pruefe(wurzel, satz)


def test_bcc_kennt_keine_gold_ausrichtung(wurzel: Path) -> None:
    _gold_samples(wurzel, "500-600")
    satz = baue_satz(wurzel, "bcc", semantic=_gold_ausrichtung(wurzel, "500|600"))
    with pytest.raises(ValueError, match=genau("Negativsatz-Splitregel hat fehlende oder fremde Felder.")):
        pruefe(wurzel, satz)


# ---------------------------------------------------------------------------
# Entscheid 30.09.2026 (2): Gold-Testhaltungen stehen in keinem Split
# ---------------------------------------------------------------------------


def _gold_bericht(wurzel: Path, *, samples: list[dict] | None = None, gruppen: list[dict] | None = None) -> None:
    ordner = wurzel / "training" / "reports"
    ordner.mkdir(parents=True, exist_ok=True)
    bericht: dict[str, Any] = {"split": {"gruppen": gruppen or []}}
    if samples is not None:
        bericht["samples"] = samples
    _schreibe_json(ordner / "gold_stock_audit_20260101_000000_000.json", bericht)


def _gold_test_meldung(haltung: str) -> str:
    return (
        r"^Proto-Negativbild proto-hn-[0-9a-f]+ stammt aus der eingefrorenen Gold-Testhaltung "
        + re.escape(haltung)
        + r" \(Split (train|validation)\); Gold-Testhaltungen duerfen in keinem Split stehen, "
        r"auch nicht in validation\.$"
    )


def _gold_test_gegenrichtung(wurzel: Path) -> Path:
    """Gold-Sample ``200-100`` hat die Rolle test; das Negativbild steht als ``100-200``."""
    assert AUDIT.split_role("haltung:200-100") == "test"
    _gold_samples(wurzel, "200-100")
    return baue_satz(wurzel, "proto")


def _gold_test_ausgerichtet(wurzel: Path) -> Path:
    """Heutiger Weg: Gold-Rolle test erzwingt validation ueber die Gold-Ausrichtung."""
    _gold_samples(wurzel, "200-100")
    return baue_satz(wurzel, "proto", semantic=_gold_ausrichtung(wurzel, "100|200", rolle="test"))


def _gold_test_audit_sample(wurzel: Path) -> Path:
    """Rolle test nur im Gold-Audit-Bericht (z. B. Haltungsverbund), nicht im Gold-Split."""
    assert AUDIT.split_role("haltung:300-400") != "test"
    _gold_bericht(wurzel, samples=[{"haltung_key": "300-400", "rolle": "test"}])
    return baue_satz(wurzel, "proto")


def _gold_test_audit_gruppe(wurzel: Path) -> Path:
    """Testgruppe des Gold-Audits in Gegenrichtung, das Bild steht trotzdem im Satz."""
    _gold_bericht(wurzel, gruppen=[{"gruppe": "haltung:600-500", "rolle": "test"}])
    return baue_satz(wurzel, "proto")


@pytest.mark.parametrize(
    ("baue", "haltung"),
    [
        pytest.param(_gold_test_gegenrichtung, "100-200", id="gold_split_gegenrichtung"),
        pytest.param(_gold_test_ausgerichtet, "100-200", id="gold_ausrichtung_test_nach_validation"),
        pytest.param(_gold_test_audit_sample, "300-400", id="gold_audit_sample"),
        pytest.param(_gold_test_audit_gruppe, "500-600", id="gold_audit_gruppe_gegenrichtung"),
    ],
)
def test_proto_negativbild_aus_gold_testhaltung_wird_abgelehnt(
    wurzel: Path, baue: Callable[[Path], Path], haltung: str
) -> None:
    satz = baue(wurzel)
    with pytest.raises(ValueError, match=_gold_test_meldung(haltung)):
        pruefe(wurzel, satz)


def test_proto_gold_train_und_val_haltungen_bleiben_erlaubt(wurzel: Path) -> None:
    """Nur die Rolle test sperrt; train- und val-Haltungen im Gold bleiben erlaubt."""
    assert AUDIT.split_role("haltung:300-400") == "val"
    assert AUDIT.split_role("haltung:500-600") == "train"
    _gold_samples(wurzel, "300-400", "500-600")
    _gold_bericht(
        wurzel,
        samples=[
            {"haltung_key": "300-400", "rolle": "val"},
            {"haltung_key": "500-600", "rolle": "train"},
        ],
    )
    bilder, _ = pruefe(wurzel, baue_satz(wurzel, "proto"))
    assert len(bilder) == 3


def test_bcc_bleibt_ohne_gold_testsperre_im_leser(wurzel: Path) -> None:
    """BCC entfernt Audit-Testhaltungen in der Ableitung; der Leser bleibt unveraendert."""
    _gold_samples(wurzel, "200-100")
    bilder, _ = pruefe(wurzel, baue_satz(wurzel, "bcc"))
    assert len(bilder) == 3


# ---------------------------------------------------------------------------
# Klassenkarte: tiefe Pruefung gegen eine synthetische aktive Karte
# ---------------------------------------------------------------------------


def _karte(eingriff: Callable[[dict], Any]) -> bytes:
    karte = json.loads(KLASSENKARTE.read_bytes().decode("utf-8-sig"))
    eingriff(karte)
    return (json.dumps(karte, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def _tausche_letzte_klassen(karte: dict) -> None:
    klassen = karte["classes"]
    namen = sorted(klassen, key=klassen.get)
    klassen[namen[13]], klassen[namen[14]] = 14, 13


def _erste_klasse(karte: dict) -> str:
    return sorted(karte["classes"], key=karte["classes"].get)[0]


@pytest.mark.parametrize(
    ("eingriff", "meldung"),
    [
        pytest.param(
            lambda k: k.__setitem__("fremd", 1),
            "Klassenkarten-Beleg hat fehlende oder fremde Felder.",
            id="fremdes_feld",
        ),
        pytest.param(
            lambda k: k.__setitem__("version", 2),
            "Der Negativsatz braucht die Detect-Klassenkarte v3.",
            id="version_2",
        ),
        pytest.param(
            lambda k: k.__setitem__("version", True),
            "Der Negativsatz braucht die Detect-Klassenkarte v3.",
            id="version_bool",
        ),
        pytest.param(
            lambda k: k["classes"].pop(_erste_klasse(k)),
            "Der Negativsatz braucht exakt 15 Detect-Klassen.",
            id="14_klassen",
        ),
        pytest.param(
            lambda k: k["classes"].__setitem__(_erste_klasse(k), "0"),
            "Der Klassenkarten-Beleg enthaelt ungueltige Klassen.",
            id="id_als_text",
        ),
        pytest.param(
            lambda k: k["classes"].__setitem__(_erste_klasse(k), 1),
            "Der Klassenkarten-Beleg enthaelt ungueltige Klassen.",
            id="doppelte_id",
        ),
        pytest.param(
            lambda k: k["classes"].__setitem__(_erste_klasse(k), 99),
            "Die gebundene Klassenkarte ist nicht die freigegebene BCC-Karte.",
            id="id_ausserhalb",
        ),
        pytest.param(
            _tausche_letzte_klassen,
            "Die gebundene Klassenkarte ist nicht die freigegebene BCC-Karte.",
            id="bcc_nicht_an_14",
        ),
        pytest.param(
            lambda k: k.__setitem__("vsa_manifest_hash", "kurz"),
            "VSA-Manifest-Hash in der Klassenkarte ist kein gueltiger SHA-256.",
            id="vsa_kein_sha",
        ),
        pytest.param(
            lambda k: k.__setitem__("vsa_manifest_hash", "a" * 64),
            "Die Klassenkarte passt nicht zum aktiven VSA-Manifest.",
            id="vsa_fremd",
        ),
    ],
)
@pytest.mark.parametrize("variante", ["bcc", "proto"])
def test_klassenkarte_wird_tief_geprueft(
    wurzel: Path, variante: str, eingriff: Callable[[dict], Any], meldung: str
) -> None:
    karte_bytes = _karte(eingriff)
    aktive_karte = wurzel / "aktive_klassenkarte.json"
    aktive_karte.write_bytes(karte_bytes)
    satz = baue_satz(wurzel, variante, klassenkarte_bytes=karte_bytes)
    with mock.patch.object(AUDIT, "ACTIVE_CLASS_MAP_PATH", aktive_karte):
        with pytest.raises(ValueError, match=genau(meldung)):
            pruefe(wurzel, satz)


# ---------------------------------------------------------------------------
# Aenderung waehrend der Pruefung
# ---------------------------------------------------------------------------


def _aendere_beim_ersten_ergebnis(aenderung: Callable[[], None]):
    """Veraendert eine Datei, sobald das erste Ergebnisbild aufgebaut wird."""
    original = AUDIT._stored_path
    zustand = {"erledigt": False}

    def ersatz(knowledge_root: Path, path: Path) -> str:
        if not zustand["erledigt"]:
            zustand["erledigt"] = True
            aenderung()
        return original(knowledge_root, path)

    return mock.patch.object(AUDIT, "_stored_path", ersatz)


@pytest.mark.parametrize(
    ("variante", "ziel", "meldung"),
    [
        ("bcc", "manifest", genau("Das Negativsatz-Manifest wurde waehrend der Pruefung geaendert.")),
        ("proto", "manifest", genau("Das Proto-Manifest wurde waehrend der Pruefung geaendert.")),
        ("bcc", "review", genau("Negativsatz-Datei wurde waehrend der Pruefung geaendert: receipts/review.json")),
        ("proto", "review", genau("Proto-Satz-Datei wurde waehrend der Pruefung geaendert: receipts/review.json")),
        ("bcc", "klassenkarte", genau("Klassenkarte oder VSA-Manifest wurde waehrend der Pruefung geaendert.")),
        ("proto", "klassenkarte", genau("Klassenkarte oder VSA-Manifest wurde waehrend der Pruefung geaendert.")),
    ],
)
def test_aenderung_waehrend_der_pruefung_wird_erkannt(
    wurzel: Path, variante: str, ziel: str, meldung: str
) -> None:
    satz = baue_satz(wurzel, variante)
    aktive_karte = wurzel / "aktive_klassenkarte.json"
    aktive_karte.write_bytes(KLASSENKARTE.read_bytes())
    pfad = {
        "manifest": satz / "_manifest.json",
        "review": satz / "receipts" / "review.json",
        "klassenkarte": aktive_karte,
    }[ziel]
    with mock.patch.object(AUDIT, "ACTIVE_CLASS_MAP_PATH", aktive_karte):
        with _aendere_beim_ersten_ergebnis(lambda: pfad.write_bytes(pfad.read_bytes() + b" ")):
            with pytest.raises(ValueError, match=meldung):
                pruefe(wurzel, satz)


# ---------------------------------------------------------------------------
# Satz-Ordner und Zusammenfuehrung mehrerer Saetze
# ---------------------------------------------------------------------------


def test_satzordner_muss_direkt_im_satz_stamm_liegen(wurzel: Path) -> None:
    with pytest.raises(ValueError, match=r"^Der Negativsatz-Stamm fehlt: "):
        pruefe(wurzel, wurzel / "training" / "negatives" / "sets" / "bcc_hn_0")
    satz = baue_satz(wurzel, "bcc")
    verschachtelt = satz.parent / "unter" / satz.name
    verschachtelt.parent.mkdir()
    shutil.copytree(satz, verschachtelt)
    with pytest.raises(ValueError, match=r"^Ein expliziter Negativsatz muss direkt unter "):
        pruefe(wurzel, verschachtelt)
    with pytest.raises(ValueError, match=r"^Der explizite Negativsatz fehlt: "):
        pruefe(wurzel, satz.parent / "bcc_hn_fehlt")


@pytest.mark.parametrize("variante", ["bcc", "proto"])
def test_satzordner_braucht_exakte_struktur(wurzel: Path, variante: str) -> None:
    satz = baue_satz(wurzel, variante)
    (satz / "notiz.txt").write_text("x", encoding="utf-8")
    with pytest.raises(
        ValueError, match=genau("Der Negativsatz muss exakt _manifest.json, images und receipts enthalten.")
    ):
        pruefe(wurzel, satz)
    (satz / "notiz.txt").unlink()
    shutil.rmtree(satz / "receipts")
    (satz / "receipts").write_text("x", encoding="utf-8")
    with pytest.raises(ValueError, match=genau("Die Negativsatz-Struktur ist ungueltig.")):
        pruefe(wurzel, satz)


def test_gleiche_physische_haltung_in_zwei_saetzen_wird_abgelehnt(wurzel: Path) -> None:
    erster = baue_satz(wurzel, "bcc", ("100-200", "300-400"))
    zweiter = baue_satz(wurzel, "proto", ("200-100", "700-800"))
    _eval_kandidaten(wurzel, "900-901")
    with pytest.raises(ValueError, match=r"^Physische Haltung ist ueber mehrere Negativsaetze doppelt: 100\|200 "):
        AUDIT.read_training_negative_sources(wurzel, wurzel / "kein_pool", (erster, zweiter))


# ---------------------------------------------------------------------------
# Eval-Schutz der Satzbilder im Leser (wie der C#-Export, 01.10.2026)
# ---------------------------------------------------------------------------


def _lese_saetze(wurzel: Path, *saetze: Path):
    return AUDIT.read_training_negative_sources(wurzel, wurzel / "kein_pool", tuple(saetze))


@pytest.mark.parametrize("variante", ["bcc", "proto"])
@pytest.mark.parametrize("eval_haltung", ["100-200", "200-100", "06.200-06.100"])
def test_leser_lehnt_satz_mit_bild_aus_eval_haltung_ab(wurzel: Path, variante: str, eval_haltung: str) -> None:
    satz = baue_satz(wurzel, variante)
    _eval_kandidaten(wurzel, eval_haltung)
    with pytest.raises(ValueError) as fehler:
        _lese_saetze(wurzel, satz)
    text = str(fehler.value)
    assert "Eval-/Abnahme-Set" in text
    assert "100-200" in text
    assert satz.name in text
    assert "300-400" not in text and "500-600" not in text


def test_leser_nennt_alle_betroffenen_bilder_sichtbar(wurzel: Path) -> None:
    satz = baue_satz(wurzel, "bcc")
    ordner = wurzel / "eval_set" / "subsets" / "fixture"
    ordner.mkdir(parents=True)
    _schreibe_json(ordner / "_candidates.json", {"candidates": [{"haltung_key": "100-200"}, {"haltung_key": "400-300"}]})
    with pytest.raises(ValueError) as fehler:
        _lese_saetze(wurzel, satz)
    text = str(fehler.value)
    assert "100-200" in text and "300-400" in text and "500-600" not in text


@pytest.mark.parametrize("variante", ["bcc", "proto"])
def test_leser_nimmt_satz_ohne_eval_haltung_an(wurzel: Path, variante: str) -> None:
    satz = baue_satz(wurzel, variante)
    _eval_kandidaten(wurzel, "900-901")
    bilder, _ = _lese_saetze(wurzel, satz)
    assert len(bilder) == 3


def test_leser_wertet_nur_kandidatenlisten_nicht_dateinamen_aus(wurzel: Path) -> None:
    # Wie in C#: Haltungsschluessel kommen aus _candidates.json (haltung_key).
    satz = baue_satz(wurzel, "bcc")
    _eval_kandidaten(wurzel, "900-901")
    ordner = wurzel / "eval_set" / "subsets" / "fixture" / "images"
    ordner.mkdir(parents=True)
    (ordner / "100-200_12s_BCC_t+0.png").write_bytes(b"x")
    bilder, _ = _lese_saetze(wurzel, satz)
    assert len(bilder) == 3


def _kaputte_eval_quelle(wurzel: Path, art: str) -> None:
    ordner = wurzel / "eval_set" / "subsets" / "fixture"
    ordner.mkdir(parents=True, exist_ok=True)
    datei = ordner / "_candidates.json"
    if art == "kein_eval_set":
        shutil.rmtree(wurzel / "eval_set", ignore_errors=True)
    elif art == "keine_kandidatenliste":
        pass
    elif art == "kein_json":
        datei.write_text("{kaputt", encoding="utf-8")
    elif art == "kein_array":
        _schreibe_json(datei, {"candidates": "nein"})
    elif art == "leere_liste":
        _schreibe_json(datei, [])
    elif art == "eintrag_kein_objekt":
        _schreibe_json(datei, [{"haltung_key": "900-901"}, "x"])
    elif art == "haltung_fehlt":
        _schreibe_json(datei, [{"haltung_key": "900-901"}, {"frame_path": "a.png"}])
    elif art == "haltung_leer":
        _schreibe_json(datei, [{"haltung_key": "900-901"}, {"haltung_key": "  "}])
    else:
        raise AssertionError(art)


@pytest.mark.parametrize(
    "art",
    [
        "kein_eval_set",
        "keine_kandidatenliste",
        "kein_json",
        "kein_array",
        "leere_liste",
        "eintrag_kein_objekt",
        "haltung_fehlt",
        "haltung_leer",
    ],
)
def test_leser_stoppt_wenn_eval_schluesselquelle_unvollstaendig(wurzel: Path, art: str) -> None:
    satz = baue_satz(wurzel, "bcc")
    _kaputte_eval_quelle(wurzel, art)
    with pytest.raises(ValueError, match=r"^Der Eval-Schutz ist nicht vollstaendig lesbar"):
        _lese_saetze(wurzel, satz)


def test_leser_braucht_ohne_negativsaetze_keine_eval_schluessel(wurzel: Path) -> None:
    bilder, provenienz = _lese_saetze(wurzel)
    assert bilder == () and provenienz == ()


def test_leser_stoppt_wenn_nur_eine_von_mehreren_kandidatenlisten_leer_ist(wurzel: Path) -> None:
    # Wie C# (ReadHoldingKeysAsync): Jede leere Liste macht die Quelle unvollstaendig.
    satz = baue_satz(wurzel, "bcc")
    _eval_kandidaten(wurzel, "900-901")
    leer = wurzel / "eval_set" / "subsets" / "leer"
    leer.mkdir(parents=True)
    _schreibe_json(leer / "_candidates.json", {"candidates": []})
    with pytest.raises(ValueError, match=r"^Der Eval-Schutz ist nicht vollstaendig lesbar"):
        _lese_saetze(wurzel, satz)


def test_leser_nimmt_eval_wurzel_aus_der_konfigurierten_quelle(wurzel: Path) -> None:
    satz = baue_satz(wurzel, "bcc")
    anders = wurzel / "anderes_eval"
    ordner = anders / "subsets" / "fixture"
    ordner.mkdir(parents=True)
    _schreibe_json(ordner / "_candidates.json", [{"haltung_key": "200-100"}])
    # Standardwurzel <wurzel>/eval_set fehlt: mit eigener Wurzel wird sie nicht gebraucht.
    with pytest.raises(ValueError, match="Eval-/Abnahme-Set"):
        AUDIT.read_training_negative_sources(
            wurzel, wurzel / "kein_pool", (satz,), eval_root=anders
        )
    _schreibe_json(ordner / "_candidates.json", [{"haltung_key": "900-901"}])
    bilder, _ = AUDIT.read_training_negative_sources(
        wurzel, wurzel / "kein_pool", (satz,), eval_root=anders
    )
    assert len(bilder) == 3
