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

Die beiden Varianten unterscheiden sich in Zweck, Pilot, Feldern und in den
Meldungstexten. Diese Unterschiede stehen ausdruecklich in ``BCC_VERTRAG`` und
``PROTO_VERTRAG``; gemeinsame Funktionen pruefen nur wirklich gleiche Regeln.
"""
from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from typing import Any, Mapping

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
    pilot: str
    ordner_praefix: str
    semantik_felder: frozenset[str]
    texte: Mapping[str, str]

    def text(self, schluessel: str, **werte: Any) -> str:
        return self.texte[schluessel].format(**werte)


BCC_VERTRAG = Satzvertrag(
    satz_zweck=NEGATIVE_SET_PURPOSE,
    pilot=NEGATIVE_SET_PILOT,
    ordner_praefix="bcc_hn_",
    semantik_felder=SATZ_SEMANTIK_FELDER,
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
    },
)

PROTO_VERTRAG = Satzvertrag(
    satz_zweck=PROTO_SET_PURPOSE,
    pilot=PROTO_PILOT,
    ordner_praefix="proto_hn_",
    # Proto-Saetze begruenden ausgeschlossene klassenfreie Bilder ausdruecklich.
    semantik_felder=SATZ_SEMANTIK_FELDER
    | {"excluded_not_normalizable", "excluded_eval_protected"},
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
