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
  TrainingSample.cs                      ← ERWEITERT (Mask-Felder)
  SourceTypeNames.cs                     ← ERWEITERT ("OperateurAnnotation")

src/AuswertungPro.Next.Application/Ai/Annotation/
  IOperateurAnnotationService.cs         ← NEU
  OperateurAnnotationModels.cs           ← NEU (DTOs)
  OperateurAnnotationSession.cs          ← NEU (Session-State)

src/AuswertungPro.Next.Infrastructure/Ai/Annotation/
  OperateurAnnotationService.cs          ← NEU (Implementation)

src/AuswertungPro.Next.Infrastructure/Ai/Training/
  YoloDatasetExportService.cs            ← ERWEITERT (AppendSampleAsync)

src/AuswertungPro.Next.UI/Views/Windows/
  PlayerWindow.OperateurAnnotation.cs    ← NEU (Partial)

sidecar/main.py (oder /segment/sam-Endpoint)
                                         ← ERWEITERT (return_polygon-Flag)
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
**Interfaces oder Adapter**:

| Bestehende Klasse | Mocking-Bedarf | Vorschlag |
|---|---|---|
| `TrainingSamplesStore` | static class, schwer mockbar | Adapter: `ITrainingSamplesWriter` mit `Add(TrainingSample)`, `Update(TrainingSample)`, `TryGetByCaseAndCode(string, string, double)`. Implementation delegiert an die statische Klasse. |
| `KnowledgeBaseManager` | konkrete Klasse | falls noch kein Interface vorhanden: `IKnowledgeBaseIndexer` mit `IndexSampleAsync(TrainingSample, CancellationToken)`. |
| `YoloDatasetExportService` | konkrete Klasse | `IYoloDatasetWriter` mit `AppendSampleAsync(TrainingSample, CancellationToken)`. |
| `VisionPipelineClient` | konkrete Klasse, aber bereits via `HttpClient` injizierbar | Stub-Pattern wie in `SidecarContractTests` reicht. |
| `VsaYoloClassMap` | static-Methoden | direkt nutzen, keine Adapter — gibt nur stabile IDs zurueck, keine I/O. |

**Implementation der Seams gehoert in den Implementations-Plan**, nicht
in diese Design-Phase. Der Plan muss klaeren ob die Adapter neu
geschrieben oder durch Refactoring der bestehenden Klassen erreicht
werden.

## 2. UI im PlayerWindow.TrainingMode

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
    TimeSpan SamLatency);

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

3. TrainingSamplesStore.Add(sample)               ← STORE = WAHRHEIT
   ↑ failed: Abbruch, kein YOLO/KB
   ↑ success: StorePersisted = true

4. YoloDatasetExportService.AppendSampleAsync(...)
   - VsaYoloClassMap.GetClassId(code) — STABIL
   - falls Code nicht in Map: skip YOLO, Warning emit
   - Polygon aus Sidecar (return_polygon:true)
   ↑ failed: YoloWritten = false, weiter zu KB

5. KnowledgeBaseManager.IndexSampleAsync(...)
   ↑ failed:
       sample.KbIndexState = Pending
       Store.Update(sample) ← Status persistieren!
       KbIndexed = false

6. CommitResult zusammenstellen.
   IsSuccess = StorePersisted (alle anderen sind Status-Felder)
```

### 3.5 Sidecar `/segment/sam` — Erweiterung

Bestehender Endpoint bekommt einen zusaetzlichen Request-Parameter:

```python
# Sidecar
class SamRequest(BaseModel):
    image_base64: str
    bounding_boxes: list[SamBoundingBox]
    pipe_diameter_mm: int | None = None
    return_polygon: bool = False    # NEU
```

Wenn `return_polygon = true`: Response enthaelt zusaetzlich
`polygon_points` (List of [x, y]-Paaren) pro Maske, vorbereinigt
fuer YOLO-seg-Format.

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
| Code nicht in `VsaYoloClassMap` | Status-Panel: "Code in YOLO-Map fehlt — Sample geht nur in KB". CommitAsync skipped YOLO-Write. CommitResult.YoloWritten = false. |
| `TrainingSamplesStore.Add` failed | CommitResult.IsSuccess = false. CodeTask.State = Error. Error-Dialog zeigt Grund. |
| YOLO-Write failed | Store hat Sample, KB versucht. CodeTask bleibt Committed. Toast "YOLO-Datensatz nicht beschreibbar". |
| KB-Indexierung failed | Sample.KbIndexState = Pending, Store.Update. CodeTask Committed. KbIngestionPipeline (existiert) zieht spaeter nach. |
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

**Infrastructure-Tests**:
- `OperateurAnnotationService.PreviewMaskAsync`:
  - Happy: Sidecar liefert Maske + Polygon
  - Sidecar 503: wirft mit klarer Message
  - Niedrige Confidence: Warning im Preview
- `OperateurAnnotationService.CommitAsync`:
  - Happy: alle drei Schritte erfolgreich
  - Store fails: Abbruch, IsSuccess=false
  - YOLO fails: Store + KB ok, YoloWritten=false
  - KB fails: Store + YOLO ok, KbIndexState=Pending, Store.Update aufgerufen
  - Code unbekannt in `VsaYoloClassMap`: skip YOLO, Warning
- Frame-Cleanup: temp-Frame nach Erfolg umbenannt, nach Fehler geloescht

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
2. Sidecar-Endpoint return_polygon-Flag
3. Adapter/Interfaces fuer Store/YOLO/KB (Mocking-Voraussetzung)
4. Infrastructure-Service mit Stub-Sidecar-Tests
5. UI-Partial: Code-Liste + Hotkeys (ohne Box-Drag)
6. UI: Box-Drag + Frame-Capture + Maske-Render
7. End-to-End Live-Test
8. Polishing (Wiederholt-Import, Cleanup, Toasts)
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
| `VsaYoloClassMap` luekenhaft fuer einige Codes | niedrig | Warning + Skip-YOLO-Pfad ist eingebaut |
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
