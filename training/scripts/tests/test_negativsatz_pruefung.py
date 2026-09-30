"""Direkte Tests der reinen Pruefschritte in negativsatz_pruefung.py (AP10).

Die Schritte lesen keine Dateien; die Tests uebergeben Bytes und Dokumente
direkt. Das Zusammenspiel aller Schritte mit echten Satzordnern pruefen
test_negativsatz_vertrag.py und test_gold_stock_audit.py.
"""
from __future__ import annotations

import copy
import hashlib
import json
import sys
from pathlib import Path
from typing import Any

import pytest

SCRIPTS_DIR = Path(__file__).resolve().parents[1]
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))

import negativsatz_pruefung as NP  # noqa: E402


def _sha(daten: bytes) -> str:
    return hashlib.sha256(daten).hexdigest()


def _semantik(vertrag: NP.Satzvertrag) -> dict[str, Any]:
    semantik: dict[str, Any] = {
        feld: None for feld in vertrag.semantik_felder
    }
    semantik.update(
        {
            "schema_version": "1.0",
            "purpose": vertrag.satz_zweck,
            "pilot": vertrag.pilot,
            "role": "training_negative_set",
        }
    )
    return semantik


def _manifest(vertrag: NP.Satzvertrag, semantik: dict[str, Any] | None = None) -> dict[str, Any]:
    semantik = semantik if semantik is not None else _semantik(vertrag)
    return {
        "schema_version": "1.0",
        "purpose": vertrag.satz_zweck,
        "set_id": _sha(NP.canonical_json_bytes(semantik)),
        "pilot": vertrag.pilot,
        "role": "training_negative_set",
        "created_utc": "2026-07-28T13:00:00Z",
        "frozen": True,
        "dataset_status": "ready_for_training",
        "hash_algorithm": "sha256",
        "images_count": 0,
        "holdings_count": 0,
        "hashes_count": 0,
        "hashes": {},
        "semantic": semantik,
    }


def _bytes(dokument: Any) -> bytes:
    return json.dumps(dokument).encode("utf-8")


VERTRAEGE = [pytest.param(NP.BCC_VERTRAG, id="bcc"), pytest.param(NP.PROTO_VERTRAG, id="proto")]


def test_beide_vertraege_fuehren_dieselben_meldungsschluessel() -> None:
    assert set(NP.BCC_VERTRAG.texte) == set(NP.PROTO_VERTRAG.texte)


def test_proto_vertrag_unterscheidet_sich_nur_ausdruecklich() -> None:
    assert NP.PROTO_VERTRAG.semantik_felder - NP.BCC_VERTRAG.semantik_felder == {
        "excluded_not_normalizable",
        "excluded_eval_protected",
    }
    assert NP.BCC_VERTRAG.semantik_felder <= NP.PROTO_VERTRAG.semantik_felder
    assert (NP.BCC_VERTRAG.ordner_praefix, NP.PROTO_VERTRAG.ordner_praefix) == ("bcc_hn_", "proto_hn_")


@pytest.mark.parametrize("vertrag", VERTRAEGE)
def test_satzkopf_liefert_manifest_semantik_und_satz_id(vertrag: NP.Satzvertrag) -> None:
    manifest = _manifest(vertrag)
    kopf = NP.pruefe_satzkopf(_bytes(manifest), f"{vertrag.ordner_praefix}{manifest['set_id'][:12]}", vertrag)
    assert kopf.set_id == manifest["set_id"]
    assert kopf.semantic == manifest["semantic"]
    assert kopf.manifest == manifest


@pytest.mark.parametrize("vertrag", VERTRAEGE)
@pytest.mark.parametrize(
    ("eingriff", "schluessel"),
    [
        (lambda m: m.__setitem__("frozen", "true"), "nicht_eingefroren"),
        (lambda m: m.__setitem__("created_utc", None), "nicht_eingefroren"),
        (lambda m: m["semantic"].__setitem__("pilot", "fremd"), "semantik_widerspruch"),
        (lambda m: m.__setitem__("set_id", "0" * 64), "satz_id_passt_nicht"),
    ],
)
def test_satzkopf_lehnt_mit_variantentext_ab(vertrag: NP.Satzvertrag, eingriff, schluessel: str) -> None:
    manifest = _manifest(vertrag)
    ordner = f"{vertrag.ordner_praefix}{manifest['set_id'][:12]}"
    eingriff(manifest)
    with pytest.raises(ValueError) as fehler:
        NP.pruefe_satzkopf(_bytes(manifest), ordner, vertrag)
    assert str(fehler.value) == vertrag.text(schluessel)


