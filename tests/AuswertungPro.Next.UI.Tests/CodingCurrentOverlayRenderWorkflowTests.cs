using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCurrentOverlayRenderWorkflowTests
{
    [Fact]
    public void Execute_skips_without_current_overlay()
    {
        var calls = new List<string>();

        var result = CodingCurrentOverlayRenderWorkflow.Execute(
            new CodingCurrentOverlayRenderWorkflowRequest(CurrentOverlay: null),
            new CodingCurrentOverlayRenderWorkflowActions(
                RenderOverlay: _ => calls.Add("render")));

        Assert.Equal(CodingCurrentOverlayRenderWorkflowOutcome.Skipped, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_renders_current_overlay_when_present()
    {
        var calls = new List<string>();
        var overlay = new OverlayGeometry();

        var result = CodingCurrentOverlayRenderWorkflow.Execute(
            new CodingCurrentOverlayRenderWorkflowRequest(overlay),
            new CodingCurrentOverlayRenderWorkflowActions(
                RenderOverlay: actual =>
                {
                    Assert.Same(overlay, actual);
                    calls.Add("render");
                }));

        Assert.Equal(CodingCurrentOverlayRenderWorkflowOutcome.Rendered, result.Outcome);
        Assert.Equal(["render"], calls);
    }
}
