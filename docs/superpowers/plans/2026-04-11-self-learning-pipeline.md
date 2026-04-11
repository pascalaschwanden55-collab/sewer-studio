# Self-Learning Pipeline — Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** SewerStudio soll Videos automatisch codieren wie ein erfahrener Operateur — wenige, korrekte Ereignisse statt hunderte Roh-Detektionen — und sich dabei aus 2031 vorprotokollierten Haltungen und menschlichen Korrekturen fortlaufend verbessern.

**Architecture:** DetectionAggregator verdichtet YOLO-Einzelframe-Detektionen zu Ereignissen (Temporal Peak Detection). YOLO wird initial auf 10 Defektkategorien trainiert (aus PDF-Ground-Truth), Qwen codiert nur PeakFrames. Korrekturen fliessen via KB + YOLO-Retraining zurueck.

**Tech Stack:** C# .NET 8 (WPF/MVVM), Python FastAPI Sidecar (Ultralytics YOLO), Ollama (Qwen3-VL), SQLite (KnowledgeBase), FFmpeg (Frame-Extraktion)

**Einschraenkung:** Nur Dateien unter `Ai/Pipeline/` und `Ai/Training/` werden geaendert. Alle anderen Features bleiben unangetastet.

---

## Dateistruktur

### Neue Dateien

| Datei | Verantwortung |
|---|---|
| `Ai/Pipeline/DetectionAggregator.cs` | Temporale Aggregation: YOLO-Stream → Ereignisse |
| `Ai/Pipeline/DetectionEvent.cs` | Event-Modell (PeakFrame, Meter, Klasse, Confidence) |
| `Ai/Training/Services/YoloAnnotationGenerator.cs` | GroundTruth + Frame → YOLO-Label-Dateien |
| `Ai/Training/Services/InitialTrainingOrchestrator.cs` | Erster Trainingslauf ueber 2031 Haltungen |
| `Ai/Training/Models/YoloDefectTaxonomy.cs` | Mapping VSA-Code ↔ YOLO-Klasse (10 Kategorien) |
| `tests/SelfLearning/DetectionAggregatorTests.cs` | Unit-Tests Aggregator |
| `tests/SelfLearning/YoloAnnotationGeneratorTests.cs` | Unit-Tests Annotation |
| `tests/SelfLearning/YoloDefectTaxonomyTests.cs` | Unit-Tests Taxonomie-Mapping |

### Modifizierte Dateien

| Datei | Aenderung |
|---|---|
| `Ai/Pipeline/MultiModelAnalysisService.cs` | DetectionAggregator einbauen |
| `Ai/Pipeline/SingleFrameMultiModelService.cs` | PeakFrame-Modus fuer Qwen |
| `sidecar/sidecar/routes/training.py` | Export-Endpoint robust machen |
| `Ai/Training/Services/BatchSelfTrainingOrchestrator.cs` | InitialTraining-Phase |

---

## Task 1: Defekt-Taxonomie (VSA-Code ↔ YOLO-Klasse)

**Files:**
- Create: `src/AuswertungPro.Next.UI/Ai/Training/Models/YoloDefectTaxonomy.cs`
- Test: `tests/SelfLearning/YoloDefectTaxonomyTests.cs`

- [ ] **Step 1: Test-Datei anlegen mit Mapping-Tests**

```csharp
// tests/SelfLearning/YoloDefectTaxonomyTests.cs
using AuswertungPro.Next.UI.Ai.Training.Models;
using Xunit;

namespace Tests.SelfLearning;

public class YoloDefectTaxonomyTests
{
    [Theory]
    [InlineData("BAB", 0, "crack")]
    [InlineData("BAB.B.A", 0, "crack")]
    [InlineData("BAC", 1, "fracture")]
    [InlineData("BAA", 2, "deformation")]
    [InlineData("BAF", 2, "deformation")]
    [InlineData("BAH", 3, "displacement")]
    [InlineData("BAI", 4, "intrusion")]
    [InlineData("BBD", 4, "intrusion")]
    [InlineData("BBB", 5, "root")]
    [InlineData("BBC", 6, "deposit")]
    [InlineData("BBA", 6, "deposit")]
    [InlineData("BBF", 7, "infiltration")]
    [InlineData("BCA", 8, "connection")]
    [InlineData("BAD", 9, "structural_other")]
    [InlineData("BAE", 9, "structural_other")]
    [InlineData("BBE", 9, "structural_other")]
    public void MapVsaCode_ReturnsCorrectClass(string vsaCode, int expectedId, string expectedName)
    {
        var result = YoloDefectTaxonomy.FromVsaCode(vsaCode);
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Value.ClassId);
        Assert.Equal(expectedName, result.Value.ClassName);
    }

    [Theory]
    [InlineData("BCD")]  // Steuercode
    [InlineData("BCE")]
    [InlineData("BCC")]
    [InlineData("BDB")]
    [InlineData("BDC")]
    [InlineData("BDD")]
    public void MapVsaCode_Steuercodes_ReturnsNull(string vsaCode)
    {
        var result = YoloDefectTaxonomy.FromVsaCode(vsaCode);
        Assert.Null(result);
    }

    [Fact]
    public void AllClasses_Returns10()
    {
        var all = YoloDefectTaxonomy.AllClasses;
        Assert.Equal(10, all.Length);
    }

    [Fact]
    public void DataYaml_ContainsAllClasses()
    {
        var yaml = YoloDefectTaxonomy.GenerateDataYaml("/tmp/dataset");
        Assert.Contains("nc: 10", yaml);
        Assert.Contains("crack", yaml);
        Assert.Contains("structural_other", yaml);
    }
}
```

- [ ] **Step 2: Tests ausfuehren — muessen fehlschlagen**

Run: `dotnet test --filter "FullyQualifiedName~YoloDefectTaxonomyTests" --no-build`
Expected: Kompilierfehler (YoloDefectTaxonomy existiert nicht)

