"""Sicherer, standardmaessig inaktiver Sidecar-Anschluss des OSD-Modells."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from types import SimpleNamespace

import pytest
from PIL import Image

from sidecar import osd_meter
from sidecar.config import SidecarSettings, settings
from sidecar.gpu_manager import ModelSlot, gpu_manager
from sidecar.models import osd_model_wrapper


def _kandidat(tmp_path: Path, monkeypatch, status: str = "diagnostic_not_deployed"):
    kandidat_id = "osd_test_fest"
    kandidat = tmp_path / kandidat_id
    gewicht = kandidat / "weights" / "best.pt"
    gewicht.parent.mkdir(parents=True)
    gewicht.write_bytes(b"osd-testgewicht")
    gewicht_sha = hashlib.sha256(gewicht.read_bytes()).hexdigest()
    manifest = {
        "schema": "osd_zeichen_kandidat_v1",
        "kandidat_id": kandidat_id,
        "status": status,
        "gewicht_datei": "weights/best.pt",
        "gewicht_sha256": gewicht_sha,
        "klassen": list(osd_meter.ZEICHEN),
        "imgsz": 320,
        "schwelle": 0.25,
    }
    (kandidat / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    monkeypatch.setattr(settings, "training_model_candidates_root", str(tmp_path))
    monkeypatch.setattr(osd_model_wrapper, "KANDIDAT_ID", kandidat_id)
    monkeypatch.setattr(osd_model_wrapper, "GEWICHT_SHA256", gewicht_sha)
    return kandidat, gewicht_sha


def test_modell_rueckfall_ist_standardmaessig_aus():
    assert SidecarSettings().osd_model_fallback_enabled is False


def test_kandidat_bindet_id_status_gewicht_schwelle_und_klassen(tmp_path, monkeypatch):
    kandidat, gewicht_sha = _kandidat(tmp_path, monkeypatch)

    gelesen = osd_model_wrapper.lade_kandidat()

    assert gelesen.candidate_id == kandidat.name
    assert gelesen.weights_sha256 == gewicht_sha
    assert gelesen.imgsz == 320


def test_nicht_diagnostischer_status_bleibt_vor_der_freigabe_gesperrt(
    tmp_path, monkeypatch,
):
    _kandidat(tmp_path, monkeypatch, status="deployed")

    with pytest.raises(osd_model_wrapper.OsdModelCandidateError, match="weichen ab"):
        osd_model_wrapper.lade_kandidat()


class _Liste:
    def __init__(self, werte):
        self._werte = werte

    def tolist(self):
        return self._werte


class _FakeModell:
    names = {index: zeichen for index, zeichen in enumerate(osd_meter.ZEICHEN)}

    def predict(self, **_kwargs):
        assert ModelSlot.YOLO_OSD in gpu_manager.busy_snapshot()
        boxen = SimpleNamespace(
            cls=_Liste([0, 0, 7]),
            xywhn=_Liste([
                [0.2, 0.5, 0.1, 0.3],
                [0.5, 0.5, 0.1, 0.3],
                [0.8, 0.5, 0.1, 0.3],
            ]),
            conf=_Liste([0.9, 0.9, 0.9]),
        )
        return [SimpleNamespace(boxes=boxen)]


def test_inferenz_verwendet_eigenen_osd_slot(monkeypatch):
    kandidat = osd_model_wrapper.OsdModelCandidate(
        osd_model_wrapper.KANDIDAT_ID,
        Path("nicht_gelesen.pt"),
        osd_model_wrapper.GEWICHT_SHA256,
        320,
    )
    monkeypatch.setattr(osd_model_wrapper, "lade_kandidat", lambda: kandidat)
    monkeypatch.setattr(osd_model_wrapper, "_geraet", lambda: "cpu")
    monkeypatch.setattr(
        osd_model_wrapper, "_lade_modell", lambda _kandidat, _device: (_FakeModell(), None))
    monkeypatch.setattr(
        osd_model_wrapper.osd_meter,
        "glyphenmaske",
        lambda _bild: (None, "dunkel"),
    )

    try:
        ergebnis = osd_model_wrapper.lese(Image.new("RGB", (640, 480)))

        assert ergebnis["meter"] == pytest.approx(0.7)
        assert ergebnis["leseweg"] == "modell"
        assert "yolo_osd" in gpu_manager.get_status()["loaded_models"]
    finally:
        gpu_manager.unload(ModelSlot.YOLO_OSD)
