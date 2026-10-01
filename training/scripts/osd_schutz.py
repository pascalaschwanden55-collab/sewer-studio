"""Sperrliste fuer das OSD-Zeichentraining.

Die drei eingefrorenen Goldsaetze sind die Messgrundlage. Kommt eines ihrer
Bilder - oder auch nur dieselbe Haltung in der Gegenrichtung - ins Training,
misst die Goldmessung hinterher sich selbst. Diese Datei ist die einzige
Wahrheit darueber, was gesperrt ist; kein anderes Skript baut eigene Regeln.

ZWEITE SPERRQUELLE: DER RESERVEBESTAND (Fix-Runde 1, 2026-08-16)
Die Schwellenkalibrierung (osd_schwelle_kalibrieren.py) braucht einen von
Gold GETRENNTEN Bestand, um die "ein falscher Wert ist teurer als zehn
fehlende"-Schwelle unabhaengig zu bestimmen - sie nimmt dafuer den Testteil
(split == "test") von osd_wahrheit_aus_protokoll.py's 897 schwach
beschrifteten Protokollbildern. Dieser Testteil war bisher NICHT gesperrt:
Der Split, den osd_datensatz.teile_auf() fuer das eigentliche Training
erzeugt, hat nichts mit dem Split in wahrheit.json zu tun. Zeigte die
dokumentierte Ernte auf D:\\Haltungen (denselben Archivbestand, aus dem der
Reservebestand geschnitten wurde), waeren Reserve-Haltungen ungehindert ins
Training gewandert - die Schwelle waere danach an Material kalibriert
worden, das das Modell effektiv auswendig gelernt hat. Deshalb sperrt
lade_schutz() jetzt zusaetzlich jede physische Haltung (beide
Fahrtrichtungen) der Reserve-Testeintraege, genau wie bei Gold. Der
Reservebestand fehlt -> harter Abbruch, kein stiller Uebersprung: ein
ungeschuetzter Reservebestand ist genau die Luecke, die hier geschlossen
wird.
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
# Alle eingefrorenen Messlatten. osd_mix_v1 kam am 2026-08-17 dazu: Er hat die
# Kettenentscheidung (Vorlagenleser plus Modell-Rueckfall) mitbestimmt und ist
# damit genauso verbraucht wie die drei alten Saetze. Ohne diesen Eintrag zoege
# eine neue Ziehung seine Haltungen wieder mit, und ein Training duerfte sie
# verwenden - dann misst die naechste Abnahme sich selbst.
#
# NICHT verwechseln mit osd_goldmessung.SAETZE: Dort stehen bewusst nur die drei
# alten Saetze, weil die Freigabemarke "170 von 197" an ihre Bilderzahl gebunden
# ist. Hier geht es um Sperren, dort um Bewerten.
SAETZE = ("osd_sd_v1", "osd_hd_v1", "osd_hd2_v1", "osd_mix_v1")

# Reservebestand fuer die Schwellenkalibrierung: der Testteil (split ==
# "test") der 897 schwach beschrifteten Protokollbilder aus
# osd_wahrheit_aus_protokoll.py (674 Train, 135 Validation, 88 Test - nur die
# 88 Test-Eintraege werden hier gesperrt). Einzige Wahrheit fuer diesen Pfad;
# osd_schwelle_kalibrieren.py liest denselben Wert von hier statt ihn ein
# zweites Mal zu pflegen.
RESERVEBESTAND_STANDARD = Path(
    r"C:\KI_BRAIN\training\diagnostics\osd_wahrheit_protokoll_v1\wahrheit.json")


@dataclass(frozen=True)
class Schutz:
    """Sperrliste aus zwei getrennten Quellen.

    Getrennt gehalten (nicht in einer gemeinsamen Menge), damit ein Operator
    sehen kann, WARUM ein Bild/eine Haltung gesperrt ist - ueber
    sperrquelle(). ist_gesperrt() bleibt die einfache Ja/Nein-Pruefung fuer
    bestehende Aufrufer und prueft beide Quellen gemeinsam.
    """

    bild_hashes_gold: frozenset[str] = field(default_factory=frozenset)
    haltungen_gold: frozenset[str] = field(default_factory=frozenset)
    bild_hashes_reserve: frozenset[str] = field(default_factory=frozenset)
    haltungen_reserve: frozenset[str] = field(default_factory=frozenset)

    @property
    def bild_hashes(self) -> frozenset[str]:
        return self.bild_hashes_gold | self.bild_hashes_reserve

    @property
    def haltungen(self) -> frozenset[str]:
        return self.haltungen_gold | self.haltungen_reserve

    def ist_gesperrt(self, bild_sha256: str, haltung: str | None) -> bool:
        return self.sperrquelle(bild_sha256, haltung) is not None

    def sperrquelle(self, bild_sha256: str, haltung: str | None) -> str | None:
        """'gold', 'reserve' oder None - erklaert WARUM etwas gesperrt ist.

        Gold wird zuerst geprueft (praktisch irrelevant, da Goldbilder nicht
        im Reservebestand liegen, aber deterministisch statt zufaellig).
        """
        sha = (bild_sha256 or "").lower()
        norm = physische_haltung(haltung) if haltung else None

        if sha and sha in self.bild_hashes_gold:
            return "gold"
        if norm and norm in self.haltungen_gold:
            return "gold"
        if sha and sha in self.bild_hashes_reserve:
            return "reserve"
        if norm and norm in self.haltungen_reserve:
            return "reserve"
        return None


def _lade_gold(gold_wurzel: Path, saetze: tuple[str, ...]) -> tuple[set[str], set[str]]:
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

    return hashes, haltungen


def _lade_reserve_testteil(reservebestand: Path) -> tuple[set[str], set[str]]:
    """Nur split == 'test': Train und Validation duerfen ins Training,
    weil sie die Schwelle nicht mitbestimmen - der Testteil dagegen schon
    und muss deshalb wie Gold gesperrt werden."""
    if not reservebestand.is_file():
        raise SystemExit(f"Reservebestand fehlt: {reservebestand}")

    daten = json.loads(reservebestand.read_text(encoding="utf-8-sig"))
    eintraege = [e for e in (daten.get("eintraege") or []) if e.get("split") == "test"]
    if not eintraege:
        raise SystemExit(f"Reservebestand ohne Testeintraege: {reservebestand}")

    hashes: set[str] = set()
    haltungen: set[str] = set()
    for eintrag in eintraege:
        roh = str(eintrag.get("bild_sha256") or "").strip().lower()
        if len(roh) != 64:
            raise SystemExit(
                f"Testeintrag ohne gueltigen Bildhash in {reservebestand}: "
                f"{eintrag.get('id')!r}")
        hashes.add(roh)

        haltung = eintrag.get("haltung")
        if not haltung:
            raise SystemExit(
                f"Testeintrag ohne Haltung in {reservebestand}: "
                f"{eintrag.get('id')!r}")
        # Beide Richtungen sperren, nicht nur die notierte - derselbe Grund
        # wie bei Gold: die Ernte erkennt eine Haltung anhand ihres
        # Ordnernamens und kennt die im Beleg notierte Fahrtrichtung nicht.
        for variante in haltungsvarianten(str(haltung)):
            haltungen.add(physische_haltung(variante))

    return hashes, haltungen


def lade_schutz(gold_wurzel: Path = GOLD_WURZEL,
                saetze: tuple[str, ...] = SAETZE,
                reservebestand: Path = RESERVEBESTAND_STANDARD) -> Schutz:
    """Liest Goldmanifeste UND den Testteil des Reservebestands.

    Fail-closed: fehlt eines von beidem oder ist es leer, bricht der Lauf ab.
    Ein fehlender Reservebestand darf nie als "keine Sperre noetig"
    interpretiert werden - genau das war die Luecke, die diese Erweiterung
    schliesst (siehe Moduldocstring).
    """
    gold_hashes, gold_haltungen = _lade_gold(gold_wurzel, saetze)
    reserve_hashes, reserve_haltungen = _lade_reserve_testteil(reservebestand)

    return Schutz(
        frozenset(gold_hashes), frozenset(gold_haltungen),
        frozenset(reserve_hashes), frozenset(reserve_haltungen),
    )
