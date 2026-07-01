using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public enum TrainingBatchImportGeneratedCaseKind
{
    Skipped,
    Samples
}

public sealed record TrainingBatchImportSkippedCaseUiPlan(
    TrainingBatchImportLivePreview Preview,
    SelfTrainingEntryResult Result);

public sealed record TrainingBatchImportGeneratedCasePlan(
    TrainingBatchImportGeneratedCaseKind Kind,
    TrainingCenterBatchSkipInfo? Skip,
    TrainingBatchImportSkippedCaseUiPlan? SkippedCase,
    IReadOnlyList<TrainingBatchImportSampleUiPlan> SampleUiPlans,
    IReadOnlyList<string> SampleLogLines,
    int NewSampleCount);

public static class TrainingBatchImportGeneratedCaseController
{
    public static TrainingBatchImportGeneratedCasePlan CreatePlan(
        string caseId,
        TrainingSampleGenerationResult generation,
        string? previewFrame,
        int firstResultIndex,
        ISet<string> existingSignatures)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(existingSignatures);

        var samples = generation.Samples;
        if (samples.Count == 0)
        {
            var skip = TrainingCenterSampleGenerationStatusFormatter.FormatBatchSkip(generation);
            return new TrainingBatchImportGeneratedCasePlan(
                TrainingBatchImportGeneratedCaseKind.Skipped,
                skip,
                CreateSkippedCaseUiPlan(
                    caseId,
                    skip,
                    previewFrame,
                    firstResultIndex),
                [],
                [],
                0);
        }

        foreach (var sample in samples)
        {
            sample.Status = TrainingSampleStatus.New;
            existingSignatures.Add(sample.Signature);
        }

        return new TrainingBatchImportGeneratedCasePlan(
            TrainingBatchImportGeneratedCaseKind.Samples,
            null,
            null,
            TrainingBatchImportSampleUiPlanBuilder.Build(
                caseId,
                samples,
                previewFrame,
                firstResultIndex),
            BuildSampleLogLines(samples),
            samples.Count);
    }

    private static TrainingBatchImportSkippedCaseUiPlan CreateSkippedCaseUiPlan(
        string caseId,
        TrainingCenterBatchSkipInfo skip,
        string? previewFrame,
        int resultIndex)
        => new TrainingBatchImportSkippedCaseUiPlan(
            new TrainingBatchImportLivePreview(
                caseId,
                skip.LiveCodeInfo,
                skip.LiveMeterInfo,
                previewFrame),
            TrainingBatchImportResultEntryFactory.CreateSkippedCase(
                resultIndex,
                caseId,
                skip.ResultSummary));

    private static IReadOnlyList<string> BuildSampleLogLines(IReadOnlyCollection<TrainingSample> samples)
    {
        var lines = new List<string>
        {
            $"  -> {samples.Count} Samples (Status: Neu, Freigabe ueber Review):"
        };
        lines.AddRange(samples.Select(sample =>
            $"     {sample.Code} @ {sample.MeterStart:F2}m [{sample.Status}] - {sample.Beschreibung}"));
        return lines;
    }
}
