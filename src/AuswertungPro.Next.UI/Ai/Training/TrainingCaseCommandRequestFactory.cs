namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCaseCommandRequestFactoryRequest(
    TrainingCase? SelectedCase,
    TrainingCaseDecision Decision,
    Action<string> SetStatusText);

public static class TrainingCaseCommandRequestFactory
{
    public static TrainingCaseCommandWorkflowRequest Create(
        TrainingCaseCommandRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);

        return new TrainingCaseCommandWorkflowRequest(
            request.SelectedCase,
            request.Decision,
            request.SetStatusText);
    }
}