def test_satzkopf_der_anderen_variante_wird_abgelehnt() -> None:
    manifest = _manifest(NP.PROTO_VERTRAG)
    with pytest.raises(ValueError, match="^Der Negativsatz ist nicht streng"):
        NP.pruefe_satzkopf(_bytes(manifest), "egal", NP.BCC_VERTRAG)


def _belege() -> tuple[dict[str, Any], dict[str, bytes]]:
    belege = {
        NP.RECEIPT_QUEUE_MANIFEST: b"queue",
        NP.RECEIPT_CANDIDATES: b"kandidaten",
        NP.RECEIPT_REVIEW: b"review",
        NP.RECEIPT_CLASS_MAP: b"karte",
    }
    semantik = {
        "queue": {
            "queue_id": "1" * 64,
            "queue_manifest_sha256": _sha(b"queue"),
            "queue_manifest_receipt_path": NP.RECEIPT_QUEUE_MANIFEST,
            "candidates_sha256": _sha(b"kandidaten").upper(),
            "candidates_receipt_path": NP.RECEIPT_CANDIDATES,
        },
        "review": {
            "purpose": "bcc_hard_negative_review",
            "review_sha256": _sha(b"review"),
            "receipt_path": NP.RECEIPT_REVIEW,
            "reviewed_images": 0,
            "decision_counts": {},
        },
        "class_map_receipt_path": NP.RECEIPT_CLASS_MAP,
    }
    return semantik, belege


@pytest.mark.parametrize("vertrag", VERTRAEGE)
def test_belegbindung_liefert_gebundene_belege(vertrag: NP.Satzvertrag) -> None:
    semantik, belege = _belege()
    bindung = NP.pruefe_belegbindung(semantik, belege, vertrag)
    assert bindung.class_map_bytes == b"karte"
    assert bindung.review_bytes == b"review"
    # Grossgeschriebene Hashes werden wie bisher normalisiert.
    assert bindung.candidates_sha == _sha(b"kandidaten")


@pytest.mark.parametrize("vertrag", VERTRAEGE)
@pytest.mark.parametrize(
    ("eingriff", "schluessel"),
    [
        (lambda s, b: s["queue"].__setitem__("candidates_receipt_path", "receipts/x.json"), "belegpfade"),
        (lambda s, b: b.__setitem__(NP.RECEIPT_QUEUE_MANIFEST, b"anders"), "queue_manifest_beleg"),
        (lambda s, b: b.__setitem__(NP.RECEIPT_CANDIDATES, b"anders"), "kandidaten_beleg"),
        (lambda s, b: b.__setitem__(NP.RECEIPT_REVIEW, b"anders"), "review_beleg"),
    ],
)
def test_belegbindung_lehnt_mit_variantentext_ab(vertrag: NP.Satzvertrag, eingriff, schluessel: str) -> None:
    semantik, belege = _belege()
    eingriff(semantik, belege)
    with pytest.raises(ValueError) as fehler:
        NP.pruefe_belegbindung(semantik, belege, vertrag)
    assert str(fehler.value) == vertrag.text(schluessel)


def test_belegbindung_bleibt_ohne_nebenwirkung() -> None:
    semantik, belege = _belege()
    vorher = (copy.deepcopy(semantik), dict(belege))
    NP.pruefe_belegbindung(semantik, belege, NP.BCC_VERTRAG)
    assert (semantik, belege) == vorher


# ---------------------------------------------------------------------------
# Queue
# ---------------------------------------------------------------------------


def _physisch(haltung: str) -> str:
    links, rechts = haltung.split("-", maxsplit=1)
    return "|".join(sorted((links, rechts)))


def _bcc_item(index: int, haltung: str) -> dict[str, Any]:
    sha = _sha(f"bild-{index}".encode("utf-8"))
    return {
        "id": f"bcc-hn-{sha[:16]}",
        "image_sha256": sha,
        "holding_key": haltung,
        "physical_holding_key": _physisch(haltung),
        "source_ref": _sha(f"quelle-{index}".encode("utf-8")),
        "inspection_date": "2026-07-28",
        "size_bytes": 2048,
        "image_format": "png",
        "predictions": [
            {"model_id": "m", "predicted_bcc": True, "bcc_detection_count": 1, "max_bcc_confidence": None}
        ],
    }


