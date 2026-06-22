using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSliderTrackBoundsTests
{
    [Theory]
    [InlineData(100, 9, 82)]
    [InlineData(18, 9, 1)]
    [InlineData(0, 9, 1)]
    public void ResolveFallback_keeps_existing_slider_track_fallback(double surfaceWidth, double expectedOffset, double expectedWidth)
    {
        var bounds = PlayerSliderTrackBounds.ResolveFallback(surfaceWidth);

        Assert.Equal(expectedOffset, bounds.offsetX);
        Assert.Equal(expectedWidth, bounds.trackWidth);
    }
}
