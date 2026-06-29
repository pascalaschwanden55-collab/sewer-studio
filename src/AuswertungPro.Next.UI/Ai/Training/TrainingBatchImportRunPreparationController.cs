namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportRunPreparationResult(
    bool ShouldStop,
    CancellationToken CancellationToken,
    CancellationTokenSource? CancellationTokenSource);

public static class TrainingBatchImportRunPreparationController
{
    public static TrainingBatchImportRunPreparationResult Prepare(
        bool isBusy,
        int rootFolderCount,
        CancellationTokenSource? previousCancellationTokenSource,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(setStatus);
        if (isBusy)
            return Stop();

        if (rootFolderCount == 0)
        {
            setStatus("Bitte zuerst einen oder mehrere Ordner wählen.");
            return Stop();
        }

        previousCancellationTokenSource?.Cancel();
        previousCancellationTokenSource?.Dispose();

        var cts = new CancellationTokenSource();
        return new TrainingBatchImportRunPreparationResult(ShouldStop: false, cts.Token, cts);
    }

    private static TrainingBatchImportRunPreparationResult Stop()
        => new(ShouldStop: true, default, null);
}
