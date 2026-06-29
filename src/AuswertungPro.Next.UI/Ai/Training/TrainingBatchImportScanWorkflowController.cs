namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportScanWorkflowResult(
    bool ShouldStop,
    IReadOnlyList<TrainingCase> CasesWithProtocol);

public static class TrainingBatchImportScanWorkflowController
{
    public static async Task<TrainingBatchImportScanWorkflowResult> RunAsync(
        int rootFolderCount,
        Func<Task<TrainingBatchImportScanResult>> scanAsync,
        ICollection<TrainingCase> cases,
        Action<string> log,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(scanAsync);
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatus);

        log($"Scanne {rootFolderCount} Ordner...");
        setStatus("Scanne Ordner...");

        var scan = await scanAsync();
        var summary = TrainingBatchImportScanPresentationBuilder.BuildSummary(
            scan.Found.Count,
            scan.CasesWithProtocol.Count);
        setStatus(summary);

        cases.Clear();
        foreach (var trainingCase in scan.Found)
            cases.Add(trainingCase);

        if (scan.CasesWithProtocol.Count == 0)
        {
            log("STOP: Keine Ordner mit Protokoll-Dateien gefunden.");
            setStatus("Keine Ordner mit Protokoll-Dateien gefunden.");
            return new TrainingBatchImportScanWorkflowResult(true, scan.CasesWithProtocol);
        }

        return new TrainingBatchImportScanWorkflowResult(false, scan.CasesWithProtocol);
    }
}
