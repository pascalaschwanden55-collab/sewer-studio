using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportGeneratedCaseUiResult(bool ShouldContinueWithNextCase);

public static class TrainingBatchImportGeneratedCaseUiController
{
    public static TrainingBatchImportGeneratedCaseUiResult Apply(
        TrainingBatchImportGeneratedCasePlan generatedCasePlan,
        TrainingBatchImportRunSummary runSummary,
        Action<TrainingBatchImportLivePreview> updateLivePreview,
        Action<Action> invokeOnUi,
        Action<SelfTrainingEntryResult> addResult,
        Action<string, MatchLevel> updateCodeDistribution,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(generatedCasePlan);
        ArgumentNullException.ThrowIfNull(runSummary);
        ArgumentNullException.ThrowIfNull(updateLivePreview);
        ArgumentNullException.ThrowIfNull(invokeOnUi);
        ArgumentNullException.ThrowIfNull(addResult);
        ArgumentNullException.ThrowIfNull(updateCodeDistribution);
        ArgumentNullException.ThrowIfNull(log);

        if (generatedCasePlan.Kind == TrainingBatchImportGeneratedCaseKind.Skipped)
        {
            var skip = generatedCasePlan.Skip!;
            runSummary.RecordSkip(skip.Kind);

            var skipUiPlan = generatedCasePlan.SkippedCase!;
            log(skip.LogMessage);
            updateLivePreview(skipUiPlan.Preview);
            invokeOnUi(() => addResult(skipUiPlan.Result));
            return new TrainingBatchImportGeneratedCaseUiResult(true);
        }

        foreach (var plan in generatedCasePlan.SampleUiPlans)
        {
            updateLivePreview(plan.Preview);
            invokeOnUi(() =>
            {
                addResult(plan.Result);
                updateCodeDistribution(plan.Result.VsaCode, plan.Result.Level);
            });
        }

        runSummary.AddNewSamples(generatedCasePlan.NewSampleCount);
        foreach (var line in generatedCasePlan.SampleLogLines)
            log(line);

        return new TrainingBatchImportGeneratedCaseUiResult(false);
    }
}
