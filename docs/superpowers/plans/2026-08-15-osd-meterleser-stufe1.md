# OSD-Meterleser Stufe 1 — Umsetzungsplan

> **Für agentische Arbeiter:** ERFORDERLICHER TEIL-SKILL: `superpowers:subagent-driven-development`
> (empfohlen) oder `superpowers:executing-plans`, um diesen Plan Aufgabe für Aufgabe
> umzusetzen. Die Schritte nutzen Kästchen-Syntax (`- [ ]`) zur Nachverfolgung.

**Ziel:** Einen 15-Klassen-Zeichen-Detektor für den OSD-Meterstand trainieren und
gegen die eingefrorenen Goldsätze messen — ohne Handbeschriftung und ohne
Sidecar-Einbau.

**Architektur:** Der heutige Vorlagenleser dient als Lehrer und liefert exakt
beschriftete echte Ausschnitte; ein künstlicher Erzeuger deckt die Stile ab, die
der Lehrer nicht liest. Beides wird zu einem YOLO-Datensatz mit 15 Zeichenklassen
zusammengeführt. Die Lesung normiert den Ausschnitt vorher auf feste Zeichenhöhe,
setzt die erkannten Zeichen von links nach rechts zusammen und gibt sie
unverändert an `parse_meter`. Eine Schwelle auf der kleinsten Zeichensicherheit
wird an einem getrennten Reservebestand kalibriert und eingefroren, bevor die
Goldmessung einmal läuft.

**Technik:** Python 3.12, Ultralytics YOLO, Pillow, NumPy, OpenCV — alles bereits
in `sidecar/.venv` vorhanden. Keine neue Abhängigkeit.

**Spec:** `docs/superpowers/specs/2026-08-15-osd-meterleser-modell-design.md`

## Globale Vorgaben

Diese Regeln gelten für **jede** Aufgabe. Sie stammen wörtlich aus der Spec.

- **Null falsch bleibt absolut.** Ein falscher Wert ist teurer als zehn fehlende.
  Im Zweifel `None` liefern, nie raten.
- **`sidecar/sidecar/osd_meter.py` wird in Stufe 1 nicht verändert.** Weder
  `parse_meter` noch `lese_meter` noch irgendeine Konstante. Alles Neue ist
  additiv in neuen Dateien.
- **Zeichenvorrat und Klassenreihenfolge:** exakt `ZEICHEN = "0123456789.mLZ:"`
  aus `osd_meter.py:68`. Klassen-ID = Position in dieser Zeichenkette, also
  `0`→0, `9`→9, `.`→10, `m`→11, `L`→12, `Z`→13, `:`→14. **Kein Minus.**
- **Schutz:** Die 197 Goldbilder (`bild_sha256`) und ihre Haltungen in **beiden
  Fahrtrichtungen** sind aus jeder Trainingsquelle ausgeschlossen.
- **Splits nach physischer Haltung**, nie nach Bild.
- **Die Schwelle wird nie an Gold eingestellt.** Kalibrierung am Reservebestand,
  einfrieren, dann einmal messen.
- **Python-Aufrufe** laufen über `sidecar/.venv/Scripts/python.exe` (OpenCV und
  Ultralytics liegen nur dort).
- **Kundenoriginale werden nie verändert.** Alle Ausgaben landen unter
  `<KnowledgeRoot>/training/osd_zeichen/`.
- **Tests** liegen unter `sidecar/tests/` und laufen ohne GPU, damit die CI sie
  fährt (`pytest -m "not gpu"` im Ordner `sidecar`).

---

## Dateistruktur

| Datei | Verantwortung |
|---|---|
| `training/scripts/osd_schutz.py` | Lädt die Sperrliste aus den drei Goldmanifesten. Einzige Wahrheit darüber, was gesperrt ist. |
| `training/scripts/osd_ernte.py` | Fährt den heutigen Leser über Bilder und schreibt vollständige Lesungen als YOLO-Labels. |
| `training/scripts/osd_kunstbilder.py` | Erzeugt künstliche Anzeigen mit exakter Wahrheit. |
| `training/scripts/osd_datensatz.py` | Führt Ernte und Kunstbilder zu einem YOLO-Datensatz mit Splits zusammen. |
| `training/scripts/train_osd_zeichen.py` | Trainiert den Kandidaten, schreibt Manifest mit Gewichts-Hash. |
| `sidecar/sidecar/osd_modell.py` | Laufzeitteil: Ausschnitt normieren, Boxen zu Zeichenkette, Sicherheitstor. |
| `training/scripts/osd_schwelle_kalibrieren.py` | Bestimmt die Schwelle am Reservebestand und friert sie im Manifest ein. |
| `training/scripts/osd_modell_goldmessung.py` | Misst den Kandidaten mit `messe_satz` aus `osd_goldmessung.py`. |
| `sidecar/tests/test_osd_schutz.py` | Tests zu Aufgabe 1 |
| `sidecar/tests/test_osd_ernte.py` | Tests zu Aufgabe 2 |
| `sidecar/tests/test_osd_kunstbilder.py` | Tests zu Aufgabe 3 |
| `sidecar/tests/test_osd_datensatz.py` | Tests zu Aufgabe 4 |
| `sidecar/tests/test_osd_modell.py` | Tests zu Aufgabe 6 |
| `sidecar/tests/test_osd_schwelle.py` | Tests zu Aufgabe 7 |

Die Trainingsskripte liegen unter `training/scripts/`, ihre Tests aber unter
`sidecar/tests/` — nur dort fährt die CI Python-Tests. Die Testdateien fügen
`training/scripts` per `sys.path` hinzu; dasselbe Verfahren benutzt
`osd_goldmessung.py` in umgekehrter Richtung (`osd_goldmessung.py:116`).

---

## Aufgabe 1: Sperrliste aus den Goldmanifesten

**Dateien:**
- Anlegen: `training/scripts/osd_schutz.py`
- Test: `sidecar/tests/test_osd_schutz.py`

**Schnittstellen:**
- Verbraucht: `haltungsvarianten` und `physische_haltung` aus
  `training/scripts/osd_wahrheit_aus_protokoll.py:55-63`
- Liefert: `lade_schutz(gold_wurzel: Path) -> Schutz` mit den Feldern
  `bild_hashes: set[str]`, `haltungen: set[str]` und der Methode
  `ist_gesperrt(bild_sha256: str, haltung: str | None) -> bool`

- [ ] **Schritt 1: Den scheiternden Test schreiben**

Datei `sidecar/tests/test_osd_schutz.py`:

```python
"""Sperrliste des OSD-Trainings (Spec Abschnitt 4.4).

Die 197 Goldbilder und ihre Haltungen duerfen in keiner Trainingsquelle
auftauchen. Sonst misst die Goldmessung am Ende sich selbst.
"""

import json
import sys
from pathlib import Path

import pytest

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_schutz


def _schreibe_satz(wurzel: Path, name: str, eintraege: list[dict]) -> None:
    satz = wurzel / name
    (satz / "frames").mkdir(parents=True)
    (satz / "manifest.json").write_text(
        json.dumps({"schema_version": 1, "name": name, "eintraege": eintraege}),
        encoding="utf-8")


def test_goldhash_ist_gesperrt(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461",
         "bild_sha256": "aa" * 32, "meter": 0.0},
    ])

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",))

    assert schutz.ist_gesperrt("aa" * 32, None) is True


def test_gegenrichtung_der_haltung_ist_gesperrt(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461",
         "bild_sha256": "aa" * 32, "meter": 0.0},
    ])

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",))

    # Dasselbe Rohr, andere Fahrtrichtung - muss ebenfalls gesperrt sein.
    assert schutz.ist_gesperrt("bb" * 32, "33461-36051") is True


def test_unbeteiligtes_bild_ist_frei(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461",
         "bild_sha256": "aa" * 32, "meter": 0.0},
    ])

    schutz = osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",))

    assert schutz.ist_gesperrt("cc" * 32, "10261-10262") is False


def test_fehlender_satz_bricht_ab(tmp_path):
    with pytest.raises(SystemExit):
        osd_schutz.lade_schutz(tmp_path, saetze=("gibt_es_nicht",))


def test_eintrag_ohne_hash_bricht_ab(tmp_path):
    _schreibe_satz(tmp_path, "osd_sd_v1", [
        {"datei": "f0001.jpg", "haltung": "36051-33461", "meter": 0.0},
    ])

    with pytest.raises(SystemExit):
        osd_schutz.lade_schutz(tmp_path, saetze=("osd_sd_v1",))
```

- [ ] **Schritt 2: Test laufen lassen und Scheitern bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_schutz.py -q
```

Erwartet: FAIL mit `ModuleNotFoundError: No module named 'osd_schutz'`.

- [ ] **Schritt 3: Die minimale Umsetzung schreiben**

Datei `training/scripts/osd_schutz.py`:

```python
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
```

- [ ] **Schritt 4: Test laufen lassen und Bestehen bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_schutz.py -q
```

Erwartet: 5 passed.

- [ ] **Schritt 5: Gegen die echten Goldsätze prüfen**

```bash
sidecar/.venv/Scripts/python.exe -c "import sys; sys.path.insert(0,'training/scripts'); import osd_schutz; s=osd_schutz.lade_schutz(); print(len(s.bild_hashes),'Hashes,',len(s.haltungen),'Haltungen')"
```

