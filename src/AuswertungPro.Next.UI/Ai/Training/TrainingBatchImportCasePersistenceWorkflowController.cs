using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportCasePersistenceWorkflowController
{
    public static async Task PersistAsync(
        List<TrainingSample> newSamples,
        List<TrainingSample> allSamples,
        int processedCount,
        Func<List<TrainingSample>, Task> saveSamplesAsync,
        Func<Task> saveStateAsync,
        TrainingBatchImportCaseUiSink caseUi)
    {
        ArgumentNullException.ThrowIfNull(newSamples);
        ArgumentNullException.ThrowIfNull(allSamples);
        ArgumentNullException.ThrowIfNull(saveSamplesAsync);
        ArgumentNullException.ThrowIfNull(saveStateAsync);
        ArgumentNullException.ThrowIfNull(caseUi);

        var persistence = await TrainingBatchImportSamplePersistenceController.SaveCandidatesAsync(
            newSamples,
            allSamples,
            saveSamplesAsync);

        caseUi.Log(persistence.CandidateLogMessage);
        caseUi.InvokeOnUi(() =>
        {
            caseUi.SetSampleCount(persistence.SampleCount);
            caseUi.SetCodesCovered(persistence.CodesCovered);
        });
        caseUi.Log(persistence.StoredLogMessage);

        if (processedCount > 0 && processedCount % 5 == 0)
        {
            try
            {
                await saveStateAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best-Effort: Batch-Import darf wegen Autosave-State nicht abbrechen.
            }
        }
    }
}
