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
    [InlineData(-500, 100000, 0)]
    [InlineData(5000, 100000, 5000)]
    [InlineData(120000, 100000, 100000)]
    [InlineData(120000, 0, 120000)]
    public void ResolveSeekTargetMs_clamps_time_to_known_video_duration(
        double requestedMs,
        long durationMs,
        long expectedMs)
    {
        var targetMs = PlayerPlaybackState.ResolveSeekTargetMs(
            TimeSpan.FromMilliseconds(requestedMs),
            durationMs);

        Assert.Equal(expectedMs, targetMs);
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

    [Fact]
    public void ResolveSliderSeekTarget_rejects_invalid_slider_maximum()
    {
        var target = PlayerPlaybackState.ResolveSliderSeekTarget(50, 0, 100000);

        Assert.False(target.IsValid);
        Assert.Equal(0, target.Ratio);
        Assert.Null(target.TimeMs);
        Assert.Null(target.Position);
    }

    [Fact]
    public void ResolveSliderSeekTarget_returns_time_when_duration_is_known()
    {
        var target = PlayerPlaybackState.ResolveSliderSeekTarget(25, 100, 120000);

        Assert.True(target.IsValid);
        Assert.Equal(0.25, target.Ratio, precision: 6);
        Assert.Equal(30000, target.TimeMs);
        Assert.Null(target.Position);
    }

    [Fact]
    public void ResolveSliderSeekTarget_returns_position_when_duration_is_unknown()
    {
        var target = PlayerPlaybackState.ResolveSliderSeekTarget(150, 100, 0);

        Assert.True(target.IsValid);
        Assert.Equal(1, target.Ratio, precision: 6);
        Assert.Null(target.TimeMs);
        Assert.Equal(1.0f, target.Position);
    }

    [Fact]
    public void BuildUiState_formats_known_duration_and_slider_value()
    {
        var state = PlayerPlaybackState.BuildUiState(30_000, 120_000, sliderMaximum: 100);

        Assert.Equal(25, state.SliderValue);
        Assert.Equal("00:30", state.CurrentTimeText);
        Assert.Equal("02:00", state.DurationText);
    }

    [Fact]
    public void BuildUiState_formats_unknown_duration_without_slider_value()
    {
        var state = PlayerPlaybackState.BuildUiState(61_000, 0, sliderMaximum: 100);

        Assert.Null(state.SliderValue);
        Assert.Equal("01:01", state.CurrentTimeText);
        Assert.Equal("--:--", state.DurationText);
    }

    [Fact]
    public void BuildUiState_clamps_negative_time_for_display_and_slider()
    {
        var state = PlayerPlaybackState.BuildUiState(-500, 100_000, sliderMaximum: 100);

        Assert.Equal(0, state.SliderValue);
        Assert.Equal("00:00", state.CurrentTimeText);
        Assert.Equal("01:40", state.DurationText);
    }

    [Theory]
    [InlineData(0.0f, "1x")]
    [InlineData(-1.0f, "1x")]
    [InlineData(1.5f, "1.5x")]
    [InlineData(1.25f, "1.25x")]
    public void FormatRateLabel_uses_normal_speed_when_current_rate_is_invalid(float rate, string expected)
    {
        Assert.Equal(expected, PlayerPlaybackState.FormatRateLabel(rate));
    }

    [Theory]
    [InlineData(1.0f, 1.0f, true)]
    [InlineData(1.009f, 1.0f, true)]
    [InlineData(1.02f, 1.0f, false)]
    public void IsRateButtonChecked_uses_existing_rate_tolerance(
        float currentRate,
        float targetRate,
        bool expected)
    {
        Assert.Equal(expected, PlayerPlaybackState.IsRateButtonChecked(currentRate, targetRate));
    }

    [Fact]
    public void BuildSeekPreviewText_formats_target_time_when_duration_is_known()
    {
        var preview = PlayerPlaybackState.BuildSeekPreviewText(0.25, 120_000);

        Assert.Equal("00:30", preview.CurrentTimeText);
        Assert.Equal("02:00", preview.DurationText);
    }

    [Fact]
    public void BuildSeekPreviewText_formats_percent_when_duration_is_unknown()
    {
        var preview = PlayerPlaybackState.BuildSeekPreviewText(0.25, 0);

        Assert.Equal(0.25.ToString("P0"), preview.CurrentTimeText);
        Assert.Equal("--:--", preview.DurationText);
    }
}
