# Phase 5.3 — KI-Schicht aus UI: Fortschritt + Plan

**Stand:** 2026-05-07

## Ziel

Code aus `src/AuswertungPro.Next.UI/Ai/**` schrittweise nach
`Application/Ai` (Vertraege, pure Logic) bzw. `Infrastructure/Ai`
(Implementierungen mit externen Systemen) verschieben — analog zu
Phase 5.1 (ServiceProvider zerlegen) und 5.2 (Module).

Layer-Regel:
- **Domain**: pure Modelle, keine Abhaengigkeiten
- **Application**: Vertraege (Interfaces), DTOs, pure Use-Case-Logic
- **Infrastructure**: Implementierungen mit externen Systemen (HTTP, ffmpeg, SQLite)
- **UI**: WPF-Code (XAML, ViewModels)

## Erledigt (Sub-A + Sub-B)

| Sub | Klassen | Commit |
|---|---|---|
| **A** | `AiSuggestionTypes` (record, interface, DTO, Schema) | `25f4d7029` |
| **B** | `VsaCatalog`, `FfmpegLocator`, `MeterTolerances`, `RuleBased-/NoopAiSuggestionPlausibilityService` | `d9fffb8f7` |

**Total bisher:** 7 Klassen migriert, 30+ Aufrufer-Files angepasst.

## Plan fuer naechste Sub-Phasen

### Sub-C: Pipeline-DTOs nach Application/Ai
Pure Datentypen aus dem Pipeline-Subfolder:
- `PipelineConfig.cs` (record)
- `PipelineVersions.cs` (Konstanten)
- `AiRuntimeConfig.cs` (record)
- `IVideoAnalysisPipelineService.cs` (Interface)

Aufwand: ~30 min, geringes Risiko.

### Sub-D: KnowledgeBase-Vertraege nach Application/Ai/KnowledgeBase
Heute existieren bereits `IRetrievalService` und `KnowledgeBaseDtos` in
Application — fehlende Stuecke:
- `EmbeddingService.cs` (HTTP zu Ollama → Infrastructure)
- `RetrievalService.cs` (Implementierung → Infrastructure)
- `KnowledgeBaseContext.cs` (SQLite → Infrastructure)
- `KnowledgeBaseManager.cs` (Pure Logic → Application)

Aufwand: 2–3 h. Tests muessen angepasst werden.

### Sub-E: Pure Pipeline-Services nach Application/Ai
Services ohne externe Abhaengigkeiten:
- `DetectionAggregator.cs` (Temporal Voting — pure Logic)
- `QualityGateService.cs` (Green/Yellow/Red — pure Logic)
- `LiveDetectionMapper.cs` (Mapping — pure Logic)
- `AiOverlayConverter.cs` (Mapping — pure Logic)

Aufwand: 2 h, geringes Risiko (kein externer State).

### Sub-F: HTTP-/Ollama-Implementations nach Infrastructure/Ai
Klassen mit externer Abhaengigkeit (HTTP):
- `OllamaClient.cs` (Polly Retry + Circuit Breaker)
- `OllamaProtocolAiService.cs`
- `OllamaVisionFindingsService.cs`
- `EnhancedVisionAnalysisService.cs`

Aufwand: 4–6 h. Interface muss in Application liegen, Implementation
nach Infrastructure ziehen.

### Sub-G: Sidecar-Plumbing nach Infrastructure/Ai
Python-Sidecar via FastAPI:
- `PythonSidecarService.cs` (Lifecycle)
- `Pipeline/VisionPipelineClient.cs` (HTTP-Client)
- `Pipeline/MultiModelAnalysisService.cs`
- `Pipeline/BatchPipelineService.cs`

Aufwand: 6–8 h, hoechstes Risiko (Lifecycle + Process-Handling).

### Sub-H: Training-Services nach Infrastructure/Ai
ffmpeg-getriebene + ML-Training-Services. Sehr volumig (~30 Files).
Aufwand: 1–2 Tage.

## Verbleibend in UI/Ai (bewusst)

- `Ai/PhotoAssistant/**`: nutzt `System.Drawing` — UI-naehe ok
- `Ai/Shared/WalkerHealthCheck.cs`: orchestriert UI-Services
  (folgt Sub-D/F/G nach)
- `Ai/Pipeline/SamMaskRenderer.cs`: WPF-Drawing

## Sub-G/H sollte nach Sub-D/F kommen
Reihenfolge: **A → B → C → D → E → F → G → H**

Sub-A/B sind Pflicht (Abhaengigkeits-Basis), C/D/E koennen unabhaengig
voneinander parallel. F/G/H bauen auf D/E auf.
