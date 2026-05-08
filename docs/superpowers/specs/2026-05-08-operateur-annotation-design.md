# Operateur-Annotation im Trainingsmodus — Design

- **Datum**: 2026-05-08
- **Status**: design accepted, awaiting implementation plan
- **Verantwortlich**: Solo-Entwicklung
- **Vorausgehend**: ADR-0005 (Thin-AI), ADR-0001 (Partial-Class-Refactor)

## Ziel

Schneller Trainingsdaten-Workflow im Trainingsmodus: aus einer schon
codierten Inspektion (Video + Operateur-Protokoll) entstehen pro Klick
+ Box hochwertige Trainings-Samples mit Frame, SAM-Maske, VSA-Code
und Provenance.

**Kerngedanke:** Der **Code** kommt vom Operateur (steht im Protokoll —
fertig). Der User muss nur noch die **raeumliche Position im Bild**
bestaetigen. Damit faellt der teuerste Teil des Labelings (Code-Wahl)
weg, die Qualitaet ist hoch (operateur-validiert), und ein Sample
braucht nur Sekunden.

## Erwartete Wirkung

- KI-Reifezeit von Monaten auf Wochen verkuerzbar
- Trainings-Samples sind operateur-validiert (statt KI-Selbst-Lernen)
- Direkte Kopplung Protokoll ↔ Trainings-Datensatz (D:/yolo_sewer_v1)

## 1. Architektur

### 1.1 Layer-Trennung

```
Domain  ─►  Application  ─►  Infrastructure  ─►  UI
```

- **Domain**: `TrainingSample` (POCO) erweitert um Mask-Felder
- **Application**: Interface + DTOs + Session-State (POCO, keine I/O)
- **Infrastructure**: konkreter Service mit Sidecar-Call, Store/KB/YOLO-Schreibern
- **UI**: PlayerWindow-Partial (Box-Drag, Overlay, Hotkeys)

### 1.2 Datei-Struktur

```
src/AuswertungPro.Next.Domain/Ai/Training/
  TrainingSample.cs                      ← ERWEITERT (Mask-Felder +
                                          SourceTypeNames-Konstante
                                          "OperateurAnnotation" in
                                          gleicher Datei)

src/AuswertungPro.Next.Application/Ai/Annotation/
  IOperateurAnnotationService.cs         ← NEU
  OperateurAnnotationModels.cs           ← NEU (DTOs)
  OperateurAnnotationSession.cs          ← NEU (Session-State)
  ITrainingSamplesWriter.cs              ← NEU (Adapter-Vertrag)
  IKnowledgeBaseIndexer.cs               ← NEU (Adapter-Vertrag)
  IYoloDatasetWriter.cs                  ← NEU (Adapter-Vertrag)

src/AuswertungPro.Next.Application/Ai/Pipeline/
  VisionPipelineDtos.cs                  ← ERWEITERT (SamRequest.ReturnPolygon,
                                          SamMaskResult.PolygonPoints/PolygonJson)

src/AuswertungPro.Next.Application/Ai/Teacher/
  VsaYoloClassMap.cs                     ← ERWEITERT (TryGetClassId-Variante
                                          ohne Auto-Create)

src/AuswertungPro.Next.Infrastructure/Ai/Annotation/
  OperateurAnnotationService.cs          ← NEU (Implementation)
  TrainingSamplesWriterAdapter.cs        ← NEU (delegiert an statischen Store)
  KnowledgeBaseIndexerAdapter.cs         ← NEU (delegiert an Manager)

src/AuswertungPro.Next.Infrastructure/Ai/Training/
  YoloDatasetExportService.cs            ← ERWEITERT (AppendSampleAsync,
                                          implementiert IYoloDatasetWriter)

src/AuswertungPro.Next.UI/Views/Windows/
  PlayerWindow.OperateurAnnotation.cs    ← NEU (Partial)

sidecar/sidecar/schemas/segmentation.py  ← ERWEITERT (SamRequest.return_polygon,
                                          SamMaskResult.polygon_points)
sidecar/sidecar/routes/sam.py            ← ERWEITERT (Polygon im Response
                                          weiterreichen)
```

### 1.3 Zwei-Phasen-API (Vertrag)

