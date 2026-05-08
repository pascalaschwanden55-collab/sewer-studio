# ADR-0005: Thin-AI-Architektur — C# orchestriert, LLM nur fuer Text

- **Status**: accepted
- **Datum**: 2026-04 (initial), 2026-05-08 (formalisiert)
- **Verantwortlich**: Solo-Entwicklung

## Kontext

SewerStudio nutzt fuenf KI-Modelle:

- YOLO26l-seg (Detektion + Segmentierung)
- SAM 2.1 (pixelgenaue Segmentierung)
- Grounding DINO 1.5 (Open-Vocabulary-Detection, lazy)
- Qwen3-VL 8B + 32B-hybrid (Schadensklassifikation, Texterklaerung)
- nomic-embed-text (KB-Embeddings)

Die Versuchung: alles ueber den LLM-Prompt loesen — "Hier ist das Bild,
sag mir Schaden, Severity, Code". Bei modernen LLMs technisch moeglich,
aber:

- **Nicht reproduzierbar**: gleicher Prompt liefert unterschiedliche
  Ausgaben.
- **Schwer testbar**: kein deterministisches Verhalten.
- **Teuer in der Inferenz-Zeit**: jede Klassifikation ist ein 8B+ Forward.
- **Schwer debuggbar**: wenn etwas schief geht, ist die Ursache im
  LLM-Inneren versteckt.

## Entscheidung

**Thin-AI**-Prinzip: 

1. **C# uebernimmt die Geschaeftslogik**:
   - VSA-Code-Validierung
   - Severity-Mapping
   - Aggregation ueber Frames
   - Quality-Gate-Bewertung
   - Persistenz, Versionierung, Audit-Logs

2. **LLMs liefern nur strukturiert generierten Text**:
   - Strict-JSON-Schema fuer alle Qwen-Outputs
   - Keine freien Texte, keine "natuerliche Sprache" als Output
   - Schema-Validation in C#

3. **Vision-Modelle (YOLO/SAM/DINO) liefern reine Geometrie**:
   - Bounding-Boxes, Masken, Klassifikations-Probabilities
   - Keine "Interpretation" — nur Detection/Segmentation.

4. **Orchestrierung ist deterministisch C#**:
   - `MultiModelAnalysisService` ist ein **State-Machine**:
     DETECT → SEGMENT → CLASSIFY → ESCALATE.
   - Eskalationsregeln (8B → 32B) sind **explizite C#-If-Bedingungen**,
     kein "ueberlasse das dem LLM".

## Konkrete Konsequenzen im Code

```csharp
// FALSCH (nicht thin-AI):
var result = await llm.AskAsync(image, "Was ist das fuer ein Schaden?");
// → unstrukturierte Antwort, schwer testbar

// RICHTIG (thin-AI):
var detections = await yolo.DetectAsync(image);          // C#-Datenstruktur
var classification = await qwen.ClassifyAsync(image, detections, schema); // Strict JSON
var validated = QualityGate.Evaluate(detections, classification); // C#-Logik
```

JSON-Schema fuer Qwen-Antworten ist in
`src/AuswertungPro.Next.UI/Ai/Pipeline/Schemas/` definiert.

## Alternativen erwogen

1. **All-LLM-Pipeline**: ein einziger Vision-LLM macht alles.
   Verworfen wegen Reproduzierbarkeit und Test-Aufwand.

2. **Reines C# + handgeschriebene Heuristiken**: war der Stand vor
   dem KI-Refactor. Verworfen weil neue Schadenstypen jedes Mal Code
   erforderten.

3. **Partial-LLM** wie heute, aber mit viel mehr LLM-Anteil
   (z.B. LLM bestimmt selbst die Pipeline-Reihenfolge).
   Verworfen weil das Verhalten unvorhersehbar wird.

## Konsequenzen

**Positiv:**
- KI-Pipeline ist **testbar** (Mocks fuer Vision-/LLM-Komponenten).
- Reproduzierbarkeit: gleiche Eingabe → gleiche Ausgabe (mit
  Seed-Fixierung wo noetig).
- Performance: schwere Modelle nur wenn noetig (Eskalation).
- Audit-Trail: jede Entscheidung ist in C# nachvollziehbar.

**Negativ:**
- Mehr C#-Code als ein "All-LLM"-Setup.
- Neue Funktionalitaet braucht oft sowohl LLM-Schema-Update als auch
  C#-Glue-Code.
- LLM-Faehigkeiten (z.B. komplexe Bild-Beschreibungen) bleiben
  ungenutzt.

## Inference-Orchestrator Zustaende (implementiert)

| Zustand | Modelle aktiv | Trigger |
|---|---|---|
| DETECT | YOLO + Tracker (CPU) | Default-Eingang |
| SEGMENT | YOLO + SAM | Sobald YOLO-Box vorhanden |
| CLASSIFY | YOLO + Qwen-8B | Box → JSON-Schema-Antwort |
| ESCALATE | YOLO + Qwen-8B + 32B-hybrid | allCodes==null OR severity>=4 OR poor-quality |

Implementiert in:
`src/AuswertungPro.Next.UI/Ai/Pipeline/MultiModelAnalysisService.cs`

## Referenzen

- `CLAUDE.md` — Architektur-Prinzipien (Thin-AI als erstes Prinzip)
- `EnhancedVisionAnalysisService.cs` — Eskalationslogik 8B → 32B
- `QualityGateService.cs` — C#-Bewertung von KI-Output
