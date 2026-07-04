using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingReviewItemDecisionRequestFactory
{
    public static TrainingReviewItemDecisionWorkflowRequest CreateWithCurrentUser(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.FeedbackIngestionService feedback,
        InfraSelfImproving.ReviewQueueService queueService,
        TrainingReviewItemDecision decision,
        string correctedCode,
        string? correctedDescription,
        CancellationToken ct,
        BoundingBox? box,
        TrainingSegmentationMask? mask,
        ICollection<InfraSelfImproving.ReviewQueueItem> reviewQueue,
        Func<InfraSelfImproving.ReviewQueueItem, Task<string?>> resolveSampleIdAsync,
        Func<IReviewApprovalService> createApprovalService,
        Func<Task> reloadSamplesAsync,
        Action<Action> onUi,
        Action<int> setReviewQueueCount,
        Action<string> setReviewStatusText,
        Action<string> log)
        => Create(
            item,
            feedback,
            queueService,
            decision,
            correctedCode,
            correctedDescription,
            ct,
            box,
            mask,
            reviewQueue,
            Environment.UserName,
            resolveSampleIdAsync,
            createApprovalService,
            reloadSamplesAsync,
            onUi,
            setReviewQueueCount,
            setReviewStatusText,
            log);

    public static TrainingReviewItemDecisionWorkflowRequest Create(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.FeedbackIngestionService feedback,
        InfraSelfImproving.ReviewQueueService queueService,
        TrainingReviewItemDecision decision,
        string correctedCode,
        string? correctedDescription,
        CancellationToken ct,
        BoundingBox? box,
        TrainingSegmentationMask? mask,
        ICollection<InfraSelfImproving.ReviewQueueItem> reviewQueue,
        string confirmedByUser,
        Func<InfraSelfImproving.ReviewQueueItem, Task<string?>> resolveSampleIdAsync,
        Func<IReviewApprovalService> createApprovalService,
        Func<Task> reloadSamplesAsync,
        Action<Action> onUi,
        Action<int> setReviewQueueCount,
        Action<string> setReviewStatusText,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(feedback);
        ArgumentNullException.ThrowIfNull(queueService);
        ArgumentNullException.ThrowIfNull(reviewQueue);
        ArgumentNullException.ThrowIfNull(resolveSampleIdAsync);
        ArgumentNullException.ThrowIfNull(createApprovalService);
        ArgumentNullException.ThrowIfNull(reloadSamplesAsync);
        ArgumentNullException.ThrowIfNull(onUi);
        ArgumentNullException.ThrowIfNull(setReviewQueueCount);
        ArgumentNullException.ThrowIfNull(setReviewStatusText);
        ArgumentNullException.ThrowIfNull(log);

        return new TrainingReviewItemDecisionWorkflowRequest(
            Item: item,
            Decision: decision,
            CorrectedCode: correctedCode,
            CorrectedDescription: correctedDescription,
            Box: box,
            Mask: mask,
            QueueService: queueService,
            ReviewQueue: reviewQueue,
            CancellationToken: ct,
            ConfirmedByUser: confirmedByUser,
            ProcessFeedbackAsync: (reviewItem, finalCode, accepted, token) =>
                feedback.ProcessFeedbackAsync(reviewItem.Entry!, finalCode, accepted, token),
            ResolveSampleIdAsync: resolveSampleIdAsync,
            CreateApprovalService: createApprovalService,
            ReloadSamplesAsync: reloadSamplesAsync,
            OnUi: onUi,
            SetReviewQueueCount: setReviewQueueCount,
            SetReviewStatusText: setReviewStatusText,
            Log: log);
    }
}
