namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportSamplePersistenceUiController
{
    public static void Apply(
        TrainingBatchImportSamplePersistenceResult persistence,
        Action<string> log,
        Action<Action> invokeOnUi,
        Action<int> setSampleCount,
        Action<int> setCodesCovered)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(invokeOnUi);
        ArgumentNullException.ThrowIfNull(setSampleCount);
        ArgumentNullException.ThrowIfNull(setCodesCovered);

        log(persistence.CandidateLogMessage);
        invokeOnUi(() =>
        {
            setSampleCount(persistence.SampleCount);
            setCodesCovered(persistence.CodesCovered);
        });
        log(persistence.StoredLogMessage);
    }
}
