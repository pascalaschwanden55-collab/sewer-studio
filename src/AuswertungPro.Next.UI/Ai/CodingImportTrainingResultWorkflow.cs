namespace AuswertungPro.Next.UI.Ai;

public enum CodingImportTrainingResultOutcome
{
    Rejected,
    Accepted
}

public sealed record CodingImportTrainingResultActions(
    Action<string> ShowBadge,
    Action<TimeSpan> ScheduleHideBadge);

public sealed record CodingImportTrainingResultWorkflowResult(
    CodingImportTrainingResultOutcome Outcome)
{
    public bool Accepted => Outcome == CodingImportTrainingResultOutcome.Accepted;
}

public static class CodingImportTrainingResultWorkflow
{
    public static CodingImportTrainingResultWorkflowResult Execute(
        CodingProtocolImportTrainingResult importResult,
        CodingImportTrainingResultActions actions)
    {
        ArgumentNullException.ThrowIfNull(importResult);
        ArgumentNullException.ThrowIfNull(actions);

        if (!importResult.Accepted)
            return Result(CodingImportTrainingResultOutcome.Rejected);

        var badge = importResult.Badge;
        actions.ShowBadge(badge.Text);
        actions.ScheduleHideBadge(badge.AutoHideDelay);
        return Result(CodingImportTrainingResultOutcome.Accepted);
    }

    private static CodingImportTrainingResultWorkflowResult Result(
        CodingImportTrainingResultOutcome outcome)
        => new(outcome);
}
