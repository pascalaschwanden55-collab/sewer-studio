using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Services;

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
    Func<TrainingGoldKbReconcileRunWorkflowRequest, Task> RunReconcileAsync,
    Func<string, IProgress<string>?, CancellationToken, Task<KnowledgeBackupService.BackupResult>>? ExportBackupAsync = null);

public static class TrainingGoldKbReconcileCommandWorkflow
{
    public static async Task RunAsync(
        TrainingGoldKbReconcileCommandWorkflowRequest request,
        ITrainingSampleStore? trainingSamples = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.GetIsBusy);
        ArgumentNullException.ThrowIfNull(request.GetIsSelfTrainingRunning);
        ArgumentNullException.ThrowIfNull(request.ResetCancellation);
        ArgumentNullException.ThrowIfNull(request.RunReconcileAsync);

        if (request.GetIsBusy() || request.GetIsSelfTrainingRunning())
            return;

        var ct = request.ResetCancellation();
        var samples = trainingSamples ?? TrainingSamplesStore.Current;

        await request.RunReconcileAsync(
            TrainingGoldKbReconcileRequestFactory.CreateWithDefaults(
                request.SetBusy,
                samples.LoadAsync,
                samples.MergeOrUpdateAsync,
                request.IndexAsync,
                request.Log,
                request.SetStatus,
                request.OnUi,
                ct,
                request.ExportBackupAsync)).ConfigureAwait(false);
    }
}
