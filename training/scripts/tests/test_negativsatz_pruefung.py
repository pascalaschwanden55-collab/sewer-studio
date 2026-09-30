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


# ---------------------------------------------------------------------------
# Review
# ---------------------------------------------------------------------------


def _review_fall(entscheidungen: dict[str, str]) -> tuple[bytes, dict[str, Any], dict[str, Any]]:
    review = {
        "schema_version": "1.0",
        "purpose": "bcc_hard_negative_review",
        "queue_id": "1" * 64,
        "queue_manifest_sha256": "2" * 64,
        "candidates_sha256": "3" * 64,
        "class_map_sha256": "4" * 64,
        "reviewer": "Besitzer",
        "updated_at_utc": "2026-07-28T12:30:00Z",
        "decisions": {
            item_id: {"decision": wert, "comment": "", "reviewed_at_utc": "2026-07-28T12:30:00Z"}
            for item_id, wert in entscheidungen.items()
        },
    }
    anzahlen = {name: 0 for name in NP.NEGATIVE_REVIEW_DECISIONS}
    for wert in entscheidungen.values():
        anzahlen[wert] += 1
    bindung = {
        "purpose": "bcc_hard_negative_review",
        "reviewed_images": len(entscheidungen),
        "decision_counts": anzahlen,
    }
    return _bytes(review), bindung, {item_id: {} for item_id in entscheidungen}


def _pruefe_review(review_bytes: bytes, bindung: dict, kandidaten: dict, vertrag: NP.Satzvertrag) -> set[str]:
    return NP.pruefe_review(
        review_bytes,
        bindung,
        queue_id="1" * 64,
        queue_manifest_sha="2" * 64,
        candidates_sha="3" * 64,
        class_map_sha="4" * 64,
        candidates_by_id=kandidaten,
        vertrag=vertrag,
    )


@pytest.mark.parametrize("vertrag", VERTRAEGE)
def test_review_liefert_nur_klassenfreie_ids(vertrag: NP.Satzvertrag) -> None:
    review_bytes, bindung, kandidaten = _review_fall(
        {"a": "all_classes_clear", "b": "mapped_object_visible", "c": "exclude_uncertain"}
    )
    assert _pruefe_review(review_bytes, bindung, kandidaten, vertrag) == {"a"}


@pytest.mark.parametrize("vertrag", VERTRAEGE)
def test_review_anzahlen_muessen_stimmen(vertrag: NP.Satzvertrag) -> None:
    review_bytes, bindung, kandidaten = _review_fall({"a": "all_classes_clear"})
    bindung["decision_counts"]["all_classes_clear"] = 2
    with pytest.raises(ValueError) as fehler:
        _pruefe_review(review_bytes, bindung, kandidaten, vertrag)
    assert str(fehler.value) == vertrag.text("review_anzahlen_widerspruch")


def test_altes_holdout_urteil_negative_ist_kein_trainingsnegativ() -> None:
    review_bytes, bindung, kandidaten = _review_fall({"a": "all_classes_clear"})
    review = json.loads(review_bytes)
    review["decisions"]["a"]["decision"] = "negative"
    with pytest.raises(ValueError, match=r"^Review-Entscheidung a ist nicht erlaubt\.$"):
        _pruefe_review(_bytes(review), bindung, kandidaten, NP.BCC_VERTRAG)


# ---------------------------------------------------------------------------
# Bildbelege
# ---------------------------------------------------------------------------


def _bild(vertrag: NP.Satzvertrag, index: int, haltung: str, split: str = "train") -> dict[str, Any]:
    sha = _sha(f"bild-{index}".encode("utf-8"))
    bild = {
        "id": f"{vertrag.bild_id_praefix}{sha}",
        "file_name": f"img_{sha}.png",
        "image_sha256": sha,
        "size_bytes": 2048,
        "image_format": "png",
        "holding_key": haltung,
        "physical_holding_key": _physisch(haltung),
        "split": split,
        "review_item_id": f"item-{index}",
        "review_decision": "all_classes_clear",
    }
    if vertrag is NP.PROTO_VERTRAG:
        bild["quelle"] = "xtf"
    else:
        bild["source_ref"] = "5" * 64
        bild["inspection_date"] = "2026-07-28"
    return bild


@pytest.mark.parametrize("vertrag", VERTRAEGE)
def test_bildbeleg_wird_gesammelt(vertrag: NP.Satzvertrag) -> None:
    sammlung = NP.Bildsammlung.leer()
    beleg = NP.pruefe_bildbeleg(_bild(vertrag, 0, "100-200"), {"item-0"}, sammlung, _physisch, vertrag)
    assert beleg.relative_path == f"images/{beleg.file_name}"
    assert sammlung.split_by_physical == {"100|200": "train"}
    assert sammlung.review_ids == {"item-0"}


