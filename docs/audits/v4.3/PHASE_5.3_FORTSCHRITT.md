# Phase 5.3 — KI-Schicht aus UI: Fortschritt + Plan

**Stand:** 2026-05-07 (Update nachmittags)

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

## Erledigt (Sub-A bis Sub-E + Sub-D Teil 1)

| Sub | Klassen | Commit |
|---|---|---|
| **A** | `AiSuggestionTypes` (record, interface, DTO, Schema) | `25f4d7029` |
| **B** | `VsaCatalog`, `FfmpegLocator`, `MeterTolerances`, `RuleBased-/NoopAiSuggestionPlausibilityService` | `d9fffb8f7` |
| **C** | `PipelineConfig` + `PipelineMode`, `PipelineVersions`, `AiRuntimeConfig` (Plus `AiRuntimeConfigExtensions` in UI) | `ee50354ba` |
| **E** | `DetectionAggregator`, `DetectionEvent`, `QualityGateService`, `CategoryWeights`, `EvidenceVector`, `UncertaintyEstimate` | `49037240d` |
| **D Teil 1** | `KnowledgeBaseContext`, `KnowledgeBaseWriter`, `KnowledgeBaseDiagnosticsService` (nach Infrastructure) + `KnowledgeBasePathProvider` (in Application) | `da5856031` |

**Total bisher:** 16 Klassen migriert (von ~60 produktiven Klassen in `UI/Ai/`).
**Aufrufer-Files angepasst:** 80+.

## Plan fuer naechste Sub-Phasen

### Sub-C: Pipeline-DTOs nach Application/Ai
Pure Datentypen aus dem Pipeline-Subfolder:
- `PipelineConfig.cs` (record)
- `PipelineVersions.cs` (Konstanten)
- `AiRuntimeConfig.cs` (record)
- `IVideoAnalysisPipelineService.cs` (Interface)

Aufwand: ~30 min, geringes Risiko.

### Sub-D Teil 2 (offen): Verbleibende KB-Klassen

In UI verblieben (haben UI-Abhaengigkeiten — TrainingSample / OllamaConfig /
AppSettings):
- `EmbeddingService.cs` (nutzt OllamaConfig)
- `RetrievalService.cs` (nutzt AppSettings)
- `KnowledgeBaseManager.cs` (nutzt TrainingSample)
- `KbDeduplicationService.cs` (nutzt TrainingSample)
- `KbEnrichmentService.cs` (nutzt TrainingSample)
- `KbIngestionPipeline.cs` (nutzt TrainingSample)

Migration erfordert vorher: TrainingSample und OllamaConfig nach
Application/Domain ziehen. Das ist Sub-H bzw. Sub-F.

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

## Reihenfolge: **A → B → C → E → D Teil 1 → F → D Teil 2 → G → H**

Sub-A/B/C/E sind komplett.
Sub-D Teil 1 (SQLite-Layer) ist done.
Sub-D Teil 2 braucht zuerst Sub-F (Ollama-Migration: bringt OllamaConfig
nach Application/Infrastructure) und Sub-H (TrainingSample-Migration).

## Realistische Aufwand-Einschaetzung (Stand 2026-05-07 nachmittags)

| Sub | Pure Migration | Mit Cross-Layer-Refactor |
|---|---|---|
| F (Ollama) | 4–6 h | 8–10 h |
| G (Sidecar) | 6–8 h | 10–12 h |
| H (Training) | 1–2 Tage | 3–4 Tage |
| D Teil 2 | nach F + H | — |

Empfehlung: nicht alles in einer Session — pro Sub-Phase einen klaren
Tag mit Kontext-Einarbeitung und ausreichend Pufferzeit fuer Tests.