- [ ] **Step 3: Implementierung schreiben**

```csharp
// src/AuswertungPro.Next.UI/Ai/Training/Models/YoloDefectTaxonomy.cs
namespace AuswertungPro.Next.UI.Ai.Training.Models;

/// <summary>
/// Mapping zwischen VSA-Schadencodes und YOLO-Defektkategorien.
/// 10 visuelle Klassen — Steuercodes (BCD, BCE, BCC, BDB, BDC, BDD) werden
/// regelbasiert aus dem Meterstand abgeleitet und sind NICHT enthalten.
/// </summary>
public static class YoloDefectTaxonomy
{
    public readonly record struct DefectClass(int ClassId, string ClassName);

    private static readonly DefectClass[] _classes =
    [
        new(0, "crack"),             // BAB
        new(1, "fracture"),          // BAC
        new(2, "deformation"),       // BAA, BAF
        new(3, "displacement"),      // BAH
        new(4, "intrusion"),         // BAI, BBD
        new(5, "root"),              // BBB
        new(6, "deposit"),           // BBC, BBA
        new(7, "infiltration"),      // BBF
        new(8, "connection"),        // BCA
        new(9, "structural_other"),  // BAD, BAE, BAG, BAJ, BAK, BBE, BBG, BBH
    ];

    public static DefectClass[] AllClasses => _classes;

    // Steuercodes die nicht visuell erkannt werden
    private static readonly HashSet<string> _steuercodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BCD", "BCE", "BCC", "BDB", "BDC", "BDD", "BDE", "BDF"
    };

    // Praefix → Klassen-ID
    private static readonly Dictionary<string, int> _prefixMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BAB"] = 0,
        ["BAC"] = 1,
        ["BAA"] = 2,
        ["BAF"] = 2,
        ["BAH"] = 3,
        ["BAI"] = 4,
        ["BBD"] = 4,
        ["BBB"] = 5,
        ["BBC"] = 6,
        ["BBA"] = 6,
        ["BBF"] = 7,
        ["BBG"] = 7,
        ["BCA"] = 8,
        ["BAD"] = 9,
        ["BAE"] = 9,
        ["BAG"] = 9,
        ["BAJ"] = 9,
        ["BAK"] = 9,
        ["BBE"] = 9,
        ["BBH"] = 9,
        ["BCB"] = 9,
    };

    /// <summary>
    /// Mappt einen VSA-Code (z.B. "BAB.B.A") auf eine YOLO-Defektklasse.
    /// Gibt null zurueck fuer Steuercodes (BCD, BCE, etc.).
    /// </summary>
    public static DefectClass? FromVsaCode(string vsaCode)
    {
        if (string.IsNullOrWhiteSpace(vsaCode)) return null;

        // Praefix extrahieren (erste 3 Zeichen, vor dem Punkt)
        var prefix = vsaCode.Length >= 3
            ? vsaCode[..3].ToUpperInvariant()
            : vsaCode.ToUpperInvariant();

        // Punkt-separierte Codes: "BAB.B.A" → "BAB"
        var dotIdx = vsaCode.IndexOf('.');
        if (dotIdx >= 3)
            prefix = vsaCode[..dotIdx].ToUpperInvariant();
        else if (dotIdx > 0)
            prefix = vsaCode[..dotIdx].ToUpperInvariant();

        if (_steuercodes.Contains(prefix)) return null;
        if (_prefixMap.TryGetValue(prefix, out var classId))
            return _classes[classId];
        return null;
    }

    /// <summary>
    /// Erzeugt data.yaml fuer YOLO-Training.
    /// </summary>
    public static string GenerateDataYaml(string datasetPath)
    {
        var names = string.Join("\n", _classes.Select(c => $"  {c.ClassId}: {c.ClassName}"));
        return $"""
            path: {datasetPath}
            train: images/train
            val: images/val
            nc: {_classes.Length}
            names:
            {names}
            """;
    }
}
```

- [ ] **Step 4: Tests ausfuehren — muessen bestehen**

Run: `dotnet test --filter "FullyQualifiedName~YoloDefectTaxonomyTests"`
Expected: Alle 19 Tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/Training/Models/YoloDefectTaxonomy.cs tests/SelfLearning/YoloDefectTaxonomyTests.cs
git commit -m "Defekt-Taxonomie: 10 YOLO-Klassen aus VSA-Codes gemappt"
```

---

## Task 2: DetectionEvent-Modell

**Files:**
- Create: `src/AuswertungPro.Next.UI/Ai/Pipeline/DetectionEvent.cs`

- [ ] **Step 1: Event-Modell erstellen**

```csharp
// src/AuswertungPro.Next.UI/Ai/Pipeline/DetectionEvent.cs
namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Ein aggregiertes Erkennungs-Ereignis — das Ergebnis des DetectionAggregator.
/// Repraesentiert EINEN Schaden/Befund, verdichtet aus mehreren Einzelframe-Detektionen.
/// </summary>
public sealed record DetectionEvent
{
    /// <summary>YOLO-Klasse (0-9), siehe YoloDefectTaxonomy</summary>
    public required int YoloClassId { get; init; }

    /// <summary>YOLO-Klassenname (z.B. "crack", "root")</summary>
    public required string YoloClassName { get; init; }

    /// <summary>Hoechste YOLO-Confidence ueber alle Frames</summary>
    public required double PeakConfidence { get; init; }

    /// <summary>Pfad zum Frame-PNG mit hoechster Confidence</summary>
    public required string PeakFramePath { get; init; }

    /// <summary>Zeitpunkt des PeakFrames im Video (Sekunden)</summary>
    public required double PeakTimeSeconds { get; init; }

    /// <summary>Meterstand am Anfang der Detektion</summary>
    public required double MeterStart { get; init; }

    /// <summary>Meterstand am Ende der Detektion (= MeterStart bei Punktschaden)</summary>
    public required double MeterEnd { get; init; }

