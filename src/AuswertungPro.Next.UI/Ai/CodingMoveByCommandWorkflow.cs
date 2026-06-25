namespace AuswertungPro.Next.UI.Ai;

public enum CodingMoveByCommandOutcome
{
    Skipped,
    Moved,
    Failed
}

public sealed record CodingMoveByCommandRequest(
    string TraceName);

public sealed record CodingMoveByCommandActions(
    Func<bool> PrepareMoveByCommand,
    Func<Task<double?>> ReadOsdMeterAsync,
    Action<string> TraceError);

public sealed record CodingMoveByCommandResult(
    CodingMoveByCommandOutcome Outcome);

public static class CodingMoveByCommandWorkflow
{
    public static async Task<CodingMoveByCommandResult> ExecuteAsync(
        CodingMoveByCommandRequest request,
        CodingMoveByCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        try
        {
            if (!actions.PrepareMoveByCommand())
                return Result(CodingMoveByCommandOutcome.Skipped);

            await actions.ReadOsdMeterAsync();
            return Result(CodingMoveByCommandOutcome.Moved);
        }
        catch (Exception ex)
        {
            actions.TraceError($"[PlayerWindow] {request.TraceName} error: {ex.Message}");
            return Result(CodingMoveByCommandOutcome.Failed);
        }
    }

    private static CodingMoveByCommandResult Result(CodingMoveByCommandOutcome outcome)
        => new(outcome);
}
