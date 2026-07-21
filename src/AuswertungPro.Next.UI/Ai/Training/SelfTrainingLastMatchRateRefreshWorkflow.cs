using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;

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
        catch (Exception ex)
        {
            // Ladefehler (defekte selftraining_history.json, IO) nicht still verschlucken —
            // die Match-Rate-Anzeige bleibt sonst kommentarlos leer.
            BestEffort.ReportWarning($"[SelfTraining] Match-Rate-Anzeige fehlgeschlagen: {ex.Message}");
        }
    }
}
