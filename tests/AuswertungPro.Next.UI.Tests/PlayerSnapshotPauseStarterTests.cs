using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSnapshotPauseStarterTests
{
    [Fact]
    public void PauseIfPlaying_pauses_waits_and_returns_true_when_playback_is_running()
    {
        var method = FindPauseIfPlayingMethod();
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.Invoke(null, [
            true,
            new Action(() => calls.Add("pause")),
            new Action(() => calls.Add("wait"))
        ]);

        Assert.Equal(true, result);
        Assert.Equal(["pause", "wait"], calls);
    }

    [Fact]
    public void PauseIfPlaying_skips_pause_and_returns_false_when_playback_is_not_running()
    {
        var method = FindPauseIfPlayingMethod();
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.Invoke(null, [
            false,
            new Action(() => calls.Add("pause")),
            new Action(() => calls.Add("wait"))
        ]);

        Assert.Equal(false, result);
        Assert.Empty(calls);
    }

    private static MethodInfo? FindPauseIfPlayingMethod()
        => typeof(PlayerSnapshotPathPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerSnapshotPauseStarter")
            ?.GetMethod(
                "PauseIfPlaying",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(bool), typeof(Action), typeof(Action)],
                modifiers: null);
}
