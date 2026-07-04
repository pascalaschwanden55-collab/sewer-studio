using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingGoldKbReconcileRunWorkflowRequest(
    Action<bool> SetBusy,
    Func<Task<List<TrainingSample>>> LoadSamplesAsync,
    Func<List<TrainingSample>, Task> MergeOrUpdateAsync,
    Func<List<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> IndexAsync,
    Func<string, IProgress<string>, CancellationToken, Task<TrainingGoldKbReconcileBackupResult>> ExportBackupAsync,
    Func<string> GetKnowledgeBaseRoot,
    Func<DateTime> GetNow,
    Action<string> CreateDirectory,
    Action<string> Log,
    Action<string> SetStatus,
    Action<Action> OnUi,
    CancellationToken CancellationToken);

public static class TrainingGoldKbReconcileRunWorkflow
{
    public static async Task RunAsync(TrainingGoldKbReconcileRunWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            request.SetBusy(true);

            await TrainingGoldKbReconcileWorkflowController.RunAsync(
                request.LoadSamplesAsync,
                request.MergeOrUpdateAsync,
                request.IndexAsync,
                request.ExportBackupAsync,
                request.GetKnowledgeBaseRoot,
                request.GetNow,
                request.CreateDirectory,
                request.Log,
                request.SetStatus,
                request.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            request.Log("KB-Nachholen abgebrochen.");
            request.SetStatus("KB-Nachholen abgebrochen");
        }
        catch (Exception ex)
        {
            request.Log($"KB-Nachholen Fehler: {ex.Message}");
            request.SetStatus("KB-Nachholen fehlgeschlagen");
        }
        finally
        {
            request.OnUi(() => request.SetBusy(false));
        }
    }
}
