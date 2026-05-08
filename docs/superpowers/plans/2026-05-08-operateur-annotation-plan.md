# Operateur-Annotation im Trainingsmodus — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Im Trainingsmodus pro Klick + Box ein Operateur-validiertes Trainings-Sample (Frame + SAM-Maske + VSA-Code) parallel in TrainingSamplesStore + KnowledgeBase + YOLO-Datensatz schreiben.

**Architecture:** Two-Phase-API (`PreviewMaskAsync` ohne Persistenz, `CommitAsync` mit Best-Effort Store→YOLO→KB). Service in Infrastructure, Adapter-Interfaces in Application, Stub-Mocking-Ready. UI als isolierter Submodus in `PlayerWindow.TrainingMode`. Sidecar `/segment/sam` um optionalen Polygon-Output erweitert.

**Tech Stack:** .NET 10, WPF, xUnit 2.7, FastAPI/Python-Sidecar, SAM 2.1, OpenCv (Python-only), VLC-Player.

**Spec-Referenz:** [`docs/superpowers/specs/2026-05-08-operateur-annotation-design.md`](../specs/2026-05-08-operateur-annotation-design.md)

---

## ⚠️ Plan-Korrekturen (Review 2026-05-08)

Vor Implementation-Start: 6 Blocker am echten Code verifiziert und korrigiert.
Tasks unten sind **mit diesen Korrekturen** auszufuehren — wo Task-Code und
Korrektur kollidieren, gilt die Korrektur.

### Blocker B1 — Sidecar: Polygon im Wrapper, nicht in der Route

`sidecar/sidecar/routes/sam.py` hat **keinen Zugriff** auf die rohe numpy-Maske
— die Route delegiert an den Wrapper. Polygon-Approximation gehoert in
`sidecar/sidecar/models/sam_wrapper.py` (dort lebt `numpy.ndarray`).
Plus: das Schema heisst real `MaskResult` (nicht `SamMaskResult`).

**Patch fuer Task 7**:
- `sidecar/sidecar/schemas/segmentation.py`: `class MaskResult(BaseModel)` um
  `polygon_points: list[list[float]] | None = None` ergaenzen.
- `sidecar/sidecar/schemas/segmentation.py`: `class SamRequest(BaseModel)` um
  `return_polygon: bool = False` ergaenzen.
- `sidecar/sidecar/models/sam_wrapper.py`: Polygon-Approximation **innerhalb**
  der Wrapper-Methode (wo die binaere Maske als numpy-Array vorliegt), Polygon
  als zusaetzliches Output zurueckgeben.
- `sidecar/sidecar/routes/sam.py`: Polygon-Wert vom Wrapper durchreichen,
  **kein** cv2-Import in der Route.

### Blocker B2 — C# `SamMaskResult`: ergaenzen, nicht ersetzen

Realer `SamMaskResult` hat **10 Felder**: `Label, Confidence, Bbox, MaskRle,
MaskAreaPixels, ImageAreaPixels, HeightPixels, WidthPixels, CentroidX,
CentroidY`. Der Plan-Code in Task 8 ersetzt den Record und verliert
4 Felder. Stattdessen: am Ende `PolygonPoints` als optional ergaenzen.

**Korrigierter Code fuer Task 8 Step 3** (`SamMaskResult`):
```csharp
public sealed record SamMaskResult(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("bbox")] IReadOnlyList<double> Bbox,
    [property: JsonPropertyName("mask_rle")] string MaskRle,
    [property: JsonPropertyName("mask_area_pixels")] int MaskAreaPixels,
    [property: JsonPropertyName("image_area_pixels")] int ImageAreaPixels,
    [property: JsonPropertyName("height_pixels")] int HeightPixels,
    [property: JsonPropertyName("width_pixels")] int WidthPixels,
    [property: JsonPropertyName("centroid_x")] double CentroidX,
    [property: JsonPropertyName("centroid_y")] double CentroidY,
    [property: JsonPropertyName("polygon_points")]
        IReadOnlyList<IReadOnlyList<double>>? PolygonPoints = null   // NEU
);
```

`SamResponse` hat **4 Felder**: `Masks, ImageWidth, ImageHeight,
InferenceTimeMs`. Test-Stubs in Task 13/14/15 muessen das beim Bauen von
`SamResponse(...)` treffen — der Plan-Stub passt nicht zur echten Signatur.

### Blocker B3 — `IYoloDatasetWriter.AppendSampleAsync` braucht Polygon

Polygon-Punkte liegen nur in `MaskPreview.PolygonJson`, nicht in
`TrainingSample`. Fuer YOLO-seg muss der Writer Polygon kennen.

**Patch fuer Task 5 Step 3** (`IYoloDatasetWriter`):
```csharp
public interface IYoloDatasetWriter
{
    Task<string> AppendSampleAsync(
        TrainingSample sample,
        MaskPreview preview,    // NEU — Polygon-Quelle
        CancellationToken ct);
}
```

`OperateurAnnotationService.CommitAsync` reicht entsprechend `preview` durch.

### Blocker B4 — YOLO-Pfad-Layout: `images/train/`, nicht `train/images/`

Realer `YoloDatasetExportService.ExportAsync` nutzt:
```
{outputDir}/images/train/   {outputDir}/images/val/
{outputDir}/labels/train/   {outputDir}/labels/val/
```
Plan in Task 12 hatte es vertauscht. Plus: der bestehende Service hat
**keinen** Konstruktor mit `_datasetRoot`-Field — er nimmt `outputDir` als
Methoden-Parameter. Fuer Tests entweder
- `AppendSampleAsync(sample, preview, outputDir, ct)` (zusaetzlicher Param), oder
- ein neuer Konstruktor mit injizierbarem Default-Dataset-Root.

**Empfehlung**: Konstruktor-Variante als Erweiterung, damit DI den Default
liefert (`D:/yolo_sewer_v1`) und Tests einen Test-Pfad uebergeben koennen.

**Wichtige Klippe**: `ExportAsync` baut die Class-Map aktuell **selbst** aus
den Sample-Codes (`classMap` in der Methode), nutzt **nicht**
`VsaYoloClassMap`. Wenn `AppendSampleAsync` `VsaYoloClassMap.TryGetClassId`
nutzt, sind Class-IDs zwischen Append- und Export-Pfad **inkonsistent**.

**Loesung in Slice 1**: `AppendSampleAsync` schreibt eine `data.yaml` mit der
gleichen Klassen-Reihenfolge wie `VsaYoloClassMap` (oder die Klassen-Map
wird beim ersten Append einmal initialisiert und gespeichert). Bestehende
`ExportAsync`-Logik bleibt unangetastet, ist aber als Tech-Debt zu
dokumentieren ("Inkompatibilitaet Append vs. Export-Pfad — Slice 2").

### Blocker B5 — Kein `PlayerWindowViewModel`

`PlayerWindow` ist **eine** partial class ueber 21 Dateien — **es gibt kein
Window-VM**. Bestehendes Pattern (siehe `PlayerWindow.MarkTool.cs`,
`PlayerWindow.TrainingMode.cs`): direkte Felder im Window plus XAML-Bindings
gegen `x:Name`-Elemente.

**Patch fuer Task 16/18/19/20/22/23**:
- `OperateurAnnotationSession` und `CodeTask? Active` als **direkte Felder**
  im PlayerWindow.OperateurAnnotation.cs:
  ```csharp
  private OperateurAnnotationSession? _operatorSession;
  private CodeTask? _operatorActive;
  ```
- DataBinding gegen `_operatorSession.Tasks` per Code-Behind:
  ```csharp
  OperatorCodeList.ItemsSource = _operatorSession?.Tasks;
  ```
- ListBox-`SelectedItem` per `SelectionChanged`-Handler statt Two-Way-Binding.
- Task 16 entfaellt komplett (keine VM-Erweiterung), bzw. wird zu
  "Field-Initialisierung im Partial".

### Blocker B6 — WPF-Airspace mit LibVLC: Canvas direkt funktioniert nicht

Ein normales `Canvas` ueber dem `VideoView` ist im Airspace-Modus
**unsichtbar**. Bestehendes Pattern in `PlayerWindow.MarkTool.cs` und
`PlayerWindow.CodingOverlayRender.cs`: `Popup` mit eigenem Render oder
`AdornerLayer` ueber dem VideoView-Container.

**Patch fuer Task 20**:
- **Nicht** `<Canvas x:Name="OperatorOverlayCanvas" .../>` direkt ueber
  `VideoView` legen.
- Stattdessen das bestehende Overlay-Pattern aus `PlayerWindow.MarkTool.cs`
  uebernehmen (gleicher Pop-up-/Adorner-Mechanismus, der heute fuer Punkt/
  Ellipse/Freihand/Rechteck-Markieren funktioniert).
- Beim Implementieren: erst `PlayerWindow.MarkTool.cs` lesen, dann
  identische Render-Pipeline fuer den neuen `_operator...`-Submodus
  wiederverwenden.

### Korrektur K1 — `FindCommittedAsync` braucht Meterstand-Parameter

Mit `meterTolerance` aber ohne Anker-Meterstand ist die Toleranz sinnlos.

**Patch fuer Task 5 Step 1, Task 10 Step 1+3, Task 25**:
```csharp
Task<IReadOnlyList<TrainingSample>> FindCommittedAsync(
    string caseId,
    string sourceType,
    string code,
    double meter,            // NEU — Anker-Meterstand
    double meterTolerance,
    CancellationToken ct);
```

Implementation in `TrainingSamplesWriterAdapter`:
```csharp
return all
    .Where(s => string.Equals(s.CaseId, caseId, StringComparison.OrdinalIgnoreCase))
    .Where(s => string.Equals(s.SourceType, sourceType, StringComparison.OrdinalIgnoreCase))
    .Where(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase))
    .Where(s => Math.Abs(s.MeterStart - meter) <= meterTolerance)   // NEU
    .ToList();
```

Test in Task 10: erwarte **2** statt 3 Treffer fuer `meter=10.0,
tolerance=0.5` (10.0 + 10.3 sind drin, 12.0 nicht).

### Korrektur K2 — `CommitAsync` setzt KB-Indexed bei Erfolg

Bei KB-Erfolg muss der Store-Zustand auch auf `KbIndexState.Indexed`
aktualisiert werden, sonst bleibt das Sample dauerhaft auf `None`.

**Patch in Task 14 Step 3** (CommitAsync, KB-Block):
```csharp
// 3. KB (best-effort)
bool kbIndexed = false;
try
{
    await _indexer.IndexSampleAsync(sample, ct);
    kbIndexed = true;
    // Status persistieren — Indexed
    try
    {
        await _writer.UpdateIndexStateAsync(
            sampleId, Domain.Ai.Training.KbIndexState.Indexed, ct);
    }
    catch
    {
        warnings.Add("KbIndexedStateUpdateFailed");
    }
}
catch (OperationCanceledException)
{
    throw;   // niemals als Warning schlucken
}
catch (Exception ex)
{
    warnings.Add($"KbFailed:{ex.Message}");
    try
    {
        await _writer.UpdateIndexStateAsync(
            sampleId, Domain.Ai.Training.KbIndexState.Pending, ct);
    }
    catch
    {
        warnings.Add("KbStateUpdateFailed");
    }
}
```

Gleiche `OperationCanceledException`-Behandlung in den Store- und
YOLO-Bloecken — Cancellation wird durchgereicht, nicht als Warning
maskiert.

### Korrektur K3 — Temp-Frame-Finalisierung

Plan erwaehnt nur das Loeschen von Temp-Frames, nicht das **Umbenennen**
nach `%KI_BRAIN%/frames/<CaseId>/<SampleId>.png` bei erfolgreichem Commit.
Das muss in `OperateurAnnotationService.CommitAsync` **vor** dem Store-
Append passieren.

**Ergaenzung in Task 14 Step 3** (vor "1. Store"):
```csharp
// 0. Frame finalisieren (temp -> KI_BRAIN/frames/<CaseId>/<SampleId>.png)
var brainRoot = AuswertungPro.Next.Application.Common.KnowledgeRootProvider.GetRoot();
var caseDir = Path.Combine(brainRoot, "frames", request.CaseId);
Directory.CreateDirectory(caseDir);
var finalFramePath = Path.Combine(caseDir, $"{sampleId}.png");
File.Copy(request.FramePath, finalFramePath, overwrite: false);

// sample bekommt finalen Pfad, nicht den temp-Pfad
sample.FramePath = finalFramePath;
```

UI-seitig (Task 22/23): nach erfolgreichem Commit den temp-Pfad aufraeumen,
**nicht** als FramePath im Sample verwenden.

### Korrektur K4 — DI ueber `ServiceCollectionConfigurator`

Real existiert `src/AuswertungPro.Next.UI/Composition/ServiceCollectionConfigurator.cs`
(getestet via `ServiceCollectionConfiguratorTests`). Plan-Code in Task 22
schreibt direkt in `App.xaml.cs` — falsch.

**Patch fuer Task 22 Step 1**: Registrierungen in
`ServiceCollectionConfigurator.cs` einfuegen, nicht in `App.xaml.cs`.

### Korrektur K5 — Test-Stub-Signaturen

Die Test-Stubs in Task 13 nutzen `new SamResponse(masks, 12.3)` mit zwei
Argumenten. Realer Constructor hat **vier** Argumente:
`SamResponse(Masks, ImageWidth, ImageHeight, InferenceTimeMs)`. Stubs in
Tasks 13/14/15 entsprechend anpassen.

Gleiches gilt fuer `new SamMaskResult(...)` — 10 Pflicht-Felder + optional
Polygon (siehe B2).

---

| Datei | Status | Verantwortung |
|---|---|---|
| `src/AuswertungPro.Next.Domain/Ai/Training/TrainingSample.cs` | MODIFY | Mask-Felder + FrameDeltaSeconds + SourceTypeNames-Konstante |
| `src/AuswertungPro.Next.Application/Ai/Annotation/OperateurAnnotationModels.cs` | CREATE | DTOs (Request/Preview/CommitResult/BBox) |
| `src/AuswertungPro.Next.Application/Ai/Annotation/OperateurAnnotationSession.cs` | CREATE | Session-State (CodeTask + CodeTaskState) |
| `src/AuswertungPro.Next.Application/Ai/Annotation/IOperateurAnnotationService.cs` | CREATE | Service-Interface (Two-Phase-API) |
| `src/AuswertungPro.Next.Application/Ai/Annotation/ITrainingSamplesWriter.cs` | CREATE | Adapter-Interface fuer Store |
| `src/AuswertungPro.Next.Application/Ai/Annotation/IKnowledgeBaseIndexer.cs` | CREATE | Adapter-Interface fuer KB |
| `src/AuswertungPro.Next.Application/Ai/Annotation/IYoloDatasetWriter.cs` | CREATE | Adapter-Interface fuer YOLO |
| `src/AuswertungPro.Next.Application/Ai/Teacher/VsaYoloClassMap.cs` | MODIFY | TryGetClassId(out) ohne Auto-Create |
| `src/AuswertungPro.Next.Application/Ai/Pipeline/VisionPipelineDtos.cs` | MODIFY | SamRequest.ReturnPolygon, SamMaskResult.PolygonPoints |
| `sidecar/sidecar/schemas/segmentation.py` | MODIFY | return_polygon, polygon_points |
| `sidecar/sidecar/routes/sam.py` | MODIFY | Polygon-Approximation via cv2 |
| `src/AuswertungPro.Next.Infrastructure/Ai/Annotation/TrainingSamplesWriterAdapter.cs` | CREATE | Delegate-Adapter |
| `src/AuswertungPro.Next.Infrastructure/Ai/Annotation/KnowledgeBaseIndexerAdapter.cs` | CREATE | Delegate-Adapter |
| `src/AuswertungPro.Next.Infrastructure/Ai/Training/YoloDatasetExportService.cs` | MODIFY | AppendSampleAsync (Single-Sample) |
| `src/AuswertungPro.Next.Infrastructure/Ai/Annotation/OperateurAnnotationService.cs` | CREATE | Service-Implementation |
| `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs` | CREATE | UI-Partial im TrainingMode |
| `src/AuswertungPro.Next.UI/Composition/ServiceCollectionConfigurator.cs` | MODIFY | DI-Registrierung der neuen Services (siehe K4) |
| `tests/AuswertungPro.Next.Pipeline.Tests/` | CREATE | Diverse Test-Dateien (Tasks zeigen Pfade) |

---

## Phase 1 — Domain + Application (Vertrag)

### Task 1: TrainingSample um Mask-Felder + FrameDeltaSeconds erweitern