    /// <summary>Anzahl Frames in denen der Defekt sichtbar war</summary>
    public required int FrameCount { get; init; }

    /// <summary>BBox des PeakFrames (normiert 0-1): x1, y1, x2, y2</summary>
    public double[]? PeakBbox { get; init; }

    /// <summary>Ob der Event bereits an Qwen zur Feincodierung geschickt wurde</summary>
    public bool IsClassified { get; set; }

    /// <summary>VSA-Code nach Qwen-Klassifizierung (null bis dahin)</summary>
    public string? VsaCode { get; set; }

    /// <summary>Severity 1-5 nach Qwen-Klassifizierung</summary>
    public int? Severity { get; set; }

    /// <summary>Uhrlage nach Qwen-Klassifizierung (z.B. "2:00")</summary>
    public string? ClockPosition { get; set; }
}

/// <summary>
/// Einzelne YOLO-Detektion aus einem Frame — Input fuer den DetectionAggregator.
/// </summary>
public sealed record FrameDetection
{
    public required int YoloClassId { get; init; }
    public required string YoloClassName { get; init; }
    public required double Confidence { get; init; }
    public required double TimeSeconds { get; init; }
    public required double Meter { get; init; }
    public required string FramePath { get; init; }
    public double[]? Bbox { get; init; }
}
```

- [ ] **Step 2: Kompilier-Check**

Run: `dotnet build src/AuswertungPro.Next.UI/ --no-restore`
Expected: Build erfolgreich

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/Pipeline/DetectionEvent.cs
git commit -m "DetectionEvent + FrameDetection Modelle erstellt"
```

---

## Task 3: DetectionAggregator (Kernstueck)

**Files:**
- Create: `src/AuswertungPro.Next.UI/Ai/Pipeline/DetectionAggregator.cs`
- Test: `tests/SelfLearning/DetectionAggregatorTests.cs`

- [ ] **Step 1: Tests schreiben**

```csharp
// tests/SelfLearning/DetectionAggregatorTests.cs
using AuswertungPro.Next.UI.Ai.Pipeline;
using Xunit;

namespace Tests.SelfLearning;

public class DetectionAggregatorTests
{
    private static FrameDetection MakeDetection(int classId, string className,
        double confidence, double time, double meter, string frame = "f.png")
        => new()
        {
            YoloClassId = classId, YoloClassName = className,
            Confidence = confidence, TimeSeconds = time,
            Meter = meter, FramePath = frame
        };

    [Fact]
    public void SingleDetection_BelowMinFrames_NoEvent()
    {
        // Ein einzelner Frame reicht nicht (MinConsecutiveFrames = 3)
        var agg = new DetectionAggregator();
        var result = agg.Feed(MakeDetection(0, "crack", 0.8, 1.0, 5.0));
        Assert.Null(result);
        var flushed = agg.Flush();
        // Weniger als 3 Frames → wird verworfen
        Assert.Empty(flushed);
    }

    [Fact]
    public void ThreeConsecutiveFrames_SameClass_ProducesEvent()
    {
        var agg = new DetectionAggregator();
        agg.Feed(MakeDetection(0, "crack", 0.5, 1.0, 5.0, "f1.png"));
        agg.Feed(MakeDetection(0, "crack", 0.9, 2.0, 5.2, "f2.png"));
        agg.Feed(MakeDetection(0, "crack", 0.6, 3.0, 5.4, "f3.png"));

        // Muss flushen um abzuschliessen
        var events = agg.Flush();
        Assert.Single(events);

        var evt = events[0];
        Assert.Equal(0, evt.YoloClassId);
        Assert.Equal("crack", evt.YoloClassName);
        Assert.Equal(0.9, evt.PeakConfidence);
        Assert.Equal("f2.png", evt.PeakFramePath);
        Assert.Equal(5.0, evt.MeterStart);
        Assert.Equal(5.4, evt.MeterEnd);
        Assert.Equal(3, evt.FrameCount);
    }

    [Fact]
    public void TwoDifferentClasses_ProducesTwoEvents()
    {
        var agg = new DetectionAggregator();
        // 3x crack
        agg.Feed(MakeDetection(0, "crack", 0.7, 1.0, 5.0));
        agg.Feed(MakeDetection(0, "crack", 0.8, 2.0, 5.2));
        agg.Feed(MakeDetection(0, "crack", 0.7, 3.0, 5.4));
        // 3x root an anderer Stelle
        agg.Feed(MakeDetection(5, "root", 0.6, 10.0, 12.0));
        agg.Feed(MakeDetection(5, "root", 0.9, 11.0, 12.3));
        agg.Feed(MakeDetection(5, "root", 0.7, 12.0, 12.5));

        var events = agg.Flush();
        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.YoloClassName == "crack");
        Assert.Contains(events, e => e.YoloClassName == "root");
    }

    [Fact]
    public void SameClass_DifferentMeterLocations_ProducesTwoEvents()
    {
        var agg = new DetectionAggregator();
        // Riss bei Meter 5
        agg.Feed(MakeDetection(0, "crack", 0.8, 1.0, 5.0));
        agg.Feed(MakeDetection(0, "crack", 0.8, 2.0, 5.1));
        agg.Feed(MakeDetection(0, "crack", 0.8, 3.0, 5.2));
        // Riss bei Meter 20 (weit weg → neues Ereignis)
        agg.Feed(MakeDetection(0, "crack", 0.7, 20.0, 20.0));
        agg.Feed(MakeDetection(0, "crack", 0.7, 21.0, 20.1));
        agg.Feed(MakeDetection(0, "crack", 0.7, 22.0, 20.2));

        var events = agg.Flush();
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void LowConfidence_BelowThreshold_Ignored()
    {
        var agg = new DetectionAggregator(minConfidence: 0.4);
        agg.Feed(MakeDetection(0, "crack", 0.1, 1.0, 5.0));
        agg.Feed(MakeDetection(0, "crack", 0.2, 2.0, 5.1));
        agg.Feed(MakeDetection(0, "crack", 0.3, 3.0, 5.2));

        var events = agg.Flush();
        Assert.Empty(events);
    }

    [Fact]
    public void GapInFrames_ClosesEventAndStartsNew()
    {
        var agg = new DetectionAggregator(maxGapFrames: 2);
        agg.Feed(MakeDetection(0, "crack", 0.8, 1.0, 5.0));
        agg.Feed(MakeDetection(0, "crack", 0.9, 2.0, 5.1));
        agg.Feed(MakeDetection(0, "crack", 0.8, 3.0, 5.2));
        // 5 Frames Luecke (Meter springt, keine Detektion dazwischen)
        // Simuliert durch Feed mit grossem Meter-Sprung
        agg.Feed(MakeDetection(0, "crack", 0.7, 30.0, 25.0));
        agg.Feed(MakeDetection(0, "crack", 0.7, 31.0, 25.1));
        agg.Feed(MakeDetection(0, "crack", 0.7, 32.0, 25.2));

        var events = agg.Flush();
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void Streckenschaden_MeterStartDiffersFromEnd()
    {
        var agg = new DetectionAggregator();
        // Ablagerung ueber 3 Meter
        agg.Feed(MakeDetection(6, "deposit", 0.6, 1.0, 10.0));
        agg.Feed(MakeDetection(6, "deposit", 0.8, 5.0, 11.5));
        agg.Feed(MakeDetection(6, "deposit", 0.7, 10.0, 13.0));

        var events = agg.Flush();
        Assert.Single(events);
        Assert.Equal(10.0, events[0].MeterStart);
        Assert.Equal(13.0, events[0].MeterEnd);
    }
}
```

