"""Sperrliste fuer das OSD-Zeichentraining.

Die drei eingefrorenen Goldsaetze sind die Messgrundlage. Kommt eines ihrer
Bilder - oder auch nur dieselbe Haltung in der Gegenrichtung - ins Training,
misst die Goldmessung hinterher sich selbst. Diese Datei ist die einzige
Wahrheit darueber, was gesperrt ist; kein anderes Skript baut eigene Regeln.
"""

from __future__ import annotations

import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

SKRIPTE = Path(__file__).resolve().parent
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

from osd_wahrheit_aus_protokoll import haltungsvarianten, physische_haltung

GOLD_WURZEL = Path(r"C:\KI_BRAIN\eval_set\osd")
SAETZE = ("osd_sd_v1", "osd_hd_v1", "osd_hd2_v1")


@dataclass(frozen=True)
class Schutz:
    bild_hashes: frozenset[str] = field(default_factory=frozenset)
    haltungen: frozenset[str] = field(default_factory=frozenset)

    def ist_gesperrt(self, bild_sha256: str, haltung: str | None) -> bool:
        if bild_sha256 and bild_sha256.lower() in self.bild_hashes:
            return True
        if haltung and physische_haltung(haltung) in self.haltungen:
            return True
        return False


def lade_schutz(gold_wurzel: Path = GOLD_WURZEL,
                saetze: tuple[str, ...] = SAETZE) -> Schutz:
    """Liest die Manifeste. Fail-closed: fehlt etwas, bricht der Lauf ab."""
    hashes: set[str] = set()
    haltungen: set[str] = set()

    for name in saetze:
        manifest = gold_wurzel / name / "manifest.json"
        if not manifest.is_file():
            raise SystemExit(f"Goldmanifest fehlt: {manifest}")

        daten = json.loads(manifest.read_text(encoding="utf-8-sig"))
        eintraege = daten.get("eintraege") or []
        if not eintraege:
            raise SystemExit(f"Goldmanifest ohne Eintraege: {manifest}")

        for eintrag in eintraege:
            roh = str(eintrag.get("bild_sha256") or "").strip().lower()
            if len(roh) != 64:
                raise SystemExit(
                    f"Eintrag ohne gueltigen Bildhash in {manifest}: "
                    f"{eintrag.get('datei')!r}")
            hashes.add(roh)

            haltung = eintrag.get("haltung")
            if haltung:
                # Beide Richtungen sperren, nicht nur die notierte.
                for variante in haltungsvarianten(str(haltung)):
                    haltungen.add(physische_haltung(variante))

    return Schutz(frozenset(hashes), frozenset(haltungen))
