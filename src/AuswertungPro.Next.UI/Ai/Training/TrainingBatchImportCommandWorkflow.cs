namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportCommandWorkflowRequest(
    Func<bool> GetIsBusy,
    IReadOnlyCollection<string> RootFolders,
    Func<CancellationTokenSource> CreateCancellationSource,
    Action<CancellationTokenSource> StoreCancellationSource,
    Func<TrainingBatchImportAutoApproveConfirmationResult> ConfirmAutoApprove,
    Action<string> SetStatusText,
    Func<CancellationToken, Task> RunImportAsync);

public static class TrainingBatchImportCommandWorkflow
{
    public static async Task RunAsync(TrainingBatchImportCommandWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GetIsBusy())
            return;

        if (request.RootFolders.Count == 0)
        {
            request.SetStatusText("Bitte zuerst einen oder mehrere Ordner wählen.");
            return;
        }

        var runCts = request.CreateCancellationSource();

        var confirmation = request.ConfirmAutoApprove();
        if (!confirmation.ShouldContinue)
        {
            runCts.Dispose();
            request.SetStatusText(confirmation.StatusText ?? "");
            return;
        }

        request.StoreCancellationSource(runCts);
        await request.RunImportAsync(runCts.Token).ConfigureAwait(false);
    }
}
