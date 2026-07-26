using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBoundaryEventCommandWorkflowTests
{
    [Fact]
    public async Task EnsureStart_skips_when_coding_session_is_not_ready()
    {
        var result = await CodingBoundaryEventCommandWorkflow.EnsureStartAsync(
            StartRequest(hasCodingViewModel: false, codingSessionService: new RecordingCodingSessionService()),
            NoStartActions());

        Assert.Equal(CodingBoundaryEventCommandOutcome.Skipped, result.Outcome);
        Assert.False(result.Added);

        result = await CodingBoundaryEventCommandWorkflow.EnsureStartAsync(
            StartRequest(hasCodingViewModel: true, useNullCodingSessionService: true),
            NoStartActions());

        Assert.Equal(CodingBoundaryEventCommandOutcome.Skipped, result.Outcome);
        Assert.False(result.Added);

        result = await CodingBoundaryEventCommandWorkflow.EnsureStartAsync(
            StartRequest(hasCodingViewModel: true, useNullViewEvents: true, codingSessionService: new RecordingCodingSessionService()),
            NoStartActions());

        Assert.Equal(CodingBoundaryEventCommandOutcome.Skipped, result.Outcome);
        Assert.False(result.Added);
    }

    [Fact]
    public async Task EnsureStart_builds_workflow_request_and_returns_added_state()
    {
        var service = new RecordingCodingSessionService();
        var viewEvents = new[] { Event("BAA") };
        var sessionEvents = new[] { Event("BAB") };
        var importEvents = new[] { Event("BCD") };
        var frameBytes = new byte[] { 1, 2, 3 };
        CodingBoundaryStartEventWorkflowRequest? delegated = null;

        var result = await CodingBoundaryEventCommandWorkflow.EnsureStartAsync(
            StartRequest(
                viewEvents: viewEvents,
                sessionEvents: sessionEvents,
                importEvents: importEvents,
                codingSessionService: service,
                firstCleanFrameSeconds: 4.5,
                analyzedFrameBytes: frameBytes),
            new CodingBoundaryStartCommandActions(
                request =>
                {
                    delegated = request;
                    return Task.FromResult(
                        new CodingBoundaryEventWorkflowResult(CodingBoundaryEventWorkflowOutcome.Added));
                }));

        Assert.Equal(CodingBoundaryEventCommandOutcome.Executed, result.Outcome);
        Assert.True(result.Added);
        Assert.NotNull(delegated);
        Assert.Equal(1.25, delegated.CurrentMeter);
        Assert.Same(viewEvents, delegated.ViewEvents);
        Assert.Same(sessionEvents, delegated.SessionEvents);
        Assert.Same(importEvents, delegated.ImportEvents);
        Assert.Same(service, delegated.CodingSessionService);
        Assert.Equal(4.5, delegated.FirstCleanFrameSeconds);
        Assert.Same(frameBytes, delegated.AnalyzedFrameBytes);
    }

    [Fact]
    public void EnsureEnd_skips_when_coding_session_is_not_ready()
    {
        var result = CodingBoundaryEventCommandWorkflow.EnsureEnd(
            EndRequest(hasCodingViewModel: false, codingSessionService: new RecordingCodingSessionService()),
            NoEndActions());

        Assert.Equal(CodingBoundaryEventCommandOutcome.Skipped, result.Outcome);
        Assert.False(result.Added);

        result = CodingBoundaryEventCommandWorkflow.EnsureEnd(
            EndRequest(hasCodingViewModel: true, useNullCodingSessionService: true),
            NoEndActions());

        Assert.Equal(CodingBoundaryEventCommandOutcome.Skipped, result.Outcome);
        Assert.False(result.Added);

        result = CodingBoundaryEventCommandWorkflow.EnsureEnd(
            EndRequest(hasCodingViewModel: true, useNullViewEvents: true, codingSessionService: new RecordingCodingSessionService()),
            NoEndActions());

        Assert.Equal(CodingBoundaryEventCommandOutcome.Skipped, result.Outcome);
        Assert.False(result.Added);
    }

    [Fact]
    public void EnsureEnd_builds_workflow_request_and_returns_added_state()
    {
        var service = new RecordingCodingSessionService();
        var viewEvents = new[] { Event("BAA") };
        var importEvents = new[] { Event("BCE") };
        var frameBytes = new byte[] { 5, 6, 7 };
        CodingBoundaryEndEventWorkflowRequest? delegated = null;

        var result = CodingBoundaryEventCommandWorkflow.EnsureEnd(
            EndRequest(
                viewEvents: viewEvents,
                importEvents: importEvents,
                codingSessionService: service,
                osdMeter: 14.25,
                fallbackEndMeter: 15.5,
                viewModelEndMeter: 16.75,
                fallbackVideoTime: TimeSpan.FromSeconds(90),
                analyzedFrameBytes: frameBytes),
            new CodingBoundaryEndCommandActions(
                request =>
                {
                    delegated = request;
                    return new CodingBoundaryEventWorkflowResult(CodingBoundaryEventWorkflowOutcome.Existing);
                }));

        Assert.Equal(CodingBoundaryEventCommandOutcome.Executed, result.Outcome);
        Assert.False(result.Added);
        Assert.NotNull(delegated);
        Assert.Same(viewEvents, delegated.ViewEvents);
        Assert.Same(importEvents, delegated.ImportEvents);
        Assert.Same(service, delegated.CodingSessionService);
        Assert.Equal(14.25, delegated.OsdMeter);
        Assert.Equal(15.5, delegated.FallbackEndMeter);
        Assert.Equal(16.75, delegated.ViewModelEndMeter);
        Assert.Equal(TimeSpan.FromSeconds(90), delegated.FallbackVideoTime);
        Assert.Same(frameBytes, delegated.AnalyzedFrameBytes);
    }

    private static CodingBoundaryStartCommandRequest StartRequest(
        bool hasCodingViewModel = true,
        IReadOnlyList<CodingEvent>? viewEvents = null,
        IReadOnlyList<CodingEvent>? sessionEvents = null,
        IReadOnlyList<CodingEvent>? importEvents = null,
        ICodingSessionService? codingSessionService = null,
        bool useNullViewEvents = false,
        bool useNullCodingSessionService = false,
        double? firstCleanFrameSeconds = null,
        byte[]? analyzedFrameBytes = null)
        => new(
            CurrentMeter: 1.25,
            HasCodingViewModel: hasCodingViewModel,
            ViewEvents: useNullViewEvents ? null : viewEvents ?? [],
            SessionEvents: sessionEvents ?? [],
            ImportEvents: importEvents ?? [],
            CodingSessionService: useNullCodingSessionService ? null : codingSessionService ?? new RecordingCodingSessionService(),
            FirstCleanFrameSeconds: firstCleanFrameSeconds,
            AnalyzedFrameBytes: analyzedFrameBytes);

    private static CodingBoundaryEndCommandRequest EndRequest(
        bool hasCodingViewModel = true,
        IReadOnlyList<CodingEvent>? viewEvents = null,
        IReadOnlyList<CodingEvent>? importEvents = null,
        ICodingSessionService? codingSessionService = null,
        bool useNullViewEvents = false,
        bool useNullCodingSessionService = false,
        double? osdMeter = null,
        double fallbackEndMeter = 2.5,
        double viewModelEndMeter = 3.5,
        TimeSpan? fallbackVideoTime = null,
        byte[]? analyzedFrameBytes = null)
        => new(
            HasCodingViewModel: hasCodingViewModel,
            ViewEvents: useNullViewEvents ? null : viewEvents ?? [],
            ImportEvents: importEvents ?? [],
            CodingSessionService: useNullCodingSessionService ? null : codingSessionService ?? new RecordingCodingSessionService(),
            OsdMeter: osdMeter,
            FallbackEndMeter: fallbackEndMeter,
            ViewModelEndMeter: viewModelEndMeter,
            FallbackVideoTime: fallbackVideoTime ?? TimeSpan.FromSeconds(12),
            AnalyzedFrameBytes: analyzedFrameBytes);

    private static CodingBoundaryStartCommandActions NoStartActions()
        => new(_ => throw new InvalidOperationException("EnsureStart should not run."));

    private static CodingBoundaryEndCommandActions NoEndActions()
        => new(_ => throw new InvalidOperationException("EnsureEnd should not run."));

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => Array.Empty<CodingEvent>();

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
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => new() { Entry = entry };
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
