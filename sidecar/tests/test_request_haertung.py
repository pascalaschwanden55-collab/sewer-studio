"""Requesthaertung des Sidecars (Gesamtaudit 2026-08-18, R-01).

Zwei getrennte Schwaechen:

1. Die Groessenpruefung lief erst in der Route - also nachdem FastAPI und
   Pydantic den ganzen Koerper als Zeichenkette und Objektbaum aufgebaut
   hatten. Bei 500 erlaubten Bildern zu je 25 MB waren rund 16,7 GB Base64
   implizit moeglich.

2. Der Standard-422-Handler nimmt das ungueltige Eingabeobjekt als ``input`` in
   die Antwort auf. Verfehlt ein CONTAINER-Feld seinen Typ, ist dieses Objekt
   der gesamte Anfragekoerper samt Bildern.

Zur zweiten Schwaeche gehoert eine Einordnung, die im Auditbericht fehlte:
Gemessen wurde sie nur bei zwei von vier Fehlerformen. Ein falscher
Literalwert oder ein unbekanntes Feld INNERHALB eines Samples ergab schon
vorher rund 1,1 KB. Die Spiegelung war moeglich, aber nie das Normalverhalten.
"""

import base64
import json

import pytest
from fastapi.testclient import TestClient

from sidecar.config import settings
from sidecar.main import app


GROSS = base64.b64encode(b"X" * 60_000).decode()


def _basis(**zusatz):
    koerper = dict(
        schema_version="2.0", plan_id="p", plan_sha256="s", class_map_version=3,
        vsa_manifest_hash="h", registry_hash="r", classes=["A"],
        manifest_json_base64=GROSS, manifest_sha256="m", samples=[],
    )
    koerper.update(zusatz)
    return koerper


def _sende(client, koerper):
    return client.post(
        "/training/export-yolo",
        content=json.dumps(koerper),
        headers={"Content-Type": "application/json"},
    )


@pytest.fixture
def client():
    return TestClient(app, raise_server_exceptions=False)


# ── Groessengrenze vor dem Parsen ───────────────────────────────────────────

def test_zu_grosser_koerper_wird_frueh_mit_413_abgewiesen(client, monkeypatch):
    monkeypatch.setattr(settings, "max_request_bytes", 1_000, raising=False)

    antwort = _sende(client, _basis())

    assert antwort.status_code == 413
    assert antwort.json()["code"] == "request_too_large"


def test_koerper_unter_der_grenze_erreicht_die_route(client, monkeypatch):
    monkeypatch.setattr(settings, "max_request_bytes", 10_000_000, raising=False)

    antwort = _sende(client, _basis())

    # Inhaltlich ungueltig (leere samples) - aber nicht wegen der Groesse.
    assert antwort.status_code != 413


def test_ungueltige_laengenangabe_wird_abgewiesen(client):
    antwort = client.post(
        "/training/export-yolo",
        content=b"{}",
        headers={"Content-Type": "application/json", "Content-Length": "keine-zahl"},
    )

    assert antwort.status_code in (400, 422)


def test_chunked_koerper_ohne_laengenangabe_wird_mit_411_abgewiesen(
    client, monkeypatch,
):
    monkeypatch.setattr(settings, "max_request_bytes", 1_000, raising=False)

    antwort = client.post(
        "/training/export-yolo",
        content=iter([b"{}"]),
        headers={
            "Content-Type": "application/json",
            "Transfer-Encoding": "chunked",
        },
    )

    assert antwort.status_code == 411
    assert antwort.json()["code"] == "content_length_required"


def test_grenze_null_schaltet_die_pruefung_ab(client, monkeypatch):
    monkeypatch.setattr(settings, "max_request_bytes", 0, raising=False)

    antwort = _sende(client, _basis())

    assert antwort.status_code != 413


# ── Validierungsfehler spiegeln keine Nutzdaten ─────────────────────────────

@pytest.mark.parametrize(
    "name, koerper",
    [
        # Genau die zwei Formen, die vorher gespiegelt haben: ein Containerfeld
        # verfehlt seinen Typ, damit wird der ganze Koerper zum "input".
        ("samples kein Array", _basis(samples={"image_base64": GROSS})),
        ("unbekanntes Feld oben", _basis(extra=GROSS)),
        # Und die Formen, die schon vorher klein blieben - sie duerfen sich
        # nicht verschlechtern.
        ("falscher Literalwert", _basis(schema_version="9.9")),
        (
            "unbekanntes Feld im Sample",
            _basis(samples=[{
                "image_sha256": "a", "image_base64": GROSS, "split": "train",
                "target_file_name": "x.jpg", "labels": [], "unbekannt": 1,
            }]),
        ),
    ],
)
def test_validierungsfehler_enthaelt_keine_nutzdaten(client, name, koerper):
    antwort = _sende(client, koerper)

    assert antwort.status_code == 422, name
    assert GROSS[:200] not in antwort.text, f"{name}: Base64 in der Antwort"
    assert len(antwort.text) < 4_000, f"{name}: Antwort unerwartet gross ({len(antwort.text)})"


def test_validierungsfehler_nennt_weiterhin_den_ort(client):
    """Die Meldung muss brauchbar bleiben - sonst waere der Aufrufer blind."""
    antwort = _sende(client, _basis(schema_version="9.9"))

    daten = antwort.json()
    assert daten["code"] == "validation_error"
    assert any("schema_version" in eintrag["loc"] for eintrag in daten["errors"])
