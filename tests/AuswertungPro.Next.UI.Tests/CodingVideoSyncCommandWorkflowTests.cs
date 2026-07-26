using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingVideoSyncCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_when_coding_view_model_is_missing()
    {
        var calls = new List<string>();

        var result = CodingVideoSyncCommandWorkflow.Execute(
            new CodingVideoSyncCommandRequest(HasCodingViewModel: false),
            new CodingVideoSyncCommandActions(
                SyncVideoToCodingMeter: () => calls.Add("sync")));

        Assert.Equal(CodingVideoSyncCommandOutcome.Skipped, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_syncs_when_coding_view_model_exists()
    {
        var calls = new List<string>();

        var result = CodingVideoSyncCommandWorkflow.Execute(
            new CodingVideoSyncCommandRequest(HasCodingViewModel: true),
            new CodingVideoSyncCommandActions(
                SyncVideoToCodingMeter: () => calls.Add("sync")));

        Assert.Equal(CodingVideoSyncCommandOutcome.Synced, result.Outcome);
        Assert.Equal(["sync"], calls);
    }
}
