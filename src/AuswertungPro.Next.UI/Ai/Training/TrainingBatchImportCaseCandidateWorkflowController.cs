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
        Action<TrainingBatchImportLivePreview> updateLivePreview,
        Action<Action> invokeOnUi,
        Action<SelfTrainingEntryResult> addResult,
        Action<string, MatchLevel> updateCodeDistribution,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(caseGeneration);
        ArgumentNullException.ThrowIfNull(existingSignatures);
        ArgumentNullException.ThrowIfNull(runSummary);
        ArgumentNullException.ThrowIfNull(updateLivePreview);
        ArgumentNullException.ThrowIfNull(invokeOnUi);
        ArgumentNullException.ThrowIfNull(addResult);
        ArgumentNullException.ThrowIfNull(updateCodeDistribution);
        ArgumentNullException.ThrowIfNull(log);

        updateLivePreview(caseGeneration.ProcessingPreview);

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
            updateLivePreview,
            invokeOnUi,
            addResult,
            updateCodeDistribution,
            log);

        return new TrainingBatchImportCaseCandidateWorkflowResult(
            !generatedCaseUi.ShouldContinueWithNextCase,
            generation.Samples);
    }
}
