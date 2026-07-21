using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PipelineResultPresenterTests
{
    [Fact]
    public void ApplySuccessful_lehnt_Fehlerergebnis_ab()
    {
        var vm = new VideoAnalysisPipelineViewModel
        {
            FramesAnalyzed = 17,
            DetectionCount = 9,
            StatsText = "unveraendert",
            TelemetryText = "unveraendert"
        };

        var error = Assert.Throws<ArgumentException>(() =>
            PipelineResultPresenter.ApplySuccessful(
                vm,
                PipelineResult.Failed("Analyse fehlgeschlagen")));

        Assert.Equal("result", error.ParamName);
        Assert.False(vm.IsDone);
        Assert.False(vm.HasError);
        Assert.Equal(17, vm.FramesAnalyzed);
        Assert.Equal(9, vm.DetectionCount);
        Assert.Equal("unveraendert", vm.StatsText);
        Assert.Equal("unveraendert", vm.TelemetryText);
    }

    [Fact]
    public void Apply_Rohbefunde_setzt_Endwerte_und_begrenzt_Radar_auf_250()
    {
        var vm = new VideoAnalysisPipelineViewModel
        {
            IsDone = false,
            HasError = true,
            ErrorText = "alter Fehler",
            StatusText = "alter Status",
            PhaseLabel = "alte Phase"
        };
        var detections = Enumerable.Range(0, 251)
            .Select(index => Raw(
                $"Befund {index}",
                heightMm: index == 0 ? 0 : null,
                widthMm: index == 1 ? 0 : null,
                intrusionPercent: index == 2 ? 0 : null,
                crossSectionReductionPercent: index == 3 ? 0 : null,
                diameterReductionMm: index == 4 ? 0 : null,
                extentPercent: index == 5 ? 0 : null,
                positionClock: index == 6 ? "3" : index == 7 ? "   " : null))
            .ToArray();
        var stats = new PipelineStats(
            FramesAnalyzed: 77,
            DurationSeconds: 12.3,
            DetectionsRaw: 999,
            EntriesGenerated: 44,
            EntriesWithHighConfidence: 5);
        var telemetry = Telemetry(totalFrames: 77, wallClockMs: 2000);

        var presentation = PipelineResultPresenter.ApplySuccessful(
            vm,
            Success(detections, [], stats, telemetry));

        Assert.Equal(250, presentation.VisibleDetections.Count);
        Assert.Equal("Befund 0", presentation.VisibleDetections[0].Label);
        Assert.Equal("Befund 249", presentation.VisibleDetections[^1].Label);
        Assert.Equal(77, vm.FramesAnalyzed);
        Assert.Equal(251, vm.DetectionCount);
        Assert.Equal(5, vm.HighConfidenceCount);
        Assert.Equal(251, vm.PillarDetectionCount);
        Assert.Equal(6, vm.PillarQuantCount);
        Assert.Equal(1, vm.PillarLocalCount);
        Assert.Equal(
            "Frames: 77, Detections: 999, Entries: 44, HighConf: 5",
            vm.StatsText);
        Assert.Equal(PipelineTelemetryFormatter.Format(telemetry), vm.TelemetryText);
        Assert.False(vm.IsDone);
        Assert.True(vm.HasError);
        Assert.Equal("alter Fehler", vm.ErrorText);
        Assert.Equal("alter Status", vm.StatusText);
        Assert.Equal("alte Phase", vm.PhaseLabel);
    }

    [Fact]
    public void Apply_bevorzugt_gemappte_Eintraege_fuer_das_Radar()
    {
        var vm = new VideoAnalysisPipelineViewModel();
        var raw = new[] { Raw("Nur Rohdaten") };
        var mapped = Enumerable.Range(0, 251)
            .Select(index => Mapped(index))
            .ToArray();

        var presentation = PipelineResultPresenter.ApplySuccessful(
            vm,
            Success(raw, mapped, stats: null, telemetry: null));

        Assert.Equal(250, presentation.VisibleDetections.Count);
        Assert.Equal("MAP-0", presentation.VisibleDetections[0].Code);
        Assert.Equal(mapped[0].EntryId, presentation.VisibleDetections[0].EntryId);
        Assert.True(presentation.VisibleDetections[0].IsSelected);
        Assert.Equal("MAP-249", presentation.VisibleDetections[^1].Code);
        Assert.Equal(1, vm.DetectionCount);
        Assert.Equal(1, vm.PillarDetectionCount);
    }

    [Fact]
    public void Apply_ohne_Statistik_Telemetrie_oder_Listen_bleibt_robust()
    {
        var vm = new VideoAnalysisPipelineViewModel
        {
            StatsText = "alt",
            TelemetryText = "alt"
        };
        var result = new PipelineResult(
            Document: null,
            Detections: null!,
            MappedEntries: null!,
            Stats: null,
            Warnings: [],
            Error: null,
            Telemetry: null);

        var presentation = PipelineResultPresenter.ApplySuccessful(vm, result);

        Assert.Empty(presentation.VisibleDetections);
        Assert.Equal(0, vm.FramesAnalyzed);
        Assert.Equal(0, vm.DetectionCount);
        Assert.Equal(0, vm.HighConfidenceCount);
        Assert.Equal(0, vm.PillarDetectionCount);
        Assert.Equal(0, vm.PillarQuantCount);
        Assert.Equal(0, vm.PillarLocalCount);
        Assert.Equal(string.Empty, vm.StatsText);
        Assert.Equal(string.Empty, vm.TelemetryText);
    }

    private static PipelineResult Success(
        IReadOnlyList<RawVideoDetection> detections,
        IReadOnlyList<MappedProtocolEntry> mapped,
        PipelineStats? stats,
        TelemetrySummary? telemetry)
        => new(
            Document: null,
            Detections: detections,
            MappedEntries: mapped,
            Stats: stats,
            Warnings: [],
            Error: null,
            Telemetry: telemetry);

    private static RawVideoDetection Raw(
        string label,
        int? heightMm = null,
        int? widthMm = null,
        int? intrusionPercent = null,
        int? crossSectionReductionPercent = null,
        int? diameterReductionMm = null,
        int? extentPercent = null,
        string? positionClock = null)
        => new(
            FindingLabel: label,
            MeterStart: 1,
            MeterEnd: 2,
            Severity: "mid",
            PositionClock: positionClock,
            ExtentPercent: extentPercent,
            HeightMm: heightMm,
            WidthMm: widthMm,
            IntrusionPercent: intrusionPercent,
            CrossSectionReductionPercent: crossSectionReductionPercent,
            DiameterReductionMm: diameterReductionMm);

    private static MappedProtocolEntry Mapped(int index)
        => new(
            Detection: Raw($"Gemappt {index}"),
            SuggestedCode: $"MAP-{index}",
            Confidence: 0.9,
            Reason: "Test",
            Warnings: [],
            Freigabe: new AiDecision(AiDecisionOutcome.AutoAccept, "Sicher"),
            EntryId: Guid.NewGuid());

    private static TelemetrySummary Telemetry(int totalFrames, long wallClockMs)
    {
        var empty = new PhaseStat(0, 0, 0, 0);
        return new TelemetrySummary(
            TotalFrames: totalFrames,
            SkippedFrames: 0,
            Extraction: empty,
            Yolo: empty,
            Dino: empty,
            Sam: empty,
            Qwen: empty,
            Total: empty,
            WallClockMs: wallClockMs);
    }
}
