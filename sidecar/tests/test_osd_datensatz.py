"""Datensatzaufbau (Spec Abschnitt 4.4).

Zwei Bilder derselben physischen Haltung duerfen nie ueber train und val
verteilt sein - sonst misst die interne Validierung sich selbst.
"""

import hashlib
import json
import sys
from pathlib import Path

import pytest

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_datensatz
from osd_schutz import Schutz


def test_haltung_landet_nie_in_beiden_teilen():
    gruppen = {f"haltung_{n}": [f"bild_{n}_a", f"bild_{n}_b"] for n in range(20)}

    zuordnung = osd_datensatz.teile_auf(gruppen, val_anteil=0.25, saat=7)

    for schluessel in gruppen:
        assert zuordnung[schluessel] in ("train", "val")
    assert len(zuordnung) == len(gruppen)


def test_aufteilung_ist_bei_gleicher_saat_gleich():
    gruppen = {f"h{n}": [f"b{n}"] for n in range(30)}

    erst = osd_datensatz.teile_auf(gruppen, val_anteil=0.2, saat=3)
    zweit = osd_datensatz.teile_auf(gruppen, val_anteil=0.2, saat=3)

    assert erst == zweit


def test_val_anteil_wird_ungefaehr_getroffen():
    gruppen = {f"h{n}": [f"b{n}"] for n in range(100)}

    zuordnung = osd_datensatz.teile_auf(gruppen, val_anteil=0.2, saat=1)
    val = sum(1 for teil in zuordnung.values() if teil == "val")

    assert 15 <= val <= 25


def test_mindestens_eine_gruppe_bleibt_im_training():
    gruppen = {"nur_eine": ["b1"]}

    zuordnung = osd_datensatz.teile_auf(gruppen, val_anteil=0.9, saat=1)

    assert zuordnung["nur_eine"] == "train"


def test_data_yaml_nennt_alle_15_klassen(tmp_path):
    from sidecar import osd_meter

    pfad = osd_datensatz.schreibe_data_yaml(tmp_path)
    text = pfad.read_text(encoding="utf-8")

    assert "nc: 15" in text
    for zeichen in osd_meter.ZEICHEN:
        assert repr(zeichen) in text or f'"{zeichen}"' in text


def test_bytegleiche_bilder_kommen_nur_einmal_vor():
    eintraege = [
        {"id": "a", "bild_sha256": "11" * 32, "haltung": "10261-10262"},
        {"id": "b", "bild_sha256": "11" * 32, "haltung": "10261-10262"},
        {"id": "c", "bild_sha256": "22" * 32, "haltung": "10261-10262"},
    ]

    gruppen = osd_datensatz.baue_gruppen(eintraege)

    alle = [wert for liste in gruppen.values() for wert in liste]
    assert sorted(alle) == ["a", "c"]


def test_bytegleiche_bilder_verbinden_ihre_haltungen():
    # Dasselbe Bild taucht unter zwei Haltungen auf: Beide muessen in denselben
    # Teil, sonst steht dieselbe Aufnahme in train UND val.
    eintraege = [
        {"id": "a", "bild_sha256": "11" * 32, "haltung": "10261-10262"},
        {"id": "b", "bild_sha256": "11" * 32, "haltung": "77457-77453"},
        {"id": "c", "bild_sha256": "33" * 32, "haltung": "77457-77453"},
    ]

    gruppen = osd_datensatz.baue_gruppen(eintraege)

    schluessel_von = {
        wert: schluessel
        for schluessel, liste in gruppen.items() for wert in liste
    }
    assert schluessel_von["a"] == schluessel_von["c"]


def test_gegenrichtung_landet_in_derselben_gruppe():
    eintraege = [
        {"id": "a", "bild_sha256": "11" * 32, "haltung": "10261-10262"},
        {"id": "b", "bild_sha256": "22" * 32, "haltung": "10262-10261"},
    ]

    gruppen = osd_datensatz.baue_gruppen(eintraege)

    assert len(gruppen) == 1


def test_kunstbilder_ohne_haltung_bilden_eigene_gruppen():
    eintraege = [
        {"id": "k1", "bild_sha256": "11" * 32, "haltung": None},
        {"id": "k2", "bild_sha256": "22" * 32, "haltung": None},
    ]

    gruppen = osd_datensatz.baue_gruppen(eintraege)

    assert len(gruppen) == 2


