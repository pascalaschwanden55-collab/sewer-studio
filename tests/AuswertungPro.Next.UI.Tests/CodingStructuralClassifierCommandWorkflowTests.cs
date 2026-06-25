using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStructuralClassifierCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_view_events()
    {
        var result = CodingStructuralClassifierCommandWorkflow.Execute(
            Request(viewEvents: null, codingSessionService: new RecordingCodingSessionService()),
            NoActions());

        Assert.Equal(CodingStructuralClassifierCommandOutcome.Skipped, result.Outcome);
        Assert.Null(result.Result);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Execute_skips_without_session_service()
    {
        var result = CodingStructuralClassifierCommandWorkflow.Execute(
            Request(viewEvents: [], codingSessionService: null),
            NoActions());

        Assert.Equal(CodingStructuralClassifierCommandOutcome.Skipped, result.Outcome);
        Assert.Null(result.Result);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Execute_resolves_meter_and_delegates_with_current_video_time()
    {
        var calls = new List<string>();
        var mmResult = Result("BCA");
        var viewEvents = new List<CodingEvent>();
        var sessionService = new RecordingCodingSessionService();
        CodingStructuralClassifierResultWorkflowRequest? delegated = null;
        var workflowResult = new CodingStructuralClassifierResultWorkflowResult(
            CodingStructuralClassifierResultWorkflowOutcome.Added);

        var result = CodingStructuralClassifierCommandWorkflow.Execute(
            new CodingStructuralClassifierCommandRequest(
                Result: mmResult,
                CaptureTimestampSeconds: 4.5,
                FrameOsdMeter: 7.25,
                CurrentVideoTime: TimeSpan.FromSeconds(21),
                FallbackVideoTime: TimeSpan.FromSeconds(99),
                ViewEvents: viewEvents,
                CodingSessionService: sessionService,
                MeterFromOsd: true),
            new CodingStructuralClassifierCommandActions(
                ResolveMeterForFrame: (timestamp, osdMeter) =>
                {
                    calls.Add("resolve");
                    Assert.Equal(4.5, timestamp);
                    Assert.Equal(7.25, osdMeter);
                    return 12.3;
                },
                ExecuteResultWorkflow: request =>
                {
                    calls.Add("execute");
                    delegated = request;
                    return workflowResult;
                }));

        Assert.Equal(CodingStructuralClassifierCommandOutcome.Executed, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["resolve", "execute"], calls);
        Assert.Equal(12.3, result.Meter);
        Assert.Equal(TimeSpan.FromSeconds(21), result.VideoTime);
        Assert.Same(workflowResult, result.Result);
        Assert.NotNull(delegated);
        Assert.Same(mmResult, delegated.Result);
        Assert.Equal(12.3, delegated.Meter);
        Assert.Equal(TimeSpan.FromSeconds(21), delegated.VideoTime);
        Assert.Same(viewEvents, delegated.ViewEvents);
        Assert.Same(sessionService, delegated.CodingSessionService);
        Assert.True(delegated.MeterFromOsd);
    }

    [Fact]
    public void Execute_uses_capture_timestamp_when_current_video_time_is_missing()
    {
        var result = CodingStructuralClassifierCommandWorkflow.Execute(
            new CodingStructuralClassifierCommandRequest(
                Result: Result("BCA"),
                CaptureTimestampSeconds: 6.75,
                FrameOsdMeter: null,
                CurrentVideoTime: null,
                FallbackVideoTime: TimeSpan.FromSeconds(6.75),
                ViewEvents: [],
                CodingSessionService: new RecordingCodingSessionService(),
                MeterFromOsd: false),
            new CodingStructuralClassifierCommandActions(
                ResolveMeterForFrame: (_, _) => 3.5,
                ExecuteResultWorkflow: _ => new CodingStructuralClassifierResultWorkflowResult(
                    CodingStructuralClassifierResultWorkflowOutcome.NotHandled)));

        Assert.Equal(CodingStructuralClassifierCommandOutcome.Executed, result.Outcome);
        Assert.False(result.Handled);
        Assert.Equal(3.5, result.Meter);
        Assert.Equal(TimeSpan.FromSeconds(6.75), result.VideoTime);
    }

    private static CodingStructuralClassifierCommandRequest Request(
        IReadOnlyList<CodingEvent>? viewEvents = null,
        ICodingSessionService? codingSessionService = null,
        double captureTimestampSeconds = 1,
        TimeSpan? currentVideoTime = null,
        TimeSpan? fallbackVideoTime = null)
        => new(
            Result: Result("BCA"),
            CaptureTimestampSeconds: captureTimestampSeconds,
            FrameOsdMeter: null,
            CurrentVideoTime: currentVideoTime ?? TimeSpan.FromSeconds(8),
            FallbackVideoTime: fallbackVideoTime ?? TimeSpan.FromSeconds(captureTimestampSeconds),
            ViewEvents: viewEvents,
            CodingSessionService: codingSessionService,
            MeterFromOsd: false);

    private static CodingStructuralClassifierCommandActions NoActions()
        => new(
            ResolveMeterForFrame: (_, _) => throw new InvalidOperationException("Meter should not be resolved."),
            ExecuteResultWorkflow: _ => throw new InvalidOperationException("Workflow should not run."));

    private static SingleFrameResult Result(string? code)
        => new(
            IsRelevant: true,
            DinoDetections: [],
            SamResponse: null,
            QuantifiedMasks: [],
            YoloTimeMs: 0,
            DinoTimeMs: 0,
            SamTimeMs: 0,
            Error: null,
            ClassifierCode: code,
            ClassifierConfidence: 0.8);

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
