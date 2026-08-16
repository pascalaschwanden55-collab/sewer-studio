"""osd_modell_goldmessung: Goldmessung des trainierten Zeichenleser-Kandidaten.

Ruling zu Aufgabe 8: Der Brief definierte eine eigene baue_modell_leser() -
das wurde NICHT uebernommen. osd_modell_leser.baue_modell_leser (Aufgabe 7)
ist der einzige Inferenzpfad; dieses Skript ruft ihn nur auf. Diese Tests
stubben ihn deshalb ueber osd_modell_leser.baue_modell_leser, statt ein
echtes Modell zu laden - kein GPU, kein trainierter Kandidat noetig.

Getestet wird nur, was ohne Modell testbar ist: die beiden Verweigerungen
(fehlende Schwelle, abweichender Gewichts-Hash), dass ein vorhandener Bericht
nie ueberschrieben wird, dass ein technischer Fehler bei der Inferenz nicht
als "nicht gelesen" verschwindet, und die reine Freigabe-Logik.
"""

import hashlib
import json
import sys
from pathlib import Path

import pytest
from PIL import Image

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_goldmessung
import osd_modell_goldmessung as goldmessung
import osd_modell_leser


class _VerbotenesLeserModul:
    """Steht in sys.modules['osd_modell_leser'] und knallt bei jedem Zugriff.

    Bricht main() VOR dem lazy Import von baue_modell_leser ab (Schwelle
    fehlt bzw. Gewichts-Hash weicht ab), wird dieses Attribut nie gelesen.
    Wuerde main() trotzdem versuchen, den Leser zu importieren, faellt der
    Test mit dieser AssertionError durch statt mit dem erwarteten Exitcode -
    kein echtes osd_modell_leser (und kein Ultralytics) noetig.
    """

    def __getattr__(self, name):
        raise AssertionError(
            "osd_modell_leser haette hier nicht importiert werden duerfen "
            f"(Zugriff auf '{name}')")


def _kandidat(tmp_path: Path, *, schwelle=0.3, gewicht_bytes: bytes = b"testgewicht",
              manifest_sha256: str | None = None) -> Path:
    kandidat = tmp_path / "kandidat"
    (kandidat / "weights").mkdir(parents=True)
    gewicht = kandidat / "weights" / "best.pt"
    gewicht.write_bytes(gewicht_bytes)
    if manifest_sha256 is None:
        manifest_sha256 = hashlib.sha256(gewicht_bytes).hexdigest()
    manifest = {
        "kandidat_id": "osd_zeichen_test",
        "gewicht_datei": "weights/best.pt",
        "gewicht_sha256": manifest_sha256,
        "schwelle": schwelle,
    }
    (kandidat / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    return kandidat


def _leerer_goldsatz(ordner: Path, name: str) -> None:
    satz = ordner / name
    satz.mkdir(parents=True)
    manifest = {"schema_version": 1, "name": name, "eintraege": []}
    (satz / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False), encoding="utf-8")


def _goldsatz_mit_einem_bild(ordner: Path, name: str, soll: float = 1.0) -> None:
    satz = ordner / name
    (satz / "frames").mkdir(parents=True)
    bild = satz / "frames" / "f0001.jpg"
    Image.new("RGB", (16, 16), (5, 5, 5)).save(bild)
    manifest = {
        "schema_version": 1,
        "name": name,
        "eintraege": [{
            "nr": 1,
            "datei": "f0001.jpg",
            "haltung": "1-2",
            "bild_sha256": hashlib.sha256(bild.read_bytes()).hexdigest(),
            "meter": soll,
        }],
    }
    (satz / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False), encoding="utf-8")


def _goldsatz_ohne_sollwert(ordner: Path, name: str) -> None:
    """Ein Goldeintrag ohne 'meter' - messe_satz() zaehlt das als
    'ohne_sollwert', nicht als 'nicht_gelesen' (Fix-Runde 1, Aufgabe 6)."""
    satz = ordner / name
    (satz / "frames").mkdir(parents=True)
    bild = satz / "frames" / "f0001.jpg"
    Image.new("RGB", (16, 16), (5, 5, 5)).save(bild)
    manifest = {
        "schema_version": 1,
        "name": name,
        "eintraege": [{
            "nr": 1,
            "datei": "f0001.jpg",
            "haltung": "1-2",
            "bild_sha256": hashlib.sha256(bild.read_bytes()).hexdigest(),
            "meter": None,
        }],
    }
    (satz / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False), encoding="utf-8")


def _leere_gold_wurzel(tmp_path: Path) -> Path:
    wurzel = tmp_path / "gold"
    for name in osd_goldmessung.SAETZE:
        _leerer_goldsatz(wurzel, name)
    return wurzel


# ---------------------------------------------------------------------------
# Verweigerungen - beide vor jedem Modellzugriff, kein Ultralytics noetig.
# ---------------------------------------------------------------------------