Erwartet: 197 Hashes (die Summe aus 95 + 30 + 72) und eine kleinere Zahl
Haltungen. Weicht die Hashzahl ab, stimmt etwas an den Manifesten nicht — dann
stoppen und nachsehen, nicht weitermachen.

- [ ] **Schritt 6: Committen**

```bash
git add training/scripts/osd_schutz.py sidecar/tests/test_osd_schutz.py
git commit -m "feat(osd): Sperrliste der Goldsaetze fuer das Zeichentraining"
```

---

## Aufgabe 2: Lehrer-Ernte

**Dateien:**
- Anlegen: `training/scripts/osd_ernte.py`
- Test: `sidecar/tests/test_osd_ernte.py`

**Schnittstellen:**
- Verbraucht: `osd_schutz.lade_schutz` (Aufgabe 1);
  `osd_meter.glyphenmaske`, `osd_meter.boxen_aus_maske`,
  `osd_meter.klassifiziere`, `osd_meter.get_templates`,
  `osd_meter.parse_meter`, `osd_meter._zeichenfolge_ist_vollstaendig`,
  `osd_meter.ZEICHEN`, `osd_meter.ZONEN`
- Liefert: `ernte_bild(bild, templates, schutz, bild_sha256, haltung) -> Ernteergebnis | None`
  mit `Ernteergebnis(ausschnitt: Image, zeichen: list[tuple[int, float, float, float, float]], zeichenfolge: str, meter: float)`.
  Die Tupel sind YOLO-Labelzeilen: `(klasse_id, x_mitte, y_mitte, breite, hoehe)`,
  alle vier Koordinaten auf den Ausschnitt normiert (0..1).

- [ ] **Schritt 1: Den scheiternden Test schreiben**

Datei `sidecar/tests/test_osd_ernte.py`:

```python
"""Lehrer-Ernte (Spec Abschnitt 4.1).

Nur VOLLSTAENDIGE Lesungen des Vorlagenwegs werden uebernommen. Genau dieser
Zweig hat auf dem gesamten Goldbestand null falsche Werte; eine Bruchstueck-
Lesung dagegen raet den Dezimalpunkt und waere ein falsches Etikett.
"""

import sys
from pathlib import Path

from PIL import Image, ImageDraw

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_ernte
from osd_schutz import Schutz


def _bild_mit_anzeige(text: str, groesse=(720, 576)) -> Image.Image:
    """Dunkles Bild mit heller Anzeige unten rechts - der SD-Normalfall."""
    bild = Image.new("RGB", groesse, (18, 18, 18))
    zeichner = ImageDraw.Draw(bild)
    zeichner.text((groesse[0] - 190, groesse[1] - 40), text, fill=(240, 240, 240))
    return bild


def test_vollstaendige_lesung_liefert_normierte_labels(monkeypatch):
    """Deterministisch: Der Leser wird gestellt, geprueft wird die Umrechnung.

    Bewusst NICHT vom echten Vorlagentreffer abhaengig. Ein Test, der bei
    ausbleibendem Treffer einfach durchlaeuft, ist ein stiller Pass - genau die
    Sorte Test, die im Audit vom 2026-08-14 als wertlos aufgefallen ist.
    """
    from sidecar import osd_meter
    import numpy as np

    bild = _bild_mit_anzeige("LZ1: 9.4m")
    breite, hoehe = bild.size
    links, oben, _r, _u = osd_meter.ZONEN["unten_rechts"]
    x0, y0 = int(links * breite) + 10, int(oben * hoehe) + 10

    monkeypatch.setattr(osd_ernte.osd_meter, "glyphenmaske",
                        lambda _b: (np.zeros((hoehe, breite), dtype="uint8"), "dunkel"))
    monkeypatch.setattr(osd_ernte.osd_meter, "boxen_aus_maske",
                        lambda _m, _s: [(x0, y0, x0 + 12, y0 + 18),
                                        (x0 + 14, y0, x0 + 26, y0 + 18)])
    folge = iter("94")
    monkeypatch.setattr(osd_ernte.osd_meter, "klassifiziere",
                        lambda _g, _t: (next(folge), 0.9))
    monkeypatch.setattr(osd_ernte.osd_meter, "_zeichenfolge_ist_vollstaendig",
                        lambda _z: True)
    monkeypatch.setattr(osd_ernte.osd_meter, "parse_meter",
                        lambda *_a, **_k: 9.4)

    ergebnis = osd_ernte.ernte_bild(bild, None, Schutz(), "ab" * 32, "10261-10262")

    assert ergebnis is not None
    assert ergebnis.zeichenfolge == "94"
    assert len(ergebnis.zeichen) == 2
    for klasse_id, x, y, b, h in ergebnis.zeichen:
        assert 0 <= klasse_id < len(osd_meter.ZEICHEN)
        assert all(0.0 <= wert <= 1.0 for wert in (x, y, b, h))
        assert b > 0 and h > 0
    # Die zweite Box liegt rechts der ersten - die Umrechnung dreht nichts um.
    assert ergebnis.zeichen[0][1] < ergebnis.zeichen[1][1]


def test_unvollstaendige_lesung_wird_verworfen(monkeypatch):
    """Bruchstueck-Lesungen sind Gift als Etikett (58 von 61 grob falsch)."""
    import numpy as np

    bild = _bild_mit_anzeige("9.4")
    breite, hoehe = bild.size

    monkeypatch.setattr(osd_ernte.osd_meter, "glyphenmaske",
                        lambda _b: (np.zeros((hoehe, breite), dtype="uint8"), "dunkel"))
    monkeypatch.setattr(osd_ernte.osd_meter, "boxen_aus_maske",
                        lambda _m, _s: [(500, 500, 512, 518)])
    monkeypatch.setattr(osd_ernte.osd_meter, "klassifiziere",
                        lambda _g, _t: ("9", 0.9))
    monkeypatch.setattr(osd_ernte.osd_meter, "_zeichenfolge_ist_vollstaendig",
                        lambda _z: False)

    ergebnis = osd_ernte.ernte_bild(bild, None, Schutz(), "ab" * 32, "10261-10262")

    assert ergebnis is None


def test_gesperrtes_bild_wird_uebersprungen():
    from sidecar import osd_meter
    bild = _bild_mit_anzeige("LZ1: 9.4m")
    schutz = Schutz(frozenset({"ab" * 32}), frozenset())

    ergebnis = osd_ernte.ernte_bild(
        bild, osd_meter.get_templates(), schutz, "ab" * 32, "10261-10262")

    assert ergebnis is None


def test_gesperrte_haltung_wird_uebersprungen():
    from sidecar import osd_meter
    bild = _bild_mit_anzeige("LZ1: 9.4m")
    schutz = Schutz(frozenset(), frozenset({"10261-10262"}))

    # Gegenrichtung angegeben - muss trotzdem greifen.
    ergebnis = osd_ernte.ernte_bild(
        bild, osd_meter.get_templates(), schutz, "cd" * 32, "10262-10261")

    assert ergebnis is None


def test_leeres_bild_liefert_nichts():
    from sidecar import osd_meter
    bild = Image.new("RGB", (720, 576), (18, 18, 18))

    ergebnis = osd_ernte.ernte_bild(
        bild, osd_meter.get_templates(), Schutz(), "ef" * 32, "10261-10262")

    assert ergebnis is None


def test_labelzeilen_sind_yolo_format():
    zeilen = osd_ernte.als_labeltext([(3, 0.5, 0.5, 0.1, 0.4)])

    assert zeilen == "3 0.500000 0.500000 0.100000 0.400000\n"
```

- [ ] **Schritt 2: Test laufen lassen und Scheitern bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_ernte.py -q
```

Erwartet: FAIL mit `ModuleNotFoundError: No module named 'osd_ernte'`.

- [ ] **Schritt 3: Die minimale Umsetzung schreiben**

Datei `training/scripts/osd_ernte.py`:

```python
"""Erntet exakt beschriftete Zeichenausschnitte mit dem heutigen Leser.

WOZU
Der Vorlagenweg von osd_meter.py liefert dort, wo er eine VOLLSTAENDIGE Lesung
schafft, nachweislich fehlerfreie Werte - auf dem gesamten Goldbestand null
falsch. Genau diese Lesungen sind gratis verfuegbare Zeichenwahrheit auf echten
Bildern, inklusive Zeichenboxen aus boxen_aus_maske().

WAS NICHT GEERNTET WIRD
Bruchstueck-Lesungen (Ziffern erkannt, aber weder Beschriftung noch Einheit).
Dort steht die Stellenzahl nicht fest und der Dezimalpunkt wird geraten; auf 897
beschrifteten Archivbildern waren 58 von 61 solcher Werte grob falsch. Als
Trainingsetikett waere das Gift.

GRENZE
Diese Quelle lehrt nur, was der Lehrer schon kann. Sie allein hebt die Abdeckung
nicht - dafuer sind die kuenstlichen Bilder und die Handfaelle da.

Rein lesend: Kundenbilder werden nie veraendert.
"""

from __future__ import annotations

import sys
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

WURZEL = Path(__file__).resolve().parents[2]
if str(WURZEL / "sidecar") not in sys.path:
    sys.path.insert(0, str(WURZEL / "sidecar"))
if str(WURZEL / "training" / "scripts") not in sys.path:
    sys.path.insert(0, str(WURZEL / "training" / "scripts"))

