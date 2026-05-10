# Phase 6.3 File-Move: MultiModelAnalysisService → Infrastructure — Mini-ADR

Datum: 2026-05-10
Status: **Entschieden** (Folge-Slice der WPF-Decouple-ADR von 2026-05-10)

Vorgeschichte:
- WPF-Decouple-Slice `f25ec49` hat MultiModelAnalysisService WPF-frei
  gemacht. Beide Partials sind verifiziert ohne System.Windows-Imports.
- ARCH-H5 / CLAUDE.md "Thin-AI": KI-Logik soll nicht in UI leben.

## Was diese ADR macht

Verschiebt `MultiModelAnalysisService.cs` + `MultiModelAnalysisService.Helpers.cs`
von `src/AuswertungPro.Next.UI/Ai/Pipeline/` nach
`src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/`. Namespace
wechselt von `AuswertungPro.Next.UI.Ai.Pipeline` zu
`AuswertungPro.Next.Infrastructure.Ai.Pipeline`. ~15 Aufrufer-Files
bekommen ein zweites `using` (oder ein Update, je nach Nutzung der
anderen Klassen im UI.Ai.Pipeline-namespace).

## Was diese ADR NICHT macht

- Keine Aenderung an `SamMaskRenderer.cs` (heavy-WPF, bleibt in UI/Ai/Pipeline).
- Keine API-Aenderung des Services.
- Keine Tests umbauen — die Tests in `Pipeline.Tests` referenzieren
  weiterhin denselben Klassennamen, nur namespace-using-Update.

## Bestandsaufnahme

Files in `UI/Ai/Pipeline/`:
- `MultiModelAnalysisService.cs` (1379 LOC, WPF-frei) → moves
- `MultiModelAnalysisService.Helpers.cs` (663 LOC, WPF-frei) → moves
- `SamMaskRenderer.cs` (548 LOC, WPF heavy) → bleibt

Aufrufer: ~15 Files mit `using AuswertungPro.Next.UI.Ai.Pipeline`.
Manche nutzen NUR MultiModelAnalysisService, manche auch SamMaskRenderer
oder andere Klassen (DetectionAggregator/MaskQuantificationService leben
in `Application.Ai.Pipeline`, nicht in UI).

## Migrations-Schnitt

### Step 1: File-Move + Namespace-Wechsel

- `git mv` der zwei Files nach `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/`.
- Namespace in beiden Files auf `AuswertungPro.Next.Infrastructure.Ai.Pipeline` setzen.

### Step 2: Aufrufer-Updates

- Pro Caller: pruefen ob noch andere Klassen aus `UI.Ai.Pipeline`
  referenziert werden.
  - Nur MultiModelAnalysisService → `using` aendern auf
    `Infrastructure.Ai.Pipeline`.
  - Auch andere → zweites `using Infrastructure.Ai.Pipeline` ergaenzen.
- Mechanisch, build catches Compile-Fehler.

### Step 3: Doku + CHANGELOG

ADR Status auf Done. CHANGELOG-Eintrag.

## Verifikation

- Build: 0 Warn / 0 Err.
- Tests: 1025 PASS, kein Regression (insbesondere
  MultiModelAnalysisServiceTests + ServiceCollectionConfiguratorTests
  + VisionPipelineClientTests).
- Kein UI-Smoke noetig — reiner Refactor ohne Verhaltensaenderung.
