"""Sperren und Manifestbau von train_osd_zeichen.py (Ruling zu Aufgabe 5).

Kein Test hier startet einen echten Sidecar oder ruehrt eine GPU an:
sidecar_laeuft() und freier_vram_mb() sind reine, modulweite Funktionen und
werden per monkeypatch ersetzt. Ein echtes Training findet in diesen Tests
nicht statt - das passiert bewusst erst spaeter, wenn der Datensatz existiert.
"""

import hashlib
import sys
from pathlib import Path

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import train_osd_zeichen
from sidecar import osd_meter


class _VerbotenesModul:
    """Steht in sys.modules['ultralytics'] und knallt bei jedem Zugriff.

    Wird ein Guard (Sidecar laeuft/zu wenig VRAM/data.yaml fehlt) korrekt
    VOR ``from ultralytics import YOLO`` ausgeloest, wird dieses Attribut nie
    gelesen. Wuerde main() den Import trotzdem erreichen, faellt der Test mit
    genau dieser AssertionError durch - kein echtes ultralytics noetig.
    """

    def __getattr__(self, name):
        raise AssertionError(
            "ultralytics haette hier nicht importiert werden duerfen "
            f"(Zugriff auf '{name}')")


def _ohne_ultralytics(monkeypatch):
    monkeypatch.setitem(sys.modules, "ultralytics", _VerbotenesModul())


# ---------------------------------------------------------------------------
# 1. Sperre gegen laufenden Sidecar
# ---------------------------------------------------------------------------

def test_main_verweigert_bei_laufendem_sidecar(tmp_path, monkeypatch):
    monkeypatch.setattr(train_osd_zeichen, "sidecar_laeuft", lambda: True)
    monkeypatch.setattr(train_osd_zeichen, "freier_vram_mb", lambda: 20000)
    _ohne_ultralytics(monkeypatch)

    rc = train_osd_zeichen.main(["--datensatz", str(tmp_path)])

    assert rc == 2


def test_main_verweigert_bei_laufendem_sidecar_ruehrt_ultralytics_nicht_an(
    tmp_path, monkeypatch, capsys,
):
    monkeypatch.setattr(train_osd_zeichen, "sidecar_laeuft", lambda: True)
    monkeypatch.setattr(train_osd_zeichen, "freier_vram_mb", lambda: 20000)
    _ohne_ultralytics(monkeypatch)

    rc = train_osd_zeichen.main(["--datensatz", str(tmp_path)])

    assert rc == 2
    ausgabe = capsys.readouterr()
    assert "Sidecar laeuft" in ausgabe.err


# ---------------------------------------------------------------------------
# 2. Sperre gegen zu wenig VRAM
# ---------------------------------------------------------------------------

def test_main_verweigert_bei_zu_wenig_vram(tmp_path, monkeypatch, capsys):
    monkeypatch.setattr(train_osd_zeichen, "sidecar_laeuft", lambda: False)
    monkeypatch.setattr(
        train_osd_zeichen, "freier_vram_mb",
        lambda: train_osd_zeichen.MIN_FREIER_VRAM_MB - 1)
    _ohne_ultralytics(monkeypatch)

    rc = train_osd_zeichen.main(["--datensatz", str(tmp_path)])

    assert rc == 2
    assert "VRAM" in capsys.readouterr().err


def test_main_verweigert_genau_an_der_schwelle_minus_eins(tmp_path, monkeypatch):
    # Randfall: exakt die Mindestmenge minus 1 MB muss noch sperren.
    monkeypatch.setattr(train_osd_zeichen, "sidecar_laeuft", lambda: False)
    monkeypatch.setattr(train_osd_zeichen, "freier_vram_mb", lambda: 7999)
    _ohne_ultralytics(monkeypatch)

    rc = train_osd_zeichen.main(["--datensatz", str(tmp_path)])

    assert rc == 2


# ---------------------------------------------------------------------------
# 3. Fehlende data.yaml
# ---------------------------------------------------------------------------

