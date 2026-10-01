"""Sperrliste des OSD-Trainings (Spec Abschnitt 4.4 + Fix-Runde 1/Aufgabe 1).

Die 197 Goldbilder und ihre Haltungen duerfen in keiner Trainingsquelle
auftauchen. Sonst misst die Goldmessung am Ende sich selbst.

Zweite Sperrquelle (Fix-Runde 1): der Testteil des Reservebestands, den
osd_schwelle_kalibrieren.py fuer eine von Gold unabhaengige Schwelle
braucht. Ohne eigene Sperre waere dieser Testteil ungehindert ins Training
gewandert - die Schwelle waere dann an auswendig gelerntem Material
kalibriert worden.
"""

import json
import sys
from pathlib import Path

import pytest

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_schutz


def _schreibe_satz(wurzel: Path, name: str, eintraege: list[dict]) -> None:
    satz = wurzel / name
    (satz / "frames").mkdir(parents=True)
    (satz / "manifest.json").write_text(
        json.dumps({"schema_version": 1, "name": name, "eintraege": eintraege}),
        encoding="utf-8")


def _schreibe_reservebestand(pfad: Path, eintraege: list[dict]) -> None:
    pfad.parent.mkdir(parents=True, exist_ok=True)
    pfad.write_text(
        json.dumps({"schema": "osd_wahrheit_protokoll_v2", "eintraege": eintraege}),
        encoding="utf-8")


def _leerer_reservebestand(tmp_path: Path, name: str = "reserve.json",
                           haltung: str = "99991-99992",
                           bild_sha256: str = "ff" * 32,
                           split: str = "test") -> Path:
    """Ein einzelner, unbeteiligter Reserve-Testeintrag - Standardfixture
    fuer Tests, die nur die GOLD-Pruefung beobachten wollen."""
    pfad = tmp_path / name
    _schreibe_reservebestand(pfad, [
        {"id": "r0001", "haltung": haltung, "split": split,
         "bild_sha256": bild_sha256},
    ])
    return pfad


# ---------------------------------------------------------------------------
# Gold - unveraendertes Verhalten, jetzt mit explizit isoliertem Reservebestand
# (keine reale Produktionsdatei mehr im Testpfad).
# ---------------------------------------------------------------------------

def test_goldhash_ist_gesperrt(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461",
         "bild_sha256": "aa" * 32, "meter": 0.0},
    ])
    reserve = _leerer_reservebestand(tmp_path)

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)

    assert schutz.ist_gesperrt("aa" * 32, None) is True
    assert schutz.sperrquelle("aa" * 32, None) == "gold"


def test_gegenrichtung_der_haltung_ist_gesperrt(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461",
         "bild_sha256": "aa" * 32, "meter": 0.0},
    ])
    reserve = _leerer_reservebestand(tmp_path)

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)

    # Dasselbe Rohr, andere Fahrtrichtung - muss ebenfalls gesperrt sein.
    assert schutz.ist_gesperrt("bb" * 32, "33461-36051") is True


def test_unbeteiligtes_bild_ist_frei(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461",
         "bild_sha256": "aa" * 32, "meter": 0.0},
    ])
    reserve = _leerer_reservebestand(tmp_path)

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)

    assert schutz.ist_gesperrt("cc" * 32, "10261-10262") is False
    assert schutz.sperrquelle("cc" * 32, "10261-10262") is None


def test_fehlender_satz_bricht_ab(tmp_path):
    reserve = _leerer_reservebestand(tmp_path)

    with pytest.raises(SystemExit):
        osd_schutz.lade_schutz(tmp_path, saetze=("gibt_es_nicht",), reservebestand=reserve)


def test_eintrag_ohne_hash_bricht_ab(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461", "meter": 0.0},
    ])
    reserve = _leerer_reservebestand(tmp_path)

    with pytest.raises(SystemExit):
        osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)


# ---------------------------------------------------------------------------
# Reservebestand (Fix-Runde 1, Aufgabe 1): der Testteil (split == "test")
# wird wie Gold gesperrt - Train/Validation duerfen ins Training, weil sie
# die Schwelle nicht mitbestimmen.
# ---------------------------------------------------------------------------

def _leerer_goldbestand(tmp_path: Path) -> Path:
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "10001-10002",
         "bild_sha256": "11" * 32, "meter": 0.0},
    ])
    return tmp_path


def test_reserve_testeintrag_sperrt_bild_und_haltung(tmp_path):
    _leerer_goldbestand(tmp_path)
    reserve = tmp_path / "reserve.json"
    _schreibe_reservebestand(reserve, [
        {"id": "r0001", "haltung": "07.1028024-10254", "split": "test",
         "bild_sha256": "ed" * 32},
    ])

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)

    assert schutz.ist_gesperrt("ed" * 32, None) is True
    assert schutz.sperrquelle("ed" * 32, None) == "reserve"
    assert schutz.ist_gesperrt("00" * 32, "07.1028024-10254") is True


def test_reserve_testeintrag_sperrt_beide_fahrtrichtungen(tmp_path):
    _leerer_goldbestand(tmp_path)
    reserve = tmp_path / "reserve.json"
    _schreibe_reservebestand(reserve, [
        {"id": "r0001", "haltung": "07.1028024-10254", "split": "test",
         "bild_sha256": "ed" * 32},
    ])

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)

    # Gegenrichtung angegeben - muss trotzdem greifen (genau wie bei Gold).
    assert schutz.ist_gesperrt("00" * 32, "10254-07.1028024") is True
    assert schutz.sperrquelle("00" * 32, "10254-07.1028024") == "reserve"