- [ ] **Step 2: Tests ausfuehren — muessen fehlschlagen**

Run: `dotnet test --filter "FullyQualifiedName~DetectionAggregatorTests" --no-build`
Expected: Kompilierfehler (DetectionAggregator existiert nicht)

- [ ] **Step 3: DetectionAggregator implementieren**

```csharp
// src/AuswertungPro.Next.UI/Ai/Pipeline/DetectionAggregator.cs
namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Verdichtet einen Strom von YOLO-Einzelframe-Detektionen zu Ereignissen.
/// Arbeitet wie ein Operateur: wartet bis ein Schaden ueber mehrere Frames
/// konsistent sichtbar ist, waehlt den Frame mit hoechster Confidence als
/// PeakFrame, und protokolliert das Ereignis einmal.
/// </summary>
public sealed class DetectionAggregator
{
    private readonly int _minConsecutiveFrames;
    private readonly double _minConfidence;
    private readonly double _meterMergeRadius;
    private readonly int _maxGapFrames;

    // Aktive Detektionen die noch nicht abgeschlossen sind
    private readonly List<ActiveDetection> _active = [];

    // Zaehler fuer Feed-Aufrufe (fuer Gap-Erkennung)
    private int _feedCount;

    public DetectionAggregator(
        int minConsecutiveFrames = 3,
        double minConfidence = 0.4,
        double meterMergeRadius = 1.5,
        int maxGapFrames = 5)
    {
        _minConsecutiveFrames = minConsecutiveFrames;
        _minConfidence = minConfidence;
        _meterMergeRadius = meterMergeRadius;
        _maxGapFrames = maxGapFrames;
    }

    /// <summary>
    /// Fuettert eine Einzelframe-Detektion ein.
    /// Gibt ein abgeschlossenes Event zurueck wenn eine ActiveDetection
    /// durch eine Luecke beendet wurde, sonst null.
    /// </summary>
    public DetectionEvent? Feed(FrameDetection detection)
    {
        _feedCount++;
        DetectionEvent? closedEvent = null;

        // Confidence-Filter
        if (detection.Confidence < _minConfidence)
            return null;

        // Suche passende aktive Detektion (gleiche Klasse + nahe genug)
        ActiveDetection? match = null;
        foreach (var active in _active)
        {
            if (active.YoloClassId == detection.YoloClassId &&
                Math.Abs(active.LastMeter - detection.Meter) <= _meterMergeRadius)
            {
                match = active;
                break;
            }
        }

        // Abgelaufene aktive Detektionen schliessen
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var a = _active[i];
            if (a == match) continue;
            if (_feedCount - a.LastFeedIndex > _maxGapFrames)
            {
                var evt = TryClose(a);
                if (evt != null) closedEvent = evt;
                _active.RemoveAt(i);
            }
        }

        if (match != null)
        {
            // Zu bestehender Detektion hinzufuegen
            match.AddFrame(detection, _feedCount);
        }
        else
        {
            // Neue aktive Detektion starten
            _active.Add(new ActiveDetection(detection, _feedCount));
        }

        return closedEvent;
    }

    /// <summary>
    /// Schliesst alle aktiven Detektionen ab. Am Video-Ende aufrufen.
    /// </summary>
    public List<DetectionEvent> Flush()
    {
        var events = new List<DetectionEvent>();
        foreach (var active in _active)
        {
            var evt = TryClose(active);
            if (evt != null) events.Add(evt);
        }
        _active.Clear();
        return events;
    }

    private DetectionEvent? TryClose(ActiveDetection active)
    {
        if (active.FrameCount < _minConsecutiveFrames)
            return null;

        return new DetectionEvent
        {
            YoloClassId = active.YoloClassId,
            YoloClassName = active.YoloClassName,
            PeakConfidence = active.PeakConfidence,
            PeakFramePath = active.PeakFramePath,
            PeakTimeSeconds = active.PeakTimeSeconds,
            MeterStart = active.MeterStart,
            MeterEnd = active.MeterEnd,
            FrameCount = active.FrameCount,
            PeakBbox = active.PeakBbox,
        };
    }

    private sealed class ActiveDetection
    {
        public int YoloClassId { get; }
        public string YoloClassName { get; }
        public double PeakConfidence { get; private set; }
        public string PeakFramePath { get; private set; }
        public double PeakTimeSeconds { get; private set; }
        public double[]? PeakBbox { get; private set; }
        public double MeterStart { get; }
        public double MeterEnd { get; private set; }
        public double LastMeter { get; private set; }
        public int FrameCount { get; private set; }
        public int LastFeedIndex { get; private set; }

        public ActiveDetection(FrameDetection first, int feedIndex)
        {
            YoloClassId = first.YoloClassId;
            YoloClassName = first.YoloClassName;
            PeakConfidence = first.Confidence;
            PeakFramePath = first.FramePath;
            PeakTimeSeconds = first.TimeSeconds;
            PeakBbox = first.Bbox;
            MeterStart = first.Meter;
            MeterEnd = first.Meter;
            LastMeter = first.Meter;
            FrameCount = 1;
            LastFeedIndex = feedIndex;
        }

        public void AddFrame(FrameDetection detection, int feedIndex)
        {
            FrameCount++;
            LastFeedIndex = feedIndex;
            LastMeter = detection.Meter;

            if (detection.Meter > MeterEnd)
                MeterEnd = detection.Meter;

            if (detection.Confidence > PeakConfidence)
            {
                PeakConfidence = detection.Confidence;
                PeakFramePath = detection.FramePath;
                PeakTimeSeconds = detection.TimeSeconds;
                PeakBbox = detection.Bbox;
            }
        }
    }
}
```

