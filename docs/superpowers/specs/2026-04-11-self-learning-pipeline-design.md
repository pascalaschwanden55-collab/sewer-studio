# Self-Learning Pipeline — Design-Spec

## Ziel

Das System soll Videos automatisch so codieren, wie ein erfahrener Operateur —
mit dem richtigen Detaillevel (15-30 Ereignisse pro Haltung, nicht 1000 Einzelframes).
Dazu lernt es aus 2031 vorprotokollierten Haltungen (Batch) und danach fortlaufend
aus menschlichen Korrekturen (Live).

## Einschraenkung

**Nur die Lern- und Codierungs-Pipeline wird geaendert.** Bestehende Features
(Import, Export, UI, VSA-Bewertung, Protokollverwaltung, PDF-Export etc.) bleiben
vollstaendig unangetastet.

---

## Ist-Zustand: Was existiert und funktioniert

| Komponente | Status | Datei |
|---|---|---|
| PdfProtocolTableParser | Produktiv | Ai/Training/Services/PdfProtocolTableParser.cs |
| ProtocolToGroundTruthMapper | Produktiv | Ai/Training/Services/ProtocolToGroundTruthMapper.cs |
| MeterToFrameResolver | Produktiv | Ai/Training/Services/MeterToFrameResolver.cs |
| VideoSelfTrainingOrchestrator | Produktiv | Ai/Training/Services/VideoSelfTrainingOrchestrator.cs |
| DifferenceAnalyzer | Produktiv | Ai/Training/Services/DifferenceAnalyzer.cs |
| KnowledgeBase (SQLite+Embeddings) | Produktiv | Ai/KnowledgeBase/ |
| KbEnrichmentService | Produktiv | Ai/Training/Services/KbEnrichmentService.cs |
| KbDeduplicationService | Produktiv | Ai/KnowledgeBase/KbDeduplicationService.cs |
| BenchmarkRunner + MetricsStore | Produktiv | Ai/Training/Services/Benchmark*.cs |
| OsdMeterDetectionService | Produktiv | Ai/OsdMeterDetectionService.cs |
| YoloRetrainOrchestrator | Implementiert, nicht verbunden | Ai/Training/Services/YoloRetrainOrchestrator.cs |
| QwenLoraOrchestrator | Implementiert, nicht verbunden | Ai/Training/Services/QwenLoraOrchestrator.cs |
| YOLO-Training-Endpoints (Sidecar) | Skeleton | sidecar/routes/training.py |

## Ist-Zustand: Was fehlt

| Luecke | Auswirkung |
|---|---|
| **DetectionAggregator** | Jeder Frame wird einzeln protokolliert → 1000 Eintraege statt 20 |
| **YOLO nicht auf Kanaldefekte trainiert** | Erkennt alles und nichts → massive False Positives |
| **Kein geschlossener Lernkreislauf** | KB wird gefuellt, aber YOLO/Qwen lernen nicht daraus |
| **Keine Defekt-Taxonomie fuer YOLO** | Unklar welche visuellen Klassen YOLO erkennen soll |
| **self_training_frames 85% Fehlquote** | Logo-Filter bricht ab, fast keine verwertbaren Frames |

---

## Design: 5-Phasen Self-Learning

### Phase 1: Ground-Truth-Extraktion (existiert, wird erweitert)

**Ziel:** Aus den 2031 PDF-Protokollen + Videos annotierte Trainingsframes erzeugen.

**Ablauf:**
```
PDF-Protokoll
    → PdfProtocolTableParser (existiert)
    → ProtocolToGroundTruthMapper (existiert)
    → GroundTruthEntry[] (~15-30 pro Haltung)

Video + OSD
    → MeterToFrameResolver (existiert)
    → Frame-PNG + Meterstelle

NEU: GroundTruthEntry + Frame
    → YoloAnnotationGenerator
    → YOLO-Format .txt (class x_center y_center width height)
```

