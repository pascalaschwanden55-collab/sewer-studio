using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Exports training data to YOLO format via the Sidecar API.
/// Uses <see cref="VisionPipelineClient"/> for HTTP communication.
/// </summary>
public sealed class TrainingExportService
{
    private readonly VisionPipelineClient _client;

    public TrainingExportService(VisionPipelineClient client)
    {
        _client = client;
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

        var exportSamples = new List<TrainingExportSample>();
        foreach (var sample in samples)
        {
            if (string.IsNullOrWhiteSpace(sample.ExtractedFramePath) || !File.Exists(sample.ExtractedFramePath))
                continue;

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
            return new TrainingExportResult(false, "Keine gültigen Bilder gefunden.", 0, 0, 0);

        try
        {
            var request = new TrainingExportRequestDto(exportSamples, outputDir, trainSplit);
            var response = await _client.ExportTrainingAsync(request, ct).ConfigureAwait(false);

            return new TrainingExportResult(
                true, null,
                response.TotalSamples,
                response.TrainCount,
                response.ValCount);
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
    int ValCount
);
