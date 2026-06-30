using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportLivePreview(
    string CaseInfo,
    string CodeInfo,
    string MeterInfo,
    string? FramePath);

public static class TrainingBatchImportLivePreviewBuilder
{
    public static TrainingBatchImportLivePreview BuildProcessing(
        string caseId,
        string? previewFrame)
        => new(
            caseId,
            "Verarbeite...",
            "\u2014",
            previewFrame);

    public static TrainingBatchImportLivePreview BuildSample(
        string caseId,
        TrainingSample sample,
        string? previewFrame)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var sampleFrame = !string.IsNullOrEmpty(sample.FramePath)
            ? sample.FramePath
            : previewFrame;
        return new TrainingBatchImportLivePreview(
            caseId,
            sample.Code,
            $"{sample.MeterStart:F2} \u2013 {sample.MeterEnd:F2} m",
            sampleFrame);
    }
}
