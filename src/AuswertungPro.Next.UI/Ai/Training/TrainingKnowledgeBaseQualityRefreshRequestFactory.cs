using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseQualityRefreshRequestFactoryRequest(
    Func<Task<KnowledgeBaseQualityReport>> ReadQualityAsync,
    Func<Task<List<SelfTrainingRunSnapshot>>> LoadRunsAsync,
    Action<TrainingKnowledgeBaseQualityPresentation> ApplyPresentation,
    Action<string> Log,
    Action<Action> OnUi);

public sealed record TrainingKnowledgeBaseQualityRefreshDefaultRequestFactoryRequest(
    Func<Task<KnowledgeBaseQualityReport>> ReadQualityAsync,
    Action<TrainingKnowledgeBaseQualityPresentation> ApplyPresentation,
    Action<string> Log,
    Action<Action> OnUi);

public static class TrainingKnowledgeBaseQualityRefreshRequestFactory
{
    public static TrainingKnowledgeBaseQualityRefreshWorkflowRequest CreateWithDefaults(
        TrainingKnowledgeBaseQualityRefreshDefaultRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ReadQualityAsync);
        ArgumentNullException.ThrowIfNull(request.ApplyPresentation);
        ArgumentNullException.ThrowIfNull(request.Log);
        ArgumentNullException.ThrowIfNull(request.OnUi);

        return Create(new TrainingKnowledgeBaseQualityRefreshRequestFactoryRequest(
            request.ReadQualityAsync,
            SelfTrainingHistoryStore.LoadAsync,
            request.ApplyPresentation,
            request.Log,
            request.OnUi));
    }

    public static TrainingKnowledgeBaseQualityRefreshWorkflowRequest Create(
        TrainingKnowledgeBaseQualityRefreshRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ReadQualityAsync);
        ArgumentNullException.ThrowIfNull(request.LoadRunsAsync);
        ArgumentNullException.ThrowIfNull(request.ApplyPresentation);
        ArgumentNullException.ThrowIfNull(request.Log);
        ArgumentNullException.ThrowIfNull(request.OnUi);

        return new TrainingKnowledgeBaseQualityRefreshWorkflowRequest(
            request.ReadQualityAsync,
            request.LoadRunsAsync,
            request.ApplyPresentation,
            request.Log,
            request.OnUi);
    }
}
