using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPositionSliderDragPlaybackTests
{
    [Fact]
    public void Start_pauses_playback_and_returns_true_when_player_was_playing()
    {
        var method = FindMethod("Start", typeof(bool), typeof(Action<bool>));
        Assert.NotNull(method);
        bool? pauseValue = null;

        var result = method.Invoke(null, [true, new Action<bool>(value => pauseValue = value)]);

        Assert.Equal(true, result);
        Assert.True(pauseValue);
    }

    [Fact]
    public void Start_returns_false_without_pausing_when_player_was_not_playing()
    {
        var method = FindMethod("Start", typeof(bool), typeof(Action<bool>));
        Assert.NotNull(method);
        bool called = false;

        var result = method.Invoke(null, [false, new Action<bool>(_ => called = true)]);

        Assert.Equal(false, result);
        Assert.False(called);
    }

    [Fact]
    public void Complete_resumes_when_player_was_playing_before_drag()
    {
        var method = FindMethod("Complete", typeof(bool), typeof(Action<bool>));
        Assert.NotNull(method);
        bool? pauseValue = null;

        method.Invoke(null, [true, new Action<bool>(value => pauseValue = value)]);

        Assert.False(pauseValue);
    }

    [Fact]
    public void Complete_skips_resume_when_player_was_not_playing_before_drag()
    {
        var method = FindMethod("Complete", typeof(bool), typeof(Action<bool>));
        Assert.NotNull(method);
        bool called = false;

        method.Invoke(null, [false, new Action<bool>(_ => called = true)]);

        Assert.False(called);
    }

    private static MethodInfo? FindMethod(string name, params Type[] parameterTypes)
        => typeof(PlayerPlaybackState).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerPositionSliderDragPlayback")
            ?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null);
}
