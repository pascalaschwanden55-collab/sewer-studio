using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPlaybackStateTests
{
    [Theory]
    [InlineData(0.1f, 0.25f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(8.0f, 8.0f)]
    [InlineData(9.0f, 8.0f)]
    public void ClampRate_keeps_supported_speed_range(float input, float expected)
    {
        Assert.Equal(expected, PlayerPlaybackState.ClampRate(input));
    }

    [Theory]
    [InlineData(0.0f, 0.25f, 1.25f)]
    [InlineData(-1.0f, 0.25f, 1.25f)]
    [InlineData(1.0f, 0.5f, 1.5f)]
    [InlineData(8.0f, 1.0f, 8.0f)]
    public void ApplyRateDelta_uses_normal_speed_when_current_rate_is_invalid(float currentRate, float delta, float expected)
    {
        Assert.Equal(expected, PlayerPlaybackState.ApplyRateDelta(currentRate, delta));
    }

    [Theory]
    [InlineData(1000, 5, 6000)]
    [InlineData(1000, -5, 0)]
    [InlineData(99000, 5, 100000)]
    public void AddSeconds_clamps_to_video_duration(long currentMs, int deltaSeconds, long expectedMs)
    {
        var next = PlayerPlaybackState.AddSeconds(currentMs, 100000, deltaSeconds);

        Assert.Equal(expectedMs, next);
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(61000, "01:01")]
    [InlineData(3599000, "59:59")]
    [InlineData(3600000, "01:00:00")]
    [InlineData(3661000, "01:01:01")]
    public void FormatMilliseconds_uses_hour_format_only_when_needed(long milliseconds, string expected)
    {
        Assert.Equal(expected, PlayerPlaybackState.FormatMilliseconds(milliseconds));
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(25, 100, 0.25)]
    [InlineData(150, 100, 1)]
    [InlineData(-20, 100, 0)]
    public void TryResolveSliderRatio_clamps_slider_value(double value, double max, double expected)
    {
        var ok = PlayerPlaybackState.TryResolveSliderRatio(value, max, out var ratio);

        Assert.True(ok);
        Assert.Equal(expected, ratio, precision: 6);
    }

    [Fact]
    public void TryResolveSliderRatio_rejects_invalid_slider_maximum()
    {
        var ok = PlayerPlaybackState.TryResolveSliderRatio(50, 0, out var ratio);

        Assert.False(ok);
        Assert.Equal(0, ratio);
    }
}
