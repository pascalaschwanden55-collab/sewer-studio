namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportCaseProgressUiController
{
    public static void Apply(
        int zeroBasedIndex,
        int totalCount,
        TrainingCase trainingCase,
        TrainingBatchUiSink ui)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);
        ArgumentNullException.ThrowIfNull(ui);

        ui.SetProgressValue(zeroBasedIndex + 1);
        var progressPresentation = TrainingBatchImportCaseProgressPresentationBuilder.Build(
            zeroBasedIndex,
            totalCount,
            trainingCase);
        ui.SetStatusText(progressPresentation.StatusText);
        foreach (var line in progressPresentation.LogLines)
            ui.Log(line);
    }
}
