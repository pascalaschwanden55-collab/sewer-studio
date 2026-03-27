# SewerStudio — AI Sewer Inspection System

## Projekt-Kontext
- **App:** WPF / .NET 8+, MVVM, Windows 11
- **Zweck:** Automatisierte Kanalinspektion, ~3000 WinCan/Ikas IBSK Videos
- **Standards:** EN 13508-2, VSA-KEK Schweiz 2023
- **Entwickler:** Solo, kein kommerzielles Ziel
- **Hardware:** Intel Core Ultra 9 285K · ASUS RTX 5090 32GB · 64GB DDR5

## AI-Pipeline (lokal, Workstation-Mode)
- YOLO26m-seg         → permanent GPU, ~1.5GB VRAM
- Qwen3-VL-8B         → FastModel GPU, ~10GB VRAM (via Ollama, keep_alive=-1 permanent)
- Qwen3-VL-32B        → ReferenceModel GPU, ~22GB VRAM (via Ollama, on-demand bei Eskalation)
- Grounding DINO 1.5  → pre-warmed, persistent im VRAM (~2GB)
- SAM 3               → pre-warmed, persistent im VRAM (~2.5GB)
- ByteTrack/OC-SORT   → CPU, immer aktiv

## Architektur-Prinzipien (NICHT brechen)
- Thin-AI: C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung
- Kein grosses Refactoring ohne explizite Diskussion
- Laptop-Mode / Workstation-Mode Hardware-Abstraktion erhalten
- VRAM-Budget: max 29GB stabil, niemals alle Modelle gleichzeitig
- QualityGate Green/Yellow/Red muss immer durchlaufen

## Inference-Orchestrator Zustaende
1. DETECT   → GPU: YOLO | CPU: Tracker + Aggregator
2. SEGMENT  → GPU: YOLO + SAM | Qwen: entladen
3. CLASSIFY → GPU: YOLO + Qwen-8B | SAM/DINO: entladen
4. ESCALATE → GPU: YOLO + Qwen-32B | 8B entladen, nur bei Eskalation
   - Trigger: allCodesNull || severity>=4 || poorQuality (in EnhancedVisionAnalysisService)
   - FastModel (8B) entladen → ReferenceModel (32B) laden → Re-Analyse → 32B entladen → 8B wieder laden
   - SemaphoreSlim(1) schuetzt vor parallelen Modellwechseln
   - VRAM Normal: 8B(10)+YOLO(1.5)+DINO(3)+SAM(3)=17.5GB
   - VRAM Eskalation: 32B(22)+YOLO(1.5)+DINO(3)+SAM(3)=29.5GB (kurzzeitig)

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