# ---------------------------------------------------------------------------
# CLI-nahe reine Logik (Ruling zu Aufgabe 4: main() liest/kopiert Dateien,
# die folgenden Bausteine sind dateisystemfrei und werden deshalb direkt
# geprueft; der Ordner-Scan/Kopiervorgang selbst wird ueber die main()-Tests
# unten nur als Ganzes mitgeprueft, nicht separat zerlegt).
# ---------------------------------------------------------------------------

def test_eintraege_aus_dokument_akzeptiert_beide_schemas():
    ernte_doc = {"schema": "osd_ernte_v1", "eintraege": [{"id": "a"}]}
    kunst_doc = {"schema": "osd_kunstbilder_v1", "eintraege": [{"id": "k1"}]}

    assert osd_datensatz.eintraege_aus_dokument(ernte_doc, "q-ernte") == [{"id": "a"}]
    assert osd_datensatz.eintraege_aus_dokument(kunst_doc, "q-kunst") == [{"id": "k1"}]


def test_eintraege_aus_dokument_lehnt_unbekanntes_schema_ab():
    dokument = {"schema": "irgendwas_v9", "eintraege": []}

    with pytest.raises(SystemExit):
        osd_datensatz.eintraege_aus_dokument(dokument, "q-unbekannt")


def test_eintraege_aus_dokument_ohne_eintraege_liste_liefert_leere_liste():
    # eintraege.json mit "eintraege": null (z.B. bei einem Nulltreffer-Lauf)
    # darf nicht mit einem TypeError abbrechen.
    dokument = {"schema": "osd_ernte_v1", "eintraege": None}

    assert osd_datensatz.eintraege_aus_dokument(dokument, "q-leer") == []


def test_pruefe_keine_gesperrten_bricht_bei_treffer_ueber_bildhash_ab():
    schutz = Schutz(frozenset({"aa" * 32}), frozenset())
    eintraege = [{"id": "a", "bild_sha256": "aa" * 32, "haltung": None}]

    with pytest.raises(SystemExit):
        osd_datensatz.pruefe_keine_gesperrten(eintraege, schutz, "q-a")


def test_pruefe_keine_gesperrten_bricht_bei_treffer_ueber_haltung_ab():
    schutz = Schutz(frozenset(), frozenset({"10261-10262"}))
    # Gegenrichtung angegeben - muss trotzdem greifen (Schutz.ist_gesperrt()).
    eintraege = [{"id": "a", "bild_sha256": "bb" * 32, "haltung": "10262-10261"}]

    with pytest.raises(SystemExit):
        osd_datensatz.pruefe_keine_gesperrten(eintraege, schutz, "q-a")


def test_pruefe_keine_gesperrten_laesst_unbeteiligte_eintraege_durch():
    schutz = Schutz(frozenset({"aa" * 32}), frozenset({"10261-10262"}))
    eintraege = [{"id": "b", "bild_sha256": "bb" * 32, "haltung": "20261-20262"}]

    osd_datensatz.pruefe_keine_gesperrten(eintraege, schutz, "q-b")  # kein Fehler


def test_pruefe_eindeutige_ids_bricht_bei_widerspruch_ab():
    # Gleiche id, aber unterschiedlicher Bildinhalt - z.B. zwei Kunstbilder-
    # Laeufe mit gleicher Saat, aber verschiedenem --hintergrund-ordner.
    eintraege = [
        {"id": "kunst_000001", "bild_sha256": "11" * 32},
        {"id": "kunst_000001", "bild_sha256": "22" * 32},
    ]

    with pytest.raises(SystemExit):
        osd_datensatz.pruefe_eindeutige_ids(eintraege)


def test_pruefe_eindeutige_ids_akzeptiert_wiederholte_gleiche_eintraege():
    eintraege = [
        {"id": "a", "bild_sha256": "11" * 32},
        {"id": "a", "bild_sha256": "11" * 32},
        {"id": "b", "bild_sha256": "22" * 32},
    ]

    osd_datensatz.pruefe_eindeutige_ids(eintraege)  # kein Fehler


def test_baue_beleg_liefert_erwartete_feldform():
    quellen = [{"pfad": "q1", "schema": "osd_ernte_v1",
                "eintraege_json_sha256": "ab" * 32, "anzahl_eintraege": 2}]
    id_zu_split = {"a": "train", "b": "train", "c": "val"}

    beleg = osd_datensatz.baue_beleg(quellen, val_anteil=0.2, saat=5,
                                     id_zu_split=id_zu_split)

    assert beleg == {
        "schema": "osd_datensatz_v1",
        "quellen": quellen,
        "val_anteil": 0.2,
        "saat": 5,
        "splits": {"train": 2, "val": 1},
        "bilder_gesamt": 3,
        "labels_gesamt": 3,
    }


