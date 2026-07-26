using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationDecisionCommandWorkflowTests
{
    [Fact]
    public async Task Execute_applies_decision_then_closes_and_resumes()
    {
        var calls = new List<string>();

        var result = await CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () =>
                {
                    calls.Add("decision");
                    return Task.FromResult(CodingConfirmationDecisionApplyOutcome.Saved);
                },
                CloseConfirmationPanel: () => calls.Add("close"),
                ResumeAfterConfirmation: () => calls.Add("resume"),
                ShowPersistenceError: _ => calls.Add("error")));

        Assert.Equal(CodingConfirmationDecisionCommandOutcome.Applied, result.Outcome);
        Assert.True(result.Applied);
        Assert.Equal(["decision", "close", "resume"], calls);
    }

    [Fact]
    public async Task Execute_still_closes_and_resumes_when_decision_is_skipped()
    {
        var calls = new List<string>();

        var result = await CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () =>
                {
                    calls.Add("decision");
                    return Task.FromResult(CodingConfirmationDecisionApplyOutcome.Skipped);
                },
                CloseConfirmationPanel: () => calls.Add("close"),
                ResumeAfterConfirmation: () => calls.Add("resume"),
                ShowPersistenceError: _ => calls.Add("error")));

        Assert.Equal(CodingConfirmationDecisionCommandOutcome.Skipped, result.Outcome);
        Assert.False(result.Applied);
        Assert.Equal(["decision", "close", "resume"], calls);
    }
}
