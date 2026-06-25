namespace AuswertungPro.Next.UI.Ai;

public enum CodingVideoSyncCommandOutcome
{
    Skipped,
    Synced
}

public sealed record CodingVideoSyncCommandRequest(
    bool HasCodingViewModel);

public sealed record CodingVideoSyncCommandActions(
    Action SyncVideoToCodingMeter);

public sealed record CodingVideoSyncCommandResult(
    CodingVideoSyncCommandOutcome Outcome);

public static class CodingVideoSyncCommandWorkflow
{
    public static CodingVideoSyncCommandResult Execute(
        CodingVideoSyncCommandRequest request,
        CodingVideoSyncCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            return Result(CodingVideoSyncCommandOutcome.Skipped);

        actions.SyncVideoToCodingMeter();
        return Result(CodingVideoSyncCommandOutcome.Synced);
    }

    private static CodingVideoSyncCommandResult Result(CodingVideoSyncCommandOutcome outcome)
        => new(outcome);
}
