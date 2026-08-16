"""Schwellenwahl (Spec Abschnitt 5).

Die Schwelle wird NIE an Gold eingestellt. Wer sie so lange dreht, bis auf Gold
null Fehler stehen, hat Gold zum Anpassen benutzt und misst danach sich selbst.
Hier zaehlt allein der getrennte Reservebestand.

Zweiter Block (Ruling zu Aufgabe 7): reine Logik des Modus, der den
Faelle-Rohbeleg aus dem TESTteil des Reservebestands baut. Echte Modell-
Inferenz wird hier bewusst NICHT getestet - dafuer existiert noch kein
trainierter Kandidat.
"""

import json
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
    code_sha256 = {"osd_meter.py": "ab" * 32}

    dokument = kal.baue_faelle_dokument("osd_zeichen_abc123", "deadbeef", code_sha256, [])

    assert dokument == {
        "schema": "osd_schwelle_faelle_v1",
        "kandidat_id": "osd_zeichen_abc123",
        "gewicht_sha256": "deadbeef",
        "code_sha256": code_sha256,
        "faelle": [],
    }


# ---------------------------------------------------------------------------
# Fix-Runde 1 zu Aufgabe 7: Transparenz, worauf die Kalibrierung sich stuetzt,
# und ein atomares Manifestschreiben.
# ---------------------------------------------------------------------------

def test_vergleichbare_faelle_laesst_faelle_ohne_sollwert_weg():
    faelle = [
        {"sicherheit": 0.9, "abweichung_m": 0.0},
        {"sicherheit": 0.3, "abweichung_m": None},
        {"sicherheit": 0.6, "abweichung_m": 0.7},
    ]

    vergleichbar = kal.vergleichbare_faelle(faelle)

    assert vergleichbar == [faelle[0], faelle[2]]


def test_grobe_fehler_ist_teilmenge_von_vergleichbaren_faellen():
    faelle = [
        {"sicherheit": 0.9, "abweichung_m": 0.0},    # vergleichbar, nicht grob
        {"sicherheit": 0.3, "abweichung_m": None},   # nicht vergleichbar
        {"sicherheit": 0.6, "abweichung_m": 0.7},    # vergleichbar UND grob
    ]

    grob = kal.grobe_fehler(faelle)

    assert grob == [faelle[2]]


def test_waehle_schwelle_und_grobe_fehler_stimmen_ueberein():
    """Die Refaktorierung von waehle_schwelle auf grobe_fehler() darf das
    urspruengliche Verhalten aus dem Brief nicht veraendern."""
    faelle = [
        {"sicherheit": 0.55, "abweichung_m": 7.4},
        {"sicherheit": 0.40, "abweichung_m": 12.0},
        {"sicherheit": 0.95, "abweichung_m": 0.0},
    ]

    grob_sicherheiten = sorted(f["sicherheit"] for f in kal.grobe_fehler(faelle))
    assert grob_sicherheiten == [0.40, 0.55]

    schwelle = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)
    assert schwelle == pytest.approx(0.55 + 1e-6)


def test_atomar_schreiben_ueberschreibt_vollstaendig_und_hinterlaesst_keine_tempdatei(tmp_path):
    ziel = tmp_path / "manifest.json"
    ziel.write_text('{"alt": true}', encoding="utf-8")

    kal._atomar_schreiben(ziel, '{"neu": true}')

    assert ziel.read_text(encoding="utf-8") == '{"neu": true}'
    assert list(tmp_path.iterdir()) == [ziel]


def test_atomar_schreiben_legt_fehlende_ordner_an(tmp_path):
    ziel = tmp_path / "neuer_ordner" / "manifest.json"

    kal._atomar_schreiben(ziel, '{"x": 1}')

    assert ziel.read_text(encoding="utf-8") == '{"x": 1}'


# ---------------------------------------------------------------------------
# Fix-Runde 1: CLI-Ebene von "kalibrieren" - Aufgabe 4 (Lesercode-Bindung)
# und Aufgabe 5 (Mindestzahl vergleichbarer Faelle). Reine Datei-Fixtures,
# kein Modell/Ultralytics noetig (_main_kalibrieren ruft keine Inferenz auf).
# ---------------------------------------------------------------------------

def _kandidat_ohne_schwelle(tmp_path: Path) -> Path:
    kandidat = tmp_path / "kandidat"
    kandidat.mkdir()
    (kandidat / "manifest.json").write_text(
        json.dumps({"kandidat_id": "osd_zeichen_test", "gewicht_sha256": "ab" * 32}),
        encoding="utf-8")
    return kandidat


# Sentinel, um "code_sha256 weggelassen -> Standardwert setzen" von
# "code_sha256=None -> bewusst weglassen" unterscheiden zu koennen.
_STANDARD_CODE_SHA256 = object()


