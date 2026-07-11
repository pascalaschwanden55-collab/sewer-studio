using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingYoloSidecarExportPayloadRequest(
    IReadOnlyList<TrainingSample> ApprovedSamples,
    string OutputDir,
    double TrainSplit,
    IReadOnlySet<string> EvalImageHashes,
    IReadOnlySet<string> EvalHaltungKeys,
    Action<int> SetProgressMax,
    Action<int> SetProgressValue,
    Action<string> SetStatusText,
    CancellationToken CancellationToken);

public sealed record TrainingYoloSidecarExportPayloadResult(
    TrainingExportRequestDto ExportRequest,
    int SkipEvalHash,
    int SkipEvalCase,
    int SkipNoBox);

public static class TrainingYoloSidecarExportPayloadWorkflow
{
    public static async Task<TrainingYoloSidecarExportPayloadResult> BuildAsync(
        TrainingYoloSidecarExportPayloadRequest request)
    {
        var ct = request.CancellationToken;
        request.SetProgressMax(request.ApprovedSamples.Count);
        request.SetProgressValue(0);

        var skipEvalHash = 0;
        var skipEvalCase = 0;
        var skipNoBox = 0;
        var exportSamples = new List<TrainingExportSample>();

        for (var i = 0; i < request.ApprovedSamples.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var sample = request.ApprovedSamples[i];
            request.SetProgressValue(i + 1);
            request.SetStatusText($"YOLO-Export: Lade Frame {i + 1}/{request.ApprovedSamples.Count}...");

            switch (EvalContaminationGuard.ClassifyForExport(
                        request.EvalImageHashes,
                        request.EvalHaltungKeys,
                        sample.FramePath,
                        sample.CaseId))
            {
                case EvalContaminationGuard.ExportContaminationResult.EvalImageHash:
                    skipEvalHash++;
                    continue;
                case EvalContaminationGuard.ExportContaminationResult.EvalHaltung:
                    skipEvalCase++;
                    continue;
            }

            if (string.IsNullOrWhiteSpace(sample.Code) || !sample.HasBbox)
            {
                skipNoBox++;
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(sample.FramePath, ct).ConfigureAwait(false);
            var labels = new List<TrainingExportSampleLabel>
            {
                new(
                    sample.Code,
                    sample.BboxXCenter!.Value,
                    sample.BboxYCenter!.Value,
                    sample.BboxWidth!.Value,
                    sample.BboxHeight!.Value)
            };
            exportSamples.Add(new TrainingExportSample(Convert.ToBase64String(bytes), labels));
        }

        return new TrainingYoloSidecarExportPayloadResult(
            new TrainingExportRequestDto(exportSamples, request.OutputDir, request.TrainSplit),
            skipEvalHash,
            skipEvalCase,
            skipNoBox);
    }
}