def test_main_verweigert_bei_fehlender_data_yaml(tmp_path, monkeypatch, capsys):
    monkeypatch.setattr(train_osd_zeichen, "sidecar_laeuft", lambda: False)
    monkeypatch.setattr(train_osd_zeichen, "freier_vram_mb", lambda: 20000)
    _ohne_ultralytics(monkeypatch)

    rc = train_osd_zeichen.main(["--datensatz", str(tmp_path)])

    assert rc == 2
    assert "data.yaml fehlt" in capsys.readouterr().err


# ---------------------------------------------------------------------------
# 4. Manifestbau (rein, ohne Training)
# ---------------------------------------------------------------------------

def test_baue_manifest_felder(tmp_path):
    gewicht = tmp_path / "best.pt"
    gewicht.write_bytes(b"fake-gewichtsbytes-fuer-den-test-12345")
    basis = tmp_path / "basis.pt"
    basis.write_bytes(b"fake-basisgewicht-bytes-98765")
    beleg = tmp_path / "datensatz.json"
    beleg.write_bytes(b'{"schema": "osd_datensatz_v1", "splits": {"train": 1}}')

    erwarteter_gewicht_hash = hashlib.sha256(gewicht.read_bytes()).hexdigest()
    erwarteter_basis_hash = hashlib.sha256(basis.read_bytes()).hexdigest()
    erwarteter_beleg_hash = hashlib.sha256(beleg.read_bytes()).hexdigest()

    manifest = train_osd_zeichen.baue_manifest(
        kandidat_id="osd_zeichen_testhash123",
        gewicht_pfad=gewicht,
        basis_pfad=basis,
        imgsz=320,
        datensatz=tmp_path / "datensatz",
        datensatz_beleg_pfad=beleg,
    )

    assert manifest["status"] == "diagnostic_not_deployed"
    assert manifest["schwelle"] is None
    assert manifest["klassen"] == list(osd_meter.ZEICHEN)
    assert len(manifest["klassen"]) == 15
    assert manifest["gewicht_sha256"] == erwarteter_gewicht_hash
    # Fix-Runde 1: basis_sha256 bindet die tatsaechlichen Basisgewicht-Bytes,
    # datensatz_receipt_sha256 bindet datensatz.json (nicht mehr data.yaml -
    # data.yaml ist fuer jeden Datensatz bytegleich und damit kein Beleg).
    assert manifest["basis_pfad"] == str(basis)
    assert manifest["basis_sha256"] == erwarteter_basis_hash
    assert manifest["datensatz_receipt_sha256"] == erwarteter_beleg_hash
    assert "datensatz_yaml_sha256" not in manifest


def test_baue_manifest_erkennt_veraenderte_gewichtsbytes(tmp_path):
    # Der Hash kommt aus den tatsaechlichen Bytes, nicht aus einem
    # mitgegebenen Wert - zwei verschiedene Dateien ergeben verschiedene
    # Hashes.
    gewicht_a = tmp_path / "a.pt"
    gewicht_a.write_bytes(b"inhalt-a")
    gewicht_b = tmp_path / "b.pt"
    gewicht_b.write_bytes(b"inhalt-b")
    basis = tmp_path / "basis.pt"
    basis.write_bytes(b"basis-inhalt")
    beleg = tmp_path / "datensatz.json"
    beleg.write_bytes(b"{}")

    manifest_a = train_osd_zeichen.baue_manifest(
        "id_a", gewicht_a, basis, 320, tmp_path, beleg)
    manifest_b = train_osd_zeichen.baue_manifest(
        "id_b", gewicht_b, basis, 320, tmp_path, beleg)

    assert manifest_a["gewicht_sha256"] != manifest_b["gewicht_sha256"]