def test_fehlende_schwelle_verweigert_den_lauf(tmp_path, monkeypatch, capsys):
    kandidat = _kandidat(tmp_path, schwelle=None)
    monkeypatch.setitem(sys.modules, "osd_modell_leser", _VerbotenesLeserModul())

    rc = goldmessung.main([
        "--kandidat", str(kandidat),
        "--gold-wurzel", str(_leere_gold_wurzel(tmp_path)),
        "--bericht-ordner", str(tmp_path / "reports"),
    ])

    assert rc == 2
    fehler = capsys.readouterr().err
    assert "Schwelle" in fehler
    assert "nicht eingefroren" in fehler
    assert not (tmp_path / "reports").exists()


def test_abweichender_gewichtshash_verweigert_den_lauf(tmp_path, monkeypatch, capsys):
    kandidat = _kandidat(tmp_path, schwelle=0.3, manifest_sha256="falscher_hash")
    monkeypatch.setitem(sys.modules, "osd_modell_leser", _VerbotenesLeserModul())

    rc = goldmessung.main([
        "--kandidat", str(kandidat),
        "--gold-wurzel", str(_leere_gold_wurzel(tmp_path)),
        "--bericht-ordner", str(tmp_path / "reports"),
    ])

    assert rc == 2
    fehler = capsys.readouterr().err
    assert "Gewichtshash" in fehler
    assert "falscher_hash" in fehler
    assert not (tmp_path / "reports").exists()


# ---------------------------------------------------------------------------
# Ein technischer Fehler bei der Inferenz darf nie als "nicht gelesen"
# verschwinden - er muss den Lauf sichtbar sprengen.
# ---------------------------------------------------------------------------

def test_technischer_fehler_bei_der_inferenz_wird_nicht_verschluckt(tmp_path, monkeypatch):
    kandidat = _kandidat(tmp_path)
    gold_wurzel = tmp_path / "gold"
    _goldsatz_mit_einem_bild(gold_wurzel, osd_goldmessung.SAETZE[0])
    for name in osd_goldmessung.SAETZE[1:]:
        _leerer_goldsatz(gold_wurzel, name)

    def kaputter_leser_bauer(_kandidat, _schwelle):
        def lese(_bild_pfad):
            raise RuntimeError("CUDA explodiert")
        return lese

    monkeypatch.setattr(osd_modell_leser, "baue_modell_leser", kaputter_leser_bauer)

    with pytest.raises(RuntimeError, match="CUDA explodiert"):
        goldmessung.main([
            "--kandidat", str(kandidat),
            "--gold-wurzel", str(gold_wurzel),
            "--bericht-ordner", str(tmp_path / "reports"),
        ])

    # Kein halber Bericht bei einem abgebrochenen Lauf.
    assert not (tmp_path / "reports").exists()


# ---------------------------------------------------------------------------
# Ein vorhandener Bericht wird nie ueberschrieben.
# ---------------------------------------------------------------------------

def test_vorhandener_bericht_wird_nicht_ueberschrieben(tmp_path, monkeypatch, capsys):
    kandidat = _kandidat(tmp_path)
    gold_wurzel = _leere_gold_wurzel(tmp_path)
    bericht_ordner = tmp_path / "reports"
    bericht_ordner.mkdir(parents=True)
    ziel = bericht_ordner / "osd_modell_goldmessung_osd_zeichen_test.json"
    ziel.write_text("alter_bericht", encoding="utf-8")

    monkeypatch.setattr(
        osd_modell_leser, "baue_modell_leser",
        lambda _kandidat, _schwelle: (lambda _bild: {"meter": None}))

    rc = goldmessung.main([
        "--kandidat", str(kandidat),
        "--gold-wurzel", str(gold_wurzel),
        "--bericht-ordner", str(bericht_ordner),
    ])

    assert rc == 0
    assert ziel.read_text(encoding="utf-8") == "alter_bericht"
    ausgabe = capsys.readouterr().out
    assert "nicht ueberschrieben" in ausgabe
    # Keine liegen gebliebene Arbeitsdatei.
    assert list(bericht_ordner.iterdir()) == [ziel]


