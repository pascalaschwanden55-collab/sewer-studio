using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventSeekPolicyTests
{
    [Fact]
    public void TryGetSeekMilliseconds_allows_zero_timestamp_when_protocol_time_exists()
    {
        var codingEvent = Event(TimeSpan.Zero);
        codingEvent.Entry.Zeit = TimeSpan.Zero;

        var canSeek = CodingEventSeekPolicy.TryGetSeekMilliseconds(codingEvent, out var milliseconds);

        Assert.True(canSeek);
        Assert.Equal(0, milliseconds);
    }

    [Fact]
    public void TryGetSeekMilliseconds_rejects_zero_timestamp_without_protocol_time()
    {
        var codingEvent = Event(TimeSpan.Zero);

        var canSeek = CodingEventSeekPolicy.TryGetSeekMilliseconds(codingEvent, out _);

        Assert.False(canSeek);
    }

    [Fact]
    public void TryGetSeekMilliseconds_allows_positive_timestamp_without_protocol_time()
    {
        var codingEvent = Event(TimeSpan.FromSeconds(7));

        var canSeek = CodingEventSeekPolicy.TryGetSeekMilliseconds(codingEvent, out var milliseconds);

        Assert.True(canSeek);
        Assert.Equal(7000, milliseconds);
    }

    [Fact]
    public void TryGetSeekMilliseconds_rejects_negative_timestamp()
    {
        var codingEvent = Event(TimeSpan.FromSeconds(-1));
        codingEvent.Entry.Zeit = TimeSpan.Zero;

        var canSeek = CodingEventSeekPolicy.TryGetSeekMilliseconds(codingEvent, out _);

        Assert.False(canSeek);
    }

    private static CodingEvent Event(TimeSpan videoTimestamp)
        => new()
        {
            Entry = new ProtocolEntry(),
            VideoTimestamp = videoTimestamp
        };
}
