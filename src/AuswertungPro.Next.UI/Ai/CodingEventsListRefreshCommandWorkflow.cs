namespace AuswertungPro.Next.UI.Ai;

public enum CodingEventsListRefreshCommandOutcome
{
    Skipped,
    Refreshed
}

public sealed record CodingEventsListRefreshCommandActions(
    Func<bool> RefreshListAndStatistics,
    Action ScheduleColorize);

public sealed record CodingEventsListRefreshCommandResult(
    CodingEventsListRefreshCommandOutcome Outcome);

public static class CodingEventsListRefreshCommandWorkflow
{
    public static CodingEventsListRefreshCommandResult Execute(
        CodingEventsListRefreshCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        if (!actions.RefreshListAndStatistics())
            return Result(CodingEventsListRefreshCommandOutcome.Skipped);

        actions.ScheduleColorize();
        return Result(CodingEventsListRefreshCommandOutcome.Refreshed);
    }

    private static CodingEventsListRefreshCommandResult Result(CodingEventsListRefreshCommandOutcome outcome)
        => new(outcome);
}
