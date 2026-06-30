namespace AuswertungPro.Next.UI.Ai.Training;

public enum TrainingCaseDecision
{
    Approve,
    Reject,
    SetNew
}

public sealed record TrainingCaseDecisionResult(string StatusText);

public static class TrainingCaseDecisionController
{
    public static TrainingCaseDecisionResult Apply(TrainingCase trainingCase, TrainingCaseDecision decision)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);

        trainingCase.Status = decision switch
        {
            TrainingCaseDecision.Approve => TrainingCaseStatus.Approved,
            TrainingCaseDecision.Reject => TrainingCaseStatus.Rejected,
            TrainingCaseDecision.SetNew => TrainingCaseStatus.New,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, null)
        };

        var statusText = decision switch
        {
            TrainingCaseDecision.Approve => $"Approved: {trainingCase.CaseId}",
            TrainingCaseDecision.Reject => $"Rejected: {trainingCase.CaseId}",
            TrainingCaseDecision.SetNew => $"Status New: {trainingCase.CaseId}",
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, null)
        };

        return new TrainingCaseDecisionResult(statusText);
    }
}