def _faelle_datei(tmp_path: Path, anzahl_vergleichbar: int,
                   grobe_fehler_anzahl: int = 0,
                   code_sha256=_STANDARD_CODE_SHA256) -> Path:
    faelle = [
        {"id": f"f{i}", "sicherheit": 0.9, "gelesen_m": 1.0,
         "soll_m": 1.0, "abweichung_m": 0.0}
        for i in range(anzahl_vergleichbar)
    ] + [
        {"id": f"g{i}", "sicherheit": 0.5, "gelesen_m": 9.0,
         "soll_m": 0.0, "abweichung_m": 9.0}
        for i in range(grobe_fehler_anzahl)
    ]
    dokument: dict = {"faelle": faelle}
    if code_sha256 is _STANDARD_CODE_SHA256:
        dokument["code_sha256"] = {"osd_meter.py": "cd" * 32}
    elif code_sha256 is not None:
        dokument["code_sha256"] = code_sha256
    pfad = tmp_path / "faelle.json"
    pfad.write_text(json.dumps(dokument), encoding="utf-8")
    return pfad


def test_kalibrieren_verweigert_bei_zu_wenig_vergleichbaren_faellen(tmp_path, capsys):
    kandidat = _kandidat_ohne_schwelle(tmp_path)
    faelle = _faelle_datei(tmp_path, anzahl_vergleichbar=kal.MINDEST_VERGLEICHBARE_FAELLE - 1)

    rc = kal.main(["kalibrieren", "--faelle", str(faelle), "--kandidat", str(kandidat)])

    assert rc == 2
    fehler = capsys.readouterr().err
    assert "vergleichbare" in fehler.lower()
    manifest = json.loads((kandidat / "manifest.json").read_text(encoding="utf-8"))
    assert "schwelle" not in manifest


def test_kalibrieren_mit_override_flag_friert_trotzdem_ein(tmp_path):
    kandidat = _kandidat_ohne_schwelle(tmp_path)
    faelle = _faelle_datei(tmp_path, anzahl_vergleichbar=kal.MINDEST_VERGLEICHBARE_FAELLE - 1)

    rc = kal.main([
        "kalibrieren", "--faelle", str(faelle), "--kandidat", str(kandidat),
        "--trotz-wenig-vergleichbaren-faellen",
    ])

    assert rc == 0
    manifest = json.loads((kandidat / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["schwelle"] is not None


def test_kalibrieren_friert_ohne_flag_bei_genug_faellen_ein(tmp_path):
    kandidat = _kandidat_ohne_schwelle(tmp_path)
    faelle = _faelle_datei(tmp_path, anzahl_vergleichbar=kal.MINDEST_VERGLEICHBARE_FAELLE)

    rc = kal.main(["kalibrieren", "--faelle", str(faelle), "--kandidat", str(kandidat)])

    assert rc == 0


def test_kalibrieren_verweigert_ohne_code_sha256_im_faelle_beleg(tmp_path, capsys):
    """Ein Faelle-Beleg ohne code_sha256 (z.B. aus einer aelteren, nicht
    kompatiblen Quelle) darf keine Schwelle einfrieren - sonst waere sie
    nicht auf den erzeugenden Code zurueckfuehrbar (Aufgabe 4)."""
    kandidat = _kandidat_ohne_schwelle(tmp_path)
    faelle = _faelle_datei(
        tmp_path, anzahl_vergleichbar=kal.MINDEST_VERGLEICHBARE_FAELLE,
        code_sha256=None)

    rc = kal.main(["kalibrieren", "--faelle", str(faelle), "--kandidat", str(kandidat)])

    assert rc == 2
    fehler = capsys.readouterr().err
    assert "code_sha256" in fehler
    manifest = json.loads((kandidat / "manifest.json").read_text(encoding="utf-8"))
    assert "schwelle" not in manifest


def test_kalibrieren_bindet_code_sha256_ins_manifest(tmp_path):
    kandidat = _kandidat_ohne_schwelle(tmp_path)
    erwartet = {"osd_meter.py": "ee" * 32, "osd_modell.py": "ff" * 32}
    faelle = _faelle_datei(
        tmp_path, anzahl_vergleichbar=kal.MINDEST_VERGLEICHBARE_FAELLE,
        code_sha256=erwartet)

    rc = kal.main(["kalibrieren", "--faelle", str(faelle), "--kandidat", str(kandidat)])

    assert rc == 0
    manifest = json.loads((kandidat / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["schwelle_code_sha256"] == erwartet


def test_kalibrieren_bricht_bei_bereits_eingefrorener_schwelle_ab(tmp_path, capsys):
    kandidat = tmp_path / "kandidat"
    kandidat.mkdir()
    (kandidat / "manifest.json").write_text(
        json.dumps({"kandidat_id": "x", "gewicht_sha256": "ab" * 32, "schwelle": 0.3}),
        encoding="utf-8")
    faelle = _faelle_datei(tmp_path, anzahl_vergleichbar=kal.MINDEST_VERGLEICHBARE_FAELLE)

    rc = kal.main(["kalibrieren", "--faelle", str(faelle), "--kandidat", str(kandidat)])

    assert rc == 2
    assert "bereits eingefroren" in capsys.readouterr().err