**Neue Komponente: `YoloAnnotationGenerator`**
- Input: GroundTruthEntry + Frame-PNG
- Mapping: VSA-Code → YOLO-Klasse (siehe Defekt-Taxonomie unten)
- Fuer den initialen Trainingslauf: Full-Frame-BBox (das gesamte Bild ist der Defekt)
  - Begruendung: Wir haben keine pixelgenauen Annotationen aus den PDFs
  - YOLO lernt trotzdem die visuellen Muster, weil der Frame genau den Moment zeigt
    wo der Operateur den Schaden dokumentiert hat
- Spaeter (Phase 5): Menschliche Korrekturen liefern praezisere BBoxen

**Defekt-Taxonomie fuer YOLO (10 Klassen):**

| YOLO-Klasse | VSA-Codes | Visuelles Muster |
|---|---|---|
| 0: crack | BAB | Linienfoermig, dunkel auf hell |
| 1: fracture | BAC | Unregelmaessige Bruchkanten |
| 2: deformation | BAA, BAF | Oval statt rund |
| 3: displacement | BAH | Versatz sichtbar an Rohrkante |
| 4: intrusion | BAI, BBD | Etwas ragt ins Rohr |
| 5: root | BBB | Organisch, fadenfoermig |
| 6: deposit | BBC, BBA | Ablagerung am Boden/Wand |
| 7: infiltration | BBF | Wasser dringt ein |
| 8: connection | BCA | Runde/ovale Oeffnung in Wand |
| 9: structural_other | BAD, BAE, BAG, BAJ, BAK, BBE, BBH | Restklasse |

**Nicht als YOLO-Klasse:** BCD, BCE, BCC, BDB, BDC (Steuercodes) — diese werden
regelbasiert aus dem Meterstand/OSD abgeleitet, nicht visuell erkannt.

**Geschaetzter Output:** ~40.000 annotierte Frames (2031 Haltungen × ~20 Ereignisse)

---

### Phase 2: YOLO-Training (Infrastruktur existiert, wird aktiviert)

**Ziel:** YOLO auf die 10 Defektkategorien trainieren.

**Ablauf:**
```
YoloAnnotationGenerator Output
    → YoloDatasetExportService (existiert im YoloRetrainOrchestrator)
    → YOLO-Dataset (images/ + labels/ + data.yaml)
    → Sidecar training.py train_yolo() (Skeleton, wird aktiviert)
    → yolo_v1.pt

Validierung:
    → BenchmarkSetStore (20 Haltungen, existiert)
    → F1-Score muss > 0.3 fuer initialen Lauf (spaeter > vorheriger)
```

**Neue Komponente: `InitialTrainingOrchestrator`**
- Einmaliger Orchestrator fuer den ersten Trainingslauf
- Unterschied zu YoloRetrainOrchestrator: Kein "vorheriges Modell" zum Vergleichen
- Splittet Daten 80/20 (Train/Val) stratifiziert nach Klasse
- Trainingsparameter: epochs=100, imgsz=640, batch=16, patience=20
- Speichert als yolo_v1.pt, setzt active.json

**YOLO-Modell-Wahl:** yolov11m (bereits als Fallback konfiguriert) — gute Balance
zwischen Geschwindigkeit und Genauigkeit fuer 640px Frames.

---

### Phase 3: DetectionAggregator (NEU — Kernstueck)

**Ziel:** Aus einem Strom von YOLO-Einzelframe-Detektionen sinnvolle Ereignisse
extrahieren — so wie ein Operateur es tun wuerde.

**Prinzip "Operateur-Denken":**
Ein Operateur sieht einen Riss von weitem, wartet bis die Kamera nah genug ist,
notiert den Code beim klarsten Bild, und faehrt weiter. Er notiert nicht 30x
"Riss" fuer denselben Riss.

**Neue Komponente: `DetectionAggregator`**

```csharp
public class DetectionAggregator
{
    // Konfigurierbare Parameter
    int MinConsecutiveFrames = 3;      // Mindestens 3 Frames sichtbar
    double MinConfidence = 0.4;        // YOLO-Confidence-Schwelle
    double MeterMergeRadius = 0.5;     // Innerhalb 0.5m = gleiches Ereignis
    int CooldownFrames = 10;           // Nach Protokollierung 10 Frames Pause

    // Zustand pro aktiver Detektion
    class ActiveDetection {
        string YoloClass;
        List<FrameDetection> Frames;   // Alle Frames mit dieser Detektion
        double PeakConfidence;          // Hoechste Confidence
        int PeakFrameIndex;            // Frame mit hoechster Confidence
        double StartMeter, EndMeter;
    }

    // Hauptmethode
    DetectionEvent? Feed(FrameDetection detection, double currentMeter);
    List<DetectionEvent> Flush();  // Am Ende: alle offenen Events abschliessen
}
```

