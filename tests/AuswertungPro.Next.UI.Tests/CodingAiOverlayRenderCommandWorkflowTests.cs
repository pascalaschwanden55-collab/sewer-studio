using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiOverlayRenderCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_render_when_coding_view_model_is_missing()
    {
        var calls = new List<string>();

        var result = CodingAiOverlayRenderCommandWorkflow.Execute(
            new CodingAiOverlayRenderCommandRequest(HasCodingViewModel: false),
            new CodingAiOverlayRenderCommandActions(
                RenderAiOverlays: () => calls.Add("render")));

        Assert.Equal(CodingAiOverlayRenderCommandOutcome.Skipped, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_renders_ai_overlays_when_coding_view_model_exists()
    {
        var calls = new List<string>();

        var result = CodingAiOverlayRenderCommandWorkflow.Execute(
            new CodingAiOverlayRenderCommandRequest(HasCodingViewModel: true),
            new CodingAiOverlayRenderCommandActions(
                RenderAiOverlays: () => calls.Add("render")));

        Assert.Equal(CodingAiOverlayRenderCommandOutcome.Rendered, result.Outcome);
        Assert.Equal(["render"], calls);
    }
}
