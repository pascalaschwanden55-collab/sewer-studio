using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingRunStartController
{
    public static void Apply(
        TrainingCase trainingCase,
        SelfTrainingUiSink ui,
        Action resetSelfTrainingVisuals)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(resetSelfTrainingVisuals);

        ui.SetBusy(true);
        ui.SetSelfTrainingRunning(true);
        resetSelfTrainingVisuals();
        ui.SetLogText("");

        var startPresentation = SelfTrainingRunPresentationBuilder.BuildStart(trainingCase);
        ui.SetStatusText(startPresentation.StatusText);
        foreach (var line in startPresentation.LogLines)
            ui.Log(line);
    }
}
