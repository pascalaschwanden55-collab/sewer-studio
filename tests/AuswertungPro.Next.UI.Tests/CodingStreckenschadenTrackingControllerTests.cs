using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStreckenschadenTrackingControllerTests
{
    [Fact]
    public void ApplyTracking_opens_and_an_empty_tick_closes_the_same_event_at_last_seen_meter()
    {
        var calls = new List<string>();
        var harness = CreateHarness(calls);
        var segment = Finding("long crack");

        var consumed = harness.Controller.ApplyTracking(
            [segment],
            meter: 2.0,
            videoTime: TimeSpan.FromSeconds(10));

        Assert.Same(segment, Assert.Single(consumed));
        var opened = Assert.Single(harness.Events);
        Assert.Equal("BBA", opened.Entry.Code);
        Assert.Equal("Laengsriss", opened.Entry.Beschreibung);
        Assert.Equal(2.0, opened.Entry.MeterStart);
        Assert.Null(opened.Entry.MeterEnd);
        Assert.True(opened.Entry.IsStreckenschaden);
        Assert.Equal(TimeSpan.FromSeconds(10), opened.Entry.Zeit);
        Assert.Equal("frames/bba.png", Assert.Single(opened.Entry.FotoPaths));
        Assert.Equal(["label", "photo", "add", "refresh"], calls);

        var consumedByContinuation = harness.Controller.ApplyTracking(
            [segment],
            meter: 2.7,
            videoTime: TimeSpan.FromSeconds(12));

        Assert.Same(segment, Assert.Single(consumedByContinuation));
        Assert.Single(harness.Events);
        Assert.Empty(harness.Service.Updates);
        Assert.Equal(["label", "photo", "add", "refresh"], calls);

        var consumedByEmptyTick = harness.Controller.ApplyTracking(
            [],
            meter: 3.8,
            videoTime: TimeSpan.FromSeconds(14));

        Assert.Empty(consumedByEmptyTick);
        Assert.Equal(2.7, opened.Entry.MeterEnd);
        var update = Assert.Single(harness.Service.Updates);
        Assert.Equal(opened.EventId, update.EventId);
        Assert.Same(opened.Entry, update.Entry);
        Assert.Equal(
            ["label", "photo", "add", "refresh", "update", "refresh"],
            calls);
    }

    [Fact]
    public void CloseTracked_uses_the_current_video_time_and_only_refreshes_when_an_event_changes()
    {
        var calls = new List<string>();
        var currentVideoTime = TimeSpan.FromSeconds(20);
        var timeReads = 0;
        var harness = CreateHarness(
            calls,
            resolveCurrentVideoTime: () =>
            {
                timeReads++;
                return currentVideoTime;
            });
        harness.Controller.ApplyTracking(
            [Finding("long crack")],
            meter: 4.0,
            videoTime: TimeSpan.FromSeconds(8));
        currentVideoTime = TimeSpan.FromSeconds(37);

        harness.Controller.CloseTracked(endMeter: 6.5);

        var opened = Assert.Single(harness.Events);
        Assert.Equal(6.5, opened.Entry.MeterEnd);
        Assert.Single(harness.Service.Updates);
        Assert.Equal(1, timeReads);
        Assert.Equal(2, calls.Count(call => call == "refresh"));

        harness.Controller.CloseTracked(endMeter: 8.0);

        Assert.Single(harness.Service.Updates);
        Assert.Equal(2, calls.Count(call => call == "refresh"));
    }

    [Fact]
    public void Reset_discards_tracked_state_without_closing_or_refreshing_the_open_event()
    {
        var calls = new List<string>();
        var harness = CreateHarness(calls);
        harness.Controller.ApplyTracking(
            [Finding("long crack")],
            meter: 1.0,
            videoTime: TimeSpan.FromSeconds(3));

        harness.Controller.Reset();
        harness.Controller.CloseTracked(endMeter: 5.0);

        var opened = Assert.Single(harness.Events);
        Assert.Null(opened.Entry.MeterEnd);
        Assert.Empty(harness.Service.Updates);
        Assert.Equal(1, calls.Count(call => call == "refresh"));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ApplyTracking_without_ready_session_does_not_resolve_or_remember_findings(
        bool hasViewModel,
        bool hasSessionService)
    {
        var calls = new List<string>();
        var host = new FakeCodingSessionHost { HasViewModel = hasViewModel };
        var service = new RecordingCodingSessionService(host.EventCollection!, calls);
        var serviceAvailable = hasSessionService;
        var resolveCalls = 0;
        var controller = CreateController(
            host,
            service,
            calls,
            resolveCode: (_, _) =>
            {
                resolveCalls++;
                return "BBA";
            },
            resolveCodingSessionService: () => serviceAvailable ? service : null);
        var segment = Finding("long crack");

        var consumed = controller.ApplyTracking(
            [segment],
            meter: 1.0,
            videoTime: TimeSpan.Zero);
        host.HasViewModel = true;
        serviceAvailable = true;
        var readyConsumed = controller.ApplyTracking(
            [segment],
            meter: 2.0,
            videoTime: TimeSpan.FromSeconds(2));

        Assert.Empty(consumed);
        Assert.Same(segment, Assert.Single(readyConsumed));
        Assert.Equal(1, resolveCalls);
        Assert.Single(host.EventCollection!);
        Assert.Empty(service.Updates);
        Assert.Equal(1, calls.Count(call => call == "refresh"));
    }

    private static Harness CreateHarness(
        List<string> calls,
        Func<TimeSpan>? resolveCurrentVideoTime = null)
    {
        var host = new FakeCodingSessionHost { HasViewModel = true };
        var service = new RecordingCodingSessionService(host.EventCollection!, calls);
        var controller = CreateController(
            host,
            service,
            calls,
            resolveCurrentVideoTime: resolveCurrentVideoTime);
        return new Harness(controller, host.EventCollection!, service);
    }

    private static CodingStreckenschadenTrackingController CreateController(
        FakeCodingSessionHost host,
        RecordingCodingSessionService service,
        ICollection<string> calls,
        Func<LiveFrameFinding, double, string?>? resolveCode = null,
        Func<TimeSpan>? resolveCurrentVideoTime = null,
        Func<ICodingSessionService?>? resolveCodingSessionService = null)
        => new(
            host,
            new CodingStreckenschadenTrackingControllerBindings(
                ResolveCodingSessionService: resolveCodingSessionService ?? (() => service),
                ResolveCode: resolveCode ?? ((_, _) => "BBA"),
                LookupLabel: _ =>
                {
                    calls.Add("label");
                    return "Laengsriss";
                },
                AttachAnalyzedFramePhoto: entry =>
                {
                    calls.Add("photo");
                    entry.FotoPaths.Add("frames/bba.png");
                },
                ResolveCurrentVideoTime: resolveCurrentVideoTime ?? (() => TimeSpan.Zero),
                RefreshEvents: () => calls.Add("refresh")));

    private static SegmentedFinding Finding(string label)
    {
        var mask = new SamMaskResult(
            Label: label,
            Confidence: 0.9,
            Bbox: [0, 0, 100, 100],
            MaskRle: "0",
            MaskAreaPixels: 100,
            ImageAreaPixels: 10_000,
            HeightPixels: 100,
            WidthPixels: 100,
            CentroidX: 50,
            CentroidY: 50);
        var quant = new MaskQuantificationService.QuantifiedMask(
            label,
            0.9,
            null,
            null,
            60,
            null,
            null,
            "3 Uhr");
        var proximity = new MetrierungProximityResult(
            MetrierungProximity.Codierbar,
            "",
            0,
            0,
            0,
            false,
            false);
        return new SegmentedFinding(null, mask, quant, proximity);
    }

    private sealed record Harness(
        CodingStreckenschadenTrackingController Controller,
        ObservableCollection<CodingEvent> Events,
        RecordingCodingSessionService Service);

    private sealed record UpdateCall(
        Guid EventId,
        ProtocolEntry Entry,
        OverlayGeometry? Overlay);

    private sealed class RecordingCodingSessionService(
        ObservableCollection<CodingEvent> events,
        ICollection<string> calls) : ICodingSessionService
    {
        public List<UpdateCall> Updates { get; } = new();
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => events;

        public event EventHandler<CodingSessionState>? StateChanged { add { } remove { } }
        public event EventHandler<double>? MeterChanged { add { } remove { } }
        public event EventHandler<CodingEvent>? EventAdded;

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
            calls.Add("add");
            var codingEvent = new CodingEvent
            {
                EventId = Guid.NewGuid(),
                Entry = entry,
                Overlay = overlay
            };
            events.Add(codingEvent);
            EventAdded?.Invoke(this, codingEvent);
            return codingEvent;
        }

        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null)
        {
            calls.Add("update");
            Updates.Add(new UpdateCall(eventId, entry, overlay));
        }

        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCodingSessionHost : ICodingSessionHost
    {
        public bool HasViewModel { get; set; }
        public bool IsRunningOrPaused => false;
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public OverlayGeometry? CurrentOverlay => null;
        public ObservableCollection<CodingEvent>? EventCollection { get; } = new();
        public IEnumerable<CodingEvent> Events => EventCollection!;
        public CodingEvent? SelectedDefect => null;
        public string? HaltungName => null;
        public string? VideoPath => null;
        public TimeSpan? CurrentVideoTime => null;
        public string SelectedCode => string.Empty;
        public string SelectedCodeDescription => string.Empty;

        public void SetCurrentVideoTime(TimeSpan videoTime) { }
        public void SelectDefect(CodingEvent? codingEvent) { }
        public void ClearSelectedDefect() { }
        public void SetCurrentOverlay(OverlayGeometry? overlay) { }
        public void ClearCurrentOverlay() { }
        public void ClearSelectedCode() { }
        public void BeginOverlayDraw(NormalizedPoint point) { }
        public void UpdateOverlayDraw(NormalizedPoint point) { }
        public void CompleteOverlayDraw(NormalizedPoint point) { }
        public bool AddMultiPointOverlayPoint(NormalizedPoint point) => false;
        public void UpdateMultiPointOverlayPreview(NormalizedPoint point) { }
        public bool ExecuteMoveNext() => false;
        public bool ExecuteMovePrevious() => false;
        public bool ExecuteAcceptDefect() => false;
        public bool ExecuteEditDefect() => false;
        public bool ExecuteStartSession(HaltungRecord? haltung) => false;
        public bool ExecuteJumpToDefect(CodingEvent? codingEvent) => false;
    }
}
