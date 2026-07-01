using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportCaseCandidateWorkflowResult(
    bool ShouldPersist,
    List<TrainingSample> NewSamples);

public static class TrainingBatchImportCaseCandidateWorkflowController
{
    public static TrainingBatchImportCaseCandidateWorkflowResult Apply(
        string caseId,
        TrainingBatchImportCaseGenerationResult caseGeneration,
        int nextResultIndex,
        ISet<string> existingSignatures,
        TrainingBatchImportRunSummary runSummary,
        TrainingBatchImportCaseUiSink caseUi)
    {
        ArgumentNullException.ThrowIfNull(caseGeneration);
        ArgumentNullException.ThrowIfNull(existingSignatures);
        ArgumentNullException.ThrowIfNull(runSummary);
        ArgumentNullException.ThrowIfNull(caseUi);

        caseUi.UpdateLivePreview(caseGeneration.ProcessingPreview);

        var generation = caseGeneration.Generation;
        var generatedCasePlan = TrainingBatchImportGeneratedCaseController.CreatePlan(
            caseId,
            generation,
            caseGeneration.PreviewFrame,
            nextResultIndex,
            existingSignatures);

        var generatedCaseUi = TrainingBatchImportGeneratedCaseUiController.Apply(
            generatedCasePlan,
            runSummary,
            caseUi);

        return new TrainingBatchImportCaseCandidateWorkflowResult(
            !generatedCaseUi.ShouldContinueWithNextCase,
            generation.Samples);
    }
}