```csharp
public interface IOperateurAnnotationService
{
    /// SAM-Maske aus Box-Prompt holen, NICHTS persistieren.
    Task<MaskPreview> PreviewMaskAsync(
        AnnotationRequest request, CancellationToken ct);

    /// Erst beim Confirm: TrainingSample bauen, Store/YOLO/KB.
    /// Best-Effort-Reihenfolge, Status pro Teilschritt im Result.
    Task<CommitResult> CommitAsync(
        AnnotationRequest request, MaskPreview preview, CancellationToken ct);
}
```

### 1.4 Offene technische Seams (Mocking-Vorbereitung)

Damit `OperateurAnnotationService` testbar bleibt (Stub-Sidecar +
Mock-Store/-YOLO/-KB), brauchen die folgenden bestehenden Klassen
**Interfaces / Adapter mit echter API-Semantik**:

| Bestehende Klasse | Reale API | Adapter-Vertrag (NEU) |
|---|---|---|
| `TrainingSamplesStore` (static) | `LoadAsync()`, `SaveAsync(list)`, `MergeAndSaveAsync(list)`, `MergeOrUpdateAsync(samples)` | `ITrainingSamplesWriter` mit drei klar formulierten Methoden:<br>`AppendAsync(TrainingSample, CancellationToken)` — Single-Sample-Schreiben (intern via `MergeAndSaveAsync` mit Single-Element-Liste).<br>`UpdateIndexStateAsync(string sampleId, KbIndexState state, CancellationToken)` — gezieltes Status-Update per SampleId (intern: Load + Find + Update + Save).<br>`FindCommittedAsync(string caseId, string sourceType, string code, double meterTolerance, CancellationToken)` — fuer wiederholten Import (Load + Filter). |
| `KnowledgeBaseManager` | konkrete Klasse | `IKnowledgeBaseIndexer` mit `IndexSampleAsync(TrainingSample, CancellationToken)`. Adapter delegiert an Manager. |
| `YoloDatasetExportService` | Batch-orientiert, kein Single-Append | `IYoloDatasetWriter.AppendSampleAsync(TrainingSample, CancellationToken)`. Service implementiert das Interface direkt; neue Methode kommt in den bestehenden Service. |
| `VisionPipelineClient` | konkret, `HttpClient` injizierbar | Stub-Pattern wie in `SidecarContractTests` reicht — kein zusaetzliches Interface noetig. |
| `VsaYoloClassMap` (static) | `GetClassId(code)` ist **auto-wachsend** und persistiert neue IDs sofort. Das ist gefaehrlich fuer einen stabilen YOLO-Datensatz. | Neue Methode `TryGetClassId(string vsaCode, out int classId)` ohne Auto-Create. `OperateurAnnotationService` nutzt **ausschliesslich** diese Variante. Wenn Code unbekannt: YOLO-Write skipped, Warning emittiert. |

**Wichtig zur `MergeOrUpdateAsync`-Realitaet**: das bestehende
`MergeOrUpdateAsync` aktualisiert bei Signatur-Treffer nur ausgewaehlte
Felder (Status, Notes, MatchLevel, KiCode, KbIndexState, SourceType,
TechniqueGrade, Rohrmaterial, NennweiteMm, IsKorrigiert, QualityGateLevel)
— **NICHT** FramePath, BBox, Mask-Felder, FrameDeltaSeconds. Fuer
Operateur-Annotation ist das richtig: das Sample wird beim ersten
`AppendAsync` mit allen Feldern geschrieben, spaeter wird **nur** der
KbIndexState aktualisiert. Wenn der Adapter spaeter mehr Felder
aktualisieren soll, muss `MergeOrUpdateAsync` erweitert werden — das
ist explizit Slice-2-Thema.

**Implementation der Seams gehoert in den Implementations-Plan**.
Adapter-Klassen sind in 1.2 als zu erstellende Dateien aufgefuehrt.

## 2. UI im PlayerWindow.TrainingMode

### 2.0 Abgrenzung zum bestehenden TrainingMode

`PlayerWindow.TrainingMode.cs` enthaelt heute bereits SAM-/Box-/
Negativ-Sample-/Box-only-Logik fuer einen anderen Workflow
(KI-Selbstlern-Modus mit `SourceTypeNames.TeacherAnnotation`).
**Diese bestehende Logik bleibt unveraendert.**

Operateur-Annotation kommt als **isolierter Submodus** im selben
TrainingMode-Partial mit:

