using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportCaseWorkflowResult(bool ShouldContinueWithNextCase);

public static class TrainingBatchImportCaseWorkflowController
{
    public static async Task<TrainingBatchImportCaseWorkflowResult> ProcessAsync(
        TrainingCase trainingCase,
        HashSet<string> existingSignatures,
        List<TrainingSample> allSamples,
        int firstResultIndex,
        int processedCount,
        TrainingBatchImportRunSummary runSummary,
        Func<TrainingCase, CancellationToken, Task<string?>> extractPreviewFrameAsync,
        Func<TrainingCaseInput, IReadOnlyCollection<string>, CancellationToken, Task<TrainingSampleGenerationResult>> generateWithDiagnosticsAsync,
        Action<TrainingBatchImportLivePreview> updateLivePreview,
        Action<Action> invokeOnUi,
        Action<SelfTrainingEntryResult> addResult,
        Action<string, MatchLevel> updateCodeDistribution,
        Func<List<TrainingSample>, Task> saveSamplesAsync,
        Func<Task> saveStateAsync,
        Action<int> setSampleCount,
        Action<int> setCodesCovered,
        Action<string> log,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);
        ArgumentNullException.ThrowIfNull(existingSignatures);
        ArgumentNullException.ThrowIfNull(allSamples);
        ArgumentNullException.ThrowIfNull(runSummary);
        ArgumentNullException.ThrowIfNull(extractPreviewFrameAsync);
        ArgumentNullException.ThrowIfNull(generateWithDiagnosticsAsync);
        ArgumentNullException.ThrowIfNull(updateLivePreview);
        ArgumentNullException.ThrowIfNull(invokeOnUi);
        ArgumentNullException.ThrowIfNull(addResult);
        ArgumentNullException.ThrowIfNull(updateCodeDistribution);
        ArgumentNullException.ThrowIfNull(saveSamplesAsync);
        ArgumentNullException.ThrowIfNull(saveStateAsync);
        ArgumentNullException.ThrowIfNull(setSampleCount);
        ArgumentNullException.ThrowIfNull(setCodesCovered);
        ArgumentNullException.ThrowIfNull(log);

        var caseGeneration = await TrainingBatchImportCaseGenerationController.GenerateAsync(
            trainingCase,
            existingSignatures,
            extractPreviewFrameAsync,
            generateWithDiagnosticsAsync,
            ct).ConfigureAwait(false);

        var candidateWorkflow = TrainingBatchImportCaseCandidateWorkflowController.Apply(
            trainingCase.CaseId,
            caseGeneration,
            firstResultIndex,
            existingSignatures,
            runSummary,
            updateLivePreview,
            invokeOnUi,
            addResult,
            updateCodeDistribution,
            log);
        if (!candidateWorkflow.ShouldPersist)
            return new TrainingBatchImportCaseWorkflowResult(true);

        await TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(
            candidateWorkflow.NewSamples,
            allSamples,
            processedCount,
            saveSamplesAsync,
            saveStateAsync,
            invokeOnUi,
            setSampleCount,
            setCodesCovered,
            log).ConfigureAwait(false);

        return new TrainingBatchImportCaseWorkflowResult(false);
    }
}