from sidecar import osd_meter
from osd_schutz import Schutz


@dataclass(frozen=True)
class Ernteergebnis:
    ausschnitt: Image.Image
    zeichen: list[tuple[int, float, float, float, float]]
    zeichenfolge: str
    meter: float


def zonen_ausschnitt(bild: Image.Image) -> tuple[Image.Image, tuple[int, int]]:
    """Schneidet die Zone unten rechts heraus - dieselbe wie im Leser."""
    links, oben, rechts, unten = osd_meter.ZONEN["unten_rechts"]
    breite, hoehe = bild.size
    kasten = (int(links * breite), int(oben * hoehe),
              int(rechts * breite), int(unten * hoehe))
    return bild.crop(kasten), (kasten[0], kasten[1])


def als_labeltext(zeichen: list[tuple[int, float, float, float, float]]) -> str:
    """YOLO-Labelzeilen mit sechs Nachkommastellen."""
    return "".join(
        f"{klasse} {x:.6f} {y:.6f} {b:.6f} {h:.6f}\n"
        for klasse, x, y, b, h in zeichen)


def ernte_bild(bild: Image.Image, templates, schutz: Schutz,
               bild_sha256: str, haltung: str | None) -> Ernteergebnis | None:
    """Liefert Ausschnitt plus Zeichenboxen - oder None, wenn nichts taugt."""
    if schutz.ist_gesperrt(bild_sha256, haltung):
        return None

    maske, stil = osd_meter.glyphenmaske(bild)
    boxen = osd_meter.boxen_aus_maske(maske, stil)
    if not boxen:
        return None

    zeichenfolge = ""
    for (x0, y0, x1, y1) in boxen:
        glyph = maske[y0:y1, x0:x1].astype("float32")
        zeichen, _ = osd_meter.klassifiziere(glyph, templates)
        zeichenfolge += zeichen or "?"

    # Nur der vollstaendige Vorlagenweg. Kein Tesseract-Rueckfall, kein Raten.
    if "?" in zeichenfolge:
        return None
    if not osd_meter._zeichenfolge_ist_vollstaendig(zeichenfolge):
        return None
    meter = osd_meter.parse_meter(zeichenfolge, stil)
    if meter is None:
        return None

    ausschnitt, (versatz_x, versatz_y) = zonen_ausschnitt(bild)
    a_breite, a_hoehe = ausschnitt.size
    if a_breite <= 0 or a_hoehe <= 0:
        return None

    zeichen_labels: list[tuple[int, float, float, float, float]] = []
    for zeichen, (x0, y0, x1, y1) in zip(zeichenfolge, boxen):
        klasse = osd_meter.ZEICHEN.find(zeichen)
        if klasse < 0:
            return None
        # Boxen liegen in Vollbildkoordinaten; auf den Ausschnitt umrechnen.
        rx0, rx1 = x0 - versatz_x, x1 - versatz_x
        ry0, ry1 = y0 - versatz_y, y1 - versatz_y
        if rx0 < 0 or ry0 < 0 or rx1 > a_breite or ry1 > a_hoehe:
            return None
        zeichen_labels.append((
            klasse,
            ((rx0 + rx1) / 2) / a_breite,
            ((ry0 + ry1) / 2) / a_hoehe,
            (rx1 - rx0) / a_breite,
            (ry1 - ry0) / a_hoehe,
        ))

    return Ernteergebnis(ausschnitt, zeichen_labels, zeichenfolge, meter)
```

- [ ] **Schritt 4: Test laufen lassen und Bestehen bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_ernte.py -q
```

Erwartet: 6 passed.

- [ ] **Schritt 5: Committen**

```bash
git add training/scripts/osd_ernte.py sidecar/tests/test_osd_ernte.py
git commit -m "feat(osd): Lehrer-Ernte fuer exakt beschriftete Zeichenausschnitte"
```

---

## Aufgabe 3: Künstliche Anzeigen erzeugen

**Dateien:**
- Anlegen: `training/scripts/osd_kunstbilder.py`
- Test: `sidecar/tests/test_osd_kunstbilder.py`

**Schnittstellen:**
- Verbraucht: `osd_meter.ZEICHEN`
- Liefert: `erzeuge(saat: int, hintergrund: Image | None = None) -> Kunstbild`
  mit `Kunstbild(bild: Image, zeichen: list[tuple[int, float, float, float, float]], text: str, meter: float)`;
  `STILE: tuple[Stil, ...]`

- [ ] **Schritt 1: Den scheiternden Test schreiben**

Datei `sidecar/tests/test_osd_kunstbilder.py`:

```python
"""Kuenstliche OSD-Anzeigen (Spec Abschnitt 4.2).

Die Wahrheit ist per Konstruktion exakt: Wir wissen, welches Zeichen wir wohin
gemalt haben. Damit lassen sich genau die Stile abdecken, die der heutige Leser
NICHT liest - die Luecke, die die Lehrer-Ernte prinzipiell nicht schliessen kann.
"""

import sys
from pathlib import Path

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_kunstbilder


def test_gleiche_saat_liefert_gleiche_bytes():
    erst = osd_kunstbilder.erzeuge(saat=42)
    zweit = osd_kunstbilder.erzeuge(saat=42)

    assert erst.text == zweit.text
    assert erst.zeichen == zweit.zeichen
    assert erst.bild.tobytes() == zweit.bild.tobytes()


def test_andere_saat_liefert_anderen_text():
    texte = {osd_kunstbilder.erzeuge(saat=n).text for n in range(20)}

    assert len(texte) > 1, "Der Erzeuger liefert immer dasselbe."


def test_labels_liegen_im_bild_und_kennen_gueltige_klassen():
    from sidecar import osd_meter

    for saat in range(10):
        kunst = osd_kunstbilder.erzeuge(saat=saat)
        assert kunst.zeichen, "Ein Bild ohne Zeichen ist nutzlos."
        for klasse, x, y, b, h in kunst.zeichen:
            assert 0 <= klasse < len(osd_meter.ZEICHEN)
            assert 0.0 <= x - b / 2 and x + b / 2 <= 1.0
            assert 0.0 <= y - h / 2 and y + h / 2 <= 1.0


def test_zeichenzahl_passt_zum_text():
    for saat in range(10):
        kunst = osd_kunstbilder.erzeuge(saat=saat)
        assert len(kunst.zeichen) == len(kunst.text.replace(" ", ""))


def test_alle_stile_werden_erzeugt():
    stile = {osd_kunstbilder.erzeuge(saat=n).stil_name for n in range(200)}

    assert stile == {s.name for s in osd_kunstbilder.STILE}
```

- [ ] **Schritt 2: Test laufen lassen und Scheitern bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_kunstbilder.py -q
```

Erwartet: FAIL mit `ModuleNotFoundError: No module named 'osd_kunstbilder'`.

- [ ] **Schritt 3: Die minimale Umsetzung schreiben**

Datei `training/scripts/osd_kunstbilder.py`:

```python
"""Erzeugt kuenstliche OSD-Meteranzeigen mit exakt bekannter Wahrheit.

WOZU
Die Lehrer-Ernte lehrt nur, was der heutige Leser schon kann. Die Stile, an denen
er scheitert, kommen dort gar nicht vor. Kuenstliche Anzeigen schliessen genau
diese Luecke - und ihre Zeichenboxen sind per Konstruktion exakt.

STILE
Abgeleitet aus der menschlichen Sichtung von 40 Haltungen (2026-08-14):
  Lage       38 unten rechts, 2 unten links, 0 oben
  Polaritaet 18 hell auf dunkel, 18 dunkel auf hell, 4 andere
  Farbe      20 weiss/grau, 7 gelb, 13 andere
  Format     19 mit Praefix/fuehrenden Nullen, 15 mit Einheit, 6 ohne Einheit
Die Stichprobe ist klein: Sie belegt mehrere Hauptstile, aber keine exakten
Archivanteile. Die Verteilung hier ist deshalb bewusst breiter gezogen.
"""

from __future__ import annotations

import random
import sys
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

WURZEL = Path(__file__).resolve().parents[2]
if str(WURZEL / "sidecar") not in sys.path:
    sys.path.insert(0, str(WURZEL / "sidecar"))

from sidecar import osd_meter

AUSSCHNITT = (274, 92)   # entspricht der Zone unten rechts eines SD-Bildes


@dataclass(frozen=True)
class Stil:
    name: str
    vordergrund: tuple[int, int, int]
    hintergrund: tuple[int, int, int]


STILE = (
    Stil("weiss_auf_dunkel", (240, 240, 240), (16, 16, 16)),
    Stil("dunkel_auf_weiss", (20, 20, 20), (235, 235, 235)),
    Stil("gelb_auf_dunkel", (250, 220, 60), (14, 14, 20)),
    Stil("gruen_auf_dunkel", (120, 240, 140), (10, 14, 10)),
)

VORLAGEN = (
    "LZ{n}: {wert}m",
    "LZ{n}:{wert}m",
    "L{n} {wert}m",
    "{wert}m",
    "{wert}",
    "LZ{n}: {wert}",
)


@dataclass(frozen=True)
class Kunstbild:
    bild: Image.Image
    zeichen: list[tuple[int, float, float, float, float]]
    text: str
    meter: float
    stil_name: str