- eigenen UI-Controls (`_operatorCodeList`, `_operatorOverlayCanvas`,
  `_operatorConfirmButton` ...) — kein Reuse der bestehenden
  Box-/SAM-Controls
- eigenem Submode-Switch (`_operatorAnnotationActive`-Flag)
- eigenem Save-Pfad (ueber `IOperateurAnnotationService`,
  **nicht** ueber das bestehende Training-Save-Verhalten)
- eigener Source-Type-Konstante (`OperateurAnnotation`,
  nicht `TeacherAnnotation`)

Damit wird sichergestellt, dass die Slice-1-Restriktionen (kein
Multi-Box, keine Negativ-Samples, kein Box-only-Fallback) nicht
durch versehentliches Wiederverwenden der bestehenden Pfade
unterlaufen werden.

### 2.1 Layout

Code-Liste rechts als Arbeitsvorrat, Player + Annotation-Overlay
links. Toolbar oben fuer Import + Auto-Advance-Toggle.

```
┌────────────────────────────────────────┬──────────────────────┐
│ Toolbar: [Haltungsordner importieren]  │ Codes-Liste rechts   │
│          [Auto-Advance: ☑]             │  ● BAB B  12.30 m    │
│                                        │  ● BAC A  18.50 m    │
├────────────────────────────────────────┤  ● BBB Z  24.10 m    │
│                                        │  ● BCC A  31.00 m    │
│       VLC-Player                       │                      │
│       + Annotation-Overlay             │ Status-Panel         │
│       (Box, SAM-Maske, Confirm)        │ Buttons              │
│                                        │                      │
└────────────────────────────────────────┴──────────────────────┘
```

### 2.2 Status-Symbole

WPF-`Ellipse`-Shape mit Farb-Brushes (kein Unicode):

| Status | Farbe | Beschriftung |
|---|---|---|
| Pending | grau | "Pending" |
| Active | blau (fett) | "Aktiv" |
| PreviewReady | orange | "Box gezogen" |
| Committed | grün | "Annotiert" |
| Skipped | gelb | "Übersprungen" |
| Rejected | rot | "Protokollfehler" |
| Error | dunkelrot | "Fehler" |

### 2.3 Hotkeys

| Taste | Aktion | Bedingung |
|---|---|---|
| `Enter` | Confirm | nur wenn `PreviewReady` |
| `Esc` | Box+Maske verwerfen | jederzeit |
| `S` | Skip | nicht bei Textfeld-Fokus |
| `R` | Reject (Protokollfehler) | nicht bei Textfeld-Fokus |
| `→` / `←` | Naechster/vorheriger Code | jederzeit |
| `Leertaste` | Player Play/Pause | bestehender Standard |
| `,` / `.` | Frame-Schritt | bestehender Standard |

S/R werden nur gefeuert, wenn der Fokus nicht auf einem `TextBox`-
Element liegt (Standard-WPF-`InputGesture`-Source-Check).

## 3. Daten-Modell

### 3.1 `TrainingSample` (Domain) — Erweiterungen

```csharp
public sealed class TrainingSample
{
    // ... bestehende Felder ...

    // ── NEU: SAM-Maske ──────────────────────────────────────────
    /// <summary>
    /// Run-Length-Encoded Maske, opaque Format vom Sidecar geliefert.
    /// Spezifisches Encoding in <see cref="SamMaskEncoding"/>.
    /// </summary>
    public string? SamMaskRle { get; set; }

    /// <summary>
    /// Format-Tag fuer Migrations-Faehigkeit. Default beim ersten
    /// Schreiben: "sidecar-sam-rle-v1".
    /// </summary>
    public string? SamMaskEncoding { get; set; }

    public int? MaskWidth { get; set; }
    public int? MaskHeight { get; set; }
    public int? MaskAreaPixels { get; set; }
    public double? SamConfidence { get; set; }

    public bool HasMask =>
        !string.IsNullOrWhiteSpace(SamMaskRle) &&
        MaskWidth.HasValue && MaskHeight.HasValue;

    // ── NEU: Frame-Delta fuer Drift-Analyse ─────────────────────
    public double? FrameDeltaSeconds { get; set; }
}

public static class SourceTypeNames
{
    // ... bestehende Konstanten ...
    public const string OperateurAnnotation = "OperateurAnnotation";
}
```

