# SewerStudio — Claude Code Kontext

## Projekt
WPF/.NET 10 Desktop-App für Kanalinspektion nach VSA-KEK 2023 / EN 13508-2.
Solo-Entwicklung. Keine Cloud. Keine kommerziellen Ziele.
Hardware: Intel Ultra 9 285K · RTX 5090 32 GB · 64 GB DDR5

## Architektur (Clean Architecture, strikt)
```
Domain        → reine Modelle, keine externen Abhängigkeiten
Application   → Interfaces, DTOs, Service-Verträge
Infrastructure→ JSON, Import/Export, SQLite, PDF
UI (WPF)      → ViewModels (CommunityToolkit.Mvvm), KI-Services, Overlays
sidecar/      → Python FastAPI, Port 8100 (YOLO · DINO · SAM)
```
MVVM: [ObservableProperty] / [RelayCommand] — kein Code-Behind.
KI-Orchestrierung liegt aktuell in UI/Ai/ (bewusste Solo-Entscheidung, kein Refactor nötig).

## Kritische Domain-Typen
- `HaltungRecord` — Dictionary<string,string> (35 Felder, heterogene Importe)
- `ProtocolEntry` — VSA-Code, MeterStart/End, Uhrposition, Source (Imported/Manual/Ai)
- `CodingSession` — States: NotStarted→Running→Paused→WaitingForUserInput→Completed/Aborted
- `ProtocolDocument` — Original + Current ProtocolRevision + History + Audit-Trail
- `VsaFinding` — EZD/EZS/EZB (0–4), Quantifizierung1/2, MeterStart/End

## KI-Pipeline (Thin-AI-Prinzip)
C# = gesamte Geschäftslogik, Messungen, Orchestrierung
Python Sidecar = reine Modell-Inferenz
Ollama = Textgenerierung (Qwen2.5-VL-32B Q5 via http://localhost:11434)

Pipeline-Pfade:
  A (Sidecar): YOLO26m → DINO 1.5 → SAM 3 → Qwen → ByteTrack
  B (Fallback): Qwen direkt (VideoFullAnalysisService)

VRAM-Budget (RTX 5090, 32 GB):
  YOLO ~1.5 GB permanent · Qwen ~26 GB permanent
  DINO ~3 GB + SAM ~8 GB → on-demand, nie gleichzeitig (Peak 37 GB!)
  DINO/SAM werden nach Gebrauch entladen.

## QualityGate (8 Signale, Green/Yellow/Red)
Green ≥ 0.75 → Auto-Accept · Yellow ≥ 0.45 → Review · Red < 0.45 → DINO+SAM nachladen

## Bekannte gelöste Bugs (nicht nochmals einführen)
1. SQLite Pooling — KnowledgeBase.db Datei-Handle nach Close():
   Fix: Pooling=False in Connection String + SqliteConnection.ClearAllPools() vor Import
2. KbCodesCovered zeigte max 20 statt echtem Count:
   Fix: separater COUNT(DISTINCT code) Query statt TopCodes.Count

## Wichtige Dateipfade
UI/Ai/VideoAnalysisPipelineService.cs     ← Haupt-Orchestrator
UI/Ai/Pipeline/MultiModelAnalysisService.cs
UI/Ai/QualityGate/QualityGateService.cs
UI/Ai/AiOverlayConverter.cs
UI/ViewModels/Windows/CodingSessionViewModel.cs
sidecar/sidecar/main.py                   ← FastAPI App
sidecar/sidecar/gpu_manager.py            ← Multi-Slot GPU Manager

## Coding-Regeln
- .NET 10, C# 13, nullable enabled
- CommunityToolkit.Mvvm — [ObservableProperty], [RelayCommand]
- Kein Code-Behind in XAML
- Async/await konsequent (kein .Result / .Wait())
- Thin-AI: LLM nur für Textgenerierung, nie für Geschäftslogik
- VSA-Codes aus VsaCatalog.cs laden, nie hardcoden
- VRAM-Peak vermeiden: DINO und SAM nie gleichzeitig laden
- SQLite: immer Pooling=False + ClearAllPools() bei DB-Operationen auf KnowledgeBase.db
- Tests: XUnit (Infrastructure.Tests, Pipeline.Tests) · Python: pytest

## Standards
VSA-KEK 2023 · SN EN 13508-2 · SIA 405
B-Codes: BAA–BHB · EZD/EZS/EZB 0–4 · BCD=Haltungsanfang · BCE=Haltungsende