**Files:**
- Modify: `src/AuswertungPro.Next.Domain/Ai/Training/TrainingSample.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/TrainingSampleMaskFieldsTests.cs` (neu)

- [ ] **Step 1: Test-Datei anlegen mit failing Tests**

```csharp
// tests/AuswertungPro.Next.Pipeline.Tests/TrainingSampleMaskFieldsTests.cs
using AuswertungPro.Next.Domain.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingSampleMaskFieldsTests
{
    [Fact]
    public void HasMask_WhenAllMaskFieldsSet_ReturnsTrue()
    {
        var sample = new TrainingSample
        {
            SamMaskRle = "fake-rle",
            SamMaskEncoding = "sidecar-sam-rle-v1",
            MaskWidth = 640,
            MaskHeight = 480,
            MaskAreaPixels = 1234,
            SamConfidence = 0.85
        };

        Assert.True(sample.HasMask);
    }

    [Fact]
    public void HasMask_WhenRleEmpty_ReturnsFalse()
    {
        var sample = new TrainingSample
        {
            SamMaskRle = "",
            MaskWidth = 640,
            MaskHeight = 480
        };

        Assert.False(sample.HasMask);
    }

    [Fact]
    public void HasMask_WhenWidthMissing_ReturnsFalse()
    {
        var sample = new TrainingSample
        {
            SamMaskRle = "fake-rle",
            MaskHeight = 480
        };

        Assert.False(sample.HasMask);
    }

    [Fact]
    public void FrameDeltaSeconds_DefaultsToNull()
    {
        var sample = new TrainingSample();
        Assert.Null(sample.FrameDeltaSeconds);
    }

    [Fact]
    public void FrameDeltaSeconds_AcceptsNegative()
    {
        var sample = new TrainingSample { FrameDeltaSeconds = -2.5 };
        Assert.Equal(-2.5, sample.FrameDeltaSeconds);
    }
}
```

- [ ] **Step 2: Tests laufen lassen — muessen failen (Felder existieren noch nicht)**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj `
  --filter "FullyQualifiedName~TrainingSampleMaskFieldsTests" --nologo
```

Expected: BUILD ERROR — `SamMaskRle`, `MaskWidth`, etc. nicht gefunden.

- [ ] **Step 3: Felder in TrainingSample.cs ergaenzen**

In `src/AuswertungPro.Next.Domain/Ai/Training/TrainingSample.cs`, **vor** dem Block `// BoundingBox (normiert 0-1, YOLO-Format: center + size)`:

```csharp
// ── SAM-Maske (Operateur-Annotation, Slice 1) ─────────────────────
/// <summary>
/// Run-Length-Encoded Maske, opaque Format vom Sidecar geliefert.
/// Format-Tag in <see cref="SamMaskEncoding"/>.
/// </summary>
public string? SamMaskRle { get; set; }

/// <summary>Format-Tag fuer Migrations-Faehigkeit (z.B. "sidecar-sam-rle-v1").</summary>
public string? SamMaskEncoding { get; set; }

/// <summary>Pixelbreite der Maske (= Frame-Breite zum Annotations-Zeitpunkt).</summary>
public int? MaskWidth { get; set; }

/// <summary>Pixelhoehe der Maske.</summary>
public int? MaskHeight { get; set; }

/// <summary>Anzahl gesetzter Pixel in der Maske (fuer Filter "zu klein").</summary>
public int? MaskAreaPixels { get; set; }

/// <summary>SAM-Score 0..1 fuer die Maske-Qualitaet.</summary>
public double? SamConfidence { get; set; }

/// <summary>Hat eine vollstaendige Maske?</summary>
public bool HasMask => !string.IsNullOrWhiteSpace(SamMaskRle)
                      && MaskWidth.HasValue
                      && MaskHeight.HasValue;

// ── Frame-Delta fuer Drift-Analyse (Operateur-Annotation, Slice 1) ─
/// <summary>
/// Differenz zwischen Protokoll-Meterstand und tatsaechlich annotiertem
/// Frame in Sekunden. Positiv = Frame liegt nach dem Protokoll-Meter.
/// Null = User hat den Vorschlag nicht verlassen.
/// </summary>
public double? FrameDeltaSeconds { get; set; }
```

- [ ] **Step 4: Tests laufen lassen — muessen jetzt passen**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj `
  --filter "FullyQualifiedName~TrainingSampleMaskFieldsTests" --nologo
```

Expected: 5 erfolgreich, 0 Fehler.

- [ ] **Step 5: Vollstaendiger Test-Lauf, keine Regression**

```powershell
dotnet test --nologo
```

Expected: vorheriger Stand + 5 neue Tests gruen.

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.Domain/Ai/Training/TrainingSample.cs tests/AuswertungPro.Next.Pipeline.Tests/TrainingSampleMaskFieldsTests.cs
git commit -m "feat(domain): add SAM mask + frame delta fields to TrainingSample"
```

---

### Task 2: SourceTypeNames um OperateurAnnotation erweitern

**Files:**
- Modify: `src/AuswertungPro.Next.Domain/Ai/Training/TrainingSample.cs` (gleiche Datei, andere Stelle)
- Test: vorhandene `TrainingSampleMaskFieldsTests` ergaenzen

- [ ] **Step 1: Failing Test ergaenzen**

In `tests/AuswertungPro.Next.Pipeline.Tests/TrainingSampleMaskFieldsTests.cs`:

```csharp
[Fact]
public void SourceTypeNames_HasOperateurAnnotation()
{
    Assert.Equal("OperateurAnnotation", SourceTypeNames.OperateurAnnotation);
}
```

- [ ] **Step 2: Test laufen lassen — failed mit "OperateurAnnotation nicht gefunden"**

```powershell
dotnet build src/AuswertungPro.Next.Domain/AuswertungPro.Next.Domain.csproj
```

Expected: BUILD ERROR.

- [ ] **Step 3: Konstante in SourceTypeNames-Klasse ergaenzen**

In `TrainingSample.cs`, in der `public static class SourceTypeNames`:

```csharp
public const string OperateurAnnotation = "OperateurAnnotation";
```

- [ ] **Step 4: Tests laufen lassen — gruen**

```powershell
dotnet test --nologo
```

Expected: alle Tests gruen.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Domain/Ai/Training/TrainingSample.cs tests/AuswertungPro.Next.Pipeline.Tests/TrainingSampleMaskFieldsTests.cs
git commit -m "feat(domain): add SourceTypeNames.OperateurAnnotation"
```

---

### Task 3: Application-DTOs (AnnotationRequest, BoundingBoxNormalized, MaskPreview, CommitResult)

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/Annotation/OperateurAnnotationModels.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationModelsTests.cs`

- [ ] **Step 1: Test-Datei anlegen mit Failing Tests**

```csharp
// tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationModelsTests.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using System.Collections.Generic;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class OperateurAnnotationModelsTests
{
    [Fact]
    public void AnnotationRequest_AllFieldsRequired()
    {
        var req = new AnnotationRequest(
            CaseId: "haltung-100-200",
            Code: "BAB B",
            ProtocolMeterstand: 12.30,
            SuggestedFrameTimeSeconds: 145.5,
            ActualFrameTimeSeconds: 149.7,
            VideoFrameIndex: 3742,
            FramePath: @"C:\KI_BRAIN\frames\test.png",
            FrameWidth: 1920,
            FrameHeight: 1080,
            Box: new BoundingBoxNormalized(0.5, 0.5, 0.2, 0.3));

        Assert.Equal("BAB B", req.Code);
        Assert.Equal(0.5, req.Box.XCenter);
        Assert.Equal(149.7, req.ActualFrameTimeSeconds);
    }

    [Fact]
    public void MaskPreview_WithWarnings()
    {
        var preview = new MaskPreview(
            SamMaskRle: "rle-data",
            SamMaskEncoding: "sidecar-sam-rle-v1",
            PolygonJson: "[[1,2],[3,4]]",
            MaskWidth: 1920,
            MaskHeight: 1080,
            MaskAreaPixels: 5000,
            SamConfidence: 0.25,
            SamLatency: System.TimeSpan.FromMilliseconds(420),
            Warnings: new[] { "LowSamConfidence" });

        Assert.NotNull(preview.Warnings);
        Assert.Single(preview.Warnings);
        Assert.Contains("LowSamConfidence", preview.Warnings);
    }

    [Fact]
    public void CommitResult_IsSuccessEqualsStorePersisted_EvenIfYoloAndKbFail()
    {
        var result = new CommitResult(
            IsSuccess: true,
            SampleId: "abc-123",
            FramePath: "frame.png",
            LabelPath: null,
            StorePersisted: true,
            KbIndexed: false,
            YoloWritten: false,
            Error: null,
            Warnings: new[] { "KbDown", "YoloDown" });

        Assert.True(result.IsSuccess);
        Assert.True(result.StorePersisted);
        Assert.False(result.KbIndexed);
        Assert.False(result.YoloWritten);
    }
}
```

- [ ] **Step 2: Tests laufen lassen — failen wegen fehlender Klassen**

```powershell
dotnet build src/AuswertungPro.Next.Application/AuswertungPro.Next.Application.csproj
```

Expected: BUILD ERROR — `AnnotationRequest`, `MaskPreview`, etc. nicht gefunden.

- [ ] **Step 3: DTOs anlegen**

```csharp
// src/AuswertungPro.Next.Application/Ai/Annotation/OperateurAnnotationModels.cs
using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Eingabe fuer PreviewMaskAsync und CommitAsync. Die UI hat den Frame
/// bereits nach <see cref="FramePath"/> geschrieben.
/// </summary>
public sealed record AnnotationRequest(
    string CaseId,
    string Code,
    double ProtocolMeterstand,
    double SuggestedFrameTimeSeconds,
    double ActualFrameTimeSeconds,
    int VideoFrameIndex,
    string FramePath,
    int FrameWidth,
    int FrameHeight,
    BoundingBoxNormalized Box);

/// <summary>
/// Bounding-Box im YOLO-Format (Center + Size, normalisiert 0..1).
/// </summary>
public sealed record BoundingBoxNormalized(
    double XCenter,
    double YCenter,
    double Width,
    double Height);

/// <summary>
/// SAM-Maske als RLE + vorberechneter Polygon-String.
/// Bei Slice 1: PolygonJson ist die Quelle fuer den YOLO-seg-Label.
/// </summary>
public sealed record MaskPreview(
    string SamMaskRle,
    string SamMaskEncoding,
    string PolygonJson,
    int MaskWidth,
    int MaskHeight,
    int MaskAreaPixels,
    double SamConfidence,
    TimeSpan SamLatency,
    IReadOnlyList<string>? Warnings);

/// <summary>
/// Ergebnis eines CommitAsync-Aufrufs. <see cref="IsSuccess"/> entspricht
/// <see cref="StorePersisted"/> — KB und YOLO sind separate Status-Felder.
/// </summary>
public sealed record CommitResult(
    bool IsSuccess,
    string SampleId,
    string? FramePath,
    string? LabelPath,
    bool StorePersisted,
    bool KbIndexed,
    bool YoloWritten,
    string? Error,
    IReadOnlyList<string>? Warnings);
```

- [ ] **Step 4: Tests laufen lassen — gruen**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj `
  --filter "FullyQualifiedName~OperateurAnnotationModelsTests" --nologo
```

Expected: 3 erfolgreich.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Application/Ai/Annotation/OperateurAnnotationModels.cs tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationModelsTests.cs
git commit -m "feat(application): add operateur annotation DTOs"
```

---

### Task 4: OperateurAnnotationSession + CodeTask + State-Transitions

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/Annotation/OperateurAnnotationSession.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationSessionTests.cs`

- [ ] **Step 1: Test-Datei anlegen**

```csharp
// tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationSessionTests.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class OperateurAnnotationSessionTests
{
    [Fact]
    public void Session_NewlyConstructed_HasEmptyTasks()
    {
        var session = new OperateurAnnotationSession
        {
            CaseId = "case-1",
            VideoPath = "video.mp4",
            PdfPath = "protocol.pdf"
        };
        Assert.Empty(session.Tasks);
        Assert.Null(session.Active);
    }

    [Fact]
    public void CodeTask_DefaultState_IsPending()
    {
        var task = new CodeTask { Code = "BAB B", Meterstand = 12.3 };
        Assert.Equal(CodeTaskState.Pending, task.State);
    }

    [Fact]
    public void CodeTask_TransitionToActive_ClearsBoxAndPreview()
    {
        var task = new CodeTask
        {
            Code = "BAB B",
            Meterstand = 12.3,
            State = CodeTaskState.PreviewReady,
            Box = new BoundingBoxNormalized(0.5, 0.5, 0.1, 0.1)
        };

        task.State = CodeTaskState.Active;
        task.Box = null;
        task.Preview = null;

        Assert.Equal(CodeTaskState.Active, task.State);
        Assert.Null(task.Box);
    }

    [Fact]
    public void CodeTask_Committed_SetsSampleIdAndUtc()
    {
        var task = new CodeTask
        {
            Code = "BAB B",
            Meterstand = 12.3,
            State = CodeTaskState.Committed,
            CommittedSampleId = "sample-xyz",
            CommittedUtc = System.DateTime.UtcNow
        };

        Assert.Equal("sample-xyz", task.CommittedSampleId);
        Assert.NotNull(task.CommittedUtc);
    }

    [Fact]
    public void CodeTask_Skipped_StoresUserReason()
    {
        var task = new CodeTask
        {
            Code = "BBB Z",
            Meterstand = 24.1,
            State = CodeTaskState.Skipped,
            UserReason = "Frame komplett unscharf"
        };

        Assert.Equal(CodeTaskState.Skipped, task.State);
        Assert.Equal("Frame komplett unscharf", task.UserReason);
    }

    [Fact]
    public void CodeTaskState_AllValuesDefined()
    {
        // Sicherstellen dass alle 7 States existieren (Slice 1)
        var states = new[]
        {
            CodeTaskState.Pending,
            CodeTaskState.Active,
            CodeTaskState.PreviewReady,
            CodeTaskState.Committed,
            CodeTaskState.Skipped,
            CodeTaskState.Rejected,
            CodeTaskState.Error
        };
        Assert.Equal(7, states.Length);
    }
}
```

- [ ] **Step 2: Tests laufen lassen — failen**

Expected: BUILD ERROR (Klassen fehlen).

- [ ] **Step 3: Session + Task anlegen**

```csharp
// src/AuswertungPro.Next.Application/Ai/Annotation/OperateurAnnotationSession.cs
using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Zustandsmaschine fuer einen aktiven Annotations-Zustand pro CodeTask.
/// PreviewReady = Box + Maske vorhanden, aber noch nicht committed.
/// </summary>
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

/// <summary>
/// Eine Annotations-Aufgabe pro Protokoll-Code. Lebt nur in der
/// Session, nicht persistiert. Dauerhafte Wahrheit ist
/// TrainingSamplesStore.
/// </summary>
public sealed class CodeTask
{
    public string Code { get; init; } = "";
    public double Meterstand { get; init; }
    public CodeTaskState State { get; set; } = CodeTaskState.Pending;

    // PreviewReady-Daten:
    public BoundingBoxNormalized? Box { get; set; }
    public MaskPreview? Preview { get; set; }
    public double? FrameDeltaSeconds { get; set; }

    // Committed-Daten:
    public string? CommittedSampleId { get; set; }
    public DateTime? CommittedUtc { get; set; }

    // Skipped/Rejected-Daten:
    public string? UserReason { get; set; }
}

/// <summary>
/// Aufgaben-Status pro TrainingMode-Sitzung. UI-thread-bound, lebt
/// im ViewModel waehrend der Sitzung. Keine Persistenz.
/// </summary>
public sealed class OperateurAnnotationSession
{
    public string CaseId { get; init; } = "";
    public string VideoPath { get; init; } = "";
    public string PdfPath { get; init; } = "";
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;

    public IList<CodeTask> Tasks { get; } = new List<CodeTask>();
    public CodeTask? Active { get; set; }
}
```

- [ ] **Step 4: Tests laufen lassen — gruen**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj `
  --filter "FullyQualifiedName~OperateurAnnotationSessionTests" --nologo
```

Expected: 6 erfolgreich.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Application/Ai/Annotation/OperateurAnnotationSession.cs tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationSessionTests.cs
git commit -m "feat(application): add operateur annotation session state"
```

---

### Task 5: Adapter-Interfaces + IOperateurAnnotationService

