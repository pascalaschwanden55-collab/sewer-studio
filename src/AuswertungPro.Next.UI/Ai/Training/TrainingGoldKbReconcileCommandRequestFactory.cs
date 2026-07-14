using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingGoldKbReconcileCommandRequestFactoryRequest(
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

public sealed record TrainingGoldKbReconcileCommandDefaultRequestFactoryRequest(
    Func<bool> GetIsBusy,
    Func<bool> GetIsSelfTrainingRunning,
    Func<CancellationToken> ResetCancellation,
    Action<bool> SetBusy,
    Func<List<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> IndexAsync,
    Action<string> Log,
    Action<string> SetStatus,
    Action<Action> OnUi,
    Func<string, IProgress<string>?, CancellationToken, Task<KnowledgeBackupService.BackupResult>>? ExportBackupAsync = null);

public static class TrainingGoldKbReconcileCommandRequestFactory
{
    public static TrainingGoldKbReconcileCommandWorkflowRequest Create(
        TrainingGoldKbReconcileCommandRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.GetIsBusy);
        ArgumentNullException.ThrowIfNull(request.GetIsSelfTrainingRunning);
        ArgumentNullException.ThrowIfNull(request.ResetCancellation);
        ArgumentNullException.ThrowIfNull(request.SetBusy);
        ArgumentNullException.ThrowIfNull(request.IndexAsync);
        ArgumentNullException.ThrowIfNull(request.Log);
        ArgumentNullException.ThrowIfNull(request.SetStatus);
        ArgumentNullException.ThrowIfNull(request.OnUi);
        ArgumentNullException.ThrowIfNull(request.RunReconcileAsync);

        return new TrainingGoldKbReconcileCommandWorkflowRequest(
            request.GetIsBusy,
            request.GetIsSelfTrainingRunning,
            request.ResetCancellation,
            request.SetBusy,
            request.IndexAsync,
            request.Log,
            request.SetStatus,
            request.OnUi,
            request.RunReconcileAsync,
            request.ExportBackupAsync);
    }

    public static TrainingGoldKbReconcileCommandWorkflowRequest CreateWithDefaults(
        TrainingGoldKbReconcileCommandDefaultRequestFactoryRequest request,
        Func<TrainingGoldKbReconcileRunWorkflowRequest, Task>? runReconcileAsync = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Create(new TrainingGoldKbReconcileCommandRequestFactoryRequest(
            request.GetIsBusy,
            request.GetIsSelfTrainingRunning,
            request.ResetCancellation,
            request.SetBusy,
            request.IndexAsync,
            request.Log,
            request.SetStatus,
            request.OnUi,
            runReconcileAsync ?? TrainingGoldKbReconcileRunWorkflow.RunAsync,
            request.ExportBackupAsync));
    }
}
