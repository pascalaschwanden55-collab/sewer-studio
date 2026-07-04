using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingGoldKbReconcileCommandWorkflowRequest(
    Func<bool> GetIsBusy,
    Func<bool> GetIsSelfTrainingRunning,
    Func<CancellationToken> ResetCancellation,
    Action<bool> SetBusy,
    Func<List<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> IndexAsync,
    Action<string> Log,
    Action<string> SetStatus,
    Action<Action> OnUi,
    Func<TrainingGoldKbReconcileRunWorkflowRequest, Task> RunReconcileAsync);

public static class TrainingGoldKbReconcileCommandWorkflow
{
    public static async Task RunAsync(TrainingGoldKbReconcileCommandWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.GetIsBusy);
        ArgumentNullException.ThrowIfNull(request.GetIsSelfTrainingRunning);
        ArgumentNullException.ThrowIfNull(request.ResetCancellation);
        ArgumentNullException.ThrowIfNull(request.RunReconcileAsync);

        if (request.GetIsBusy() || request.GetIsSelfTrainingRunning())
            return;

        var ct = request.ResetCancellation();

        await request.RunReconcileAsync(
            TrainingGoldKbReconcileRequestFactory.CreateWithDefaults(
                request.SetBusy,
                TrainingSamplesStore.LoadAsync,
                TrainingSamplesStore.MergeOrUpdateAsync,
                request.IndexAsync,
                request.Log,
                request.SetStatus,
                request.OnUi,
                ct)).ConfigureAwait(false);
    }
}
