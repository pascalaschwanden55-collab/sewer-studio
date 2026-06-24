using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerConfirmationPlaybackTests
{
    [Fact]
    public void PauseCodingConfirmation_sets_pause_true()
    {
        var method = FindMethod("PauseCodingConfirmation", typeof(Action<bool>));
        Assert.NotNull(method);
        bool? pauseValue = null;

        method.Invoke(null, [new Action<bool>(pause => pauseValue = pause)]);

        Assert.True(pauseValue);
    }

    [Fact]
    public void ResumeCodingLiveAi_sets_pause_false_only_when_live_ai_is_enabled()
    {
        var method = FindMethod("ResumeCodingLiveAi", typeof(bool), typeof(Action<bool>));
        Assert.NotNull(method);
        var calls = new List<bool>();

        method.Invoke(null, [false, new Action<bool>(pause => calls.Add(pause))]);
        method.Invoke(null, [true, new Action<bool>(pause => calls.Add(pause))]);

        Assert.Equal([false], calls);
    }

    [Fact]
    public void PauseLiveDetectionConfirmation_sets_pause_true_only_when_playback_is_running()
    {
        var method = FindMethod("PauseLiveDetectionConfirmation", typeof(bool), typeof(Action<bool>));
        Assert.NotNull(method);
        var calls = new List<bool>();

        method.Invoke(null, [false, new Action<bool>(pause => calls.Add(pause))]);
        method.Invoke(null, [true, new Action<bool>(pause => calls.Add(pause))]);

        Assert.Equal([true], calls);
    }

    private static MethodInfo? FindMethod(string name, params Type[] parameterTypes)
        => typeof(PlayerKeyboardActionController).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerConfirmationPlayback")
            ?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null);
}
