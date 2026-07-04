namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportRunControlController
{
    public static void Cancel(Action cancel, Action<string> setStatusText)
    {
        ArgumentNullException.ThrowIfNull(cancel);
        ArgumentNullException.ThrowIfNull(setStatusText);

        cancel();
        setStatusText("Abbruch angefordert...");
    }
}
