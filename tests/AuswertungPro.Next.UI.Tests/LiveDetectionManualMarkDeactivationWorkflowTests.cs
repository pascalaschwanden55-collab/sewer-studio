using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionManualMarkDeactivationWorkflowTests
{
    [Fact]
    public void Execute_keeps_coding_overlay_active_when_coding_mode_is_open()
    {
        var calls = new List<string>();

        var result = LiveDetectionManualMarkDeactivationWorkflow.Execute(
            new LiveDetectionManualMarkDeactivationWorkflowRequest(
                IsCodingMode: true,
                IsLiveDetectionRunning: true),
            Actions(calls));

        Assert.Equal(
            [
                "mark:None",
                "manual:False",
                "label",
                "detection:True"
            ],
            calls);
        Assert.Equal(LiveDetectionManualMarkDeactivationWorkflowOutcome.CodingOverlayKept, result.Outcome);
    }

    [Fact]
    public void Execute_tears_down_coding_overlay_outside_coding_mode()
    {
        var calls = new List<string>();

        var result = LiveDetectionManualMarkDeactivationWorkflow.Execute(
            new LiveDetectionManualMarkDeactivationWorkflowRequest(
                IsCodingMode: false,
                IsLiveDetectionRunning: false),
            Actions(calls));

        Assert.Equal(
            [
                "mark:None",
                "manual:False",
                "label",
                "detection:False",
                "cancel-schema",
                "cancel-draw",
                "active:None",
                "deactivate-overlay"
            ],
            calls);
        Assert.Equal(LiveDetectionManualMarkDeactivationWorkflowOutcome.CodingOverlayDeactivated, result.Outcome);
    }

    private static LiveDetectionManualMarkDeactivationWorkflowActions Actions(List<string> calls)
        => new(
            SetMarkToolType: tool => calls.Add($"mark:{tool}"),
            SetManualMarkMode: enabled => calls.Add($"manual:{enabled}"),
            ResetToolLabel: () => calls.Add("label"),
            DeactivateDetectionSide: running => calls.Add($"detection:{running}"),
            CancelSchema: () => calls.Add("cancel-schema"),
            CancelDraw: () => calls.Add("cancel-draw"),
            SetActiveTool: tool => calls.Add($"active:{tool}"),
            DeactivateCodingOverlay: () => calls.Add("deactivate-overlay"));
}
