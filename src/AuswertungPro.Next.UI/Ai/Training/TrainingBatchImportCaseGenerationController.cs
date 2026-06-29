using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportCaseGenerationResult(
    string? PreviewFrame,
    TrainingBatchImportLivePreview ProcessingPreview,
    TrainingSampleGenerationResult Generation);

public static class TrainingBatchImportCaseGenerationController
{
    public static async Task<TrainingBatchImportCaseGenerationResult> GenerateAsync(
        TrainingCase trainingCase,
        IReadOnlyCollection<string> existingSignatures,
        Func<TrainingCase, CancellationToken, Task<string?>> extractPreviewFrameAsync,
        Func<TrainingCaseInput, IReadOnlyCollection<string>, CancellationToken, Task<TrainingSampleGenerationResult>> generateWithDiagnosticsAsync,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);
        ArgumentNullException.ThrowIfNull(existingSignatures);
        ArgumentNullException.ThrowIfNull(extractPreviewFrameAsync);
        ArgumentNullException.ThrowIfNull(generateWithDiagnosticsAsync);

        var previewFrame = await extractPreviewFrameAsync(trainingCase, ct).ConfigureAwait(false);
        var processingPreview = TrainingBatchImportLivePreviewBuilder.BuildProcessing(
            trainingCase.CaseId,
            previewFrame);
        var generation = await generateWithDiagnosticsAsync(
            new TrainingCaseInput(
                trainingCase.CaseId,
                trainingCase.FolderPath,
                trainingCase.VideoPath,
                trainingCase.ProtocolPath,
                trainingCase.InspectionDate),
            existingSignatures,
            ct).ConfigureAwait(false);

        return new TrainingBatchImportCaseGenerationResult(
            previewFrame,
            processingPreview,
            generation);
    }
}