### 3.2 Application-DTOs

```csharp
public sealed record AnnotationRequest(
    string CaseId,
    string Code,
    double ProtocolMeterstand,
    double SuggestedFrameTimeSeconds,    // Player-Position beim ersten Sprung
    double ActualFrameTimeSeconds,       // Player-Position beim Box-Drop
    int VideoFrameIndex,                 // klar benannt (war FrameIndex)
    string FramePath,                    // UI hat Frame schon geschrieben
    int FrameWidth,
    int FrameHeight,
    BoundingBoxNormalized Box);

public sealed record BoundingBoxNormalized(
    double XCenter, double YCenter, double Width, double Height);

public sealed record MaskPreview(
    string SamMaskRle,
    string SamMaskEncoding,
    string PolygonJson,                  // Sidecar mit return_polygon:true
    int MaskWidth,
    int MaskHeight,
    int MaskAreaPixels,
    double SamConfidence,
    TimeSpan SamLatency,
    IReadOnlyList<string>? Warnings);    // z.B. "LowSamConfidence",
                                         // "MaskTooSmall"

public sealed record CommitResult(
    bool IsSuccess,                      // = StorePersisted
    string SampleId,
    string? FramePath,                   // finale Sample-Frame-Datei
    string? LabelPath,                   // YOLO-.txt Datei (falls geschrieben)
    bool StorePersisted,
    bool KbIndexed,
    bool YoloWritten,
    string? Error,
    IReadOnlyList<string>? Warnings);
```

### 3.3 `OperateurAnnotationSession` — Session-State

```csharp
public sealed class OperateurAnnotationSession
{
    public string CaseId { get; init; } = "";
    public string VideoPath { get; init; } = "";
    public string PdfPath { get; init; } = "";
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;

    public IList<CodeTask> Tasks { get; } = new List<CodeTask>();
    public CodeTask? Active { get; set; }
}

public sealed class CodeTask
{
    public string Code { get; init; } = "";
    public double Meterstand { get; init; }
    public CodeTaskState State { get; set; } = CodeTaskState.Pending;

    // PreviewReady:
    public BoundingBoxNormalized? Box { get; set; }
    public MaskPreview? Preview { get; set; }
    public double? FrameDeltaSeconds { get; set; }

    // Committed:
    public string? CommittedSampleId { get; set; }
    public DateTime? CommittedUtc { get; set; }

    // Skipped/Rejected:
    public string? UserReason { get; set; }
}

public enum CodeTaskState
{
    Pending,
    Active,
    PreviewReady,
    Committed,
    Skipped,
    Rejected,    // = Protokollfehler / Nicht zutreffend
    Error
}
```

Session ist UI-thread-bound, lebt nur waehrend der Sitzung im
ViewModel und ist **nicht persistiert**. Persistente Wahrheit ueber
"was wurde annotiert" liegt im `TrainingSamplesStore`.

### 3.4 Schreibreihenfolge in `CommitAsync` (Best-Effort)

```
INPUT: AnnotationRequest + MaskPreview

1. TrainingSample bauen
   - SourceType = OperateurAnnotation
   - BBox-Felder + Mask-Felder + FrameDelta + Signatur

2. Frame.png finalisieren (temp → SampleId-Pfad)

3. ITrainingSamplesWriter.AppendAsync(sample)     ← STORE = WAHRHEIT
   (intern via Store.MergeAndSaveAsync mit Single-Element-Liste)
   ↑ failed: Abbruch, kein YOLO/KB
   ↑ success: StorePersisted = true

4. IYoloDatasetWriter.AppendSampleAsync(sample)
   - VsaYoloClassMap.TryGetClassId(code, out classId) — STABIL,
     KEIN Auto-Create
   - falls Code nicht mappbar: skip YOLO, Warning "UnknownYoloClass"
   - Polygon aus MaskPreview.PolygonJson
   ↑ failed: YoloWritten = false, weiter zu KB

5. IKnowledgeBaseIndexer.IndexSampleAsync(sample)
   ↑ failed:
       ITrainingSamplesWriter.UpdateIndexStateAsync(
           sample.SampleId, KbIndexState.Pending)
       KbIndexed = false

6. CommitResult zusammenstellen.
   IsSuccess = StorePersisted (alle anderen sind Status-Felder)
```

