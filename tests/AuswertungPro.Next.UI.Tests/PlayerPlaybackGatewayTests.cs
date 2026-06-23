using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPlaybackGatewayTests
{
    [Fact]
    public void TryGetCurrentTime_clamps_negative_time_and_returns_true()
    {
        var ok = PlayerPlaybackGateway.TryGetCurrentTime(() => -500, out var time);

        Assert.True(ok);
        Assert.Equal(TimeSpan.Zero, time);
    }

    [Fact]
    public void TryGetCurrentTime_returns_false_when_reader_throws()
    {
        var ok = PlayerPlaybackGateway.TryGetCurrentTime(
            () => throw new InvalidOperationException("player disposed"),
            out var time);

        Assert.False(ok);
        Assert.Equal(default, time);
    }

    [Fact]
    public void TrySeekTo_ensures_playing_sets_clamped_time_and_updates_ui()
    {
        var calls = new List<string>();
        long? assignedTime = null;

        var ok = PlayerPlaybackGateway.TrySeekTo(
            TimeSpan.FromSeconds(200),
            getDurationMs: () => 120_000,
            setTimeMs: value =>
            {
                calls.Add("set");
                assignedTime = value;
            },
            ensurePlaying: () => calls.Add("ensure"),
            updateUi: () => calls.Add("update"));

        Assert.True(ok);
        Assert.Equal(120_000, assignedTime);
        Assert.Equal(["ensure", "set", "update"], calls);
    }

    [Fact]
    public void TrySeekTo_returns_false_when_seek_fails()
    {
        var updateCalled = false;

        var ok = PlayerPlaybackGateway.TrySeekTo(
            TimeSpan.FromSeconds(1),
            getDurationMs: () => 10_000,
            setTimeMs: _ => throw new InvalidOperationException("seek failed"),
            ensurePlaying: () => { },
            updateUi: () => updateCalled = true);

        Assert.False(ok);
        Assert.False(updateCalled);
    }
}