def _schriftart(groesse: int) -> ImageFont.FreeTypeFont:
    for name in ("consola.ttf", "cour.ttf", "arial.ttf", "DejaVuSansMono.ttf"):
        try:
            return ImageFont.truetype(name, groesse)
        except OSError:
            continue
    return ImageFont.load_default(groesse)


def erzeuge(saat: int, hintergrund: Image.Image | None = None) -> Kunstbild:
    """Ein kuenstlicher Ausschnitt. Gleiche Saat, gleiches Ergebnis."""
    zufall = random.Random(saat)
    stil = zufall.choice(STILE)

    meter = round(zufall.uniform(0.0, 99.9), 1)
    wert = f"{meter:.1f}"
    if zufall.random() < 0.3:
        wert = wert.zfill(6)          # fuehrende Nullen, z.B. 0009.4
    text = zufall.choice(VORLAGEN).format(n=zufall.choice("123"), wert=wert)

    if hintergrund is None:
        bild = Image.new("RGB", AUSSCHNITT, stil.hintergrund)
    else:
        bild = hintergrund.convert("RGB").resize(AUSSCHNITT)

    groesse = zufall.choice((16, 18, 20, 24, 28, 34))
    schrift = _schriftart(groesse)
    zeichner = ImageDraw.Draw(bild)

    laenge = zeichner.textlength(text, font=schrift)
    x = max(2.0, min(AUSSCHNITT[0] - laenge - 2.0,
                     zufall.uniform(2.0, AUSSCHNITT[0] - laenge - 2.0)))
    y = zufall.uniform(2.0, max(3.0, AUSSCHNITT[1] - groesse - 4.0))

    zeichen: list[tuple[int, float, float, float, float]] = []
    laufend = x
    for buchstabe in text:
        breite = zeichner.textlength(buchstabe, font=schrift)
        if buchstabe != " ":
            zeichner.text((laufend, y), buchstabe, font=schrift,
                          fill=stil.vordergrund)
            klasse = osd_meter.ZEICHEN.find(buchstabe)
            if klasse >= 0:
                x0, y0 = laufend, y
                x1, y1 = laufend + breite, y + groesse
                zeichen.append((
                    klasse,
                    ((x0 + x1) / 2) / AUSSCHNITT[0],
                    ((y0 + y1) / 2) / AUSSCHNITT[1],
                    (x1 - x0) / AUSSCHNITT[0],
                    (y1 - y0) / AUSSCHNITT[1],
                ))
        laufend += breite

    if zufall.random() < 0.5:
        bild = bild.filter(ImageFilter.GaussianBlur(zufall.uniform(0.2, 0.9)))

    return Kunstbild(bild, zeichen, text, meter, stil.name)
```

- [ ] **Schritt 4: Test laufen lassen und Bestehen bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_kunstbilder.py -q
```

Erwartet: 5 passed. Scheitert `test_labels_liegen_im_bild_...`, liegt ein Zeichen
über dem Rand — dann die Randabstände in `erzeuge` vergrössern, nicht den Test
lockern.

- [ ] **Schritt 5: Ein paar Bilder zur Sichtprüfung schreiben**

```bash
sidecar/.venv/Scripts/python.exe -c "
import sys; sys.path.insert(0,'training/scripts')
import osd_kunstbilder
from pathlib import Path
ziel = Path('C:/KI_BRAIN/training/osd_zeichen/probe'); ziel.mkdir(parents=True, exist_ok=True)
for n in range(12):
    k = osd_kunstbilder.erzeuge(saat=n)
    k.bild.save(ziel / f'probe_{n:02d}_{k.stil_name}.png')
print('geschrieben nach', ziel)
"
```

Die zwölf Bilder ansehen. Sehen sie nicht wie echte OSD-Anzeigen aus, hier
nachbessern — sie sind die halbe Trainingsgrundlage.

- [ ] **Schritt 6: Committen**

```bash
git add training/scripts/osd_kunstbilder.py sidecar/tests/test_osd_kunstbilder.py
git commit -m "feat(osd): kuenstliche Meteranzeigen mit exakter Zeichenwahrheit"
```

---

## Aufgabe 4: Datensatz zusammenstellen

**Dateien:**
- Anlegen: `training/scripts/osd_datensatz.py`
- Test: `sidecar/tests/test_osd_datensatz.py`

**Schnittstellen:**
- Verbraucht: `osd_schutz.Schutz`, `osd_wahrheit_aus_protokoll.physische_haltung`
- Liefert:
  - `baue_gruppen(eintraege: list[dict]) -> dict[str, list[str]]` — Eingabe je Eintrag
    `{"id": str, "bild_sha256": str, "haltung": str | None}`; Rückgabe
    Gruppenschlüssel → Liste der Eintrags-IDs
  - `teile_auf(gruppen: dict[str, list[str]], val_anteil: float, saat: int) -> dict[str, str]`
    (Gruppenschlüssel → `"train"` oder `"val"`)
  - `schreibe_data_yaml(ziel: Path) -> Path`

- [ ] **Schritt 1: Den scheiternden Test schreiben**

Datei `sidecar/tests/test_osd_datensatz.py`:

```python
"""Datensatzaufbau (Spec Abschnitt 4.4).

Zwei Bilder derselben physischen Haltung duerfen nie ueber train und val
verteilt sein - sonst misst die interne Validierung sich selbst.
"""

import sys
from pathlib import Path

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_datensatz


def test_haltung_landet_nie_in_beiden_teilen():
    gruppen = {f"haltung_{n}": [f"bild_{n}_a", f"bild_{n}_b"] for n in range(20)}

    zuordnung = osd_datensatz.teile_auf(gruppen, val_anteil=0.25, saat=7)

    for schluessel in gruppen:
        assert zuordnung[schluessel] in ("train", "val")
    assert len(zuordnung) == len(gruppen)


def test_aufteilung_ist_bei_gleicher_saat_gleich():
    gruppen = {f"h{n}": [f"b{n}"] for n in range(30)}

    erst = osd_datensatz.teile_auf(gruppen, val_anteil=0.2, saat=3)
    zweit = osd_datensatz.teile_auf(gruppen, val_anteil=0.2, saat=3)

    assert erst == zweit


def test_val_anteil_wird_ungefaehr_getroffen():
    gruppen = {f"h{n}": [f"b{n}"] for n in range(100)}

    zuordnung = osd_datensatz.teile_auf(gruppen, val_anteil=0.2, saat=1)
    val = sum(1 for teil in zuordnung.values() if teil == "val")

    assert 15 <= val <= 25


def test_mindestens_eine_gruppe_bleibt_im_training():
    gruppen = {"nur_eine": ["b1"]}

    zuordnung = osd_datensatz.teile_auf(gruppen, val_anteil=0.9, saat=1)

    assert zuordnung["nur_eine"] == "train"


def test_data_yaml_nennt_alle_15_klassen(tmp_path):
    from sidecar import osd_meter

    pfad = osd_datensatz.schreibe_data_yaml(tmp_path)
    text = pfad.read_text(encoding="utf-8")

    assert "nc: 15" in text
    for zeichen in osd_meter.ZEICHEN:
        assert repr(zeichen) in text or f'"{zeichen}"' in text


def test_bytegleiche_bilder_kommen_nur_einmal_vor():
    eintraege = [
        {"id": "a", "bild_sha256": "11" * 32, "haltung": "10261-10262"},
        {"id": "b", "bild_sha256": "11" * 32, "haltung": "10261-10262"},
        {"id": "c", "bild_sha256": "22" * 32, "haltung": "10261-10262"},
    ]

    gruppen = osd_datensatz.baue_gruppen(eintraege)

    alle = [wert for liste in gruppen.values() for wert in liste]
    assert sorted(alle) == ["a", "c"]


def test_bytegleiche_bilder_verbinden_ihre_haltungen():
    # Dasselbe Bild taucht unter zwei Haltungen auf: Beide muessen in denselben
    # Teil, sonst steht dieselbe Aufnahme in train UND val.
    eintraege = [
        {"id": "a", "bild_sha256": "11" * 32, "haltung": "10261-10262"},
        {"id": "b", "bild_sha256": "11" * 32, "haltung": "77457-77453"},
        {"id": "c", "bild_sha256": "33" * 32, "haltung": "77457-77453"},
    ]

    gruppen = osd_datensatz.baue_gruppen(eintraege)

    schluessel_von = {
        wert: schluessel
        for schluessel, liste in gruppen.items() for wert in liste
    }
    assert schluessel_von["a"] == schluessel_von["c"]


def test_gegenrichtung_landet_in_derselben_gruppe():
    eintraege = [
        {"id": "a", "bild_sha256": "11" * 32, "haltung": "10261-10262"},
        {"id": "b", "bild_sha256": "22" * 32, "haltung": "10262-10261"},
    ]

    gruppen = osd_datensatz.baue_gruppen(eintraege)

    assert len(gruppen) == 1


def test_kunstbilder_ohne_haltung_bilden_eigene_gruppen():
    eintraege = [
        {"id": "k1", "bild_sha256": "11" * 32, "haltung": None},
        {"id": "k2", "bild_sha256": "22" * 32, "haltung": None},
    ]

    gruppen = osd_datensatz.baue_gruppen(eintraege)

    assert len(gruppen) == 2
```

