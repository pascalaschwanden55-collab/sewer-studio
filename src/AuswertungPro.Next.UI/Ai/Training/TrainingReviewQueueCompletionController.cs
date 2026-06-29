using System.Collections.Generic;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingReviewQueueCompletionResult(
    int ReviewQueueCount,
    string StatusText,
    string LogText);

public static class TrainingReviewQueueCompletionController
{
    public static TrainingReviewQueueCompletionResult ApplyApproved(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.ReviewQueueService queueService,
        ICollection<InfraSelfImproving.ReviewQueueItem> reviewQueue)
    {
        queueService.Remove(item.Id);
        reviewQueue.Remove(item);

        var remaining = reviewQueue.Count;
        return new TrainingReviewQueueCompletionResult(
            remaining,
            $"Approved: {item.SuggestedCode} | {remaining} verbleibend",
            $"Review Approved: {item.Label} \u2192 {item.SuggestedCode}");
    }

    public static TrainingReviewQueueCompletionResult ApplyRejected(
        InfraSelfImproving.ReviewQueueItem item,
        string correctedCode,
        InfraSelfImproving.ReviewQueueService queueService,
        ICollection<InfraSelfImproving.ReviewQueueItem> reviewQueue)
    {
        queueService.Remove(item.Id);
        reviewQueue.Remove(item);

        var remaining = reviewQueue.Count;
        return new TrainingReviewQueueCompletionResult(
            remaining,
            $"Rejected: {item.SuggestedCode} \u2192 {correctedCode} | {remaining} verbleibend",
            $"Review Rejected: {item.Label} \u2192 {item.SuggestedCode} korrigiert zu {correctedCode}");
    }
}
