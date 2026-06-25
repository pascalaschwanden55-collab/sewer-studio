using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCanvasRedrawWorkflowTests
{
    [Fact]
    public void Execute_renders_schema_branch_in_order()
    {
        var calls = new List<string>();

        var result = CodingCanvasRedrawWorkflow.Execute(
            new CodingCanvasRedrawWorkflowRequest(
                IncludeManualOverlay: true,
                IsSchemaActive: true,
                HasCurrentOverlay: true),
            Actions(calls.Add));

        Assert.Equal(CodingCanvasRedrawWorkflowOutcome.SchemaRendered, result.Outcome);
        Assert.Equal(
            [
                "update-viewport",
                "clear-transient:true",
                "render-ai",
                "render-reference-dn",
                "render-active-schema",
                "update-tool-badge"
            ],
            calls);
    }

    [Fact]
    public void Execute_renders_manual_overlay_when_requested_and_available()
    {
        var calls = new List<string>();

        var result = CodingCanvasRedrawWorkflow.Execute(
            new CodingCanvasRedrawWorkflowRequest(
                IncludeManualOverlay: true,
                IsSchemaActive: false,
                HasCurrentOverlay: true),
            Actions(calls.Add));

        Assert.Equal(CodingCanvasRedrawWorkflowOutcome.ManualOverlayRendered, result.Outcome);
        Assert.Equal(
            [
                "update-viewport",
                "clear-transient:true",
                "render-ai",
                "render-reference-dn",
                "render-manual-overlay",
                "update-tool-badge"
            ],
            calls);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Execute_skips_manual_overlay_without_request_or_current_overlay(
        bool includeManualOverlay,
        bool hasCurrentOverlay)
    {
        var calls = new List<string>();

        var result = CodingCanvasRedrawWorkflow.Execute(
            new CodingCanvasRedrawWorkflowRequest(
                includeManualOverlay,
                IsSchemaActive: false,
                hasCurrentOverlay),
            Actions(calls.Add));

        Assert.Equal(CodingCanvasRedrawWorkflowOutcome.NoOverlayRendered, result.Outcome);
        Assert.DoesNotContain("render-active-schema", calls);
        Assert.DoesNotContain("render-manual-overlay", calls);
        Assert.Equal("update-tool-badge", calls[^1]);
    }

    private static CodingCanvasRedrawWorkflowActions Actions(Action<string> calls)
        => new(
            UpdateViewport: () => calls("update-viewport"),
            ClearTransientCanvas: clearManualOverlay => calls($"clear-transient:{clearManualOverlay.ToString().ToLowerInvariant()}"),
            RenderAiOverlays: () => calls("render-ai"),
            RenderReferenceDn: () => calls("render-reference-dn"),
            RenderActiveSchema: () => calls("render-active-schema"),
            RenderManualOverlay: () => calls("render-manual-overlay"),
            UpdateToolBadge: () => calls("update-tool-badge"));
}
