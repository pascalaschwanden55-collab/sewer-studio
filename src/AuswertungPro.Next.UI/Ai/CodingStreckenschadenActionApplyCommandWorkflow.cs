namespace AuswertungPro.Next.UI.Ai;

public enum CodingStreckenschadenActionApplyCommandOutcome
{
    Skipped,
    NoChanges,
    Applied
}

public sealed record CodingStreckenschadenActionApplyCommandRequest(
    bool HasCodingSessionService,
    bool HasCodingEvents,
    bool HasActions);

public sealed record CodingStreckenschadenActionApplyCommandActions(
    Func<bool> ApplyActions);

public sealed record CodingStreckenschadenActionApplyCommandResult(
    CodingStreckenschadenActionApplyCommandOutcome Outcome)
{
    public bool Changed => Outcome == CodingStreckenschadenActionApplyCommandOutcome.Applied;
}

public static class CodingStreckenschadenActionApplyCommandWorkflow
{
    public static CodingStreckenschadenActionApplyCommandResult Execute(
        CodingStreckenschadenActionApplyCommandRequest request,
        CodingStreckenschadenActionApplyCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingSessionService || !request.HasCodingEvents || !request.HasActions)
            return Result(CodingStreckenschadenActionApplyCommandOutcome.Skipped);

        return Result(
            actions.ApplyActions()
                ? CodingStreckenschadenActionApplyCommandOutcome.Applied
                : CodingStreckenschadenActionApplyCommandOutcome.NoChanges);
    }

    private static CodingStreckenschadenActionApplyCommandResult Result(
        CodingStreckenschadenActionApplyCommandOutcome outcome)
        => new(outcome);
}
