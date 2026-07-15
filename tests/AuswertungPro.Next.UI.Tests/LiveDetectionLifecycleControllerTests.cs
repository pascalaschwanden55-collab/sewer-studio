using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionLifecycleControllerTests
{
    [Fact]
    public async Task HandleClickAsync_stops_and_unchecks_when_detection_is_running()
    {
        var calls = new List<string>();
        var controller = new LiveDetectionLifecycleController(
            Actions(
                isDetecting: () => true,
                stopLiveDetection: () => calls.Add("stop"),
                uncheckToggle: () => calls.Add("uncheck"),
                startWithDisplayAsync: _ => throw new InvalidOperationException("Start must not run.")));

        await controller.HandleClickAsync();

        Assert.Equal(["stop", "uncheck"], calls);
    }

    [Fact]
    public async Task HandleClickAsync_starts_runtime_with_the_existing_action_order()
    {
        var calls = new List<string>();
        var runtime = new LiveDetectionRuntime(null!, null!, "qwen3-vl:8b-q8");
        var status = new LiveDetectionRuntimeStartStatus(
            "KI aktiv",
            PlayerStatusColors.Success,
            "Modell: qwen3-vl:8b-q8",
            "Aktiv",
            "qwen3-vl:8b-q8");
        var controller = new LiveDetectionLifecycleController(
            Actions(
                isDetecting: () => false,
                startWithDisplayAsync: actions =>
                {
                    calls.Add("display");
                    actions.StartRuntime(runtime);
                    return Task.FromResult(true);
                },
                startRuntime: (actualRuntime, actions) =>
                {
                    calls.Add($"runtime:{actualRuntime.VisionModel}");
                    actions.ShowOverlay();
                    actions.ApplyActiveStatus(status);
                    actions.ShowWaitingForFrame();
                    actions.TimerTick(null, EventArgs.Empty);
                    actions.RunFirstDetection();
                },
                showOverlay: () => calls.Add("overlay"),
                applyActiveStatus: actual => calls.Add($"status:{actual.BadgeText}|{actual.YoloText}"),
                showWaitingForFrame: () => calls.Add("waiting"),
                timerTick: (_, _) => calls.Add("timer"),
                runFirstDetection: () => calls.Add("detect")));

        await controller.HandleClickAsync();

        Assert.Equal(
            [
                "display",
                "runtime:qwen3-vl:8b-q8",
                "overlay",
                "status:KI aktiv|Aktiv",
                "waiting",
                "timer",
                "detect"
            ],
            calls);
    }

    private static LiveDetectionLifecycleControllerActions Actions(
        Func<bool>? isDetecting = null,
        Action? stopLiveDetection = null,
        Action? uncheckToggle = null,
        Func<LiveDetectionStartupActions, Task<bool>>? startWithDisplayAsync = null,
        Action<LiveDetectionRuntime, LiveDetectionControllerStartActions>? startRuntime = null,
        Action? showOverlay = null,
        Action<LiveDetectionRuntimeStartStatus>? applyActiveStatus = null,
        Action? showWaitingForFrame = null,
        EventHandler? timerTick = null,
        Action? runFirstDetection = null)
        => new(
            IsDetecting: isDetecting ?? (() => false),
            StopLiveDetection: stopLiveDetection ?? (() => { }),
            UncheckToggle: uncheckToggle ?? (() => { }),
            StartWithDisplayAsync: startWithDisplayAsync ?? (_ => Task.FromResult(true)),
            StartRuntime: startRuntime ?? ((_, _) => { }),
            ShowOverlay: showOverlay ?? (() => { }),
            ApplyActiveStatus: applyActiveStatus ?? (_ => { }),
            ShowWaitingForFrame: showWaitingForFrame ?? (() => { }),
            TimerTick: timerTick ?? ((_, _) => { }),
            RunFirstDetection: runFirstDetection ?? (() => { }));
}
