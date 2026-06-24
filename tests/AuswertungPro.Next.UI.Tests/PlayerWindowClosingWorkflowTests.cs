using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowClosingWorkflowTests
{
    [Fact]
    public void Execute_skips_without_actions_when_already_closing()
    {
        var result = PlayerWindowClosingWorkflow.Execute(
            new PlayerWindowClosingWorkflowRequest(AlreadyClosing: true),
            NoActions());

        Assert.Equal(PlayerWindowClosingWorkflowOutcome.AlreadyClosing, result.Outcome);
        Assert.False(result.CancelClose);
    }

    [Fact]
    public void Execute_cancels_close_when_unapplied_changes_are_not_confirmed()
    {
        var calls = new List<string>();

        var result = PlayerWindowClosingWorkflow.Execute(
            new PlayerWindowClosingWorkflowRequest(AlreadyClosing: false),
            Actions(
                confirmCanClose: () =>
                {
                    calls.Add("confirm");
                    return false;
                },
                markClosing: () => calls.Add("mark")));

        Assert.Equal(["confirm"], calls);
        Assert.Equal(PlayerWindowClosingWorkflowOutcome.Cancelled, result.Outcome);
        Assert.True(result.CancelClose);
    }

    [Fact]
    public void Execute_marks_closing_stops_runtime_and_cleans_resources_in_order()
    {
        var calls = new List<string>();

        var result = PlayerWindowClosingWorkflow.Execute(
            new PlayerWindowClosingWorkflowRequest(AlreadyClosing: false),
            Actions(
                confirmCanClose: () =>
                {
                    calls.Add("confirm");
                    return true;
                },
                markClosing: () => calls.Add("mark"),
                clearLastOpened: () => calls.Add("last"),
                stopPlayerTimers: () => calls.Add("timers"),
                cancelQuickScan: () => calls.Add("quick"),
                cancelLiveDetection: () => calls.Add("live-cancel"),
                cancelCodingAnalysis: () => calls.Add("coding-cancel"),
                stopLiveDetection: () => calls.Add("live-stop"),
                stopPipelineHealthMonitor: () => calls.Add("health"),
                detachVideoView: () => calls.Add("detach"),
                stopPlayer: () => calls.Add("player"),
                cleanup: () => calls.Add("cleanup")));

        Assert.Equal(
            [
                "confirm",
                "mark",
                "last",
                "timers",
                "quick",
                "live-cancel",
                "coding-cancel",
                "live-stop",
                "health",
                "detach",
                "player",
                "cleanup"
            ],
            calls);
        Assert.Equal(PlayerWindowClosingWorkflowOutcome.Closed, result.Outcome);
        Assert.False(result.CancelClose);
    }

    [Fact]
    public void Execute_logs_cleanup_error_without_throwing()
    {
        var calls = new List<string>();

        var result = PlayerWindowClosingWorkflow.Execute(
            new PlayerWindowClosingWorkflowRequest(AlreadyClosing: false),
            Actions(
                confirmCanClose: () => true,
                cleanup: () => throw new InvalidOperationException("boom"),
                logCleanupError: ex => calls.Add(ex.Message)));

        Assert.Equal(["boom"], calls);
        Assert.Equal(PlayerWindowClosingWorkflowOutcome.Closed, result.Outcome);
        Assert.False(result.CancelClose);
    }

    private static PlayerWindowClosingWorkflowActions NoActions()
        => Actions(
            confirmCanClose: () => throw new InvalidOperationException("Confirm should not run."),
            markClosing: () => throw new InvalidOperationException("Closing should not be marked."));

    private static PlayerWindowClosingWorkflowActions Actions(
        Func<bool>? confirmCanClose = null,
        Action? markClosing = null,
        Action? clearLastOpened = null,
        Action? stopPlayerTimers = null,
        Action? cancelQuickScan = null,
        Action? cancelLiveDetection = null,
        Action? cancelCodingAnalysis = null,
        Action? stopLiveDetection = null,
        Action? stopPipelineHealthMonitor = null,
        Action? detachVideoView = null,
        Action? stopPlayer = null,
        Action? cleanup = null,
        Action<Exception>? logCleanupError = null)
        => new(
            ConfirmCanClose: confirmCanClose ?? (() => true),
            MarkClosing: markClosing ?? (() => { }),
            ClearLastOpened: clearLastOpened ?? (() => { }),
            StopPlayerTimers: stopPlayerTimers ?? (() => { }),
            CancelQuickScan: cancelQuickScan ?? (() => { }),
            CancelLiveDetection: cancelLiveDetection ?? (() => { }),
            CancelCodingAnalysis: cancelCodingAnalysis ?? (() => { }),
            StopLiveDetection: stopLiveDetection ?? (() => { }),
            StopPipelineHealthMonitor: stopPipelineHealthMonitor ?? (() => { }),
            DetachVideoView: detachVideoView ?? (() => { }),
            StopPlayer: stopPlayer ?? (() => { }),
            Cleanup: cleanup ?? (() => { }),
            LogCleanupError: logCleanupError ?? (_ => { }));
}
