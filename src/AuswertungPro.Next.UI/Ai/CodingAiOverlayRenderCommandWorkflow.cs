namespace AuswertungPro.Next.UI.Ai;

public enum CodingAiOverlayRenderCommandOutcome
{
    Skipped,
    Rendered
}

public sealed record CodingAiOverlayRenderCommandRequest(
    bool HasCodingViewModel);

public sealed record CodingAiOverlayRenderCommandActions(
    Action RenderAiOverlays);

public sealed record CodingAiOverlayRenderCommandResult(
    CodingAiOverlayRenderCommandOutcome Outcome);

public static class CodingAiOverlayRenderCommandWorkflow
{
    public static CodingAiOverlayRenderCommandResult Execute(
        CodingAiOverlayRenderCommandRequest request,
        CodingAiOverlayRenderCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            return Result(CodingAiOverlayRenderCommandOutcome.Skipped);

        actions.RenderAiOverlays();
        return Result(CodingAiOverlayRenderCommandOutcome.Rendered);
    }

    private static CodingAiOverlayRenderCommandResult Result(
        CodingAiOverlayRenderCommandOutcome outcome)
        => new(outcome);
}