> ⚠️ **Korrektur K1 (FindCommittedAsync) + Blocker B3 (IYoloDatasetWriter)** —
> siehe Header "Plan-Korrekturen" oben. `FindCommittedAsync` braucht
> `double meter`-Parameter; `IYoloDatasetWriter.AppendSampleAsync` braucht
> `MaskPreview preview` als zusaetzlichen Parameter (Polygon-Quelle).
> Die Code-Bloecke unten sind die **alten** Signaturen — bei der Implementation
> mit den korrigierten Signaturen aus dem Header arbeiten.

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/Annotation/ITrainingSamplesWriter.cs`
- Create: `src/AuswertungPro.Next.Application/Ai/Annotation/IKnowledgeBaseIndexer.cs`
- Create: `src/AuswertungPro.Next.Application/Ai/Annotation/IYoloDatasetWriter.cs`
- Create: `src/AuswertungPro.Next.Application/Ai/Annotation/IOperateurAnnotationService.cs`

- [ ] **Step 1: ITrainingSamplesWriter**

```csharp
// src/AuswertungPro.Next.Application/Ai/Annotation/ITrainingSamplesWriter.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Adapter um den statischen TrainingSamplesStore. Drei Methoden mit
/// klarer Semantik fuer Slice 1.
/// </summary>
public interface ITrainingSamplesWriter
{
    /// <summary>
    /// Single-Sample-Append. Intern via MergeAndSaveAsync mit
    /// Single-Element-Liste (Dedup via Signature ist OK).
    /// </summary>
    Task AppendAsync(TrainingSample sample, CancellationToken ct);

    /// <summary>
    /// Findet den Sample anhand SampleId und aktualisiert nur den
    /// KbIndexState. Andere Felder bleiben unangetastet.
    /// </summary>
    Task UpdateIndexStateAsync(string sampleId, KbIndexState state, CancellationToken ct);

    /// <summary>
    /// Findet alle bereits committed Samples fuer eine Haltung+Source
    /// mit Code-Filter und Meter-Toleranz. Fuer Wiederholt-Import-
    /// Erkennung.
    /// </summary>
    Task<IReadOnlyList<TrainingSample>> FindCommittedAsync(
        string caseId,
        string sourceType,
        string code,
        double meterTolerance,
        CancellationToken ct);
}
```

- [ ] **Step 2: IKnowledgeBaseIndexer**

```csharp
// src/AuswertungPro.Next.Application/Ai/Annotation/IKnowledgeBaseIndexer.cs
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Adapter um den KnowledgeBaseManager. Indexiert ein Sample
/// (Embedding + KB-Eintrag).
/// </summary>
public interface IKnowledgeBaseIndexer
{
    Task IndexSampleAsync(TrainingSample sample, CancellationToken ct);
}
```

- [ ] **Step 3: IYoloDatasetWriter**

```csharp
// src/AuswertungPro.Next.Application/Ai/Annotation/IYoloDatasetWriter.cs
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Adapter fuer Single-Sample-Append in den YOLO-Datensatz
/// (D:/yolo_sewer_v1/{train|val}/images/*.png + labels/*.txt).
/// </summary>
public interface IYoloDatasetWriter
{
    /// <summary>
    /// Schreibt Frame.png + .txt-Label-Datei fuer ein einzelnes Sample.
    /// Wirft wenn Class-ID nicht stabil aufloesbar ist (Service muss
    /// vorher VsaYoloClassMap.TryGetClassId pruefen).
    /// </summary>
    /// <returns>Pfad zur geschriebenen Label-Datei.</returns>
    Task<string> AppendSampleAsync(TrainingSample sample, CancellationToken ct);
}
```

- [ ] **Step 4: IOperateurAnnotationService**

```csharp
// src/AuswertungPro.Next.Application/Ai/Annotation/IOperateurAnnotationService.cs
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Two-Phase-API fuer Operateur-Annotation.
/// Phase 1: Preview (SAM-Call ohne Persistenz)
/// Phase 2: Commit (Best-Effort Store > YOLO > KB)
/// </summary>
public interface IOperateurAnnotationService
{
    Task<MaskPreview> PreviewMaskAsync(AnnotationRequest request, CancellationToken ct);
    Task<CommitResult> CommitAsync(AnnotationRequest request, MaskPreview preview, CancellationToken ct);
}
```

- [ ] **Step 5: Build pruefen**

```powershell
dotnet build src/AuswertungPro.Next.Application/AuswertungPro.Next.Application.csproj
```

Expected: 0 Warnungen, 0 Fehler.

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.Application/Ai/Annotation/
git commit -m "feat(application): add adapter interfaces + service contract"
```

---

## Phase 2 — VsaYoloClassMap erweitern

### Task 6: TryGetClassId-Methode ohne Auto-Create

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Teacher/VsaYoloClassMap.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/VsaYoloClassMapTryGetTests.cs`

- [ ] **Step 1: Failing Test schreiben**

```csharp
// tests/AuswertungPro.Next.Pipeline.Tests/VsaYoloClassMapTryGetTests.cs
using AuswertungPro.Next.Application.Ai.Teacher;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VsaYoloClassMapTryGetTests
{
    [Fact]
    public void TryGetClassId_KnownCategory_ReturnsTrue()
    {
        // BAB ist in der Default-Map (Index 0)
        var ok = VsaYoloClassMap.TryGetClassId("BAB B", out var id);
        Assert.True(ok);
        Assert.True(id >= 0);
    }

    [Fact]
    public void TryGetClassId_UnknownCategory_ReturnsFalse_NoSideEffect()
    {
        // Wirklich unbekannte Kategorie (XYZ existiert nicht im VSA-Katalog)
        var ok = VsaYoloClassMap.TryGetClassId("XYZ Q", out var id);
        Assert.False(ok);
        Assert.Equal(-1, id);

        // Kontrolle: GetClassId macht Auto-Create — TryGetClassId nicht
        // (wir koennen das nicht direkt aus dem Test pruefen, aber
        // der Test dokumentiert die Erwartung)
    }

    [Fact]
    public void TryGetClassId_EmptyOrNull_ReturnsFalse()
    {
        Assert.False(VsaYoloClassMap.TryGetClassId("", out _));
        Assert.False(VsaYoloClassMap.TryGetClassId(null!, out _));
        Assert.False(VsaYoloClassMap.TryGetClassId("   ", out _));
    }
}
```

- [ ] **Step 2: Test laufen lassen — failed (Methode existiert nicht)**

```powershell
dotnet build src/AuswertungPro.Next.Application/AuswertungPro.Next.Application.csproj
```

Expected: BUILD ERROR.

- [ ] **Step 3: TryGetClassId in VsaYoloClassMap.cs ergaenzen**

In `src/AuswertungPro.Next.Application/Ai/Teacher/VsaYoloClassMap.cs`, **nach** der bestehenden `GetClassId`-Methode:

```csharp
/// <summary>
/// Wie <see cref="GetClassId"/>, aber **ohne** Auto-Create.
/// Liefert false wenn die Kategorie nicht in der Map ist.
/// Fuer Operateur-Annotation: keine instabilen Class-IDs erzeugen,
/// damit der YOLO-Datensatz konsistent bleibt.
/// </summary>
public static bool TryGetClassId(string vsaCode, out int classId)
{
    classId = -1;
    if (string.IsNullOrWhiteSpace(vsaCode)) return false;

    var category = ExtractCategory(vsaCode);

    lock (_lock)
    {
        EnsureLoaded();
        if (_map!.TryGetValue(category, out var id))
        {
            classId = id;
            return true;
        }
    }

    return false;
}
```

- [ ] **Step 4: Tests laufen lassen — gruen**

```powershell
dotnet test --filter "FullyQualifiedName~VsaYoloClassMapTryGetTests" --nologo
```

Expected: 3 erfolgreich.

- [ ] **Step 5: Vollstaendiger Test-Lauf — keine Regression**

```powershell
dotnet test --nologo
```

Expected: alle bestehenden Tests bleiben gruen.

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.Application/Ai/Teacher/VsaYoloClassMap.cs tests/AuswertungPro.Next.Pipeline.Tests/VsaYoloClassMapTryGetTests.cs
git commit -m "feat(yolo-class-map): add TryGetClassId without auto-create"
```

---

## Phase 3 — Sidecar + C#-DTOs

### Task 7: Python-Sidecar — return_polygon + cv2-Polygon-Approximation

> ⚠️ **Blocker B1 (Polygon im Wrapper, nicht in der Route)** — siehe Header.
> `routes/sam.py` hat **keinen Zugriff** auf die rohe numpy-Maske; cv2-Approx
> gehoert in `sidecar/sidecar/models/sam_wrapper.py`. Schema-Klasse heisst
> `MaskResult` (nicht `SamMaskResult`). Vor Implementation: `sam_wrapper.py`
> lesen und Polygon dort produzieren — die Route reicht nur durch.

**Files:**
- Modify: `sidecar/sidecar/schemas/segmentation.py`
- Modify: `sidecar/sidecar/models/sam_wrapper.py`   ← Polygon-Approx hier
- Modify: `sidecar/sidecar/routes/sam.py`           ← reicht nur durch

- [ ] **Step 1: Schema erweitern**

In `sidecar/sidecar/schemas/segmentation.py`, in `SamRequest`:

```python
class SamRequest(BaseModel):
    image_base64: str
    bounding_boxes: list[SamBoundingBox]
    pipe_diameter_mm: int | None = None
    point_prompts: list[SamPointPrompt] | None = None
    ring_scan: RingScanParams | None = None
    return_polygon: bool = False    # NEU
```

In `SamMaskResult`:

```python
class SamMaskResult(BaseModel):
    label: str
    confidence: float
    bbox: list[float]
    mask_rle: str
    mask_area_pixels: int
    image_area_pixels: int
    polygon_points: list[list[float]] | None = None    # NEU
```

- [ ] **Step 2: Route erweitern**

In `sidecar/sidecar/routes/sam.py`, im SAM-Endpoint, **nach** der Maske-Berechnung und **vor** dem Result-Bauen:

```python
import cv2
import numpy as np
from pycocotools import mask as mask_utils

# ... bestehender Code ...

# Polygon-Approximation wenn gewuenscht
polygon_points = None
if request.return_polygon:
    # mask_rle → numpy-Array → Contour → Polygon
    binary_mask = mask_utils.decode(rle_dict).astype(np.uint8) * 255
    contours, _ = cv2.findContours(
        binary_mask,
        cv2.RETR_EXTERNAL,
        cv2.CHAIN_APPROX_SIMPLE
    )
    if contours:
        # Groesste Contour
        largest = max(contours, key=cv2.contourArea)
        # Approximation mit ~1 % der Contour-Laenge
        epsilon = 0.005 * cv2.arcLength(largest, True)
        approx = cv2.approxPolyDP(largest, epsilon, True)
        # Zu [[x, y], [x, y], ...]
        polygon_points = [[float(p[0][0]), float(p[0][1])] for p in approx]

result = SamMaskResult(
    label=label,
    confidence=confidence,
    bbox=bbox,
    mask_rle=rle_string,
    mask_area_pixels=area_pixels,
    image_area_pixels=image_area,
    polygon_points=polygon_points
)
```

- [ ] **Step 3: Sidecar manuell starten und Health-Check**

```powershell
cd sidecar
python -m sidecar
```

In separatem Terminal:

```powershell
curl http://localhost:8100/health
```

Expected: `{"status":"ok",...}`.

- [ ] **Step 4: Manueller Test mit Test-Bild**

Create `sidecar/test_polygon.py`:

```python
import base64, requests

with open("sidecar/test_data/sample.png", "rb") as f:
    b64 = base64.b64encode(f.read()).decode()

r = requests.post("http://localhost:8100/segment/sam", json={
    "image_base64": b64,
    "bounding_boxes": [{"x1": 100, "y1": 100, "x2": 300, "y2": 300, "label": "test", "confidence": 1.0}],
    "return_polygon": True
})
print(r.status_code)
data = r.json()
print(f"Mask count: {len(data.get('masks', []))}")
if data.get('masks'):
    poly = data['masks'][0].get('polygon_points')
    print(f"Polygon points: {len(poly) if poly else 'None'}")
```

```powershell
python sidecar/test_polygon.py
```

Expected: Polygon-Points-Anzahl > 0.

- [ ] **Step 5: Commit**

```powershell
git add sidecar/sidecar/schemas/segmentation.py sidecar/sidecar/routes/sam.py
git commit -m "feat(sidecar): add return_polygon flag with cv2 approximation"
```

---

### Task 8: C# DTOs — SamRequest.ReturnPolygon + SamMaskResult.PolygonPoints

> ⚠️ **Blocker B2 (SamMaskResult ergaenzen, nicht ersetzen)** — siehe Header.
> Der reale Record hat **10 Felder**. Bei Step 3 die korrigierte Variante aus
> dem Header nehmen — die haengt `PolygonPoints` als optionales 11. Feld am
> Ende an und behaelt `HeightPixels`, `WidthPixels`, `CentroidX`, `CentroidY`.
> Achtung auch: `SamResponse` hat **4 Felder** (Masks, ImageWidth, ImageHeight,
> InferenceTimeMs) — relevant fuer Test-Stubs in Task 13/14/15.

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Ai/Pipeline/VisionPipelineDtos.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/SamPolygonDtoTests.cs`

- [ ] **Step 1: Failing Test**

```csharp
// tests/AuswertungPro.Next.Pipeline.Tests/SamPolygonDtoTests.cs
using AuswertungPro.Next.Application.Ai.Pipeline;
using System.Text.Json;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SamPolygonDtoTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void SamRequest_SerializesReturnPolygonAsSnakeCase()
    {
        var box = new SamBoundingBox(10, 20, 100, 200, "x", 1.0);
        var req = new SamRequest("base64", new[] { box }, ReturnPolygon: true);

        var json = JsonSerializer.Serialize(req, Json);
        Assert.Contains("\"return_polygon\":true", json);
    }

    [Fact]
    public void SamMaskResult_DeserializesPolygonPoints()
    {
        var json = """
        {
            "label": "x",
            "confidence": 0.9,
            "bbox": [1.0, 2.0, 3.0, 4.0],
            "mask_rle": "rle",
            "mask_area_pixels": 100,
            "image_area_pixels": 1000,
            "polygon_points": [[1.5, 2.5], [3.5, 4.5]]
        }
        """;
        var result = JsonSerializer.Deserialize<SamMaskResult>(json, Json);

        Assert.NotNull(result);
        Assert.NotNull(result!.PolygonPoints);
        Assert.Equal(2, result.PolygonPoints.Count);
        Assert.Equal(1.5, result.PolygonPoints[0][0]);
    }

    [Fact]
    public void SamMaskResult_NullPolygonPoints_IsValid()
    {
        var json = """
        {
            "label": "x",
            "confidence": 0.9,
            "bbox": [1.0, 2.0, 3.0, 4.0],
            "mask_rle": "rle",
            "mask_area_pixels": 100,
            "image_area_pixels": 1000
        }
        """;
        var result = JsonSerializer.Deserialize<SamMaskResult>(json, Json);

        Assert.NotNull(result);
        Assert.Null(result!.PolygonPoints);
    }
}
```

- [ ] **Step 2: Test build — failed**

```powershell
dotnet build tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj
```

Expected: BUILD ERROR.

- [ ] **Step 3: SamRequest und SamMaskResult erweitern**

In `src/AuswertungPro.Next.Application/Ai/Pipeline/VisionPipelineDtos.cs`:

`SamRequest` — neuer Parameter am Ende (Default `false`, rueckwaertskompatibel):

```csharp
public sealed record SamRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("bounding_boxes")] IReadOnlyList<SamBoundingBox> BoundingBoxes,
    [property: JsonPropertyName("point_prompts")] IReadOnlyList<SamPointPrompt>? PointPrompts = null,
    [property: JsonPropertyName("pipe_diameter_mm")] int? PipeDiameterMm = null,
    [property: JsonPropertyName("ring_scan")] RingScanParams? RingScan = null,
    [property: JsonPropertyName("return_polygon")] bool ReturnPolygon = false
);
```

`SamMaskResult` — neue optional Property `PolygonPoints`:

```csharp
public sealed record SamMaskResult(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("bbox")] IReadOnlyList<double> Bbox,
    [property: JsonPropertyName("mask_rle")] string MaskRle,
    [property: JsonPropertyName("mask_area_pixels")] int MaskAreaPixels,
    [property: JsonPropertyName("image_area_pixels")] int ImageAreaPixels,
    [property: JsonPropertyName("polygon_points")] IReadOnlyList<IReadOnlyList<double>>? PolygonPoints = null
);
```

- [ ] **Step 4: Tests laufen lassen — gruen**

```powershell
dotnet test --filter "FullyQualifiedName~SamPolygonDtoTests" --nologo
```

Expected: 3 erfolgreich.

- [ ] **Step 5: Vollstaendiger Test-Lauf**

```powershell
dotnet test --nologo
```

Expected: keine Regression.

- [ ] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.Application/Ai/Pipeline/VisionPipelineDtos.cs tests/AuswertungPro.Next.Pipeline.Tests/SamPolygonDtoTests.cs
git commit -m "feat(dto): add ReturnPolygon + PolygonPoints to SAM DTOs"
```

