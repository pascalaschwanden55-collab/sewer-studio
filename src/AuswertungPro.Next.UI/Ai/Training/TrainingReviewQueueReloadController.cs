using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingReviewQueueReloadController
{
    public static void Reload(
        InfraSelfImproving.ReviewQueueService? queueService,
        Action<InfraSelfImproving.ReviewQueueService> loadReviewQueue)
    {
        ArgumentNullException.ThrowIfNull(loadReviewQueue);

        if (queueService is null)
            return;

        loadReviewQueue(queueService);
    }
}
