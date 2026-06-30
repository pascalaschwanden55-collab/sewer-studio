namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportRunStartController
{
    public static void Apply(
        Action<bool> setBusy,
        Action<string> setLogText,
        Action<int> setProgressValue,
        Action<int> setProgressMax,
        Action clearLivePreview,
        Action resetSelfTrainingVisuals)
    {
        ArgumentNullException.ThrowIfNull(setBusy);
        ArgumentNullException.ThrowIfNull(setLogText);
        ArgumentNullException.ThrowIfNull(setProgressValue);
        ArgumentNullException.ThrowIfNull(setProgressMax);
        ArgumentNullException.ThrowIfNull(clearLivePreview);
        ArgumentNullException.ThrowIfNull(resetSelfTrainingVisuals);

        setBusy(true);
        setLogText("");
        setProgressValue(0);
        setProgressMax(1);
        clearLivePreview();
        resetSelfTrainingVisuals();
    }
}
