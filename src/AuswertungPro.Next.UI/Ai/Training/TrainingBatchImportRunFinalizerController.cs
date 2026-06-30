namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportRunFinalizerController
{
    public static void Apply(Action<bool> setBusy)
    {
        ArgumentNullException.ThrowIfNull(setBusy);

        setBusy(false);
    }
}
