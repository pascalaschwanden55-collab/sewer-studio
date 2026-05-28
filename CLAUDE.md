# SewerStudio — AI Sewer Inspection System

## Projekt-Kontext
- **App:** WPF / .NET 8+, MVVM, Windows 11
- **Zweck:** Automatisierte Kanalinspektion, ~3000 Videos aus Kanal-TV-Exporten
- **Standards:** EN 13508-2, VSA-KEK; aktive Quelle: `vsa_kek_2020_catalog_manifest.json`
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
- BAA = Verformung (A=vertikal, B=horizontal)
- BAB = Riss (A=laengs, B=quer, C=diagonal, D=ringfoermig, E=verzweigt)
- BAC = Bruch (A=partiell, B=total)
- BAF = Oberflaechenschaden (rauhe Rohrwandung, chemischer Angriff, Korrosion)
- BAH = Schadhafter Anschluss
- BAI = Einragendes Dichtungsmaterial
- BAJ = Verschobene Rohrverbindung (breit, versetzt, Knick)

**Betriebliche Stoerungen (BB-Gruppe):**
- BBA = Wurzeln/Bewuchs
- BBB = Anhaftende Stoffe/Inkrustation/Fett
- BBC = Ablagerung (A=Sand, B=Kies, C=verfestigt)
- BBD* = Eindringender Boden (kein Basiscode BBD, nur Untercodes)

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
