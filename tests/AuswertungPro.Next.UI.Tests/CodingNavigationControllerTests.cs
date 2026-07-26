using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingNavigationControllerTests
{
    [Fact]
    public void UpdateUi_with_pending_meter_change_syncs_video_and_updates_visible_state_in_order()
    {
        var calls = new List<string>();
        long playerTimeMs = 1_000;
        var pendingState = new CodingNavigationPendingState();
        pendingState.MarkPending();
        var osdMeterController = new CodingOsdMeterController();
        osdMeterController.ApplyState(new CodingOsdMeterState(5, null, "5.00m"));
        var overlay = new OverlayGeometry();
        var host = new FakeCodingSessionHost
        {
            HasViewModel = true,
            CurrentMeter = 5,
            EndMeter = 10,
            CurrentOverlay = overlay,
            Events =
            [
                new CodingEvent
                {
                    MeterAtCapture = 5.2,
                    Entry = new ProtocolEntry { Code = "BAB" }
                }
            ]
        };
        var controller = CreateController(
            host,
            pendingState,
            osdMeterController,
            calls,
            () => playerTimeMs,
            () => 10_000,
            value =>
            {
                playerTimeMs = value;
                calls.Add($"seek:{value}");
            });

        var result = controller.UpdateUi(nameof(CodingSessionViewModel.CurrentMeter));

        Assert.Equal(CodingUiUpdateCommandOutcome.Applied, result.Outcome);
        Assert.False(pendingState.IsPending);
        Assert.Equal(TimeSpan.FromSeconds(5), host.CurrentVideoTime);
        Assert.Equal(
            ["meter:5.00", "seek:5000", "overlay:True", "badge:5.20m BAB"],
            calls);
    }

    [Fact]
    public async Task MoveNextAsync_marks_pending_pauses_resets_osd_and_reads_new_meter()
    {
        var calls = new List<string>();
        var pendingState = new CodingNavigationPendingState();
        var osdMeterController = new CodingOsdMeterController();
        osdMeterController.ApplyState(new CodingOsdMeterState(7, 2, "7.00m"));
        var host = new FakeCodingSessionHost
        {
            HasViewModel = true,
            MoveNext = () => calls.Add($"move:{pendingState.IsPending}")
        };
        var controller = CreateController(
            host,
            pendingState,
            osdMeterController,
            calls,
            () => 0,
            () => 10_000,
            _ => { },
            readOsdMeterAsync: () =>
            {
                calls.Add("read");
                return Task.FromResult<double?>(8);
            });

        var result = await controller.MoveNextAsync("CodingNext_Click");

        Assert.Equal(CodingMoveByCommandOutcome.Moved, result.Outcome);
        Assert.True(pendingState.IsPending);
        Assert.Null(osdMeterController.LastMeter);
        Assert.Equal(["move:True", "pause", "read"], calls);
    }

    [Fact]
    public async Task MoveNextAsync_logs_failure_and_does_not_continue_after_command_error()
    {
        var calls = new List<string>();
        var host = new FakeCodingSessionHost
        {
            HasViewModel = true,
            MoveNext = () => throw new InvalidOperationException("kaputt")
        };
        var controller = CreateController(
            host,
            new CodingNavigationPendingState(),
            new CodingOsdMeterController(),
            calls,
            () => 0,
            () => 10_000,
            _ => { });

        var result = await controller.MoveNextAsync("CodingNext_Click");

        Assert.Equal(CodingMoveByCommandOutcome.Failed, result.Outcome);
        Assert.Equal(["trace:[PlayerWindow] CodingNext_Click error: kaputt"], calls);
    }

    private static CodingNavigationController CreateController(
        FakeCodingSessionHost host,
        CodingNavigationPendingState pendingState,
        CodingOsdMeterController osdMeterController,
        List<string> calls,
        Func<long> readPlayerTime,
        Func<long> readPlayerLength,
        Action<long> seekPlayer,
        Func<Task<double?>>? readOsdMeterAsync = null)
    {
        var timelineHost = new PlayerTimelineHost(
            () => readPlayerTime(),
            () => readPlayerLength(),
            seekPlayer);

        return new CodingNavigationController(
            host,
            pendingState,
            osdMeterController,
            timelineHost,
            new CodingNavigationControllerActions(
                ApplyMeterTimeline: meter => calls.Add($"meter:{meter:F2}"),
                UpdateOverlayInfo: overlay => calls.Add($"overlay:{overlay is not null}"),
                ApplyCurrentCodeState: state => calls.Add($"badge:{state.Text}"),
                UpdateStatistics: () => calls.Add("statistics"),
                PausePlayback: () => calls.Add("pause"),
                ReadOsdMeterAsync: readOsdMeterAsync ?? (() => Task.FromResult<double?>(null)),
                TraceError: message => calls.Add($"trace:{message}")));
    }

    private sealed class FakeCodingSessionHost : ICodingSessionHost
    {
        public bool HasViewModel { get; init; }
        public bool IsRunningOrPaused => false;
        public double CurrentMeter { get; init; }
        public double EndMeter { get; init; }
        public OverlayGeometry? CurrentOverlay { get; init; }
        public ObservableCollection<CodingEvent>? EventCollection => null;
        public IEnumerable<CodingEvent> Events { get; init; } = [];
        public CodingEvent? SelectedDefect => null;
        public string? HaltungName => null;
        public string? VideoPath => null;
        public TimeSpan? CurrentVideoTime { get; private set; }
        public string SelectedCode => string.Empty;
        public string SelectedCodeDescription => string.Empty;
        public Action MoveNext { get; init; } = () => { };

        public void SetCurrentVideoTime(TimeSpan videoTime) => CurrentVideoTime = videoTime;
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
        public bool ExecuteMoveNext()
        {
            MoveNext();
            return true;
        }

        public bool ExecuteMovePrevious() => true;
        public bool ExecuteAcceptDefect() => false;
        public bool ExecuteEditDefect() => false;
        public bool ExecuteStartSession(HaltungRecord? haltung) => false;
        public bool ExecuteJumpToDefect(CodingEvent? codingEvent) => false;
    }
}
