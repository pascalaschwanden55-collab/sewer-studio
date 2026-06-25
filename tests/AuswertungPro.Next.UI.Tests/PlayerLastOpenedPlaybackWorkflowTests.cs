using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerLastOpenedPlaybackWorkflowTests
{
    [Fact]
    public void TryGetCurrentTime_skips_when_no_window_is_open()
    {
        var result = PlayerLastOpenedPlaybackWorkflow.TryGetCurrentTime(
            new PlayerLastOpenedCurrentTimeRequest(HasWindow: false),
            new PlayerLastOpenedCurrentTimeActions(
                TryGetCurrentTime: () => throw new InvalidOperationException("Current time should not be read.")));

        Assert.False(result.Success);
        Assert.Equal(default, result.Time);
        Assert.Equal(PlayerLastOpenedPlaybackWorkflowOutcome.MissingWindow, result.Outcome);
    }

    [Fact]
    public void TryGetCurrentTime_reads_current_window_time()
    {
        var result = PlayerLastOpenedPlaybackWorkflow.TryGetCurrentTime(
            new PlayerLastOpenedCurrentTimeRequest(HasWindow: true),
            new PlayerLastOpenedCurrentTimeActions(
                TryGetCurrentTime: () => new PlayerLastOpenedCurrentTimeActionResult(
                    Success: true,
                    Time: TimeSpan.FromSeconds(42))));

        Assert.True(result.Success);
        Assert.Equal(TimeSpan.FromSeconds(42), result.Time);
        Assert.Equal(PlayerLastOpenedPlaybackWorkflowOutcome.CurrentTimeRead, result.Outcome);
    }

    [Fact]
    public void TrySeekTo_skips_when_no_window_is_open()
    {
        var result = PlayerLastOpenedPlaybackWorkflow.TrySeekTo(
            new PlayerLastOpenedSeekRequest(
                HasWindow: false,
                Time: TimeSpan.FromSeconds(5)),
            new PlayerLastOpenedSeekActions(
                TrySeekTo: _ => throw new InvalidOperationException("Seek should not run.")));

        Assert.False(result.Success);
        Assert.Equal(PlayerLastOpenedPlaybackWorkflowOutcome.MissingWindow, result.Outcome);
    }

    [Fact]
    public void TrySeekTo_seeks_current_window_to_requested_time()
    {
        TimeSpan? requestedTime = null;

        var result = PlayerLastOpenedPlaybackWorkflow.TrySeekTo(
            new PlayerLastOpenedSeekRequest(
                HasWindow: true,
                Time: TimeSpan.FromSeconds(5)),
            new PlayerLastOpenedSeekActions(
                TrySeekTo: time =>
                {
                    requestedTime = time;
                    return true;
                }));

        Assert.True(result.Success);
        Assert.Equal(TimeSpan.FromSeconds(5), requestedTime);
        Assert.Equal(PlayerLastOpenedPlaybackWorkflowOutcome.Seeked, result.Outcome);
    }
}
