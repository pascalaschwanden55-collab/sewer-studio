using AuswertungPro.Next.Application.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public delegate Task TrainingSelectedReviewApproveHandler(
    InfraSelfImproving.ReviewQueueItem item,
    InfraSelfImproving.FeedbackIngestionService feedback,
    InfraSelfImproving.ReviewQueueService queueService,
    CancellationToken ct,
    BoundingBox? box,
    TrainingSegmentationMask? mask);

public delegate Task TrainingSelectedReviewRejectHandler(
    InfraSelfImproving.ReviewQueueItem item,
    string correctedCode,
    InfraSelfImproving.FeedbackIngestionService feedback,
    InfraSelfImproving.ReviewQueueService queueService,
    CancellationToken ct,
    string? correctedDescription);

public sealed record TrainingSelectedReviewCommandRequestFactoryDefaults(
    Func<
        InfraSelfImproving.ReviewQueueItem,
        InfraSelfImproving.ReviewQueueService,
        CancellationToken,
        BoundingBox?,
        TrainingSegmentationMask?,
        AppSettings?,
        TrainingSelectedReviewApproveHandler,
        Task> ApproveWithDefaultsAsync,
    Func<
        InfraSelfImproving.ReviewQueueItem,
        InfraSelfImproving.ReviewQueueService,
        CancellationToken,
        AppSettings?,
        TrainingSelectedReviewRejectHandler,
        Task> RejectWithDefaultsAsync,
    Func<
        InfraSelfImproving.ReviewQueueItem,
        string,
        string?,
        InfraSelfImproving.ReviewQueueService,
        CancellationToken,
        AppSettings?,
        TrainingSelectedReviewRejectHandler,
        Task> CorrectWithDefaultsAsync);

public sealed record TrainingSelectedReviewApproveFactoryRequest(
    InfraSelfImproving.ReviewQueueItem? Item,
    InfraSelfImproving.ReviewQueueService? QueueService,
    Func<BoundingBox?> GetPendingBox,
    Func<TrainingSegmentationMask?> GetPendingMask,
    Action ClearPendingReviewGeometry,
    AppSettings? Settings,
    TrainingSelectedReviewApproveHandler ApproveReviewItemAsync,
    CancellationToken CancellationToken,
    Action<string> Log,
    Action<Action> OnUi,
    Action<string> SetReviewStatusText);

public sealed record TrainingSelectedReviewRejectFactoryRequest(
    InfraSelfImproving.ReviewQueueItem? Item,
    InfraSelfImproving.ReviewQueueService? QueueService,
    AppSettings? Settings,
    TrainingSelectedReviewRejectHandler RejectReviewItemAsync,
    CancellationToken CancellationToken,
    Action<string> Log,
    Action<Action> OnUi,
    Action<string> SetReviewStatusText);

public sealed record TrainingSelectedReviewCorrectionFactoryRequest(
    InfraSelfImproving.ReviewQueueItem? Item,
    InfraSelfImproving.ReviewQueueService? QueueService,
    string CorrectedCode,
    string? CorrectedDescription,
    AppSettings? Settings,
    TrainingSelectedReviewRejectHandler RejectReviewItemAsync,
    CancellationToken CancellationToken,
    Action<string> Log,
    Action<Action> OnUi,
    Action<string> SetReviewStatusText);

public static class TrainingSelectedReviewCommandRequestFactory
{
    public static TrainingSelectedReviewApproveRequest CreateApproveWithDefaults(
        TrainingSelectedReviewApproveFactoryRequest request)
        => CreateApprove(request, Defaults());

    public static TrainingSelectedReviewRejectRequest CreateRejectWithDefaults(
        TrainingSelectedReviewRejectFactoryRequest request)
        => CreateReject(request, Defaults());

    public static TrainingSelectedReviewCorrectionRequest CreateCorrectionWithDefaults(
        TrainingSelectedReviewCorrectionFactoryRequest request)
        => CreateCorrection(request, Defaults());

