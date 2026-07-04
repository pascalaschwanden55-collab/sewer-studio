using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingLastMatchRateRefreshWorkflowRequest(
    Func<Task<List<SelfTrainingRunSnapshot>>> LoadRunsAsync,
    SelfTrainingMatchRatePresentationUi Ui);

public static class SelfTrainingLastMatchRateRefreshWorkflow
{
    public static async Task RunAsync(SelfTrainingLastMatchRateRefreshWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.LoadRunsAsync);
        ArgumentNullException.ThrowIfNull(request.Ui);

        try
        {
            var runs = await request.LoadRunsAsync().ConfigureAwait(false);
            var presentation = SelfTrainingLastMatchRatePresentationBuilder.Build(runs);
            if (presentation is null)
                return;

            SelfTrainingMatchRatePresentationController.Apply(
                presentation,
                request.Ui);
        }
        catch
        {
        }
    }
}
