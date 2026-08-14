"""Tests fuer die Sperrdatei-Sicherheitspruefung (Gesamtaudit 2026-08-14, P1-1).

GPU- und netzfrei: prueft nur das Aufteilen der Sperrdatei und den Abgleich der Funde
gegen die Ausnahmeliste. Der eigentliche pip-audit-Aufruf braucht Netz und laeuft in
der CI, nicht hier.
"""

import json
import sys
from pathlib import Path

SECURITY = Path(__file__).resolve().parents[1] / "security"
sys.path.insert(0, str(SECURITY))

import audit_lock  # noqa: E402


def test_lokale_cuda_und_git_pins_werden_nicht_geprueft_aber_gemeldet():
    inhalt = "\n".join([
        "# Kommentar",
        "",
        "requests==2.34.2",
        "torch==2.12.0.dev20260408+cu128",
        "SAM-2 @ git+https://github.com/facebookresearch/sam2.git@2b90b9f5",
        "pillow==12.3.0",
    ])

    pruefbar, uebersprungen = audit_lock.lock_aufteilen(inhalt)

    assert pruefbar == ["requests==2.34.2", "pillow==12.3.0"]
    # nicht still weggelassen: beide unpruefbaren Zeilen bleiben sichtbar
    assert len(uebersprungen) == 2
    assert any("+cu128" in z for z in uebersprungen)
    assert any("git+" in z for z in uebersprungen)


def test_neue_luecke_ohne_ausnahme_ist_ein_fehler():
    funde = [{"paket": "requests", "version": "2.34.2", "id": "PYSEC-9999-1", "fix": ["2.35.0"]}]

    unerlaubt, veraltet = audit_lock.bewerte(funde, ausnahmen=[])

    assert unerlaubt == funde
    assert veraltet == []


def test_belegte_ausnahme_wird_akzeptiert():
    funde = [{"paket": "transformers", "version": "4.57.6", "id": "PYSEC-2026-2288", "fix": ["5.0.0"]}]
    ausnahmen = [{"paket": "transformers", "id": "PYSEC-2026-2288"}]

    unerlaubt, veraltet = audit_lock.bewerte(funde, ausnahmen)

    assert unerlaubt == []
    assert veraltet == []


def test_veraltete_ausnahme_ist_ebenfalls_ein_fehler():
    """Wenn die Luecke behoben ist, soll die CI zum Aktualisieren zwingen."""
    ausnahmen = [{"paket": "setuptools", "id": "PYSEC-2026-3447"}]

    unerlaubt, veraltet = audit_lock.bewerte(funde=[], ausnahmen=ausnahmen)

    assert unerlaubt == []
    assert veraltet == ausnahmen


def test_gross_kleinschreibung_des_paketnamens_spielt_keine_rolle():
    funde = [{"paket": "Pillow", "version": "12.3.0", "id": "PYSEC-1", "fix": []}]
    ausnahmen = [{"paket": "pillow", "id": "PYSEC-1"}]

    unerlaubt, veraltet = audit_lock.bewerte(funde, ausnahmen)

    assert (unerlaubt, veraltet) == ([], [])


def test_echte_ausnahmedatei_ist_lesbar_und_vollstaendig_belegt():
    dokument = json.loads((SECURITY / "lock_audit_exceptions.json").read_text(encoding="utf-8"))

    assert dokument["ausnahmen"], "Ausnahmeliste darf nicht leer erfunden werden"
    for eintrag in dokument["ausnahmen"]:
        # jede Ausnahme braucht Paket, ID, Grund und Beleg - sonst ist sie nicht pruefbar
        for feld in ("paket", "id", "grund", "beleg"):
            assert eintrag.get(feld), f"{eintrag.get('id')}: Feld '{feld}' fehlt"


def test_echte_sperrdatei_enthaelt_die_erwarteten_unpruefbaren_pins():
    inhalt = (SECURITY.parent / "requirements-lock.txt").read_text(encoding="utf-8")

    pruefbar, uebersprungen = audit_lock.lock_aufteilen(inhalt)

    assert len(pruefbar) > 50
    namen = " ".join(uebersprungen).lower()
    assert "torch" in namen and "sam-2" in namen
