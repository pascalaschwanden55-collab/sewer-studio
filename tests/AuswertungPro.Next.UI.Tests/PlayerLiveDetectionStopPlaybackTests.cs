using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerLiveDetectionStopPlaybackTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void PauseIfRunning_skips_when_player_is_missing_disposed_or_not_playing(
        bool hasPlayer,
        bool isPlaybackDisposed,
        bool isPlaying)
    {
        var method = FindMethod();
        Assert.NotNull(method);
        var calls = new List<bool>();

        method.Invoke(null, [hasPlayer, isPlaybackDisposed, isPlaying, new Action<bool>(pause => calls.Add(pause))]);

        Assert.Empty(calls);
    }

    [Fact]
    public void PauseIfRunning_sets_pause_true_when_player_is_available_and_playing()
    {
        var method = FindMethod();
        Assert.NotNull(method);
        bool? pauseValue = null;

        method.Invoke(null, [true, false, true, new Action<bool>(pause => pauseValue = pause)]);

        Assert.True(pauseValue);
    }

    private static MethodInfo? FindMethod()
        => typeof(PlayerKeyboardActionController).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerLiveDetectionStopPlayback")
            ?.GetMethod(
                "PauseIfRunning",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(bool), typeof(bool), typeof(bool), typeof(Action<bool>)],
                modifiers: null);
}
