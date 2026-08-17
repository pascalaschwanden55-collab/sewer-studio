"""Diagnostischer Laufzeitanschluss des trainierten OSD-Zeichenlesers.

Der Kandidat ist absichtlich fest angeheftet. Weder Client noch Einstellung
koennen ID, Gewicht oder Schwelle austauschen. Das Modell wird erst geladen,
wenn die bisherige OSD-Kette keinen Wert liefert und der getrennte Schalter
aktiv ist. Der Schalter ist standardmaessig aus: Diese Verdrahtung ist noch
keine Produktfreigabe.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import tempfile
import threading
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from .. import osd_meter, osd_modell
from ..config import settings
from ..gpu_manager import ModelSlot, ModelUnloadedError, gpu_manager
from . import yolo_wrapper

KANDIDAT_ID = "osd_zeichen_c668e35d59cb"
GEWICHT_SHA256 = "c668e35d59cb4feba82b60b857663a11ac6f493104d03bf1b0414103a4a75845"
SCHWELLE = 0.25
KANDIDAT_STATUS = "diagnostic_not_deployed"
GEWICHT_DATEI = "weights/best.pt"
_YOLO_CONF = 0.05
_ERWARTETE_KLASSEN = tuple(osd_meter.ZEICHEN)
_predict_lock = threading.Lock()


class OsdModelCandidateError(RuntimeError):
    """Der fest angeheftete Diagnosekandidat ist nicht sicher verwendbar."""


@dataclass(frozen=True)
class OsdModelCandidate:
    candidate_id: str
    weights_path: Path
    weights_sha256: str
    imgsz: int


def _ist_link_oder_junction(pfad: Path) -> bool:
    if pfad.is_symlink():
        return True
    is_junction = getattr(os.path, "isjunction", None)
    return bool(is_junction and is_junction(pfad))


def _sha256(pfad: Path) -> str:
    digest = hashlib.sha256()
    with pfad.open("rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def _unter_wurzel(pfad: Path, wurzel: Path) -> bool:
    try:
        pfad.relative_to(wurzel)
        return True
    except ValueError:
        return False


def lade_kandidat() -> OsdModelCandidate:
    """Prueft Ordner, Manifest und Gewicht des fest angehefteten Kandidaten."""
    try:
        wurzel = Path(settings.training_model_candidates_root).resolve(strict=True)
        kandidat = (wurzel / KANDIDAT_ID).resolve(strict=True)
    except OSError as exc:
        raise OsdModelCandidateError(
            "Der fest angeheftete OSD-Kandidat ist nicht verfuegbar.") from exc

    if (not wurzel.is_dir()
            or _ist_link_oder_junction(wurzel)
            or kandidat.parent != wurzel
            or _ist_link_oder_junction(kandidat)):
        raise OsdModelCandidateError(
            "Der fest angeheftete OSD-Kandidat liegt nicht in einem sicheren Ordner.")

    manifest_pfad = kandidat / "manifest.json"
    gewicht_pfad = kandidat / GEWICHT_DATEI
    if (not manifest_pfad.is_file()
            or not gewicht_pfad.is_file()
            or _ist_link_oder_junction(manifest_pfad)
            or _ist_link_oder_junction(gewicht_pfad)):
        raise OsdModelCandidateError("Manifest oder Gewicht des OSD-Kandidaten fehlt.")

    try:
        manifest = json.loads(manifest_pfad.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise OsdModelCandidateError("Das OSD-Kandidatenmanifest ist nicht lesbar.") from exc
    if not isinstance(manifest, dict):
        raise OsdModelCandidateError("Das OSD-Kandidatenmanifest ist ungueltig.")

    schwelle = manifest.get("schwelle")
    if isinstance(schwelle, bool) or not isinstance(schwelle, (int, float)):
        schwelle = math.nan
    klassen = manifest.get("klassen")
    imgsz = manifest.get("imgsz")
    if (
        manifest.get("schema") != "osd_zeichen_kandidat_v1"
        or manifest.get("kandidat_id") != KANDIDAT_ID
        or manifest.get("status") != KANDIDAT_STATUS
        or manifest.get("gewicht_datei") != GEWICHT_DATEI
        or str(manifest.get("gewicht_sha256", "")).lower() != GEWICHT_SHA256
        or not math.isclose(float(schwelle), SCHWELLE, abs_tol=1e-12)
        or tuple(klassen or ()) != _ERWARTETE_KLASSEN
        or isinstance(imgsz, bool)
        or not isinstance(imgsz, int)
        or imgsz <= 0
    ):
        raise OsdModelCandidateError(
            "ID, Status, Gewicht, Schwelle oder Klassen des OSD-Kandidaten weichen ab.")

    try:
        aufgeloestes_gewicht = gewicht_pfad.resolve(strict=True)
        if (not _unter_wurzel(aufgeloestes_gewicht, kandidat)
                or _sha256(aufgeloestes_gewicht).lower() != GEWICHT_SHA256):
            raise OsdModelCandidateError(
                "Der Gewichtshash des OSD-Kandidaten stimmt nicht.")
    except OSError as exc:
        raise OsdModelCandidateError("Das OSD-Gewicht ist nicht sicher lesbar.") from exc

    return OsdModelCandidate(KANDIDAT_ID, aufgeloestes_gewicht, GEWICHT_SHA256, imgsz)


def _geraet() -> str:
    device = settings.effective_yolo_device
    if device.startswith("cuda") and not yolo_wrapper._cuda_available():
        return "cpu"
    return device


def _normalisierte_namen(rohe_namen: object) -> dict[int, str]:
    if isinstance(rohe_namen, list):
        return {index: str(wert) for index, wert in enumerate(rohe_namen)}
    if isinstance(rohe_namen, dict):
        try:
            return {int(key): str(wert) for key, wert in rohe_namen.items()}
        except (TypeError, ValueError):
            return {}
    return {}


def _lade_modell(kandidat: OsdModelCandidate, device: str):
    from ultralytics import YOLO

    try:
        with tempfile.TemporaryDirectory(prefix="sewerstudio_osd_") as temp:
            snapshot = Path(temp) / "osd.pt"
            digest = hashlib.sha256()
            with kandidat.weights_path.open("rb") as quelle, snapshot.open("xb") as ziel:
                for block in iter(lambda: quelle.read(1 << 20), b""):
                    digest.update(block)
                    ziel.write(block)
            if digest.hexdigest().lower() != kandidat.weights_sha256:
                raise OsdModelCandidateError(
                    "Der Hash des OSD-Gewichts hat sich vor dem Laden geaendert.")

            modell = YOLO(str(snapshot))
            erwartet = {index: wert for index, wert in enumerate(_ERWARTETE_KLASSEN)}
            if _normalisierte_namen(modell.names) != erwartet:
                raise OsdModelCandidateError(
                    "Das OSD-Modell hat nicht die festgelegte Zeichenkarte.")
            if _sha256(snapshot).lower() != kandidat.weights_sha256:
                raise OsdModelCandidateError(
                    "Die private OSD-Modellkopie hat sich beim Laden geaendert.")
            modell.to(device)
            return modell, None
    except OsdModelCandidateError:
        raise
    except OSError as exc:
        raise OsdModelCandidateError(
            "Die gepruefte OSD-Modellkopie konnte nicht erstellt werden.") from exc


def _erkennungen(results: object) -> list[tuple[int, float, float, float, float, float]]:
    if not results:
        return []
    boxen = results[0].boxes
    if boxen is None:
        return []
    return [
        (int(klasse), x, y, breite, hoehe, float(sicherheit))
        for klasse, (x, y, breite, hoehe), sicherheit in zip(
            boxen.cls.tolist(), boxen.xywhn.tolist(), boxen.conf.tolist())
    ]


def lese(bild: Image.Image, format: str | None = None) -> dict:
    """Liest ein PIL-Bild mit dem fest angehefteten Diagnosekandidaten."""
    kandidat = lade_kandidat()
    device = _geraet()
    rgb = bild.convert("RGB")
    ausschnitt, _ = osd_modell.schneide_zone(rgb)
    normiert = osd_modell.normiere_ausschnitt(ausschnitt)
    _, stil = osd_meter.glyphenmaske(rgb)

    with _predict_lock, gpu_manager.busy_slot(ModelSlot.YOLO_OSD):
        state = gpu_manager.ensure_loaded(
            ModelSlot.YOLO_OSD,
            device,
            lambda: _lade_modell(kandidat, device),
            content_id=kandidat.weights_sha256,
        )
        if state.model is None:
            raise ModelUnloadedError(ModelSlot.YOLO_OSD.value)
        results = state.model.predict(
            source=yolo_wrapper._pil_rgb_to_ultralytics_bgr(normiert),
            imgsz=kandidat.imgsz,
            conf=_YOLO_CONF,
            verbose=False,
            save=False,
        )

    return osd_modell.ergebnis_aus_erkennungen(
        _erkennungen(results), stil, SCHWELLE, format)
