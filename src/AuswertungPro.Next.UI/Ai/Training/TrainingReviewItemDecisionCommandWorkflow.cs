using AuswertungPro.Next.Application.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingReviewItemDecisionCommandWorkflowRequest(
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
    Action<string> Log,
    Func<TrainingReviewItemDecisionWorkflowRequest, Task> RunDecisionAsync);

public static class TrainingReviewItemDecisionCommandWorkflow
{
    public static Task RunAsync(TrainingReviewItemDecisionCommandWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.RunDecisionAsync(
            TrainingReviewItemDecisionRequestFactory.CreateWithCurrentUser(
                request.Item,
                request.Feedback,
                request.QueueService,
                request.Decision,
                request.CorrectedCode,
                request.CorrectedDescription,
                request.CancellationToken,
                request.Box,
                request.Mask,
                request.ReviewQueue,
                request.ResolveSampleIdAsync,
                () => TrainingReviewApprovalServiceFactory.Create(
                    request.IndexSamplesAsync,
                    request.DeindexSample),
                request.ReloadSamplesAsync,
                request.OnUi,
                request.SetReviewQueueCount,
                request.SetReviewStatusText,
                request.Log));
    }
}
