using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportSampleUiPlan(
    TrainingBatchImportLivePreview Preview,
    SelfTrainingEntryResult Result);

public static class TrainingBatchImportSampleUiPlanBuilder
{
    public static IReadOnlyList<TrainingBatchImportSampleUiPlan> Build(
        string caseId,
        IEnumerable<TrainingSample> samples,
        string? previewFrame,
        int firstResultIndex)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var index = firstResultIndex;
        return samples
            .Select(sample => new TrainingBatchImportSampleUiPlan(
                TrainingBatchImportLivePreviewBuilder.BuildSample(caseId, sample, previewFrame),
                TrainingBatchImportResultEntryFactory.CreateSample(index++, sample)))
            .ToList();
    }
}