---

### Task 9: Live-Test gegen echten Sidecar (opt-in)

**Files:**
- Modify: `tests/AuswertungPro.Next.Pipeline.Tests/SidecarLiveContractTests.cs`

- [ ] **Step 1: Live-Test ergaenzen**

In `SidecarLiveContractTests.cs` als neuer Test:

```csharp
[Fact]
[Trait("Category", "LiveSidecar")]
public async Task LiveSegmentSam_WithReturnPolygon_ReturnsPolygonPoints()
{
    if (!await SidecarReachableAsync())
    {
        _output.WriteLine($"SKIP: Sidecar nicht erreichbar auf {SidecarBaseUrl}");
        return;
    }

    var token = TryReadToken();
    if (token is null && !IsAuthDisabled())
    {
        _output.WriteLine("SKIP: kein Token, Auth aktiv");
        return;
    }

    // 100x100 schwarzes Test-Bild als Base64
    var img = new System.Drawing.Bitmap(100, 100);
    using var ms = new System.IO.MemoryStream();
    img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
    var b64 = System.Convert.ToBase64String(ms.ToArray());

    var client = new VisionPipelineClient(
        new System.Uri(SidecarBaseUrl),
        new System.Net.Http.HttpClient(),
        authToken: token);

    var box = new SamBoundingBox(10, 10, 90, 90, "test", 1.0);
    var request = new SamRequest(b64, new[] { box }, ReturnPolygon: true);

    var resp = await client.SegmentSamAsync(request, System.Threading.CancellationToken.None);

    Assert.NotNull(resp);
    _output.WriteLine($"Mask count: {resp.Masks?.Count ?? 0}");
    if (resp.Masks is { Count: > 0 })
    {
        var firstMask = resp.Masks[0];
        _output.WriteLine($"Polygon points: {firstMask.PolygonPoints?.Count ?? 0}");
    }
}
```

- [ ] **Step 2: Wenn Sidecar laeuft, Test ausfuehren**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj `
  -s tests/AuswertungPro.Next.Pipeline.Tests/.runsettings.live `
  --filter "Category=LiveSidecar&FullyQualifiedName~LiveSegmentSam_WithReturnPolygon" `
  --nologo
```

Expected: gruen, Output zeigt `Polygon points: > 0`.

- [ ] **Step 3: Commit**

```powershell
git add tests/AuswertungPro.Next.Pipeline.Tests/SidecarLiveContractTests.cs
git commit -m "test(sidecar): add live test for return_polygon flag"
```

---

## Phase 4 — Adapter-Implementierungen

### Task 10: TrainingSamplesWriterAdapter

