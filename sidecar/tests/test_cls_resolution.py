"""GPU-freie Tests fuer die cls-Modell-Aufloesung (active.json-Weg) + Letterbox.

Kein Modell wird geladen — getestet wird nur _resolve_cls_model/_letterbox_rgb:
  - active.json mit korrektem SHA-256 -> Quelle "active.json", Metadaten uebernommen
  - SHA-Mismatch / fehlende Gewichte -> None (KEIN stilles Laden falscher Gewichte)
  - expliziter Override (yolo_cls_model_path) -> Quelle "configured"
  - Letterbox: proportional + schwarz gepaddet, kein Crop
"""

import hashlib
import json

import numpy as np
import pytest
from PIL import Image

from sidecar.config import settings
from sidecar.models import yolo_wrapper


def _write_weights(path, content=b"fake-weights"):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)
    return hashlib.sha256(content).hexdigest()


def _write_active(models_dir, entry):
    (models_dir / "active.json").write_text(
        json.dumps({"classifier": entry}), encoding="utf-8")


@pytest.fixture
def models_dir(tmp_path, monkeypatch):
    monkeypatch.setattr(settings, "models_dir", str(tmp_path))
    monkeypatch.setattr(settings, "yolo_cls_model_path", "")
    return tmp_path


def test_active_json_mit_korrektem_hash_gewinnt(models_dir):
    sha = _write_weights(models_dir / "cls" / "weights" / "best.pt")
    _write_active(models_dir, {
        "name": "vsa_cls_v5_nocrop",
        "weights_path": str(models_dir / "cls" / "weights" / "best.pt"),
        "sha256": sha,
        "imgsz": 1024,
        "preprocessing": "letterbox",
    })

    meta = yolo_wrapper._resolve_cls_model()

    assert meta is not None
    assert meta["source"] == "active.json"
    assert meta["name"] == "vsa_cls_v5_nocrop"
    assert meta["imgsz"] == 1024
    assert meta["preprocessing"] == "letterbox"
    assert meta["sha256"] == sha


def test_active_json_sha_mismatch_blockiert(models_dir):
    _write_weights(models_dir / "cls" / "weights" / "best.pt")
    _write_active(models_dir, {
        "weights_path": str(models_dir / "cls" / "weights" / "best.pt"),
        "sha256": "deadbeef" * 8,
    })

    assert yolo_wrapper._resolve_cls_model() is None


def test_active_json_fehlende_gewichte_blockiert(models_dir):
    _write_active(models_dir, {
        "weights_path": str(models_dir / "gibt-es-nicht.pt"),
        "sha256": "",
    })

    assert yolo_wrapper._resolve_cls_model() is None


def test_configured_override_ohne_active_json(models_dir, monkeypatch):
    sha = _write_weights(models_dir / "override" / "weights" / "best.pt")
    monkeypatch.setattr(settings, "yolo_cls_model_path",
                        str(models_dir / "override" / "weights" / "best.pt"))
    monkeypatch.setattr(settings, "yolo_cls_imgsz", 512)
    monkeypatch.setattr(settings, "yolo_cls_preprocessing", "letterbox")

    meta = yolo_wrapper._resolve_cls_model()

    assert meta is not None
    assert meta["source"] == "configured"
    assert meta["imgsz"] == 512
    assert meta["sha256"] == sha


def test_letterbox_proportional_und_gepaddet():
    img = Image.new("RGB", (640, 480), (200, 100, 50))
    lb = yolo_wrapper._letterbox_rgb(img, 224)

    assert lb.size == (224, 224)
    arr = np.asarray(lb)
    # 640x480 -> Skalierung 0.35 -> Inhalt 224x168, oben/unten je 28px schwarz
    assert (arr[0, :, :] == 0).all()        # oberer Rand schwarz
    assert (arr[-1, :, :] == 0).all()       # unterer Rand schwarz
    assert (arr[112, :, :] != 0).any()      # Mitte enthaelt Bild
    assert (arr[112, 0] == (200, 100, 50)).all()  # KEIN seitlicher Crop: linke Kante erhalten


def test_letterbox_passthrough_bei_zielgroesse():
    img = Image.new("RGB", (224, 224), (1, 2, 3))
    assert yolo_wrapper._letterbox_rgb(img, 224) is img
