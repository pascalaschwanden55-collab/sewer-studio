"""Laedt SAM 2.1 und Grounding DINO wirklich und misst ein wiederholbares Ergebnis.

Wozu
----
Die 571 schnellen Sidecar-Tests laden keine Modellgewichte. Sie koennen deshalb
nicht zeigen, ob ein Wechsel einer Abhaengigkeit den echten Ladeweg bricht.
Besonders heikel ist SAM 2: Es baut sein Modell ueber hydra (compose und
instantiate). Ein reiner Importtest reicht dafuer nicht.

Dieser Nachweis faehrt denselben Weg wie der Sidecar: er laedt die produktiven
Gewichte, segmentiert ein festes Bild an einer festen Box und gibt einen
Fingerabdruck der Maske aus. Vor und nach einer Aenderung aufgerufen, zeigt der
Vergleich sofort, ob sich etwas verschoben hat.

Aufruf
------
    cd sidecar
    $env:PYTHONPATH = "<Repo>\\sidecar"
    .\\.venv\\Scripts\\python.exe ..\\tools\\SidecarModellNachweis\\modell_nachweis.py

Der Sidecar darf dabei nicht laufen (er haelt sonst die GPU-Plaetze).
Rueckgabewert 0 heisst: SAM geladen und segmentiert. 1 heisst: gescheitert.

Belegt am 2026-09-01 beim Wechsel hydra-core 1.3.3 -> 1.3.4:
Maskenfingerabdruck 8619453d19412d3b vor und nach dem Wechsel identisch.
"""
import base64
import hashlib
import io
import json
import sys

import numpy as np
from PIL import Image


def testbild() -> str:
    """Festes Bild ohne Zufall: heller Kreis auf dunklem Grund."""
    hoehe, breite = 480, 640
    yy, xx = np.mgrid[0:hoehe, 0:breite]
    kreis = ((yy - 240) ** 2 + (xx - 320) ** 2) < (120 ** 2)
    bild = np.zeros((hoehe, breite, 3), dtype=np.uint8)
    bild[..., 0] = 40
    bild[..., 1] = 50
    bild[..., 2] = 60
    bild[kreis] = (210, 200, 190)
    puffer = io.BytesIO()
    Image.fromarray(bild).save(puffer, format="PNG")
    return base64.b64encode(puffer.getvalue()).decode("ascii")


def pruefe_sam(bild: str) -> dict:
    from sidecar.models import sam_wrapper
    from sidecar.schemas.detection import BoundingBox

    antwort = sam_wrapper.segment(
        bild, [BoundingBox(x1=200.0, y1=120.0, x2=440.0, y2=360.0)]
    )
    return {
        "status": "ok",
        "degraded": bool(antwort.degraded),
        "angefragte_boxen": antwort.requested_boxes,
        "uebersprungene_boxen": antwort.skipped_boxes,
        "masken": [
            {
                "konfidenz": round(float(m.confidence), 4),
                "maskenpixel": int(m.mask_area_pixels),
                "breite": int(m.width_pixels),
                "hoehe": int(m.height_pixels),
                "rle_sha256": hashlib.sha256(m.mask_rle.encode()).hexdigest()[:16],
            }
            for m in antwort.masks
        ],
    }


def pruefe_dino(bild: str) -> dict:
    from sidecar.models import dino_wrapper

    antwort = dino_wrapper.detect(bild, "pipe . circle . object", 0.30, 0.25)
    return {
        "status": "degraded" if antwort.degraded else "ok",
        "treffer": len(antwort.detections or []),
        "fehler": antwort.error_code,
    }


def main() -> int:
    import hydra

    bild = testbild()
    ergebnis: dict = {"hydra": hydra.__version__}

    try:
        ergebnis["sam"] = pruefe_sam(bild)
    except Exception as exc:  # bewusst breit: der Nachweis soll den Grund zeigen
        ergebnis["sam"] = {"status": "FEHLER", "grund": f"{type(exc).__name__}: {exc}"}

    try:
        ergebnis["dino"] = pruefe_dino(bild)
    except Exception as exc:
        ergebnis["dino"] = {"status": "FEHLER", "grund": f"{type(exc).__name__}: {exc}"}

    print(json.dumps(ergebnis, ensure_ascii=False, indent=2, sort_keys=True))
    return 0 if ergebnis["sam"]["status"] == "ok" else 1


if __name__ == "__main__":
    sys.exit(main())
