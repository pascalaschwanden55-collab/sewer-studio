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
    erwarteter_hash = hashlib.sha256(gewicht.read_bytes()).hexdigest()

    manifest = train_osd_zeichen.baue_manifest(
        kandidat_id="osd_zeichen_testhash123",
        gewicht_pfad=gewicht,
        basis="yolo26n.pt",
        imgsz=320,
        datensatz=tmp_path / "datensatz",
        datensatz_yaml_sha256="ab" * 32,
    )

    assert manifest["status"] == "diagnostic_not_deployed"
    assert manifest["schwelle"] is None
    assert manifest["klassen"] == list(osd_meter.ZEICHEN)
    assert len(manifest["klassen"]) == 15
    assert manifest["gewicht_sha256"] == erwarteter_hash


def test_baue_manifest_erkennt_veraenderte_gewichtsbytes(tmp_path):
    # Der Hash kommt aus den tatsaechlichen Bytes, nicht aus einem
    # mitgegebenen Wert - zwei verschiedene Dateien ergeben verschiedene
    # Hashes.
    gewicht_a = tmp_path / "a.pt"
    gewicht_a.write_bytes(b"inhalt-a")
    gewicht_b = tmp_path / "b.pt"
    gewicht_b.write_bytes(b"inhalt-b")

    manifest_a = train_osd_zeichen.baue_manifest(
        "id_a", gewicht_a, "basis.pt", 320, tmp_path, "00" * 32)
    manifest_b = train_osd_zeichen.baue_manifest(
        "id_b", gewicht_b, "basis.pt", 320, tmp_path, "00" * 32)

    assert manifest_a["gewicht_sha256"] != manifest_b["gewicht_sha256"]


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