**Algorithmus:**
1. Jede YOLO-Detektion wird einem laufenden `ActiveDetection` zugeordnet
   (gleiche Klasse + innerhalb MeterMergeRadius)
2. Wenn keine passende ActiveDetection → neue starten
3. Wenn ActiveDetection seit `MinConsecutiveFrames` keinen neuen Frame hat
   → Event abschliessen, PeakFrame als "bestes Bild" waehlen
4. Das PeakFrame wird an Qwen geschickt fuer VSA-Feincodierung
5. Cooldown verhindern Re-Detektion desselben Schadens

**Output:** `DetectionEvent` mit:
- YoloClass, PeakFrame (PNG-Pfad), PeakConfidence
- MeterStart, MeterEnd (Punktschaden: gleich; Streckenschaden: unterschiedlich)
- Alle Rohframes fuer spaetere Analyse

**Erwartetes Ergebnis:** Statt 500+ Roh-Detektionen → ~15-30 Events pro Haltung

---

### Phase 4: Qwen-Feincodierung (existiert, wird optimiert)

**Ziel:** Nur fuer die aggregierten Events (PeakFrames) aufgerufen — nicht fuer
jeden einzelnen Frame.

**Aenderungen am bestehenden Flow:**
1. Qwen bekommt nur PeakFrames vom DetectionAggregator (statt jeden Frame)
2. YOLO-Klasse als Vorfilter: Qwen muss nicht "was ist das?" fragen,
   sondern nur den VSA-Code praezisieren
3. KB-Retrieval (existiert): 3 aehnlichste Beispiele als Few-Shot-Kontext

**Prompt-Optimierung:**
```
Du siehst ein Bild aus einer Kanalinspektion.
YOLO hat erkannt: {yolo_class} (Confidence: {confidence})
Aehnliche Beispiele aus der Wissensbasis: {kb_examples}

Bestimme:
- VSA-Code (z.B. BAB.B.A)
- Severity 1-5
- Uhrlage (z.B. 2:00-4:00)
- Quantifizierung falls anwendbar (%, mm)

WICHTIG: Nur codieren was klar sichtbar ist.
Wenn der Schaden nicht eindeutig erkennbar ist → "nicht_codierbar"
```

**Reduktion der Qwen-Aufrufe:** Von ~500/Haltung auf ~20/Haltung
→ Schneller, billiger, weniger Fehler.

---

### Phase 5: Fortlaufendes Lernen (existiert teilweise, wird verbunden)

**Ziel:** Jede menschliche Korrektur verbessert das System.

**Bestehender Flow (bleibt):**
```
Korrektur → KbEnrichmentService → KnowledgeBase (SQLite)
                                → RetrievalService (RAG fuer Qwen)
```

**Neuer zusaetzlicher Flow:**
```
Korrektur → YoloRetrainOrchestrator (existiert, wird aktiviert)
         → Wenn ≥50 neue Samples + ≥2 Klassen:
           → Export YOLO-Dataset
           → Inkrementelles Training (wenige Epochs auf neuem Daten)
           → Benchmark-Gate (F1 darf nicht fallen)
           → Hot-Swap via active.json (existiert im yolo_wrapper.py)
```

**Lernzyklus-Trigger:**
- **Automatisch:** Nach Batch-Nachtbetrieb, wenn genug neue Samples
- **Manuell:** Benutzer kann "Jetzt trainieren" ausloesen
- **Benchmark:** Woechentlich gegen 20 Goldstandard-Haltungen

---

## Steuercodes (regelbasiert, KEIN ML)

Die Grundgeruest-Codes werden NICHT von YOLO/Qwen erkannt, sondern
deterministisch aus dem Video-/OSD-Kontext abgeleitet:

