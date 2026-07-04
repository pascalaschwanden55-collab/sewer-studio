using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingRunControlController
{
    public static void Stop(Action cancel, Action<string> setStatusText)
    {
        ArgumentNullException.ThrowIfNull(cancel);
        ArgumentNullException.ThrowIfNull(setStatusText);

        cancel();
        setStatusText("Selbsttraining wird abgebrochen...");
    }

    public static void TogglePause(
        ISelfTrainingOrchestrator? orchestrator,
        Action<string> setStatusText,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(setStatusText);
        ArgumentNullException.ThrowIfNull(log);

        if (orchestrator is null) return;

        if (orchestrator.IsPaused)
        {
            orchestrator.Resume();
            setStatusText("Selbsttraining fortgesetzt.");
            log("Pipeline fortgesetzt.");
            return;
        }

        orchestrator.Pause();
        setStatusText("Selbsttraining pausiert.");
        log("Pipeline pausiert.");
    }
}
