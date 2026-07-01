using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportGeneratedCaseUiResult(bool ShouldContinueWithNextCase);

public static class TrainingBatchImportGeneratedCaseUiController
{
    public static TrainingBatchImportGeneratedCaseUiResult Apply(
        TrainingBatchImportGeneratedCasePlan generatedCasePlan,
        TrainingBatchImportRunSummary runSummary,
        TrainingBatchImportCaseUiSink caseUi)
    {
        ArgumentNullException.ThrowIfNull(generatedCasePlan);
        ArgumentNullException.ThrowIfNull(runSummary);
        ArgumentNullException.ThrowIfNull(caseUi);

        if (generatedCasePlan.Kind == TrainingBatchImportGeneratedCaseKind.Skipped)
        {
            var skip = generatedCasePlan.Skip!;
            runSummary.RecordSkip(skip.Kind);

            var skipUiPlan = generatedCasePlan.SkippedCase!;
            caseUi.Log(skip.LogMessage);
            caseUi.UpdateLivePreview(skipUiPlan.Preview);
            caseUi.InvokeOnUi(() => caseUi.AddResult(skipUiPlan.Result));
            return new TrainingBatchImportGeneratedCaseUiResult(true);
        }

        foreach (var plan in generatedCasePlan.SampleUiPlans)
        {
            caseUi.UpdateLivePreview(plan.Preview);
            caseUi.InvokeOnUi(() =>
            {
                caseUi.AddResult(plan.Result);
                caseUi.UpdateCodeDistribution(plan.Result.VsaCode, plan.Result.Level);
            });
        }

        runSummary.AddNewSamples(generatedCasePlan.NewSampleCount);
        foreach (var line in generatedCasePlan.SampleLogLines)
            caseUi.Log(line);

        return new TrainingBatchImportGeneratedCaseUiResult(false);
    }
}
