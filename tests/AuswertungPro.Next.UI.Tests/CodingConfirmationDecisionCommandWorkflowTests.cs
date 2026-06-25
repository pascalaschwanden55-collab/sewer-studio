using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationDecisionCommandWorkflowTests
{
    [Fact]
    public void Execute_applies_decision_then_closes_and_resumes()
    {
        var calls = new List<string>();

        var result = CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () =>
                {
                    calls.Add("decision");
                    return true;
                },
                CloseConfirmationPanel: () => calls.Add("close"),
                ResumeAfterConfirmation: () => calls.Add("resume")));

        Assert.Equal(CodingConfirmationDecisionCommandOutcome.Applied, result.Outcome);
        Assert.True(result.Applied);
        Assert.Equal(["decision", "close", "resume"], calls);
    }

    [Fact]
    public void Execute_still_closes_and_resumes_when_decision_is_skipped()
    {
        var calls = new List<string>();

        var result = CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () =>
                {
                    calls.Add("decision");
                    return false;
                },
                CloseConfirmationPanel: () => calls.Add("close"),
                ResumeAfterConfirmation: () => calls.Add("resume")));

        Assert.Equal(CodingConfirmationDecisionCommandOutcome.Skipped, result.Outcome);
        Assert.False(result.Applied);
        Assert.Equal(["decision", "close", "resume"], calls);
    }
}
