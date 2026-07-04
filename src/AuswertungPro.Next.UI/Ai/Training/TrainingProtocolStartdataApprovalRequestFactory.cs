using AuswertungPro.Next.Application.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public delegate Task TrainingProtocolStartdataApprovalRuntimeApproveAsync(
    InfraSelfImproving.ReviewQueueItem item,
    InfraSelfImproving.ReviewQueueService queueService,
    CancellationToken cancellationToken,
    AppSettings? settings,
    Func<InfraSelfImproving.ReviewQueueItem, InfraSelfImproving.FeedbackIngestionService, InfraSelfImproving.ReviewQueueService, CancellationToken, BoundingBox?, TrainingSegmentationMask?, Task> approveAsync);

public sealed record TrainingProtocolStartdataApprovalRequestFactoryRequest(
    InfraSelfImproving.ReviewQueueService? QueueService,
    Func<IReadOnlyList<InfraSelfImproving.ReviewQueueItem>> SelectItems,
    AppSettings? Settings,
    Func<InfraSelfImproving.ReviewQueueItem, InfraSelfImproving.FeedbackIngestionService, InfraSelfImproving.ReviewQueueService, CancellationToken, BoundingBox?, TrainingSegmentationMask?, Task> ApproveReviewItemAsync,
    CancellationToken CancellationToken,
    Action<string> Log,
    Action<Action> OnUi,
    Action<string> SetReviewStatusText);

public static class TrainingProtocolStartdataApprovalRequestFactory
{
    public static TrainingProtocolStartdataApprovalWorkflowRequest CreateWithDefaults(
        TrainingProtocolStartdataApprovalRequestFactoryRequest request)
        => Create(
            request,
            (item, queueService, token, settings, approveAsync) =>
                TrainingSelectedReviewRuntime.ApproveWithDefaultsAsync(
                    item,
                    queueService,
                    token,
                    box: null,
                    mask: null,
                    settings,
                    approveAsync));

    public static TrainingProtocolStartdataApprovalWorkflowRequest Create(
        TrainingProtocolStartdataApprovalRequestFactoryRequest request,
        TrainingProtocolStartdataApprovalRuntimeApproveAsync ApproveWithRuntimeAsync)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SelectItems);
        ArgumentNullException.ThrowIfNull(request.ApproveReviewItemAsync);
        ArgumentNullException.ThrowIfNull(request.Log);
        ArgumentNullException.ThrowIfNull(request.OnUi);
        ArgumentNullException.ThrowIfNull(request.SetReviewStatusText);
        ArgumentNullException.ThrowIfNull(ApproveWithRuntimeAsync);

        return new TrainingProtocolStartdataApprovalWorkflowRequest(
            request.QueueService,
            request.SelectItems,
            (item, queueService, token) => ApproveWithRuntimeAsync(
                item,
                queueService,
                token,
                request.Settings,
                request.ApproveReviewItemAsync),
            request.CancellationToken,
            request.Log,
            request.OnUi,
            request.SetReviewStatusText);
    }
}
