using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerClockTests
{
    [Fact]
    public void Now_uses_supplied_time_provider_local_time()
    {
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 6, 23, 14, 15, 16, TimeSpan.Zero));

        var now = PlayerClock.Now(provider);

        Assert.Equal(new DateTime(2026, 6, 23, 14, 15, 16), now);
    }

    [Fact]
    public void UtcNow_uses_supplied_time_provider_utc_time()
    {
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 6, 23, 14, 15, 16, TimeSpan.Zero));

        var now = PlayerClock.UtcNow(provider);

        Assert.Equal(new DateTime(2026, 6, 23, 14, 15, 16, DateTimeKind.Utc), now);
    }

    [Fact]
    public void NowOffset_uses_supplied_time_provider_local_time()
    {
        var expected = new DateTimeOffset(2026, 6, 23, 14, 15, 16, TimeSpan.Zero);
        var provider = new FixedTimeProvider(expected);

        var now = PlayerClock.NowOffset(provider);

        Assert.Equal(expected, now);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow()
            => utcNow;
    }
}
