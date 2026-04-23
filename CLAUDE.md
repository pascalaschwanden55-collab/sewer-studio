# SewerStudio — AI Sewer Inspection System

## Projekt-Kontext
- **App:** WPF / .NET 8+, MVVM, Windows 11
- **Zweck:** Automatisierte Kanalinspektion, ~3000 WinCan/Ikas IBSK Videos
- **Standards:** EN 13508-2, VSA-KEK Schweiz 2023
- **Entwickler:** Solo, kein kommerzielles Ziel
- **Hardware:** Intel Core Ultra 9 285K · ASUS RTX 5090 32GB · 64GB DDR5

## AI-Pipeline (lokal, Workstation-Mode) — Stand 2026-04-23
- YOLO26l-seg         → permanent GPU (TensorRT), ~1.5 GB VRAM
- Qwen3-VL-8B-Q8      → FastModel GPU, ~11 GB VRAM (via Ollama, keep_alive permanent,
                        num_gpu=999 erzwungen — sonst CPU-Fallback bei RTX 5090 mit fehlender CUDA-Runtime)
- Qwen3-VL-32B        → ReferenceModel HYBRID (num_gpu=10): ~2 GB VRAM + ~20 GB RAM,
                        kein Swap mit 8B — beide laufen parallel (CLAUDE.md-V1-Logik deprecated)
- Grounding DINO 1.5  → **lazy** (V4.2 Phase 3.4) — wird bei erstem Request geladen,
                        ausser SEWER_SIDECAR_PREWARM_DINO=1 gesetzt
- SAM 3               → pre-warmed, persistent im VRAM (~2.5 GB)
- ByteTrack/OC-SORT   → CPU, immer aktiv

VRAM-Budget Soll-Stand: YOLO(1.5) + SAM(2.5) + Qwen-8B(11) + 32B-hybrid(2) + nomic(0.6) ≈ 17.6 GB.
Freie Reserve: ~14 GB fuer DINO (wenn on-demand geladen) + Puffer.

## Architektur-Prinzipien (NICHT brechen)
- Thin-AI: C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung
  (HINWEIS: In der Praxis lebt die KI-Logik aktuell in `src/AuswertungPro.Next.UI/Ai/**` —
  Migration nach Application/Infrastructure ist im Audit 2026-04-23 als CRITICAL-Task
  dokumentiert, siehe docs/AUDIT_SEWERSTUDIO_2026-04-23.md)
- Kein grosses Refactoring ohne explizite Diskussion
- Laptop-Mode / Workstation-Mode Hardware-Abstraktion erhalten
- VRAM-Budget: max 29 GB stabil, niemals alle Modelle gleichzeitig
- QualityGate Green/Yellow/Red muss immer durchlaufen

## Inference-Orchestrator Zustaende (implementiert in `MultiModelAnalysisService`)
1. DETECT   → GPU: YOLO | CPU: Tracker + Aggregator
2. SEGMENT  → GPU: YOLO + SAM | Qwen: nicht erforderlich
3. CLASSIFY → GPU: YOLO + Qwen-8B | SAM: optional
4. ESCALATE → GPU: YOLO + Qwen-8B + 32B-hybrid | nur bei Eskalation
   - Trigger: allCodesNull || severity>=4 || poorQuality (in `EnhancedVisionAnalysisService`)
   - 32B wird hybrid mit num_gpu=10 geladen (CPU/RAM-Mehrheit) — kein Swap mit 8B mehr.
   - Laufzeit 32B-hybrid: ~9 s pro Request (statt 28 s bei num_gpu=0)
   - SemaphoreSlim(1) schuetzt vor parallelen 32B-Anfragen

## Build & Test
```bash
dotnet build AuswertungPro.sln
dotnet test --filter Category=Recommendation
```

