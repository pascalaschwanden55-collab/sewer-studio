namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingRunCommandWorkflowRequest(
    Func<Task<SelfTrainingRunPreparationWorkflowResult>> PrepareAsync,
    Func<TrainingCase, CancellationToken, SelfTrainingRunWorkflowRequest> CreateRunRequest,
    Func<SelfTrainingRunWorkflowRequest, Task> RunAsync);

public static class SelfTrainingRunCommandWorkflow
{
    public static async Task RunAsync(SelfTrainingRunCommandWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.PrepareAsync);
        ArgumentNullException.ThrowIfNull(request.CreateRunRequest);
        ArgumentNullException.ThrowIfNull(request.RunAsync);

        var preparation = await request.PrepareAsync().ConfigureAwait(false);
        if (preparation.ShouldStop || preparation.SelectedCase is null)
            return;

        await request.RunAsync(
            request.CreateRunRequest(
                preparation.SelectedCase,
                preparation.CancellationToken)).ConfigureAwait(false);
    }
}
