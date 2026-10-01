"""Vierter, stilgemischter Goldsatz: Ziehung und Einfrieren.

Der Grund fuer dieses Werkzeug steht im Moduldocstring von osd_goldsatz.py.
Hier wird das geprueft, was schiefgehen kann, ohne dass es auffaellt:

- Die Ziehung darf NICHT nach Lesbarkeit auswaehlen (sonst entsteht genau die
  Verzerrung neu, die der Satz messen soll) und muss trotzdem wiederholbar
  sein.
- Gesperrt ist dreifach: Gold, Reservebestand UND Trainingsmaterial. Faellt
  eine der drei Sperren aus, misst der Satz hinterher Auswendiggelerntes.
- Eine physische Haltung liefert genau ein Bild, Gegenrichtung eingeschlossen.
- Das Einfrieren ist die letzte Gelegenheit, einen Fehler zu bemerken:
  unvollstaendige Ablesung, veraenderte Bildbytes, schon vorhandener Satz.
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest
from PIL import Image

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_goldsatz
from osd_schutz import Schutz


def _bild(pfad: Path, farbe=(20, 20, 20)) -> str:
    """Legt ein Bild an und liefert seinen SHA-256."""
    pfad.parent.mkdir(parents=True, exist_ok=True)
    Image.new("RGB", (64, 48), farbe).save(pfad, "JPEG", quality=92)
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


def _archiv(wurzel: Path, haltungen: dict[str, int]) -> dict[str, list[str]]:
    """Baut <wurzel>/<Haltung>/<n>.jpg. Liefert Haltung -> Hashes.

    Die Farben liegen weit auseinander: Zwei benachbarte Grauwerte runden bei
    JPEG-Quantisierung auf bytegleiche Dateien, und dann prueft ein Test ueber
    Bildhashes stillschweigend etwas anderes als gemeint.
    """
    hashes: dict[str, list[str]] = {}
    for name, anzahl in haltungen.items():
        hashes[name] = [
            _bild(wurzel / name / f"v_{i:03d}.jpg",
                  ((20 + i * 23) % 256, (40 + i * 61) % 256, (90 + i * 37) % 256))
            for i in range(anzahl)
        ]
        assert len(set(hashes[name])) == anzahl, "Testbilder muessen sich unterscheiden"
    return hashes


# ---------------------------------------------------------------------------
# freie_haltungen() - die dreifache Sperre
# ---------------------------------------------------------------------------

def test_freie_haltungen_fasst_gegenrichtung_zusammen(tmp_path):
    _archiv(tmp_path, {"100-200": 2, "200-100": 3})

    frei = osd_goldsatz.freie_haltungen(tmp_path, Schutz(), set())

    assert len(frei) == 1, "Gegenrichtung ist dieselbe physische Haltung"
    assert len(next(iter(frei.values()))) == 5, "beide Ordner liefern Bilder"


def test_freie_haltungen_sperrt_gold_reserve_und_training(tmp_path):
    _archiv(tmp_path, {"1-2": 1, "3-4": 1, "5-6": 1, "7-8": 1})
    schutz = Schutz(haltungen_gold=frozenset({"1-2"}),
                    haltungen_reserve=frozenset({"3-4"}))

    frei = osd_goldsatz.freie_haltungen(tmp_path, schutz, {"5-6"})

    assert set(frei) == {"7-8"}


def test_freie_haltungen_sperrt_gesperrten_bildhash_einzeln(tmp_path):
    hashes = _archiv(tmp_path, {"1-2": 2})
    schutz = Schutz(bild_hashes_gold=frozenset({hashes["1-2"][0]}))

    frei = osd_goldsatz.freie_haltungen(tmp_path, schutz, set())

    # Das Goldbild selbst faellt weg - die Haltung ist ueber ihren Namen
    # ohnehin schon gesperrt, sobald der Schutz sie kennt; hier ist nur der
    # Hash bekannt, und dann darf zumindest dieses Bild nicht mitkommen.
    assert sum(len(v) for v in frei.values()) == 1


def test_freie_haltungen_uebergeht_ordner_ohne_haltungsform(tmp_path):
    _archiv(tmp_path, {"1-2": 1})
    _bild(tmp_path / "irgendwas" / "v_000.jpg")

    frei = osd_goldsatz.freie_haltungen(tmp_path, Schutz(), set())

    assert set(frei) == {"1-2"}


def test_freie_haltungen_folgt_keiner_verknuepfung(tmp_path, monkeypatch):
    _archiv(tmp_path, {"1-2": 1})
    monkeypatch.setattr(Path, "is_symlink", lambda self: self.name == "1-2")

    assert osd_goldsatz.freie_haltungen(tmp_path, Schutz(), set()) == {}


# ---------------------------------------------------------------------------
# trainings_haltungen() - fail-closed
# ---------------------------------------------------------------------------

def test_trainings_haltungen_vereinigt_quellen_und_normalisiert(tmp_path):
    a = tmp_path / "ernte.json"
    b = tmp_path / "hand.json"
    a.write_text(json.dumps([{"haltung": "100-200"}]), encoding="utf-8")
    b.write_text(json.dumps({"eintraege": [{"haltung": "200-100"},
                                           {"haltung": "300-400"}]}),
                 encoding="utf-8")

    haltungen = osd_goldsatz.trainings_haltungen((a, b))

    assert haltungen == {"100-200", "300-400"}


def test_trainings_haltungen_bricht_bei_fehlender_quelle_ab(tmp_path):
    with pytest.raises(SystemExit):
        osd_goldsatz.trainings_haltungen((tmp_path / "fehlt.json",))


def test_trainings_haltungen_bricht_bei_leerer_quelle_ab(tmp_path):
    leer = tmp_path / "leer.json"
    leer.write_text("[]", encoding="utf-8")

    with pytest.raises(SystemExit):
        osd_goldsatz.trainings_haltungen((leer,))


# ---------------------------------------------------------------------------
# ziehe() - wiederholbar, eine Haltung ein Bild, KEINE Lesung im Spiel
# ---------------------------------------------------------------------------

def test_ziehe_ist_bei_gleicher_saat_identisch(tmp_path):
    _archiv(tmp_path, {f"{i}-{i+1}": 4 for i in range(0, 20, 2)})
    frei = osd_goldsatz.freie_haltungen(tmp_path, Schutz(), set())

    a = osd_goldsatz.ziehe(frei, 5, saat=7)
    b = osd_goldsatz.ziehe(frei, 5, saat=7)

    assert [f["bild_pfad"] for f in a] == [f["bild_pfad"] for f in b]


def test_ziehe_unterscheidet_sich_bei_anderer_saat(tmp_path):
    _archiv(tmp_path, {f"{i}-{i+1}": 6 for i in range(0, 40, 2)})
    frei = osd_goldsatz.freie_haltungen(tmp_path, Schutz(), set())

    a = osd_goldsatz.ziehe(frei, 10, saat=1)
    b = osd_goldsatz.ziehe(frei, 10, saat=2)

    assert [f["bild_pfad"] for f in a] != [f["bild_pfad"] for f in b]


def test_ziehe_nimmt_je_haltung_genau_ein_bild(tmp_path):
    _archiv(tmp_path, {f"{i}-{i+1}": 5 for i in range(0, 12, 2)})
    frei = osd_goldsatz.freie_haltungen(tmp_path, Schutz(), set())

    faelle = osd_goldsatz.ziehe(frei, 6, saat=0)

    assert len({f["haltung"] for f in faelle}) == 6


def test_ziehe_waehlt_nicht_immer_das_erste_bild(tmp_path):
    """Das erste Bild eines Videos zeigt fast immer 0.00 - genau diese
    Verzerrung hat die Handliste schon einmal ruiniert (141 von 200)."""
    _archiv(tmp_path, {f"{i}-{i+1}": 10 for i in range(0, 60, 2)})
    frei = osd_goldsatz.freie_haltungen(tmp_path, Schutz(), set())

    faelle = osd_goldsatz.ziehe(frei, 30, saat=3)
    erste = sum(1 for f in faelle if Path(f["bild_pfad"]).name == "v_000.jpg")

    assert erste <= 8, f"{erste} von 30 sind Anfangsbilder - Auswahl verzerrt"


def test_ziehe_bricht_ab_wenn_zu_wenige_haltungen_frei_sind(tmp_path):
    _archiv(tmp_path, {"1-2": 3})
    frei = osd_goldsatz.freie_haltungen(tmp_path, Schutz(), set())

    with pytest.raises(SystemExit):
        osd_goldsatz.ziehe(frei, 5, saat=0)


# ---------------------------------------------------------------------------
# baue_station() - der Ableseplatz. Keine Lesung darf sichtbar werden.
# ---------------------------------------------------------------------------

def _station(tmp_path, anzahl=3, bilder_je=4):
    quelle = tmp_path / "archiv"
    _archiv(quelle, {f"{i}-{i+1}": bilder_je for i in range(0, anzahl * 2 + 6, 2)})
    frei = osd_goldsatz.freie_haltungen(quelle, Schutz(), set())
    faelle = osd_goldsatz.ziehe(frei, anzahl, saat=0)
    ziel = tmp_path / "station"
    osd_goldsatz.baue_station(faelle, ziel, quelle, saat=0)
    return ziel, faelle


def test_baue_station_legt_ableseplatz_in_hausform_an(tmp_path):
    ziel, faelle = _station(tmp_path)

    assert (ziel / "wahrheit.txt").is_file()
    assert (ziel / "frames" / "f0001.jpg").is_file()
    assert (ziel / "frames" / f"f{len(faelle):04d}.jpg").is_file()
    assert (ziel / "queue.json").is_file()


def test_baue_station_schreibt_offene_zeilen_je_bild(tmp_path):
    ziel, faelle = _station(tmp_path)

    zeilen = [z for z in (ziel / "wahrheit.txt").read_text(encoding="utf-8")
              .splitlines() if z and not z.startswith("#")]

    assert len(zeilen) == len(faelle)
    assert all(z.endswith("=") or z.endswith("= ") for z in zeilen), zeilen


def test_baue_station_zeigt_keine_lesung(tmp_path):
    """leser_ergebnisse.json traegt bewusst NUR Nummer, Haltung, Datei."""
    ziel, _ = _station(tmp_path)

    daten = json.loads((ziel / "leser_ergebnisse.json").read_text(encoding="utf-8"))

    assert daten
    for eintrag in daten:
        assert set(eintrag) == {"nr", "haltung", "datei"}


def test_baue_station_haelt_bildbytes_unveraendert(tmp_path):
    ziel, faelle = _station(tmp_path)

    for nr, fall in enumerate(faelle, start=1):
        kopie = (ziel / "frames" / f"f{nr:04d}.jpg").read_bytes()
        assert hashlib.sha256(kopie).hexdigest() == fall["bild_sha256"]


def test_baue_station_verweigert_bestehendes_ziel(tmp_path):
    ziel, faelle = _station(tmp_path)

    with pytest.raises(SystemExit):
        osd_goldsatz.baue_station(faelle, ziel, tmp_path, saat=0)


# ---------------------------------------------------------------------------
# lese_wahrheit() - '?' heisst "keine Anzeige lesbar", nicht 0
# ---------------------------------------------------------------------------

def test_lese_wahrheit_wandelt_fragezeichen_in_none(tmp_path):
    datei = tmp_path / "wahrheit.txt"
    datei.write_text("# Kopf\n0001 = 12.5\n0002 = ?\n", encoding="utf-8")

    assert osd_goldsatz.lese_wahrheit(datei) == {1: 12.5, 2: None}


def test_lese_wahrheit_meldet_offene_zeile(tmp_path):
    datei = tmp_path / "wahrheit.txt"
    datei.write_text("0001 = 1.0\n0002 = \n", encoding="utf-8")

    with pytest.raises(SystemExit, match="offen"):
        osd_goldsatz.lese_wahrheit(datei)


def test_lese_wahrheit_lehnt_unzahl_ab(tmp_path):
    datei = tmp_path / "wahrheit.txt"
    datei.write_text("0001 = zwei\n", encoding="utf-8")

    with pytest.raises(SystemExit):
        osd_goldsatz.lese_wahrheit(datei)


# ---------------------------------------------------------------------------
# friere_ein() - die letzte Gelegenheit, einen Fehler zu bemerken
# ---------------------------------------------------------------------------

def _abgelesen(ziel: Path, werte: dict[int, str]) -> None:
    zeilen = ["# Kopf"] + [f"{nr:04d} = {wert}" for nr, wert in sorted(werte.items())]
    (ziel / "wahrheit.txt").write_text("\n".join(zeilen) + "\n", encoding="utf-8")


def test_friere_ein_schreibt_manifest_in_goldform(tmp_path):
    station, faelle = _station(tmp_path, anzahl=3)
    _abgelesen(station, {1: "1.5", 2: "?", 3: "20.0"})
    gold = tmp_path / "gold"

    satz = osd_goldsatz.friere_ein(station, gold, "osd_mix", 1)

    manifest = json.loads((satz / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["schema_version"] == 1
    assert manifest["name"] == "osd_mix"
    assert manifest["version"] == 1
    assert manifest["bilder"] == 3
    assert manifest["menschlich_lesbar"] == 2
    eintraege = manifest["eintraege"]
    assert [e["nr"] for e in eintraege] == [1, 2, 3]
    assert eintraege[0]["meter"] == 1.5
    assert eintraege[0]["menschlich_lesbar"] is True
    assert eintraege[1]["meter"] is None
    assert eintraege[1]["menschlich_lesbar"] is False
    assert all(e["haltung"] for e in eintraege)
    for nr, eintrag in enumerate(eintraege, start=1):
        bild = satz / "frames" / eintrag["datei"]
        assert hashlib.sha256(bild.read_bytes()).hexdigest() == eintrag["bild_sha256"]


def test_friere_ein_verweigert_unvollstaendige_ablesung(tmp_path):
    station, _ = _station(tmp_path, anzahl=3)
    _abgelesen(station, {1: "1.5", 2: "2.0"})

    with pytest.raises(SystemExit):
        osd_goldsatz.friere_ein(station, tmp_path / "gold", "osd_mix", 1)


def test_friere_ein_verweigert_veraenderte_bildbytes(tmp_path):
    station, _ = _station(tmp_path, anzahl=3)
    _abgelesen(station, {1: "1.5", 2: "2.0", 3: "3.0"})
    Image.new("RGB", (64, 48), (255, 0, 0)).save(station / "frames" / "f0002.jpg", "JPEG")

    with pytest.raises(SystemExit, match="Bildbytes"):
        osd_goldsatz.friere_ein(station, tmp_path / "gold", "osd_mix", 1)


def test_friere_ein_ueberschreibt_bestehenden_satz_nicht(tmp_path):
    station, _ = _station(tmp_path, anzahl=3)
    _abgelesen(station, {1: "1.5", 2: "2.0", 3: "3.0"})
    gold = tmp_path / "gold"
    osd_goldsatz.friere_ein(station, gold, "osd_mix", 1)

    with pytest.raises(SystemExit):
        osd_goldsatz.friere_ein(station, gold, "osd_mix", 1)


def test_friere_ein_laesst_keinen_arbeitsordner_zurueck(tmp_path):
    station, _ = _station(tmp_path, anzahl=3)
    _abgelesen(station, {1: "1.5", 2: "2.0"})
    gold = tmp_path / "gold"

    with pytest.raises(SystemExit):
        osd_goldsatz.friere_ein(station, gold, "osd_mix", 1)

    assert not list(gold.glob("*.arbeit")) if gold.exists() else True
