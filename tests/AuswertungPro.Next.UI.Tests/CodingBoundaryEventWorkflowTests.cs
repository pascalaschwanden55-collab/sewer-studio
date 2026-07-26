using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBoundaryEventWorkflowTests
{
    [Fact]
    public async Task EnsureStart_skips_existing_bcd_without_adding_event()
    {
        var calls = new List<string>();
        var service = new RecordingCodingSessionService();

        var result = await CodingBoundaryEventWorkflow.EnsureStartAsync(
            new CodingBoundaryStartEventWorkflowRequest(
                CurrentMeter: 0.3,
                ViewEvents: [Event("BCD")],
                SessionEvents: [Event("BCD")],
                ImportEvents: [],
                CodingSessionService: service,
                FirstCleanFrameSeconds: 4.2,
                AnalyzedFrameBytes: [1, 2, 3]),
            Actions(
                trace: message =>
                {
                    calls.Add("trace");
                    Assert.Contains("bereits vorhanden", message);
                },
                lookupLabel: _ => throw new InvalidOperationException("Label lookup should not run."),
                tryExtractFrameAtSecondsAsync: _ => throw new InvalidOperationException("Frame extraction should not run."),
                attachBoundaryAnalyzedFramePhoto: (_, _) => throw new InvalidOperationException("Attach should not run."),
                startAutoCalibration: () => throw new InvalidOperationException("Auto calibration should not run.")));

        Assert.Equal(CodingBoundaryEventWorkflowOutcome.Existing, result.Outcome);
        Assert.False(result.Added);
        Assert.Empty(service.AddedEvents);
        Assert.Equal(["trace"], calls);
    }

    [Fact]
    public async Task EnsureStart_adds_bcd_from_import_reference_and_prefers_clean_frame()
    {
        var calls = new List<string>();
        var service = new RecordingCodingSessionService();
        var analyzedFrame = new byte[] { 1, 2, 3 };
        var cleanFrame = new byte[] { 9, 8, 7 };
        ProtocolEntry? attachedEntry = null;

        var result = await CodingBoundaryEventWorkflow.EnsureStartAsync(
            new CodingBoundaryStartEventWorkflowRequest(
                CurrentMeter: 0.3,
                ViewEvents: [],
                SessionEvents: [],
                ImportEvents: [Event("BCD", 0.12, 5)],
                CodingSessionService: service,
                FirstCleanFrameSeconds: 6.5,
                AnalyzedFrameBytes: analyzedFrame),
            Actions(
                trace: message =>
                {
                    calls.Add("trace");
                    Assert.Contains("NEU erzeugen", message);
                },
                lookupLabel: code =>
                {
                    calls.Add($"lookup:{code}");
                    return "Rohranfang";
                },
                tryExtractFrameAtSecondsAsync: seconds =>
                {
                    calls.Add($"extract:{seconds:F1}");
                    return Task.FromResult<byte[]?>(cleanFrame);
                },
                attachBoundaryAnalyzedFramePhoto: (entry, frameBytes) =>
                {
                    calls.Add("attach");
                    attachedEntry = entry;
                    Assert.Same(cleanFrame, frameBytes);
                },
                startAutoCalibration: () => calls.Add("auto-calibration")));

        Assert.Equal(
            ["trace", "lookup:BCD", "extract:6.5", "attach", "auto-calibration"],
            calls);
        Assert.Equal(CodingBoundaryEventWorkflowOutcome.Added, result.Outcome);
        Assert.True(result.Added);
        var added = Assert.Single(service.AddedEvents);
        Assert.Same(attachedEntry, added.Entry);
        Assert.Equal("BCD", added.Entry.Code);
        Assert.Equal("Rohranfang", added.Entry.Beschreibung);
        Assert.Equal(0.12, added.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(5), added.VideoTimestamp);
    }

    [Fact]
    public async Task EnsureStartAsync_awaits_clean_frame_extraction_before_attaching_photo()
    {
        var calls = new List<string>();
        var service = new RecordingCodingSessionService();
        var cleanFrame = new byte[] { 9, 8, 7 };
        var frameCompletion = new TaskCompletionSource<byte[]?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var attached = false;

        var pending = CodingBoundaryEventWorkflow.EnsureStartAsync(
            new CodingBoundaryStartEventWorkflowRequest(
                CurrentMeter: 0.3,
                ViewEvents: [],
                SessionEvents: [],
                ImportEvents: [Event("BCD", 0.12, 5)],
                CodingSessionService: service,
                FirstCleanFrameSeconds: 6.5,
                AnalyzedFrameBytes: [1, 2, 3]),
            Actions(
                trace: _ => calls.Add("trace"),
                lookupLabel: code =>
                {
                    calls.Add($"lookup:{code}");
                    return "Rohranfang";
                },
                tryExtractFrameAtSecondsAsync: seconds =>
                {
                    calls.Add($"extract:{seconds:F1}");
                    return frameCompletion.Task;
                },
                attachBoundaryAnalyzedFramePhoto: (_, frameBytes) =>
                {
                    calls.Add("attach");
                    attached = true;
                    Assert.Same(cleanFrame, frameBytes);
                },
                startAutoCalibration: () => calls.Add("auto-calibration")));

        Assert.False(pending.IsCompleted);
        Assert.False(attached);
        Assert.Equal(["trace", "lookup:BCD", "extract:6.5"], calls);

        frameCompletion.SetResult(cleanFrame);
        var result = await pending;

        Assert.Equal(["trace", "lookup:BCD", "extract:6.5", "attach", "auto-calibration"], calls);
        Assert.Equal(CodingBoundaryEventWorkflowOutcome.Added, result.Outcome);
        Assert.True(result.Added);
    }

    [Fact]
    public void EnsureEnd_skips_existing_bce_without_refresh()
    {
        var service = new RecordingCodingSessionService();

        var result = CodingBoundaryEventWorkflow.EnsureEnd(
            new CodingBoundaryEndEventWorkflowRequest(
                ViewEvents: [Event("BCE")],
                ImportEvents: [],
                CodingSessionService: service,
                OsdMeter: 15.0,
                FallbackEndMeter: 15.0,
                ViewModelEndMeter: 15.0,
                FallbackVideoTime: TimeSpan.FromSeconds(80),
                AnalyzedFrameBytes: [1, 2, 3]),
            Actions(
                lookupLabel: _ => throw new InvalidOperationException("Label lookup should not run."),
                attachBoundaryAnalyzedFramePhoto: (_, _) => throw new InvalidOperationException("Attach should not run."),
                refreshEvents: () => throw new InvalidOperationException("Refresh should not run.")));

        Assert.Equal(CodingBoundaryEventWorkflowOutcome.Existing, result.Outcome);
        Assert.False(result.Added);
        Assert.Empty(service.AddedEvents);
    }

    [Fact]
    public void EnsureEnd_adds_bce_with_resolved_import_reference_and_refreshes_events()
    {
        var calls = new List<string>();
        var service = new RecordingCodingSessionService();
        var frameBytes = new byte[] { 4, 5, 6 };
        ProtocolEntry? attachedEntry = null;

        var result = CodingBoundaryEventWorkflow.EnsureEnd(
            new CodingBoundaryEndEventWorkflowRequest(
                ViewEvents: [],
                ImportEvents: [Event("BCE", 15.82, 90)],
                CodingSessionService: service,
                OsdMeter: 114.13,
                FallbackEndMeter: 15.82,
                ViewModelEndMeter: 15.82,
                FallbackVideoTime: TimeSpan.FromSeconds(80),
                AnalyzedFrameBytes: frameBytes),
            Actions(
                lookupLabel: code =>
                {
                    calls.Add($"lookup:{code}");
                    return "Rohrende";
                },
                attachBoundaryAnalyzedFramePhoto: (entry, attachedFrameBytes) =>
                {
                    calls.Add("attach");
                    attachedEntry = entry;
                    Assert.Same(frameBytes, attachedFrameBytes);
                },
                refreshEvents: () => calls.Add("refresh")));

        Assert.Equal(["lookup:BCE", "attach", "refresh"], calls);
        Assert.Equal(CodingBoundaryEventWorkflowOutcome.Added, result.Outcome);
        Assert.True(result.Added);
        var added = Assert.Single(service.AddedEvents);
        Assert.Same(attachedEntry, added.Entry);
        Assert.Equal("BCE", added.Entry.Code);
        Assert.Equal("Rohrende", added.Entry.Beschreibung);
        Assert.Equal(15.82, added.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(90), added.VideoTimestamp);
    }

    private static CodingBoundaryEventWorkflowActions Actions(
        Func<string, string?>? lookupLabel = null,
        Action<string>? trace = null,
        Func<double?, Task<byte[]?>>? tryExtractFrameAtSecondsAsync = null,
        Action<ProtocolEntry, byte[]?>? attachBoundaryAnalyzedFramePhoto = null,
        Action? startAutoCalibration = null,
        Action? refreshEvents = null)
        => new(
            LookupLabel: lookupLabel ?? (_ => null),
            Trace: trace ?? (_ => { }),
            TryExtractFrameAtSecondsAsync: tryExtractFrameAtSecondsAsync ?? (_ => Task.FromResult<byte[]?>(null)),
            AttachBoundaryAnalyzedFramePhoto: attachBoundaryAnalyzedFramePhoto ?? ((_, _) => { }),
            StartAutoCalibration: startAutoCalibration ?? (() => { }),
            RefreshEvents: refreshEvents ?? (() => { }));

    private static CodingEvent Event(string code, double meter = 0, double seconds = 0)
        => new()
        {
            Entry = new ProtocolEntry { Code = code },
            MeterAtCapture = meter,
            VideoTimestamp = TimeSpan.FromSeconds(seconds)
        };

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public List<CodingEvent> AddedEvents { get; } = new();

        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => AddedEvents;

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

        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null)
        {
            var ev = new CodingEvent { Entry = entry, Overlay = overlay };
            AddedEvents.Add(ev);
            return ev;
        }

        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