# ---------------------------------------------------------------------------
# main(): Schreibweg ueber beide Quellenschemas, Schutz-Zweitpruefung,
# Ziel-Schutz gegen Ueberschreiben und die Kopplung "gleiche Haltung -
# gleicher Teil" auch durch den echten Kopierweg hindurch.
# ---------------------------------------------------------------------------

def _mini_bild():
    from PIL import Image
    return Image.new("RGB", (8, 8), (10, 20, 30))


def _schreibe_quelle(wurzel: Path, name: str, eintraege: list[dict], schema: str) -> Path:
    quelle = wurzel / name
    (quelle / "bilder").mkdir(parents=True)
    (quelle / "labels").mkdir(parents=True)
    for eintrag in eintraege:
        bildpfad = quelle / "bilder" / f"{eintrag['id']}.png"
        _mini_bild().save(bildpfad)
        (quelle / "labels" / f"{eintrag['id']}.txt").write_text(
            "0 0.5 0.5 0.1 0.1\n", encoding="utf-8")
        if schema == osd_datensatz.SCHEMA_ERNTE and "ausschnitt_sha256" not in eintrag:
            # Aufgabe 7: main() prueft jetzt ausschnitt_sha256 gegen die
            # tatsaechlich geschriebene Datei - der Testfixture-Hash muss
            # deshalb echt sein, nicht wie bild_sha256 (Sperrlisten-Feld)
            # frei erfunden werden koennen.
            eintrag["ausschnitt_sha256"] = hashlib.sha256(bildpfad.read_bytes()).hexdigest()
    (quelle / "eintraege.json").write_text(
        json.dumps({"schema": schema, "eintraege": eintraege}, ensure_ascii=False),
        encoding="utf-8")
    return quelle


def test_main_baut_datensatz_aus_beiden_quellenschemas(tmp_path, monkeypatch):
    monkeypatch.setattr(osd_datensatz, "lade_schutz", lambda *_a, **_k: Schutz())

    ernte_eintraege = [
        {"id": f"e{n}", "bild_sha256": f"{n:02x}" * 32,
         "haltung": f"haltung_{n}-ende_{n}", "zeichenfolge": "9", "meter": float(n)}
        for n in range(6)
    ]
    kunst_eintraege = [
        {"id": f"kunst_{n:06d}", "bild_sha256": f"{n + 100:02x}" * 32,
         "haltung": None, "text": "9.4m", "meter": 9.4, "stil": "test"}
        for n in range(4)
    ]
    quellen_wurzel = tmp_path / "quellen"
    ernte = _schreibe_quelle(quellen_wurzel, "ernte", ernte_eintraege, "osd_ernte_v1")
    kunst = _schreibe_quelle(quellen_wurzel, "kunst", kunst_eintraege, "osd_kunstbilder_v1")

    ziel = tmp_path / "ziel"
    rc = osd_datensatz.main([
        "--quelle", str(ernte), "--quelle", str(kunst),
        "--ziel", str(ziel), "--val-anteil", "0.3", "--saat", "1",
    ])

    assert rc == 0
    assert (ziel / "data.yaml").is_file()

    beleg = json.loads((ziel / "datensatz.json").read_text(encoding="utf-8"))
    assert beleg["schema"] == "osd_datensatz_v1"
    assert beleg["val_anteil"] == 0.3
    assert beleg["saat"] == 1
    assert {q["pfad"] for q in beleg["quellen"]} == {str(ernte), str(kunst)}
    assert beleg["bilder_gesamt"] == 10
    assert beleg["splits"]["train"] + beleg["splits"]["val"] == 10

    bilder_train = sorted((ziel / "images" / "train").glob("*.png"))
    bilder_val = sorted((ziel / "images" / "val").glob("*.png"))
    assert len(bilder_train) + len(bilder_val) == 10
    for bild in bilder_train + bilder_val:
        teil = bild.parent.name
        assert (ziel / "labels" / teil / f"{bild.stem}.txt").is_file()


