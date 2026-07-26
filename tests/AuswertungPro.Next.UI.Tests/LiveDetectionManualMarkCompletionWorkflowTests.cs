using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionManualMarkCompletionWorkflowTests
{
    [Fact]
    public void Execute_deactivates_mark_tool_after_saved_mark_outside_coding_mode()
    {
        var calls = new List<string>();

        var result = LiveDetectionManualMarkCompletionWorkflow.Execute(
            new LiveDetectionManualMarkCompletionWorkflowRequest(
                Saved: true,
                IsCodingMode: false,
                MarkToolType: OverlayToolType.Rectangle),
            Actions(calls));

        Assert.Equal(
            [
                "clear-sam",
                "clear-bend",
                "clear-overlay",
                "redraw",
                "deactivate"
            ],
            calls);
        Assert.Equal(LiveDetectionManualMarkCompletionWorkflowOutcome.Deactivated, result.Outcome);
    }

    [Fact]
    public void Execute_keeps_mark_tool_active_in_coding_mode()
    {
        var calls = new List<string>();

        var result = LiveDetectionManualMarkCompletionWorkflow.Execute(
            new LiveDetectionManualMarkCompletionWorkflowRequest(
                Saved: true,
                IsCodingMode: true,
                MarkToolType: OverlayToolType.Freehand),
            Actions(calls));

        Assert.Equal(
            [
                "clear-sam",
                "clear-bend",
                "clear-overlay",
                "redraw",
                "active:Freehand",
                "cursor"
            ],
            calls);
        Assert.Equal(LiveDetectionManualMarkCompletionWorkflowOutcome.KeptActive, result.Outcome);
    }

    [Fact]
    public void Execute_keeps_mark_tool_active_after_cancelled_mark()
    {
        var calls = new List<string>();

        var result = LiveDetectionManualMarkCompletionWorkflow.Execute(
            new LiveDetectionManualMarkCompletionWorkflowRequest(
                Saved: false,
                IsCodingMode: false,
                MarkToolType: OverlayToolType.Ellipse),
            Actions(calls));

        Assert.Equal(
            [
                "clear-sam",
                "clear-bend",
                "clear-overlay",
                "redraw",
                "active:Ellipse",
                "cursor"
            ],
            calls);
        Assert.Equal(LiveDetectionManualMarkCompletionWorkflowOutcome.KeptActive, result.Outcome);
    }

    private static LiveDetectionManualMarkCompletionWorkflowActions Actions(List<string> calls)
        => new(
            ClearSamMasks: () => calls.Add("clear-sam"),
            ClearBendMarker: () => calls.Add("clear-bend"),
            ClearCurrentOverlay: () => calls.Add("clear-overlay"),
            RedrawCodingCanvasWithoutManualOverlay: () => calls.Add("redraw"),
            DeactivateMarkTool: () => calls.Add("deactivate"),
            SetActiveTool: tool => calls.Add($"active:{tool}"),
            ApplyCrossCursor: () => calls.Add("cursor"));
}
