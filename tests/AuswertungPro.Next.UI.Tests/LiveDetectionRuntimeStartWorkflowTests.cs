using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionRuntimeStartWorkflowTests
{
    [Fact]
    public void Start_stores_runtime_prepares_ui_starts_timer_and_runs_first_detection_in_order()
    {
        var calls = new List<string>();
        var runtime = new LiveDetectionRuntime(null!, null!, "models/qwen2.5-vl:7b");

        LiveDetectionRuntimeStartWorkflow.Start(
            runtime,
            new LiveDetectionRuntimeStartActions(
                StoreRuntime: value => calls.Add($"store:{value.VisionModel}"),
                ResetCancellation: () => calls.Add("reset-cancellation"),
                MarkDetecting: () => calls.Add("detecting"),
                ShowOverlay: () => calls.Add("overlay"),
                ApplyActiveStatus: status =>
                {
                    Assert.Equal("KI aktiv", status.BadgeText);
                    Assert.Equal(PlayerStatusColors.Success, status.StatusColor);
                    Assert.Equal("Modell: qwen2.5-vl:7b", status.BadgeDetails);
                    Assert.Equal("Aktiv", status.YoloText);
                    Assert.Equal("qwen2.5-vl:7b", status.ModelLabel);
                    calls.Add("status");
                },
                ShowWaitingForFrame: () => calls.Add("waiting"),
                StartTimer: () => calls.Add("timer"),
                RunFirstDetection: () => calls.Add("run")));

        Assert.Equal(
            [
                "store:models/qwen2.5-vl:7b",
                "reset-cancellation",
                "detecting",
                "overlay",
                "status",
                "waiting",
                "timer",
                "run"
            ],
            calls);
    }
}