    public static TrainingSelectedReviewApproveRequest CreateApprove(
        TrainingSelectedReviewApproveFactoryRequest request,
        TrainingSelectedReviewCommandRequestFactoryDefaults defaults)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(defaults);

        return new TrainingSelectedReviewApproveRequest(
            Item: request.Item,
            QueueService: request.QueueService,
            GetPendingBox: request.GetPendingBox,
            GetPendingMask: request.GetPendingMask,
            ApproveAsync: (item, queueService, token, box, mask) =>
                defaults.ApproveWithDefaultsAsync(
                    item,
                    queueService,
                    token,
                    box,
                    mask,
                    request.Settings,
                    request.ApproveReviewItemAsync),
            ClearPendingReviewGeometry: request.ClearPendingReviewGeometry,
            CancellationToken: request.CancellationToken,
            Log: request.Log,
            OnUi: request.OnUi,
            SetReviewStatusText: request.SetReviewStatusText);
    }

    public static TrainingSelectedReviewRejectRequest CreateReject(
        TrainingSelectedReviewRejectFactoryRequest request,
        TrainingSelectedReviewCommandRequestFactoryDefaults defaults)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(defaults);

        return new TrainingSelectedReviewRejectRequest(
            Item: request.Item,
            QueueService: request.QueueService,
            RejectAsync: (item, queueService, token) =>
                defaults.RejectWithDefaultsAsync(
                    item,
                    queueService,
                    token,
                    request.Settings,
                    request.RejectReviewItemAsync),
            CancellationToken: request.CancellationToken,
            Log: request.Log,
            OnUi: request.OnUi,
            SetReviewStatusText: request.SetReviewStatusText);
    }

    public static TrainingSelectedReviewCorrectionRequest CreateCorrection(
        TrainingSelectedReviewCorrectionFactoryRequest request,
        TrainingSelectedReviewCommandRequestFactoryDefaults defaults)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(defaults);

        return new TrainingSelectedReviewCorrectionRequest(
            Item: request.Item,
            QueueService: request.QueueService,
            CorrectedCode: request.CorrectedCode,
            CorrectedDescription: request.CorrectedDescription,
            ApplyCorrectionAsync: (item, code, description, token) =>
            {
                if (request.QueueService is null)
                    return Task.CompletedTask;

                return defaults.CorrectWithDefaultsAsync(
                    item,
                    code,
                    description,
                    request.QueueService,
                    token,
                    request.Settings,
                    request.RejectReviewItemAsync);
            },
            CancellationToken: request.CancellationToken,
            Log: request.Log,
            OnUi: request.OnUi,
            SetReviewStatusText: request.SetReviewStatusText);
    }

    private static TrainingSelectedReviewCommandRequestFactoryDefaults Defaults()
        => new(
            (item, queueService, ct, box, mask, settings, approveAsync) =>
                TrainingSelectedReviewRuntime.ApproveWithDefaultsAsync(
                    item,
                    queueService,
                    ct,
                    box,
                    mask,
                    settings,
                    (reviewItem, feedback, queue, token, pendingBox, pendingMask) =>
                        approveAsync(reviewItem, feedback, queue, token, pendingBox, pendingMask)),
            (item, queueService, ct, settings, rejectAsync) =>
                TrainingSelectedReviewRuntime.RejectWithDefaultsAsync(
                    item,
                    queueService,
                    ct,
                    settings,
                    (reviewItem, code, feedback, queue, token, description) =>
                        rejectAsync(reviewItem, code, feedback, queue, token, description)),
            (item, code, description, queueService, ct, settings, rejectAsync) =>
                TrainingSelectedReviewRuntime.CorrectWithDefaultsAsync(
                    item,
                    code,
                    description,
                    queueService,
                    ct,
                    settings,
                    (reviewItem, correctedCode, feedback, queue, token, correctedDescription) =>
                        rejectAsync(reviewItem, correctedCode, feedback, queue, token, correctedDescription)));
}
