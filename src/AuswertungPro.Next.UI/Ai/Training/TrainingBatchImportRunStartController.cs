namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportRunStartController
{
    public static void Apply(
        TrainingBatchUiSink ui,
        Action clearLivePreview,
        Action resetSelfTrainingVisuals)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(clearLivePreview);
        ArgumentNullException.ThrowIfNull(resetSelfTrainingVisuals);

        ui.SetBusy(true);
        ui.SetLogText("");
        ui.SetProgressValue(0);
        ui.SetProgressMax(1);
        clearLivePreview();
        resetSelfTrainingVisuals();
    }
}
