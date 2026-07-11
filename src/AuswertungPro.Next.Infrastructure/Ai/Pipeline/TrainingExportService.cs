using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Exports training data to YOLO format via the Sidecar API.
/// Uses <see cref="IVisionPipelineClient"/> for HTTP communication.
/// </summary>
public sealed class TrainingExportService
{
    private const int SidecarExportMaxSamplesPerRequest = 500;

    private readonly IVisionPipelineClient _client;
    private readonly string _evalSetRoot;

    public TrainingExportService(IVisionPipelineClient client, string? evalSetRoot = null)
    {
        _client = client;
        _evalSetRoot = string.IsNullOrWhiteSpace(evalSetRoot)
            ? TrainingSamplesStore.EffectiveEvalSetRoot
            : Path.GetFullPath(evalSetRoot);
    }

    /// <summary>
    /// Export ground truth samples to YOLO training format.
    /// </summary>
    public async Task<TrainingExportResult> ExportAsync(
        IReadOnlyList<GroundTruthEntry> samples,
        string outputDir,
        double trainSplit = 0.8,
        CancellationToken ct = default)
    {
        if (samples.Count == 0)
            return new TrainingExportResult(false, "Keine Trainingssamples vorhanden.", 0, 0, 0);

        var evalImageHashes = EvalContaminationGuard.LoadEvalImageHashes(_evalSetRoot);
        var exportSamples = new List<TrainingExportSample>();
        var skippedEvalSamples = 0;
        foreach (var sample in samples)
        {
            if (string.IsNullOrWhiteSpace(sample.ExtractedFramePath) || !File.Exists(sample.ExtractedFramePath))
                continue;

            if (EvalContaminationGuard.IsEvalContaminated(evalImageHashes, sample.ExtractedFramePath))
            {
                skippedEvalSamples++;
                continue;
            }

            var imageBytes = await File.ReadAllBytesAsync(sample.ExtractedFramePath, ct).ConfigureAwait(false);
            var imageBase64 = Convert.ToBase64String(imageBytes);

            var labels = new List<TrainingExportSampleLabel>();
            if (!string.IsNullOrWhiteSpace(sample.VsaCode))
            {
                labels.Add(new TrainingExportSampleLabel(
                    ClassName: sample.VsaCode,
                    XCenter: 0.5,
                    YCenter: 0.5,
                    Width: 0.8,
                    Height: 0.8));
            }

            exportSamples.Add(new TrainingExportSample(imageBase64, labels));
        }

        if (exportSamples.Count == 0)
        {
            var reason = skippedEvalSamples > 0
                ? "Alle gueltigen Bilder gehoeren zu einem geschuetzten Eval-Set."
                : "Keine gueltigen Bilder gefunden.";
            return new TrainingExportResult(false, reason, 0, 0, 0, skippedEvalSamples);
        }

        if (exportSamples.Count > SidecarExportMaxSamplesPerRequest)
            return new TrainingExportResult(
                false,
                $"Sidecar-Export unterstuetzt maximal {SidecarExportMaxSamplesPerRequest} Samples pro Request. Bitte lokalen Export verwenden.",
                0,
                0,
                0);

        try
        {
            var request = new TrainingExportRequestDto(exportSamples, outputDir, trainSplit);
            var response = await _client.ExportTrainingAsync(request, ct).ConfigureAwait(false);

            return new TrainingExportResult(
                true, null,
                response.TotalSamples,
                response.TrainCount,
                response.ValCount,
                skippedEvalSamples);
        }
        catch (Exception ex)
        {
            return new TrainingExportResult(false, $"Export-Fehler: {ex.Message}", 0, 0, 0);
        }
    }
}

public sealed record TrainingExportResult(
    bool IsSuccess,
    string? Error,
    int TotalSamples,
    int TrainCount,
    int ValCount,
    int SkippedEvalSamples = 0
);
