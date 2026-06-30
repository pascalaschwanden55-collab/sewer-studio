namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportCaseProgressUiController
{
    public static void Apply(
        int zeroBasedIndex,
        int totalCount,
        TrainingCase trainingCase,
        Action<int> setProgressValue,
        Action<string> setStatus,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);
        ArgumentNullException.ThrowIfNull(setProgressValue);
        ArgumentNullException.ThrowIfNull(setStatus);
        ArgumentNullException.ThrowIfNull(log);

        setProgressValue(zeroBasedIndex + 1);
        var progressPresentation = TrainingBatchImportCaseProgressPresentationBuilder.Build(
            zeroBasedIndex,
            totalCount,
            trainingCase);
        setStatus(progressPresentation.StatusText);
        foreach (var line in progressPresentation.LogLines)
            log(line);
    }
}
