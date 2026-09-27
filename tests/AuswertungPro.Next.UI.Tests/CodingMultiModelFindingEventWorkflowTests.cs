using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
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

    [Fact]
    public void Dino_only_mit_gleichem_Code_und_nahem_Meter_erzeugt_keine_zweite_Zeile()
    {
        var service = new SessionCodingSessionService();
        var actions = SessionActions("BAI");

        var first = Execute(service, actions, SegmentedWithOrigin(Dino(0, 0, 20, 20)), 5, 10);
        var second = Execute(service, actions, SegmentedWithOrigin(Dino(80, 80, 100, 100)), 5.2, 15);

        Assert.Equal(1, first.AddedCount);
        Assert.Equal(0, second.AddedCount);
        Assert.Equal(1, second.CoveredCount);
        Assert.Single(service.Events);
    }

    [Fact]
    public void Modellgebundenes_Yolo_darf_bei_raumgetrennter_Box_zweite_Zeile_erzeugen()
    {
        var previousCatalog = VsaCodeResolver.CurrentCatalog;
        try
        {
            VsaCodeResolver.ConfigureCatalog(new TestCatalog());
            var service = new SessionCodingSessionService();
            var actions = SessionActions("BAI");

            var first = Execute(service, actions, SegmentedWithOrigin(Yolo(0, 0, 20, 20)), 5, 10);
            var second = Execute(service, actions, SegmentedWithOrigin(Yolo(80, 80, 100, 100)), 5.2, 15);

            Assert.Equal(1, first.AddedCount);
            Assert.Equal(1, second.AddedCount);
            Assert.Equal(0, second.CoveredCount);
            Assert.Equal(2, service.Events.Count);
        }
        finally
        {
            VsaCodeResolver.ConfigureCatalog(previousCatalog);
        }
    }

    private static CodingMultiModelFindingEventWorkflowResult Execute(
        SessionCodingSessionService service,
        CodingMultiModelFindingEventWorkflowActions actions,
        SegmentedFinding finding,
        double meter,
        double seconds)
        => CodingMultiModelFindingEventWorkflow.Execute(
            new CodingMultiModelFindingEventWorkflowRequest(
                Segmented: [finding],
                StretchConsumed: [],
                Meter: meter,
                VideoTime: TimeSpan.FromSeconds(seconds),
                ImageWidth: 100,
                ImageHeight: 100,
                YoloMaxConfidence: finding.Origin?.YoloConfidence,
                CodingSessionService: service,
                ViewEvents: service.Events,
                QualityGate: null,
                MeterFromOsd: true,
                Calibration: null,
                CodeSelectionCatalog: null),
            actions);

    private static CodingMultiModelFindingEventWorkflowActions SessionActions(string code)
        => new(
            ResolveFindingCodeForCoding: (_, _) => code,
            LookupVsaLabel: _ => code,
            AttachAnalyzedFramePhoto: entry => entry.FotoPaths.Add($"frame-{entry.Zeit?.TotalSeconds}.png"),
            Trace: _ => { },
            RefreshEvents: () => { },
            UpdateToolBadge: () => { });

    private static CodingLocalizedDetection Dino(double x1, double y1, double x2, double y2)
        => new(CodingDetectionSource.Dino, "pipe defect", x1, y1, x2, y2, DinoConfidence: 0.5);

    private static CodingLocalizedDetection Yolo(double x1, double y1, double x2, double y2)
        => new(CodingDetectionSource.Yolo, "BAI_dichtung", x1, y1, x2, y2,
            YoloConfidence: 0.5,
            YoloArtifactSha256: new string('a', 64),
            VsaMainCode: "BAI",
            YoloModelName: "test-yolo");

    private static SegmentedFinding SegmentedWithOrigin(CodingLocalizedDetection origin)
    {
        var baseFinding = Segmented(origin.Label);
        return baseFinding with
        {
            Mask = baseFinding.Mask with { Bbox = [origin.X1, origin.Y1, origin.X2, origin.Y2] },
            Origin = origin
        };
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

    private sealed class SessionCodingSessionService : ICodingSessionService
    {
        private readonly CodingSession _session = new();

        public double CurrentMeter => 0;
        public double EndMeter => 20;
        public double ProgressPercent => 0;
        public CodingSession ActiveSession => _session;
        public IReadOnlyList<CodingEvent> Events => _session.Events;

        public event EventHandler<CodingSessionState>? StateChanged { add { } remove { } }
        public event EventHandler<double>? MeterChanged { add { } remove { } }
        public event EventHandler<CodingEvent>? EventAdded { add { } remove { } }

        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => _session;
        public void PauseSession() { }
        public void ResumeSession() { }
        public void SetWaitingForInput() { }
        public void AbortSession(string reason) { }
        public ProtocolDocument CompleteSession() => new();
        public void MoveNext(double stepSizeM = 0.5) { }
        public void MovePrevious(double stepSizeM = 0.5) { }
        public void MoveToMeter(double meter) { }
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null)
        {
            var codingEvent = new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero,
                Overlay = overlay
            };
            _session.Events.Add(codingEvent);
            return codingEvent;
        }
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }
        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class TestCatalog : ICodeCatalogProvider
    {
        private static readonly CodeDefinition Code = new()
        {
            Code = "BAI",
            Title = "Einragendes Dichtungsmaterial",
            IsSelectable = true
        };

        public IReadOnlyList<CodeDefinition> GetAll() => [Code];
        public bool TryGet(string code, out CodeDefinition definition)
        {
            definition = Code;
            return string.Equals(code, Code.Code, StringComparison.OrdinalIgnoreCase);
        }
        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new InvalidOperationException();
        public IReadOnlyList<string> AllowedCodes() => [Code.Code];
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }
}