## Wichtige Klassen (Pfade relativ zu `src/`)
- `AuswertungPro.Next.UI/Ai/Pipeline/MultiModelAnalysisService.cs` → GPU-State-Automat (Orchestrator)
- `AuswertungPro.Next.UI/Ai/EnhancedVisionAnalysisService.cs`      → Qwen-Wrapper mit Eskalation
- `AuswertungPro.Next.UI/Ai/Pipeline/DetectionAggregator.cs`       → Temporal Voting
- `AuswertungPro.Next.UI/Ai/Pipeline/QualityGateService.cs`        → Green/Yellow/Red
- `AuswertungPro.Next.UI/Ai/Pipeline/BatchPipelineService.cs`      → Batch-Pipeline mit Frame-Persistierung
- `AuswertungPro.Next.UI/Ai/VideoAnalysisPipelineService.cs`       → Video-End-to-End-Flow
- `AuswertungPro.Next.Application/Reports/ProtocolPdfExporter.cs`  → EN 13508-2 Output
- `AuswertungPro.Next.Infrastructure/Reports/HaltungsDossierPdfBuilder.cs` → Haltungs-Dossier-PDF
- `AuswertungPro.Next.UI/Ai/OllamaClient.cs`                       → Ollama-HTTP mit Polly Retry + Circuit Breaker
- `AuswertungPro.Next.UI/Ai/PythonSidecarService.cs`               → Sidecar-Lifecycle auf :8100
- `AuswertungPro.Next.UI/ServiceProvider.cs`                       → Manueller DI-Container (Warmup, Config)

## Fachdomaene Kanalinspektion

### Grundbegriffe
- **Haltung:** Kanalabschnitt zwischen zwei Schaechten (typisch 30-80m)
- **Schacht:** Zugang zum Kanal (Anfangs-/Endknoten einer Haltung)
- **DN:** Nennweite in mm (DN150=Hausanschluss, DN300=Standard, DN600+=Sammler)
- **OSD:** On-Screen Display im Video — zeigt Meterstand, Haltungsname, Datum
- **Meterstand:** Position der Kamera in der Haltung (0.00m = Anfang, z.B. 45.30m = Ende)

### Schadenscodierung (VSA-KEK / EN 13508-2)
Codes sind hierarchisch aufgebaut: **Hauptcode** (2-3 Buchstaben) + **Char1** (Untertyp) + **Char2** (Lage)

**Grundgeruest (BC-Gruppe, Bestandsaufnahme):**
- BCD = Rohranfang (Kamera faehrt in Rohr ein, Schacht sichtbar)
- BCE = Rohrende (Endknoten erreicht)
- BCA = Seitlicher Anschluss (runde/ovale Oeffnung in Rohrwand)
- BCC = Bogen (Richtungsaenderung, ueber mehrere Frames sichtbar)

**Strukturelle Schaeden (BA-Gruppe):**
- BAB = Riss (A=laengs, B=quer, C=diagonal, D=ringfoermig, E=verzweigt)
- BAC = Bruch (A=partiell, B=total)
- BAF = Deformation (A=vertikal, B=horizontal)
- BAH = Versatz (A=vertikal, B=horizontal)
- BAI = Einragender Stutzen

**Betriebliche Stoerungen (BB-Gruppe):**
- BBA = Inkrustation/Kalkablagerung
- BBB = Wurzeleinwuchs
- BBC = Ablagerung (A=Sand, B=Kies, C=verfestigt)
- BBD = Eindringender Boden

### Quantifizierung
- **Uhrlage:** 12:00=Scheitel (oben), 6:00=Sohle (unten), 3:00=rechts, 9:00=links
- **Severity 1-5:** 1=optisch, 2=leicht, 3=mittel (Sanierung mittelfristig), 4=schwer (kurzfristig), 5=kritisch (Sofortmassnahme)
- **Ausdehnung:** Prozent des Rohrumfangs
- **Querschnittsverringerung:** Prozent des freien Querschnitts

### Punktschaden vs. Streckenschaden
- **Punktschaden:** An einer Stelle (z.B. Riss, Anschluss) — ein Meterstand
- **Streckenschaden:** Ueber Laenge (z.B. Korrosion 2.5m-8.0m) — MeterStart bis MeterEnd

## Coding-Regeln
- Bestehenden Code nur aendern wenn explizit gefragt
- Neue Features als separate Services mit Interface
- Tests NUR fuer Recommendation- und QualityGate-Logik
- Keine NuGet-Pakete ohne Rueckfrage
- Kommentare auf Deutsch
- JSON-Schema fuer alle Qwen-Outputs (strict, kein freier Text)