Die Adapter-Interfaces (siehe 1.4) entkoppeln die Service-Logik von
den konkreten statischen/I/O-Klassen — damit ist Stub-Mocking in
Tests sauber moeglich.

### 3.5 Sidecar `/segment/sam` — Erweiterung

Drei Stellen muessen erweitert werden — Sidecar **und** C#-DTOs:

**Python-Sidecar** (`sidecar/sidecar/schemas/segmentation.py`):

```python
class SamRequest(BaseModel):
    image_base64: str
    bounding_boxes: list[SamBoundingBox]
    pipe_diameter_mm: int | None = None
    return_polygon: bool = False    # NEU


class SamMaskResult(BaseModel):
    # ... bestehende Felder ...
    polygon_points: list[list[float]] | None = None    # NEU
                                                       # [[x, y], [x, y], ...]
```

`sidecar/sidecar/routes/sam.py` reicht das Polygon im Response weiter
(berechnet via `cv2.findContours(...) + cv2.approxPolyDP(...)` aus der
binaeren Maske wenn `return_polygon=true`).

**C#-DTOs** (`src/AuswertungPro.Next.Application/Ai/Pipeline/VisionPipelineDtos.cs`):

```csharp
public sealed record SamRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("bounding_boxes")] IReadOnlyList<SamBoundingBox> BoundingBoxes,
    [property: JsonPropertyName("point_prompts")] IReadOnlyList<SamPointPrompt>? PointPrompts = null,
    [property: JsonPropertyName("pipe_diameter_mm")] int? PipeDiameterMm = null,
    [property: JsonPropertyName("ring_scan")] RingScanParams? RingScan = null,
    [property: JsonPropertyName("return_polygon")] bool ReturnPolygon = false   // NEU
);

public sealed record SamMaskResult(
    // ... bestehende Felder ...
    [property: JsonPropertyName("polygon_points")]
        IReadOnlyList<IReadOnlyList<double>>? PolygonPoints = null   // NEU
);
```

Defaults sind ruckwaertskompatibel — bestehende Aufrufer brauchen
keine Aenderung.

## 4. Workflow + Edge-Cases

### 4.1 Happy Path

```
Trainingsmodus
  → Haltungsordner importieren (PdfProtocolTableParser etc.)
  → Code-Liste gefuellt mit Pending-Tasks
  → User klickt Code in Liste
  → Player springt zum Meterstand
  → User scrubbt ggf. manuell (Frame-Δ wird angezeigt)
  → User zieht Box
  → UI captured Frame
  → Service.PreviewMaskAsync(...) → SAM via Sidecar (return_polygon:true)
  → Maske im Overlay angezeigt
  → CodeTask.State = PreviewReady
  → User drueckt Enter
  → Service.CommitAsync(...) (Best-Effort: Store > YOLO > KB)
  → CommitResult mit getrennten Status
  → CodeTask.State = Committed
  → Auto-Advance zum naechsten Pending
```

### 4.2 Wichtige Edge-Cases

| Fall | Verhalten |
|---|---|
| Sidecar nicht erreichbar | PreviewMaskAsync wirft. Toast "Sidecar nicht erreichbar". Confirm bleibt disabled. **Slice 1: kein Box-only-Fallback.** |
| SAM-Confidence < 0.3 | Maske wird angezeigt, Status-Panel Warning. Confirm aktiv. Sample bekommt `Warnings = ["LowSamConfidence"]`. |
| Code nicht in `VsaYoloClassMap` (`TryGetClassId` returns false) | Status-Panel: "Code in YOLO-Map fehlt — Sample geht nur in KB". CommitAsync skipped YOLO-Write. CommitResult.YoloWritten = false, Warning "UnknownYoloClass". **Wichtig:** kein Auto-Create der Class-ID — sonst werden YOLO-Datensatz-IDs leise instabil. |
| `ITrainingSamplesWriter.AppendAsync` failed | CommitResult.IsSuccess = false. CodeTask.State = Error. Error-Dialog zeigt Grund. |
| YOLO-Write failed | Store hat Sample, KB versucht. CodeTask bleibt Committed. Toast "YOLO-Datensatz nicht beschreibbar". |
| KB-Indexierung failed | `ITrainingSamplesWriter.UpdateIndexStateAsync(sampleId, Pending)` aufgerufen. CodeTask Committed. KbIngestionPipeline (existiert) zieht spaeter nach. |
| Streckenschaden | **Slice 1: nur 1 Sample am MeterCenter.** Multi-Sample Slice 2. |
| User wechselt Code im PreviewReady | Modal "Verwerfen / Hier bleiben / Bestaetigen+wechseln". |
| Wiederholter Import einer Haltung | Beim Import: Store-Query nach `CaseId+SourceType=OperateurAnnotation`. Bekannte Codes mit ±0.5m-Toleranz als Committed initialisieren. |
| Frame-Capture failed | PreviewMaskAsync nicht aufrufen. Toast "Player nicht ready". |
| Esc / Window schliessen / Wechsel | Temp-Frame-Dateien aufraeumen (Service + UI dispose-Pfad). |