| Code | Regel |
|---|---|
| BCD (Rohranfang) | Meter = 0.00, immer am Start |
| BCE (Rohrende) | Meter = Haltungslaenge, immer am Ende |
| BCA (Anschluss) | YOLO-Klasse "connection" → regelbasiert BCA |
| BCC (Bogen) | OSD-Richtungsaenderung oder YOLO "structural_other" |
| BDB (Beginn) | Automatisch bei Inspektionsstart |
| BDC (Abbruch) | Video endet vor Rohrende |

---

## Architektur-Uebersicht: Neuer End-to-End-Flow

```
Video-Input
    │
    ▼
OsdMeterDetectionService (existiert)
    → Meter-Timeline
    │
    ▼
YOLO (trainiert auf 10 Defektkategorien)
    → FrameDetection[] pro Frame
    │
    ▼
DetectionAggregator (NEU)
    → ~20 DetectionEvents mit PeakFrame
    │
    ▼
Steuercodes (regelbasiert)
    → BCD, BCE, BCA aus Meter/Kontext
    │
    ▼
Qwen (nur fuer PeakFrames, mit KB-RAG)
    → VSA-Code + Severity + Uhrlage
    │
    ▼
ProtocolDocument
    → Mensch korrigiert
    │
    ▼
KbEnrichmentService (existiert)
    → KnowledgeBase + YOLO-Retraining-Queue
```

## Neue Dateien (werden erstellt)

| Datei | Zweck |
|---|---|
| `Ai/Pipeline/DetectionAggregator.cs` | Temporale Aggregation (Kernstueck) |
| `Ai/Pipeline/DetectionEvent.cs` | Event-Modell |
| `Ai/Training/Services/YoloAnnotationGenerator.cs` | GroundTruth → YOLO-Labels |
| `Ai/Training/Services/InitialTrainingOrchestrator.cs` | Erster Trainingslauf |
| `Ai/Training/Models/YoloTrainingConfig.cs` | Trainingsparameter |

## Bestehende Dateien (werden modifiziert)

| Datei | Aenderung |
|---|---|
| `Ai/Pipeline/MultiModelAnalysisService.cs` | DetectionAggregator integrieren |
| `Ai/Pipeline/SingleFrameMultiModelService.cs` | Qwen nur fuer PeakFrames |
| `sidecar/routes/training.py` | Skeleton → produktiv aktivieren |
| `Ai/Training/Services/YoloRetrainOrchestrator.cs` | An Lernzyklus anbinden |
| `Ai/Training/Services/BatchSelfTrainingOrchestrator.cs` | InitialTraining-Phase einbauen |

## Nicht angefasst (explizit)

Alles ausserhalb der genannten Dateien bleibt unberuehrt:
- UI/Views/ViewModels
- Import-/Export-Services
- VSA-Bewertung
- Protokollverwaltung
- Projektmanagement
- PDF-Export
- Alle bestehenden Tests

---

## Risiken und Mitigationen

| Risiko | Mitigation |
|---|---|
| Full-Frame-BBox zu ungenau fuer YOLO | Reicht fuer Klassifikation (nicht Lokalisierung), spaeter praezisere BBoxen durch Korrekturen |
| 40k Frames zu wenig fuer YOLO | Augmentation (Flip, Rotation, Brightness), Transfer-Learning von vortrainiertem YOLOv11m |
| OSD-Meter ungenau | Existierender Smoothing-Algorithmus + 0.5m Toleranz im Aggregator |
| Qwen halluziniert VSA-Codes | Strict JSON-Schema + AllowedCodes-Whitelist (existiert) |
| YOLO-Retraining verschlechtert | Benchmark-Gate mit Rollback (existiert im YoloRetrainOrchestrator) |

## Erfolgskriterien

| Metrik | Ziel Phase 1-2 | Ziel nach 3 Monaten |
|---|---|---|
| Events pro Haltung | 10-40 (statt 500+) | 15-30 (wie Operateur) |
| F1-Score (DifferenceAnalyzer) | > 0.30 | > 0.60 |
| False Positives pro Haltung | < 10 | < 5 |
| Qwen-Aufrufe pro Haltung | < 30 | < 25 |
| Benchmark-Regression | Nie unter vorherigen F1 | Nie unter vorherigen F1 |
