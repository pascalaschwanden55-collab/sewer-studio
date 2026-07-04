using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingReviewQueueLoadRequestFactoryRequest(
    InfraSelfImproving.ReviewQueueService QueueService,
    ICollection<InfraSelfImproving.ReviewQueueItem> ReviewQueue,
    Action<int> SetReviewQueueCount,
    Action<string> SetReviewStatusText,
    Action<Action> OnUi);

public static class TrainingReviewQueueLoadRequestFactory
{
    public static TrainingReviewQueueLoadWorkflowRequest Create(
        TrainingReviewQueueLoadRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.QueueService);
        ArgumentNullException.ThrowIfNull(request.ReviewQueue);
        ArgumentNullException.ThrowIfNull(request.SetReviewQueueCount);
        ArgumentNullException.ThrowIfNull(request.SetReviewStatusText);
        ArgumentNullException.ThrowIfNull(request.OnUi);

        return new TrainingReviewQueueLoadWorkflowRequest(
            request.QueueService,
            request.ReviewQueue,
            request.SetReviewQueueCount,
            request.SetReviewStatusText,
            request.OnUi);
    }
}