## 5. Slice-Plan

### 5.1 Slice 1 — explizit IN

```
✅ Session aus Codes aufbauen (mit Wiederholt-Import-Erkennung)
✅ Code waehlen → Player-Seek
✅ Manuelles Scrubbing + Frame-Δ-Anzeige
✅ Box-Drag → Frame-Capture → SAM-Preview (return_polygon:true)
✅ Maske im Overlay rendern
✅ Confirm: Store + Best-Effort YOLO + Best-Effort KB
✅ Skip / Reject als Session-State
✅ Auto-Advance
✅ Hotkeys (Enter/Esc/S/R/←/→) mit Textfeld-Fokus-Schutz
✅ Wiederholter Import erkennt Committed
✅ Tests: Domain, Application, Infrastructure (Stub-Sidecar)
✅ 1 Live-End-to-End-Test (opt-in, Trait="LiveSidecar")
```

### 5.2 Slice 2 — explizit AUS Slice 1

Diese Punkte werden in Slice 1 **nicht** angefasst und bekommen
spaeter eigene Designs:

```
❌ Multi-Box pro Code im selben Frame
❌ Negative-Sample-Schreiben bei Reject
❌ Streckenschaden Multi-Sample (mehrere Frames pro MeterStart-MeterEnd)
❌ Box-only-Fallback bei Sidecar-Down
❌ Drift-Dashboard (Frame-Δ-Auswertung im Diagnose-Tab)
❌ Operateur-vs-KI-Diff-Report
❌ Bulk-Operationen (alle Codes annotieren in einem Lauf)
❌ Cross-Haltung-Sessions
```

### 5.3 Test-Plan (Slice 1)

Bei der Implementierung wird Folgendes erwartet — **Anzahl ist Richtwert**,
nicht hart festgenagelt:

**Architektur-Tests**:
- `OperateurAnnotationService` lebt in Infrastructure-Assembly, nicht Application
- bestehende `ArchitectureLayerGuardTests` bleiben gruen

**Domain-Tests**:
- `TrainingSample` Mask-Felder Round-Trip
- `HasMask`-Property korrekt
- `BuildCanonicalSignature`-Stabilitaet bei OperateurAnnotation-Samples

**Application-Tests**:
- `OperateurAnnotationSession` State-Transitions (Pending → Active →
  PreviewReady → Committed)
- DTO-Roundtrip JSON

**Infrastructure-Tests** (mit Stub-Sidecar + Mock-Adaptern fuer
`ITrainingSamplesWriter`, `IKnowledgeBaseIndexer`, `IYoloDatasetWriter`):
- `OperateurAnnotationService.PreviewMaskAsync`:
  - Happy: Sidecar liefert Maske + Polygon
  - Sidecar 503: wirft mit klarer Message
  - Niedrige Confidence: `MaskPreview.Warnings` enthaelt "LowSamConfidence"
- `OperateurAnnotationService.CommitAsync`:
  - Happy: alle drei Schritte erfolgreich
  - `ITrainingSamplesWriter.AppendAsync` fails: Abbruch, IsSuccess=false
  - `IYoloDatasetWriter` fails: Store + KB ok, YoloWritten=false
  - `IKnowledgeBaseIndexer` fails: Store + YOLO ok, KbIndexState=Pending,
    `ITrainingSamplesWriter.UpdateIndexStateAsync` wurde aufgerufen
  - `VsaYoloClassMap.TryGetClassId` returns false: skip YOLO,
    Warning "UnknownYoloClass" im CommitResult
