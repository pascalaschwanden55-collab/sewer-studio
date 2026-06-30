using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingRunStartController
{
    public static void Apply(
        TrainingCase trainingCase,
        Action<bool> setBusy,
        Action<bool> setSelfTrainingRunning,
        Action resetSelfTrainingVisuals,
        Action<string> setLogText,
        Action<string> setStatusText,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);
        ArgumentNullException.ThrowIfNull(setBusy);
        ArgumentNullException.ThrowIfNull(setSelfTrainingRunning);
        ArgumentNullException.ThrowIfNull(resetSelfTrainingVisuals);
        ArgumentNullException.ThrowIfNull(setLogText);
        ArgumentNullException.ThrowIfNull(setStatusText);
        ArgumentNullException.ThrowIfNull(log);

        setBusy(true);
        setSelfTrainingRunning(true);
        resetSelfTrainingVisuals();
        setLogText("");

        var startPresentation = SelfTrainingRunPresentationBuilder.BuildStart(trainingCase);
        setStatusText(startPresentation.StatusText);
        foreach (var line in startPresentation.LogLines)
            log(line);
    }
}
