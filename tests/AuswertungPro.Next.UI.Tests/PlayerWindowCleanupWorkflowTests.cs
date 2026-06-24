using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCleanupWorkflowTests
{
    [Fact]
    public void Execute_skips_all_actions_when_playback_is_already_disposed()
    {
        var result = PlayerWindowCleanupWorkflow.Execute(
            new PlayerWindowCleanupWorkflowRequest(IsPlaybackDisposed: true),
            NoActions());

        Assert.Equal(PlayerWindowCleanupWorkflowOutcome.Skipped, result.Outcome);
        Assert.False(result.Cleaned);
    }

    [Fact]
    public void Execute_marks_playback_disposed_then_cleans_resources_in_order()
    {
        var calls = new List<string>();

        var result = PlayerWindowCleanupWorkflow.Execute(
            new PlayerWindowCleanupWorkflowRequest(IsPlaybackDisposed: false),
            new PlayerWindowCleanupWorkflowActions(
                MarkPlaybackDisposed: () => calls.Add("mark"),
                StopPlayerTimers: () => calls.Add("timers"),
                DetachVideoView: () => calls.Add("detach"),
                DisposeMediaPlayer: () => calls.Add("media"),
                DisposeLibVlc: () => calls.Add("vlc")));

        Assert.Equal(["mark", "timers", "detach", "media", "vlc"], calls);
        Assert.Equal(PlayerWindowCleanupWorkflowOutcome.Cleaned, result.Outcome);
        Assert.True(result.Cleaned);
    }

    private static PlayerWindowCleanupWorkflowActions NoActions()
        => new(
            MarkPlaybackDisposed: () => throw new InvalidOperationException("Mark should not run."),
            StopPlayerTimers: () => throw new InvalidOperationException("Timers should not stop."),
            DetachVideoView: () => throw new InvalidOperationException("VideoView should not detach."),
            DisposeMediaPlayer: () => throw new InvalidOperationException("Media player should not dispose."),
            DisposeLibVlc: () => throw new InvalidOperationException("LibVLC should not dispose."));
}
