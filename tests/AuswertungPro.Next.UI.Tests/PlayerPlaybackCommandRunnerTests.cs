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

    [Fact]
    public void SetSpeed_clamps_rate_sets_player_rate_and_updates_label()
    {
        var method = FindMethod("SetSpeed", typeof(float), typeof(Func<float, int>), typeof(Action<float>), typeof(Action));
        Assert.NotNull(method);
        var calls = new List<string>();

        method.Invoke(null, [
            12f,
            new Func<float, int>(rate =>
            {
                calls.Add($"set:{rate:0.##}");
                return 0;
            }),
            new Action<float>(rate => calls.Add($"unsupported:{rate:0.##}")),
            new Action(() => calls.Add("rate-label"))
        ]);

        Assert.Equal(["set:8", "rate-label"], calls);
    }

    [Fact]
    public void SetSpeed_shows_unsupported_rate_dialog_when_player_rejects_rate()
    {
        var method = FindMethod("SetSpeed", typeof(float), typeof(Func<float, int>), typeof(Action<float>), typeof(Action));
        Assert.NotNull(method);
        var calls = new List<string>();

        method.Invoke(null, [
            0.5f,
            new Func<float, int>(rate =>
            {
                calls.Add($"set:{rate:0.##}");
                return -1;
            }),
            new Action<float>(rate => calls.Add($"unsupported:{rate:0.##}")),
            new Action(() => calls.Add("rate-label"))
        ]);

        Assert.Equal(["set:0.5", "unsupported:0.5", "rate-label"], calls);
    }

    [Fact]
    public void TogglePlayPause_ensures_playback_and_sets_pause_to_current_playing_state()
    {
        var method = FindMethod("TogglePlayPause", typeof(Action), typeof(Func<bool>), typeof(Action<bool>));
        Assert.NotNull(method);
        var calls = new List<string>();

        method.Invoke(null, [
            new Action(() => calls.Add("ensure")),
            new Func<bool>(() =>
            {
                calls.Add("is-playing");
                return true;
            }),
            new Action<bool>(pause => calls.Add(pause ? "pause" : "resume"))
        ]);

        Assert.Equal(["ensure", "is-playing", "pause"], calls);
    }

    [Fact]
    public void JumpSeconds_moves_player_clears_detection_overlays_and_updates_ui()
    {
        var method = FindMethod(
            "JumpSeconds",
            typeof(long),
            typeof(long),
            typeof(int),
            typeof(Action<long>),
            typeof(Action),
            typeof(Action));
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.Invoke(null, [
            10_000L,
            120_000L,
            30,
            new Action<long>(value => calls.Add($"time:{value}")),
            new Action(() => calls.Add("clear")),
            new Action(() => calls.Add("update"))
        ]);

        Assert.Equal(true, result);
        Assert.Equal(["time:40000", "clear", "update"], calls);
    }

    [Fact]
    public void JumpSeconds_skips_when_duration_is_unknown()
    {
        var method = FindMethod(
            "JumpSeconds",
            typeof(long),
            typeof(long),
            typeof(int),
            typeof(Action<long>),
            typeof(Action),
            typeof(Action));
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.Invoke(null, [
            10_000L,
            0L,
            30,
            new Action<long>(_ => calls.Add("time")),
            new Action(() => calls.Add("clear")),
            new Action(() => calls.Add("update"))
        ]);

        Assert.Equal(false, result);
        Assert.Empty(calls);
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