def test_reserve_train_und_validation_eintraege_bleiben_frei(tmp_path):
    """Train/Validation des Reservebestands duerfen ins Training - nur der
    Testteil hat die Schwelle mitbestimmt."""
    _leerer_goldbestand(tmp_path)
    reserve = tmp_path / "reserve.json"
    _schreibe_reservebestand(reserve, [
        {"id": "r0001", "haltung": "20001-20002", "split": "train",
         "bild_sha256": "aa" * 32},
        {"id": "r0002", "haltung": "20003-20004", "split": "validation",
         "bild_sha256": "bb" * 32},
        {"id": "r0003", "haltung": "20005-20006", "split": "test",
         "bild_sha256": "cc" * 32},
    ])

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)

    assert schutz.ist_gesperrt("aa" * 32, "20001-20002") is False
    assert schutz.ist_gesperrt("bb" * 32, "20003-20004") is False
    assert schutz.ist_gesperrt("cc" * 32, "20005-20006") is True


def test_reservebestand_ohne_testeintraege_bricht_ab(tmp_path):
    _leerer_goldbestand(tmp_path)
    reserve = tmp_path / "reserve.json"
    _schreibe_reservebestand(reserve, [
        {"id": "r0001", "haltung": "20001-20002", "split": "train",
         "bild_sha256": "aa" * 32},
    ])

    with pytest.raises(SystemExit):
        osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)


def test_fehlender_reservebestand_bricht_ab(tmp_path):
    """Die zentrale Regel dieser Erweiterung: ein fehlender Reservebestand
    ist ein harter Fehler, kein stiller Uebersprung."""
    _leerer_goldbestand(tmp_path)
    reserve = tmp_path / "gibt_es_nicht.json"

    with pytest.raises(SystemExit):
        osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)


def test_reserve_eintrag_ohne_hash_bricht_ab(tmp_path):
    _leerer_goldbestand(tmp_path)
    reserve = tmp_path / "reserve.json"
    _schreibe_reservebestand(reserve, [
        {"id": "r0001", "haltung": "20001-20002", "split": "test"},
    ])

    with pytest.raises(SystemExit):
        osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)


def test_reserve_eintrag_ohne_haltung_bricht_ab(tmp_path):
    _leerer_goldbestand(tmp_path)
    reserve = tmp_path / "reserve.json"
    _schreibe_reservebestand(reserve, [
        {"id": "r0001", "haltung": None, "split": "test", "bild_sha256": "aa" * 32},
    ])

    with pytest.raises(SystemExit):
        osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",), reservebestand=reserve)


def test_default_verwendet_den_echten_produktiven_reservebestand():
    """lade_schutz() ohne Argument zeigt auf die echte Datei (Fail-closed-
    Standard) - kein Test-Stub. Nur die ERREICHBARKEIT des Standardpfads
    wird hier geprueft, nicht sein Inhalt (der gehoert dem produktiven
    Bestand, nicht diesem Test)."""
    assert osd_schutz.RESERVEBESTAND_STANDARD.name == "wahrheit.json"


# ---------------------------------------------------------------------------
# Schutz-Objekt: Quellen bleiben unterscheidbar (sperrquelle()).
# ---------------------------------------------------------------------------

def test_schutz_unterscheidet_gold_und_reserve_quelle():
    schutz = osd_schutz.Schutz(
        bild_hashes_gold=frozenset({"aa" * 32}),
        haltungen_gold=frozenset(),
        bild_hashes_reserve=frozenset({"bb" * 32}),
        haltungen_reserve=frozenset(),
    )

    assert schutz.sperrquelle("aa" * 32, None) == "gold"
    assert schutz.sperrquelle("bb" * 32, None) == "reserve"
    assert schutz.sperrquelle("cc" * 32, None) is None


def test_schutz_positionale_konstruktion_bleibt_kompatibel():
    """Bestehende Aufrufer bauen Schutz(bild_hashes, haltungen) positional -
    das muss weiterhin GOLD befuellen (Reserve bleibt leer)."""
    schutz = osd_schutz.Schutz(frozenset({"aa" * 32}), frozenset({"10261-10262"}))

    assert schutz.bild_hashes_gold == frozenset({"aa" * 32})
    assert schutz.haltungen_gold == frozenset({"10261-10262"})
    assert schutz.bild_hashes_reserve == frozenset()
    assert schutz.haltungen_reserve == frozenset()
    assert schutz.ist_gesperrt("aa" * 32, None) is True


def test_saetze_enthalten_jede_eingefrorene_messlatte():
    """osd_mix_v1 hat am 2026-08-17 die Kettenentscheidung mitbestimmt und ist
    damit verbraucht. Fehlt er in der Sperrliste, zieht die naechste Ziehung
    seine Haltungen wieder mit und die naechste Abnahme misst sich selbst."""
    import osd_schutz

    assert osd_schutz.SAETZE == ("osd_sd_v1", "osd_hd_v1", "osd_hd2_v1", "osd_mix_v1")


def test_sperrliste_und_bewertungsliste_sind_getrennt():
    """osd_goldmessung.SAETZE bleibt bei den drei alten Saetzen - die
    Freigabemarke '170 von 197' ist an deren Bilderzahl gebunden."""
    import osd_goldmessung
    import osd_schutz

    assert osd_goldmessung.SAETZE == ("osd_sd_v1", "osd_hd_v1", "osd_hd2_v1")
    assert set(osd_goldmessung.SAETZE).issubset(set(osd_schutz.SAETZE))
