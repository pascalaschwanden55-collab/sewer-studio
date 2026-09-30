"""Reine Pruefschritte fuer veroeffentlichte Negativsaetze (bcc_hn_* und proto_hn_*).

Dieses Modul liest keine Dateien. Es bekommt bereits gelesene Bytes und
Dokumente und liefert ein kleines, geprueftes Ergebnis oder bricht mit
``ValueError`` ab. Dateizugriff, Pfadschutz, aktive Klassenkarte und die
Abschlusspruefung "waehrend der Pruefung unveraendert" bleiben in
``gold_stock_audit.py``; dort werden die Schritte auch in fester Reihenfolge
zusammengesetzt. Erst wenn alle Schritte bestanden sind, gilt ein Satz als
freigegeben.

Pruefgruppen:
  JSON-Struktur      strikte Bytes, exakte Felder, SHA-256, Anzahlen
  Rollen/Versionen   Zweck, Pilot, Rolle, Schema, eingefroren, Satz-ID, Ordner
  Beleg-Bindung      feste Belegpfade und Belegbytes gegen gebundene Hashes
  Queue              Queue-Manifest, Auswahl, Queue-Bilder, Kandidatenliste

Die beiden Varianten unterscheiden sich in Zweck, Pilot, Feldern und in den
Meldungstexten. Diese Unterschiede stehen ausdruecklich in ``BCC_VERTRAG`` und
``PROTO_VERTRAG``; gemeinsame Funktionen pruefen nur wirklich gleiche Regeln.
"""
from __future__ import annotations

import hashlib
import json
import math
from dataclasses import dataclass
from datetime import datetime
from typing import Any, Callable, Mapping, NamedTuple

# ---------------------------------------------------------------------------
# Vertragskonstanten
# ---------------------------------------------------------------------------

MIN_TRAINING_NEGATIVE_BYTES = 1024
NEGATIVE_SET_SCHEMA_VERSION = "1.0"
NEGATIVE_SET_PURPOSE = "bcc_reviewed_negative_set"
NEGATIVE_SET_ROLE = "training_negative_set"
NEGATIVE_SET_PILOT = "BCC_bogen"
NEGATIVE_QUEUE_PURPOSE = "bcc_hard_negative_review_queue"
NEGATIVE_QUEUE_ROLE = "training_candidate_review"
NEGATIVE_REVIEW_PURPOSE = "bcc_hard_negative_review"
NEGATIVE_SPLIT_SALT = "bcc-hard-negative-split-v1"
NEGATIVE_REVIEW_DECISIONS = (
    "all_classes_clear",
    "mapped_object_visible",
    "exclude_uncertain",
)
# Protokollbasierte Negativsaetze (Geschwister-Vertrag ohne Modellbindung).
PROTO_SET_PURPOSE = "proto_reviewed_negative_set"
PROTO_QUEUE_PURPOSE = "proto_hard_negative_review_queue"
PROTO_PILOT = "protokoll_negative"
PROTO_ITEM_ID_PREFIX = "proto-hn-"
PROTO_IMAGE_ID_PREFIX = "proto-neg-"

RECEIPT_REVIEW = "receipts/review.json"
RECEIPT_QUEUE_MANIFEST = "receipts/queue_manifest.json"
RECEIPT_CANDIDATES = "receipts/queue_candidates.json"
RECEIPT_CLASS_MAP = "receipts/class_map.json"

SATZ_MANIFEST_FELDER = frozenset(
    {
        "schema_version",
        "purpose",
        "set_id",
        "pilot",
        "role",
        "created_utc",
        "frozen",
        "dataset_status",
        "hash_algorithm",
        "images_count",
        "holdings_count",
        "hashes_count",
        "hashes",
        "semantic",
    }
)
SATZ_SEMANTIK_FELDER = frozenset(
    {
        "schema_version",
        "purpose",
        "pilot",
        "role",
        "queue",
        "review",
        "class_map_version",
        "class_map_sha256",
        "class_map_receipt_path",
        "vsa_manifest_hash",
        "class_names",
        "protected_sets",
        "protection_snapshot",
        "split_rule",
        "images",
    }
)
QUEUE_BINDUNG_FELDER = frozenset(
    {
        "queue_id",
        "queue_manifest_sha256",
        "queue_manifest_receipt_path",
        "candidates_sha256",
        "candidates_receipt_path",
    }
)
REVIEW_BINDUNG_FELDER = frozenset(
    {
        "purpose",
        "review_sha256",
        "receipt_path",
        "reviewed_images",
        "decision_counts",
    }
)
PROTO_QUEUE_SEMANTIK_FELDER = frozenset(
    {
        "schema_version",
        "purpose",
        "pilot",
        "role",
        "class_map_version",
        "class_map_sha256",
        "vsa_manifest_hash",
        "class_names",
        "protected_sets",
        "protection_snapshot",
        "selection_rule",
        "sources",
        "items",
    }
)
# Nur die BCC-Queue bindet die ausloesenden Auswahlmodelle.
BCC_QUEUE_SEMANTIK_FELDER = PROTO_QUEUE_SEMANTIK_FELDER | {"model_scope"}


