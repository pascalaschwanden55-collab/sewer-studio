using AuswertungPro.Next.Application.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingReviewItemDecisionCommandRequestFactoryRequest(
    InfraSelfImproving.ReviewQueueItem Item,
    InfraSelfImproving.FeedbackIngestionService Feedback,
    InfraSelfImproving.ReviewQueueService QueueService,
    TrainingReviewItemDecision Decision,
    string CorrectedCode,
    string? CorrectedDescription,
    CancellationToken CancellationToken,
    BoundingBox? Box,
    TrainingSegmentationMask? Mask,
    ICollection<InfraSelfImproving.ReviewQueueItem> ReviewQueue,
    Func<InfraSelfImproving.ReviewQueueItem, Task<string?>> ResolveSampleIdAsync,
    Func<IReadOnlyList<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> IndexSamplesAsync,
    Action<string> DeindexSample,
    Func<Task> ReloadSamplesAsync,
    Action<Action> OnUi,
    Action<int> SetReviewQueueCount,
    Action<string> SetReviewStatusText,
    Action<string> Log);

public static class TrainingReviewItemDecisionCommandRequestFactory
{
    public static TrainingReviewItemDecisionCommandWorkflowRequest Create(
        TrainingReviewItemDecisionCommandRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Item);
        ArgumentNullException.ThrowIfNull(request.Feedback);
        ArgumentNullException.ThrowIfNull(request.QueueService);
        ArgumentNullException.ThrowIfNull(request.CorrectedCode);
        ArgumentNullException.ThrowIfNull(request.ReviewQueue);
        ArgumentNullException.ThrowIfNull(request.ResolveSampleIdAsync);
        ArgumentNullException.ThrowIfNull(request.IndexSamplesAsync);
        ArgumentNullException.ThrowIfNull(request.DeindexSample);
        ArgumentNullException.ThrowIfNull(request.ReloadSamplesAsync);
        ArgumentNullException.ThrowIfNull(request.OnUi);
        ArgumentNullException.ThrowIfNull(request.SetReviewQueueCount);
        ArgumentNullException.ThrowIfNull(request.SetReviewStatusText);
        ArgumentNullException.ThrowIfNull(request.Log);

        return new TrainingReviewItemDecisionCommandWorkflowRequest(
            Item: request.Item,
            Feedback: request.Feedback,
            QueueService: request.QueueService,
            Decision: request.Decision,
            CorrectedCode: request.CorrectedCode,
            CorrectedDescription: request.CorrectedDescription,
            CancellationToken: request.CancellationToken,
            Box: request.Box,
            Mask: request.Mask,
            ReviewQueue: request.ReviewQueue,
            ResolveSampleIdAsync: request.ResolveSampleIdAsync,
            IndexSamplesAsync: request.IndexSamplesAsync,
            DeindexSample: request.DeindexSample,
            ReloadSamplesAsync: request.ReloadSamplesAsync,
            OnUi: request.OnUi,
            SetReviewQueueCount: request.SetReviewQueueCount,
            SetReviewStatusText: request.SetReviewStatusText,
            Log: request.Log,
            RunDecisionAsync: TrainingReviewItemDecisionWorkflow.RunAsync);
    }
}
