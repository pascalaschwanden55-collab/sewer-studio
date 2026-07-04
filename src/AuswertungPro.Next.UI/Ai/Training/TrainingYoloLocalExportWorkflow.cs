using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingYoloLocalExportWorkflowRequest(
    IReadOnlyList<TrainingSample> ApprovedSamples,
    string OutputDir,
    IReadOnlySet<string> EvalImageHashes,
    IReadOnlySet<string> EvalHaltungKeys,
    Func<Task<IReadOnlyList<TeacherAnnotation>>> LoadAnnotationsAsync,
    Func<Task> PersistSamplesAsync,
    Action<string> Log,
    Action<int> SetProgressMax,
    Action<int> SetProgressValue,
    Action<string> SetStatusText,
    Func<string, int> GetClassId,
    Func<IReadOnlyDictionary<string, int>> GetFullClassMap,
    Func<string, Task> ExportClassesTxtAsync,
    Func<DateTime> UtcNow,
    CancellationToken CancellationToken);

public static class TrainingYoloLocalExportRequestFactory
{
    public static TrainingYoloLocalExportWorkflowRequest CreateWithDefaults(
        IReadOnlyList<TrainingSample> approvedSamples,
        string outputDir,
        IReadOnlySet<string> evalImageHashes,
        IReadOnlySet<string> evalHaltungKeys,
        Func<Task> persistSamplesAsync,
        Action<string> log,
        Action<int> setProgressMax,
        Action<int> setProgressValue,
        Action<string> setStatusText,
        CancellationToken cancellationToken)
        => new(
            ApprovedSamples: approvedSamples,
            OutputDir: outputDir,
            EvalImageHashes: evalImageHashes,
            EvalHaltungKeys: evalHaltungKeys,
            LoadAnnotationsAsync: async () => await TeacherAnnotationStore.LoadAsync().ConfigureAwait(false),
            PersistSamplesAsync: persistSamplesAsync,
            Log: log,
            SetProgressMax: setProgressMax,
            SetProgressValue: setProgressValue,
            SetStatusText: setStatusText,
            GetClassId: VsaYoloClassMap.GetClassId,
            GetFullClassMap: VsaYoloClassMap.GetFullMap,
            ExportClassesTxtAsync: VsaYoloClassMap.ExportClassesTxtAsync,
            UtcNow: () => DateTime.UtcNow,
            CancellationToken: cancellationToken);
}

