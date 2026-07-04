using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingSelectedReviewApproveRequest(
    InfraSelfImproving.ReviewQueueItem? Item,
    InfraSelfImproving.ReviewQueueService? QueueService,
    Func<BoundingBox?> GetPendingBox,
    Func<TrainingSegmentationMask?> GetPendingMask,
    Func<InfraSelfImproving.ReviewQueueItem, InfraSelfImproving.ReviewQueueService, CancellationToken, BoundingBox?, TrainingSegmentationMask?, Task> ApproveAsync,
    Action ClearPendingReviewGeometry,
    CancellationToken CancellationToken,
    Action<string> Log,
    Action<Action> OnUi,
    Action<string> SetReviewStatusText);

public sealed record TrainingSelectedReviewRejectRequest(
    InfraSelfImproving.ReviewQueueItem? Item,
    InfraSelfImproving.ReviewQueueService? QueueService,
    Func<InfraSelfImproving.ReviewQueueItem, InfraSelfImproving.ReviewQueueService, CancellationToken, Task> RejectAsync,
    CancellationToken CancellationToken,
    Action<string> Log,
    Action<Action> OnUi,
    Action<string> SetReviewStatusText);

public sealed record TrainingSelectedReviewCorrectionRequest(
    InfraSelfImproving.ReviewQueueItem? Item,
    InfraSelfImproving.ReviewQueueService? QueueService,
    string CorrectedCode,
    string? CorrectedDescription,
    Func<InfraSelfImproving.ReviewQueueItem, string, string?, CancellationToken, Task> ApplyCorrectionAsync,
    CancellationToken CancellationToken,
    Action<string> Log,
    Action<Action> OnUi,
    Action<string> SetReviewStatusText);

public static class TrainingSelectedReviewCommandWorkflow
{
    public static async Task ApproveAsync(TrainingSelectedReviewApproveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Item is null || request.QueueService is null)
            return;

        var box = request.GetPendingBox();
        var mask = request.GetPendingMask();

        try
        {
            await request.ApproveAsync(
                request.Item,
                request.QueueService,
                request.CancellationToken,
                box,
                mask).ConfigureAwait(false);
            request.ClearPendingReviewGeometry();
        }
        catch (Exception ex)
        {
            ReportError(request.Log, request.OnUi, request.SetReviewStatusText, "Review-Freigabe Fehler", ex);
        }
    }

    public static async Task RejectAsync(TrainingSelectedReviewRejectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Item is null || request.QueueService is null)
            return;

        try
        {
            await request.RejectAsync(
                request.Item,
                request.QueueService,
                request.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ReportError(request.Log, request.OnUi, request.SetReviewStatusText, "Review-Ablehnung Fehler", ex);
        }
    }

    public static async Task CorrectAsync(TrainingSelectedReviewCorrectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Item is null
            || request.QueueService is null
            || string.IsNullOrWhiteSpace(request.CorrectedCode))
            return;

        try
        {
            await request.ApplyCorrectionAsync(
                request.Item,
                request.CorrectedCode,
                request.CorrectedDescription,
                request.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ReportError(request.Log, request.OnUi, request.SetReviewStatusText, "Review-Korrektur Fehler", ex);
        }
    }

    private static void ReportError(
        Action<string> log,
        Action<Action> onUi,
        Action<string> setReviewStatusText,
        string prefix,
        Exception ex)
    {
        log($"{prefix}: {ex.Message}");
        onUi(() => setReviewStatusText($"Fehler: {ex.Message}"));
    }
}
