using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingStepWorkflowRequestFactoryRequest(
    SelfTrainingStep Step,
    string ActiveVisionModel,
    Action<Action> OnUi,
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
    Action<string?> SetLiveFrame,
    SelfTrainingMatchRateTracker MatchRateTracker,
    Action RefreshMatchRatePercents,
    IList<SelfTrainingEntryResult> Results,
    Action<string, MatchLevel> UpdateCodeDistribution);

public static class SelfTrainingStepWorkflowRequestFactory
{
    public static SelfTrainingStepWorkflowRequest Create(SelfTrainingStepWorkflowRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Step);
        ArgumentNullException.ThrowIfNull(request.ActiveVisionModel);
        ArgumentNullException.ThrowIfNull(request.OnUi);
        ArgumentNullException.ThrowIfNull(request.SetPipelineActiveStep);
        ArgumentNullException.ThrowIfNull(request.SetCurrentEntryCode);
        ArgumentNullException.ThrowIfNull(request.SetCurrentEntryMeter);
        ArgumentNullException.ThrowIfNull(request.SetProgressValue);
        ArgumentNullException.ThrowIfNull(request.SetProgressMax);
        ArgumentNullException.ThrowIfNull(request.SetActiveModelName);
        ArgumentNullException.ThrowIfNull(request.SetIsModelActive);
        ArgumentNullException.ThrowIfNull(request.SetCurrentTechniqueGrade);
        ArgumentNullException.ThrowIfNull(request.SetCurrentTechniqueDetails);
        ArgumentNullException.ThrowIfNull(request.SetCurrentComparisonText);
        ArgumentNullException.ThrowIfNull(request.Log);
        ArgumentNullException.ThrowIfNull(request.SetLiveFrame);
        ArgumentNullException.ThrowIfNull(request.MatchRateTracker);
        ArgumentNullException.ThrowIfNull(request.RefreshMatchRatePercents);
        ArgumentNullException.ThrowIfNull(request.Results);
        ArgumentNullException.ThrowIfNull(request.UpdateCodeDistribution);

        return new SelfTrainingStepWorkflowRequest(
            request.Step,
            request.ActiveVisionModel,
            request.OnUi,
            new SelfTrainingStepWorkflowUi(
                request.SetPipelineActiveStep,
                request.SetCurrentEntryCode,
                request.SetCurrentEntryMeter,
                request.SetProgressValue,
                request.SetProgressMax,
                request.SetActiveModelName,
                request.SetIsModelActive,
                request.SetCurrentTechniqueGrade,
                request.SetCurrentTechniqueDetails,
                request.SetCurrentComparisonText,
                request.Log,
                request.SetLiveFrame),
            request.MatchRateTracker,
            request.RefreshMatchRatePercents,
            request.Results,
            request.UpdateCodeDistribution);
    }
}
