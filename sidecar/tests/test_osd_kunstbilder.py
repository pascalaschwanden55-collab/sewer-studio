"""Kuenstliche OSD-Anzeigen (Spec Abschnitt 4.2).

Die Wahrheit ist per Konstruktion exakt: Wir wissen, welches Zeichen wir wohin
gemalt haben. Damit lassen sich genau die Stile abdecken, die der heutige Leser
NICHT liest - die Luecke, die die Lehrer-Ernte prinzipiell nicht schliessen kann.
"""

import json
import sys
from pathlib import Path

import pytest

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_kunstbilder


def test_gleiche_saat_liefert_gleiche_bytes():
    erst = osd_kunstbilder.erzeuge(saat=42)
    zweit = osd_kunstbilder.erzeuge(saat=42)

    assert erst.text == zweit.text
    assert erst.zeichen == zweit.zeichen
    assert erst.bild.tobytes() == zweit.bild.tobytes()


def test_andere_saat_liefert_anderen_text():
    texte = {osd_kunstbilder.erzeuge(saat=n).text for n in range(20)}

    assert len(texte) > 1, "Der Erzeuger liefert immer dasselbe."


def test_labels_liegen_im_bild_und_kennen_gueltige_klassen():
    from sidecar import osd_meter

    for saat in range(10):
        kunst = osd_kunstbilder.erzeuge(saat=saat)
        assert kunst.zeichen, "Ein Bild ohne Zeichen ist nutzlos."
        for klasse, x, y, b, h in kunst.zeichen:
            assert 0 <= klasse < len(osd_meter.ZEICHEN)
            assert 0.0 <= x - b / 2 and x + b / 2 <= 1.0
            assert 0.0 <= y - h / 2 and y + h / 2 <= 1.0


def test_zeichenzahl_passt_zum_text():
    for saat in range(10):
        kunst = osd_kunstbilder.erzeuge(saat=saat)
        assert len(kunst.zeichen) == len(kunst.text.replace(" ", ""))


def test_alle_stile_werden_erzeugt():
    stile = {osd_kunstbilder.erzeuge(saat=n).stil_name for n in range(200)}

    assert stile == {s.name for s in osd_kunstbilder.STILE}


# ---------------------------------------------------------------------------
# Fix-Runde 1 (2026-08-15): `saat` ist laut Schnittstelle ein beliebiger int -
# `_video_hintergrund` gab negative Werte ungeprueft an `np.random.default_rng`
# weiter, das nur nicht-negative Startwerte akzeptiert. Entscheidung: der
# Startwert wird intern vorzeichenunabhaengig abgeleitet (`saat & 0xFFFFFFFF`);
# `erzeuge()` bleibt dadurch fuer JEDEN int deterministisch, ohne eine stille
# Nichtnegativitaetsbedingung an Aufrufer weiterzugeben.
# ---------------------------------------------------------------------------

def test_negativer_saat_erzeugt_sauber_ein_bild():
    from sidecar import osd_meter

    kunst = osd_kunstbilder.erzeuge(saat=-3)

    assert kunst.zeichen, "Auch bei negativer Saat muss ein Bild entstehen."
    for klasse, x, y, b, h in kunst.zeichen:
        assert 0 <= klasse < len(osd_meter.ZEICHEN)
        assert 0.0 <= x - b / 2 and x + b / 2 <= 1.0
        assert 0.0 <= y - h / 2 and y + h / 2 <= 1.0

    # Determinismus gilt auch fuer negative Saaten - dieselbe Garantie wie
    # test_gleiche_saat_liefert_gleiche_bytes, nur mit negativem Vorzeichen.
    wiederholt = osd_kunstbilder.erzeuge(saat=-3)
    assert kunst.bild.tobytes() == wiederholt.bild.tobytes()
    assert kunst.zeichen == wiederholt.zeichen


# ---------------------------------------------------------------------------
# Fix-Runde 2 (2026-08-15): Der Fix aus Fix-Runde 1 legte eine zweite Falle
# frei. `random.Random(int)` nimmt in CPython intern den Betrag des
# Startwerts - erzeuge(-n) und erzeuge(n) liefern deshalb denselben Text und
# dieselben Zeichenboxen. Vor dem Fix-Runde-1-Fix war das unerreichbar (ein
# negativer Startwert stuerzte sofort ab); jetzt liefe er still durch und
# wuerde Dubletten in den Datensatz einschleusen. Entscheidung: erzeuge()
# bleibt fuer negative Werte tolerant (siehe oben) - dokumentiertes Verhalten
# einer reinen Funktion -, aber main() weist negative --saat klar ab.
# ---------------------------------------------------------------------------

def test_negativer_saat_kollidiert_mit_positivem_gleichen_betrags():
    """Dokumentiert die Kollision, damit main() nie wieder ungeprueft
    negative Saaten zulaesst, ohne dass es hier auffaellt."""
    negativ = osd_kunstbilder.erzeuge(saat=-2)
    positiv = osd_kunstbilder.erzeuge(saat=2)

    assert negativ.text == positiv.text
    assert negativ.zeichen == positiv.zeichen
    # Nur der Hintergrund unterscheidet sich (separat maskierter Startwert
    # in _video_hintergrund) - die Bildbytes sind deshalb NICHT gleich.
    assert negativ.bild.tobytes() != positiv.bild.tobytes()