- Frame-Cleanup: temp-Frame nach Erfolg umbenannt, nach Fehler geloescht

**Adapter-Tests** (Infrastructure):
- `TrainingSamplesWriterAdapter.AppendAsync` schreibt via
  `MergeAndSaveAsync` und Sample landet in der Datei
- `TrainingSamplesWriterAdapter.UpdateIndexStateAsync` findet das
  Sample per SampleId und aktualisiert nur `KbIndexState`
- `TrainingSamplesWriterAdapter.FindCommittedAsync` filtert auf
  `CaseId + SourceType + Code` mit Meter-Toleranz
- `VsaYoloClassMap.TryGetClassId` mit unbekanntem Code: returns
  false, **kein Eintrag im Map-File** (im Gegensatz zu `GetClassId`)

**UI-Smoke** (ohne FlaUI):
- ViewModel-Test: Session-Load, Active-Task-Selection
- Code-Liste-Binding gegen Mock-Session

**Live-Test** (opt-in `[Trait("Category", "LiveSidecar")]`):
- End-to-End: echter Sidecar, Test-Frame, Test-Store, Test-YOLO,
  Test-KB. Ueberprueft alle drei Persistierungen.

**Acceptance**: alle bestehenden Tests + neue OperateurAnnotation-Tests
gruen, Build 0 Warnungen / 0 Fehler.

### 5.4 Implementierungs-Reihenfolge

```
1. Domain + Application — Vertrag fixieren
   - TrainingSample-Erweiterung (Mask-Felder + FrameDeltaSeconds)
   - SourceTypeNames.OperateurAnnotation in TrainingSample.cs
   - DTOs (AnnotationRequest, MaskPreview mit Warnings, CommitResult)
   - OperateurAnnotationSession + CodeTask + States
   - Adapter-Interfaces (ITrainingSamplesWriter, IKnowledgeBaseIndexer,
     IYoloDatasetWriter)

2. VsaYoloClassMap erweitern
   - Neue Methode TryGetClassId(code, out id) ohne Auto-Create
   - Bestehende GetClassId bleibt fuer andere Konsumenten unveraendert

3. Sidecar-Endpoint + C#-DTOs
   - sidecar/schemas/segmentation.py: return_polygon, polygon_points
   - sidecar/routes/sam.py: cv2-Polygon-Approximation
   - VisionPipelineDtos.cs: SamRequest.ReturnPolygon,
     SamMaskResult.PolygonPoints
   - Sidecar-Live-Test mit return_polygon:true

4. Adapter-Implementierungen in Infrastructure
   - TrainingSamplesWriterAdapter: AppendAsync, UpdateIndexStateAsync,
     FindCommittedAsync (delegiert an statische TrainingSamplesStore)
   - KnowledgeBaseIndexerAdapter: delegiert an KnowledgeBaseManager
   - YoloDatasetExportService.AppendSampleAsync (neu, implementiert
     IYoloDatasetWriter)

5. Infrastructure-Service mit Stub-Sidecar-Tests
   - OperateurAnnotationService implementiert IOperateurAnnotationService
   - Konstruktor injiziert: VisionPipelineClient, IYoloDatasetWriter,
     IKnowledgeBaseIndexer, ITrainingSamplesWriter, VsaYoloClassMap-
     Lookup-Helper
   - Tests via StubHandler (Sidecar) + Mock-Adapter

6. UI-Partial: Code-Liste + Hotkeys (ohne Box-Drag)
   - eigene _operator...-Felder, isolierter Submode
   - Code-Liste-Binding gegen OperateurAnnotationSession
   - Smoke-Tests via ViewModel

7. UI: Box-Drag + Frame-Capture + Maske-Render
   - bestehendes Mark-Tool als Vorlage, aber eigener Overlay
   - Frame-Capture wiederverwendet bestehende Snapshot-Logik
   - Maske-Render aus PolygonJson (oder RLE-Fallback)

8. End-to-End Live-Test gegen echten Sidecar

9. Polishing (Wiederholt-Import via FindCommittedAsync,
   Temp-Frame-Cleanup, Toasts, Doku)
```

Geschaetzte Gesamtzeit: 2-3 Tage Solo-Entwicklung. Konkrete
Aufwands-Verteilung pro Schritt: Sache des Implementations-Plans.

