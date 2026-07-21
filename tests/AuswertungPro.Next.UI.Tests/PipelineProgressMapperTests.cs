using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PipelineProgressMapperTests
{
    [Fact]
    public void Apply_Videoanalyse_uebernimmt_Status_Frame_Befunde_und_Eta()
    {
        var vm = new VideoAnalysisPipelineViewModel
        {
            DetectionCount = 2
        };
        var visibleFindings = new List<LiveFrameFinding>();
        var preview = new DrawingImage();
        var decodedBytes = Array.Empty<byte>();
        var eta = new RecordingEtaCalculator(new EtaErgebnis(TimeSpan.FromSeconds(252), 12.37));
        var findings = Enumerable.Range(1, 9)
            .Select(index => new LiveFrameFinding(
                $"Befund {index}",
                Severity: index,
                PositionClock: index <= 3 ? index.ToString() : null,
                ExtentPercent: index <= 4 ? index * 10 : null,
                HeightMm: index <= 2 ? index : null))
            .ToArray();
        var mapper = new PipelineProgressMapper(
            vm,
            visibleFindings,
            bytes =>
            {
                decodedBytes = bytes;
                return preview;
            },
            eta,
            () => TimeSpan.FromSeconds(11));

        var effects = mapper.Apply(new PipelineProgress(
            PipelinePhase.VideoAnalysis,
            PercentInPhase: 120,
            Status: "Frame @ 12.5m - 7 Befunde",
            FramesDone: 42,
            FramesTotal: 200,
            FramePreviewPng: [1, 2, 3],
            LiveFindings: findings));

        Assert.Equal("Videoanalyse", vm.PhaseLabel);
        Assert.Equal("Frame @ 12.5m - 7 Befunde", vm.StatusText);
        Assert.True(vm.VideoPhaseActive);
        Assert.False(vm.VideoPhaseDone);
        Assert.False(vm.MappingPhaseDone);
        Assert.False(vm.IsMultiModelActive);
        Assert.Equal(100, vm.VideoProgressPct);
        Assert.Equal(0, vm.MappingProgressPct);
        Assert.Equal(42, vm.FramesAnalyzed);
        Assert.Equal(200, vm.TotalFrames);
        Assert.Equal("12.5 m", vm.CurrentMeter);
        Assert.Equal(7, vm.DetectionCount);
        Assert.Equal("Frame 42/200  |  Meter 12.5 m", vm.LiveFrameInfo);
        Assert.Equal("Frame @ 12.5m - 7 Befunde", vm.LiveFrameStatus);
        Assert.Same(preview, vm.LiveFrameImage);
        Assert.Equal([1, 2, 3], decodedBytes);
        Assert.Equal(findings.Take(8), visibleFindings);
        Assert.Equal(8, vm.PillarDetectionCount);
        Assert.Equal(4, vm.PillarQuantCount);
        Assert.Equal(3, vm.PillarLocalCount);
        Assert.StartsWith("Q: ", vm.LiveFrameQuantSummary);
        Assert.NotNull(eta.LastCall);
        Assert.Equal((42L, 200L, TimeSpan.FromSeconds(11)), eta.LastCall.Value);
        Assert.Equal("12.4 Frames/s · Rest ~ 04:12", vm.EtaText);
        Assert.True(effects.RenderLiveFrameOverlay);
        Assert.True(effects.ForwardLiveFrame);
    }

    [Fact]
    public void Apply_MultiModel_liest_Gesamtframes_und_leitet_auch_ohne_Bild_weiter()
    {
        var vm = new VideoAnalysisPipelineViewModel();
        var visibleFindings = new List<LiveFrameFinding>();
        var mapper = CreateMapper(vm, visibleFindings);

        var effects = mapper.Apply(new PipelineProgress(
            PipelinePhase.MultiModelDetection,
            PercentInPhase: -5,
            Status: "YOLO: 38 gesamt"));

        Assert.Equal("Multi-Model Pipeline", vm.PhaseLabel);
        Assert.True(vm.VideoPhaseActive);
        Assert.True(vm.IsMultiModelActive);
        Assert.Equal(38, vm.YoloSkippedFrames);
        Assert.Equal(0, vm.VideoProgressPct);
        Assert.False(effects.RenderLiveFrameOverlay);
        Assert.True(effects.ForwardLiveFrame);
    }

    [Fact]
    public void Apply_CodeMapping_bewahrt_hoehere_Befundzahl_und_aktualisiert_nur_Mapping()
    {
        var vm = new VideoAnalysisPipelineViewModel
        {
            FramesAnalyzed = 42,
            TotalFrames = 200,
            CurrentMeter = "12.5 m",
            DetectionCount = 9
        };
        var mapper = CreateMapper(vm, []);

        var effects = mapper.Apply(new PipelineProgress(
            PipelinePhase.CodeMapping,
            PercentInPhase: 125,
            Status: "Codes werden zugeordnet",
            ItemsDone: 3));

        Assert.Equal("Code-Mapping", vm.PhaseLabel);
        Assert.False(vm.VideoPhaseActive);
        Assert.True(vm.VideoPhaseDone);
        Assert.False(vm.MappingPhaseDone);
        Assert.False(vm.IsMultiModelActive);
        Assert.Equal(100, vm.VideoProgressPct);
        Assert.Equal(100, vm.MappingProgressPct);
        Assert.Equal(9, vm.DetectionCount);
        Assert.Equal("Codes werden zugeordnet", vm.LiveFrameStatus);
        Assert.Equal("Frame 42/200  |  Meter 12.5 m", vm.LiveFrameInfo);
        Assert.False(effects.RenderLiveFrameOverlay);
        Assert.False(effects.ForwardLiveFrame);
    }

    [Fact]
    public void Apply_Done_markiert_beide_Strecken_als_fertig()
    {
        var vm = new VideoAnalysisPipelineViewModel
        {
            FramesAnalyzed = 42,
            TotalFrames = 200
        };
        var visibleFindings = new List<LiveFrameFinding>
        {
            new("Riss", 4, "3", 20)
        };
        var mapper = CreateMapper(vm, visibleFindings);

        var effects = mapper.Apply(new PipelineProgress(
            PipelinePhase.Done,
            PercentInPhase: 0,
            Status: "Pipeline fertig"));

        Assert.Equal("Fertig", vm.PhaseLabel);
        Assert.Equal("Pipeline fertig", vm.StatusText);
        Assert.False(vm.VideoPhaseActive);
        Assert.True(vm.VideoPhaseDone);
        Assert.True(vm.MappingPhaseDone);
        Assert.Equal(100, vm.VideoProgressPct);
        Assert.Equal(100, vm.MappingProgressPct);
        Assert.Equal("Analyse abgeschlossen", vm.LiveFrameStatus);
        Assert.StartsWith("Q: ", vm.LiveFrameQuantSummary);
        Assert.False(effects.RenderLiveFrameOverlay);
        Assert.False(effects.ForwardLiveFrame);
    }

    [Fact]
    public void Apply_negative_Framewerte_werden_auf_Null_begrenzt()
    {
        var vm = new VideoAnalysisPipelineViewModel();
        var mapper = CreateMapper(vm, []);

        mapper.Apply(new PipelineProgress(
            PipelinePhase.VideoAnalysis,
            PercentInPhase: 25,
            Status: "Analyse",
            FramesDone: -2,
            FramesTotal: -9));

        Assert.Equal(0, vm.FramesAnalyzed);
        Assert.Equal(0, vm.TotalFrames);
        Assert.Equal(string.Empty, vm.EtaText);
    }

    [Fact]
    public void Apply_unterscheidet_fehlende_und_bewusst_leere_Befundliste()
    {
        var vm = new VideoAnalysisPipelineViewModel();
        var oldFinding = new LiveFrameFinding("Alter Riss", 4, "3", 20);
        var visibleFindings = new List<LiveFrameFinding> { oldFinding };
        var mapper = CreateMapper(vm, visibleFindings);

        var missingEffects = mapper.Apply(new PipelineProgress(
            PipelinePhase.VideoAnalysis,
            PercentInPhase: 10,
            Status: "Nur Status",
            LiveFindings: null));

        Assert.Equal([oldFinding], visibleFindings);
        Assert.False(missingEffects.RenderLiveFrameOverlay);
        Assert.True(missingEffects.ForwardLiveFrame);

        var emptyEffects = mapper.Apply(new PipelineProgress(
            PipelinePhase.VideoAnalysis,
            PercentInPhase: 20,
            Status: "Keine Befunde",
            LiveFindings: []));

        Assert.Empty(visibleFindings);
        Assert.Equal("Quantifizierung: keine Punkte erkannt", vm.LiveFrameQuantSummary);
        Assert.True(emptyEffects.RenderLiveFrameOverlay);
        Assert.True(emptyEffects.ForwardLiveFrame);
    }

    [Fact]
    public void Apply_Vorschaubild_ohne_Befundliste_fordert_Neuzeichnen_an()
    {
        var vm = new VideoAnalysisPipelineViewModel();
        var preview = new DrawingImage();
        var mapper = new PipelineProgressMapper(
            vm,
            [],
            _ => preview,
            new RecordingEtaCalculator(new EtaErgebnis(null, null)),
            () => TimeSpan.Zero);

        var effects = mapper.Apply(new PipelineProgress(
            PipelinePhase.VideoAnalysis,
            PercentInPhase: 10,
            Status: "Neues Bild",
            FramePreviewPng: [1],
            LiveFindings: null));

        Assert.Same(preview, vm.LiveFrameImage);
        Assert.True(effects.RenderLiveFrameOverlay);
        Assert.True(effects.ForwardLiveFrame);
    }

    [Fact]
    public void Apply_Pillarzaehler_behalten_ihren_hoechsten_EinzelFrame_Wert()
    {
        var vm = new VideoAnalysisPipelineViewModel();
        var mapper = CreateMapper(vm, []);
        var manyFindings = Enumerable.Range(1, 3)
            .Select(index => new LiveFrameFinding(
                $"Befund {index}",
                Severity: 3,
                PositionClock: index.ToString(),
                ExtentPercent: 10))
            .ToArray();

        mapper.Apply(new PipelineProgress(
            PipelinePhase.VideoAnalysis,
            PercentInPhase: 10,
            Status: "Drei Befunde",
            LiveFindings: manyFindings));
        mapper.Apply(new PipelineProgress(
            PipelinePhase.VideoAnalysis,
            PercentInPhase: 20,
            Status: "Ein Befund",
            LiveFindings: [new LiveFrameFinding("Klein", 1, null, null)]));

        Assert.Equal(3, vm.PillarDetectionCount);
        Assert.Equal(3, vm.PillarQuantCount);
        Assert.Equal(3, vm.PillarLocalCount);
    }

    private static PipelineProgressMapper CreateMapper(
        VideoAnalysisPipelineViewModel vm,
        List<LiveFrameFinding> visibleFindings)
        => new(
            vm,
            visibleFindings,
            _ => throw new InvalidOperationException("Dieser Test erwartet kein Vorschaubild."),
            new RecordingEtaCalculator(new EtaErgebnis(null, null)),
            () => TimeSpan.Zero);

    private sealed class RecordingEtaCalculator(EtaErgebnis result) : IEtaCalculator
    {
        public (long Done, long Total, TimeSpan Elapsed)? LastCall { get; private set; }

        public EtaErgebnis MeldeFortschritt(long erledigt, long gesamt, TimeSpan verstrichen)
        {
            LastCall = (erledigt, gesamt, verstrichen);
            return result;
        }
    }
}
