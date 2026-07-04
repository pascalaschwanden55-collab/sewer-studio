using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingStepWorkflowUi(
    Action<int> SetPipelineActiveStep,
    Action<string> SetCurrentEntryCode,
    Action<double> SetCurrentEntryMeter,
    Action<int> SetProgressValue,
    Action<int> SetProgressMax,
    Action<string> SetActiveModelName,
    Action<bool> SetIsModelActive,
    Action<string> SetCurrentTechniqueGrade,
    Action<string> SetCurrentTechniqueDetails,
    Action<string> SetCurrentComparisonText,
    Action<string> Log,
    Action<string?> SetLiveFrame);

public sealed record SelfTrainingStepWorkflowRequest(
    SelfTrainingStep Step,
    string ActiveVisionModel,
    Action<Action> OnUi,
    SelfTrainingStepWorkflowUi Ui,
    SelfTrainingMatchRateTracker MatchRateTracker,
    Action RefreshMatchRatePercents,
    IList<SelfTrainingEntryResult> Results,
    Action<string, MatchLevel> UpdateCodeDistribution);

public static class SelfTrainingStepWorkflow
{
    public static void Apply(SelfTrainingStepWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var presentation = SelfTrainingStepPresentationBuilder.Build(
            request.Step,
            request.ActiveVisionModel);

        void ApplyOnUi()
        {
            request.Ui.SetPipelineActiveStep(presentation.PipelineActiveStep);
            request.Ui.SetCurrentEntryCode(presentation.CurrentEntryCode);
            request.Ui.SetCurrentEntryMeter(presentation.CurrentEntryMeter);
            request.Ui.SetProgressValue(presentation.ProgressValue);
            request.Ui.SetProgressMax(presentation.ProgressMax);
            request.Ui.SetActiveModelName(presentation.ActiveModelName);
            request.Ui.SetIsModelActive(presentation.IsModelActive);

            if (presentation.CurrentTechniqueGrade is not null)
            {
                request.Ui.SetCurrentTechniqueGrade(presentation.CurrentTechniqueGrade);
                request.Ui.SetCurrentTechniqueDetails(presentation.CurrentTechniqueDetails ?? "");
            }

            if (presentation.CurrentComparisonText is not null)
                request.Ui.SetCurrentComparisonText(presentation.CurrentComparisonText);

            foreach (var logLine in presentation.LogLines)
                request.Ui.Log(logLine);

            if (presentation.LiveFramePath is not null)
                request.Ui.SetLiveFrame(presentation.LiveFramePath);

            if (presentation.Result is not null && presentation.CompletedMatchLevel is { } level)
            {
                request.MatchRateTracker.Record(level);
                request.RefreshMatchRatePercents();

                request.Results.Add(presentation.Result);
                request.UpdateCodeDistribution(presentation.Result.VsaCode, level);
            }
        }

        request.OnUi(ApplyOnUi);
    }
}
