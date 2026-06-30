namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportScanResult(
    IReadOnlyList<TrainingCase> Found,
    IReadOnlyList<TrainingCase> CasesWithProtocol);

public static class TrainingBatchImportScanController
{
    public static async Task<TrainingBatchImportScanResult> ScanAsync(
        IEnumerable<string> rootFolders,
        Func<string, bool> directoryExists,
        Func<string, Task<IReadOnlyList<TrainingCase>>> scanFolderAsync,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(rootFolders);
        ArgumentNullException.ThrowIfNull(directoryExists);
        ArgumentNullException.ThrowIfNull(scanFolderAsync);
        ArgumentNullException.ThrowIfNull(log);

        var found = new List<TrainingCase>();
        foreach (var folder in rootFolders)
        {
            if (!directoryExists(folder))
            {
                log($"  WARNUNG: Ordner existiert nicht: {folder}");
                continue;
            }

            log($"  Scanne: {folder}");
            var result = await scanFolderAsync(folder).ConfigureAwait(false);
            found.AddRange(result);
        }

        var casesWithProtocol = found
            .Where(c => !string.IsNullOrEmpty(c.ProtocolPath))
            .ToList();

        log(TrainingBatchImportScanPresentationBuilder.BuildSummary(found.Count, casesWithProtocol.Count));
        foreach (var c in found)
            log(TrainingBatchImportScanPresentationBuilder.BuildCaseLine(c));

        return new TrainingBatchImportScanResult(found, casesWithProtocol);
    }
}
