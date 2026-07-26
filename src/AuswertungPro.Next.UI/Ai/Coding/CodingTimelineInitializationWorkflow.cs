namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingTimelineInitializationOutcome
{
    Configured
}

public sealed record CodingTimelineInitializationRequest(
    bool HasCodingViewModel);

public sealed record CodingTimelineInitializationActions(
    Action ConfigureTimeline);

public sealed record CodingTimelineInitializationResult(
    CodingTimelineInitializationOutcome Outcome);

public static class CodingTimelineInitializationWorkflow
{
    public static CodingTimelineInitializationResult Execute(
        CodingTimelineInitializationRequest request,
        CodingTimelineInitializationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            throw new InvalidOperationException("Coding timeline requires an active coding view model.");

        actions.ConfigureTimeline();
        return new CodingTimelineInitializationResult(CodingTimelineInitializationOutcome.Configured);
    }
}
