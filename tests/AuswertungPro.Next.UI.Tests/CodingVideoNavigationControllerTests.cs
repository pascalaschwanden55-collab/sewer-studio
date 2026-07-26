using System.Reflection;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingVideoNavigationControllerTests
{
    [Fact]
    public void ResolveDisplayMeter_prefers_osd_meter()
    {
        var method = FindResolveDisplayMeterMethod();
        Assert.NotNull(method);

        var result = method.Invoke(null, [12.34, 5_000L, 10_000L, 100d, 20d]);

        Assert.Equal(12.34, result);
    }

    [Fact]
    public void ResolveDisplayMeter_falls_back_to_video_ratio()
    {
        var method = FindResolveDisplayMeterMethod();
        Assert.NotNull(method);

        var result = method.Invoke(null, [null, 2_500L, 10_000L, 100d, 20d]);

        Assert.Equal(25d, result);
    }

    [Fact]
    public void SyncVideoToCodingMeter_sets_player_time_then_records_actual_player_time()
    {
        var method = FindSyncVideoToCodingMeterMethod();
        Assert.NotNull(method);
        var calls = new List<string>();
        long currentPlayerTime = 0;

        var result = method.Invoke(null, [
            25d,
            100d,
            10_000L,
            new Action<long>(target =>
            {
                calls.Add($"set:{target}");
                currentPlayerTime = target + 123;
            }),
            new Func<long>(() => currentPlayerTime),
            new Action<TimeSpan>(time => calls.Add($"vm:{time.TotalMilliseconds:0}"))
        ]);

        Assert.Equal(true, result);
        Assert.Equal(["set:2500", "vm:2623"], calls);
    }

    [Fact]
    public void SyncVideoToCodingMeter_skips_when_target_cannot_be_resolved()
    {
        var method = FindSyncVideoToCodingMeterMethod();
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.Invoke(null, [
            25d,
            0d,
            10_000L,
            new Action<long>(_ => calls.Add("set")),
            new Func<long>(() => 0),
            new Action<TimeSpan>(_ => calls.Add("vm"))
        ]);

        Assert.Equal(false, result);
        Assert.Empty(calls);
    }

    [Fact]
    public void PrepareMoveByCommand_sets_pending_executes_move_pauses_and_resets_tracking()
    {
        var method = FindPrepareMoveByCommandMethod();
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.MakeGenericMethod(typeof(string)).Invoke(null, [
            "vm",
            new Action<string>(vm => calls.Add($"move:{vm}")),
            new Action(() => calls.Add("pending")),
            new Action(() => calls.Add("pause")),
            new Action(() => calls.Add("reset"))
        ]);

        Assert.Equal(true, result);
        Assert.Equal(["pending", "move:vm", "pause", "reset"], calls);
    }

    [Fact]
    public void PrepareMoveByCommand_returns_false_without_side_effects_when_view_model_is_missing()
    {
        var method = FindPrepareMoveByCommandMethod();
        Assert.NotNull(method);
        var calls = new List<string>();

        var result = method.MakeGenericMethod(typeof(string)).Invoke(null, [
            null,
            new Action<string>(_ => calls.Add("move")),
            new Action(() => calls.Add("pending")),
            new Action(() => calls.Add("pause")),
            new Action(() => calls.Add("reset"))
        ]);

        Assert.Equal(false, result);
        Assert.Empty(calls);
    }

    private static MethodInfo? FindResolveDisplayMeterMethod()
        => typeof(CodingVideoSyncPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingVideoNavigationController")
            ?.GetMethod(
                "ResolveDisplayMeter",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(double?), typeof(long), typeof(long), typeof(double), typeof(double)],
                modifiers: null);

    private static MethodInfo? FindSyncVideoToCodingMeterMethod()
        => typeof(CodingVideoSyncPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingVideoNavigationController")
            ?.GetMethod(
                "SyncVideoToCodingMeter",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(double), typeof(double), typeof(long), typeof(Action<long>), typeof(Func<long>), typeof(Action<TimeSpan>)],
                modifiers: null);

    private static MethodInfo? FindPrepareMoveByCommandMethod()
        => typeof(CodingVideoSyncPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingVideoNavigationController")
            ?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(method => method.Name == "PrepareMoveByCommand" && method.IsGenericMethodDefinition);
}