- [ ] **Step 4: Tests ausfuehren — muessen bestehen**

Run: `dotnet test --filter "FullyQualifiedName~DetectionAggregatorTests"`
Expected: Alle 7 Tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/Pipeline/DetectionAggregator.cs tests/SelfLearning/DetectionAggregatorTests.cs
git commit -m "DetectionAggregator: Temporale Verdichtung von YOLO-Detektionen"
```

---

## Task 4: YoloAnnotationGenerator (Ground-Truth → YOLO-Labels)

**Files:**
- Create: `src/AuswertungPro.Next.UI/Ai/Training/Services/YoloAnnotationGenerator.cs`
- Test: `tests/SelfLearning/YoloAnnotationGeneratorTests.cs`

- [ ] **Step 1: Tests schreiben**

```csharp
// tests/SelfLearning/YoloAnnotationGeneratorTests.cs
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.Ai.Training.Services;
using Xunit;

namespace Tests.SelfLearning;

public class YoloAnnotationGeneratorTests
{
    [Fact]
    public void GenerateLabel_Crack_ReturnsFullFrameBbox()
    {
        var gt = new GroundTruthEntry
        {
            MeterStart = 5.0, MeterEnd = 5.0,
            VsaCode = "BAB.B.A", Text = "Riss laengs"
        };
        var line = YoloAnnotationGenerator.GenerateLabelLine(gt);
        // Klasse 0 (crack), Full-Frame-BBox: center 0.5 0.5, size 1.0 1.0
        Assert.Equal("0 0.5 0.5 1.0 1.0", line);
    }

    [Fact]
    public void GenerateLabel_Steuercode_ReturnsNull()
    {
        var gt = new GroundTruthEntry
        {
            MeterStart = 0.0, MeterEnd = 0.0,
            VsaCode = "BCD", Text = "Rohranfang"
        };
        var line = YoloAnnotationGenerator.GenerateLabelLine(gt);
        Assert.Null(line);
    }

    [Fact]
    public void GenerateLabel_NullCode_ReturnsNull()
    {
        var gt = new GroundTruthEntry
        {
            MeterStart = 5.0, MeterEnd = 5.0,
            VsaCode = null, Text = "Unbekannt"
        };
        var line = YoloAnnotationGenerator.GenerateLabelLine(gt);
        Assert.Null(line);
    }

