namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCaseCommandWorkflowRequest(
    TrainingCase? SelectedCase,
    TrainingCaseDecision Decision,
    Action<string> SetStatusText);

public static class TrainingCaseCommandWorkflow
{
    public static void Run(TrainingCaseCommandWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);

        if (request.SelectedCase is null)
            return;

        var result = TrainingCaseDecisionController.Apply(request.SelectedCase, request.Decision);
        TrainingCaseDecisionCompletionController.Apply(result, request.SetStatusText);
    }
}