- [ ] **Schritt 2: Test laufen lassen und Scheitern bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_datensatz.py -q
```

Erwartet: FAIL mit `ModuleNotFoundError: No module named 'osd_datensatz'`.

- [ ] **Schritt 3: Die minimale Umsetzung schreiben**

Datei `training/scripts/osd_datensatz.py`:

```python
"""Fuehrt Ernte und Kunstbilder zu einem YOLO-Datensatz zusammen.

Die Aufteilung geht nach PHYSISCHER Haltung, nie nach Bild: Zwei Bilder aus
derselben Haltung in train und val zugleich waeren eine verdeckte Selbstmessung.
Kuenstliche Bilder haben keine Haltung und bilden eigene Gruppen.
"""

from __future__ import annotations

import json
import random
import sys
from pathlib import Path

WURZEL = Path(__file__).resolve().parents[2]
for pfad in (WURZEL / "sidecar", WURZEL / "training" / "scripts"):
    if str(pfad) not in sys.path:
        sys.path.insert(0, str(pfad))

from sidecar import osd_meter
from osd_wahrheit_aus_protokoll import physische_haltung

ZIEL_WURZEL = Path(r"C:\KI_BRAIN\training\osd_zeichen")


def baue_gruppen(eintraege: list[dict]) -> dict[str, list[str]]:
    """Bildet die Split-Gruppen. Bytegleiche Bilder kommen nur einmal vor.

    Taucht DASSELBE Bild unter zwei Haltungen auf, werden diese Haltungen zu
    einer gemeinsamen Gruppe verbunden - sonst stuende dieselbe Aufnahme in
    train und val zugleich. Dasselbe Verfahren benutzt gold_stock_audit.py.
    Kuenstliche Bilder haben keine Haltung und bilden je eine eigene Gruppe.
    """
    # 1. Byte-Duplikate wegwerfen, ersten Eintrag behalten.
    gesehen: set[str] = set()
    eindeutig: list[dict] = []
    for eintrag in eintraege:
        hash_wert = str(eintrag.get("bild_sha256") or "").lower()
        if hash_wert and hash_wert in gesehen:
            continue
        if hash_wert:
            gesehen.add(hash_wert)
        eindeutig.append(eintrag)

    # 2. Haltungen verbinden, die sich ein Bild teilen (Union-Find).
    eltern: dict[str, str] = {}

    def finde(knoten: str) -> str:
        eltern.setdefault(knoten, knoten)
        while eltern[knoten] != knoten:
            eltern[knoten] = eltern[eltern[knoten]]
            knoten = eltern[knoten]
        return knoten

    def verbinde(a: str, b: str) -> None:
        wurzel_a, wurzel_b = finde(a), finde(b)
        if wurzel_a != wurzel_b:
            eltern[max(wurzel_a, wurzel_b)] = min(wurzel_a, wurzel_b)

    hash_zu_haltung: dict[str, str] = {}
    for eintrag in eintraege:
        haltung = eintrag.get("haltung")
        if not haltung:
            continue
        schluessel = physische_haltung(str(haltung))
        finde(schluessel)
        hash_wert = str(eintrag.get("bild_sha256") or "").lower()
        if not hash_wert:
            continue
        if hash_wert in hash_zu_haltung:
            verbinde(hash_zu_haltung[hash_wert], schluessel)
        else:
            hash_zu_haltung[hash_wert] = schluessel

    # 3. Eintraege den Gruppen zuordnen.
    gruppen: dict[str, list[str]] = {}
    for lauf, eintrag in enumerate(eindeutig):
        haltung = eintrag.get("haltung")
        if haltung:
            schluessel = finde(physische_haltung(str(haltung)))
        else:
            # Ohne Haltung: eigene Gruppe, damit kuenstliche Bilder keine
            # echten Haltungen an sich binden.
            schluessel = f"kunst_{lauf:06d}"
        gruppen.setdefault(schluessel, []).append(str(eintrag["id"]))
    return gruppen


def teile_auf(gruppen: dict[str, list[str]], val_anteil: float,
              saat: int) -> dict[str, str]:
    """Ordnet jede Gruppe genau einem Teil zu. Gleiche Saat, gleiche Aufteilung."""
    schluessel = sorted(gruppen)
    zufall = random.Random(saat)
    zufall.shuffle(schluessel)

    anzahl_val = int(len(schluessel) * val_anteil)
    # Mindestens eine Gruppe bleibt im Training, sonst ist der Lauf sinnlos.
    anzahl_val = min(anzahl_val, max(0, len(schluessel) - 1))

    zuordnung = {name: "train" for name in schluessel}
    for name in schluessel[:anzahl_val]:
        zuordnung[name] = "val"
    return zuordnung


def schreibe_data_yaml(ziel: Path) -> Path:
    """Die Klassenliste ist die Zeichenkette ZEICHEN, Position = Klassen-ID."""
    namen = "\n".join(f"  {i}: {zeichen!r}"
                      for i, zeichen in enumerate(osd_meter.ZEICHEN))
    text = (
        "# Erzeugt von osd_datensatz.py - nicht von Hand aendern.\n"
        "path: .\n"
        "train: images/train\n"
        "val: images/val\n"
        f"nc: {len(osd_meter.ZEICHEN)}\n"
        "names:\n"
        f"{namen}\n"
    )
    ziel.mkdir(parents=True, exist_ok=True)
    pfad = ziel / "data.yaml"
    pfad.write_text(text, encoding="utf-8")
    return pfad
```

- [ ] **Schritt 4: Test laufen lassen und Bestehen bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_datensatz.py -q
```

Erwartet: 9 passed.

- [ ] **Schritt 5: Committen**

```bash
git add training/scripts/osd_datensatz.py sidecar/tests/test_osd_datensatz.py
git commit -m "feat(osd): Datensatzaufbau mit Split nach physischer Haltung"
```

---

## Aufgabe 5: Kandidat trainieren

**Dateien:**
- Anlegen: `training/scripts/train_osd_zeichen.py`
- Vorbild: `training/scripts/train_bcc_pilot.py` (Sperren, Manifestform)

**Schnittstellen:**
- Verbraucht: den Datensatz aus Aufgabe 4
- Liefert: Kandidatenordner unter
  `<KnowledgeRoot>/training/models/candidates/osd_zeichen_<kurzhash>/` mit
  `weights/best.pt` und `manifest.json` (Felder: `kandidat_id`,
  `gewicht_sha256`, `status`, `klassen`, `datensatz_sha256`, `schwelle`)

- [ ] **Schritt 1: Das Skript schreiben**

Datei `training/scripts/train_osd_zeichen.py`:

