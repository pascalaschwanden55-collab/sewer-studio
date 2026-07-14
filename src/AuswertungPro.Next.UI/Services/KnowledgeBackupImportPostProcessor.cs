using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Backup;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Passt die importierten, rechnerabhaengigen Pfade an den aktuellen PC an.
/// Fehler werden bewusst weitergegeben, damit der Import alles zurueckrollen kann.
/// </summary>
internal static class KnowledgeBackupImportPostProcessor
{
    public static async Task ApplyAsync(
        KnowledgeBackupLocations locations,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        progress?.Report("Passe Frame-Pfade an lokale Struktur an...");
        await RemapFramePathsAsync(locations, ct).ConfigureAwait(false);

        CopyTrainingCenterStateToAppData(locations);

        progress?.Report("Passe Lehrer-Annotationspfade an...");
        RemapTeacherAnnotationPaths(locations);
    }

    private static async Task RemapFramePathsAsync(
        KnowledgeBackupLocations locations,
        CancellationToken ct)
    {
        try
        {
            var samplesPath = Path.Combine(locations.KnowledgeRoot, "training_samples.json");
            if (!File.Exists(samplesPath))
                return;

            var json = await File.ReadAllTextAsync(samplesPath, ct).ConfigureAwait(false);
            var samples = JsonSerializer.Deserialize<List<TrainingSample>>(json);
            if (samples is null || samples.Count == 0)
                return;

            var localFramesDir = Path.Combine(locations.KnowledgeRoot, "frames");
            var changed = false;
            foreach (var sample in samples)
            {
                if (!string.IsNullOrEmpty(sample.FramePath))
                {
                    var remapped = FramePathRemapper.RemapFramePath(
                        sample.FramePath,
                        localFramesDir,
                        File.Exists);
                    if (remapped is not null)
                    {
                        sample.FramePath = remapped;
                        changed = true;
                    }
                }

                if (sample.AdditionalFramePaths is not { Count: > 0 })
                    continue;

                for (var index = 0; index < sample.AdditionalFramePaths.Count; index++)
                {
                    var remapped = FramePathRemapper.RemapFramePath(
                        sample.AdditionalFramePaths[index],
                        localFramesDir,
                        File.Exists);
                    if (remapped is null)
                        continue;

                    sample.AdditionalFramePaths[index] = remapped;
                    changed = true;
                }
            }

            if (!changed)
                return;

            var newJson = JsonSerializer.Serialize(
                samples,
                new JsonSerializerOptions { WriteIndented = true });
            await AtomicTextFileWriter.WriteAllTextAsync(samplesPath, newJson, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new IOException("Frame-Pfade konnten nicht sicher gespeichert werden.", ex);
        }
    }

    private static void CopyTrainingCenterStateToAppData(KnowledgeBackupLocations locations)
    {
        try
        {
            var importedPath = Path.Combine(locations.KnowledgeRoot, "training_center.json");
            if (!File.Exists(importedPath))
                return;

            var targetDirectory = Path.GetDirectoryName(locations.TrainingCenterStatePath);
            if (targetDirectory is not null)
                Directory.CreateDirectory(targetDirectory);

            AtomicTextFileWriter.WriteAllText(
                locations.TrainingCenterStatePath,
                File.ReadAllText(importedPath));
            System.Diagnostics.Trace.WriteLine(
                $"[KnowledgeBackup] training_center.json -> {locations.TrainingCenterStatePath}");
        }
        catch (Exception ex)
        {
            throw new IOException("Training-Center-Stand konnte nicht sicher gespeichert werden.", ex);
        }
    }

    private static void RemapTeacherAnnotationPaths(KnowledgeBackupLocations locations)
    {
        try
        {
            var annotationsPath = Path.Combine(locations.KnowledgeRoot, "teacher_annotations.json");
            if (!File.Exists(annotationsPath))
                return;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            var annotations = JsonSerializer.Deserialize<List<TeacherAnnotation>>(
                File.ReadAllText(annotationsPath),
                options);
            if (annotations is null || annotations.Count == 0)
                return;

            var localImagesDir = Path.Combine(locations.KnowledgeRoot, "teacher_images");
            var localLabelsDir = Path.Combine(locations.KnowledgeRoot, "teacher_labels");
            var changed = false;
            foreach (var annotation in annotations)
            {
                var fullFramePath = FramePathRemapper.RemapPathToLocal(
                    annotation.FullFramePath,
                    localImagesDir,
                    File.Exists);
                if (fullFramePath is not null)
                {
                    annotation.FullFramePath = fullFramePath;
                    changed = true;
                }

                var croppedRegionPath = FramePathRemapper.RemapPathToLocal(
                    annotation.CroppedRegionPath,
                    localImagesDir,
                    File.Exists);
                if (croppedRegionPath is not null)
                {
                    annotation.CroppedRegionPath = croppedRegionPath;
                    changed = true;
                }

                var yoloAnnotationPath = FramePathRemapper.RemapPathToLocal(
                    annotation.YoloAnnotationPath,
                    localLabelsDir,
                    File.Exists);
                if (yoloAnnotationPath is not null)
                {
                    annotation.YoloAnnotationPath = yoloAnnotationPath;
                    changed = true;
                }
            }

            if (!changed)
                return;

            AtomicTextFileWriter.WriteAllText(
                annotationsPath,
                JsonSerializer.Serialize(annotations, options));
            System.Diagnostics.Trace.WriteLine(
                $"[KnowledgeBackup] Teacher-Annotationen remapped: {annotations.Count} Eintraege");
        }
        catch (Exception ex)
        {
            throw new IOException("Lehrer-Annotationen konnten nicht sicher gespeichert werden.", ex);
        }
    }
}
