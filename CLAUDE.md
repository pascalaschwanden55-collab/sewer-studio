# SewerStudio — AI Sewer Inspection System

## Projekt-Kontext
- **App:** WPF / .NET 8+, MVVM, Windows 11
- **Zweck:** Automatisierte Kanalinspektion, ~3000 Videos aus Kanal-TV-Exporten
- **Standards:** EN 13508-2, VSA-KEK; aktive Quelle: `vsa_kek_2020_catalog_manifest.json`
- **Entwickler:** Solo, kein kommerzielles Ziel
- **Hardware:** Intel Core Ultra 9 285K · ASUS RTX 5090 32GB · 64GB DDR5

## AI-Pipeline (Ist-Zustand, HEAD)
- C# steuert Geschaeftslogik, UI, Dedup, QualityGate und Persistenz.
- Sidecar `sidecar/sidecar/` liefert YOLO, Grounding DINO und SAM ueber HTTP.
- YOLO: Standard-Gewicht `yolo26m.pt` bzw. TensorRT-Engine, wenn vorhanden; COCO-Fallback `yolo11m.pt`, wenn eigene Gewichte fehlen und Fallback erlaubt ist.
- Qwen2.5-VL laeuft ueber Ollama fuer Bild-/Code-Analyse. Keine Doku-Annahme zu automatischer 8B->32B-Laufzeit-Eskalation treffen.
- Grounding DINO: on-demand im Sidecar.
- SAM: Segment Anything `vit_h`; Gewichte liegen aktuell unter `models/sam3/`. Der Ordnername bedeutet nicht "SAM 3".
- Dedup/Merge: C#-framebasiert in `MultiModelAnalysisService.UpdateActive` und `VideoFullAnalysisService.UpdateActive` ueber `DedupWindowFrames`.
- Kein ByteTrack/OC-SORT und kein echtes Multi-Object-Tracking in HEAD.

## Architektur-Prinzipien (NICHT brechen)
- Thin-AI: C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung
- Kein grosses Refactoring ohne explizite Diskussion
- Laptop-Mode / Workstation-Mode Hardware-Abstraktion erhalten
- VRAM-Budget: max 29GB stabil, niemals alle Modelle gleichzeitig
- QualityGate Green/Yellow/Red muss immer durchlaufen

## Aktueller Pipeline-Ablauf
1. UI/Service startet Analyse ueber `VideoAnalysisPipelineService`, `SingleFrameMultiModelService` oder `VideoFullAnalysisService`.
2. C# ruft den Sidecar ueber `VisionPipelineClient` auf.
3. Sidecar verwaltet Modell-Locks und GPU-Slots in `sidecar/sidecar/gpu_manager.py`.
4. Multi-Model-Pfad: YOLO -> DINO -> SAM -> Quantifizierung -> optional Qwen.
5. C# mappt VSA-Code, dedupliziert framebasiert und laesst `QualityGateService` laufen.

## Geplant / nicht implementiert (nicht als Ist-Zustand behandeln)
- `ByteTrack` / `OC-SORT`: kein Tracking im aktuellen HEAD.
- `DetectionAggregator` / meterbasierter Merge-Radius / Temporal Voting: nicht im aktuellen HEAD.
- `InferenceOrchestratorService`: keine C#-Klasse im aktuellen HEAD; GPU-Slots liegen im Sidecar.
- `KbDeduplicationService` / Cosine-Dedup beim Schreiben: nicht implementiert; Cosine wird fuer Retrieval genutzt.
- Automatische 8B->32B-Laufzeit-Eskalation: nicht als implementiert annehmen.

## Build & Test
```bash
dotnet build AuswertungPro.sln
dotnet test AuswertungPro.sln
```

## Wichtige Klassen
- `VideoAnalysisPipelineService`  → waehlt Multi-Model- oder Fallback-Pfad fuer Videoanalyse
- `MultiModelAnalysisService`     → YOLO/DINO/SAM/Qwen-Pipeline mit framebasiertem Dedup
- `VideoFullAnalysisService`      → Vollanalyse-/Fallback-Pfad mit eigener Dedup-Logik
- `SingleFrameMultiModelService`  → Live-Einzelframe YOLO/DINO/SAM
- `VisionPipelineClient`          → C#-HTTP-Client zum Sidecar
- `QualityGateService`            → Green/Yellow/Red aus verfuegbaren Evidence-Signalen
- `FullProtocolGenerationService` → KI-Befunde zu Protokolleintraegen mappen
- `KnowledgeBaseManager`          → SQLite-KB: Samples + Embeddings indexieren/retrieven
- `TrainingSamplesStore`          → JSON-Trainingssamples speichern/mergen

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