```python
"""Trainiert den OSD-Zeichen-Detektor. Schreibt NUR einen Kandidaten.

Sperren wie bei train_bcc_pilot.py: kein Lauf bei erreichbarem Sidecar (er haelt
GPU-Speicher), kein Lauf unter 8000 MB freiem VRAM. Produktive Gewichte oder
Modellzeiger werden nie angefasst. Der Kandidat startet als
diagnostic_not_deployed und laeuft erst nach ausdruecklicher Freigabe mit.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import urllib.request
from pathlib import Path

WURZEL = Path(__file__).resolve().parents[2]
if str(WURZEL / "sidecar") not in sys.path:
    sys.path.insert(0, str(WURZEL / "sidecar"))

from sidecar import osd_meter

KANDIDATEN = Path(r"C:\KI_BRAIN\training\models\candidates")
MIN_FREIER_VRAM_MB = 8000


def sha256(pfad: Path) -> str:
    h = hashlib.sha256()
    with pfad.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def sidecar_laeuft() -> bool:
    try:
        with urllib.request.urlopen("http://127.0.0.1:8100/health", timeout=2):
            return True
    except Exception:
        return False


def freier_vram_mb() -> int | None:
    try:
        ergebnis = subprocess.run(
            ["nvidia-smi", "--query-gpu=memory.free", "--format=csv,noheader,nounits"],
            capture_output=True, text=True, timeout=10, check=True)
        return int(ergebnis.stdout.strip().splitlines()[0])
    except Exception:
        return None


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--datensatz", type=Path, required=True)
    p.add_argument("--epochen", type=int, default=60)
    p.add_argument("--imgsz", type=int, default=320)
    p.add_argument("--batch", type=int, default=16)
    p.add_argument("--basis", default="yolo26n.pt")
    args = p.parse_args(argv)

    if sidecar_laeuft():
        print("ABBRUCH: Der Sidecar laeuft und haelt GPU-Speicher. Erst beenden.",
              file=sys.stderr)
        return 2

    frei = freier_vram_mb()
    if frei is not None and frei < MIN_FREIER_VRAM_MB:
        print(f"ABBRUCH: Nur {frei} MB VRAM frei, noetig sind {MIN_FREIER_VRAM_MB}.",
              file=sys.stderr)
        return 2

    yaml_pfad = args.datensatz / "data.yaml"
    if not yaml_pfad.is_file():
        print(f"ABBRUCH: data.yaml fehlt unter {args.datensatz}", file=sys.stderr)
        return 2

    from ultralytics import YOLO

    modell = YOLO(args.basis)
    ergebnis = modell.train(
        data=str(yaml_pfad),
        epochs=args.epochen,
        imgsz=args.imgsz,
        batch=args.batch,
        # Uhrlage und Leserichtung sind fest: Ein gespiegeltes "9" waere eine "P".
        flipud=0.0,
        fliplr=0.0,
        degrees=0.0,
        # Die Anzeige variiert in Helligkeit und Farbe, nicht in der Form.
        hsv_h=0.02, hsv_s=0.4, hsv_v=0.5,
        patience=15,
        project=str(KANDIDATEN),
        name="osd_zeichen_lauf",
        exist_ok=False,
    )

    quelle = Path(ergebnis.save_dir) / "weights" / "best.pt"
    if not quelle.is_file():
        print("ABBRUCH: Kein best.pt erzeugt.", file=sys.stderr)
        return 1

    gewicht_hash = sha256(quelle)
    kandidat_id = f"osd_zeichen_{gewicht_hash[:12]}"
    ziel = KANDIDATEN / kandidat_id
    if ziel.exists():
        print(f"ABBRUCH: Kandidat besteht bereits: {ziel}", file=sys.stderr)
        return 1
    shutil.copytree(Path(ergebnis.save_dir), ziel)

    manifest = {
        "schema": "osd_zeichen_kandidat_v1",
        "kandidat_id": kandidat_id,
        "status": "diagnostic_not_deployed",
        "gewicht_datei": "weights/best.pt",
        "gewicht_sha256": gewicht_hash,
        "basis": args.basis,
        "klassen": list(osd_meter.ZEICHEN),
        "imgsz": args.imgsz,
        "datensatz": str(args.datensatz),
        "datensatz_yaml_sha256": sha256(yaml_pfad),
        # Wird erst von osd_schwelle_kalibrieren.py gesetzt. Solange None,
        # verweigert die Goldmessung den Lauf.
        "schwelle": None,
    }
    (ziel / "manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"Kandidat: {ziel}")
    print(f"Gewicht-SHA-256: {gewicht_hash}")
    print("Status: diagnostic_not_deployed - Schwelle fehlt noch.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Schritt 2: Syntaxprüfung**

```bash
sidecar/.venv/Scripts/python.exe -m py_compile training/scripts/train_osd_zeichen.py
```

Erwartet: keine Ausgabe.

- [ ] **Schritt 3: Die Sperre gegen den laufenden Sidecar prüfen**

Sidecar starten, dann:

```bash
sidecar/.venv/Scripts/python.exe training/scripts/train_osd_zeichen.py --datensatz C:/KI_BRAIN/training/osd_zeichen/egal
```

Erwartet: `ABBRUCH: Der Sidecar laeuft ...`, Exitcode 2. Danach Sidecar beenden.

- [ ] **Schritt 4: Committen**

```bash
git add training/scripts/train_osd_zeichen.py
git commit -m "feat(osd): Trainingsskript fuer den Zeichen-Detektor"
```

---

## Aufgabe 6: Laufzeitteil — Ausschnitt normieren und Zeichen zusammensetzen

**Dateien:**
- Anlegen: `sidecar/sidecar/osd_modell.py`
- Test: `sidecar/tests/test_osd_modell.py`

**Schnittstellen:**
- Verbraucht: `osd_meter.ZEICHEN`, `osd_meter.REFERENZ_GLYPHE_H`
- Liefert:
  - `normiere_ausschnitt(bild: Image, ziel_hoehe: int = 32) -> Image`
  - `zu_zeichenfolge(erkennungen: list[tuple[int, float, float, float, float, float]]) -> tuple[str, float]`
    — Eingabe je Erkennung `(klasse_id, x_mitte, y_mitte, breite, hoehe, sicherheit)`,
    Rückgabe `(zeichenfolge, kleinste_sicherheit)`
  - `TOR_MINDESTZEICHEN = 3`

- [ ] **Schritt 1: Den scheiternden Test schreiben**

Datei `sidecar/tests/test_osd_modell.py`:

```python
"""Laufzeitteil des Modell-Lesers (Spec Abschnitte 3 und 5).

Zwei Dinge werden hier festgehalten:
  1. Der Ausschnitt wird auf feste Zeichenhoehe normiert. SD und HD sehen fuer
     das Modell dadurch gleich aus - der Aufloesungsfehler vom 2026-08-14 kann
     bauartbedingt nicht wiederkehren.
  2. Die Sicherheit einer Lesung ist die KLEINSTE Zeichensicherheit. Ein
     wackliges Zeichen macht die ganze Lesung wacklig.
"""

from PIL import Image

from sidecar import osd_meter, osd_modell


def test_normierung_macht_sd_und_hd_gleich_gross():
    sd = Image.new("RGB", (274, 92))
    hd = Image.new("RGB", (548, 184))

    assert (osd_modell.normiere_ausschnitt(sd).height
            == osd_modell.normiere_ausschnitt(hd).height)


def test_normierung_haelt_das_seitenverhaeltnis():
    bild = Image.new("RGB", (400, 100))

    normiert = osd_modell.normiere_ausschnitt(bild, ziel_hoehe=50)

    assert normiert.height == 50
    assert normiert.width == 200


def test_zeichen_werden_von_links_nach_rechts_gelesen():
    # Absichtlich in falscher Reihenfolge uebergeben.
    erkennungen = [
        (osd_meter.ZEICHEN.find("4"), 0.7, 0.5, 0.1, 0.4, 0.9),
        (osd_meter.ZEICHEN.find("9"), 0.3, 0.5, 0.1, 0.4, 0.9),
        (osd_meter.ZEICHEN.find("."), 0.5, 0.5, 0.05, 0.4, 0.9),
    ]

    folge, _ = osd_modell.zu_zeichenfolge(erkennungen)

    assert folge == "9.4"


def test_kleinste_sicherheit_zaehlt():
    erkennungen = [
        (osd_meter.ZEICHEN.find("9"), 0.3, 0.5, 0.1, 0.4, 0.95),
        (osd_meter.ZEICHEN.find("4"), 0.7, 0.5, 0.1, 0.4, 0.41),
    ]

    _, sicherheit = osd_modell.zu_zeichenfolge(erkennungen)

    assert sicherheit == 0.41


def test_leere_erkennung_liefert_leere_folge_und_null():
    folge, sicherheit = osd_modell.zu_zeichenfolge([])

    assert folge == ""
    assert sicherheit == 0.0


def test_doppelte_box_am_selben_ort_wird_einmal_gezaehlt():
    # Zwei Erkennungen fast am selben Ort: die schwaechere faellt weg.
    erkennungen = [
        (osd_meter.ZEICHEN.find("9"), 0.30, 0.5, 0.10, 0.4, 0.90),
        (osd_meter.ZEICHEN.find("8"), 0.31, 0.5, 0.10, 0.4, 0.40),
    ]

    folge, sicherheit = osd_modell.zu_zeichenfolge(erkennungen)

    assert folge == "9"
    assert sicherheit == 0.90
```

- [ ] **Schritt 2: Test laufen lassen und Scheitern bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_modell.py -q
```

Erwartet: FAIL mit `ImportError: cannot import name 'osd_modell'`.

- [ ] **Schritt 3: Die minimale Umsetzung schreiben**

Datei `sidecar/sidecar/osd_modell.py`:

```python
"""Laufzeitteil des trainierten OSD-Zeichenlesers.

Hier liegt bewusst NUR das, was ohne geladenes Modell prueffaehig ist:
Normierung des Ausschnitts und der Zusammenbau erkannter Zeichen zu einer
Zeichenkette. Die Deutung der Kette macht unveraendert osd_meter.parse_meter.

Zur Normierung: Die Abstandsschranken des alten Vorlagenlesers standen als feste
Pixelwerte da, eingestellt auf SD mit rund 18 Pixel hohen Ziffern. Auf HD sind
dieselben Zeichen doppelt so gross und der Leser verlor Dezimalpunkt und Einheit
("LZ1: 3.2m" wurde "L132"). Wer den Ausschnitt vor der Inferenz auf eine feste
Hoehe bringt, kann diesen Fehler gar nicht erst machen.
"""

from __future__ import annotations

from PIL import Image

from . import osd_meter

# Zielhoehe des normierten Ausschnitts. Rund die doppelte SD-Ziffernhoehe
# (REFERENZ_GLYPHE_H = 18), damit auch kleine Zeichen genug Pixel behalten.
ZIEL_HOEHE = 32

# Unter drei Zeichen ist keine sinnvolle Meterangabe moeglich.
TOR_MINDESTZEICHEN = 3

# Zwei Boxen, deren Mitten naeher als dieser Anteil der Ausschnittsbreite
# beieinanderliegen, gelten als dasselbe Zeichen.
_MINDESTABSTAND = 0.02


def normiere_ausschnitt(bild: Image.Image, ziel_hoehe: int = ZIEL_HOEHE) -> Image.Image:
    """Bringt den Ausschnitt auf feste Hoehe, Seitenverhaeltnis bleibt."""
    breite, hoehe = bild.size
    if hoehe <= 0 or breite <= 0:
        return bild
    faktor = ziel_hoehe / hoehe
    return bild.resize((max(1, round(breite * faktor)), ziel_hoehe), Image.BICUBIC)


def zu_zeichenfolge(
    erkennungen: list[tuple[int, float, float, float, float, float]],
) -> tuple[str, float]:
    """Setzt Erkennungen von links nach rechts zu einer Zeichenkette zusammen.

    Rueckgabe: (Zeichenkette, kleinste Sicherheit). Ohne Erkennung ("", 0.0).
    """
    if not erkennungen:
        return "", 0.0

    # Staerkste zuerst, damit bei Ueberlappung die schwaechere Box faellt.
    nach_staerke = sorted(erkennungen, key=lambda e: e[5], reverse=True)
    behalten: list[tuple[int, float, float, float, float, float]] = []
    for kandidat in nach_staerke:
        if any(abs(kandidat[1] - fest[1]) < _MINDESTABSTAND for fest in behalten):
            continue
        behalten.append(kandidat)

    behalten.sort(key=lambda e: e[1])

    folge = ""
    for klasse, _x, _y, _b, _h, _s in behalten:
        if 0 <= klasse < len(osd_meter.ZEICHEN):
            folge += osd_meter.ZEICHEN[klasse]
        else:
            # Unbekannte Klasse: Lieber gar nichts als ein geratenes Zeichen.
            return "", 0.0

    return folge, min(e[5] for e in behalten)
```

