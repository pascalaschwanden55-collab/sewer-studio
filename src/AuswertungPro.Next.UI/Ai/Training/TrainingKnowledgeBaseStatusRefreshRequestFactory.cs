using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseStatusRefreshRequestFactoryRequest(
    Func<int, Task<KnowledgeBaseStatusReport>> ReadStatusAsync,
    Action<TrainingKnowledgeBaseStatusPresentation> ApplyPresentation,
    Func<Task> RefreshQualityAsync,
    Action<Action> OnUi);

public static class TrainingKnowledgeBaseStatusRefreshRequestFactory
{
    public static TrainingKnowledgeBaseStatusRefreshWorkflowRequest Create(
        TrainingKnowledgeBaseStatusRefreshRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ReadStatusAsync);
        ArgumentNullException.ThrowIfNull(request.ApplyPresentation);
        ArgumentNullException.ThrowIfNull(request.RefreshQualityAsync);
        ArgumentNullException.ThrowIfNull(request.OnUi);

        return new TrainingKnowledgeBaseStatusRefreshWorkflowRequest(
            request.ReadStatusAsync,
            request.ApplyPresentation,
            request.RefreshQualityAsync,
            request.OnUi);
    }
}
