namespace AuswertungPro.Next.UI.Ai;

public enum CodingConfirmationDecisionCommandOutcome
{
    Skipped,
    Applied
}

public sealed record CodingConfirmationDecisionCommandActions(
    Func<bool> ApplyDecision,
    Action CloseConfirmationPanel,
    Action ResumeAfterConfirmation);

public sealed record CodingConfirmationDecisionCommandResult(
    CodingConfirmationDecisionCommandOutcome Outcome)
{
    public bool Applied => Outcome == CodingConfirmationDecisionCommandOutcome.Applied;
}

public static class CodingConfirmationDecisionCommandWorkflow
{
    public static CodingConfirmationDecisionCommandResult Execute(
        CodingConfirmationDecisionCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var applied = actions.ApplyDecision();
        actions.CloseConfirmationPanel();
        actions.ResumeAfterConfirmation();

        return new CodingConfirmationDecisionCommandResult(applied
            ? CodingConfirmationDecisionCommandOutcome.Applied
            : CodingConfirmationDecisionCommandOutcome.Skipped);
    }
}