def test_baue_manifest_basis_hash_stimmt_mit_echten_bytes_ueberein(tmp_path):
    # Fix-Runde 1b: der Basis-Hash kommt aus den tatsaechlichen Bytes der
    # Datei, nicht aus einem blossen Dateinamen oder einem mitgegebenen Wert.
    gewicht = tmp_path / "g.pt"
    gewicht.write_bytes(b"gewicht")
    beleg = tmp_path / "datensatz.json"
    beleg.write_bytes(b"{}")
    basis_a = tmp_path / "basis_a.pt"
    basis_a.write_bytes(b"echte-basisgewicht-bytes-a")
    basis_b = tmp_path / "basis_b.pt"
    basis_b.write_bytes(b"echte-basisgewicht-bytes-b")

    manifest_a = train_osd_zeichen.baue_manifest(
        "id", gewicht, basis_a, 320, tmp_path, beleg)
    manifest_b = train_osd_zeichen.baue_manifest(
        "id", gewicht, basis_b, 320, tmp_path, beleg)

    assert manifest_a["basis_sha256"] == hashlib.sha256(
        basis_a.read_bytes()).hexdigest()
    assert manifest_a["basis_sha256"] != manifest_b["basis_sha256"]


def test_baue_manifest_beleg_hash_stimmt_mit_echten_bytes_ueberein(tmp_path):
    # Fix-Runde 1a: der Beleg-Hash kommt aus den tatsaechlichen Bytes von
    # datensatz.json - zwei verschiedene Belege ergeben verschiedene Hashes.
    gewicht = tmp_path / "g.pt"
    gewicht.write_bytes(b"gewicht")
    basis = tmp_path / "basis.pt"
    basis.write_bytes(b"basis")
    beleg_a = tmp_path / "a.json"
    beleg_a.write_bytes(b'{"splits": {"train": 1}}')
    beleg_b = tmp_path / "b.json"
    beleg_b.write_bytes(b'{"splits": {"train": 2}}')

    manifest_a = train_osd_zeichen.baue_manifest(
        "id", gewicht, basis, 320, tmp_path, beleg_a)
    manifest_b = train_osd_zeichen.baue_manifest(
        "id", gewicht, basis, 320, tmp_path, beleg_b)

    assert manifest_a["datensatz_receipt_sha256"] == hashlib.sha256(
        beleg_a.read_bytes()).hexdigest()
    assert manifest_a["datensatz_receipt_sha256"] != manifest_b["datensatz_receipt_sha256"]


# ---------------------------------------------------------------------------
# 5. freier_vram_mb(): None statt Ausnahme, und None blockiert NICHT
# ---------------------------------------------------------------------------

def test_freier_vram_mb_liefert_none_ohne_nvidia_smi(monkeypatch):
    def _boom(*args, **kwargs):
        raise FileNotFoundError("nvidia-smi nicht gefunden")

    monkeypatch.setattr(train_osd_zeichen.subprocess, "run", _boom)

    assert train_osd_zeichen.freier_vram_mb() is None


def test_freier_vram_mb_liefert_none_bei_fehlgeschlagenem_aufruf(monkeypatch):
    import subprocess as subprocess_modul

    def _fehlschlag(*args, **kwargs):
        raise subprocess_modul.CalledProcessError(1, "nvidia-smi")

    monkeypatch.setattr(train_osd_zeichen.subprocess, "run", _fehlschlag)

    assert train_osd_zeichen.freier_vram_mb() is None


def test_main_blockiert_nicht_bei_unbekanntem_vram(tmp_path, monkeypatch, capsys):
    """Unbekannter VRAM (None) ist NICHT dasselbe wie zu wenig VRAM.

    Ohne data.yaml unter --datensatz bricht main() erst NACH der
    VRAM-Pruefung ab. Erscheint die data.yaml-Meldung (statt der
    VRAM-Meldung), hat der None-Wert die Sperre nachweislich nicht
    ausgeloest.
    """
    monkeypatch.setattr(train_osd_zeichen, "sidecar_laeuft", lambda: False)
    monkeypatch.setattr(train_osd_zeichen, "freier_vram_mb", lambda: None)
    _ohne_ultralytics(monkeypatch)

    rc = train_osd_zeichen.main(["--datensatz", str(tmp_path)])

    ausgabe = capsys.readouterr()
    assert rc == 2
    assert "VRAM" not in ausgabe.err
    assert "data.yaml fehlt" in ausgabe.err


