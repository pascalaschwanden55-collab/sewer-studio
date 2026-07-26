using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionStopUiWorkflowTests
{
    [Fact]
    public void Execute_skips_all_ui_actions_when_ui_update_is_not_allowed()
    {
        var result = LiveDetectionStopUiWorkflow.Execute(
            new LiveDetectionStopUiWorkflowRequest(
                ShouldUpdateUi: false,
                HideOverlay: true,
                TotalEvents: 3,
                HasPlayer: true,
                IsPlaybackDisposed: false,
                IsPlayerPlaying: true),
            NoActions());

        Assert.Equal(LiveDetectionStopUiWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.UpdatedUi);
    }

    [Fact]
    public void Execute_updates_stop_ui_and_pauses_playback_in_order()
    {
        var calls = new List<string>();

        var result = LiveDetectionStopUiWorkflow.Execute(
            new LiveDetectionStopUiWorkflowRequest(
                ShouldUpdateUi: true,
                HideOverlay: true,
                TotalEvents: 7,
                HasPlayer: true,
                IsPlaybackDisposed: false,
                IsPlayerPlaying: true),
            new LiveDetectionStopUiWorkflowActions(
                SetStoppedStatus: () => calls.Add("status"),
                ClearOverlay: hideOverlay => calls.Add($"overlay:{hideOverlay}"),
                ShowStoppedDetectionStatus: totalEvents => calls.Add($"panel:{totalEvents}"),
                SetPause: pause => calls.Add($"pause:{pause}"),
                StartHideStatusTimer: () => calls.Add("hide")));

        Assert.Equal(
            ["status", "overlay:True", "panel:7", "pause:True", "hide"],
            calls);
        Assert.Equal(LiveDetectionStopUiWorkflowOutcome.Updated, result.Outcome);
        Assert.True(result.UpdatedUi);
    }

    private static LiveDetectionStopUiWorkflowActions NoActions()
        => new(
            SetStoppedStatus: () => throw new InvalidOperationException("Status should not run."),
            ClearOverlay: _ => throw new InvalidOperationException("Overlay should not run."),
            ShowStoppedDetectionStatus: _ => throw new InvalidOperationException("Panel should not run."),
            SetPause: _ => throw new InvalidOperationException("Pause should not run."),
            StartHideStatusTimer: () => throw new InvalidOperationException("Hide timer should not run."));
}