    [Fact]
    public void GenerateLabel_Root_ClassId5()
    {
        var gt = new GroundTruthEntry
        {
            MeterStart = 12.0, MeterEnd = 14.0,
            VsaCode = "BBB", Text = "Wurzeleinwuchs"
        };
        var line = YoloAnnotationGenerator.GenerateLabelLine(gt);
        Assert.StartsWith("5 ", line);
    }
}
```

- [ ] **Step 2: Tests ausfuehren — muessen fehlschlagen**

Run: `dotnet test --filter "FullyQualifiedName~YoloAnnotationGeneratorTests" --no-build`
Expected: Kompilierfehler

- [ ] **Step 3: Implementierung**

```csharp
// src/AuswertungPro.Next.UI/Ai/Training/Services/YoloAnnotationGenerator.cs
using AuswertungPro.Next.UI.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Erzeugt YOLO-Annotationsdateien (.txt) aus GroundTruth-Eintraegen.
/// Format pro Zeile: class_id x_center y_center width height (normiert 0-1)
/// Fuer den initialen Trainingslauf: Full-Frame-BBox (0.5 0.5 1.0 1.0),
/// weil aus PDF-Protokollen keine pixelgenauen Annotationen vorliegen.
/// </summary>
public static class YoloAnnotationGenerator
{
    /// <summary>
    /// Erzeugt eine YOLO-Label-Zeile fuer einen GroundTruthEntry.
    /// Gibt null zurueck fuer Steuercodes oder unbekannte Codes.
    /// </summary>
    public static string? GenerateLabelLine(GroundTruthEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.VsaCode)) return null;
        var defect = YoloDefectTaxonomy.FromVsaCode(entry.VsaCode);
        if (defect == null) return null;
        // Full-Frame-BBox: Zentrum 0.5/0.5, Groesse 1.0/1.0
        return $"{defect.Value.ClassId} 0.5 0.5 1.0 1.0";
    }

    /// <summary>
    /// Erzeugt ein komplettes YOLO-Dataset aus Frame-Mappings.
    /// Kopiert Frames nach images/ und schreibt Labels nach labels/.
    /// </summary>
    public static async Task<DatasetStats> ExportDatasetAsync(
        IReadOnlyList<(GroundTruthEntry Entry, string FramePath)> mappings,
        string outputDir,
        double trainSplit = 0.8,
        CancellationToken ct = default)
    {
        var trainImages = Path.Combine(outputDir, "images", "train");
        var valImages = Path.Combine(outputDir, "images", "val");
        var trainLabels = Path.Combine(outputDir, "labels", "train");
        var valLabels = Path.Combine(outputDir, "labels", "val");

        Directory.CreateDirectory(trainImages);
        Directory.CreateDirectory(valImages);
        Directory.CreateDirectory(trainLabels);
        Directory.CreateDirectory(valLabels);

        var stats = new DatasetStats();
        var rng = new Random(42); // Reproduzierbar

        foreach (var (entry, framePath) in mappings)
        {
            ct.ThrowIfCancellationRequested();
            var label = GenerateLabelLine(entry);
            if (label == null) { stats.Skipped++; continue; }
            if (!File.Exists(framePath)) { stats.Skipped++; continue; }

            var isTrain = rng.NextDouble() < trainSplit;
            var imgDir = isTrain ? trainImages : valImages;
            var lblDir = isTrain ? trainLabels : valLabels;

            var baseName = $"{stats.Total:D6}";
            var ext = Path.GetExtension(framePath);
            File.Copy(framePath, Path.Combine(imgDir, baseName + ext), overwrite: true);
            await File.WriteAllTextAsync(
                Path.Combine(lblDir, baseName + ".txt"), label, ct);

            stats.Total++;
            stats.ClassCounts.TryGetValue(label[0] - '0', out var c);
            stats.ClassCounts[label[0] - '0'] = c + 1;
            if (isTrain) stats.Train++; else stats.Val++;
        }

        // data.yaml schreiben
        var yaml = YoloDefectTaxonomy.GenerateDataYaml(outputDir);
        await File.WriteAllTextAsync(Path.Combine(outputDir, "data.yaml"), yaml, ct);

        return stats;
    }

    public sealed class DatasetStats
    {
        public int Total { get; set; }
        public int Train { get; set; }
        public int Val { get; set; }
        public int Skipped { get; set; }
        public Dictionary<int, int> ClassCounts { get; } = [];
    }
}
```

- [ ] **Step 4: Tests ausfuehren — muessen bestehen**

Run: `dotnet test --filter "FullyQualifiedName~YoloAnnotationGeneratorTests"`
Expected: Alle 4 Tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/Training/Services/YoloAnnotationGenerator.cs tests/SelfLearning/YoloAnnotationGeneratorTests.cs
git commit -m "YoloAnnotationGenerator: Ground-Truth zu YOLO-Labels konvertieren"
```

---

## Task 5: InitialTrainingOrchestrator

**Files:**
- Create: `src/AuswertungPro.Next.UI/Ai/Training/Services/InitialTrainingOrchestrator.cs`

- [ ] **Step 1: Orchestrator implementieren**

```csharp
// src/AuswertungPro.Next.UI/Ai/Training/Services/InitialTrainingOrchestrator.cs
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training.Models;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Einmaliger Orchestrator: Durchlauf ueber alle Haltungen in D:\Haltungen,
/// extrahiert Ground-Truth aus PDFs, ordnet Frames zu, erzeugt YOLO-Dataset
/// und startet das initiale Training via Sidecar.
/// </summary>
public sealed class InitialTrainingOrchestrator
{
    private readonly MeterTimelineService _meterTimeline;
    private readonly VisionPipelineClient _sidecar;
    private readonly string _ffmpegPath;
    private readonly ILogger? _logger;

    public InitialTrainingOrchestrator(
        MeterTimelineService meterTimeline,
        VisionPipelineClient sidecar,
        string ffmpegPath,
        ILogger? logger = null)
    {
        _meterTimeline = meterTimeline;
        _sidecar = sidecar;
        _ffmpegPath = ffmpegPath;
        _logger = logger;
    }

    /// <summary>
    /// Phase 1: Alle Haltungen scannen, Frames extrahieren, Dataset erzeugen.
    /// </summary>
    public async Task<InitialTrainingResult> RunAsync(
        string haltungenRoot,
        string datasetOutputDir,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new InitialTrainingResult();

        // 1. Haltungen entdecken (Video + PDF Paare)
        progress?.Report("Scanne Haltungen...");
        var haltungen = DiscoverHaltungen(haltungenRoot);
        result.TotalHaltungen = haltungen.Count;
        _logger?.LogInformation("Gefunden: {Count} Haltungen", haltungen.Count);

        // 2. Pro Haltung: PDF parsen → GroundTruth → Frames zuordnen
        var allMappings = new List<(GroundTruthEntry Entry, string FramePath)>();
        var parser = new PdfProtocolTableParser();
        var mapper = new ProtocolToGroundTruthMapper();
        var resolver = new MeterToFrameResolver(_meterTimeline, _ffmpegPath, _logger);

        foreach (var (haltungId, videoPath, pdfPath) in haltungen)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                progress?.Report($"Verarbeite {haltungId}...");

                // PDF → Protokoll
                var protocol = parser.Parse(pdfPath);
                if (protocol?.Current?.Entries == null || protocol.Current.Entries.Count == 0)
                {
                    result.Skipped++;
                    continue;
                }

                // Protokoll → GroundTruth
                var groundTruth = mapper.Map(protocol);
                if (groundTruth.Count == 0) { result.Skipped++; continue; }

                // GroundTruth → Frames
                var tempDir = Path.Combine(
                    Path.GetDirectoryName(videoPath)!,
                    "self_training_frames");
                Directory.CreateDirectory(tempDir);

                var frameMappings = await resolver.ResolveAsync(
                    videoPath, groundTruth, protocol, tempDir, ct);

                foreach (var fm in frameMappings)
                {
                    if (fm.FramePath != null && File.Exists(fm.FramePath))
                        allMappings.Add((fm.Entry, fm.FramePath));
                }

                result.Processed++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Fehler bei Haltung {Id}", haltungId);
                result.Failed++;
            }
        }

        // 3. Dataset exportieren
        progress?.Report($"Exportiere Dataset ({allMappings.Count} Frames)...");
        var stats = await YoloAnnotationGenerator.ExportDatasetAsync(
            allMappings, datasetOutputDir, trainSplit: 0.8, ct);
        result.DatasetStats = stats;

        _logger?.LogInformation(
            "Dataset: {Total} Frames ({Train} Train, {Val} Val), {Skipped} uebersprungen",
            stats.Total, stats.Train, stats.Val, stats.Skipped);

        // 4. Training starten (via Sidecar)
        if (stats.Total >= 100) // Minimum fuer sinnvolles Training
        {
            progress?.Report("Starte YOLO-Training...");
            result.TrainingStarted = true;
            // Training laeuft async im Sidecar — Status via /training/jobs/{id}
            var trainResult = await _sidecar.StartYoloTrainingAsync(
                datasetOutputDir, epochs: 100, imageSize: 640, ct: ct);
            result.TrainingJobId = trainResult?.JobId;
        }
        else
        {
            _logger?.LogWarning("Zu wenige Frames ({Count}), Training uebersprungen", stats.Total);
        }

        return result;
    }

    /// <summary>
    /// Findet Video+PDF-Paare in der Haltungen-Ordnerstruktur.
    /// </summary>
    private static List<(string Id, string Video, string Pdf)> DiscoverHaltungen(string root)
    {
        var results = new List<(string, string, string)>();

        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var id = Path.GetFileName(dir);
            var videos = Directory.GetFiles(dir, "*.mpg")
                .Concat(Directory.GetFiles(dir, "*.mp4"))
                .Concat(Directory.GetFiles(dir, "*.avi"))
                .ToArray();
            var pdfs = Directory.GetFiles(dir, "*.pdf");

            if (videos.Length > 0 && pdfs.Length > 0)
            {
                // Nehme das erste Video und erste PDF (nach Datum sortiert)
                var video = videos.OrderByDescending(File.GetLastWriteTime).First();
                var pdf = pdfs.OrderByDescending(File.GetLastWriteTime).First();
                results.Add((id, video, pdf));
            }
        }

        return results;
    }
}

public sealed class InitialTrainingResult
{
    public int TotalHaltungen { get; set; }
    public int Processed { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public YoloAnnotationGenerator.DatasetStats? DatasetStats { get; set; }
    public bool TrainingStarted { get; set; }
    public string? TrainingJobId { get; set; }
}
```

