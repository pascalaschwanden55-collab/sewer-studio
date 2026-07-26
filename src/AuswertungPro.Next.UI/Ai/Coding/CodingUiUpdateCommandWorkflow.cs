namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingUiUpdateCommandOutcome
{
    Skipped,
    Applied
}

public sealed record CodingUiUpdateCommandRequest(
    bool HasCodingViewModel,
    string? PropertyName,
    bool NavigationPending);

public sealed record CodingUiUpdateCommandActions(
    Func<string?, bool, CodingUiUpdateResult> ApplyUiUpdate);

public sealed record CodingUiUpdateCommandResult(
    CodingUiUpdateCommandOutcome Outcome,
    bool NavigationPending);

public static class CodingUiUpdateCommandWorkflow
{
    public static CodingUiUpdateCommandResult Execute(
        CodingUiUpdateCommandRequest request,
        CodingUiUpdateCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            return Result(CodingUiUpdateCommandOutcome.Skipped, request.NavigationPending);

        var result = actions.ApplyUiUpdate(
            request.PropertyName,
            request.NavigationPending);
        return Result(CodingUiUpdateCommandOutcome.Applied, result.NavigationPending);
    }

    private static CodingUiUpdateCommandResult Result(
        CodingUiUpdateCommandOutcome outcome,
        bool navigationPending)
        => new(outcome, navigationPending);
}