public static class TrainingYoloLocalExportWorkflow
{
    public static async Task RunAsync(TrainingYoloLocalExportWorkflowRequest request)
    {
        var ct = request.CancellationToken;
        var annotations = await request.LoadAnnotationsAsync().ConfigureAwait(false);
        var annotationsWithImages = annotations
            .Where(a => !string.IsNullOrWhiteSpace(a.FullFramePath) && File.Exists(a.FullFramePath))
            .ToList();

        request.Log($"YOLO-Export: {annotationsWithImages.Count} TeacherAnnotations mit Bildern, {request.ApprovedSamples.Count} TrainingSamples");

        var imgTrain = Path.Combine(request.OutputDir, "images", "train");
        var imgVal = Path.Combine(request.OutputDir, "images", "val");
        var lblTrain = Path.Combine(request.OutputDir, "labels", "train");
        var lblVal = Path.Combine(request.OutputDir, "labels", "val");
        foreach (var directory in new[] { imgTrain, imgVal, lblTrain, lblVal })
            Directory.CreateDirectory(directory);

        var totalExported = 0;
        var skipEvalHash = 0;
        var skipEvalCase = 0;

        if (annotationsWithImages.Count > 0)
        {
            var splitIndex = (int)(annotationsWithImages.Count * 0.8);
            request.SetProgressMax(annotationsWithImages.Count);

            for (var i = 0; i < annotationsWithImages.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var annotation = annotationsWithImages[i];
                request.SetProgressValue(i + 1);
                request.SetStatusText($"YOLO-Export (Teacher): {i + 1}/{annotationsWithImages.Count}...");

                switch (EvalContaminationGuard.ClassifyForExport(
                            request.EvalImageHashes,
                            request.EvalHaltungKeys,
                            annotation.FullFramePath,
                            annotation.HaltungName))
                {
                    case EvalContaminationGuard.ExportContaminationResult.EvalImageHash:
                        skipEvalHash++;
                        continue;
                    case EvalContaminationGuard.ExportContaminationResult.EvalHaltung:
                        skipEvalCase++;
                        continue;
                }

                var isTrain = i < splitIndex;
                var imageDirectory = isTrain ? imgTrain : imgVal;
                var labelDirectory = isTrain ? lblTrain : lblVal;
                var extension = Path.GetExtension(annotation.FullFramePath);
                var imageDestination = Path.Combine(imageDirectory, $"teacher_{annotation.AnnotationId}{extension}");
                File.Copy(annotation.FullFramePath!, imageDestination, overwrite: true);

                var classId = request.GetClassId(annotation.VsaCode);
                var labelPath = Path.Combine(labelDirectory, $"teacher_{annotation.AnnotationId}.txt");
                var box = annotation.BoundingBox;
                var labelText = box is not null && box.Width > 0 && box.Height > 0
                    ? $"{classId} {box.XCenter:F6} {box.YCenter:F6} {box.Width:F6} {box.Height:F6}"
                    : $"{classId} 0.500000 0.500000 1.000000 1.000000";

                await File.WriteAllTextAsync(labelPath, labelText, ct).ConfigureAwait(false);
                totalExported++;
            }
        }

        if (request.ApprovedSamples.Count > 0)
        {
            var samplesWithBox = request.ApprovedSamples.Count(s => s.HasBbox);
            request.Log($"  Exportiere {samplesWithBox} TrainingSamples mit echter Box (von {request.ApprovedSamples.Count}; {request.ApprovedSamples.Count - samplesWithBox} ohne Box uebersprungen)");
            var splitIndex = (int)(request.ApprovedSamples.Count * 0.8);

            for (var i = 0; i < request.ApprovedSamples.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var sample = request.ApprovedSamples[i];
                request.SetStatusText($"YOLO-Export (Samples): {i + 1}/{request.ApprovedSamples.Count}...");

                if (!File.Exists(sample.FramePath) || !sample.HasBbox)
                    continue;

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

                var isTrain = i < splitIndex;
                var imageDirectory = isTrain ? imgTrain : imgVal;
                var labelDirectory = isTrain ? lblTrain : lblVal;
                var extension = Path.GetExtension(sample.FramePath);
                var imageDestination = Path.Combine(imageDirectory, $"sample_{i:D6}{extension}");
                try
                {
                    File.Copy(sample.FramePath, imageDestination, overwrite: true);
                }
                catch (IOException)
                {
                    continue;
                }

                var classId = request.GetClassId(sample.Code);
                var labelPath = Path.Combine(labelDirectory, $"sample_{i:D6}.txt");
                await File.WriteAllTextAsync(
                    labelPath,
                    $"{classId} {sample.BboxXCenter!.Value:F6} {sample.BboxYCenter!.Value:F6} {sample.BboxWidth!.Value:F6} {sample.BboxHeight!.Value:F6}",
                    ct).ConfigureAwait(false);

                sample.ExportedUtc = request.UtcNow();
                totalExported++;
            }

            await request.PersistSamplesAsync().ConfigureAwait(false);
        }

        var sortedClasses = request.GetFullClassMap()
            .OrderBy(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();
        var yamlPath = Path.Combine(request.OutputDir, "data.yaml");
        var yamlLines = new[]
        {
            $"path: {Path.GetFullPath(request.OutputDir)}",
            "train: images/train",
            "val: images/val",
            $"nc: {sortedClasses.Count}",
            $"names: [{string.Join(", ", sortedClasses.Select(c => $"'{c}'"))}]"
        };
        await File.WriteAllLinesAsync(yamlPath, yamlLines, ct).ConfigureAwait(false);

        await request.ExportClassesTxtAsync(Path.Combine(request.OutputDir, "classes.txt")).ConfigureAwait(false);

        if (skipEvalHash + skipEvalCase > 0)
            request.Log($"  Eval-Schutz: {skipEvalHash} per Hash, {skipEvalCase} per Haltung uebersprungen.");

        var message = $"YOLO-Export fertig: {totalExported} Samples " +
                      $"({annotationsWithImages.Count} Teacher + {totalExported - annotationsWithImages.Count} Samples), " +
                      $"{sortedClasses.Count} Klassen \u2192 {request.OutputDir}";
        request.Log(message);
        request.Log($"  data.yaml: {yamlPath}");
        request.Log($"  Klassen: {string.Join(", ", sortedClasses)}");
        request.SetStatusText(message);
    }
}