def test_schreibt_bericht_wenn_noch_keiner_existiert(tmp_path, monkeypatch):
    kandidat = _kandidat(tmp_path, schwelle=0.42)
    gold_wurzel = _leere_gold_wurzel(tmp_path)
    bericht_ordner = tmp_path / "reports"

    monkeypatch.setattr(
        osd_modell_leser, "baue_modell_leser",
        lambda _kandidat, _schwelle: (lambda _bild: {"meter": None}))

    rc = goldmessung.main([
        "--kandidat", str(kandidat),
        "--gold-wurzel", str(gold_wurzel),
        "--bericht-ordner", str(bericht_ordner),
    ])

    assert rc == 0
    ziel = bericht_ordner / "osd_modell_goldmessung_osd_zeichen_test.json"
    assert ziel.is_file()
    bericht = json.loads(ziel.read_text(encoding="utf-8"))
    assert bericht["schema"] == "osd_modell_goldmessung_v1"
    assert bericht["kandidat_id"] == "osd_zeichen_test"
    assert bericht["schwelle"] == 0.42
    assert bericht["gesamt"] == {
        "bilder": 0, "richtig": 0, "falsch": 0, "nicht_gelesen": 0, "ohne_sollwert": 0,
    }
    assert bericht["freigabe_erreicht"] is False
    assert [s["satz"] for s in bericht["saetze"]] == list(osd_goldmessung.SAETZE)
    # Atomar geschrieben, keine Arbeitsdatei liegen geblieben.
    assert list(bericht_ordner.iterdir()) == [ziel]


# ---------------------------------------------------------------------------
# Fix-Runde 1 (Aufgabe 4 + Aufgabe 6): Lesercode-Bindung und ohne_sollwert in
# der Summe, damit sich die Tabelle IMMER zu "bilder" aufsummiert.
# ---------------------------------------------------------------------------

def test_bericht_bindet_den_lesercode(tmp_path, monkeypatch):
    kandidat = _kandidat(tmp_path, schwelle=0.42)
    gold_wurzel = _leere_gold_wurzel(tmp_path)
    bericht_ordner = tmp_path / "reports"

    monkeypatch.setattr(
        osd_modell_leser, "baue_modell_leser",
        lambda _kandidat, _schwelle: (lambda _bild: {"meter": None}))

    rc = goldmessung.main([
        "--kandidat", str(kandidat),
        "--gold-wurzel", str(gold_wurzel),
        "--bericht-ordner", str(bericht_ordner),
    ])

    assert rc == 0
    ziel = bericht_ordner / "osd_modell_goldmessung_osd_zeichen_test.json"
    bericht = json.loads(ziel.read_text(encoding="utf-8"))
    erwartet = osd_modell_leser.code_hashes()
    assert bericht["code_sha256"] == erwartet
    assert set(erwartet) == {"osd_modell_leser.py", "osd_modell.py", "osd_meter.py"}


def test_gesamt_zaehlt_ohne_sollwert_mit_und_tabelle_summiert_sich(tmp_path, monkeypatch, capsys):
    kandidat = _kandidat(tmp_path, schwelle=0.3)
    gold_wurzel = tmp_path / "gold"
    _goldsatz_ohne_sollwert(gold_wurzel, osd_goldmessung.SAETZE[0])
    for name in osd_goldmessung.SAETZE[1:]:
        _leerer_goldsatz(gold_wurzel, name)

    monkeypatch.setattr(
        osd_modell_leser, "baue_modell_leser",
        lambda _kandidat, _schwelle: (lambda _bild: {"meter": 3.5}))

    rc = goldmessung.main([
        "--kandidat", str(kandidat),
        "--gold-wurzel", str(gold_wurzel),
        "--bericht-ordner", str(tmp_path / "reports"),
    ])

    assert rc == 0
    ziel = (tmp_path / "reports") / "osd_modell_goldmessung_osd_zeichen_test.json"
    bericht = json.loads(ziel.read_text(encoding="utf-8"))
    gesamt = bericht["gesamt"]
    assert gesamt["ohne_sollwert"] == 1
    # Die Tabelle muss sich immer zu bilder aufsummieren.
    assert (gesamt["richtig"] + gesamt["falsch"] + gesamt["nicht_gelesen"]
            + gesamt["ohne_sollwert"]) == gesamt["bilder"]

    ausgabe = capsys.readouterr().out
    assert "ohne Soll" in ausgabe


# ---------------------------------------------------------------------------
# Reine Freigabe-Logik: nur BEIDE Bedingungen gemeinsam gelten als erreicht.
# ---------------------------------------------------------------------------

def test_freigabe_erreicht_bei_null_falsch_und_genug_richtig():
    assert goldmessung.freigabe_erreicht(
        {"falsch": 0, "richtig": goldmessung.FREIGABE_MINDEST_RICHTIG}) is True


def test_freigabe_nicht_erreicht_bei_zu_wenig_richtig():
    assert goldmessung.freigabe_erreicht(
        {"falsch": 0, "richtig": goldmessung.FREIGABE_MINDEST_RICHTIG - 1}) is False


def test_freigabe_nicht_erreicht_trotz_genug_richtig_bei_einem_falschen():
    assert goldmessung.freigabe_erreicht(
        {"falsch": 1, "richtig": 197}) is False


def test_freigabe_erreicht_mit_deutlich_mehr_als_der_mindestzahl():
    assert goldmessung.freigabe_erreicht({"falsch": 0, "richtig": 197}) is True