- [ ] **Step 2: Kompilier-Check**

Run: `dotnet build src/AuswertungPro.Next.UI/ --no-restore`
Expected: Build erfolgreich (ggf. fehlende Methoden-Signaturen in VisionPipelineClient, MeterToFrameResolver anpassen — siehe Step 3)

- [ ] **Step 3: Fehlende Signaturen pruefen und anpassen**

Pruefen ob `VisionPipelineClient.StartYoloTrainingAsync` und `MeterToFrameResolver.ResolveAsync` mit den erwarteten Signaturen existieren. Falls noetig, Adapter-Methoden schreiben die die bestehenden Signaturen aufrufen. Die bestehenden Klassen nur minimal aendern (Wrapper-Methoden).

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/Training/Services/InitialTrainingOrchestrator.cs
git commit -m "InitialTrainingOrchestrator: Erster YOLO-Trainingslauf aus 2031 Haltungen"
```

---

## Task 6: DetectionAggregator in MultiModelAnalysisService integrieren

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Ai/Pipeline/MultiModelAnalysisService.cs`

- [ ] **Step 1: Bestehende Datei lesen und verstehen**

Lies `MultiModelAnalysisService.cs` komplett. Identifiziere wo aktuell Frame-fuer-Frame-Ergebnisse gesammelt werden und wo der DetectionAggregator eingebaut werden muss.

- [ ] **Step 2: DetectionAggregator als Feld hinzufuegen**

Im Konstruktor von `MultiModelAnalysisService`:
```csharp
private readonly DetectionAggregator _aggregator;

// Im Konstruktor:
_aggregator = new DetectionAggregator(
    minConsecutiveFrames: 3,
    minConfidence: 0.4,
    meterMergeRadius: 1.5,
    maxGapFrames: 5);
```

- [ ] **Step 3: In der Frame-Schleife YOLO-Detektionen an Aggregator fuettern**

Nach dem YOLO-Aufruf pro Frame: Jede YOLO-Detektion als `FrameDetection` an `_aggregator.Feed()` uebergeben. Events die zurueckkommen in eine Liste sammeln.

Am Ende des Videos: `_aggregator.Flush()` aufrufen und restliche Events sammeln.

- [ ] **Step 4: Qwen nur fuer PeakFrames aufrufen**

Statt Qwen fuer jeden Frame aufzurufen:
- Nur fuer `DetectionEvent.PeakFramePath` Qwen aufrufen
- YOLO-Klasse als Kontext mitgeben
- Ergebnis (VsaCode, Severity, ClockPosition) auf das DetectionEvent schreiben

- [ ] **Step 5: Build + bestehende Tests**

