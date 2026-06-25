namespace AuswertungPro.Next.UI.Ai;

public enum CodingModeCommandOutcome
{
    MissingHaltung,
    Entered
}

public sealed record CodingModeCommandRequest(
    bool HasHaltungRecord);

public sealed record CodingModeCommandActions(
    Action ShowMissingHaltung,
    Action EnterCodingMode);

public sealed record CodingModeCommandResult(
    CodingModeCommandOutcome Outcome);

public static class CodingModeCommandWorkflow
{
    public static CodingModeCommandResult Execute(
        CodingModeCommandRequest request,
        CodingModeCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasHaltungRecord)
        {
            actions.ShowMissingHaltung();
            return Result(CodingModeCommandOutcome.MissingHaltung);
        }

        actions.EnterCodingMode();
        return Result(CodingModeCommandOutcome.Entered);
    }

    private static CodingModeCommandResult Result(CodingModeCommandOutcome outcome)
        => new(outcome);
}
