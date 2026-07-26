using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeExitCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_when_coding_mode_is_not_active()
    {
        var calls = new List<string>();

        var result = CodingModeExitCommandWorkflow.Execute(
            new CodingModeExitCommandRequest(IsCodingMode: false),
            new CodingModeExitCommandActions(
                SetCodingMode: enabled => calls.Add($"mode:{enabled}"),
                FinalizeExit: () => throw new InvalidOperationException("Finalization should not run."),
                Teardown: () => throw new InvalidOperationException("Teardown should not run.")));

        Assert.Equal(CodingModeExitCommandOutcome.Skipped, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_restores_coding_mode_when_finalization_blocks_exit()
    {
        var calls = new List<string>();

        var result = CodingModeExitCommandWorkflow.Execute(
            new CodingModeExitCommandRequest(IsCodingMode: true),
            new CodingModeExitCommandActions(
                SetCodingMode: enabled => calls.Add($"mode:{enabled}"),
                FinalizeExit: () =>
                {
                    calls.Add("finalize");
                    return new CodingModeExitFinalizationWorkflowResult(CanExit: false);
                },
                Teardown: () => throw new InvalidOperationException("Teardown should not run.")));

        Assert.Equal(CodingModeExitCommandOutcome.Blocked, result.Outcome);
        Assert.Equal(["mode:False", "finalize", "mode:True"], calls);
    }

    [Fact]
    public void Execute_runs_teardown_after_successful_finalization()
    {
        var calls = new List<string>();

        var result = CodingModeExitCommandWorkflow.Execute(
            new CodingModeExitCommandRequest(IsCodingMode: true),
            new CodingModeExitCommandActions(
                SetCodingMode: enabled => calls.Add($"mode:{enabled}"),
                FinalizeExit: () =>
                {
                    calls.Add("finalize");
                    return new CodingModeExitFinalizationWorkflowResult(CanExit: true);
                },
                Teardown: () => calls.Add("teardown")));

        Assert.Equal(CodingModeExitCommandOutcome.Exited, result.Outcome);
        Assert.Equal(["mode:False", "finalize", "teardown"], calls);
    }
}
