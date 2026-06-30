namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportCaseLoopController
{
    public static async Task RunAsync(
        IReadOnlyList<TrainingCase> casesToProcess,
        Action<int, int, TrainingCase> applyProgress,
        Func<int, TrainingCase, CancellationToken, Task> processCaseAsync,
        Action<Exception> recordCaseFailure,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(casesToProcess);
        ArgumentNullException.ThrowIfNull(applyProgress);
        ArgumentNullException.ThrowIfNull(processCaseAsync);
        ArgumentNullException.ThrowIfNull(recordCaseFailure);

        for (var i = 0; i < casesToProcess.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var trainingCase = casesToProcess[i];
            applyProgress(i, casesToProcess.Count, trainingCase);

            try
            {
                await processCaseAsync(i, trainingCase, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                recordCaseFailure(ex);
            }
        }
    }
}
