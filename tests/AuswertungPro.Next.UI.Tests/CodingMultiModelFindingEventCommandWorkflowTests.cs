using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelFindingEventCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_view_model()
    {
        var result = CodingMultiModelFindingEventCommandWorkflow.Execute(
            Request(hasCodingViewModel: false, codingSessionService: new RecordingCodingSessionService()),
            NoActions());

        Assert.Equal(CodingMultiModelFindingEventCommandOutcome.Skipped, result.Outcome);
        Assert.Null(result.EventResult);
    }

    [Fact]
    public void Execute_skips_without_session_service()
    {
        var result = CodingMultiModelFindingEventCommandWorkflow.Execute(
            new CodingMultiModelFindingEventCommandRequest(
                HasCodingViewModel: true,
                Segmented: [],
                ImageWidth: 100,
                ImageHeight: 100,
                YoloMaxConfidence: null,
                CaptureTimestampSeconds: 1,
                FrameOsdMeter: null,
                CodingSessionService: null,
                ViewEvents: [],
                QualityGate: null,
                MeterFromOsd: false,
                Calibration: null,
                CodeSelectionCatalog: null,
                CurrentVideoTime: TimeSpan.FromSeconds(8),
                FallbackVideoTime: TimeSpan.FromSeconds(9)),
            NoActions());

        Assert.Equal(CodingMultiModelFindingEventCommandOutcome.Skipped, result.Outcome);
        Assert.Null(result.EventResult);
    }

    [Fact]
    public void Execute_resolves_meter_tracks_stretch_damage_and_delegates_with_current_video_time()
    {
        var calls = new List<string>();
        var segmented = Array.Empty<SegmentedFinding>();
        var stretchConsumed = Array.Empty<SegmentedFinding>();
        var viewEvents = new List<CodingEvent>();
        var sessionService = new RecordingCodingSessionService();
        CodingMultiModelFindingEventWorkflowRequest? delegated = null;
        var eventResult = new CodingMultiModelFindingEventWorkflowResult(
            AddedCount: 1,
            SkippedCount: 0,
            CoveredCount: 0,
            StretchConsumedCount: 0);

        var result = CodingMultiModelFindingEventCommandWorkflow.Execute(
            new CodingMultiModelFindingEventCommandRequest(
                HasCodingViewModel: true,
                Segmented: segmented,
                ImageWidth: 640,
                ImageHeight: 480,
                YoloMaxConfidence: 0.91,
                CaptureTimestampSeconds: 4.5,
                FrameOsdMeter: 7.25,
                CodingSessionService: sessionService,
                ViewEvents: viewEvents,
                QualityGate: null,
                MeterFromOsd: true,
                Calibration: null,
                CodeSelectionCatalog: null,
                CurrentVideoTime: TimeSpan.FromSeconds(21),
                FallbackVideoTime: TimeSpan.FromSeconds(99)),
            new CodingMultiModelFindingEventCommandActions(
                ResolveMeterForFrame: (timestamp, osdMeter) =>
                {
                    calls.Add("resolve");
                    Assert.Equal(4.5, timestamp);
                    Assert.Equal(7.25, osdMeter);
                    return 12.3;
                },
                ApplyStretchTracking: (actualSegmented, meter, videoTime) =>
                {
                    calls.Add("stretch");
                    Assert.Same(segmented, actualSegmented);
                    Assert.Equal(12.3, meter);
                    Assert.Equal(TimeSpan.FromSeconds(21), videoTime);
                    return stretchConsumed;
                },
                ExecuteFindingWorkflow: request =>
                {
                    calls.Add("execute");
                    delegated = request;
                    return eventResult;
                }));

        Assert.Equal(CodingMultiModelFindingEventCommandOutcome.Executed, result.Outcome);
        Assert.Equal(["resolve", "stretch", "execute"], calls);
        Assert.Equal(12.3, result.Meter);
        Assert.Equal(TimeSpan.FromSeconds(21), result.VideoTime);
        Assert.Same(eventResult, result.EventResult);
        Assert.NotNull(delegated);
        Assert.Same(segmented, delegated.Segmented);
        Assert.Same(stretchConsumed, delegated.StretchConsumed);
        Assert.Equal(12.3, delegated.Meter);
        Assert.Equal(TimeSpan.FromSeconds(21), delegated.VideoTime);
        Assert.Equal(640, delegated.ImageWidth);
        Assert.Equal(480, delegated.ImageHeight);
        Assert.Equal(0.91, delegated.YoloMaxConfidence);
        Assert.Same(sessionService, delegated.CodingSessionService);
        Assert.Same(viewEvents, delegated.ViewEvents);
        Assert.True(delegated.MeterFromOsd);
    }

    [Fact]
    public void Execute_uses_fallback_video_time_when_session_time_is_missing()
    {
        var result = CodingMultiModelFindingEventCommandWorkflow.Execute(
            new CodingMultiModelFindingEventCommandRequest(
                HasCodingViewModel: true,
                Segmented: [],
                ImageWidth: 100,
                ImageHeight: 100,
                YoloMaxConfidence: null,
                CaptureTimestampSeconds: 1,
                FrameOsdMeter: null,
                CodingSessionService: new RecordingCodingSessionService(),
                ViewEvents: [],
                QualityGate: null,
                MeterFromOsd: false,
                Calibration: null,
                CodeSelectionCatalog: null,
                CurrentVideoTime: null,
                FallbackVideoTime: TimeSpan.FromSeconds(13)),
            new CodingMultiModelFindingEventCommandActions(
                ResolveMeterForFrame: (_, _) => 3.5,
                ApplyStretchTracking: (_, _, _) => [],
                ExecuteFindingWorkflow: _ => new CodingMultiModelFindingEventWorkflowResult(0, 0, 0, 0)));

        Assert.Equal(CodingMultiModelFindingEventCommandOutcome.Executed, result.Outcome);
        Assert.Equal(3.5, result.Meter);
        Assert.Equal(TimeSpan.FromSeconds(13), result.VideoTime);
    }

    private static CodingMultiModelFindingEventCommandRequest Request(
        bool hasCodingViewModel = true,
        ICodingSessionService? codingSessionService = null,
        TimeSpan? currentVideoTime = null,
        TimeSpan? fallbackVideoTime = null)
        => new(
            HasCodingViewModel: hasCodingViewModel,
            Segmented: [],
            ImageWidth: 100,
            ImageHeight: 100,
            YoloMaxConfidence: null,
            CaptureTimestampSeconds: 1,
            FrameOsdMeter: null,
            CodingSessionService: codingSessionService ?? new RecordingCodingSessionService(),
            ViewEvents: [],
            QualityGate: null,
            MeterFromOsd: false,
            Calibration: null,
            CodeSelectionCatalog: null,
            CurrentVideoTime: currentVideoTime ?? TimeSpan.FromSeconds(8),
            FallbackVideoTime: fallbackVideoTime ?? TimeSpan.FromSeconds(9));

    private static CodingMultiModelFindingEventCommandActions NoActions()
        => new(
            ResolveMeterForFrame: (_, _) => throw new InvalidOperationException("Meter should not be resolved."),
            ApplyStretchTracking: (_, _, _) => throw new InvalidOperationException("Stretch tracking should not run."),
            ExecuteFindingWorkflow: _ => throw new InvalidOperationException("Findings should not be executed."));

    private sealed class RecordingCodingSessionService : ICodingSessionService
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
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => new();
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