# ---------------------------------------------------------------------------
# 6. Fehlender Datensatz-Beleg bricht den Lauf ab (Fix-Runde 1a)
# ---------------------------------------------------------------------------

def _datensatz_mit_data_yaml(ordner: Path) -> Path:
    ordner.mkdir(parents=True, exist_ok=True)
    (ordner / "data.yaml").write_text("nc: 15\n", encoding="utf-8")
    return ordner


def test_main_verweigert_bei_fehlendem_datensatz_beleg(tmp_path, monkeypatch, capsys):
    monkeypatch.setattr(train_osd_zeichen, "sidecar_laeuft", lambda: False)
    monkeypatch.setattr(train_osd_zeichen, "freier_vram_mb", lambda: 20000)
    _ohne_ultralytics(monkeypatch)
    datensatz = _datensatz_mit_data_yaml(tmp_path / "datensatz")
    # datensatz.json bewusst NICHT angelegt - data.yaml allein reicht nicht.

    rc = train_osd_zeichen.main(["--datensatz", str(datensatz)])

    assert rc == 2
    assert "datensatz.json fehlt" in capsys.readouterr().err


# ---------------------------------------------------------------------------
# 7. Fehlendes Basisgewicht bricht den Lauf ab (Fix-Runde 1b)
# ---------------------------------------------------------------------------

def test_main_verweigert_bei_fehlendem_basisgewicht(tmp_path, monkeypatch, capsys):
    monkeypatch.setattr(train_osd_zeichen, "sidecar_laeuft", lambda: False)
    monkeypatch.setattr(train_osd_zeichen, "freier_vram_mb", lambda: 20000)
    _ohne_ultralytics(monkeypatch)
    datensatz = _datensatz_mit_data_yaml(tmp_path / "datensatz")
    (datensatz / "datensatz.json").write_text("{}", encoding="utf-8")
    fehlende_basis = tmp_path / "nicht_vorhanden.pt"

    rc = train_osd_zeichen.main([
        "--datensatz", str(datensatz), "--basis", str(fehlende_basis)])

    assert rc == 2
    assert "Basisgewicht fehlt" in capsys.readouterr().err


def test_laufzeit_yaml_traegt_absoluten_pfad(tmp_path):
    """Ultralytics loest ein relatives path: gegen das Arbeitsverzeichnis auf.

    Beim ersten echten Trainingslauf am 2026-08-16 suchte es deshalb
    <Repo>/images/val statt im Datensatzordner und brach ab.
    """
    ds = tmp_path / "datensatz_v1"
    ds.mkdir()
    (ds / "data.yaml").write_text(
        "path: .\ntrain: images/train\nval: images/val\nnc: 15\nnames:\n  0: '0'\n",
        encoding="utf-8")

    laufzeit = train_osd_zeichen.schreibe_laufzeit_yaml(ds)
    text = laufzeit.read_text(encoding="utf-8")

    assert laufzeit.name == "data.runtime.yaml"
    assert f"path: {ds.resolve().as_posix()}" in text
    assert "path: ." not in text
    assert "train: images/train" in text and "val: images/val" in text
    # Die data.yaml des Datensatzes ist gehasht und bleibt unveraendert.
    assert (ds / "data.yaml").read_text(encoding="utf-8").startswith("path: .")


def test_label_caches_werden_entfernt(tmp_path):
    ds = tmp_path / "datensatz_v1"
    (ds / "labels").mkdir(parents=True)
    for name in ("train.cache", "val.cache"):
        (ds / "labels" / name).write_bytes(b"x")
    (ds / "labels" / "bleibt.txt").write_text("0 0.5 0.5 0.1 0.4\n", encoding="utf-8")

    train_osd_zeichen.entferne_label_caches(ds)

    assert not (ds / "labels" / "train.cache").exists()
    assert not (ds / "labels" / "val.cache").exists()
    assert (ds / "labels" / "bleibt.txt").is_file()
