using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelFindingEventWorkflowTests
{
    [Fact]
    public void Execute_adds_segment_and_runs_post_actions_when_event_was_added()
    {
        var calls = new List<string>();
        ProtocolEntry? addedEntry = null;
        var sessionService = new RecordingCodingSessionService(entry =>
        {
            calls.Add("add");
            addedEntry = entry;
            return new CodingEvent { Entry = entry, MeterAtCapture = entry.MeterStart ?? 0 };
        });

        var result = CodingMultiModelFindingEventWorkflow.Execute(
            new CodingMultiModelFindingEventWorkflowRequest(
                Segmented: [Segmented("connection")],
                StretchConsumed: [],
                Meter: 6.4,
                VideoTime: TimeSpan.FromSeconds(14),
                ImageWidth: 100,
                ImageHeight: 100,
                YoloMaxConfidence: 0.91,
                CodingSessionService: sessionService,
                ViewEvents: [],
                QualityGate: null,
                MeterFromOsd: true,
                Calibration: CalibratedPipe(),
                CodeSelectionCatalog: null),
            new CodingMultiModelFindingEventWorkflowActions(
                ResolveFindingCodeForCoding: (finding, meter) =>
                {
                    calls.Add("resolve");
                    Assert.Equal(6.4, meter);
                    Assert.Equal("connection", finding.Label);
                    return "BCAEB";
                },
                LookupVsaLabel: code => code == "BCAEB" ? "Anschluss" : null,
                AttachAnalyzedFramePhoto: entry => calls.Add("attach"),
                Trace: message => calls.Add("trace:" + message),
                RefreshEvents: () => calls.Add("refresh"),
                UpdateToolBadge: () => calls.Add("badge")));

        Assert.Equal(["resolve", "attach", "add", "refresh", "badge"], calls);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.CoveredCount);
        Assert.Equal(0, result.StretchConsumedCount);
        Assert.NotNull(addedEntry);
        Assert.Equal("BCAEB", addedEntry.Code);
        Assert.Equal("Anschluss", addedEntry.Beschreibung);
        Assert.Equal(6.4, addedEntry.MeterStart);
        Assert.Equal(TimeSpan.FromSeconds(14), addedEntry.Zeit);
    }

    [Fact]
    public void Execute_skips_stretch_consumed_segments_before_resolving_code()
    {
        var consumed = Segmented("crack");

        var result = CodingMultiModelFindingEventWorkflow.Execute(
            new CodingMultiModelFindingEventWorkflowRequest(
                Segmented: [consumed],
                StretchConsumed: [consumed],
                Meter: 2,
                VideoTime: TimeSpan.Zero,
                ImageWidth: 100,
                ImageHeight: 100,
                YoloMaxConfidence: null,
                CodingSessionService: new RecordingCodingSessionService(_ => throw new InvalidOperationException("No event should be added.")),
                ViewEvents: [],
                QualityGate: null,
                MeterFromOsd: true,
                Calibration: null,
                CodeSelectionCatalog: null),
            NoPostActions());

        Assert.Equal(0, result.AddedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.CoveredCount);
        Assert.Equal(1, result.StretchConsumedCount);
    }

    private static CodingMultiModelFindingEventWorkflowActions NoPostActions()
        => new(
            ResolveFindingCodeForCoding: (_, _) => throw new InvalidOperationException("No code should be resolved."),
            LookupVsaLabel: _ => throw new InvalidOperationException("No label should be resolved."),
            AttachAnalyzedFramePhoto: _ => throw new InvalidOperationException("No photo should be attached."),
            Trace: _ => throw new InvalidOperationException("No trace should be written."),
            RefreshEvents: () => throw new InvalidOperationException("No refresh should run."),
            UpdateToolBadge: () => throw new InvalidOperationException("No badge update should run."));

    private static SegmentedFinding Segmented(string label)
    {
        var mask = new SamMaskResult(
            label,
            0.87,
            [70, 40, 90, 60],
            "mask-rle",
            MaskAreaPixels: 100,
            ImageAreaPixels: 10_000,
            HeightPixels: 40,
            WidthPixels: 60,
            CentroidX: 40,
            CentroidY: 40);

        var quant = new MaskQuantificationService.QuantifiedMask(
            Label: label,
            Confidence: 0.87,
            HeightMm: 12,
            WidthMm: 8,
            ExtentPercent: 4,
            CrossSectionReductionPercent: null,
            IntrusionPercent: null,
            ClockPosition: "3:00");

        var proximity = new MetrierungProximityResult(
            MetrierungProximity.Codierbar,
            "test",
            FillRatio: 0,
            DistToVanish: 0,
            OuterRadius: 0,
            WandNaehe: true,
            EnthaeltCenter: false);

        return new SegmentedFinding(Dino: null, mask, quant, proximity);
    }

    private static PipeCalibration CalibratedPipe()
        => new()
        {
            NominalDiameterMm = 300,
            NormalizedDiameter = 0.7,
            Source = CalibrationSource.Auto
        };

    private sealed class RecordingCodingSessionService(Func<ProtocolEntry, CodingEvent> addEvent) : ICodingSessionService
    {
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => [];

        public event EventHandler<CodingSessionState>? StateChanged { add { } remove { } }
        public event EventHandler<double>? MeterChanged { add { } remove { } }
        public event EventHandler<CodingEvent>? EventAdded { add { } remove { } }

        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => new();
        public void PauseSession() { }
        public void ResumeSession() { }
        public void SetWaitingForInput() { }
        public void AbortSession(string reason) { }
        public ProtocolDocument CompleteSession() => new();
        public void MoveNext(double stepSizeM = 0.5) { }
        public void MovePrevious(double stepSizeM = 0.5) { }
        public void MoveToMeter(double meter) { }
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => addEvent(entry);
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
