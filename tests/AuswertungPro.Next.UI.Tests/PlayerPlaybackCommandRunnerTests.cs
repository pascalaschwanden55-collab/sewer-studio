using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPlaybackCommandRunnerTests
{
    [Fact]
    public void Play_ensures_playback_resumes_updates_rate_and_clears_detection_overlays()
    {
        var method = FindMethod("Play", typeof(Action), typeof(Action<bool>), typeof(Action), typeof(Action));
        Assert.NotNull(method);
        var calls = new List<string>();

        method.Invoke(null, [
            new Action(() => calls.Add("ensure")),
            new Action<bool>(pause => calls.Add(pause ? "pause" : "resume")),
            new Action(() => calls.Add("rate")),
            new Action(() => calls.Add("clear"))
        ]);

        Assert.Equal(["ensure", "resume", "rate", "clear"], calls);
    }

    [Fact]
    public void Pause_pauses_and_updates_rate()
    {
        var method = FindMethod("Pause", typeof(Action<bool>), typeof(Action));
        Assert.NotNull(method);
        var calls = new List<string>();

        method.Invoke(null, [
            new Action<bool>(pause => calls.Add(pause ? "pause" : "resume")),
            new Action(() => calls.Add("rate"))
        ]);

        Assert.Equal(["pause", "rate"], calls);
    }

    [Fact]
    public void Stop_stops_and_updates_rate()
    {
        var method = FindMethod("Stop", typeof(Action), typeof(Action));
        Assert.NotNull(method);
        var calls = new List<string>();

        method.Invoke(null, [
            new Action(() => calls.Add("stop")),
            new Action(() => calls.Add("rate"))
        ]);

        Assert.Equal(["stop", "rate"], calls);
    }

    private static MethodInfo? FindMethod(string name, params Type[] parameterTypes)
        => typeof(PlayerPlaybackState).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerPlaybackCommandRunner")
            ?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null);
}