- [ ] **Schritt 4: Test laufen lassen und Bestehen bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_modell.py -q
```

Erwartet: 6 passed.

- [ ] **Schritt 5: Die ganze Sidecar-Suite laufen lassen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest -q
```

Erwartet: alles grün. `osd_meter.py` wurde nicht angefasst, die bestehenden
OSD-Tests müssen unverändert bestehen.

- [ ] **Schritt 6: Committen**

```bash
git add sidecar/sidecar/osd_modell.py sidecar/tests/test_osd_modell.py
git commit -m "feat(osd): Normierung und Zeichenzusammenbau fuer den Modell-Leser"
```

---

## Aufgabe 7: Schwelle am Reservebestand kalibrieren

**Dateien:**
- Anlegen: `training/scripts/osd_schwelle_kalibrieren.py`
- Test: `sidecar/tests/test_osd_schwelle.py`

**Schnittstellen:**
- Verbraucht: Kandidatenmanifest aus Aufgabe 5, den Reservebestand
  (`C:\KI_BRAIN\training\diagnostics\osd_wahrheit_protokoll_v1\wahrheit.json`,
  nur Einträge mit `split == "test"`)
- Liefert: `waehle_schwelle(faelle: list[dict], sicherheitsabstand: float) -> float`
  — Eingabe je Fall `{"sicherheit": float, "abweichung_m": float | None}`

- [ ] **Schritt 1: Den scheiternden Test schreiben**

Datei `sidecar/tests/test_osd_schwelle.py`:

```python
"""Schwellenwahl (Spec Abschnitt 5).

Die Schwelle wird NIE an Gold eingestellt. Wer sie so lange dreht, bis auf Gold
null Fehler stehen, hat Gold zum Anpassen benutzt und misst danach sich selbst.
Hier zaehlt allein der getrennte Reservebestand.
"""

import sys
from pathlib import Path

import pytest

SKRIPTE = Path(__file__).resolve().parents[2] / "training" / "scripts"
if str(SKRIPTE) not in sys.path:
    sys.path.insert(0, str(SKRIPTE))

import osd_schwelle_kalibrieren as kal


def test_schwelle_schliesst_alle_groben_fehler_aus():
    faelle = [
        {"sicherheit": 0.95, "abweichung_m": 0.0},
        {"sicherheit": 0.90, "abweichung_m": 0.02},
        {"sicherheit": 0.55, "abweichung_m": 7.4},   # grob falsch
        {"sicherheit": 0.40, "abweichung_m": 12.0},  # grob falsch
    ]

    schwelle = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)

    assert schwelle > 0.55
    assert schwelle <= 0.90


def test_sicherheitsabstand_wird_aufgeschlagen():
    faelle = [
        {"sicherheit": 0.90, "abweichung_m": 0.0},
        {"sicherheit": 0.50, "abweichung_m": 9.0},
    ]

    ohne = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)
    mit = kal.waehle_schwelle(faelle, sicherheitsabstand=0.05)

    assert mit == pytest.approx(ohne + 0.05)


def test_ohne_groben_fehler_bleibt_die_grundschwelle():
    faelle = [
        {"sicherheit": 0.80, "abweichung_m": 0.01},
        {"sicherheit": 0.70, "abweichung_m": 0.03},
    ]

    schwelle = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)

    assert schwelle == kal.GRUNDSCHWELLE


def test_faelle_ohne_sollwert_zaehlen_nicht():
    faelle = [
        {"sicherheit": 0.30, "abweichung_m": None},
        {"sicherheit": 0.80, "abweichung_m": 0.0},
    ]

    schwelle = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)

    assert schwelle == kal.GRUNDSCHWELLE


def test_alles_falsch_liefert_unerreichbare_schwelle():
    faelle = [
        {"sicherheit": 0.99, "abweichung_m": 5.0},
    ]

    schwelle = kal.waehle_schwelle(faelle, sicherheitsabstand=0.0)

    assert schwelle > 0.99
```

- [ ] **Schritt 2: Test laufen lassen und Scheitern bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_schwelle.py -q
```

Erwartet: FAIL mit `ModuleNotFoundError`.

- [ ] **Schritt 3: Die minimale Umsetzung schreiben**

Datei `training/scripts/osd_schwelle_kalibrieren.py`:

```python
"""Bestimmt die Sicherheitsschwelle des OSD-Zeichenlesers.

WOZU
Die Regel des Projekts lautet: null falsch ist wichtiger als Abdeckung. Also
braucht das Modell eine Schwelle, unter der es lieber nichts sagt. Diese Schwelle
wird an einem GETRENNTEN Reservebestand bestimmt - dem Testteil der 897 schwach
beschrifteten Protokollbilder - und danach eingefroren.

WARUM NICHT AN GOLD
Wer die Schwelle so lange dreht, bis auf Gold null Fehler stehen, hat Gold zum
Anpassen benutzt. Die anschliessende Goldmessung waere dann keine unabhaengige
Messung mehr, sondern eine Selbstbestaetigung.

WARUM DIE SCHWACHEN ETIKETTEN HIER TAUGEN
Sie stimmen nur auf wenige Zentimeter genau (Sichtprobe: 25 von 30 auf 1 cm).
Fuer die Frage "liegt diese Lesung GROB daneben" reicht das voellig - und nur
diese Frage wird hier gestellt. Als Zeichenwahrheit fuers Training bleiben sie
gesperrt.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

# Ab dieser Abweichung gilt eine Lesung als grob falsch. Deutlich ueber dem
# Zentimeter-Rauschen der schwachen Etiketten, deutlich unter einem echten
# Lesefehler (der verschiebt den Wert meist um Meter).
GROB_FALSCH_AB_M = 0.5

# Ohne jeden groben Fehler bleibt es bei diesem Wert. Nicht 0.0: Eine Lesung
# ohne jede Sicherheit soll auch dann nicht durchgehen.
GRUNDSCHWELLE = 0.25


def waehle_schwelle(faelle: list[dict], sicherheitsabstand: float = 0.05) -> float:
    """Kleinste Schwelle, die JEDEN groben Fehler aussperrt, plus Abstand."""
    grob = [
        fall["sicherheit"] for fall in faelle
        if fall.get("abweichung_m") is not None
        and abs(float(fall["abweichung_m"])) >= GROB_FALSCH_AB_M
    ]
    if not grob:
        return GRUNDSCHWELLE

    # Knapp ueber der staerksten falschen Lesung.
    schwelle = max(grob) + 1e-6
    return round(min(schwelle + sicherheitsabstand, 1.0 + sicherheitsabstand), 6)


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--faelle", type=Path, required=True,
                   help="JSON mit den Lesungen auf dem Reservebestand.")
    p.add_argument("--kandidat", type=Path, required=True)
    p.add_argument("--sicherheitsabstand", type=float, default=0.05)
    args = p.parse_args(argv)

    daten = json.loads(args.faelle.read_text(encoding="utf-8-sig"))
    faelle = daten.get("faelle") or []
    if not faelle:
        print("ABBRUCH: Keine Faelle im Reservebestand.", file=sys.stderr)
        return 2

    schwelle = waehle_schwelle(faelle, args.sicherheitsabstand)

    manifest_pfad = args.kandidat / "manifest.json"
    manifest = json.loads(manifest_pfad.read_text(encoding="utf-8-sig"))
    if manifest.get("schwelle") is not None:
        print(f"ABBRUCH: Schwelle ist bereits eingefroren "
              f"({manifest['schwelle']}). Ein zweites Einstellen waere keine "
              f"unabhaengige Messung mehr.", file=sys.stderr)
        return 2

    manifest["schwelle"] = schwelle
    manifest["schwelle_quelle"] = str(args.faelle)
    manifest["schwelle_faelle"] = len(faelle)
    manifest_pfad.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"Schwelle: {schwelle}  (aus {len(faelle)} Faellen)")
    print(f"Eingefroren in: {manifest_pfad}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Schritt 4: Test laufen lassen und Bestehen bestätigen**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest tests/test_osd_schwelle.py -q
```

Erwartet: 5 passed.

- [ ] **Schritt 5: Committen**

```bash
git add training/scripts/osd_schwelle_kalibrieren.py sidecar/tests/test_osd_schwelle.py
git commit -m "feat(osd): Schwellenkalibrierung am getrennten Reservebestand"
```

---

## Aufgabe 8: Goldmessung des Kandidaten

**Dateien:**
- Anlegen: `training/scripts/osd_modell_goldmessung.py`

**Schnittstellen:**
- Verbraucht: `osd_goldmessung.messe_satz` und `osd_goldmessung.sha256`
  (unverändert importiert), `osd_modell.normiere_ausschnitt`,
  `osd_modell.zu_zeichenfolge`, `osd_meter.parse_meter`, `osd_meter.ZONEN`
- Liefert: Bericht unter `<KnowledgeRoot>/training/reports/osd_modell_goldmessung_<kandidat>.json`

- [ ] **Schritt 1: Das Skript schreiben**

Datei `training/scripts/osd_modell_goldmessung.py`:

```python
"""Misst den trainierten Zeichenleser gegen die drei eingefrorenen Goldsaetze.