def test_main_lehnt_negativen_saat_ab(tmp_path, capsys):
    ziel = tmp_path / "ziel"

    with pytest.raises(SystemExit) as ausnahme:
        osd_kunstbilder.main(
            ["--ziel", str(ziel), "--anzahl", "3", "--saat", "-2"])

    assert ausnahme.value.code == 2
    assert not ziel.exists(), "Bei abgewiesenem --saat darf nichts geschrieben werden."
    ausgabe = capsys.readouterr().err
    assert "--saat" in ausgabe


# ---------------------------------------------------------------------------
# CLI-nahe reine Logik (Ruling zu Aufgabe 3: main() liest/schreibt Dateien,
# die folgenden Bausteine sind dateisystemfrei und werden deshalb direkt
# geprueft; der Ordner-Scan selbst wird bewusst nicht separat getestet).
# ---------------------------------------------------------------------------

def test_kunst_id_ist_reproduzierbar_und_kollisionsfrei():
    assert osd_kunstbilder.kunst_id(42) == "kunst_00000042"
    assert osd_kunstbilder.kunst_id(0) == "kunst_00000000"
    # Verschiedene Saaten duerfen nie auf dieselbe Kennung fallen.
    kennungen = {osd_kunstbilder.kunst_id(n) for n in range(50)}
    assert len(kennungen) == 50


def test_eintrag_erzeugen_liefert_erwartete_feldform():
    kunst = osd_kunstbilder.erzeuge(saat=5)

    eintrag = osd_kunstbilder.eintrag_erzeugen("kunst_00000005", kunst, "ab" * 32)

    assert eintrag == {
        "id": "kunst_00000005",
        "bild_sha256": "ab" * 32,
        "haltung": None,
        "text": kunst.text,
        "meter": kunst.meter,
        "stil": kunst.stil_name,
    }
    # Ein kuenstliches Bild gehoert per Definition zu keiner Haltung.
    assert eintrag["haltung"] is None


# ---------------------------------------------------------------------------
# main(): Schreibweg, Determinismus und Schema von eintraege.json.
# ---------------------------------------------------------------------------

def test_main_schreibt_bilder_labels_und_eintraege(tmp_path):
    ziel = tmp_path / "ziel"

    rc = osd_kunstbilder.main(
        ["--ziel", str(ziel), "--anzahl", "3", "--saat", "10"])

    assert rc == 0
    for saat in (10, 11, 12):
        kennung = osd_kunstbilder.kunst_id(saat)
        assert (ziel / "bilder" / f"{kennung}.png").is_file()
        assert (ziel / "labels" / f"{kennung}.txt").is_file()

    dokument = json.loads((ziel / "eintraege.json").read_text(encoding="utf-8"))
    assert dokument["schema"] == "osd_kunstbilder_v1"
    assert len(dokument["eintraege"]) == 3
    for eintrag in dokument["eintraege"]:
        assert eintrag["haltung"] is None
        assert eintrag["stil"] in {s.name for s in osd_kunstbilder.STILE}
        assert len(eintrag["bild_sha256"]) == 64
        assert isinstance(eintrag["meter"], float)


def test_main_ist_deterministisch_bei_gleicher_saat(tmp_path):
    args_a = ["--ziel", str(tmp_path / "a"), "--anzahl", "4", "--saat", "3"]
    args_b = ["--ziel", str(tmp_path / "b"), "--anzahl", "4", "--saat", "3"]

    osd_kunstbilder.main(args_a)
    osd_kunstbilder.main(args_b)

    eintraege_a = (tmp_path / "a" / "eintraege.json").read_text(encoding="utf-8")
    eintraege_b = (tmp_path / "b" / "eintraege.json").read_text(encoding="utf-8")
    assert eintraege_a == eintraege_b

    for saat in range(3, 7):
        kennung = osd_kunstbilder.kunst_id(saat)
        bild_a = (tmp_path / "a" / "bilder" / f"{kennung}.png").read_bytes()
        bild_b = (tmp_path / "b" / "bilder" / f"{kennung}.png").read_bytes()
        assert bild_a == bild_b


def test_main_nutzt_hintergrund_ordner_deterministisch(tmp_path):
    from PIL import Image

    hintergrund_ordner = tmp_path / "hintergruende"
    hintergrund_ordner.mkdir()
    Image.new("RGB", (50, 50), (80, 60, 40)).save(hintergrund_ordner / "h1.png")
    Image.new("RGB", (50, 50), (40, 60, 80)).save(hintergrund_ordner / "h2.jpg")

    ziel_a = tmp_path / "ziel_a"
    ziel_b = tmp_path / "ziel_b"
    for ziel in (ziel_a, ziel_b):
        rc = osd_kunstbilder.main([
            "--ziel", str(ziel), "--anzahl", "3", "--saat", "0",
            "--hintergrund-ordner", str(hintergrund_ordner),
        ])
        assert rc == 0

    kennung = osd_kunstbilder.kunst_id(0)
    bild_a = (ziel_a / "bilder" / f"{kennung}.png").read_bytes()
    bild_b = (ziel_b / "bilder" / f"{kennung}.png").read_bytes()
    assert bild_a == bild_b
