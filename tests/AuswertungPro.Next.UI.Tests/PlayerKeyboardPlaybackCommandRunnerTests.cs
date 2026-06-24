using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerKeyboardPlaybackCommandRunnerTests
{
    [Fact]
    public void Stop_invokes_stop_action()
    {
        var method = FindMethod("Stop", typeof(Action));
        Assert.NotNull(method);
        var called = false;

        method.Invoke(null, [new Action(() => called = true)]);

        Assert.True(called);
    }

    [Fact]
    public void Pause_sets_pause_true()
    {
        var method = FindMethod("Pause", typeof(Action<bool>));
        Assert.NotNull(method);
        bool? value = null;

        method.Invoke(null, [new Action<bool>(pause => value = pause)]);

        Assert.True(value);
    }

    [Fact]
    public void Resume_ensures_playback_then_sets_pause_false()
    {
        var method = FindMethod("Resume", typeof(Action), typeof(Action<bool>));
        Assert.NotNull(method);
        var calls = new List<string>();

        method.Invoke(null, [
            new Action(() => calls.Add("ensure")),
            new Action<bool>(pause => calls.Add(pause ? "pause" : "resume"))
        ]);

        Assert.Equal(["ensure", "resume"], calls);
    }

    private static MethodInfo? FindMethod(string name, params Type[] parameterTypes)
        => typeof(PlayerKeyboardActionController).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerKeyboardPlaybackCommandRunner")
            ?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null);
}