def test_bcc_queue_bilder_nutzen_die_uebergebene_haltungsidentitaet() -> None:
    items = [_bcc_item(0, "100-200"), _bcc_item(1, "300-400")]
    ergebnis = NP.pruefe_bcc_queue_bilder(items, {"m"}, _physisch)
    assert set(ergebnis) == {items[0]["id"], items[1]["id"]}

    gegenrichtung = [_bcc_item(0, "100-200"), _bcc_item(1, "200-100")]
    with pytest.raises(ValueError, match=r"^Queue-Bildbeleg bcc-hn-[0-9a-f]{16} ist ungueltig\.$"):
        NP.pruefe_bcc_queue_bilder(gegenrichtung, {"m"}, _physisch)


def test_bcc_queue_bild_braucht_einen_modelltrigger() -> None:
    item = _bcc_item(0, "100-200")
    item["predictions"][0]["predicted_bcc"] = False
    with pytest.raises(ValueError, match="nicht an einen BCC-Modelltrigger gebunden"):
        NP.pruefe_bcc_queue_bilder([item], {"m"}, _physisch)


def _proto_item(index: int, haltung: str) -> dict[str, Any]:
    sha = _sha(f"proto-{index}".encode("utf-8"))
    return {
        "item_id": f"proto-hn-{sha[:20]}",
        "image_sha256": sha,
        "holding_key": haltung,
        "code": "BCDYA",
        "gruppe": "rohranfang_ende",
        "quelle": "xtf",
        "quell_datei": "q.xtf",
        "leitungsinspektion": False,
        "size_bytes": 2048,
        "image_format": "png",
        "target_file_name": f"img_{sha}.png",
    }


def test_proto_queue_bilder_sperren_gegenrichtung_ueber_proto_schluessel() -> None:
    ok = NP.pruefe_proto_queue_bilder([_proto_item(0, "100-200"), _proto_item(1, "300-400")], _physisch)
    assert len(ok) == 2
    with pytest.raises(ValueError, match=r"^Proto-Queue-Bildbeleg proto-hn-[0-9a-f]{20} ist ungueltig\.$"):
        NP.pruefe_proto_queue_bilder([_proto_item(0, "100-200"), _proto_item(1, "200-100")], _physisch)


@pytest.mark.parametrize(
    "regel",
    [
        {"one_image_per_physical_holding": True, "model_involved": True},
        {"one_image_per_physical_holding": True},
        {"one_image_per_physical_holding": False, "model_involved": False},
        {"one_image_per_physical_holding": True, "model_involved": False, "requires_current_model_bcc_trigger": True},
    ],
)
def test_proto_auswahlregel_bleibt_modellfrei(regel: dict[str, Any]) -> None:
    NP.pruefe_proto_auswahlregel({"selection_rule": {"one_image_per_physical_holding": True, "model_involved": False}})
    with pytest.raises(ValueError, match="modellfrei"):
        NP.pruefe_proto_auswahlregel({"selection_rule": regel})


@pytest.mark.parametrize("vertrag", VERTRAEGE)
def test_queue_hashliste_bindet_kandidaten_bytegenau(vertrag: NP.Satzvertrag) -> None:
    kandidaten = b"[]"
    manifest = {
        "hashes": {"_candidates.json": {"sha256": _sha(kandidaten), "size_bytes": len(kandidaten)}},
        "hashes_count": 1,
    }
    assert NP.pruefe_queue_hashliste(manifest, kandidaten, _sha(kandidaten), vertrag) is manifest["hashes"]
    with pytest.raises(ValueError) as fehler:
        NP.pruefe_queue_hashliste(manifest, b"[ ]", _sha(kandidaten), vertrag)
    assert str(fehler.value) == vertrag.text("queue_kandidaten_bytegenau")


@pytest.mark.parametrize("vertrag", VERTRAEGE)
def test_kandidatenliste_muss_queue_decken(vertrag: NP.Satzvertrag) -> None:
    item = _bcc_item(0, "100-200")
    kandidat = {
        "id": item["id"],
        "frame_path": f"img_{item['image_sha256']}.png",
        "category": "all_class_background_review",
        "status": "pending_review",
        "source_sha256": item["image_sha256"],
    }
    queue_manifest = {"candidates_count": 1, "images_count": 1, "holdings_count": 1}
    hashes = {
        "_candidates.json": {},
        f"images/{kandidat['frame_path']}": {"sha256": item["image_sha256"], "size_bytes": 2048},
    }
    ergebnis = NP.pruefe_kandidaten(
        json.dumps([kandidat]).encode("utf-8"), queue_manifest, {item["id"]: item}, hashes, vertrag
    )
    assert ergebnis == {item["id"]: kandidat}
    with pytest.raises(ValueError) as fehler:
        NP.pruefe_kandidaten(b"[]", queue_manifest, {item["id"]: item}, hashes, vertrag)
    assert str(fehler.value) == vertrag.text("kandidatenliste_unvollstaendig")