def test_main_haelt_gleiche_haltung_im_selben_teil(tmp_path, monkeypatch):
    """Smoke-Test des echten Kopierwegs fuer die zentrale Split-Regel."""
    monkeypatch.setattr(osd_datensatz, "lade_schutz", lambda *_a, **_k: Schutz())

    eintraege = [
        {"id": "a", "bild_sha256": "11" * 32, "haltung": "100-200"},
        {"id": "b", "bild_sha256": "22" * 32, "haltung": "200-100"},
    ] + [
        {"id": f"c{n}", "bild_sha256": f"{n:02x}" * 32, "haltung": f"h{n}-x{n}"}
        for n in range(8)
    ]
    quelle = _schreibe_quelle(tmp_path / "quellen", "ernte", eintraege, "osd_ernte_v1")

    ziel = tmp_path / "ziel"
    rc = osd_datensatz.main([
        "--quelle", str(quelle), "--ziel", str(ziel),
        "--val-anteil", "0.3", "--saat", "5",
    ])

    assert rc == 0
    a_train = (ziel / "images" / "train" / "a.png").is_file()
    a_val = (ziel / "images" / "val" / "a.png").is_file()
    b_train = (ziel / "images" / "train" / "b.png").is_file()
    b_val = (ziel / "images" / "val" / "b.png").is_file()
    assert (a_train and b_train) or (a_val and b_val), (
        "Gegenrichtungen derselben Haltung sind ueber train/val gestreut.")


def test_main_bricht_bei_geschuetztem_eintrag_ab(tmp_path, monkeypatch):
    schutz = Schutz(frozenset({"11" * 32}), frozenset())
    monkeypatch.setattr(osd_datensatz, "lade_schutz", lambda *_a, **_k: schutz)

    eintraege = [{"id": "a", "bild_sha256": "11" * 32, "haltung": "1-2"}]
    quelle = _schreibe_quelle(tmp_path / "quellen", "ernte", eintraege, "osd_ernte_v1")

    ziel = tmp_path / "ziel"
    with pytest.raises(SystemExit):
        osd_datensatz.main(["--quelle", str(quelle), "--ziel", str(ziel)])

    assert not ziel.exists(), "Bei einem gesperrten Treffer darf kein Ziel entstehen."


# ---------------------------------------------------------------------------
# Fix-Runde 1 (Aufgabe 7): main() muss die tatsaechlich kopierte Datei gegen
# ausschnitt_sha256 pruefen, nicht nur nach Dateiname kopieren.
# ---------------------------------------------------------------------------

def test_pruefe_ausschnitt_hash_bricht_bei_abweichung_ab(tmp_path):
    bild = tmp_path / "bild.png"
    bild.write_bytes(b"echte bytes")
    eintrag = {"id": "a", "ausschnitt_sha256": "ff" * 32}

    with pytest.raises(SystemExit):
        osd_datensatz.pruefe_ausschnitt_hash(eintrag, osd_datensatz.SCHEMA_ERNTE, bild)


def test_pruefe_ausschnitt_hash_bricht_bei_fehlendem_feld_ab(tmp_path):
    bild = tmp_path / "bild.png"
    bild.write_bytes(b"echte bytes")
    eintrag = {"id": "a"}

    with pytest.raises(SystemExit):
        osd_datensatz.pruefe_ausschnitt_hash(eintrag, osd_datensatz.SCHEMA_ERNTE, bild)


def test_pruefe_ausschnitt_hash_akzeptiert_uebereinstimmung(tmp_path):
    bild = tmp_path / "bild.png"
    bild.write_bytes(b"echte bytes")
    eintrag = {"id": "a", "ausschnitt_sha256": hashlib.sha256(b"echte bytes").hexdigest()}

    osd_datensatz.pruefe_ausschnitt_hash(eintrag, osd_datensatz.SCHEMA_ERNTE, bild)  # kein Fehler


def test_pruefe_ausschnitt_hash_uebersprungen_fuer_kunstbilder(tmp_path):
    """osd_kunstbilder_v1 hat kein ausschnitt_sha256-Feld - bild_sha256 IST
    bereits der Hash der geschriebenen Datei, siehe Docstring."""
    bild = tmp_path / "bild.png"
    bild.write_bytes(b"irgendwas")
    eintrag = {"id": "k1", "bild_sha256": "voellig falsch"}

    osd_datensatz.pruefe_ausschnitt_hash(
        eintrag, osd_datensatz.SCHEMA_KUNSTBILDER, bild)  # kein Fehler - nichts geprueft