### 5.5 Acceptance-Kriterien (Slice 1)

Slice 1 ist fertig, wenn:

- [ ] Haltungsordner-Import im TrainingMode funktioniert
- [ ] Code-Liste rechts ist befuellt
- [ ] Klick auf Code → Player-Seek
- [ ] Manuelles Scrubbing zeigt Frame-Δ
- [ ] Box-Drag loest SAM-Preview aus
- [ ] Maske wird im Overlay gerendert
- [ ] Confirm schreibt Sample in Store, YOLO (wenn Code mappbar) und KB
- [ ] CommitResult-Status sind im UI sichtbar
- [ ] Auto-Advance funktioniert
- [ ] Skip / Reject aendert Session-State, kein Sample geschrieben
- [ ] Wiederholter Import erkennt Committed-Codes
- [ ] Sidecar-Down: klare Fehlermeldung, kein Confirm moeglich
- [ ] KB-Down: Store + YOLO ok, KbIndexState=Pending
- [ ] Alle bestehenden Tests + neue OperateurAnnotation-Tests gruen

## 6. Risiken

| Risiko | Wahrscheinlichkeit | Mitigation |
|---|---|---|
| WPF-Box-Drag fummelig | hoch | bestehendes Mark-Tool aus PlayerWindow als Vorlage nutzen |
| Frame-Capture vom VLC-Player flickert | mittel | bestehende Snapshot-Logik aus Codiermodus wiederverwenden |
| SAM-Latenz bei grossen Frames | mittel | Frame vor SAM-Call auf 1024px schrumpfen |
| Adapter-Schreiben fuer Store/YOLO/KB groesser als gedacht | mittel | im Implementations-Plan separat planen, ggf. erst Slice 1.5 |
| `VsaYoloClassMap` luekenhaft fuer einige Codes | niedrig | Warning + Skip-YOLO-Pfad ist eingebaut, `TryGetClassId` ohne Auto-Create |
| Bestehender TrainingMode wird versehentlich wiederverwendet | mittel | isolierter Submode mit eigenen `_operator...`-Feldern (siehe 2.0) |
| `MergeOrUpdateAsync` aktualisiert Mask-Felder nicht | niedrig | Adapter-Vertrag (siehe 1.4) — Slice 1 schreibt einmal vollstaendig, danach nur KbIndexState-Update |
| Polygon-Format Edge-Cases (sehr kleine Mask) | niedrig | Sidecar bereinigt, C# reicht durch |

## 7. Offene Punkte fuer den Implementations-Plan

Die folgenden Entscheidungen werden im Plan, nicht im Design getroffen:

- Konkrete Adapter-Form fuer `TrainingSamplesStore` (Wrapper-Klasse vs.
  Refactor zu Instanz-Service)
- Ob `KnowledgeBaseManager` schon ein Interface hat oder eines bekommt
- Genauer Pfad-Pattern fuer YOLO-Datensatz `D:/yolo_sewer_v1/<train|val>/...`
  (existierende Convention pruefen)
- Ob "wiederholter Import erkennt Committed" einen Migrations-Step
  fuer alte Samples ohne `OperateurAnnotation`-SourceType braucht
- Ob `OperateurAnnotationSession` als ViewModel-Property oder als
  separates Service injiziert wird

## 8. Referenzen

- ADR-0001: Partial-Class-Refactor von HoldingFolderDistributor
- ADR-0005: Thin-AI-Architektur
- `CLAUDE.md`: Inference-Orchestrator, Pipeline-Stand
- `src/AuswertungPro.Next.Domain/Ai/Training/TrainingSample.cs`: Domain-POCO
- `src/AuswertungPro.Next.Application/Ai/Teacher/VsaYoloClassMap.cs`: stabile Class-IDs
- `src/AuswertungPro.Next.Application/Ai/Training/TrainingSamplesStore.cs`: Sample-Wahrheit
- `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs`: KB-Schnittstelle
- `src/AuswertungPro.Next.Infrastructure/Ai/Training/YoloDatasetExportService.cs`: YOLO-Schreiber
- `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.TrainingMode.cs`: bestehender Trainingsmodus
- `tests/AuswertungPro.Next.Pipeline.Tests/SidecarContractTests.cs`: StubHandler-Pattern fuer Tests
