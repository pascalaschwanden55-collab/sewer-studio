using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseStatusRefreshWorkflowRequest(
    Func<int, Task<KnowledgeBaseStatusReport>> ReadStatusAsync,
    Action<TrainingKnowledgeBaseStatusPresentation> ApplyPresentation,
    Func<Task> RefreshQualityAsync,
    Action<Action> OnUi);

public static class TrainingKnowledgeBaseStatusRefreshWorkflow
{
    public static async Task RunAsync(TrainingKnowledgeBaseStatusRefreshWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var status = await request.ReadStatusAsync(20).ConfigureAwait(false);
            var presentation = TrainingKnowledgeBaseStatusPresentationBuilder.Build(status);

            request.OnUi(() => request.ApplyPresentation(presentation));

            await request.RefreshQualityAsync().ConfigureAwait(false);
        }
        catch
        {
            // KB might not exist yet.
        }
    }
}