def test_main_bricht_bei_abweichendem_ausschnitt_hash_ab(tmp_path, monkeypatch):
    """main() darf eine still vertauschte oder beschaedigte Zuschnittdatei
    nicht in den Datensatz kopieren."""
    monkeypatch.setattr(osd_datensatz, "lade_schutz", lambda *_a, **_k: Schutz())

    eintraege = [{"id": "a", "bild_sha256": "11" * 32, "haltung": "1-2",
                  "ausschnitt_sha256": "ff" * 32}]  # bewusst falsch
    quelle = _schreibe_quelle(tmp_path / "quellen", "ernte", eintraege, "osd_ernte_v1")

    ziel = tmp_path / "ziel"
    with pytest.raises(SystemExit):
        osd_datensatz.main(["--quelle", str(quelle), "--ziel", str(ziel)])

    assert not ziel.exists(), "Bei einem Hash-Mismatch darf kein Ziel entstehen."


def test_main_verweigert_nicht_leeres_ziel(tmp_path, monkeypatch):
    monkeypatch.setattr(osd_datensatz, "lade_schutz", lambda *_a, **_k: Schutz())

    eintraege = [{"id": "a", "bild_sha256": "11" * 32, "haltung": "1-2"}]
    quelle = _schreibe_quelle(tmp_path / "quellen", "ernte", eintraege, "osd_ernte_v1")

    ziel = tmp_path / "ziel"
    ziel.mkdir()
    (ziel / "vorhanden.txt").write_text("x", encoding="utf-8")

    with pytest.raises(SystemExit):
        osd_datensatz.main(["--quelle", str(quelle), "--ziel", str(ziel)])

    assert (ziel / "vorhanden.txt").is_file(), (
        "Bestehender Zielinhalt darf nie repariert oder ueberschrieben werden.")


def test_main_akzeptiert_leeres_vorhandenes_ziel(tmp_path, monkeypatch):
    monkeypatch.setattr(osd_datensatz, "lade_schutz", lambda *_a, **_k: Schutz())

    eintraege = [{"id": "a", "bild_sha256": "11" * 32, "haltung": "1-2"}]
    quelle = _schreibe_quelle(tmp_path / "quellen", "ernte", eintraege, "osd_ernte_v1")

    ziel = tmp_path / "ziel"
    ziel.mkdir()  # leerer Ordner - kein bestehender Datensatz

    rc = osd_datensatz.main(["--quelle", str(quelle), "--ziel", str(ziel)])

    assert rc == 0
    assert (ziel / "data.yaml").is_file()


# ---------------------------------------------------------------------------
# Fix-Runde 1 (2026-08-15): SystemExit erbt von BaseException, nicht von
# Exception. Das urspruengliche "except Exception:" um den Kopiervorgang liess
# genau die drei Abbruchpruefungen dort (mehrdeutiges/fehlendes Bild, fehlende
# Labeldatei, Wettlaufpruefung am Ziel) durchschluepfen: main() brach zwar
# korrekt ab und --ziel blieb unangetastet, aber der Staging-Ordner samt
# bereits kopierten Bildern/Labels blieb liegen.
# ---------------------------------------------------------------------------

def test_main_raeumt_staging_bei_fehlender_labeldatei_mitten_im_lauf_auf(tmp_path, monkeypatch):
    monkeypatch.setattr(osd_datensatz, "lade_schutz", lambda *_a, **_k: Schutz())

    eintraege = [
        {"id": "a", "bild_sha256": "11" * 32, "haltung": "1-2"},
        {"id": "b", "bild_sha256": "22" * 32, "haltung": "3-4"},
    ]
    quelle = _schreibe_quelle(tmp_path / "quellen", "ernte", eintraege, "osd_ernte_v1")
    # Labeldatei eines Eintrags erst NACH der Ernte entfernen - simuliert
    # einen Fehler mitten im Kopiervorgang, nachdem bereits mindestens ein
    # anderer Eintrag erfolgreich in den Staging-Ordner kopiert wurde.
    (quelle / "labels" / "b.txt").unlink()

    ziel = tmp_path / "ziel"
    with pytest.raises(SystemExit):
        osd_datensatz.main(["--quelle", str(quelle), "--ziel", str(ziel)])

    assert not ziel.exists(), "Bei einem Abbruch mitten im Lauf darf kein Ziel entstehen."
    staging_reste = list(ziel.parent.glob(f".{ziel.name}.staging-*"))
    assert staging_reste == [], (
        f"Staging-Ordner wurde nach SystemExit nicht aufgeraeumt: {staging_reste}")
