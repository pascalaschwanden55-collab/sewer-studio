using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingCurrentOverlayRenderWorkflowOutcome
{
    Skipped,
    Rendered
}

public sealed record CodingCurrentOverlayRenderWorkflowRequest(
    OverlayGeometry? CurrentOverlay);

public sealed record CodingCurrentOverlayRenderWorkflowActions(
    Action<OverlayGeometry> RenderOverlay);

public sealed record CodingCurrentOverlayRenderWorkflowResult(
    CodingCurrentOverlayRenderWorkflowOutcome Outcome);

public static class CodingCurrentOverlayRenderWorkflow
{
    public static CodingCurrentOverlayRenderWorkflowResult Execute(
        CodingCurrentOverlayRenderWorkflowRequest request,
        CodingCurrentOverlayRenderWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.CurrentOverlay is null)
            return Result(CodingCurrentOverlayRenderWorkflowOutcome.Skipped);

        actions.RenderOverlay(request.CurrentOverlay);
        return Result(CodingCurrentOverlayRenderWorkflowOutcome.Rendered);
    }

    private static CodingCurrentOverlayRenderWorkflowResult Result(
        CodingCurrentOverlayRenderWorkflowOutcome outcome)
        => new(outcome);
}
