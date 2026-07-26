using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStreckenschadenActionApplyCommandWorkflowTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Execute_skips_when_required_state_is_missing(
        bool hasCodingSessionService,
        bool hasCodingEvents,
        bool hasActions)
    {
        var calls = new List<string>();

        var result = CodingStreckenschadenActionApplyCommandWorkflow.Execute(
            new CodingStreckenschadenActionApplyCommandRequest(
                hasCodingSessionService,
                hasCodingEvents,
                hasActions),
            new CodingStreckenschadenActionApplyCommandActions(
                ApplyActions: () =>
                {
                    calls.Add("apply");
                    return true;
                }));

        Assert.Equal(CodingStreckenschadenActionApplyCommandOutcome.Skipped, result.Outcome);
        Assert.False(result.Changed);
        Assert.Empty(calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Execute_returns_apply_result_when_required_state_exists(bool changed)
    {
        var calls = new List<string>();

        var result = CodingStreckenschadenActionApplyCommandWorkflow.Execute(
            new CodingStreckenschadenActionApplyCommandRequest(
                HasCodingSessionService: true,
                HasCodingEvents: true,
                HasActions: true),
            new CodingStreckenschadenActionApplyCommandActions(
                ApplyActions: () =>
                {
                    calls.Add("apply");
                    return changed;
                }));

        Assert.Equal(
            changed
                ? CodingStreckenschadenActionApplyCommandOutcome.Applied
                : CodingStreckenschadenActionApplyCommandOutcome.NoChanges,
            result.Outcome);
        Assert.Equal(changed, result.Changed);
        Assert.Equal(["apply"], calls);
    }
}
