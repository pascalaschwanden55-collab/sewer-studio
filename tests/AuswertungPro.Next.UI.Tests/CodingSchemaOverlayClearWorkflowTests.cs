using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaOverlayClearWorkflowTests
{
    [Fact]
    public void Execute_clears_schema_overlay_without_redraw()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayClearWorkflow.Execute(
            new CodingSchemaOverlayClearRequest(Redraw: false),
            Actions(calls.Add));

        Assert.Equal(CodingSchemaOverlayClearWorkflowOutcome.Cleared, result.Outcome);
        Assert.Equal(
            [
                "cancel",
                "clear-current",
                "create:false",
                "info:null"
            ],
            calls);
    }

    [Fact]
    public void Execute_clears_schema_overlay_and_redraws_canvas_when_requested()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayClearWorkflow.Execute(
            new CodingSchemaOverlayClearRequest(Redraw: true),
            Actions(calls.Add));

        Assert.Equal(CodingSchemaOverlayClearWorkflowOutcome.ClearedAndRedrawn, result.Outcome);
        Assert.Equal(
            [
                "cancel",
                "clear-current",
                "create:false",
                "info:null",
                "redraw:false"
            ],
            calls);
    }

    private static CodingSchemaOverlayClearActions Actions(Action<string> calls)
        => new(
            CancelSchema: () => calls("cancel"),
            ClearCurrentOverlay: () => calls("clear-current"),
            SetCreateEventEnabled: enabled => calls($"create:{enabled.ToString().ToLowerInvariant()}"),
            ClearOverlayInfo: () => calls("info:null"),
            RedrawCodingCanvas: includeManualOverlay => calls($"redraw:{includeManualOverlay.ToString().ToLowerInvariant()}"));
}
