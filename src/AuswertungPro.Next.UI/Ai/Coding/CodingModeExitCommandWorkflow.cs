namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingModeExitCommandOutcome
{
    Skipped,
    Blocked,
    Exited
}

public sealed record CodingModeExitCommandRequest(
    bool IsCodingMode);

public sealed record CodingModeExitCommandActions(
    Action<bool> SetCodingMode,
    Func<CodingModeExitFinalizationWorkflowResult> FinalizeExit,
    Action Teardown);

public sealed record CodingModeExitCommandResult(
    CodingModeExitCommandOutcome Outcome);

public static class CodingModeExitCommandWorkflow
{
    public static CodingModeExitCommandResult Execute(
        CodingModeExitCommandRequest request,
        CodingModeExitCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsCodingMode)
            return Result(CodingModeExitCommandOutcome.Skipped);

        actions.SetCodingMode(false);
        var finalization = actions.FinalizeExit();
        if (!finalization.CanExit)
        {
            actions.SetCodingMode(true);
            return Result(CodingModeExitCommandOutcome.Blocked);
        }

        actions.Teardown();
        return Result(CodingModeExitCommandOutcome.Exited);
    }

    private static CodingModeExitCommandResult Result(CodingModeExitCommandOutcome outcome)
        => new(outcome);
}
