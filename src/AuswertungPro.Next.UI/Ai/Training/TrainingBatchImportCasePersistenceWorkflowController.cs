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
        Action<Action> invokeOnUi,
        Action<int> setSampleCount,
        Action<int> setCodesCovered,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(newSamples);
        ArgumentNullException.ThrowIfNull(allSamples);
        ArgumentNullException.ThrowIfNull(saveSamplesAsync);
        ArgumentNullException.ThrowIfNull(saveStateAsync);
        ArgumentNullException.ThrowIfNull(invokeOnUi);
        ArgumentNullException.ThrowIfNull(setSampleCount);
        ArgumentNullException.ThrowIfNull(setCodesCovered);
        ArgumentNullException.ThrowIfNull(log);

        var persistence = await TrainingBatchImportSamplePersistenceController.SaveCandidatesAsync(
            newSamples,
            allSamples,
            saveSamplesAsync);

        TrainingBatchImportSamplePersistenceUiController.Apply(
            persistence,
            log,
            invokeOnUi,
            setSampleCount,
            setCodesCovered);

        await TrainingBatchImportCaseStateSaveController.SaveIfDueAsync(
            processedCount,
            5,
            saveStateAsync);
    }
}
