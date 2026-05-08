# ADR-0002: Charakterisierungs-Tests als Sicherungsnetz vor grossen Refactors

- **Status**: accepted
- **Datum**: 2026-05-07
- **Verantwortlich**: Solo-Entwicklung

## Kontext

Vor dem geplanten HoldingFolderDistributor-Refactor (siehe ADR-0001)
hatte die Klasse trotz 4.576 Zeilen und 12 Public-Methoden nur **3 echte
Verhaltens-Tests**:

- `HoldingTxtDistributionTests` (1 Happy-Path)
- `HoldingFolderDistributorVideoMatchingTests` (2 Reflection-Tests auf
  privates)

Plus: 11 ParsePdf-Tests in `WinCanPdfParsingTests` und 2 ParseSchachtPdf-
Tests in `SchachtPdfParsingTests`. Die `ErstfeldJagdmattDiagnose`-Tests
waren reine WriteLine-Reports ohne Asserts.

**8 von 12 Public-Methoden waren komplett ungetestet.** Ein Refactor
ohne Sicherheitsnetz waere ein Glücksspiel gewesen.

## Entscheidung

**Charakterisierungs-Tests** im Sinne von Michael Feathers
(*Working Effectively with Legacy Code*) schreiben, **bevor** auch nur
eine Methode bewegt wird:

- Tests dokumentieren das **heutige Verhalten** der Klasse — auch
  unschoene Eigenheiten (z.B. Asymmetrien zwischen Methoden).
- Sie sind **keine Soll-Spezifikation**, sondern Ist-Sicherung.
- Beim Refactor schlagen sie an, sobald sich Verhalten aendert
  — dann muss bewusst entschieden werden ob das gewuenscht ist.

22 Tests wurden in 4 Chargen geschrieben:

| Charge | Tests | Was eingefroren wird |
|---|---:|---|
| 1 | 8 | Argument-Validation (Distribute / DistributeFiles / DistributeTxt) |
| 2 | 4 | DistributeTxtFiles Verhalten (NotFound / Ambiguous / Overwrite / Move) |
| 3 | 8 | DistributeShafts + DistributeDichtheit Argument-Validation + **Asymmetrien** |
| 4 | 2 | recursiveVideoSearch A/B-Symmetrie |

**Wichtig: Charge 3 friert bewusst Asymmetrien ein**, z.B.:
- `DistributeShafts` Empty-Message: `"No PDF files found (recursive) in:"`
- `DistributeDichtheit` Empty-Message: `"No PDF files found in:"` (ohne `(recursive)`)
- `DistributeShaftFiles` filtert `split_*.pdf`, `DistributeDichtheitFiles` **nicht**.

Beim Refactor wuerde eine "Vereinheitlichung" sofort Tests brechen
— gewollter Bremseffekt.

## Alternativen erwogen

1. **Direkt refactoren ohne Tests**: das war die Position vor dem Audit.
   Verworfen wegen 4.576-Zeilen-Risiko.
2. **Vollstaendige Unit-Tests aller Methoden**: zu viel Aufwand fuer
   einen einmaligen Refactor. Charakterisierungs-Tests sind das
   Minimum.
3. **Integration-Tests gegen echte PDF-Fixtures**: zu langsam,
   Dateipfade-abhaengig.

## Konsequenzen

**Positiv:**
- Refactor in 19 Chargen ohne einen einzigen Verhaltens-Bruch.
- 22 Tests laufen unter 700 ms — schnell genug fuer jeden Edit-Test-Cycle.
- Tests sind **selbst-dokumentierend**: sie zeigen das Verhalten
  expliziter als jeder Kommentar.
- Asymmetrien sind fuer immer sichtbar — wer sie aendern will,
  muss die Tests anpassen + es wird sichtbar im Diff.

**Negativ:**
- Tests friert auch unschoene Eigenheiten ein. Wenn man die spaeter
  bereinigen will, muss man Tests + Code parallel anpassen.
- Etwa 2 Tage Aufwand vor dem eigentlichen Refactor.

## Lehre

**Bei jeder Klasse > 1.000 Zeilen vor einem Refactor:**

1. Public-API-Inventur (welche Methoden gibt es?)
2. Coverage-Karte (wie viele Tests pro Methode?)
3. Wenn < 50 % Coverage: Charakterisierungs-Tests in Chargen schreiben.
4. Erst dann mit dem Refactor beginnen.

## Referenzen

- Test-Datei: `tests/AuswertungPro.Next.Infrastructure.Tests/HoldingFolderDistributorCharacterizationTests.cs`
- Commits: `c96c500` (Charge 1), `bd23d45` (Charge 2), `5aab246` (Charge 3), `cd6f52f` (Charge 4)
- Buch: Michael C. Feathers, *Working Effectively with Legacy Code*, 2004
