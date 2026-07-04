using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseQualityRefreshWorkflowRequest(
    Func<Task<KnowledgeBaseQualityReport>> ReadQualityAsync,
    Func<Task<List<SelfTrainingRunSnapshot>>> LoadRunsAsync,
    Action<TrainingKnowledgeBaseQualityPresentation> ApplyPresentation,
    Action<string> Log,
    Action<Action> OnUi);

public static class TrainingKnowledgeBaseQualityRefreshWorkflow
{
    public static async Task RunAsync(TrainingKnowledgeBaseQualityRefreshWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var quality = await request.ReadQualityAsync().ConfigureAwait(false);
            var runs = await request.LoadRunsAsync().ConfigureAwait(false);
            var presentation = TrainingKnowledgeBaseQualityPresentationBuilder.Build(quality, runs);

            request.OnUi(() =>
            {
                request.ApplyPresentation(presentation);
                foreach (var logLine in presentation.LogLines)
                    request.Log(logLine);
            });
        }
        catch
        {
            // KB evtl. noch nicht vorhanden.
        }
    }
}
