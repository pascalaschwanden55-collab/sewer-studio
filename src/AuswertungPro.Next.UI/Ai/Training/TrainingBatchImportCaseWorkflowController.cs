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
        TrainingBatchImportCaseUiSink caseUi,
        Func<List<TrainingSample>, Task> saveSamplesAsync,
        Func<Task> saveStateAsync,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);
        ArgumentNullException.ThrowIfNull(existingSignatures);
        ArgumentNullException.ThrowIfNull(allSamples);
        ArgumentNullException.ThrowIfNull(runSummary);
        ArgumentNullException.ThrowIfNull(extractPreviewFrameAsync);
        ArgumentNullException.ThrowIfNull(generateWithDiagnosticsAsync);
        ArgumentNullException.ThrowIfNull(caseUi);
        ArgumentNullException.ThrowIfNull(saveSamplesAsync);
        ArgumentNullException.ThrowIfNull(saveStateAsync);

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
            caseUi);
        if (!candidateWorkflow.ShouldPersist)
            return new TrainingBatchImportCaseWorkflowResult(true);

        await TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(
            candidateWorkflow.NewSamples,
            allSamples,
            processedCount,
            saveSamplesAsync,
            saveStateAsync,
            caseUi).ConfigureAwait(false);

        return new TrainingBatchImportCaseWorkflowResult(false);
    }
}
