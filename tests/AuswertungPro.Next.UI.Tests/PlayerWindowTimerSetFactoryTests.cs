using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowTimerSetFactoryTests
{
    [Fact]
    public void Create_builds_update_and_scrub_timers()
    {
        var timers = PlayerWindowTimerSetFactory.Create(
            createRequest: () => new PlayerWindowTimerTickWorkflowRequest(
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsDragging: false),
            actions: new PlayerWindowTimerTickWorkflowActions(
                UpdateUi: () => { },
                ScrubSeekToSlider: () => { }));

        Assert.Equal(TimeSpan.FromMilliseconds(250), timers.UpdateTimer.Interval);
        Assert.False(timers.UpdateTimer.IsEnabled);
        Assert.Equal(TimeSpan.FromMilliseconds(60), timers.ScrubTimer.Interval);
        Assert.False(timers.ScrubTimer.IsEnabled);
    }
}