Run: `dotnet build src/AuswertungPro.Next.UI/ --no-restore`
Run: `dotnet test --filter Category=Recommendation`
Expected: Build OK, bestehende Tests PASS (Integration hat keine bestehenden Tests gebrochen)

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/Pipeline/MultiModelAnalysisService.cs
git commit -m "DetectionAggregator in Video-Pipeline integriert — Qwen nur fuer PeakFrames"
```

---

## Task 7: Sidecar Training-Endpoint robust machen

**Files:**
- Modify: `sidecar/sidecar/routes/training.py`

- [ ] **Step 1: Bestehende Datei lesen**

Lies `training.py` komplett. Der `export_yolo` Endpoint (Z. 340-410) und `train_yolo` (Z. 413-472) sind Skeleton — sie muessen fuer den produktiven Einsatz robust gemacht werden.

- [ ] **Step 2: Export-Endpoint pruefen**

Sicherstellen dass `export_yolo`:
- Bilder korrekt kopiert (nicht nur Pfade schreibt)
- data.yaml mit allen 10 Klassen erzeugt
- Train/Val-Split korrekt durchfuehrt
- Fehler bei fehlenden Dateien loggt statt abzubrechen

- [ ] **Step 3: Train-Endpoint pruefen**

Sicherstellen dass `train_yolo`:
- GPU-Speicher freigibt bevor Training startet (YOLO-Inference-Modell entladen)
- Korrekte Ultralytics model.train() Parameter uebergibt
- Nach Training: Inference-Modell mit neuen Gewichten neu laedt (active.json)
- Job-Status korrekt aktualisiert (running → completed/failed)

- [ ] **Step 4: `StartYoloTrainingAsync` in VisionPipelineClient ergaenzen**

Falls noch nicht vorhanden, eine C#-Methode in `VisionPipelineClient` hinzufuegen:
```csharp
public async Task<TrainJobResponse?> StartYoloTrainingAsync(
    string datasetPath, int epochs = 100, int imageSize = 640,
    CancellationToken ct = default)
{
    var request = new YoloTrainRequest(datasetPath, epochs, imageSize);
    var response = await _http.PostAsJsonAsync("/training/train-yolo", request, ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<TrainJobResponse>(ct);
}
```

- [ ] **Step 5: Commit**

```bash
git add sidecar/sidecar/routes/training.py src/AuswertungPro.Next.UI/Ai/Pipeline/VisionPipelineClient.cs
git commit -m "Sidecar Training-Endpoint produktionsbereit gemacht"
```

---

## Task 8: YOLO-Retraining-Kreislauf aktivieren

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Ai/Training/Services/BatchSelfTrainingOrchestrator.cs`

- [ ] **Step 1: Bestehende Datei lesen**

Lies `BatchSelfTrainingOrchestrator.cs` komplett. Identifiziere wo nach dem Batch-Durchlauf `YoloRetrainOrchestrator.RunIfEligibleAsync()` aufgerufen werden sollte.

- [ ] **Step 2: YoloRetrainOrchestrator als Dependency hinzufuegen**

Im Konstruktor:
```csharp
private readonly YoloRetrainOrchestrator? _yoloRetrain;

// Konstruktor-Parameter erweitern:
public BatchSelfTrainingOrchestrator(
    ...,
    YoloRetrainOrchestrator? yoloRetrain = null)
{
    _yoloRetrain = yoloRetrain;
}
```

- [ ] **Step 3: Nach Batch-Durchlauf Retraining pruefen**

Am Ende von `RunAsync`, nach der KB-Enrichment-Schleife:
```csharp
// YOLO-Retraining pruefen wenn genug neue Samples
if (_yoloRetrain != null)
{
    _logger?.LogInformation("Pruefe YOLO-Retraining-Berechtigung...");
    progress?.Report("Pruefe YOLO-Retraining...");
    try
    {
        var retrainResult = await _yoloRetrain.RunIfEligibleAsync(ct: ct);
        _logger?.LogInformation("Retraining: {Status}", retrainResult.Status);
    }
    catch (Exception ex)
    {
        _logger?.LogWarning(ex, "YOLO-Retraining fehlgeschlagen");
    }
}
```

- [ ] **Step 4: Build + bestehende Tests**

Run: `dotnet build src/AuswertungPro.Next.UI/ --no-restore`
Run: `dotnet test --filter Category=Recommendation`
Expected: Build OK, Tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Ai/Training/Services/BatchSelfTrainingOrchestrator.cs
git commit -m "YOLO-Retraining nach Batch-Durchlauf aktiviert"
```

---

## Task 9: End-to-End Integrationstest

- [ ] **Step 1: Sidecar starten**

```bash
cd sidecar && python -m uvicorn sidecar.main:app --port 8100
```
Warten auf Health-Check: `curl http://localhost:8100/health`

- [ ] **Step 2: Eine einzelne Haltung testen**

Eine Haltung aus D:\Haltungen waehlen die ein bekanntes PDF-Protokoll hat. Den VideoSelfTrainingOrchestrator manuell ausfuehren und pruefen:
1. PDF wird korrekt geparst
2. Frames werden korrekt zugeordnet
3. DetectionAggregator erzeugt sinnvolle Anzahl Events (< 50)
4. DifferenceAnalyzer zeigt F1 > 0

- [ ] **Step 3: Ergebnis dokumentieren**

Erwartetes Ergebnis in der Konsole: Anzahl Events, F1-Score, TP/FP/FN.
Bei Problemen: Ursache identifizieren und in einem Follow-up-Task fixen.

- [ ] **Step 4: Commit**

```bash
git commit -m "End-to-End Self-Learning Pipeline verifiziert"
```

---

## Zusammenfassung Reihenfolge

| Task | Abhaengigkeiten | Geschaetzter Aufwand |
|---|---|---|
| 1: Defekt-Taxonomie | Keine | Klein |
| 2: DetectionEvent-Modell | Keine | Klein |
| 3: DetectionAggregator | Task 2 | Mittel |
| 4: YoloAnnotationGenerator | Task 1 | Mittel |
| 5: InitialTrainingOrchestrator | Task 4 | Mittel |
| 6: Pipeline-Integration | Task 3 | Mittel |
| 7: Sidecar Training | Keine | Klein |
| 8: Retraining-Kreislauf | Task 7 | Klein |
| 9: End-to-End Test | Alle | Klein |

**Parallelisierbar:** Tasks 1+2+7 koennen parallel laufen. Tasks 3+4 koennen parallel laufen.
