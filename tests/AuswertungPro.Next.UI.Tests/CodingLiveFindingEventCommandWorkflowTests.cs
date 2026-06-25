using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveFindingEventCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_view_model()
    {
        var result = CodingLiveFindingEventCommandWorkflow.Execute(
            new CodingLiveFindingEventCommandRequest(
                HasCodingViewModel: false,
                Result: Detection(),
                ValidFindings: [],
                CodingSessionService: new RecordingCodingSessionService(),
                ViewEvents: [],
                QualityGate: null,
                CurrentVideoTime: null,
                FallbackVideoTime: TimeSpan.FromSeconds(8)),
            NoActions());

        Assert.Equal(CodingLiveFindingEventCommandOutcome.Skipped, result.Outcome);
        Assert.Null(result.EventResult);
    }

    [Fact]
    public void Execute_skips_without_session_service()
    {
        var result = CodingLiveFindingEventCommandWorkflow.Execute(
            new CodingLiveFindingEventCommandRequest(
                HasCodingViewModel: true,
                Result: Detection(),
                ValidFindings: [],
                CodingSessionService: null,
                ViewEvents: [],
                QualityGate: null,
                CurrentVideoTime: null,
                FallbackVideoTime: TimeSpan.FromSeconds(8)),
            NoActions());

        Assert.Equal(CodingLiveFindingEventCommandOutcome.Skipped, result.Outcome);
        Assert.Null(result.EventResult);
    }

    [Fact]
    public void Execute_resolves_meter_and_delegates_with_current_video_time()
    {
        var calls = new List<string>();
        var finding = new LiveFrameFinding("Riss", 4, null, null, "BAB");
        var events = new List<CodingEvent>();
        var sessionService = new RecordingCodingSessionService();
        CodingLiveFindingEventWorkflowRequest? delegated = null;

        var eventResult = new CodingLiveFindingEventWorkflowResult(
            AddedCount: 1,
            SkippedCount: 0,
            CoveredCount: 0,
            ConfirmationEvent: null,
            ConfirmationGate: null);

        var result = CodingLiveFindingEventCommandWorkflow.Execute(
            new CodingLiveFindingEventCommandRequest(
                HasCodingViewModel: true,
                Result: new LiveDetection(4.5, [finding], 7.25, Error: null),
                ValidFindings: [finding],
                CodingSessionService: sessionService,
                ViewEvents: events,
                QualityGate: null,
                CurrentVideoTime: TimeSpan.FromSeconds(21),
                FallbackVideoTime: TimeSpan.FromSeconds(99)),
            new CodingLiveFindingEventCommandActions(
                ResolveMeterForFrame: (timestamp, osdMeter) =>
                {
                    calls.Add("resolve");
                    Assert.Equal(4.5, timestamp);
                    Assert.Equal(7.25, osdMeter);
                    return 12.3;
                },
                ExecuteFindingWorkflow: request =>
                {
                    calls.Add("execute");
                    delegated = request;
                    return eventResult;
                }));

        Assert.Equal(CodingLiveFindingEventCommandOutcome.Executed, result.Outcome);
        Assert.Equal(["resolve", "execute"], calls);
        Assert.Equal(12.3, result.Meter);
        Assert.Equal(TimeSpan.FromSeconds(21), result.VideoTime);
        Assert.Same(eventResult, result.EventResult);
        Assert.NotNull(delegated);
        Assert.Equal(12.3, delegated.Meter);
        Assert.Equal(TimeSpan.FromSeconds(21), delegated.VideoTime);
        Assert.Same(sessionService, delegated.CodingSessionService);
        Assert.Same(events, delegated.ViewEvents);
        Assert.Equal([finding], delegated.ValidFindings);
    }

    [Fact]
    public void Execute_uses_fallback_video_time_when_session_time_is_missing()
    {
        var result = CodingLiveFindingEventCommandWorkflow.Execute(
            new CodingLiveFindingEventCommandRequest(
                HasCodingViewModel: true,
                Result: Detection(timestampSeconds: 6, meterReading: null),
                ValidFindings: [],
                CodingSessionService: new RecordingCodingSessionService(),
                ViewEvents: [],
                QualityGate: null,
                CurrentVideoTime: null,
                FallbackVideoTime: TimeSpan.FromSeconds(13)),
            new CodingLiveFindingEventCommandActions(
                ResolveMeterForFrame: (_, _) => 3.5,
                ExecuteFindingWorkflow: _ => new CodingLiveFindingEventWorkflowResult(0, 0, 0, null, null)));

        Assert.Equal(CodingLiveFindingEventCommandOutcome.Executed, result.Outcome);
        Assert.Equal(3.5, result.Meter);
        Assert.Equal(TimeSpan.FromSeconds(13), result.VideoTime);
    }

    private static CodingLiveFindingEventCommandActions NoActions()
        => new(
            ResolveMeterForFrame: (_, _) => throw new InvalidOperationException("Meter should not be resolved."),
            ExecuteFindingWorkflow: _ => throw new InvalidOperationException("Findings should not be executed."));

    private static LiveDetection Detection(double timestampSeconds = 1, double? meterReading = null)
        => new(timestampSeconds, [], meterReading, Error: null);

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
