using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingReviewQueueLoadResult(
    IReadOnlyList<InfraSelfImproving.ReviewQueueItem> Items,
    int ReviewQueueCount,
    string StatusText);

public static class TrainingReviewQueueLoadController
{
    public static TrainingReviewQueueLoadResult Load(InfraSelfImproving.ReviewQueueService queueService)
    {
        ArgumentNullException.ThrowIfNull(queueService);

        var items = queueService.GetAll();
        var count = items.Count;
        return new TrainingReviewQueueLoadResult(
            items,
            count,
            $"{count} Einträge zur Prüfung");
    }

    public static void Apply(
        TrainingReviewQueueLoadResult result,
        ICollection<InfraSelfImproving.ReviewQueueItem> reviewQueue)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(reviewQueue);

        reviewQueue.Clear();
        foreach (var item in result.Items)
            reviewQueue.Add(item);
    }
}
