using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSliderSeekControllerTests
{
    [Fact]
    public void SeekToSlider_sets_time_and_updates_ui_when_duration_is_known()
    {
        var method = FindSeekToSliderMethod();
        Assert.NotNull(method);
        var calls = new List<string>();
        long? timeMs = null;

        var result = method.Invoke(null, [
            25d,
            100d,
            120_000L,
            new Action<long>(value =>
            {
                calls.Add("time");
                timeMs = value;
            }),
            new Action<float>(_ => calls.Add("position")),
            new Action(() => calls.Add("update"))
        ]);

        Assert.Equal(true, result);
        Assert.Equal(30_000L, timeMs);
        Assert.Equal(["time", "update"], calls);
    }

    [Fact]
    public void SeekToSlider_sets_position_and_updates_ui_when_duration_is_unknown()
    {
        var method = FindSeekToSliderMethod();
        Assert.NotNull(method);
        var calls = new List<string>();
        float? position = null;

        var result = method.Invoke(null, [
            25d,
            100d,
            0L,
            new Action<long>(_ => calls.Add("time")),
            new Action<float>(value =>
            {
                calls.Add("position");
                position = value;
            }),
            new Action(() => calls.Add("update"))
        ]);

        Assert.Equal(true, result);
        Assert.Equal(0.25f, position);
        Assert.Equal(["position", "update"], calls);
    }

    [Fact]
    public void SeekToSlider_returns_false_without_moving_player_when_slider_maximum_is_invalid()
    {
        var method = FindSeekToSliderMethod();
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.Invoke(null, [
            25d,
            0d,
            120_000L,
            new Action<long>(_ => calls.Add("time")),
            new Action<float>(_ => calls.Add("position")),
            new Action(() => calls.Add("update"))
        ]);

        Assert.Equal(false, result);
        Assert.Empty(calls);
    }

    [Fact]
    public void UpdateSeekPreview_applies_preview_and_starts_scrub_timer_when_dragging()
    {
        var method = FindUpdateSeekPreviewMethod();
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.Invoke(null, [
            25d,
            100d,
            120_000L,
            true,
            false,
            new Action<double, long>((ratio, duration) => calls.Add($"preview:{ratio:0.##}:{duration}")),
            new Action(() => calls.Add("start"))
        ]);

        Assert.Equal(true, result);
        Assert.Equal(["preview:0.25:120000", "start"], calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void UpdateSeekPreview_does_not_start_scrub_timer_when_not_needed(
        bool isDragging,
        bool isScrubTimerEnabled)
    {
        var method = FindUpdateSeekPreviewMethod();
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.Invoke(null, [
            25d,
            100d,
            120_000L,
            isDragging,
            isScrubTimerEnabled,
            new Action<double, long>((_, _) => calls.Add("preview")),
            new Action(() => calls.Add("start"))
        ]);

        Assert.Equal(true, result);
        Assert.Equal(["preview"], calls);
    }

    [Fact]
    public void ScrubSeekToSlider_moves_player_and_applies_scrub_preview()
    {
        var method = FindScrubSeekToSliderMethod();
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.Invoke(null, [
            50d,
            100d,
            120_000L,
            new Action<long>(value => calls.Add($"time:{value}")),
            new Action<float>(_ => calls.Add("position")),
            new Action<double, long>((ratio, duration) => calls.Add($"scrub:{ratio:0.##}:{duration}"))
        ]);

        Assert.Equal(true, result);
        Assert.Equal(["time:60000", "scrub:0.5:120000"], calls);
    }

    private static MethodInfo? FindSeekToSliderMethod()
        => FindMethod(
            "SeekToSlider",
            typeof(double),
            typeof(double),
            typeof(long),
            typeof(Action<long>),
            typeof(Action<float>),
            typeof(Action));

    private static MethodInfo? FindUpdateSeekPreviewMethod()
        => FindMethod(
            "UpdateSeekPreview",
            typeof(double),
            typeof(double),
            typeof(long),
            typeof(bool),
            typeof(bool),
            typeof(Action<double, long>),
            typeof(Action));

    private static MethodInfo? FindScrubSeekToSliderMethod()
        => FindMethod(
            "ScrubSeekToSlider",
            typeof(double),
            typeof(double),
            typeof(long),
            typeof(Action<long>),
            typeof(Action<float>),
            typeof(Action<double, long>));

    private static MethodInfo? FindMethod(string name, params Type[] parameterTypes)
        => typeof(PlayerPlaybackState).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerSliderSeekController")
            ?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null);
}
