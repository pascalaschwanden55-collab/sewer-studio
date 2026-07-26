namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingStatisticsUpdateCommandOutcome
{
    Skipped,
    Refreshed
}

public sealed record CodingStatisticsUpdateCommandRequest(
    bool HasCodingViewModel);

public sealed record CodingStatisticsUpdateCommandActions(
    Action RefreshStatistics);

public sealed record CodingStatisticsUpdateCommandResult(
    CodingStatisticsUpdateCommandOutcome Outcome);

public static class CodingStatisticsUpdateCommandWorkflow
{
    public static CodingStatisticsUpdateCommandResult Execute(
        CodingStatisticsUpdateCommandRequest request,
        CodingStatisticsUpdateCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            return Result(CodingStatisticsUpdateCommandOutcome.Skipped);

        actions.RefreshStatistics();
        return Result(CodingStatisticsUpdateCommandOutcome.Refreshed);
    }

    private static CodingStatisticsUpdateCommandResult Result(
        CodingStatisticsUpdateCommandOutcome outcome)
        => new(outcome);
}
