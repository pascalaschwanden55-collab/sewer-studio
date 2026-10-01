#!/usr/bin/env python3
"""Prototyp/Diagnose-CLI: guenstiger OSD-Meterleser (keine Produkt-Abhaengigkeit).

Die Leser-Logik liegt seit der Sidecar-Anbindung in
`sidecar/sidecar/osd_meter.py` und wird von dort hierher re-exportiert, damit
Diagnose und Sidecar nicht auseinanderlaufen. Diese Datei bleibt die CLI:
Bilder oeffnen, Ergebnisse ausgeben, optional Debug-Overlays schreiben.

--debug schreibt die Zone mit eingezeichneten Zeichenboxen und erkannter
Zeichenfolge je Bild nach <out>/debug/.
--format erzwingt das Zahlenlayout (Format-Lock): ein_dezimal | vierziffern.
Ohne Angabe gilt auto (beide bekannten Formen, bisheriges Verhalten).
Ein lokal vorhandenes Tesseract dient nur als enger Rueckfall fuer die
vollstaendige Vierziffern-Form; fehlt es, bleibt der bisherige Weg erhalten.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "sidecar"))

from sidecar.osd_meter import (  # noqa: E402,F401  (Re-Export fuer Diagnose und Copilot)
    FORMATE,
    boxen_aus_maske,
    glyphenmaske,
    klassifiziere,
    lese_meter as _lese_meter_bild,
    parse_meter,
    plausibilisiere_sequenz,
    rendere_templates,
)


def lese_meter(pfad: Path, templates, debug_dir: Path | None = None,
               format: str | None = None) -> dict:
    """Pfad-Huelle um sidecar.osd_meter.lese_meter: oeffnet das Bild."""
    with Image.open(pfad) as img:
        img.load()
        bild = img.convert("RGB")
    ergebnis = _lese_meter_bild(
        bild, templates, format=format,
        debug_dir=debug_dir, debug_name=pfad.name if debug_dir else None)
    return {"bild": pfad.name, **ergebnis}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("bilder", type=Path, nargs="+")
    parser.add_argument("--debug", type=Path, default=None)
    parser.add_argument("--format", choices=FORMATE, default=None,
                        help="Zahlenlayout erzwingen (Format-Lock pro Video)")
    args = parser.parse_args(argv)

    templates = rendere_templates()
    ergebnisse = []
    for pfad in args.bilder:
        e = lese_meter(pfad, templates, args.debug, format=args.format)
        ergebnisse.append(e)
        print(f"{e['bild'][:44]:46s} '{e['zeichenfolge']}'  meter={e['meter']}  "
              f"glyphen={e['glyphen']}  kmin={e['konfidenz_min']}")
    if args.debug:
        (args.debug / "ergebnis.json").write_text(
            json.dumps(ergebnisse, ensure_ascii=False, indent=1), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
