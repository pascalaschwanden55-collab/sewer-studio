using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingSelectedReviewRuntime
{
    public static Task ApproveWithDefaultsAsync(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct,
        BoundingBox? box,
        TrainingSegmentationMask? mask,
        AppSettings? settings,
        Func<InfraSelfImproving.ReviewQueueItem, InfraSelfImproving.FeedbackIngestionService, InfraSelfImproving.ReviewQueueService, CancellationToken, BoundingBox?, TrainingSegmentationMask?, Task> approveAsync)
        => ApproveAsync(
            item,
            queueService,
            ct,
            box,
            mask,
            static () => new KnowledgeBaseContext(),
            db => TrainingReviewFeedbackServiceFactory.Create(db, settings),
            approveAsync);

    public static Task RejectWithDefaultsAsync(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct,
        AppSettings? settings,
        Func<InfraSelfImproving.ReviewQueueItem, string, InfraSelfImproving.FeedbackIngestionService, InfraSelfImproving.ReviewQueueService, CancellationToken, string?, Task> rejectAsync)
        => RejectAsync(
            item,
            queueService,
            ct,
            static () => new KnowledgeBaseContext(),
            db => TrainingReviewFeedbackServiceFactory.Create(db, settings),
            rejectAsync);

    public static Task CorrectWithDefaultsAsync(
        InfraSelfImproving.ReviewQueueItem item,
        string correctedCode,
        string? correctedDescription,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct,
        AppSettings? settings,
        Func<InfraSelfImproving.ReviewQueueItem, string, InfraSelfImproving.FeedbackIngestionService, InfraSelfImproving.ReviewQueueService, CancellationToken, string?, Task> rejectAsync)
        => CorrectAsync(
            item,
            correctedCode,
            correctedDescription,
            queueService,
            ct,
            static () => new KnowledgeBaseContext(),
            db => TrainingReviewFeedbackServiceFactory.Create(db, settings),
            rejectAsync);

    public static async Task ApproveAsync<TScope, TFeedback>(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct,
        BoundingBox? box,
        TrainingSegmentationMask? mask,
        Func<TScope> openScope,
        Func<TScope, TFeedback> createFeedback,
        Func<InfraSelfImproving.ReviewQueueItem, TFeedback, InfraSelfImproving.ReviewQueueService, CancellationToken, BoundingBox?, TrainingSegmentationMask?, Task> approveAsync)
        where TScope : IDisposable
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(queueService);
        ArgumentNullException.ThrowIfNull(openScope);
        ArgumentNullException.ThrowIfNull(createFeedback);
        ArgumentNullException.ThrowIfNull(approveAsync);

        using var scope = openScope();
        var feedback = createFeedback(scope);
        await approveAsync(item, feedback, queueService, ct, box, mask).ConfigureAwait(false);
    }

    public static async Task RejectAsync<TScope, TFeedback>(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct,
        Func<TScope> openScope,
        Func<TScope, TFeedback> createFeedback,
        Func<InfraSelfImproving.ReviewQueueItem, string, TFeedback, InfraSelfImproving.ReviewQueueService, CancellationToken, string?, Task> rejectAsync)
        where TScope : IDisposable
    {
        await CorrectAsync(
            item,
            correctedCode: string.Empty,
            correctedDescription: null,
            queueService,
            ct,
            openScope,
            createFeedback,
            rejectAsync).ConfigureAwait(false);
    }

    public static async Task CorrectAsync<TScope, TFeedback>(
        InfraSelfImproving.ReviewQueueItem item,
        string correctedCode,
        string? correctedDescription,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct,
        Func<TScope> openScope,
        Func<TScope, TFeedback> createFeedback,
        Func<InfraSelfImproving.ReviewQueueItem, string, TFeedback, InfraSelfImproving.ReviewQueueService, CancellationToken, string?, Task> rejectAsync)
        where TScope : IDisposable
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(queueService);
        ArgumentNullException.ThrowIfNull(openScope);
        ArgumentNullException.ThrowIfNull(createFeedback);
        ArgumentNullException.ThrowIfNull(rejectAsync);

        using var scope = openScope();
        var feedback = createFeedback(scope);
        await rejectAsync(
            item,
            correctedCode,
            feedback,
            queueService,
            ct,
            correctedDescription).ConfigureAwait(false);
    }
}
