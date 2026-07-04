using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public enum TrainingReviewItemDecision
{
    Approve,
    Reject
}

public sealed record TrainingReviewItemDecisionWorkflowRequest(
    InfraSelfImproving.ReviewQueueItem Item,
    TrainingReviewItemDecision Decision,
    string CorrectedCode,
    string? CorrectedDescription,
    BoundingBox? Box,
    TrainingSegmentationMask? Mask,
    InfraSelfImproving.ReviewQueueService QueueService,
    ICollection<InfraSelfImproving.ReviewQueueItem> ReviewQueue,
    CancellationToken CancellationToken,
    string ConfirmedByUser,
    Func<InfraSelfImproving.ReviewQueueItem, string, bool, CancellationToken, Task> ProcessFeedbackAsync,
    Func<InfraSelfImproving.ReviewQueueItem, Task<string?>> ResolveSampleIdAsync,
    Func<IReviewApprovalService> CreateApprovalService,
    Func<Task> ReloadSamplesAsync,
    Action<Action> OnUi,
    Action<int> SetReviewQueueCount,
    Action<string> SetReviewStatusText,
    Action<string> Log);

public static class TrainingReviewItemDecisionWorkflow
{
    public static async Task RunAsync(TrainingReviewItemDecisionWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Item.Entry is not null)
        {
            await request.ProcessFeedbackAsync(
                request.Item,
                ResolveFeedbackCode(request),
                request.Decision == TrainingReviewItemDecision.Approve,
                request.CancellationToken).ConfigureAwait(false);
        }
        else if (request.Item.IsFromSelfTraining)
        {
            await ApplySelfTrainingDecisionAsync(request).ConfigureAwait(false);
        }

        request.OnUi(() =>
        {
            var completion = request.Decision == TrainingReviewItemDecision.Approve
                ? TrainingReviewQueueCompletionController.ApplyApproved(
                    request.Item,
                    request.QueueService,
                    request.ReviewQueue)
                : TrainingReviewQueueCompletionController.ApplyRejected(
                    request.Item,
                    request.CorrectedCode,
                    request.QueueService,
                    request.ReviewQueue);

            request.SetReviewQueueCount(completion.ReviewQueueCount);
            request.SetReviewStatusText(completion.StatusText);
            request.Log(completion.LogText);
        });
    }

    private static async Task ApplySelfTrainingDecisionAsync(TrainingReviewItemDecisionWorkflowRequest request)
    {
        var sampleId = await request.ResolveSampleIdAsync(request.Item).ConfigureAwait(false);
        if (string.IsNullOrEmpty(sampleId))
        {
            request.Log($"Self-Training Review: Sample nicht gefunden ({request.Item.SelfTrainingCaseId}/{request.Item.SelfTrainingVsaCode}@{request.Item.SelfTrainingMeter:F1}m)");
            return;
        }

        var svc = request.CreateApprovalService();
        if (request.Decision == TrainingReviewItemDecision.Approve)
            await ApproveSelfTrainingAsync(request, svc, sampleId).ConfigureAwait(false);
        else
            await RejectSelfTrainingAsync(request, svc, sampleId).ConfigureAwait(false);

        await request.ReloadSamplesAsync().ConfigureAwait(false);
    }

    private static async Task ApproveSelfTrainingAsync(
        TrainingReviewItemDecisionWorkflowRequest request,
        IReviewApprovalService svc,
        string sampleId)
    {
        var result = await svc.ApproveSelfTrainingAsync(
            sampleId,
            request.Box,
            request.CancellationToken,
            request.ConfirmedByUser,
            request.Mask).ConfigureAwait(false);

        if (!result.Found)
            return;

        var bboxInfo = request.Box.HasValue ? " (Box gesetzt)" : "";
        request.Log($"Self-Training Review: {request.Item.SelfTrainingVsaCode}@{request.Item.SelfTrainingMeter:F1}m → Approved{bboxInfo}, KB: {(result.Indexed ? "Indexed" : "Error")}");
    }

    private static async Task RejectSelfTrainingAsync(
        TrainingReviewItemDecisionWorkflowRequest request,
        IReviewApprovalService svc,
        string sampleId)
    {
        var result = await svc.RejectSelfTrainingAsync(
            sampleId,
            request.CorrectedCode,
            request.CancellationToken,
            request.ConfirmedByUser,
            request.CorrectedDescription).ConfigureAwait(false);

        if (!result.Found)
            return;

        if (!string.IsNullOrEmpty(result.CorrectedSampleId))
            request.Log($"Korrigiertes Sample {result.CorrectedSampleId} erzeugt");
        else
            request.Log($"Self-Training Review: {request.Item.SelfTrainingVsaCode}@{request.Item.SelfTrainingMeter:F1}m → Rejected");
    }

    private static string ResolveFeedbackCode(TrainingReviewItemDecisionWorkflowRequest request)
        => request.Decision == TrainingReviewItemDecision.Approve
            ? request.Item.Entry?.SuggestedCode ?? ""
            : request.CorrectedCode;
}