Benutzt bewusst messe_satz() aus osd_goldmessung.py: dieselbe Hashpruefung der
Bildbytes, dieselbe Einteilung in richtig / falsch / nicht_gelesen. Nur der Leser
ist ein anderer. Damit sind alter und neuer Stand direkt vergleichbar.

Der Lauf verweigert, solange die Schwelle im Kandidatenmanifest nicht eingefroren
ist - sonst waere die Versuchung gross, sie nach dem Ergebnis nachzuziehen.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

WURZEL = Path(__file__).resolve().parents[2]
for pfad in (WURZEL / "sidecar", WURZEL / "training" / "scripts"):
    if str(pfad) not in sys.path:
        sys.path.insert(0, str(pfad))

from PIL import Image

from sidecar import osd_meter, osd_modell
import osd_goldmessung

GOLD_WURZEL = Path(r"C:\KI_BRAIN\eval_set\osd")
BERICHT_ORDNER = Path(r"C:\KI_BRAIN\training\reports")


def baue_modell_leser(kandidat: Path, schwelle: float):
    """Liefert eine lese()-Funktion mit derselben Form wie osd_meter.lese_meter."""
    from ultralytics import YOLO

    gewicht = kandidat / "weights" / "best.pt"
    modell = YOLO(str(gewicht))

    def lese(bild_pfad: Path) -> dict:
        with Image.open(bild_pfad) as bild:
            rgb = bild.convert("RGB")
            links, oben, rechts, unten = osd_meter.ZONEN["unten_rechts"]
            breite, hoehe = rgb.size
            ausschnitt = rgb.crop((int(links * breite), int(oben * hoehe),
                                   int(rechts * breite), int(unten * hoehe)))
            normiert = osd_modell.normiere_ausschnitt(ausschnitt)

            ergebnis = modell.predict(source=normiert, verbose=False)[0]
            erkennungen = []
            if ergebnis.boxes is not None:
                for box in ergebnis.boxes:
                    klasse = int(box.cls[0].cpu().item())
                    sicher = float(box.conf[0].cpu().item())
                    x, y, b, h = (float(v) for v in box.xywhn[0].cpu().tolist())
                    erkennungen.append((klasse, x, y, b, h, sicher))

            folge, kleinste = osd_modell.zu_zeichenfolge(erkennungen)

            meter = None
            if (len(folge) >= osd_modell.TOR_MINDESTZEICHEN
                    and kleinste >= schwelle):
                # Die Deutung macht unveraendert der alte Leser. Dort stecken
                # die beiden Regeln gegen das Raten.
                meter = osd_meter.parse_meter(folge, "dunkel")

            return {
                "meter": meter,
                "zeichenfolge": folge,
                "stil": "modell",
                "leseweg": "modell" if meter is not None else None,
                "konfidenz_min": kleinste,
            }

    return lese


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--kandidat", type=Path, required=True)
    p.add_argument("--gold-wurzel", type=Path, default=GOLD_WURZEL)
    p.add_argument("--bericht-ordner", type=Path, default=BERICHT_ORDNER)
    args = p.parse_args(argv)

    manifest_pfad = args.kandidat / "manifest.json"
    if not manifest_pfad.is_file():
        print(f"ABBRUCH: Kandidatenmanifest fehlt: {manifest_pfad}", file=sys.stderr)
        return 2
    manifest = json.loads(manifest_pfad.read_text(encoding="utf-8-sig"))

    schwelle = manifest.get("schwelle")
    if schwelle is None:
        print("ABBRUCH: Die Schwelle ist nicht eingefroren. Erst "
              "osd_schwelle_kalibrieren.py laufen lassen.", file=sys.stderr)
        return 2

    gewicht = args.kandidat / manifest["gewicht_datei"]
    ist_hash = osd_goldmessung.sha256(gewicht)
    if ist_hash != manifest["gewicht_sha256"]:
        print(f"ABBRUCH: Gewichtshash weicht ab.\n  Manifest: "
              f"{manifest['gewicht_sha256']}\n  Datei:    {ist_hash}", file=sys.stderr)
        return 2

    lese = baue_modell_leser(args.kandidat, float(schwelle))

    saetze = [osd_goldmessung.messe_satz(args.gold_wurzel / name, lese)
              for name in osd_goldmessung.SAETZE]

    gesamt = {
        "bilder": sum(s["bilder"] for s in saetze),
        "richtig": sum(s["richtig"] for s in saetze),
        "falsch": sum(s["falsch"] for s in saetze),
        "nicht_gelesen": sum(s["nicht_gelesen"] for s in saetze),
    }

    print(f"Kandidat: {manifest['kandidat_id']}  Schwelle {schwelle}")
    print(f"{'Satz':<14}{'Bilder':>8}{'richtig':>9}{'falsch':>8}{'nicht ges.':>12}")
    for s in saetze:
        print(f"{s['satz']:<14}{s['bilder']:>8}{s['richtig']:>9}"
              f"{s['falsch']:>8}{s['nicht_gelesen']:>12}")
    print(f"{'GESAMT':<14}{gesamt['bilder']:>8}{gesamt['richtig']:>9}"
          f"{gesamt['falsch']:>8}{gesamt['nicht_gelesen']:>12}")
    print()
    print("Freigabemarke: null falsch UND mindestens 170 richtig.")
    if gesamt["falsch"] == 0 and gesamt["richtig"] >= 170:
        print("ERREICHT.")
    else:
        print("NICHT erreicht - der Kandidat bleibt diagnostic_not_deployed.")

    bericht = {
        "schema": "osd_modell_goldmessung_v1",
        "kandidat_id": manifest["kandidat_id"],
        "gewicht_sha256": manifest["gewicht_sha256"],
        "schwelle": schwelle,
        "gesamt": gesamt,
        "saetze": saetze,
    }
    args.bericht_ordner.mkdir(parents=True, exist_ok=True)
    ziel = args.bericht_ordner / f"osd_modell_goldmessung_{manifest['kandidat_id']}.json"
    if ziel.exists():
        print(f"\nBericht besteht bereits und wird nicht ueberschrieben: {ziel}")
        return 0
    text = json.dumps(bericht, indent=1, ensure_ascii=False)
    arbeit = ziel.with_suffix(".json.arbeit")
    arbeit.write_bytes(text.encode("utf-8"))
    arbeit.replace(ziel)
    print(f"\nBericht: {ziel}")
    print(f"Bericht-SHA-256: {hashlib.sha256(text.encode('utf-8')).hexdigest()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Schritt 2: Syntaxprüfung**

```bash
sidecar/.venv/Scripts/python.exe -m py_compile training/scripts/osd_modell_goldmessung.py
```

Erwartet: keine Ausgabe.

- [ ] **Schritt 3: Die Verweigerung ohne Schwelle prüfen**

Einen Testordner mit einem Manifest ohne Schwelle anlegen und aufrufen:

```bash
sidecar/.venv/Scripts/python.exe -c "
import json, pathlib
p = pathlib.Path('C:/KI_BRAIN/training/osd_zeichen/probe_kandidat')
(p / 'weights').mkdir(parents=True, exist_ok=True)
(p / 'manifest.json').write_text(json.dumps({'kandidat_id':'probe','gewicht_datei':'weights/best.pt','gewicht_sha256':'00','schwelle':None}))
print(p)
"
sidecar/.venv/Scripts/python.exe training/scripts/osd_modell_goldmessung.py --kandidat C:/KI_BRAIN/training/osd_zeichen/probe_kandidat
```

Erwartet: `ABBRUCH: Die Schwelle ist nicht eingefroren.`, Exitcode 2.

- [ ] **Schritt 4: Committen**

```bash
git add training/scripts/osd_modell_goldmessung.py
git commit -m "feat(osd): Goldmessung des Zeichenleser-Kandidaten"
```

---

## Abschluss von Stufe 1

Nach Aufgabe 8 steht die Werkzeugkette. Der eigentliche Durchlauf ist dann:

1. Ernte über Archivbilder laufen lassen
2. Kunstbilder erzeugen
3. Datensatz zusammenstellen
4. Trainieren (Sidecar vorher beenden)
5. Lesungen auf dem Reservebestand erzeugen, Schwelle kalibrieren und einfrieren
6. `osd_modell_goldmessung.py` **einmal** laufen lassen

Das Ergebnis von Schritt 6 entscheidet, ob Stufe 2 (die 200 Handfälle) nötig ist.

**Die volle Sidecar-Suite muss am Ende grün sein:**

```bash
cd sidecar && .venv/Scripts/python.exe -m pytest -q
```

`osd_meter.py` wurde in dieser Stufe nicht angefasst — die bestehenden Tests
müssen unverändert bestehen, und der heutige Leser liefert weiterhin seine
138 richtigen Werte bei null falschen.
