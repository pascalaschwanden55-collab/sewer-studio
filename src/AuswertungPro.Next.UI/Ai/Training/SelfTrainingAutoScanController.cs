using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingAutoScanController
{
    public const string StatusText = "Scanne Ordner automatisch...";

    public static bool ShouldScan(int currentCaseCount, int rootFolderCount)
        => currentCaseCount == 0 && rootFolderCount > 0;

    public static async Task<IReadOnlyList<TrainingCase>> ScanAsync(
        IEnumerable<string> rootFolders,
        Func<string, bool> directoryExists,
        Func<string, Task<IReadOnlyList<TrainingCase>>> scanFolderAsync)
    {
        ArgumentNullException.ThrowIfNull(rootFolders);
        ArgumentNullException.ThrowIfNull(directoryExists);
        ArgumentNullException.ThrowIfNull(scanFolderAsync);

        var cases = new List<TrainingCase>();
        foreach (var folder in rootFolders)
        {
            if (!directoryExists(folder))
                continue;

            cases.AddRange(await scanFolderAsync(folder));
        }

        return cases;
    }

    public static async Task RunAsync(
        int currentCaseCount,
        int rootFolderCount,
        IEnumerable<string> rootFolders,
        Func<string, bool> directoryExists,
        Func<string, Task<IReadOnlyList<TrainingCase>>> scanFolderAsync,
        Action<string> setStatus,
        Action<TrainingCase> addCase)
    {
        ArgumentNullException.ThrowIfNull(rootFolders);
        ArgumentNullException.ThrowIfNull(directoryExists);
        ArgumentNullException.ThrowIfNull(scanFolderAsync);
        ArgumentNullException.ThrowIfNull(setStatus);
        ArgumentNullException.ThrowIfNull(addCase);

        if (!ShouldScan(currentCaseCount, rootFolderCount))
            return;

        setStatus(StatusText);
        var autoScannedCases = await ScanAsync(rootFolders, directoryExists, scanFolderAsync);
        foreach (var trainingCase in autoScannedCases)
            addCase(trainingCase);
    }
}
