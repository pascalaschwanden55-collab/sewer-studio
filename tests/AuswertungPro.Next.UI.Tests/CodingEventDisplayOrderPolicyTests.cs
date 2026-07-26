using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventDisplayOrderPolicyTests
{
    [Fact]
    public void Order_sorts_by_meter_then_video_timestamp()
    {
        var lateMeter = Event("LATE", meter: 9, seconds: 1);
        var lateTime = Event("LATE_TIME", meter: 2, seconds: 7);
        var earlyTime = Event("EARLY_TIME", meter: 2, seconds: 3);

        var ordered = CodingEventDisplayOrderPolicy.Order(
            new[] { lateMeter, lateTime, earlyTime });

        Assert.Equal(new[] { earlyTime, lateTime, lateMeter }, ordered);
    }

    [Fact]
    public void Order_preserves_input_order_when_meter_and_video_timestamp_are_equal()
    {
        var first = Event("FIRST", meter: 4, seconds: 2);
        var second = Event("SECOND", meter: 4, seconds: 2);

        var ordered = CodingEventDisplayOrderPolicy.Order(
            new[] { first, second });

        Assert.Equal(new[] { first, second }, ordered);
    }

    private static CodingEvent Event(string code, double meter, int seconds)
        => new()
        {
            Entry = new ProtocolEntry { Code = code },
            MeterAtCapture = meter,
            VideoTimestamp = TimeSpan.FromSeconds(seconds)
        };
}