> ⚠️ **Korrektur K1 (FindCommittedAsync)** — siehe Header. Method-Signatur
> hat zusaetzlich `double meter`-Parameter; LINQ-Filter nutzt
> `Math.Abs(s.MeterStart - meter) <= meterTolerance`. Test-Erwartung **2** statt
> 3 Treffer fuer `meter=10.0, tolerance=0.5` (10.0 + 10.3 sind drin, 12.0 nicht).

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Annotation/TrainingSamplesWriterAdapter.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/TrainingSamplesWriterAdapterTests.cs`

- [ ] **Step 1: Failing Test schreiben**

```csharp
// tests/AuswertungPro.Next.Infrastructure.Tests/TrainingSamplesWriterAdapterTests.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Annotation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TrainingSamplesWriterAdapterTests
{
    [Fact]
    public async Task AppendAsync_WritesSampleToStore()
    {
        // Test isolieren: KnowledgeRoot auf temp setzen
        var tempRoot = Path.Combine(Path.GetTempPath(), "tswa_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            // Adapter manuell konfigurieren (der Store nutzt KnowledgeRootProvider statisch)
            // → fuer den Test Env-Var setzen
            System.Environment.SetEnvironmentVariable("KI_BRAIN", tempRoot);

            var adapter = new TrainingSamplesWriterAdapter();
            var sample = new TrainingSample
            {
                SampleId = "test-1",
                CaseId = "case-x",
                Code = "BAB B",
                MeterStart = 12.3,
                MeterEnd = 12.3,
                Signature = TrainingSample.BuildCanonicalSignature("case-x", "BAB B", 12.3, 12.3, null),
                SourceType = SourceTypeNames.OperateurAnnotation
            };

            await adapter.AppendAsync(sample, CancellationToken.None);

            var found = await adapter.FindCommittedAsync(
                "case-x", SourceTypeNames.OperateurAnnotation, "BAB B", 0.5, CancellationToken.None);

            Assert.Single(found);
            Assert.Equal("test-1", found[0].SampleId);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task UpdateIndexStateAsync_UpdatesOnlyKbState()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "tswa_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            System.Environment.SetEnvironmentVariable("KI_BRAIN", tempRoot);

            var adapter = new TrainingSamplesWriterAdapter();
            var sample = new TrainingSample
            {
                SampleId = "test-2",
                CaseId = "case-y",
                Code = "BAC A",
                MeterStart = 18.5,
                MeterEnd = 18.5,
                Signature = TrainingSample.BuildCanonicalSignature("case-y", "BAC A", 18.5, 18.5, null),
                SourceType = SourceTypeNames.OperateurAnnotation,
                KbIndexState = KbIndexState.None
            };
            await adapter.AppendAsync(sample, CancellationToken.None);

            await adapter.UpdateIndexStateAsync("test-2", KbIndexState.Pending, CancellationToken.None);

            var found = await adapter.FindCommittedAsync(
                "case-y", SourceTypeNames.OperateurAnnotation, "BAC A", 0.5, CancellationToken.None);
            Assert.Single(found);
            Assert.Equal(KbIndexState.Pending, found[0].KbIndexState);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task FindCommittedAsync_FiltersByCaseSourceCodeAndMeterTolerance()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "tswa_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            System.Environment.SetEnvironmentVariable("KI_BRAIN", tempRoot);

            var adapter = new TrainingSamplesWriterAdapter();
            await adapter.AppendAsync(MakeSample("a", "case-z", "BAB B", 10.0), CancellationToken.None);
            await adapter.AppendAsync(MakeSample("b", "case-z", "BAB B", 10.3), CancellationToken.None);
            await adapter.AppendAsync(MakeSample("c", "case-z", "BAB B", 12.0), CancellationToken.None);

            var found = await adapter.FindCommittedAsync(
                "case-z", SourceTypeNames.OperateurAnnotation, "BAB B", 0.5, CancellationToken.None);

            // 10.0 + 10.3 fallen in die Toleranz, 12.0 nicht (ueber 0.5m vom letzten)
            // Test prueft also: alle drei werden gefunden, weil keine Anker-Meterstand-Filterung
            Assert.Equal(3, found.Count);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private static TrainingSample MakeSample(string id, string caseId, string code, double meter)
        => new()
        {
            SampleId = id,
            CaseId = caseId,
            Code = code,
            MeterStart = meter,
            MeterEnd = meter,
            Signature = TrainingSample.BuildCanonicalSignature(caseId, code, meter, meter, null),
            SourceType = SourceTypeNames.OperateurAnnotation
        };
}
```

- [ ] **Step 2: Build → Failed (Adapter fehlt)**

- [ ] **Step 3: Adapter implementieren**

```csharp
// src/AuswertungPro.Next.Infrastructure/Ai/Annotation/TrainingSamplesWriterAdapter.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai.Annotation;

/// <summary>
/// Adapter um den statischen <see cref="TrainingSamplesStore"/>.
/// Implementiert <see cref="ITrainingSamplesWriter"/> mit den
/// drei Slice-1-Methoden.
/// </summary>
public sealed class TrainingSamplesWriterAdapter : ITrainingSamplesWriter
{
    public async Task AppendAsync(TrainingSample sample, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await TrainingSamplesStore.MergeAndSaveAsync(new List<TrainingSample> { sample });
    }

    public async Task UpdateIndexStateAsync(string sampleId, KbIndexState state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var all = await TrainingSamplesStore.LoadAsync();
        var target = all.FirstOrDefault(s => string.Equals(s.SampleId, sampleId, StringComparison.OrdinalIgnoreCase));
        if (target is null) return;

        target.KbIndexState = state;
        await TrainingSamplesStore.SaveAsync(all);
    }

    public async Task<IReadOnlyList<TrainingSample>> FindCommittedAsync(
        string caseId,
        string sourceType,
        string code,
        double meterTolerance,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var all = await TrainingSamplesStore.LoadAsync();
        return all
            .Where(s => string.Equals(s.CaseId, caseId, StringComparison.OrdinalIgnoreCase))
            .Where(s => string.Equals(s.SourceType, sourceType, StringComparison.OrdinalIgnoreCase))
            .Where(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
```

- [ ] **Step 4: Tests laufen lassen — gruen**

```powershell
dotnet test --filter "FullyQualifiedName~TrainingSamplesWriterAdapterTests" --nologo
```

Expected: 3 erfolgreich.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Infrastructure/Ai/Annotation/TrainingSamplesWriterAdapter.cs tests/AuswertungPro.Next.Infrastructure.Tests/TrainingSamplesWriterAdapterTests.cs
git commit -m "feat(infra): add TrainingSamplesWriterAdapter"
```

---

### Task 11: KnowledgeBaseIndexerAdapter

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Annotation/KnowledgeBaseIndexerAdapter.cs`

- [ ] **Step 1: Adapter anlegen (delegiert an existierenden Manager)**

```csharp
// src/AuswertungPro.Next.Infrastructure/Ai/Annotation/KnowledgeBaseIndexerAdapter.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai.Annotation;

/// <summary>
/// Adapter um <see cref="KnowledgeBaseManager"/>. Indexiert ein
/// Sample (Embedding via nomic-embed-text + KB-Eintrag).
/// </summary>
public sealed class KnowledgeBaseIndexerAdapter : IKnowledgeBaseIndexer
{
    private readonly KnowledgeBaseManager _manager;

    public KnowledgeBaseIndexerAdapter(KnowledgeBaseManager manager)
    {
        _manager = manager;
    }

    public Task IndexSampleAsync(TrainingSample sample, CancellationToken ct)
        => _manager.IndexSampleAsync(sample, ct);
}
```

> **Hinweis:** Wenn `KnowledgeBaseManager.IndexSampleAsync` mit der genauen Signatur nicht existiert, vor dem Build pruefen welche Methode der Manager bietet (`IndexAsync`, `AddSampleAsync` etc.) und Adapter entsprechend anpassen. Vorgesehen: per Suche `KnowledgeBaseManager` im Repo den Methodennamen finden.

- [ ] **Step 2: Build pruefen**

```powershell
dotnet build src/AuswertungPro.Next.Infrastructure/AuswertungPro.Next.Infrastructure.csproj
```

Expected: 0 Fehler. Wenn `IndexSampleAsync` nicht existiert: Adapter auf den realen Method-Namen anpassen.

- [ ] **Step 3: Commit**

```powershell
git add src/AuswertungPro.Next.Infrastructure/Ai/Annotation/KnowledgeBaseIndexerAdapter.cs
git commit -m "feat(infra): add KnowledgeBaseIndexerAdapter"
```

---

### Task 12: YoloDatasetExportService.AppendSampleAsync (Single-Sample-Write)

> ⚠️ **Blocker B4 (YOLO-Pfad-Layout + Class-Map-Inkonsistenz)** — siehe Header.
> Reales Layout ist `{outputDir}/images/train/` und `{outputDir}/labels/train/`
> (nicht `train/images/`). Service hat heute **keinen** `_datasetRoot`-Field —
> entweder neuer Konstruktor mit injizierbarem Default oder zusaetzlicher
> `outputDir`-Parameter in `AppendSampleAsync`. Plus: `ExportAsync` baut
> `classMap` selbst aus den Sample-Codes; `AppendSampleAsync` muss eine
> kompatible `data.yaml` mit `VsaYoloClassMap`-Reihenfolge schreiben und die
> Inkonsistenz als Tech-Debt-Marker dokumentieren ("Slice 2: classMap
> harmonisieren"). Method-Signatur: `AppendSampleAsync(sample, preview, ct)`
> — Polygon kommt aus `preview.PolygonJson`.

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Training/YoloDatasetExportService.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/YoloDatasetAppendTests.cs`

- [ ] **Step 1: Failing Test**

```csharp
// tests/AuswertungPro.Next.Infrastructure.Tests/YoloDatasetAppendTests.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class YoloDatasetAppendTests
{
    [Fact]
    public async Task AppendSampleAsync_WritesPngAndTxtToImagesAndLabels()
    {
        var tempDataset = Path.Combine(Path.GetTempPath(), "yolo_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDataset);
        try
        {
            // Test-Frame anlegen
            var frameSrc = Path.Combine(tempDataset, "src-frame.png");
            using (var bmp = new System.Drawing.Bitmap(640, 480))
                bmp.Save(frameSrc, System.Drawing.Imaging.ImageFormat.Png);

            var sample = new TrainingSample
            {
                SampleId = "yolo-test-1",
                CaseId = "case-y",
                Code = "BAB B",
                MeterStart = 10.0,
                MeterEnd = 10.0,
                Signature = TrainingSample.BuildCanonicalSignature("case-y", "BAB B", 10.0, 10.0, null),
                SourceType = SourceTypeNames.OperateurAnnotation,
                FramePath = frameSrc,
                BboxXCenter = 0.5,
                BboxYCenter = 0.5,
                BboxWidth = 0.2,
                BboxHeight = 0.3,
                SamMaskRle = "fake",
                MaskWidth = 640,
                MaskHeight = 480,
                MaskAreaPixels = 100
            };

            // Service mit Test-Root konfigurieren
            var service = new YoloDatasetExportService(datasetRoot: tempDataset);

            var labelPath = await service.AppendSampleAsync(sample, CancellationToken.None);

            Assert.True(File.Exists(labelPath), $"Label-Datei nicht da: {labelPath}");
            var imagePath = Path.ChangeExtension(labelPath, ".png").Replace("labels", "images");
            Assert.True(File.Exists(imagePath), $"Bild-Datei nicht da: {imagePath}");

            // Label-Format pruefen: "<class_id> <points...>"
            var content = await File.ReadAllTextAsync(labelPath);
            Assert.False(string.IsNullOrWhiteSpace(content));
            var parts = content.Trim().Split(' ');
            Assert.True(int.TryParse(parts[0], out _), "Erstes Token muss class-id sein");
        }
        finally
        {
            try { Directory.Delete(tempDataset, recursive: true); } catch { }
        }
    }
}
```

- [ ] **Step 2: Test build → failed**

- [ ] **Step 3: AppendSampleAsync implementieren**

In `src/AuswertungPro.Next.Infrastructure/Ai/Training/YoloDatasetExportService.cs`:

```csharp
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Ai.Training;

// ... bestehende usings + class header bleiben ...

// ── Slice 1 (Operateur-Annotation): Single-Sample-Append ─────────
public async Task<string> AppendSampleAsync(TrainingSample sample, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();

    if (!VsaYoloClassMap.TryGetClassId(sample.Code, out var classId))
        throw new System.InvalidOperationException(
            $"VSA-Code '{sample.Code}' nicht in YOLO-Class-Map. " +
            "Service muss vorher TryGetClassId pruefen und ggf. skippen.");

    // train|val: einfache Heuristik fuer Slice 1 — alles nach train
    // (Split kann spaeter via separater Logik passieren)
    var subdir = "train";

    var imagesDir = System.IO.Path.Combine(_datasetRoot, subdir, "images");
    var labelsDir = System.IO.Path.Combine(_datasetRoot, subdir, "labels");
    System.IO.Directory.CreateDirectory(imagesDir);
    System.IO.Directory.CreateDirectory(labelsDir);

    // Frame.png nach images/
    var imagePath = System.IO.Path.Combine(imagesDir, $"{sample.SampleId}.png");
    if (!System.IO.File.Exists(imagePath))
        System.IO.File.Copy(sample.FramePath, imagePath);

    // Label nach labels/
    var labelPath = System.IO.Path.Combine(labelsDir, $"{sample.SampleId}.txt");

    // YOLO-seg-Format:
    //   <class_id> <x_center> <y_center> <w> <h> <p1x> <p1y> <p2x> <p2y> ...
    // Box-Felder im Domain-Sample sind bereits normalisiert.
    var labelLine = new System.Text.StringBuilder();
    labelLine.Append(classId).Append(' ');
    labelLine.Append(F(sample.BboxXCenter)).Append(' ');
    labelLine.Append(F(sample.BboxYCenter)).Append(' ');
    labelLine.Append(F(sample.BboxWidth)).Append(' ');
    labelLine.Append(F(sample.BboxHeight));

    // Polygon-Punkte aus PolygonJson — in Slice 1 wird das aus dem
    // MaskPreview befuellt (siehe Service). Falls nicht vorhanden:
    // Box-only-Label (YOLO-Det funktioniert auch ohne Polygon).
    // Da TrainingSample kein PolygonJson-Feld hat, fallen wir hier
    // auf Box-only zurueck. Polygon-Persistierung ist Slice-2-Thema.

    await System.IO.File.WriteAllTextAsync(labelPath, labelLine.ToString(), ct);
    return labelPath;

    static string F(double? v) => v.HasValue
        ? v.Value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)
        : "0.000000";
}
```

> **Wichtig:** Der bestehende `YoloDatasetExportService` hat heute keine konfigurierbare `_datasetRoot`-Variable. Vor diesem Step pruefen wie der Pfad heute kommt (Konstruktor / Constants / Settings) und entsprechend anpassen. Falls der Service einen anderen Konstruktor braucht, in Tests einen Test-Constructor mit Override-Pfad nutzen.

- [ ] **Step 4: Tests laufen lassen**

```powershell
dotnet test --filter "FullyQualifiedName~YoloDatasetAppendTests" --nologo
```

Expected: 1 erfolgreich.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Infrastructure/Ai/Training/YoloDatasetExportService.cs tests/AuswertungPro.Next.Infrastructure.Tests/YoloDatasetAppendTests.cs
git commit -m "feat(yolo): add AppendSampleAsync for single-sample write"
```

> **Hinweis:** Nach diesem Task `IYoloDatasetWriter` als Interface auf `YoloDatasetExportService` implementieren (nur Interface-Marker hinzufuegen, kein Code-Aenderung):
> ```csharp
> public class YoloDatasetExportService : IYoloDatasetWriter
> ```
> Das ist Teil dieses Commits.

---

## Phase 5 — Infrastructure Service

### Task 13: OperateurAnnotationService Skeleton + PreviewMaskAsync

> ⚠️ **Korrektur K5 (Test-Stub-Signaturen)** — siehe Header.
> `new SamResponse(masks, 12.3)` aus dem Plan-Code passt **nicht** zur echten
> Signatur. Korrekt: `SamResponse(Masks, ImageWidth, ImageHeight,
> InferenceTimeMs)` (4 Args). `SamMaskResult`-Konstruktor hat 10 Pflicht-Felder
> + optional `PolygonPoints` (siehe B2). Bei jedem Stub-Bau die echten Felder
> setzen.

**Files:**
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Annotation/OperateurAnnotationService.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationServicePreviewTests.cs`

- [ ] **Step 1: Failing Test fuer PreviewMaskAsync**

```csharp
// tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationServicePreviewTests.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Annotation;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class OperateurAnnotationServicePreviewTests
{
    private static AnnotationRequest MakeRequest(string framePath)
        => new("case-1", "BAB B", 12.3, 145.0, 145.0, 3742, framePath, 640, 480,
               new BoundingBoxNormalized(0.5, 0.5, 0.2, 0.3));

    private static string MakeFakeFrame()
    {
        var path = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid():N}.png");
        using var bmp = new System.Drawing.Bitmap(640, 480);
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    [Fact]
    public async Task PreviewMaskAsync_SidecarReturnsMask_PreviewContainsPolygonAndConfidence()
    {
        var frame = MakeFakeFrame();
        try
        {
            var stub = new SamStubHandler(samMasks: new[]
            {
                new SamMaskResult("BAB B", 0.85, new[] {0.0, 0.0, 1.0, 1.0}, "rle-data", 1234, 307200,
                                  new[] { (IReadOnlyList<double>)new[] {1.5, 2.5}, new[] {3.5, 4.5} })
            });

            var client = new VisionPipelineClient(new Uri("http://localhost:8100"), new HttpClient(stub));
            var service = new OperateurAnnotationService(
                client,
                writer: NullWriter.Instance,
                indexer: NullIndexer.Instance,
                yolo: NullYolo.Instance);

            var preview = await service.PreviewMaskAsync(MakeRequest(frame), CancellationToken.None);

            Assert.Equal("rle-data", preview.SamMaskRle);
            Assert.Equal(0.85, preview.SamConfidence);
            Assert.Equal(1234, preview.MaskAreaPixels);
            Assert.NotNull(preview.PolygonJson);
            Assert.Contains("1.5", preview.PolygonJson);
        }
        finally
        {
            File.Delete(frame);
        }
    }

    [Fact]
    public async Task PreviewMaskAsync_LowConfidence_AddsWarning()
    {
        var frame = MakeFakeFrame();
        try
        {
            var stub = new SamStubHandler(samMasks: new[]
            {
                new SamMaskResult("BAB B", 0.15, new[] {0.0, 0.0, 1.0, 1.0}, "rle", 100, 307200, null)
            });
            var client = new VisionPipelineClient(new Uri("http://localhost:8100"), new HttpClient(stub));
            var service = new OperateurAnnotationService(
                client, NullWriter.Instance, NullIndexer.Instance, NullYolo.Instance);

            var preview = await service.PreviewMaskAsync(MakeRequest(frame), CancellationToken.None);

            Assert.NotNull(preview.Warnings);
            Assert.Contains("LowSamConfidence", preview.Warnings!);
        }
        finally
        {
            File.Delete(frame);
        }
    }

    [Fact]
    public async Task PreviewMaskAsync_SidecarReturns503_Throws()
    {
        var frame = MakeFakeFrame();
        try
        {
            var stub = new SamStubHandler(statusCode: HttpStatusCode.ServiceUnavailable);
            var client = new VisionPipelineClient(new Uri("http://localhost:8100"), new HttpClient(stub));
            var service = new OperateurAnnotationService(
                client, NullWriter.Instance, NullIndexer.Instance, NullYolo.Instance);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                service.PreviewMaskAsync(MakeRequest(frame), CancellationToken.None));
        }
        finally
        {
            File.Delete(frame);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────
    private sealed class SamStubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly IReadOnlyList<SamMaskResult>? _masks;

        public SamStubHandler(HttpStatusCode statusCode = HttpStatusCode.OK,
                              IReadOnlyList<SamMaskResult>? samMasks = null)
        {
            _status = statusCode;
            _masks = samMasks;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            if (_status != HttpStatusCode.OK)
                return Task.FromResult(new HttpResponseMessage(_status));

            var body = new SamResponse(_masks ?? Array.Empty<SamMaskResult>(), 12.3);
            var json = System.Text.Json.JsonSerializer.Serialize(body, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    // Minimal-No-Op-Implementations fuer Adapter (PreviewMaskAsync ruft sie nicht auf)
    private sealed class NullWriter : ITrainingSamplesWriter
    {
        public static readonly NullWriter Instance = new();
        public Task AppendAsync(TrainingSample s, CancellationToken c) => Task.CompletedTask;
        public Task UpdateIndexStateAsync(string id, KbIndexState st, CancellationToken c) => Task.CompletedTask;
        public Task<IReadOnlyList<TrainingSample>> FindCommittedAsync(string a, string b, string c, double d, CancellationToken e)
            => Task.FromResult<IReadOnlyList<TrainingSample>>(Array.Empty<TrainingSample>());
    }
    private sealed class NullIndexer : IKnowledgeBaseIndexer
    {
        public static readonly NullIndexer Instance = new();
        public Task IndexSampleAsync(TrainingSample s, CancellationToken c) => Task.CompletedTask;
    }
    private sealed class NullYolo : IYoloDatasetWriter
    {
        public static readonly NullYolo Instance = new();
        public Task<string> AppendSampleAsync(TrainingSample s, CancellationToken c) => Task.FromResult("");
    }
}
```

- [ ] **Step 2: Build → failed**

- [ ] **Step 3: Service-Skeleton mit PreviewMaskAsync**

```csharp
// src/AuswertungPro.Next.Infrastructure/Ai/Annotation/OperateurAnnotationService.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai.Annotation;

public sealed class OperateurAnnotationService : IOperateurAnnotationService
{
    private readonly VisionPipelineClient _client;
    private readonly ITrainingSamplesWriter _writer;
    private readonly IKnowledgeBaseIndexer _indexer;
    private readonly IYoloDatasetWriter _yolo;

    public OperateurAnnotationService(
        VisionPipelineClient client,
        ITrainingSamplesWriter writer,
        IKnowledgeBaseIndexer indexer,
        IYoloDatasetWriter yolo)
    {
        _client = client;
        _writer = writer;
        _indexer = indexer;
        _yolo = yolo;
    }

    public async Task<MaskPreview> PreviewMaskAsync(AnnotationRequest request, CancellationToken ct)
    {
        // Frame als Base64
        var imageBytes = await File.ReadAllBytesAsync(request.FramePath, ct);
        var b64 = Convert.ToBase64String(imageBytes);

        // Box: normalisiert (0..1) → SAM erwartet Pixel-Koordinaten
        var x1 = (request.Box.XCenter - request.Box.Width / 2) * request.FrameWidth;
        var y1 = (request.Box.YCenter - request.Box.Height / 2) * request.FrameHeight;
        var x2 = (request.Box.XCenter + request.Box.Width / 2) * request.FrameWidth;
        var y2 = (request.Box.YCenter + request.Box.Height / 2) * request.FrameHeight;

        var samBox = new SamBoundingBox(x1, y1, x2, y2, request.Code, 1.0);
        var samReq = new SamRequest(b64, new[] { samBox }, ReturnPolygon: true);

        var sw = Stopwatch.StartNew();
        var resp = await _client.SegmentSamAsync(samReq, ct);
        sw.Stop();

        if (resp.Masks is null || resp.Masks.Count == 0)
            throw new InvalidOperationException("SAM lieferte keine Maske");

        var mask = resp.Masks[0];
        var polygonJson = mask.PolygonPoints != null
            ? JsonSerializer.Serialize(mask.PolygonPoints)
            : "[]";

        var warnings = new List<string>();
        if (mask.Confidence < 0.3) warnings.Add("LowSamConfidence");
        if (mask.MaskAreaPixels < 100) warnings.Add("MaskTooSmall");

        return new MaskPreview(
            SamMaskRle: mask.MaskRle,
            SamMaskEncoding: "sidecar-sam-rle-v1",
            PolygonJson: polygonJson,
            MaskWidth: request.FrameWidth,
            MaskHeight: request.FrameHeight,
            MaskAreaPixels: mask.MaskAreaPixels,
            SamConfidence: mask.Confidence,
            SamLatency: sw.Elapsed,
            Warnings: warnings.Count > 0 ? warnings : null);
    }

    public Task<CommitResult> CommitAsync(AnnotationRequest request, MaskPreview preview, CancellationToken ct)
    {
        // Slice 1, Task 14+15
        throw new NotImplementedException("CommitAsync — Task 14/15");
    }
}
```

- [ ] **Step 4: Tests laufen lassen**

```powershell
dotnet test --filter "FullyQualifiedName~OperateurAnnotationServicePreviewTests" --nologo
```

Expected: 3 erfolgreich.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Infrastructure/Ai/Annotation/OperateurAnnotationService.cs tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationServicePreviewTests.cs
git commit -m "feat(annotation): add OperateurAnnotationService with PreviewMaskAsync"
```

---

### Task 14: CommitAsync Happy Path

> ⚠️ **Korrekturen K2 + K3 + K4** — siehe Header.
> - **K2 (KbIndexed bei Erfolg)**: nach `IndexSampleAsync`-Erfolg
>   `UpdateIndexStateAsync(..., KbIndexState.Indexed, ct)` aufrufen — sonst
>   bleibt das Sample dauerhaft auf `None`.
> - **K3 (Temp-Frame finalisieren)**: vor dem Store-Append den temp-Frame
>   nach `%KI_BRAIN%/frames/<CaseId>/<SampleId>.png` kopieren und
>   `sample.FramePath` auf den finalen Pfad setzen.
> - **K4 (OperationCanceledException nicht schlucken)**: in allen drei Bloecken
>   (Store, YOLO, KB) Cancellation per `catch (OperationCanceledException) { throw; }`
>   durchreichen — niemals als Warning maskieren.
> - **B3 (Polygon an Writer)**: `IYoloDatasetWriter.AppendSampleAsync(sample,
>   preview, ct)` mit `MaskPreview` aus dem Cache aufrufen.
> Genaue Code-Bloecke stehen im Header.

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Annotation/OperateurAnnotationService.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationServiceCommitTests.cs`

- [ ] **Step 1: Test fuer Happy Path**

```csharp
// tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationServiceCommitTests.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Annotation;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class OperateurAnnotationServiceCommitTests
{
    private static AnnotationRequest MakeRequest(string framePath, string code = "BAB B")
        => new("case-1", code, 12.3, 145.0, 149.7, 3742, framePath, 640, 480,
               new BoundingBoxNormalized(0.5, 0.5, 0.2, 0.3));

    private static MaskPreview MakePreview()
        => new("rle", "sidecar-sam-rle-v1", "[[1,2],[3,4]]", 640, 480, 1500, 0.85,
               TimeSpan.FromMilliseconds(300), null);

    private static string MakeFrame()
    {
        var path = Path.Combine(Path.GetTempPath(), $"f_{Guid.NewGuid():N}.png");
        using var bmp = new System.Drawing.Bitmap(640, 480);
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    [Fact]
    public async Task CommitAsync_AllAdaptersSucceed_IsSuccessTrue()
    {
        var frame = MakeFrame();
        try
        {
            var w = new RecordingWriter();
            var i = new RecordingIndexer();
            var y = new RecordingYolo("yolo-label.txt");

            var service = new OperateurAnnotationService(
                MakeClient(), w, i, y);
            var request = MakeRequest(frame);
            var preview = MakePreview();

            var result = await service.CommitAsync(request, preview, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(result.StorePersisted);
            Assert.True(result.YoloWritten);
            Assert.True(result.KbIndexed);
            Assert.Equal(1, w.Appends);
            Assert.Equal(1, y.Appends);
            Assert.Equal(1, i.Indexes);

            // Sample wurde mit allen Mask-/Box-/SourceType-Feldern angelegt
            Assert.NotNull(w.LastSample);
            Assert.Equal("rle", w.LastSample!.SamMaskRle);
            Assert.Equal(SourceTypeNames.OperateurAnnotation, w.LastSample.SourceType);
            Assert.Equal(0.5, w.LastSample.BboxXCenter);
            // FrameDelta = Actual - Suggested = 149.7 - 145.0 = 4.7
            Assert.Equal(4.7, w.LastSample.FrameDeltaSeconds!.Value, precision: 1);
        }
        finally
        {
            File.Delete(frame);
        }
    }

    private static VisionPipelineClient MakeClient()
        => new(new Uri("http://localhost:9999"), new HttpClient());

    // ── Recording Test-Doubles ──────────────────────────────────────
    public sealed class RecordingWriter : ITrainingSamplesWriter
    {
        public int Appends;
        public int Updates;
        public TrainingSample? LastSample;
        public List<KbIndexState> StateUpdates = new();

        public Task AppendAsync(TrainingSample s, CancellationToken ct)
        {
            Appends++; LastSample = s; return Task.CompletedTask;
        }
        public Task UpdateIndexStateAsync(string id, KbIndexState st, CancellationToken ct)
        {
            Updates++; StateUpdates.Add(st); return Task.CompletedTask;
        }
        public Task<IReadOnlyList<TrainingSample>> FindCommittedAsync(string a, string b, string c, double d, CancellationToken e)
            => Task.FromResult<IReadOnlyList<TrainingSample>>(Array.Empty<TrainingSample>());
    }
    public sealed class RecordingIndexer : IKnowledgeBaseIndexer
    {
        public int Indexes;
        public Task IndexSampleAsync(TrainingSample s, CancellationToken ct)
        { Indexes++; return Task.CompletedTask; }
    }
    public sealed class RecordingYolo : IYoloDatasetWriter
    {
        public int Appends;
        private readonly string _labelPath;
        public RecordingYolo(string labelPath) { _labelPath = labelPath; }
        public Task<string> AppendSampleAsync(TrainingSample s, CancellationToken ct)
        { Appends++; return Task.FromResult(_labelPath); }
    }
}
```

- [ ] **Step 2: Test build → failed**

- [ ] **Step 3: CommitAsync implementieren (Happy Path)**

In `OperateurAnnotationService.cs`, `CommitAsync`-Body ersetzen:

```csharp
public async Task<CommitResult> CommitAsync(AnnotationRequest request, MaskPreview preview, CancellationToken ct)
{
    var warnings = new List<string>();
    var sampleId = Guid.NewGuid().ToString("N").Substring(0, 12);
    var frameDelta = request.ActualFrameTimeSeconds - request.SuggestedFrameTimeSeconds;

    var sample = new Domain.Ai.Training.TrainingSample
    {
        SampleId = sampleId,
        CaseId = request.CaseId,
        Code = request.Code,
        MeterStart = request.ProtocolMeterstand,
        MeterEnd = request.ProtocolMeterstand,
        TimeSeconds = request.ActualFrameTimeSeconds,
        DetectedMeter = request.ProtocolMeterstand,
        FramePath = request.FramePath,
        FrameIndex = request.VideoFrameIndex,
        Status = Domain.Ai.Training.TrainingSampleStatus.Approved,
        SourceType = Domain.Ai.Training.SourceTypeNames.OperateurAnnotation,
        Signature = Domain.Ai.Training.TrainingSample.BuildCanonicalSignature(
            request.CaseId, request.Code,
            request.ProtocolMeterstand, request.ProtocolMeterstand, null),
        // Box
        BboxXCenter = request.Box.XCenter,
        BboxYCenter = request.Box.YCenter,
        BboxWidth = request.Box.Width,
        BboxHeight = request.Box.Height,
        // Mask
        SamMaskRle = preview.SamMaskRle,
        SamMaskEncoding = preview.SamMaskEncoding,
        MaskWidth = preview.MaskWidth,
        MaskHeight = preview.MaskHeight,
        MaskAreaPixels = preview.MaskAreaPixels,
        SamConfidence = preview.SamConfidence,
        // Drift
        FrameDeltaSeconds = frameDelta,
        // KB-Init
        KbIndexState = Domain.Ai.Training.KbIndexState.None
    };

    // 1. Store
    bool storePersisted = false;
    string? error = null;
    try
    {
        await _writer.AppendAsync(sample, ct);
        storePersisted = true;
    }
    catch (Exception ex)
    {
        error = $"Store-Append fehlgeschlagen: {ex.Message}";
        return new CommitResult(
            IsSuccess: false, SampleId: sampleId,
            FramePath: null, LabelPath: null,
            StorePersisted: false, KbIndexed: false, YoloWritten: false,
            Error: error, Warnings: warnings.Count > 0 ? warnings : null);
    }

    // 2. YOLO (best-effort)
    string? labelPath = null;
    bool yoloWritten = false;
    try
    {
        if (!Application.Ai.Teacher.VsaYoloClassMap.TryGetClassId(request.Code, out _))
        {
            warnings.Add("UnknownYoloClass");
        }
        else
        {
            labelPath = await _yolo.AppendSampleAsync(sample, ct);
            yoloWritten = true;
        }
    }
    catch (Exception ex)
    {
        warnings.Add($"YoloFailed:{ex.Message}");
    }

    // 3. KB (best-effort)
    bool kbIndexed = false;
    try
    {
        await _indexer.IndexSampleAsync(sample, ct);
        kbIndexed = true;
    }
    catch (Exception ex)
    {
        warnings.Add($"KbFailed:{ex.Message}");
        try
        {
            await _writer.UpdateIndexStateAsync(sampleId, Domain.Ai.Training.KbIndexState.Pending, ct);
        }
        catch
        {
            warnings.Add("KbStateUpdateFailed");
        }
    }

    return new CommitResult(
        IsSuccess: storePersisted,
        SampleId: sampleId,
        FramePath: sample.FramePath,
        LabelPath: labelPath,
        StorePersisted: storePersisted,
        KbIndexed: kbIndexed,
        YoloWritten: yoloWritten,
        Error: error,
        Warnings: warnings.Count > 0 ? warnings : null);
}
```

- [ ] **Step 4: Tests gruen**

```powershell
dotnet test --filter "FullyQualifiedName~OperateurAnnotationServiceCommitTests" --nologo
```

Expected: 1 erfolgreich.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.Infrastructure/Ai/Annotation/OperateurAnnotationService.cs tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationServiceCommitTests.cs
git commit -m "feat(annotation): implement CommitAsync happy path"
```

---

### Task 15: CommitAsync Error-Pfade

> ⚠️ **Korrektur K3 (OCE)** — siehe Header. Zusaetzlicher Test noetig:
> `CommitAsync_StoreCancelled_RethrowsOCE` — der Token wird zwischen Step 0
> (Frame-Finalize) und Step 1 (Store-Append) gecancelt; erwartet ist
> `OperationCanceledException` (kein `result.Warnings`-Eintrag, keine
> Maskierung). Gleicher Test-Typ optional auch fuer YOLO-/KB-Block.

**Files:**
- Modify: `tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationServiceCommitTests.cs`

- [ ] **Step 1: Tests fuer Error-Pfade ergaenzen**

```csharp
[Fact]
public async Task CommitAsync_StoreFails_IsSuccessFalse_NoYoloNoKb()
{
    var frame = MakeFrame();
    try
    {
        var w = new ThrowingWriter();
        var i = new RecordingIndexer();
        var y = new RecordingYolo("");

        var service = new OperateurAnnotationService(MakeClient(), w, i, y);

        var result = await service.CommitAsync(MakeRequest(frame), MakePreview(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.StorePersisted);
        Assert.False(result.YoloWritten);
        Assert.False(result.KbIndexed);
        Assert.NotNull(result.Error);
        Assert.Equal(0, y.Appends);
        Assert.Equal(0, i.Indexes);
    }
    finally { File.Delete(frame); }
}

[Fact]
public async Task CommitAsync_YoloFails_StoreOk_KbStillTried()
{
    var frame = MakeFrame();
    try
    {
        var w = new RecordingWriter();
        var i = new RecordingIndexer();
        var y = new ThrowingYolo();

        var service = new OperateurAnnotationService(MakeClient(), w, i, y);
        var result = await service.CommitAsync(MakeRequest(frame), MakePreview(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.StorePersisted);
        Assert.False(result.YoloWritten);
        Assert.True(result.KbIndexed);
        Assert.Contains(result.Warnings ?? new List<string>(), w => w.StartsWith("YoloFailed"));
    }
    finally { File.Delete(frame); }
}

[Fact]
public async Task CommitAsync_KbFails_StoreUpdateIndexStatePending()
{
    var frame = MakeFrame();
    try
    {
        var w = new RecordingWriter();
        var i = new ThrowingIndexer();
        var y = new RecordingYolo("yolo.txt");

        var service = new OperateurAnnotationService(MakeClient(), w, i, y);
        var result = await service.CommitAsync(MakeRequest(frame), MakePreview(), CancellationToken.None);

        Assert.True(result.StorePersisted);
        Assert.True(result.YoloWritten);
        Assert.False(result.KbIndexed);
        Assert.Equal(1, w.Updates);
        Assert.Contains(KbIndexState.Pending, w.StateUpdates);
    }
    finally { File.Delete(frame); }
}

[Fact]
public async Task CommitAsync_UnknownYoloClass_SkipYolo_StoreAndKbStillRun()
{
    var frame = MakeFrame();
    try
    {
        var w = new RecordingWriter();
        var i = new RecordingIndexer();
        var y = new RecordingYolo("");

        var service = new OperateurAnnotationService(MakeClient(), w, i, y);
        // Code "XYZ Q" ist garantiert nicht in der VsaYoloClassMap
        var result = await service.CommitAsync(MakeRequest(frame, "XYZ Q"), MakePreview(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.StorePersisted);
        Assert.False(result.YoloWritten);
        Assert.True(result.KbIndexed);
        Assert.Equal(0, y.Appends);
        Assert.Contains("UnknownYoloClass", result.Warnings ?? new List<string>());
    }
    finally { File.Delete(frame); }
}

private sealed class ThrowingWriter : ITrainingSamplesWriter
{
    public Task AppendAsync(TrainingSample s, CancellationToken ct)
        => throw new InvalidOperationException("Disk full");
    public Task UpdateIndexStateAsync(string id, KbIndexState st, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<TrainingSample>> FindCommittedAsync(string a, string b, string c, double d, CancellationToken e)
        => Task.FromResult<IReadOnlyList<TrainingSample>>(Array.Empty<TrainingSample>());
}
private sealed class ThrowingIndexer : IKnowledgeBaseIndexer
{
    public Task IndexSampleAsync(TrainingSample s, CancellationToken ct)
        => throw new InvalidOperationException("Ollama down");
}
private sealed class ThrowingYolo : IYoloDatasetWriter
{
    public Task<string> AppendSampleAsync(TrainingSample s, CancellationToken ct)
        => throw new InvalidOperationException("YOLO disk not mounted");
}
```

- [ ] **Step 2: Tests laufen lassen — alle 5 sollten gruen sein**

```powershell
dotnet test --filter "FullyQualifiedName~OperateurAnnotationServiceCommitTests" --nologo
```

Expected: 5 erfolgreich (1 Happy + 4 Error).

- [ ] **Step 3: Commit**

```powershell
git add tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationServiceCommitTests.cs
git commit -m "test(annotation): cover commit error paths"
```

---

## Phase 6 — UI Partial (Code-Liste + Hotkeys)

> **Hinweis**: Phase 6+7 sind UI-spezifisch und WPF-fummelig. Die Steps zeigen die Struktur. Beim Implementieren das bestehende `PlayerWindow.TrainingMode.cs` als Vorlage nehmen — gleicher Snapshot-Pfad, gleiches Overlay-Pattern.

### Task 16: ViewModel-Erweiterung fuer OperateurAnnotationSession

> ⚠️ **Blocker B5 (kein PlayerWindowViewModel)** — siehe Header.
> Task 16 in der urspruenglichen Form **entfaellt**: `PlayerWindow` ist eine
> partial class ueber 21 Dateien, **es gibt kein Window-VM**. Stattdessen:
> direkte Felder (`_operatorSession`, `_operatorActive`) im neuen Partial
> `PlayerWindow.OperateurAnnotation.cs` plus Code-Behind-Bindings gegen
> `x:Name`-Elemente (`OperatorCodeList.ItemsSource = _operatorSession?.Tasks`).
> Step 1 unten ("class PlayerWindowViewModel finden") wird **kein** Ergebnis
> liefern — ist Erwartung. Step 2 wird zu "Field-Initialisierung im Partial".

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Windows/PlayerWindowViewModel.cs` (oder TrainingMode-spezifisches VM falls separat)
- Test: minimal — Session-Property + Active-Property mit INotifyPropertyChanged

- [ ] **Step 1: Bestehendes PlayerWindow-VM finden**

```powershell
grep -n "class PlayerWindowViewModel" src/AuswertungPro.Next.UI/ViewModels/Windows/*.cs
```

- [ ] **Step 2: Session-Property + Active-Property ergaenzen**

```csharp
// In PlayerWindowViewModel.cs (oder dediziertem TrainingMode-VM)
private OperateurAnnotationSession? _operatorSession;
public OperateurAnnotationSession? OperatorSession
{
    get => _operatorSession;
    set => SetProperty(ref _operatorSession, value);
}

private CodeTask? _operatorActiveTask;
public CodeTask? OperatorActiveTask
{
    get => _operatorActiveTask;
    set => SetProperty(ref _operatorActiveTask, value);
}
```

(`SetProperty` ist die ObservableObject-Methode des bestehenden VMs.)

- [ ] **Step 3: Build pruefen**

```powershell
dotnet build
```

Expected: 0 Fehler.

- [ ] **Step 4: Commit**

```powershell
git add src/AuswertungPro.Next.UI/ViewModels/Windows/PlayerWindowViewModel.cs
git commit -m "feat(ui-vm): add operator annotation session properties"
```

---

### Task 17: PlayerWindow.OperateurAnnotation.cs Skeleton

**Files:**
- Create: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs`

- [ ] **Step 1: Skeleton-Partial anlegen**

```csharp
// src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Operateur-Annotations-Submodus im TrainingMode (Slice 1).
/// Eigene Felder/Controls (_operator...), keine Vermischung mit der
/// bestehenden TeacherAnnotation-Logik.
/// </summary>
public partial class PlayerWindow
{
    // ── Submode-State ─────────────────────────────────────────────
    private bool _operatorAnnotationActive;
    private CancellationTokenSource? _operatorPreviewCts;
    private string? _operatorTempFramePath;

    /// <summary>
    /// Aktiviert den Operateur-Annotation-Submodus.
    /// </summary>
    private void EnterOperatorAnnotationMode()
    {
        _operatorAnnotationActive = true;
        // UI-Controls einblenden, andere Submodi ausblenden
        // (Implementierung in Task 18)
    }

    private void ExitOperatorAnnotationMode()
    {
        _operatorAnnotationActive = false;
        _operatorPreviewCts?.Cancel();
        CleanupOperatorTempFrame();
    }

    private void CleanupOperatorTempFrame()
    {
        if (_operatorTempFramePath is not null && System.IO.File.Exists(_operatorTempFramePath))
        {
            try { System.IO.File.Delete(_operatorTempFramePath); } catch { }
            _operatorTempFramePath = null;
        }
    }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build
```

Expected: 0 Fehler.

- [ ] **Step 3: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs
git commit -m "feat(ui): add PlayerWindow.OperateurAnnotation partial skeleton"
```

---

### Task 18: Code-Liste UI mit Status-Dots + Import-Button

> ⚠️ **Blocker B5 (kein VM)** — siehe Header. XAML-Bindings wie
> `ItemsSource="{Binding OperatorSession.Tasks}"` funktionieren **nicht**, weil
> `PlayerWindow.DataContext` nicht auf ein VM mit dieser Property zeigt.
> Stattdessen Code-Behind im Partial:
> `OperatorCodeList.ItemsSource = _operatorSession?.Tasks;` direkt nach
> Session-Initialisierung. Selektion per `SelectionChanged`-Handler statt
> Two-Way-Binding. `IsEnabled`/`Visibility` werden ebenfalls per Code-Behind
> gesteuert (z.B. `OperatorImportButton.IsEnabled = ...`).

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` (oder TrainingMode-Tab)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs`

- [ ] **Step 1: XAML-Block fuer rechte Code-Liste hinzufuegen**

In `PlayerWindow.xaml`, im rechten Side-Panel im TrainingMode-Bereich:

```xml
<StackPanel x:Name="OperatorPanel" Visibility="Collapsed" Margin="8">
    <TextBlock Text="Operateur-Annotation" FontWeight="Bold" Margin="0,0,0,8"/>

    <Button x:Name="OperatorImportButton"
            Content="Haltungsordner importieren..."
            Click="OperatorImportButton_Click"
            Margin="0,0,0,8"/>

    <ListBox x:Name="OperatorCodeList"
             ItemsSource="{Binding OperatorSession.Tasks}"
             SelectedItem="{Binding OperatorActiveTask, Mode=TwoWay}"
             Height="280">
        <ListBox.ItemTemplate>
            <DataTemplate>
                <StackPanel Orientation="Horizontal" Margin="2">
                    <Ellipse Width="10" Height="10" Margin="0,0,8,0">
                        <Ellipse.Style>
                            <Style TargetType="Ellipse">
                                <Setter Property="Fill" Value="Gray"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding State}" Value="Active">
                                        <Setter Property="Fill" Value="DodgerBlue"/>
                                    </DataTrigger>
                                    <DataTrigger Binding="{Binding State}" Value="PreviewReady">
                                        <Setter Property="Fill" Value="Orange"/>
                                    </DataTrigger>
                                    <DataTrigger Binding="{Binding State}" Value="Committed">
                                        <Setter Property="Fill" Value="ForestGreen"/>
                                    </DataTrigger>
                                    <DataTrigger Binding="{Binding State}" Value="Skipped">
                                        <Setter Property="Fill" Value="Goldenrod"/>
                                    </DataTrigger>
                                    <DataTrigger Binding="{Binding State}" Value="Rejected">
                                        <Setter Property="Fill" Value="Crimson"/>
                                    </DataTrigger>
                                    <DataTrigger Binding="{Binding State}" Value="Error">
                                        <Setter Property="Fill" Value="DarkRed"/>
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </Ellipse.Style>
                    </Ellipse>
                    <TextBlock Text="{Binding Code}" Width="60" FontWeight="SemiBold"/>
                    <TextBlock Text="{Binding Meterstand, StringFormat={}{0:F2} m}"/>
                </StackPanel>
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>

    <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
        <Button x:Name="OperatorConfirmButton" Content="Bestätigen (Enter)"
                Click="OperatorConfirmButton_Click" IsEnabled="False" Margin="0,0,4,0"/>
        <Button x:Name="OperatorSkipButton" Content="Skip (S)"
                Click="OperatorSkipButton_Click" Margin="0,0,4,0"/>
        <Button x:Name="OperatorRejectButton" Content="Protokollfehler (R)"
                Click="OperatorRejectButton_Click"/>
    </StackPanel>
</StackPanel>
```

- [ ] **Step 2: Click-Handler im OperateurAnnotation-Partial**

```csharp
private void OperatorImportButton_Click(object sender, System.Windows.RoutedEventArgs e)
{
    var dlg = new System.Windows.Forms.FolderBrowserDialog
    {
        Description = "Haltungsordner waehlen (Video + PDF)"
    };
    if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

    LoadHaltungsordnerAsync(dlg.SelectedPath);
}

private async void LoadHaltungsordnerAsync(string folder)
{
    // Codes aus PDF extrahieren via TrainingCenterImportService oder
    // PdfProtocolTableParser. Sample-Code:
    var session = new OperateurAnnotationSession
    {
        CaseId = System.IO.Path.GetFileName(folder),
        VideoPath = FindFirstVideo(folder),
        PdfPath = FindFirstPdf(folder)
    };

    var codes = await ExtractCodesFromPdfAsync(session.PdfPath);
    foreach (var c in codes)
    {
        session.Tasks.Add(new CodeTask { Code = c.Code, Meterstand = c.Meter });
    }

    // Wiederholt-Import: bekannte Codes als Committed markieren (Task 25)
    await MarkAlreadyCommittedAsync(session);

    if (DataContext is PlayerWindowViewModel vm)
        vm.OperatorSession = session;
    EnterOperatorAnnotationMode();
}

private string FindFirstVideo(string folder)
{
    foreach (var ext in new[] { "*.mp4", "*.mpg", "*.mp2", "*.avi" })
    {
        var f = System.IO.Directory.GetFiles(folder, ext);
        if (f.Length > 0) return f[0];
    }
    return "";
}

private string FindFirstPdf(string folder)
{
    var f = System.IO.Directory.GetFiles(folder, "*.pdf");
    return f.Length > 0 ? f[0] : "";
}

private Task<System.Collections.Generic.List<(string Code, double Meter)>> ExtractCodesFromPdfAsync(string pdfPath)
{
    // Slice 1: nutze bestehenden Service.
    // TrainingCenterImportService oder PdfProtocolTableParser.
    // Reihenfolge: try TrainingCenter, fallback auf PdfProtocolTableParser.
    // Implementierungs-Detail: vor diesem Step im Repo grep'en welcher Service
    // die "Codes-aus-Protokoll"-Extraktion macht, dann hier injizieren.
    return Task.FromResult(new System.Collections.Generic.List<(string, double)>());
}

private Task MarkAlreadyCommittedAsync(OperateurAnnotationSession session)
{
    // Task 25 — wird dort vollstaendig implementiert
    return Task.CompletedTask;
}

private void OperatorSkipButton_Click(object sender, System.Windows.RoutedEventArgs e)
{
    if (DataContext is not PlayerWindowViewModel vm || vm.OperatorActiveTask is null) return;
    vm.OperatorActiveTask.State = CodeTaskState.Skipped;
    AdvanceToNextPending();
}

private void OperatorRejectButton_Click(object sender, System.Windows.RoutedEventArgs e)
{
    if (DataContext is not PlayerWindowViewModel vm || vm.OperatorActiveTask is null) return;
    vm.OperatorActiveTask.State = CodeTaskState.Rejected;
    AdvanceToNextPending();
}

private void OperatorConfirmButton_Click(object sender, System.Windows.RoutedEventArgs e)
{
    // Task 23 — Confirm-Logik
}

private void AdvanceToNextPending()
{
    if (DataContext is not PlayerWindowViewModel vm || vm.OperatorSession is null) return;
    foreach (var t in vm.OperatorSession.Tasks)
    {
        if (t.State == CodeTaskState.Pending)
        {
            vm.OperatorActiveTask = t;
            return;
        }
    }
}
```

- [ ] **Step 3: Build + Smoke-Test**

```powershell
dotnet build
```

Expected: 0 Fehler.

- [ ] **Step 4: App starten, Trainingsmodus, OperatorPanel sichtbar**

App per `dotnet run` starten. TrainingMode aktivieren. `OperatorPanel.Visibility` per Code auf `Visible` setzen (z.B. via Toolbar-Button oder F12-Hotkey). Code-Liste muss leer dargestellt werden.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs
git commit -m "feat(ui): add operator code list with status dots"
```

---

### Task 19: Hotkeys (Enter/Esc/S/R/←/→) mit Textfeld-Schutz

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs`

- [ ] **Step 1: KeyDown-Handler ergaenzen**

```csharp
private void OperatorPanel_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
{
    if (!_operatorAnnotationActive) return;
    if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;

    if (DataContext is not PlayerWindowViewModel vm || vm.OperatorActiveTask is null) return;

    switch (e.Key)
    {
        case System.Windows.Input.Key.Enter:
            if (vm.OperatorActiveTask.State == CodeTaskState.PreviewReady)
            {
                OperatorConfirmButton_Click(sender!, null!);
                e.Handled = true;
            }
            break;
        case System.Windows.Input.Key.Escape:
            CancelCurrentOperatorBox();
            e.Handled = true;
            break;
        case System.Windows.Input.Key.S:
            OperatorSkipButton_Click(sender!, null!);
            e.Handled = true;
            break;
        case System.Windows.Input.Key.R:
            OperatorRejectButton_Click(sender!, null!);
            e.Handled = true;
            break;
        case System.Windows.Input.Key.Right:
            AdvanceToNextPending();
            e.Handled = true;
            break;
        case System.Windows.Input.Key.Left:
            BackToPreviousTask();
            e.Handled = true;
            break;
    }
}

private void CancelCurrentOperatorBox()
{
    _operatorPreviewCts?.Cancel();
    if (DataContext is PlayerWindowViewModel vm && vm.OperatorActiveTask is not null)
    {
        vm.OperatorActiveTask.Box = null;
        vm.OperatorActiveTask.Preview = null;
        if (vm.OperatorActiveTask.State == CodeTaskState.PreviewReady)
            vm.OperatorActiveTask.State = CodeTaskState.Active;
    }
    CleanupOperatorTempFrame();
    OperatorConfirmButton.IsEnabled = false;
}

private void BackToPreviousTask()
{
    if (DataContext is not PlayerWindowViewModel vm || vm.OperatorSession is null || vm.OperatorActiveTask is null)
        return;
    var idx = vm.OperatorSession.Tasks.IndexOf(vm.OperatorActiveTask);
    if (idx > 0) vm.OperatorActiveTask = vm.OperatorSession.Tasks[idx - 1];
}
```

In XAML: `KeyDown="OperatorPanel_KeyDown"` an `OperatorPanel`-StackPanel binden.

- [ ] **Step 2: Build + manueller Smoke-Test**

App starten, OperatorMode aktivieren. Hotkeys testen:
- Esc: keine Action wenn keine Box, sonst verwerfen
- S/R/←/→: ohne Crash

- [ ] **Step 3: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml
git commit -m "feat(ui): add operator annotation hotkeys"
```

---

## Phase 7 — UI Box-Drag + Live-Maske

### Task 20: Box-Drag-Handler im Player-Overlay

> ⚠️ **Blocker B6 (WPF-Airspace mit LibVLC) + B5 (kein VM)** — siehe Header.
> Ein normales `<Canvas>` ueber `VideoView` ist im Airspace-Modus
> **unsichtbar**. Vor der Implementation **Pflicht**:
> `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.MarkTool.cs` und
> `PlayerWindow.CodingOverlayRender.cs` lesen — die nutzen Popup/AdornerLayer
> als funktionierendes Overlay-Pattern. Identische Render-Pipeline
> wiederverwenden, **nicht** `<Canvas x:Name="OperatorOverlayCanvas" .../>`
> direkt ueber `VideoView` legen. Visibility/Mouse-Handler kommen ebenfalls
> aus dem Mark-Tool-Pattern (kein `{Binding OperatorMode.Active, ...}`).

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` (Overlay-Canvas)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs`

- [ ] **Step 1: Overlay-Canvas im XAML, ueber dem VLC-Player**

In `PlayerWindow.xaml`, in dem Grid das den VLC-Player enthaelt:

```xml
<Canvas x:Name="OperatorOverlayCanvas"
        Background="Transparent"
        Visibility="{Binding OperatorMode.Active, Converter={StaticResource BoolToVisibility}}"
        MouseDown="OperatorOverlayCanvas_MouseDown"
        MouseMove="OperatorOverlayCanvas_MouseMove"
        MouseUp="OperatorOverlayCanvas_MouseUp"/>
```

- [ ] **Step 2: Drag-Handler im OperateurAnnotation-Partial**

```csharp
private System.Windows.Point? _operatorDragStart;
private System.Windows.Shapes.Rectangle? _operatorBoxShape;

private void OperatorOverlayCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    if (!_operatorAnnotationActive) return;
    _operatorDragStart = e.GetPosition(OperatorOverlayCanvas);
    _operatorBoxShape = new System.Windows.Shapes.Rectangle
    {
        Stroke = System.Windows.Media.Brushes.Cyan,
        StrokeThickness = 2,
        Fill = System.Windows.Media.Brushes.Transparent
    };
    System.Windows.Controls.Canvas.SetLeft(_operatorBoxShape, _operatorDragStart.Value.X);
    System.Windows.Controls.Canvas.SetTop(_operatorBoxShape, _operatorDragStart.Value.Y);
    OperatorOverlayCanvas.Children.Add(_operatorBoxShape);
    OperatorOverlayCanvas.CaptureMouse();
}

private void OperatorOverlayCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
{
    if (_operatorDragStart is null || _operatorBoxShape is null) return;
    var p = e.GetPosition(OperatorOverlayCanvas);
    var x = System.Math.Min(_operatorDragStart.Value.X, p.X);
    var y = System.Math.Min(_operatorDragStart.Value.Y, p.Y);
    var w = System.Math.Abs(p.X - _operatorDragStart.Value.X);
    var h = System.Math.Abs(p.Y - _operatorDragStart.Value.Y);
    System.Windows.Controls.Canvas.SetLeft(_operatorBoxShape, x);
    System.Windows.Controls.Canvas.SetTop(_operatorBoxShape, y);
    _operatorBoxShape.Width = w;
    _operatorBoxShape.Height = h;
}

private async void OperatorOverlayCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    if (_operatorDragStart is null || _operatorBoxShape is null) return;
    OperatorOverlayCanvas.ReleaseMouseCapture();

    var canvasW = OperatorOverlayCanvas.ActualWidth;
    var canvasH = OperatorOverlayCanvas.ActualHeight;
    var x1 = System.Windows.Controls.Canvas.GetLeft(_operatorBoxShape);
    var y1 = System.Windows.Controls.Canvas.GetTop(_operatorBoxShape);
    var w = _operatorBoxShape.Width;
    var h = _operatorBoxShape.Height;

    var box = new BoundingBoxNormalized(
        XCenter: (x1 + w / 2) / canvasW,
        YCenter: (y1 + h / 2) / canvasH,
        Width: w / canvasW,
        Height: h / canvasH);

    if (DataContext is PlayerWindowViewModel vm && vm.OperatorActiveTask is not null)
    {
        vm.OperatorActiveTask.Box = box;
        // Task 21+22: Frame-Capture + SAM-Preview
        await TriggerSamPreviewAsync(vm.OperatorActiveTask, box);
    }

    _operatorDragStart = null;
}

private Task TriggerSamPreviewAsync(CodeTask task, BoundingBoxNormalized box)
{
    // Task 22 — Frame capture + service.PreviewMaskAsync
    return Task.CompletedTask;
}
```

- [ ] **Step 3: Build + Smoke-Test**

App starten, Box ziehen — Cyan-Rechteck erscheint.

- [ ] **Step 4: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs
git commit -m "feat(ui): add operator box drag handler"
```

---

### Task 21: Frame-Capture aus VLC-Player

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs`

- [ ] **Step 1: Frame-Snapshot via bestehende Player-Logik**

```csharp
private async Task<string?> CaptureCurrentFrameAsync()
{
    // Bestehende Snapshot-Methode des PlayerWindow nutzen.
    // Im Repo: SnapshotCurrentFrameAsync() oder TakeSnapshotAsync().
    // Falls vorhanden -> direkter Aufruf:
    var path = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"oa_frame_{System.Guid.NewGuid():N}.png");

    try
    {
        // VLC-Snapshot: Mediaplayer.TakeSnapshot(num=0, filePath, width, height)
        // (genaue Signatur des bestehenden Players pruefen)
        var ok = TakeVlcSnapshot(path);
        if (!ok) return null;

        // Cleanup-Pfad merken
        if (_operatorTempFramePath is not null && System.IO.File.Exists(_operatorTempFramePath))
            System.IO.File.Delete(_operatorTempFramePath);
        _operatorTempFramePath = path;

        await Task.CompletedTask;
        return path;
    }
    catch (System.Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Frame-Capture fehlgeschlagen: {ex.Message}");
        return null;
    }
}

private bool TakeVlcSnapshot(string outPath)
{
    // Annahme: _mediaPlayer ist die existierende VLC-Player-Instanz.
    // Wenn das Projekt LibVLCSharp 3.x nutzt:
    //   _mediaPlayer.TakeSnapshot(0, outPath, 0, 0);
    // Das gibt void zurueck — wir pruefen via File.Exists.
    try
    {
        // Pseudo-Code, exakte Methode beim Implementieren aus dem
        // bestehenden PlayerWindow.cs uebernehmen:
        //   _mediaPlayer.TakeSnapshot(0, outPath, 0, 0);
        return System.IO.File.Exists(outPath);
    }
    catch { return false; }
}
```

> **Hinweis**: Beim Implementieren die Code-Stelle in `PlayerWindow.cs` finden, die heute schon einen Snapshot fuer den Codiermodus macht (`SnapshotCurrentFrame`, `TakeSnapshotAsync` o.ae.) und identisch wiederverwenden — kein neues VLC-Pattern erfinden.

- [ ] **Step 2: Build + Smoke-Test**

```powershell
dotnet build
```

App starten, Box ziehen, in `MouseUp` einen Breakpoint setzen, `CaptureCurrentFrameAsync` aufrufen, Datei pruefen.

- [ ] **Step 3: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs
git commit -m "feat(ui): capture frame from vlc for sam preview"
```

---

### Task 22: SAM-Preview-Aufruf + Maske-Render

> ⚠️ **Korrektur K4 (DI ueber ServiceCollectionConfigurator)** — siehe Header.
> Registrierungen kommen in
> `src/AuswertungPro.Next.UI/Composition/ServiceCollectionConfigurator.cs`
> (mit Test-Coverage in `ServiceCollectionConfiguratorTests`), **nicht** in
> `App.xaml.cs`. Step 1 unten ist falsch verortet — den Code in den
> Configurator schreiben.
> Maske-Render im UI muss ebenfalls ueber das Mark-Tool-Pattern (B6) laufen,
> nicht ueber direkt im Canvas gezeichnete Polygone.

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs`
- Modify: `src/AuswertungPro.Next.UI/Composition/ServiceCollectionConfigurator.cs` (DI-Registrierung)

- [ ] **Step 1: DI fuer IOperateurAnnotationService**

In `ServiceCollectionConfigurator.cs`, im DI-Setup:

```csharp
services.AddSingleton<ITrainingSamplesWriter, TrainingSamplesWriterAdapter>();
services.AddSingleton<IKnowledgeBaseIndexer>(sp =>
    new KnowledgeBaseIndexerAdapter(sp.GetRequiredService<KnowledgeBaseManager>()));
services.AddSingleton<IYoloDatasetWriter>(sp => sp.GetRequiredService<YoloDatasetExportService>());
services.AddSingleton<IOperateurAnnotationService, OperateurAnnotationService>();
```

(Genaue using-Imports / DI-Methode anpassen je nach Repo-Stil.)

- [ ] **Step 2: Service in PlayerWindow injizieren oder via ServiceLocator beziehen**

```csharp
private IOperateurAnnotationService? _operatorService;

// Im Konstruktor / OnLoaded:
_operatorService = (App.Current as App)?.Services?.GetService(typeof(IOperateurAnnotationService))
    as IOperateurAnnotationService;
```

- [ ] **Step 3: TriggerSamPreviewAsync vollstaendig implementieren**

```csharp
private async Task TriggerSamPreviewAsync(CodeTask task, BoundingBoxNormalized box)
{
    if (_operatorService is null) return;

    var framePath = await CaptureCurrentFrameAsync();
    if (framePath is null)
    {
        System.Windows.MessageBox.Show("Frame-Capture fehlgeschlagen.");
        return;
    }

    using var bmpProbe = new System.Drawing.Bitmap(framePath);
    var fw = bmpProbe.Width;
    var fh = bmpProbe.Height;

    if (DataContext is not PlayerWindowViewModel vm || vm.OperatorSession is null) return;
    var actualSec = GetCurrentVideoPositionSeconds();
    var suggestedSec = task.FrameDeltaSeconds.HasValue
        ? actualSec - task.FrameDeltaSeconds.Value
        : actualSec; // erste Annotation

    var request = new AnnotationRequest(
        CaseId: vm.OperatorSession.CaseId,
        Code: task.Code,
        ProtocolMeterstand: task.Meterstand,
        SuggestedFrameTimeSeconds: suggestedSec,
        ActualFrameTimeSeconds: actualSec,
        VideoFrameIndex: GetCurrentVideoFrameIndex(),
        FramePath: framePath,
        FrameWidth: fw,
        FrameHeight: fh,
        Box: box);

    _operatorPreviewCts?.Cancel();
    _operatorPreviewCts = new CancellationTokenSource();

    try
    {
        var preview = await _operatorService.PreviewMaskAsync(request, _operatorPreviewCts.Token);
        task.Box = box;
        task.Preview = preview;
        task.FrameDeltaSeconds = actualSec - suggestedSec;
        task.State = CodeTaskState.PreviewReady;
        RenderMaskOverlay(preview);
        OperatorConfirmButton.IsEnabled = true;
    }
    catch (System.Exception ex)
    {
        System.Windows.MessageBox.Show($"SAM-Preview fehlgeschlagen: {ex.Message}");
        task.Box = null;
        OperatorConfirmButton.IsEnabled = false;
    }
}

private void RenderMaskOverlay(MaskPreview preview)
{
    // Polygon aus PolygonJson zeichnen — halbtransparente Polygon-Shape
    if (string.IsNullOrEmpty(preview.PolygonJson)) return;
    try
    {
        var pts = System.Text.Json.JsonSerializer.Deserialize<double[][]>(preview.PolygonJson);
        if (pts is null || pts.Length < 3) return;

        var canvasW = OperatorOverlayCanvas.ActualWidth;
        var canvasH = OperatorOverlayCanvas.ActualHeight;
        var poly = new System.Windows.Shapes.Polygon
        {
            Stroke = System.Windows.Media.Brushes.Magenta,
            StrokeThickness = 1.5,
            Fill = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(80, 255, 0, 200))
        };
        foreach (var p in pts)
            poly.Points.Add(new System.Windows.Point(
                p[0] / preview.MaskWidth * canvasW,
                p[1] / preview.MaskHeight * canvasH));
        OperatorOverlayCanvas.Children.Add(poly);
    }
    catch { /* Polygon-Render best-effort */ }
}

private double GetCurrentVideoPositionSeconds()
{
    // Player-API: _mediaPlayer.Time ist meistens in ms
    return 0.0; // TODO beim Implementieren echte Player-Position
}

private int GetCurrentVideoFrameIndex()
{
    // Player-API: meistens kein direkter Frame-Index, sondern Time*FPS
    return 0;
}
```

- [ ] **Step 4: Build + manueller Smoke-Test**

```powershell
dotnet build
```

App starten, Sidecar laufen lassen, OperatorPanel oeffnen, Box ziehen.
Magenta-Polygon sollte erscheinen, Confirm-Button wird aktiviert.

- [ ] **Step 5: Commit**

```powershell
git add src/AuswertungPro.Next.UI/App.xaml.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs
git commit -m "feat(ui): wire sam preview + render polygon mask"
```

---

### Task 23: Confirm-Logik + Auto-Advance

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs`

- [ ] **Step 1: OperatorConfirmButton_Click implementieren**

```csharp
private async void OperatorConfirmButton_Click(object sender, System.Windows.RoutedEventArgs e)
{
    if (_operatorService is null) return;
    if (DataContext is not PlayerWindowViewModel vm
        || vm.OperatorActiveTask is null
        || vm.OperatorActiveTask.Preview is null
        || vm.OperatorActiveTask.Box is null
        || vm.OperatorSession is null
        || _operatorTempFramePath is null) return;

    var task = vm.OperatorActiveTask;

    using var bmp = new System.Drawing.Bitmap(_operatorTempFramePath);
    var request = new AnnotationRequest(
        CaseId: vm.OperatorSession.CaseId,
        Code: task.Code,
        ProtocolMeterstand: task.Meterstand,
        SuggestedFrameTimeSeconds: GetCurrentVideoPositionSeconds() - (task.FrameDeltaSeconds ?? 0),
        ActualFrameTimeSeconds: GetCurrentVideoPositionSeconds(),
        VideoFrameIndex: GetCurrentVideoFrameIndex(),
        FramePath: _operatorTempFramePath,
        FrameWidth: bmp.Width,
        FrameHeight: bmp.Height,
        Box: task.Box);

    try
    {
        var result = await _operatorService.CommitAsync(request, task.Preview, CancellationToken.None);
        if (result.IsSuccess)
        {
            task.State = CodeTaskState.Committed;
            task.CommittedSampleId = result.SampleId;
            task.CommittedUtc = System.DateTime.UtcNow;
            // Toast / Status-Bar
            System.Diagnostics.Debug.WriteLine(
                $"Sample {result.SampleId} gespeichert. KB={result.KbIndexed}, YOLO={result.YoloWritten}");
            CleanupOperatorTempFrame();
            ClearMaskOverlay();
            AdvanceToNextPending();
        }
        else
        {
            task.State = CodeTaskState.Error;
            System.Windows.MessageBox.Show($"Sample-Speicherung fehlgeschlagen: {result.Error}");
        }
    }
    catch (System.Exception ex)
    {
        task.State = CodeTaskState.Error;
        System.Windows.MessageBox.Show($"Fehler beim Bestaetigen: {ex.Message}");
    }
    finally
    {
        OperatorConfirmButton.IsEnabled = false;
    }
}

private void ClearMaskOverlay()
{
    OperatorOverlayCanvas.Children.Clear();
    _operatorBoxShape = null;
}
```

- [ ] **Step 2: Build + manueller End-to-End-Test**

App starten, Sidecar laufen lassen, Haltungsordner laden, Code waehlen,
Box ziehen, Maske erscheinen lassen, Enter / Confirm druecken.
Sample muss in TrainingSamplesStore (`%KI_BRAIN%/training_samples.json`)
landen.

- [ ] **Step 3: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs
git commit -m "feat(ui): implement confirm + auto-advance"
```

---

## Phase 8 — End-to-End Live-Test

### Task 24: Live-Test mit echtem Sidecar

**Files:**
- Create: `tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationLiveTests.cs`

- [ ] **Step 1: Live-Test schreiben**

```csharp
// tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationLiveTests.cs
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Annotation;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class OperateurAnnotationLiveTests
{
    private readonly ITestOutputHelper _out;
    public OperateurAnnotationLiveTests(ITestOutputHelper o) => _out = o;

    [Fact]
    [Trait("Category", "LiveSidecar")]
    public async Task EndToEnd_LiveSidecar_FullCommitFlow()
    {
        var sidecarUrl = "http://localhost:8100";
        using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try { var r = await probe.GetAsync($"{sidecarUrl}/health"); if (!r.IsSuccessStatusCode) { _out.WriteLine("SKIP: kein Sidecar"); return; } }
        catch { _out.WriteLine("SKIP: kein Sidecar"); return; }

        var tempRoot = Path.Combine(Path.GetTempPath(), "oa_e2e_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            Environment.SetEnvironmentVariable("KI_BRAIN", tempRoot);

            // Test-Frame
            var framePath = Path.Combine(tempRoot, "frame.png");
            using (var bmp = new System.Drawing.Bitmap(640, 480))
                bmp.Save(framePath, System.Drawing.Imaging.ImageFormat.Png);

            // Adapter + Service zusammenbauen
            var writer = new TrainingSamplesWriterAdapter();
            // KB-Indexer + Yolo-Writer mit No-Op-Implementations fuer den
            // E2E-Test (KB/YOLO werden separat gepruef)
            var noopIndexer = new NoOpKbIndexer();
            var noopYolo = new NoOpYoloWriter();

            var client = new VisionPipelineClient(new Uri(sidecarUrl), new HttpClient());
            var service = new OperateurAnnotationService(client, writer, noopIndexer, noopYolo);

            var req = new AnnotationRequest(
                "case-e2e", "BAB B", 12.3, 145.0, 145.0, 3742, framePath, 640, 480,
                new BoundingBoxNormalized(0.5, 0.5, 0.3, 0.3));

            var preview = await service.PreviewMaskAsync(req, CancellationToken.None);
            _out.WriteLine($"Preview: confidence={preview.SamConfidence}, area={preview.MaskAreaPixels}");
            Assert.NotEmpty(preview.SamMaskRle);

            var commit = await service.CommitAsync(req, preview, CancellationToken.None);
            Assert.True(commit.IsSuccess);
            Assert.True(commit.StorePersisted);

            var found = await writer.FindCommittedAsync(
                "case-e2e", SourceTypeNames.OperateurAnnotation, "BAB B", 0.5, CancellationToken.None);
            Assert.Single(found);
            Assert.NotNull(found[0].SamMaskRle);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private sealed class NoOpKbIndexer : IKnowledgeBaseIndexer
    {
        public Task IndexSampleAsync(TrainingSample s, CancellationToken c) => Task.CompletedTask;
    }
    private sealed class NoOpYoloWriter : IYoloDatasetWriter
    {
        public Task<string> AppendSampleAsync(TrainingSample s, CancellationToken c) => Task.FromResult("");
    }
}
```

- [ ] **Step 2: Sidecar starten, Test laufen lassen**

```powershell
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj `
  -s tests/AuswertungPro.Next.Pipeline.Tests/.runsettings.live `
  --filter "Category=LiveSidecar&FullyQualifiedName~OperateurAnnotationLiveTests" `
  --nologo
```

Expected: 1 erfolgreich.

- [ ] **Step 3: Commit**

```powershell
git add tests/AuswertungPro.Next.Pipeline.Tests/OperateurAnnotationLiveTests.cs
git commit -m "test(annotation): add e2e live test for full commit flow"
```

---

## Phase 9 — Polishing

### Task 25: Wiederholt-Import-Erkennung

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs`

- [ ] **Step 1: MarkAlreadyCommittedAsync vollstaendig implementieren**

```csharp
private ITrainingSamplesWriter? _operatorWriter;

private async Task MarkAlreadyCommittedAsync(OperateurAnnotationSession session)
{
    _operatorWriter ??= (App.Current as App)?.Services?.GetService(typeof(ITrainingSamplesWriter))
        as ITrainingSamplesWriter;
    if (_operatorWriter is null) return;

    foreach (var task in session.Tasks)
    {
        var found = await _operatorWriter.FindCommittedAsync(
            session.CaseId,
            SourceTypeNames.OperateurAnnotation,
            task.Code,
            meterTolerance: 0.5,
            CancellationToken.None);

        // Zusaetzlicher Meterstand-Filter
        var match = found.FirstOrDefault(s =>
            System.Math.Abs(s.MeterStart - task.Meterstand) <= 0.5);
        if (match is not null)
        {
            task.State = CodeTaskState.Committed;
            task.CommittedSampleId = match.SampleId;
            task.CommittedUtc = System.DateTime.UtcNow;
        }
    }
}
```

- [ ] **Step 2: Manueller Test**

App starten, Haltungsordner annotieren, App neu starten, gleichen Ordner
laden — Codes mit Sample sollten als Committed (gruen) erscheinen.

- [ ] **Step 3: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs
git commit -m "feat(ui): mark already-committed codes on reimport"
```

---

### Task 26: Temp-Frame-Cleanup

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs`

- [ ] **Step 1: Cleanup an Window-Closing + Mode-Exit ergaenzen**

```csharp
// Im PlayerWindow.OperateurAnnotation.cs - bestehende ExitMode-Methode erweitern,
// und OnClosed des Window die Cleanup mit aufrufen.
private void OnPlayerWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
{
    if (_operatorAnnotationActive
        && DataContext is PlayerWindowViewModel vm
        && vm.OperatorSession?.Tasks.Any(t => t.State == CodeTaskState.PreviewReady) == true)
    {
        var r = System.Windows.MessageBox.Show(
            "Es gibt nicht bestaetigte Annotationen. Verwerfen?",
            "Operateur-Annotation",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (r != System.Windows.MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
    }
    CleanupOperatorTempFrame();
}
```

In Konstruktor / OnInitialized: `Closing += OnPlayerWindowClosing;`.

- [ ] **Step 2: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.OperateurAnnotation.cs
git commit -m "feat(ui): cleanup temp frames on close + warn on unsaved"
```

---

### Task 27: Final Build/Test/Doku

- [ ] **Step 1: Vollstaendiger Build + Test**

```powershell
dotnet build AuswertungPro.sln
dotnet test --nologo
```

Expected: 0 Warnungen, 0 Fehler. Alle vorhandenen + neue Tests gruen.

- [ ] **Step 2: README/ROADMAP aktualisieren**

`docs/ROADMAP.md`: P1.1 Active-Learning-Wochenroutine wird jetzt mit
dem neuen Operateur-Annotations-Workflow umgesetzt.

`README.md` Funktions-Tabelle ergaenzen:
- "Operateur-Annotation im Trainingsmodus: aus Protokoll-Codes pro
  Klick + Box ein operateur-validiertes Trainings-Sample (Frame +
  SAM-Maske + Code) parallel in Store + KB + YOLO."

- [ ] **Step 3: Final-Commit**

```powershell
git add docs/ROADMAP.md README.md
git commit -m "docs: announce operateur annotation feature"
```

- [ ] **Step 4: Final-Push**

```powershell
git push
```

---

## Acceptance-Kriterien (Slice 1)

- [ ] Haltungsordner-Import im TrainingMode funktioniert
- [ ] Code-Liste rechts ist befuellt mit Status-Dots
- [ ] Klick auf Code springt Player zum Meterstand
- [ ] Manuelles Scrubbing zeigt aktualisierten Frame
- [ ] Box-Drag im Overlay loest SAM-Preview aus
- [ ] Polygon-Maske wird im Overlay gerendert
- [ ] Confirm schreibt Sample in Store (immer), YOLO (wenn mappbar) und KB
- [ ] CommitResult-Status sichtbar (Console/Toast)
- [ ] Auto-Advance zum naechsten Pending-Code
- [ ] Skip / Reject veraendern Session-State, kein Sample
- [ ] Wiederholter Import erkennt Committed-Codes (gruener Dot)
- [ ] Sidecar-Down: klare Fehlermeldung, kein Confirm moeglich
- [ ] KB-Down: Sample landet in Store + YOLO, KbIndexState=Pending
- [ ] Alle bestehenden Tests + neue OperateurAnnotation-Tests gruen
- [ ] Build: 0 Warnungen, 0 Fehler

---

## Out of Scope (Slice 2)

- Multi-Box pro Code im selben Frame
- Negative-Sample-Schreiben bei Reject
- Streckenschaden Multi-Sample (mehrere Frames pro MeterStart-MeterEnd)
- Box-only-Fallback bei Sidecar-Down
- Drift-Dashboard (FrameDeltaSeconds-Auswertung)
- Operateur-vs-KI-Diff-Report
- Bulk-Operationen
- Cross-Haltung-Sessions
- YOLO-Polygon-Persistierung im TrainingSample-Domain
  (Slice 1: Polygon nur in MaskPreview-DTO + YOLO-File)
