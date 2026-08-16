"""Schwellenwahl (Spec Abschnitt 5).

Die Schwelle wird NIE an Gold eingestellt. Wer sie so lange dreht, bis auf Gold
null Fehler stehen, hat Gold zum Anpassen benutzt und misst danach sich selbst.
Hier zaehlt allein der getrennte Reservebestand.

Zweiter Block (Ruling zu Aufgabe 7): reine Logik des Modus, der den
Faelle-Rohbeleg aus dem TESTteil des Reservebestands baut. Echte Modell-
Inferenz wird hier bewusst NICHT getestet - dafuer existiert noch kein
trainierter Kandidat.
"""

import sys
from pathlib import Path

import pytest

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_schwelle_kalibrieren as kal


def test_schwelle_schliesst_alle_groben_fehler_aus():
    faelle = [
        {"sicherheit": 0.95, "abweichung_m": 0.0},
        {"sicherheit": 0.90, "abweichung_m": 0.02},
        {"sicherheit": 0.55, "abweichung_m": 7.4},   # grob falsch
        {"sicherheit": 0.40, "abweichung_m": 12.0},  # grob falsch
    ]

    schwelle = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)

    assert schwelle > 0.55
    assert schwelle <= 0.90


def test_sicherheitsabstand_wird_aufgeschlagen():
    faelle = [
        {"sicherheit": 0.90, "abweichung_m": 0.0},
        {"sicherheit": 0.50, "abweichung_m": 9.0},
    ]

    ohne = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)
    mit = kal.waehle_schwelle(faelle, sicherheitsabstand=0.05)

    assert mit == pytest.approx(ohne + 0.05)


def test_ohne_groben_fehler_bleibt_die_grundschwelle():
    faelle = [
        {"sicherheit": 0.80, "abweichung_m": 0.01},
        {"sicherheit": 0.70, "abweichung_m": 0.03},
    ]

    schwelle = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)

    assert schwelle == kal.GRUNDSCHWELLE


def test_faelle_ohne_sollwert_zaehlen_nicht():
    faelle = [
        {"sicherheit": 0.30, "abweichung_m": None},
        {"sicherheit": 0.80, "abweichung_m": 0.0},
    ]

    schwelle = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)

    assert schwelle == kal.GRUNDSCHWELLE


def test_alles_falsch_liefert_unerreichbare_schwelle():
    faelle = [
        {"sicherheit": 0.99, "abweichung_m": 5.0},
    ]

    schwelle = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)

    assert schwelle > 0.99


# ---------------------------------------------------------------------------
# Modus "faelle": reine Logik ohne Modell-Inferenz
# ---------------------------------------------------------------------------

def test_nur_testteil_filtert_split():
    wahrheit = {"eintraege": [
        {"id": "a", "split": "train"},
        {"id": "b", "split": "validation"},
        {"id": "c", "split": "test"},
        {"id": "d", "split": "test"},
    ]}

    eintraege = kal.nur_testteil(wahrheit)

    assert [e["id"] for e in eintraege] == ["c", "d"]


def test_nur_testteil_ohne_eintraege_ist_leer():
    assert kal.nur_testteil({}) == []
    assert kal.nur_testteil({"eintraege": []}) == []


def test_abweichung_m_ohne_lesung_ist_none():
    assert kal.abweichung_m(None, 5.0) is None


def test_abweichung_m_ohne_sollwert_ist_none():
    assert kal.abweichung_m(5.0, None) is None


def test_abweichung_m_ohne_lesung_und_ohne_sollwert_ist_none():
    assert kal.abweichung_m(None, None) is None


def test_abweichung_m_rechnet_absolutbetrag():
    assert kal.abweichung_m(5.0, 5.3) == pytest.approx(0.3)
    assert kal.abweichung_m(5.3, 5.0) == pytest.approx(0.3)


def test_fall_hat_die_erwarteten_felder():
    fall = kal.baue_fall("bild_1", 0.83, 12.4, 12.41)

    assert fall == {
        "id": "bild_1",
        "sicherheit": 0.83,
        "gelesen_m": 12.4,
        "soll_m": 12.41,
        "abweichung_m": pytest.approx(0.01),
    }


def test_fall_ohne_lesung_hat_abweichung_none():
    fall = kal.baue_fall("bild_2", 0.10, None, 3.0)

    assert fall["gelesen_m"] is None
    assert fall["abweichung_m"] is None


def test_faelle_dokument_hat_das_erwartete_schema():
    dokument = kal.baue_faelle_dokument("osd_zeichen_abc123", "deadbeef", [])

    assert dokument == {
        "schema": "osd_schwelle_faelle_v1",
        "kandidat_id": "osd_zeichen_abc123",
        "gewicht_sha256": "deadbeef",
        "faelle": [],
    }
