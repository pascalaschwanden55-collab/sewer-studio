using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowTimerTickWorkflowTests
{
    [Fact]
    public void ExecuteUpdate_skips_when_window_is_closing()
    {
        var result = PlayerWindowTimerTickWorkflow.ExecuteUpdate(
            new PlayerWindowTimerTickWorkflowRequest(
                IsClosing: true,
                IsPlaybackDisposed: false,
                IsDragging: false),
            NoActions());

        Assert.Equal(PlayerWindowTimerTickWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void ExecuteUpdate_runs_ui_update_when_active()
    {
        var calls = new List<string>();

        var result = PlayerWindowTimerTickWorkflow.ExecuteUpdate(
            new PlayerWindowTimerTickWorkflowRequest(
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsDragging: true),
            Actions(calls));

        Assert.Equal(["update"], calls);
        Assert.Equal(PlayerWindowTimerTickWorkflowOutcome.Updated, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void ExecuteScrub_skips_when_playback_is_disposed()
    {
        var result = PlayerWindowTimerTickWorkflow.ExecuteScrub(
            new PlayerWindowTimerTickWorkflowRequest(
                IsClosing: false,
                IsPlaybackDisposed: true,
                IsDragging: true),
            NoActions());

        Assert.Equal(PlayerWindowTimerTickWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void ExecuteScrub_is_idle_when_not_dragging()
    {
        var result = PlayerWindowTimerTickWorkflow.ExecuteScrub(
            new PlayerWindowTimerTickWorkflowRequest(
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsDragging: false),
            NoActions());

        Assert.Equal(PlayerWindowTimerTickWorkflowOutcome.Idle, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void ExecuteScrub_runs_scrub_when_dragging()
    {
        var calls = new List<string>();

        var result = PlayerWindowTimerTickWorkflow.ExecuteScrub(
            new PlayerWindowTimerTickWorkflowRequest(
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsDragging: true),
            Actions(calls));

        Assert.Equal(["scrub"], calls);
        Assert.Equal(PlayerWindowTimerTickWorkflowOutcome.Scrubbed, result.Outcome);
        Assert.True(result.Handled);
    }

    private static PlayerWindowTimerTickWorkflowActions Actions(List<string> calls)
        => new(
            UpdateUi: () => calls.Add("update"),
            ScrubSeekToSlider: () => calls.Add("scrub"));

    private static PlayerWindowTimerTickWorkflowActions NoActions()
        => new(
            UpdateUi: () => throw new InvalidOperationException("Update should not run."),
            ScrubSeekToSlider: () => throw new InvalidOperationException("Scrub should not run."));
}
