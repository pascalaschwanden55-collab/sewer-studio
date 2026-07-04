using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingProtocolStartdataApprovalWorkflowRequest(
    InfraSelfImproving.ReviewQueueService? QueueService,
    Func<IReadOnlyList<InfraSelfImproving.ReviewQueueItem>> SelectItems,
    Func<InfraSelfImproving.ReviewQueueItem, InfraSelfImproving.ReviewQueueService, CancellationToken, Task> ApproveAsync,
    CancellationToken CancellationToken,
    Action<string> Log,
    Action<Action> OnUi,
    Action<string> SetReviewStatusText);

public static class TrainingProtocolStartdataApprovalWorkflow
{
    public static async Task RunAsync(TrainingProtocolStartdataApprovalWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SelectItems);
        ArgumentNullException.ThrowIfNull(request.ApproveAsync);
        ArgumentNullException.ThrowIfNull(request.Log);
        ArgumentNullException.ThrowIfNull(request.OnUi);
        ArgumentNullException.ThrowIfNull(request.SetReviewStatusText);

        if (request.QueueService is null)
            return;

        var items = request.SelectItems();
        var result = await TrainingProtocolStartdataApprovalController.ApproveAllAsync(
            items,
            (item, token) => request.ApproveAsync(item, request.QueueService, token),
            request.CancellationToken).ConfigureAwait(false);

        TrainingProtocolStartdataApprovalCompletionController.Apply(
            result,
            request.Log,
            request.OnUi,
            request.SetReviewStatusText);
    }
}
