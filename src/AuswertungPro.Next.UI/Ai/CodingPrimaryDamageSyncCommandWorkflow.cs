namespace AuswertungPro.Next.UI.Ai;

public enum CodingPrimaryDamageSyncCommandOutcome
{
    NoRecord,
    Synced
}

public sealed record CodingPrimaryDamageSyncCommandRequest(
    bool HasHaltungRecord);

public sealed record CodingPrimaryDamageSyncCommandActions(
    Action SyncPrimaryDamages);

public sealed record CodingPrimaryDamageSyncCommandResult(
    CodingPrimaryDamageSyncCommandOutcome Outcome);

public static class CodingPrimaryDamageSyncCommandWorkflow
{
    public static CodingPrimaryDamageSyncCommandResult Execute(
        CodingPrimaryDamageSyncCommandRequest request,
        CodingPrimaryDamageSyncCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasHaltungRecord)
            return Result(CodingPrimaryDamageSyncCommandOutcome.NoRecord);

        actions.SyncPrimaryDamages();
        return Result(CodingPrimaryDamageSyncCommandOutcome.Synced);
    }

    private static CodingPrimaryDamageSyncCommandResult Result(
        CodingPrimaryDamageSyncCommandOutcome outcome)
        => new(outcome);
}
