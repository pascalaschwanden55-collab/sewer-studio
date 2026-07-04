using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingReviewQueueLoadWorkflowRequest(
    InfraSelfImproving.ReviewQueueService QueueService,
    ICollection<InfraSelfImproving.ReviewQueueItem> ReviewQueue,
    Action<int> SetReviewQueueCount,
    Action<string> SetReviewStatusText,
    Action<Action> OnUi);

public static class TrainingReviewQueueLoadWorkflow
{
    public static void Run(TrainingReviewQueueLoadWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.QueueService);
        ArgumentNullException.ThrowIfNull(request.ReviewQueue);

        request.OnUi(() =>
        {
            var items = request.QueueService.GetAll();
            request.ReviewQueue.Clear();
            foreach (var item in items)
                request.ReviewQueue.Add(item);

            request.SetReviewQueueCount(items.Count);
            request.SetReviewStatusText(BuildStatusText(items.Count));
        });
    }

    public static string BuildStatusText(int count)
        => $"{count} Eintr\u00e4ge zur Pr\u00fcfung";
}
