namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingRunFinalizerController
{
    public static void Apply(
        Action<bool> setIsBusy,
        Action<bool> setIsSelfTrainingRunning,
        Action clearOrchestrator)
    {
        ArgumentNullException.ThrowIfNull(setIsBusy);
        ArgumentNullException.ThrowIfNull(setIsSelfTrainingRunning);
        ArgumentNullException.ThrowIfNull(clearOrchestrator);

        setIsBusy(false);
        setIsSelfTrainingRunning(false);
        clearOrchestrator();
    }
}