# ---------------------------------------------------------------------------
# JSON-Struktur: gemeinsame Bausteine
# ---------------------------------------------------------------------------


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def strict_json_bytes(data: bytes, label: str) -> Any:
    def reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"{label} enthaelt ein doppeltes Feld: {key}")
            result[key] = value
        return result

    try:
        return json.loads(
            data.decode("utf-8-sig"),
            object_pairs_hook=reject_duplicates,
        )
    except (UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{label} ist kein sicher lesbares JSON.") from error


def require_exact_fields(
    value: Any,
    expected: set[str] | frozenset[str],
    label: str,
) -> Mapping[str, Any]:
    if not isinstance(value, dict) or set(value) != expected:
        raise ValueError(f"{label} hat fehlende oder fremde Felder.")
    return value


def require_sha256(value: Any, label: str) -> str:
    text = str(value or "").strip().casefold()
    if len(text) != 64 or any(character not in "0123456789abcdef" for character in text):
        raise ValueError(f"{label} ist kein gueltiger SHA-256.")
    return text


def require_count(value: Any, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise ValueError(f"{label} ist keine gueltige Anzahl.")
    return value


# ---------------------------------------------------------------------------
# Varianten: alle Unterschiede zwischen BCC- und Proto-Satz an einer Stelle
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class Satzvertrag:
    """Feste Werte und Meldungstexte einer Negativsatz-Variante.

    Die Texte sind die bisherigen Meldungen und Feldbezeichnungen; sie
    unterscheiden sich zwischen den Varianten teilweise unregelmaessig und
    werden deshalb einzeln gefuehrt statt aus einem Namen zusammengesetzt.
    """

    satz_zweck: str
    queue_zweck: str
    pilot: str
    ordner_praefix: str
    semantik_felder: frozenset[str]
    queue_semantik_felder: frozenset[str]
    texte: Mapping[str, str]

    def text(self, schluessel: str, **werte: Any) -> str:
        return self.texte[schluessel].format(**werte)


BCC_VERTRAG = Satzvertrag(
    satz_zweck=NEGATIVE_SET_PURPOSE,
    queue_zweck=NEGATIVE_QUEUE_PURPOSE,
    pilot=NEGATIVE_SET_PILOT,
    ordner_praefix="bcc_hn_",
    semantik_felder=SATZ_SEMANTIK_FELDER,
    queue_semantik_felder=BCC_QUEUE_SEMANTIK_FELDER,
    texte={
        "nicht_eingefroren": "Der Negativsatz ist nicht streng eingefroren und trainingsbereit.",
        "semantik_beleg": "Semantischer Negativsatz-Beleg",
        "semantik_widerspruch": "Manifest und semantischer Negativsatz-Beleg widersprechen sich.",
        "satz_id": "Negativsatz-ID",
        "satz_id_passt_nicht": "Die Negativsatz-ID passt nicht zum semantischen Beleg.",
        "ordner_passt_nicht": "Der Negativsatz-Ordner passt nicht zur Negativsatz-ID.",
        "queue_bindung": "Queue-Bindung",
        "review_bindung": "Review-Bindung",
        "belegpfade": "Der Negativsatz verweist nicht auf die festen Belegpfade.",
        "queue_manifest_beleg": "Der Queue-Manifest-Beleg passt nicht zum Negativsatz.",
        "kandidaten_beleg": "Der Kandidaten-Beleg passt nicht zum Negativsatz.",
        "review_beleg": "Der Review-Beleg passt nicht zum Negativsatz.",
        "queue_id": "Queue-ID",
        "queue_semantik_beleg": "Semantischer Queue-Beleg",
        "queue_nicht_gebunden": "Der Queue-Beleg ist nicht fest an die Queue-ID gebunden.",
        "queue_widerspruch": "Queue und Negativsatz widersprechen sich bei {feld}.",
        "queue_hashliste": "Der Queue-Beleg besitzt keine gueltige Hashliste.",
        "queue_kandidaten_bytegenau": "Die Queue bindet den Kandidaten-Beleg nicht bytegenau.",
        "kandidatenliste_unvollstaendig": (
            "Queue-Manifest, Auswahlbeleg und Kandidatenliste sind unvollstaendig."
        ),
    },
)

PROTO_VERTRAG = Satzvertrag(
    satz_zweck=PROTO_SET_PURPOSE,
    queue_zweck=PROTO_QUEUE_PURPOSE,
    pilot=PROTO_PILOT,
    ordner_praefix="proto_hn_",
    # Proto-Saetze begruenden ausgeschlossene klassenfreie Bilder ausdruecklich.
    semantik_felder=SATZ_SEMANTIK_FELDER
    | {"excluded_not_normalizable", "excluded_eval_protected"},
    queue_semantik_felder=PROTO_QUEUE_SEMANTIK_FELDER,
    texte={
        "nicht_eingefroren": "Der Proto-Negativsatz ist nicht streng eingefroren und trainingsbereit.",
        "semantik_beleg": "Semantischer Proto-Negativsatz-Beleg",
        "semantik_widerspruch": "Manifest und semantischer Proto-Beleg widersprechen sich.",
        "satz_id": "Proto-Negativsatz-ID",
        "satz_id_passt_nicht": "Die Proto-Negativsatz-ID passt nicht zum semantischen Beleg.",
        "ordner_passt_nicht": "Der Proto-Negativsatz-Ordner passt nicht zur Satz-ID.",
        "queue_bindung": "Proto-Queue-Bindung",
        "review_bindung": "Proto-Review-Bindung",
        "belegpfade": "Der Proto-Negativsatz verweist nicht auf die festen Belegpfade.",
        "queue_manifest_beleg": "Der Queue-Manifest-Beleg passt nicht zum Proto-Satz.",
        "kandidaten_beleg": "Der Kandidaten-Beleg passt nicht zum Proto-Satz.",
        "review_beleg": "Der Review-Beleg passt nicht zum Proto-Satz.",
        "queue_id": "Proto-Queue-ID",
        "queue_semantik_beleg": "Semantischer Proto-Queue-Beleg",
        "queue_nicht_gebunden": "Der Proto-Queue-Beleg ist nicht fest an die Queue-ID gebunden.",
        "queue_widerspruch": "Proto-Queue und Negativsatz widersprechen sich bei {feld}.",
        "queue_hashliste": "Der Proto-Queue-Beleg besitzt keine gueltige Hashliste.",
        "queue_kandidaten_bytegenau": "Die Proto-Queue bindet den Kandidaten-Beleg nicht bytegenau.",
        "kandidatenliste_unvollstaendig": (
            "Proto-Queue-Manifest, Auswahlbeleg und Kandidatenliste sind unvollstaendig."
        ),
    },
)


# ---------------------------------------------------------------------------
# Rollen/Versionen: Satz-Manifest, semantischer Beleg, Satz-ID und Ordner
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class Satzkopf:
    manifest: Mapping[str, Any]
    semantic: Mapping[str, Any]
    set_id: str


def pruefe_satzkopf(
    manifest_bytes: bytes,
    ordnername: str,
    vertrag: Satzvertrag,
) -> Satzkopf:
    """Prueft Manifestfelder, eingefrorenen Zustand, Semantik, Satz-ID und Ordnername."""
    manifest = require_exact_fields(
        strict_json_bytes(manifest_bytes, "Negativsatz-Manifest"),
        SATZ_MANIFEST_FELDER,
        "Negativsatz-Manifest",
    )
    if (
        manifest.get("schema_version") != NEGATIVE_SET_SCHEMA_VERSION
        or manifest.get("purpose") != vertrag.satz_zweck
        or manifest.get("pilot") != vertrag.pilot
        or manifest.get("role") != NEGATIVE_SET_ROLE
        or manifest.get("frozen") is not True
        or manifest.get("dataset_status") != "ready_for_training"
        or manifest.get("hash_algorithm") != "sha256"
        or not isinstance(manifest.get("created_utc"), str)
        or not str(manifest.get("created_utc")).endswith("Z")
    ):
        raise ValueError(vertrag.text("nicht_eingefroren"))

    semantic = require_exact_fields(
        manifest.get("semantic"),
        vertrag.semantik_felder,
        vertrag.text("semantik_beleg"),
    )
    if (
        semantic.get("schema_version") != NEGATIVE_SET_SCHEMA_VERSION
        or semantic.get("purpose") != vertrag.satz_zweck
        or semantic.get("pilot") != vertrag.pilot
        or semantic.get("role") != NEGATIVE_SET_ROLE
    ):
        raise ValueError(vertrag.text("semantik_widerspruch"))
    set_id = require_sha256(manifest.get("set_id"), vertrag.text("satz_id"))
    if hashlib.sha256(canonical_json_bytes(semantic)).hexdigest() != set_id:
        raise ValueError(vertrag.text("satz_id_passt_nicht"))
    if ordnername != f"{vertrag.ordner_praefix}{set_id[:12]}":
        raise ValueError(vertrag.text("ordner_passt_nicht"))
    return Satzkopf(manifest=manifest, semantic=semantic, set_id=set_id)


# ---------------------------------------------------------------------------
# Beleg-Bindung: feste Belegpfade und Belegbytes gegen die gebundenen Hashes
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class Belegbindung:
    queue_binding: Mapping[str, Any]
    review_binding: Mapping[str, Any]
    queue_manifest_bytes: bytes
    candidates_bytes: bytes
    review_bytes: bytes
    class_map_bytes: bytes
    queue_manifest_sha: str
    candidates_sha: str
    review_sha: str


def pruefe_belegbindung(
    semantic: Mapping[str, Any],
    receipts: Mapping[str, bytes],
    vertrag: Satzvertrag,
) -> Belegbindung:
    """Bindet Queue-Manifest, Kandidatenliste und Review bytegenau an den Satz.

    ``receipts`` sind die bereits gegen die Satz-Hashliste geprueften Belege.
    Die Klassenkarte wird hier nur weitergereicht; ihre Pruefung braucht die
    aktive Karte und bleibt deshalb beim Aufrufer.
    """
    queue_binding = require_exact_fields(
        semantic.get("queue"),
        QUEUE_BINDUNG_FELDER,
        vertrag.text("queue_bindung"),
    )
    review_binding = require_exact_fields(
        semantic.get("review"),
        REVIEW_BINDUNG_FELDER,
        vertrag.text("review_bindung"),
    )
    if (
        queue_binding.get("queue_manifest_receipt_path") != RECEIPT_QUEUE_MANIFEST
        or queue_binding.get("candidates_receipt_path") != RECEIPT_CANDIDATES
        or review_binding.get("receipt_path") != RECEIPT_REVIEW
        or semantic.get("class_map_receipt_path") != RECEIPT_CLASS_MAP
    ):
        raise ValueError(vertrag.text("belegpfade"))

    queue_manifest_bytes = receipts[RECEIPT_QUEUE_MANIFEST]
    candidates_bytes = receipts[RECEIPT_CANDIDATES]
    review_bytes = receipts[RECEIPT_REVIEW]
    class_map_bytes = receipts[RECEIPT_CLASS_MAP]
    queue_manifest_sha = require_sha256(
        queue_binding.get("queue_manifest_sha256"),
        "Queue-Manifest-Hash",
    )
    candidates_sha = require_sha256(
        queue_binding.get("candidates_sha256"),
        "Kandidaten-Hash",
    )
    review_sha = require_sha256(
        review_binding.get("review_sha256"),
        "Review-Hash",
    )
    if hashlib.sha256(queue_manifest_bytes).hexdigest() != queue_manifest_sha:
        raise ValueError(vertrag.text("queue_manifest_beleg"))
    if hashlib.sha256(candidates_bytes).hexdigest() != candidates_sha:
        raise ValueError(vertrag.text("kandidaten_beleg"))
    if hashlib.sha256(review_bytes).hexdigest() != review_sha:
        raise ValueError(vertrag.text("review_beleg"))
    return Belegbindung(
        queue_binding=queue_binding,
        review_binding=review_binding,
        queue_manifest_bytes=queue_manifest_bytes,
        candidates_bytes=candidates_bytes,
        review_bytes=review_bytes,
        class_map_bytes=class_map_bytes,
        queue_manifest_sha=queue_manifest_sha,
        candidates_sha=candidates_sha,
        review_sha=review_sha,
    )


# ---------------------------------------------------------------------------
# Queue: Queue-Manifest, Auswahl, Queue-Bilder und Kandidatenliste
# ---------------------------------------------------------------------------

QUEUE_MANIFEST_FELDER = frozenset(
    {
        "schema_version",
        "purpose",
        "queue_id",
        "pilot",
        "role",
        "created_utc",
        "frozen",
        "dataset_status",
        "warning",
        "review_target",
        "class_map_version",
        "class_map_sha256",
        "vsa_manifest_hash",
        "class_names",
        "protected_sets",
        "protection_snapshot",
        "selection_rule",
        "sources",
        "candidates_count",
        "images_count",
        "holdings_count",
        "hash_algorithm",
        "hashes_count",
        "hashes",
        "semantic",
        "selection_receipt",
    }
)
HASHBELEG_FELDER = frozenset({"sha256", "size_bytes"})
KANDIDAT_FELDER = frozenset({"id", "frame_path", "category", "status", "source_sha256"})
BILDFORMATE = frozenset({"jpg", "jpeg", "png"})


class Klassenkarte(NamedTuple):
    """Bereits gegen die aktive Karte gepruefte Klassenkarten-Bindung."""

    version: int
    sha256: str
    vsa_hash: str
    namen: list[str]


@dataclass(frozen=True)
class Queuekopf:
    manifest: Mapping[str, Any]
    semantic: Mapping[str, Any]
    queue_id: str


def pruefe_queue_kopf(
    queue_manifest_bytes: bytes,
    queue_binding: Mapping[str, Any],
    semantic: Mapping[str, Any],
    karte: Klassenkarte,
    vertrag: Satzvertrag,
) -> Queuekopf:
    """Bindet das Queue-Manifest an Queue-ID, Klassenkarte und Schutzmengen des Satzes."""
    queue_manifest = require_exact_fields(
        strict_json_bytes(queue_manifest_bytes, "Queue-Manifest-Beleg"),
        QUEUE_MANIFEST_FELDER,
        "Queue-Manifest-Beleg",
    )
    queue_id = require_sha256(queue_binding.get("queue_id"), vertrag.text("queue_id"))
    queue_semantic = require_exact_fields(
        queue_manifest.get("semantic"),
        vertrag.queue_semantik_felder,
        vertrag.text("queue_semantik_beleg"),
    )
    if (
        queue_manifest.get("schema_version") != NEGATIVE_SET_SCHEMA_VERSION
        or queue_manifest.get("purpose") != vertrag.queue_zweck
        or queue_manifest.get("queue_id") != queue_id
        or queue_manifest.get("pilot") != vertrag.pilot
        or queue_manifest.get("role") != NEGATIVE_QUEUE_ROLE
        or queue_manifest.get("frozen") is not True
        or queue_manifest.get("hash_algorithm") != "sha256"
        or hashlib.sha256(canonical_json_bytes(queue_semantic)).hexdigest()
        != queue_id
        or queue_semantic.get("purpose") != vertrag.queue_zweck
        or queue_semantic.get("pilot") != vertrag.pilot
        or queue_semantic.get("role") != NEGATIVE_QUEUE_ROLE
    ):
        raise ValueError(vertrag.text("queue_nicht_gebunden"))
    for field, expected in (
        ("class_map_version", karte.version),
        ("class_map_sha256", karte.sha256),
        ("vsa_manifest_hash", karte.vsa_hash),
        ("class_names", karte.namen),
        ("protected_sets", semantic.get("protected_sets")),
        ("protection_snapshot", semantic.get("protection_snapshot")),
    ):
        if (
            queue_manifest.get(field) != expected
            or queue_semantic.get(field) != expected
        ):
            raise ValueError(vertrag.text("queue_widerspruch", feld=field))
    return Queuekopf(manifest=queue_manifest, semantic=queue_semantic, queue_id=queue_id)


def pruefe_proto_auswahlregel(queue_semantic: Mapping[str, Any]) -> None:
    """Proto-Auswahl: modellfrei und genau ein Bild je physischer Haltung."""
    selection_rule = queue_semantic.get("selection_rule")
    if (
        not isinstance(selection_rule, dict)
        or selection_rule.get("one_image_per_physical_holding") is not True
        or selection_rule.get("model_involved") is not False
        or selection_rule.get("requires_current_model_bcc_trigger") is True
    ):
        raise ValueError("Die Proto-Auswahlregel muss modellfrei und haltungseinheitlich sein.")


def pruefe_queue_hashliste(
    queue_manifest: Mapping[str, Any],
    candidates_bytes: bytes,
    candidates_sha: str,
    vertrag: Satzvertrag,
) -> Mapping[str, Any]:
    """Die Queue-Hashliste muss die Kandidatenliste bytegenau binden."""
    queue_hashes = queue_manifest.get("hashes")
    if not isinstance(queue_hashes, dict) or require_count(
        queue_manifest.get("hashes_count"),
        "Queue hashes_count",
    ) != len(queue_hashes):
        raise ValueError(vertrag.text("queue_hashliste"))
    candidate_hash_entry = require_exact_fields(
        queue_hashes.get("_candidates.json"),
        HASHBELEG_FELDER,
        "Queue-Kandidaten-Hash",
    )
    if (
        require_sha256(
            candidate_hash_entry.get("sha256"),
            "Queue-Kandidaten-Hash",
        )
        != candidates_sha
        or require_count(
            candidate_hash_entry.get("size_bytes"),
            "Queue-Kandidaten-Groesse",
        )
        != len(candidates_bytes)
    ):
        raise ValueError(vertrag.text("queue_kandidaten_bytegenau"))
    return queue_hashes


def pruefe_bcc_auswahl(
    queue_manifest: Mapping[str, Any],
    queue_semantic: Mapping[str, Any],
) -> tuple[set[str], list[Any]]:
    """BCC-Auswahl: gebundene Auswahlmodelle und deckungsgleicher Auswahlbeleg."""
    selection_receipt = require_exact_fields(
        queue_manifest.get("selection_receipt"),
        {"models", "items"},
        "Queue-Auswahlbeleg",
    )
    model_scope = queue_semantic.get("model_scope")
    if not isinstance(model_scope, list) or not model_scope:
        raise ValueError("Der Queue-Beleg besitzt keine gebundenen Auswahlmodelle.")
    model_ids: set[str] = set()
    for raw_model in model_scope:
        model = require_exact_fields(
            raw_model,
            {
                "candidate_id",
                "candidate_manifest_sha256",
                "weights_sha256",
                "dataset_plan_id",
                "dataset_manifest_sha256",
            },
            "Queue-Auswahlmodell",
        )
        model_id = str(model.get("candidate_id") or "")
        if not model_id or model_id in model_ids:
            raise ValueError("Der Queue-Beleg besitzt doppelte oder leere Modell-IDs.")
        model_ids.add(model_id)
        for field in (
            "candidate_manifest_sha256",
            "weights_sha256",
            "dataset_plan_id",
            "dataset_manifest_sha256",
        ):
            require_sha256(model.get(field), f"{field} von {model_id}")
    queue_items = queue_semantic.get("items")
    if (
        not isinstance(queue_items, list)
        or selection_receipt.get("items") != queue_items
        or selection_receipt.get("models") != model_scope
    ):
        raise ValueError("Semantischer Queue-Beleg und Auswahlbeleg widersprechen sich.")
    return model_ids, queue_items


def pruefe_proto_auswahl(
    queue_manifest: Mapping[str, Any],
    queue_semantic: Mapping[str, Any],
) -> list[Any]:
    """Proto-Auswahl: Auswahlbeleg ohne Modelle, deckungsgleich mit der Semantik."""
    selection_receipt = require_exact_fields(
        queue_manifest.get("selection_receipt"),
        {"items"},
        "Proto-Queue-Auswahlbeleg",
    )
    if selection_receipt.get("models"):
        raise ValueError("Eine Proto-Queue darf keine gebundenen Auswahlmodelle tragen.")
    queue_items = queue_semantic.get("items")
    if not isinstance(queue_items, list) or selection_receipt.get("items") != queue_items:
        raise ValueError("Semantischer Proto-Beleg und Auswahlbeleg widersprechen sich.")
    return queue_items


def pruefe_bcc_queue_bilder(
    queue_items: list[Any],
    model_ids: set[str],
    physischer_schluessel_von: Callable[[str], str],
) -> dict[str, Mapping[str, Any]]:
    """Jedes BCC-Queue-Bild: eindeutige ID/Haltung, Mindestgroesse, Format und Modelltrigger.

    ``physischer_schluessel_von`` ist die strenge Haltungsidentitaet des
    Aufrufers; sie sperrt auch die Gegenrichtung derselben Haltung.
    """
    queue_by_id: dict[str, Mapping[str, Any]] = {}
    queue_image_hashes_seen: set[str] = set()
    queue_physical_holdings_seen: set[str] = set()
    for raw_item in queue_items:
        item = require_exact_fields(
            raw_item,
            {
                "id",
                "image_sha256",
                "holding_key",
                "physical_holding_key",
                "source_ref",
                "inspection_date",
                "size_bytes",
                "image_format",
                "predictions",
            },
            "Queue-Bildbeleg",
        )
        item_id = str(item.get("id") or "")
        if not item_id or item_id in queue_by_id:
            raise ValueError("Der Queue-Beleg enthaelt doppelte oder leere Bild-IDs.")
        queue_image_sha = require_sha256(
            item.get("image_sha256"),
            f"Queue-Bildhash {item_id}",
        )
        queue_holding = str(item.get("holding_key") or "")
        queue_physical = str(item.get("physical_holding_key") or "")
        queue_source_ref = require_sha256(
            item.get("source_ref"),
            f"Queue-Quellbeleg {item_id}",
        )
        queue_inspection_date = str(item.get("inspection_date") or "")
        try:
            datetime.strptime(queue_inspection_date, "%Y-%m-%d")
        except ValueError as error:
            raise ValueError(
                f"Queue-Bild {item_id} besitzt kein gueltiges Inspektionsdatum."
            ) from error
        queue_size = require_count(
            item.get("size_bytes"),
            f"Queue-Bildgroesse {item_id}",
        )
        queue_format = str(item.get("image_format") or "").casefold()
        if (
            item_id != f"bcc-hn-{queue_image_sha[:16]}"
            or queue_physical != physischer_schluessel_von(queue_holding)
            or queue_image_sha in queue_image_hashes_seen
            or queue_physical in queue_physical_holdings_seen
            or queue_size < MIN_TRAINING_NEGATIVE_BYTES
            or queue_format not in BILDFORMATE
            or item.get("source_ref") != queue_source_ref
        ):
            raise ValueError(f"Queue-Bildbeleg {item_id} ist ungueltig.")
        queue_image_hashes_seen.add(queue_image_sha)
        queue_physical_holdings_seen.add(queue_physical)
        _pruefe_bcc_modelltrigger(item_id, item.get("predictions"), model_ids)
        queue_by_id[item_id] = item
    return queue_by_id


def _pruefe_bcc_modelltrigger(item_id: str, predictions: Any, model_ids: set[str]) -> None:
    """Jedes gebundene Modell hat genau eine gueltige Vorhersage; mindestens eines loest BCC aus."""
    if not isinstance(predictions, list) or len(predictions) != len(model_ids):
        raise ValueError(
            f"Queue-Bild {item_id} besitzt keine vollstaendige Modellvorhersage."
        )
    predicted_model_ids: set[str] = set()
    triggered = False
    for raw_prediction in predictions:
        prediction = require_exact_fields(
            raw_prediction,
            {
                "model_id",
                "predicted_bcc",
                "bcc_detection_count",
                "max_bcc_confidence",
            },
            f"Queue-Vorhersage {item_id}",
        )
        model_id = str(prediction.get("model_id") or "")
        predicted_bcc = prediction.get("predicted_bcc")
        detection_count = prediction.get("bcc_detection_count")
        confidence = prediction.get("max_bcc_confidence")
        if (
            model_id not in model_ids
            or model_id in predicted_model_ids
            or not isinstance(predicted_bcc, bool)
            or isinstance(detection_count, bool)
            or not isinstance(detection_count, int)
            or detection_count < 0
            or (
                confidence is not None
                and (
                    isinstance(confidence, bool)
                    or not isinstance(confidence, (int, float))
                    or not math.isfinite(float(confidence))
                    or not 0.0 <= float(confidence) <= 1.0
                )
            )
            or (predicted_bcc and detection_count < 1)
        ):
            raise ValueError(f"Queue-Vorhersage {item_id} ist ungueltig.")
        predicted_model_ids.add(model_id)
        triggered = triggered or predicted_bcc
    if predicted_model_ids != model_ids or not triggered:
        raise ValueError(
            f"Queue-Bild {item_id} ist nicht an einen BCC-Modelltrigger gebunden."
        )


def pruefe_proto_queue_bilder(
    queue_items: list[Any],
    proto_schluessel_von: Callable[[Any], str],
) -> dict[str, Mapping[str, Any]]:
    """Jedes Proto-Queue-Bild: ID aus dem Bildhash, eine physische Haltung, Groesse, Format, Zieldatei.

    ``proto_schluessel_von`` ist die praefix-schonende Proto-Haltungsidentitaet
    des Aufrufers; sie sperrt auch die Gegenrichtung derselben Haltung.
    """
    queue_by_id: dict[str, Mapping[str, Any]] = {}
    queue_image_hashes_seen: set[str] = set()
    queue_physical_seen: set[str] = set()
    for raw_item in queue_items:
        item = require_exact_fields(
            raw_item,
            {
                "item_id",
                "image_sha256",
                "holding_key",
                "code",
                "gruppe",
                "quelle",
                "quell_datei",
                "leitungsinspektion",
                "size_bytes",
                "image_format",
                "target_file_name",
            },
            "Proto-Queue-Bildbeleg",
        )
        item_id = str(item.get("item_id") or "")
        queue_image_sha = require_sha256(item.get("image_sha256"), f"Proto-Queue-Bildhash {item_id}")
        queue_size = require_count(item.get("size_bytes"), f"Proto-Queue-Bildgroesse {item_id}")
        queue_format = str(item.get("image_format") or "").casefold()
        physical = proto_schluessel_von(item.get("holding_key"))
        if (
            not item_id
            or item_id in queue_by_id
            or item_id != f"{PROTO_ITEM_ID_PREFIX}{queue_image_sha[:20]}"
            or queue_image_sha in queue_image_hashes_seen
            or physical in queue_physical_seen
            or queue_size < MIN_TRAINING_NEGATIVE_BYTES
            or queue_format not in BILDFORMATE
            or item.get("target_file_name") != f"img_{queue_image_sha}.{queue_format}"
        ):
            raise ValueError(f"Proto-Queue-Bildbeleg {item_id} ist ungueltig.")
        queue_image_hashes_seen.add(queue_image_sha)
        queue_physical_seen.add(physical)
        queue_by_id[item_id] = item
    return queue_by_id


def pruefe_kandidaten(
    candidates_bytes: bytes,
    queue_manifest: Mapping[str, Any],
    queue_by_id: Mapping[str, Mapping[str, Any]],
    queue_hashes: Mapping[str, Any],
    vertrag: Satzvertrag,
) -> dict[str, Mapping[str, Any]]:
    """Kandidatenliste, Queue-Bilder und Queue-Hashliste muessen deckungsgleich sein."""
    candidates = strict_json_bytes(candidates_bytes, "Queue-Kandidaten-Beleg")
    if not isinstance(candidates, list):
        raise ValueError("Der Queue-Kandidaten-Beleg ist kein JSON-Array.")
    candidates_by_id: dict[str, Mapping[str, Any]] = {}
    for raw_candidate in candidates:
        candidate = require_exact_fields(
            raw_candidate,
            KANDIDAT_FELDER,
            "Queue-Kandidat",
        )
        candidate_id = str(candidate.get("id") or "")
        if not candidate_id or candidate_id in candidates_by_id:
            raise ValueError("Der Kandidaten-Beleg enthaelt doppelte oder leere IDs.")
        if (
            candidate.get("category") != "all_class_background_review"
            or candidate.get("status") != "pending_review"
        ):
            raise ValueError("Ein Queue-Kandidat besitzt einen ungueltigen Reviewstatus.")
        candidates_by_id[candidate_id] = candidate
    if (
        set(candidates_by_id) != set(queue_by_id)
        or require_count(queue_manifest.get("candidates_count"), "candidates_count")
        != len(candidates)
        or require_count(queue_manifest.get("images_count"), "Queue images_count")
        != len(candidates)
        or require_count(queue_manifest.get("holdings_count"), "Queue holdings_count")
        != len(candidates)
    ):
        raise ValueError(vertrag.text("kandidatenliste_unvollstaendig"))
    expected_queue_hash_paths = {"_candidates.json"}
    for candidate_id, candidate in candidates_by_id.items():
        queue_item = queue_by_id[candidate_id]
        queue_image_sha = str(queue_item["image_sha256"])
        queue_format = str(queue_item["image_format"]).casefold()
        expected_file_name = f"img_{queue_image_sha}.{queue_format}"
        if (
            candidate.get("frame_path") != expected_file_name
            or candidate.get("source_sha256") != queue_image_sha
        ):
            raise ValueError(
                f"Queue-Kandidat {candidate_id} passt nicht zum Auswahlbeleg."
            )
        relative_queue_image = f"images/{expected_file_name}"
        expected_queue_hash_paths.add(relative_queue_image)
        queue_image_hash = require_exact_fields(
            queue_hashes.get(relative_queue_image),
            HASHBELEG_FELDER,
            f"Queue-Bildhash {relative_queue_image}",
        )
        if (
            require_sha256(
                queue_image_hash.get("sha256"),
                f"Queue-Bildhash {relative_queue_image}",
            )
            != queue_image_sha
            or require_count(
                queue_image_hash.get("size_bytes"),
                f"Queue-Bildgroesse {relative_queue_image}",
            )
            != queue_item["size_bytes"]
        ):
            raise ValueError(
                f"Queue-Hashliste bindet {candidate_id} nicht bytegenau."
            )
    if set(queue_hashes) != expected_queue_hash_paths:
        raise ValueError(
            "Queue-Kandidaten und Queue-Hashliste sind nicht deckungsgleich."
        )
    return candidates_by_id
