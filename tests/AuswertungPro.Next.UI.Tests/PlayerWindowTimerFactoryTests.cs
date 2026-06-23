using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowTimerFactoryTests
{
    [Fact]
    public void CreateUpdateTimer_uses_quarter_second_interval_and_starts_disabled()
    {
        var timer = PlayerWindowTimerFactory.CreateUpdateTimer(() => { });

        Assert.Equal(TimeSpan.FromMilliseconds(250), timer.Interval);
        Assert.False(timer.IsEnabled);
    }

    [Fact]
    public void CreateScrubTimer_uses_sixty_millisecond_interval_and_starts_disabled()
    {
        var timer = PlayerWindowTimerFactory.CreateScrubTimer(() => { });

        Assert.Equal(TimeSpan.FromMilliseconds(60), timer.Interval);
        Assert.False(timer.IsEnabled);
    }
}
