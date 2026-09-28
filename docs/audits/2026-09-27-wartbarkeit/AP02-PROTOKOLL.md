# AP02 – Arbeitsprotokoll: Trainingsskript-Tests in der CI

Stand: 28.09.2026. Plan: [UMSETZUNGSPLAN.md](UMSETZUNGSPLAN.md), Paket AP02.

## Ausgangsstand

- 38 Testdateien unter `training/scripts/tests`, von keinem automatischen Schritt ausgeführt.
- Die CI (`.github/workflows/ci.yml`) läuft bei jedem Push ohne Pfadfilter; ein neuer Schritt
  greift also auch bei einer reinen Änderung an `training/scripts`.

## Inventur

| Frage | Ergebnis |
|---|---|
| Reine CPU-Prüfung? | Ja, alle 38 Dateien. Keine GPU, kein Modell, keine Kundendaten; Daten entstehen in temporären Ordnern. |
| Optionale Abhängigkeiten? | Keine. Es gibt keine einzige Skip-Regel (`skipIf`, `importorskip` …); ein fehlendes Paket ist ein Sammelfehler. |
| Echte Prozesse / Hardware? | Nein. `test_subprocess_timeouts.py` prüft nur die gesetzten Zeitlimits. |
| Benötigte Pakete | `numpy`, `pillow`, `pytest` – Versionen wie in `sidecar/requirements-lock.txt`. |
| Aufruf | Vom Projektwurzelverzeichnis, weil einige Tests `training.scripts…` und `tools…` importieren. |

**Befund:** In der Sidecar-Umgebung scheitern die Tests. SAM 2 (`sam_2-1.0`) installiert ein
eigenes Top-Level-Paket `training` in `site-packages`; ein regulär installiertes Paket verdeckt
in Python immer den Projektordner `training/` (kein `__init__.py`, Namespace-Paket). Deshalb
läuft der Schritt in einer eigenen, frischen Umgebung.

## Umsetzung

- `training/scripts/tests/requirements-test.txt`: numpy 2.5.2, pillow 12.3.0, pytest 9.0.3.
- CI, Job `python`: Schritt „Trainingsskript-Tests (CPU)“, eigene venv, Bash mit
  `-eo pipefail` (Standard der GitHub-Bash), damit ein Installations- oder Testfehler den
  Schritt rot macht. Bestehende Sidecar- und QGIS-Schritte unverändert.
- `AGENTS.md`: Prüfbefehl für lokale Arbeit an `training/scripts`; `.gitignore`: `.venv-training/`.
- Keine Änderung an produktiven Trainingsskripten.

## Nachweise

- Zwei frische Umgebungen: 324 Tests + 46 Untertests grün in rund 12 s, auch in umgekehrter
  Dateireihenfolge.
- CI-Befehle lokal mit `bash -eo pipefail` nachgestellt: Exit 0; ohne Pillow Exit 2.
- Gegenprobe Klassenkarte: `BCC_bogen` in `detect_class_map_v3.json` umbenannt → 40 Tests rot.
- Gegenprobe Negativsatz: Die Prüfung „Set-ID passt zum semantischen Beleg“ in
  `gold_stock_audit.py` ausgeschaltet → **alles blieb grün**. Der vorhandene Test änderte nur
  die Set-ID; das fängt auch die Ordnernamen-Prüfung mit gleichlautender Meldung ab.
  Neu: `test_geaenderter_beleg_bei_unveraenderter_set_id_wird_abgelehnt` (Inhalt geändert,
  Set-ID und Ordner stimmig). Echt grün, mit ausgeschalteter Prüfung rot. Jetzt 325 Tests.
- Alle Gegenproben danach bytegenau zurückgesetzt.

## Restgrenze

- Die erste Ausführung in der echten GitHub-CI steht noch aus (nach dem nächsten Push).
- Das Modelltraining selbst bleibt ein eigener Ablauf und ist nicht Teil dieses Schritts.
- Rücknahme: nur den CI-Schritt entfernen; die Paketliste stört nicht.
