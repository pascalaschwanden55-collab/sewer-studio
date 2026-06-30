using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportRunCompletionResult(bool ShouldStop);

public static class TrainingBatchImportRunCompletionController
{
    public static async Task<TrainingBatchImportRunCompletionResult> CompleteAsync(
        TrainingBatchImportRunSummary runSummary,
        int processedCaseCount,
        Func<Task<IReadOnlyList<TrainingSample>>> loadSamplesAsync,
        Action<IReadOnlyList<TrainingSample>> replaceSamples,
        Func<Task> refreshKbStatusAsync,
        Func<Task> saveStateAsync,
        Action<string> log,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(runSummary);
        ArgumentNullException.ThrowIfNull(loadSamplesAsync);
        ArgumentNullException.ThrowIfNull(replaceSamples);
        ArgumentNullException.ThrowIfNull(refreshKbStatusAsync);
        ArgumentNullException.ThrowIfNull(saveStateAsync);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatus);

        replaceSamples(Array.Empty<TrainingSample>());
        var allSamples = await loadSamplesAsync();
        replaceSamples(allSamples);

        if (runSummary.BuildNoNewStatus(processedCaseCount) is { } noNewStatus)
        {
            log(noNewStatus);
            setStatus(noNewStatus);
            return new TrainingBatchImportRunCompletionResult(true);
        }

        var finalStatus = runSummary.BuildCompletionStatus();
        log(finalStatus);
        setStatus(finalStatus);

        await refreshKbStatusAsync();
        await saveStateAsync();
        log("F\u00e4lle gespeichert. Batch-Import abgeschlossen.");

        return new TrainingBatchImportRunCompletionResult(false);
    }
}
