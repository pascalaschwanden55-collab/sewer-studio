# ADR-0001: HoldingFolderDistributor in 16 Partials zerlegt

- **Status**: accepted
- **Datum**: 2026-05-08
- **Verantwortlich**: Solo-Entwicklung

## Kontext

`HoldingFolderDistributor` war eine **4.576-Zeilen-Klasse** mit 12 public
Methoden, ueber 100 private Helpern, 17 Regex-Patterns und nested Records
in einer einzigen `.cs`-Datei. Die Klasse ist aus historischen Gruenden
gewachsen: jede neue PDF-Format-Variante (KIT Bauinspekt, Abwasser Uri,
IBAK direkt, Fretz, KINS) hat private Helfer angelegt, ohne Re-Strukturierung.

Probleme:
- Cognitive Load: ein Refactor erforderte das Lesen der ganzen Datei.
- Merge-Konflikte beim parallelen Arbeiten extrem wahrscheinlich.
- Keine klare Domain-Grenze zwischen Parser, Video-Matching, Distribution.
- IDE-Navigation langsam (4.576 Zeilen).

## Entscheidung

`HoldingFolderDistributor` bleibt eine `public static class` mit ihrem
oeffentlichen API-Vertrag, wird aber **physisch auf 16 Partials** verteilt:

| Partial | Inhalt | Zeilen |
|---|---|---:|
| `HoldingFolderDistributor.cs` | Coordination + Caches | ~190 |
| `.Regex.cs` | 17 Regex-Patterns + Konstanten | ~80 |
| `.Types.cs` | Public Records + Enum | ~50 |
| `.Util.cs` | ID-/Filename-Normalisierung + TryFindFilmName + TrimLeadingZerosValue | ~250 |
| `.IO.cs` | MoveOrCopy, EnsureUniquePath, Marker-Files, FindExistingVideo | ~120 |
| `.PdfReading.cs` | ReadPdfPages, NormalizeText, BuildPageRange, IsContentsPage | ~115 |
| `.PdfManipulation.cs` | WritePdfPages, AppendPdfFile, In-Place-Text-Korrektur | ~260 |
| `.DateParsing.cs` | TryParseDateString, TryFindInspectionDate, TryFindSchachtDate | ~170 |
| `.HaltungExtraction.cs` | TryFindHaltungId, TryExtractFromShafts, etc. | ~510 |
| `.PdfHaltungParsing.cs` | ParsePdf, ParsePdfPage, OCR-Fallbacks, SplitPdfIntoHoldings | ~370 |
| `.SchachtPdfParsing.cs` | ParseSchachtPdf, Form-Field-Helpers, Sibling-Date, SplitPdfIntoShafts | ~370 |
| `.TxtParsing.cs` | ParseTxtSections (KINS-Format) | ~120 |
| `.VideoMatching.cs` | FindVideo, Sidecar-Index, CDIndex, Photo-Hint-Voting | ~570 |
| `.PhotoHints.cs` | ExtractPhotoHintsFromPdf + Photo-Token-Regex | ~95 |
| `.Distribute.cs` | Distribute / DistributeFiles / DistributeTxt + Cores | ~470 |
| `.DistributeShafts.cs` | DistributeShafts + Cores | ~175 |
| `.DistributeDichtheit.cs` | DistributeDichtheit + Multi-Page-Splitting | ~370 |
| `.Pipeline.cs` | HandleParsedDistribution, FindRecordByHolding, TryMatchPdfToHolding | ~390 |

## Alternativen erwogen

1. **Mehrere kleine Klassen** statt Partials: haette die Public-API
   geaendert (Naming, Aufrufstellen). Riskant ohne saubere Tests.
2. **Eine `HoldingFolderDistribution.Internal`-Sub-Class** mit den Helfern:
   waere C#-untypisch und haette IntelliSense-Pfade verschlechtert.
3. **Status quo lassen**: war keine Option — die Wartbarkeit war akut.

## Konsequenzen

**Positiv:**
- Hauptdatei jetzt < 200 Zeilen (von 4.576 — 96 % weg).
- Domain-Trennung explizit (Parser vs. Matcher vs. Distributor).
- Merge-Konflikte deutlich unwahrscheinlicher.
- Neue PDF-Formate gehen in den passenden Parser-Partial.
- IDE-Navigation schnell (Files thematisch).

**Negativ:**
- Compiler muss alle Partials einer Klasse zur Compile-Zeit zusammenfuehren —
  marginal langsamer, aber irrelevant.
- Methoden-Lookup via "Go to Definition" springt auf den richtigen Partial,
  aber Datei-Liste in der Solution Explorer wird laenger.
- Kosmetisch: die 16 Files muessen alle die `partial`-Deklaration tragen,
  was leichten Boilerplate-Aufwand bedeutet.

## Sicherungsnetz

Vor dem Refactor wurden **22 Charakterisierungs-Tests** geschrieben
(siehe ADR-0002). Diese liefen durch alle 19 Refactor-Chargen
(R1–R19) hindurch gruen.

## Referenzen

- Refactor-Commits: `0612e13` (R1) bis `c3799cd` (R15-R19)
- Charakterisierungs-Tests: `tests/AuswertungPro.Next.Infrastructure.Tests/HoldingFolderDistributorCharacterizationTests.cs`
