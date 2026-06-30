namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingRunPreparationResult(
    CancellationToken CancellationToken,
    CancellationTokenSource CancellationTokenSource);

public static class SelfTrainingRunPreparationController
{
    public static SelfTrainingRunPreparationResult PrepareCancellation(
        CancellationTokenSource? previousCancellationTokenSource)
    {
        previousCancellationTokenSource?.Cancel();
        previousCancellationTokenSource?.Dispose();

        var cts = new CancellationTokenSource();
        return new SelfTrainingRunPreparationResult(cts.Token, cts);
    }
}
