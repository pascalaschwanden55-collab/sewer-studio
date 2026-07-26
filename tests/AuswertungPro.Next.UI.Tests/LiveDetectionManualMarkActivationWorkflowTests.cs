using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionManualMarkActivationWorkflowTests
{
    [Fact]
    public void Execute_activates_point_tool_without_opening_coding_overlay()
    {
        var calls = new List<string>();

        var result = LiveDetectionManualMarkActivationWorkflow.Execute(
            new LiveDetectionManualMarkActivationWorkflowRequest(
                OverlayToolType.Point,
                "Punkt"),
            Actions(calls));

        Assert.Equal(
            [
                "begin:Punkt",
                "mark-tool:Point",
                "pause:True",
                "cancel-schema",
                "clear-schema-type",
                "manual-mode:True",
                "point-tool"
            ],
            calls);
        Assert.Equal(LiveDetectionManualMarkActivationWorkflowOutcome.PointToolActivated, result.Outcome);
    }

    [Fact]
    public void Execute_activates_drawing_tool_and_prepares_coding_overlay()
    {
        var calls = new List<string>();

        var result = LiveDetectionManualMarkActivationWorkflow.Execute(
            new LiveDetectionManualMarkActivationWorkflowRequest(
                OverlayToolType.Rectangle,
                "Rechteck"),
            Actions(calls));

        Assert.Equal(
            [
                "begin:Rechteck",
                "mark-tool:Rectangle",
                "pause:True",
                "cancel-schema",
                "clear-schema-type",
                "manual-mode:False",
                "ensure-overlay",
                "active-tool:Rectangle",
                "clear-overlay",
                "open-overlay",
                "viewport",
                "enable-input"
            ],
            calls);
        Assert.Equal(LiveDetectionManualMarkActivationWorkflowOutcome.DrawingToolActivated, result.Outcome);
    }

    private static LiveDetectionManualMarkActivationWorkflowActions Actions(List<string> calls)
        => new(
            BeginActivation: label => calls.Add($"begin:{label}"),
            SetMarkToolType: tool => calls.Add($"mark-tool:{tool}"),
            SetPause: pause => calls.Add($"pause:{pause}"),
            CancelSchema: () => calls.Add("cancel-schema"),
            ClearSchemaType: () => calls.Add("clear-schema-type"),
            SetManualMarkMode: enabled => calls.Add($"manual-mode:{enabled}"),
            ActivatePointTool: () => calls.Add("point-tool"),
            EnsureOverlayReady: () => calls.Add("ensure-overlay"),
            SetActiveTool: tool => calls.Add($"active-tool:{tool}"),
            ClearCurrentOverlay: () => calls.Add("clear-overlay"),
            OpenCodingOverlay: () => calls.Add("open-overlay"),
            UpdateCodingOverlayViewport: () => calls.Add("viewport"),
            EnableCodingOverlayInput: () => calls.Add("enable-input"));
}
