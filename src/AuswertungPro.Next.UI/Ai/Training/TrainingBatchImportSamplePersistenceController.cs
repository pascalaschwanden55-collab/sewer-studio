using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportSamplePersistenceResult(
    int SampleCount,
    int CodesCovered,
    string CandidateLogMessage,
    string StoredLogMessage);

public static class TrainingBatchImportSamplePersistenceController
{
    public static async Task<TrainingBatchImportSamplePersistenceResult> SaveCandidatesAsync(
        List<TrainingSample> newSamples,
        List<TrainingSample> allSamples,
        Func<List<TrainingSample>, Task> saveAsync)
    {
        ArgumentNullException.ThrowIfNull(newSamples);
        ArgumentNullException.ThrowIfNull(allSamples);
        ArgumentNullException.ThrowIfNull(saveAsync);

        await saveAsync(newSamples).ConfigureAwait(false);

        allSamples.AddRange(newSamples);
        var codesCovered = allSamples.Select(s => s.Code).Distinct().Count();
        return new TrainingBatchImportSamplePersistenceResult(
            allSamples.Count,
            codesCovered,
            $"{newSamples.Count} Samples als Kandidaten gespeichert (Status: Neu). Freigabe ueber Review (Modul I) - KEIN Auto-Index.",
            $"  Gespeichert | Gesamt: {allSamples.Count} Samples, {codesCovered} Codes");
    }
}
