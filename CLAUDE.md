# SewerStudio — AI Sewer Inspection System

## Projekt-Kontext
- **App:** WPF / .NET 8+, MVVM, Windows 11
- **Zweck:** Automatisierte Kanalinspektion, ~3000 WinCan/Ikas IBSK Videos
- **Standards:** EN 13508-2, VSA-KEK Schweiz 2023
- **Entwickler:** Solo, kein kommerzielles Ziel
- **Hardware:** Intel Core Ultra 9 285K · ASUS RTX 5090 32GB · 64GB DDR5

## AI-Pipeline (lokal, Workstation-Mode)
- YOLO26m-seg         → permanent GPU, ~1.5GB VRAM
- Qwen2.5-VL-32B Q5  → permanent GPU, ~26GB VRAM (via Ollama)
- Grounding DINO 1.5  → on-demand, nur bei QualityGate Yellow/Red
- SAM 3               → on-demand, exklusiv mit DINO
- ByteTrack/OC-SORT   → CPU, immer aktiv

## Architektur-Prinzipien (NICHT brechen)
- Thin-AI: C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung
- Kein grosses Refactoring ohne explizite Diskussion
- Laptop-Mode / Workstation-Mode Hardware-Abstraktion erhalten
- VRAM-Budget: max 29GB stabil, niemals alle Modelle gleichzeitig
- QualityGate Green/Yellow/Red muss immer durchlaufen

## Inference-Orchestrator Zustaende
1. DETECT  → GPU: YOLO | CPU: Tracker + Aggregator
2. SEGMENT → GPU: YOLO + SAM | Qwen: entladen
3. CLASSIFY→ GPU: YOLO + Qwen | SAM/DINO: entladen

## Build & Test
```bash
dotnet build AuswertungPro.sln
dotnet test --filter Category=Recommendation
```

## Wichtige Klassen
- `InferenceOrchestratorService` → Zustandssteuerung GPU
- `DetectionAggregator`          → Temporal Voting
- `QualityGateService`           → Green/Yellow/Red
- `MeasurementService`           → deterministisch, KEIN LLM
- `ClassificationService`        → Qwen-Wrapper
- `ReportGenerator`              → EN 13508-2 Output

## Coding-Regeln
- Bestehenden Code nur aendern wenn explizit gefragt
- Neue Features als separate Services mit Interface
- Tests NUR fuer Recommendation- und QualityGate-Logik
- Keine NuGet-Pakete ohne Rueckfrage
- Kommentare auf Deutsch
- JSON-Schema fuer alle Qwen-Outputs (strict, kein freier Text)
