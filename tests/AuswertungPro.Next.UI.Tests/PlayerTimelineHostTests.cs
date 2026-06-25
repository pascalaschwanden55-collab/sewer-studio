using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerTimelineHostTests
{
    [Fact]
    public void Host_exposes_time_and_duration_as_milliseconds_and_seconds()
    {
        var host = new PlayerTimelineHost(
            readTimeMilliseconds: () => 12_500,
            readLengthMilliseconds: () => 90_000,
            seekMilliseconds: _ => { });

        Assert.Equal(12_500, host.TimeMilliseconds);
        Assert.Equal(90_000, host.LengthMilliseconds);
        Assert.Equal(12.5, host.CurrentSeconds);
        Assert.Equal(90, host.DurationSeconds);
    }

    [Fact]
    public void Host_preserves_missing_values()
    {
        var host = new PlayerTimelineHost(
            readTimeMilliseconds: () => null,
            readLengthMilliseconds: () => null,
            seekMilliseconds: _ => { });

        Assert.Null(host.TimeMilliseconds);
        Assert.Null(host.LengthMilliseconds);
        Assert.Null(host.CurrentSeconds);
        Assert.Null(host.DurationSeconds);
    }

    [Fact]
    public void Seek_forwards_to_underlying_player()
    {
        long? seen = null;
        var host = new PlayerTimelineHost(
            readTimeMilliseconds: () => 0,
            readLengthMilliseconds: () => 100,
            seekMilliseconds: value => seen = value);

        host.SeekMilliseconds(42);

        Assert.Equal(42, seen);
    }
}