@pytest.mark.parametrize("vertrag", VERTRAEGE)
def test_bildbeleg_sperrt_gegenrichtung_derselben_haltung(vertrag: NP.Satzvertrag) -> None:
    sammlung = NP.Bildsammlung.leer()
    akzeptiert = {"item-0", "item-1"}
    NP.pruefe_bildbeleg(_bild(vertrag, 0, "100-200"), akzeptiert, sammlung, _physisch, vertrag)
    with pytest.raises(ValueError) as fehler:
        NP.pruefe_bildbeleg(_bild(vertrag, 1, "200-100"), akzeptiert, sammlung, _physisch, vertrag)
    assert str(fehler.value) == vertrag.text("bild_doppelt")


@pytest.mark.parametrize("vertrag", VERTRAEGE)
@pytest.mark.parametrize(
    ("eingriff", "schluessel"),
    [
        (lambda b: b.__setitem__("split", "test"), "bild_nicht_gebunden"),
        (lambda b: b.__setitem__("review_decision", "negative"), "bild_nicht_gebunden"),
        (lambda b: b.__setitem__("review_item_id", "nicht-akzeptiert"), "bild_nicht_gebunden"),
        (lambda b: b.__setitem__("physical_holding_key", "900|901"), "bild_haltung_widerspruch"),
    ],
)
def test_bildbeleg_lehnt_mit_variantentext_ab(vertrag: NP.Satzvertrag, eingriff, schluessel: str) -> None:
    bild = _bild(vertrag, 0, "100-200")
    eingriff(bild)
    with pytest.raises(ValueError) as fehler:
        NP.pruefe_bildbeleg(bild, {"item-0"}, NP.Bildsammlung.leer(), _physisch, vertrag)
    assert str(fehler.value) == vertrag.text(schluessel)


def test_bcc_bildbeleg_mit_proto_feldern_wird_abgelehnt() -> None:
    bild = _bild(NP.PROTO_VERTRAG, 0, "100-200")
    bild["id"] = bild["id"].replace("proto-neg-", "bcc-neg-")
    with pytest.raises(ValueError, match="^Negativsatz-Bild hat fehlende oder fremde Felder\\.$"):
        NP.pruefe_bildbeleg(bild, {"item-0"}, NP.Bildsammlung.leer(), _physisch, NP.BCC_VERTRAG)


@pytest.mark.parametrize(
    ("format_", "signatur", "erwartet"),
    [
        ("png", b"\x89PNG\r\n\x1a\n", True),
        ("jpg", b"\xff\xd8\xff\xe0\x00\x10JF", True),
        ("jpeg", b"\xff\xd8\xff\xe1\x00\x10Ex", True),
        ("png", b"\xff\xd8\xff\xe0\x00\x10JF", False),
        ("jpg", b"\x89PNG\r\n\x1a\n", False),
        ("gif", b"GIF89a\x00\x00", False),
    ],
)
def test_bildsignatur_passt_zum_format(format_: str, signatur: bytes, erwartet: bool) -> None:
    assert NP.hat_bildsignatur(format_, signatur) is erwartet


def test_proto_bild_vergleicht_queue_haltung_normalisiert() -> None:
    bild = _bild(NP.PROTO_VERTRAG, 0, "100-200")
    beleg = NP.pruefe_bildbeleg(bild, {"item-0"}, NP.Bildsammlung.leer(), _physisch, NP.PROTO_VERTRAG)
    queue_item = {"image_sha256": bild["image_sha256"], "size_bytes": 2048, "image_format": "png", "quelle": "xtf"}
    NP.pruefe_proto_bild_gegen_queue(beleg, {**queue_item, "holding_key": "06.100-200"}, lambda h: "100-200", NP.PROTO_VERTRAG)
    with pytest.raises(ValueError, match="widersprechen sich bei holding_key"):
        NP.pruefe_proto_bild_gegen_queue(beleg, {**queue_item, "holding_key": "x"}, lambda h: None, NP.PROTO_VERTRAG)


# ---------------------------------------------------------------------------
# Proto-Ausnahmen und Split
# ---------------------------------------------------------------------------


