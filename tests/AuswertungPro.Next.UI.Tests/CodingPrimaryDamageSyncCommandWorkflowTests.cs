using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPrimaryDamageSyncCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_sync_when_haltung_record_is_missing()
    {
        var calls = new List<string>();

        var result = CodingPrimaryDamageSyncCommandWorkflow.Execute(
            new CodingPrimaryDamageSyncCommandRequest(HasHaltungRecord: false),
            new CodingPrimaryDamageSyncCommandActions(
                SyncPrimaryDamages: () => calls.Add("sync")));

        Assert.Equal(CodingPrimaryDamageSyncCommandOutcome.NoRecord, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_syncs_primary_damages_when_haltung_record_exists()
    {
        var calls = new List<string>();

        var result = CodingPrimaryDamageSyncCommandWorkflow.Execute(
            new CodingPrimaryDamageSyncCommandRequest(HasHaltungRecord: true),
            new CodingPrimaryDamageSyncCommandActions(
                SyncPrimaryDamages: () => calls.Add("sync")));

        Assert.Equal(CodingPrimaryDamageSyncCommandOutcome.Synced, result.Outcome);
        Assert.Equal(["sync"], calls);
    }
}
