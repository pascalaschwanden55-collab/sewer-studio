namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportRunExceptionController
{
    public static void RecordCaseFailure(
        Exception exception,
        TrainingBatchImportRunSummary summary,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(log);

        summary.RecordError(exception.Message);
        log($"  FEHLER: {exception.Message}");
    }

    public static void ApplyCanceled(
        Action<string> log,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatus);

        log("Batch-Import abgebrochen durch Benutzer.");
        setStatus("Batch-Import abgebrochen.");
    }

    public static void ApplyFatal(
        Exception exception,
        Action<string> log,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatus);

        log($"FATALER FEHLER: {exception.Message}");
        setStatus($"Fehler beim Batch-Import: {exception.Message}");
    }
}
