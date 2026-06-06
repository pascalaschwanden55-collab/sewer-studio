# PDF-Coverage-Audit — Befunde (Stand 2026-06-06)

Lauf des **echten** `PdfProtocolExtractor` (Training-Parser) ueber `D:\Haltungen`
+ `H:\02_Sanierung_Abnahmedoku_Kunde_25100490`.

## Gesamt
- **1.882 PDFs**, **1.534 erkannt (81,5 %)**, **348 mit 0 Befunden**, **0 Abstuerze**.
- Wichtig: `ExtractFromPdf` schluckt Fehler und gibt leer zurueck
  (`PdfProtocolExtractor.cs` catch → `Array.Empty`). „0 Befunde" heisst also
  NICHT automatisch „schadenfreies PDF" — es kann ein Parser-/Lesefehler sein.

## Kategorisierung der 348 „0 Befunde" (anhand der Diagnose-Text-Dumps)
| Anzahl | Kategorie | Echte Luecke? |
|---|---|---|
| **68** | **Pallon-Layout** `[Meter] ZEIT CODE Text [Uhr] [Foto]` (Codes im Text, von keiner Strategie gematcht) | ✅ ja — neue Strategie |
| **51** | **Alt-Fretz 2017** „Beobachtung"-Spalte mit deutschem Text, KEINE VSA-Codes | ✅ ja — Text→Code-Map |
| **~18** | Codes da, aber anderes Layout: Meter-in-Klammern `(22,17 m) CODE`; PdfPig-Zeichen-Verdopplung (`EEEnnntttfff…`) | teils ja (Klammern), teils schwer (Artefakt) |
| 115 | Kein Text-Dump (PdfPig liefert keinen Text → gescannt/Bild/Lesefehler) | OCR noetig (groesser) |
| 76 | Text aber keine Codes (DP-Protokolle, „Haltung N.pdf", Stammdaten-only) | meist korrekt leer |
| 10 | Plan/Situation | korrekt leer |
| 9 | Kein/kaum Text (gescannt) | OCR noetig |
| 1 | Inspektion 0,00 m (abgebrochen) | korrekt leer |

## Echte, gezielt behebbare Training-Luecken (Prioritaet)
1. **Pallon** (68) — Reihenfolge `[Meter] ZEIT CODE Text`, Meter optional. Eine neue Regex-Strategie.
2. **Alt-Fretz 2017** (51) — deutsche Beobachtungs-Begriffe → VSA-Code (Rohranfang→BCD, Rohrende→BCE, Bogen→BCC, Anschluss→BCA …).
3. **Meter-in-Klammern** `(22,17 m) CODE` — kleine Zusatz-Regex.

## Bewusst (vorerst) NICHT
- Gescannte PDFs ohne Text (115+9) → braeuchte OCR; viele davon sind ohnehin DP/korrekt leer.
- PdfPig-Zeichen-Verdopplungs-Artefakt → fragiles De-Dup, wenige Dateien.

## Tool nutzen
```
dotnet run --project tools/PdfCoverageAudit -c Release -- "D:\Haltungen" "H:\..."
```
Ohne Argumente nimmt es die zwei o.g. Wurzeln. Schreibt `C:\tmp\pdf_coverage_report.csv`
(Status;Befunde;Pfad;Beispiel-Codes;Fehler). Read-only, aendert nichts an PDFs/KB.