def test_proto_ausnahmen_verlangen_begruendung() -> None:
    queue = {"a": {"holding_key": "unbekannt"}, "b": {"holding_key": "500-600"}}

    def normiert(wert: Any) -> str | None:
        return None if wert == "unbekannt" else str(wert)

    NP.pruefe_proto_ausnahmen({"a"}, {"b"}, {"a", "b"}, queue, {"500|600"}, normiert, _physisch)
    with pytest.raises(ValueError, match="nicht im geschuetzten Bestand"):
        NP.pruefe_proto_ausnahmen(set(), {"b"}, {"b"}, queue, set(), normiert, _physisch)
    with pytest.raises(ValueError, match="belastbare Haltung"):
        NP.pruefe_proto_ausnahmen({"b"}, set(), {"b"}, queue, set(), normiert, _physisch)


def test_proto_ausnahmelisten_muessen_vollzaehlig_sein() -> None:
    semantik = {"excluded_not_normalizable": ["a"], "excluded_eval_protected": []}
    assert NP.pruefe_proto_ausnahmelisten(semantik, {"b"}, {"a", "b"}) == ({"a"}, set())
    with pytest.raises(ValueError, match="^Der Proto-Satz muss exakt"):
        NP.pruefe_proto_ausnahmelisten(semantik, {"b"}, {"a", "b", "c"})


def _sammlung(*haltungen: str) -> NP.Bildsammlung:
    physisch = [_physisch(haltung) for haltung in haltungen]
    splits, _ = NP.negative_split_map(physisch)
    return NP.Bildsammlung(set(), set(), physisch, dict(splits))


def _splitregel(sammlung: NP.Bildsammlung) -> dict[str, Any]:
    validierung = sum(1 for wert in sammlung.split_by_physical.values() if wert == "validation")
    return {
        "name": "stable_rank_v1",
        "salt": NP.NEGATIVE_SPLIT_SALT,
        "one_image_per_physical_holding": True,
        "validation_count": validierung,
        "train_count": len(sammlung.physical_keys) - validierung,
    }


def test_split_regel_ist_fuer_beide_varianten_stabil() -> None:
    sammlung = _sammlung("100-200", "300-400", "500-600")
    semantik = {"split_rule": _splitregel(sammlung)}
    assert NP.pruefe_bcc_split(semantik, sammlung, 3) == 1
    nie_geladen = lambda: pytest.fail("Gold-Rollen duerfen fuer stable_rank_v1 nicht geladen werden")  # noqa: E731
    assert NP.pruefe_proto_split(semantik, sammlung, 3, nie_geladen) == 1


def test_manipulierter_split_wird_in_beiden_varianten_erkannt() -> None:
    sammlung = _sammlung("100-200", "300-400", "500-600")
    semantik = {"split_rule": _splitregel(sammlung)}
    erste = next(iter(sammlung.split_by_physical))
    sammlung.split_by_physical[erste] = "train" if sammlung.split_by_physical[erste] == "validation" else "validation"
    with pytest.raises(ValueError, match="^Der Negativsatz besitzt einen manipulierten Split"):
        NP.pruefe_bcc_split(semantik, sammlung, 3)
    with pytest.raises(ValueError, match="^Der Proto-Negativsatz besitzt einen manipulierten Split"):
        NP.pruefe_proto_split(semantik, sammlung, 3, dict)


def test_negativbild_eintrag_und_provenienz_behalten_das_berichtsformat() -> None:
    karte = NP.Klassenkarte(3, "a" * 64, "b" * 64, ["x"])
    bindung = NP.Satzbindung("s" * 64, "m" * 64, "q" * 64, "k" * 64, "c" * 64, "r" * 64, karte)
    beleg = NP.pruefe_bildbeleg(
        _bild(NP.BCC_VERTRAG, 0, "100-200"), {"item-0"}, NP.Bildsammlung.leer(), _physisch, NP.BCC_VERTRAG
    )
    assert list(NP.negativbild_eintrag(beleg, "pfad", bindung)) == [
        "path", "sha256", "split", "source_type", "holding_key", "physical_holding_key",
        "set_id", "set_manifest_sha256", "queue_id", "queue_manifest_sha256",
        "candidates_sha256", "review_sha256", "class_map_version", "class_map_sha256",
        "vsa_manifest_hash", "review_item_id", "review_decision",
    ]
    assert list(NP.satz_provenienz(bindung, "stamm", 3, 1)) == [
        "set_id", "root_path", "manifest_sha256", "queue_id", "queue_manifest_sha256",
        "candidates_sha256", "review_sha256", "class_map_version", "class_map_sha256",
        "vsa_manifest_hash", "images", "train_images", "validation_images",
    ]
