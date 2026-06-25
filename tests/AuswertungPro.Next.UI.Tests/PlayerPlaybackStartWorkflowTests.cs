using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPlaybackStartWorkflowTests
{
    [Fact]
    public void EnsurePlaying_skips_when_playback_should_not_start()
    {
        var result = PlayerPlaybackStartWorkflow.EnsurePlaying(
            new PlayerPlaybackEnsurePlayingRequest(
                ShouldStartPlayback: false,
                VideoPath: "video.mp4"),
            new PlayerPlaybackEnsurePlayingActions(
                Play: _ => throw new InvalidOperationException("Play should not run.")));

        Assert.Equal(PlayerPlaybackStartWorkflowOutcome.Idle, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void EnsurePlaying_plays_video_when_playback_should_start()
    {
        var calls = new List<string>();

        var result = PlayerPlaybackStartWorkflow.EnsurePlaying(
            new PlayerPlaybackEnsurePlayingRequest(
                ShouldStartPlayback: true,
                VideoPath: "video.mp4"),
            new PlayerPlaybackEnsurePlayingActions(
                Play: path => calls.Add($"play:{path}")));

        Assert.Equal(["play:video.mp4"], calls);
        Assert.Equal(PlayerPlaybackStartWorkflowOutcome.Started, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Play_starts_media_timer_and_rate_update_in_order()
    {
        var calls = new List<string>();

        var result = PlayerPlaybackStartWorkflow.Play(
            new PlayerPlaybackStartRequest(VideoPath: "video.mp4"),
            new PlayerPlaybackStartActions(
                PlayPath: path => calls.Add($"path:{path}"),
                StartTimer: () => calls.Add("timer"),
                UpdateRateLabel: () => calls.Add("rate")));

        Assert.Equal(["path:video.mp4", "timer", "rate"], calls);
        Assert.Equal(PlayerPlaybackStartWorkflowOutcome.Started, result.Outcome);
        Assert.True(result.Handled);
    }
}
