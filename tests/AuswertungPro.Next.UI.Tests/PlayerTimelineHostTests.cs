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

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-250L, 0)]
    [InlineData(12_500L, 12_500)]
    public void Host_exposes_non_negative_current_time_fallback(long? currentMilliseconds, double expectedMilliseconds)
    {
        var host = new PlayerTimelineHost(
            readTimeMilliseconds: () => currentMilliseconds,
            readLengthMilliseconds: () => 90_000,
            seekMilliseconds: _ => { });

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), host.CurrentTimeOrZero);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-1000L, 0)]
    [InlineData(12_500L, 12.5)]
    public void Host_exposes_non_negative_current_seconds_fallback(long? currentMilliseconds, double expectedSeconds)
    {
        var host = new PlayerTimelineHost(
            readTimeMilliseconds: () => currentMilliseconds,
            readLengthMilliseconds: () => 90_000,
            seekMilliseconds: _ => { });

        Assert.Equal(expectedSeconds, host.CurrentSecondsOrZero);
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
