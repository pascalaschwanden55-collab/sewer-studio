using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionStopControllerTests
{
    [Fact]
    public void Stop_stops_runtime_and_skips_ui_when_window_is_unavailable()
    {
        var calls = new List<string>();
        var controller = new LiveDetectionStopController(
            Sources(
                stopRuntime: () => calls.Add("runtime"),
                shouldUpdateUi: () => false),
            Actions(
                setStoppedStatus: () => throw new InvalidOperationException("UI must not update."),
                clearOverlay: _ => throw new InvalidOperationException("UI must not update."),
                showStoppedDetectionStatus: _ => throw new InvalidOperationException("UI must not update."),
                setPause: _ => throw new InvalidOperationException("UI must not update."),
                scheduleHideStatusTimer: _ => throw new InvalidOperationException("Timer must not start.")));

        controller.Stop();

        Assert.Equal(["runtime"], calls);
    }

    [Fact]
    public void Stop_preserves_ui_order_and_delayed_hide_wiring()
    {
        var calls = new List<string>();
        var isDetecting = true;
        var controller = new LiveDetectionStopController(
            Sources(
                stopRuntime: () =>
                {
                    calls.Add("runtime");
                    isDetecting = false;
                },
                shouldUpdateUi: () => true,
                hideOverlay: () => true,
                getTotalEvents: () => 7,
                hasPlayer: () => true,
                isPlaybackDisposed: () => false,
                isPlayerPlaying: () => true,
                isDetecting: () => isDetecting),
            Actions(
                setStoppedStatus: () => calls.Add("status"),
                clearOverlay: hide => calls.Add($"overlay:{hide}"),
                showStoppedDetectionStatus: total => calls.Add($"summary:{total}"),
                setPause: pause => calls.Add($"pause:{pause}"),
                scheduleHideStatusTimer: displayActions =>
                {
                    calls.Add("schedule");
                    if (!displayActions.IsDetecting())
                        displayActions.HideDetectionStatus();
                },
                hideDetectionStatus: () => calls.Add("hide")));

        controller.Stop();

        Assert.Equal(
            [
                "runtime",
                "status",
                "overlay:True",
                "summary:7",
                "pause:True",
                "schedule",
                "hide"
            ],
            calls);
    }

    private static LiveDetectionStopControllerSources Sources(
        Action? stopRuntime = null,
        Func<bool>? shouldUpdateUi = null,
        Func<bool>? hideOverlay = null,
        Func<int>? getTotalEvents = null,
        Func<bool>? hasPlayer = null,
        Func<bool>? isPlaybackDisposed = null,
        Func<bool>? isPlayerPlaying = null,
        Func<bool>? isDetecting = null)
        => new(
            StopRuntime: stopRuntime ?? (() => { }),
            ShouldUpdateUi: shouldUpdateUi ?? (() => true),
            HideOverlay: hideOverlay ?? (() => false),
            GetTotalEvents: getTotalEvents ?? (() => 0),
            HasPlayer: hasPlayer ?? (() => false),
            IsPlaybackDisposed: isPlaybackDisposed ?? (() => true),
            IsPlayerPlaying: isPlayerPlaying ?? (() => false),
            IsDetecting: isDetecting ?? (() => false));

    private static LiveDetectionStopControllerActions Actions(
        Action? setStoppedStatus = null,
        Action<bool>? clearOverlay = null,
        Action<int>? showStoppedDetectionStatus = null,
        Action<bool>? setPause = null,
        Action<LiveDetectionHideStatusTimerDisplayActions>? scheduleHideStatusTimer = null,
        Action? hideDetectionStatus = null)
        => new(
            SetStoppedStatus: setStoppedStatus ?? (() => { }),
            ClearOverlay: clearOverlay ?? (_ => { }),
            ShowStoppedDetectionStatus: showStoppedDetectionStatus ?? (_ => { }),
            SetPause: setPause ?? (_ => { }),
            ScheduleHideStatusTimer: scheduleHideStatusTimer ?? (_ => { }),
            HideDetectionStatus: hideDetectionStatus ?? (() => { }));
}
