using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportExistingSampleSnapshot(
    List<TrainingSample> AllSamples,
    HashSet<string> ExistingSignatures);

public static class TrainingBatchImportExistingSampleSnapshotController
{
    public static async Task<TrainingBatchImportExistingSampleSnapshot> LoadAsync(
        Func<Task<List<TrainingSample>>> loadSamplesAsync,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(loadSamplesAsync);
        ArgumentNullException.ThrowIfNull(log);

        var allSamples = await loadSamplesAsync().ConfigureAwait(false);
        var existingSigs = allSamples.Select(s => s.Signature)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet(StringComparer.Ordinal);
        log($"Bestehende Samples: {allSamples.Count} ({existingSigs.Count} Signaturen)");

        return new TrainingBatchImportExistingSampleSnapshot(allSamples, existingSigs);
    }
}
