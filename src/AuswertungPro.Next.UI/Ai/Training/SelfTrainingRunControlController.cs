using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingPauseToggleResult(
    bool Handled,
    string? StatusText,
    string? LogMessage);

public static class SelfTrainingRunControlController
{
    public static string RequestCancel(CancellationTokenSource? cancellationTokenSource)
    {
        cancellationTokenSource?.Cancel();
        return "Selbsttraining wird abgebrochen...";
    }

    public static SelfTrainingPauseToggleResult TogglePause(ISelfTrainingOrchestrator? orchestrator)
    {
        if (orchestrator is null)
            return new SelfTrainingPauseToggleResult(false, null, null);

        if (orchestrator.IsPaused)
        {
            orchestrator.Resume();
            return new SelfTrainingPauseToggleResult(
                true,
                "Selbsttraining fortgesetzt.",
                "Pipeline fortgesetzt.");
        }

        orchestrator.Pause();
        return new SelfTrainingPauseToggleResult(
            true,
            "Selbsttraining pausiert.",
            "Pipeline pausiert.");
    }
}
