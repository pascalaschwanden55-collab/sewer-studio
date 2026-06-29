namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportSkippedCaseUiPlan(
    TrainingBatchImportLivePreview Preview,
    SelfTrainingEntryResult Result);

public static class TrainingBatchImportSkippedCaseUiPlanBuilder
{
    public static TrainingBatchImportSkippedCaseUiPlan Build(
        string caseId,
        TrainingCenterBatchSkipInfo skip,
        string? previewFrame,
        int resultIndex)
        => new(
            new TrainingBatchImportLivePreview(
                caseId,
                skip.LiveCodeInfo,
                skip.LiveMeterInfo,
                previewFrame),
            TrainingBatchImportResultEntryFactory.CreateSkippedCase(
                resultIndex,
                caseId,
                skip.ResultSummary));
}
