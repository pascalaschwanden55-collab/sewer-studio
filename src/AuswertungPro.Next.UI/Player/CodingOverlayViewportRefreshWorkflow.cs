namespace AuswertungPro.Next.UI.Player;

public enum CodingOverlayViewportRefreshOutcome
{
    NotNeeded,
    Updated
}

public sealed record CodingOverlayViewportRefreshRequest(
    double ActualWidth,
    double ActualHeight);

public sealed record CodingOverlayViewportRefreshActions(
    Action UpdateViewport);

public sealed record CodingOverlayViewportRefreshResult(
    CodingOverlayViewportRefreshOutcome Outcome)
{
    public bool Updated => Outcome == CodingOverlayViewportRefreshOutcome.Updated;
}

public static class CodingOverlayViewportRefreshWorkflow
{
    public static CodingOverlayViewportRefreshResult Execute(
        CodingOverlayViewportRefreshRequest request,
        CodingOverlayViewportRefreshActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.ActualWidth <= 0 || request.ActualHeight <= 0)
        {
            actions.UpdateViewport();
            return Result(CodingOverlayViewportRefreshOutcome.Updated);
        }

        return Result(CodingOverlayViewportRefreshOutcome.NotNeeded);
    }

    private static CodingOverlayViewportRefreshResult Result(
        CodingOverlayViewportRefreshOutcome outcome)
        => new(outcome);
}
