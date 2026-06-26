using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowShutdownStateControllerTests
{
    [Fact]
    public void Initial_state_is_available()
    {
        var controller = new PlayerWindowShutdownStateController();

        Assert.False(controller.IsClosing);
        Assert.False(controller.IsPlaybackDisposed);
        Assert.False(controller.IsUnavailable);
    }

    [Fact]
    public void MarkClosing_sets_closing_and_unavailable()
    {
        var controller = new PlayerWindowShutdownStateController();

        controller.MarkClosing();

        Assert.True(controller.IsClosing);
        Assert.False(controller.IsPlaybackDisposed);
        Assert.True(controller.IsUnavailable);
    }

    [Fact]
    public void MarkPlaybackDisposed_sets_disposed_and_unavailable()
    {
        var controller = new PlayerWindowShutdownStateController();

        controller.MarkPlaybackDisposed();

        Assert.False(controller.IsClosing);
        Assert.True(controller.IsPlaybackDisposed);
        Assert.True(controller.IsUnavailable);
    }
}
