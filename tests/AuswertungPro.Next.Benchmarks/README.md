# AuswertungPro.Next.Benchmarks

Roadmap Z.1: BenchmarkDotNet-Suite für Performance-Regressions-Tests an kritischen CPU-Workloads.

## Was wird gemessen

| Benchmark | Hot-Path | Soll-Schwelle |
|---|---|---|
| `BeobachtungParserBench.Parse_Large_500Rows` | Operateur-Annotation Submodus-Start mit Sammler-Protokoll | < 100 ms |
| `VsaYoloClassMapBench.TryGetClassId_Known` | Confirm-Pfad pro Sample | < 1 µs |
| `TrainingSampleSignatureBench.Build_NoClock` | Self-Training Dedup-Pfad pro Sample | < 1 µs |

YOLO-Sidecar-Calls und KB-SQLite-Queries sind **bewusst nicht** in dieser Suite — die brauchen echte Hardware/Netz/IO und gehören in eine separate Integrations-Bench (z.B. mit laufendem Sidecar im CI-Container, Slice 2+).

## Bewusst nicht in der Solution

Das Projekt ist nicht in `AuswertungPro.sln` eingebunden. BenchmarkDotNet erzwingt Release-Build und würde CI verlangsamen. On-demand ausführen.

## Ausführung

Alle Benchmarks:

```powershell
dotnet run -c Release --project tests/AuswertungPro.Next.Benchmarks -- --filter '*'
```

Einzelne Klasse:

```powershell
dotnet run -c Release --project tests/AuswertungPro.Next.Benchmarks -- --filter '*BeobachtungParserBench*'
```

Auflistung der verfügbaren Benchmarks:

```powershell
dotnet run -c Release --project tests/AuswertungPro.Next.Benchmarks -- --list flat
```

## Ergebnis-Ablage

BenchmarkDotNet legt Ergebnisse unter `BenchmarkDotNet.Artifacts/` ab (CSV + HTML-Report + Markdown). Bei Regressions-Verdacht: aktuelles Ergebnis mit dem letzten committeten vergleichen, das in `docs/perf/` abgelegt sein sollte.

## Noch nicht enthalten (Slice 2+)

- PdfTextExtractor (braucht echte PDFs als Test-Fixtures, ggf. lizenzrechtlich heikel)
- YOLO-Sidecar-Calls (braucht laufenden Sidecar)
- KB-SQLite-Queries (braucht KnowledgeBase.db als Fixture)
- Memory-Allocation-Profile für die Operateur-Annotation Hot-Path-Box-Drag-Sequenz
