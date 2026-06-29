namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportRunControlController
{
    public static string RequestCancel(CancellationTokenSource? cancellationTokenSource)
    {
        cancellationTokenSource?.Cancel();
        return TrainingBatchImportTerminalPresentationBuilder.BuildCancelRequestedStatus();
    }
}
