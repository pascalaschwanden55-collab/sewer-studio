# BAH-Verfügbarkeit PDF-Kanal (Scan 2026-08-06)

Werkzeug: `tools/PdfCodeScanner` über den echten Importpfad
(`LegacyPdfImportService` — deutsche Protokollbeschreibungen, keine
Code-Regex). Quelle: `artifacts/bah-pdf-kanal-20260806/scan-v2.json`
(1476 Ordner, 0 Importfehler).

## Ergebnis

| Gruppe | Haltungen |
|---|---:|
| Mit BAH-Befund (360 Befunde, BAHC 148 / BAHD 5 / BAHE 1) | **151** |
| … alle mit eingebetteten Fotos | 151 |
| Davon bereits in Gold | 65 |
| Davon nur im Benchmark (Holdout) | 47 |
| **Davon frei (weder Gold noch Benchmark)** | **39** |

## Einordnung

- Die Inventur-Schätzung (120–150 verfügbare BAH-Haltungen) bestätigt sich:
  151 Haltungen mit Foto. 132 PDFs ohne Textebene wurden korrekt importiert.
- **Das Trainingsziel „50–70 BAH-Haltungen" ist belegt erreichbar:**
  65 bereits in Gold + 39 frei = 104 erreichbare Haltungen. Selbst mit
  Reserve für künftige Benchmark-Bedürfnisse reichen 13–33 der 39 Freien.
- Aus XTF/WinCan kommt dagegen nichts Neues (Pool von 49 restlos in Gold
  aufgegangen) — BAH-Sammlung läuft zwangsläufig über diesen PDF-Kanal.
- Auffällig: `06.691078-691070` steht in der Frei-Liste — das ist der
  Quarantäne-Ordner aus der offenen Liste. Er bleibt Pascals Entscheid.
- Die 47 Benchmark-Haltungen sind tabu (Schutzquellen greifen bereits).

## Konsequenz für die Arbeitsliste

1. Review der Benchmark-Erweiterung (17 Bilder) durch Pascal — danach steht
   die BAH-Abdeckung bei ~22 Sollboxen aus 11+ Haltungen.
2. BAH-Sammlung aus den 39 freien Haltungen, Ziel ~50–70 im Training;
   ein Rest bleibt als Reserve markiert.
3. Drei Seeds je Bedingung für künftige Vergleiche (Varianz-Disziplin aus
   `detect-strategie-2026-08-06.md`).
